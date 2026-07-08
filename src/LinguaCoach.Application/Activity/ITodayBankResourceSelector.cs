namespace LinguaCoach.Application.Activity;

/// <summary>
/// Phase D1/D2 — bank-first slice for Today lesson generation. Selects a small, balanced set of
/// published Resource Bank entries (vocabulary/grammar/reading — see
/// docs/architecture/resource-bank.md) as supporting material for an AI-generated Today activity,
/// for the subset of patterns this phase supports. Never replaces AI generation — the selected
/// resources are only ever appended as extra prompt guidance (see
/// TodayBankSelectionResult.PromptSupplementText); the caller always still calls
/// IAiActivityGenerator afterwards. See ActivityMaterializationJob.MaterializeExerciseAsync.
/// </summary>
public interface ITodayBankResourceSelector
{
    Task<TodayBankSelectionResult> SelectAsync(TodayBankSelectionRequest request, CancellationToken ct = default);
}

public sealed record TodayBankSelectionRequest(
    Guid StudentProfileId,
    string CefrLevel,
    /// <summary>pattern.PrimarySkill, e.g. "Vocabulary" or "Reading".</summary>
    string PatternPrimarySkill,
    /// <summary>
    /// Parsed pattern.SecondarySkillsJson — used only to opportunistically pull in grammar bank
    /// content for gap_fill_workplace_phrase. Never used to gate a whole pattern.
    /// </summary>
    IReadOnlyList<string> PatternSecondarySkills,
    int MaxResources = 4,
    /// <summary>
    /// Phase D2 — when true (routing reason is Review/Scaffold/Remediation), the selector may
    /// widen its CEFR search to the next level down if the exact level has no bank rows. Never
    /// widens upward, and never widens at all for ordinary (non-review) generation — a student
    /// must never be silently served harder content than their routed level.
    /// </summary>
    bool AllowLowerLevelReview = false,
    /// <summary>
    /// Phase D3 — the specific ExercisePatternKey (e.g. "reading_multiple_choice_single"). Used
    /// only to decide, for Reading-primary patterns, whether a full <c>CefrReadingPassage</c> is a
    /// suitable anchor (comprehension/reorder patterns) or whether a short
    /// <c>CefrReadingReference</c> is the better fit (cloze/fill-in-blanks patterns). Null/empty
    /// falls back to the D2 short-reference behavior — full passages are strictly opt-in per pattern.
    /// </summary>
    string? PatternKey = null);

public enum TodayBankSelectionOutcome
{
    BankResourcesFound,
    PartialResourcesFound,
    NoSuitableResources,
    SkippedUnsupportedPattern,
    BlockedByNovelty
}

/// <summary>
/// Phase D2/D3 — one bank resource offered to the AI prompt, with enough metadata to reconstruct
/// full provenance later (see LearningActivity.BankResourceProvenanceJson) without re-querying
/// the bank. ResourceType is "Vocabulary"|"Grammar"|"Reading"|"ReadingPassage". The trailing
/// passage-specific fields (<see cref="CefrLevel"/>/<see cref="Title"/>/<see cref="PassageText"/>/
/// <see cref="WordCount"/>/<see cref="EstimatedReadingMinutes"/>) are populated only for
/// "ReadingPassage" resources (Phase D3, full <c>CefrReadingPassage</c> anchors) and stay null for
/// the compact short-resource types.
/// </summary>
public sealed record TodayBankSelectedResource(
    Guid Id,
    string ResourceType,
    string DisplayText,
    Guid SourceId,
    /// <summary>Deterministic synthetic fingerprint used for both the novelty precheck and
    /// durable provenance — e.g. "bank-vocab-precheck:{id}". Not a content-hash of AI output.</summary>
    string ContentFingerprint,
    /// <summary>Short human-readable reason this resource was selected, e.g. "exact CEFR match"
    /// or "review/lower-level match (B1, routing reason Scaffold)".</summary>
    string SelectionReason,
    /// <summary>Phase D3 — the resource's own CEFR level (full passages only; null otherwise).</summary>
    string? CefrLevel = null,
    /// <summary>Phase D3 — full-passage title (full passages only; null otherwise).</summary>
    string? Title = null,
    /// <summary>Phase D3 — the full passage text used as the AI reading anchor (full passages only;
    /// null otherwise). For short resources the anchor text lives in <see cref="DisplayText"/>.</summary>
    string? PassageText = null,
    /// <summary>Phase D3 — passage word count (full passages only; null otherwise).</summary>
    int? WordCount = null,
    /// <summary>Phase D3 — estimated reading time in minutes (full passages only; null otherwise).</summary>
    int? EstimatedReadingMinutes = null);

public sealed record TodayBankSelectionResult(
    TodayBankSelectionOutcome Outcome,
    IReadOnlyList<TodayBankSelectedResource> Resources,
    /// <summary>
    /// Ready-to-append structured prompt block for TopicHint — resource type, content, CEFR,
    /// source, and explicit anchor/constraint instructions. Null when Resources is empty.
    /// </summary>
    string? PromptSupplementText)
{
    public static readonly TodayBankSelectionResult SkippedUnsupported =
        new(TodayBankSelectionOutcome.SkippedUnsupportedPattern, Array.Empty<TodayBankSelectedResource>(), null);

    public static readonly TodayBankSelectionResult NoResources =
        new(TodayBankSelectionOutcome.NoSuitableResources, Array.Empty<TodayBankSelectedResource>(), null);
}
