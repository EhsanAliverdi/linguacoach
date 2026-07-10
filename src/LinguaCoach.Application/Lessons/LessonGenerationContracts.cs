namespace LinguaCoach.Application.Lessons;

// ── Phase H3 — "Generate Learn" from selected Resource Bank rows. Deterministic draft
// composition only in this phase: the draft is built directly from the selected rows' own
// fields (word/definition, grammar point/description, reading excerpt/passage text), never
// guessed or invented. No AI call is made — see IGenerateLessonFromResourcesHandler's doc
// comment and docs/architecture/product-model-realignment-h0.md for why real AI generation is
// future work, not this phase's scope. ──

public sealed record GenerateLessonFromResourcesRequest(
    IReadOnlyList<LessonResourceLinkInput> Resources,
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

public sealed record GenerateLessonFromResourcesResult(LessonDto Lesson, string ReviewRoute);

/// <summary>
/// Generates a pending-review <see cref="LessonDto"/> draft from one or more selected published
/// Resource Bank rows. Phase H3 implements this as a deterministic composer (no AI provider call)
/// — there is no existing "generate explanatory teaching content from source text" AI service in
/// this codebase to safely reuse (every existing AI generation service is scoped to
/// activity/exercise/learning-path generation, not teaching prose), and adding a new AI feature
/// key is out of scope for a foundation phase. The resulting <see cref="LessonDto.GenerationProvider"/>
/// is honestly labeled "Deterministic", never a fake AI attribution.
/// </summary>
public interface IGenerateLessonFromResourcesHandler
{
    Task<GenerateLessonFromResourcesResult> HandleAsync(GenerateLessonFromResourcesRequest request, CancellationToken ct = default);
}

/// <summary>
/// Phase J2a — AI-assisted alternative to <see cref="IGenerateLessonFromResourcesHandler"/>. Same
/// request/result shape and the same selected-resources input; the difference is entirely in how
/// the teaching content (title/body/examples/commonMistakes/usageNotes) is produced — an AI
/// provider call instead of direct field copying. Deliberately a separate action, not a
/// replacement: the deterministic handler above is untouched and remains available regardless of
/// AI availability (2026-07-10 product decision — see
/// docs/reviews/2026-07-10-phase-j2a-ai-lesson-generation-review.md). On AI unavailability or
/// unparseable output (after one retry), this handler throws <see cref="LessonValidationException"/>
/// rather than silently falling back to a deterministic draft — the admin sees a clear error and
/// can use the existing deterministic action instead. Metadata fields (CEFR/skill/subskill/tags/
/// difficulty) stay deterministic from the selected resources/request, matching the deterministic
/// handler — only the teaching prose itself is AI-generated.
/// </summary>
public interface IGenerateLessonFromResourcesWithAiHandler
{
    Task<GenerateLessonFromResourcesResult> HandleAsync(GenerateLessonFromResourcesRequest request, CancellationToken ct = default);
}
