namespace LinguaCoach.Application.ActivityDefinitions;

// ── Phase H4 — "Generate Activity" from selected Resource Bank rows or an existing Learn Item.
// Deterministic composition only in this phase: no AI provider call, matching Phase H3's Learn
// Item generation decision — no existing AI service in this codebase generates a scored practice
// exercise from source text, and adding one is out of scope for a foundation phase. Supported
// ActivityType values in H4: "gap_fill" and "multiple_choice_single" (Vocabulary/Grammar
// resources — deterministically scorable), "short_answer" (ReadingReference/ReadingPassage —
// open-ended, explicitly marked as requiring manual/AI evaluation, never a fake score). ──

public sealed record GenerateActivityFromResourcesRequest(
    IReadOnlyList<ActivityResourceLinkInput> Resources,
    string? RequestedActivityType = null,
    string? Title = null,
    string? DefaultCefrLevel = null,
    string? DefaultSkill = null,
    string? DefaultSubskill = null,
    IReadOnlyList<string>? DefaultContextTags = null,
    IReadOnlyList<string>? DefaultFocusTags = null,
    int? DefaultDifficultyBand = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateActivityFromLearnItemRequest(
    Guid LearnItemId,
    string? RequestedActivityType = null,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateActivityDefinitionResult(ActivityDefinitionDto Activity, string ReviewRoute);

/// <summary>Generates a pending-review <see cref="ActivityDefinitionDto"/> draft directly from the
/// selected resources' own fields — no AI call (see this file's header comment). Distractors for
/// "multiple_choice_single" are pulled from sibling published resources of the same type already
/// in the bank; if none are available, the request is rejected with a suggestion to use
/// "gap_fill" instead, rather than generating a degenerate single-option multiple choice.</summary>
public interface IGenerateActivityFromResourcesHandler
{
    Task<GenerateActivityDefinitionResult> HandleAsync(GenerateActivityFromResourcesRequest request, CancellationToken ct = default);
}

/// <summary>Generates an Activity from an existing Learn Item's own linked resources (reusing the
/// same resource snapshot each link already carries) and links the resulting Activity back to
/// that Learn Item. The Learn Item itself is never modified.</summary>
public interface IGenerateActivityFromLearnItemHandler
{
    Task<GenerateActivityDefinitionResult> HandleAsync(GenerateActivityFromLearnItemRequest request, CancellationToken ct = default);
}
