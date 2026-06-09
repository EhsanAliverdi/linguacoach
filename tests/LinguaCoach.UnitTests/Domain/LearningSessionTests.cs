using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class LearningSessionTests
{
    private static readonly Guid ModuleId = Guid.NewGuid();

    private static LearningSession Valid() => new(
        learningModuleId: ModuleId,
        title: "Explaining a Delay Professionally",
        topic: "Professional delay communication",
        sessionGoal: "Write a professional delay notification message",
        durationMinutes: 15,
        focusSkill: "Writing",
        order: 0);

    // ── Constructor ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidArgs_CreatesNotStartedSession()
    {
        var session = Valid();

        session.Status.Should().Be(SessionStatus.NotStarted);
        session.LearningModuleId.Should().Be(ModuleId);
        session.Title.Should().Be("Explaining a Delay Professionally");
        session.DurationMinutes.Should().Be(15);
        session.FocusSkill.Should().Be("Writing");
        session.Order.Should().Be(0);
        session.StartedAtUtc.Should().BeNull();
        session.CompletedAtUtc.Should().BeNull();
        session.SecondarySkillsJson.Should().BeNull();
        session.GeneratedFromMemorySnapshotJson.Should().BeNull();
        session.Exercises.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyModuleId_Throws()
    {
        var fn = () => new LearningSession(Guid.Empty, "T", "T", "G", 15, "Writing", 0);
        fn.Should().Throw<ArgumentException>().WithMessage("*LearningModuleId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankTitle_Throws(string title)
    {
        var fn = () => new LearningSession(ModuleId, title, "Topic", "Goal", 15, "Writing", 0);
        fn.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankTopic_Throws(string topic)
    {
        var fn = () => new LearningSession(ModuleId, "Title", topic, "Goal", 15, "Writing", 0);
        fn.Should().Throw<ArgumentException>().WithMessage("*Topic*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankSessionGoal_Throws(string goal)
    {
        var fn = () => new LearningSession(ModuleId, "Title", "Topic", goal, 15, "Writing", 0);
        fn.Should().Throw<ArgumentException>().WithMessage("*SessionGoal*");
    }

    [Fact]
    public void Constructor_WithZeroDuration_Throws()
    {
        var fn = () => new LearningSession(ModuleId, "T", "T", "G", 0, "Writing", 0);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*DurationMinutes*");
    }

    [Fact]
    public void Constructor_WithNegativeDuration_Throws()
    {
        var fn = () => new LearningSession(ModuleId, "T", "T", "G", -5, "Writing", 0);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*DurationMinutes*");
    }

    [Fact]
    public void Constructor_WithNegativeOrder_Throws()
    {
        var fn = () => new LearningSession(ModuleId, "T", "T", "G", 15, "Writing", -1);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Order*");
    }

    [Fact]
    public void Constructor_WithBlankFocusSkill_Throws()
    {
        var fn = () => new LearningSession(ModuleId, "T", "T", "G", 15, "", 0);
        fn.Should().Throw<ArgumentException>().WithMessage("*FocusSkill*");
    }

    [Fact]
    public void Constructor_WithOptionalFields_StoresCorrectly()
    {
        var session = new LearningSession(
            ModuleId, "Title", "Topic", "Goal", 20, "Listening", 2,
            secondarySkillsJson: """["Vocabulary","Grammar"]""",
            generatedFromMemorySnapshotJson: """{"cefrLevel":"B1"}""");

        session.SecondarySkillsJson.Should().Be("""["Vocabulary","Grammar"]""");
        session.GeneratedFromMemorySnapshotJson.Should().Be("""{"cefrLevel":"B1"}""");
    }

    [Fact]
    public void Constructor_WithWhitespaceOptionalFields_StoresNull()
    {
        var session = new LearningSession(
            ModuleId, "Title", "Topic", "Goal", 20, "Listening", 0,
            secondarySkillsJson: "   ",
            generatedFromMemorySnapshotJson: "  ");

        session.SecondarySkillsJson.Should().BeNull();
        session.GeneratedFromMemorySnapshotJson.Should().BeNull();
    }

    // ── Status transitions ─────────────────────────────────────────────────────

    [Fact]
    public void Start_FromNotStarted_SetsInProgressAndTimestamp()
    {
        var session = Valid();
        var before = DateTime.UtcNow;

        session.Start();

        session.Status.Should().Be(SessionStatus.InProgress);
        session.StartedAtUtc.Should().NotBeNull();
        session.StartedAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Start_WhenAlreadyInProgress_Throws()
    {
        var session = Valid();
        session.Start();

        var fn = () => session.Start();
        fn.Should().Throw<InvalidOperationException>().WithMessage("*InProgress*");
    }

    [Fact]
    public void Start_WhenCompleted_Throws()
    {
        var session = Valid();
        session.Start();
        session.Complete();

        var fn = () => session.Start();
        fn.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_AfterStart_SetsCompletedAndTimestamp()
    {
        var session = Valid();
        session.Start();
        var before = DateTime.UtcNow;

        session.Complete();

        session.Status.Should().Be(SessionStatus.Completed);
        session.CompletedAtUtc.Should().NotBeNull();
        session.CompletedAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Complete_FromNotStarted_Throws()
    {
        var session = Valid();
        var fn = () => session.Complete();
        fn.Should().Throw<InvalidOperationException>().WithMessage("*NotStarted*");
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_Throws()
    {
        var session = Valid();
        session.Start();
        session.Complete();

        var fn = () => session.Complete();
        fn.Should().Throw<InvalidOperationException>();
    }
}
