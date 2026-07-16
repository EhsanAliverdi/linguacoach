# Phase 4.4B — Audited Cost-Ceiling Amendment and Cost Operations UI

**Date:** 2026-07-16
**Related:** Phase 4.4A (`docs/reviews/2026-07-16-phase-4-4a-admin-plan-editor-and-cost-ui-review.md`), Phase 4.4, Phase 4.3
**HEAD before work:** `d0438a64` (feat: add admin import plan editor and cost controls)

## Scope decision

The full Phase 4.4B brief specified: the audited ceiling-amendment backend, a full Angular cost
panel (summary, amendment form, amendment history, STT operation summary table), 14 backend/
frontend tests, and a Playwright spec.

Before starting, the user was asked to scope the session via `AskUserQuestion` and selected
**"Backend + core Angular amendment flow"**: ship the audited entity/migration/endpoint
(concurrency-checked, validates paused-for-cost + new-greater-than-current-ceiling, resume only on
success) plus the Angular amendment form and cost summary (accrued/ceiling/remaining/pause
reason/amendment history), replacing the old `approve-revised-ceiling` call — deferring the STT
operation-summary UI and Playwright, tracked as `TODO-4.4B-PLAYWRIGHT` (the STT summary was
already tracked from 4.4A as `TODO-4.4A-STT-OPERATION-SUMMARY`, now fixed-target for a future
session).

## Files and migration changed

**New:**
- `src/LinguaCoach.Domain/Entities/ImportCostCeilingAmendment.cs` — immutable audit entity.
- `src/LinguaCoach.Application/ResourceImport/ImportCostCeilingAmendmentContracts.cs` — command, DTO, service interface.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportCostCeilingAmendmentService.cs` — validation + amend + resume + audit-persist.
- `src/LinguaCoach.Persistence/Configurations/ImportCostCeilingAmendmentConfiguration.cs`
- `src/LinguaCoach.Persistence/Migrations/20260715234258_Phase_4_4B_CostCeilingAmendments.cs` (+`.Designer.cs`, `ModelSnapshot.cs` updated)
- `tests/LinguaCoach.IntegrationTests/Api/ImportCostCeilingAmendmentTests.cs` — 8 facts.

**Modified:**
- `src/LinguaCoach.Api/Controllers/AdminImportPackageController.cs` — new `POST .../plan/{planId}/amend-ceiling` route + `AmendCostCeilingBody`.
- `src/LinguaCoach.Application/ResourceImport/ImportExecutionPlanContracts.cs` — `ImportExecutionPlanDto` gained `AccruedCost`, `AccruedCostCurrency`, `RemainingCeiling`, `CeilingAmendments`.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportExecutionPlanApprovalService.cs`, `ImportExecutionPlanGenerationService.cs`, `ImportPlanDraftService.cs` — all three `ToDto` builders became async (`ToDtoAsync`) to load amendment history and populate the new cost fields.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportPlanDtoHelpers.cs` — new `LoadCeilingAmendmentsAsync` helper.
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `IImportCostCeilingAmendmentService`.
- `src/LinguaCoach.Persistence/Configurations/ImportProfileConfiguration.cs` — `ConcurrencyStamp` marked `IsConcurrencyToken()`.
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — new `DbSet<ImportCostCeilingAmendment>`.
- `src/LinguaCoach.Web/src/app/core/models/admin-import-package.models.ts` — new fields + `ImportCostCeilingAmendmentDto`.
- `src/LinguaCoach.Web/src/app/core/services/admin-import-package.service.ts` — new `amendCostCeiling()`, `approveRevisedCeiling()` marked `@deprecated`.
- `admin-import-package-plan.component.ts`/`.html`/`.spec.ts` — new "Cost details" card, reworked resume modal, 5 new specs.

## Amendment lifecycle

```
Plan PausedForCostApproval (pre-existing, Phase 4.4's own cost-ceiling check)
        ↓
Admin opens "Amend ceiling and resume" (from Plan status or the Cost details pause alert)
        ↓
Admin enters new ceiling (> current) + reason, confirms
        ↓
POST .../plan/{planId}/amend-ceiling { expectedConcurrencyStamp, newApprovedCostCeiling, reason }
        ↓
Service validates: concurrency stamp match → plan.Status == PausedForCostApproval →
                    newCeiling > currentCeiling → reason non-blank
        ↓
ImportCostCeilingAmendment row created + plan.ApproveRevisedCeilingAndResume() +
package.MoveToStatus(Queued) — all staged in one SaveChangesAsync
        ↓
Success → package resumes (Executing/Queued) ; Failure at any check → nothing persisted, package stays paused
```

No step in this chain can be skipped or reordered — every validation happens before any mutation, and the mutation + audit-row insert are one atomic save.

## Concurrency mechanism

Two layers, not one:
1. **Application-level check** (same pattern as Phase 4.4's draft-update/approve): the loaded
   `plan.ConcurrencyStamp` is compared to the caller's `ExpectedConcurrencyStamp` before any
   mutation — catches the common case (a second, later request against already-stale data).
2. **New this phase — EF-level concurrency token:** `ImportProfile.ConcurrencyStamp` is now
   configured with `.IsConcurrencyToken()`. EF Core includes the originally-loaded value in every
   `UPDATE`'s `WHERE` clause; if a concurrent transaction already changed it, the `UPDATE` affects
   zero rows and `SaveChangesAsync` throws `DbUpdateConcurrencyException`. `AmendAsync` catches
   this specifically and maps it to the same `ImportPlanConcurrencyConflictException` → HTTP 409
   the application-level check produces, after re-querying the row's current stamp so the client
   gets an accurate "reload" target.

This second layer closes the one gap layer 1 cannot: two requests that both read the same stamp
before either commits. This is a systemic strengthening of `ImportProfile`'s concurrency model
(the column was already always regenerated on every mutator — Phase 4.4B just tells EF to
*enforce* it at the database layer too), not something narrowly bolted onto the amendment path
alone. Verified safe for the rest of Phase 4.4/4.4A's plan-editing/approval flows by running the
**entire** existing test suite after the change — all 1,303 pre-existing integration tests plus
2,338 unit tests still pass unchanged.

**Known test-environment limitation:** a literal `Task.WhenAll` double-POST against this test
host's SQLite in-memory connection throws `"SqliteConnection does not support nested
transactions"` rather than exercising the intended race (SQLite's test-harness connection doesn't
support two concurrent transactions the way pooled Postgres connections would in production). The
"two concurrent amendments" test (`Two_amendments_built_from_the_same_stamp_cannot_both_succeed`)
therefore issues the two requests sequentially, both built from the same originally-read stamp —
which is exactly what two admins racing from the same loaded page would send, and is what the
concurrency token guarantees regardless of request timing. See the test's own doc comment for the
full reasoning.

## Angular cost UI

New "Cost details" section card on the plan page (always visible once a plan exists, not only
while paused): approved ceiling, accrued cost (backend-calculated, "actual" not "estimated"),
remaining ceiling, and — while `PausedForCostApproval` — the pause reason plus an inline "Amend
ceiling and resume" button. An amendment-history table (when non-empty) shows every past amendment:
timestamp, previous → new ceiling, reason.

The resume modal now requires a reason (validated client-side before the request is even sent) and
displays the current ceiling for context; on success it refreshes the whole plan (including the
just-added amendment in history and the new accrued/remaining figures); on a 409 it shows the same
reload-guidance pattern as draft save/approval (`resumeConcurrencyConflict` signal), never
silently retrying or merging.

## STT summary

**Not built this phase** — explicitly deferred per the scoping decision (`TODO-4.4A-STT-OPERATION-SUMMARY`, unchanged from Phase 4.4A).

## Critical proof

Automated tests proving the required chain:

```
Cost-paused package → explicit ceiling amendment → audited update → controlled continuation
```
- `Package_resumes_only_after_a_successful_amendment` — package/plan status confirmed post-amendment.
- `Failed_amendment_does_not_resume_processing` — a rejected amendment (ceiling not raised) leaves the package paused, zero amendment rows created.
- `Amendment_preserves_previous_and_new_ceilings_actor_reason_and_currency_in_audit_history` — every audited field asserted directly against the persisted row.
- `Two_amendments_built_from_the_same_stamp_cannot_both_succeed` — exactly one of two same-stamp attempts succeeds; exactly one audit row exists.
- `Plan_detail_returns_the_full_cost_summary_and_amendment_history` — the read path (`GET .../plan`) returns the same cost/history data the write path produced.
- Frontend: `admin-import-package-plan.component.spec.ts` → `'submits the new ceiling, reason, and current concurrency stamp'`, `'shows stale-conflict guidance on a 409 without resuming'`, `'refreshes package/plan state after a successful amendment'`.

## Tests

| Suite | Count | Result |
|---|---|---|
| Backend unit | 2,338 | Pass (unchanged) |
| Backend integration | 1,311 | Pass (+8: `ImportCostCeilingAmendmentTests`) |
| Backend architecture | 19 | Pass (unchanged) |
| Angular unit (this component) | 5 new specs (21 total for the file) | Compiles clean under `tsc --noEmit`; **not executed** via Karma — see below |
| Playwright | 0 | Not added — deferred (`TODO-4.4B-PLAYWRIGHT`) |

**Gate results:**
- `git diff --check`: clean (one benign CRLF-normalization warning on the migration snapshot file).
- `dotnet restore` / `dotnet build --configuration Release`: 0 errors.
- `dotnet test` (all three projects, Release): 2,338 + 1,311 + 19 = 3,668 passing, 0 failing.
- `npx tsc --noEmit`: identical pre-existing baseline error set to Phase 4.4A (`feedbackPolicy`, `moduleSuggestions`, unrelated `e2e/*.spec.ts` files) — zero new errors, confirmed by diffing the exact error list against the pre-Phase-4.4B baseline.
- `npm run build -- --configuration production`: succeeds.
- `npm test -- --watch=false --browsers=ChromeHeadless`: **blocked**, same pre-existing baseline TypeScript errors abort the Karma bundle before any spec (including this phase's) can run — not introduced or worsened this phase.
- `npx playwright test`: not run — no specs added.

## Data and migrations

One additive migration, `20260715234258_Phase_4_4B_CostCeilingAmendments`: a single new table
(`import_cost_ceiling_amendments`) with two FKs (cascade) and two indexes — no column changes, no
data backfill, no existing table touched. Live DB: **not touched**, consistent with every prior
phase. Existing data: **unchanged**.

## Documentation

- Added: `docs/reviews/2026-07-16-phase-4-4b-audited-cost-ceiling-amendment-review.md` (this file).
- Updated: `TODOS.md` — closed `TODO-4.4A-CEILING-AMENDMENT-AUDIT` and `TODO-4.4A-COST-SUMMARY-PANEL` as fixed; added a Phase 4.4B section with `TODO-4.4B-PLAYWRIGHT`.
- Updated: `docs/handoffs/current-product-state.md` — new Phase 4.4B section prepended, `lastUpdated` bumped.

## Known limitations

- STT operation-ledger visibility in the admin UI still does not exist.
- No Playwright coverage for this flow, or any Import admin flow — deferred since Phase 4.2.
- The pre-existing `approve-revised-ceiling` endpoint/service method was left in place (marked
  superseded in a doc comment) rather than removed — it still works, unaudited, unconcurrency-
  checked, simply unused by the current UI. A future cleanup could remove it once confident nothing
  else depends on it.
- The "two concurrent requests" test exercises sequential-but-same-stamp requests, not literal
  simultaneous dispatch, due to a SQLite test-harness limitation (documented above and in the
  test's own comment) — the underlying EF concurrency-token guarantee does not depend on request
  timing to hold, but this specific test cannot directly observe true parallel contention.
- Currency is always `USD` in current data (matches the plan's own `Currency` field, itself always
  `"USD"` today per Phase 4 defaults) — the amendment entity stores currency defensively but no
  multi-currency path exists anywhere in the Import pipeline to actually exercise a mismatch.

## Verdict

Core audited-amendment workflow delivered end-to-end (backend + Angular) and proven with real
persistence-backed tests, including the specific "two same-stamp requests, only one wins" proof
the brief required. A genuine correctness gap in the existing concurrency model (app-checked only,
no DB-level guarantee) was identified and closed as a direct, verified-safe consequence of building
this feature correctly, not narrowly scoped to just this one endpoint. All backend gates green.
Frontend production build succeeds; Karma remains blocked by the same pre-existing, unrelated
baseline issue reported in every prior phase this series.

## Next recommended action

`TODO-4.4A-STT-OPERATION-SUMMARY` is now the largest remaining gap in cost-operation visibility —
STT is still the only ledgered billable operation and nothing in the admin UI surfaces it.
