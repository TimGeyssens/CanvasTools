using CanvasAfspraaksloten.Models;
using Newtonsoft.Json;

namespace CanvasAfspraaksloten.Services;

public class CanvasService
{
    private readonly HttpClient _httpClient;
    private string? _apiUrl;

    public CanvasService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>True once the user has supplied an instance URL and a token via the UI.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiUrl);

    /// <summary>Instance URL currently in use, without the API suffix.</summary>
    public string InstanceUrl => _apiUrl?.Replace("/api/v1", "", StringComparison.OrdinalIgnoreCase) ?? string.Empty;

    /// <summary>
    /// Points the service at a Canvas instance. The token lives only in memory for the
    /// lifetime of this (scoped) service, which in Blazor Server means the lifetime of the
    /// browser circuit. It is never written to disk and never read from appsettings.
    /// </summary>
    public void Configure(string instanceUrl, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Canvas API token ontbreekt.", nameof(accessToken));

        _apiUrl = BuildApiRoot(instanceUrl);

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
    }

    /// <summary>Turns "https://instructure.com" into "https://instructure.com/api/v1".</summary>
    private static string BuildApiRoot(string instanceUrl)
    {
        var trimmed = (instanceUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
            throw new ArgumentException("Canvas URL ontbreekt.", nameof(instanceUrl));

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        return trimmed.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + "/api/v1";
    }

    /// <summary>Throws unless the user has connected first, so failures explain the real cause.</summary>
    private string ApiRoot => _apiUrl ?? throw new InvalidOperationException(
        "Nog niet verbonden met Canvas. Vul de Canvas-URL en een API-token in en klik op Verbinden.");

    /// <summary>Verifies the instance URL and token by fetching the current user.</summary>
    public async Task<string> TestConnectionAsync()
    {
        var response = await _httpClient.GetAsync($"{ApiRoot}/users/self");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Canvas geweigerde de aanvraag: {response.StatusCode}. Antwoord: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonConvert.DeserializeObject<CanvasUser>(content);
        return string.IsNullOrWhiteSpace(user?.Name) ? "verbonden" : user!.Name;
    }

    private static string? GetNextLink(HttpResponseMessage response)
    {
        // Canvas uses RFC5988 Link headers. Some endpoints use opaque page tokens (bookmark pagination),
        // so we must follow rel="next" instead of incrementing ?page=1,2,3...
        if (!response.Headers.TryGetValues("Link", out var values))
            return null;

        var linkHeader = string.Join(",", values);
        var parts = linkHeader.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!trimmed.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                continue;

            var start = trimmed.IndexOf('<');
            var end = trimmed.IndexOf('>');
            if (start >= 0 && end > start)
            {
                return trimmed.Substring(start + 1, end - start - 1);
            }
        }

        return null;
    }

    public async Task<List<CanvasCourse>> GetCoursesForCurrentUser()
    {
        var allCourses = new List<CanvasCourse>();
        var perPage = 100;

        // Canvas list courses endpoint
        // We bias toward active/available courses to keep the dropdown usable.
        string? nextUrl = $"{ApiRoot}/courses?per_page={perPage}&enrollment_state=active";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            if (!seen.Add(nextUrl))
                break;

            var response = await _httpClient.GetAsync(nextUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch courses: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var courses = JsonConvert.DeserializeObject<List<CanvasCourse>>(content) ?? new List<CanvasCourse>();
            allCourses.AddRange(courses);

            nextUrl = GetNextLink(response);
        }

        // Prefer available courses; if the API doesn't return workflow_state, keep everything.
        var filtered = allCourses
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Where(c => string.IsNullOrWhiteSpace(c.WorkflowState) || c.WorkflowState.Equals("available", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();

        return filtered;
    }
    public async Task<List<CanvasAppointmentGroup>> GetMyAppointmentGroupsForCourse(long courseId, bool includePastAppointments = true)
    {
        // Step 1: list manageable groups for the course (lightweight response)
        var groupSummaries = new List<CanvasAppointmentGroup>();
        var perPage = 50;

        // Many appointment groups are in the past; Canvas defaults to excluding them.
        var includePast = includePastAppointments ? "&include_past_appointments=true" : string.Empty;

        string? nextUrl = $"{ApiRoot}/appointment_groups?scope=manageable&context_codes[]=course_{courseId}&per_page={perPage}{includePast}";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            if (!seen.Add(nextUrl))
                break;

            var response = await _httpClient.GetAsync(nextUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch appointment groups: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var groups = JsonConvert.DeserializeObject<List<CanvasAppointmentGroup>>(content) ?? new List<CanvasAppointmentGroup>();
            groupSummaries.AddRange(groups);

            nextUrl = GetNextLink(response);
        }

        // Step 2: fetch group details so we actually get the appointment slots (appointments array)
        var detailed = new List<CanvasAppointmentGroup>();
        foreach (var g in groupSummaries)
        {
            // /appointment_groups/:id returns appointments; include child_events for reservations
            var detailUrl = $"{ApiRoot}/appointment_groups/{g.Id}?include[]=appointments&include[]=child_events";
            if (includePastAppointments)
                detailUrl += "&include_past_appointments=true";

            var response = await _httpClient.GetAsync(detailUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch appointment group details: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var group = JsonConvert.DeserializeObject<CanvasAppointmentGroup>(content);
            if (group != null)
                detailed.Add(group);
        }

        return detailed;
    }
    public async Task<Dictionary<long, CanvasStudentScore>> GetStudentScoresForCourse(long courseId)
    {
        var map = new Dictionary<long, CanvasStudentScore>();
        var perPage = 100;

        // include[]=user gives us the name, so we can show booked student name even when calendar events don't include it.
        string? nextUrl = $"{ApiRoot}/courses/{courseId}/enrollments?type[]=StudentEnrollment&state[]=active&include[]=user&per_page={perPage}";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            if (!seen.Add(nextUrl))
                break;

            var response = await _httpClient.GetAsync(nextUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch enrollments/grades: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var enrollments = JsonConvert.DeserializeObject<List<CanvasEnrollment>>(content) ?? new List<CanvasEnrollment>();

            foreach (var e in enrollments)
            {
                if (e.UserId == 0)
                    continue;

                var name = e.User?.Name ?? string.Empty;

                map[e.UserId] = new CanvasStudentScore
                {
                    UserId = e.UserId,
                    Name = name,
                    CurrentScore = e.Grades?.CurrentScore,
                    FinalScore = e.Grades?.FinalScore
                };
            }

            nextUrl = GetNextLink(response);
        }

        return map;
    }
}
