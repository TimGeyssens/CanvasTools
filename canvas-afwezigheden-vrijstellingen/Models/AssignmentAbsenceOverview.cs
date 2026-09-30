namespace CanvasAfwezighedenVrijstellingen.Models;

public class AssignmentAbsenceOverview
{
    public string AssignmentName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public List<AbsentStudent> AbsentStudents { get; set; } = new();
}

public class AbsentStudent
{
    public string StudentName { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
