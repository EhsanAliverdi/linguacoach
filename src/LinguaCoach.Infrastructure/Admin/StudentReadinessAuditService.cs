using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Progress;
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
/// Phase I2C: the readiness-pool-dependent checks (Practice Gym pool health/pilot gate,
/// activity-content pattern/malformed-content validity, listening-audio-for-ready-items, and the
/// stale/pending-reserved-item feedback checks) were removed along with StudentActivityReadinessItem
/// — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class StudentReadinessAuditService : IStudentReadinessAuditService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly IStudentProgressSummaryHandler _progressHandler;
    private readonly IStudentLearningLedger _ledger;
    private readonly IExerciseTypeRegistry _exerciseTypeRegistry;
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
        IExerciseTypeRegistry exerciseTypeRegistry,
        ILogger<StudentReadinessAuditService> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _progressHandler = progressHandler;
        _ledger = ledger;
        _exerciseTypeRegistry = exerciseTypeRegistry;
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
        AddPracticeGymCheck(checks, now);
        await AddAudioTtsChecksAsync(checks, profile, now, ct);
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
            types = await _exerciseTypeRegistry.GetGenerationEligibleAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness audit: GetGenerationEligibleAsync failed.");
            types = [];
        }

        var exerciseTypesOk = types.Count > 0;
        checks.Add(exerciseTypesOk
            ? Check("today.exercise_types_available", "Exercise types available", category, ReadinessCheckStatus.Pass, ReadinessCheckSeverity.Blocking,
                $"{types.Count} enabled, ready exercise type(s) available for generation.", now)
            : Check("today.exercise_types_available", "Exercise types available", category, ReadinessCheckStatus.Fail, ReadinessCheckSeverity.Blocking,
                "No enabled, ready exercise types exist — generation would fail.", now));

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

    /// <summary>
    /// Phase I2C: the readiness-pool health/pilot-gate checks that used to live here were removed
    /// along with StudentActivityReadinessItem. Practice Gym content is now served exclusively by
    /// the H7 module-selection pipeline (deterministic, no per-student pool to report health on) —
    /// see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
    /// </summary>
    private static void AddPracticeGymCheck(List<StudentReadinessCheckDto> checks, DateTime now)
    {
        const string category = "Practice Gym";
        checks.Add(Check("practicegym.module_based", "Practice Gym content source", category, ReadinessCheckStatus.NotApplicable, ReadinessCheckSeverity.Info,
            "Practice Gym is served by the module-selection pipeline (Phase H7); there is no per-student readiness pool to audit.", now));
    }

    // --- 7. Audio/TTS ---

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

        // Phase I2C: the "listening activities linked to ready/reserved readiness items have
        // audio" check was removed along with StudentActivityReadinessItem — there are no
        // ready/reserved readiness items to check anymore. See
        // docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
    }

    // --- 8. Progress/mastery ---

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
