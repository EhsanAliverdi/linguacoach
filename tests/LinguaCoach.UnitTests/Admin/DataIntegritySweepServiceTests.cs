using FluentAssertions;
using LinguaCoach.Application.Admin;
using LinguaCoach.Application.AdminRepair;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Admin;

/// <summary>
/// Sprint 11 — unified data-integrity sweep. Uses SQLite in-memory plus fakes for the four
/// existing per-entity repair services (Module/Lesson/Exercise/Resource Bank), since their real
/// implementations depend on AI infrastructure irrelevant to this sweep's own new logic (orphan
/// detection + aggregation) — matching this repo's "tests use fake providers, never real AI"
/// convention.
/// </summary>
public sealed class DataIntegritySweepServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeModuleRepairService _moduleRepair = new(totalItems: 5, itemsWithIssues: 0);
    private readonly FakeLessonRepairService _lessonRepair = new(totalItems: 3, itemsWithIssues: 1);
    private readonly FakeExerciseRepairService _exerciseRepair = new(totalItems: 10, itemsWithIssues: 0);
    private readonly FakeResourceBankRepairService _resourceBankRepair = new(totalItems: 180, itemsWithIssues: 2);
    private readonly DataIntegritySweepService _sut;

    public DataIntegritySweepServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new DataIntegritySweepService(_db, _moduleRepair, _lessonRepair, _exerciseRepair, _resourceBankRepair);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private StudentProfile SeedStudent()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(profile);
        _db.SaveChanges();
        return profile;
    }

    [Fact]
    public async Task Reports_zero_orphans_and_healthy_for_a_clean_database()
    {
        var result = await _sut.RunAsync();

        var objectiveCategory = result.Categories.Single(c => c.Category == "Learning Plan Objectives");
        objectiveCategory.IssuesFound.Should().Be(0);
        objectiveCategory.Healthy.Should().BeTrue();

        var attemptCategory = result.Categories.Single(c => c.Category == "Activity Attempts");
        attemptCategory.IssuesFound.Should().Be(0);
        attemptCategory.Healthy.Should().BeTrue();

        var launchCategory = result.Categories.Single(c => c.Category == "Exercise Launches");
        launchCategory.IssuesFound.Should().Be(0);
        launchCategory.Healthy.Should().BeTrue();
    }

    [Fact]
    public async Task Detects_an_activity_attempt_orphaned_by_a_deleted_student_profile()
    {
        var profile = SeedStudent();
        var activity = new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.AiGenerated, "Test Activity", "B1", "{}");
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();
        var attempt = new ActivityAttempt(profile.Id, activity.Id, "content", "{}", "prompt_key");
        _db.ActivityAttempts.Add(attempt);
        _db.SaveChanges();

        // Detach the profile row directly via raw SQL, bypassing the app-level FK the domain
        // model would otherwise enforce — simulates a hypothetical future FK regression or manual
        // DB tampering, which is exactly the scenario this sweep exists to catch.
        _db.Database.ExecuteSqlRaw(
            "PRAGMA foreign_keys = OFF; DELETE FROM student_profiles WHERE id = {0};", profile.Id);

        var result = await _sut.RunAsync();

        var attemptCategory = result.Categories.Single(c => c.Category == "Activity Attempts");
        attemptCategory.IssuesFound.Should().Be(1);
        attemptCategory.Healthy.Should().BeFalse();
        attemptCategory.TotalChecked.Should().Be(1);
    }

    [Fact]
    public async Task Aggregates_the_existing_per_entity_issue_counts_from_the_four_repair_services()
    {
        var result = await _sut.RunAsync();

        result.Categories.Single(c => c.Category == "Modules").Should().BeEquivalentTo(
            new DataIntegrityCategoryResult("Modules", "", 5, 0, true), opt => opt.Excluding(c => c.Description));
        result.Categories.Single(c => c.Category == "Lessons").Should().BeEquivalentTo(
            new DataIntegrityCategoryResult("Lessons", "", 3, 1, false), opt => opt.Excluding(c => c.Description));
        result.Categories.Single(c => c.Category == "Exercises").Should().BeEquivalentTo(
            new DataIntegrityCategoryResult("Exercises", "", 10, 0, true), opt => opt.Excluding(c => c.Description));
        result.Categories.Single(c => c.Category == "Resource Bank").Should().BeEquivalentTo(
            new DataIntegrityCategoryResult("Resource Bank", "", 180, 2, false), opt => opt.Excluding(c => c.Description));
    }

    [Fact]
    public async Task AllHealthy_is_false_when_any_category_has_issues()
    {
        // _lessonRepair and _resourceBankRepair fakes are seeded with issues > 0.
        var result = await _sut.RunAsync();

        result.AllHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task AllHealthy_is_true_when_every_category_is_clean()
    {
        var sut = new DataIntegritySweepService(
            _db,
            new FakeModuleRepairService(0, 0),
            new FakeLessonRepairService(0, 0),
            new FakeExerciseRepairService(0, 0),
            new FakeResourceBankRepairService(0, 0));

        var result = await sut.RunAsync();

        result.AllHealthy.Should().BeTrue();
    }

    // ── Fakes for the four existing per-entity repair services — DataIntegritySweepService only
    // ever calls GetIssuesSummaryAsync on each; the other members are never reached by this
    // sweep, so any real call to one is a test setup bug, not something a production caller
    // could trigger. Separate classes since each interface's RepairAsync return type differs. ──

    private sealed class FakeModuleRepairService(int totalItems, int itemsWithIssues) : IModuleRepairService
    {
        public Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new IssuesSummary(totalItems, itemsWithIssues));
        public Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ModuleRepairResult> RepairAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeLessonRepairService(int totalItems, int itemsWithIssues) : ILessonRepairService
    {
        public Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new IssuesSummary(totalItems, itemsWithIssues));
        public Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LessonRepairResult> RepairAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeExerciseRepairService(int totalItems, int itemsWithIssues) : IExerciseRepairService
    {
        public Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new IssuesSummary(totalItems, itemsWithIssues));
        public Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ExerciseRepairResult> RepairAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeResourceBankRepairService(int totalItems, int itemsWithIssues) : IResourceBankRepairService
    {
        public Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new IssuesSummary(totalItems, itemsWithIssues));
        public Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ResourceBankItemRepairResult> RepairAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
