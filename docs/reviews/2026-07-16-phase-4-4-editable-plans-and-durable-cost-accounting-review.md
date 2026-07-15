# Phase 4.4 — Editable Import Plans and Durable Cost Accounting — Engineering Review

**Date:** 2026-07-16
**Related sprint/feature:** Content Creation Pipeline, Phase 4 series (Phase 4 → 4.1 audit → 4.2 → 4.3 → **4.4**)
**HEAD before work:** `f2b17236` (feat: execute imports from approved plans)
**Commit produced by this phase:** `feat: add editable import plans and durable cost accounting` (hash recorded in "Repository state" below, after commit)

---

## 1. Scope decision (made explicit before implementation)

The Phase 4.4 brief as written spans: a full Angular plan-editor UI, real audio-duration
measurement (ffprobe or equivalent), a general multi-operation cost ledger, immutable pricing
snapshots across every billable operation type, a controlled ceiling-resume UI, and Playwright
coverage — realistically several days of work. Before writing any code, this was raised explicitly
and the user confirmed a **backend-first scope**:

- Workstream A (plan editing): the draft-update API, optimistic concurrency, and the revision
  lifecycle — backend only, no Angular UI.
- Workstream B (cost accounting): a durable, retry-safe operation ledger scoped to STT (the
  brief's own "highest-risk duplicate-cost path"), with fail-closed pricing and persisted
  cost-ceiling enforcement.
- Explicitly deferred: the Angular plan-editor UI, real audio-duration measurement, an
  AI-enrichment operation ledger, and Playwright — each tracked as a `TODO-4.4-*` item in
  `TODOS.md`, not silently dropped.

This review documents exactly what was built against that agreed scope, not the full original brief.

---

## 2. Architecture before and after

**Before (Phase 4.3 baseline):**

```
ImportProfile: Draft -[SubmitForApproval]-> AwaitingApproval -[Approve]-> Approved -[MarkExecuting]-> Executing -> Completed
  - ReplaceProfileJson: Draft-only, no estimate recalculation
  - No concurrency protection at all (confirmed: plain FirstOrDefaultAsync + SaveChangesAsync)
  - No revision concept beyond full plan regeneration (which supersedes any prior live plan)

ImportPackageProcessingService.MapAndCreateCandidatesAsync
  - runningCost = 0m, every processing pass — resets on every retry
  - AI pricing unresolved → silently $0 (both at generation-time estimate and execution-time accrual)
  - STT: no operation identity at all — a crash mid-loop (nothing saved until stage checkpoint)
    means retry re-transcribes every audio file in the package, including already-successful ones
```

**After (Phase 4.4):**

```
ImportProfile
  + ConcurrencyStamp (Guid, application-checked, bumped by every mutator)
  + ReviseDraft(...) — editable while Draft OR AwaitingApproval, always updates the estimate too

IImportPlanDraftService.UpdateDraftAsync   — validate (shared validator) → recalculate estimate → ReviseDraft → save
IImportPlanDraftService.ReviseAsync        — new Draft/AwaitingApproval revision copying an approved plan,
                                              blocked once package processing has started
IImportPlanEstimateService.RecalculateAsync — volume/time/cost from a candidate instruction set,
                                              honouring Included, fails closed on missing AI pricing
IImportPlanPreviewService.PreviewAsync      — bounded sample preview, same mapping logic as execution,
                                              zero persistence, zero AI/STT calls
ImportExecutionPlanApprovalService.ApproveAsync
  + concurrency check (409 on stale ExpectedConcurrencyStamp)
  + fail-closed AI pricing check (only when the plan actually has structured files needing it)
  + supersedes any other still-Approved plan for the package (revision workflow invariant)

ImportPackage
  + AccruedCost / AccruedCostCurrency — durable running total, seeded at the START of every
    processing pass (package.AccruedCost, not 0m), persisted immediately alongside each operation

ImportSttOperation (new entity/table, unique-indexed on LogicalOperationKey)
  — one row per (package, asset, checksum); mutated across retries, not accumulated
IImportSttOperationLedger.ClaimAsync/MarkSucceededAsync/MarkFailedAsync
  — claim-or-reuse before ever calling the STT provider

ImportPackageProcessingService's audio/STT branch
  — checks the ceiling BEFORE claiming; claims via the ledger; on success, ledger row + package
    AccrueCost saved in ONE SaveChangesAsync (never drift apart); AlreadySucceeded means the
    provider is never called and no cost is accrued again
CheckAndAccrueAiCostAsync (AI enrichment)
  — throws ImportPricingUnavailableException instead of defaulting pricing to $0
```

---

## 3. Existing plan-edit limitations (before this phase)

Confirmed by direct code inspection before any change:

- `ImportProfile.ReplaceProfileJson` existed (Draft-only) but was never called by any endpoint —
  the only two admin actions on a plan were Approve and Reject; a plan could not be edited at all
  through the product.
- `ImportExecutionPlanApprovalService.LoadAsync` did a plain `FirstOrDefaultAsync` read followed
  by a mutation and `SaveChangesAsync`, with zero concurrency protection — two concurrent
  `ApproveAsync` calls on the same plan would both succeed, last write wins. This matches the
  Phase 4.1 audit's confirmed gap.
- The only precedent for optimistic concurrency anywhere in this codebase was a Postgres-only
  `xmin` configuration for `LearningPath` — not portable to the SQLite test provider, and not
  applied to any Import entity.
- `IAiPricingResolver.ResolveAsync` returning `null` was treated as "$0" at two call sites
  (`ImportExecutionPlanGenerationService.BuildCostEstimateAsync`,
  `ImportPackageProcessingService.CheckAndAccrueAiCostAsync`) — confirmed fail-open behaviour.
- STT cost used a flat, hardcoded 5-minutes-per-file assumption with no operation identity of any
  kind — a crash between a successful provider call and the next `SaveChangesAsync` (which only
  happened at whole-stage checkpoints, not per-operation) would silently lose track of both the
  transcript and the cost already incurred, and a retry would re-transcribe every audio file in
  the package.

---

## 4. Final plan revision lifecycle

```
Plan generation (unchanged)
  → Draft → SubmitForApproval → AwaitingApproval

Admin edits (new, Workstream A)
  → PUT .../plan/{planId}                       (Draft or AwaitingApproval only)
  → validated (ImportPlanInstructionValidator)  → estimate recalculated (IImportPlanEstimateService)
  → ImportProfile.ReviseDraft(...)              → ConcurrencyStamp bumped

Approval (unchanged endpoint, hardened)
  → POST .../plan/{planId}/approve
  → concurrency check (ExpectedConcurrencyStamp must match) → 409 if stale
  → existing ValidatePlanQuality checks (candidate count > 0, estimate present, AwaitingApproval)
  → NEW: fail-closed AI pricing check (only if the plan has structured files that would need it)
  → ImportProfile.Approve(...) → ImportPackage.ApproveProfile(planId)
  → any OTHER still-Approved plan for this package is superseded

Revision workflow (new, Workstream A4)
  → POST .../plan/{planId}/revise               (source plan must be Approved; package must not
                                                    have started executing it yet — Extracting/
                                                    Mapping/CreatingCandidates/Completed* all reject)
  → new ImportProfile row, next Version, copies the source plan's instructions, AwaitingApproval
  → admin may PUT-edit this new revision before approving it
  → approving it supersedes the prior Approved plan (see above) and reassigns
    ImportPackage.ApprovedImportProfileId via the normal ApproveProfile call

Execution (Phase 4.3, unchanged) always reads the exact ApprovedImportProfileId — a later draft
revision existing (even AwaitingApproval) never affects an already-running/completed package.
```

An approved plan itself remains fully immutable: `ReviseDraft`/`ReplaceProfileJson` both throw for
any status other than Draft/AwaitingApproval, and there is no code path that mutates an Approved
row's `ProfileJson`.

---

## 5. Concurrency strategy

Application-checked optimistic concurrency via `ImportProfile.ConcurrencyStamp` (`Guid`), not an
EF-native rowversion/xmin token — the only existing precedent (`LearningPath`'s xmin config) is
Postgres-provider-conditional and does not work against the SQLite test provider this codebase's
entire test suite depends on. Every mutator on `ImportProfile` (`ReplaceProfileJson`, `ReviseDraft`,
`SubmitForApproval`, `Approve`, `Reject`, `MarkExecuting`, `PauseForCostApproval`,
`ApproveRevisedCeilingAndResume`, `MarkCompleted`, `MarkFailed`, `Cancel`, `Supersede`) regenerates
the stamp. `ImportPlanDraftService`/`ImportExecutionPlanApprovalService` compare a caller-supplied
`ExpectedConcurrencyStamp` against the persisted value before applying any mutation and throw
`ImportPlanConcurrencyConflictException` (mapped to HTTP 409, carrying the current stamp so a
client can reload) on mismatch. Proven by
`ImportPlanDraftServiceTests.Stale_plan_update_returns_conflict` and
`ImportPlanEditingAndCostAccountingTests.Stale_concurrency_stamp_on_draft_update_returns_conflict`.

Approval's own concurrency check was similarly added (`ApproveImportExecutionPlanCommand.ExpectedConcurrencyStamp`).

---

## 6. Typed update contract

No new frontend-only model was introduced. `PUT .../plan/{planId}` accepts
`IReadOnlyList<ImportExecutionGroupInstruction>` — the exact same type Phase 4.3's
`ApprovedImportProfileResolver` deserializes `ProfileJson` into for execution. `ImportPlanInstructionValidator`
(the same class Phase 4.3's resolver already used, now shared) validates both the admin's draft
edit and the frozen approved plan with identical rules — there is one authoritative plan schema,
not a parallel one. `ImportExecutionPlanDto` gained a `GroupInstructions` field so callers (and a
future UI) can read the current typed shape without parsing `ProfileJson` themselves.

---

## 7. Validation rules (draft update)

Enforced by `ImportPlanDraftService.UpdateDraftAsync` + `ImportPlanInstructionValidator`:

1. Package exists.
2. Plan exists and belongs to the specified package.
3. Plan is Draft or AwaitingApproval (not Approved/Executing/Completed/etc.) — otherwise a plain
   `ResourceImportValidationException` ("only a Draft or AwaitingApproval plan can be edited").
4. `ExpectedConcurrencyStamp` matches the plan's current stamp — otherwise `ImportPlanConcurrencyConflictException`.
5. Every group instruction has a non-empty, unique `GroupKey`.
6. Every `FieldMappings` target is in `ResourceImportRecognizedFields.All` (the same set the AI
   column-mapping proposal is validated against).
7. Every folder group present in the package manifest has a covering instruction (full-coverage
   rule — an incomplete plan is rejected, not silently defaulted).
8. An audio-content-bearing group may only route to `ResourceCandidateType.ListeningPassage`.

All violations are collected (not just the first) into `ImportPlanValidationFailedException.Errors`
(`IReadOnlyList<ImportPlanValidationError>`, each optionally scoped to a `GroupKey`) — a future UI
can group them by source-group card without re-deriving that mapping itself.

CSV/JSON "path" validation beyond recognized-field-name checking, and "duplicate or contradictory
mapping" detection beyond one-target-per-source-column, were not separately implemented — the
existing `FieldMappings` shape (`IReadOnlyDictionary<string,string>`) structurally cannot represent
two different targets for the same source column, so that class of contradiction is already
impossible by construction, not by an extra runtime check.

---

## 8. Preview design and safety boundary

`ImportPlanPreviewService.PreviewAsync`:

- Loads already-extracted `ImportAsset` rows for the package (CSV/JSON/JSONL only — audio groups
  are not previewed, since there is no generic "predicted field" shape for a transcript that
  doesn't exist yet).
- For each Included group with sample-parseable assets, reads a bounded number of rows via the
  existing `IResourceImportService.ParseSample` (already pure/no-DB-write) and maps each row
  through the **new** `IResourceImportService.PreviewRow` method — which reuses the exact private
  `ApplyColumnRenames`/`InferCandidateType`/`ExtractCanonicalTextForType` logic `ImportAsync` uses,
  so a preview is provably the same mapping code path execution runs, not a second implementation
  that could silently diverge.
- Never constructs a `ResourceRawRecord`, `ResourceCandidate`, or `ResourceImportRun`. Never calls
  `IAiPricingResolver`, any AI provider, or `ISpeechToTextService`.
- Also runs `ImportPlanInstructionValidator` and returns its errors alongside the preview rows, so
  a UI can show "this mapping is invalid" and "here's what valid rows would look like" together.

Architecture guard (`Only_known_types_depend_on_the_execution_group_instruction_contract`, extended
this phase) keeps the set of types allowed to touch the typed instruction contract closed — a
future service growing its own parsing/preview logic outside this boundary fails the build.

**Known limitation:** preview only works for packages whose `ImportAsset` rows already exist —
inline/loose-file submissions have them immediately; ZIP packages don't until the approved plan's
Extract stage runs (same limitation Phase 4.3 already documented for structured-mapping proposals).

---

## 9. Estimate invalidation/recalculation rules

`IImportPlanEstimateService.RecalculateAsync` is called on every `UpdateDraftAsync` call — there is
no separate "mark stale" flag; the estimate is unconditionally recomputed from the submitted
instruction set every time, so it can never lag behind the mapping it describes. It:

- Filters the package manifest's entries down to only those in `Included` groups before computing
  volume (an excluded group contributes zero candidates/cost).
- Rebuilds `DetectedGroups` for display from the instructions themselves (group key, file count,
  routed-to description) rather than reusing plan-generation's AI-review output, which doesn't
  exist at edit time.
- Fails closed (`ImportPricingUnavailableException`) when the package's processing mode requires
  AI enrichment **and** the included set actually contains structured (CSV/JSON/JSONL) files —
  scoped this way specifically so a pure-audio package (which never reaches AI batch analysis) is
  never blocked over AI pricing it will never spend. Proven by
  `ImportPlanDraftServiceTests.Excluding_a_group_recalculates_the_estimate_to_zero_candidates`.

Approval independently re-checks pricing (§11) rather than trusting the estimate's own successful
computation, since the estimate could have been computed before a DB pricing override changed.

---

## 10. Current and final cost-accounting design

**Package-level durable total** (`ImportPackage.AccruedCost`/`AccruedCostCurrency`) — chosen over a
separate summary entity per the phase brief's "prefer an operation-level ledger... do not create
unnecessary general-ledger complexity" guidance combined with "cost fields on ImportPackage" being
the suggested shape. `MapAndCreateCandidatesAsync`'s local `runningCost` variable is now **seeded**
from `package.AccruedCost` (not `0m`) at the start of every processing pass, and every accrual
(`AI enrichment`, `STT`) is written to `package.AccruedCost` and saved in the same
`SaveChangesAsync` as the operation that produced it — not batched until a later stage checkpoint.

**Operation-level ledger** (`ImportSttOperation`, STT-scoped this phase — see §1 for why AI
enrichment doesn't have one yet) — see §12/§14 below for identity/idempotency and §15 for audit
fields.

---

## 11. Pricing validation

Two independent fail-closed checks, both new this phase:

1. **Estimate time** (`ImportPlanEstimateService.BuildCostEstimateAsync`) — throws if the mode
   requires AI and the included set has structured files but `IAiPricingResolver.ResolveAsync`
   returns `null`.
2. **Approval time** (`ImportExecutionPlanApprovalService.ValidatePricingAsync`) — re-checks
   independently (using the plan's persisted `PlanEstimateJson.Volume.FilesByExtension` to decide
   whether structured files are actually present), since pricing configuration could have changed
   between estimate computation and approval.
3. **Execution time** (`ImportPackageProcessingService.CheckAndAccrueAiCostAsync`) — throws
   `ImportPricingUnavailableException` instead of the old `?? 0m` fallback, as defense-in-depth for
   the case where pricing was deactivated after approval.

STT pricing (`ImportCostEstimationOptions.SttCostPerMinute`) was **not** made fail-closed — it is a
single compiled-in config value (not a dynamic per-provider/model resolver like `IAiPricingResolver`),
so there is no distinguishable "missing" state separate from "configured as zero"; a zero value
here is treated as a deliberately configured free rate, consistent with the phase brief's "a
deliberately configured zero price may be valid."

The admin-facing error (`ImportPricingUnavailableException`) names the exact provider, model, and
operation ("AI candidate enrichment") that lacks pricing.

---

## 12. Operation identity / idempotency strategy (STT)

`ImportSttOperationKey.Compute(packageId, assetId, checksum)` — content-addressed by design:
transcription output depends only on the audio bytes (checksum), not on which plan/profile
revision is currently approved, so the key deliberately excludes `ImportProfileId` (which is still
stored on the row for provenance). A changed checksum (re-uploaded/replaced content) produces a
different key, so a stale prior measurement/result is never reused across different content.

`ImportSttOperation` has exactly one row per logical key (DB-enforced via a unique index on
`LogicalOperationKey`), mutated in place across attempts rather than accumulating a row per retry:

- `Succeeded` is terminal — never mutated again.
- `Failed` may retry exactly once more (`BeginRetry`, increments `AttemptNumber`, clears
  `FailureReason`/`CompletedAtUtc`).
- A dangling `Pending` row (left by a crash between claim and outcome) is treated as safe to
  re-claim, not a permanent block — see §14 for why this is safe under the documented boundary.

---

## 13. Audio measurement implementation

**Not implemented this phase** (`TODO-4.4-AUDIO-DURATION-PROBE`). STT cost continues to use the
pre-existing flat 5-minutes-per-file assumption, now surfaced as a named constant
(`ImportSttOperation.AssumedMinutes`) ready to receive a real measured value once a duration probe
exists. This was an explicit, user-confirmed scope cut (§1) — introducing an external-process
(ffprobe) or media-metadata-library runtime dependency is materially separate, higher-risk work.

---

## 14. Cost-ceiling transaction/concurrency strategy

Before ever calling the STT provider: `projectedSttCost = runningCost (seeded from persisted
AccruedCost) + assumedMinutes * SttCostPerMinute`; if that exceeds `ceiling * (1 + tolerance)`, the
package pauses (`PauseForCostApproval`) and the provider is never called — matching the pre-existing
AI-enrichment ceiling check's shape, now backed by a durable starting total instead of an ephemeral
one. Proven by `ImportPlanEditingAndCostAccountingTests.Ceiling_blocks_the_provider_call_before_it_would_be_exceeded_using_persisted_cost`
(two audio files, a ceiling covering exactly one STT call — the fake provider's `CallCount` is
asserted to be exactly `1`, not `2`).

**Documented remaining concurrency boundary** (Workstream B11): the unique index on
`LogicalOperationKey` guarantees at most one row per logical operation and a `Succeeded` row is
terminal, so two callers can never both record a successful charge for the same content. What is
**not** guaranteed is strict mutual exclusion of the claim-to-outcome window under true concurrent
execution — a second, genuinely concurrent worker processing the exact same asset mid-flight is not
caught by anything beyond the unique index at INSERT time. This assumes at most one active
package-processing worker at a time, the same single-worker assumption the rest of this codebase's
Import pipeline already makes (Quartz clustering remains deferred, per the existing, unchanged
`TODO`). A full transactional claim/reservation primitive for true multi-worker safety was
explicitly out of scope per the phase brief's own "Quartz clustering remains deferred... cost-
operation claiming must be safe in this phase" — read here as "safe under the existing single-
worker assumption," which this design satisfies; a stronger multi-instance guarantee is not claimed.

---

## 15. Pause and resume lifecycle

Unchanged from Phase 4 (not touched this phase, since `TODO-4.4-CEILING-RESUME-UI` was scoped out):
`ImportProfile.PauseForCostApproval(reason)` / `ApproveRevisedCeilingAndResume(newCeiling)` still
work exactly as before — no concurrency stamp check or audit trail of the previous ceiling was
added to that path this phase. `package.AccruedCost` is now visible on the paused package
(previously there was no durable field to show at all), which is a strict improvement even without
UI/audit work: `ImportPlanEditingAndCostAccountingTests.Ceiling_blocks_the_provider_call_before_it_would_be_exceeded_using_persisted_cost`
confirms the paused package's `AccruedCost > 0` and the plan's `PauseReason` is non-empty and
queryable via the existing `GET .../plan` endpoint.

---

## 16. Migration details

One migration: `20260715214525_Phase_4_4_EditablePlansAndCostAccounting`. Fully additive:

- `import_profiles.concurrency_stamp` (uuid, not null, default `00000000-0000-0000-0000-000000000000`
  for existing rows — a neutral sentinel; concurrency correctness only matters for edits made after
  this migration runs).
- `import_packages.accrued_cost` (numeric(12,4), not null, default `0`).
- `import_packages.accrued_cost_currency` (varchar(8), not null, default `'USD'`).
- New table `import_stt_operations` (all columns, FKs to `import_packages`/`import_assets` with
  cascade delete, unique index on `logical_operation_key`, non-unique index on `import_package_id`).

No existing data is altered or backfilled with fabricated values. No live database connection was
available this session — the same constraint noted in Phase 4.2/4.3 — so this migration was
validated by (a) successful `dotnet build` against the generated migration, (b) inspecting the
generated SQL directly (pasted in the review discussion; standard `AddColumn`/`CreateTable`, no
destructive operations), and (c) the full test suite running against SQLite (schema built from the
current EF model via `EnsureCreated`, which reflects the same shape the migration produces). No
claim is made that this migration was run against a live/production database.

This migration is independent of `TODO-4.2-DB-PROVENANCE` (making `ResourceImportRun.ImportPackageId`
non-nullable) — not combined with it, per the phase brief's instruction.

---

## 17. Critical proof tests

### Proof 1 — Admin-edited plan → exact approved revision → changed candidate output

`ImportPlanEditingAndCostAccountingTests.Admin_edited_plan_through_the_draft_API_produces_the_edited_candidate_output`:
submits a CSV with an unrecognized column name (`mystery1`), generates a plan (default mapping
empty), edits it through the real `PUT .../plan/{planId}` endpoint (maps `mystery1 → word`, forces
`VocabularyEntry`), approves using the updated `ConcurrencyStamp`, processes, and asserts the
resulting candidate has `candidateType == "VocabularyEntry"` and `canonicalText` equal to the
marker value — i.e. the value only reachable through the admin's edit, never through deterministic
clustering's own inference for a column literally named "mystery1".

### Proof 2 — Successful STT → simulated retry → no second provider call, no duplicate cost

`ImportPlanEditingAndCostAccountingTests.Retry_after_a_successful_STT_operation_does_not_call_the_provider_again_or_double_charge`:
approves and processes a single-audio-file package (`FakeSpeechToTextService.CallCount == 1`,
`package.AccruedCost > 0`, exactly one `Succeeded` `ImportSttOperation` row). Simulates the crash
window described in §5/§14/§B4 by resetting the asset's `ProcessingState` and the package's
`LastCompletedStageIndex`/`Status` back to a pre-completion state — exactly what a crash between
the ledger+cost save and the later stage-checkpoint save would leave behind — while leaving the STT
ledger row and `AccruedCost` (already durably saved together) untouched. Re-runs processing: the
**second pass's own** `FakeSpeechToTextService.CallCount == 0` (the provider was never called
again), `package.AccruedCost` is unchanged (not doubled), and still exactly one `ImportSttOperation`
row exists for that logical key.

### Proof 3 — Persisted accrued cost + next-operation estimate > ceiling → provider not called, package pauses safely

`ImportPlanEditingAndCostAccountingTests.Ceiling_blocks_the_provider_call_before_it_would_be_exceeded_using_persisted_cost`:
two audio files, a ceiling that covers exactly one STT call. `FakeSpeechToTextService.CallCount == 1`
after processing (the second file's projected cost is checked against the ceiling **before**
claiming/calling — the second call never happens). Package ends in
`AwaitingMappingApproval`/plan `PausedForCostApproval` with a non-empty `PauseReason`, and the
first call's cost is preserved in `package.AccruedCost` (not lost).

---

## 18. Tests and exact counts

| Suite | Before (Phase 4.3 baseline) | After (Phase 4.4) |
|---|---|---|
| Unit | 2,320 / 2,320 | **2,338 / 2,338** |
| Integration | 1,298 / 1,298 | **1,302 / 1,302** |
| Architecture | 15 / 15 | **19 / 19** |
| Angular unit (Karma) | Blocked (pre-existing `feedbackPolicy`/`moduleSuggestions` compile errors) | Blocked — same pre-existing errors; no Angular file was touched this phase |
| Playwright | Not added (deferred) | Not added — still deferred (`TODO-4.4-PLAYWRIGHT`), no UI shipped to test against |

New tests (net +18 unit / +4 integration / +4 architecture, 0 removed):

- `tests/LinguaCoach.UnitTests/ResourceImport/ImportPlanDraftServiceTests.cs` — 10 facts: draft can
  be updated; approved plan cannot be edited; plan belonging to another package cannot be edited;
  stale update returns conflict; unsupported resource type fails validation; unknown mapping target
  fails validation (errors grouped by group key); manifest group not represented fails validation;
  excluding a group recalculates the estimate to zero; a revision creates a new draft without
  altering the approved plan; a revision is rejected once package processing has started.
- `tests/LinguaCoach.UnitTests/ResourceImport/ImportSttOperationLedgerTests.cs` — 8 facts: identical
  inputs → identical logical key; changed checksum → different key; first claim is fresh Pending;
  a succeeded operation is reused (no duplicate row); a failed operation can retry (attempt number
  increments); a failed attempt is not reusable as success; exactly one row exists per logical key
  after multiple claims; a succeeded operation's pricing snapshot is immutable.
- `tests/LinguaCoach.IntegrationTests/Api/ImportPlanEditingAndCostAccountingTests.cs` — 4 facts (the
  three critical proofs in §17, plus a stale-concurrency-via-API conflict test).
- `tests/LinguaCoach.ArchitectureTests/ImportPipelineBoundaryTests.cs` — 4 new facts: the import
  controller does not depend on pricing/cost-estimation options directly; the import controller
  does not depend on the STT ledger directly; monetary properties on the Phase 4.4 cost-bearing
  entities use `decimal`, never `double`/`float`; `ImportSttOperation.LogicalOperationKey` has a
  unique index configured (guards against the DB-level dedup guarantee being silently dropped). The
  existing `Only_known_types_depend_on_the_execution_group_instruction_contract` guard's allow-list
  was extended for the four new Phase 4.4 services that legitimately consume the typed contract.

One existing test file updated for the new required `ApproveImportExecutionPlanCommand.ExpectedConcurrencyStamp`
parameter and `ImportExecutionPlanApprovalService`'s new constructor dependencies:
`ImportPackagePlanFlowTests.cs`, `ImportPackageProcessingServiceTests.cs`,
`AdminResourceImportEndpointTests.cs`, `ImportExecutionPlanDrivenExecutionTests.cs` (the latter two
from Phase 4.3) — all pre-existing facts in these files still pass unmodified after the parameter
update.

`FakeSpeechToTextService` gained a test-only `CallCount` property (additive, non-breaking) so
retry-safety tests can assert the provider was genuinely called the expected number of times.

### Build / restore / frontend

```
dotnet restore                          → clean
dotnet build --configuration Release    → 0 errors (pre-existing warnings only)
npx tsc --noEmit                        → same pre-existing e2e/spec errors as the Phase 4.3
                                           baseline (feedbackPolicy, moduleSuggestions,
                                           learningActivityId) — zero new errors, no Angular
                                           source file was touched this phase
npm run build -- --configuration production   → not re-run this phase (no frontend change);
                                           Phase 4.3 established this baseline passes
npm test -- --watch=false --browsers=ChromeHeadless   → not re-run this phase; same pre-existing
                                           blocker as Phase 4.2/4.3, unrelated to this phase
npx playwright test                     → not run; no Playwright coverage exists for this pipeline
                                           in the Phase 4.3 baseline or this phase (still deferred)
```

---

## 19. Data and migrations

- **Migration added:** `20260715214525_Phase_4_4_EditablePlansAndCostAccounting.cs` (see §16 for
  exact contents).
- **Test-database validation:** full unit/integration/architecture suites pass against SQLite
  (schema built from the current EF model, matching this migration's shape).
- **Live DB audit performed:** no (no live database connection available this session, same as
  Phase 4.2/4.3).
- **Live DB migrated:** no.
- **Existing data changed:** no — nothing was run against a persistent store; all new/changed
  behaviour was exercised through SQLite in-memory test databases.
- **Blockers:** none beyond the pre-existing lack of a live DB connection.

---

## 20. Documentation

Added:
- `docs/reviews/2026-07-16-phase-4-4-editable-plans-and-durable-cost-accounting-review.md` (this file)

Updated:
- `docs/handoffs/current-product-state.md` — new "Phase 4.4" section prepended above Phase 4.3.
- `TODOS.md` — new "Phase 4.4" section with `TODO-4.4-PLAN-EDITOR-UI`,
  `TODO-4.4-AUDIO-DURATION-PROBE`, `TODO-4.4-AI-ENRICHMENT-LEDGER`, `TODO-4.4-PLAYWRIGHT`,
  `TODO-4.4-CEILING-RESUME-UI`, and `TODO-4.4-LOOSE-FILE-FOLDER-BUG` (a pre-existing bug discovered,
  not introduced, while writing this phase's tests).

No `docs/sprints/` document exists for this feature area (same as Phase 4.3); not invented.

---

## 21. Deferred items — explicitly confirmed not implemented

- Angular plan-editor UI (`TODO-4.4-PLAN-EDITOR-UI`) — no admin-facing UI change was made.
- Real audio-duration measurement / ffprobe integration (`TODO-4.4-AUDIO-DURATION-PROBE`) — STT
  cost still uses the flat 5-minutes-per-file assumption.
- A durable operation ledger for AI candidate-enrichment calls (`TODO-4.4-AI-ENRICHMENT-LEDGER`) —
  only STT has a ledger table this phase; AI enrichment cost is fail-closed and persisted
  (`package.AccruedCost`) but has no per-call ledger row/idempotency key of its own.
- Playwright coverage (`TODO-4.4-PLAYWRIGHT`) — no UI shipped to test against.
- A controlled ceiling-resume UI or concurrency/audit hardening on the existing
  `ApproveRevisedCeilingAndResume` path (`TODO-4.4-CEILING-RESUME-UI`) — untouched this phase.
- Immutable pricing snapshots for every billable operation type — only `ImportSttOperation` carries
  one (`PricePerMinuteSnapshot`, `Currency`, `CalculatedCost`); AI enrichment's per-call rate is not
  separately snapshotted per call (it inherits whatever `IAiPricingResolver` returns at accrual
  time, same as before this phase, just no longer silently defaulted to $0 when absent).
- No typed candidate schemas, new multimodal Resource types, TTS, content-aware AI sampling,
  Resource Bank media preview, Lesson media discovery, resumable upload, 10GB upload support,
  local-storage upload parity, Quartz clustering, broad distributed locking beyond the STT ledger's
  documented boundary, Candidate Review redesign, direct Resource-to-Exercise generation, or
  approve-and-publish shortcuts were implemented — none of these were in scope even under the full
  brief, per its own explicit exclusion list.
- `TODO-4.2-SERVICE-LEVEL-GATE`, `TODO-4.2-DB-PROVENANCE`, `TODO-4.2-PLAYWRIGHT` remain open,
  untouched by this phase.

---

## 22. Known limitations (precise, evidence-based)

- **No admin UI exists to exercise any of this phase's editing/preview/revision capability.** The
  three critical-proof tests call the real HTTP API directly, which is a legitimate proof that the
  *backend* is plan-driven and edit-aware, but no administrator can use it through the product yet.
- **STT is the only operation with a durable, retry-safe ledger.** AI candidate enrichment's cost
  is fail-closed and persisted, but a retry of the exact same batch-analysis call is not itself
  guarded against re-invocation by a ledger row — it relies on the pre-existing, unchanged
  idempotency of `IResourceCandidateBatchAnalysisService` (skips already-analyzed candidates), not
  a new mechanism from this phase.
- **Audio duration remains a flat assumption**, not a measurement — cost estimates and ledger
  entries for STT are only as accurate as that 5-minute-per-file assumption always was.
- **The documented concurrency boundary (§14) assumes a single active processing worker.** This
  phase does not add distributed locking; Quartz clustering remains deferred as before.
- **A pre-existing, unrelated bug was discovered** (not introduced): a loose-file submission whose
  filename contains a `/` mis-groups against the synthetic manifest's single root folder group,
  causing a deterministic (safe, not silent) processing failure. Documented as
  `TODO-4.4-LOOSE-FILE-FOLDER-BUG`; two integration tests in this phase were adjusted to avoid it
  after discovering it mid-session.
- **The Import pipeline is not claimed to be production-ready** by this phase. Three Phase 4.2
  TODOs and five new Phase 4.4 TODOs remain open.

---

## 23. Final verdict

Within the explicitly agreed backend-first scope, Phase 4.4 delivers a real, tested plan-editing API
with optimistic concurrency and a non-destructive revision lifecycle, and closes the STT
duplicate-cost/duplicate-provider-call risk the brief itself called the highest-risk item — with
all three critical proofs demonstrated end-to-end against the real API and real EF Core persistence.
All pre-existing tests continue to pass (no regressions); 26 new tests were added across
unit/integration/architecture layers, all passing. One additive, non-destructive migration was
generated and validated against the test suite. The Angular UI, real duration measurement, and a
generalized operation ledger remain deliberately, explicitly deferred rather than attempted shallowly.

## 24. Next recommended action

`TODO-4.4-PLAN-EDITOR-UI` — give an admin an actual product surface for the editing capability this
phase built. Without it, the typed contract, validator, estimate recalculation, and revision
lifecycle are real and tested, but only reachable via direct API calls.
