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
    ];

    private static readonly Dictionary<ActivityType, string[]> RequiredPracticeKeysByType = new()
    {
        [ActivityType.ListeningComprehension] = ["audioScript", "questions"],
        [ActivityType.WritingScenario] = ["prompt", "situation", "audience", "tone"],
        [ActivityType.SpeakingRolePlay] = ["prompt", "role", "partnerRole", "situation"],
    };

    public static ValidationResult Validate(JsonElement root, ActivityType activityType)
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

        if (practiceContent.ValueKind == JsonValueKind.Object
            && RequiredPracticeKeysByType.TryGetValue(activityType, out var requiredKeys))
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
