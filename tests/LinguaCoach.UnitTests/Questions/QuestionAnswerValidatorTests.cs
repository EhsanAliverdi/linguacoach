using LinguaCoach.Domain.Questions;
using LinguaCoach.Infrastructure.Questions;

namespace LinguaCoach.UnitTests.Questions;

public sealed class QuestionAnswerValidatorTests
{
    private readonly QuestionAnswerValidator _sut = new();

    private static QuestionAnswer Answer(params (string Id, string[] Values)[] items) =>
        new(items.Select(i => new QuestionAnswerItem(i.Id, i.Values)).ToList());

    [Fact]
    public void SingleChoice_ValidChoiceKey_IsValid()
    {
        var q = new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }, new ChoiceOption { Key = "B", Label = "b" }] };
        var result = _sut.Validate(q, Answer(("q1", ["A"])));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void SingleChoice_UnknownChoiceKey_IsInvalid()
    {
        var q = new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }] };
        var result = _sut.Validate(q, Answer(("q1", ["Z"])));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void SingleChoice_MissingAnswer_IsInvalid()
    {
        var q = new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }] };
        var result = _sut.Validate(q, new QuestionAnswer([]));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void MultipleChoice_AllValidKeys_IsValid()
    {
        var q = new MultipleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }, new ChoiceOption { Key = "B", Label = "b" }] };
        var result = _sut.Validate(q, Answer(("q1", ["A", "B"])));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void GapFill_EmptyAnswer_IsInvalid()
    {
        var q = new GapFillQuestion { Id = "q1", QuestionText = "?" };
        var result = _sut.Validate(q, Answer(("q1", [""])));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FreeText_ExceedsMaxLength_IsInvalid()
    {
        var q = new FreeTextQuestion { Id = "q1", QuestionText = "?", MaxLength = 5 };
        var result = _sut.Validate(q, Answer(("q1", ["way too long"])));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FreeText_WithinMaxLength_IsValid()
    {
        var q = new FreeTextQuestion { Id = "q1", QuestionText = "?", MaxLength = 20 };
        var result = _sut.Validate(q, Answer(("q1", ["short"])));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ListeningGroup_AllSubQuestionsAnswered_IsValid()
    {
        var group = new ListeningGroupQuestion
        {
            AudioScript = "script",
            Questions =
            [
                new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }] },
                new GapFillQuestion { Id = "q2", QuestionText = "?" },
            ],
        };
        var result = _sut.Validate(group, Answer(("q1", ["A"]), ("q2", ["word"])));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ReadingGroup_MissingOneSubQuestionAnswer_IsInvalid()
    {
        var group = new ReadingGroupQuestion
        {
            Passage = "passage",
            Questions =
            [
                new SingleChoiceQuestion { Id = "q1", QuestionText = "?", Choices = [new ChoiceOption { Key = "A", Label = "a" }] },
                new GapFillQuestion { Id = "q2", QuestionText = "?" },
            ],
        };
        var result = _sut.Validate(group, Answer(("q1", ["A"])));
        Assert.False(result.IsValid);
    }
}
