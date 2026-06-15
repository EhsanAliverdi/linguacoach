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


    private const string LegacyWritingJson = """
    {
      "situation": "Your task is delayed because another team has not sent data.",
      "audience": "your manager",
      "tone": "polite and professional",
      "expectedLength": "3-5 sentences",
      "learningGoal": "Write a clear short update to a manager.",
      "skillFocus": "manager updates",
      "targetPhrases": ["I wanted to update you"],
      "targetVocabulary": ["delay"],
      "exampleText": "I wanted to update you on the report.",
      "commonMistakeToAvoid": "Do not sound too direct.",
      "instructionInSourceLanguage": "Support text."
    }
    """;

    private const string StagedWritingJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Write a manager update",
      "moduleGoal": "Write a clear short update to a manager.",
      "primarySkill": "writing",
      "secondarySkills": ["grammar", "vocabulary"],
      "exerciseType": "writing_scenario",
      "learnContent": {
        "teachingTitle": "Clear status updates",
        "explanation": "A good update starts with the main point.",
        "keyPoints": ["Start with purpose"],
        "examples": [{"phrase": "I wanted to update you", "meaning": "opens a status update", "note": "polite"}],
        "strategy": "Plan before writing.",
        "commonMistakes": ["Too much background"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Write a short professional update.",
        "scenario": "Your task is delayed.",
        "task": "Write to your manager.",
        "exerciseData": {
          "situation": "Your task is delayed.",
          "audience": "your manager",
          "tone": "polite",
          "prompt": "Write a short update to your manager."
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion"],
        "rubric": [],
        "feedbackFocus": "Task completion",
        "successCriteria": []
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


    [Fact]
    public void BuildStageContent_WithLegacyWritingJson_ReturnsLegacyAdaptedWritingContent()
    {
        var result = ActivityGetHandler.BuildStageContent(LegacyWritingJson, "Manager update");

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be(ModuleStageSchema.LegacyAdaptedVersion);
        result.PrimarySkill.Should().Be("writing");
        result.ExerciseType.Should().Be("writing_scenario");
        result.Learn.TeachingTitle.Should().Be("Manager update");
        result.Learn.Strategy.Should().NotBeNullOrWhiteSpace();

        using var exerciseData = JsonDocument.Parse(result.Practice.ExerciseData.GetRawText());
        exerciseData.RootElement.GetProperty("prompt").GetString().Should().Contain("delayed");
        exerciseData.RootElement.GetProperty("situation").GetString().Should().Contain("delayed");
        exerciseData.RootElement.GetProperty("audience").GetString().Should().Be("your manager");
        exerciseData.RootElement.GetProperty("tone").GetString().Should().Be("polite and professional");
    }

    [Fact]
    public void BuildStageContent_WithStagedWritingJson_MapsWritingMetadata()
    {
        var result = ActivityGetHandler.BuildStageContent(StagedWritingJson, "Manager update");

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be(ModuleStageSchema.Version);
        result.PrimarySkill.Should().Be("writing");
        result.SecondarySkills.Should().BeEquivalentTo(["grammar", "vocabulary"]);
        result.ExerciseType.Should().Be("writing_scenario");

        using var exerciseData = JsonDocument.Parse(result.Practice.ExerciseData.GetRawText());
        exerciseData.RootElement.GetProperty("prompt").GetString().Should().Be("Write a short update to your manager.");
        exerciseData.RootElement.GetProperty("situation").GetString().Should().Be("Your task is delayed.");
        exerciseData.RootElement.GetProperty("audience").GetString().Should().Be("your manager");
        exerciseData.RootElement.GetProperty("tone").GetString().Should().Be("polite");
    }

    private const string LegacySpeakingJson = """
    {
      "activityType": "SpeakingRolePlay",
      "title": "Explain a delay to your manager",
      "scenario": "Your manager asks about a delayed report.",
      "studentRole": "Document Controller",
      "listenerRole": "Manager",
      "speakingGoal": "Explain the delay clearly and offer a next step.",
      "prompt": "Record a short response explaining the delay.",
      "expectedPoints": ["mention the delay", "give a reason", "state next step"],
      "suggestedPhrases": ["I wanted to update you", "The reason for the delay is"],
      "maxDurationSeconds": 60
    }
    """;

    private const string StagedSpeakingJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Explain a project delay to your manager",
      "moduleGoal": "Explain a delay clearly and professionally in 30-60 seconds.",
      "primarySkill": "speaking",
      "secondarySkills": ["listening", "vocabulary"],
      "exerciseType": "speaking_roleplay",
      "learnContent": {
        "teachingTitle": "Explaining delays professionally",
        "explanation": "When explaining a delay, state the problem, the reason, and the next step.",
        "keyPoints": ["State the issue first.", "Give one clear reason."],
        "examples": [{"phrase": "I wanted to update you", "meaning": "Opens a status update politely", "note": "Use a professional tone"}],
        "strategy": "Before recording, decide: what happened, why, and what you will do next.",
        "commonMistakes": ["Giving too much background."],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Record a 30-60 second response.",
        "scenario": "Your manager asks why the report is late.",
        "task": "Explain the delay and give your next step.",
        "exerciseData": {
          "role": "Document Controller",
          "partnerRole": "Manager",
          "situation": "Your manager asked why the report is late.",
          "prompt": "Record a short response explaining the delay.",
          "expectedResponseLength": "30-60 seconds",
          "tone": "professional and direct",
          "requiredPhrases": ["I wanted to update you"],
          "targetVocabulary": ["delay"],
          "successChecklist": ["Mentions the delay", "Gives a reason", "States a next step"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Fluency", "Tone"],
        "rubric": [{"criterion": "Task completion", "description": "Addresses the delay", "weight": 0.3}],
        "feedbackFocus": "Task completion and fluency",
        "successCriteria": ["The response is clear.", "The tone fits the situation."]
      }
    }
    """;

    [Fact]
    public void BuildStageContent_WithLegacySpeakingJson_ReturnsLegacyAdaptedWithGenericLearnContent()
    {
        var result = ActivityGetHandler.BuildStageContent(LegacySpeakingJson, "Explain a delay");

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be(ModuleStageSchema.LegacyAdaptedVersion);
        result.PrimarySkill.Should().Be("speaking");
        result.ExerciseType.Should().Be("speaking_roleplay");
        result.Learn.TeachingTitle.Should().Be("Explain a delay");
        result.Learn.Strategy.Should().NotBeNullOrWhiteSpace();

        using var exerciseData = JsonDocument.Parse(result.Practice.ExerciseData.GetRawText());
        exerciseData.RootElement.GetProperty("prompt").GetString().Should().NotBeNullOrWhiteSpace();
        exerciseData.RootElement.GetProperty("role").GetString().Should().Be("Document Controller");
        exerciseData.RootElement.GetProperty("partnerRole").GetString().Should().Be("Manager");
        exerciseData.RootElement.GetProperty("situation").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildStageContent_WithLegacySpeakingJson_LearnContentDoesNotContainRecordingControls()
    {
        var result = ActivityGetHandler.BuildStageContent(LegacySpeakingJson, "Explain a delay");

        result.Should().NotBeNull();
        var learnJson = System.Text.Json.JsonSerializer.Serialize(result!.Learn);
        learnJson.Should().NotContainAny("recordingControls", "startRecording", "microphone", "submitLabel");
    }

    [Fact]
    public void BuildStageContent_WithStagedSpeakingJson_MapsSpeakingMetadata()
    {
        var result = ActivityGetHandler.BuildStageContent(StagedSpeakingJson, "Explain a delay");

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be(ModuleStageSchema.Version);
        result.PrimarySkill.Should().Be("speaking");
        result.SecondarySkills.Should().BeEquivalentTo(["listening", "vocabulary"]);
        result.ExerciseType.Should().Be("speaking_roleplay");

        result.Learn.TeachingTitle.Should().Be("Explaining delays professionally");
        result.Learn.Strategy.Should().Contain("what happened");

        using var exerciseData = JsonDocument.Parse(result.Practice.ExerciseData.GetRawText());
        exerciseData.RootElement.GetProperty("role").GetString().Should().Be("Document Controller");
        exerciseData.RootElement.GetProperty("partnerRole").GetString().Should().Be("Manager");
        exerciseData.RootElement.GetProperty("situation").GetString().Should().Contain("manager");
        exerciseData.RootElement.GetProperty("prompt").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildStageContent_WithStagedSpeakingJson_LearnContentDoesNotContainPracticeOnlyFields()
    {
        var result = ActivityGetHandler.BuildStageContent(StagedSpeakingJson, "Explain a delay");

        result.Should().NotBeNull();
        var learnJson = System.Text.Json.JsonSerializer.Serialize(result!.Learn);
        learnJson.Should().NotContainAny("recordingControls", "startRecording", "submitLabel", "exerciseData", "audioScript");
    }

}
