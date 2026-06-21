# Phase 10U-8 — AI Usage CSV Export — Engineering Review

**Date:** 2026-06-21
**Sprint:** Phase 10U-8
**Commit:** phase 10u ai usage csv export
**Reviewer:** Claude Sonnet 4.6

---

## Related sprint

`docs/sprints/current-sprint.md`

---

## Files changed

### Backend
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — added `GetExportAsync` to `IAdminAiUsageHandler`
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — implemented `GetExportAsync`; reuses `ApplyDateFilter` + `ApplyColumnFilter`; applies 10 000-row cap via `Take(maxRows)`
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — added `GET /api/admin/ai-usage/export.csv` action with `BuildCsv` and `CsvEscape` helpers
- `tests/LinguaCoach.IntegrationTests/Api/AiUsageExportTests.cs` — 16 new integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — added `exportUsageCsv(range?, filters?): Observable<Blob>`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
  - Added `exporting` and `exportError` signals
  - Added `exportCsv()` method; uses `inject<Document>(DOCUMENT)` to create anchor element for blob download
  - Added `SpAdminAlertComponent` to imports
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html`
  - Export error alert (`<sp-admin-alert variant="error">`) shown above recent calls card when `exportError()` is truthy
  - "Export CSV" / "Exporting…" button in the filter bar using `<sp-admin-button>`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — 6 new export tests; `exportUsageCsv` added to spy

---

## Findings by priority

### P0 — Resolved

**`DOCUMENT` token is from `@angular/common`, not `@angular/core`.**
Fixed. Also needed `inject<Document>(DOCUMENT)` explicit type parameter to satisfy TypeScript strict mode.

**`require('rxjs').Subject` in test caused Karma load error.**
Fixed by importing `Subject` from `rxjs` at the top of the spec.

### P1 — Design decisions

**No `file-saver` dependency — browser native `URL.createObjectURL` + anchor click.**
Keeps the dependency surface clean. Works in all modern browsers. Tested via spy.

**10 000-row cap applied server-side via `Take(maxRows)` before materialisation.**
Protects against accidental large memory allocation. Default passed as named parameter `maxRows: 10_000` for clarity.

**CSV escaping: RFC 4180.**
`CsvEscape` wraps in quotes when value contains `,`, `"`, `\n`, or `\r`. Internal quotes doubled. Tested with `Timeout,"quoted"` failure reason.

**No pagination params sent on export.**
`exportUsageCsv(range, filters)` signature has no `page`/`pageSize`. Verified by test checking `args.length === 2`.

**Content-Disposition filename includes timestamp.**
`ai-usage-yyyyMMdd-HHmmss.csv` from `File()` return value on the server. Frontend also generates matching timestamp for the anchor `download` attribute.

**Export button always visible (not conditional on active filters).**
Useful for full unfiltered exports. Disabled and shows "Exporting…" text while in progress.

**Export error shown via `sp-admin-alert variant="error"`.**
Cleared on next successful export. Does not interfere with summary or recent-calls error states.

---

## CSV columns

`CreatedAt, Provider, Model, FeatureKey, StudentId, WasSuccessful, IsFallback, FailureReason, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, CorrelationId`

Sensitive data excluded: no user PII beyond `StudentId` (opaque GUID). No email, no name, no content.

---

## Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors) |
| `dotnet test --configuration Release` | PASS (775/775 integration + 1248/1248 unit + 3/3 arch) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (847/847) |

---

## Backend tests added

`AiUsageExportTests.cs` — 16 tests:
- `Export_Returns200WithCsvContentType`
- `Export_HasFilenameContentDispositionHeader`
- `Export_CsvIncludesExpectedHeader`
- `Export_CsvIncludesDataRows`
- `Export_ProviderFilter_OnlyIncludesThatProvider`
- `Export_FeatureKeyFilter_OnlyIncludesThatFeature`
- `Export_StatusSuccess_OnlyIncludesSuccessNonFallbackRows`
- `Export_StatusFailed_OnlyIncludesFailedRows`
- `Export_StatusFallback_OnlyIncludesFallbackRows`
- `Export_StudentIdFilter_OnlyIncludesThatStudentsRows`
- `Export_DateFilter_FutureFromReturnsOnlyHeader`
- `Export_CsvEscapesCommasAndQuotesInFailureReason`
- `Export_InvalidStatus_Returns400`
- `Export_InvalidStudentId_Returns400`
- `Export_InvertedDateRange_Returns400`
- `Export_ModelFilter_OnlyIncludesThatModel`

## Frontend tests added

6 new tests in `admin-ai-usage.component.spec.ts`:
- `exportCsv calls exportUsageCsv with current filters`
- `exportCsv sends date range but no page or pageSize`
- `exportCsv sets exporting signal to true then false on success`
- `export button is disabled while exporting`
- `exportCsv shows error alert on failure`
- `exportCsv passes student filter to service`

---

## Risks / unresolved questions

- Row cap is 10 000 — hardcoded default. Could be a query param in future if needed.
- No progress indicator for large exports (just button text change). Acceptable for admin tool.

---

## Final verdict

**SHIP.** All gates pass. No regressions. Export uses same filter helper as summary/recent.

---

## Next recommended action

Phase 10U-8 complete. AI Usage admin feature set (date/pagination/column-filters/student/summary-alignment/CSV-export) is fully shipped.
