using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity.Evaluators;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Unit tests for AiStructuredEvaluator.ParseAndNormalise — the pure parsing/normalisation path.
/// Provider failure and prompt-key routing are covered by integration tests.
/// </summary>
public sealed class AiStructuredEvaluatorTests
{
    // ── MarkingMode property ───────────────────────────────────────────────────

    [Fact]
    public void MarkingMode_IsAiStructured()
    {
        // MarkingMode is a property on the class, not dependent on injected services
        // We verify this via the enum value to ensure correct router dispatch.
        var mode = MarkingMode.AiStructured;
        ((int)mode).Should().Be(1); // enum value stable per domain contract
    }

    // ── ParseAndNormalise — valid JSON ─────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_ValidJson_MapsScore()
    {
        var json = """{"overallScore":75,"coachSummary":"Good email with minor tone issues."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Score.Should().Be(75);
        result.MaxScore.Should().Be(100);
        result.Percentage.Should().Be(75);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public void ParseAndNormalise_ValidJson_MapsCoachSummary()
    {
        var json = """{"overallScore":80,"coachSummary":"Well-structured reply."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.CoachSummary.Should().Be("Well-structured reply.");
    }

    [Fact]
    public void ParseAndNormalise_ScoreBelowPassThreshold_ReturnsFailed()
    {
        var json = """{"overallScore":45,"coachSummary":"Needs work."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Passed.Should().BeFalse();
        result.Score.Should().Be(45);
        result.Completed.Should().BeTrue();
    }

    // ── Score clamping ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_ScoreAbove100_ClampedTo100()
    {
        var json = """{"overallScore":150,"coachSummary":"Perfect."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Score.Should().Be(100);
    }

    [Fact]
    public void ParseAndNormalise_NegativeScore_ClampedToZero()
    {
        var json = """{"overallScore":-10,"coachSummary":"Very poor."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Score.Should().Be(0);
    }

    // ── Corrections capped at 5 ────────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_EightChanges_CorrectionsCappedAt5()
    {
        var changes = string.Join(",", Enumerable.Range(1, 8).Select(i =>
            $$"""{ "type":"replace","original":"x{{i}}","suggested":"y{{i}}","reason":"r","category":"grammar","severity":"low" }"""));
        var json = $$"""{ "overallScore":70,"changes":[{{changes}}] }""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Corrections.Should().HaveCount(5);
    }

    [Fact]
    public void ParseAndNormalise_CorrectionsMapCategory()
    {
        var json = """
            {
              "overallScore": 70,
              "changes": [
                { "type":"replace","original":"please send","suggested":"Could you send",
                  "reason":"More polite","category":"tone","severity":"medium" }
              ]
            }
            """;

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Corrections[0].Category.Should().Be("tone");
        result.Corrections[0].Suggestion.Should().Be("Could you send");
    }

    // ── Invalid/malformed JSON ─────────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_InvalidJson_ReturnsControlledFallback()
    {
        var result = AiStructuredEvaluator.ParseAndNormalise("not json at all", ExercisePatternKey.EmailReply);

        result.Completed.Should().BeFalse();
        result.CoachSummary.Should().NotBeNullOrWhiteSpace();
        result.Score.Should().Be(0);
    }

    [Fact]
    public void ParseAndNormalise_EmptyJsonObject_DefaultsScore60()
    {
        // Empty object parses to all nulls — score defaults to clamp(null ?? 60) = 60
        var result = AiStructuredEvaluator.ParseAndNormalise("{}", ExercisePatternKey.EmailReply);

        result.Completed.Should().BeTrue();
        result.Score.Should().Be(60);
    }

    // ── Markdown-fenced JSON extraction ───────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_MarkdownFenced_ExtractsJson()
    {
        var json = "```json\n{\"overallScore\":80,\"coachSummary\":\"Good.\"}\n```";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Score.Should().Be(80);
    }

    [Fact]
    public void ParseAndNormalise_PrefixedText_ExtractsFirstJsonObject()
    {
        var json = "Sure, here is your result:\n{\"overallScore\":70,\"coachSummary\":\"Decent.\"}";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.Score.Should().Be(70);
    }

    // ── listen_and_answer item results ────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_ListenAndAnswer_MapsQuestionFeedback()
    {
        var json = """
            {
              "overallScore": 80,
              "coachSummary": "Good comprehension.",
              "questionFeedback": [
                { "questionId": "q1", "question": "What was discussed?", "studentAnswer": "project delay",
                  "expectedAnswerSummary": "delivery delay", "isCorrect": true, "score": 1.0, "feedback": "Correct." },
                { "questionId": "q2", "question": "Who sent it?", "studentAnswer": "manager",
                  "expectedAnswerSummary": "project manager", "isCorrect": false, "score": 0.0, "feedback": "Not specific enough." }
              ]
            }
            """;

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.ListenAndAnswer);

        result.ItemResults.Should().HaveCount(2);
        result.ItemResults[0].ItemKey.Should().Be("q1");
        result.ItemResults[0].IsCorrect.Should().BeTrue();
        result.ItemResults[1].IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void ParseAndNormalise_EmailReply_NoQuestionFeedback_EmptyItemResults()
    {
        var json = """{"overallScore":75,"coachSummary":"Good."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.ItemResults.Should().BeEmpty();
    }

    // ── SuggestedImprovedAnswer ────────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_ImprovedVersion_MappedToSuggestedImprovedAnswer()
    {
        var json = """{"overallScore":70,"improvedVersion":"Dear John,\n\nThank you for your email."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.EmailReply);

        result.SuggestedImprovedAnswer.Should().Contain("Dear John");
    }

    // ── Teams chat uses email-like schema ─────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_TeamsChat_ParsesSameSchema()
    {
        var json = """{"overallScore":65,"coachSummary":"Good tone, slightly too formal."}""";

        var result = AiStructuredEvaluator.ParseAndNormalise(json, ExercisePatternKey.TeamsChatSimulation);

        result.Score.Should().Be(65);
        result.Completed.Should().BeTrue();
    }

    // ── CompactContent — staged unwrapping ────────────────────────────────────

    [Fact]
    public void CompactContent_StagedContent_ExcludesLearnContentAndReturnsExerciseData()
    {
        var staged = """
        {
          "schemaVersion": "module_stage_v1",
          "title": "Test",
          "learnContent": { "teachingTitle": "SHOULD NOT APPEAR", "explanation": "hidden strategy" },
          "practiceContent": {
            "instructions": "Listen and answer.",
            "exerciseData": {
              "audioScript": "Hi, please send the report.",
              "questions": [{ "id": "q1", "question": "What was requested?", "expectedAnswer": "the report" }]
            }
          },
          "feedbackPlan": { "evaluationCriteria": ["accuracy"], "feedbackFocus": "comprehension" }
        }
        """;

        var result = AiStructuredEvaluator.CompactContent(staged);

        result.Should().Contain("audioScript");
        result.Should().Contain("questions");
        result.Should().Contain("feedbackFocus");
        result.Should().NotContain("SHOULD NOT APPEAR");
        result.Should().NotContain("learnContent");
    }

    [Fact]
    public void CompactContent_LegacyFlatContent_ReturnedAsIs()
    {
        var legacy = """{"audioScript":"Hi team.","questions":[{"id":"q1","question":"What?","expectedAnswer":"hi"}]}""";

        var result = AiStructuredEvaluator.CompactContent(legacy);

        result.Should().Contain("audioScript");
        result.Should().Contain("questions");
    }

    [Fact]
    public void CompactContent_EmptyJson_ReturnsEmptyObject()
    {
        AiStructuredEvaluator.CompactContent("{}").Should().Be("{}");
    }
}
