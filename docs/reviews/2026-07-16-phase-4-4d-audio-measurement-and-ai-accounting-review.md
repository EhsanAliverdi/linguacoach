# Phase 4.4D — Real Audio Measurement and AI Operation Accounting

**Date:** 2026-07-16
**Related:** Phase 4.4C (`docs/reviews/2026-07-16-phase-4-4c-cost-cleanup-and-stt-visibility-review.md`), Phase 4.4B, Phase 4.4A, Phase 4.4
**HEAD before work:** `e815e6ed` (refactor: remove legacy cost resume path)

## Scope decision

The full Phase 4.4D brief bundled two large, mostly-independent efforts: real audio-duration
measurement (a new external `ffprobe`-class dependency, remeasurement-on-checksum-change, wired
into estimates/ceiling/final cost) and a durable AI candidate-enrichment operation ledger
(generalizing the STT ledger pattern, with unified STT+AI cost-ceiling math and fail-closed
pricing).

Scoped via `AskUserQuestion` to **"AI operation ledger first"**: build the ledger, unified ceiling
enforcement, and admin visibility; defer real audio-duration measurement entirely (the flat
5-minute-per-file assumption is unchanged) — tracked as `TODO-4.4-AUDIO-DURATION-PROBE`
(re-confirmed, not newly deferred).

## Audio measurement implementation

**Not implemented this phase.** No `IAudioDurationProbe`/`AudioDurationProbe`, no `ffprobe`
dependency, no `ImportAsset` measurement fields. `ImportPackageProcessingService`'s STT path still
uses the constant `const decimal assumedMinutes = 5m;`. This is an explicit, user-confirmed scope
exclusion, not an oversight — see `TODOS.md`'s `TODO-4.4-AUDIO-DURATION-PROBE` entry.

## Runtime dependency

None added. No new external process, binary, or NuGet package. `AiExecutionResult` gained three
in-memory fields (`InputTokens`, `OutputTokens`, `CostUsd`) copied from `AiExecutionService`'s
already-computed internal `AiResponse` — a pure code change, no new dependency.

## AI operations covered

**Candidate enrichment only** — the one AI-calling operation type that runs after plan approval in
this codebase today (`ResourceCandidateAnalysisService.AnalyzeAsync`, called via
`ResourceCandidateBatchAnalysisService.AnalyzePendingForRunAsync`, called via
`ImportPackageProcessingService`'s structured-file loop). No other post-approval AI Import
operation exists in the current codebase to extend the ledger to — the general AI-enrichment
ledger scope from Phase 4.4's original brief is fully closed by this single operation type.

## Idempotency and concurrency design

**Operation identity** (`ImportAiEnrichmentOperationKey.Compute`): package + candidate +
`ResourceRawRecord.RawHash` (the candidate's content checksum) + assumed provider/model + prompt
version (`ResourceCandidateAnalysisService.AnalyzePromptKey`) + processing mode. Any material
change to any of these produces a different key — proven directly by
`A_materially_changed_input_produces_a_different_logical_key` (5 theory cases: checksum, provider,
model, prompt version, processing mode).

**Claim/mark lifecycle** (`ImportAiEnrichmentOperationLedger`), mirroring `ImportSttOperationLedger`
exactly:
- New key → insert `Pending` row (unique-index-protected; a `DbUpdateException` race defers to the
  winner row rather than risking two callers both proceeding).
- Existing `Succeeded` row → `AlreadySucceeded` — the stored `ResultReferenceJson` (the bounded,
  already-parsed analysis output — never the raw AI response body) is deserialized and re-applied
  to the candidate; the provider is never called and no cost is accrued again.
- Existing `Failed` row → `BeginRetry` (increments `AttemptNumber`, resets to `Pending`) — same
  unbounded-retry-per-pass policy the STT ledger already documents (single-active-worker
  assumption; Quartz clustering remains a separately-tracked, pre-existing deferral).
- `MarkSucceededAsync` mutates only (caller saves together with `package.AccrueCost` — one save,
  never drifts apart); `MarkFailedAsync` persists immediately (a failed call accrues no cost).

**Database-level dedup guarantee**: a unique index on `LogicalOperationKey`
(`ux_import_ai_enrichment_operations_logical_key`), guarded by
`Ai_enrichment_operation_logical_key_has_a_unique_index_configured` (architecture test) —
guarantees two workers can never both successfully claim-and-charge the same logical operation.

## Cost-ceiling behaviour

Checked **per candidate, before the provider call**, inside `ResourceCandidateAnalysisService.AnalyzeAsync`
(the actual call site — moved from the old whole-batch pre-check in `ImportPackageProcessingService`,
which is now removed):

```
projected = package.AccruedCost (persisted, durable) + one-candidate estimated cost
if projected > approvedCeiling * (1 + tolerance):
    do not call the provider
    return CeilingReached=true, PauseReason=<message>
    (the claimed Pending ledger row is left untouched — safely re-claimable once resumed)
```

`ResourceCandidateBatchAnalysisService.AnalyzePendingForRunAsync` stops the batch loop immediately
on the first `CeilingReached` result (remaining not-yet-processed candidates in that run are left
untouched, still eligible once processing resumes) and propagates the pause reason up through
`ImportPackageProcessingService.MapAndCreateCandidatesAsync`'s return value — the exact same
plan-pause mechanism (`ImportProfile.PauseForCostApproval`, `ImportPackage` status transition) the
STT path already uses, now shared by both.

**Only the billable AI-structuring modes are gated**: `requiresCostTracking = package.ProcessingMode
is not (null or ImportProcessingMode.Direct)`. This mirrors the pre-existing production gate — a
Direct-mode package's candidates are never routed through AI enrichment in production
(`ImportPackageProcessingService` only calls the batch analysis service when
`ProcessingMode != Direct`) — so calling `AnalyzeAsync` directly against a package with no
`ProcessingMode` set (every pre-existing unit test's fixture) never requires AI pricing to be
configured, preserving 100% backward compatibility with the pre-4.4D test suite.

**Fail-closed pricing**: unresolved pricing throws `ImportPricingUnavailableException` before any
provider call, proven by `Missing_pricing_prevents_the_provider_call` (asserts
`_provider.CallCount == 0` after the throw).

## STT + AI unified accrual

Both operation types call the same `ImportPackage.AccrueCost(amount, currency)` method, so
`ImportPackage.AccruedCost` is the single durable total regardless of which operation type
contributed. Proven directly by
`Stt_and_AI_costs_both_contribute_to_the_packages_single_accrued_cost_total` (0.03 STT + 0.02 AI =
0.05 total, read back via `GET .../plan`).

`ImportPackageProcessingService.MapAndCreateCandidatesAsync` re-syncs its local `runningCost`
variable from `package.AccruedCost` immediately after each AI-enrichment batch call, so the STT
ceiling check later in the same processing pass compares against the true combined total — not a
stale pre-AI-accrual snapshot. This was a real correctness gap the refactor would otherwise have
introduced (moving AI accrual out of the loop that previously kept `runningCost` in sync) and was
caught and fixed during implementation.

## UI changes

New "AI operations" card on the Import plan page (`AdminImportPackagePlanComponent`), placed
directly after the existing "STT operations" card, with the same four states (loading/error/empty/
populated): source label (candidate's canonical text, truncated), status badge (+ "Reused on retry
— no extra charge" badge when applicable), provider/model, attempt count, input/output tokens,
calculated cost, completion time, and a safe error message row for failed operations. Loaded
alongside the plan on initial load and refreshed after plan regeneration, mirroring the STT
section's exact lifecycle.

## Critical tests

All 17 brief items proven, split across unit (fake `IAiProvider`, no real/paid calls) and
integration (directly-seeded ledger rows, since no fake `IAiProvider` is registered in the API test
host — see Known Limitations):

| # | Requirement | Test |
|---|---|---|
| 1 | Actual audio duration replaces the fixed assumption | **N/A this phase** — deferred with audio measurement |
| 2 | Stored duration reused when checksum unchanged | **N/A this phase** — deferred |
| 3 | Changed checksum causes remeasurement | **N/A this phase** — deferred |
| 4 | Corrupt audio fails clearly | **N/A this phase** — deferred |
| 5 | Measured duration changes the plan estimate | **N/A this phase** — deferred |
| 6 | Identical successful AI operation reused after retry | `Identical_successful_AI_operation_is_reused_after_retry_no_second_provider_call_no_duplicate_cost` |
| 7 | Retry does not call the AI provider twice | same test — `_provider.CallCount.Should().Be(1)` after retry |
| 8 | Retry does not add duplicate cost | same test — `package.AccruedCost` unchanged after retry |
| 9 | Materially changed prompt/model/profile creates new identity | `A_materially_changed_input_produces_a_different_logical_key` (5 cases) |
| 10 | Missing pricing prevents the provider call | `Missing_pricing_prevents_the_provider_call` |
| 11 | STT and AI costs both contribute to accrued cost | `Stt_and_AI_costs_both_contribute_to_the_packages_single_accrued_cost_total` |
| 12 | Cost ceiling blocks the next AI call before execution | `Cost_ceiling_blocks_the_next_AI_call_before_execution` |
| 13 | Concurrent duplicate operation claims cannot both succeed | `Exactly_one_row_exists_per_logical_key_after_multiple_claims` + the unique-index architecture test |
| 14 | Historical pricing snapshots immutable after config changes | `Pricing_snapshot_on_a_succeeded_operation_is_immutable_after_options_change` |
| 15 | Admin API/UI displays STT and AI summaries safely | `Ai_operation_summary_returns_safe_fields_for_succeeded_and_failed_operations` + Angular specs + Playwright |
| 16 | Existing audited ceiling-amendment tests still pass | Full integration suite re-run, all 8 `ImportCostCeilingAmendmentTests` unchanged |
| 17 | Plan-driven execution / publish lifecycle still pass | Full integration suite re-run (1,321/1,321), no regressions |

Items 1–5 are honestly reported as not applicable — no code exists this phase to test against them.

## Tests

| Suite | Count | Result |
|---|---|---|
| Backend unit | 2,352 | Pass (+14: 3 in `ResourceCandidateAnalysisServiceTests`, 11 in new `ImportAiEnrichmentOperationLedgerTests`) |
| Backend integration | 1,321 | Pass (+4: new `ImportAiEnrichmentOperationSummaryTests`) |
| Backend architecture | 26 | Pass (+4: unique index, monetary-decimal, controller-boundary, no-credential-field guards) |
| Angular unit (plan component) | +7 new specs | Compiles clean under `tsc --noEmit`; not executed (Karma still blocked, unchanged) |
| Playwright | 4 | **Pass** — 2 pre-existing (re-verified) + 2 new (AI operations card) |

**Gate results:**
- `git diff --check`: clean.
- `dotnet restore` / `dotnet build --configuration Release`: 0 errors.
- `dotnet test` (all three projects, Release): 2,352 + 1,321 + 26 = 3,699 passing, 0 failing.
- `npx tsc --noEmit`: identical pre-existing baseline error set — zero new.
- `npm run build -- --configuration production`: succeeds.
- `npm test -- --watch=false --browsers=ChromeHeadless`: blocked, same pre-existing baseline
  TypeScript errors, confirmed unchanged.
- `npx playwright test --workers=1` (the Import specs): 4 passed, 0 failed, ~42s.

## Migration and live DB status

One additive migration, `Phase_4_4D_AiEnrichmentOperations`: a single new table
(`import_ai_enrichment_operations`) with two FKs (cascade, to `import_packages` and
`resource_candidates`) and two indexes (one non-unique on package, one unique on the logical key) —
no column changes, no data backfill, no existing table touched. Live DB: **not touched**. Existing
data: **unchanged**.

## Documentation

- Added: `docs/reviews/2026-07-16-phase-4-4d-audio-measurement-and-ai-accounting-review.md` (this file).
- Updated: `TODOS.md` — closed `TODO-4.4-AI-ENRICHMENT-LEDGER` as fixed; re-confirmed
  `TODO-4.4-AUDIO-DURATION-PROBE` as deferred (not newly deferred, explicitly re-scoped-out); added
  a Phase 4.4D section with `TODO-4.4D-JSON-AI-OPERATION-HISTORY-UI` and
  `TODO-4.4D-PLAYWRIGHT-AI-SUCCESS-PATH`.
- Updated: `docs/handoffs/current-product-state.md` — new Phase 4.4D section prepended,
  `lastUpdated` bumped.

## Known limitations

- Real audio-duration measurement remains entirely unimplemented — every audio file is still
  assumed to be exactly 5 minutes for STT cost purposes. This is the single largest remaining gap
  in this phase's cost-accounting accuracy.
- No fake `IAiProvider` is registered in the API integration/Playwright test host (unlike STT's
  auto-substituted `FakeSpeechToTextService`), so no test in this session drives a real AI success
  through the actual HTTP processing pipeline end-to-end. The ledger's core guarantees are proven
  at the unit level with a fake provider (genuinely never a real/paid call, per the brief's
  requirement) and the admin-visibility/combined-cost proofs use directly-seeded ledger rows at the
  integration level. A follow-up registering a fake AI provider in the test host (mirroring the STT
  precedent) would allow a true end-to-end integration proof — tracked as
  `TODO-4.4D-PLAYWRIGHT-AI-SUCCESS-PATH`.
- The AI-enrichment ledger's "unbounded retry per processing pass" policy is the same
  single-active-worker assumption the STT ledger already carries — not newly introduced, but worth
  restating: Quartz clustering remains deferred, and this ledger does not add any additional
  protection beyond what STT already has for genuinely concurrent workers.
- `ResultReusable` (surfaced in the admin UI and summary DTO) is a derived-from-current-state
  signal, not a persisted per-attempt reuse history, exactly matching the same documented caveat on
  `ImportSttOperationSummaryDto`.
- The STT ceiling-check re-sync fix (`runningCost = package.AccruedCost` after AI accrual) was
  necessary specifically because this phase moved AI accrual out of the loop that used to keep it
  in sync — flagged here for visibility since it's a subtle correctness dependency between the two
  operation types sharing one package-level running total within a single processing pass.

## Verdict

The AI operation ledger is delivered in full, generalizing the STT pattern with genuine parity:
same identity-key discipline, same claim/retry/terminal-success lifecycle, same one-save cost
discipline, same fail-closed pricing, same per-operation ceiling gating, same admin-visibility
shape. All 12 applicable critical-test requirements (6–17) are proven; items 1–5 (audio
measurement) are honestly reported as out of scope, not silently skipped. All backend gates green,
zero regressions across 3,699 backend tests. Frontend production build and Playwright succeed;
Karma remains blocked by the same pre-existing, unrelated baseline issue reported since Phase 4.2.

## Next recommended action

`TODO-4.4-AUDIO-DURATION-PROBE` is now the last major deferred item from the original Phase 4.4
brief and the single biggest remaining accuracy gap in Import cost accounting — real STT cost
still rests on a flat 5-minute assumption regardless of actual audio length.
