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
    public void Validate_ForNonListeningActivityType_SkipsRequiredPracticeKeyCheck()
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

        result.IsValid.Should().BeTrue();
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
