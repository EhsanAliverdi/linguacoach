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

    // ── CompactContent — staged unwrapping ────────────────────────────────────

    [Fact]
    public void CompactContent_StagedContent_ExcludesLearnContentAndReturnsExerciseData()
    {
        var staged = """
        {
          "schemaVersion": "module_stage_v1",
          "title": "Write a status update",
          "learnContent": { "teachingTitle": "SHOULD NOT APPEAR", "explanation": "hidden strategy" },
          "practiceContent": {
            "instructions": "Write a response.",
            "exerciseData": {
              "prompt": "Write a short status update for your manager.",
              "tone": "professional",
              "expectedLength": "60-80 words"
            }
          },
          "feedbackPlan": { "evaluationCriteria": ["Task completion"], "feedbackFocus": "clarity" }
        }
        """;

        var result = AiOpenEndedEvaluator.CompactContent(staged);

        result.Should().Contain("prompt");
        result.Should().Contain("feedbackFocus");
        result.Should().NotContain("SHOULD NOT APPEAR");
        result.Should().NotContain("learnContent");
    }

    [Fact]
    public void CompactContent_LegacyFlatContent_ReturnedAsIs()
    {
        var legacy = """{"prompt":"Write a short update.","situation":"Your manager needs an update."}""";

        var result = AiOpenEndedEvaluator.CompactContent(legacy);

        result.Should().Contain("prompt");
        result.Should().Contain("situation");
    }

    [Fact]
    public void CompactContent_EmptyJson_ReturnsEmptyObject()
    {
        AiOpenEndedEvaluator.CompactContent("{}").Should().Be("{}");
    }

    // ── respond_to_situation — evaluation response parsing ────────────────────

    [Fact]
    public void ParseAndNormalise_RespondToSituation_HighScore_Passes()
    {
        var json = """
            {
              "overallScore": 82,
              "coachSummary": "Your response was clear and appropriate for the situation.",
              "strengths": ["Relevant", "Natural phrasing"],
              "improvements": ["Could be slightly more formal"]
            }
            """;

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(82);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.CoachSummary.Should().Contain("clear and appropriate");
    }

    [Fact]
    public void ParseAndNormalise_RespondToSituation_LowScore_Fails()
    {
        var json = """{"overallScore":45,"coachSummary":"Response was off-topic."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(45);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public void CompactContent_RespondToSituationStaged_ExcludesLearnContent()
    {
        var staged = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "HIDDEN", "explanation": "strategy here" },
          "practiceContent": {
            "exerciseData": {
              "items": [
                {
                  "id": "sit1",
                  "situation": "You are at a hotel check-in and the room is not ready.",
                  "expectedResponseGuidance": "Politely ask when the room will be ready."
                }
              ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "relevance and tone" }
        }
        """;

        var result = AiOpenEndedEvaluator.CompactContent(staged);

        result.Should().Contain("sit1");
        result.Should().Contain("feedbackFocus");
        result.Should().NotContain("HIDDEN");
        result.Should().NotContain("learnContent");
    }

    // ── describe_image — evaluation response parsing ──────────────────────────

    [Fact]
    public void ParseAndNormalise_DescribeImage_HighScore_Passes()
    {
        var json = """
            {
              "overallScore": 78,
              "coachSummary": "Your description was clear and covered the main elements.",
              "strengths": ["Good use of location words", "Varied vocabulary"],
              "improvements": ["Could mention colours more specifically"]
            }
            """;

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(78);
        result.Passed.Should().BeTrue();
        result.Completed.Should().BeTrue();
        result.CoachSummary.Should().Contain("clear and covered");
    }

    [Fact]
    public void ParseAndNormalise_DescribeImage_LowScore_Fails()
    {
        var json = """{"overallScore":35,"coachSummary":"Description did not address the image."}""";

        var result = AiOpenEndedEvaluator.ParseAndNormalise(json);

        result.Score.Should().Be(35);
        result.Passed.Should().BeFalse();
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public void CompactContent_DescribeImageStaged_ExcludesLearnContent()
    {
        var staged = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "HIDDEN", "explanation": "description strategy" },
          "practiceContent": {
            "exerciseData": {
              "items": [
                {
                  "id": "img1",
                  "imagePrompt": "A busy street market with colourful stalls.",
                  "expectedResponseGuidance": "Describe the setting, people, and objects."
                }
              ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "detail and vocabulary" }
        }
        """;

        var result = AiOpenEndedEvaluator.CompactContent(staged);

        result.Should().Contain("img1");
        result.Should().Contain("feedbackFocus");
        result.Should().NotContain("HIDDEN");
        result.Should().NotContain("learnContent");
    }
}
