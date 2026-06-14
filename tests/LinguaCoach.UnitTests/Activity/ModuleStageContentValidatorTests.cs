using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Activity;

public sealed class ModuleStageContentValidatorTests
{
    private const string ValidListeningJson = """
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


    private const string ValidWritingJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Write a manager update",
      "moduleGoal": "Write a clear short update to a manager.",
      "primarySkill": "writing",
      "secondarySkills": ["grammar", "vocabulary"],
      "exerciseType": "writing_scenario",
      "learnContent": {
        "teachingTitle": "Clear status updates",
        "explanation": "A good update starts with the main point, then adds the key detail. Keep the tone polite and direct.",
        "keyPoints": ["Start with purpose", "Use polite tone", "Keep sentences clear"],
        "examples": [{"phrase": "I wanted to update you on", "meaning": "opens a status update", "note": "polite and direct"}],
        "strategy": "Plan the audience, purpose, and key details before writing.",
        "commonMistakes": ["Giving too much background first"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Write a short professional update.",
        "scenario": "Your task is delayed because another team has not sent data.",
        "task": "Write to your manager.",
        "exerciseData": {
          "situation": "Your task is delayed because another team has not sent data.",
          "audience": "your manager",
          "tone": "polite and professional",
          "expectedLength": "3-5 sentences",
          "prompt": "Write a short update to your manager explaining the delay and next step.",
          "requiredPhrases": ["I wanted to update you"],
          "targetVocabulary": ["delay"],
          "successChecklist": ["Explain the delay", "Include next step"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Clarity", "Tone", "Grammar accuracy", "Vocabulary use"],
        "rubric": [{"criterion": "Task completion", "description": "Addresses the situation", "weight": 1.0}],
        "feedbackFocus": "Clarity, tone, grammar, and task completion",
        "successCriteria": ["The message is clear and complete."]
      }
    }
    """;

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Validate_WithValidListeningPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(Parse(ValidListeningJson), ActivityType.ListeningComprehension);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("learnContent")]
    [InlineData("practiceContent")]
    [InlineData("feedbackPlan")]
    public void Validate_WithMissingTopLevelSection_Fails(string propertyToRemove)
    {
        using var doc = JsonDocument.Parse(ValidListeningJson);
        var json = RemoveProperty(doc.RootElement, propertyToRemove);

        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.ListeningComprehension);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("questions")]
    [InlineData("transcript")]
    [InlineData("expectedAnswer")]
    public void Validate_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        using var doc = JsonDocument.Parse(ValidListeningJson);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("learnContent"))
                {
                    writer.WritePropertyName("learnContent");
                    writer.WriteStartObject();
                    foreach (var learnProp in prop.Value.EnumerateObject())
                    {
                        learnProp.WriteTo(writer);
                    }
                    writer.WriteString(forbiddenKey, "should not be here");
                    writer.WriteEndObject();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.ListeningComprehension);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("questions")]
    public void Validate_WithMissingRequiredExerciseDataKey_ForListening_Fails(string requiredKey)
    {
        using var doc = JsonDocument.Parse(ValidListeningJson);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("practiceContent"))
                {
                    writer.WritePropertyName("practiceContent");
                    writer.WriteStartObject();
                    foreach (var pcProp in prop.Value.EnumerateObject())
                    {
                        if (pcProp.NameEquals("exerciseData"))
                        {
                            writer.WritePropertyName("exerciseData");
                            writer.WriteStartObject();
                            foreach (var edProp in pcProp.Value.EnumerateObject())
                            {
                                if (!edProp.NameEquals(requiredKey))
                                    edProp.WriteTo(writer);
                            }
                            writer.WriteEndObject();
                        }
                        else
                        {
                            pcProp.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.ListeningComprehension);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WithMissingRequiredExerciseDataKey_ForWriting_Fails()
    {
        using var doc = JsonDocument.Parse(ValidListeningJson);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("practiceContent"))
                {
                    writer.WritePropertyName("practiceContent");
                    writer.WriteStartObject();
                    foreach (var pcProp in prop.Value.EnumerateObject())
                    {
                        if (!pcProp.NameEquals("exerciseData"))
                            pcProp.WriteTo(writer);
                    }
                    writer.WritePropertyName("exerciseData");
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.WritingScenario);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("prompt"));
    }


    [Fact]
    public void Validate_WithValidWritingPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(Parse(ValidWritingJson), ActivityType.WritingScenario);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithPracticeOnlyWritingKeyInLearnContent_Fails()
    {
        using var doc = JsonDocument.Parse(ValidWritingJson);
        var json = AddLearnProperty(doc.RootElement, "textarea", "not allowed");

        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.WritingScenario);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("textarea"));
    }

    private static string AddLearnProperty(JsonElement root, string name, string value)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals("learnContent"))
                {
                    writer.WritePropertyName("learnContent");
                    writer.WriteStartObject();
                    foreach (var learnProp in prop.Value.EnumerateObject())
                        learnProp.WriteTo(writer);
                    writer.WriteString(name, value);
                    writer.WriteEndObject();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string RemoveProperty(JsonElement root, string propertyToRemove)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (!prop.NameEquals(propertyToRemove))
                    prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
