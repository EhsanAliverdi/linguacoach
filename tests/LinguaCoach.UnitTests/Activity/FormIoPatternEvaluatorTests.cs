using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Activity.Evaluators;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Activity;

public sealed class FormIoPatternEvaluatorTests
{
    private static PatternEvaluationRequest MakeRequest(string submittedAnswerJson, string? scoringRulesJson) => new(
        ActivityId: Guid.NewGuid(),
        StudentProfileId: Guid.NewGuid(),
        ExercisePatternKey: "formio_practice_gym_pilot",
        MarkingMode: MarkingMode.FormIoScored,
        InteractionMode: InteractionMode.FreeTextEntry,
        ActivityType: ActivityType.VocabularyPractice,
        ContentJson: """{"display":"form","components":[{"type":"textfield","key":"answer"}]}""",
        SubmittedAnswerJson: submittedAnswerJson,
        ScoringRulesJson: scoringRulesJson);

    [Fact]
    public void MarkingMode_IsFormIoScored()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        Assert.Equal(MarkingMode.FormIoScored, evaluator.MarkingMode);
    }

    [Fact]
    public async Task EvaluateAsync_CorrectSingleChoiceAnswer_PassesWithFullScore()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        var scoringRules = """{"components":{"answer":{"kind":"single_choice","correctAnswer":"A","points":1.0}}}""";
        var submitted = """{"answer":"A"}""";

        var result = await evaluator.EvaluateAsync(MakeRequest(submitted, scoringRules), CancellationToken.None);

        Assert.True(result.Passed);
        Assert.Equal(1.0, result.Score);
        Assert.Equal(1.0, result.MaxScore);
        Assert.True(result.Completed);
        Assert.Single(result.ItemResults);
        Assert.True(result.ItemResults[0].IsCorrect);
    }

    [Fact]
    public async Task EvaluateAsync_IncorrectAnswer_FailsWithZeroScore()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        var scoringRules = """{"components":{"answer":{"kind":"single_choice","correctAnswer":"A","points":1.0}}}""";
        var submitted = """{"answer":"B"}""";

        var result = await evaluator.EvaluateAsync(MakeRequest(submitted, scoringRules), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Equal(0.0, result.Score);
        Assert.False(result.ItemResults[0].IsCorrect);
    }

    [Fact]
    public async Task EvaluateAsync_ItemResults_NeverExposeCorrectAnswer()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        var scoringRules = """{"components":{"answer":{"kind":"single_choice","correctAnswer":"A","points":1.0}}}""";
        var submitted = """{"answer":"B"}""";

        var result = await evaluator.EvaluateAsync(MakeRequest(submitted, scoringRules), CancellationToken.None);

        Assert.Null(result.ItemResults[0].CorrectAnswer);
        Assert.Empty(result.ItemResults[0].AcceptedAnswers);
    }

    [Fact]
    public async Task EvaluateAsync_MalformedSubmission_TreatsAsIncorrectWithoutThrowing()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        var scoringRules = """{"components":{"answer":{"kind":"single_choice","correctAnswer":"A","points":1.0}}}""";

        var result = await evaluator.EvaluateAsync(MakeRequest("not json", scoringRules), CancellationToken.None);

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_CorrectMultipleChoiceSelectboxesAnswer_PassesWithFullScore()
    {
        // Phase C2 — representative of reading_multiple_choice_multi's "selectboxes" component.
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        var scoringRules = """{"components":{"answers":{"kind":"multiple_choice","correctAnswers":["A","C"],"points":1.0}}}""";
        var submitted = """{"answers":{"A":true,"B":false,"C":true,"D":false}}""";

        var result = await evaluator.EvaluateAsync(MakeRequest(submitted, scoringRules), CancellationToken.None);

        Assert.True(result.Passed);
        Assert.Equal(1.0, result.Score);
        Assert.True(result.ItemResults[0].IsCorrect);
    }

    [Fact]
    public async Task EvaluateAsync_PartialMultipleChoiceSelectboxesAnswer_FailsWithZeroScore()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());
        var scoringRules = """{"components":{"answers":{"kind":"multiple_choice","correctAnswers":["A","C"],"points":1.0}}}""";
        var submitted = """{"answers":{"A":true,"B":false,"C":false,"D":false}}""";

        var result = await evaluator.EvaluateAsync(MakeRequest(submitted, scoringRules), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Equal(0.0, result.Score);
        Assert.False(result.ItemResults[0].IsCorrect);
    }

    [Fact]
    public async Task EvaluateAsync_NullScoringRules_ReturnsIncompleteZeroScore()
    {
        var evaluator = new FormIoPatternEvaluator(new LinguaCoach.Infrastructure.Placement.PlacementScoringService());

        var result = await evaluator.EvaluateAsync(MakeRequest("""{"answer":"A"}""", null), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Equal(0.0, result.MaxScore);
    }
}
