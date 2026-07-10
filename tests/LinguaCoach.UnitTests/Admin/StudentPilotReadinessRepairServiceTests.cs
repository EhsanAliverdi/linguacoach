using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinguaCoach.UnitTests.Admin;

/// <summary>
/// Unit tests for StudentPilotReadinessRepairService (Phase 20D). Proves dry-run/reason/audit/
/// idempotency requirements and that repairs never touch attempts/submissions/evaluations.
/// Phase I2C: the readiness-pool-specific repair actions (ExpireInvalidReadinessItems,
/// ExpireStaleReservedItems) were removed along with StudentActivityReadinessItem — see
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. Remaining coverage
/// exercises the two surviving actions (GenerateLearningPlanIfMissing, RefillTodayLessonIfEmpty).
/// </summary>
public sealed class StudentPilotReadinessRepairServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeLearningPlanService _learningPlan = new();
    private readonly FakeTodaysSessionHandler _todaysSession = new();
    private static readonly Guid AdminId = Guid.NewGuid();

    public StudentPilotReadinessRepairServiceTests()
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

    private StudentPilotReadinessRepairService BuildSut() => new(
        _db, _learningPlan, _todaysSession,
        NullLogger<StudentPilotReadinessRepairService>.Instance);

    private StudentProfile SeedStudent()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        return student;
    }

    [Fact]
    public async Task DryRun_MakesNoDbChanges()
    {
        var student = SeedStudent();
        var sut = BuildSut();

        var result = await sut.RepairAsync(student.Id, AdminId, new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            DryRun = true,
        });

        Assert.Equal(1, result.ChangedCount);
        Assert.False(_db.AdminAuditLogs.Any());
        Assert.False(_learningPlan.WasCalled);
    }

    [Fact]
    public async Task RealRepair_WithoutReason_Throws()
    {
        var student = SeedStudent();
        var sut = BuildSut();

        await Assert.ThrowsAsync<ArgumentException>(() => sut.RepairAsync(student.Id, AdminId, new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            DryRun = false,
            Reason = "",
        }));
    }

    [Fact]
    public async Task RealRepair_WritesOneAuditLog()
    {
        var student = SeedStudent();
        var sut = BuildSut();

        var result = await sut.RepairAsync(student.Id, AdminId, new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            DryRun = false,
            Reason = "Generating for pilot test.",
        });

        Assert.Equal(1, result.ChangedCount);
        Assert.NotNull(result.AuditLogId);
        var logs = _db.AdminAuditLogs.Where(a => a.TargetStudentId == student.Id).ToList();
        Assert.Single(logs);
        Assert.Equal("RepairStudentReadiness", logs[0].Action);
        Assert.True(_learningPlan.WasCalled);
    }

    [Fact]
    public async Task Repair_IsIdempotent()
    {
        var student = SeedStudent();
        _db.StudentLearningPlans.Add(new StudentLearningPlan(student.Id, "B2", "initial_generation"));
        _db.SaveChanges();
        var sut = BuildSut();
        var request = new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            DryRun = false,
            Reason = "First run.",
        };

        var first = await sut.RepairAsync(student.Id, AdminId, request);
        var second = await sut.RepairAsync(student.Id, AdminId, request with { Reason = "Second run." });

        Assert.Equal(0, first.ChangedCount);
        Assert.Equal(1, first.SkippedCount);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(1, second.SkippedCount);
    }

    [Fact]
    public async Task Repair_NeverTouchesAttemptsOrAudioAssets()
    {
        var student = SeedStudent();
        var activity = new LearningActivity(ActivityType.ListeningComprehension, ActivitySource.AiGenerated, "Listening", "B2", "{}");
        _db.LearningActivities.Add(activity);
        _db.ActivityAttempts.Add(new ActivityAttempt(student.Id, activity.Id, "answer", "{}", "prompt_key"));
        _db.AudioAssets.Add(new AudioAsset(student.Id, AssetType.ListeningTts, "key/path", "audio/mpeg"));
        _db.SaveChanges();
        var attemptCountBefore = _db.ActivityAttempts.Count();
        var audioCountBefore = _db.AudioAssets.Count();

        var sut = BuildSut();
        await sut.RepairAsync(student.Id, AdminId, new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            DryRun = false,
            Reason = "Verify no collateral damage.",
        });

        Assert.Equal(attemptCountBefore, _db.ActivityAttempts.Count());
        Assert.Equal(audioCountBefore, _db.AudioAssets.Count());
    }

    [Fact]
    public async Task RunAllSafeRepairs_OnlyRunsTheTwoImplementedActions()
    {
        var student = SeedStudent();
        var sut = BuildSut();
        var results = await sut.RunAllSafeRepairsAsync(student.Id, AdminId, "Run all safe repairs.", dryRun: true);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.ActionKey == StudentReadinessRepairActions.GenerateLearningPlanIfMissing);
        Assert.Contains(results, r => r.ActionKey == StudentReadinessRepairActions.RefillTodayLessonIfEmpty);
        Assert.DoesNotContain(results, r => r.ActionKey == StudentReadinessRepairActions.RefillPracticeGymIfEmpty);
    }

    [Fact]
    public async Task UnimplementedAction_Throws()
    {
        var student = SeedStudent();
        var sut = BuildSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RepairAsync(student.Id, AdminId, new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.RefillPracticeGymIfEmpty,
            DryRun = true,
        }));
    }

    [Fact]
    public async Task GenerateLearningPlan_AlreadyExists_IsIdempotentNoOp()
    {
        var student = SeedStudent();
        _db.StudentLearningPlans.Add(new StudentLearningPlan(student.Id, "B2", "initial_generation"));
        _db.SaveChanges();

        var sut = BuildSut();
        var result = await sut.RepairAsync(student.Id, AdminId, new StudentReadinessRepairRequestDto
        {
            ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            DryRun = false,
            Reason = "Should be a no-op.",
        });

        Assert.Equal(0, result.ChangedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.False(_learningPlan.WasCalled);
    }

    // --- fakes ---

    private sealed class FakeLearningPlanService : ILearningPlanService
    {
        public bool WasCalled { get; private set; }

        public Task<StudentJourneyResult> GetJourneyAsync(Guid studentProfileId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<StudentJourneyResult> GetJourneyForUserAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<LearningPlanSummary> GetOrCreatePlanAsync(Guid studentProfileId, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new LearningPlanSummary(
                Guid.NewGuid(), studentProfileId, "B2", LearningPlanStatus.Active, "initial_generation",
                0, 5, 5, 0, 0, 0, 0, 10, null, []));
        }

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

    private sealed class FakeTodaysSessionHandler : IGetTodaysSessionHandler
    {
        public Task<TodaysSessionResult> HandleAsync(GetTodaysSessionQuery query, CancellationToken ct = default) =>
            throw new InvalidOperationException("No enabled ready exercise types are available for today's lesson.");
    }
}
