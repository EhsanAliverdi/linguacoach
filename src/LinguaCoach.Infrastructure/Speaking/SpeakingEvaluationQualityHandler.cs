using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Speaking;

public sealed class SpeakingEvaluationQualityHandler : ISpeakingEvaluationQualityQuery
{
    private readonly LinguaCoachDbContext _db;
    private readonly SpeakingSignalThresholds _thresholds;

    public SpeakingEvaluationQualityHandler(
        LinguaCoachDbContext db,
        IOptions<SpeakingEvaluationOptions> options)
    {
        _db = db;
        _thresholds = SpeakingSignalThresholds.FromOptions(options.Value);
    }

    public async Task<SpeakingEvaluationQualitySummaryDto> GetQualitySummaryAsync(CancellationToken ct = default)
    {
        var evals = await _db.SpeakingEvaluations.ToListAsync(ct);

        if (evals.Count == 0)
            return Empty();

        var total       = evals.Count;
        var completed   = evals.Count(e => e.Status == SpeakingEvaluationStatus.Completed);
        var failed      = evals.Count(e => e.Status == SpeakingEvaluationStatus.Failed);
        var notSupported = evals.Count(e => e.Status == SpeakingEvaluationStatus.NotSupported
                                         || e.Status == SpeakingEvaluationStatus.Skipped);
        var pending     = evals.Count(e => e.Status == SpeakingEvaluationStatus.Pending
                                        || e.Status == SpeakingEvaluationStatus.Evaluating);

        var completionRate = Round((double)completed / total * 100);
        var failureRate    = Round((double)failed / total * 100);

        var completedSet = evals.Where(e => e.Status == SpeakingEvaluationStatus.Completed).ToList();

        var avgOverall      = Avg(completedSet, e => e.OverallScore);
        var avgFluency      = Avg(completedSet, e => e.FluencyScore);
        var avgCompleteness = Avg(completedSet, e => e.CompletenessScore);
        var avgRelevance    = Avg(completedSet, e => e.RelevanceScore);
        var avgPronunciation = Avg(completedSet, e => e.PronunciationScore);

        var nullOverallRate      = NullRate(completedSet, e => e.OverallScore);
        var nullFluencyRate      = NullRate(completedSet, e => e.FluencyScore);
        var nullCompletenessRate = NullRate(completedSet, e => e.CompletenessScore);
        var nullRelevanceRate    = NullRate(completedSet, e => e.RelevanceScore);

        var latestFailures = evals
            .Where(e => e.Status == SpeakingEvaluationStatus.Failed && e.FailureReason != null)
            .OrderByDescending(e => e.FailedAtUtc)
            .Take(5)
            .Select(e => e.FailureReason!)
            .ToList();

        // Dry-run signal counts — computed from Completed evaluations only.
        // Never modifies mastery, CEFR, or Learning Plan state.
        var signals = completedSet.Select(e => SpeakingDryRunSignalMapper.Map(e, _thresholds)).ToList();

        var dryRunPositive = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        var dryRunReview   = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.CandidateReviewSignal);
        var dryRunNoSignal = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.CandidateNoSignal);
        var dryRunBlocked  = signals.Count(s => s.IsBlocked);
        var dryRunCandidates = dryRunPositive + dryRunReview;

        // Blocked-by-status counts (from all evals, not just completed)
        var blockedByFailedEval      = failed;
        var blockedByUnsupportedStatus = notSupported;

        // Blocked-by-missing-score (from completed evals with null OverallScore)
        var blockedByMissingScore = completedSet.Count(e => e.OverallScore is null);

        // Blocked-by-confidence (completed, has score, but low confidence)
        var blockedByConfidence = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.BlockedLowConfidence);

        // Latest blocked reasons from dry-run signals on completed evals
        var latestBlockedReasons = signals
            .Where(s => s.IsBlocked && s.BlockedReason != null)
            .TakeLast(5)
            .Select(s => s.BlockedReason!)
            .ToList();

        // Applied signal counts from DB
        var appliedByType = await _db.SpeakingEvaluationAppliedSignals
            .GroupBy(s => s.SignalType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var applied         = appliedByType.Sum(x => x.Count);
        var appliedReview   = appliedByType.FirstOrDefault(x => x.Type == "Review")?.Count ?? 0;
        var appliedPositive = appliedByType.FirstOrDefault(x => x.Type == "Positive")?.Count ?? 0;

        // Blocked-by-config: candidate signals that are not yet applied (approx; full accuracy from signal service)
        var blockedByConfig = Math.Max(0, dryRunCandidates - applied);

        // Duplicate-skipped: evaluation IDs with applied signal that are still candidate-passing
        var appliedIds = await _db.SpeakingEvaluationAppliedSignals
            .Select(s => s.EvaluationId)
            .ToHashSetAsync(ct);
        var duplicateSkipped = completedSet
            .Where(e => appliedIds.Contains(e.Id))
            .Count(e => SpeakingDryRunSignalMapper.Map(e, _thresholds).IsCandidate);

        // Provider/model distribution
        var providerModelDistribution = completedSet
            .Where(e => e.ProviderName != null)
            .GroupBy(e => (Provider: e.ProviderName!, Model: e.ModelName))
            .Select(g => new SpeakingProviderModelCount(g.Key.Provider, g.Key.Model, g.Count()))
            .ToList();

        return new SpeakingEvaluationQualitySummaryDto(
            Total: total,
            Completed: completed,
            Failed: failed,
            NotSupported: notSupported,
            Pending: pending,
            CompletionRate: completionRate,
            FailureRate: failureRate,
            AverageOverallScore: avgOverall,
            AverageFluencyScore: avgFluency,
            AverageCompletenessScore: avgCompleteness,
            AverageRelevanceScore: avgRelevance,
            AveragePronunciationScore: avgPronunciation,
            NullOverallScoreRate: nullOverallRate,
            NullFluencyScoreRate: nullFluencyRate,
            NullCompletenessScoreRate: nullCompletenessRate,
            NullRelevanceScoreRate: nullRelevanceRate,
            DryRunCandidatePositiveSignals: dryRunPositive,
            DryRunCandidateReviewSignals: dryRunReview,
            DryRunCandidateNoSignals: dryRunNoSignal,
            DryRunBlocked: dryRunBlocked,
            DryRunCandidates: dryRunCandidates,
            Applied: applied,
            BlockedByConfig: blockedByConfig,
            BlockedByConfidence: blockedByConfidence,
            BlockedByMissingScore: blockedByMissingScore,
            BlockedByUnsupportedStatus: blockedByUnsupportedStatus,
            BlockedByFailedEval: blockedByFailedEval,
            DuplicateSkipped: duplicateSkipped,
            AppliedReview: appliedReview,
            AppliedPositive: appliedPositive,
            ProviderModelDistribution: providerModelDistribution,
            LatestFailureReasons: latestFailures,
            LatestBlockedReasons: latestBlockedReasons);
    }

    private static SpeakingEvaluationQualitySummaryDto Empty() => new(
        Total: 0, Completed: 0, Failed: 0, NotSupported: 0, Pending: 0,
        CompletionRate: 0, FailureRate: 0,
        AverageOverallScore: null, AverageFluencyScore: null,
        AverageCompletenessScore: null, AverageRelevanceScore: null, AveragePronunciationScore: null,
        NullOverallScoreRate: 0, NullFluencyScoreRate: 0,
        NullCompletenessScoreRate: 0, NullRelevanceScoreRate: 0,
        DryRunCandidatePositiveSignals: 0, DryRunCandidateReviewSignals: 0,
        DryRunCandidateNoSignals: 0, DryRunBlocked: 0,
        DryRunCandidates: 0, Applied: 0, BlockedByConfig: 0, BlockedByConfidence: 0,
        BlockedByMissingScore: 0, BlockedByUnsupportedStatus: 0, BlockedByFailedEval: 0,
        DuplicateSkipped: 0, AppliedReview: 0, AppliedPositive: 0,
        ProviderModelDistribution: [], LatestFailureReasons: [], LatestBlockedReasons: []);

    private static double Round(double value) => Math.Round(value, 1);

    private static double? Avg<T>(IReadOnlyList<T> set, Func<T, double?> selector)
    {
        var values = set.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return values.Count == 0 ? null : Round(values.Average());
    }

    private static double NullRate<T>(IReadOnlyList<T> set, Func<T, double?> selector)
    {
        if (set.Count == 0) return 0;
        var nullCount = set.Count(e => !selector(e).HasValue);
        return Round((double)nullCount / set.Count * 100);
    }
}
