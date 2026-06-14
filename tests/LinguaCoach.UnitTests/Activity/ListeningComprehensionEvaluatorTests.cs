using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Infrastructure.Activity;

namespace LinguaCoach.UnitTests.Activity;

public sealed class ListeningComprehensionEvaluatorTests
{
    private readonly ListeningComprehensionEvaluator _sut = new();

    private const string RawLegacyJson = """
    {
      "scenario": "A colleague leaves a voicemail.",
      "speakerRole": "Manager",
      "listenerRole": "You",
      "audioScript": "Hi, please send me the report by 5pm today.",
      "transcriptAvailableAfterSubmit": true,
      "questions": [{"id": "q1", "question": "What was requested?", "expectedAnswer": "the report", "type": "short_answer"}],
      "responseTask": null
    }
    """;

    private static string Wrap(string schemaVersion, string exerciseDataJson) => $$"""
    {
      "schemaVersion": "{{schemaVersion}}",
      "learnContent": {
        "teachingTitle": "t", "explanation": "e", "keyPoints": [], "examples": [],
        "strategy": null, "commonMistakes": [], "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "i", "scenario": null, "task": null,
        "exerciseData": {{exerciseDataJson}}
      },
      "feedbackPlan": {
        "evaluationCriteria": [], "rubric": [], "feedbackFocus": null, "successCriteria": []
      }
    }
    """;

    private static readonly IReadOnlyList<ListeningAnswerDto> CorrectAnswers =
        [new ListeningAnswerDto("q1", "the report")];

    [Fact]
    public void Evaluate_WithRawLegacyJson_ScoresCorrectly()
    {
        var (feedbackJson, score) = _sut.Evaluate(RawLegacyJson, CorrectAnswers, null);

        score.Should().BeGreaterThanOrEqualTo(85);
        using var doc = JsonDocument.Parse(feedbackJson);
        doc.RootElement.GetProperty("transcript").GetString().Should().Be("Hi, please send me the report by 5pm today.");
    }

    [Fact]
    public void Evaluate_WithLegacyAdaptedV1_ProducesSameScoreAsRawLegacy()
    {
        var staged = Wrap(ModuleStageSchema.LegacyAdaptedVersion, RawLegacyJson);

        var (rawFeedback, rawScore) = _sut.Evaluate(RawLegacyJson, CorrectAnswers, null);
        var (stagedFeedback, stagedScore) = _sut.Evaluate(staged, CorrectAnswers, null);

        stagedScore.Should().Be(rawScore);
        using var rawDoc = JsonDocument.Parse(rawFeedback);
        using var stagedDoc = JsonDocument.Parse(stagedFeedback);
        stagedDoc.RootElement.GetProperty("transcript").GetString()
            .Should().Be(rawDoc.RootElement.GetProperty("transcript").GetString());
    }

    [Fact]
    public void Evaluate_WithModuleStageV1_ProducesSameScoreAsRawLegacy()
    {
        var exerciseData = """
        {
          "speakerRole": "Manager",
          "listenerRole": "You",
          "audioScript": "Hi, please send me the report by 5pm today.",
          "transcriptAvailableAfterSubmit": true,
          "questions": [{"id": "q1", "question": "What was requested?", "expectedAnswer": "the report", "type": "short_answer"}],
          "responseTask": null
        }
        """;
        var staged = Wrap(ModuleStageSchema.Version, exerciseData);

        var (rawFeedback, rawScore) = _sut.Evaluate(RawLegacyJson, CorrectAnswers, null);
        var (stagedFeedback, stagedScore) = _sut.Evaluate(staged, CorrectAnswers, null);

        stagedScore.Should().Be(rawScore);
        using var rawDoc = JsonDocument.Parse(rawFeedback);
        using var stagedDoc = JsonDocument.Parse(stagedFeedback);
        stagedDoc.RootElement.GetProperty("questionFeedback")[0].GetProperty("score").GetDouble()
            .Should().Be(rawDoc.RootElement.GetProperty("questionFeedback")[0].GetProperty("score").GetDouble());
    }
}
