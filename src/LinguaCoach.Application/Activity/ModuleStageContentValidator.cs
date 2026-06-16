using System.Text.Json;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Ok() => new(true, []);
    public static ValidationResult Fail(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Optional per-format count bounds used to enforce item/option counts.
/// When null, count enforcement is skipped (callers without registry access).
/// </summary>
public sealed record PracticeCountSettings(
    int MinItemsPerPractice,
    int MaxItemsPerPractice,
    int MinOptionsPerItem,
    int MaxOptionsPerItem);

/// <summary>
/// Classifies how workload sanity is applied to a format.
/// SingleSubstantialTask: one item is valid because the task itself is substantial (essay, summary, lecture retell).
/// MultiItem: multiple items are normally expected; one trivial item is considered under-workload.
/// </summary>
public enum WorkloadMode
{
    /// <summary>One item/task is the full expected workload for this format.</summary>
    SingleSubstantialTask,
    /// <summary>Multiple items are normally expected; one trivial item is a warning signal.</summary>
    MultiItem
}

/// <summary>
/// Centralised mapping from pattern key to workload mode.
/// Formats not listed here default to MultiItem.
/// </summary>
public static class WorkloadModeRegistry
{
    // Single-substantial-task formats: one task IS the full exercise.
    // Skipping workload enforcement for these prevents false failures.
    private static readonly HashSet<string> SingleSubstantialTaskPatterns = new(StringComparer.Ordinal)
    {
        "write_essay",
        "summarize_written_text",
        "summarize_spoken_text",
        "open_writing_task",
        "spoken_response_from_prompt",
        "speaking_roleplay_turn",
        "email_reply",
        "teams_chat_simulation",
        "describe_image",
        "retell_lecture",
        "summarize_group_discussion",
        "respond_to_situation",
        // Reading: single passage + single question formats are valid one-item tasks
        "reading_multiple_choice_single",
        "reading_multiple_choice_multi",
        "listening_multiple_choice_single",
        "listening_multiple_choice_multi",
        "select_missing_word",
        "highlight_correct_summary",
        "highlight_incorrect_words",
        "lesson_reflection",
    };

    public static WorkloadMode GetMode(string? patternKey) =>
        patternKey is not null && SingleSubstantialTaskPatterns.Contains(patternKey)
            ? WorkloadMode.SingleSubstantialTask
            : WorkloadMode.MultiItem;

    public static bool IsSingleSubstantialTask(string? patternKey) =>
        GetMode(patternKey) == WorkloadMode.SingleSubstantialTask;
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
        "summaryOptions", "checkAnswer",
        "displayTranscript", "tokens", "incorrectTokenIds", "corrections",
        "tokenExplanations", "selectedTokenIds",
    ];

    // Additional forbidden learnContent keys scoped to a specific pattern key.
    // Reserved for keys that are legitimate teaching content for most formats but
    // must be hidden for one specific format. Note: learnContent.keyPoints is the
    // standard teaching-points array (LearnContentDto.KeyPoints) and is generic
    // strategy, never the expected answer — the answer-bearing keyPoints live in
    // practiceContent.exerciseData and are never surfaced to the Learn page or the
    // renderer before submission.
    private static readonly Dictionary<string, string[]> ForbiddenLearnContentKeysByPatternKey = new(StringComparer.Ordinal)
    {
    };

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
        ["highlight_correct_summary"]            = ["audioScript", "options", "correctOptionId"],
        ["highlight_incorrect_words"]            = ["audioScript", "displayTranscript", "tokens", "incorrectTokenIds"],
        ["write_from_dictation"]                 = ["items"],
        ["summarize_spoken_text"]                = ["audioScript", "prompt"],
        ["answer_short_question"]                = ["items"],
        ["read_aloud"]                           = ["items"],
        ["repeat_sentence"]                      = ["items"],
        ["respond_to_situation"]                 = ["items"],
        ["describe_image"]                       = ["items"],
        ["retell_lecture"]                       = ["items"],
        ["summarize_group_discussion"]           = ["items"],
    };

    // Per-item field requirements for item-array formats: pattern key => required item fields.
    private static readonly Dictionary<string, string[]> RequiredItemFieldsByPatternKey = new(StringComparer.Ordinal)
    {
        ["write_from_dictation"]    = ["id", "audioScript", "answer"],
        ["answer_short_question"]   = ["id", "question"],
        ["read_aloud"]              = ["id", "text"],
        ["repeat_sentence"]         = ["id", "sentence"],
        ["respond_to_situation"]    = ["id", "situation"],
        ["describe_image"]          = ["id", "imagePrompt"],
        ["retell_lecture"]                       = ["id", "lectureTitle", "audioScript"],
        ["summarize_group_discussion"]           = ["id", "discussionTitle", "audioScript"],
    };

    public static ValidationResult Validate(
        JsonElement root,
        ActivityType activityType,
        string? exercisePatternKey = null,
        PracticeCountSettings? countSettings = null)
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

            if (exercisePatternKey is not null
                && ForbiddenLearnContentKeysByPatternKey.TryGetValue(exercisePatternKey, out var patternForbidden))
            {
                foreach (var forbiddenKey in patternForbidden)
                {
                    if (HasPropertyIgnoreCase(learnContent, forbiddenKey))
                        errors.Add($"learnContent must not contain \"{forbiddenKey}\" (practice/exercise data).");
                }
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

                if (exercisePatternKey is not null
                    && RequiredItemFieldsByPatternKey.TryGetValue(exercisePatternKey, out var itemFields))
                    ValidateItemFields(exerciseData, itemFields, errors);

                if (countSettings is not null && exercisePatternKey is not null)
                    EnforceCounts(exerciseData, exercisePatternKey, countSettings, errors);

                if (countSettings is not null && exercisePatternKey is not null)
                    EnforceWorkloadSanity(exerciseData, exercisePatternKey, countSettings, errors);
            }
        }

        ValidateDurationMetadata(root, exercisePatternKey, countSettings, errors);

        return errors.Count == 0 ? ValidationResult.Ok() : ValidationResult.Fail(errors);
    }

    // Item-count formats: pattern key => exerciseData array field whose length is the item count.
    // Also used by EnforceWorkloadSanity to locate the countable array for multi-item formats.
    private static readonly Dictionary<string, string> ItemCountArrayByPattern = new(StringComparer.Ordinal)
    {
        // Pattern-backed multi-item formats
        ["gap_fill_workplace_phrase"]      = "items",
        ["listen_and_gap_fill"]            = "gaps",
        ["listen_and_answer"]              = "questions",
        ["phrase_match"]                   = "pairs",
        // Reading fill/reorder
        ["reading_fill_in_blanks"]         = "gaps",
        ["reading_writing_fill_in_blanks"] = "gaps",
        ["listening_fill_in_blanks"]       = "gaps",
        ["reorder_paragraphs"]             = "items",
        ["highlight_incorrect_words"]      = "incorrectTokenIds",
        // Listening/speaking multi-item
        ["write_from_dictation"]           = "items",
        ["answer_short_question"]          = "items",
        ["read_aloud"]                     = "items",
        ["repeat_sentence"]                = "items",
        ["respond_to_situation"]           = "items",
        ["describe_image"]                 = "items",
        ["retell_lecture"]                 = "items",
        ["summarize_group_discussion"]     = "items",
    };

    private static void ValidateItemFields(JsonElement exerciseData, string[] requiredItemFields, List<string> errors)
    {
        JsonElement itemsArray = default;
        var found = false;
        foreach (var prop in exerciseData.EnumerateObject())
        {
            if (string.Equals(prop.Name, "items", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Array)
            {
                itemsArray = prop.Value;
                found = true;
                break;
            }
        }
        if (!found) return;

        var index = 0;
        foreach (var item in itemsArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"practiceContent.exerciseData.items[{index}] must be an object.");
                index++;
                continue;
            }
            foreach (var field in requiredItemFields)
            {
                if (!HasPropertyIgnoreCase(item, field))
                    errors.Add($"practiceContent.exerciseData.items[{index}] is missing required field \"{field}\".");
            }
            index++;
        }
    }

    // Option-count formats: pattern key => exerciseData array field whose length is the option count.
    private static readonly Dictionary<string, string> OptionCountArrayByPattern = new(StringComparer.Ordinal)
    {
        ["reading_multiple_choice_single"]   = "options",
        ["reading_multiple_choice_multi"]    = "options",
        ["listening_multiple_choice_single"] = "options",
        ["listening_multiple_choice_multi"]  = "options",
        ["select_missing_word"]              = "options",
        ["highlight_correct_summary"]        = "options",
    };

    private static void EnforceCounts(JsonElement exerciseData, string patternKey, PracticeCountSettings counts, List<string> errors)
    {
        if (ItemCountArrayByPattern.TryGetValue(patternKey, out var itemField)
            && TryGetArrayLength(exerciseData, itemField, out var itemCount))
        {
            if (itemCount < counts.MinItemsPerPractice || itemCount > counts.MaxItemsPerPractice)
                errors.Add($"practiceContent.exerciseData.{itemField} count {itemCount} is outside allowed range [{counts.MinItemsPerPractice}, {counts.MaxItemsPerPractice}].");
        }

        if (OptionCountArrayByPattern.TryGetValue(patternKey, out var optField)
            && TryGetArrayLength(exerciseData, optField, out var optCount))
        {
            if (optCount < counts.MinOptionsPerItem || optCount > counts.MaxOptionsPerItem)
                errors.Add($"practiceContent.exerciseData.{optField} count {optCount} is outside allowed range [{counts.MinOptionsPerItem}, {counts.MaxOptionsPerItem}].");
        }
    }

    private static bool TryGetArrayLength(JsonElement obj, string name, out int length)
    {
        length = 0;
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Array)
            {
                length = prop.Value.GetArrayLength();
                return true;
            }
        }
        return false;
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

    /// <summary>
    /// Validates optional duration metadata fields when present.
    /// Old content without duration fields is always accepted (backward compatible).
    /// New content with duration fields must have positive values and sane totals.
    /// When estimatedPracticeMinutes is present for a multi-item format, the item count
    /// must be proportionate to the claimed practice time.
    /// </summary>
    private static void ValidateDurationMetadata(
        JsonElement root,
        string? patternKey,
        PracticeCountSettings? countSettings,
        List<string> errors)
    {
        // All duration fields are optional — missing = backward compatible, no error.
        var hasDuration = TryGetPositiveInt(root, "estimatedDurationMinutes", out var totalMinutes, errors);
        var hasLearn    = TryGetPositiveInt(root, "estimatedLearnMinutes",    out var learnMinutes,    errors);
        var hasPractice = TryGetPositiveInt(root, "estimatedPracticeMinutes", out var practiceMinutes, errors);
        var hasFeedback = TryGetPositiveInt(root, "estimatedFeedbackMinutes", out var feedbackMinutes, errors);

        // Total parts must not obviously exceed total duration.
        if (hasDuration && (hasLearn || hasPractice || hasFeedback))
        {
            var partTotal = (hasLearn ? learnMinutes : 0)
                          + (hasPractice ? practiceMinutes : 0)
                          + (hasFeedback ? feedbackMinutes : 0);
            // Allow 1-minute tolerance for rounding.
            if (partTotal > totalMinutes + 1)
                errors.Add(
                    $"Duration parts total {partTotal} min exceeds estimatedDurationMinutes {totalMinutes} min.");
        }

        // Practice-time vs workload check: only meaningful for multi-item formats.
        if (hasPractice && patternKey is not null && countSettings is not null
            && !WorkloadModeRegistry.IsSingleSubstantialTask(patternKey)
            && ItemCountArrayByPattern.TryGetValue(patternKey, out var itemField)
            && root.TryGetProperty("practiceContent", out var practiceContent)
            && practiceContent.ValueKind == JsonValueKind.Object)
        {
            // Look inside exerciseData if present, else practiceContent directly.
            var exerciseData = practiceContent.TryGetProperty("exerciseData", out var ed) && ed.ValueKind == JsonValueKind.Object
                ? ed
                : practiceContent;

            if (TryGetArrayLength(exerciseData, itemField, out var itemCount))
            {
                // Rough rule: each item takes ~30 seconds minimum.
                // A 2-minute practice session should have at least 4 items for short-item formats,
                // but we keep this conservative: require at least ceil(practiceMinutes * 1.0) items.
                // This catches the obvious "5-minute session with 1 trivial item" case.
                var minItemsForDuration = practiceMinutes; // 1 item per claimed minute, conservative
                if (itemCount < minItemsForDuration && itemCount < countSettings.MinItemsPerPractice)
                {
                    errors.Add(
                        $"Workload mismatch for \"{patternKey}\": " +
                        $"estimatedPracticeMinutes is {practiceMinutes} but {itemField} has only {itemCount} item(s). " +
                        $"Increase item count or reduce estimated practice time.");
                }
            }
        }
    }

    private static bool TryGetPositiveInt(JsonElement root, string name, out int value, List<string> errors)
    {
        value = 0;
        foreach (var prop in root.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind == JsonValueKind.Null)
                return false; // null = not present, backward compatible
            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out value))
            {
                errors.Add($"\"{name}\" must be a positive integer when present.");
                return false;
            }
            if (value <= 0)
            {
                errors.Add($"\"{name}\" must be positive (got {value}).");
                return false;
            }
            return true;
        }
        return false; // not present = backward compatible
    }

    /// <summary>
    /// Workload sanity check for multi-item formats.
    /// SingleSubstantialTask formats (essay, summary, retell) are exempt — one task is the full exercise.
    /// MultiItem formats must meet the configured MinItemsPerPractice; if MinItemsPerPractice is 2+
    /// and only 1 item is present, the activity is considered under-workload.
    /// </summary>
    private static void EnforceWorkloadSanity(
        JsonElement exerciseData,
        string patternKey,
        PracticeCountSettings counts,
        List<string> errors)
    {
        // Single-substantial-task formats are always valid with one task.
        if (WorkloadModeRegistry.IsSingleSubstantialTask(patternKey))
            return;

        // For multi-item formats: find the item count array and check it meets MinItemsPerPractice.
        if (!ItemCountArrayByPattern.TryGetValue(patternKey, out var itemField))
            return;

        if (!TryGetArrayLength(exerciseData, itemField, out var itemCount))
            return;

        // EnforceCounts already reports out-of-range errors.
        // EnforceWorkloadSanity adds a clearer semantic message when count < min.
        if (itemCount < counts.MinItemsPerPractice)
        {
            errors.Add(
                $"Workload too small for \"{patternKey}\": " +
                $"{itemField} has {itemCount} item(s) but this format requires at least {counts.MinItemsPerPractice}. " +
                $"The activity would not provide meaningful practice.");
        }
    }
}
