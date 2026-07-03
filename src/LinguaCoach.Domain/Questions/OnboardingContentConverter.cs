using System.Text.Json;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Questions;

/// <summary>
/// Converts the legacy OnboardingStepDefinition fields (OptionsJson/ValidationMetadataJson/
/// AssessmentMetadataJson, keyed off StepType) into the shared QuestionContent schema (Unified
/// Question-Schema Phase 5). Only the generic step types map onto QuestionContent — the
/// semantically-named one-off types (SupportLanguage, LearningGoals, WorkExperience, etc.) and
/// non-question steps (Welcome, Summary) keep their own dedicated orchestration and have no
/// Content (this converter returns null for them).
/// </summary>
public static class OnboardingContentConverter
{
    public static QuestionContent? FromLegacyStep(
        OnboardingStepTypeV2 stepType, string questionText, string? optionsJson,
        string? validationMetadataJson, string? assessmentMetadataJson)
    {
        return stepType switch
        {
            OnboardingStepTypeV2.SingleChoice => new SingleChoiceQuestion
            {
                QuestionText = questionText,
                Choices = ParseChoices(optionsJson),
            },
            OnboardingStepTypeV2.MultipleChoice => new MultipleChoiceQuestion
            {
                QuestionText = questionText,
                Choices = ParseChoices(optionsJson),
            },
            OnboardingStepTypeV2.FreeText => new FreeTextQuestion
            {
                QuestionText = questionText,
                MaxLength = ParseIntMetadata(validationMetadataJson, "maxLength"),
            },
            OnboardingStepTypeV2.AssessmentQuestion => new SingleChoiceQuestion
            {
                QuestionText = questionText,
                Choices = ParseChoices(optionsJson),
                CorrectAnswerKey = ParseStringMetadata(assessmentMetadataJson, "correctAnswerKey"),
            },
            _ => null,
        };
    }

    private static List<ChoiceOption> ParseChoices(string? optionsJson)
    {
        if (optionsJson is null) return [];
        var options = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(optionsJson);
        return options?.Select(o => new ChoiceOption { Key = o["key"], Label = o["label"] }).ToList() ?? [];
    }

    private static int? ParseIntMetadata(string? metadataJson, string propertyName)
    {
        if (metadataJson is null) return null;
        var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson);
        return meta is not null && meta.TryGetValue(propertyName, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : null;
    }

    private static string? ParseStringMetadata(string? metadataJson, string propertyName)
    {
        if (metadataJson is null) return null;
        var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson);
        return meta is not null && meta.TryGetValue(propertyName, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
    }
}
