---
status: current
lastUpdated: 2026-07-11 06:00
owner: engineering
supersedes:
supersededBy:
---

# Phase J4 — Honest Launch-Support Signaling for Exercises

**Date:** 2026-07-11
**Related sprint/feature:** From `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`
(§D) — the last of the audit's five recommended phases (J0-J4); J5 (import content-type expansion)
remains open as lower priority.
**Files reviewed/changed:**
- `src/LinguaCoach.Application/ExerciseLaunch/ExerciseLaunchEligibility.cs`
- `src/LinguaCoach.Application/Exercises/ExerciseContracts.cs`
- `src/LinguaCoach.Infrastructure/Exercises/ExerciseMappers.cs`
- `src/LinguaCoach.Web/src/app/core/models/admin-exercise.models.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercises/admin-exercises.component.html`
- `tests/LinguaCoach.UnitTests/Exercises/ExerciseGenerationServiceTests.cs`

## Problem and decision

The audit found `short_answer` Exercises can be generated, reviewed, and approved, but have no
runtime launch path — `ExerciseLaunchEligibility` only supports `gap_fill`/`multiple_choice_single`
— with no signal anywhere telling the admin this before they invest review time in one. Two
directions were possible: build real AI-assisted grading to make `short_answer` launchable, or
honestly surface the limitation wherever it matters. **Decided (user, explicit AskUserQuestion):
surface honestly, don't build new grading infrastructure this phase** — the smaller, safer option,
consistent with the project's "do not overbuild" principle (`AGENTS.md` §10) and its own precedent
of not adding new architecture when a UI/UX signal is what's actually needed.

## What changed

**`ExerciseLaunchEligibility.EvaluateContentSupport`** (new) — the existing `Evaluate` method's
checks, minus the `ReviewStatus == Approved` gate, factored into a separate pure function.
`Evaluate` now delegates to it after its own approval check, so real runtime behavior
(`ExerciseLaunchService`, `PracticeGymModuleSelectionService`, and Phase J3's preview) is completely
unchanged — same messages, same logic, same call sites. The new method exists purely so admin
authoring surfaces can ask "would this be launchable once approved" for a still-`PendingReview`
draft, without `Evaluate`'s own answer ("not approved yet") trivially masking the real question.

**`ExerciseDto`** gained two fields — `CanLaunchOnceApproved` (bool) and `LaunchUnsupportedReason`
(string?) — computed once, centrally, in `ExerciseMappers.ToDto` via `EvaluateContentSupport`. Since
every Exercise-returning endpoint (list, get, generate-from-resources, generate-from-resources/ai,
generate-from-lesson, approve, reject, update) goes through this one mapper, the honest signal is
now visible everywhere an Exercise appears in the admin API — not just at one moment like generation
or approval — with zero duplicated logic across those call sites.

**Frontend**: the Exercises list page shows a "Not launchable yet" badge (with the reason as a
tooltip) next to the activity-type badge for any Exercise where `canLaunchOnceApproved` is false; the
detail drawer shows a dedicated "Student launch support" field with either a green "Launchable once
approved" badge or the warning badge plus the full reason text. The page's own subtitle copy was
also corrected — it still said "Launching a scored attempt... is not implemented yet (planned for
Phase H10)", which became stale once H10 (2026-07-10) and J3 (this session) actually shipped launch/
preview paths; it now accurately states that only `gap_fill`/`multiple_choice_single` currently
launch, and other types remain reviewable/reusable in Modules without a runtime path.

## What was NOT changed

- No new grading/evaluation infrastructure — `short_answer` remains unscored, exactly as before.
- `ExerciseLaunchEligibility.Evaluate`, `ExerciseLaunchService`, `PracticeGymModuleSelectionService`
  — all untouched; their real runtime behavior and exact messages are unchanged (verified by the
  full test suite passing unmodified for every pre-existing test that depends on `Evaluate`'s
  output).
- No blocking added anywhere — a `short_answer` Exercise can still be generated, reviewed, and
  approved exactly as before; this phase only adds visibility, never a new restriction.
- No schema/migration change — `CanLaunchOnceApproved`/`LaunchUnsupportedReason` are computed at
  read time, not persisted columns.

## Tests

Added two tests to `ExerciseGenerationServiceTests.cs` (existing file, deterministic composer):
- A generated `gap_fill` draft is flagged `CanLaunchOnceApproved = true` with no reason, while still
  `PendingReview` — proves the flag reflects content support, not approval status.
- A generated `short_answer` draft is flagged `CanLaunchOnceApproved = false` with a reason
  containing "not launchable yet", also while still `PendingReview`.

No new test file was needed — the flag is computed centrally in the mapper, so these two tests (plus
the full pre-existing suite passing unmodified) are sufficient coverage; every other
Exercise-returning code path shares the same mapper.

## Validation

- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline).
- `dotnet test --configuration Release` — 3,459/3,459 passing (5 architecture, 2,142 unit [+2 new],
  1,312 integration). The full pre-existing suite passed **unmodified** after the
  `ExerciseLaunchEligibility` refactor and the `ExerciseDto` field addition — confirms the change is
  purely additive with zero behavior change to any existing call site.
- `npm run build -- --configuration production` — no new TS/Angular compile errors; fails only the
  same pre-existing bundle-size budget, unrelated to this change.
- Frontend unit tests (Karma) not run — still blocked by pre-existing, unrelated `TODO-H8-2`.
- Playwright not run; the new badges were not manually verified in a live browser session (same
  caveat as Phase J3's preview modal — still pending a manual smoke test).

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`,
  `ExerciseLaunchEligibility.cs`, `ExerciseLaunchService.cs`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry); the Exercises admin page's own
  subtitle copy (corrected stale H10 reference, in-app, not a docs file, but worth noting here since
  it was a copy-accuracy fix in the same spirit as this phase).
- Docs intentionally not updated: none.
- Reason: n/a.

## Risks or unresolved questions

- Same manual-verification gap as Phase J3: the new badges compile cleanly but haven't been visually
  confirmed in a running browser.
- This phase deliberately did not address the audit's underlying content-type gap itself (4 of 7
  import content types still stubbed, `short_answer` still unscored) — it only makes the existing
  gap honestly visible. Building real grading for open-ended text remains future work if the product
  decides it's worth the investment (flagged as the alternative option in this session's
  AskUserQuestion, not chosen).

## Final verdict

Closes Phase J4 as scoped: a small, additive, zero-risk change that makes an existing limitation
honestly visible everywhere an Exercise is shown to an admin, without touching any real runtime
behavior. This completes all five phases (J0-J4) from the original architecture audit's
recommended ordering except J5 (import content-type expansion), which remains open as explicitly
lower priority.

## Next recommended action

J5 (import content-type expansion — Listening/Speaking/Writing/Mixed, currently UI-stubbed) is the
only remaining phase from the original audit. Given J0-J4 are now all closed, a good next step
before starting new work would be the manual browser smoke tests flagged as pending in both this
review and the J3 review (Module preview modal, and now these launch-support badges).
