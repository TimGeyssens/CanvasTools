using CanvasBamaflexExport.Models;
using Newtonsoft.Json;

namespace CanvasBamaflexExport.Services;

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
    public async Task<List<BamaFlexGrade>> GetFinalGrades(long courseId)
    {
        // 1) Fetch enrollments (authoritative for current_score/final_score)
        var enrollmentGradesByUserId = new Dictionary<long, CanvasEnrollmentGrades>();
        var perPage = 100;

        string? nextEnrollmentsUrl = $"{ApiRoot}/courses/{courseId}/enrollments?type[]=StudentEnrollment&state[]=active&include[]=user&per_page={perPage}";
        var seenEnrollments = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextEnrollmentsUrl))
        {
            if (!seenEnrollments.Add(nextEnrollmentsUrl))
                break;

            var response = await _httpClient.GetAsync(nextEnrollmentsUrl);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch enrollments/grades: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var enrollments = JsonConvert.DeserializeObject<List<CanvasEnrollment>>(content) ?? new List<CanvasEnrollment>();

            foreach (var e in enrollments)
            {
                if (e.UserId == 0 || e.Grades == null)
                    continue;

                enrollmentGradesByUserId[e.UserId] = e.Grades;
            }

            nextEnrollmentsUrl = GetNextLink(response);
        }

        // 2) Fetch course users (for student id + email)
        var users = new List<Dictionary<string, object>>();
        string? nextUsersUrl = $"{ApiRoot}/courses/{courseId}/users?enrollment_type[]=student&enrollment_state[]=active&include[]=email&per_page={perPage}";
        var seenUsers = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextUsersUrl))
        {
            if (!seenUsers.Add(nextUsersUrl))
                break;

            var response = await _httpClient.GetAsync(nextUsersUrl);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch students: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var page = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(content) ?? new List<Dictionary<string, object>>();
            users.AddRange(page);

            nextUsersUrl = GetNextLink(response);
        }

        // 3) Combine into BamaFlex export rows
        var grades = new List<BamaFlexGrade>();

        foreach (var user in users)
        {
            var name = user.ContainsKey("name") ? user["name"]?.ToString() : string.Empty;
            var email = user.ContainsKey("email") ? user["email"]?.ToString() : string.Empty;
            var loginId = user.ContainsKey("login_id") ? user["login_id"]?.ToString() : string.Empty;
            var sisUserId = user.ContainsKey("sis_user_id") ? user["sis_user_id"]?.ToString() : string.Empty;

            long.TryParse(user.ContainsKey("id") ? user["id"]?.ToString() : null, out var canvasUserId);

            // Prefer email/login prefix since our other imports use that.
            string? studentId = null;
            if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            {
                studentId = email.Split('@')[0];
            }
            else if (!string.IsNullOrWhiteSpace(loginId) && loginId.Contains('@'))
            {
                studentId = loginId.Split('@')[0];
            }
            else if (!string.IsNullOrWhiteSpace(sisUserId))
            {
                studentId = sisUserId;
            }
            else if (!string.IsNullOrWhiteSpace(loginId))
            {
                studentId = loginId;
            }
            else
            {
                studentId = canvasUserId != 0 ? canvasUserId.ToString() : string.Empty;
            }

            enrollmentGradesByUserId.TryGetValue(canvasUserId, out var enrollmentGrades);

            decimal? score = enrollmentGrades?.FinalScore ?? enrollmentGrades?.CurrentScore;
            if (score.HasValue)
                score = CanvasStudentScore.ToTwentyPointScale(score.Value);

            var letter = enrollmentGrades?.FinalGrade ?? enrollmentGrades?.CurrentGrade;

            grades.Add(new BamaFlexGrade
            {
                StudentId = studentId ?? string.Empty,
                StudentName = name ?? string.Empty,
                Email = (!string.IsNullOrWhiteSpace(email) ? email : (loginId ?? string.Empty)),
                FinalGrade = score,
                LetterGrade = letter
            });
        }

        return grades;
    }
}
