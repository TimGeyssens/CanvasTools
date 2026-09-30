namespace CanvasAfwezighedenVrijstellingen.Models;

public class ExemptionResult
{
    public string StudentName { get; set; } = string.Empty;
    public string AssignmentName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
