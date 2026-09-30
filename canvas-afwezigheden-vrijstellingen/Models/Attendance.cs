namespace CanvasAfwezighedenVrijstellingen.Models;

public class Attendance
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool Present { get; set; }
}

public class UnjustifiedAbsence
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    // Filled in from Canvas roster (when inferred from roster-based absence detection)
    public string? SectionName { get; set; }
}
