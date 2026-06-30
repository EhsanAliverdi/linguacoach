using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Writing;

/// <summary>
/// Admin read-only query handler for writing evaluations.
/// Joins evaluation records with attempt and activity context.
/// Phase 17B adds quality summary and per-evaluation dry-run signal preview.
/// Never updates mastery, CEFR, objectives, or Learning Plan.
/// </summary>
public sealed class AdminWritingEvaluationHandler : IAdminWritingEvaluationQuery
{
    private readonly LinguaCoachDbContext _db;
    private readonly WritingEvaluationOptions _options;

    public AdminWritingEvaluationHandler(
        LinguaCoachDbContext db,
        IOptions<WritingEvaluationOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<AdminWritingEvaluationItemDto>> GetForStudentAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var query =
            from e in _db.WritingEvaluations.AsNoTracking()
            where e.StudentProfileId == studentProfileId
            join a in _db.ActivityAttempts.AsNoTracking()
                on e.ActivityAttemptId equals a.Id into attempts
            from a in attempts.DefaultIfEmpty()
            join act in _db.LearningActivities.AsNoTracking()
                on e.LearningActivityId equals act.Id into activities
            from act in activities.DefaultIfEmpty()
            orderby e.CreatedAt descending
            select new
            {
                e.Id,
                e.ActivityAttemptId,
                e.LearningActivityId,
                ActivityTitle = act != null ? act.Title : null,
                ActivityType = act != null ? (ActivityType?)act.ActivityType : null,
                e.Status,
                e.ProviderName,
                e.ModelName,
                SubmittedAtUtc = (DateTime?)(a != null ? a.CreatedAt : (DateTime?)null),
                e.CompletedAtUtc,
                e.OverallScore,
                e.GrammarScore,
                e.VocabularyScore,
                e.CoherenceScore,
                e.TaskCompletionScore,
                e.FeedbackText,
                e.SuggestedImprovement,
                e.CorrectedText,
                e.FailureReason,
            };

        var rows = await query.ToListAsync(ct);

        return rows.Select(r => new AdminWritingEvaluationItemDto(
            EvaluationId: r.Id,
            AttemptId: r.ActivityAttemptId,
            ActivityId: r.LearningActivityId,
            ActivityTitle: r.ActivityTitle,
            ActivityType: r.ActivityType?.ToString(),
            Status: r.Status.ToString(),
            ProviderName: r.ProviderName,
            ModelName: r.ModelName,
            SubmittedAtUtc: r.SubmittedAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            OverallScore: r.OverallScore,
            GrammarScore: r.GrammarScore,
            VocabularyScore: r.VocabularyScore,
            CoherenceScore: r.CoherenceScore,
            TaskCompletionScore: r.TaskCompletionScore,
            FeedbackText: r.FeedbackText,
            SuggestedImprovement: r.SuggestedImprovement,
            CorrectedText: r.CorrectedText,
            FailureReason: r.Status == WritingEvaluationStatus.Failed ? r.FailureReason : null))
            .ToList();
    }

    public async Task<WritingEvaluationQualitySummaryDto> GetQualitySummaryAsync(CancellationToken ct = default)
    {
        var evals = await _db.WritingEvaluations.AsNoTracking().ToListAsync(ct);

        if (evals.Count == 0)
            return Empty();

        var total = evals.Count;
        var pending = evals.Count(e => e.Status == WritingEvaluationStatus.Pending);
        var evaluating = evals.Count(e => e.Status == WritingEvaluationStatus.Evaluating);
        var completed = evals.Count(e => e.Status == WritingEvaluationStatus.Completed);
        var failed = evals.Count(e => e.Status == WritingEvaluationStatus.Failed);
        var notSupported = evals.Count(e => e.Status == WritingEvaluationStatus.NotSupported
                                         || e.Status == WritingEvaluationStatus.Skipped);

        var completionRate = Round((double)completed / total * 100);
        var failureRate = Round((double)failed / total * 100);

        var completedSet = evals.Where(e => e.Status == WritingEvaluationStatus.Completed).ToList();

        var avgOverall = Avg(completedSet, e => e.OverallScore);
        var avgGrammar = Avg(completedSet, e => e.GrammarScore);
        var avgVocabulary = Avg(completedSet, e => e.VocabularyScore);
        var avgCoherence = Avg(completedSet, e => e.CoherenceScore);
        var avgTaskCompletion = Avg(completedSet, e => e.TaskCompletionScore);

        var nullOverallRate = NullRate(completedSet, e => e.OverallScore);
        var nullGrammarRate = NullRate(completedSet, e => e.GrammarScore);
        var nullVocabularyRate = NullRate(completedSet, e => e.VocabularyScore);
        var nullCoherenceRate = NullRate(completedSet, e => e.CoherenceScore);
        var nullTaskCompletionRate = NullRate(completedSet, e => e.TaskCompletionScore);

        var correctedTextRate = completedSet.Count == 0
            ? 0.0
            : Round((double)completedSet.Count(e => e.CorrectedText != null) / completedSet.Count * 100);

        var latestFailures = evals
            .Where(e => e.Status == WritingEvaluationStatus.Failed && e.FailureReason != null)
            .OrderByDescending(e => e.FailedAtUtc)
            .Take(5)
            .Select(e => e.FailureReason!)
            .ToList();

        // Dry-run signal counts — computed from Completed evaluations only.
        // Never modifies mastery, CEFR, or Learning Plan state.
        var signals = completedSet.Select(WritingDryRunSignalMapper.Map).ToList();

        var outcomeBreakdown = signals
            .GroupBy(s => s.Outcome.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var dryRunCandidateCount = signals.Count(s => s.IsCandidate);
        var dryRunBlockedCount = signals.Count(s => s.IsBlocked);

        return new WritingEvaluationQualitySummaryDto
        {
            ConfigEnabled = _options.Enabled,
            ProviderName = _options.Provider,
            ModelName = _options.Model,
            TotalEvaluations = total,
            PendingCount = pending,
            EvaluatingCount = evaluating,
            CompletedCount = completed,
            FailedCount = failed,
            NotSupportedCount = notSupported,
            CompletionRate = completionRate,
            FailureRate = failureRate,
            NullOverallScoreRate = nullOverallRate,
            NullGrammarScoreRate = nullGrammarRate,
            NullVocabularyScoreRate = nullVocabularyRate,
            NullCoherenceScoreRate = nullCoherenceRate,
            NullTaskCompletionScoreRate = nullTaskCompletionRate,
            CorrectedTextAvailabilityRate = correctedTextRate,
            AverageOverallScore = avgOverall,
            AverageGrammarScore = avgGrammar,
            AverageVocabularyScore = avgVocabulary,
            AverageCoherenceScore = avgCoherence,
            AverageTaskCompletionScore = avgTaskCompletion,
            DryRunCandidateCount = dryRunCandidateCount,
            DryRunBlockedCount = dryRunBlockedCount,
            DryRunOutcomeBreakdown = outcomeBreakdown,
            LatestFailureReasons = latestFailures,
            Note = "Dry-run only — not applied to mastery",
        };
    }

    public async Task<WritingEvaluationWithDryRunDto?> GetWithDryRunAsync(
        Guid evaluationId,
        CancellationToken ct = default)
    {
        var evaluation = await _db.WritingEvaluations.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == evaluationId, ct);

        if (evaluation is null) return null;

        var signal = WritingDryRunSignalMapper.Map(evaluation);
        var signalDto = ToSignalDto(signal);

        return new WritingEvaluationWithDryRunDto
        {
            EvaluationId = evaluation.Id,
            AttemptId = evaluation.ActivityAttemptId,
            StudentId = evaluation.StudentProfileId,
            ActivityId = evaluation.LearningActivityId,
            Status = evaluation.Status.ToString(),
            ProviderName = evaluation.ProviderName,
            ModelName = evaluation.ModelName,
            CompletedAtUtc = evaluation.CompletedAtUtc,
            OverallScore = evaluation.OverallScore,
            GrammarScore = evaluation.GrammarScore,
            VocabularyScore = evaluation.VocabularyScore,
            CoherenceScore = evaluation.CoherenceScore,
            TaskCompletionScore = evaluation.TaskCompletionScore,
            FeedbackText = evaluation.FeedbackText,
            SuggestedImprovement = evaluation.SuggestedImprovement,
            CorrectedText = evaluation.CorrectedText,
            FailureReason = evaluation.Status == WritingEvaluationStatus.Failed ? evaluation.FailureReason : null,
            DryRunSignal = signalDto,
        };
    }

    private static WritingEvaluationQualitySummaryDto Empty() => new()
    {
        ConfigEnabled = false,
        TotalEvaluations = 0,
        PendingCount = 0,
        EvaluatingCount = 0,
        CompletedCount = 0,
        FailedCount = 0,
        NotSupportedCount = 0,
        CompletionRate = 0,
        FailureRate = 0,
        NullOverallScoreRate = 0,
        NullGrammarScoreRate = 0,
        NullVocabularyScoreRate = 0,
        NullCoherenceScoreRate = 0,
        NullTaskCompletionScoreRate = 0,
        CorrectedTextAvailabilityRate = 0,
        DryRunCandidateCount = 0,
        DryRunBlockedCount = 0,
        DryRunOutcomeBreakdown = new Dictionary<string, int>(),
        LatestFailureReasons = new List<string>(),
        Note = "Dry-run only — not applied to mastery",
    };

    private static WritingEvaluationDryRunSignalDto ToSignalDto(WritingEvaluationDryRunSignal s) =>
        new()
        {
            EvaluationId = s.EvaluationId,
            AttemptId = s.AttemptId,
            StudentId = s.StudentId,
            ActivityId = s.ActivityId,
            CreatedAt = s.CreatedAt,
            ProviderName = s.ProviderName,
            ModelName = s.ModelName,
            SourceStatus = s.SourceStatus.ToString(),
            CandidateSkill = s.CandidateSkill,
            OverallScore = s.OverallScore,
            GrammarScore = s.GrammarScore,
            VocabularyScore = s.VocabularyScore,
            CoherenceScore = s.CoherenceScore,
            TaskCompletionScore = s.TaskCompletionScore,
            ConfidenceBand = s.ConfidenceBand.ToString(),
            Outcome = s.Outcome.ToString(),
            SuggestedMasteryDelta = s.SuggestedMasteryDelta,
            SuggestedReviewNeed = s.SuggestedReviewNeed,
            AcceptedForFutureSignal = s.AcceptedForFutureSignal,
            BlockedReason = s.BlockedReason,
            Notes = s.Notes,
        };

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
