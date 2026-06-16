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

    private const string ValidSpeakingJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Explain a project delay to your manager",
      "moduleGoal": "Explain a delay clearly and professionally in 30-60 seconds.",
      "primarySkill": "speaking",
      "secondarySkills": ["listening", "vocabulary"],
      "exerciseType": "speaking_roleplay",
      "learnContent": {
        "teachingTitle": "Explaining delays professionally",
        "explanation": "When explaining a delay, state the problem, the reason, and the next step. Keep it short and direct.",
        "keyPoints": ["State the issue first.", "Give one clear reason.", "Offer a next step."],
        "examples": [{"phrase": "I wanted to update you on the timeline", "meaning": "Opens a status update politely", "note": "Use a professional tone"}],
        "strategy": "Before recording, decide: what happened, why, and what you will do next.",
        "commonMistakes": ["Giving too much background before the main point.", "Using an overly informal tone."],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Record a 30-60 second response for this workplace situation.",
        "scenario": "Your manager asks you about a delayed report.",
        "task": "Explain the delay and give your next step.",
        "exerciseData": {
          "role": "Document Controller",
          "partnerRole": "Manager",
          "situation": "Your manager just asked why the report is late.",
          "prompt": "Record a short response explaining the delay and your next step.",
          "expectedResponseLength": "30-60 seconds",
          "tone": "professional and direct",
          "requiredPhrases": ["I wanted to update you"],
          "targetVocabulary": ["delay", "update"],
          "successChecklist": ["Mentions the delay", "Gives a reason", "States a next step"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Fluency", "Pronunciation clarity", "Tone", "Grammar and vocabulary"],
        "rubric": [{"criterion": "Task completion", "description": "Addresses the delay and next step", "weight": 0.3}],
        "feedbackFocus": "Task completion, fluency, and tone",
        "successCriteria": ["The response is clear and relevant.", "The tone fits the situation."]
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

    [Fact]
    public void Validate_WithValidSpeakingPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(Parse(ValidSpeakingJson), ActivityType.SpeakingRolePlay);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("learnContent")]
    [InlineData("practiceContent")]
    [InlineData("feedbackPlan")]
    public void Validate_Speaking_WithMissingTopLevelSection_Fails(string propertyToRemove)
    {
        var json = RemoveProperty(Parse(ValidSpeakingJson), propertyToRemove);

        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.SpeakingRolePlay);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("role")]
    [InlineData("partnerRole")]
    [InlineData("situation")]
    public void Validate_Speaking_WithMissingRequiredExerciseDataKey_Fails(string requiredKey)
    {
        using var doc = JsonDocument.Parse(ValidSpeakingJson);
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
        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.SpeakingRolePlay);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(requiredKey));
    }

    [Theory]
    [InlineData("recordingControls")]
    [InlineData("startRecording")]
    [InlineData("microphoneInstructions")]
    public void Validate_Speaking_WithForbiddenSpeakingKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidSpeakingJson), forbiddenKey, "should not be here");

        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.SpeakingRolePlay);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
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

    private const string ValidVocabularyJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Vocabulary practice",
      "moduleGoal": "Use workplace vocabulary accurately.",
      "primarySkill": "vocabulary",
      "secondarySkills": ["reading", "writing"],
      "exerciseType": "vocabulary_practice",
      "learnContent": {
        "teachingTitle": "Learn workplace phrases",
        "explanation": "Learn the meaning and usage before practice.",
        "keyPoints": ["Meaning", "Word form", "Tone"],
        "examples": [{"phrase": "follow up", "meaning": "check again later", "note": "verb phrase"}],
        "strategy": "Read the context first.",
        "commonMistakes": ["Wrong spelling"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Fill in the blank.",
        "scenario": "Workplace vocabulary review",
        "task": "Type the missing phrase.",
        "exerciseData": {
          "items": [{"term": "follow up", "meaning": "check again later", "example": "I will _____ tomorrow.", "correctAnswer": "follow up"}],
          "practiceMode": "fill_blank",
          "successChecklist": ["Choose the correct meaning"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Meaning accuracy", "Context use", "Word form", "Spelling", "Collocation"],
        "rubric": [],
        "feedbackFocus": "Vocabulary feedback",
        "successCriteria": ["The student identifies the correct meaning."]
      }
    }
    """;

    [Fact]
    public void Validate_WithValidVocabularyPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(Parse(ValidVocabularyJson), ActivityType.VocabularyPractice);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("learnContent")]
    [InlineData("practiceContent")]
    [InlineData("feedbackPlan")]
    public void Validate_Vocabulary_WithMissingTopLevelSection_Fails(string propertyToRemove)
    {
        var json = RemoveProperty(Parse(ValidVocabularyJson), propertyToRemove);

        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.VocabularyPractice);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("items")]
    [InlineData("practiceMode")]
    public void Validate_Vocabulary_WithMissingRequiredExerciseDataKey_Fails(string requiredKey)
    {
        using var doc = JsonDocument.Parse(ValidVocabularyJson);
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
                                if (!edProp.NameEquals(requiredKey)) edProp.WriteTo(writer);
                            writer.WriteEndObject();
                        }
                        else pcProp.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                else prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        var result = ModuleStageContentValidator.Validate(Parse(System.Text.Encoding.UTF8.GetString(ms.ToArray())), ActivityType.VocabularyPractice);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(requiredKey));
    }

    [Theory]
    [InlineData("practiceMode")]
    [InlineData("correctAnswer")]
    [InlineData("answerControls")]
    public void Validate_Vocabulary_WithPracticeOnlyLearnKey_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidVocabularyJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(Parse(json), ActivityType.VocabularyPractice);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── phrase_match pattern key tests ─────────────────────────────────────────

    private const string ValidPhraseMatchJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Workplace Phrase Match",
      "moduleGoal": "Match workplace phrases to their meanings.",
      "primarySkill": "vocabulary",
      "secondarySkills": ["reading"],
      "exerciseType": "phrase_match",
      "learnContent": {
        "teachingTitle": "Key workplace phrases",
        "explanation": "These phrases are used in professional workplace communication.",
        "keyPoints": ["Notice the context", "Check the tone"],
        "examples": [{"phrase": "action item", "meaning": "a task assigned to someone", "note": "used in meetings"}],
        "strategy": "Look for the most natural workplace meaning.",
        "commonMistakes": ["Confusing formal and informal register"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Match each phrase to its correct meaning.",
        "scenario": null,
        "task": "Match each workplace phrase to its meaning.",
        "exerciseData": {
          "pairs": [
            {"phrase": "action item", "meaning": "a task assigned to someone", "context": "We have three action items from today's meeting."},
            {"phrase": "follow up", "meaning": "check on progress later", "context": "I will follow up with the team tomorrow."}
          ]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Meaning accuracy", "Context recognition"],
        "rubric": [],
        "feedbackFocus": "Help the student understand phrase meaning and workplace usage.",
        "successCriteria": ["Correctly match all phrases to their meanings."]
      }
    }
    """;

    [Fact]
    public void Validate_PhraseMatch_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidPhraseMatchJson), ActivityType.VocabularyPractice, "phrase_match");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_PhraseMatch_MissingPairs_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "title": "T",
          "learnContent": {"teachingTitle": "T", "explanation": "E", "keyPoints": [], "examples": [], "strategy": "S", "commonMistakes": [], "sourceLanguageSupport": null},
          "practiceContent": {"instructions": "I", "scenario": null, "task": "T", "exerciseData": {}},
          "feedbackPlan": {"evaluationCriteria": [], "rubric": [], "feedbackFocus": "F", "successCriteria": []}
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.VocabularyPractice, "phrase_match");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("pairs"));
    }

    [Theory]
    [InlineData("pairs")]
    [InlineData("answerKey")]
    [InlineData("selectedAnswers")]
    public void Validate_PhraseMatch_WithPracticeOnlyKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidPhraseMatchJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.VocabularyPractice, "phrase_match");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── gap_fill_workplace_phrase pattern key tests ────────────────────────────

    private const string ValidGapFillJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Workplace Gap Fill",
      "moduleGoal": "Practise workplace vocabulary in context.",
      "primarySkill": "vocabulary",
      "secondarySkills": ["reading"],
      "exerciseType": "gap_fill_workplace_phrase",
      "learnContent": {
        "teachingTitle": "Polite requests",
        "explanation": "Use polite modal verbs to soften workplace requests.",
        "keyPoints": ["Use 'could' for polite requests", "Avoid direct imperatives"],
        "examples": [{"phrase": "Could you send me the report?", "meaning": "polite request", "note": "less direct than 'Send me the report'"}],
        "strategy": "Look for the context to identify the missing word.",
        "commonMistakes": ["Using informal language in formal contexts"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Fill in each blank with the correct workplace word or phrase.",
        "scenario": null,
        "task": "Complete the missing words in each sentence.",
        "exerciseData": {
          "items": [
            {"sentence": "Could you ___ me the report by Friday?", "answer": "send", "distractors": ["give", "bring"], "hint": "verb"}
          ]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Correct word choice", "Grammar accuracy"],
        "rubric": [],
        "feedbackFocus": "Help the student choose words based on grammar and context.",
        "successCriteria": ["Fill all missing words correctly."]
      }
    }
    """;

    [Fact]
    public void Validate_GapFill_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidGapFillJson), ActivityType.VocabularyPractice, "gap_fill_workplace_phrase");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_GapFill_MissingItems_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "title": "T",
          "learnContent": {"teachingTitle": "T", "explanation": "E", "keyPoints": [], "examples": [], "strategy": "S", "commonMistakes": [], "sourceLanguageSupport": null},
          "practiceContent": {"instructions": "I", "scenario": null, "task": "T", "exerciseData": {}},
          "feedbackPlan": {"evaluationCriteria": [], "rubric": [], "feedbackFocus": "F", "successCriteria": []}
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.VocabularyPractice, "gap_fill_workplace_phrase");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("items"));
    }

    [Theory]
    [InlineData("gaps")]
    [InlineData("answerKey")]
    public void Validate_GapFill_WithPracticeOnlyKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidGapFillJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.VocabularyPractice, "gap_fill_workplace_phrase");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── listen_and_answer pattern key tests ───────────────────────────────────

    private const string ValidListenAndAnswerJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Team Update Voicemail",
      "learnContent": {
        "teachingTitle": "Listening for action and deadline",
        "explanation": "Focus on the main request and any deadline mentioned.",
        "keyPoints": ["Listen for verbs", "Note dates or deadlines"],
        "strategy": "Identify who, what, and when.",
        "commonMistakes": ["Missing the deadline"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen and answer the questions.",
        "scenario": "A manager leaves a voicemail for the team.",
        "task": "Answer each comprehension question.",
        "exerciseData": {
          "speakerRole": "Manager",
          "listenerRole": "Team Member",
          "audioScript": "Hi everyone, please send me your status updates by end of day Friday.",
          "transcriptAvailableAfterSubmit": true,
          "questions": [
            { "id": "q1", "question": "What was requested?", "expectedAnswer": "status updates", "type": "short_answer" }
          ]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understood"],
        "rubric": [],
        "feedbackFocus": "Comprehension accuracy",
        "successCriteria": ["Student identifies the request"]
      }
    }
    """;

    [Fact]
    public void Validate_ListenAndAnswer_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidListenAndAnswerJson), ActivityType.ListeningComprehension, "listen_and_answer");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("questions")]
    public void Validate_ListenAndAnswer_MissingRequiredKey_Fails(string missingKey)
    {
        var json = RemoveExerciseDataKey(ValidListenAndAnswerJson, missingKey);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listen_and_answer");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(missingKey));
    }

    // ── listen_and_gap_fill pattern key tests ─────────────────────────────────

    private const string ValidListenAndGapFillJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Fill the Gaps",
      "learnContent": {
        "teachingTitle": "Catching key words",
        "explanation": "Listen for content words — nouns, verbs, and adjectives carry the most meaning.",
        "keyPoints": ["Predict word type before listening", "Focus on stressed syllables"],
        "strategy": "Listen for the word type suggested by the sentence context.",
        "commonMistakes": ["Guessing from meaning instead of listening carefully"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen and fill in the missing words.",
        "scenario": "A manager gives a project update.",
        "task": "Complete the gaps with the exact word you hear.",
        "exerciseData": {
          "speakerRole": "Project Manager",
          "audioScript": "The deadline has been moved to next Monday. Please submit your draft by Friday.",
          "transcriptAvailableAfterSubmit": true,
          "gaps": [
            { "id": "g1", "sentenceWithBlank": "The ___ has been moved to next Monday.", "answer": "deadline", "hint": "noun" },
            { "id": "g2", "sentenceWithBlank": "Please ___ your draft by Friday.", "answer": "submit", "hint": "verb" }
          ]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Exact word match"],
        "rubric": [],
        "feedbackFocus": "Exact word recognition",
        "successCriteria": ["Student fills at least 1 of 2 gaps correctly"]
      }
    }
    """;

    [Fact]
    public void Validate_ListenAndGapFill_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidListenAndGapFillJson), ActivityType.ListeningComprehension, "listen_and_gap_fill");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("gaps")]
    public void Validate_ListenAndGapFill_MissingRequiredKey_Fails(string missingKey)
    {
        var json = RemoveExerciseDataKey(ValidListenAndGapFillJson, missingKey);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listen_and_gap_fill");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(missingKey));
    }

    [Fact]
    public void Validate_ListenAndGapFill_WithQuestionsInsteadOfGaps_Fails()
    {
        // gaps is required for listen_and_gap_fill; questions alone is insufficient.
        var json = RemoveExerciseDataKey(ValidListenAndGapFillJson, "gaps");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listen_and_gap_fill");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("gaps"));
    }

    [Fact]
    public void Validate_ListenAndAnswer_PatternKeyOverrides_ActivityTypeCheck()
    {
        // listen_and_answer requires "questions", not "gaps".
        // This verifies that the pattern-key lookup overrides the ActivityType-level requirement.
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidListenAndAnswerJson), ActivityType.ListeningComprehension, "listen_and_answer");

        result.IsValid.Should().BeTrue();
    }

    // ── email_reply pattern key tests ─────────────────────────────────────────

    private const string ValidEmailReplyJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Reply to a leave request",
      "moduleGoal": "Practise writing a professional email reply.",
      "primarySkill": "writing",
      "secondarySkills": ["reading", "vocabulary"],
      "exerciseType": "email_reply",
      "learnContent": {
        "teachingTitle": "Writing a professional email reply",
        "explanation": "A good professional email reply opens with a clear acknowledgement, provides the required information, and closes with a next step.",
        "keyPoints": ["Acknowledge the message first", "Be clear and direct", "Close with a next step"],
        "examples": [{ "phrase": "Thank you for your email.", "meaning": "polite acknowledgement opener", "note": "standard semi-formal opener" }],
        "strategy": "Plan your reply: acknowledge, inform, close.",
        "commonMistakes": ["Starting with 'I' instead of acknowledging the message"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the email below and write a professional reply.",
        "scenario": "Your colleague has sent you a message about a project update.",
        "task": "Write a professional email reply.",
        "exerciseData": {
          "incomingMessage": "Hi, could you please send me the project status report by end of day? I need it for the client meeting tomorrow.",
          "recipient": "your colleague",
          "relationship": "colleague",
          "tone": "semi-formal",
          "prompt": "Read the email above and write a professional reply.",
          "requiredInformation": ["Acknowledge the request", "Confirm when you will send it"],
          "requiredPhrases": ["Thank you for your email"],
          "targetVocabulary": ["status report", "confirm"],
          "expectedLength": "3-5 sentences",
          "suggestedSubject": "Re: Project Status Report",
          "successChecklist": ["Acknowledges the request", "Confirms the deadline"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Tone", "Clarity"],
        "rubric": [],
        "feedbackFocus": "Clear, professional email reply.",
        "successCriteria": ["The reply addresses the request clearly."]
      }
    }
    """;

    [Fact]
    public void Validate_EmailReply_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidEmailReplyJson), ActivityType.WritingScenario, "email_reply");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("incomingMessage")]
    public void Validate_EmailReply_MissingRequiredKey_Fails(string missingKey)
    {
        var json = RemoveExerciseDataKey(ValidEmailReplyJson, missingKey);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "email_reply");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(missingKey));
    }

    [Theory]
    [InlineData("answerKey")]
    [InlineData("submitLabel")]
    [InlineData("textarea")]
    public void Validate_EmailReply_WithPracticeControlInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidEmailReplyJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "email_reply");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── teams_chat_simulation pattern key tests ────────────────────────────────

    private const string ValidTeamsChatJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Respond to a chat request",
      "moduleGoal": "Practise writing concise professional chat replies.",
      "primarySkill": "writing",
      "secondarySkills": ["reading", "communication"],
      "exerciseType": "teams_chat_simulation",
      "learnContent": {
        "teachingTitle": "Writing effective workplace chat messages",
        "explanation": "Good workplace chat messages are clear, concise, and friendly without being too casual.",
        "keyPoints": ["Get to the point immediately", "Keep it to 1-3 sentences", "Match the tone of the conversation"],
        "examples": [{ "phrase": "Sure, I can do that.", "meaning": "simple, direct confirmation", "note": "natural chat tone" }],
        "strategy": "Read the message, identify what is needed, reply directly.",
        "commonMistakes": ["Writing too formally — like an email"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the chat thread and write the next message.",
        "scenario": "A colleague messages you about a task.",
        "task": "Write your next message in the chat.",
        "exerciseData": {
          "chatHistory": [
            { "sender": "Amir", "role": "Project Manager", "message": "Hi! Can you confirm you received the updated brief?" }
          ],
          "speakerRole": "Team Member",
          "recipientRole": "Project Manager",
          "tone": "friendly but professional",
          "prompt": "Write your reply to the chat above.",
          "requiredInformation": ["Confirm receipt of the brief"],
          "requiredPhrases": ["Got it"],
          "targetVocabulary": ["confirm", "brief"],
          "successChecklist": ["Confirms receipt", "Is brief and direct"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Tone", "Clarity"],
        "rubric": [],
        "feedbackFocus": "Clear, natural workplace chat reply.",
        "successCriteria": ["The reply is direct and professional."]
      }
    }
    """;

    [Fact]
    public void Validate_TeamsChat_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidTeamsChatJson), ActivityType.WritingScenario, "teams_chat_simulation");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("chatHistory")]
    public void Validate_TeamsChat_MissingRequiredKey_Fails(string missingKey)
    {
        var json = RemoveExerciseDataKey(ValidTeamsChatJson, missingKey);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "teams_chat_simulation");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(missingKey));
    }

    // ── open_writing_task pattern key tests ────────────────────────────────────

    private const string ValidOpenWritingJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Write a project status update",
      "moduleGoal": "Practise writing a clear, professional status update.",
      "primarySkill": "writing",
      "secondarySkills": ["grammar", "vocabulary"],
      "exerciseType": "open_writing_task",
      "learnContent": {
        "teachingTitle": "Writing a clear status update",
        "explanation": "A good status update tells the reader what has happened, what is in progress, and what comes next — in plain, direct language.",
        "keyPoints": ["Lead with the most important information", "Be specific about progress and blockers", "Keep sentences short"],
        "examples": [{ "phrase": "The project is on track to meet the deadline.", "meaning": "clear progress statement", "note": "direct, professional tone" }],
        "strategy": "Plan three things: what is done, what is in progress, what is next.",
        "commonMistakes": ["Writing too vaguely — readers need specific details"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the situation and write your response.",
        "scenario": "You are a project coordinator and need to send a status update to your manager.",
        "task": "Write a clear, professional status update.",
        "exerciseData": {
          "prompt": "Write a short status update for your manager about the website redesign project.",
          "tone": "professional and direct",
          "expectedLength": "60-80 words",
          "requiredInformation": ["Current progress", "Any blockers", "Next step"],
          "requiredPhrases": ["is on track", "the next step"],
          "targetVocabulary": ["milestone", "blocker", "deliverable"],
          "successChecklist": ["Covers progress, blockers, and next steps", "Uses professional tone", "Is appropriately concise"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Clarity", "Grammar"],
        "rubric": [],
        "feedbackFocus": "Clear, structured workplace writing.",
        "successCriteria": ["The update is complete and easy to follow."]
      }
    }
    """;

    [Fact]
    public void Validate_OpenWritingTask_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidOpenWritingJson), ActivityType.WritingScenario, "open_writing_task");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_OpenWritingTask_MissingPrompt_Fails()
    {
        var json = RemoveExerciseDataKey(ValidOpenWritingJson, "prompt");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "open_writing_task");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("prompt"));
    }

    [Theory]
    [InlineData("submitLabel")]
    [InlineData("checkLabel")]
    [InlineData("textarea")]
    public void Validate_OpenWritingTask_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidOpenWritingJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "open_writing_task");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    private static string RemoveExerciseDataKey(string originalJson, string keyToRemove)
    {
        using var doc = JsonDocument.Parse(originalJson);
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
                                if (!edProp.NameEquals(keyToRemove)) edProp.WriteTo(writer);
                            writer.WriteEndObject();
                        }
                        else pcProp.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                else prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    // ── spoken_response_from_prompt pattern key tests ─────────────────────────

    private const string ValidSpokenResponseJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Project delay update",
      "moduleGoal": "Practise giving a clear spoken status update.",
      "primarySkill": "speaking",
      "secondarySkills": ["listening"],
      "exerciseType": "spoken_response_from_prompt",
      "learnContent": {
        "teachingTitle": "Giving a clear spoken update",
        "explanation": "A good spoken update leads with the main point, then adds a brief reason and next step. Keep your tone calm and professional.",
        "keyPoints": ["Lead with the main point", "Speak at a steady pace"],
        "examples": [{ "phrase": "I wanted to let you know that", "meaning": "signals an update", "note": "polite opener" }],
        "strategy": "Plan three things before speaking: what happened, why, and what comes next.",
        "commonMistakes": ["Speaking too fast when nervous"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the situation and record your spoken response.",
        "scenario": "Your project is delayed. Your manager needs an update.",
        "task": "Record a clear, professional spoken response.",
        "exerciseData": {
          "prompt": "Record a 30-second update for your manager explaining the project delay and your next step.",
          "expectedResponseLength": "30-45 seconds",
          "tone": "professional and direct",
          "requiredInformation": ["Explain the delay", "Give next step"],
          "requiredPhrases": ["I wanted to let you know"],
          "targetVocabulary": ["delay", "update", "next step"],
          "successChecklist": ["Explains delay clearly", "States next step", "Appropriate tone"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Fluency", "Clarity", "Tone", "Grammar and vocabulary"],
        "rubric": [],
        "feedbackFocus": "Clear, natural professional spoken updates.",
        "successCriteria": ["The response addresses the situation clearly."]
      }
    }
    """;

    [Fact]
    public void Validate_SpokenResponse_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidSpokenResponseJson), ActivityType.SpeakingRolePlay, "spoken_response_from_prompt");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SpokenResponse_MissingPrompt_Fails()
    {
        var json = RemoveExerciseDataKey(ValidSpokenResponseJson, "prompt");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "spoken_response_from_prompt");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("prompt"));
    }

    [Theory]
    [InlineData("recordingControls")]
    [InlineData("startRecording")]
    [InlineData("microphoneInstructions")]
    public void Validate_SpokenResponse_WithRecordingControlInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidSpokenResponseJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "spoken_response_from_prompt");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── speaking_roleplay_turn pattern key tests ───────────────────────────────

    private const string ValidSpeakingRoleplayTurnJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Handle a client complaint",
      "moduleGoal": "Practise responding clearly to a client in a roleplay.",
      "primarySkill": "speaking",
      "secondarySkills": ["listening"],
      "exerciseType": "speaking_roleplay_turn",
      "learnContent": {
        "teachingTitle": "Responding naturally in a roleplay",
        "explanation": "A good roleplay response acknowledges what the other person said before making your point. Match your register to the situation.",
        "keyPoints": ["Acknowledge before responding", "Match your register to the relationship"],
        "examples": [{ "phrase": "I understand your concern", "meaning": "acknowledges the complaint", "note": "empathetic opener" }],
        "strategy": "Listen for the key issue in the partner's turn, then respond to it directly.",
        "commonMistakes": ["Ignoring what the partner said and just giving your own point"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the roleplay situation and record your spoken response.",
        "scenario": "A client has contacted you about a delayed delivery.",
        "task": "Record your spoken response to the client's turn.",
        "exerciseData": {
          "role": "Account Manager",
          "partnerRole": "Client",
          "partnerTurn": "I was expecting the delivery last Friday. Can you explain what happened?",
          "prompt": "Respond to the client and explain the delay professionally.",
          "expectedResponseLength": "30-45 seconds",
          "tone": "professional and empathetic",
          "requiredInformation": ["Acknowledge the delay", "Give a reason", "Provide next step"],
          "requiredPhrases": ["I understand your concern"],
          "targetVocabulary": ["delay", "apologise", "resolve"],
          "successChecklist": ["Acknowledges delay", "Gives reason", "States next step"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Task completion", "Fluency", "Roleplay relevance", "Tone", "Grammar and vocabulary"],
        "rubric": [],
        "feedbackFocus": "Natural, clear roleplay response.",
        "successCriteria": ["The response fits the partner's turn and is understandable."]
      }
    }
    """;

    [Fact]
    public void Validate_SpeakingRoleplayTurn_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidSpeakingRoleplayTurnJson), ActivityType.SpeakingRolePlay, "speaking_roleplay_turn");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("partnerTurn")]
    public void Validate_SpeakingRoleplayTurn_MissingRequiredKey_Fails(string missingKey)
    {
        var json = RemoveExerciseDataKey(ValidSpeakingRoleplayTurnJson, missingKey);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "speaking_roleplay_turn");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(missingKey));
    }

    [Theory]
    [InlineData("recordingControls")]
    [InlineData("microphoneInstructions")]
    public void Validate_SpeakingRoleplayTurn_WithRecordingControlInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidSpeakingRoleplayTurnJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "speaking_roleplay_turn");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── lesson_reflection pattern key tests ───────────────────────────────────

    private const string ValidLessonReflectionJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Lesson reflection: workplace emails",
      "moduleGoal": "Reflect on today's email writing practice and identify a next improvement step.",
      "primarySkill": "reflection",
      "secondarySkills": ["writing"],
      "exerciseType": "lesson_reflection",
      "learnContent": {
        "teachingTitle": "How to reflect on your learning",
        "explanation": "Reflection helps you notice what you have improved and decide what to focus on next. Being specific about what you practised makes reflection more useful.",
        "keyPoints": ["Be specific — name the exact phrase or skill you used", "Identify one thing you want to improve next time"],
        "examples": [{ "phrase": "Today I practised writing clearer subject lines.", "meaning": "specific observation", "note": "good reflection is concrete" }],
        "strategy": "Write one strength, one challenge, and one next step.",
        "commonMistakes": ["Writing only vague comments like 'it was good'"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Take a moment to reflect on what you practised today.",
        "scenario": "You have been practising professional email writing.",
        "task": "Write a short reflection on today's practice.",
        "exerciseData": {
          "prompt": "What was the most useful phrase you used today? What would you do differently next time?",
          "reflectionFocus": "email tone and workplace vocabulary",
          "expectedLength": "3-5 sentences",
          "successChecklist": [
            "Identifies one specific strength from today",
            "Names one thing to improve",
            "Gives a concrete next step"
          ]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Self-awareness", "Specificity", "Improvement plan", "Clarity"],
        "rubric": [],
        "feedbackFocus": "Help the student notice progress and choose a next improvement step.",
        "successCriteria": ["The reflection identifies one strength, one challenge, and one next step."]
      }
    }
    """;

    [Fact]
    public void Validate_LessonReflection_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidLessonReflectionJson), ActivityType.WritingScenario, "lesson_reflection");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_LessonReflection_MissingPrompt_Fails()
    {
        var json = RemoveExerciseDataKey(ValidLessonReflectionJson, "prompt");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "lesson_reflection");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("prompt"));
    }

    [Theory]
    [InlineData("textarea")]
    [InlineData("submitLabel")]
    [InlineData("answerKey")]
    public void Validate_LessonReflection_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidLessonReflectionJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "lesson_reflection");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── reading_multiple_choice_single pattern key tests ──────────────────────

    private const string ValidReadingMultipleChoiceSingleJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Reading for the main idea: project updates",
      "moduleGoal": "Practise identifying the main idea of a workplace email.",
      "primarySkill": "reading",
      "secondarySkills": [],
      "exerciseType": "reading_multiple_choice_single",
      "learnContent": {
        "teachingTitle": "Finding the main idea in a workplace text",
        "explanation": "Skimming a text for its main idea before reading in detail helps you answer comprehension questions quickly and accurately.",
        "keyPoints": ["Skim for the main idea before reading in detail", "Watch for signal words that suggest contrast or cause and effect"],
        "examples": [{ "phrase": "however", "meaning": "signals contrast", "note": "the sentence after 'however' often contains the key point" }],
        "strategy": "Read the question first, then scan the passage for the relevant section.",
        "commonMistakes": ["Choosing an option that is true but does not answer the question"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the passage, then choose the one best answer to the question.",
        "scenario": "An email about a project status update.",
        "task": "Read the passage and choose the option that best answers the question.",
        "exerciseData": {
          "passage": "The marketing team completed the website redesign ahead of schedule. However, the launch has been delayed by two weeks because the new analytics integration needs additional testing. The team lead asked everyone to focus on bug fixes this week.",
          "question": "Why has the launch been delayed?",
          "options": [
            { "id": "A", "text": "The website redesign was not finished." },
            { "id": "B", "text": "The analytics integration needs more testing." },
            { "id": "C", "text": "The marketing team is on holiday." },
            { "id": "D", "text": "The budget was not approved." }
          ],
          "correctOptionId": "B",
          "explanation": "The passage says the launch was delayed because the analytics integration needs additional testing.",
          "distractorExplanations": {
            "A": "The redesign was completed ahead of schedule, not unfinished.",
            "C": "There is no mention of the team being on holiday.",
            "D": "Budget approval is not mentioned in the passage."
          },
          "successChecklist": ["Selected the option supported by the passage", "Can explain why the other options are wrong"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Inference", "Distractor elimination"],
        "rubric": [],
        "feedbackFocus": "Help the student read carefully and choose the best supported answer.",
        "successCriteria": ["The selected option is supported by the passage.", "The student can explain why distractors are weaker."]
      }
    }
    """;

    [Fact]
    public void Validate_ReadingMultipleChoiceSingle_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidReadingMultipleChoiceSingleJson), ActivityType.ReadingTask, "reading_multiple_choice_single");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("passage")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    public void Validate_ReadingMultipleChoiceSingle_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidReadingMultipleChoiceSingleJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_multiple_choice_single");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("passage")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    [InlineData("answerKey")]
    [InlineData("selectedAnswer")]
    public void Validate_ReadingMultipleChoiceSingle_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidReadingMultipleChoiceSingleJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_multiple_choice_single");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── reading_multiple_choice_multi pattern key tests ───────────────────────

    private const string ValidReadingMultipleChoiceMultiJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Reading for multiple supported facts",
      "moduleGoal": "Practise identifying all answers supported by a workplace text.",
      "primarySkill": "reading",
      "secondarySkills": [],
      "exerciseType": "reading_multiple_choice_multi",
      "learnContent": {
        "teachingTitle": "Selecting all answers supported by the passage",
        "explanation": "In multiple-answer questions, more than one option may be correct. Read the passage fully and check each option against the text before selecting.",
        "keyPoints": ["Read every option before selecting any", "Each correct option must be directly supported by the passage", "Eliminate options that are not mentioned"],
        "examples": [{ "phrase": "in addition", "meaning": "signals another point", "note": "look for additional facts after this phrase" }],
        "strategy": "Read the passage, then test each option individually against what the text says.",
        "commonMistakes": ["Stopping after finding one correct option", "Selecting options that sound reasonable but are not in the passage"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Read the passage, then choose ALL correct answers to the question.",
        "scenario": "An internal memo about a project update.",
        "task": "Select every option that is supported by the text.",
        "exerciseData": {
          "passage": "The project was delayed due to testing issues and budget approval delays. The team worked overtime to meet the revised deadline. No staff left the company during this period.",
          "question": "Which of the following caused the project delay?",
          "options": [
            { "id": "A", "text": "Testing issues" },
            { "id": "B", "text": "Staff resignations" },
            { "id": "C", "text": "Budget approval delays" },
            { "id": "D", "text": "Office relocation" }
          ],
          "correctOptionIds": ["A", "C"],
          "explanation": "The passage explicitly mentions testing issues and budget approval delays as causes.",
          "optionExplanations": {
            "A": "Correct — testing issues are mentioned.",
            "B": "Incorrect — no staff left the company.",
            "C": "Correct — budget approval delays are mentioned.",
            "D": "Incorrect — office relocation is not mentioned."
          },
          "successChecklist": ["Selected all passage-supported options", "Avoided unsupported distractors"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Complete answer selection", "Distractor elimination"],
        "rubric": [],
        "feedbackFocus": "Help the student select all answers supported by the passage.",
        "successCriteria": ["All selected options are supported by the passage.", "No correct options are missed."]
      }
    }
    """;

    [Fact]
    public void Validate_ReadingMultipleChoiceMulti_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidReadingMultipleChoiceMultiJson), ActivityType.ReadingTask, "reading_multiple_choice_multi");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("passage")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionIds")]
    public void Validate_ReadingMultipleChoiceMulti_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidReadingMultipleChoiceMultiJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_multiple_choice_multi");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("passage")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionIds")]
    [InlineData("answerKey")]
    [InlineData("selectedAnswers")]
    [InlineData("optionExplanations")]
    public void Validate_ReadingMultipleChoiceMulti_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidReadingMultipleChoiceMultiJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_multiple_choice_multi");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── reading_fill_in_blanks pattern key tests ──────────────────────────────

    private const string ValidReadingFillInBlanksJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Fill in the Blanks",
      "moduleGoal": "Practise reading context clues to choose the correct missing word.",
      "primarySkill": "reading",
      "secondarySkills": [],
      "exerciseType": "reading_fill_in_blanks",
      "learnContent": {
        "teachingTitle": "Reading for context clues",
        "explanation": "Read the passage and select the word that best completes each blank."
      },
      "practiceContent": {
        "exerciseData": {
          "passageWithBlanks": "The {{gap1}} ran quickly across the {{gap2}}.",
          "gaps": [
            { "id": "gap1", "answer": "dog", "options": ["dog","cat","bird","fish"], "explanation": "Dogs are known to run quickly." },
            { "id": "gap2", "answer": "park", "options": ["park","river","mountain","desert"], "explanation": "Parks are common open spaces." }
          ]
        }
      },
      "feedbackPlan": {
        "coachingFocus": "vocabulary and context clues"
      }
    }
    """;

    [Fact]
    public void Validate_ReadingFillInBlanks_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidReadingFillInBlanksJson), ActivityType.ReadingTask, "reading_fill_in_blanks");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("passageWithBlanks")]
    [InlineData("gaps")]
    public void Validate_ReadingFillInBlanks_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidReadingFillInBlanksJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_fill_in_blanks");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("passage")]
    [InlineData("correctOptionId")]
    [InlineData("answerKey")]
    public void Validate_ReadingFillInBlanks_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidReadingFillInBlanksJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_fill_in_blanks");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── reorder_paragraphs pattern key tests ──────────────────────────────────

    private const string ValidReorderParagraphsJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Reorder the Team Update",
      "moduleGoal": "Practise recognising logical paragraph order in a workplace email update.",
      "primarySkill": "reading",
      "secondarySkills": [],
      "exerciseType": "reorder_paragraphs",
      "learnContent": {
        "teachingTitle": "Putting paragraphs in logical order",
        "explanation": "Look for topic sentences, pronouns, and sequence words to identify paragraph order."
      },
      "practiceContent": {
        "exerciseData": {
          "items": [
            { "id": "p1", "text": "The project kick-off meeting has been scheduled for Monday." },
            { "id": "p2", "text": "All team members should confirm their attendance by Friday." },
            { "id": "p3", "text": "The agenda will be shared in advance." },
            { "id": "p4", "text": "Please bring any questions you have about the new process." }
          ],
          "correctOrder": ["p1", "p2", "p3", "p4"],
          "explanation": "The opening sentence introduces the event; subsequent paragraphs provide logistic details."
        }
      },
      "feedbackPlan": {
        "feedbackFocus": "Help the student recognise logical flow and paragraph cohesion."
      }
    }
    """;

    [Fact]
    public void Validate_ReorderParagraphs_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidReorderParagraphsJson), ActivityType.ReadingTask, "reorder_paragraphs");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("items")]
    [InlineData("correctOrder")]
    public void Validate_ReorderParagraphs_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidReorderParagraphsJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reorder_paragraphs");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("items")]
    [InlineData("correctOrder")]
    [InlineData("answerKey")]
    [InlineData("selectedOrder")]
    public void Validate_ReorderParagraphs_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidReorderParagraphsJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reorder_paragraphs");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── reading_writing_fill_in_blanks pattern key tests ──────────────────────

    private const string ValidReadingWritingFillInBlanksJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Word Forms in Context",
      "moduleGoal": "Practise choosing the correct word form to complete a workplace passage.",
      "primarySkill": "reading",
      "secondarySkills": ["writing"],
      "exerciseType": "reading_writing_fill_in_blanks",
      "learnContent": {
        "teachingTitle": "Choosing the right word form",
        "explanation": "Read the passage and select the word that best fits each blank grammatically and contextually."
      },
      "practiceContent": {
        "instructions": "Read the passage and choose the correct word for each blank.",
        "exerciseData": {
          "passageWithBlanks": "The {{gap1}} of a new system requires {{gap2}} planning.",
          "gaps": [
            { "id": "gap1", "answer": "implementation", "options": ["implementation", "implement", "implemented"], "explanation": "A noun is required here." },
            { "id": "gap2", "answer": "careful", "options": ["careful", "carefully", "care"], "explanation": "An adjective modifying 'planning' is needed." }
          ]
        }
      },
      "feedbackPlan": {
        "feedbackFocus": "Help the student recognise word forms from context."
      }
    }
    """;

    [Fact]
    public void Validate_ReadingWritingFillInBlanks_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidReadingWritingFillInBlanksJson), ActivityType.ReadingTask, "reading_writing_fill_in_blanks");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("passageWithBlanks")]
    [InlineData("gaps")]
    public void Validate_ReadingWritingFillInBlanks_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidReadingWritingFillInBlanksJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_writing_fill_in_blanks");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("passageWithBlanks")]
    [InlineData("gaps")]
    [InlineData("answerKey")]
    [InlineData("selectedAnswer")]
    public void Validate_ReadingWritingFillInBlanks_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidReadingWritingFillInBlanksJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ReadingTask, "reading_writing_fill_in_blanks");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── summarize_written_text pattern key tests ───────────────────────────────

    private const string ValidSummarizeWrittenTextJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Summarise a Meeting Update",
      "moduleGoal": "Practise identifying main ideas and writing a concise summary.",
      "primarySkill": "writing",
      "secondarySkills": ["reading"],
      "exerciseType": "summarize_written_text",
      "learnContent": {
        "teachingTitle": "How to write a concise summary",
        "explanation": "Read the whole text first, identify the main idea, then select the key supporting points."
      },
      "practiceContent": {
        "instructions": "Read the passage and write a concise summary in your own words.",
        "exerciseData": {
          "sourceText": "The company held its quarterly all-hands meeting last Friday. The CEO announced that revenue had grown by 12% compared to the previous quarter, driven mainly by increased demand in the Asia-Pacific region. The leadership team also revealed plans to hire 50 new engineers over the next six months to support product development. Employees were encouraged to submit their ideas for improving workplace efficiency through the internal suggestion portal.",
          "prompt": "Write a summary of approximately 30-50 words. Include the main idea and key supporting points. Use your own words."
        }
      },
      "feedbackPlan": {
        "feedbackFocus": "Help the student summarise the main idea clearly and concisely."
      }
    }
    """;

    [Fact]
    public void Validate_SummarizeWrittenText_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidSummarizeWrittenTextJson), ActivityType.WritingScenario, "summarize_written_text");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("sourceText")]
    [InlineData("prompt")]
    public void Validate_SummarizeWrittenText_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidSummarizeWrittenTextJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "summarize_written_text");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("sourceText")]
    [InlineData("prompt")]
    [InlineData("expectedSummary")]
    [InlineData("answerKey")]
    [InlineData("textarea")]
    public void Validate_SummarizeWrittenText_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidSummarizeWrittenTextJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "summarize_written_text");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── summarize_spoken_text pattern key tests ────────────────────────────────

    private const string ValidSummarizeSpokenTextJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Summarise a Spoken Update",
      "moduleGoal": "Practise listening for main ideas and writing a concise summary.",
      "primarySkill": "listening",
      "secondarySkills": ["writing"],
      "exerciseType": "summarize_spoken_text",
      "learnContent": {
        "teachingTitle": "How to summarise what you hear",
        "explanation": "Listen for the main idea first, note key supporting points, then paraphrase concisely.",
        "keyPoints": ["Listen for the topic before details", "Use your own words"]
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then write a concise summary in your own words.",
        "exerciseData": {
          "audioScript": "Good morning team. Our quarterly results are in and revenue grew twelve percent, led by strong demand in Asia-Pacific. We also plan to hire fifty new engineers over the next six months. Please share efficiency ideas through the suggestion portal.",
          "audioUrl": null,
          "prompt": "Listen to the audio and write a summary of 50-70 words in your own words.",
          "summaryRequirements": ["Cover the main idea", "Include key supporting points", "Use your own words"],
          "keyPoints": ["Revenue grew 12%", "Fifty new engineers will be hired"],
          "successChecklist": ["Summary covers main idea", "No unsupported details added"]
        }
      },
      "feedbackPlan": {
        "feedbackFocus": "Help the student summarise the spoken text clearly and concisely."
      }
    }
    """;

    [Fact]
    public void Validate_SummarizeSpokenText_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidSummarizeSpokenTextJson), ActivityType.ListeningComprehension, "summarize_spoken_text");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("prompt")]
    public void Validate_SummarizeSpokenText_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidSummarizeSpokenTextJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "summarize_spoken_text");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("prompt")]
    [InlineData("expectedSummary")]
    [InlineData("modelSummary")]
    [InlineData("answerKey")]
    [InlineData("textarea")]
    [InlineData("submit")]
    [InlineData("checkAnswer")]
    public void Validate_SummarizeSpokenText_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidSummarizeSpokenTextJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "summarize_spoken_text");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── write_essay pattern key tests ───────────────────────────────────────────

    private const string ValidWriteEssayJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Write an Opinion Essay",
      "moduleGoal": "Practise planning and writing a structured essay that answers a prompt.",
      "primarySkill": "writing",
      "secondarySkills": [],
      "exerciseType": "write_essay",
      "learnContent": {
        "teachingTitle": "How to plan and structure an essay",
        "explanation": "Answer the prompt directly, organise your ideas into an introduction, body, and conclusion, and support each point with an example."
      },
      "practiceContent": {
        "instructions": "Read the essay prompt below and write a structured essay response.",
        "exerciseData": {
          "prompt": "Some companies now allow employees to work from home permanently. Discuss the advantages and disadvantages of this approach.",
          "topic": "Remote work policies",
          "essayType": "advantage-disadvantage",
          "requirements": {
            "targetWordCount": "180-250 words",
            "minimumParagraphs": 3,
            "mustAddress": ["advantages of remote work", "disadvantages of remote work"],
            "avoid": ["off-topic personal anecdotes"]
          }
        }
      },
      "feedbackPlan": {
        "feedbackFocus": "Help the student write a clear, structured essay with supported ideas."
      }
    }
    """;

    [Fact]
    public void Validate_WriteEssay_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidWriteEssayJson), ActivityType.WritingScenario, "write_essay");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("topic")]
    public void Validate_WriteEssay_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidWriteEssayJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "write_essay");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("modelEssay")]
    [InlineData("expectedAnswer")]
    [InlineData("answerKey")]
    [InlineData("textarea")]
    public void Validate_WriteEssay_WithForbiddenKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidWriteEssayJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.WritingScenario, "write_essay");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── listening_multiple_choice_single pattern key tests ────────────────────

    private const string ValidListeningMultipleChoiceSingleJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Listening for the main idea: team update",
      "moduleGoal": "Practise listening for the main idea in a short workplace announcement.",
      "primarySkill": "listening",
      "secondarySkills": [],
      "exerciseType": "listening_multiple_choice_single",
      "learnContent": {
        "teachingTitle": "Listening for the main idea",
        "explanation": "Listen for the overall point of the speaker before focusing on small details, and watch for signal words that show contrast.",
        "keyPoints": ["Listen for the main idea before the details", "Watch for signal words like 'however' or 'instead'"],
        "examples": [{ "phrase": "instead", "meaning": "signals a change of plan", "note": "the information after 'instead' is often the key point" }],
        "strategy": "Listen once for the overall message, then again for supporting details.",
        "commonMistakes": ["Choosing an answer based on one familiar word"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then choose the one best answer to the question.",
        "scenario": "A short team update about a schedule change.",
        "task": "Listen and choose the option that best answers the question.",
        "exerciseData": {
          "audioScript": "Good morning everyone. We were planning to release the update on Friday, but instead we'll release it next Monday so the support team has time to prepare.",
          "audioUrl": null,
          "question": "When will the update be released?",
          "options": [
            { "id": "A", "text": "On Friday" },
            { "id": "B", "text": "Next Monday" },
            { "id": "C", "text": "It has been cancelled" },
            { "id": "D", "text": "Immediately" }
          ],
          "correctOptionId": "B",
          "explanation": "The speaker says 'instead we'll release it next Monday', so the update is now planned for Monday.",
          "distractorExplanations": {
            "A": "Friday was the original plan, which was changed.",
            "C": "The update was not cancelled, only rescheduled.",
            "D": "The speaker does not say the release is immediate."
          },
          "successChecklist": ["Selected the option supported by the audio", "Can explain why the other options are wrong"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Listening for contrast", "Distractor elimination"],
        "rubric": [],
        "feedbackFocus": "Help the student listen for meaning and choose the best supported answer.",
        "successCriteria": ["The selected option is supported by the audio.", "The student avoids distractors based on isolated words."]
      }
    }
    """;

    [Fact]
    public void Validate_ListeningMultipleChoiceSingle_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidListeningMultipleChoiceSingleJson), ActivityType.ListeningComprehension, "listening_multiple_choice_single");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    public void Validate_ListeningMultipleChoiceSingle_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidListeningMultipleChoiceSingleJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listening_multiple_choice_single");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    public void Validate_ListeningMultipleChoiceSingle_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidListeningMultipleChoiceSingleJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listening_multiple_choice_single");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── listening_multiple_choice_multi pattern key tests ─────────────────────

    private const string ValidListeningMultipleChoiceMultiJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Listening for multiple supported details: project update",
      "moduleGoal": "Practise listening for several supported details in a short workplace update.",
      "primarySkill": "listening",
      "secondarySkills": [],
      "exerciseType": "listening_multiple_choice_multi",
      "learnContent": {
        "teachingTitle": "Listening for multiple details",
        "explanation": "Listen for several pieces of information at once, and track who, what, and when details as you go.",
        "keyPoints": ["Listen for multiple key details", "Avoid choosing based on one familiar word"],
        "examples": [{ "phrase": "in addition", "meaning": "signals an extra point", "note": "often introduces another correct detail" }],
        "strategy": "Listen for all supported details and select every option the audio confirms.",
        "commonMistakes": ["Choosing only one option", "Selecting an unsupported distractor"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then choose ALL correct answers to the question.",
        "scenario": "A short project update about timeline and staffing changes.",
        "task": "Listen and select every option that is supported by the audio.",
        "exerciseData": {
          "audioScript": "Good afternoon team. The project deadline has moved to next Friday, and we're also adding two more developers to help with testing. The budget remains unchanged.",
          "audioUrl": null,
          "question": "Which changes were announced?",
          "options": [
            { "id": "A", "text": "The deadline moved to next Friday" },
            { "id": "B", "text": "Two more developers were added" },
            { "id": "C", "text": "The budget was increased" },
            { "id": "D", "text": "The project was cancelled" }
          ],
          "correctOptionIds": ["A", "B"],
          "explanation": "The speaker says the deadline moved to Friday and that two more developers are joining, while the budget stays the same and the project continues.",
          "optionExplanations": {
            "A": "Correct — the deadline moved to next Friday.",
            "B": "Correct — two more developers were added for testing.",
            "C": "Incorrect — the budget remains unchanged.",
            "D": "Incorrect — the project was not cancelled."
          },
          "successChecklist": ["Selected all options supported by the audio", "Avoided unsupported distractors"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Complete answer selection", "Distractor elimination"],
        "rubric": [],
        "feedbackFocus": "Help the student listen for all supported details and avoid unsupported distractors.",
        "successCriteria": ["All selected options are supported by the audio.", "No correct options are missed.", "Unsupported distractors are avoided."]
      }
    }
    """;

    [Fact]
    public void Validate_ListeningMultipleChoiceMulti_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidListeningMultipleChoiceMultiJson), ActivityType.ListeningComprehension, "listening_multiple_choice_multi");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionIds")]
    public void Validate_ListeningMultipleChoiceMulti_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidListeningMultipleChoiceMultiJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listening_multiple_choice_multi");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionIds")]
    public void Validate_ListeningMultipleChoiceMulti_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidListeningMultipleChoiceMultiJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listening_multiple_choice_multi");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── listening_fill_in_blanks pattern key tests ────────────────────────────

    private const string ValidListeningFillInBlanksJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Listening fill in the blanks: shift handover",
      "moduleGoal": "Practise listening for missing words using context, grammar, and sound clues.",
      "primarySkill": "listening",
      "secondarySkills": ["writing"],
      "exerciseType": "listening_fill_in_blanks",
      "learnContent": {
        "teachingTitle": "Filling in missing words while listening",
        "explanation": "Listen carefully and use context and grammar clues to predict the missing words.",
        "keyPoints": ["Use context to predict word type", "Listen for word endings and sounds"],
        "examples": [{ "phrase": "in addition", "meaning": "signals an extra point", "note": "often introduces another detail" }],
        "strategy": "Read the surrounding text first, then listen for the missing words.",
        "commonMistakes": ["Guessing without listening", "Ignoring grammar clues"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then choose the correct word for each blank.",
        "scenario": "A short shift handover between two warehouse workers.",
        "task": "Listen and complete the transcript by selecting the correct word for each gap.",
        "exerciseData": {
          "audioScript": "Hi, before you go, just a quick handover. The forklift in bay three needs a battery swap, so please don't use it until that's done. Also, the afternoon delivery has been delayed until four o'clock. Can you let the supervisor know when it arrives?",
          "audioUrl": null,
          "passageWithBlanks": "The forklift in bay three needs a battery {{gap1}}, so please don't use it until that's done. Also, the afternoon delivery has been {{gap2}} until four o'clock. Can you let the {{gap3}} know when it {{gap4}}?",
          "gaps": [
            { "id": "gap1", "answer": "swap", "acceptedAnswers": ["swap"], "options": ["swap", "charge", "check", "test"], "explanation": "The audio says the forklift needs a battery swap." },
            { "id": "gap2", "answer": "delayed", "acceptedAnswers": ["delayed"], "options": ["delayed", "cancelled", "delivered", "returned"], "explanation": "The audio says the delivery has been delayed." },
            { "id": "gap3", "answer": "supervisor", "acceptedAnswers": ["supervisor"], "options": ["supervisor", "driver", "customer", "manager"], "explanation": "The audio asks to let the supervisor know." },
            { "id": "gap4", "answer": "arrives", "acceptedAnswers": ["arrives"], "options": ["arrives", "leaves", "departs", "stops"], "explanation": "The audio asks to be told when it arrives." }
          ],
          "successChecklist": ["Used context and grammar to predict missing words", "Checked each answer against the audio"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Detail recognition", "Vocabulary in context", "Grammar awareness"],
        "rubric": [],
        "feedbackFocus": "Help the student use context and grammar clues to fill in missing words while listening.",
        "successCriteria": ["All gaps are filled with words supported by the audio.", "Grammar and context clues are used correctly."]
      }
    }
    """;

    [Fact]
    public void Validate_ListeningFillInBlanks_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidListeningFillInBlanksJson), ActivityType.ListeningComprehension, "listening_fill_in_blanks");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("passageWithBlanks")]
    [InlineData("gaps")]
    public void Validate_ListeningFillInBlanks_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidListeningFillInBlanksJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listening_fill_in_blanks");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("passageWithBlanks")]
    [InlineData("gaps")]
    [InlineData("options")]
    [InlineData("answer")]
    [InlineData("answers")]
    [InlineData("acceptedAnswers")]
    public void Validate_ListeningFillInBlanks_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidListeningFillInBlanksJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "listening_fill_in_blanks");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── select_missing_word pattern key tests ─────────────────────────────────

    private const string ValidSelectMissingWordJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Predicting the missing word: shift handover",
      "moduleGoal": "Practise predicting a missing word from listening context and grammar clues.",
      "primarySkill": "listening",
      "secondarySkills": [],
      "exerciseType": "select_missing_word",
      "learnContent": {
        "teachingTitle": "Predicting the missing word",
        "explanation": "Listen to the words and grammar around a gap to predict what is likely to come next, and avoid options that only sound familiar.",
        "keyPoints": ["Listen for context before the missing word", "Check the grammar that follows the blank", "Avoid distractors that sound plausible but do not fit"],
        "examples": [{ "phrase": "as soon as possible", "meaning": "signals urgency", "note": "the missing word often relates to timing or action" }],
        "strategy": "Listen for context clues, predict the grammar and meaning, then choose the best fit.",
        "commonMistakes": ["Choosing based on familiar sound only", "Ignoring grammar after the blank", "Missing contrast or cause/effect cues"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then choose the word or phrase that correctly completes it.",
        "scenario": "A warehouse supervisor gives an end-of-shift handover.",
        "task": "Listen and choose the missing word or phrase.",
        "exerciseData": {
          "audioScript": "Before you leave, please make sure the loading dock is clear and the report is submitted by six o'clock.",
          "audioUrl": null,
          "incompleteText": "Before you leave, please make sure the loading dock is clear and the report is {{missing}} by six o'clock.",
          "question": "Choose the missing word or phrase.",
          "options": [
            { "id": "A", "text": "submitted" },
            { "id": "B", "text": "ignored" },
            { "id": "C", "text": "cancelled" },
            { "id": "D", "text": "forgotten" }
          ],
          "correctOptionId": "A",
          "explanation": "The audio says the report is 'submitted by six o'clock', matching option A.",
          "distractorExplanations": {
            "B": "Ignoring the report does not match the instruction to complete it on time.",
            "C": "The report is not cancelled in the audio.",
            "D": "The supervisor reminds the team to submit, not forget, the report."
          },
          "successChecklist": ["Selected the option supported by the audio", "Can explain why the other options are wrong"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Listening context understanding", "Prediction from meaning", "Grammar fit", "Distractor elimination"],
        "rubric": [],
        "feedbackFocus": "Help the student use listening context and grammar clues to choose the best missing word.",
        "successCriteria": ["The selected word or phrase fits the audio meaning.", "The selected option fits the grammar and context.", "The student avoids distractors based on sound or isolated words."]
      }
    }
    """;

    [Fact]
    public void Validate_SelectMissingWord_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidSelectMissingWordJson), ActivityType.ListeningComprehension, "select_missing_word");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("incompleteText")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    public void Validate_SelectMissingWord_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidSelectMissingWordJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "select_missing_word");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("incompleteText")]
    [InlineData("question")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    public void Validate_SelectMissingWord_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidSelectMissingWordJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "select_missing_word");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── highlight_correct_summary pattern key tests ───────────────────────────

    private const string ValidHighlightCorrectSummaryJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Choosing the best summary: project update",
      "moduleGoal": "Practise choosing the summary that best matches a spoken passage.",
      "primarySkill": "listening",
      "secondarySkills": ["reading"],
      "exerciseType": "highlight_correct_summary",
      "learnContent": {
        "teachingTitle": "Choosing the best summary",
        "explanation": "Listen for the overall meaning of the whole passage, then compare each summary against it and reject any that add, distort, or omit a key fact.",
        "keyPoints": ["Listen for the main idea, not single words", "Compare each summary against the whole passage", "Reject summaries that distort or omit key facts"],
        "examples": [{ "phrase": "the main point is", "meaning": "signals the central idea", "note": "use it to anchor your summary choice" }],
        "strategy": "Listen for the gist, then select the summary that most accurately reflects it.",
        "commonMistakes": ["Matching one detail but missing the main point", "Choosing a summary that adds new information", "Choosing a summary that contradicts a key fact"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then choose the summary that best matches what you heard.",
        "scenario": "A team lead gives a short project status update.",
        "task": "Listen and choose the best summary.",
        "exerciseData": {
          "audioScript": "Thanks everyone. The redesign is on track and we'll ship the first release next Friday. We added one more reviewer to speed up testing, and the budget is unchanged.",
          "audioUrl": null,
          "question": "Which summary best matches the audio?",
          "options": [
            { "id": "A", "text": "The redesign is on track to ship next Friday, with an extra reviewer and no budget change." },
            { "id": "B", "text": "The redesign is delayed and the budget has increased." },
            { "id": "C", "text": "The redesign shipped last Friday and testing is finished." },
            { "id": "D", "text": "The redesign was cancelled because of budget cuts." }
          ],
          "correctOptionId": "A",
          "explanation": "The speaker says the redesign is on track to ship next Friday, an extra reviewer was added, and the budget is unchanged.",
          "distractorExplanations": {
            "B": "The audio says the work is on track and the budget is unchanged.",
            "C": "The release is next Friday, not last Friday.",
            "D": "Nothing was cancelled and the budget was not cut."
          },
          "successChecklist": ["Selected the summary supported by the audio", "Can explain why the other summaries are wrong"]
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Main-idea comprehension", "Summary accuracy", "Detail verification", "Distractor elimination"],
        "rubric": [],
        "feedbackFocus": "Help the student listen for the overall meaning and choose the most accurate summary.",
        "successCriteria": ["The selected summary matches the main idea of the audio.", "The selected summary does not add or distort facts.", "The student avoids summaries that match only one detail."]
      }
    }
    """;

    [Fact]
    public void Validate_HighlightCorrectSummary_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidHighlightCorrectSummaryJson), ActivityType.ListeningComprehension, "highlight_correct_summary");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    public void Validate_HighlightCorrectSummary_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidHighlightCorrectSummaryJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "highlight_correct_summary");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("options")]
    [InlineData("correctOptionId")]
    [InlineData("answerKey")]
    [InlineData("correctAnswer")]
    [InlineData("selectedAnswer")]
    [InlineData("summaryOptions")]
    [InlineData("submit")]
    [InlineData("checkAnswer")]
    public void Validate_HighlightCorrectSummary_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidHighlightCorrectSummaryJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "highlight_correct_summary");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── highlight_incorrect_words pattern key tests ───────────────────────────

    private const string ValidHighlightIncorrectWordsJson = """
    {
      "schemaVersion": "module_stage_v1",
      "title": "Spotting words that differ: status call",
      "moduleGoal": "Practise listening closely and spotting words that differ from spoken audio.",
      "primarySkill": "listening",
      "secondarySkills": ["reading"],
      "exerciseType": "highlight_incorrect_words",
      "learnContent": {
        "teachingTitle": "Spotting words that differ",
        "explanation": "Read along while you listen and notice when a written word does not match what you hear; even a small change can alter the meaning.",
        "keyPoints": ["Read along while listening", "Small changes alter meaning", "Focus on content words"],
        "examples": [{ "phrase": "next week", "meaning": "a future time", "note": "compare it to what you hear" }],
        "strategy": "Compare every word you read against what you hear.",
        "commonMistakes": ["Selecting words that match the audio", "Missing a similar-sounding change", "Reading without listening"],
        "sourceLanguageSupport": null
      },
      "practiceContent": {
        "instructions": "Listen to the audio, then click the words that are different.",
        "scenario": "A manager confirms a meeting time.",
        "task": "Listen and select every word that differs from the audio.",
        "exerciseData": {
          "audioScript": "Let's meet on Monday at nine to review the final budget.",
          "audioUrl": null,
          "displayTranscript": "Let's meet on Tuesday at nine to review the draft budget.",
          "tokens": [
            { "id": "t0", "text": "Let's", "position": 0 },
            { "id": "t1", "text": "meet", "position": 1 },
            { "id": "t2", "text": "on", "position": 2 },
            { "id": "t3", "text": "Tuesday", "position": 3 },
            { "id": "t4", "text": "at", "position": 4 },
            { "id": "t5", "text": "nine", "position": 5 },
            { "id": "t6", "text": "to", "position": 6 },
            { "id": "t7", "text": "review", "position": 7 },
            { "id": "t8", "text": "the", "position": 8 },
            { "id": "t9", "text": "draft", "position": 9 },
            { "id": "t10", "text": "budget.", "position": 10 }
          ],
          "incorrectTokenIds": ["t3", "t9"],
          "corrections": { "t3": "Monday", "t9": "final" },
          "tokenExplanations": { "t3": "The audio says Monday, not Tuesday.", "t9": "The audio says final, not draft." },
          "question": "Which words are different from the audio?",
          "explanation": "Two words were changed: the day and the budget description."
        }
      },
      "feedbackPlan": {
        "evaluationCriteria": ["Careful listening", "Word-level accuracy", "Difference detection"],
        "rubric": [],
        "feedbackFocus": "Help the student detect every word that differs from the audio.",
        "successCriteria": ["The student selects all changed words.", "The student avoids words that match the audio.", "The student understands each change."]
      }
    }
    """;

    [Fact]
    public void Validate_HighlightIncorrectWords_WithValidPayload_ReturnsValid()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(ValidHighlightIncorrectWordsJson), ActivityType.ListeningComprehension, "highlight_incorrect_words");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("displayTranscript")]
    [InlineData("tokens")]
    [InlineData("incorrectTokenIds")]
    public void Validate_HighlightIncorrectWords_MissingRequiredKey_Fails(string keyToRemove)
    {
        var json = RemoveExerciseDataKey(ValidHighlightIncorrectWordsJson, keyToRemove);

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "highlight_incorrect_words");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(keyToRemove));
    }

    [Theory]
    [InlineData("audioScript")]
    [InlineData("transcript")]
    [InlineData("displayTranscript")]
    [InlineData("tokens")]
    [InlineData("incorrectTokenIds")]
    [InlineData("corrections")]
    [InlineData("answerKey")]
    [InlineData("correctAnswer")]
    [InlineData("selectedTokenIds")]
    [InlineData("submit")]
    [InlineData("checkAnswer")]
    public void Validate_HighlightIncorrectWords_WithControlKeyInLearnContent_Fails(string forbiddenKey)
    {
        var json = AddLearnProperty(Parse(ValidHighlightIncorrectWordsJson), forbiddenKey, "not allowed");

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.ListeningComprehension, "highlight_incorrect_words");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(forbiddenKey));
    }

    // ── Phase 8N: configurable count enforcement ──────────────────────────────

    private static string FillInBlanksJson(int gapCount)
    {
        var gaps = string.Join(",", Enumerable.Range(1, gapCount)
            .Select(i => $$"""{ "id": "g{{i}}", "answer": "word{{i}}" }"""));
        return $$"""
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "explanation": "x" },
          "practiceContent": {
            "exerciseData": {
              "passageWithBlanks": "Some [[g1]] text.",
              "gaps": [ {{gaps}} ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "x" }
        }
        """;
    }

    private static readonly PracticeCountSettings ReadingFillCounts = new(3, 6, 3, 5);

    [Fact]
    public void Validate_FillInBlanks_GapCountBelowMin_Fails()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(FillInBlanksJson(2)), ActivityType.ReadingTask, "reading_fill_in_blanks", ReadingFillCounts);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("gaps") && e.Contains("range"));
    }

    [Fact]
    public void Validate_FillInBlanks_GapCountAboveMax_Fails()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(FillInBlanksJson(7)), ActivityType.ReadingTask, "reading_fill_in_blanks", ReadingFillCounts);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("gaps") && e.Contains("range"));
    }

    [Fact]
    public void Validate_FillInBlanks_GapCountWithinRange_Passes()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(FillInBlanksJson(4)), ActivityType.ReadingTask, "reading_fill_in_blanks", ReadingFillCounts);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FillInBlanks_WithoutCountSettings_SkipsCountEnforcement()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(FillInBlanksJson(2)), ActivityType.ReadingTask, "reading_fill_in_blanks");

        result.IsValid.Should().BeTrue();
    }

    // ── respond_to_situation ───────────────────────────────────────────────────

    private static string RespondToSituationJson(int itemCount = 2) => $$"""
    {
      "schemaVersion": "module_stage_v1",
      "learnContent": { "teachingTitle": "How to respond", "explanation": "General strategy." },
      "practiceContent": {
        "exerciseData": {
          "items": [
            {{string.Join(",\n            ", Enumerable.Range(1, itemCount).Select(i => $$"""{"id":"sit{{i}}","situation":"You arrive at a hotel and the room is not ready. What do you say?"}"""))}}
          ]
        }
      },
      "feedbackPlan": { "feedbackFocus": "relevance and tone" }
    }
    """;

    [Fact]
    public void Validate_RespondToSituation_ValidContent_Passes()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(RespondToSituationJson()), ActivityType.SpeakingRolePlay, "respond_to_situation");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RespondToSituation_MissingItems_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "Strategy" },
          "practiceContent": { "exerciseData": {} },
          "feedbackPlan": {}
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "respond_to_situation");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("items"));
    }

    [Fact]
    public void Validate_RespondToSituation_ItemMissingSituationField_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "Strategy" },
          "practiceContent": {
            "exerciseData": {
              "items": [{ "id": "sit1" }]
            }
          },
          "feedbackPlan": {}
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "respond_to_situation");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("situation"));
    }

    [Fact]
    public void Validate_RespondToSituation_LearnContentDoesNotContainItems_Passes()
    {
        // learnContent should never contain exercise items
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "How to respond in real-life situations", "explanation": "General strategy." },
          "practiceContent": {
            "exerciseData": {
              "items": [{ "id": "sit1", "situation": "You are at a hotel and the room is not ready." }]
            }
          },
          "feedbackPlan": { "feedbackFocus": "relevance" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "respond_to_situation");

        result.IsValid.Should().BeTrue();
    }

    // ── describe_image ─────────────────────────────────────────────────────────

    private static string DescribeImageJson(int itemCount = 1) => $$"""
    {
      "schemaVersion": "module_stage_v1",
      "learnContent": { "teachingTitle": "Describing images" },
      "practiceContent": {
        "exerciseData": {
          "items": [
            {{string.Join(",", Enumerable.Range(1, itemCount).Select(i => $$"""
            { "id": "img{{i}}", "imagePrompt": "A busy street with colourful market stalls and people shopping." }
            """))}}
          ]
        }
      },
      "feedbackPlan": { "feedbackFocus": "detail and vocabulary" }
    }
    """;

    [Fact]
    public void Validate_DescribeImage_ValidContent_Passes()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(DescribeImageJson()), ActivityType.SpeakingRolePlay, "describe_image");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DescribeImage_MissingItems_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "Describing images" },
          "practiceContent": { "exerciseData": {} },
          "feedbackPlan": { "feedbackFocus": "detail" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "describe_image");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("items"));
    }

    [Fact]
    public void Validate_DescribeImage_ItemMissingImagePromptField_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "Describing images" },
          "practiceContent": {
            "exerciseData": {
              "items": [ { "id": "img1" } ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "detail" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "describe_image");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("imagePrompt"));
    }

    [Fact]
    public void Validate_DescribeImage_LearnContentDoesNotContainItems_Passes()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": {
            "teachingTitle": "How to describe images",
            "explanation": "Use location words and descriptive vocabulary."
          },
          "practiceContent": {
            "exerciseData": {
              "items": [
                { "id": "img1", "imagePrompt": "A park on a sunny day with children playing." }
              ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "detail and vocabulary" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "describe_image");

        result.IsValid.Should().BeTrue();
    }

    // ── retell_lecture ──────────────────────────────────────────────────────────

    private static string RetellLectureJson(int itemCount = 1) => $$"""
    {
      "schemaVersion": "module_stage_v1",
      "learnContent": { "teachingTitle": "How to retell a lecture" },
      "practiceContent": {
        "exerciseData": {
          "items": [
            {{string.Join(",", Enumerable.Range(1, itemCount).Select(i => $$"""
            { "id": "lec{{i}}", "lectureTitle": "How Sleep Affects Memory", "audioScript": "Sleep is essential for memory. During deep sleep the brain consolidates what you learned during the day." }
            """))}}
          ]
        }
      },
      "feedbackPlan": { "feedbackFocus": "main ideas and key details" }
    }
    """;

    [Fact]
    public void Validate_RetellLecture_ValidContent_Passes()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(RetellLectureJson()), ActivityType.SpeakingRolePlay, "retell_lecture");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RetellLecture_MissingItems_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "How to retell a lecture" },
          "practiceContent": { "exerciseData": {} },
          "feedbackPlan": { "feedbackFocus": "main ideas" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "retell_lecture");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("items"));
    }

    [Fact]
    public void Validate_RetellLecture_ItemMissingAudioScript_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "How to retell a lecture" },
          "practiceContent": {
            "exerciseData": {
              "items": [ { "id": "lec1", "lectureTitle": "Sleep and Memory" } ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "main ideas" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "retell_lecture");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("audioScript"));
    }

    [Fact]
    public void Validate_RetellLecture_LearnContentDoesNotContainItems_Passes()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": {
            "teachingTitle": "How to retell key ideas clearly",
            "explanation": "Listen for the main topic first, then note key supporting details."
          },
          "practiceContent": {
            "exerciseData": {
              "items": [
                { "id": "lec1", "lectureTitle": "Sleep and Memory", "audioScript": "Sleep is essential for memory consolidation." }
              ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "main ideas and key details" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "retell_lecture");

        result.IsValid.Should().BeTrue();
    }

    // ── summarize_group_discussion ───────────────────────────────────────────────

    private static string SummarizeGroupDiscussionJson(int itemCount = 1) => $$"""
    {
      "schemaVersion": "module_stage_v1",
      "learnContent": { "teachingTitle": "How to summarize a group discussion" },
      "practiceContent": {
        "exerciseData": {
          "items": [
            {{string.Join(",", Enumerable.Range(1, itemCount).Select(i => $$"""
            { "id": "disc{{i}}", "discussionTitle": "Planning a Weekend Trip", "audioScript": "Ali: I think we should go to the mountains. Sara: I prefer the beach. Ali: OK, let's vote." }
            """))}}
          ]
        }
      },
      "feedbackPlan": { "feedbackFocus": "main points and speaker views" }
    }
    """;

    [Fact]
    public void Validate_SummarizeGroupDiscussion_ValidContent_Passes()
    {
        var result = ModuleStageContentValidator.Validate(
            Parse(SummarizeGroupDiscussionJson()), ActivityType.SpeakingRolePlay, "summarize_group_discussion");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SummarizeGroupDiscussion_MissingItems_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "How to summarize a group discussion" },
          "practiceContent": { "exerciseData": {} },
          "feedbackPlan": { "feedbackFocus": "main points" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "summarize_group_discussion");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("items"));
    }

    [Fact]
    public void Validate_SummarizeGroupDiscussion_ItemMissingAudioScript_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "How to summarize a group discussion" },
          "practiceContent": {
            "exerciseData": {
              "items": [ { "id": "disc1", "discussionTitle": "Planning a Trip" } ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "main points" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "summarize_group_discussion");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("audioScript"));
    }

    [Fact]
    public void Validate_SummarizeGroupDiscussion_ItemMissingDiscussionTitle_Fails()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": { "teachingTitle": "How to summarize a group discussion" },
          "practiceContent": {
            "exerciseData": {
              "items": [ { "id": "disc1", "audioScript": "Ali: Let's go. Sara: OK." } ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "main points" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "summarize_group_discussion");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("discussionTitle"));
    }

    [Fact]
    public void Validate_SummarizeGroupDiscussion_LearnContentDoesNotContainItems_Passes()
    {
        var json = """
        {
          "schemaVersion": "module_stage_v1",
          "learnContent": {
            "teachingTitle": "How to summarize a group discussion clearly",
            "explanation": "Listen for each speaker's main point and any agreements reached."
          },
          "practiceContent": {
            "exerciseData": {
              "items": [
                { "id": "disc1", "discussionTitle": "Planning a Trip", "audioScript": "Ali: Mountains. Sara: Beach. Ali: Let's vote." }
              ]
            }
          },
          "feedbackPlan": { "feedbackFocus": "main points and speaker views" }
        }
        """;

        var result = ModuleStageContentValidator.Validate(
            Parse(json), ActivityType.SpeakingRolePlay, "summarize_group_discussion");

        result.IsValid.Should().BeTrue();
    }

}
