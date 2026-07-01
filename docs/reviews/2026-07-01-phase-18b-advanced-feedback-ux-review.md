# Phase 18B — Advanced Feedback UX — Engineering Review

**Date:** 2026-07-01
**Sprint:** Phase 18B
**Author:** Engineering (Claude Code)

---

## Summary

Phase 18B improves the student feedback experience across all activity types. The change is a presentation and UX upgrade only. No AI scoring logic, CEFR evaluation, objective completion, LP regeneration, or activity format changes were made. The writing evaluation endpoint existed on the backend but was never called by the frontend — Phase 18B wires it up for the first time.

---

## Files Created / Changed

### New Angular components

| File | Purpose |
|------|---------|
| `src/app/features/student/activity/feedback/feedback-ai-disclaimer.component.ts` | Inline AI disclaimer; configurable text; used by writing eval and coach feedback paths |
| `src/app/features/student/activity/feedback/feedback-pending-state.component.ts` + `.html` | Pending/failed/not-supported state renderer for speaking and writing evaluation |
| `src/app/features/student/activity/feedback/feedback-writing-eval.component.ts` + `.html` | Writing evaluation scores grid and feedback; handles all `EvaluationStatus` values |
| `src/app/features/student/activity/feedback/feedback-next-steps.component.ts` + `.html` | Context-aware action buttons; emits `improve`, `tryAgain`, `nextActivity`, `backToDashboard` |
| `src/app/features/student/activity/feedback/feedback-skill-context.component.ts` + `.html` | Skill/exercise-type/difficulty context header from `stageContent` |
| `src/app/features/student/activity/feedback/feedback-support-lang.component.ts` + `.html` | Generic collapsible "help in your language" — not hardcoded to Persian |

### Modified Angular

| File | Change |
|------|--------|
| `src/app/core/models/activity.models.ts` | Added `EvaluationStatus` type and `WritingEvaluationDto` interface |
| `src/app/core/services/activity.service.ts` | Added `getWritingEvaluation(activityId, attemptId)` method |
| `src/app/features/student/activity/activity-feedback-page/activity-feedback-page.component.ts` | 2 new inputs; writing eval polling; imports new components; new getters `isWritingActivity`, `isSpeakingActivity` |
| `src/app/features/student/activity/activity-feedback-page/activity-feedback-page.component.html` | Skill context header; writing eval section; generic support lang; context-aware next steps; AI disclaimer after coach summary; `nextPracticeSuggestion` rendered; `pronunciationScore` added to speaking scores grid |
| `src/app/features/student/activity/activity-lesson/activity-lesson.component.html` | Passes `activityType` and `stageContent` to `<app-activity-feedback-page>` |
| `src/app/features/student/activity/pattern-evaluation-result/pattern-evaluation-result.component.ts` | Imports `FeedbackAiDisclaimerComponent` |
| `src/app/features/student/activity/pattern-evaluation-result/pattern-evaluation-result.component.html` | AI disclaimer added at end of chat/email and spoken response sections |

### New spec files

| File | Tests |
|------|-------|
| `feedback-ai-disclaimer.component.spec.ts` | 2 |
| `feedback-pending-state.component.spec.ts` | 6 |
| `feedback-next-steps.component.spec.ts` | 12 |
| `feedback-skill-context.component.spec.ts` | 5 |
| `feedback-support-lang.component.spec.ts` | 7 |
| `feedback-writing-eval.component.spec.ts` | 15 |
| `activity-feedback-page.component.spec.ts` | 22 |

### Modified spec files

| File | Change |
|------|--------|
| `activity-lesson-submission.component.spec.ts` | Added `getAttemptEvaluation` and `getWritingEvaluation` to spy; both return `NEVER` to not interfere with lesson submission tests |

---

## Parts Delivered

| Part | Description | Status |
|------|-------------|--------|
| A | Audit current feedback flows | Complete — findings documented below |
| B | Define consistent UX model | Complete — shared components deliver the model |
| C | Shared feedback components | Complete — 6 new components |
| D | Writing feedback display | Complete — scores grid, correctedText, suggestedImprovement, feedbackText, AI disclaimer |
| E | Speaking feedback display | Complete — `pronunciationScore` added; AI disclaimer added |
| F | Skill/objective context | Complete — `FeedbackSkillContextComponent` from `stageContent` |
| G | Context-aware next-step actions | Complete — `FeedbackNextStepsComponent` with 4 outputs; writing/speaking/other rules applied |
| H | Pending/failed/notSupported states | Complete — `FeedbackPendingStateComponent`; writing eval states handled |
| I | Support-language help (generic) | Complete — `FeedbackSupportLangComponent`; Persian hardcoding removed |
| J | Admin visibility not broken | Verified — no admin component changes |
| K | Backward compatibility | Verified — all old `ActivityFeedbackDto` fields still render; `SpeakingEvaluationDto` polling unchanged |
| L | 25+ tests (mocked data) | Complete — 69 new tests; all 119 pre-existing failures remain; 0 new regressions |
| M | Build and test validation | Complete — Angular build clean; 1483/1602 unit tests pass |
| N | Sprint doc, roadmap, review | Complete — sprint doc updated, this review written |

---

## Audit Findings (Part A)

### Critical gaps addressed by this phase

1. **Writing evaluation never loaded** — `GET .../writing-evaluation` existed on the backend but frontend never called it. Students completed writing activities and never saw scores (grammar, vocabulary, coherence, task completion). Fixed: `ActivityFeedbackPageComponent` now polls this endpoint for `writingScenario` activities.

2. **`nextPracticeSuggestion` unreachable** — `ActivityFeedbackDto.nextPracticeSuggestion` was always null in the rendered output. The field existed in the DTO but had no template binding. Fixed: now rendered with a dedicated section.

3. **Hardcoded Persian** — `feedbackInSourceLanguage` label was hardcoded to "Show/Hide Persian explanation" and the heading "توضیح به فارسی". This broke for any other source language. Fixed: replaced with `FeedbackSupportLangComponent` using "Show/Hide help in your language".

4. **Action buttons not context-aware** — "Improve my answer" and "Try again" were always shown regardless of activity type. Speaking activities cannot improve answer (no text editor); pattern activities don't need try-again the same way. Fixed: `FeedbackNextStepsComponent` applies per-type rules.

5. **No AI disclaimer in legacy coach feedback path** — The pattern evaluation block had no disclaimer. Chat/email and spoken response blocks had no disclaimer. Fixed: `FeedbackAiDisclaimerComponent` added to all relevant paths.

6. **`pronunciationScore` not shown** — `SpeakingEvaluationDto.pronunciationScore` existed but was missing from the scores grid. Fixed.

---

## Decisions Made

1. **Writing evaluation is supplemental** — It appears alongside coach feedback for `writingScenario`, not replacing it. This preserves backward compatibility with older attempts that have coach feedback but no writing eval record.

2. **Writing polling interval 8s, max 15 polls** — Slower than speaking (10s, 12 polls) to match expected writing eval latency. Total wait ceiling: 2 minutes.

3. **`NEVER` observable in lesson tests** — The two eval observables in existing tests return `NEVER` so they don't complete and don't interfere with submission state machine timing in fakeAsync tests.

4. **`ngOnChanges()` called manually in new spec** — `ActivityFeedbackPageComponent` declares `ngOnChanges()` with no parameter. Test sets inputs directly on `componentInstance` (not via Angular bindings), so `ngOnChanges()` must be called manually to trigger eval loading.

5. **AI disclaimer text is default-configurable** — `FeedbackAiDisclaimerComponent` has a default text but accepts an `@Input() text` override for future specialization per activity type.

---

## Hard Constraints — Verified

| Constraint | Status |
|-----------|--------|
| No new activity formats | ✓ None added |
| No AI scoring logic changes | ✓ Unchanged |
| No speaking/writing mastery signal changes | ✓ Unchanged |
| No CEFR update from feedback | ✓ Not touched |
| No objective completion from feedback | ✓ Not touched |
| No LP regeneration from feedback | ✓ Not touched |
| No placement logic changes | ✓ Not touched |
| No provider evaluation rule changes | ✓ Not touched |
| No enterprise org work | ✓ Not started |
| No real-time conversation | ✓ Not started |
| No full student UI redesign | ✓ Not started |
| No live providers in tests | ✓ All tests use mocked data and `NEVER`/`of()` observables |

---

## Test Coverage

| Spec | Tests | Result |
|------|-------|--------|
| `feedback-ai-disclaimer.component.spec.ts` | 2 | Pass |
| `feedback-pending-state.component.spec.ts` | 6 | Pass |
| `feedback-next-steps.component.spec.ts` | 12 | Pass |
| `feedback-skill-context.component.spec.ts` | 5 | Pass |
| `feedback-support-lang.component.spec.ts` | 7 | Pass |
| `feedback-writing-eval.component.spec.ts` | 15 | Pass |
| `activity-feedback-page.component.spec.ts` | 22 | Pass |
| Angular full unit suite | 1483/1602 | 119 pre-existing failures, 0 new regressions |
| Angular production build | — | Clean |

---

## AskUserQuestion Answers

None required. All decisions within engineering scope.

---

## Implementation Tasks Produced

None. All Phase 18B scope is complete.

---

## Risks and Unresolved Questions

| Risk | Severity | Status |
|------|----------|--------|
| Pre-existing 119 Angular unit failures (`getStudentWritingEvaluations` not in spy) | Low | Pre-existing; not introduced here |
| Writing eval endpoint returns 404 for old attempts with no eval record | Low | Frontend handles gracefully: `writingEvaluationLoading` is set to false on error; no eval block shown |
| Writing eval polling max 15 polls (2 min ceiling) | Low | Consistent with speaking poll ceiling; student can reload if needed |

---

## Final Verdict

**PASS.** All Parts (A–N) implemented and verified. Build clean. 69 new tests added. No new regressions. All hard constraints honoured.

---

## Next Recommended Action

- Monitor writing evaluation completion rates in production to validate the 8s / 15-poll polling budget.
- Phase 18C scope to be determined by product.
