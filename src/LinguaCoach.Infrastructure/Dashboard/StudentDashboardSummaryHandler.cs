using LinguaCoach.Application.Dashboard;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.Sessions;

namespace LinguaCoach.Infrastructure.Dashboard;

public sealed class StudentDashboardSummaryHandler : IStudentDashboardSummaryHandler
{
    private readonly IDashboardQueryHandler _dashboardHandler;
    private readonly IGetTodaysSessionHandler _sessionHandler;
    private readonly IPracticeGymSuggestionService _practiceService;
    private readonly IStudentMemoryQuery _memoryQuery;

    public StudentDashboardSummaryHandler(
        IDashboardQueryHandler dashboardHandler,
        IGetTodaysSessionHandler sessionHandler,
        IPracticeGymSuggestionService practiceService,
        IStudentMemoryQuery memoryQuery)
    {
        _dashboardHandler = dashboardHandler;
        _sessionHandler = sessionHandler;
        _practiceService = practiceService;
        _memoryQuery = memoryQuery;
    }

    public async Task<StudentDashboardSummaryResult> HandleAsync(
        StudentDashboardSummaryQuery query, CancellationToken ct = default)
    {
        // Core data is required; propagates InvalidOperationException for incomplete onboarding.
        var core = await _dashboardHandler.HandleAsync(new DashboardQuery(query.UserId), ct);

        bool isCourseActive = core.LifecycleStage is "CourseReady" or "InLesson" or "ActiveLearning";

        // Today's session — optional, failure maps to Preparing/NotAvailable.
        TodaysSessionResult? todaySession = null;
        bool sessionFailed = false;
        if (isCourseActive)
        {
            try
            {
                todaySession = await _sessionHandler.HandleAsync(
                    new GetTodaysSessionQuery(query.UserId), ct);
            }
            catch
            {
                sessionFailed = true;
            }
        }

        // Practice suggestions — optional.
        PracticeGymSuggestionsDto? practice = null;
        bool practiceFailed = false;
        try
        {
            practice = await _practiceService.GetSuggestionsForStudentAsync(query.UserId, ct);
        }
        catch
        {
            practiceFailed = true;
        }

        // Learning memory — optional.
        StudentLearningMemoryDto? memory = null;
        try
        {
            memory = await _memoryQuery.GetForUserAsync(query.UserId, ct);
        }
        catch { }

        return Build(core, todaySession, sessionFailed, practice, practiceFailed, memory);
    }

    private static StudentDashboardSummaryResult Build(
        DashboardResult core,
        TodaysSessionResult? session,
        bool sessionFailed,
        PracticeGymSuggestionsDto? practice,
        bool practiceFailed,
        StudentLearningMemoryDto? memory)
    {
        bool isCourseActive = core.LifecycleStage is "CourseReady" or "InLesson" or "ActiveLearning";
        bool placementGated = core.LifecycleStage is "PlacementRequired" or "PlacementInProgress";

        var profile = new DashboardSummaryProfile(
            DisplayName: core.StudentName,
            CefrLevel: core.CefrLevel,
            SupportLanguage: null);

        var courseReadiness = new DashboardSummaryCourseReadiness(
            IsLearningReady: isCourseActive,
            LifecycleStatus: core.LifecycleStage,
            PlacementRequired: placementGated,
            LearningPlanExists: core.LearningPath is not null);

        var todaySessionSection = BuildTodaySession(session, sessionFailed, isCourseActive);
        var learningPlanSection = BuildLearningPlan(core);
        var practiceSection = BuildPractice(practice, practiceFailed);
        var progressSection = BuildProgress(core, memory);

        var quickStats = new DashboardSummaryQuickStats(
            CurrentCefr: core.CefrLevel,
            StreakDays: core.StreakDays,
            ActivitiesCompleted: core.ActivityStats?.ActivitiesCompleted ?? 0,
            ReviewQueueCount: practice?.ReviewItems.Count ?? 0);

        var warnings = new DashboardSummaryWarnings(
            MissingLearningPlan: core.LearningPath is null && isCourseActive,
            MissingTodaySession: isCourseActive && !sessionFailed && (session is null || !session.Available),
            PracticeUnavailable: practiceFailed,
            PlacementIncomplete: placementGated);

        return new StudentDashboardSummaryResult(
            profile, courseReadiness, todaySessionSection,
            learningPlanSection, practiceSection, progressSection,
            quickStats, warnings);
    }

    /// <summary>
    /// Phase I2B — Today is module-only now: <see cref="TodaysSessionResult"/> no longer carries a
    /// LearningSession/SessionExercise shape, so this section is built from the Daily Lesson
    /// Module selection instead. "Ready" only when a module was actually selected;
    /// otherwise "NotAvailable" — never a stale/legacy shape. SessionId is always null (there is
    /// no session concept left on this path); ExerciseCount reports the selected module's linked
    /// Lessons + Exercises as a rough size indicator for the summary card.
    /// </summary>
    private static DashboardSummaryTodaySession BuildTodaySession(
        TodaysSessionResult? session, bool failed, bool courseActive)
    {
        if (!courseActive || failed)
            return new DashboardSummaryTodaySession(
                "NotAvailable", null, null, null, null, null, null, null,
                "Start today's lesson");

        if (session is null)
            return new DashboardSummaryTodaySession(
                "Preparing", null, null, null, null, null, null, null,
                "Start today's lesson");

        var selected = session.ModuleSection?.SelectedModules.FirstOrDefault();
        if (!session.Available || selected is null)
            return new DashboardSummaryTodaySession(
                "NotAvailable", null, null, null, null, null, null, null,
                "Nothing available yet");

        return new DashboardSummaryTodaySession(
            Status: "Ready",
            SessionId: null,
            Title: selected.Title,
            Topic: selected.Description,
            SessionGoal: selected.Reason,
            FocusSkill: selected.Skill,
            DurationMinutes: selected.EstimatedMinutes,
            ExerciseCount: selected.LinkedLessons.Count + selected.LinkedExercises.Count,
            ActionLabel: "Start today's lesson");
    }

    private static DashboardSummaryLearningPlan BuildLearningPlan(DashboardResult core)
    {
        var path = core.LearningPath;
        if (path is null)
            return new DashboardSummaryLearningPlan(null, null, null, 0, 0, 0, 0, 0, 0, 0);

        var mod = path.CurrentModule;
        int remaining = Math.Max(0, path.TotalModules - path.ModulesCompleted - 1);
        int progressPct = path.TotalModules > 0
            ? (int)Math.Round((double)path.ModulesCompleted / path.TotalModules * 100)
            : 0;

        return new DashboardSummaryLearningPlan(
            PathTitle: path.Title,
            CurrentObjective: mod?.Title,
            CurrentObjectiveDescription: mod?.Description,
            ObjectiveIndex: mod?.Order ?? (path.ModulesCompleted + 1),
            TotalObjectives: path.TotalModules,
            ModulesCompleted: path.ModulesCompleted,
            RemainingObjectives: remaining,
            CompletedActivities: mod?.CompletedActivities ?? 0,
            TotalActivities: mod?.TotalActivities ?? 0,
            ProgressPercent: progressPct);
    }

    private static DashboardSummaryPractice BuildPractice(
        PracticeGymSuggestionsDto? dto, bool failed)
    {
        if (failed)
            return new DashboardSummaryPractice("NotAvailable", null, 0, null);

        if (dto is null)
            return new DashboardSummaryPractice("Preparing", null, 0, null);

        var active = dto.SuggestedItems.Concat(dto.ContinueItems).ToList();
        var practiceStatus = active.Count > 0 ? "Ready" : "Preparing";

        DashboardSummaryPracticeItem? suggestedItem = null;
        if (active.Count > 0)
        {
            var first = active[0];
            suggestedItem = new DashboardSummaryPracticeItem(
                first.ReadinessItemId, first.Title, first.Description,
                first.PrimarySkill, first.CallToAction);
        }

        return new DashboardSummaryPractice(
            Status: practiceStatus,
            SuggestedItem: suggestedItem,
            ReviewQueueCount: dto.ReviewItems.Count,
            WeakestSkill: dto.ReviewItems.FirstOrDefault()?.PrimarySkill);
    }

    private static DashboardSummaryProgress BuildProgress(
        DashboardResult core, StudentLearningMemoryDto? memory)
    {
        var skillProfile = memory?.SkillProfile
            .Select(s => new DashboardSummarySkillItem(
                s.SkillKey, s.SkillLabel, s.IsWeak, s.ScorePercent))
            .ToList()
            as IReadOnlyList<DashboardSummarySkillItem>
            ?? [];

        return new DashboardSummaryProgress(
            SkillProfile: skillProfile,
            StrongSkills: memory?.StrongSkills ?? [],
            WeakSkills: memory?.WeakSkills ?? [],
            NextRecommendedFocus: memory?.NextRecommendedFocus ?? [],
            JourneySummary: memory?.JourneySummary,
            ActivitiesCompleted: core.ActivityStats?.ActivitiesCompleted ?? 0,
            StreakDays: core.StreakDays);
    }
}
