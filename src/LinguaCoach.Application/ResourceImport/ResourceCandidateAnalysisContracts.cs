namespace LinguaCoach.Application.ResourceImport;

// ── Phase E2 — AI analysis (advisory), deterministic rule validation, and dedup/fingerprint
// gates for staged ResourceCandidate rows. Still staging only — no publish/approve workflow
// (Phase E4) and no writes to any published Cefr* bank table happen anywhere in E2. ──

/// <summary>
/// The AI's advisory classification output for a single candidate. Every field is optional/
/// nullable because the AI response is untrusted input — the analysis service must defensively
/// drop anything malformed rather than throw. This is stored verbatim (as the raw cleaned JSON)
/// in <c>ResourceCandidate.AiAnalysisJson</c> and the parsed fields are copied onto the
/// candidate's own classification columns via <c>ResourceCandidate.ApplyAnalysis</c>. The AI
/// never decides <c>ValidationStatus</c> — that is exclusively <see cref="IResourceCandidateValidationService"/>'s
/// job, run separately (see Part 4/5 of the Phase E2 spec).
/// </summary>
public sealed record ResourceCandidateAnalysisOutput(
    string? CefrLevel,
    double? CefrConfidence,
    string? PrimarySkill,
    string? Subskill,
    int? DifficultyBand,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    IReadOnlyList<string> GrammarTags,
    IReadOnlyList<string> VocabularyTags,
    IReadOnlyList<string> PronunciationTags,
    IReadOnlyList<string> ActivitySuitabilityTags,
    IReadOnlyList<string> SafetyTags,
    double? QualityScore,
    bool NeedsHumanReview,
    IReadOnlyList<string> QualityIssues,
    IReadOnlyList<string> SuggestedActivityUses,
    string? SearchText);

/// <summary>
/// Outcome of one candidate analysis attempt. <see cref="Success"/> is false whenever the AI
/// provider is unavailable/errors, or both the first attempt and the one retry produce JSON the
/// service cannot parse into anything usable — in all of those cases the candidate's existing
/// data is left completely intact and (per Phase E2 spec point 6) is marked re-analyzable rather
/// than the exception propagating to the caller.
/// </summary>
public sealed record ResourceCandidateAnalysisResult(
    Guid CandidateId,
    bool Success,
    string? ErrorMessage,
    ResourceCandidateAnalysisOutput? Output,
    string? ProviderName,
    string? ModelName,
    /// <summary>Phase 4.4D — true when the projected cost of this specific AI operation would
    /// reach or exceed the plan's approved ceiling; the provider was deliberately NOT called.
    /// Distinct from a normal analysis failure (<see cref="Success"/> false for other reasons) —
    /// callers must stop attempting further candidates in the same run when this is true rather
    /// than treating it as a per-candidate error to skip past.</summary>
    bool CeilingReached = false,
    string? PauseReason = null);

public interface IResourceCandidateAnalysisService
{
    /// <summary>
    /// Analyzes one candidate via AI and stores the (advisory) result on it. Idempotent/
    /// update-safe — re-running overwrites the prior AiAnalysisJson/fields, never duplicates
    /// anything. Never throws for AI-availability/format reasons — see
    /// <see cref="ResourceCandidateAnalysisResult"/>.
    /// </summary>
    Task<ResourceCandidateAnalysisResult> AnalyzeAsync(Guid candidateId, CancellationToken ct = default);
}

/// <summary>Bounded-batch analysis of all not-yet-analyzed candidates for one import run.</summary>
public sealed record ResourceCandidateBatchAnalysisResult(
    int CandidatesConsidered,
    int CandidatesAnalyzed,
    int SucceededCount,
    int FailedCount,
    bool BatchLimitReached,
    /// <summary>Phase 4.4D — true when the approved cost ceiling was reached mid-batch; the
    /// remaining not-yet-processed candidates in this run were left untouched (still eligible for
    /// analysis once the ceiling is amended and processing resumes).</summary>
    bool CeilingReached = false,
    string? PauseReason = null);

public interface IResourceCandidateBatchAnalysisService
{
    Task<ResourceCandidateBatchAnalysisResult> AnalyzePendingForRunAsync(
        Guid resourceImportRunId, CancellationToken ct = default);
}

/// <summary>Deterministic rule-validation outcome for one candidate (Part 4/5 of the Phase E2 spec).</summary>
public sealed record ResourceCandidateValidationResult(
    Guid CandidateId,
    string Status,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    bool NeedsHumanReview);

public interface IResourceCandidateValidationService
{
    /// <summary>
    /// Re-validates a candidate's current field values (which may include AI-suggested
    /// classification from <see cref="IResourceCandidateAnalysisService"/>) against every
    /// deterministic gate: English-only, CEFR validity/confidence, skill/subskill taxonomy,
    /// candidate-type, text bounds, safety signals, source license/approval (re-checked live,
    /// not just at original import time), Form.io schema safety (for ActivityTemplateCandidate
    /// rows), attribution, and exact-fingerprint dedup. Persists the outcome onto the candidate
    /// via <c>ResourceCandidate.ApplyValidation</c>.
    /// </summary>
    Task<ResourceCandidateValidationResult> ValidateAsync(Guid candidateId, CancellationToken ct = default);
}
