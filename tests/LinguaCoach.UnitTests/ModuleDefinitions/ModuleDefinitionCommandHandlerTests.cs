using FluentAssertions;
using LinguaCoach.Application.ModuleDefinitions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ModuleDefinitions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ModuleDefinitions;

/// <summary>Phase H5 — manual create/update/approve/reject command handlers.</summary>
public sealed class ModuleDefinitionCommandHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminCreateModuleDefinitionHandler _createHandler;
    private readonly AdminUpdateModuleDefinitionHandler _updateHandler;
    private readonly AdminApproveModuleDefinitionHandler _approveHandler;
    private readonly AdminRejectModuleDefinitionHandler _rejectHandler;

    public ModuleDefinitionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _createHandler = new AdminCreateModuleDefinitionHandler(_db);
        _updateHandler = new AdminUpdateModuleDefinitionHandler(_db);
        _approveHandler = new AdminApproveModuleDefinitionHandler(_db);
        _rejectHandler = new AdminRejectModuleDefinitionHandler(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private LearnItem SeedLearnItem()
    {
        var item = new LearnItem("Resilient", "Means able to recover quickly.", LearnItemSourceMode.Manual, "B1", "Vocabulary");
        _db.LearnItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private ActivityDefinition SeedActivity()
    {
        var activity = new ActivityDefinition("Gap fill", "Type the missing word.", "gap_fill", ActivityRendererType.Formio, ActivitySourceMode.Manual);
        _db.ActivityDefinitions.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private CreateModuleDefinitionCommand ManualCommand(Guid learnItemId, Guid activityId, string title = "Grammar module") =>
        new(title, new[] { new ModuleLearnItemLinkInput(learnItemId, "Primary") },
            new[] { new ModuleActivityLinkInput(activityId, "PrimaryPractice") },
            Description: "Manual draft", ObjectiveKey: null, CefrLevel: "B1", Skill: "Vocabulary", Subskill: "CoreWords",
            ContextTags: new[] { "travel" }, FocusTags: new[] { "past-experience" }, DifficultyBand: 3,
            EstimatedMinutes: 10, FeedbackPlanJson: "{\"completionMessage\":\"Well done\"}", CreatedByUserId: null);

    [Fact]
    public async Task Create_manual_module_is_pending_review()
    {
        var learnItem = SeedLearnItem();
        var activity = SeedActivity();

        var result = await _createHandler.HandleAsync(ManualCommand(learnItem.Id, activity.Id));

        result.ReviewStatus.Should().Be("PendingReview");
        result.SourceMode.Should().Be("Manual");
        (await _db.ModuleDefinitions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_stores_metadata_and_feedback_plan()
    {
        var learnItem = SeedLearnItem();
        var activity = SeedActivity();

        var result = await _createHandler.HandleAsync(ManualCommand(learnItem.Id, activity.Id));

        result.CefrLevel.Should().Be("B1");
        result.Skill.Should().Be("Vocabulary");
        result.Subskill.Should().Be("CoreWords");
        result.ContextTagsJson.Should().Contain("travel");
        result.DifficultyBand.Should().Be(3);
        result.FeedbackPlanJson.Should().Contain("Well done");
    }

    [Fact]
    public async Task Create_requires_at_least_one_learn_item()
    {
        var activity = SeedActivity();
        var command = ManualCommand(Guid.Empty, activity.Id) with { LearnItemLinks = Array.Empty<ModuleLearnItemLinkInput>() };

        var act = async () => await _createHandler.HandleAsync(command);

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    [Fact]
    public async Task Create_requires_at_least_one_activity()
    {
        var learnItem = SeedLearnItem();
        var command = ManualCommand(learnItem.Id, Guid.Empty) with { ActivityLinks = Array.Empty<ModuleActivityLinkInput>() };

        var act = async () => await _createHandler.HandleAsync(command);

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    [Fact]
    public async Task Approve_transitions_review_status_to_approved()
    {
        var learnItem = SeedLearnItem();
        var activity = SeedActivity();
        var created = await _createHandler.HandleAsync(ManualCommand(learnItem.Id, activity.Id));
        var reviewerId = Guid.NewGuid();

        var approved = await _approveHandler.HandleAsync(new ApproveModuleDefinitionCommand(created.Id, reviewerId, "Looks good"));

        approved.ReviewStatus.Should().Be("Approved");
        approved.ReviewedByUserId.Should().Be(reviewerId);
    }

    [Fact]
    public async Task Reject_transitions_review_status_to_rejected_with_reason()
    {
        var learnItem = SeedLearnItem();
        var activity = SeedActivity();
        var created = await _createHandler.HandleAsync(ManualCommand(learnItem.Id, activity.Id));

        var rejected = await _rejectHandler.HandleAsync(new RejectModuleDefinitionCommand(created.Id, "Needs a better activity", null));

        rejected.ReviewStatus.Should().Be("Rejected");
        rejected.RejectionReason.Should().Be("Needs a better activity");
    }

    [Fact]
    public async Task Editing_an_approved_module_is_blocked()
    {
        var learnItem = SeedLearnItem();
        var activity = SeedActivity();
        var created = await _createHandler.HandleAsync(ManualCommand(learnItem.Id, activity.Id));
        await _approveHandler.HandleAsync(new ApproveModuleDefinitionCommand(created.Id, null));

        var act = async () => await _updateHandler.HandleAsync(new UpdateModuleDefinitionCommand(
            created.Id, "New title", null, null, null, null, null, null, null, null, null));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }
}
