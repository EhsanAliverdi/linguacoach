# Phase 15B — Consolidated Student Dashboard Summary API

**Date:** 2026-06-27
**Sprint:** Phase 15B
**Scope:** Dashboard orchestration and API consolidation — single summary endpoint replacing 4–5 separate calls.

---

## Files reviewed and changed

### New backend files
- `src/LinguaCoach.Application/Dashboard/StudentDashboardSummaryQuery.cs`
- `src/LinguaCoach.Infrastructure/Dashboard/StudentDashboardSummaryHandler.cs`
- `src/LinguaCoach.Api/Controllers/StudentDashboardController.cs`

### Modified backend files
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `IStudentDashboardSummaryHandler`

### New frontend files
- `src/LinguaCoach.Web/src/app/core/models/dashboard-summary.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/dashboard-summary.service.ts`

### Modified frontend files
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.spec.ts`
- `src/LinguaCoach.Web/e2e/today-lesson.spec.ts`
- `src/LinguaCoach.Web/e2e/today-page-identity.spec.ts`

### New test files
- `tests/LinguaCoach.IntegrationTests/Api/StudentDashboardSummaryTests.cs`

---

## Findings

### High priority — resolved before merge

**Section failure isolation:** Each optional section (session, practice, memory) wrapped in its own `try/catch`. A single section failure returns a named status string (`"NotAvailable"` or `"Preparing"`) rather than propagating to the HTTP layer. Core dashboard data failure propagates normally (400/403 depending on exception type).

**Practice signal null-vs-empty distinction:** Template uses `practiceSuggestions() === null` to render `practice-preparing` and empty arrays to render `practice-empty`. The new `applyFromSummary()` skips setting the signal when `practice.status === "NotAvailable"`, preserving this template behavior. This was discovered during Playwright test failures and fixed in the component.

**`IClassFixture` requirement for SQLite factories:** `ThrowingPracticeTestFactory` must be registered as `IClassFixture<T>`, not constructed inline. `IAsyncLifetime.InitializeAsync()` only runs for class fixtures — inline `new` leaves `_connection` null and DI registration fails. Fixed by creating a separate `StudentDashboardSummaryIsolationTests` class.

**Playwright mock endpoint migration:** Both `today-lesson.spec.ts` and `today-page-identity.spec.ts` mocked `**/api/dashboard`, `**/api/sessions/today`, and `**/api/learning-path/memory`. The component no longer calls those endpoints. Updated to mock `**/api/student/dashboard/summary` with equivalent structured responses.

---

## Decisions made

### Route naming
`GET /api/student/dashboard/summary` — scoped under `student/` to distinguish from the legacy `api/dashboard` endpoint which is retained for backward compatibility.

### Legacy endpoint retention
`GET /api/dashboard` (the old `DashboardController`) is retained. The summary endpoint wraps it internally. No API removal in this phase.

### Template unchanged
The Angular template HTML was intentionally kept unchanged. `applyFromSummary()` synthesizes the 4 existing signal types from the new DTO, so all template bindings continue to work.

### Session status string mapping
`SessionStatus` enum → string states: `Completed → "Completed"`, `InProgress → "InProgress"`, all others → `"Ready"`. Non-active lifecycle or failure → `"NotAvailable"`. Missing session data → `"Preparing"`. These 5 states are surfaced in the API contract and documented in the TypeScript model.

---

## Test coverage

| Test class | Tests | Validates |
|---|---|---|
| `StudentDashboardSummaryTests` | 8 | 200 response, section shapes, cross-tenant isolation, auth |
| `StudentDashboardSummaryIsolationTests` | 1 | Practice section `"NotAvailable"` on service failure |
| `DashboardComponent` (Karma) | 13 | Signal synthesis, session badge states, practice card states, error handling |
| `today-lesson.spec.ts` (Playwright) | 7+ | Session badge, CTA labels, lesson navigation |
| `today-page-identity.spec.ts` (Playwright) | 17+ | Page identity, route links, element presence |

---

## Build/test totals

| Suite | Count |
|---|---|
| Architecture tests | 3 |
| Unit tests | 1,504 |
| Integration tests | 1,208 |
| Angular unit tests | 1,413 |
| Playwright E2E | 24 (in targeted specs) |

All pass. Zero errors. Production build clean (warnings are pre-existing, unrelated to this phase).

---

## Risks and unresolved questions

- The legacy `GET /api/dashboard` endpoint is still called by the summary handler internally. It could be deprecated and inlined in a future cleanup phase.
- `practice-preparing` vs `practice-empty` template distinction is preserved by a null-signal convention. This convention is implicit — consider documenting it in the component or making the status explicit via a dedicated signal.

---

## Final verdict

Phase 15B complete. The consolidated summary endpoint reduces dashboard HTTP round trips from 4–5 to 1. Optional section failures degrade gracefully. Template unchanged. All tests pass.

## Next recommended action

Phase 15C or the Today Lesson Player — the lesson detail route (`/lesson/:id`) is now the primary CTA from the dashboard and is ready for a deeper interactive implementation.
