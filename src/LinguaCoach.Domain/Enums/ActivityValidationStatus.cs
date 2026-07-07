namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Automated-validation outcome for an AI-generated activity instance, recorded before any
/// admin review (AdminReviewStatus is a separate, human-approval gate). Set on
/// StudentActivityReadinessItem when an instance is generated from an ActivityTemplate
/// (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md, Phase 6).
/// Never reorder or insert — append only.
/// </summary>
public enum ActivityValidationStatus
{
    /// <summary>The generated instance passed automated validation (schema + template rules) on
    /// the first or retry attempt.</summary>
    Passed = 0,

    /// <summary>The generated instance failed automated validation even after retry.</summary>
    Failed = 1,

    /// <summary>The generated instance passed automated validation but is flagged for human
    /// review before being served (e.g. borderline content).</summary>
    NeedsReview = 2,
}
