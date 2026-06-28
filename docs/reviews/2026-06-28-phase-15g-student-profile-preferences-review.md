---
title: Phase 15G — Student Profile and Preferences Functional Integration
date: 2026-06-28
sprint: Phase 15G
status: complete
---

# Phase 15G — Student Profile and Preferences Functional Integration

Date: 2026-06-28

## Related sprint

Phase 15G — final main student page completing the full student navigation set (Dashboard, Today, Practice, Journey, Progress, Profile).

---

## Files reviewed

### Frontend

- `src/LinguaCoach.Web/src/app/features/student/profile/profile.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/profile/profile.component.html`
- `src/LinguaCoach.Web/src/app/features/student/profile/profile.component.spec.ts`
- `src/LinguaCoach.Web/src/app/core/services/profile.service.ts`
- `src/LinguaCoach.Web/src/app/core/services/placement.service.ts`
- `src/LinguaCoach.Web/src/app/core/models/placement.models.ts`
- `src/LinguaCoach.Web/e2e/profile.spec.ts` (new)

### Backend (read-only, confirmed wiring)

- `src/LinguaCoach.Application/Profile/ProfileQueries.cs`
- `src/LinguaCoach.Application/Profile/ProfileCommands.cs`
- `src/LinguaCoach.Infrastructure/Profile/ProfileCommandHandler.cs`
- `src/LinguaCoach.Infrastructure/Profile/ProfileQueryHandler.cs`
- `src/LinguaCoach.Api/Controllers/ProfileController.cs`

### Admin spec (pre-existing failures fixed)

- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`

---

## Scope

**Functional integration only — no visual redesign.**

Connected the student Profile page to real backend data. Students can safely update supported learning preferences. CEFR level is permanently read-only in the UI. Placement summary pulled from existing placement endpoints and rendered on the profile page.

---

## Findings grouped by priority

### P0 — CEFR read-only contract

The backend `UpdateLearningPreferencesCommand` already excludes `CefrLevel`. The frontend profile template has no input or select inside `data-testid="level-section"`. The template shows an explicit explainer: "Your level is updated through placement, learning progress, and teacher/admin review." Playwright test confirms: `level-section` contains zero `input` or `select` elements. Contract enforced at all three layers (backend command, Angular template, Playwright assertion).

### P1 — Placement summary in profile

`PlacementService.getAdaptiveCurrent()` (GET `/api/student/placement/current`) and `PlacementService.getPlacementConfig()` (GET `/api/student/placement/config`) are called in `ngOnInit`, both wrapped with `catchError(() => of(null))`. Placement load failure is non-fatal and does not break the rest of the profile page. `placementLoading` signal gates the template render.

Badge logic: `isProvisional === false` → `data-testid="confirmed-badge"`. `isProvisional === true` → `data-testid="provisional-badge"`. Retake button gated by `placementConfig().allowPlacementRetake`. When retake is disabled (default), `data-testid="retake-not-available"` is shown instead.

### P1 — Preference update flow

`ProfileService.updatePreferences()` (PUT `/api/profile/preferences`) is the only write path. `ProfileCommandHandler` fires `ILearningPlanService.RegeneratePlanAsync("preference_change")` as fire-and-forget after save — plan regeneration failure does not surface to the student.

### P2 — Pre-existing admin spec failures fixed

110 Angular tests in `admin-student-detail.component.spec.ts` were failing due to Phase 15F adding `getStudentProgressSummary()` to the component without updating the 15 Jasmine spy setups. Fixed by adding `AdminStudentProgressSummary` import, a `makeProgressSummary()` factory, and the method + return value to all 15 spy blocks.

---

## Implementation tasks produced

1. **Profile component** — `PlacementService` and `Router` injected. Three new signals: `placement`, `placementConfig`, `placementLoading`. `ngOnInit` calls both placement endpoints. `navigateToPlacement()` method added.

2. **Profile template** — Placement summary section added between Level and Learning Goals sections. Test IDs: `placement-summary-section`, `no-placement-message`, `placement-in-progress`, `continue-placement-button`, `confirmed-badge`, `provisional-badge`, `placement-date`, `skill-breakdown`, `retake-placement-button`, `retake-not-available`. CEFR protection notice text updated.

3. **Profile spec** — `PlacementService` spy added; `provideRouter([])` added to providers. 13 new test cases (7 placement-related, existing tests updated). Total: 38 tests (was 25).

4. **E2E spec** — `src/LinguaCoach.Web/e2e/profile.spec.ts` created. 10 Playwright tests covering: page load, CEFR read-only, placement summary, retake disabled, learning goals, support language, notification prefs, save button, no raw JSON, CEFR protection notice.

5. **Admin spec fix** — 110 pre-existing failures resolved. All 15 `createSpyObj` calls updated.

---

## AskUserQuestion decisions

None required. Specification was unambiguous on all key questions:
- CEFR: read-only always
- AI prompts: never shown to students
- Retake button: gated by `allowPlacementRetake` (default false)
- Placement load failure: non-fatal, graceful degradation

---

## Build and test results

| Suite | Result |
|---|---|
| Backend (arch + unit + integration) | 2,732 pass |
| Angular unit (Karma) | 1,464 pass |
| Playwright E2E (profile spec) | 10/10 pass |
| Playwright E2E (full suite) | 229 pass, 3 skipped |

No failures. No regressions in existing specs.

---

## Risks and unresolved questions

- **Retake flow**: `navigateToPlacement()` routes to `/placement`. The placement page itself must handle the retake context (e.g., distinguish first-time vs. retake). Not in scope for this phase.
- **Notification preferences**: Backend endpoint is mocked in E2E; actual backend wiring was pre-existing and not changed in this phase.
- **Admin parity**: Admin can inspect CEFR, learning goals, focus areas, support language, difficulty, and session length via existing `GET /api/admin/students/{id}/profile`. No new admin endpoint was needed.

---

## Final verdict

**Complete.** All Phase 15G parts (A–N) delivered. Student Profile page is fully functional. CEFR read-only contract enforced at all layers. All tests pass. Documentation updated.

---

## Next recommended action

Phase 15H or sprint close — all six main student pages (Dashboard, Today, Practice, Journey, Progress, Profile) are now functionally complete.
