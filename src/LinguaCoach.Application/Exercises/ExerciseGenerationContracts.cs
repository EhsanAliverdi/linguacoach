namespace LinguaCoach.Application.Exercises;

// ── Phase H4 — "Generate Activity" from selected Resource Bank rows or an existing Lesson.
// Deterministic composition only in this phase: no AI provider call, matching Phase H3's Learn
// Item generation decision — no existing AI service in this codebase generates a scored practice
// exercise from source text, and adding one is out of scope for a foundation phase. Supported
// ActivityType values in H4: "gap_fill" and "multiple_choice_single" (Vocabulary/Grammar
// resources — deterministically scorable), "short_answer" (ReadingReference/ReadingPassage —
// open-ended, explicitly marked as requiring manual/AI evaluation, never a fake score). ──

public sealed record GenerateActivityFromResourcesRequest(
    IReadOnlyList<ExerciseResourceLinkInput> Resources,
    string? RequestedActivityType = null,
    string? Title = null,
    string? DefaultCefrLevel = null,
    string? DefaultSkill = null,
    string? DefaultSubskill = null,
    IReadOnlyList<string>? DefaultContextTags = null,
    IReadOnlyList<string>? DefaultFocusTags = null,
    int? DefaultDifficultyBand = null,
    string? Notes = null,
    Guid? CreatedByUserId = null,
    // Phase 1 (2026-07-15 pipeline safety audit) — null for a direct "generate from resources"
    // call with no Lesson context. Set when this request was synthesized from a Lesson's own
    // resource links (see GenerateActivitiesFromLessonRequest /
    // LessonExerciseBatchGenerationService.BuildResourcesRequestAsync) so the resulting Exercise
    // still retains Exercise.LessonId even though it was composed via the resources-based
    // (including AI) handler rather than IGenerateActivityFromLessonHandler.
    Guid? LessonId = null
);

public sealed record GenerateActivityFromLessonRequest(
    Guid LessonId,
    string? RequestedActivityType = null,
    string? Title = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateExerciseResult(ExerciseDto Activity, string ReviewRoute);

/// <summary>Generates a pending-review <see cref="ExerciseDto"/> draft directly from the
/// selected resources' own fields — no AI call (see this file's header comment). Distractors for
/// "multiple_choice_single" are pulled from sibling published resources of the same type already
/// in the bank; if none are available, the request is rejected with a suggestion to use
/// "gap_fill" instead, rather than generating a degenerate single-option multiple choice.</summary>
public interface IGenerateActivityFromResourcesHandler
{
    Task<GenerateExerciseResult> HandleAsync(GenerateActivityFromResourcesRequest request, CancellationToken ct = default);
}

/// <summary>Generates an Activity from an existing Lesson's own linked resources (reusing the
/// same resource snapshot each link already carries) and links the resulting Activity back to
/// that Lesson. The Lesson itself is never modified.</summary>
public interface IGenerateActivityFromLessonHandler
{
    Task<GenerateExerciseResult> HandleAsync(GenerateActivityFromLessonRequest request, CancellationToken ct = default);
}

/// <summary>
/// Phase J2b — AI-assisted alternative to <see cref="IGenerateActivityFromResourcesHandler"/>. Same
/// request/result shape; a deliberately separate action, not a replacement — the deterministic
/// handler stays untouched and available regardless of AI availability (2026-07-10/11 product
/// decision, same as Phase J2a's Lesson equivalent — see
/// docs/reviews/2026-07-11-phase-j2b-ai-exercise-generation-review.md). Narrower than a full "AI
/// writes the exercise" feature by deliberate design: AI only supplies framing content (a natural
/// gap-fill sentence for "gap_fill", plausible-but-wrong distractor definitions for
/// "multiple_choice_single", a tailored comprehension question for "short_answer") — the actual
/// correct answer, scoring rule, and answer key always stay deterministically derived from the
/// resource's own fields, never AI-supplied, matching the existing project precedent that AI must
/// never be trusted to decide "which option/value is correct" (see the 2026-07-08 ActivityTemplate
/// generation-instructions decision in docs/roadmap/road-map.md's Decision Log). Only the
/// "generate from resources" entry point has an AI variant this phase; "generate from Lesson"
/// remains deterministic-only, deferred to keep this pass small. On AI unavailability or
/// unparseable/unsafe output (after one retry — including a leak check on gap_fill sentences),
/// throws <see cref="ExerciseValidationException"/> rather than silently degrading.
/// </summary>
public interface IGenerateActivityFromResourcesWithAiHandler
{
    Task<GenerateExerciseResult> HandleAsync(GenerateActivityFromResourcesRequest request, CancellationToken ct = default);
}

// ── Phase K5 — "Generate Exercises from Lesson" with an admin-picked count per type (e.g. 5
// gap_fill + 5 multiple_choice_single = 10 Exercises), instead of always exactly one. Every
// Exercise created this way is auto-linked into a Module together with the source Lesson — see
// IModuleAutoLinkService in LinguaCoach.Application.Modules — closing the loop the product now
// wants: Resource Bank → Lesson (manual) → Exercises (manual, admin picks how many/which types) →
// Module (automatic, no separate "Generate Module" step). ──

/// <summary>Null <see cref="ActivityType"/> means "auto-pick" — the same per-resource-type default
/// (gap_fill/multiple_choice_single for Vocabulary/Grammar, short_answer for Reading) the
/// single-item generate endpoints already use when no type is requested.</summary>
public sealed record ActivityGenerationSpec(string? ActivityType, int Count);

public sealed record GenerateActivitiesFromLessonRequest(
    Guid LessonId,
    IReadOnlyList<ActivityGenerationSpec> Specs,
    string? TitlePrefix = null,
    string? Notes = null,
    Guid? CreatedByUserId = null
);

public sealed record GenerateActivitiesFromLessonResult(
    IReadOnlyList<ExerciseDto> Activities,
    Guid ModuleId,
    string ModuleReviewRoute
);

/// <summary>Generates N Exercises per requested type (reusing <see cref="IGenerateActivityFromLessonHandler"/>
/// once per item — same deterministic composer, same per-item validation, nothing duplicated),
/// then auto-creates-or-extends the Module linking this Lesson to every Exercise it has ever
/// generated (not just this call's batch) via <c>IModuleAutoLinkService</c>.</summary>
public interface IGenerateActivitiesFromLessonHandler
{
    Task<GenerateActivitiesFromLessonResult> HandleAsync(GenerateActivitiesFromLessonRequest request, CancellationToken ct = default);
}
