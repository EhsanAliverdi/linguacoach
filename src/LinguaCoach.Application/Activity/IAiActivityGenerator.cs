using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

/// <summary>
/// Evaluates student attempts using AI. Infrastructure implements this. Application defines the
/// contract. Throws on AI call failure — callers must catch and return a graceful error response.
/// Phase I2C: narrowed from IAiActivityGenerator's original generate+evaluate contract —
/// GenerateActivityContentAsync (and ActivityGenerationContext) were removed once the last
/// callers (the readiness-pool/legacy-generation pipelines deleted in Passes A/B/C) were gone.
/// The interface/class name is a mild misnomer now (it only evaluates), left unchanged rather
/// than churn every reference for a single remaining method — see
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public interface IAiActivityGenerator
{
    /// <summary>
    /// Evaluates a student's submission and returns structured feedback JSON.
    /// Returns the JSON string to be stored in ActivityAttempt.FeedbackJson.
    /// Throws if AI call fails — callers must catch and return a graceful error response.
    /// </summary>
    Task<string> EvaluateAttemptAsync(
        ActivityEvaluationContext context,
        CancellationToken ct = default);
}

public sealed record ActivityEvaluationContext(
    ActivityType ActivityType,
    string ActivityContentJson,
    string StudentSubmission,
    string CefrLevel,
    string CareerContext,
    string SourceLanguageName,
    string TargetLanguageName,
    /// <summary>
    /// Compact, bounded learner preference context — same as generation context.
    /// Allows evaluation prompts to match feedback tone/difficulty to student preference.
    /// </summary>
    string? LearnerPreferenceContext = null,
    /// <summary>
    /// Resolved learning goal context label for evaluation prompt framing.
    /// </summary>
    string? LearningGoalContext = null);
