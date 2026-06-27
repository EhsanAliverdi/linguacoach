using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.LearningPlan;

/// <summary>
/// Deterministic Learning Plan orchestrator.
/// Generates and refreshes student learning plans by coordinating:
///   - CurriculumRoutingService (objective selection)
///   - StudentMasteryEvaluationService (mastery state)
///   - LearnerPreferences (goal context, difficulty, focus)
///
/// No direct AI calls. Review scaffold generation remains disabled by default.
/// </summary>
public sealed class LearningPlanService : ILearningPlanService
{
    // Source tag recorded on plan objectives so downstream services can trace origin.
    private const string SourcePlan = "LearningPlanService";
    private const string SourceReview = "LearningPlanService:Review";
    private const string SourceWeak = "LearningPlanService:WeakSkill";

    private readonly LinguaCoachDbContext _db;
    private readonly ICurriculumRoutingService _routing;
    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly LearningPlanOptions _options;
    private readonly ILogger<LearningPlanService> _logger;

    public LearningPlanService(
        LinguaCoachDbContext db,
        ICurriculumRoutingService routing,
        IStudentMasteryEvaluationService mastery,
        ILearningGoalContextResolver goalContextResolver,
        IOptions<LearningPlanOptions> options,
        ILogger<LearningPlanService> logger)
    {
        _db = db;
        _routing = routing;
        _mastery = mastery;
        _goalContextResolver = goalContextResolver;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LearningPlanSummary> GetOrCreatePlanAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var plan = await LoadActivePlanAsync(studentProfileId, ct);
        if (plan is not null)
            return BuildSummary(plan);

        return await RegeneratePlanAsync(studentProfileId, "initial_generation", ct);
    }

    public async Task<LearningPlanSummary> RegeneratePlanAsync(
        Guid studentProfileId,
        string reason,
        CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair).ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair).ThenInclude(lp => lp!.TargetLanguage)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct);

        if (profile is null)
            throw new InvalidOperationException($"StudentProfile {studentProfileId} not found.");

        var cefrLevel = _routing.NormalizeCefrLevel(profile.CefrLevel);

        // Supersede any existing active plan.
        var existingPlans = await _db.StudentLearningPlans
            .Where(p => p.StudentProfileId == studentProfileId
                && (p.Status == LearningPlanStatus.Active || p.Status == LearningPlanStatus.Regenerating))
            .ToListAsync(ct);

        foreach (var old in existingPlans)
            old.Supersede();

        // Evaluate mastery to get mastered/weak objective keys.
        StudentMasteryReport? masteryReport = null;
        try
        {
            masteryReport = await _mastery.EvaluateStudentAsync(
                studentProfileId, MasteryEvaluationReason.PlanGeneration, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LearningPlanService: mastery evaluation failed for {StudentProfileId}; proceeding without mastery data.",
                studentProfileId);
        }

        var masteredKeys = masteryReport?.MasteredObjectiveKeys ?? [];
        var weakKeys = masteryReport?.WeakObjectiveKeys ?? [];

        // Resolve learner goal context for routing.
        var goalContext = _goalContextResolver.Resolve(profile);

        // Build the objective sequence for the plan.
        var objectives = await BuildObjectiveSequenceAsync(
            profile, cefrLevel, goalContext, masteredKeys, weakKeys, ct);

        // Persist the new plan.
        var plan = new StudentLearningPlan(
            studentProfileId,
            cefrLevel,
            reason,
            _options.PlannedLessonCount);

        if (existingPlans.Count > 0)
            plan.StartRegeneration(reason);

        _db.StudentLearningPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        // Persist objectives.
        foreach (var obj in objectives)
        {
            var planObj = new StudentLearningPlanObjective(
                plan.Id,
                obj.ObjectiveKey,
                obj.CefrLevel,
                obj.PrimarySkill,
                obj.ContextTags.FirstOrDefault() ?? CurriculumContextTagConstants.GeneralEnglish,
                title: null,
                priority: obj.Priority,
                source: obj.Source,
                plannedOrder: obj.PlannedOrder,
                isReview: obj.IsReview,
                isBlocked: obj.IsBlocked,
                blockedByObjectiveKey: obj.BlockedByObjectiveKey);
            _db.StudentLearningPlanObjectives.Add(planObj);
        }

        plan.MarkReady(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LearningPlanService: regenerated plan {PlanId} for {StudentProfileId} reason={Reason} objectives={Count}",
            plan.Id, studentProfileId, reason, objectives.Count);

        // Reload to include objectives.
        var loaded = await LoadActivePlanAsync(studentProfileId, ct)
            ?? throw new InvalidOperationException("Plan was not persisted correctly.");

        return BuildSummary(loaded);
    }

    public async Task<LearningPlanProgressSummary> GetProgressAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct);

        var cefrLevel = _routing.NormalizeCefrLevel(profile?.CefrLevel);

        var plan = await LoadActivePlanAsync(studentProfileId, ct);

        if (plan is null)
        {
            return new LearningPlanProgressSummary(
                StudentProfileId: studentProfileId,
                CurrentCefrLevel: cefrLevel,
                TotalObjectives: 0,
                ObjectivesCompleted: 0,
                ObjectivesMastered: 0,
                ObjectivesInProgress: 0,
                ObjectivesRemaining: 0,
                ReviewObjectives: 0,
                BlockedObjectives: 0,
                DeferredObjectives: 0,
                CompletionPercentage: 0,
                MasteryPercentage: 0,
                CurrentLearningPhase: "No plan",
                LessonQueueLength: 0,
                LessonQueueTarget: _options.PlannedLessonCount,
                LastCompletedAt: null,
                CurrentObjectiveKey: null,
                NextObjectiveKey: null,
                ObjectivesCompletedToday: 0);
        }

        var objectives = plan.Objectives.ToList();
        var total = objectives.Count;
        var completed = objectives.Count(o => o.Status == LearningPlanObjectiveStatus.Completed);
        var mastered = objectives.Count(o => o.Status == LearningPlanObjectiveStatus.Mastered);
        var inProgress = objectives.Count(o => o.Status == LearningPlanObjectiveStatus.InProgress);
        var remaining = objectives.Count(o => o.Status == LearningPlanObjectiveStatus.Active);
        var review = objectives.Count(o => o.IsReview && o.Status == LearningPlanObjectiveStatus.Active);
        var blocked = objectives.Count(o => o.IsBlocked);
        var deferred = objectives.Count(o => o.Status == LearningPlanObjectiveStatus.Deferred);

        var doneCount = completed + mastered;
        var completionPct = total > 0 ? Math.Round((double)doneCount / total * 100, 1) : 0;
        var masteryPct = total > 0 ? Math.Round((double)mastered / total * 100, 1) : 0;

        var lastCompletedAt = objectives
            .Where(o => o.Status is LearningPlanObjectiveStatus.Completed
                or LearningPlanObjectiveStatus.Mastered)
            .Select(o => o.LastEvaluatedAt)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        var lastCompletedAtNullable = lastCompletedAt == DateTime.MinValue ? (DateTime?)null : lastCompletedAt;

        var phase = DeterminePhase(cefrLevel, completionPct);

        var todayUtc = DateTime.UtcNow.Date;
        var objectivesCompletedToday = objectives.Count(o =>
            o.Status is LearningPlanObjectiveStatus.Completed or LearningPlanObjectiveStatus.Mastered
            && o.LastEvaluatedAt.HasValue
            && o.LastEvaluatedAt.Value.Date >= todayUtc);

        var inProgressObj = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.InProgress)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .FirstOrDefault();

        var activeOrdered = objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .ThenBy(o => o.Priority)
            .ToList();

        string? currentObjectiveKey;
        string? nextObjectiveKey;

        if (inProgressObj is not null)
        {
            currentObjectiveKey = inProgressObj.ObjectiveKey;
            nextObjectiveKey = activeOrdered.FirstOrDefault()?.ObjectiveKey;
        }
        else
        {
            currentObjectiveKey = activeOrdered.FirstOrDefault()?.ObjectiveKey;
            nextObjectiveKey = activeOrdered.Skip(1).FirstOrDefault()?.ObjectiveKey;
        }

        // Count ready lessons in the pool as queue length.
        var queueLength = await _db.StudentActivityReadinessItems
            .CountAsync(i => i.StudentId == studentProfileId
                && i.Source == ReadinessPoolSource.LessonBatch
                && i.Status == ReadinessPoolStatus.Ready, ct);

        return new LearningPlanProgressSummary(
            StudentProfileId: studentProfileId,
            CurrentCefrLevel: cefrLevel,
            TotalObjectives: total,
            ObjectivesCompleted: completed,
            ObjectivesMastered: mastered,
            ObjectivesInProgress: inProgress,
            ObjectivesRemaining: remaining,
            ReviewObjectives: review,
            BlockedObjectives: blocked,
            DeferredObjectives: deferred,
            CompletionPercentage: completionPct,
            MasteryPercentage: masteryPct,
            CurrentLearningPhase: phase,
            LessonQueueLength: queueLength,
            LessonQueueTarget: plan.PlannedLessonCount,
            LastCompletedAt: lastCompletedAtNullable,
            CurrentObjectiveKey: currentObjectiveKey,
            NextObjectiveKey: nextObjectiveKey,
            ObjectivesCompletedToday: objectivesCompletedToday);
    }

    public async Task<PlannedObjectiveContext?> GetNextPlannedObjectiveAsync(
        Guid studentProfileId,
        string? preferredSkill = null,
        CancellationToken ct = default)
    {
        var plan = await LoadActivePlanAsync(studentProfileId, ct);
        if (plan is null)
            return null;

        var candidates = plan.Objectives
            .Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsBlocked)
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .ThenBy(o => o.Priority)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredSkill))
        {
            var skillMatch = candidates
                .Where(o => string.Equals(o.Skill, preferredSkill, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (skillMatch is not null)
                return ToContext(skillMatch);
        }

        var next = candidates.FirstOrDefault();
        return next is null ? null : ToContext(next);
    }

    public async Task<IReadOnlyList<PlannedObjectiveContext>> GetPracticeGymObjectivesAsync(
        Guid studentProfileId,
        int maxCount = 5,
        CancellationToken ct = default)
    {
        var plan = await LoadActivePlanAsync(studentProfileId, ct);
        if (plan is null)
            return [];

        var objectives = plan.Objectives.ToList();

        // Priority order for Practice Gym: current → review → deferred
        var ordered = objectives
            .Where(o => !o.IsBlocked)
            .OrderBy(o => o.IsReview ? 1 : 0)         // current objectives first
            .ThenBy(o => o.Status == LearningPlanObjectiveStatus.Deferred ? 1 : 0)
            .ThenBy(o => o.PlannedOrder ?? int.MaxValue)
            .ThenBy(o => o.Priority)
            .Take(maxCount)
            .Select(ToContext)
            .ToList();

        return ordered;
    }

    public async Task MarkObjectiveInProgressAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default)
    {
        var plan = await _db.StudentLearningPlans
            .Include(p => p.Objectives)
            .Where(p => p.StudentProfileId == studentProfileId && p.Status == LearningPlanStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (plan is null)
            return;

        var objective = plan.Objectives.FirstOrDefault(
            o => string.Equals(o.ObjectiveKey, objectiveKey, StringComparison.OrdinalIgnoreCase));

        if (objective is null)
            return;

        objective.MarkInProgress();

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "LearningPlanService: objective '{ObjectiveKey}' marked InProgress for {StudentProfileId}",
                objectiveKey, studentProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LearningPlanService: could not save InProgress status for objective '{ObjectiveKey}' student {StudentProfileId}",
                objectiveKey, studentProfileId);
        }
    }

    public async Task MarkObjectiveCompletedAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default)
    {
        await TransitionObjectiveAsync(
            studentProfileId, objectiveKey,
            targetStatus: LearningPlanObjectiveStatus.Completed,
            apply: o => o.MarkCompleted(),
            label: "Completed",
            ct);
    }

    public async Task MarkObjectiveMasteredAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default)
    {
        await TransitionObjectiveAsync(
            studentProfileId, objectiveKey,
            targetStatus: LearningPlanObjectiveStatus.Mastered,
            apply: o => o.MarkMastered(),
            label: "Mastered",
            ct);
    }

    private async Task TransitionObjectiveAsync(
        Guid studentProfileId,
        string objectiveKey,
        LearningPlanObjectiveStatus targetStatus,
        Action<StudentLearningPlanObjective> apply,
        string label,
        CancellationToken ct)
    {
        var plan = await _db.StudentLearningPlans
            .Include(p => p.Objectives)
            .Where(p => p.StudentProfileId == studentProfileId && p.Status == LearningPlanStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (plan is null)
            return;

        var objective = plan.Objectives.FirstOrDefault(
            o => string.Equals(o.ObjectiveKey, objectiveKey, StringComparison.OrdinalIgnoreCase));

        if (objective is null)
            return;

        // Idempotent — already in target state or a "higher" terminal state.
        var alreadyDone = targetStatus == LearningPlanObjectiveStatus.Completed
            ? objective.Status is LearningPlanObjectiveStatus.Completed
                or LearningPlanObjectiveStatus.Mastered
            : objective.Status == LearningPlanObjectiveStatus.Mastered;

        if (alreadyDone)
        {
            _logger.LogDebug(
                "LearningPlanService: objective '{ObjectiveKey}' already {Label} for {StudentProfileId} — no-op.",
                objectiveKey, label, studentProfileId);
            return;
        }

        apply(objective);

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "LearningPlanService: objective '{ObjectiveKey}' marked {Label} for {StudentProfileId}",
                objectiveKey, label, studentProfileId);

            LogPlanExhaustionIfNeeded(plan, studentProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LearningPlanService: could not save {Label} status for objective '{ObjectiveKey}' student {StudentProfileId}",
                label, objectiveKey, studentProfileId);
        }
    }

    public async Task<LearningPlanObjectiveProgressUpdate> TryUpdateObjectiveProgressAsync(
        Guid studentProfileId,
        string objectiveKey,
        CancellationToken ct = default)
    {
        try
        {
            var plan = await LoadActivePlanAsync(studentProfileId, ct);
            if (plan is null)
                return new LearningPlanObjectiveProgressUpdate(objectiveKey, null, null, false, "no_active_plan");

            var objective = plan.Objectives.FirstOrDefault(
                o => string.Equals(o.ObjectiveKey, objectiveKey, StringComparison.OrdinalIgnoreCase));

            if (objective is null)
                return new LearningPlanObjectiveProgressUpdate(objectiveKey, null, null, false, "objective_not_in_plan");

            var previousStatus = objective.Status;

            // Only update Active or InProgress objectives — terminal states are no-ops.
            if (previousStatus is not (LearningPlanObjectiveStatus.Active or LearningPlanObjectiveStatus.InProgress))
                return new LearningPlanObjectiveProgressUpdate(objectiveKey, previousStatus, previousStatus, false, "already_terminal");

            var signal = await _mastery.EvaluateObjectiveMasteryAsync(studentProfileId, objectiveKey, ct);

            LearningPlanObjectiveStatus newStatus;
            string reason;

            switch (signal.MasteryStatus)
            {
                case MasteryStatus.Mastered:
                    await MarkObjectiveMasteredAsync(studentProfileId, objectiveKey, ct);
                    newStatus = LearningPlanObjectiveStatus.Mastered;
                    reason = "mastered";
                    break;

                case MasteryStatus.NeedsReview:
                    await MarkObjectiveCompletedAsync(studentProfileId, objectiveKey, ct);
                    newStatus = LearningPlanObjectiveStatus.Completed;
                    reason = "needs_review";
                    break;

                default:
                    return new LearningPlanObjectiveProgressUpdate(
                        objectiveKey, previousStatus, previousStatus, false,
                        $"insufficient_evidence_{signal.MasteryStatus}");
            }

            var changed = newStatus != previousStatus;
            if (changed)
                _logger.LogInformation(
                    "LearningPlanService: real-time progress — objective '{ObjectiveKey}' {Prev} → {New} for {StudentProfileId} (evidence={Count})",
                    objectiveKey, previousStatus, newStatus, studentProfileId, signal.EvidenceCount);

            return new LearningPlanObjectiveProgressUpdate(objectiveKey, previousStatus, newStatus, changed, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LearningPlanService: TryUpdateObjectiveProgressAsync failed for objective '{ObjectiveKey}' student {StudentProfileId}",
                objectiveKey, studentProfileId);
            return new LearningPlanObjectiveProgressUpdate(objectiveKey, null, null, false, "error");
        }
    }

    private void LogPlanExhaustionIfNeeded(StudentLearningPlan plan, Guid studentProfileId)
    {
        var hasActiveObjectives = plan.Objectives.Any(
            o => o.Status == LearningPlanObjectiveStatus.Active
              || o.Status == LearningPlanObjectiveStatus.InProgress);

        if (!hasActiveObjectives)
        {
            _logger.LogInformation(
                "LearningPlanService: all objectives consumed for plan {PlanId} student {StudentProfileId} — plan exhausted, regeneration recommended.",
                plan.Id, studentProfileId);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<StudentLearningPlan?> LoadActivePlanAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        return await _db.StudentLearningPlans
            .Include(p => p.Objectives)
            .Where(p => p.StudentProfileId == studentProfileId
                && p.Status == LearningPlanStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<ObjectiveCandidate>> BuildObjectiveSequenceAsync(
        StudentProfile profile,
        string cefrLevel,
        ResolvedLearningGoalContext goalContext,
        IReadOnlyList<string> masteredKeys,
        IReadOnlyList<string> weakKeys,
        CancellationToken ct)
    {
        var results = new List<ObjectiveCandidate>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Primary new-learning objectives: up to PlannedLessonCount unique objectives.
        var skills = GetSkillSequence(profile);
        var plannedOrder = 0;

        foreach (var skill in skills)
        {
            if (results.Count(r => !r.IsReview) >= _options.PlannedLessonCount)
                break;

            var request = new CurriculumRoutingRequest
            {
                StudentId = profile.Id,
                CurrentCefrLevel = cefrLevel,
                PrimarySkill = skill,
                Source = SourcePlan,
                ResolvedLearningGoalContext = goalContext,
                FocusAreas = profile.FocusAreas ?? [],
                CustomFocusArea = profile.CustomFocusArea,
                DifficultyPreference = profile.DifficultyPreference?.ToString(),
                MasteredObjectiveKeys = masteredKeys,
                AllowReviewOfMastered = false,
                Mode = RoutingMode.NewLearning,
                AllowReviewOrScaffold = false
            };

            var rec = await _routing.RecommendAsync(request, ct);

            if (rec.CurriculumObjectiveKey is null || seenKeys.Contains(rec.CurriculumObjectiveKey))
                continue;

            seenKeys.Add(rec.CurriculumObjectiveKey);
            results.Add(new ObjectiveCandidate(
                ObjectiveKey: rec.CurriculumObjectiveKey,
                CefrLevel: rec.TargetCefrLevel,
                PrimarySkill: rec.PrimarySkill ?? skill,
                SecondarySkills: rec.SecondarySkills,
                ContextTags: rec.ContextTags,
                Priority: plannedOrder,
                Source: SourcePlan,
                PlannedOrder: plannedOrder++,
                IsReview: false,
                IsBlocked: false,
                BlockedByObjectiveKey: null));
        }

        // 2. Review objectives from weak keys — insert after primary sequence.
        foreach (var weakKey in weakKeys.Take(3))
        {
            if (seenKeys.Contains(weakKey))
                continue;

            // Route in review mode to confirm the key is still valid/runnable.
            var request = new CurriculumRoutingRequest
            {
                StudentId = profile.Id,
                CurrentCefrLevel = cefrLevel,
                Source = SourceWeak,
                ResolvedLearningGoalContext = goalContext,
                MasteredObjectiveKeys = masteredKeys,
                AllowReviewOfMastered = true,
                Mode = RoutingMode.Review,
                AllowReviewOrScaffold = true
            };

            var rec = await _routing.RecommendAsync(request, ct);
            var key = rec.CurriculumObjectiveKey ?? weakKey;

            if (seenKeys.Contains(key))
                continue;

            seenKeys.Add(key);
            results.Add(new ObjectiveCandidate(
                ObjectiveKey: key,
                CefrLevel: rec.TargetCefrLevel,
                PrimarySkill: rec.PrimarySkill ?? CurriculumSkillConstants.Speaking,
                SecondarySkills: rec.SecondarySkills,
                ContextTags: rec.ContextTags,
                Priority: plannedOrder,
                Source: SourceWeak,
                PlannedOrder: plannedOrder++,
                IsReview: true,
                IsBlocked: false,
                BlockedByObjectiveKey: null));
        }

        // 3. Review objectives from mastered keys (optional reinforcement).
        foreach (var masteredKey in masteredKeys.Take(2))
        {
            if (seenKeys.Contains(masteredKey))
                continue;

            seenKeys.Add(masteredKey);
            results.Add(new ObjectiveCandidate(
                ObjectiveKey: masteredKey,
                CefrLevel: cefrLevel,
                PrimarySkill: CurriculumSkillConstants.Speaking,
                SecondarySkills: [],
                ContextTags: [],
                Priority: plannedOrder,
                Source: SourceReview,
                PlannedOrder: plannedOrder++,
                IsReview: true,
                IsBlocked: false,
                BlockedByObjectiveKey: null));
        }

        return results;
    }

    /// <summary>
    /// Returns the skill rotation for plan objective generation.
    /// Cycles through core runnable skills to ensure variety in the plan.
    /// </summary>
    private static IReadOnlyList<string> GetSkillSequence(StudentProfile profile)
    {
        // Balanced skill rotation: mix all runnable skills across the plan.
        return
        [
            CurriculumSkillConstants.Speaking,
            CurriculumSkillConstants.Writing,
            CurriculumSkillConstants.Listening,
            CurriculumSkillConstants.Reading,
            CurriculumSkillConstants.Vocabulary,
            CurriculumSkillConstants.Speaking,
            CurriculumSkillConstants.Writing,
            CurriculumSkillConstants.Listening,
            CurriculumSkillConstants.Reading,
            CurriculumSkillConstants.Vocabulary,
        ];
    }

    private static LearningPlanSummary BuildSummary(StudentLearningPlan plan)
    {
        var objectives = plan.Objectives.ToList();
        var active = objectives.Where(o => o.Status == LearningPlanObjectiveStatus.Active && !o.IsReview).ToList();
        var review = objectives.Where(o => o.IsReview && o.Status == LearningPlanObjectiveStatus.Active).ToList();
        var blocked = objectives.Where(o => o.IsBlocked).ToList();
        var mastered = objectives.Where(o => o.Status == LearningPlanObjectiveStatus.Mastered).ToList();
        var completed = objectives.Where(o => o.Status == LearningPlanObjectiveStatus.Completed).ToList();

        var upcoming = active
            .OrderBy(o => o.PlannedOrder ?? int.MaxValue)
            .ThenBy(o => o.Priority)
            .Take(5)
            .Select(ToContext)
            .ToList();

        return new LearningPlanSummary(
            PlanId: plan.Id,
            StudentProfileId: plan.StudentProfileId,
            CefrLevel: plan.CefrLevelSnapshot,
            Status: plan.Status,
            RegenerationReason: plan.RegenerationReason,
            RegenerationCount: plan.RegenerationCount,
            TotalObjectives: objectives.Count,
            ActiveObjectives: active.Count,
            ReviewObjectives: review.Count,
            BlockedObjectives: blocked.Count,
            MasteredObjectives: mastered.Count,
            CompletedObjectives: completed.Count,
            PlannedLessonCount: plan.PlannedLessonCount,
            LastEvaluatedAt: plan.LastEvaluatedAt,
            UpcomingObjectives: upcoming);
    }

    private static PlannedObjectiveContext ToContext(StudentLearningPlanObjective o) =>
        new(
            ObjectiveKey: o.ObjectiveKey,
            CefrLevel: o.CefrLevel,
            PrimarySkill: o.Skill,
            SecondarySkills: [],
            ContextTags: string.IsNullOrWhiteSpace(o.Context) ? [] : [o.Context],
            IsReview: o.IsReview,
            Priority: o.Priority,
            Source: o.Source);

    private static PlannedObjectiveContext ToContext(ObjectiveCandidate c) =>
        new(
            ObjectiveKey: c.ObjectiveKey,
            CefrLevel: c.CefrLevel,
            PrimarySkill: c.PrimarySkill,
            SecondarySkills: c.SecondarySkills,
            ContextTags: c.ContextTags,
            IsReview: c.IsReview,
            Priority: c.Priority,
            Source: c.Source);

    private static string DeterminePhase(string cefrLevel, double masteryPct) =>
        (cefrLevel, masteryPct) switch
        {
            ("A1", _) => "Foundation",
            ("A2", < 50) => "Elementary — Building",
            ("A2", _) => "Elementary — Consolidating",
            ("B1", < 50) => "Intermediate — Developing",
            ("B1", _) => "Intermediate — Consolidating",
            ("B2", < 50) => "Upper-Intermediate — Developing",
            ("B2", _) => "Upper-Intermediate — Consolidating",
            ("C1", _) => "Advanced",
            ("C2", _) => "Proficient",
            _ => "Learning"
        };

    private sealed record ObjectiveCandidate(
        string ObjectiveKey,
        string CefrLevel,
        string PrimarySkill,
        IReadOnlyList<string> SecondarySkills,
        IReadOnlyList<string> ContextTags,
        int Priority,
        string Source,
        int? PlannedOrder,
        bool IsReview,
        bool IsBlocked,
        string? BlockedByObjectiveKey);
}
