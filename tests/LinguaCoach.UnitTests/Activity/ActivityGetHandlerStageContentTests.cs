using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Infrastructure.Activity;

namespace LinguaCoach.UnitTests.Activity;

public sealed class ActivityGetHandlerStageContentTests
{
    private const string LegacyFlatJson = """
    {
      "scenario": "A colleague leaves a voicemail.",
      "speakerRole": "Manager",
      "listenerRole": "You",
      "instructions": "Listen and answer the questions.",
      "audioScript": "Hi, please send me the report by 5pm today.",
      "transcriptAvailableAfterSubmit": true,
      "questions": [{"id": "q1", "question": "What was requested?", "expectedAnswer": "the report", "type": "short_answer"}],
      "responseTask": {"prompt": "Reply confirming you will send it.", "expectedFocus": "report, today"}
    }
    """;

    private const string StagedJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Voicemail practice",
      "moduleGoal": "Understand a workplace voicemail",
      "skillFocus": "listening",
      "exerciseType": "listening_comprehension",
      "learnContent": {
        "teachingTitle": "Listening for action and deadline",
        "explanation": "Listen for the main idea, the action requested, and any deadline.",
        "keyPoints": ["Focus on verbs", "Note any dates or times"],
        "examples": [{"phrase": "by end of day", "meaning": "before today finishes", "note": "common deadline phrase"}],
        "strategy": "Listen for who, what, and when.",
        "commonMistakes": ["Missing the deadline"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen and answer the questions.",
        "scenario": "A colleague leaves a voicemail.",
        "task": null,
        "exerciseData": {
          "speakerRole": "Manager",
          "listenerRole": "You",
          "audioScript": "Hi, please send me the report by 5pm today.",
          "transcriptAvailableAfterSubmit": true,
          "questions": [{"id": "q1", "question": "What was requested?", "expectedAnswer": "the report", "type": "short_answer"}],
          "responseTask": null
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understood"],
        "rubric": [{"criterion": "Main idea", "description": "Identifies the request", "weight": 1.0}],
        "feedbackFocus": "Main idea and deadline",
        "successCriteria": ["Identifies the requested action and deadline"]
      }
    }
    """;

    [Fact]
    public void BuildStageContent_WithLegacyFlatJson_ReturnsLegacyAdaptedWithGenericLearnContent()
    {
        var result = ActivityGetHandler.BuildStageContent(LegacyFlatJson, "Voicemail practice");

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be(ModuleStageSchema.LegacyAdaptedVersion);
        result.Learn.TeachingTitle.Should().Be("Voicemail practice");
        result.Learn.KeyPoints.Should().BeEmpty();
        result.Learn.Examples.Should().BeEmpty();

        result.Practice.Instructions.Should().Be("Listen and answer the questions.");
        result.Practice.Scenario.Should().Be("A colleague leaves a voicemail.");
        result.Practice.Task.Should().Be("Reply confirming you will send it.");

        using var exerciseData = JsonDocument.Parse(result.Practice.ExerciseData.GetRawText());
        exerciseData.RootElement.GetProperty("audioScript").GetString()
            .Should().Be("Hi, please send me the report by 5pm today.");
        exerciseData.RootElement.GetProperty("questions").GetArrayLength().Should().Be(1);
        exerciseData.RootElement.GetProperty("responseTask").GetProperty("prompt").GetString()
            .Should().Be("Reply confirming you will send it.");
    }

    [Fact]
    public void BuildStageContent_WithModuleStageV1Json_MapsFieldsOneToOne()
    {
        var result = ActivityGetHandler.BuildStageContent(StagedJson, "Voicemail practice");

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be(ModuleStageSchema.Version);
        result.Learn.TeachingTitle.Should().Be("Listening for action and deadline");
        result.Learn.KeyPoints.Should().BeEquivalentTo(["Focus on verbs", "Note any dates or times"]);
        result.Learn.Examples.Should().ContainSingle(e => e.Phrase == "by end of day");
        result.Learn.Strategy.Should().Be("Listen for who, what, and when.");
        result.Learn.CommonMistakes.Should().BeEquivalentTo(["Missing the deadline"]);

        result.Practice.Instructions.Should().Be("Listen and answer the questions.");
        result.Practice.Scenario.Should().Be("A colleague leaves a voicemail.");

        using var exerciseData = JsonDocument.Parse(result.Practice.ExerciseData.GetRawText());
        exerciseData.RootElement.GetProperty("audioScript").GetString()
            .Should().Be("Hi, please send me the report by 5pm today.");

        result.FeedbackPlan.EvaluationCriteria.Should().BeEquivalentTo(["Main idea understood"]);
        result.FeedbackPlan.Rubric.Should().ContainSingle(r => r.Criterion == "Main idea");
    }
}
