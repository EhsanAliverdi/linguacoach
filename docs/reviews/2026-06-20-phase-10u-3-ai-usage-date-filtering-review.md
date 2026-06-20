# Phase 10U-3 — AI Usage Date Filtering — Engineering Review

**Date:** 2026-06-20
**Related sprint:** Phase 10U-3 (current sprint)
**Related review:** docs/reviews/2026-06-20-phase-10u-1-2-ai-pricing-config-token-totals-review.md

---

## Files reviewed / changed

### Backend
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — Added `AiUsageDateFilter` record and updated `IAdminAiUsageHandler` interface signatures
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — Both query methods updated to accept and apply date filter; `ApplyDateFilter` helper added
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — Added `from`/`to` query params to summary and recent endpoints; `BuildFilter` helper with `IsInverted` guard; 400 on invalid range
- `tests/LinguaCoach.IntegrationTests/Api/AiUsageDateFilterTests.cs` — New file; 12 integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — Added `AiUsageDateRange` interface; both service methods accept optional range and forward as query params
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — Added `PeriodPreset` type (exported), period signals, `periodOptions`, `load()`, `onPeriodChange()`, `buildRange()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — Period preset `sp-admin-select` added in `sp-admin-filter-bar` above stat grid
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — Added 12 period preset tests

---

## Findings

### P0 — None

### P1 — None

### P2 — Resolved during implementation

**`PeriodPreset` type placement bug:** The `export type` declaration was initially placed between the `@Component` decorator closing bracket and the `export class` keyword — invalid TypeScript. Fixed by moving it to the top of the file (after the import block), before the decorator.

### P3 — Design notes

**`From` inclusive, `To` exclusive** semantics are consistent with standard half-open interval convention. The `IsInverted` guard returns 400 when `From >= To`, which covers both the equal and reversed cases.

**No custom date range inputs** in this phase per spec. Users pick from: All time, Today, Last 7 days, Last 30 days, This month. All presets produce only a `from` (no `to`), which means they cover up to "now". This is correct for current-period dashboards.

**`buildRange` uses UTC** for day and month boundaries (`Date.UTC(...)`) to avoid local-timezone drift when the admin's browser timezone differs from server UTC.

**Integration test seeding** backdates `created_at` via `ExecuteSqlRawAsync` because `BaseEntity.CreatedAt` has `protected set`. Static `_seeded` + `SemaphoreSlim(1,1)` ensures one-time seeding per `ApiTestFactory` instance, avoiding duplicate seed rows across parallel test runs.

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| No `to` param for period presets | Presets anchor on a start time and implicitly end at "now"; adding `to` would complicate buildRange with no UX benefit |
| `AiUsageDateFilter.None` sentinel | Allows callers to pass an explicit "no filter" object without null; also aids testability |
| `AiUsageDateFilter.IsInverted` guard on controller | Single responsibility: controller owns HTTP validation, handler owns query logic |
| Period select above stat grid | User reads summary first; filtering above the stats means the preset affects both summary and recent in one mental action |

---

## Implementation tasks produced

None. Phase complete.

---

## Risks / unresolved questions

- No user timezone support: All date boundaries are UTC. For a user in UTC-8, "Today" at midnight UTC is actually 4pm their previous day. Deferred — acceptable for admin-internal tooling.
- No custom date range UI. If needed, a future 10U-4 phase can add a date picker pair using the same `AiUsageDateRange` contract — backend already accepts arbitrary `from`/`to`.

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS — 0 errors, 7 pre-existing warnings |
| `dotnet test --configuration Release` | PASS — 1967/1967 (arch 3, unit 1248, integration 716) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 805/805 |

Test count delta vs 10U-1/10U-2:
- Backend: +12 (date filter integration tests)
- Frontend: +11 (period preset component tests)

---

## Final verdict

Phase 10U-3 is complete. No scope creep. No migration. No provider routing or usage governance change. All gates green.

## Next recommended action

Ready to commit and push. Commit message: `phase 10u-3 ai usage date filtering`
