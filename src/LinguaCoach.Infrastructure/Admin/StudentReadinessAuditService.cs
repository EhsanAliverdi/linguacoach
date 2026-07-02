using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Progress;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

/// <summary>
/// Phase 20D: read-only "can this student safely use the app end-to-end today?" audit.
/// Every check is either a cheap targeted query or a call to a service method already
/// documented/confirmed safe to call without side effects. Deliberately never calls
/// IGetTodaysSessionHandler or ILearningPlanService.GetOrCreatePlanAsync/RegeneratePlanAsync,
/// both of which mutate state — plan/session existence is checked via direct queries instead.
/// </summary>
public sealed class StudentReadinessAuditService : IStudentReadinessAuditService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly IStudentProgressSummaryHandler _progressHandler;
    private readonly IStudentLearningLedger _ledger;
    private readonly IReadinessPoolReplenishmentService _replenishment;
    private readonly IExerciseTypeRegistry _exerciseTypeRegistry;
    private readonly IEffectiveReadinessPoolSettingsProvider _settingsProvider;
    private readonly ILogger<StudentReadinessAuditService> _logger;

    private static readonly StudentLifecycleStage[] LearningReadyStages =
    [
        StudentLifecycleStage.CourseReady,
        StudentLifecycleStage.InLesson,
        StudentLifecycleStage.ActiveLearning,
        StudentLifecycleStage.Paused,
    ];

    public StudentReadinessAuditService(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        IStudentProgressSummaryHandler progressHandler,
        IStudentLearningLedger ledger,
        IReadinessPoolReplenishmentService replenishment,
        IExerciseTypeRegistry exerciseTypeRegistry,
        IEffectiveReadinessPoolSettingsProvider settingsProvider,
        ILogger<StudentReadinessAuditService> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _progressHandler = progressHandler;
        _ledger = ledger;
        _replenishment = replenishment;
        _exerciseTypeRegistry = exerciseTypeRegistry;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<StudentReadinessSummaryDto?> GetReadinessAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == studentProfileId, ct);
        if (profile is null) return null;

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == profile.UserId, ct);
        var now = DateTime.UtcNow;
        var checks = new List<StudentReadinessCheckDto>();

        AddAccountChecks(checks, profile, user, now);
        await AddPlacementChecksAsync(checks, profile, now, ct);
        await AddLearningPlanChecksAsync(checks, profile, now, ct);
        AddCourseReadinessCheck(checks, profile, now);
        await AddTodayLessonChecksAsync(checks, profile, now, ct);
        await AddPracticeGymChecksAsync(checks, profile, now, ct);
        await AddActivityContentChecksAsync(checks, profile, now, ct);
        await AddAudioTtsChecksAsync(checks, profile, now, ct);
        await AddFeedbackAndReviewScaffoldChecksAsync(checks, profile, now, ct);
        await AddProgressChecksAsync(checks, profile, now, ct);

        var blocking = checks.Count(c => c.Status == ReadinessCheckStatus.Fail && c.Severity == ReadinessCheckSeverity.Blocking);
        var warnings = checks.Count(c =>
            (c.Status == ReadinessCheckStatus.Warning || c.Status == ReadinessCheckStatus.Fail)
            && c.Severity == ReadinessCheckSeverity.Warning);
        var infos = checks.Count(c => c.Severity == ReadinessCheckSeverity.Info && c.Status != ReadinessCheckStatus.Pass);

        var readyForPilot = blocking == 0;
        var overall = blocking > 0
            ? ReadinessOverallStatus.Blocked
            : warnings > 0
                ? ReadinessOverallStatus.NeedsAttention
                : profile.OnboardingStatus == OnboardingStatus.NotStarted
                    ? ReadinessOverallStatus.NotStarted
                    : ReadinessOverallStatus.Ready;

        var lastRepair = await _db.AdminAuditLogs.AsNoTracking()
            .Where(a => a.TargetStudentId == studentProfileId && a.EntityType == "StudentReadinessRepair")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (DateTime?)a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var recommendedActions = checks
            .Where(c => c.CanRepair && c.RecommendedActionKey is not null)
            .Select(c => c.RecommendedActionKey!)
            .Distinct()
            .ToList();

        return new StudentReadinessSummaryDto
        {
            StudentId = studentProfileId,
            StudentEmail = user?.Email,
            GeneratedAtUtc = now,
            ReadyForPilot = readyForPilot,
            ReadinessStatus = overall,
            BlockingIssueCount = blocking,
            WarningCount = warnings,
            InfoCount = infos,
            LastRepairAtUtc = lastRepair,
            Checks = checks,
            RecommendedActions = recommendedActions,
            UnavailableSections = [],
        };
    }

    private static StudentReadinessCheckDto Check(
        string key, string displayName, string category, ReadinessCheckStatus status, ReadinessCheckSeverity severity,
        string message, DateTime now, string? technicalDetail = null, string? recommendedActionKey = null,
        ReadinessRepairRiskLevel? repairRiskLevel = null) => new()
    {
        Key = key,
        DisplayName = displayName,
        Category = category,
        Status = status,
        Severity = severity,
        Message = message,
        TechnicalDetail = technicalDetail,
        RecommendedActionKey = recommendedActionKey,
        CanRepair = recommendedActionKey is not null,
        RepairRiskLevel = repairRiskLevel,
        LastCheckedAtUtc = now,
    };

    // --- 1. Account & access ---

    private static void AddAccountChecks(List<StudentReadinessCheckDto> checks, StudentProfile profile, ApplicationUser? user, DateTime now)
    {
        const string category = "Account & access";

        checks.Add(user is null
            ? Check("account.user_exists", "Login account exists", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "No login account is linked to this student profile.", now)
            : Check("account.user_exists", "Login account exists", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                "A login account is linked to this student profile.", now));

        if (user is not null)
        {
            checks.Add(user.Role == UserRole.Student
                ? Check("account.role_is_student", "Account has Student role", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                    "Account role is Student.", now)
                : Check("account.role_is_student", "Account has Student role", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                    $"Account role is {user.Role}, not Student.", now));
        }

        checks.Add(profile.LifecycleStage == StudentLifecycleStage.Archived
            ? Check("account.not_archived", "Account is not archived", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "Student is archived and cannot log in (mirrors the login gate).", now)
            : Check("account.not_archived", "Account is not archived", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                "Student is not archived.", now));

        var stuckOnPasswordChange = user is { MustChangePassword: true }
            && profile.LifecycleStage != StudentLifecycleStage.Created
            && profile.LifecycleStage != StudentLifecycleStage.PasswordChangeRequired;
        checks.Add(stuckOnPasswordChange
            ? Check("account.password_change_state", "Force-password-change state is consistent", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Student has progressed past initial setup but still has a forced password change pending.", now)
            : Check("account.password_change_state", "Force-password-change state is consistent", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                "No inconsistent forced-password-change state detected.", now));
    }

    // --- 2. Placement & CEFR ---

    private async Task AddPlacementChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Placement & CEFR";

        var latestCompleted = await _db.PlacementAssessments.AsNoTracking()
            .Where(a => a.StudentProfileId == profile.Id && a.Status == PlacementStatus.Completed)
            .OrderByDescending(a => a.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        var placementRequired = profile.LifecycleStage >= StudentLifecycleStage.PlacementRequired;

        if (latestCompleted is not null)
        {
            checks.Add(Check("placement.status", "Placement completed", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                $"Placement completed at {latestCompleted.CompletedAtUtc:u}.", now));
        }
        else if (!placementRequired)
        {
            checks.Add(Check("placement.status", "Placement completed", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                "Placement not required yet at this lifecycle stage.", now));
        }
        else if (profile.LifecycleStage >= StudentLifecycleStage.CourseReady)
        {
            checks.Add(Check("placement.status", "Placement completed", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "Student is CourseReady or beyond but has no completed placement assessment.", now));
        }
        else
        {
            checks.Add(Check("placement.status", "Placement completed", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Placement is required but not yet completed.", now));
        }

        var validCefr = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        if (profile.CefrLevel is null)
        {
            checks.Add(placementRequired
                ? Check("placement.cefr_valid", "CEFR level is set", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                    "No CEFR level set yet.", now)
                : Check("placement.cefr_valid", "CEFR level is set", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                    "CEFR not required yet at this lifecycle stage.", now));
        }
        else
        {
            var core = profile.CefrLevel.TrimEnd('+', '-');
            checks.Add(validCefr.Contains(core, StringComparer.OrdinalIgnoreCase)
                ? Check("placement.cefr_valid", "CEFR level is set", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                    $"CEFR level is {profile.CefrLevel}.", now)
                : Check("placement.cefr_valid", "CEFR level is set", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                    $"CEFR level '{profile.CefrLevel}' does not normalize to a known band.", now));
        }

        if (latestCompleted is not null)
        {
            checks.Add(string.IsNullOrWhiteSpace(latestCompleted.SkillLevelsJson) || latestCompleted.SkillLevelsJson == "{}"
                ? Check("placement.per_skill_estimates", "Per-skill CEFR estimates exist", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Info,
                    "Placement completed but no per-skill estimates were recorded.", now)
                : Check("placement.per_skill_estimates", "Per-skill CEFR estimates exist", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                    "Per-skill CEFR estimates are present.", now));
        }
    }

    // --- 3. Learning Plan ---

    private async Task AddLearningPlanChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Learning Plan";

        if (profile.LifecycleStage < StudentLifecycleStage.CourseReady)
        {
            checks.Add(Check("learningplan.exists", "Learning Plan exists", category, ReadinessCheckStatus.NotApplicable, ReadinessCheckSeverity.Info,
                "Learning Plan is not expected before CourseReady.", now));
            return;
        }

        var exists = await _db.StudentLearningPlans.AsNoTracking()
            .AnyAsync(p => p.StudentProfileId == profile.Id
                && (p.Status == LearningPlanStatus.Active || p.Status == LearningPlanStatus.Regenerating), ct);

        checks.Add(exists
            ? Check("learningplan.exists", "Learning Plan exists", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                "An active Learning Plan exists.", now)
            : Check("learningplan.exists", "Learning Plan exists", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "No active Learning Plan exists for this CourseReady+ student.", now,
                recommendedActionKey: StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
                repairRiskLevel: ReadinessRepairRiskLevel.Low));

        if (!exists) return;

        try
        {
            var journey = await _learningPlan.GetJourneyAsync(profile.Id, ct);
            checks.Add(journey.TotalObjectives > 0
                ? Check("learningplan.has_objectives", "Learning Plan has objectives", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                    $"Learning Plan has {journey.TotalObjectives} objective(s).", now)
                : Check("learningplan.has_objectives", "Learning Plan has objectives", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                    "Learning Plan exists but has no objectives.", now));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: GetJourneyAsync failed for student {StudentId}.", profile.Id);
            checks.Add(Check("learningplan.has_objectives", "Learning Plan has objectives", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Could not load Learning Plan journey detail.", now, technicalDetail: ex.GetType().Name));
        }
    }

    // --- 4. Course readiness ---

    private static void AddCourseReadinessCheck(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now)
    {
        const string category = "Course readiness";
        var isLearningReady = Array.IndexOf(LearningReadyStages, profile.LifecycleStage) >= 0;

        if (!isLearningReady)
        {
            checks.Add(Check("course.lifecycle_stage", "Lifecycle stage", category, ReadinessCheckStatus.NotApplicable, ReadinessCheckSeverity.Info,
                $"Student is at lifecycle stage {profile.LifecycleStage} — not yet expected to be learning-ready. Not marked ready for pilot.", now));
            return;
        }

        var consistent = profile.OnboardingStatus == OnboardingStatus.Complete;
        checks.Add(consistent
            ? Check("course.lifecycle_consistent", "Lifecycle is consistent", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                "Lifecycle stage and onboarding status are consistent.", now)
            : Check("course.lifecycle_consistent", "Lifecycle is consistent", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                $"Lifecycle stage is {profile.LifecycleStage} but onboarding status is {profile.OnboardingStatus}.", now));
    }

    // --- 5. Today lesson ---

    private async Task AddTodayLessonChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Today lesson";

        if (profile.LifecycleStage < StudentLifecycleStage.CourseReady)
        {
            checks.Add(Check("today.exercise_types_available", "Exercise types available", category, ReadinessCheckStatus.NotApplicable, ReadinessCheckSeverity.Info,
                "Today lesson is not expected before CourseReady.", now));
            return;
        }

        IReadOnlyList<ExerciseTypeRegistryEntry> types;
        try
        {
            types = await _exerciseTypeRegistry.GetForTodayAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: GetForTodayAsync failed.");
            types = [];
        }

        var exerciseTypesOk = types.Count > 0;
        checks.Add(exerciseTypesOk
            ? Check("today.exercise_types_available", "Exercise types available", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                $"{types.Count} enabled exercise type(s) support Today lesson.", now)
            : Check("today.exercise_types_available", "Exercise types available", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "No enabled, ready exercise types support Today lesson — generation would fail.", now));

        var hasActivePath = await _db.LearningPaths.AsNoTracking().AnyAsync(p => p.StudentProfileId == profile.Id, ct);
        checks.Add(hasActivePath
            ? Check("today.session_ready_or_creatable", "Session ready or creatable", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "An active learning path exists; a session can be generated on demand.", now)
            : Check("today.session_ready_or_creatable", "Session ready or creatable", category,
                exerciseTypesOk ? ReadinessCheckStatus.Warning : ReadinessCheckStatus.Fail,
                exerciseTypesOk ? ReadinessCheckSeverity.Warning : ReadinessCheckSeverity.Blocking,
                "No learning path exists yet; one will be generated lazily on first Today-lesson request.", now,
                recommendedActionKey: StudentReadinessRepairActions.RefillTodayLessonIfEmpty,
                repairRiskLevel: ReadinessRepairRiskLevel.Low));

        var stuckCutoff = now.AddMinutes(-30);
        var stuckSessions = await _db.LearningSessions.AsNoTracking()
            .CountAsync(s => s.StudentProfileId == profile.Id
                && s.DeletedAtUtc == null
                && (s.GenerationStatus == GenerationStatus.Pending || s.GenerationStatus == GenerationStatus.Failed)
                && s.CreatedAt < stuckCutoff, ct);
        checks.Add(stuckSessions == 0
            ? Check("today.no_stuck_generating_session", "No stuck session generation", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "No sessions stuck in Pending/Failed generation.", now)
            : Check("today.no_stuck_generating_session", "No stuck session generation", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                $"{stuckSessions} session(s) stuck in Pending/Failed generation for over 30 minutes.", now));
    }

    // --- 6. Practice Gym ---

    private async Task AddPracticeGymChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Practice Gym";

        if (profile.LifecycleStage < StudentLifecycleStage.CourseReady)
        {
            checks.Add(Check("practicegym.pool_health", "Pool health", category, ReadinessCheckStatus.NotApplicable, ReadinessCheckSeverity.Info,
                "Practice Gym is not expected before CourseReady.", now));
            return;
        }

        try
        {
            await AddPracticeGymChecksCoreAsync(checks, profile, now, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: Practice Gym checks failed for student {StudentId}.", profile.Id);
            checks.Add(Check("practicegym.check_failed", "Practice Gym checks completed", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Could not fully evaluate Practice Gym readiness for this student.", now, technicalDetail: ex.GetType().Name));
        }
    }

    private async Task AddPracticeGymChecksCoreAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Practice Gym";

        var health = await _replenishment.GetHealthAsync(profile.Id, ReadinessPoolSource.PracticeGym, ct);
        // Nothing ready and nothing in flight, but failures are piling up — replenishment isn't
        // recovering on its own. NeedsReplenishment is not a useful signal here: with a nonzero
        // target and zero ready/in-flight items it is almost always true, so it can't distinguish
        // "healthy, about to replenish" from "stuck failing" on its own.
        var looksStuck = health.ReadyCount == 0 && health.QueuedOrGeneratingCount == 0 && health.FailedCount > 0;

        checks.Add(looksStuck
            ? Check("practicegym.pool_health", "Pool health", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                $"Practice Gym has {health.FailedCount} failed item(s) and nothing ready, but replenishment isn't recommended — generation may be stuck.", now,
                recommendedActionKey: StudentReadinessRepairActions.RefillPracticeGymIfEmpty,
                repairRiskLevel: ReadinessRepairRiskLevel.Medium)
            : Check("practicegym.pool_health", "Pool health", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                health.ReadyCount > 0
                    ? $"{health.ReadyCount} ready Practice Gym item(s)."
                    : "No ready items yet, but the pool is healthy and being replenished.", now));

        var effective = await _settingsProvider.GetEffectiveAsync(ct);
        checks.Add(Check("practicegym.pilot_gate_visibility", "Practice Gym pilot gate", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
            $"Practice Gym review-scaffold pilot is currently {(effective.PracticeGymPilotEnabled ? "enabled" : "disabled")}.", now));
    }

    // --- 7. Activity content validity ---

    private async Task AddActivityContentChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Activity content validity";

        try
        {
            await AddActivityContentChecksCoreAsync(checks, profile, now, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: activity content checks failed for student {StudentId}.", profile.Id);
            checks.Add(Check("activities.check_failed", "Activity content checks completed", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Could not fully evaluate activity content validity for this student.", now, technicalDetail: ex.GetType().Name));
        }
    }

    private async Task AddActivityContentChecksCoreAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Activity content validity";

        var activeItems = await _db.StudentActivityReadinessItems.AsNoTracking()
            .Where(i => i.StudentId == profile.Id
                && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.Reserved)
                && i.PatternKey != null)
            .Select(i => i.PatternKey!)
            .Distinct()
            .ToListAsync(ct);

        var invalidPatternCount = 0;
        if (activeItems.Count > 0)
        {
            var knownKeys = await _db.ExercisePatterns.AsNoTracking()
                .Where(p => activeItems.Contains(p.Key))
                .Select(p => p.Key)
                .ToListAsync(ct);
            invalidPatternCount = activeItems.Count(k => !knownKeys.Contains(k));
        }

        checks.Add(invalidPatternCount == 0
            ? Check("activities.pattern_keys_valid", "Ready items reference valid patterns", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "All ready/reserved readiness items reference a known exercise pattern.", now)
            : Check("activities.pattern_keys_valid", "Ready items reference valid patterns", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                $"{invalidPatternCount} ready/reserved item(s) reference an unrecognized exercise pattern.", now,
                recommendedActionKey: StudentReadinessRepairActions.ExpireInvalidReadinessItems,
                repairRiskLevel: ReadinessRepairRiskLevel.Low));

        var activityIds = await _db.StudentActivityReadinessItems.AsNoTracking()
            .Where(i => i.StudentId == profile.Id
                && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.Reserved)
                && i.LearningActivityId != null)
            .Select(i => i.LearningActivityId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var malformedCount = activityIds.Count == 0 ? 0 : await _db.LearningActivities.AsNoTracking()
            .CountAsync(a => activityIds.Contains(a.Id) && (a.AiGeneratedContentJson == null || a.AiGeneratedContentJson == "" || a.AiGeneratedContentJson == "{}"), ct);

        checks.Add(malformedCount == 0
            ? Check("activities.content_present", "Materialized activities have content", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "No materialized activities with empty generated content were found.", now)
            : Check("activities.content_present", "Materialized activities have content", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                $"{malformedCount} materialized activity/activities have empty/missing generated content.", now));
    }

    // --- 8. Audio/TTS ---

    private async Task AddAudioTtsChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Audio/TTS";

        try
        {
            await AddAudioTtsChecksCoreAsync(checks, profile, now, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: audio/TTS checks failed for student {StudentId}.", profile.Id);
            checks.Add(Check("audio.check_failed", "Audio/TTS checks completed", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Could not fully evaluate audio/TTS readiness for this student.", now, technicalDetail: ex.GetType().Name));
        }
    }

    private async Task AddAudioTtsChecksCoreAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Audio/TTS";

        var settings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var ttsEnabled = settings?.EnableTtsGeneration ?? true;

        checks.Add(Check("audio.tts_setting", "TTS generation setting", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
            $"TTS generation is currently {(ttsEnabled ? "enabled" : "disabled")}.", now));

        var listeningActivityIds = await (
            from item in _db.StudentActivityReadinessItems.AsNoTracking()
            join activity in _db.LearningActivities.AsNoTracking() on item.LearningActivityId equals activity.Id
            where item.StudentId == profile.Id
                && (item.Status == ReadinessPoolStatus.Ready || item.Status == ReadinessPoolStatus.Reserved)
                && activity.ActivityType == ActivityType.ListeningComprehension
            select activity.Id).Distinct().ToListAsync(ct);

        if (listeningActivityIds.Count == 0)
        {
            checks.Add(Check("audio.missing_for_listening", "Listening activities have audio", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "No ready/reserved listening activities to check.", now));
            return;
        }

        var readyAudioActivityIds = await _db.AudioAssets.AsNoTracking()
            .Where(a => a.LearningActivityId != null
                && listeningActivityIds.Contains(a.LearningActivityId.Value)
                && a.AssetType == AssetType.ListeningTts
                && a.GenerationStatus == GenerationStatus.Ready)
            .Select(a => a.LearningActivityId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var missingCount = listeningActivityIds.Count(id => !readyAudioActivityIds.Contains(id));

        checks.Add(missingCount == 0
            ? Check("audio.missing_for_listening", "Listening activities have audio", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "All ready/reserved listening activities have generated audio.", now)
            : Check("audio.missing_for_listening", "Listening activities have audio", category,
                ttsEnabled ? ReadinessCheckStatus.Warning : ReadinessCheckStatus.NotApplicable,
                ttsEnabled ? ReadinessCheckSeverity.Warning : ReadinessCheckSeverity.Info,
                ttsEnabled
                    ? $"{missingCount} listening activity/activities have no ready audio asset."
                    : $"{missingCount} listening activity/activities have no audio, but TTS generation is disabled by setting.", now));
    }

    // --- 9. Feedback/completion & review scaffold ---

    private async Task AddFeedbackAndReviewScaffoldChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Feedback/completion";

        try
        {
            await AddFeedbackAndReviewScaffoldChecksCoreAsync(checks, profile, now, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: feedback/review scaffold checks failed for student {StudentId}.", profile.Id);
            checks.Add(Check("feedback.check_failed", "Feedback/review scaffold checks completed", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                "Could not fully evaluate feedback/review scaffold state for this student.", now, technicalDetail: ex.GetType().Name));
        }
    }

    private async Task AddFeedbackAndReviewScaffoldChecksCoreAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Feedback/completion";
        var effective = await _settingsProvider.GetEffectiveAsync(ct);
        var reservedCutoff = now.AddHours(-effective.ReservedItemExpiryHours);

        var staleReserved = await _db.StudentActivityReadinessItems.AsNoTracking()
            .CountAsync(i => i.StudentId == profile.Id
                && i.Status == ReadinessPoolStatus.Reserved
                && i.ReservedAt != null && i.ReservedAt < reservedCutoff, ct);

        checks.Add(staleReserved == 0
            ? Check("feedback.no_stuck_reserved", "No stuck reserved items", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Warning,
                "No reserved items are past the reservation-expiry window.", now)
            : Check("feedback.no_stuck_reserved", "No stuck reserved items", category, ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Warning,
                $"{staleReserved} reserved item(s) are past the reservation-expiry window.", now,
                recommendedActionKey: StudentReadinessRepairActions.ExpireStaleReservedItems,
                repairRiskLevel: ReadinessRepairRiskLevel.Low));

        var staleCount = await _db.StudentActivityReadinessItems.AsNoTracking()
            .CountAsync(i => i.StudentId == profile.Id && i.Status == ReadinessPoolStatus.Stale, ct);

        checks.Add(staleCount == 0
            ? Check("reviewscaffold.stale_items", "No stale readiness items", "Review scaffold/readiness", ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                "No stale readiness items found.", now)
            : Check("reviewscaffold.stale_items", "No stale readiness items", "Review scaffold/readiness", ReadinessCheckStatus.Warning, ReadinessCheckSeverity.Info,
                $"{staleCount} readiness item(s) are marked Stale.", now,
                recommendedActionKey: StudentReadinessRepairActions.ExpireInvalidReadinessItems,
                repairRiskLevel: ReadinessRepairRiskLevel.Low));

        // PendingReview/Rejected items legitimately exist in Ready/ReviewOnly/Reserved status while
        // queued for admin decision — that's expected, not a bug. PassesAdminReviewGate is a computed
        // property (NotRequired/Approved only), so it can never itself report such an item as visible.
        // This check is deliberately informational only — it must never report a "not visible" item
        // as a Warning/Fail, per the requirement that the audit itself never marks pending/rejected
        // scaffold as student-visible.
        var pendingOrRejectedCount = await _db.StudentActivityReadinessItems.AsNoTracking()
            .CountAsync(i => i.StudentId == profile.Id
                && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.ReviewOnly || i.Status == ReadinessPoolStatus.Reserved)
                && i.RequiresAdminReview
                && (i.AdminReviewStatus == AdminReviewStatus.PendingReview || i.AdminReviewStatus == AdminReviewStatus.Rejected), ct);

        checks.Add(Check("reviewscaffold.pending_not_visible", "Pending/rejected scaffold not visible", "Review scaffold/readiness", ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
            pendingOrRejectedCount == 0
                ? "No pending/rejected review-scaffold items are queued for this student."
                : $"{pendingOrRejectedCount} pending/rejected review-scaffold item(s) queued — correctly hidden from the student by the admin-review gate.",
            now));
    }

    // --- 10. Progress/mastery ---

    private async Task AddProgressChecksAsync(List<StudentReadinessCheckDto> checks, StudentProfile profile, DateTime now, CancellationToken ct)
    {
        const string category = "Progress/mastery";

        try
        {
            await _progressHandler.HandleAsync(new Application.Progress.GetStudentProgressSummaryQuery(profile.UserId));
            checks.Add(Check("progress.summary_loads", "Progress summary loads", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                "Progress summary loaded without error.", now));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: progress summary failed for student {StudentId}.", profile.Id);
            checks.Add(Check("progress.summary_loads", "Progress summary loads", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "Progress summary failed to load.", now, technicalDetail: ex.GetType().Name));
        }

        try
        {
            await _ledger.GetRecentAsync(profile.Id, limit: 1, ct);
            checks.Add(Check("progress.ledger_baseline_ok", "Learning ledger accessible", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Info,
                "Learning ledger is accessible (empty history is a valid baseline).", now));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: ledger read failed for student {StudentId}.", profile.Id);
            checks.Add(Check("progress.ledger_baseline_ok", "Learning ledger accessible", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Warning,
                "Learning ledger read failed.", now, technicalDetail: ex.GetType().Name));
        }
    }
}
