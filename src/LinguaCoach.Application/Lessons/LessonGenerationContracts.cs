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
