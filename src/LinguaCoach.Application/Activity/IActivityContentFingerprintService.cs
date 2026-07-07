namespace LinguaCoach.Application.Activity;

/// <summary>
/// Known content shapes an activity's JSON payload can be in. See
/// docs/architecture/repetition-and-novelty.md for the full list of shapes this fingerprint
/// service understands and their limitations.
/// </summary>
public enum ActivityContentShape
{
    /// <summary>Legacy <c>LearningActivity.AiGeneratedContentJson</c> — module_stage_v1 /
    /// legacy_adapted_v1 (see <c>ModuleStageSchema</c>).</summary>
    ModuleStageSchema,

    /// <summary>Form.io component-tree JSON — <c>LearningActivity.FormIoSchemaJson</c> or
    /// <c>ActivityTemplate.FormIoBaseSchemaJson</c>.</summary>
    FormIoSchema,

    /// <summary>Content shape not recognized — falls back to a plain normalized-JSON hash with
    /// no shape-specific field extraction.</summary>
    Unknown
}

/// <summary>
/// Request to compute a deterministic content fingerprint for a piece of activity content.
///
/// IMPORTANT: <paramref name="ContentJson"/> must be content the STUDENT WILL SEE (or the
/// bank/AI-generated source of it) — never <c>ActivityAttempt.SubmittedAnswerJson</c> or any
/// other student-authored submission data. Callers are responsible for this; the service does
/// not attempt to detect and strip submission data.
/// </summary>
public sealed record ActivityContentFingerprintRequest(
    string? ContentJson,
    ActivityContentShape ContentShape,
    string? PatternKey = null,
    string? Skill = null,
    string? Subskill = null,
    string? CefrLevel = null,
    string? TopicKey = null,
    string? ScenarioKey = null,
    string? PassageKey = null,
    string? PromptKey = null);

/// <summary>
/// Computes a deterministic content fingerprint from actual activity content, for use by the
/// repetition/novelty foundation (Phase B — see docs/architecture/repetition-and-novelty.md).
/// Same content-defining input always produces the same fingerprint; meaningfully different
/// content produces a different one. This is NOT semantic/embedding-based near-duplicate
/// detection — it is exact-match (post-normalization) only.
/// </summary>
public interface IActivityContentFingerprintService
{
    string ComputeFingerprint(ActivityContentFingerprintRequest request);
}
