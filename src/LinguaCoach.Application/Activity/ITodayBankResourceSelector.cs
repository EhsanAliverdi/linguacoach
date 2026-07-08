namespace LinguaCoach.Application.Activity;

/// <summary>
/// Phase D1 — first, narrow bank-first slice for Today lesson generation. Selects a small set of
/// published, approved Resource Bank entries (vocabulary/grammar/reading — see
/// docs/architecture/resource-bank.md) as supporting material for an AI-generated Today activity,
/// for the small subset of patterns this phase supports. Never replaces AI generation — the
/// selected resources are only ever appended as extra prompt guidance (see
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
    int MaxResources = 3);

public enum TodayBankSelectionOutcome
{
    BankResourcesFound,
    PartialResourcesFound,
    NoSuitableResources,
    SkippedUnsupportedPattern,
    BlockedByNovelty
}

/// <summary>ResourceType is "Vocabulary"|"Grammar"|"Reading".</summary>
public sealed record TodayBankSelectedResource(Guid Id, string ResourceType, string DisplayText);

public sealed record TodayBankSelectionResult(
    TodayBankSelectionOutcome Outcome,
    IReadOnlyList<TodayBankSelectedResource> Resources,
    /// <summary>Ready-to-append free text for topicHint. Null when Resources is empty.</summary>
    string? PromptSupplementText)
{
    public static readonly TodayBankSelectionResult SkippedUnsupported =
        new(TodayBankSelectionOutcome.SkippedUnsupportedPattern, Array.Empty<TodayBankSelectedResource>(), null);

    public static readonly TodayBankSelectionResult NoResources =
        new(TodayBankSelectionOutcome.NoSuitableResources, Array.Empty<TodayBankSelectedResource>(), null);
}
