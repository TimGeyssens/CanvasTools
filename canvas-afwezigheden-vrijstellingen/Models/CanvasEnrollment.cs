using Newtonsoft.Json;

namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasEnrollment
{
    [JsonProperty("user_id")]
    public long UserId { get; set; }

    [JsonProperty("user")]
    public CanvasUser? User { get; set; }

    [JsonProperty("grades")]
    public CanvasEnrollmentGrades? Grades { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("enrollment_state")]
    public string? EnrollmentState { get; set; }
}

public class CanvasEnrollmentGrades
{
    [JsonProperty("current_score")]
    public decimal? CurrentScore { get; set; }

    [JsonProperty("final_score")]
    public decimal? FinalScore { get; set; }

    [JsonProperty("current_grade")]
    public string? CurrentGrade { get; set; }

    [JsonProperty("final_grade")]
    public string? FinalGrade { get; set; }
}
