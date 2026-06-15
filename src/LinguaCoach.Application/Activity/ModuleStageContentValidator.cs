using System.Text.Json;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok() => new(true, []);
    public static ValidationResult Fail(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Validates AI-generated activity JSON against the module_stage_v1 contract:
/// schemaVersion + learnContent/practiceContent/feedbackPlan sections, with
/// learnContent forbidden from carrying any practice/exercise data.
/// </summary>
public static class ModuleStageContentValidator
{
    private static readonly string[] ForbiddenLearnContentKeys =
    [
        "audioScript", "audioUrl", "questions", "expectedAnswer", "correctAnswer",
        "answerKey", "gaps", "pairs", "transcriptAvailableAfterSubmit", "transcript",
        "exerciseData", "interactionMode", "submittedAnswer", "studentAnswer",
        "textarea", "submitLabel", "checkLabel", "answerControls",
        "recordingControls", "microphoneInstructions", "startRecording", "stopRecording",
        "practiceMode", "selectedAnswer", "selectedAnswers", "options", "fillBlank",
        "matchingPairs", "submitButton", "checkButton",
        "passage", "question", "correctOptionId", "distractorExplanations",
        "correctOptionIds", "optionExplanations",
        "passageWithBlanks", "gaps",
        "items", "correctOrder", "selectedOrder", "checkAnswer",
        "sourceText", "prompt", "expectedSummary", "modelSummary",
        "submittedSummary",
        "modelEssay", "expectedEssay", "submittedEssay",
        "answer", "answers", "acceptedAnswers", "submit",
        "incompleteText", "missingWord", "missingPhrase",
    ];

    private static readonly Dictionary<ActivityType, string[]> RequiredPracticeKeysByType = new()
    {
        [ActivityType.ListeningComprehension] = ["audioScript", "questions"],
        [ActivityType.WritingScenario] = ["prompt", "situation", "audience", "tone"],
        [ActivityType.SpeakingRolePlay] = ["prompt", "role", "partnerRole", "situation"],
        [ActivityType.VocabularyPractice] = ["items", "practiceMode"],
    };

    // Pattern-key-based required keys take precedence over ActivityType-based keys.
    // Used for pattern-backed staged activities where ActivityType alone is ambiguous.
    private static readonly Dictionary<string, string[]> RequiredPracticeKeysByPatternKey = new(StringComparer.Ordinal)
    {
        ["phrase_match"]              = ["pairs"],
        ["gap_fill_workplace_phrase"] = ["items"],
        ["listen_and_answer"]         = ["audioScript", "questions"],
        ["listen_and_gap_fill"]       = ["audioScript", "gaps"],
        ["email_reply"]               = ["prompt", "incomingMessage"],
        ["teams_chat_simulation"]     = ["prompt", "chatHistory"],
        ["open_writing_task"]              = ["prompt"],
        ["spoken_response_from_prompt"]    = ["prompt"],
        ["speaking_roleplay_turn"]         = ["prompt", "partnerTurn"],
        ["lesson_reflection"]              = ["prompt"],
        ["reading_multiple_choice_single"] = ["passage", "question", "options", "correctOptionId"],
        ["reading_multiple_choice_multi"]   = ["passage", "question", "options", "correctOptionIds"],
        ["reading_fill_in_blanks"]               = ["passageWithBlanks", "gaps"],
        ["reorder_paragraphs"]                   = ["items", "correctOrder"],
        ["reading_writing_fill_in_blanks"]       = ["passageWithBlanks", "gaps"],
        ["summarize_written_text"]               = ["sourceText", "prompt"],
        ["write_essay"]                           = ["prompt", "topic"],
        ["listening_multiple_choice_single"]     = ["audioScript", "question", "options", "correctOptionId"],
        ["listening_multiple_choice_multi"]      = ["audioScript", "question", "options", "correctOptionIds"],
        ["listening_fill_in_blanks"]             = ["audioScript", "passageWithBlanks", "gaps"],
        ["select_missing_word"]                  = ["audioScript", "incompleteText", "options", "correctOptionId"],
    };

    public static ValidationResult Validate(JsonElement root, ActivityType activityType, string? exercisePatternKey = null)
    {
        var errors = new List<string>();

        if (!root.TryGetProperty("schemaVersion", out var schemaVersion)
            || schemaVersion.ValueKind != JsonValueKind.String
            || schemaVersion.GetString() != ModuleStageSchema.Version)
        {
            errors.Add($"Missing or invalid schemaVersion (expected \"{ModuleStageSchema.Version}\").");
        }

        if (!root.TryGetProperty("learnContent", out var learnContent) || learnContent.ValueKind != JsonValueKind.Object)
            errors.Add("Missing learnContent object.");

        if (!root.TryGetProperty("practiceContent", out var practiceContent) || practiceContent.ValueKind != JsonValueKind.Object)
            errors.Add("Missing practiceContent object.");

        if (!root.TryGetProperty("feedbackPlan", out var feedbackPlan) || feedbackPlan.ValueKind != JsonValueKind.Object)
            errors.Add("Missing feedbackPlan object.");

        if (learnContent.ValueKind == JsonValueKind.Object)
        {
            foreach (var forbiddenKey in ForbiddenLearnContentKeys)
            {
                if (HasPropertyIgnoreCase(learnContent, forbiddenKey))
                    errors.Add($"learnContent must not contain \"{forbiddenKey}\" (practice/exercise data).");
            }
        }

        if (practiceContent.ValueKind == JsonValueKind.Object)
        {
            string[]? requiredKeys = null;
            if (exercisePatternKey is not null && RequiredPracticeKeysByPatternKey.TryGetValue(exercisePatternKey, out var patternKeys))
                requiredKeys = patternKeys;
            else
                RequiredPracticeKeysByType.TryGetValue(activityType, out requiredKeys);

            if (requiredKeys is not null)
            {
                var exerciseData = practiceContent.TryGetProperty("exerciseData", out var ed) && ed.ValueKind == JsonValueKind.Object
                    ? ed
                    : practiceContent;

                foreach (var requiredKey in requiredKeys)
                {
                    if (!HasPropertyIgnoreCase(exerciseData, requiredKey))
                        errors.Add($"practiceContent.exerciseData is missing required field \"{requiredKey}\".");
                }
            }
        }

        return errors.Count == 0 ? ValidationResult.Ok() : ValidationResult.Fail(errors);
    }

    private static bool HasPropertyIgnoreCase(JsonElement obj, string name)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
