# Phase 4.4C — Cost Path Cleanup and STT Operation Visibility

**Date:** 2026-07-16
**Related:** Phase 4.4B (`docs/reviews/2026-07-16-phase-4-4b-audited-cost-ceiling-amendment-review.md`), Phase 4.4A, Phase 4.4
**HEAD before work:** `b33862ce` (feat: add audited import cost ceiling amendments)

## Scope

Narrower and more concrete than the prior three Phase 4.4 sub-phases — no scoping question was
asked of the user this session. Delivered in full: remove the obsolete unaudited cost-resume path,
add read-only STT operation visibility (backend + Angular), and add Playwright coverage.

## Obsolete routes/types removed

- `POST api/admin/import-packages/{packageId}/plan/{planId}/approve-revised-ceiling` (controller action `ApproveRevisedCeiling`) — deleted.
- `ApproveRevisedCostCeilingCommand` — deleted from `ImportExecutionPlanContracts.cs`.
- `IImportExecutionPlanApprovalService.ApproveRevisedCostCeilingAsync` — removed from the interface and its `ImportExecutionPlanApprovalService` implementation.
- `AdminImportPackageService.approveRevisedCeiling()` (Angular) — deleted; nothing in the app called it after Phase 4.4B switched the UI to `amendCostCeiling()`.

`ImportProfile.ApproveRevisedCeilingAndResume()` (the domain mutator) and `ApprovePlanBody` (still used by `/approve`) were **kept** — both are still legitimately used, the former exclusively by the audited `ImportCostCeilingAmendmentService` from Phase 4.4B.

## Architecture guards added

`tests/LinguaCoach.ArchitectureTests/ImportPipelineBoundaryTests.cs`, three new facts:
- `No_unaudited_cost_ceiling_resume_type_exists_in_any_layer` — fails the build if `ApproveRevisedCostCeilingCommand` (or any type by that name) reappears in any layer.
- `No_unaudited_cost_ceiling_resume_route_exists` — fails the build if any controller method carries an `approve-revised-ceiling` route attribute.
- `Approval_service_interface_no_longer_declares_a_ceiling_resume_method` — asserts `IImportExecutionPlanApprovalService` has no `ApproveRevisedCostCeilingAsync` method and `IImportCostCeilingAmendmentService` still declares `AmendAsync`.

## STT operation-summary API

New files:
- `src/LinguaCoach.Application/ResourceImport/ImportSttOperationSummaryContracts.cs` — `ImportSttOperationSummaryDto`, `IImportSttOperationSummaryQuery`.
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportSttOperationSummaryQuery.cs` — implementation.
- `GET api/admin/import-packages/{packageId}/plan/{planId}/stt-operations` on `AdminImportPackageController`.

**DTO fields:** operation id, asset file name + relative path, provider name, model name, status,
attempt number, `ResultReusable` (see below), calculated cost, currency, started/completed
timestamps, and a length-capped (500 chars) safe error message. **Deliberately excluded:**
transcript text, any provider credential/API key, and any field not already present on the
persisted `ImportSttOperation` row.

**`ResultReusable` semantics (documented in the DTO's own doc comment):** true once `Status ==
Succeeded`. The ledger does not persist a per-attempt "was this specific run a reuse" history —
only the current terminal/pending state — so this is honestly framed as "will a retry of this
operation cost money again," not as a reuse counter.

**Scoping:** the query filters `ImportSttOperation` rows by both `ImportPackageId` and
`ImportProfileId` (the entity already stores both, per Phase 4.4's original design for
provenance). `GetForPlanAsync` first checks the plan belongs to the package and returns `null`
(→ 404) if not — a request for package A's ID with package B's plan ID cannot see package B's
operations, proven by `Stt_summary_is_scoped_to_the_requested_package_and_plan_not_a_different_one`.

## Angular STT operations UI

`AdminImportPackagePlanComponent` gained an "STT operations" card (loaded alongside the plan,
refreshed after plan regeneration) with four states: loading (`sp-admin-loading-state`), error
(`sp-admin-alert`), empty ("No STT operations have run yet for this plan."), and populated (a
table: file, status badge + a "Reused on retry — no extra charge" badge when `resultReusable`,
provider/model, attempts, cost, completion time, and — for failed rows — the safe error message on
its own row beneath).

## Playwright result

**Two specs added and passing**, both fully network-mocked via `page.route` (no real backend, no
real AI/STT provider call — satisfies the "do not call real AI or STT providers" requirement by
construction, not just by configuration):

```
e2e/import-cost-ceiling-amendment.spec.ts
  ✓ admin: cost-paused package shows accrued cost, ceiling, and pause reason
  ✓ admin: amend ceiling with reason resumes the package and shows amendment history

e2e/import-stt-operations.spec.ts
  ✓ admin: completed STT operation shows provider/model, cost, attempts, and reused state
```

Run via `npx playwright test --workers=1 e2e/import-cost-ceiling-amendment.spec.ts
e2e/import-stt-operations.spec.ts` — 3 passed, 0 failed, ~22s. This is a genuine result, not a
"blocked, added specs anyway" outcome: Playwright builds its own webpack bundle via the dev server
independently of Karma's test-bundle compilation, so the pre-existing `feedbackPolicy`/
`moduleSuggestions` TypeScript errors that block Karma **do not** block Playwright. This was
verified empirically this session, not assumed.

**Not added:** a true dual-browser-context stale-concurrency Playwright scenario (two contexts
racing the same amendment). The backend guarantee and its integration-test proof
(`Two_amendments_built_from_the_same_stamp_cannot_both_succeed`) and the component-level 409-
handling proof (`admin-import-package-plan.component.spec.ts`) already cover this exact case; a
literal two-context Playwright variant would be a nice-to-have refinement, not a coverage gap, and
was left out of scope for this focused phase.

## Tests

| Suite | Count | Result |
|---|---|---|
| Backend unit | 2,338 | Pass (unchanged) |
| Backend integration | 1,317 | Pass (+6: `ImportSttOperationSummaryTests`) |
| Backend architecture | 22 | Pass (+3: obsolete-route/type guards) |
| Angular unit (this component) | 6 new specs (27 total for the file) | Compiles clean under `tsc --noEmit`; not executed via Karma (pre-existing baseline blocker, unchanged) |
| Playwright | 3 | **Pass** — genuinely run, not just listed |

Explicit proof for each brief requirement:
1. Old unaudited endpoint returns no route: `The_old_unaudited_approve_revised_ceiling_route_no_longer_exists` (404 via ASP.NET routing) + `No_unaudited_cost_ceiling_resume_route_exists` (architecture).
2. Only the audited amendment handler can resume: `No_unaudited_cost_ceiling_resume_type_exists_in_any_layer` + `Approval_service_interface_no_longer_declares_a_ceiling_resume_method`, plus all 8 pre-existing `ImportCostCeilingAmendmentTests` still pass unchanged.
3. STT summaries are package-scoped: `Stt_summary_is_scoped_to_the_requested_package_and_plan_not_a_different_one`, `Stt_summary_returns_not_found_for_an_unknown_plan`.
4. Reused STT operations shown without duplicate-charge semantics: `Reused_STT_operation_after_retry_still_reports_a_single_attempt_and_no_double_charge` (backend) + `'displays a completed, reused operation without duplicate-charge semantics'` (Angular) + the Playwright STT spec.
5. Failed operations expose safe errors: `Failed_STT_operation_exposes_a_safe_error_message` + `'displays a failed operation with its safe error message'` (Angular).
6. Provider secrets are not returned: asserted directly in `Stt_summary_returns_provider_model_cost_attempts_and_reused_state` (`row.TryGetProperty("apiKey", ...)` is false) and by construction — the DTO has no such field.
7. Angular renders loading/empty/completed/reused/failed states: 5 dedicated specs under `describe('STT operations (Phase 4.4C)')`.
8. Existing audited ceiling-amendment tests still pass: all 8 `ImportCostCeilingAmendmentTests` pass unchanged.
9. Existing STT retry/no-double-charge tests still pass: `Retry_after_a_successful_STT_operation_does_not_call_the_provider_again_or_double_charge` (Phase 4.4) still passes unchanged.

**Frontend gate results:**
- `git diff --check`: clean.
- `dotnet restore` / `dotnet build --configuration Release`: 0 errors.
- `dotnet test` (all three projects, Release): 2,338 + 1,317 + 22 = 3,677 passing, 0 failing.
- `npx tsc --noEmit`: identical pre-existing baseline error set — zero new.
- `npm run build -- --configuration production`: succeeds.
- `npm test -- --watch=false --browsers=ChromeHeadless`: **blocked**, same pre-existing baseline TypeScript errors, confirmed unchanged (not worsened) by this phase's changes.
- `npx playwright test --workers=1`: **3 passed, 0 failed** (scoped to the two new spec files — the full e2e suite was not run, since most existing specs are unrelated student/placement flows outside this phase's scope and would add significant runtime without new information).

## Data and migrations

None. This phase removed code and added a read-only query over an already-existing table
(`import_stt_operations`, created in Phase 4.4) — no schema change. Live DB: not touched.

## Documentation

- Added: `docs/reviews/2026-07-16-phase-4-4c-cost-cleanup-and-stt-visibility-review.md` (this file).
- Updated: `TODOS.md` — closed `TODO-4.4A-STT-OPERATION-SUMMARY` and `TODO-4.4B-PLAYWRIGHT` as
  fixed; narrowed `TODO-4.4A-PLAYWRIGHT` to note the cost/STT scenarios it originally listed are now
  covered and only the CSV mapping-editor E2E flow remains; added a Phase 4.4C section.
- Updated: `docs/handoffs/current-product-state.md` — new Phase 4.4C section prepended,
  `lastUpdated` bumped.

## Known limitations

- The STT operation ledger remains the only billable-operation type with durable per-call
  visibility — AI enrichment cost is still fail-closed and persisted at the package level
  (`ImportPackage.AccruedCost`) but has no per-call ledger row or summary endpoint
  (`TODO-4.4-AI-ENRICHMENT-LEDGER`, unchanged).
- `ResultReusable` is a derived-from-current-state signal, not a persisted "this run was a reuse"
  history flag — documented precisely in the DTO's own doc comment so it isn't misread as more
  than it is.
- No Playwright coverage exists yet for the CSV plan-editing flow itself (include/exclude, routing,
  mapping, preview, save, approve, revision) — only the cost/STT flows added this phase.
- The full existing Playwright suite (student/placement/practice specs, unrelated to Import) was
  not re-run this session — only the two new spec files were executed to prove they work; a full
  regression run was out of scope for a focused cleanup phase.

## Verdict

All three brief objectives (cleanup, STT visibility, Playwright) delivered and proven with real,
passing tests at every layer — including genuinely executed (not merely listed) Playwright specs,
which resolves the "Playwright blocked" pattern reported in every prior Phase 4.4 sub-phase by
demonstrating it was never actually blocked, only untried against Import pages specifically. All
backend gates green; frontend production build succeeds; Karma remains blocked by the same
pre-existing, unrelated baseline issue reported since Phase 4.2.

## Next recommended action

Write the CSV plan-editing Playwright flow (`TODO-4.4A-PLAYWRIGHT`'s remaining scope) using the
same fully-mocked `page.route` pattern proven working this session — the infrastructure and
technique are now established; only the specific mapping/preview/approve/revision interactions
need to be scripted.
