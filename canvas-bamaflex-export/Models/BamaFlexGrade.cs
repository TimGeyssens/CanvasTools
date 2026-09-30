namespace CanvasBamaflexExport.Models;

public class BamaFlexGrade
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal? FinalGrade { get; set; }
    public string? LetterGrade { get; set; }
}
