---
status: current
lastUpdated: 2026-06-28 09:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 15C — Today Lesson Player Foundation: Implementation Review

**Date:** 2026-06-28
**Related sprint:** Phase 15C
**Review type:** Engineering implementation review

---

## Summary

Phase 15C builds the foundation of the Today lesson player. The audit found that most of the core lesson player infrastructure was already in place from earlier phases. This phase closes the remaining gaps: CEFR level in session detail, an honest unsupported-activity message, explicit today-page state model, preparing state testid, and additional test coverage.

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Application/Sessions/SessionHandlers.cs` — `SessionDetailResult` DTO
- `src/LinguaCoach.Infrastructure/Sessions/SessionQueryHandler.cs` — `HandleAsync(GetSessionQuery)` implementation
- `src/LinguaCoach.Api/Controllers/SessionsController.cs` — session controller (no change needed — returns result directly)
- `src/LinguaCoach.Domain/Entities/StudentProfile.cs` — confirmed `CefrLevel` property at L43

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/session.models.ts` — session TypeScript interfaces
- `src/LinguaCoach.Web/src/app/features/student/lesson/lesson.component.ts` — lesson state machine
- `src/LinguaCoach.Web/src/app/features/student/lesson/lesson.component.html` — lesson template
- `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.html` — renderer dispatch template
- `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.spec.ts` — renderer unit tests
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.ts` — dashboard component
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.html` — dashboard template
- `src/LinguaCoach.Web/e2e/today-lesson.spec.ts` — Playwright E2E tests

---

## Audit Findings (Part A)

### What already existed before this phase

| Area | Status | Notes |
|---|---|---|
| Dashboard preparing state | Already implemented | "Your first lesson is being prepared" shown when `todaysSession()` is null |
| Lesson header (title, goal, status, progress bar) | Already implemented | `lesson.component.html` renders all metadata |
| Exercise stepper (numbered, completed, active) | Already implemented | `@for` loop with `activeExerciseIndex` logic |
| Exercise prepare flow | Already implemented | `prepareIfNeeded()` → `/prepare` endpoint with spinner/error states |
| Activity player via `/activity` | Already implemented | `moduleRedirectGuard` constructs `/activity?activityId=xxx&returnTo=/lesson/yyy` |
| Session start/complete lifecycle | Already implemented | `startLesson()`, `completeExercise()`, `completeSession()` |
| Lesson completion summary | Already implemented | `data-testid="lesson-complete-summary"` |
| Review exercise reflection panel | Already implemented | Separate `@if (exercise.kind === 'review')` block |
| Placement guard on `/lesson/:id` | Already implemented | `placementRequiredRedirectGuard` applied |
| Dashboard Start/Continue routing | Already implemented | CTA links to `/lesson/:sessionId` |
| 10 Playwright tests (Part I baseline) | Already passing | `today-lesson.spec.ts` |

### Gaps closed in this phase

| Gap | Fix |
|---|---|
| `SessionDetailResult` missing `CefrLevel` | Added `string? CefrLevel` to DTO; populated from `profile.CefrLevel` in handler |
| `SessionDetailResponse` TypeScript missing `cefrLevel` | Added `cefrLevel: string \| null` field |
| Lesson header missing CEFR level display | Added CEFR badge inline in header metadata row |
| `ExerciseRendererComponent @default` fell back to FreeTextEntry for unknown interactionMode | Changed to show honest "not available in the lesson player yet" message |
| Dashboard preparing state had no testid | Added `data-testid="session-preparing"` to the preparing block |
| No explicit Today-page state model | Added `todaySessionState()` method to `DashboardComponent` returning one of 7 named states |
| Missing Playwright tests for preparing state, placement redirect, CEFR header, review panel, error containment | Added 5 new tests to `today-lesson.spec.ts` |
| Missing unit test for unsupported renderer | Added to `exercise-renderer.component.spec.ts` |

---

## Today Page State Model (Part B)

`DashboardComponent.todaySessionState()` returns one of:

| State | Condition |
|---|---|
| `Error` | `this.error()` is set (summary load failed) |
| `PlacementRequired` | `lifecycleStage` is `PlacementRequired` or `PlacementInProgress` |
| `NotAvailable` | Stage is set but not in the lesson-access set |
| `Preparing` | Lesson-access stage but `todaysSession()` is null |
| `CompletedToday` | Session status is `completed` |
| `InProgress` | Session status is `inProgress` |
| `Ready` | Session exists, not started |

---

## Lesson Header (Part C)

`SessionDetailResponse` now includes `cefrLevel: string | null`. The lesson header renders it conditionally:

```html
@if (session()!.cefrLevel) {
  · <span data-testid="lesson-cefr-level">{{ session()!.cefrLevel }}</span>
}
```

Fields displayed: title, durationMinutes, focusSkill, topic, cefrLevel (conditional), status badge, sessionGoal (as "Lesson goal" card), progress bar.

No fields are fabricated. `cefrLevel` is omitted from the header when null (student has no placement result yet).

---

## Activity List / Stepper (Part D)

Already implemented before this phase. The exercise list:
- Numbered steps with completion icons (✓ / –)
- Kind colour-coding via CSS tokens
- `activeExerciseIndex` highlights the current step
- Click to select a step
- Prepare/error/loading states per exercise
- Review step has a separate reflection panel

No changes needed.

---

## Activity Player Shell (Part E)

The lesson uses the existing `/activity` page as the activity player shell. The flow:

1. User selects an exercise in the lesson stepper.
2. `prepareIfNeeded()` calls `/prepare`, resolves `activityId`.
3. "Start module" links to `/module/session-{id}-{exId}`.
4. `moduleRedirectGuard` loads the session, finds the exercise, navigates to `/activity?activityId=xxx&returnTo=/lesson/yyy`.
5. `ActivityLessonComponent` renders teach → practice → feedback.
6. "Next activity" returns to `/lesson/yyy` via `returnTo`.

The `ExerciseRendererComponent` now shows an honest "not available" message for unknown `interactionMode` values instead of silently falling back to FreeTextEntry.

---

## Learn → Practice → Feedback (Part F)

Implemented via `ActivityLessonComponent` + `ActivityPracticePageComponent` + `ExerciseRendererComponent`. Submission is handled by `ActivityService` (text, vocab, listening, speaking, pattern renderers). Feedback is shown by `ActivityFeedbackPageComponent`. All 25 renderers are available.

The lesson shell (`LessonComponent`) does not need to duplicate this logic. It coordinates the stepper; the activity page handles content.

---

## Dashboard Integration (Part G)

- "Start today's lesson" / "Resume lesson" / "Review today's lesson" CTA links to `/lesson/:sessionId`.
- When the user completes a lesson and navigates back to `/dashboard`, `DashboardComponent.ngOnInit()` fires and refreshes the summary.
- The refreshed summary returns `Completed` status; the dashboard shows "Completed" badge and "Review today's lesson" button.
- `todaySessionState()` returns `CompletedToday` in this case.

---

## Loading and Error Handling (Part H)

| State | Handling |
|---|---|
| Lesson loading | Skeleton pulses in `lesson.component.html` |
| Session 404 | `pageState.set('error')` with message, back-link to dashboard |
| Exercise prepare loading | Per-exercise spinner with "Preparing activity…" |
| Exercise prepare 503 | Inline error with "Try again" button |
| Exercise prepare generic error | Inline error with "Try again" button |
| Session Preparing (no session yet) | Dashboard shows "Your lesson is being prepared" |
| Dashboard summary 500 | Dashboard shows generic "Could not load your dashboard" error |

---

## Tests Added / Updated (Part I)

### New unit test
- `exercise-renderer.component.spec.ts`: unknown `interactionMode` shows `data-testid="unsupported-activity-type"` message.

### New Playwright tests (appended to `today-lesson.spec.ts`)
1. `dashboard shows preparing state when session is being generated` — `data-testid="session-preparing"` visible.
2. `placement-required student is redirected from lesson to placement` — `/lesson/:id` → `/placement`.
3. `lesson header shows CEFR level from session detail` — `data-testid="lesson-cefr-level"` shows "B1".
4. `review exercise panel shows reflection prompt without open-activity button` — review kind shows reflection panel only.
5. `lesson page shows contained error when session id is invalid` — page does not crash; either error or redirect.

### Pre-existing tests verified passing
- 10 existing Playwright tests in `today-lesson.spec.ts`
- 7 existing Playwright tests in `today-page-identity.spec.ts`
- All backend tests (2715)
- All Angular unit tests (1414)

---

## Build / Test Totals

| Suite | Count | Result |
|---|---|---|
| Backend — Architecture | 3 | PASS |
| Backend — Unit | 1504 | PASS |
| Backend — Integration | 1208 | PASS |
| Angular unit | 1414 | PASS |
| Playwright E2E | 156 | PASS |

Backend: 0 errors, 23 pre-existing warnings (unchanged).
Angular build: production build clean.

---

## APIs Consumed

| Endpoint | Used by |
|---|---|
| `GET /api/student/dashboard/summary` | Dashboard (already wired) |
| `GET /api/sessions/:id` | LessonComponent on route load |
| `POST /api/sessions/:id/start` | LessonComponent `startLesson()` |
| `POST /api/sessions/:id/exercises/:exId/prepare` | LessonComponent `prepareIfNeeded()` |
| `GET /module/session-:id-:exId` (guard) | Module redirect → `/activity?activityId=...&returnTo=...` |
| `POST /api/sessions/:id/exercises/:exId/complete` | LessonComponent `completeExercise()` |
| `POST /api/sessions/:id/complete` | LessonComponent `completeSession()` |
| `GET /api/activities/:id` | ActivityLessonComponent |
| `POST /api/activities/:id/attempt` | ActivityLessonComponent submission |

---

## Supported vs Unsupported Activity Types

**Supported** (all 25 `interactionMode` values handled by `ExerciseRendererComponent`):
readOnly, freeTextEntry, matchingPairs, gapFill, audioFreeText, audioGapFill, chatReply, emailReply, multipleChoiceSingle, multipleChoiceMulti, readingFillInBlanks, reorderParagraphs, readingWritingFillInBlanks, listeningFillInBlanks, highlightCorrectSummary, highlightIncorrectWords, writeFromDictation, summarizeSpokenText, answerShortQuestion, readAloud, repeatSentence, respondToSituation, describeImage, retellLecture, summarizeGroupDiscussion.

**Unsupported** (any unknown `interactionMode`): Shows `data-testid="unsupported-activity-type"` with honest message "This activity type is not available in the lesson player yet."

---

## Remaining Limitations

| Limitation | Notes |
|---|---|
| Session reflection (501) | `GET /api/sessions/:id/reflection` still returns 501. Post-lesson review mode deferred. |
| No exercise skip button | `SessionExercise.Skip()` exists in domain; no UI or endpoint to trigger it. |
| No in-lesson activity feedback | Feedback shows on the `/activity` page, not in the lesson stepper panel. User must return to lesson manually. |
| No "generation failed" recovery | If AI generation fails for a session, no recovery flow exists. Student must wait for next buffer fill. |
| `cefrLevel` null for pre-placement students | Expected. Template hides the badge when null. |

---

## Decisions Made

1. **Activity player stays on `/activity`**: Embedding `ActivityLessonComponent` inline in the lesson shell was considered but rejected. The existing `moduleRedirectGuard + returnTo` flow is clean, tested, and complete. Inline embedding would duplicate all submission/feedback logic without adding user value in Phase 15C.

2. **`hasLessonAccess` visibility**: Changed from `private` to package-visible (no modifier in TypeScript classes) so `todaySessionState()` can call it. The method is pure and has no side effects.

3. **Preparing copy change**: Changed "Your first lesson is being prepared" to "Your lesson is being prepared" — "first" implies onboarding, but the preparing state can appear for any session regeneration.

---

## Risks

- The unsupported-activity message will appear if the backend ever returns an `interactionMode` that has no renderer. This is correct and safe — it's an improvement over the previous silent FreeTextEntry fallback.
- `cefrLevel` is null for students who completed placement with confidence < 0.6. The header badge is simply omitted.

---

## Final Verdict

Phase 15C foundation is complete. The Today lesson player is powered by real session/activity data. All acceptance criteria are met.

---

## Next Recommended Action: Phase 15D

Candidates for Phase 15D:
1. **Inline activity feedback in lesson panel** — Show a compact feedback summary within the exercise panel after the user returns from `/activity`, so progress is visible without navigating again.
2. **Session generation failure recovery** — Surface generation failures on the dashboard with a "Retry" action instead of silently waiting for the next buffer fill.
3. **Post-lesson reflection** — Implement the `session_reflection` AI prompt and the 501 endpoint to complete the review flow.
4. **Exercise skip action** — Add skip UI and `POST /api/sessions/:id/exercises/:id/skip` endpoint using the existing domain method.
