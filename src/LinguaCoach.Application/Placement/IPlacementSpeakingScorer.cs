using System.Text.Json;

namespace LinguaCoach.Application.Placement;

/// <summary>
/// Scores a placement item whose scoring rules include a "speaking" component — routes the
/// submitted audio storage-key reference to ISpeakingEvaluationProvider instead of the
/// deterministic IPlacementScoringService. Kept separate from IPlacementScoringService because
/// AI evaluation is async and can fail/degrade, unlike the deterministic comparisons.
/// </summary>
public interface IPlacementSpeakingScorer
{
    bool CanScore(ScoringRulesDocument scoringDoc);

    Task<PlacementScoreResult> ScoreAsync(
        Guid itemId,
        Guid studentProfileId,
        string? prompt,
        string cefrLevel,
        ScoringRulesDocument scoringDoc,
        IReadOnlyDictionary<string, JsonElement> submissionData,
        CancellationToken ct = default);
}
