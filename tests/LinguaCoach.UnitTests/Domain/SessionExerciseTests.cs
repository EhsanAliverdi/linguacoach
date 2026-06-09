using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class SessionExerciseTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private static SessionExercise Valid() => new(
        learningSessionId: SessionId,
        order: 0,
        exercisePatternKey: "listen_and_gap_fill",
        primarySkill: "Listening",
        instructions: "Listen to the audio and fill in the gaps.",
        estimatedMinutes: 4);

    // ── Constructor ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidArgs_CreatesNotStartedExercise()
    {
        var exercise = Valid();

        exercise.Status.Should().Be(ExerciseStatus.NotStarted);
        exercise.LearningSessionId.Should().Be(SessionId);
        exercise.Order.Should().Be(0);
        exercise.ExercisePatternKey.Should().Be("listen_and_gap_fill");
        exercise.PrimarySkill.Should().Be("Listening");
        exercise.EstimatedMinutes.Should().Be(4);
        exercise.LearningActivityId.Should().BeNull();
        exercise.CompletedAtUtc.Should().BeNull();
        exercise.SecondarySkillsJson.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptySessionId_Throws()
    {
        var fn = () => new SessionExercise(Guid.Empty, 0, "key", "Skill", "Instructions", 3);
        fn.Should().Throw<ArgumentException>().WithMessage("*LearningSessionId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankPatternKey_Throws(string key)
    {
        var fn = () => new SessionExercise(SessionId, 0, key, "Skill", "Instructions", 3);
        fn.Should().Throw<ArgumentException>().WithMessage("*ExercisePatternKey*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankPrimarySkill_Throws(string skill)
    {
        var fn = () => new SessionExercise(SessionId, 0, "key", skill, "Instructions", 3);
        fn.Should().Throw<ArgumentException>().WithMessage("*PrimarySkill*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WithBlankInstructions_Throws(string instructions)
    {
        var fn = () => new SessionExercise(SessionId, 0, "key", "Skill", instructions, 3);
        fn.Should().Throw<ArgumentException>().WithMessage("*Instructions*");
    }

    [Fact]
    public void Constructor_WithZeroEstimatedMinutes_Throws()
    {
        var fn = () => new SessionExercise(SessionId, 0, "key", "Skill", "Instr", 0);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*EstimatedMinutes*");
    }

    [Fact]
    public void Constructor_WithNegativeOrder_Throws()
    {
        var fn = () => new SessionExercise(SessionId, -1, "key", "Skill", "Instr", 3);
        fn.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Order*");
    }

    [Fact]
    public void Constructor_WithSecondarySkillsJson_StoresCorrectly()
    {
        var exercise = new SessionExercise(
            SessionId, 0, "key", "Listening", "Instr", 4,
            secondarySkillsJson: """["Vocabulary"]""");

        exercise.SecondarySkillsJson.Should().Be("""["Vocabulary"]""");
    }

    [Fact]
    public void Constructor_WithWhitespaceSecondarySkills_StoresNull()
    {
        var exercise = new SessionExercise(SessionId, 0, "key", "Listening", "Instr", 4, secondarySkillsJson: "  ");
        exercise.SecondarySkillsJson.Should().BeNull();
    }

    // ── AssignActivity ─────────────────────────────────────────────────────────

    [Fact]
    public void AssignActivity_WithValidId_SetsLearningActivityId()
    {
        var exercise = Valid();
        var activityId = Guid.NewGuid();

        exercise.AssignActivity(activityId);

        exercise.LearningActivityId.Should().Be(activityId);
    }

    [Fact]
    public void AssignActivity_WithEmptyId_Throws()
    {
        var exercise = Valid();
        var fn = () => exercise.AssignActivity(Guid.Empty);
        fn.Should().Throw<ArgumentException>().WithMessage("*LearningActivityId*");
    }

    // ── Status transitions ─────────────────────────────────────────────────────

    [Fact]
    public void Start_FromNotStarted_SetsInProgress()
    {
        var exercise = Valid();
        exercise.Start();
        exercise.Status.Should().Be(ExerciseStatus.InProgress);
    }

    [Fact]
    public void Start_WhenAlreadyInProgress_Throws()
    {
        var exercise = Valid();
        exercise.Start();
        var fn = () => exercise.Start();
        fn.Should().Throw<InvalidOperationException>().WithMessage("*InProgress*");
    }

    [Fact]
    public void Complete_FromNotStarted_SetsCompletedAndTimestamp()
    {
        var exercise = Valid();
        var before = DateTime.UtcNow;

        exercise.Complete();

        exercise.Status.Should().Be(ExerciseStatus.Completed);
        exercise.CompletedAtUtc.Should().NotBeNull();
        exercise.CompletedAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Complete_FromInProgress_SetsCompleted()
    {
        var exercise = Valid();
        exercise.Start();
        exercise.Complete();
        exercise.Status.Should().Be(ExerciseStatus.Completed);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_Throws()
    {
        var exercise = Valid();
        exercise.Complete();
        var fn = () => exercise.Complete();
        fn.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Skip_FromNotStarted_SetsSkippedAndTimestamp()
    {
        var exercise = Valid();
        var before = DateTime.UtcNow;

        exercise.Skip();

        exercise.Status.Should().Be(ExerciseStatus.Skipped);
        exercise.CompletedAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Skip_FromInProgress_SetsSkipped()
    {
        var exercise = Valid();
        exercise.Start();
        exercise.Skip();
        exercise.Status.Should().Be(ExerciseStatus.Skipped);
    }

    [Fact]
    public void Skip_WhenCompleted_Throws()
    {
        var exercise = Valid();
        exercise.Complete();
        var fn = () => exercise.Skip();
        fn.Should().Throw<InvalidOperationException>();
    }
}
