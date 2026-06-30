using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Writing;

/// <summary>
/// Applies high-confidence writing evaluation signals to student learning state.
/// Conservative: disabled by default. CEFR and objective-completion are permanently off in Phase 17C.
/// Idempotent: one applied-signal record per evaluation enforced by unique index.
/// Five-gate pipeline mirrors the speaking signal application pattern from Phase 16I.
/// </summary>
public sealed class WritingEvaluationSignalApplicationService : IWritingEvaluationSignalApplicationService
{
    private const string RuleVersion = "17C-v1";
    private const string SkillKey = "writing";

    private readonly LinguaCoachDbContext _db;
    private readonly WritingEvaluationOptions _options;
    private readonly ILogger<WritingEvaluationSignalApplicationService> _logger;

    public WritingEvaluationSignalApplicationService(
        LinguaCoachDbContext db,
        IOptions<WritingEvaluationOptions> options,
        ILogger<WritingEvaluationSignalApplicationService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WritingSignalApplicationBatchResult> ApplyPendingSignalsAsync(
        int maxBatch, CancellationToken ct = default)
    {
        // Load completed evaluations that do not yet have an applied signal.
        var evaluations = await _db.WritingEvaluations
            .Where(e => e.Status == WritingEvaluationStatus.Completed)
            .Where(e => !_db.WritingEvaluationAppliedSignals.Any(s => s.EvaluationId == e.Id))
            .OrderBy(e => e.CreatedAt)
            .Take(maxBatch)
            .ToListAsync(ct);

        var result = new WritingSignalApplicationBatchResult(0, 0, 0, 0, 0, 0, 0, 0);
        foreach (var evaluation in evaluations)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                result = await ProcessOneAsync(evaluation, result, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "WritingSignalApplication: unexpected error EvaluationId={Id}", evaluation.Id);
                result = result with { Processed = result.Processed + 1, Failed = result.Failed + 1 };
            }
        }
        return result;
    }

    private async Task<WritingSignalApplicationBatchResult> ProcessOneAsync(
        WritingEvaluation evaluation,
        WritingSignalApplicationBatchResult running,
        CancellationToken ct)
    {
        // Gate 1: Evaluation status — only Completed evaluations eligible.
        // (Already filtered in the query, but guard against race conditions.)
        if (evaluation.Status != WritingEvaluationStatus.Completed)
        {
            return running with { Processed = running.Processed + 1 };
        }

        // Gate 2: Dry-run outcome — compute signal; block if upstream conditions prevent it.
        var dryRun = WritingDryRunSignalMapper.Map(evaluation);

        if (dryRun.IsBlocked)
        {
            _logger.LogDebug(
                "WritingSignalApplication: dry-run blocked EvaluationId={Id} Reason={Reason}",
                evaluation.Id, dryRun.BlockedReason);
            return running with { Processed = running.Processed + 1 };
        }

        if (dryRun.Outcome == WritingDryRunSignalOutcome.CandidateNoSignal)
        {
            return running with { Processed = running.Processed + 1, NoSignal = running.NoSignal + 1 };
        }

        // Gate 3: Config gate — must explicitly enable mastery signal application.
        if (!_options.ApplyMasterySignals)
        {
            _logger.LogDebug(
                "WritingSignalApplication: blocked by config (ApplyMasterySignals=false) EvaluationId={Id}",
                evaluation.Id);
            return running with { Processed = running.Processed + 1, BlockedByConfig = running.BlockedByConfig + 1 };
        }

        // Gate 4: Confidence gate.
        var minBand = ParseConfidenceBand(_options.MinimumConfidenceForMasterySignal);
        if (!MeetsConfidenceThreshold(dryRun.ConfidenceBand, minBand))
        {
            _logger.LogDebug(
                "WritingSignalApplication: blocked by confidence EvaluationId={Id} Band={Band} Min={Min}",
                evaluation.Id, dryRun.ConfidenceBand, minBand);
            return running with { Processed = running.Processed + 1, BlockedByConfidence = running.BlockedByConfidence + 1 };
        }

        // Gate 5: Signal-type gate and safety rules.
        var isReview = dryRun.Outcome == WritingDryRunSignalOutcome.CandidateReviewSignal;
        var isPositive = dryRun.Outcome == WritingDryRunSignalOutcome.CandidatePositiveSignal;

        if (isReview && !_options.AllowReviewSignals)
        {
            return running with { Processed = running.Processed + 1, BlockedBySignalType = running.BlockedBySignalType + 1 };
        }
        if (isPositive && !_options.AllowPositiveSignals)
        {
            return running with { Processed = running.Processed + 1, BlockedBySignalType = running.BlockedBySignalType + 1 };
        }

        // Idempotency: check again inside the operation to guard against race.
        var alreadyApplied = await _db.WritingEvaluationAppliedSignals
            .AnyAsync(s => s.EvaluationId == evaluation.Id, ct);
        if (alreadyApplied)
        {
            return running with { Processed = running.Processed + 1, DuplicateSkipped = running.DuplicateSkipped + 1 };
        }

        // Apply the signal.
        var signalType = isReview ? "Review" : "Positive";
        var confidenceStr = dryRun.ConfidenceBand.ToString();
        var reason = isReview
            ? $"Writing review signal: score {evaluation.OverallScore:F0}, confidence {confidenceStr}."
            : $"Writing positive signal: score {evaluation.OverallScore:F0}, confidence {confidenceStr}.";

        Guid? eventId = null;

        // Write StudentLearningEvent.
        var learningEvent = new StudentLearningEvent(
            studentProfileId: evaluation.StudentProfileId,
            source: LearningEventSource.WritingEvaluation,
            outcome: isReview ? LearningEventOutcome.NeedsReview : LearningEventOutcome.Practised,
            activityId: evaluation.LearningActivityId,
            activityAttemptId: evaluation.ActivityAttemptId,
            primarySkill: SkillKey,
            score: evaluation.OverallScore,
            normalizedScore: evaluation.OverallScore.HasValue ? evaluation.OverallScore.Value / 100.0 : null,
            metadataJson: $"{{\"source\":\"WritingEvaluation\",\"evaluationId\":\"{evaluation.Id:N}\",\"ruleVersion\":\"{RuleVersion}\"}}");
        _db.StudentLearningEvents.Add(learningEvent);
        eventId = learningEvent.Id;

        // For review signals: update or insert StudentSkillProfile with MarkWeak.
        if (isReview)
        {
            await ApplyWeakSkillSignalAsync(evaluation.StudentProfileId, ct);
        }

        // Write audit record.
        var appliedSignal = WritingEvaluationAppliedSignal.Create(
            evaluationId: evaluation.Id,
            attemptId: evaluation.ActivityAttemptId,
            studentProfileId: evaluation.StudentProfileId,
            activityId: evaluation.LearningActivityId,
            signalType: signalType,
            confidence: confidenceStr,
            scoreUsed: evaluation.OverallScore,
            skillAffected: SkillKey,
            dryRunOutcome: dryRun.Outcome.ToString(),
            reason: reason,
            learningEventId: eventId);
        _db.WritingEvaluationAppliedSignals.Add(appliedSignal);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WritingSignalApplication: applied EvaluationId={Id} SignalType={Type} Confidence={Confidence}",
            evaluation.Id, signalType, confidenceStr);

        return running with { Processed = running.Processed + 1, Applied = running.Applied + 1 };
    }

    private async Task ApplyWeakSkillSignalAsync(Guid studentProfileId, CancellationToken ct)
    {
        var normalizedKey = StudentSkillProfile.NormaliseSkillKey(SkillKey);
        var existing = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfileId && s.SkillKey == normalizedKey, ct);

        if (existing is not null)
        {
            existing.MarkWeak(true);
        }
        else
        {
            _db.StudentSkillProfiles.Add(
                new StudentSkillProfile(studentProfileId, SkillKey, "Writing", isWeak: true));
        }
    }

    public async Task<WritingSignalApplicationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        // All evaluations for cross-status counts.
        var allEvals = await _db.WritingEvaluations
            .Select(e => new
            {
                e.Id,
                e.Status,
                e.OverallScore,
                e.GrammarScore,
                e.VocabularyScore,
                e.CoherenceScore,
                e.TaskCompletionScore,
                e.FeedbackText,
                e.CorrectedText,
                e.ProviderName,
                e.ModelName,
            })
            .ToListAsync(ct);

        var totalCompleted = allEvals.Count(e => e.Status == WritingEvaluationStatus.Completed);
        var blockedByFailedOrUnsupported = allEvals.Count(e =>
            e.Status == WritingEvaluationStatus.Failed ||
            e.Status == WritingEvaluationStatus.NotSupported ||
            e.Status == WritingEvaluationStatus.Skipped);

        var appliedGroups = await _db.WritingEvaluationAppliedSignals
            .GroupBy(s => s.SignalType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalApplied = appliedGroups.Sum(g => g.Count);

        var appliedIds = await _db.WritingEvaluationAppliedSignals
            .Select(s => s.EvaluationId)
            .ToHashSetAsync(ct);

        var candidateCount = 0;
        var blockedByMissingScore = 0;
        var blockedByConfidence = 0;
        var blockedBySignalType = 0;
        var duplicateSkipped = 0;
        var noSignal = 0;

        foreach (var e in allEvals.Where(e => e.Status == WritingEvaluationStatus.Completed))
        {
            if (e.OverallScore is null) { blockedByMissingScore++; continue; }

            var signal = WritingDryRunSignalMapper.MapFromFields(
                evalId: e.Id,
                attemptId: Guid.Empty,
                studentId: Guid.Empty,
                activityId: Guid.Empty,
                createdAt: DateTime.UtcNow,
                providerName: e.ProviderName,
                modelName: e.ModelName,
                status: WritingEvaluationStatus.Completed,
                overallScore: e.OverallScore,
                grammarScore: e.GrammarScore,
                vocabularyScore: e.VocabularyScore,
                coherenceScore: e.CoherenceScore,
                taskCompletionScore: e.TaskCompletionScore,
                feedbackText: e.FeedbackText,
                correctedText: e.CorrectedText);

            if (signal.Outcome == WritingDryRunSignalOutcome.BlockedLowConfidence)
            {
                blockedByConfidence++;
                continue;
            }

            if (signal.Outcome == WritingDryRunSignalOutcome.CandidateNoSignal)
            {
                noSignal++;
                continue;
            }

            if (signal.IsCandidate)
            {
                candidateCount++;

                if (appliedIds.Contains(e.Id))
                {
                    duplicateSkipped++;
                    continue;
                }

                var isReview = signal.Outcome == WritingDryRunSignalOutcome.CandidateReviewSignal;
                var isPositive = signal.Outcome == WritingDryRunSignalOutcome.CandidatePositiveSignal;
                if (isReview && !_options.AllowReviewSignals) blockedBySignalType++;
                if (isPositive && !_options.AllowPositiveSignals) blockedBySignalType++;
            }
        }

        var blockedByConfig = _options.ApplyMasterySignals ? 0
            : Math.Max(0, candidateCount - totalApplied - blockedBySignalType - duplicateSkipped);

        return new WritingSignalApplicationSummaryDto(
            MasteryIntegrationEnabled: _options.ApplyMasterySignals,
            ReviewSignalsAllowed: _options.AllowReviewSignals,
            PositiveSignalsAllowed: _options.AllowPositiveSignals,
            ObjectiveCompletionAllowed: _options.AllowObjectiveCompletion,
            CefrUpdateAllowed: _options.AllowCefrUpdate,
            MinimumConfidenceRequired: _options.MinimumConfidenceForMasterySignal,
            TotalCompletedEvaluations: totalCompleted,
            CandidateSignals: candidateCount,
            AppliedSignals: totalApplied,
            BlockedByConfig: blockedByConfig,
            BlockedByConfidence: blockedByConfidence,
            BlockedBySignalType: blockedBySignalType,
            BlockedByFailedOrUnsupported: blockedByFailedOrUnsupported,
            BlockedByMissingScore: blockedByMissingScore,
            DuplicateSkipped: duplicateSkipped,
            NoSignal: noSignal,
            FailedApplication: 0);
    }

    public async Task<WritingSignalSafetySummaryDto> GetSignalSafetySummaryAsync(CancellationToken ct = default)
    {
        var appliedGroups = await _db.WritingEvaluationAppliedSignals
            .GroupBy(s => s.SignalType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalApplied = appliedGroups.Sum(g => g.Count);
        var reviewApplied = appliedGroups.FirstOrDefault(g => g.Type == "Review")?.Count ?? 0;
        var positiveApplied = appliedGroups.FirstOrDefault(g => g.Type == "Positive")?.Count ?? 0;

        // Structural invariants: AllowCefrUpdate and AllowObjectiveCompletion are computed => false.
        var invariantViolation = _options.AllowCefrUpdate || _options.AllowObjectiveCompletion;

        return new WritingSignalSafetySummaryDto(
            CefrUpdatesDisabled: !_options.AllowCefrUpdate,
            ObjectiveCompletionsDisabled: !_options.AllowObjectiveCompletion,
            LearningPlanAutoRegenDisabled: true,  // structural: no ILearningPlanService dependency
            SignalApplicationEnabled: _options.ApplyMasterySignals,
            PositiveSignalsEnabled: _options.AllowPositiveSignals,
            ReviewSignalsEnabled: _options.AllowReviewSignals,
            TotalApplied: totalApplied,
            PositiveApplied: positiveApplied,
            ReviewApplied: reviewApplied,
            InvariantViolationsDetected: invariantViolation);
    }

    private static WritingDryRunConfidenceBand ParseConfidenceBand(string value) =>
        value?.ToUpperInvariant() switch
        {
            "HIGH" => WritingDryRunConfidenceBand.High,
            "MEDIUM" => WritingDryRunConfidenceBand.Medium,
            _ => WritingDryRunConfidenceBand.High,
        };

    private static bool MeetsConfidenceThreshold(
        WritingDryRunConfidenceBand actual,
        WritingDryRunConfidenceBand minimum) =>
        (int)actual >= (int)minimum;
}
