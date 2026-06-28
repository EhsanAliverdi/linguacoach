# Phase 16B Review — Activity Completion and Feedback Loop Hardening

**Date:** 2026-06-28
**Sprint:** Phase 16B
**Type:** Engineering review + implementation

---

## Scope

Verify and harden the full real activity completion loop:

> Activity rendered → Student submits response → Backend records attempt → Feedback returned → Activity/session marked complete → Mastery/LearningPlan updates → Dashboard/Practice/Progress/Journey reflect the change

Constraint: no new exercise formats, no new AI evaluation logic, no UI redesign, no new routing or placement rules unless fixing a verified bug.

---

## Files Reviewed

### Frontend
- `src/LinguaCoach.Web/src/app/core/guards/module-redirect.guard.ts`
- `src/LinguaCoach.Web/src/app/core/guards/module-redirect.guard.spec.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-lesson/activity-lesson-vocab.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-lesson/activity-lesson-submission.component.spec.ts` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-feedback-page/activity-feedback-page.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-feedback-page/activity-feedback-page.component.html`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-practice-page/activity-practice-page.component.html`
- `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.html`
- `src/LinguaCoach.Web/src/app/features/student/lesson/lesson.component.ts`
- `src/LinguaCoach.Web/src/app/core/services/activity.service.ts`
- `src/LinguaCoach.Web/src/app/app.routes.ts`
- `src/LinguaCoach.Web/e2e/exercise-pattern-renderers.spec.ts`
- `src/LinguaCoach.Web/e2e/today-lesson.spec.ts`
- `src/LinguaCoach.Web/e2e/practice-gym.spec.ts`

### Backend
- `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs`

---

## Findings

### P0 — moduleRedirectGuard UUID split bug (FIXED)

**File:** `module-redirect.guard.ts`

The guard constructs the route parameter as `session-{sessionId}-{exerciseId}`. It previously split this using `rest.lastIndexOf('-')`, which fails when both IDs are standard UUIDs (36 chars, 4 hyphens each). `lastIndexOf` finds the last hyphen *inside* the exerciseId UUID, producing two incorrect ID fragments.

**Impact:** Every student navigating from a lesson page to an activity via the `/module/` route would receive an incorrect `sessionId` and `exerciseId`, causing a 404 or misrouted activity load. The existing test suite used short IDs like `sess1`/`ex1` and never caught this.

**Fix:** UUID pair regex match — `/^([uuid])-([uuid])$/i` — with fallback to `lastIndexOf` for short test IDs.

```typescript
const UUID_PAIR = /^([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})-([0-9a-f]{8}-...)/i;
const uuidMatch = rest.match(UUID_PAIR);
const sessionId = uuidMatch ? uuidMatch[1] : rest.slice(0, rest.lastIndexOf('-'));
const exerciseId = uuidMatch ? uuidMatch[2] : rest.slice(rest.lastIndexOf('-') + 1);
```

A new guard spec test verifies the UUID-format split explicitly.

---

### P1 — Mojibake in scoreImprovementMessage (FIXED)

**File:** `activity-feedback-page.component.ts`

Three occurrences of `â€"` (UTF-8 em-dash rendered as mojibake) in `scoreImprovementMessage()`. Same class of encoding bug fixed in Phase 15H for the profile page.

Fixed to `—` in all three places.

---

### P1 — No honest empty-feedback state (FIXED)

**File:** `activity-feedback-page.component.html` and `.component.ts`

When the AI evaluation path returns an empty `ActivityFeedbackDto` (all fields null/empty — the backend saves `feedbackJson = "{}"` on AI failure), the feedback page rendered a blank card with no content and no explanation.

**Fix:**
- Added `hasFeedbackContent` getter in the component that checks `patternEvaluation`, `score`, `coachSummary`, `changes`, `whatYouDidWell`, and `questionFeedback`.
- When all are falsy, the template shows a `data-testid="feedback-pending"` card: "Your response was saved. Feedback will appear after this activity is evaluated."

---

### Verified — Disabled state propagation (no change needed)

`ActivityLessonComponent.state === 'submitting'` → passed into `ActivityPracticePageComponent` as `[state]` → forwarded to `ExerciseRendererComponent` as `[disabled]="state === 'submitting'"`. All renderers receive `@Input() disabled = false` and pass it through. Verified correct, no fix required.

---

### Verified — Session reload on return from activity (no change needed)

`LessonComponent.ngOnInit()` calls `loadSession()`. Angular destroys and recreates the component on route change, so returning from `/activity` to `/lesson/:id` always triggers a fresh session load. No manual cache-busting required.

---

### Verified — Practice Gym suggestions reload (no change needed)

`PracticeGymComponent.ngOnInit()` calls the suggestions endpoint. Since Angular recreates the component on navigation, suggestions always reload when the student returns to `/practice` from an activity. No manual refresh needed.

---

### Verified — Exercise completion model (no change needed)

Pattern evaluation path: `ActivitySubmitHandler` calls `SessionExercise.Complete()` when `evalResult.Completed = true`. Backend returns `sessionComplete = true` when all exercises are done. LessonComponent detects this and calls `completeSession()`.

Legacy paths (VocabularyPractice, ListeningComprehension, WritingScenario): exercises are NOT auto-completed. Student uses the "Mark complete" button in the lesson UI. This is intentional — legacy evaluation does not have deterministic pass/fail.

---

## Payload Contract Verification

All renderer payloads verified against `onRendererSubmit()` serialization:

| Renderer | Payload kind | Serialized to |
|---|---|---|
| freeText / freeTextEntry | `freeText` | plain text string |
| chatReply | `chatReply` | `replyText` string |
| emailReply | `emailReply` | `{ subject, body }` |
| matchingPairs | `matchingPairs` | `{ pairs: { leftId: rightId } }` |
| gapFill | `gapFill` | `{ answers: { gapId: value } }` |
| multipleChoice | `multipleChoiceSingle` | `{ selectedOptionId }` |
| multipleChoiceMulti | `multipleChoiceMulti` | `{ selectedOptionIds }` |
| reorderParagraphs | `reorderParagraphs` | `{ orderedIds }` |
| listeningFillInBlanks | `listeningFillInBlanks` | `{ answers: Record<string,string> }` |
| readingFillInBlanks | `readingFillInBlanks` | `{ answers: Record<string,string> }` |
| readingWritingFillInBlanks | `readingWritingFillInBlanks` | `{ answers: Record<string,string> }` |
| highlightCorrectSummary | `highlightCorrectSummary` | `{ selectedOptionId }` |
| highlightIncorrectWords | `highlightIncorrectWords` | `{ selectedTokenIds }` |
| writeFromDictation | `writeFromDictation` | `{ items: [{itemId, submittedText}] }` |
| summarizeSpokenText | `summarizeSpokenText` | `{ summaryText }` |
| answerShortQuestion | `answerShortQuestion` | `{ items: [{itemId, answerText}] }` |
| readAloud | `readAloud` | `{ items }` |
| repeatSentence | `repeatSentence` | `{ items }` |
| respondToSituation | `respondToSituation` | `{ items }` |
| describeImage | `describeImage` | `{ items }` |
| retellLecture | `retellLecture` | `{ items }` |
| summarizeGroupDiscussion | `summarizeGroupDiscussion` | `{ items }` |
| audioAndFreeText | `audioFreeText` | `submitListeningAttempt(answers, responseText)` |
| audioAndGapFill | `audioGapFill` | `submitListeningAttempt(answers, '')` |

---

## Tests Added / Updated

### Unit tests (Karma/Jasmine)

**New file:** `activity-lesson-submission.component.spec.ts`

- State transitions through `submitting` → `feedback` (uses `Subject` to pause observable mid-flight)
- Feedback rendered after successful submit
- `attemptCount` increments after each submission
- Backend error → state back to `writing`, error message shown
- Generic error (no body) → fallback message
- `multipleChoiceSingle` serialized to `{ selectedOptionId }`
- `multipleChoiceMulti` serialized to `{ selectedOptionIds }`
- `gapFill` serialized to `{ answers: { gapId: value } }`
- `reorderParagraphs` serialized to `{ orderedIds }`
- `writeFromDictation` serialized to `{ items }`
- `summarizeSpokenText` serialized to `{ summaryText }`
- `nextActivity()` navigates to `returnTo` URL in lesson context
- `nextActivity()` reloads next activity when no `returnTo`
- `improveAnswer()` returns to `writing` state

**Updated:** `module-redirect.guard.spec.ts`

- New test: correctly splits session and exercise when both IDs are standard UUIDs (36-char format).

### E2E (Playwright)

**Updated:** `today-lesson.spec.ts`

- Today completion smoke: GapFill activity submitted from `/activity?returnTo=/lesson/SESSION_ID` → feedback shown → "Next activity" → URL is `/lesson/SESSION_ID`.

**Updated:** `practice-gym.spec.ts`

- Practice completion smoke: ChatReply activity submitted from `/activity?returnTo=/practice` → feedback shown → "Next activity" → URL is `/practice` → suggestions section visible.

---

## Test Results (at completion)

| Suite | Result |
|---|---|
| Karma (1479 tests) | 1479 SUCCESS |
| dotnet UnitTests (1504 tests) | 1504 PASS |
| dotnet IntegrationTests (1225 tests) | 1225 PASS |
| dotnet ArchitectureTests (3 tests) | 3 PASS |
| Angular production build | PASS |

---

## Decisions Made

1. **UUID regex split is correct approach.** Alternative (index 36 boundary) is brittle against schema changes. Regex is explicit and self-documenting.
2. **`hasFeedbackContent` on the component class (not inline in template).** Keeps template readable and allows future unit testing of the computed state.
3. **No changes to backend evaluation paths.** Legacy activities intentionally require manual "Mark complete" — this is not a bug.
4. **Smoke tests added inline to existing E2E files** rather than a new file, to maintain a single spec-per-feature grouping.

---

## Risks and Unresolved Questions

- The mojibake fix in `scoreImprovementMessage` was identical to Phase 15H. The root cause (file saved with wrong encoding somewhere in the pipeline) has not been addressed at the tooling level. Consider adding an encoding lint rule.
- Legacy activities (WritingScenario, ListeningComprehension, VocabularyPractice via the old path) still do not auto-complete exercises on submit. This is intentional but creates divergent UX from pattern-backed activities. Flagged for future harmonisation.
- E2E smoke tests for today/practice completion rely on `nextActivity()` navigation but do not verify that the lesson page correctly updates its exercise list (that would require mocking the session reload). Covered by existing `today-lesson.spec.ts` lesson-completion tests.

---

## Final Verdict

All P0 and P1 findings resolved. No regressions. 1479 frontend unit tests and 2732 backend tests green. Phase 16B hardening complete.

---

## Next Recommended Action

Phase 16C or begin the next sprint. The UUID split fix in `moduleRedirectGuard` is the most impactful change — consider a targeted manual smoke test in staging with a real session UUID pair before the next release.
