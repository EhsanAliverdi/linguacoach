using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

/// <summary>
/// Generates activity content and evaluates student attempts using AI.
/// Infrastructure implements this. Application defines the contract.
/// If AI generation fails, the caller falls back to a SystemFallback activity — never throws to the controller.
/// </summary>
public interface IAiActivityGenerator
{
    /// <summary>
    /// Generates the JSONB content payload for a new activity of the given type.
    /// Returns the JSON string to be stored in LearningActivity.AiGeneratedContentJson.
    /// Throws if AI call fails — callers must catch and fall back to SystemFallback.
    /// </summary>
    Task<string> GenerateActivityContentAsync(
        ActivityGenerationContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates a student's submission and returns structured feedback JSON.
    /// Returns the JSON string to be stored in ActivityAttempt.FeedbackJson.
    /// Throws if AI call fails — callers must catch and return a graceful error response.
    /// </summary>
    Task<string> EvaluateAttemptAsync(
        ActivityEvaluationContext context,
        CancellationToken ct = default);
}

public sealed record ActivityGenerationContext(
    ActivityType ActivityType,
    string CefrLevel,
    string CareerContext,
    string LanguagePairCode,
    string SourceLanguageName,
    string TargetLanguageName,
    string? RecentMistakesSummary = null,
    string? TopicHint = null,
    /// <summary>
    /// When set, overrides the broad ActivityType-based prompt key with a pattern-specific one.
    /// AiGeneratePromptKey from the ExercisePatternDefinition.
    /// </summary>
    string? OverridePromptKey = null,
    /// <summary>
    /// The canonical exercise pattern key (e.g. "email_reply"). Stored on LearningActivity.
    /// </summary>
    string? ExercisePatternKey = null,
    /// <summary>
    /// Compact, bounded learner preference context for AI generation.
    /// </summary>
    string? LearnerPreferenceContext = null,
    /// <summary>
    /// Compact goal label for ledger and adaptive selection.
    /// </summary>
    string? LearningGoalContext = null);

public sealed record ActivityEvaluationContext(
    ActivityType ActivityType,
    string ActivityContentJson,
    string StudentSubmission,
    string CefrLevel,
    string CareerContext,
    string SourceLanguageName,
    string TargetLanguageName);
