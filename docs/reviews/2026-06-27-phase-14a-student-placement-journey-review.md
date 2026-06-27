# Phase 14A — Student Placement Journey (End-to-End) — Engineering Review

**Date:** 2026-06-27
**Sprint:** Phase 14A
**Status:** Complete — all tests pass, no regressions

---

## Overview

Phase 14A delivers the first complete student-facing vertical slice on top of the adaptive placement engine (Phases 13A/13B). A student who logs in for the first time now flows end-to-end: First Login → Placement Assessment → Adaptive Questions → Placement Complete → Learning Plan Generated → Today Lesson Unlocked.

---

## Files reviewed / modified

### Backend

| File | Change |
|------|--------|
| `src/LinguaCoach.Application/Placement/PlacementAssessmentOptions.cs` | Added 5 config flags: `PlacementRequiredBeforeLearning`, `AllowSkipPlacement`, `AllowPlacementRetake`, `ResumeInterruptedPlacement`, `AutoStartPlacement` |
| `src/LinguaCoach.Api/appsettings.json` | Added defaults for all 5 new flags inside `"PlacementAssessment"` block |
| `src/LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs` | Added lifecycle transitions: `PlacementInProgress` on start, `PlacementCompleted` on finalise |
| `src/LinguaCoach.Api/Controllers/StudentPlacementController.cs` | **New.** 7 endpoints for student-facing adaptive placement |
| `src/LinguaCoach.Api/Controllers/AdminPlacementController.cs` | Added `POST .../abandon` and `POST .../expire` admin actions |
| `tests/LinguaCoach.IntegrationTests/Api/StudentPlacementControllerTests.cs` | **New.** 17 integration tests |

### Frontend

| File | Change |
|------|--------|
| `src/LinguaCoach.Web/src/app/core/models/placement.models.ts` | Added 6 new adaptive interfaces |
| `src/LinguaCoach.Web/src/app/core/services/placement.service.ts` | Added 7 new adaptive service methods; all old methods kept |
| `src/LinguaCoach.Web/src/app/features/student/placement/placement.component.ts` | Rewritten: adaptive state machine (`loading/welcome/question/submitting/completing/done/error`), signal-based, question-by-question flow |
| `src/LinguaCoach.Web/src/app/features/student/placement/placement.component.html` | Rewritten: progress bar, MCQ choice buttons, gap_fill input, CEFR result card, skill breakdown grid |
| `src/LinguaCoach.Web/src/app/features/student/placement/placement.component.spec.ts` | Rewritten: 19 unit tests for new adaptive flow |
| `src/LinguaCoach.Web/src/app/core/guards/placement.guard.ts` | Fixed `placementAccessGuard` to redirect completed students to `/dashboard` |
| `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` | Added `abandonPlacement` and `expirePlacement` methods |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` | Added `abandoningPlacement`/`expiringPlacement` signals and methods |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html` | Added Abandon/Expire buttons to placement card (InProgress only) |

---

## Findings by priority

### Critical (fixed during implementation)

**C1 — Missing lifecycle transitions in PlacementAssessmentService.**
The service had no lifecycle transitions on start or completion. `StartAssessmentAsync` did not set `PlacementInProgress`; `FinalizeCompletionAsync` did not set `PlacementCompleted`. Without this, `placementRequiredRedirectGuard` would redirect students to `/placement` forever even after completing. Fixed: both transitions added directly in the service at the correct points.

**C2 — placementAccessGuard allowed completed students into /placement.**
Guard had no check for post-placement lifecycle stages. Fixed: `completedStages` array (`PlacementCompleted`, `CourseReady`, `InLesson`, `ActiveLearning`, `Paused`, `Archived`) redirects to `/dashboard`.

### Medium (design decisions)

**M1 — Old placement service methods preserved.**
`PlacementService.getStatus()` → `/api/placement/status` is still called by both `placementRequiredRedirectGuard` and `placementAccessGuard`. Replacing those calls would have required guard rewrites. Decision: keep all 6 old methods; add 7 new adaptive methods alongside them. Old section-based flow coexists safely.

**M2 — No AI evaluation in student flow.**
By spec constraint: all scoring is deterministic. The 72-item seeded bank from Phase 13B handles all item selection and scoring. No AI calls during student placement.

**M3 — No migration needed.**
`StudentLifecycleStage.PlacementInProgress` (value 5) and `PlacementCompleted` (value 6) already existed in the enum and `lifecycle_stage` column. Phase 14A adds no schema changes.

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| `POST /api/student/placement/start` returns HTTP 201 | New resource created; consistent with REST convention |
| `start` returns 409 when already completed and `AllowPlacementRetake=false` | Clear conflict error; UI shows a message rather than silently redirecting |
| `resume` returns existing in-progress or starts new | Single endpoint reduces frontend complexity for the interrupted-placement case |
| Lifecycle transitions in service, not controller | Service is the authoritative boundary; admin and student paths both go through it |
| Gap fill uses `(ngModelChange)` + signal | Angular 17 standalone component with Signals; avoids two-way binding on a signal directly |

---

## Tests produced

| Suite | Tests | Result |
|-------|-------|--------|
| `StudentPlacementControllerTests` (integration) | 17 | Pass |
| `placement.component.spec.ts` (Karma/Jasmine) | 19 | Pass |
| Pre-existing backend suite | 2,673 | Pass (no regressions) |

**Total after Phase 14A:** 3 arch + 1,493 unit + 1,194 integration = **2,690 backend**. 19 new Angular placement specs.

---

## Risks and unresolved questions

| Risk | Status |
|------|--------|
| Old placement section flow still live alongside adaptive flow | Acceptable short-term; old endpoints are guarded and not surfaced to new students. Cleanup is Phase 14B or later. |
| `AllowPlacementRetake` defaults to `false` | Intentional — retake not in scope for 14A. Config flag wired; no UI for retake flow yet. |
| `autoStartPlacement` defaults to `false` | Config wired; `begin()` in placement component respects it. No auto-start UX built. |
| Learning plan regeneration on placement completion | Triggered by existing service call in `FinalizeCompletionAsync`; failure is caught/logged, never blocks placement. |

---

## Final verdict

**Ship.** All 11 parts of the Phase 14A spec implemented. No schema migration needed. All tests pass. The student placement journey is end-to-end: from `PlacementRequired` lifecycle stage through adaptive questions to `PlacementCompleted` and dashboard redirect. Admin gets abandon/expire actions on in-progress assessments.

---

## Next recommended action

Phase 14B — Student dashboard "Today's Lesson" unlock: verify `CourseReady` lifecycle transition post-placement, surface first lesson card on dashboard, smoke-test the full First Login → Dashboard flow end-to-end with Playwright.
