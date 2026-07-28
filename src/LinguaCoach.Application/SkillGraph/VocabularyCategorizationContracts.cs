namespace LinguaCoach.Application.SkillGraph;

/// <summary>Full content reseed (2026-07-28) — AI-assisted classification for vocabulary words the
/// deterministic <see cref="IVocabularyImportService"/> pass couldn't assign a real topic category
/// to (CEFR-J leaves ~78% of rows uncategorized; Octanove C1/C2 has no category column at all).
/// Same bounded-call/retry-once/never-throws discipline as <c>SkillGraphDraftingService</c>. Per
/// the user's explicit decision, the AI is free to propose a brand-new category when nothing in
/// <see cref="VocabularyCategorizationRequest.ExistingCategories"/> fits well — this is advisory
/// only, every proposal lands <c>PendingReview</c>, an admin batch-approves after review (including
/// a near-duplicate-category cleanup pass reusing <c>GraphChangeSuggestionService</c>).</summary>
public interface IVocabularyCategorizationService
{
    Task<VocabularyCategorizationResult> CategorizeBatchAsync(
        VocabularyCategorizationRequest request, CancellationToken ct = default);
}

/// <summary><c>Words</c> should be a small bounded batch (recommended ~50) — one AI call per
/// batch, per AGENTS.md's bounded-call discipline. <c>ExistingCategories</c> is the current full
/// category list (deterministic ones from Phase 2a plus any already AI-proposed in earlier
/// batches this same run) so later batches prefer reusing an already-created category over
/// proposing a near-duplicate.</summary>
public sealed record VocabularyCategorizationRequest(
    IReadOnlyList<VocabularyWordRow> Words,
    IReadOnlyList<string> ExistingCategories);

public sealed record VocabularyCategorizationResult(
    bool Success,
    IReadOnlyList<VocabularyWordCategorization> Words,
    string? ErrorMessage);

/// <summary>One word's AI-proposed classification. <c>Headword</c>/<c>PartOfSpeech</c> are echoed
/// back and validated against the original request batch — a categorization for a word never sent
/// to the AI is dropped, never trusted. <c>Category</c> may be an existing one or a genuinely new
/// proposal (never validated against a closed list, per the user's decision) — near-duplicate
/// cleanup happens as a separate later step, not here.</summary>
public sealed record VocabularyWordCategorization(
    string Headword,
    string PartOfSpeech,
    string Category,
    string Description,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags);
