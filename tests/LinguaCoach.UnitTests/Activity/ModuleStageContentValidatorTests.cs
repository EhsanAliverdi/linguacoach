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

}
