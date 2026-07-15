using FluentAssertions;
using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Modules;

/// <summary>Phase H5 — manual create/update/approve/reject command handlers.</summary>
public sealed class ModuleCommandHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminCreateModuleHandler _createHandler;
    private readonly AdminUpdateModuleHandler _updateHandler;
    private readonly AdminApproveModuleHandler _approveHandler;
    private readonly AdminRejectModuleHandler _rejectHandler;

    public ModuleCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _createHandler = new AdminCreateModuleHandler(_db);
        _updateHandler = new AdminUpdateModuleHandler(_db);
        _approveHandler = new AdminApproveModuleHandler(_db);
        _rejectHandler = new AdminRejectModuleHandler(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Lesson SeedLesson()
    {
        var item = new Lesson("Resilient", "Means able to recover quickly.", LessonSourceMode.Manual, "B1", "Vocabulary");
        _db.Lessons.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Exercise SeedActivity()
    {
        var activity = new Exercise("Gap fill", "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, lessonId: SeedLesson().Id);
        _db.Exercises.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private CreateModuleCommand ManualCommand(Guid lessonId, Guid activityId, string title = "Grammar module") =>
        new(title, new[] { new ModuleLessonLinkInput(lessonId, "Primary") },
            new[] { new ModuleExerciseLinkInput(activityId, "PrimaryPractice") },
            Description: "Manual draft", ObjectiveKey: null, CefrLevel: "B1", Skill: "Vocabulary", Subskill: "CoreWords",
            ContextTags: new[] { "travel" }, FocusTags: new[] { "past-experience" }, DifficultyBand: 3,
            EstimatedMinutes: 10, FeedbackPlanJson: "{\"completionMessage\":\"Well done\"}", CreatedByUserId: null);

    [Fact]
    public async Task Create_manual_module_is_pending_review()
    {
        var lesson = SeedLesson();
        var activity = SeedActivity();

        var result = await _createHandler.HandleAsync(ManualCommand(lesson.Id, activity.Id));

        result.ReviewStatus.Should().Be("PendingReview");
        result.SourceMode.Should().Be("Manual");
        (await _db.Modules.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_stores_metadata_and_feedback_plan()
    {
        var lesson = SeedLesson();
        var activity = SeedActivity();

        var result = await _createHandler.HandleAsync(ManualCommand(lesson.Id, activity.Id));

        result.CefrLevel.Should().Be("B1");
        result.Skill.Should().Be("Vocabulary");
        result.Subskill.Should().Be("CoreWords");
        result.ContextTagsJson.Should().Contain("travel");
        result.DifficultyBand.Should().Be(3);
        result.FeedbackPlanJson.Should().Contain("Well done");
    }

    [Fact]
    public async Task Create_requires_at_least_one_lesson()
    {
        var activity = SeedActivity();
        var command = ManualCommand(Guid.Empty, activity.Id) with { LessonLinks = Array.Empty<ModuleLessonLinkInput>() };

        var act = async () => await _createHandler.HandleAsync(command);

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    [Fact]
    public async Task Create_requires_at_least_one_activity()
    {
        var lesson = SeedLesson();
        var command = ManualCommand(lesson.Id, Guid.Empty) with { ExerciseLinks = Array.Empty<ModuleExerciseLinkInput>() };

        var act = async () => await _createHandler.HandleAsync(command);

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    [Fact]
    public async Task Approve_transitions_review_status_to_approved()
    {
        var lesson = SeedLesson();
        var activity = SeedActivity();
        var created = await _createHandler.HandleAsync(ManualCommand(lesson.Id, activity.Id));
        var reviewerId = Guid.NewGuid();

        var approved = await _approveHandler.HandleAsync(new ApproveModuleCommand(created.Id, reviewerId, "Looks good"));

        approved.ReviewStatus.Should().Be("Approved");
        approved.ReviewedByUserId.Should().Be(reviewerId);
    }

    [Fact]
    public async Task Reject_transitions_review_status_to_rejected_with_reason()
    {
        var lesson = SeedLesson();
        var activity = SeedActivity();
        var created = await _createHandler.HandleAsync(ManualCommand(lesson.Id, activity.Id));

        var rejected = await _rejectHandler.HandleAsync(new RejectModuleCommand(created.Id, "Needs a better activity", null));

        rejected.ReviewStatus.Should().Be("Rejected");
        rejected.RejectionReason.Should().Be("Needs a better activity");
    }

    [Fact]
    public async Task Editing_an_approved_module_is_blocked()
    {
        var lesson = SeedLesson();
        var activity = SeedActivity();
        var created = await _createHandler.HandleAsync(ManualCommand(lesson.Id, activity.Id));
        await _approveHandler.HandleAsync(new ApproveModuleCommand(created.Id, null));

        var act = async () => await _updateHandler.HandleAsync(new UpdateModuleCommand(
            created.Id, "New title", null, null, null, null, null, null, null, null, null));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }
}
