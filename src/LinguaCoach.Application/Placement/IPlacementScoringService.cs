namespace LinguaCoach.Application.Placement;

public sealed record PlacementScoreResult(
    bool IsCorrect,
    double Score,
    string EvaluationNotes);

/// <summary>
/// Deterministic scoring for placement assessment items.
/// No AI, no LLM — reproducible for any given input.
/// </summary>
public interface IPlacementScoringService
{
    PlacementScoreResult Score(string candidateResponse, string? correctAnswer, string itemType);
}
