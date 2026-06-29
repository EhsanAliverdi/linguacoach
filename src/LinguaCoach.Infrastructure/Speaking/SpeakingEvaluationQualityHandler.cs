using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Speaking;

public sealed class SpeakingEvaluationQualityHandler : ISpeakingEvaluationQualityQuery
{
    private readonly LinguaCoachDbContext _db;

    public SpeakingEvaluationQualityHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<SpeakingEvaluationQualitySummaryDto> GetQualitySummaryAsync(CancellationToken ct = default)
    {
        var evals = await _db.SpeakingEvaluations.ToListAsync(ct);

        if (evals.Count == 0)
            return Empty();

        var total = evals.Count;
        var completed = evals.Count(e => e.Status == SpeakingEvaluationStatus.Completed);
        var failed = evals.Count(e => e.Status == SpeakingEvaluationStatus.Failed);
        var notSupported = evals.Count(e => e.Status == SpeakingEvaluationStatus.NotSupported
                                         || e.Status == SpeakingEvaluationStatus.Skipped);
        var pending = evals.Count(e => e.Status == SpeakingEvaluationStatus.Pending
                                    || e.Status == SpeakingEvaluationStatus.Evaluating);

        var completionRate = Round((double)completed / total * 100);
        var failureRate    = Round((double)failed / total * 100);

        var completedSet = evals.Where(e => e.Status == SpeakingEvaluationStatus.Completed).ToList();

        var avgOverall     = Avg(completedSet, e => e.OverallScore);
        var avgFluency     = Avg(completedSet, e => e.FluencyScore);
        var avgCompleteness = Avg(completedSet, e => e.CompletenessScore);
        var avgRelevance   = Avg(completedSet, e => e.RelevanceScore);

        var nullOverallRate     = NullRate(completedSet, e => e.OverallScore);
        var nullFluencyRate     = NullRate(completedSet, e => e.FluencyScore);
        var nullCompletenessRate = NullRate(completedSet, e => e.CompletenessScore);
        var nullRelevanceRate   = NullRate(completedSet, e => e.RelevanceScore);

        var latestFailures = evals
            .Where(e => e.Status == SpeakingEvaluationStatus.Failed && e.FailureReason != null)
            .OrderByDescending(e => e.FailedAtUtc)
            .Take(5)
            .Select(e => e.FailureReason!)
            .ToList();

        // Dry-run signal counts — computed from Completed evaluations only.
        // This never modifies any mastery, CEFR, or Learning Plan state.
        var signals = completedSet.Select(SpeakingDryRunSignalMapper.Map).ToList();

        var dryRunPositive  = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.CandidatePositiveSignal);
        var dryRunReview    = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.CandidateReviewSignal);
        var dryRunNoSignal  = signals.Count(s => s.Outcome == SpeakingDryRunSignalOutcome.CandidateNoSignal);
        var dryRunBlocked   = signals.Count(s => s.IsBlocked);

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
            NullOverallScoreRate: nullOverallRate,
            NullFluencyScoreRate: nullFluencyRate,
            NullCompletenessScoreRate: nullCompletenessRate,
            NullRelevanceScoreRate: nullRelevanceRate,
            DryRunCandidatePositiveSignals: dryRunPositive,
            DryRunCandidateReviewSignals: dryRunReview,
            DryRunCandidateNoSignals: dryRunNoSignal,
            DryRunBlocked: dryRunBlocked,
            LatestFailureReasons: latestFailures);
    }

    private static SpeakingEvaluationQualitySummaryDto Empty() => new(
        Total: 0, Completed: 0, Failed: 0, NotSupported: 0, Pending: 0,
        CompletionRate: 0, FailureRate: 0,
        AverageOverallScore: null, AverageFluencyScore: null,
        AverageCompletenessScore: null, AverageRelevanceScore: null,
        NullOverallScoreRate: 0, NullFluencyScoreRate: 0,
        NullCompletenessScoreRate: 0, NullRelevanceScoreRate: 0,
        DryRunCandidatePositiveSignals: 0, DryRunCandidateReviewSignals: 0,
        DryRunCandidateNoSignals: 0, DryRunBlocked: 0,
        LatestFailureReasons: []);

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
