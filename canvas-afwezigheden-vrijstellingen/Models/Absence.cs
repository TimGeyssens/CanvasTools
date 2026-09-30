namespace CanvasAfwezighedenVrijstellingen.Models;

public class Absence
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime AbsenceDate { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Filled in (when possible) from the Canvas roster.
    public string? SectionName { get; set; }
}
