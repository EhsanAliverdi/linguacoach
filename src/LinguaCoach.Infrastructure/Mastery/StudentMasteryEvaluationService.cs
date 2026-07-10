using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Mastery;

/// <summary>
/// Deterministic mastery evaluation engine. No AI calls.
/// Reads StudentLearningEvent history and applies rule-based thresholds.
/// </summary>
public sealed class StudentMasteryEvaluationService : IStudentMasteryEvaluationService
{
    private readonly IStudentLearningLedger _ledger;
    private readonly MasteryOptions _opts;
    private readonly ILogger<StudentMasteryEvaluationService> _logger;

    // Outcomes that count as a "success" for consecutive-run tracking.
    private static readonly HashSet<LearningEventOutcome> SuccessOutcomes =
    [
        LearningEventOutcome.Mastered,
        LearningEventOutcome.Practised,
        LearningEventOutcome.Reviewed
    ];

    private static readonly HashSet<LearningEventOutcome> FailureOutcomes =
    [
        LearningEventOutcome.Failed,
        LearningEventOutcome.NeedsReview
    ];

    public StudentMasteryEvaluationService(
        IStudentLearningLedger ledger,
        IOptions<MasteryOptions> opts,
        ILogger<StudentMasteryEvaluationService> logger)
    {
        _ledger = ledger;
        _opts = opts.Value;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public async Task<StudentMasteryReport> EvaluateStudentAsync(
        Guid studentId,
        MasteryEvaluationReason reason,
        CancellationToken ct = default)
    {
        var events = await _ledger.GetRecentAsync(studentId, limit: 200, ct: ct);

        // Group events by primary skill (our proxy for objective key when no explicit key).
        var byObjective = events
            .Where(e => e.PrimarySkill is not null || e.CurriculumObjectiveKey() is not null)
            .GroupBy(e => e.CurriculumObjectiveKey() ?? e.PrimarySkill!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.OccurredAtUtc).ToList());

        var mastered = new List<string>();
        var completed = new List<string>();  // NeedsReview signal = sufficient completion evidence
        var weak = new List<string>();
        var atRisk = new List<string>();

        foreach (var (key, keyEvents) in byObjective)
        {
            var signal = ComputeSignal(key, keyEvents);
            switch (signal.MasteryStatus)
            {
                case MasteryStatus.Mastered:
                    mastered.Add(key);
                    break;
                case MasteryStatus.AtRisk:
                    atRisk.Add(key);
                    break;
                case MasteryStatus.NeedsReview:
                    // NeedsReview = mixed results but trending positive: treat as completed for plan.
                    completed.Add(key);
                    weak.Add(key);
                    break;
                case MasteryStatus.NeedsPractice:
                    weak.Add(key);
                    break;
            }
        }

        // Phase I2C: the readiness-pool demotion sweep that used to run here was removed along
        // with StudentActivityReadinessItem. DemotedCount/SkippedCount/MarkedReviewOnlyCount are
        // always 0 now — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
        return new StudentMasteryReport
        {
            StudentId = studentId,
            EvaluatedAtUtc = DateTime.UtcNow,
            Reason = reason,
            MasteredObjectiveKeys = mastered,
            CompletedObjectiveKeys = completed,
            WeakObjectiveKeys = weak,
            AtRiskObjectiveKeys = atRisk,
            DemotedCount = 0,
            SkippedCount = 0,
            MarkedReviewOnlyCount = 0
        };
    }

    public async Task<ObjectiveMasterySignal> EvaluateObjectiveMasteryAsync(
        Guid studentId,
        string objectiveKey,
        CancellationToken ct = default)
    {
        var allEvents = await _ledger.GetRecentAsync(studentId, limit: 200, ct: ct);
        // Ledger contract: GetRecentAsync returns events newest-first. Preserve that order.
        var relevant = allEvents
            .Where(e => string.Equals(e.CurriculumObjectiveKey() ?? e.PrimarySkill, objectiveKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ComputeSignal(objectiveKey, relevant);
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private ObjectiveMasterySignal ComputeSignal(string objectiveKey, List<StudentLearningEvent> events)
    {
        var skillKey = events.FirstOrDefault()?.PrimarySkill;

        if (events.Count == 0)
        {
            return new ObjectiveMasterySignal
            {
                ObjectiveKey = objectiveKey,
                SkillKey = skillKey,
                MasteryStatus = MasteryStatus.InsufficientEvidence,
                EvidenceCount = 0,
                ConsecutiveSuccesses = 0,
                ConsecutiveFailures = 0,
                RecentAverageScore = 0,
                LastSeenUtc = null
            };
        }

        var avgScore = events
            .Where(e => e.Score.HasValue)
            .Select(e => e.Score!.Value)
            .DefaultIfEmpty(0)
            .Average();

        // Consecutive success/failure counting from most-recent first.
        var consecutiveSuccesses = 0;
        var consecutiveFailures = 0;

        foreach (var ev in events)
        {
            if (SuccessOutcomes.Contains(ev.Outcome))
            {
                if (consecutiveFailures == 0) consecutiveSuccesses++;
                else break;
            }
            else if (FailureOutcomes.Contains(ev.Outcome))
            {
                if (consecutiveSuccesses == 0) consecutiveFailures++;
                else break;
            }
            else
            {
                break;
            }
        }

        var status = ClassifyStatus(events.Count, consecutiveSuccesses, consecutiveFailures, avgScore);

        return new ObjectiveMasterySignal
        {
            ObjectiveKey = objectiveKey,
            SkillKey = skillKey,
            MasteryStatus = status,
            EvidenceCount = events.Count,
            ConsecutiveSuccesses = consecutiveSuccesses,
            ConsecutiveFailures = consecutiveFailures,
            RecentAverageScore = avgScore,
            LastSeenUtc = events[0].OccurredAtUtc
        };
    }

    private MasteryStatus ClassifyStatus(
        int count,
        int consecutiveSuccesses,
        int consecutiveFailures,
        double avgScore)
    {
        if (count < 3)
            return MasteryStatus.InsufficientEvidence;

        // AtRisk: 2+ consecutive failures OR avg score < 30
        if (consecutiveFailures >= 2 || avgScore < 30)
            return MasteryStatus.AtRisk;

        // Mastered: >= threshold events, last N consecutive successes, high avg
        if (count >= _opts.EvidenceCountThreshold
            && consecutiveSuccesses >= _opts.ConsecutiveSuccessThreshold
            && avgScore >= _opts.MasteryScoreThreshold)
            return MasteryStatus.Mastered;

        // NeedsReview: mixed but recent success, avg 50-79
        if (consecutiveSuccesses >= 1 && avgScore >= 50 && avgScore < 80)
            return MasteryStatus.NeedsReview;

        // NeedsPractice: mixed, avg 30-79
        if (avgScore >= 30 && avgScore < 80)
            return MasteryStatus.NeedsPractice;

        return MasteryStatus.NeedsPractice;
    }

}

/// <summary>Extension to extract the curriculum objective key from a learning event.</summary>
file static class LearningEventExtensions
{
    // Phase 8 (AI Bank-First Teaching Architecture): StudentLearningEvent.CurriculumObjectiveKey
    // is now a real field, populated where the writer knows it (see ActivitySubmitHandler).
    // PatternKey remains the fallback proxy for events written before this field existed, or by
    // callers that don't yet resolve a real objective key — backward compatible, no threshold
    // logic below this method changes.
    public static string? CurriculumObjectiveKey(this StudentLearningEvent e) => e.CurriculumObjectiveKey ?? e.PatternKey;
}
