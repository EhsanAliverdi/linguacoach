# Phase 10U-7 — AI Usage Summary Filter Alignment — Engineering Review

**Date:** 2026-06-20
**Sprint:** Phase 10U-7
**Commit:** phase 10u ai usage summary filter alignment
**Reviewer:** Claude Sonnet 4.6

---

## Related sprint

`docs/sprints/current-sprint.md`

---

## Files changed

### Backend
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — `IAdminAiUsageHandler.GetSummaryAsync` signature updated to accept `AiUsageRecentFilter? columnFilter`
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — `GetSummaryAsync` now calls `ApplyColumnFilter` (renamed from `ApplyRecentFilter`); shared helper used by both summary and recent
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — `GetSummary` endpoint extended with `provider`, `model`, `featureKey`, `status`, `studentId` query params; same validation logic as `GetRecent`; `BuildFilter` renamed to `BuildDateFilter`
- `tests/LinguaCoach.IntegrationTests/Api/AiUsageSummaryFilterTests.cs` — 14 new integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — `getSummary(range?, filters?)` — accepts and forwards all column filters to `/summary`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
  - Extracted `buildColumnFilters()` private helper
  - `load()` now passes active column filters to `getSummary`
  - All filter change handlers (`onRecentProviderChange`, `onRecentModelChange`, `onRecentFeatureChange`, `onRecentStatusChange`, `onRecentStudentChange`) now call `load()` instead of `loadRecent()` — reloads both summary and recent calls
  - `clearRecentFilters()` now calls `load()` — clears filters then reloads both
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — conditional helper text "Summary totals reflect active filters." shown when `hasActiveRecentFilters()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — 10 new tests; 3 existing tests updated to match new 2-arg `getSummary` call signature

---

## Findings by priority

### P0 — Resolved

**`ApplyRecentFilter` used only by `GetRecentAsync` — not shared.**
Fixed by renaming to `ApplyColumnFilter` and calling it from both `GetSummaryAsync` and `GetRecentAsync`. Single implementation, no duplication.

### P1 — Design decisions

**Filter change handlers call `load()` not `loadRecent()`.**
`load()` calls `getSummary` + `loadRecent()` together. This ensures summary and recent table always reflect the same slice. Side effect: each column filter change triggers two HTTP calls (summary + recent). Acceptable for an admin-only page; volume is low.

**`buildColumnFilters()` private helper.**
Extracts filter signal reads into one place. Called by `load()` (for summary) and `loadRecent()` (for recent). Keeps both in sync without repeating signal reads.

**Helper text "Summary totals reflect active filters."**
Shown only when `hasActiveRecentFilters()` is true. Uses existing muted style class. No new UI component needed.

**Status semantics preserved exactly from 10U-5.**
`ApplyColumnFilter` is the same code path — `success` = WasSuccessful && !IsFallback, `failed` = !WasSuccessful, `fallback` = IsFallback.

**Invalid studentId / invalid status return 400 for summary endpoint.**
Same validation as `GetRecent` — copied GUID parse + `HasInvalidStatus` check into `GetSummary` action.

---

## Decisions made

- No migration. No new columns. No schema change.
- Provider routing unchanged.
- Usage governance unchanged.
- `clearRecentFilters` does not reset date period — period is a separate concern (summary period filter bar vs column filter bar).

---

## Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors) |
| `dotnet test --configuration Release` | PASS (759/759 integration + 1248/1248 unit + 3/3 arch) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (841/841) |

---

## Backend tests added

`AiUsageSummaryFilterTests.cs` — 14 tests:
- `Summary_ProviderFilter_ReturnsTotalsForThatProviderOnly`
- `Summary_ProviderFilter_ExcludesOtherProviders`
- `Summary_ModelFilter_ReturnsTotalsForThatModelOnly`
- `Summary_FeatureKeyFilter_ReturnsTotalsForThatFeatureOnly`
- `Summary_StatusSuccess_ReturnsOnlySuccessNonFallbackLogs`
- `Summary_StatusFailed_ReturnsOnlyFailedLogs`
- `Summary_StatusFallback_ReturnsOnlyFallbackLogs`
- `Summary_InvalidStatus_Returns400`
- `Summary_StudentIdFilter_ReturnsTotalsForThatStudentOnly`
- `Summary_UnknownStudentId_ReturnsZeroTotals`
- `Summary_InvalidStudentId_Returns400`
- `Summary_ProviderFilter_UpdatesTokenTotals`
- `Summary_ProviderFilter_UpdatesCostTotals`
- `Summary_DateAndStudentId_Combined_WorkTogether`

## Frontend tests added/updated

New (10U-7):
- `onRecentProviderChange also reloads getSummary with provider filter`
- `onRecentStatusChange also reloads getSummary with status filter`
- `onRecentStudentChange also reloads getSummary with studentId filter`
- `onRecentModelChange also reloads getSummary with model filter`
- `onRecentFeatureChange also reloads getSummary with featureKey filter`
- `clearRecentFilters reloads getSummary with no column filters`
- `clearRecentFilters does not change the date period`
- `period change reloads getSummary and getRecent together`
- `summary cards still render after filter alignment change`
- `getSummary receives both date range and column filters when both active`

Updated (signature fix):
- `default load calls getSummary without date params` — checks `args[0]` not full call signature
- `onPeriodChange to last7days passes a from date to getSummary` — reads `args[0]` not `args`
- `onPeriodChange to last30days / today / month` — same

---

## Risks / unresolved questions

- Each column filter change now triggers 2 HTTP calls (summary + recent). Acceptable for admin-only low-volume page.
- Student select still capped at 50 (from 10U-6). Not addressed in this phase.

---

## Final verdict

**SHIP.** All gates pass. No regressions. Summary and recent calls now reflect the same filter slice.

---

## Next recommended action

Phase 10U-7 complete. AI Usage admin feature set (date/pagination/column-filters/student/summary-alignment) is now fully shipped.
