# Phase 10Students-F-F — Server-Side Student Pagination, Filtering, Sorting Review

**Date:** 2026-06-19
**Related sprint:** Phase 10Students-F-F
**Review type:** Engineering implementation review

---

## Files Reviewed

### Backend
- `src/LinguaCoach.Application/Admin/AdminQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminController.cs`
- `tests/LinguaCoach.IntegrationTests/Api/AdminEndpointTests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/AdminManagementEndpointTests.cs`

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts`

---

## Findings by Priority

### P0 — Breaking change handled correctly

- `GET /api/admin/students` now returns `PagedResponse<StudentListItem>` (an object with `items`, `totalCount`, `page`, `pageSize`, `totalPages`) instead of `StudentListItem[]`.
- All downstream consumers updated: `admin-students.component.ts` (server-driven), `admin-dashboard.component.ts` (reads `r.items`), all spec files.
- Two existing integration tests in `AdminManagementEndpointTests.cs` were failing because they called `.EnumerateArray()` on the top-level response object. Fixed by reading `.GetProperty("items")` first.

### P1 — Implementation decisions

- `ListStudentsPagedAsync` joins profiles with identity users in memory (same pattern as existing `ListStudentsAsync`) so email search works without a cross-database join. For the pilot scale this is acceptable; revisit if student counts exceed ~5000.
- `pageSize` capped at 100 in both the controller (via `Math.Clamp`) and the handler (via `Math.Min`). Double-safeguard.
- Sort by name uses concatenated display name string; for a pilot list this is fine.
- Unknown `sortBy` values fall back to `createdAt` descending — no 400 returned for unknown sort keys.
- `LifecycleStage` and `OnboardingStatus` filters use `Enum.TryParse` with `ignoreCase: true`; invalid values are silently ignored (no match returned is the safe default).

### P2 — Preserved behaviours

- `GET /api/admin/students/{id}` (detail endpoint) unchanged.
- `GET /api/admin/students/{id}/audit-history` unchanged.
- All existing row actions (edit, reset password, reset data, archive) reload the current page via `load()` after success.
- Include-archived toggle resets page to 1 before reloading.
- Search input resets page to 1 before reloading.
- Sort header click resets page to 1 before reloading.

### P3 — Not added (out of scope)

- Lifecycle, onboarding, CEFR filter dropdowns in the UI (backend params exist; UI selects not added in this phase).
- Bulk operations.
- Student-facing changes.
- New DB migration (not required).

---

## Decisions Made

1. Keep `ListStudentsAsync(bool includeArchived)` on the interface for backward compatibility (used by other internal callers if any). Added `ListStudentsPagedAsync` as a second method.
2. Dashboard still calls `listStudents({ pageSize: 100 })` to get a list for its recent students panel. This is acceptable at pilot scale; a dedicated stats endpoint is preferred long-term.
3. Frontend search is debounce-free — every keystroke triggers `onSearchChange` which calls the API. For pilot user counts this is fine; add RxJS `debounceTime` when latency becomes noticeable.

---

## AskUserQuestion Answers

None required. All decisions resolved per spec.

---

## Implementation Tasks Produced

None — phase is complete as specified.

---

## Risks / Unresolved Questions

- In-memory user join + search is O(n) over all profiles. At pilot scale (< 200 students) this is negligible. At scale, move email search into EF with a join on the Identity table.
- Dashboard loads up to 100 students for its panel. If the panel only needs a summary, a dedicated lightweight stats endpoint would be more efficient.

---

## Build / Test Results

- `dotnet build --configuration Release`: **succeeded**, 0 errors, 7 pre-existing warnings.
- `dotnet test --configuration Release`: **1944 passed** (3 architecture + 1237 unit + 704 integration), 0 failed.
- `ng build --configuration production`: **succeeded**.
- `ng test --watch=false --browsers=ChromeHeadless`: **756 passed**, 0 failed.

---

## Final Verdict

Phase 10Students-F-F is complete and all gates green.

---

## Next Recommended Action

Phase 10Students-F-G or equivalent: add lifecycle/onboarding/CEFR filter selects to the admin students UI to expose the backend filter params already implemented.
