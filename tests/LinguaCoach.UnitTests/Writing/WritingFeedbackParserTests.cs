using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Writing;

namespace LinguaCoach.UnitTests.Writing;

public sealed class WritingFeedbackParserTests
{
    // ── Valid JSON ────────────────────────────────────────────────────────────

    [Fact]
    public void ParseFeedback_ValidJson_ReturnsAllFields()
    {
        var json = """
            {
              "overallScore": 72,
              "correctedEmail": "Dear John, I hope this finds you well.",
              "feedbackInSourceLanguage": "عالی بود!",
              "grammarIssues": ["Missing article before 'approval'"],
              "vocabularyIssues": [],
              "toneIssues": ["Slightly too informal"],
              "suggestedPhrases": ["I would appreciate your response"],
              "mistakesToTrack": ["article omission"]
            }
            """;

        var result = WritingExerciseHandler.ParseFeedback(json);

        result.OverallScore.Should().Be(72);
        result.CorrectedEmail.Should().Contain("John");
        result.FeedbackInSourceLanguage.Should().Be("عالی بود!");
        result.GrammarIssues.Should().HaveCount(1);
        result.VocabularyIssues.Should().BeEmpty();
        result.ToneIssues.Should().HaveCount(1);
        result.SuggestedPhrases.Should().HaveCount(1);
        result.MistakesToTrack.Should().HaveCount(1);
    }

    [Fact]
    public void ParseFeedback_JsonWrappedInMarkdownFence_Parsed()
    {
        var json = "```json\n{\"overallScore\": 60, \"correctedEmail\": \"Fixed.\"}\n```";
        var result = WritingExerciseHandler.ParseFeedback(json);
        result.OverallScore.Should().Be(60);
    }

    [Fact]
    public void ParseFeedback_JsonWrappedInPlainFence_Parsed()
    {
        var json = "```\n{\"overallScore\": 55}\n```";
        var result = WritingExerciseHandler.ParseFeedback(json);
        result.OverallScore.Should().Be(55);
    }

    [Fact]
    public void ParseFeedback_NullableFieldsMissing_ReturnsNulls()
    {
        var result = WritingExerciseHandler.ParseFeedback("{}");
        result.OverallScore.Should().BeNull();
        result.CorrectedEmail.Should().BeNull();
        result.GrammarIssues.Should().BeNull();
    }

    // ── Invalid JSON ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseFeedback_InvalidJson_ThrowsValidationException()
    {
        var act = () => WritingExerciseHandler.ParseFeedback("not json at all");
        act.Should().Throw<AiResponseValidationException>()
           .WithMessage("*not valid JSON*");
    }

    [Fact]
    public void ParseFeedback_EmptyString_ThrowsValidationException()
    {
        var act = () => WritingExerciseHandler.ParseFeedback(string.Empty);
        act.Should().Throw<AiResponseValidationException>();
    }

    // ── Score edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void ParseFeedback_ScoreZero_IsValid()
    {
        var result = WritingExerciseHandler.ParseFeedback("{\"overallScore\": 0}");
        result.OverallScore.Should().Be(0);
    }

    [Fact]
    public void ParseFeedback_Score100_IsValid()
    {
        var result = WritingExerciseHandler.ParseFeedback("{\"overallScore\": 100}");
        result.OverallScore.Should().Be(100);
    }
}
