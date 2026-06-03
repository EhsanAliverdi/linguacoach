using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Writing;

namespace LinguaCoach.UnitTests.Writing;

public sealed class WritingFeedbackParserTests
{
    // ── Valid JSON (v1 fields) ────────────────────────────────────────────────

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

        var result = WritingExerciseSubmitHandler.ParseFeedback(json);

        result.OverallScore.Should().Be(72);
        result.CorrectedEmail.Should().Contain("John");
        result.FeedbackInSourceLanguage.Should().Be("عالی بود!");
        result.GrammarIssues.Should().HaveCount(1);
        result.VocabularyIssues.Should().BeEmpty();
        result.ToneIssues.Should().HaveCount(1);
        result.SuggestedPhrases.Should().HaveCount(1);
        result.MistakesToTrack.Should().HaveCount(1);
    }

    // ── Teaching fields (v2) ──────────────────────────────────────────────────

    [Fact]
    public void ParseFeedback_V2TeachingFields_Parsed()
    {
        var json = """
            {
              "overallScore": 80,
              "correctedEmail": "Dear Ms. Smith,\n\nThank you.",
              "feedbackInSourceLanguage": "خوب بود!",
              "grammarIssues": [],
              "vocabularyIssues": [],
              "toneIssues": [],
              "suggestedPhrases": [],
              "mistakesToTrack": [],
              "whatYouDidWell": ["Good use of formal greeting", "Clear subject line"],
              "mainMistakes": ["Missing article before 'approval'"],
              "grammarExplanation": "Use 'the' before specific nouns like 'the approval'.",
              "toneExplanation": "Your tone was mostly professional, but 'ASAP' is too casual.",
              "vocabularyToRemember": ["at your earliest convenience", "pending approval"],
              "rewriteChallenge": "Rewrite the opening sentence using 'I hope this email finds you well'.",
              "nextPracticeSuggestion": "Try writing an email to explain a project delay."
            }
            """;

        var result = WritingExerciseSubmitHandler.ParseFeedback(json);

        result.WhatYouDidWell.Should().HaveCount(2);
        result.WhatYouDidWell![0].Should().Contain("formal greeting");
        result.MainMistakes.Should().HaveCount(1);
        result.GrammarExplanation.Should().Contain("the approval");
        result.ToneExplanation.Should().Contain("ASAP");
        result.VocabularyToRemember.Should().HaveCount(2);
        result.RewriteChallenge.Should().Contain("I hope this email finds you well");
        result.NextPracticeSuggestion.Should().Contain("delay");
    }

    [Fact]
    public void ParseFeedback_V2TeachingFieldsMissing_ReturnsNulls()
    {
        var result = WritingExerciseSubmitHandler.ParseFeedback("{\"overallScore\": 50}");
        result.WhatYouDidWell.Should().BeNull();
        result.GrammarExplanation.Should().BeNull();
        result.RewriteChallenge.Should().BeNull();
    }

    [Fact]
    public void ParseFeedback_JsonWrappedInMarkdownFence_Parsed()
    {
        var json = "```json\n{\"overallScore\": 60, \"correctedEmail\": \"Fixed.\"}\n```";
        var result = WritingExerciseSubmitHandler.ParseFeedback(json);
        result.OverallScore.Should().Be(60);
    }

    [Fact]
    public void ParseFeedback_JsonWrappedInPlainFence_Parsed()
    {
        var json = "```\n{\"overallScore\": 55}\n```";
        var result = WritingExerciseSubmitHandler.ParseFeedback(json);
        result.OverallScore.Should().Be(55);
    }

    [Fact]
    public void ParseFeedback_NullableFieldsMissing_ReturnsNulls()
    {
        var result = WritingExerciseSubmitHandler.ParseFeedback("{}");
        result.OverallScore.Should().BeNull();
        result.CorrectedEmail.Should().BeNull();
        result.GrammarIssues.Should().BeNull();
    }

    // ── Invalid JSON ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseFeedback_InvalidJson_ThrowsValidationException()
    {
        var act = () => WritingExerciseSubmitHandler.ParseFeedback("not json at all");
        act.Should().Throw<AiResponseValidationException>()
           .WithMessage("*not valid JSON*");
    }

    [Fact]
    public void ParseFeedback_EmptyString_ThrowsValidationException()
    {
        var act = () => WritingExerciseSubmitHandler.ParseFeedback(string.Empty);
        act.Should().Throw<AiResponseValidationException>();
    }

    // ── Score edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void ParseFeedback_ScoreZero_IsValid()
    {
        var result = WritingExerciseSubmitHandler.ParseFeedback("{\"overallScore\": 0}");
        result.OverallScore.Should().Be(0);
    }

    [Fact]
    public void ParseFeedback_Score100_IsValid()
    {
        var result = WritingExerciseSubmitHandler.ParseFeedback("{\"overallScore\": 100}");
        result.OverallScore.Should().Be(100);
    }
}
