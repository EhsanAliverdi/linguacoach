---
status: current
lastUpdated: 2026-07-11 05:00
owner: engineering
supersedes:
supersededBy:
---

# Phase J3 — Admin "Preview as Learner" for Modules

**Date:** 2026-07-11
**Related sprint/feature:** From `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`
(§D) — the second of two remaining Critical product gaps identified by the audit, after Phase J2
(AI-assisted generation) closed the first.
**Files reviewed/changed:**
- `src/LinguaCoach.Application/Modules/ModulePreviewContracts.cs` (new)
- `src/LinguaCoach.Infrastructure/Modules/AdminModulePreviewService.cs` (new)
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Controllers/AdminModuleController.cs`
- `src/LinguaCoach.Web/src/app/core/models/admin-module.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin-module.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-modules/admin-modules.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-modules/admin-modules.component.html`
- `tests/LinguaCoach.UnitTests/Modules/AdminModulePreviewServiceTests.cs` (new)

## Problem

The audit found no way for an admin to complete a Module's Exercise and see scored Feedback before
approving it — the only runtime path that renders and scores an Exercise
(`IExerciseLaunchService`) requires an already-`Approved` Module and a real student ID, so it
cannot be used pre-approval. This is explicitly named in the product vision as a precondition for
Module approval.

## Design

**Read path** (`GET api/admin/modules/{id}/preview`): loads the Module's linked Lesson and Exercise
for rendering, for a Module in **any** review status — the whole point of preview is doing it
*before* approval. Returns a student-safe Exercise DTO (Form.io schema, instructions, activity
type) that never includes `AnswerKeyJson`/`ScoringRulesJson`, plus a `CanScore`/`UnscorableReason`
pair computed via the existing `ExerciseLaunchEligibility.Evaluate` — the exact same eligibility
check the real student runtime launch path uses, so preview never claims something is scorable when
the live path would disagree (and vice versa).

**Score path** (`POST api/admin/modules/{id}/preview/submit`): scores a submitted answer using the
existing `ComponentAnswerScorer` — the same scoring engine placement, onboarding, and the real
student runtime attempt pipeline all already use. No new scoring logic was written; this reuses the
production scorer directly against the Exercise's real `ScoringRulesJson`.

**Deliberately separate from the real runtime launch path** (`IExerciseLaunchService`, Phase H10):
preview creates **no** `LearningActivity`, `ActivityAttempt`, or `StudentExerciseLaunch` row — it is
a pure read/score-only admin diagnostic. This was a conscious design choice over reusing
`ExerciseLaunchService` directly, for two reasons: (1) that service hard-requires `Module.ReviewStatus
== Approved`, which preview must bypass by design; (2) materializing a real `LearningActivity` tied
to a fabricated "admin as student" ID would pollute the same tables real student data lives in,
for a purely diagnostic action — the audit's own framing ("preview... before approving") implies an
ephemeral action, not one that leaves runtime rows behind.

**Frontend** reuses the existing shared `FormioRendererComponent` (already used for onboarding/
placement rendering) rather than building a new form renderer — the preview modal parses the
returned `FormSchemaJson` and renders it exactly as the student-facing renderer would, with a
`(submit)` handler that posts the submission to the new score endpoint and displays the result
(score percentage, correct/incorrect feedback message, or a clear "not auto-scorable yet" notice
for unsupported activity types like `short_answer`).

**UI entry point**: a new "Preview as Learner" button in the Module admin page's detail drawer
footer, next to Approve/Reject — available regardless of the Module's current review status.

## What was NOT changed

- `IExerciseLaunchService`/`ExerciseLaunchEligibility` — completely untouched; preview reuses the
  eligibility check as a pure function call, doesn't modify it.
- No new `LearningActivity`/`ActivityAttempt`/`StudentExerciseLaunch` rows are ever created by
  preview — verified by a dedicated test.
- Today Plan / Practice Gym module selection — unchanged; preview has no effect on which Modules
  are eligible for student delivery.
- No schema/migration change — this phase is entirely new read/score endpoints over existing data.

## Tests

Added `AdminModulePreviewServiceTests.cs` (9 tests), building real Modules via the existing
deterministic `ModuleGenerationService` (not hand-rolled links) so link wiring matches production
exactly:
- Preview works for a `PendingReview` Module, not just `Approved` — the core requirement.
- Preview never exposes the answer key (schema shown doesn't contain the correct answer text).
- Preview of a `short_answer` Exercise reports `CanScore = false` with a clear reason.
- Preview of a nonexistent Module returns null (404 at the controller).
- Submitting the correct answer scores 100% with the exercise's own `correctFeedback` message.
- Submitting a wrong answer scores 0% with the `incorrectFeedback` message.
- Submitting against an unscorable Exercise returns `Scored = false` with a reason, never throws.
- **Preview submit never creates a `LearningActivity` or `ActivityAttempt` row** — the safety/
  isolation-critical test for this pass.
- Submitting against a nonexistent Module throws `ModuleValidationException`.

## Validation

- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline).
- `dotnet test --configuration Release` — 3,457/3,457 passing (5 architecture, 2,140 unit [+9 new],
  1,312 integration).
- `npm run build -- --configuration production` — no new TS/Angular compile errors (one `@else if
  (...; as x)` template syntax error was found and fixed during implementation — Angular only
  allows the `as` binding on the primary `@if`, not `@else if`); fails only the same pre-existing
  bundle-size budget, unrelated to this change.
- Frontend unit tests (Karma) not run — still blocked by pre-existing, unrelated `TODO-H8-2`.
- Playwright not run — a live click-through of the new preview modal was not performed this
  session (no running dev server); the backend scoring path is verified end-to-end by the unit
  tests above, but the actual Form.io rendering + submit event wiring in the browser has not been
  manually verified.

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`,
  `ExerciseLaunchService.cs`, `ExerciseLaunchEligibility.cs`, `ComponentAnswerScorer.cs`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry).
- Docs intentionally not updated: none — scope was contained to this one feature.
- Reason: n/a.

## Risks or unresolved questions

- Not manually verified in a live browser session — the frontend wiring (schema parsing, Form.io
  submit event, score display) compiles cleanly but has not been click-tested end-to-end. Worth a
  quick manual smoke test on the actual admin Modules page before considering this fully done.
- Preview only shows the Module's *primary* Lesson/Exercise link (first by `SortOrder`) — a Module
  with multiple linked Exercises only previews one of them. This matches the scope of what
  `IExerciseLaunchService` itself does (it also only launches one eligible Exercise per Module), so
  it's consistent with the existing runtime behavior, not a new limitation.
- The preview modal's Form.io rendering assumes the same component conventions (`textfield`/`radio`
  with an `answer` key) that the deterministic and AI generation services already produce — a
  hand-authored Exercise with a differently-shaped schema would still render (Form.io is generic)
  but the scoring dictionary lookup by component key still works correctly as long as the schema's
  component keys match the `ScoringRulesJson` keys, which is already a general Exercise-authoring
  invariant, not something new to this phase.

## Final verdict

Closes the second of the audit's two Critical product gaps (the first, "no AI in generation," was
closed by Phase J2). Admins can now genuinely preview a Module exactly as a learner would — see the
Lesson, complete the Exercise, get a real score and feedback message — entirely before approving it,
using the exact same scoring engine and eligibility rules the real student runtime uses, with zero
student-facing runtime rows created.

## Next recommended action

Per the original audit's phase ordering, the remaining open phases are J4 (`short_answer` runtime
support or explicit UI gating) and J5 (import content-type expansion) — both lower-priority than the
two Critical gaps now closed. A manual browser smoke test of the new preview modal is also
recommended before this is considered fully verified, given Playwright/live-browser testing wasn't
performed this session.
