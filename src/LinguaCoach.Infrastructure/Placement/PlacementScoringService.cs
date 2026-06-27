using LinguaCoach.Application.Placement;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Deterministic placement scorer. No AI, no LLM, fully reproducible.
/// Compares candidate response to stored correct answer (case-insensitive trim).
/// </summary>
public sealed class PlacementScoringService : IPlacementScoringService
{
    public PlacementScoreResult Score(string candidateResponse, string? correctAnswer, string itemType)
    {
        if (string.IsNullOrWhiteSpace(candidateResponse))
            return new PlacementScoreResult(false, 0.0, "Empty response received.");

        if (string.IsNullOrWhiteSpace(correctAnswer))
            return new PlacementScoreResult(false, 0.0, "No correct answer defined for this item.");

        var given = candidateResponse.Trim();
        var expected = correctAnswer.Trim();
        var isCorrect = string.Equals(given, expected, StringComparison.OrdinalIgnoreCase);
        var score = isCorrect ? 1.0 : 0.0;
        var notes = isCorrect
            ? $"Correct. Expected: '{expected}'."
            : $"Incorrect. Expected: '{expected}'. Received: '{given}'.";

        return new PlacementScoreResult(isCorrect, score, notes);
    }
}
