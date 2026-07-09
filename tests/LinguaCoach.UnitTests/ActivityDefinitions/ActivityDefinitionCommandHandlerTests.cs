using FluentAssertions;
using LinguaCoach.Application.ActivityDefinitions;
using LinguaCoach.Infrastructure.ActivityDefinitions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ActivityDefinitions;

/// <summary>Phase H4 — manual create/update/approve/reject command handlers.</summary>
public sealed class ActivityDefinitionCommandHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminCreateActivityDefinitionHandler _createHandler;
    private readonly AdminUpdateActivityDefinitionHandler _updateHandler;
    private readonly AdminApproveActivityDefinitionHandler _approveHandler;
    private readonly AdminRejectActivityDefinitionHandler _rejectHandler;

    public ActivityDefinitionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _createHandler = new AdminCreateActivityDefinitionHandler(_db);
        _updateHandler = new AdminUpdateActivityDefinitionHandler(_db);
        _approveHandler = new AdminApproveActivityDefinitionHandler(_db);
        _rejectHandler = new AdminRejectActivityDefinitionHandler(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static CreateActivityDefinitionCommand ManualCommand(
        string title = "Gap fill: resilient",
        string instructions = "Type the word that means 'able to recover quickly'.") =>
        new(title, instructions, "gap_fill", "Formio", Description: "Manual draft",
            PatternKey: null, FormSchemaJson: "{\"components\":[{\"type\":\"textfield\",\"key\":\"answer\"}]}",
            AnswerKeyJson: "{\"answer\":\"resilient\"}", ScoringRulesJson: "{\"Components\":{}}", FeedbackPlanJson: null,
            CefrLevel: "B1", Skill: "Vocabulary", Subskill: "Tenses",
            ContextTags: new[] { "travel" }, FocusTags: new[] { "past-experience" },
            DifficultyBand: 3, EstimatedMinutes: 5, LearnItemId: null, Links: null, CreatedByUserId: null);

    [Fact]
    public async Task Create_manual_activity_is_pending_review()
    {
        var result = await _createHandler.HandleAsync(ManualCommand());

        result.ReviewStatus.Should().Be("PendingReview");
        result.SourceMode.Should().Be("Manual");
        (await _db.ActivityDefinitions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_stores_cefr_skill_subskill_context_focus_difficulty_metadata()
    {
        var result = await _createHandler.HandleAsync(ManualCommand());

        result.CefrLevel.Should().Be("B1");
        result.Skill.Should().Be("Vocabulary");
        result.Subskill.Should().Be("Tenses");
        result.ContextTagsJson.Should().Contain("travel");
        result.FocusTagsJson.Should().Contain("past-experience");
        result.DifficultyBand.Should().Be(3);
    }

    [Fact]
    public async Task Create_stores_renderer_type_and_formio_schema()
    {
        var result = await _createHandler.HandleAsync(ManualCommand());

        result.RendererType.Should().Be("Formio");
        result.FormSchemaJson.Should().Contain("textfield");
    }

    [Fact]
    public async Task Create_stores_answer_scoring_and_feedback_plan_json()
    {
        var result = await _createHandler.HandleAsync(ManualCommand());

        result.AnswerKeyJson.Should().Contain("resilient");
        result.ScoringRulesJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_can_link_to_a_learn_item()
    {
        var learnItem = new LinguaCoach.Domain.Entities.LearnItem(
            "Learn resilient", "resilient means able to recover quickly", LinguaCoach.Domain.Enums.LearnItemSourceMode.Manual);
        _db.LearnItems.Add(learnItem);
        await _db.SaveChangesAsync();

        var command = ManualCommand() with { LearnItemId = learnItem.Id };
        var result = await _createHandler.HandleAsync(command);

        result.LearnItemId.Should().Be(learnItem.Id);
    }

    [Fact]
    public async Task Create_with_unknown_learn_item_id_is_rejected()
    {
        var command = ManualCommand() with { LearnItemId = Guid.NewGuid() };
        var act = async () => await _createHandler.HandleAsync(command);
        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
    }

    [Fact]
    public async Task Create_requires_title()
    {
        var command = ManualCommand() with { Title = "   " };
        var act = async () => await _createHandler.HandleAsync(command);
        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
    }

    [Fact]
    public async Task Create_rejects_invalid_renderer_type()
    {
        var command = ManualCommand() with { RendererType = "unknown" };
        var act = async () => await _createHandler.HandleAsync(command);
        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
    }

    [Fact]
    public async Task Approve_transitions_review_status_to_approved()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());
        var reviewerId = Guid.NewGuid();

        var approved = await _approveHandler.HandleAsync(new ApproveActivityDefinitionCommand(created.Id, reviewerId, "Looks good"));

        approved.ReviewStatus.Should().Be("Approved");
        approved.ReviewedByUserId.Should().Be(reviewerId);
    }

    [Fact]
    public async Task Reject_transitions_review_status_to_rejected_with_reason()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());

        var rejected = await _rejectHandler.HandleAsync(new RejectActivityDefinitionCommand(created.Id, "Answer key is wrong", null));

        rejected.ReviewStatus.Should().Be("Rejected");
        rejected.RejectionReason.Should().Be("Answer key is wrong");
    }

    [Fact]
    public async Task Editing_an_approved_activity_is_blocked()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());
        await _approveHandler.HandleAsync(new ApproveActivityDefinitionCommand(created.Id, null));

        var act = async () => await _updateHandler.HandleAsync(new UpdateActivityDefinitionCommand(
            created.Id, "New title", "New instructions.", null, null, null, null, null, null, null, null, null, null, null, null));

        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
    }
}
