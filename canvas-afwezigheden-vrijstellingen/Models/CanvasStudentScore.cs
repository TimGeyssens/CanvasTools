namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasStudentScore
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    public decimal? CurrentScore { get; set; }
    public decimal? FinalScore { get; set; }

    // Canvas "current_score"/"final_score" are commonly percent (0-100). Many of our courses report final
    // grades on a 20-point scale, so convert percentages to /20 for display/export.
    public static decimal ToTwentyPointScale(decimal score)
    {
        // Heuristic: if it looks like a percentage, convert to /20.
        return score > 20m ? score / 5m : score;
    }

    public string DisplayScore()
    {
        // Calendar overview wants the numeric score only.
        if (FinalScore.HasValue)
            return $"{ToTwentyPointScale(FinalScore.Value):0.##}";
        if (CurrentScore.HasValue)
            return $"{ToTwentyPointScale(CurrentScore.Value):0.##}";
        return "—";
    }
}
