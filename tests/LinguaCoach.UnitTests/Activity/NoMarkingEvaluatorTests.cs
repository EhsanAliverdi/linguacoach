using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Activity.Evaluators;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Activity;

public sealed class NoMarkingEvaluatorTests
{
    private readonly NoMarkingEvaluator _sut = new();

    private static PatternEvaluationRequest MakeRequest() =>
        new(
            ActivityId: Guid.NewGuid(),
            StudentProfileId: Guid.NewGuid(),
            ExercisePatternKey: "lesson_reflection",
            MarkingMode: MarkingMode.NoMarking,
            InteractionMode: InteractionMode.ReadOnly,
            ActivityType: ActivityType.WritingScenario,
            ContentJson: "{}",
            SubmittedAnswerJson: "{}");

    // ── MarkingMode ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkingMode_IsNoMarking()
    {
        _sut.MarkingMode.Should().Be(MarkingMode.NoMarking);
    }

    // ── result shape ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns_Completed_True()
    {
        var result = await _sut.EvaluateAsync(MakeRequest(), default);
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_Passed_True()
    {
        var result = await _sut.EvaluateAsync(MakeRequest(), default);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_ScoreZero_MaxScoreZero()
    {
        var result = await _sut.EvaluateAsync(MakeRequest(), default);
        result.Score.Should().Be(0);
        result.MaxScore.Should().Be(0);
    }

    [Fact]
    public async Task Returns_Percentage_100()
    {
        var result = await _sut.EvaluateAsync(MakeRequest(), default);
        result.Percentage.Should().Be(100);
    }

    [Fact]
    public async Task Returns_CoachSummary()
    {
        var result = await _sut.EvaluateAsync(MakeRequest(), default);
        result.CoachSummary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Returns_EmptyItemResults()
    {
        var result = await _sut.EvaluateAsync(MakeRequest(), default);
        result.ItemResults.Should().BeEmpty();
    }

    // ── no AI dependency ───────────────────────────────────────────────────────

    [Fact]
    public async Task DoesNotRequireAiDependency()
    {
        var evaluator = new NoMarkingEvaluator();
        var result = await evaluator.EvaluateAsync(MakeRequest(), default);
        result.Should().NotBeNull();
    }
}
