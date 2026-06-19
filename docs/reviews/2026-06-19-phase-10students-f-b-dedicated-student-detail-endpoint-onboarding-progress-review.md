# Phase 10Students-F-B — Dedicated Student Detail Endpoint + Onboarding Progress

**Date:** 2026-06-19
**Sprint:** Phase 10Students-F-B
**Related sprint doc:** docs/sprints/current-sprint.md

---

## Scope

Deliver a dedicated `GET /api/admin/students/{id}` endpoint returning full student detail including onboarding progress. Wire the Angular `admin-student-detail` component to call this endpoint. Fix the SQLite integration test blocker introduced by the previous agent.

---

## Files reviewed / changed

### Backend
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs` — `GetStudentDetailAsync` implemented; `OrderByDescending(p => p.StartedAt)` removed (unique index guarantees at most one row; SQLite does not support `DateTimeOffset` in ORDER BY)
- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — `AdminStudentDetailDto`, `StudentOnboardingProgressInfo` records
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — `GET /api/admin/students/{studentId:guid}` route
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — xmin config already guarded by Npgsql provider check; no change needed
- `src/LinguaCoach.Persistence/Configurations/StudentOnboardingProgressConfiguration.cs` — xmin registered in `OnModelCreating`, not here; no change needed

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/AdminEndpointTests.cs` — 6 new integration tests; FK blocker fixed by resolving active `OnboardingFlowDefinition` ID from DB
- `tests/LinguaCoach.IntegrationTests/Api/ApiTestFactory.cs` — added `OnboardingFlowSeeder.SeedAsync(db)` to `EnsureCreatedAsync`

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — `AdminStudentDetail`, `StudentOnboardingProgressInfo` models
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — `getStudent(id: string): Observable<AdminStudentDetail>`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` — loads via `getStudent(id)` from route param; loading/error states; onboarding progress section
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` — tests fixed: `require('rxjs')` replaced with typed `Subject<AdminStudentDetail>`, `displayName: null` added so firstName/lastName fall through in title

---

## xmin blocker — root cause and resolution

**What the previous agent believed:** `StudentOnboardingProgress.xmin` concurrency token would break SQLite.

**Actual root cause:** Two distinct issues:

1. `GetStudentDetailAsync` used `OrderByDescending(p => p.StartedAt)` on a `DateTimeOffset` column. SQLite throws `NotSupportedException: SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses`. Since there is a unique index on `UserId`, at most one row exists; the `OrderByDescending` was removed entirely.

2. The `ReturnsOnboardingProgressWhenRowExists` integration test seeded `StudentOnboardingProgress` with a random `Guid.NewGuid()` as `flowDefinitionId`. No `OnboardingFlowDefinition` row existed in the test database (FK constraint), so `SaveChanges` threw `SQLite Error 19: FOREIGN KEY constraint failed`. Fixed by: (a) adding `OnboardingFlowSeeder.SeedAsync(db)` to `ApiTestFactory.EnsureCreatedAsync`, and (b) querying the active flow's ID from the DB before seeding progress.

**The xmin configuration itself was already correct** — guarded by `if (Database.ProviderName?.Contains("Npgsql") == true)` in `OnModelCreating`, so SQLite never saw the `xmin` column.

---

## Findings by priority

### P0 — Resolved
- `NotSupportedException` on `DateTimeOffset ORDER BY` in SQLite: fixed by removing redundant sort.
- FK constraint failure in test: fixed by seeding `OnboardingFlowDefinition` in test factory.
- Frontend spec compile error (`require('rxjs')`): fixed by importing `Subject` from `rxjs`.
- Frontend test logic error (`displayName` override): fixed by passing `displayName: null` in override.

### P1 — None
### P2 — None

---

## Decisions made

- Do not ORDER BY `StartedAt` — unique index makes it unnecessary; avoids SQLite incompatibility.
- Do not add SQLite-specific workarounds to production handler code.
- `OnboardingFlowSeeder` is idempotent; safe to call in `EnsureCreatedAsync`.
- `AdminStudentDetailDto` includes `onboardingProgress: StudentOnboardingProgressInfo?` (null when no row exists).

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors) |
| `dotnet test --configuration Release` | PASS (1911/1911: 671 integration, 1237 unit, 3 architecture) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (719/719) |

---

## Implementation tasks produced

None — phase complete.

---

## Risks and unresolved questions

- None. The unique index on `student_onboarding_progress.user_id` is enforced at DB level; the handler correctly returns the single row or null.

---

## Final verdict

COMPLETE. All gates pass. No lifecycle controls, no CEFR editing, no pagination, no student-facing changes, no migration added, no commit/push.

---

## Next recommended action

Proceed to Phase 10Students-F-C or next sprint item per backlog.
