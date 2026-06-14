using System.Text.Json;

namespace LinguaCoach.Application.Activity;

public static class ModuleStageSchema
{
    public const string Version = "module_stage_v1";
    public const string LegacyAdaptedVersion = "legacy_adapted_v1";
}

public sealed record LearnExampleDto(string Phrase, string Meaning, string? Note);

public sealed record LearnContentDto(
    string TeachingTitle,
    string Explanation,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<LearnExampleDto> Examples,
    string? Strategy,
    IReadOnlyList<string> CommonMistakes,
    string? SourceLanguageSupport);

public sealed record PracticeContentDto(
    string Instructions,
    string? Scenario,
    string? Task,
    JsonElement ExerciseData);

public sealed record FeedbackRubricItemDto(string Criterion, string Description, double Weight);

public sealed record FeedbackPlanDto(
    IReadOnlyList<string> EvaluationCriteria,
    IReadOnlyList<FeedbackRubricItemDto> Rubric,
    string? FeedbackFocus,
    IReadOnlyList<string> SuccessCriteria);

public sealed record StageContentDto(
    string SchemaVersion,
    string? PrimarySkill,
    IReadOnlyList<string> SecondarySkills,
    string? ExerciseType,
    LearnContentDto Learn,
    PracticeContentDto Practice,
    FeedbackPlanDto FeedbackPlan);

/// <summary>Wire shape of a module_stage_v1 JSON document — property names match the AI-generated JSON.</summary>
public sealed record ModuleStageWireDto(
    string SchemaVersion,
    string? PrimarySkill,
    IReadOnlyList<string>? SecondarySkills,
    string? ExerciseType,
    LearnContentDto LearnContent,
    PracticeContentDto PracticeContent,
    FeedbackPlanDto FeedbackPlan);
