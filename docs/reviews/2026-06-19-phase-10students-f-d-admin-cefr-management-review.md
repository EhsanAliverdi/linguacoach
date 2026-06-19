# Phase 10Students-F-D — Admin CEFR Management: Engineering Review

**Date:** 2026-06-19
**Sprint:** Phase 10Students-F-D
**Reviewer:** Claude Code (engineering review)

---

## Related sprint

`docs/sprints/current-sprint.md` — Phase 10Students-F-D entry

---

## Goal

Allow admins to set or clear a student's CEFR level from the admin student detail page. Students must not be able to edit their own CEFR level.

---

## Files reviewed

- `src/LinguaCoach.Domain/Entities/StudentProfile.cs`
- `src/LinguaCoach.Domain/Entities/AdminAuditLog.cs`
- `src/LinguaCoach.Application/Admin/AdminQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminController.cs`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`
- `tests/LinguaCoach.IntegrationTests/Api/AdminEndpointTests.cs`

---

## Files changed

| File | Change |
|------|--------|
| `StudentProfile.cs` | Added `AdminSetCefrLevel(string? level)` method |
| `AdminQueries.cs` | Added `SetStudentCefrCommand` record; added `SetStudentCefrAsync` to `IAdminStudentQuery` |
| `AdminHandler.cs` | Implemented `SetStudentCefrAsync` with audit log write |
| `AdminController.cs` | Added `PUT /api/admin/students/{id}/cefr` endpoint + `SetStudentCefrRequest` record |
| `admin.api.service.ts` | Added `updateStudentCefr(id, cefrLevel, reason?)` method |
| `admin-student-detail.component.ts` | Added CEFR display badge, Set CEFR button, modal, signals, and save/cancel methods |
| `admin-student-detail.component.spec.ts` | Added 9 new CEFR management tests |
| `AdminEndpointTests.cs` | Added 5 new integration tests (set, clear, invalid, 404, audit log) |

---

## Findings

### Architecture

- Existing `SetCefrLevel(string level)` on `StudentProfile` throws on null/empty — not suitable for admin clear. New `AdminSetCefrLevel(string? level)` handles both set and clear, keeping the two operations semantically distinct. Placement-driven method is unchanged.
- `ClearPlacementResult()` continues to handle the reset-flow clear path. Admin CEFR management is a separate administrative action with its own audit trail.
- Handler follows exact same pattern as `ReactivateStudentAsync` and `PauseStudentAsync`: load profile, mutate via domain method, write `AdminAuditLog`, `SaveChangesAsync`.
- Audit entry includes `oldValueJson`, `newValueJson`, and `reason`. Uses the JSON string literal format already used by other audit entries (no extra dependencies).

### Security

- Endpoint is admin-only via `[Authorize(Roles = nameof(UserRole.Admin))]` inherited from `AdminController`.
- Student-facing profile endpoint (`ProfileController`) is not modified. `UpdateLearningPreferences` already excludes `CefrLevel` (confirmed by unit test `UpdateLearningPreferences_DoesNotChangeCefrLevel` in `StudentProfileLearningPreferencesTests`).
- No CEFR controls added to student-facing components.

### Validation

- Input normalised: trimmed, uppercased before validation.
- Valid values: A1, A2, B1, B2, C1, C2 (case-insensitive on input).
- Null or empty string clears the level.
- Invalid non-empty value returns 400 via `ArgumentException` from domain method.
- Missing student returns 404 via `InvalidOperationException` caught in controller.

### Migration

- No migration required. `CefrLevel` column already exists on `StudentProfiles` table (added in a prior sprint). `AdminAuditLog` table already exists.

### Frontend

- CEFR display shows a badge when set; "Not set" muted text otherwise.
- Helper text: "CEFR is controlled by assessment and admin. Students cannot edit this."
- "Set CEFR" button opens a modal (same pattern as assign policy modal).
- Dropdown includes A1–C2 and "Clear / Not set" option.
- Optional reason field.
- On success: `getStudent` reloaded, toast shown.
- On error: inline error in modal.
- No CEFR controls added to preferences slide-over or any student-facing component.

---

## Decisions

### Why CEFR is admin/assessment-only

CEFR level drives curriculum routing, activity difficulty banding, and skill progression. Allowing students to self-report CEFR would corrupt routing quality. CEFR must come from:
1. Placement assessment (automatic, via `SetCefrLevel`).
2. Admin override (this feature, via `AdminSetCefrLevel`).

Student-authored `LearningPreferences` fields are intentionally separated and never touch `CefrLevel` (enforced by domain method and existing unit test).

### Why a separate domain method

`SetCefrLevel` throws on null, because placement always produces a value. Admin clear is a distinct operation. Using a separate `AdminSetCefrLevel(string? level)` keeps intent explicit without changing placement semantics.

### Why modal over slide-over

The assign policy modal pattern already exists in this component and is lightweight. A full slide-over would be heavier for a 2-field form. Modal is consistent with existing assign policy and edit forms.

---

## Risks

- None identified. The change is narrowly scoped to admin-only path. No migration, no schema change, no placement logic touched.

---

## Final verdict

Implementation complete. All gates pass.

- Backend build: clean (0 errors, 7 pre-existing warnings)
- Backend tests: 1925 passing (685 integration, 1237 unit, 3 architecture)
- Frontend build: clean
- Frontend tests: 743 passing

---

## Next recommended action

Phase 10Students-F-E or next backlog item. No blockers.
