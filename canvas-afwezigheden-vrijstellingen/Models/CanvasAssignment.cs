namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasAssignment
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }

    // Inleverdatum is the class date we match against absence dates.
    // By default it's set once per assignment, but it can optionally be configured per class/section.
    public DateTime? Inleverdatum { get; set; }

    public bool UseInleverdatumBySection { get; set; } = false;

    // Key: section name as shown in Canvas ("class"). Value: inleverdatum for that class.
    public Dictionary<string, DateTime?> InleverdatumBySection { get; set; } = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);

    public long CourseId { get; set; }
}
