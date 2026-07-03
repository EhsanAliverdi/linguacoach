using LinguaCoach.Domain.Questions;
using LinguaCoach.Infrastructure.Questions;

namespace LinguaCoach.UnitTests.Questions;

public sealed class QuestionScorerTests
{
    private readonly QuestionScorer _sut = new();

    private static QuestionAnswer Answer(params (string Id, string[] Values)[] items) =>
        new(items.Select(i => new QuestionAnswerItem(i.Id, i.Values)).ToList());

    [Fact]
    public void SingleChoice_CorrectAnswer_ScoresCorrect()
    {
        var q = new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }], CorrectAnswerKey = "A" };
        var result = _sut.Score(q, Answer(("q1", ["A"])));
        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public void SingleChoice_WrongAnswer_ScoresIncorrect()
    {
        var q = new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }, new ChoiceOption { Key = "B", Label = "b" }], CorrectAnswerKey = "A" };
        var result = _sut.Score(q, Answer(("q1", ["B"])));
        Assert.False(result.IsCorrect);
        Assert.Equal(0.0, result.Score);
    }

    [Fact]
    public void SingleChoice_NoCorrectAnswerDefined_ScoresAsCorrect()
    {
        // Onboarding profile-capture questions have no correct answer — nothing to grade.
        var q = new SingleChoiceQuestion { Id = "q1", QuestionText = "Native language?", Choices = [new ChoiceOption { Key = "fa", Label = "Farsi" }] };
        var result = _sut.Score(q, Answer(("q1", ["fa"])));
        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public void MultipleChoice_ExactSetMatch_ScoresCorrect()
    {
        var q = new MultipleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }, new ChoiceOption { Key = "B", Label = "b" }], CorrectAnswerKeys = ["A", "B"] };
        var result = _sut.Score(q, Answer(("q1", ["B", "A"])));
        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void MultipleChoice_PartialMatch_ScoresIncorrect()
    {
        var q = new MultipleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }, new ChoiceOption { Key = "B", Label = "b" }], CorrectAnswerKeys = ["A", "B"] };
        var result = _sut.Score(q, Answer(("q1", ["A"])));
        Assert.False(result.IsCorrect);
    }

    [Fact]
    public void GapFill_CaseInsensitiveMatch_ScoresCorrect()
    {
        var q = new GapFillQuestion { Id = "q1", QuestionText = "?", CorrectAnswer = "Left" };
        var result = _sut.Score(q, Answer(("q1", ["left"])));
        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void ListeningGroup_AllSubQuestionsCorrect_ScoresGroupCorrect()
    {
        var group = new ListeningGroupQuestion
        {
            AudioScript = "Turn left at the corner.",
            Questions =
            [
                new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "left" }], CorrectAnswerKey = "A" },
                new GapFillQuestion { Id = "q2", QuestionText = "?", CorrectAnswer = "left" },
            ],
        };
        var result = _sut.Score(group, Answer(("q1", ["A"]), ("q2", ["left"])));
        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
        Assert.Equal(2, result.SubScores.Count);
    }

    [Fact]
    public void ReadingGroup_OneOfTwoSubQuestionsWrong_ScoresGroupIncorrectWithPartialScore()
    {
        var group = new ReadingGroupQuestion
        {
            Passage = "The cat sat on the mat.",
            Questions =
            [
                new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "mat" }, new ChoiceOption { Key = "B", Label = "chair" }], CorrectAnswerKey = "A" },
                new GapFillQuestion { Id = "q2", QuestionText = "?", CorrectAnswer = "cat" },
            ],
        };
        var result = _sut.Score(group, Answer(("q1", ["B"]), ("q2", ["cat"])));
        Assert.False(result.IsCorrect);
        Assert.Equal(0.5, result.Score);
    }
}
