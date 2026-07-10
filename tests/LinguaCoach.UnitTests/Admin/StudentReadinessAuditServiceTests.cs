using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Progress;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinguaCoach.UnitTests.Admin;

/// <summary>
/// Unit tests for StudentReadinessAuditService (Phase 20D). Uses SQLite in-memory for real
/// data queries and lightweight fakes for the collaborating services so specific readiness
/// scenarios can be triggered deterministically.
/// Phase I2C: the readiness-pool-dependent checks (Practice Gym pool health/pilot gate,
/// activity-content pattern/malformed-content validity, listening-audio-for-ready-items, and the
/// stale/pending-reserved-item feedback checks) were removed along with StudentActivityReadinessItem
/// — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. Tests that exercised
/// those checks were removed with them.
/// </summary>
public sealed class StudentReadinessAuditServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private FakeLearningPlanService _learningPlan = new();
    private FakeProgressHandler _progressHandler = new();
    private FakeLedger _ledger = new();
    private FakeExerciseTypeRegistry _exerciseTypes = new();

    public StudentReadinessAuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private StudentReadinessAuditService BuildSut() => new(
        _db, _learningPlan, _progressHandler, _ledger, _exerciseTypes,
        NullLogger<StudentReadinessAuditService>.Instance);

    private StudentProfile SeedCourseReadyStudent(string cefr = "B2")
    {
        var student = new StudentProfile(Guid.NewGuid());
        student.SetLifecycleStage(StudentLifecycleStage.CourseReady);
        typeof(StudentProfile).GetProperty(nameof(StudentProfile.OnboardingStatus))!
            .SetValue(student, OnboardingStatus.Complete);
        typeof(StudentProfile).GetProperty(nameof(StudentProfile.CefrLevel))!.SetValue(student, cefr);
        _db.StudentProfiles.Add(student);

        var user = new ApplicationUser
        {
            Id = student.UserId,
            UserName = $"{student.UserId:N}@test.com",
            NormalizedUserName = $"{student.UserId:N}@TEST.COM",
            Email = $"{student.UserId:N}@test.com",
            NormalizedEmail = $"{student.UserId:N}@TEST.COM",
            Role = UserRole.Student,
        };
        _db.Users.Add(user);

        _db.PlacementAssessments.Add(new PlacementAssessment(student.Id, "intro"));
        var placement = _db.PlacementAssessments.Local.First(p => p.StudentProfileId == student.Id);
        placement.Complete("{}", cefr, "{\"writing\":\"B2\"}");

        _db.StudentLearningPlans.Add(new StudentLearningPlan(student.Id, cefr, "initial_generation"));

        _db.SaveChanges();
        return student;
    }

    [Fact]
    public async Task CleanCourseReadyStudent_ReadyForPilot()
    {
        _exerciseTypes.ForToday = [MakeExerciseType()];
        var student = SeedCourseReadyStudent();
        _db.LearningPaths.Add(new LinguaCoach.Domain.Entities.LearningPath(student.Id, "Workplace English", "desc"));
        _db.SaveChanges();

        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(student.Id);

        Assert.NotNull(summary);
        Assert.True(summary!.ReadyForPilot);
        Assert.Equal(0, summary.BlockingIssueCount);
    }

    [Fact]
    public async Task UnknownStudent_ReturnsNull()
    {
        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(Guid.NewGuid());
        Assert.Null(summary);
    }

    [Fact]
    public async Task MissingLearningPlanForCourseReadyStudent_IsBlockingFail()
    {
        var student = SeedCourseReadyStudent();
        _db.StudentLearningPlans.RemoveRange(_db.StudentLearningPlans.Where(p => p.StudentProfileId == student.Id));
        _db.SaveChanges();

        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(student.Id);

        Assert.False(summary!.ReadyForPilot);
        var check = summary.Checks.Single(c => c.Key == "learningplan.exists");
        Assert.Equal(ReadinessCheckStatus.Fail, check.Status);
        Assert.Equal(ReadinessCheckSeverity.Blocking, check.Severity);
        Assert.Equal(StudentReadinessRepairActions.GenerateLearningPlanIfMissing, check.RecommendedActionKey);
    }

    [Fact]
    public async Task NoAvailableExerciseTypes_TodayLessonCheckIsBlockingFail()
    {
        _exerciseTypes.ForToday = [];
        var student = SeedCourseReadyStudent();

        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(student.Id);

        var check = summary!.Checks.Single(c => c.Key == "today.exercise_types_available");
        Assert.Equal(ReadinessCheckStatus.Fail, check.Status);
        Assert.Equal(ReadinessCheckSeverity.Blocking, check.Severity);
        Assert.False(summary.ReadyForPilot);
    }

    [Fact]
    public async Task PracticeGymCheck_IsModuleBasedNotApplicable()
    {
        _exerciseTypes.ForToday = [MakeExerciseType()];
        var student = SeedCourseReadyStudent();

        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(student.Id);

        var check = summary!.Checks.Single(c => c.Key == "practicegym.module_based");
        Assert.Equal(ReadinessCheckStatus.NotApplicable, check.Status);
        Assert.Equal(ReadinessCheckSeverity.Info, check.Severity);
    }

    [Fact]
    public async Task TtsSetting_ReportsCurrentValue()
    {
        _exerciseTypes.ForToday = [MakeExerciseType()];
        var student = SeedCourseReadyStudent();
        _db.LessonGenerationSettings.Add(new LessonGenerationSettings());
        _db.SaveChanges();

        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(student.Id);

        var check = summary!.Checks.Single(c => c.Key == "audio.tts_setting");
        Assert.Equal(ReadinessCheckStatus.Pass, check.Status); // TTS enabled by default
    }

    [Fact]
    public async Task EmptyLedger_IsPass_NotCrash()
    {
        _exerciseTypes.ForToday = [MakeExerciseType()];
        var student = SeedCourseReadyStudent();

        var sut = BuildSut();
        var summary = await sut.GetReadinessAsync(student.Id);

        var check = summary!.Checks.Single(c => c.Key == "progress.ledger_baseline_ok");
        Assert.Equal(ReadinessCheckStatus.Pass, check.Status);
    }

    private static ExerciseTypeRegistryEntry MakeExerciseType() => new(
        Key: "writing_scenario", DisplayName: "Writing", Description: "d", PrimarySkill: "writing",
        SecondarySkills: [], Category: "writing", IsEnabled: true, ImplementationStatus: "ready",
        IsAvailableForGeneration: true, RendererKey: "writing", EvaluatorKey: "writing",
        GenerationPromptKey: "writing", LegacyActivityType: ActivityType.WritingScenario,
        ExercisePatternKey: null, EstimatedDurationMinutes: 10, RequiresAudio: false, RequiresImage: false,
        SupportsPracticeGym: true, SupportsTodayLesson: true);

    // --- fakes ---

    private sealed class FakeLearningPlanService : ILearningPlanService
    {
        public StudentJourneyResult Journey { get; set; } = new(
            "B2", "Preparing", 0, 0, null, null, [], [], [], [], "Active");

        public Task<StudentJourneyResult> GetJourneyAsync(Guid studentProfileId, CancellationToken ct = default) =>
            Task.FromResult(Journey);

        public Task<StudentJourneyResult> GetJourneyForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Journey);

        public Task<LearningPlanSummary> GetOrCreatePlanAsync(Guid studentProfileId, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by the audit service.");

        public Task<LearningPlanSummary> RegeneratePlanAsync(Guid studentProfileId, string reason, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<LearningPlanProgressSummary> GetProgressAsync(Guid studentProfileId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PlannedObjectiveContext?> GetNextPlannedObjectiveAsync(Guid studentProfileId, string? preferredSkill = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PlannedObjectiveContext>> GetPracticeGymObjectivesAsync(Guid studentProfileId, int maxCount = 5, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task MarkObjectiveInProgressAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task MarkObjectiveCompletedAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task MarkObjectiveMasteredAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<LearningPlanObjectiveProgressUpdate> TryUpdateObjectiveProgressAsync(Guid studentProfileId, string objectiveKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProgressHandler : IStudentProgressSummaryHandler
    {
        public bool ShouldThrow { get; set; }

        public Task<StudentProgressSummaryDto> HandleAsync(GetStudentProgressSummaryQuery query, CancellationToken ct = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("simulated failure");
            return Task.FromResult(new StudentProgressSummaryDto(
                new StudentProgressLearningSummaryDto(null, null, "Preparing", 0, 0, 0, 0, 0, 0, null, null, 0),
                [], new StudentProgressCefrDto(null, null, false, null, null),
                new StudentProgressMasteryDto(0, 0, 0, 0, []), [], new StudentProgressFocusDto([], [], null)));
        }
    }

    private sealed class FakeLedger : IStudentLearningLedger
    {
        public Task RecordAsync(StudentLearningEvent learningEvent, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<StudentLearningEvent>> GetRecentAsync(Guid studentProfileId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentLearningEvent>>([]);
        public Task<IReadOnlyList<StudentLearningEvent>> GetRecentByPatternKeysAsync(Guid studentProfileId, IEnumerable<string> patternKeys, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentLearningEvent>>([]);
        public Task<IReadOnlyList<string>> GetRecentPatternKeysAsync(Guid studentProfileId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<StudentLearningEvent>> GetWeakEventsAsync(Guid studentProfileId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudentLearningEvent>>([]);
    }

    private sealed class FakeExerciseTypeRegistry : IExerciseTypeRegistry
    {
        public IReadOnlyList<ExerciseTypeRegistryEntry> ForToday { get; set; } = [];

        public Task<ExerciseTypeRegistryEntry?> GetByKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetGenerationEligibleAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetByPrimarySkillAsync(string primarySkill, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetForPracticeGymAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetForTodayAsync(CancellationToken ct = default) =>
            Task.FromResult(ForToday);
        public Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetEligibleExerciseTypesForSkillAsync(
            string primarySkill, ExerciseTypeSupportContext supportContext = ExerciseTypeSupportContext.Any, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<ExerciseTypeRegistryEntry?> SelectForPracticeGymSkillAsync(string primarySkill, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<string?> ResolveRendererKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<string?> ResolveEvaluatorKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<string?> ResolveGenerationPromptKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<ActivityType?> ResolveLegacyActivityTypeAsync(string exerciseTypeKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<string?> ResolveExercisePatternKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
