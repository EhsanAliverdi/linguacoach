using System.Text.Json;

namespace LinguaCoach.Application.Placement;

/// <summary>Per-component scoring outcome — proves every component is scored independently,
/// not just the first (a group item can have several answer components).</summary>
public sealed record ComponentScoreResult(
    string ComponentKey,
    string? NormalizedValue,
    bool IsCorrect,
    double PointsEarned,
    double MaxPoints);

public sealed record PlacementScoreResult(
    bool IsCorrect,
    double Score,
    string EvaluationNotes,
    IReadOnlyList<ComponentScoreResult> Components);

/// <summary>
/// Deterministic scoring for Form.io-native placement items.
/// No AI, no LLM — reproducible for any given input. Scores every component declared in the
/// scoring rules document independently and aggregates into an overall score/correctness.
/// </summary>
public interface IPlacementScoringService
{
    PlacementScoreResult ScoreSubmission(string? scoringRulesJson, IReadOnlyDictionary<string, JsonElement> submissionData);
}
