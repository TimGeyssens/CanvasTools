using CanvasAfwezighedenVrijstellingen.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CanvasAfwezighedenVrijstellingen.Services;

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
    public async Task<List<CanvasAssignment>> GetAssignmentsForCourse(long courseId)
    {
        var allAssignments = new List<CanvasAssignment>();
        var perPage = 100;

        string? nextUrl = $"{ApiRoot}/courses/{courseId}/assignments?per_page={perPage}";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            if (!seen.Add(nextUrl))
                break;

            var response = await _httpClient.GetAsync(nextUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch assignments: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var assignments = JsonConvert.DeserializeObject<List<CanvasAssignment>>(content) ?? new List<CanvasAssignment>();
            allAssignments.AddRange(assignments);

            nextUrl = GetNextLink(response);
        }
        
        return allAssignments;
    }
    public async Task<List<CanvasStudent>> GetStudentsForCourse(long courseId)
    {
        var allStudents = new List<CanvasStudent>();
        var perPage = 100;

        // Fetch sections once so we can map course_section_id -> name.
        var sectionsById = new Dictionary<long, string>();
        string? nextSectionsUrl = $"{ApiRoot}/courses/{courseId}/sections?per_page={perPage}";
        var seenSections = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextSectionsUrl))
        {
            if (!seenSections.Add(nextSectionsUrl))
                break;

            var response = await _httpClient.GetAsync(nextSectionsUrl);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch sections: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var sections = JsonConvert.DeserializeObject<List<CanvasSection>>(content) ?? new List<CanvasSection>();

            foreach (var s in sections)
            {
                if (s.Id != 0 && !string.IsNullOrWhiteSpace(s.Name))
                    sectionsById[s.Id] = s.Name;
            }

            nextSectionsUrl = GetNextLink(response);
        }

        // Include enrollments so we can infer each student's section.
        string? nextUrl = $"{ApiRoot}/courses/{courseId}/users?enrollment_type[]=student&enrollment_state[]=active&include[]=email&include[]=enrollments&per_page={perPage}";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            if (!seen.Add(nextUrl))
                break;

            var response = await _httpClient.GetAsync(nextUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to fetch students: {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var students = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(content) ?? new List<Dictionary<string, object>>();

            foreach (var student in students)
            {
                var name = student.ContainsKey("name") ? student["name"]?.ToString() : string.Empty;
                var email = student.ContainsKey("email") ? student["email"]?.ToString() : string.Empty;
                var loginId = student.ContainsKey("login_id") ? student["login_id"]?.ToString() : string.Empty;
                var sisUserId = student.ContainsKey("sis_user_id") ? student["sis_user_id"]?.ToString() : string.Empty;

                // Prefer email/login prefix since both absences Excel + Forms attendance exports use that.
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
                else if (student.ContainsKey("id"))
                {
                    studentId = student["id"]?.ToString();
                }

                var sectionNames = new List<string>();
                if (student.ContainsKey("enrollments") && student["enrollments"] is JArray enrollments)
                {
                    foreach (var e in enrollments)
                    {
                        // The enrollments array may include non-student enrollments; filter defensively.
                        var type = e?["type"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(type) && !type.Contains("Student", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var sectionName = e?["course_section_name"]?.ToString();

                        if (string.IsNullOrWhiteSpace(sectionName))
                        {
                            if (long.TryParse(e?["course_section_id"]?.ToString(), out var sectionId)
                                && sectionsById.TryGetValue(sectionId, out var mappedName))
                            {
                                sectionName = mappedName;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(sectionName))
                            sectionNames.Add(sectionName);
                    }
                }

                var sectionDisplay = string.Join(", ", sectionNames
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.Ordinal));

                allStudents.Add(new CanvasStudent
                {
                    StudentId = studentId ?? string.Empty,
                    StudentName = name ?? string.Empty,
                    Email = (!string.IsNullOrWhiteSpace(email) ? email : (loginId ?? string.Empty)),
                    SectionName = sectionDisplay
                });
            }

            nextUrl = GetNextLink(response);
        }

        return allStudents;
    }
    public async Task<string?> GetStudentCanvasId(long courseId, string studentSisId)
    {
        // Safety: never search with empty term (could match arbitrary users)
        if (string.IsNullOrWhiteSpace(studentSisId))
            return null;

        var url = $"{ApiRoot}/courses/{courseId}/users?search_term={Uri.EscapeDataString(studentSisId)}";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(content);
        
        if (users != null && users.Count > 0)
        {
            return users[0]["id"].ToString();
        }

        return null;
    }
    public async Task<bool> SetAssignmentExemption(long courseId, long assignmentId, string studentCanvasId, bool simulate = false)
    {
        // In simulation mode, just return success without making the actual API call
        if (simulate)
        {
            return true;
        }

        var url = $"{ApiRoot}/courses/{courseId}/assignments/{assignmentId}/submissions/{studentCanvasId}";
        
        var payload = new
        {
            submission = new
            {
                excuse = true
            }
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PutAsync(url, content);
        
        return response.IsSuccessStatusCode;
    }
    public async Task<bool> SetAssignmentGradeToZero(long courseId, long assignmentId, string studentCanvasId, bool simulate = false)
    {
        // In simulation mode, just return success without making the actual API call
        if (simulate)
        {
            return true;
        }

        var url = $"{ApiRoot}/courses/{courseId}/assignments/{assignmentId}/submissions/{studentCanvasId}";

        // Canvas Submissions API supports submission[posted_grade]=0
        // We also explicitly set excuse=false so the 0 is not ignored for an excused submission.
        var payload = new
        {
            submission = new
            {
                posted_grade = "0",
                excuse = false
            }
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(url, content);

        return response.IsSuccessStatusCode;
    }
    private static IEnumerable<string> SplitSectionNames(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
            return Array.Empty<string>();

        return sectionName
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }
    private static DateTime? GetMatchingInleverdatum(CanvasAssignment assignment, DateTime absenceDate, string? sectionName)
    {
        // 1) Per-section override (if enabled)
        if (assignment.UseInleverdatumBySection && assignment.InleverdatumBySection != null)
        {
            foreach (var s in SplitSectionNames(sectionName))
            {
                if (assignment.InleverdatumBySection.TryGetValue(s, out var d) && d.HasValue && d.Value.Date == absenceDate.Date)
                    return d.Value;
            }

            // If per-section is enabled but nothing matches, optionally fall back to the global date.
            if (assignment.Inleverdatum.HasValue && assignment.Inleverdatum.Value.Date == absenceDate.Date)
                return assignment.Inleverdatum.Value;

            return null;
        }

        // 2) Global assignment date
        if (assignment.Inleverdatum.HasValue && assignment.Inleverdatum.Value.Date == absenceDate.Date)
            return assignment.Inleverdatum.Value;

        return null;
    }
    public async Task<List<ExemptionResult>> ProcessExemptions(long courseId, List<Absence> absences, List<CanvasAssignment> assignments, bool simulate = false)
    {
        if (assignments == null)
            throw new ArgumentNullException(nameof(assignments));

        var results = new List<ExemptionResult>();

        foreach (var absence in absences)
        {
            var studentCanvasId = await GetStudentCanvasId(courseId, absence.StudentId);
            
            if (studentCanvasId == null)
            {
                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = "N/A",
                    DueDate = absence.AbsenceDate,
                    Success = false,
                    Message = "Student not found in Canvas course"
                });
                continue;
            }

            // Find assignments with Inleverdatum matching the absence date (globally or for this student's section)
            var matchingAssignments = assignments
                .Select(a => new { Assignment = a, Match = GetMatchingInleverdatum(a, absence.AbsenceDate, absence.SectionName) })
                .Where(x => x.Match.HasValue)
                .ToList();

            if (!matchingAssignments.Any())
            {
                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = "N/A",
                    DueDate = absence.AbsenceDate,
                    Success = false,
                    Message = string.IsNullOrWhiteSpace(absence.SectionName)
                        ? "No assignments with Inleverdatum on this date"
                        : "No assignments with Inleverdatum on this date for the student's class"
                });
                continue;
            }

            foreach (var match in matchingAssignments)
            {
                var assignment = match.Assignment;
                var inleverdatum = match.Match!.Value;

                var success = await SetAssignmentExemption(courseId, assignment.Id, studentCanvasId, simulate);
                
                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = assignment.Name,
                    DueDate = inleverdatum,
                    Success = success,
                    Message = simulate 
                        ? "[SIMULATION] Would set exemption" 
                        : (success ? "Exemption set successfully" : "Failed to set exemption")
                });
            }
        }

        return results;
    }
    public async Task<List<ExemptionResult>> ProcessZeros(long courseId, List<UnjustifiedAbsence> unjustifiedAbsences, List<CanvasAssignment> assignments, bool simulate = false)
    {
        if (unjustifiedAbsences == null)
            throw new ArgumentNullException(nameof(unjustifiedAbsences));
        if (assignments == null)
            throw new ArgumentNullException(nameof(assignments));

        var results = new List<ExemptionResult>();

        // De-duplicate (in case multiple attendance exports contain the same absence)
        var uniqueAbsences = unjustifiedAbsences
            .GroupBy(a => new { StudentId = a.StudentId?.Trim().ToLowerInvariant() ?? string.Empty, Date = a.Date.Date })
            .Select(g => g.First())
            .ToList();

        foreach (var absence in uniqueAbsences)
        {
            if (string.IsNullOrWhiteSpace(absence.StudentId))
            {
                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = "N/A",
                    DueDate = absence.Date,
                    Success = false,
                    Message = "Student ID missing in attendance data (cannot safely update Canvas)"
                });
                continue;
            }

            var studentCanvasId = await GetStudentCanvasId(courseId, absence.StudentId);

            if (studentCanvasId == null)
            {
                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = "N/A",
                    DueDate = absence.Date,
                    Success = false,
                    Message = "Student not found in Canvas course"
                });
                continue;
            }

            // Find assignments with Inleverdatum matching the absence date (globally or for this student's section)
            var matchingAssignments = assignments
                .Select(a => new { Assignment = a, Match = GetMatchingInleverdatum(a, absence.Date, absence.SectionName) })
                .Where(x => x.Match.HasValue)
                .ToList();

            if (!matchingAssignments.Any())
            {
                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = "N/A",
                    DueDate = absence.Date,
                    Success = false,
                    Message = string.IsNullOrWhiteSpace(absence.SectionName)
                        ? "No assignments with Inleverdatum on this date"
                        : "No assignments with Inleverdatum on this date for the student's class"
                });
                continue;
            }

            foreach (var match in matchingAssignments)
            {
                var assignment = match.Assignment;
                var inleverdatum = match.Match!.Value;

                var success = await SetAssignmentGradeToZero(courseId, assignment.Id, studentCanvasId, simulate);

                results.Add(new ExemptionResult
                {
                    StudentName = absence.StudentName,
                    AssignmentName = assignment.Name,
                    DueDate = inleverdatum,
                    Success = success,
                    Message = simulate
                        ? "[SIMULATION] Would set posted grade to 0"
                        : (success ? "Posted grade set to 0" : "Failed to set posted grade to 0")
                });
            }
        }

        return results;
    }
}
