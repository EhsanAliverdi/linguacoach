# Phase 10Students-F-C — Targeted Lifecycle Controls Review

**Date:** 2026-06-19
**Sprint:** Phase 10Students-F-C
**Feature:** Admin targeted lifecycle controls — Pause, Unpause, Reactivate

---

## Files reviewed and changed

### Backend

- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — commands and interface
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs` — handler methods
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — HTTP endpoints

### Frontend

- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — API service
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` — detail component

### Tests

- `tests/LinguaCoach.IntegrationTests/Api/AdminEndpointTests.cs` — 9 new integration tests
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` — 16 new frontend tests

---

## Findings by priority

### Implemented

1. Three new commands added: `ReactivateStudentCommand`, `PauseStudentCommand`, `UnpauseStudentCommand` — each carrying `AdminUserId` for audit.
2. Three handler methods in `AdminHandler` — follow exact shape of `ArchiveStudentAsync`. Each writes an `AdminAuditLog` entry (action: Reactivate/Pause/Unpause).
3. Three controller endpoints: `POST /api/admin/students/{id}/reactivate`, `/pause`, `/unpause`. Not-found vs. bad-request split correctly via `ex.Message.Contains("not found")`.
4. Lifecycle guard rules enforced in handler:
   - Reactivate: requires `Archived`.
   - Pause: rejects `Archived` or already `Paused`.
   - Unpause: requires `Paused`.
5. Reactivate sets `user.EmailConfirmed = true` (mirrors archive setting it false).
6. Unpause and Reactivate both transition to `OnboardingRequired` (safe re-entry point).
7. Frontend service exposes `reactivateStudent`, `pauseStudent`, `unpauseStudent`.
8. Detail component shows context-sensitive buttons: Reactivate (Archived only), Unpause (Paused only), Pause (any other active stage).
9. Inline confirm modal with cancel/confirm, saving state, and error display.
10. On success: `loadStudent` called to refresh view; toast shown.

### Not changed

- `ArchiveStudentCommand` remains without `AdminUserId` (no audit log on archive — pre-existing gap, not in scope).
- No migration added — `Paused` stage (value 10) already in `StudentLifecycleStage` enum.
- `window.confirm` in `confirmArchive` and `confirmRemovePolicy` left in place (not in scope for this phase).

---

## Decisions made

- Unpause and Reactivate both land on `OnboardingRequired` (not the stage the student was in before). This is the safe conservative choice — admin can use Reset Data for finer control.
- Audit log written for all three new actions. Archive still has no audit log (pre-existing).
- Frontend modal is inline (no `SpAdminSlideOverComponent`) — consistent with edit/reset modals already in the component.

---

## Implementation tasks produced

None — all tasks delivered in this phase.

---

## Risks and unresolved questions

- Audit log for `Archive` is still missing. Recommend adding in a follow-up.
- `window.confirm` still used for Archive and Remove Policy. Recommend modal replacement in a UI polish pass.
- Unpause/Reactivate both set `OnboardingRequired` — if the student was mid-placement, this forces re-onboarding. Admin can mitigate with Reset Data → custom preset.

---

## Final verdict

All implementation complete. Build clean. 680 integration tests pass (9 new). 734 frontend tests pass (16 new). No migration required.

## Next recommended action

Mark Phase 10Students-F-C done in sprint doc. Next: consider audit log for Archive, or proceed to next planned phase.
