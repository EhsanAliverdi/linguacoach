using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Mastery;

/// <summary>
/// Deterministic mastery evaluation engine. No AI calls.
/// Reads StudentLearningEvent history and applies rule-based thresholds.
///
/// Adaptive Curriculum Sprint 4 — <see cref="EvaluateStudentAsync"/>'s grouping key changed from
/// the flat CurriculumObjectiveKey()/PrimarySkill fallback chain to resolved
/// <c>SkillGraphNode</c> keys (see <see cref="ResolveNodeKeysByActivityIdAsync"/>). An event fans
/// out to EVERY approved node its Module is linked to (a Module can cover several nodes — same
/// "fan out, don't force a single pick" pattern Sprint 3 used for goal-vector implicit drift from
/// Module.ContextTagsJson), so one event can now contribute evidence to more than one bucket, and
/// an event with no resolvable node (legacy content with no StudentExerciseLaunch row) contributes
/// to none. This is an accepted, deliberate hard cutover — see
/// docs/reviews/2026-07-20-adaptive-curriculum-sprint4-node-mastery-review.md for the tradeoffs.
///
/// <see cref="EvaluateObjectiveMasteryAsync"/> is UNCHANGED — still grouped by the legacy
/// CurriculumObjectiveKey()/PrimarySkill chain — because it serves a different, still-live
/// consumer (LearningPlanService's own CurriculumObjective-keyed per-plan-objective progress
/// tracking), which was confirmed NOT superseded this sprint (its own sequencing logic is still
/// built entirely on CurriculumObjective/CurriculumRoutingService, a separate migration).
/// </summary>
public sealed class StudentMasteryEvaluationService : IStudentMasteryEvaluationService
{
    private readonly IStudentLearningLedger _ledger;
    private readonly LinguaCoachDbContext _db;
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
        LinguaCoachDbContext db,
        IOptions<MasteryOptions> opts,
        ILogger<StudentMasteryEvaluationService> logger)
    {
        _ledger = ledger;
        _db = db;
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
        var byNode = await GroupByNodeKeyAsync(events, ct);

        var mastered = new List<string>();
        var completed = new List<string>();  // NeedsReview signal = sufficient completion evidence
        var weak = new List<string>();
        var atRisk = new List<string>();

        foreach (var (key, keyEvents) in byNode)
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

    /// <summary>Adaptive Curriculum Sprint 4 — resolves each event to the approved, active
    /// <c>SkillGraphNode</c> key(s) its underlying Module is linked to, via
    /// ActivityId → StudentExerciseLaunch → ModuleId → ModuleSkillGraphNodeLink → SkillGraphNode,
    /// and fans each event out into every matching node's bucket (an event with zero resolvable
    /// nodes — legacy content, no StudentExerciseLaunch row — contributes to no bucket). One
    /// batched query resolves all events' node keys up front rather than querying per event.
    /// Preserves <c>events</c>' newest-first order within each bucket, required by
    /// <see cref="ComputeSignal"/>'s consecutive-streak counting.</summary>
    private async Task<Dictionary<string, List<StudentLearningEvent>>> GroupByNodeKeyAsync(
        IReadOnlyList<StudentLearningEvent> events, CancellationToken ct)
    {
        var activityIds = events.Where(e => e.ActivityId.HasValue).Select(e => e.ActivityId!.Value).Distinct().ToList();

        var nodeKeysByActivityId = activityIds.Count == 0
            ? new Dictionary<Guid, List<string>>()
            : (await _db.StudentExerciseLaunches.AsNoTracking()
                .Where(l => activityIds.Contains(l.LearningActivityId))
                .Join(_db.ModuleSkillGraphNodeLinks.AsNoTracking(), l => l.ModuleId, link => link.ModuleId,
                    (l, link) => new { l.LearningActivityId, link.SkillGraphNodeId })
                .Join(_db.SkillGraphNodes.AsNoTracking()
                        .Where(n => n.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved && n.IsActive),
                    x => x.SkillGraphNodeId, n => n.Id, (x, n) => new { x.LearningActivityId, n.Key })
                .ToListAsync(ct))
                .GroupBy(x => x.LearningActivityId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        var buckets = new Dictionary<string, List<StudentLearningEvent>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in events) // already newest-first per the ledger contract — preserved below
        {
            if (e.ActivityId is null) continue;
            if (!nodeKeysByActivityId.TryGetValue(e.ActivityId.Value, out var nodeKeys) || nodeKeys.Count == 0) continue;

            foreach (var key in nodeKeys)
            {
                if (!buckets.TryGetValue(key, out var list))
                    buckets[key] = list = [];
                list.Add(e);
            }
        }

        return buckets;
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
