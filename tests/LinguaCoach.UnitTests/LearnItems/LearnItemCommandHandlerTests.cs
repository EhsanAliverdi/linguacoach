using FluentAssertions;
using LinguaCoach.Application.LearnItems;
using LinguaCoach.Infrastructure.LearnItems;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.LearnItems;

/// <summary>Phase H3 — manual create/update/approve/reject command handlers.</summary>
public sealed class LearnItemCommandHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminCreateLearnItemHandler _createHandler;
    private readonly AdminUpdateLearnItemHandler _updateHandler;
    private readonly AdminApproveLearnItemHandler _approveHandler;
    private readonly AdminRejectLearnItemHandler _rejectHandler;

    public LearnItemCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _createHandler = new AdminCreateLearnItemHandler(_db);
        _updateHandler = new AdminUpdateLearnItemHandler(_db);
        _approveHandler = new AdminApproveLearnItemHandler(_db);
        _rejectHandler = new AdminRejectLearnItemHandler(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static CreateLearnItemCommand ManualCommand(
        string title = "Present Perfect", string body = "Used for past actions with present relevance.") =>
        new(title, body, "B1", "Grammar", "Tenses",
            new[] { "travel" }, new[] { "past-experience" }, new[] { "I have visited Paris." }, new[] { "Confusing with simple past" },
            "Common in spoken English.", 3, 5, Links: null, CreatedByUserId: null);

    [Fact]
    public async Task Create_manual_learn_item_is_pending_review()
    {
        var result = await _createHandler.HandleAsync(ManualCommand());

        result.ReviewStatus.Should().Be("PendingReview");
        result.SourceMode.Should().Be("Manual");
        (await _db.LearnItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_stores_cefr_skill_subskill_context_focus_difficulty_metadata()
    {
        var result = await _createHandler.HandleAsync(ManualCommand());

        result.CefrLevel.Should().Be("B1");
        result.Skill.Should().Be("Grammar");
        result.Subskill.Should().Be("Tenses");
        result.ContextTagsJson.Should().Contain("travel");
        result.FocusTagsJson.Should().Contain("past-experience");
        result.DifficultyBand.Should().Be(3);
        result.EstimatedMinutes.Should().Be(5);
    }

    [Fact]
    public async Task Create_requires_title()
    {
        var command = ManualCommand() with { Title = "   " };
        var act = async () => await _createHandler.HandleAsync(command);
        await act.Should().ThrowAsync<LearnItemValidationException>();
    }

    [Fact]
    public async Task Create_requires_body()
    {
        var command = ManualCommand() with { Body = "" };
        var act = async () => await _createHandler.HandleAsync(command);
        await act.Should().ThrowAsync<LearnItemValidationException>();
    }

    [Fact]
    public async Task Create_rejects_invalid_cefr_level()
    {
        var command = ManualCommand() with { CefrLevel = "Z9" };
        var act = async () => await _createHandler.HandleAsync(command);
        await act.Should().ThrowAsync<LearnItemValidationException>();
    }

    [Fact]
    public async Task Update_changes_draft_fields_and_stays_pending_review()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());

        var updated = await _updateHandler.HandleAsync(new UpdateLearnItemCommand(
            created.Id, "Updated title", "Updated body.", new[] { "Updated example" }, new[] { "Updated mistake" },
            "Updated notes", "B2", "Grammar", "Tenses", new[] { "work" }, new[] { "formality" }, 4, 6));

        updated.Title.Should().Be("Updated title");
        updated.CefrLevel.Should().Be("B2");
        updated.ReviewStatus.Should().Be("PendingReview");
    }

    [Fact]
    public async Task Approve_transitions_review_status_to_approved()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());
        var reviewerId = Guid.NewGuid();

        var approved = await _approveHandler.HandleAsync(new ApproveLearnItemCommand(created.Id, reviewerId, "Looks good"));

        approved.ReviewStatus.Should().Be("Approved");
        approved.ReviewedByUserId.Should().Be(reviewerId);
        approved.ApprovedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Reject_transitions_review_status_to_rejected_with_reason()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());

        var rejected = await _rejectHandler.HandleAsync(new RejectLearnItemCommand(created.Id, "Inaccurate explanation", null));

        rejected.ReviewStatus.Should().Be("Rejected");
        rejected.RejectionReason.Should().Be("Inaccurate explanation");
    }

    [Fact]
    public async Task Reject_requires_a_reason()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());

        var act = async () => await _rejectHandler.HandleAsync(new RejectLearnItemCommand(created.Id, "", null));

        await act.Should().ThrowAsync<LearnItemValidationException>();
    }

    [Fact]
    public async Task Editing_an_approved_learn_item_is_blocked()
    {
        var created = await _createHandler.HandleAsync(ManualCommand());
        await _approveHandler.HandleAsync(new ApproveLearnItemCommand(created.Id, null));

        var act = async () => await _updateHandler.HandleAsync(new UpdateLearnItemCommand(
            created.Id, "New title", "New body.", null, null, null, null, null, null, null, null, null, null));

        await act.Should().ThrowAsync<LearnItemValidationException>();
    }
}
