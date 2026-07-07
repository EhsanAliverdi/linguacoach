using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Bridges a placement item's "speaking" scoring rule to the existing
/// ISpeakingEvaluationProvider/OpenAiSpeakingEvaluationProvider (the same provider the Activity
/// feature already uses) — reused as-is; AttemptId/ActivityId here are correlation metadata for
/// this placement item, not a real ActivityAttempt/Activity foreign key.
/// </summary>
public sealed class PlacementSpeakingScorer : IPlacementSpeakingScorer
{
    private readonly ISpeakingEvaluationProvider _provider;
    private readonly PlacementAssessmentOptions _opts;
    private readonly ILogger<PlacementSpeakingScorer> _logger;

    public PlacementSpeakingScorer(
        ISpeakingEvaluationProvider provider,
        IOptions<PlacementAssessmentOptions> opts,
        ILogger<PlacementSpeakingScorer> logger)
    {
        _provider = provider;
        _opts = opts.Value;
        _logger = logger;
    }

    public bool CanScore(ScoringRulesDocument scoringDoc) =>
        scoringDoc.Components.Values.Any(c => c.Kind == ScoringRuleKinds.Speaking);

    public async Task<PlacementScoreResult> ScoreAsync(
        Guid itemId,
        Guid studentProfileId,
        string? prompt,
        string cefrLevel,
        ScoringRulesDocument scoringDoc,
        IReadOnlyDictionary<string, JsonElement> submissionData,
        CancellationToken ct = default)
    {
        var entry = scoringDoc.Components.FirstOrDefault(kv => kv.Value.Kind == ScoringRuleKinds.Speaking);
        var componentKey = entry.Key ?? "answer";
        var points = entry.Value?.Points ?? 1.0;

        var storageKey = ExtractStorageKey(submissionData, componentKey);

        if (string.IsNullOrWhiteSpace(storageKey))
            return Fail(componentKey, points, "No recorded audio was submitted for this item.");

        if (!_provider.IsSupported)
            return Fail(componentKey, points, "Speaking evaluation is unavailable.");

        try
        {
            var result = await _provider.EvaluateAsync(new SpeakingEvaluationRequest(
                AttemptId: itemId,
                StudentProfileId: studentProfileId,
                ActivityId: itemId,
                AudioStorageKey: storageKey,
                ActivityPrompt: prompt,
                ActivityTitle: "Placement speaking item",
                CefrLevel: cefrLevel,
                CorrelationId: itemId.ToString()), ct);

            if (!result.Success || result.OverallScore is null)
                return Fail(componentKey, points, result.FailureReason ?? "Speaking evaluation failed.");

            // OverallScore is 0..100 (same convention as WritingEvaluation/SpeakingEvaluation
            // elsewhere in the app) — normalize to 0..1 to match PlacementScoreResult.Score.
            var score = Math.Clamp(result.OverallScore.Value / 100.0, 0.0, 1.0);
            var isCorrect = score >= _opts.SpeakingPassThreshold;
            var notes = result.FeedbackText
                ?? (isCorrect ? "Speaking response passed." : "Speaking response was below the passing threshold.");

            return new PlacementScoreResult(
                isCorrect, score, notes,
                new[]
                {
                    new ComponentScoreResult(
                        componentKey, result.Transcript, isCorrect, isCorrect ? points : 0.0, points),
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Speaking evaluation failed for placement item {ItemId}", itemId);
            return Fail(componentKey, points, "Speaking evaluation is unavailable.");
        }
    }

    private static string? ExtractStorageKey(IReadOnlyDictionary<string, JsonElement> submissionData, string componentKey)
    {
        if (!submissionData.TryGetValue(componentKey, out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        return el.TryGetProperty("storageKey", out var keyEl) && keyEl.ValueKind == JsonValueKind.String
            ? keyEl.GetString()
            : null;
    }

    private static PlacementScoreResult Fail(string componentKey, double points, string notes) =>
        new(false, 0.0, notes, new[] { new ComponentScoreResult(componentKey, null, false, 0.0, points) });
}
