# Phase 10Students-F-E — Student Audit / History Tab Review

**Date:** 2026-06-19
**Sprint:** Phase 10Students-F-E
**Related sprint doc:** docs/sprints/current-sprint.md

---

## Files reviewed

### Backend
- `src/LinguaCoach.Domain/Entities/AdminAuditLog.cs`
- `src/LinguaCoach.Domain/Entities/StudentResetLog.cs`
- `src/LinguaCoach.Application/Admin/AdminQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminController.cs`
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs`

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/AdminEndpointTests.cs`

---

## Findings by priority

### P0 — No blockers found

### P1 — Design decisions

**Audit sources:** Two log entities cover distinct actions:
- `AdminAuditLog` — governance audit trail (SetCefr, Archive, lifecycle changes, policy assignments). FK link: `TargetStudentId`.
- `StudentResetLog` — lifecycle reset records. FK link: `StudentProfileId`.

Both existed in the DB. No migration was needed.

**Actor email:** Not stored on either log entity. Left null in the DTO. No extra DB call per row. Consistent with the spec requirement.

**Timestamp handling:** `AdminAuditLog.CreatedAt` is `DateTime` (UTC, from BaseEntity). Converted to `DateTimeOffset` via `new DateTimeOffset(dt, TimeSpan.Zero)`. `StudentResetLog.PerformedAtUtc` is also `DateTime`; same conversion applied.

**Ordering:** Combined in memory after two separate queries (cannot join across heterogeneous tables in LINQ). 50-item cap applied after sort. Acceptable given low expected volume per student.

**Password safety:** No password, secret, or credential field is present on either log entity. Response DTO has no such fields.

### P2 — Implementation notes

- `GetStudentAuditHistoryAsync` returns `null` when student not found (matches `GetStudentDetailAsync` pattern). Controller maps null → 404.
- Empty list returns 200, not 404. Correct per spec.
- Reset log action string is human-readable: `"Reset: {PreviousStage} → {NewStage}"`.
- Details field carries `ClearedItemsJson` for reset logs. Long details (>80 chars) show a "View details" button opening `sp-admin-slide-over`. Short details show inline code pill.

---

## Decisions made

1. Combine AdminAuditLog + StudentResetLog in memory (two queries) rather than union SQL — avoids type mismatch complexity.
2. Null actor email — no per-row user lookup. actorId (admin GUID prefix) shown instead.
3. 50-item limit applied in application layer after merge and sort.
4. "View details" slide-over threshold: 80 characters of details string.

---

## Implementation tasks produced

None — all tasks completed in this phase.

---

## Risks / unresolved questions

- If either log table grows very large per student, the in-memory merge of 50+50 rows before sorting is still safe, but a future phase could add DB-level union or pagination.
- Actor email join is not implemented. If actor email is needed in a future phase, it would require joining `AdminAuditLog.ActorAdminUserId` to the Users table.

---

## Final verdict

All gates pass. Feature is complete and correct.

- Backend build: PASSED (0 errors, 7 pre-existing warnings)
- .NET tests: 1932/1932 pass (692 integration + 1237 unit + 3 architecture)
- Frontend build: PASSED (production)
- Angular tests: 751/751 pass

---

## Next recommended action

Phase 10Students-F-F or next backlog item. Remaining student management TODOs: see TODOS.md (TODO-10X-DRAWER, TODO-10X-MODAL, TODO-10X-TOAST, TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT, etc.).
