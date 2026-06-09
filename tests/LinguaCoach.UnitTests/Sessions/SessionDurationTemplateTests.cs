using FluentAssertions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;

namespace LinguaCoach.UnitTests.Sessions;

/// <summary>
/// Pure unit tests for SessionDurationTemplates — no DB, no DI.
/// </summary>
public sealed class SessionDurationTemplateTests
{
    // ── NormalizeDuration ──────────────────────────────────────────────────────

    [Fact]
    public void NormalizeDuration_NullPreference_ReturnsDefault()
        => SessionDurationTemplates.NormalizeDuration(null)
            .Should().Be(SessionDurationTemplates.DefaultDurationMinutes);

    [Fact]
    public void NormalizeDuration_ZeroPreference_ReturnsDefault()
        => SessionDurationTemplates.NormalizeDuration(0)
            .Should().Be(SessionDurationTemplates.DefaultDurationMinutes);

    [Theory]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(11, 15)]
    [InlineData(15, 15)]
    [InlineData(16, 20)]
    [InlineData(20, 20)]
    [InlineData(21, 30)]
    [InlineData(30, 30)]
    [InlineData(45, 30)]
    [InlineData(60, 30)]
    public void NormalizeDuration_MapsToCorrectBucket(int input, int expected)
        => SessionDurationTemplates.NormalizeDuration(input).Should().Be(expected);

    // ── GetTemplate — step counts ──────────────────────────────────────────────

    [Fact]
    public void Template10Min_HasThreeSteps()
        => SessionDurationTemplates.GetTemplate(10).Should().HaveCount(3);

    [Fact]
    public void Template15Min_HasFourSteps()
        => SessionDurationTemplates.GetTemplate(15).Should().HaveCount(4);

    [Fact]
    public void Template20Min_HasFourSteps()
        => SessionDurationTemplates.GetTemplate(20).Should().HaveCount(4);

    [Fact]
    public void Template30Min_HasFiveSteps()
        => SessionDurationTemplates.GetTemplate(30).Should().HaveCount(5);

    // ── GetTemplate — step ordering ────────────────────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_StepsAreZeroIndexedAndContiguous(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        for (var i = 0; i < steps.Count; i++)
            steps[i].Order.Should().Be(i, because: $"step {i} must have order {i}");
    }

    // ── GetTemplate — structure invariants ────────────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_FirstStepIsAlwaysVocabularyWarmup(int duration)
        => SessionDurationTemplates.GetTemplate(duration)[0].Kind
            .Should().Be(ExerciseKind.VocabularyWarmup);

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_LastStepIsAlwaysReview(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        steps[^1].Kind.Should().Be(ExerciseKind.Review);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_ContainsAtLeastOneMainTask(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        steps.Should().Contain(s =>
            s.Kind == ExerciseKind.WritingTask || s.Kind == ExerciseKind.SpeakingTask);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_TotalMinutesMatchDurationBucket(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        var total = steps.Sum(s => s.EstimatedMinutes);
        // Allow ±1 minute tolerance for rounding in the templates
        total.Should().BeInRange(duration - 1, duration + 1,
            because: $"total step minutes should approximately equal {duration}");
    }

    // ── Template — 30-minute specific ─────────────────────────────────────────

    [Fact]
    public void Template30Min_ContainsSpeakingTask()
        => SessionDurationTemplates.GetTemplate(30)
            .Should().Contain(s => s.Kind == ExerciseKind.SpeakingTask);

    [Fact]
    public void Template30Min_ContainsListeningInput()
        => SessionDurationTemplates.GetTemplate(30)
            .Should().Contain(s => s.Kind == ExerciseKind.ListeningInput);

    // ── GetTemplate — edge cases map to correct bucket ────────────────────────

    [Fact]
    public void GetTemplate_ForDuration3_Returns10MinTemplate()
        => SessionDurationTemplates.GetTemplate(3).Should().HaveCount(3);

    [Fact]
    public void GetTemplate_ForDuration100_Returns30MinTemplate()
        => SessionDurationTemplates.GetTemplate(100).Should().HaveCount(5);

    // ── All steps have required non-empty fields ───────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_AllStepsHaveNonEmptyFields(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        foreach (var step in steps)
        {
            step.PatternKey.Should().NotBeNullOrWhiteSpace();
            step.PrimarySkill.Should().NotBeNullOrWhiteSpace();
            step.Instructions.Should().NotBeNullOrWhiteSpace();
            step.EstimatedMinutes.Should().BePositive();
        }
    }
}
