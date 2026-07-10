---
status: current
lastUpdated: 2026-07-11 02:00
owner: engineering
supersedes:
supersededBy:
---

# Phase J2b — AI-Assisted Exercise Generation

**Date:** 2026-07-11
**Related sprint/feature:** Second pass of Phase J2 from `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md` (§D), following Phase J2a (Lesson).
**Files reviewed/changed:**
- `src/LinguaCoach.Application/Exercises/ExerciseGenerationContracts.cs`
- `src/LinguaCoach.Infrastructure/Exercises/AiExerciseGenerationService.cs` (new)
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Infrastructure/Ai/AiExecutionService.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Controllers/AdminExerciseController.cs`
- `src/LinguaCoach.Web/src/app/core/services/admin-exercise.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-unified/admin-resource-bank-unified.component.ts`
- `tests/LinguaCoach.UnitTests/Exercises/AiExerciseGenerationServiceTests.cs` (new)

## Problem and why this pass is narrower than J2a

Exercise generation is higher-stakes than Lesson generation: `Exercise` carries a backend-only
answer key and scoring rule that, if wrong or leaked, produces either an unfair/broken exercise or
a security issue (answer visible to the student). Unlike Lesson content (free-form teaching prose
with no correctness constraint), an AI-generated Exercise has a hard correctness requirement: the
scoring rule must always match reality.

**Design constraint adopted for this pass:** AI supplies only framing/wrapper content — never the
correct answer, never the scoring rule identity. Specifically:
- `gap_fill` — AI writes a natural sentence using the target term in context; the blank's answer is
  always the resource's own term (`primary.Title`), never AI-supplied.
- `multiple_choice_single` — AI writes plausible-but-wrong distractor options; the correct option's
  text is always the resource's own definition (`primary.Body`), verbatim, never AI-paraphrased.
- `short_answer` — AI writes a tailored comprehension question; the excerpt shown is always the
  resource's own body text. Already honestly marked `RequiresManualOrAiEvaluation`, so there is no
  scoring-integrity risk here regardless.

This mirrors an existing project precedent: the 2026-07-08 `ActivityTemplate` generation-
instructions decision (`docs/roadmap/road-map.md` Decision Log) explicitly forbade AI from
"renaming component keys or changing which option/value is correct" for the same reason.

This pass also only implements the AI variant for the "generate from resources" entry point —
`IGenerateActivityFromLessonHandler`'s deterministic-only "generate from Lesson" path is untouched
and has no AI variant yet, deferred to keep this pass small (same incremental-phase discipline as
J0/J1/J2a).

## What changed

**New interface** `IGenerateActivityFromResourcesWithAiHandler` — same request/result shape as the
existing deterministic `IGenerateActivityFromResourcesHandler`.

**New service** `AiExerciseGenerationService` — resolves resources the same way
`ActivityGenerationService` does (shared `LessonResourceLookup`), applies the same activity-type
support-matrix validation (definitional types → `gap_fill`/`multiple_choice_single`, reading types →
`short_answer`), then calls AI for framing content only. A **defensive answer-leak check** runs on
every `gap_fill` response: if the AI sentence doesn't contain the `___` blank marker, or contains
the answer term anywhere outside the marker, the response is treated as unparseable (triggers the
retry, then throws) — this is new safety logic with no equivalent in Lesson generation, since Lesson
has no secret to leak. For `multiple_choice_single`, distractors are filtered to exclude any that
exactly match the correct answer text (case-insensitive) before being used; if filtering leaves zero
usable distractors, that's also treated as unparseable.

Every generated Form.io schema still passes through the existing
`IFormIoSchemaValidationService.ValidateSchema` check, same as the deterministic composer — a second
defense layer on top of the leak check above, even though the schema-building code itself never
embeds AI-supplied text into an answer-bearing field.

**Prompt** (`exercise_generate_from_resources`, seeded via `DefaultAiSeeder`) is conditioned on the
requested activity type via a single templated prompt with per-type rules, explicit that
distractors must not be synonyms/paraphrases of the real definition, and that the gap-fill sentence
must not repeat the answer term outside the blank. Mapped to the existing `llm.generation` category,
same as J2a's Lesson prompt — no new admin AI-config screen needed.

**Endpoint** `POST api/admin/exercises/generate-from-resources/ai` — sibling to the existing
`POST api/admin/exercises/generate-from-resources`, same request body shape.

**Frontend**: a new "Generate Activity (AI)" row action on the Resource Bank page, next to the
existing "Generate Activity", with its own loading-state signal
(`generatingActivityAiId`), independent of both the deterministic action and the Lesson AI action
added in J2a.

## What was NOT changed

- `ActivityGenerationService` (the deterministic composer, both entry points) — completely
  untouched, still the default/fallback-safe path.
- `IGenerateActivityFromLessonHandler` — no AI variant this pass (deferred).
- No schema/migration change — reuses `Exercise`'s existing `GenerationProvider`/`GenerationModel`
  fields (present since Phase H4).
- No new admin AI-config UI — reuses the existing `llm.generation` category.
- `ExerciseLaunchEligibility` (which types are actually launchable at runtime) — unchanged; AI-
  generated Exercises follow the exact same launch-eligibility rules as deterministic ones (only
  `gap_fill`/`multiple_choice_single` are launchable, `short_answer` is not, regardless of how the
  draft was generated).

## Tests

Added `AiExerciseGenerationServiceTests.cs` (9 tests), reusing the same fake AI infrastructure as
J2a's Lesson tests:
- `gap_fill`: valid AI sentence creates an Exercise with the deterministic answer key intact.
- `gap_fill`: missing blank marker retries and succeeds on a valid second response.
- **`gap_fill`: an AI sentence that leaks the answer term outside the blank on both attempts throws
  `ExerciseValidationException` and creates no Exercise row** — the safety-critical test for this
  pass.
- `multiple_choice_single`: valid distractors create an Exercise with the deterministic correct
  answer intact, distractors present in the schema.
- **`multiple_choice_single`: distractors that all match the correct answer (filtered to zero
  usable) on both attempts throws, creates no Exercise row.**
- `multiple_choice_single`: a resource with no definition throws before any AI call is attempted
  (same fail-fast behavior as the deterministic composer).
- `short_answer`: valid AI question creates an Exercise still marked
  `RequiresManualOrAiEvaluation`.
- AI provider unavailable throws, creates no Exercise row.
- No resources throws before any AI call.

## Validation

- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline).
- `dotnet test --configuration Release` — 3,442/3,442 passing (5 architecture, 2,125 unit [+9 new],
  1,312 integration).
- `npm run build -- --configuration production` — no new TS/Angular compile errors; fails only the
  same pre-existing bundle-size budget, unrelated to this change.
- Frontend unit tests (Karma) not run — still blocked by pre-existing, unrelated `TODO-H8-2`.
- Playwright not run — same reasoning as J2a.

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`,
  `docs/reviews/2026-07-11-phase-j2a-ai-lesson-generation-review.md` (pattern source),
  `ActivityGenerationService.cs`, `Exercise.cs`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry).
- Docs intentionally not updated: `docs/architecture/product-model-realignment-h0.md` — same
  reasoning as J2a, this implements a slice of the existing target model.
- Reason: n/a.

## Risks or unresolved questions

- Same as J2a: no admin has configured a real (non-"fake") `llm.generation` provider in any
  environment this session touched, so this has only been exercised against the fake test provider.
- The gap-fill leak check is a simple case-insensitive substring match on the answer term. It would
  not catch a leak via an inflected form (e.g. term "resilient", sentence containing "resiliently")
  — judged acceptable for this pass since a partial-word leak still requires the student to infer
  the base form, unlike an exact-match leak which gives the answer away outright; a stemming-aware
  check would be a reasonable future hardening if this proves to be a real problem in practice.
- `multiple_choice_single`'s distractor-quality rule ("must not be synonyms of the correct answer")
  is prompt-level guidance only, not code-enforced beyond the exact-text-match filter — a
  near-synonym distractor could still slip through and make a question unfairly easy or ambiguous.
  This is a content-quality risk, not a correctness/security risk (the scoring key itself is never
  wrong), so it was judged acceptable to leave as an admin-review responsibility (every AI draft is
  still pending-review before publish).

## Final verdict

Closes the Exercise slice of the audit's "no AI anywhere in generation" gap, using a narrower and
more defensive design than Lesson generation given the real answer-key/scoring correctness stakes.
The correct answer and scoring rule are provably never AI-supplied for any activity type, verified
by dedicated tests. The deterministic path remains fully intact and available regardless of AI
status.

## Next recommended action

Proceed to Phase J2c (AI-assisted Module generation) to close the last slice of the audit's
AI-generation gap, or move to Phase J3 (admin preview-as-learner for Modules) if the user prefers to
close the UX gap next — both remain open per the original audit's phase ordering.
