---
status: current
lastUpdated: 2026-07-11 03:15
owner: engineering
supersedes:
supersededBy:
---

# Phase J2c — AI-Assisted Module Generation

**Date:** 2026-07-11
**Related sprint/feature:** Third and final pass of Phase J2 from
`docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md` (§D), following Phase
J2a (Lesson) and Phase J2b (Exercise). This closes the entire Phase J2 AI-generation gap.
**Files reviewed/changed:**
- `src/LinguaCoach.Application/Modules/ModuleGenerationContracts.cs`
- `src/LinguaCoach.Infrastructure/Modules/AiModuleGenerationService.cs` (new)
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Infrastructure/Ai/AiExecutionService.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Controllers/AdminModuleController.cs`
- `src/LinguaCoach.Web/src/app/core/services/admin-module.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-unified/admin-resource-bank-unified.component.ts`
- `tests/LinguaCoach.UnitTests/Modules/AiModuleGenerationServiceTests.cs` (new)

## Problem and risk profile

Module is the top of the content hierarchy — it composes existing, already-approved Lessons and
Exercises plus a module-level feedback plan. Unlike Exercise generation, there is **no answer key or
scoring rule at the Module level** (`Module.FeedbackPlanJson` is descriptive coaching copy — a
completion message and evaluation-criteria labels, not a scoring mechanism), so this pass carries
the same low risk profile as Phase J2a's Lesson generation, not J2b's Exercise generation. The one
hard invariant that must be preserved regardless of generation method: **AI must never
cascade-generate a new Lesson or Exercise** — it only composes framing text around Lesson(s)/
Exercise(s) that already exist and are already `Approved`, exactly like the deterministic composer.

## What changed

**New interface** `IGenerateModuleFromResourceWithAiHandler` — same request/result shape as
`IGenerateModuleFromResourceHandler`. Only the "generate from resource" entry point has an AI
variant this pass; "generate from items/Lesson/Exercise" remain deterministic-only, deferred to
keep this pass small, matching J2b's precedent of scoping to one entry point.

**New service** `AiModuleGenerationService` — finds the same existing Approved Lesson and Approved
Exercise linked to the given resource that the deterministic `ModuleGenerationService` would find
(identical lookup query), then asks AI to write the module's own title, description, and
feedback-plan copy (`completionMessage`, `evaluationCriteria`, `feedbackFocus`) referencing what the
selected Lesson and Exercise actually contain. The prompt explicitly instructs the AI that it is
writing framing/coaching copy around existing content, not inventing new teaching content or a new
practice task.

**Prompt** (`module_generate_from_resource`, seeded via `DefaultAiSeeder`) receives the full Lesson
title/body and Exercise title/instructions/type as context, so the generated description and
feedback plan are genuinely specific to the module's actual content rather than generic filler.
Mapped to the existing `llm.generation` category, same as J2a/J2b — no new admin AI-config screen.

**Endpoint** `POST api/admin/modules/generate-from-resource/ai` — sibling to the existing
`POST api/admin/modules/generate-from-resource`.

**Frontend**: a new "Generate Module (AI)" row action on the Resource Bank page, with its own
loading-state signal (`generatingModuleAiId`), independent of the deterministic action and the
Lesson/Exercise AI actions added in J2a/J2b.

## What was NOT changed

- `ModuleGenerationService` (the deterministic composer, all four entry points) — completely
  untouched.
- `IGenerateModuleFromItemsHandler`/`IGenerateModuleFromLessonHandler`/
  `IGenerateModuleFromExerciseHandler` — no AI variants this pass.
- No schema/migration change — reuses `Module`'s existing `GenerationProvider`/`GenerationModel`
  fields (present since Phase H5).
- No new admin AI-config UI.
- Today Plan / Practice Gym module-selection logic — unchanged; AI-generated Modules go through
  the exact same `PendingReview` → `Approved` gate as deterministic ones before either runtime
  surface can ever select them.

## Tests

Added `AiModuleGenerationServiceTests.cs` (6 tests), reusing the same fake AI infrastructure as
J2a/J2b:
- Valid AI response creates a pending-review Module linking the same Lesson and Exercise the
  deterministic composer would have found, with real AI provider/model attribution and the
  AI-written feedback plan.
- Bad JSON retries once and succeeds on a valid second response.
- A response missing `feedbackPlan.completionMessage` is treated as unparseable and throws after
  retry, no Module row created.
- No approved Lesson linked to the resource throws before any AI call (same fail-fast behavior as
  the deterministic composer).
- No approved Exercise linked to the resource throws before any AI call.
- AI provider unavailable throws, no Module row created.

## Validation

- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline).
- `dotnet test --configuration Release` — 3,448/3,448 passing (5 architecture, 2,131 unit [+6 new],
  1,312 integration).
- `npm run build -- --configuration production` — no new TS/Angular compile errors; fails only the
  same pre-existing bundle-size budget, unrelated to this change.
- Frontend unit tests (Karma) not run — still blocked by pre-existing, unrelated `TODO-H8-2`.
- Playwright not run — same reasoning as J2a/J2b.

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`,
  `docs/reviews/2026-07-11-phase-j2a-ai-lesson-generation-review.md`,
  `docs/reviews/2026-07-11-phase-j2b-ai-exercise-generation-review.md`, `ModuleGenerationService.cs`,
  `Module.cs`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry).
- Docs intentionally not updated: `docs/architecture/product-model-realignment-h0.md` — same
  reasoning as J2a/J2b.
- Reason: n/a.

## Risks or unresolved questions

- Same as J2a/J2b: no admin has configured a real (non-"fake") `llm.generation` provider in any
  environment this session touched — only exercised against the fake test provider.
- `IGenerateModuleFromItemsHandler`/Lesson/Exercise entry points have no AI variant — an admin
  composing a Module from an explicit multi-Lesson/multi-Exercise selection (rather than the
  single-resource shortcut) still only gets the deterministic feedback-plan copy. A reasonable
  future increment if that entry point turns out to be commonly used, but out of scope here.

## Final verdict

Closes the Module slice of the audit's "no AI anywhere in generation" gap — and with it, the entire
Phase J2 (AI-assisted Lesson/Exercise/Module generation). All three content types can now be
generated with genuine AI assistance while every deterministic path remains fully intact and
available, and every safety-critical invariant (no AI-cascaded content creation at the Module level,
no AI-supplied correct answers at the Exercise level) is preserved and test-verified.

## Next recommended action

Per the original audit's phase ordering (`docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`
§D), the remaining open phases are J3 (admin preview-as-learner for Modules — the audit's other
Critical product gap alongside AI generation), J4 (`short_answer` runtime support or explicit UI
gating), and J5 (import content-type expansion). J3 is the more product-critical of the two
remaining Critical gaps, since it's named directly in the vision as a precondition for Module
approval.
