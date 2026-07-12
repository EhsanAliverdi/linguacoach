---
status: current
lastUpdated: 2026-07-13 00:00
owner: engineering
supersedes:
supersededBy:
---

# Live Browser Smoke Test — Phase J3 (Module Preview) and J4 (Launch-Support Badges)

**Date:** 2026-07-13
**Related sprint/feature:** Follow-up verification of `docs/reviews/2026-07-11-phase-j3-admin-module-preview-review.md`
and `docs/reviews/2026-07-11-phase-j4-exercise-launch-support-honesty-review.md`, both of which
flagged "not yet manually verified in a live browser session" as an open risk.
**Type:** Manual QA + one real bug fix found and fixed during testing.
**Files changed:**
- `src/LinguaCoach.Web/src/app/features/admin/admin-modules/admin-modules.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-modules/admin-modules.component.html`

## Setup

Started the backend stack via `docker compose up -d db minio api` (the `web` service's own Docker
build fails on the same pre-existing bundle-size budget documented elsewhere — worked around by
using the already-running `ng serve` dev server on port 4200 instead, proxying `/api` to the
container). Logged in as the seeded admin (`admin@linguacoach.local`). The shared dev Postgres
database (persistent across sessions, ~4 weeks old) had import-run history but no published
Resource Bank items, Lessons, Exercises, or Modules — its `approve-and-publish` endpoint reported
`success: true` but the row never appeared in `resource_bank_items` on direct SQL inspection. This
publish-pipeline discrepancy in the shared dev environment was not investigated further — it's
orthogonal to J3/J4 and risked turning a smoke test into an unrelated infrastructure debugging
session. Worked around it by seeding one Lesson, two Exercises (one `gap_fill` approved, one
`short_answer` pending), and one `PendingReview` Module directly via SQL, matching the exact JSON
shapes `ExerciseGenerationService`/`AiExerciseGenerationService` produce in production. Test data
was deleted after verification.

## What was verified

**Phase J3 — Module preview, backend:** `GET /api/admin/modules/{id}/preview` and
`POST /api/admin/modules/{id}/preview/submit` both called directly against the live API for the
seeded `PendingReview` Module — confirmed working exactly as designed: the preview loads for a
non-approved Module, a correct answer scores 100% with the `correctFeedback` message, a wrong
answer scores 0% with the `incorrectFeedback` message, and no `LearningActivity`/`ActivityAttempt`
row was created (matching Phase J3's own unit test coverage).

**Phase J3 — Module preview, frontend UI:** the "Preview as Learner" button appears in the Module
detail drawer footer for a `PendingReview` Module. Clicking it opens a modal rendering the Learn
content (Lesson title/body/examples/usage notes) and Practice content (Exercise instructions, the
Form.io-rendered gap-fill sentence with a text field), matching the backend response exactly.

**Phase J4 — launch-support badges:** the Exercises list shows a yellow "Not launchable yet" badge
next to the `short_answer` Exercise and no badge next to the `gap_fill` Exercise. The detail drawer
shows a "Student launch support" field with the same badge plus the full reason text ("This module
contains an activity type that is not launchable yet..."). The page subtitle correction (no longer
claiming H10 launch is "planned") is also live.

## Bug found and fixed: J3 preview modal had no way to submit

The rendered Form.io form (a `content` component plus a `textfield` component — the same shape
`ExerciseGenerationService.ComposeGapFill`/`AiExerciseGenerationService`'s equivalent produce)
**does not include its own submit button**, and `Formio.createForm()` does not add one by default
for this schema shape. The J3 preview modal, as shipped in the earlier session, relied entirely on
the Form.io-native `(submit)` event with no way to trigger it — an admin could fill in an answer but
had no button to press. Confirmed by inspecting the rendered DOM directly (`.formio-form`'s
`innerHTML` contains only the two authored components, no button element anywhere).

This app's own established pattern for schemas without a built-in submit button is for the host
page to trigger submission externally via `FormioRendererComponent.submitForm()` — `PlacementComponent`
already does exactly this. The J3 preview modal was missing that wiring. Fixed by adding a
`@ViewChild(FormioRendererComponent)` reference and a "Submit Answer" button that calls
`.submitForm()` — verified end-to-end afterward: filling "thorough" and clicking Submit Answer shows
"Score: 100% — Correct!"; filling a wrong answer shows "Score: 0% — Not quite — the answer was
'thorough'."

## Separate, more significant finding — NOT fixed this session

**The same missing-submit-button condition appears to affect the real student runtime**, not just
the new J3 preview modal. `exercise-renderer.component.html` (the student-facing Form.io renderer
used by `/activity`) wires `app-formio-renderer`'s `(submit)` event with no external "Submit" button
and no `submitForm()` call anywhere in `exercise-renderer.component.ts` or its parent
`activity-practice-page.component.ts`. Since the deterministic and AI-generated `gap_fill`/
`multiple_choice_single` schemas both lack their own submit button (confirmed by reading the
generation code, not just this session's test data), this suggests a student launching one of these
Exercises via the Phase H10 launch bridge may have **no way to submit their answer in the browser
today**, despite the backend scoring pipeline being fully proven by H10's integration tests (which
call `POST /api/activity/{id}/attempt` directly, bypassing the UI, so they would not have caught
this).

**This was not investigated further or fixed in this session** — it's outside J3/J4's scope, touches
the live student-facing runtime (higher risk, deserves its own dedicated verification), and AGENTS.md's
own testing rule already anticipates exactly this gap ("Do not declare a frontend feature complete
based only on unit tests"). Flagging it here as a discovered, unverified hypothesis for the user to
prioritize — it has not been confirmed against the real `/activity` page with a real student account,
only inferred from reading the same component code path the J3 modal shares.

## Validation

- Backend: no changes this session beyond the earlier J0-J4 commits; not re-run (no backend files
  touched).
- Frontend: `npx ng build --configuration production` — no new compile errors after the submit-button
  fix (same pre-existing bundle-size budget failure, unrelated).
- Manual browser verification: full round-trip for J3 (open module → preview → fill answer → submit
  → see score/feedback, for both correct and incorrect answers) and J4 (badge on list + drawer for
  an unscorable type, no badge for a scorable one) — both confirmed working via `gstack browse`
  against the live dev server.

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-11-phase-j3-admin-module-preview-review.md`,
  `docs/reviews/2026-07-11-phase-j4-exercise-launch-support-honesty-review.md`,
  `formio-renderer.component.ts`, `exercise-renderer.component.ts`/`.html`,
  `activity-practice-page.component.html`, `placement.component.ts`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (updated dated entry, resolves the "not yet manually
  verified" caveat from J3/J4 and flags the new student-runtime hypothesis).
- Docs intentionally not updated: none.
- Reason: n/a.

## Risks or unresolved questions

- **The student-runtime submit-button hypothesis above is unconfirmed** — it was inferred from code
  reading, not verified against a live student session. This is the most important open item from
  this session and should be verified (or disproven) before it's treated as fact.
- The shared dev database's `approve-and-publish` endpoint returning `success: true` without a
  corresponding row in `resource_bank_items` was observed but not root-caused — flagged as a
  possible separate issue worth investigating if it recurs, but not confirmed as a real bug (could
  be a dev-environment-specific artifact of this long-lived, heavily-reused database).

## Final verdict

Both J3 and J4 are now confirmed working end-to-end in a live browser, closing the "not yet manually
verified" caveat both phases carried. One real bug in the newly-shipped J3 preview modal (no way to
submit) was found and fixed in the same session. One more significant, unconfirmed hypothesis about
a potential pre-existing gap in the live student runtime was surfaced and flagged for the user's
attention, not fixed.

## Next recommended action

Verify (or disprove) the student-runtime submit-button hypothesis — the fastest path is likely a
live click-through of `/activity` for a real launched `gap_fill` or `multiple_choice_single`
Exercise via the H10 bridge. If confirmed, this would be a higher-priority fix than J5 (import
content-type expansion), since it would mean a shipped, "proven" runtime feature is currently
unusable by students in the browser.
