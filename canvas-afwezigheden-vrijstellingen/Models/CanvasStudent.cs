namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasStudent
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Canvas section name ("class") within the course. A student can be in multiple sections.
    public string SectionName { get; set; } = string.Empty;
}
