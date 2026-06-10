using FluentAssertions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity.Evaluators;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Unit tests for AiOpenEndedEvaluator.ParseAndNormalise — the pure parsing/normalisation path.
/// Provider failure is covered by integration tests.
/// </summary>
public sealed class AiOpenEndedEvaluatorTests
{
    // ── MarkingMode property ───────────────────────────────────────────────────

    [Fact]
    public void MarkingMode_IsAiOpenEnded()
    {
        var mode = MarkingMode.AiOpenEnded;
        ((int)mode).Should().Be(0); // enum value stable per domain contract
    }

    // ── ParseAndNormalise — valid JSON ─────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_ValidJson_MapsScore()
    {
        var json = """{"score":72,"coachSummary":"Good spoken response with minor pronunciation notes."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(72);
        result.MaxScore.Should().Be(100);
        result.Percentage.Should().Be(72);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public void ParseAndNormalise_OverallScoreField_AlsoMapped()
    {
        // spoken_response prompt uses "score"; fall through to "overallScore" fallback
        var json = """{"overallScore":68,"coachSummary":"Decent response."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(68);
    }

    [Fact]
    public void ParseAndNormalise_ValidJson_MapsCoachSummary()
    {
        var json = """{"score":80,"coachSummary":"Clear and confident delivery."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.CoachSummary.Should().Be("Clear and confident delivery.");
    }

    [Fact]
    public void ParseAndNormalise_ScoreBelowPassThreshold_ReturnsFailed()
    {
        var json = """{"score":55,"coachSummary":"Needs more vocabulary."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
    }

    // ── Score clamping ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_ScoreAbove100_ClampedTo100()
    {
        var json = """{"score":130,"coachSummary":"Excellent."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(100);
    }

    [Fact]
    public void ParseAndNormalise_NegativeScore_ClampedToZero()
    {
        var json = """{"score":-5,"coachSummary":"Very poor."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(0);
    }

    // ── Corrections from improvements/missingExpectedPoints ───────────────────

    [Fact]
    public void ParseAndNormalise_Improvements_MappedAsCorrections()
    {
        var json = """
            {
              "score": 70,
              "improvements": ["Use more formal vocabulary", "Structure your answer better"]
            }
            """;

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Corrections.Should().HaveCount(2);
        result.Corrections[0].Category.Should().Be("speaking");
    }

    [Fact]
    public void ParseAndNormalise_MissingExpectedPoints_MappedAsCorrections()
    {
        var json = """
            {
              "score": 65,
              "missingExpectedPoints": ["Mention the deadline", "Acknowledge the request"]
            }
            """;

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Corrections.Should().HaveCount(2);
        result.Corrections[0].Category.Should().Be("missing_point");
    }

    [Fact]
    public void ParseAndNormalise_SixImprovements_CappedAt5()
    {
        var improvements = string.Join(",", Enumerable.Range(1, 6).Select(i => $"\"improvement {i}\""));
        var json = $$"""{ "score": 70, "improvements": [{{improvements}}] }""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Corrections.Should().HaveCount(5);
    }

    // ── SuggestedImprovedResponse ──────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_SuggestedImprovedResponse_Mapped()
    {
        var json = """{"score":75,"suggestedImprovedResponse":"I would like to inform you that..."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.SuggestedImprovedAnswer.Should().Contain("I would like to inform you");
    }

    // ── Invalid/malformed JSON ─────────────────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_InvalidJson_ReturnsControlledFallback()
    {
        var result = AiOpenEndedEvaluator.ParseAndNormalise("not valid json");

        result.Completed.Should().BeFalse();
        result.CoachSummary.Should().NotBeNullOrWhiteSpace();
        result.Score.Should().Be(0);
    }

    [Fact]
    public void ParseAndNormalise_EmptyJsonObject_DefaultsScore60()
    {
        var result = AiOpenEndedEvaluator.ParseAndNormalise("{}");

        result.Completed.Should().BeTrue();
        result.Score.Should().Be(60);
    }

    // ── Markdown-fenced JSON extraction ───────────────────────────────────────

    [Fact]
    public void ParseAndNormalise_MarkdownFenced_ExtractsJson()
    {
        var json = "```json\n{\"score\":78,\"coachSummary\":\"Good.\"}\n```";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(78);
    }

    [Fact]
    public void ParseAndNormalise_PrefixedText_ExtractsFirstJsonObject()
    {
        var json = "Here is the evaluation:\n{\"score\":66,\"coachSummary\":\"Fair.\"}";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(66);
    }
}
