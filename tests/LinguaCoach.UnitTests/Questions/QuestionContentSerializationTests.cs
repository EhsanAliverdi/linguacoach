using System.Text.Json;
using LinguaCoach.Domain.Questions;

namespace LinguaCoach.UnitTests.Questions;

/// <summary>Round-trip serialization tests for the shared QuestionContent schema
/// (Unified Question-Schema Phase 1) — confirms System.Text.Json polymorphism config
/// preserves the "type" discriminator for every leaf and group type.</summary>
public sealed class QuestionContentSerializationTests
{
    [Fact]
    public void SingleChoiceQuestion_RoundTrips()
    {
        QuestionContent original = new SingleChoiceQuestion
        {
            Id = "q1",
            QuestionText = "Which is correct?",
            Choices = [new ChoiceOption { Key = "A", Label = "am" }, new ChoiceOption { Key = "B", Label = "is" }],
            CorrectAnswerKey = "A",
        };

        var json = JsonSerializer.Serialize(original);
        Assert.Contains("\"type\":\"single_choice\"", json);

        var deserialized = JsonSerializer.Deserialize<QuestionContent>(json);
        var result = Assert.IsType<SingleChoiceQuestion>(deserialized);
        Assert.Equal("q1", result.Id);
        Assert.Equal("Which is correct?", result.QuestionText);
        Assert.Equal(2, result.Choices.Count);
        Assert.Equal("A", result.CorrectAnswerKey);
    }

    [Fact]
    public void MultipleChoiceQuestion_RoundTrips()
    {
        QuestionContent original = new MultipleChoiceQuestion
        {
            QuestionText = "Select all nouns.",
            Choices = [new ChoiceOption { Key = "A", Label = "run" }, new ChoiceOption { Key = "B", Label = "cat" }],
            CorrectAnswerKeys = ["B"],
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<QuestionContent>(json);
        var result = Assert.IsType<MultipleChoiceQuestion>(deserialized);
        Assert.Single(result.CorrectAnswerKeys!);
        Assert.Equal("B", result.CorrectAnswerKeys![0]);
    }

    [Fact]
    public void GapFillQuestion_RoundTrips()
    {
        QuestionContent original = new GapFillQuestion { QuestionText = "I ___ happy.", CorrectAnswer = "am" };
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<QuestionContent>(json);
        var result = Assert.IsType<GapFillQuestion>(deserialized);
        Assert.Equal("am", result.CorrectAnswer);
    }

    [Fact]
    public void FreeTextQuestion_RoundTrips()
    {
        QuestionContent original = new FreeTextQuestion { QuestionText = "Why?", MaxLength = 200 };
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<QuestionContent>(json);
        var result = Assert.IsType<FreeTextQuestion>(deserialized);
        Assert.Equal(200, result.MaxLength);
    }

    [Fact]
    public void ListeningGroupQuestion_WithMultipleSubQuestions_RoundTrips()
    {
        QuestionContent original = new ListeningGroupQuestion
        {
            AudioScript = "Turn left at the corner.",
            Questions =
            [
                new SingleChoiceQuestion { Id = "q1", QuestionText = "Which direction?", Choices = [new ChoiceOption { Key = "A", Label = "left" }], CorrectAnswerKey = "A" },
                new GapFillQuestion { Id = "q2", QuestionText = "Turn ___ at the corner.", CorrectAnswer = "left" },
            ],
        };

        var json = JsonSerializer.Serialize(original);
        Assert.Contains("\"type\":\"listening_group\"", json);

        var deserialized = JsonSerializer.Deserialize<QuestionContent>(json);
        var result = Assert.IsType<ListeningGroupQuestion>(deserialized);
        Assert.Equal("Turn left at the corner.", result.AudioScript);
        Assert.Equal(2, result.Questions.Count);
        Assert.IsType<SingleChoiceQuestion>(result.Questions[0]);
        Assert.IsType<GapFillQuestion>(result.Questions[1]);
    }

    [Fact]
    public void ReadingGroupQuestion_WithSingleSubQuestion_RoundTrips()
    {
        QuestionContent original = new ReadingGroupQuestion
        {
            Passage = "The cat sat on the mat.",
            Questions = [new SingleChoiceQuestion { QuestionText = "Where did the cat sit?", Choices = [new ChoiceOption { Key = "A", Label = "mat" }], CorrectAnswerKey = "A" }],
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<QuestionContent>(json);
        var result = Assert.IsType<ReadingGroupQuestion>(deserialized);
        Assert.Single(result.Questions);
    }
}
