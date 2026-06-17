namespace LinguaCoach.Domain.Services;

// Simple accumulator scoring from assessment step answers.
// Assessment steps carry cefrScoreWeight in AssessmentMetadataJson.
// Correct answer = adds weight; wrong = adds 0.
// Total correct weight / max possible weight → CEFR band.
public static class PreliminaryCefrCalculator
{
    public static string? Calculate(IReadOnlyList<AssessmentScore> scores)
    {
        if (scores.Count == 0) return null;

        var maxWeight = scores.Sum(s => s.Weight);
        if (maxWeight <= 0) return null;

        var earnedWeight = scores.Sum(s => s.IsCorrect ? s.Weight : 0);
        var pct = (double)earnedWeight / maxWeight * 100.0;

        return pct switch
        {
            <= 25 => "A1",
            <= 45 => "A2",
            <= 65 => "B1",
            <= 80 => "B2",
            <= 95 => "C1",
            _ => "C2"
        };
    }
}

public sealed record AssessmentScore(bool IsCorrect, int Weight);
