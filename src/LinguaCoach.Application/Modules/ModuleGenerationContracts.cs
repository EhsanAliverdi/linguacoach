namespace LinguaCoach.Application.Modules;

// ── Phase H5 — "Generate Module" from existing Lessons and Exercises. Deterministic
// composition only — no AI provider call, same reasoning as H3/H4. Module generation composes
// EXISTING approved Lessons/Exercises; it never cascade-generates new ones. Every
// "find compatible" entry point (from-resource/from-lesson/from-exercise) only considers
// Approved sources — a draft/pending Lesson or Activity is never silently pulled into a
// generated Module. ──

public sealed record GenerateModuleFromItemsRequest(
    IReadOnlyList<ModuleLessonLinkInput> LessonLinks,
    IReadOnlyList<ModuleExerciseLinkInput> ExerciseLinks,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleFromResourceRequest(
    string ResourceType,
    Guid ResourceId,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleFromLessonRequest(
    Guid LessonId,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleFromExerciseRequest(
    Guid ExerciseId,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateModuleResult(ModuleDto Module, string ReviewRoute);

/// <summary>Composes a Module from explicitly-selected Lesson(s) + Exercise(s).
/// Every selected item must already be <c>Approved</c> — rejected with a clear message naming the
/// first non-approved item, rather than silently allowing a draft into a generated Module.</summary>
public interface IGenerateModuleFromItemsHandler
{
    Task<GenerateModuleResult> HandleAsync(GenerateModuleFromItemsRequest request, CancellationToken ct = default);
}

/// <summary>Finds an existing Approved Lesson and an existing Approved Exercise
/// both linked to the given published Resource Bank row, and composes a Module from them. Never
/// creates a Lesson or Exercise — rejected with a clear message when either is
/// missing.</summary>
public interface IGenerateModuleFromResourceHandler
{
    Task<GenerateModuleResult> HandleAsync(GenerateModuleFromResourceRequest request, CancellationToken ct = default);
}

/// <summary>Starts from an Approved Lesson and finds compatible (matching CEFR/skill where
/// set) Approved Exercises to compose a Module. Rejected when the Lesson itself
/// isn't approved yet, or when no compatible Approved Exercise exists.</summary>
public interface IGenerateModuleFromLessonHandler
{
    Task<GenerateModuleResult> HandleAsync(GenerateModuleFromLessonRequest request, CancellationToken ct = default);
}

/// <summary>Starts from an Approved Exercise and finds compatible (matching CEFR/skill
/// where set) Approved Lessons to compose a Module. Rejected when the Activity itself isn't
/// approved yet, or when no compatible Approved Lesson exists.</summary>
public interface IGenerateModuleFromExerciseHandler
{
    Task<GenerateModuleResult> HandleAsync(GenerateModuleFromExerciseRequest request, CancellationToken ct = default);
}

/// <summary>
/// Phase J2c — AI-assisted alternative to <see cref="IGenerateModuleFromResourceHandler"/>. Same
/// request/result shape; a deliberately separate action, not a replacement — the deterministic
/// handler stays untouched and available regardless of AI availability (same 2026-07-10/11
/// product decision as Phases J2a/J2b — see
/// docs/reviews/2026-07-11-phase-j2c-ai-module-generation-review.md). Still only combines
/// EXISTING Approved Lesson(s)/Exercise(s) found via the resource link — never cascade-generates
/// a new Lesson or Exercise, same hard invariant as the deterministic composer. AI supplies only
/// the module's own descriptive framing (title/description/feedback-plan copy) — there is no
/// answer key or scoring rule at the Module level, so this carries the same (low) risk profile as
/// Phase J2a's Lesson generation, not J2b's Exercise generation. Only the "generate from
/// resource" entry point has an AI variant this phase; "generate from items/Lesson/Exercise"
/// remain deterministic-only, deferred to keep this pass small, matching J2b's precedent.
/// </summary>
public interface IGenerateModuleFromResourceWithAiHandler
{
    Task<GenerateModuleResult> HandleAsync(GenerateModuleFromResourceRequest request, CancellationToken ct = default);
}
