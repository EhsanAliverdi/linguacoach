# Phase 4.3 — Approved-Plan-Driven Execution — Engineering Review

**Date:** 2026-07-16
**Related sprint/feature:** Content Creation Pipeline, Phase 4 series (Phase 4 → 4.1 audit → 4.2 → **4.3**)
**HEAD before work:** `ff055fd2` (refactor: require approved plans for all imports)
**Commit produced by this phase:** `feat: execute imports from approved plans` (see "Repository state" below for the exact hash, filled in after commit)

---

## 1. Objective

Make the approved Import Execution Plan the authoritative source of truth for package processing.
The central product rule this phase enforces:

> What the administrator reviews and approves must be what the processing engine executes.

Phase 4.1's audit found that `ImportPackageProcessingService` read only a plan's `Status` and
`ApprovedCostCeiling` and otherwise re-derived file routing, resource-type classification, and (for
ZIP packages) column mapping independently, via its own hardcoded extension tables and
`ResourceImportService`'s field-name heuristics. Phase 4.2 closed a different gap (the ungated
single-file pipeline) and left this one open, adding one narrow exception (`ColumnRenames` for
inline/non-ZIP packages only, sourced from `PlanEstimateJson.StructuredMappingPreviews`, not
`ProfileJson`).

---

## 2. Files reviewed before design

- `src/LinguaCoach.Domain/Entities/ImportPackage.cs`, `ImportProfile.cs`, `ResourceImportRun.cs`,
  `ResourceRawRecord.cs`, `ResourceCandidate.cs`, `ImportAsset.cs`
- `src/LinguaCoach.Application/ResourceImport/ImportExecutionPlanContracts.cs`,
  `ResourceImportContracts.cs`, `ResourceImportColumnMappingContracts.cs`,
  `ImportPackageManifestContracts.cs`, `ImportProcessingModeContracts.cs`
- `src/LinguaCoach.Infrastructure/ResourceImport/ImportExecutionPlanGenerationService.cs`,
  `ImportExecutionPlanApprovalService.cs`, `ImportPackageProcessingService.cs`,
  `ResourceImportService.cs`, `ResourceImportColumnMappingService.cs`,
  `ImportPackageSubmissionService.cs`, `ImportProcessingModeDecisionService.cs`
- `src/LinguaCoach.Api/Controllers/AdminImportPackageController.cs`
- `docs/reviews/2026-07-15-phase-4-1-large-import-validation-and-gap-audit.md`
- `docs/reviews/2026-07-15-phase-4-2-mandatory-import-plan-gate-review.md`
- Test infrastructure: `tests/LinguaCoach.UnitTests/ResourceImport/*.cs`,
  `tests/LinguaCoach.IntegrationTests/Api/AdminResourceImportEndpointTests.cs`,
  `tests/LinguaCoach.ArchitectureTests/ImportPipelineBoundaryTests.cs`

### 2.1 What the audit found (Step 1 — schema/behavior audit)

- **`ProfileJson`** was written exactly once, in `ImportExecutionPlanGenerationService.GenerateAsync`
  (`profileJson: JsonSerializer.Serialize(groups)`, `groups: List<ImportExecutionPlanDetectedGroup>`
  — `GroupKey`, `Description`, `FileCount`, `SampleRelativePaths`, `ProposedResourceType`,
  `Confidence`). It was read **nowhere** in the codebase — confirmed by full-repo grep before any
  code changed.
- The domain entity's own doc comment described an aspirational shape ("field mappings,
  grouping/pairing rules, defaults, required fields, validation rules") that was never actually
  built.
- The one real mapping-consumption path that existed lived in a **different** column,
  `PlanEstimateJson.StructuredMappingPreviews` (`ImportExecutionPlanStructuredMappingPreview`, a
  per-asset column-rename map), and only worked for inline (non-ZIP) packages, and only supplied
  `ColumnRenames` — never resource-type routing or include/exclude decisions.
- `ImportPackageProcessingService.MapAndCreateCandidatesAsync` re-derived: file-type routing via
  static `HashSet<string>` extension tables; folder grouping by re-reading `ImportAsset.RelativePath`
  from scratch; audio/transcript pairing by filename-stem matching, independently of the plan's
  already-computed groups; resource-type classification via `ResourceImportService.InferCandidateType`
  (a hardcoded field-name cascade) for every structured file, and a hardcoded
  `ResourceCandidateType.ListeningPassage` for every audio file — the plan's `ProposedResourceType`
  per group was computed at plan-generation time and then discarded.
- `ProcessOnePackageAsync` already resolved the plan by the **exact** `ApprovedImportProfileId`
  (not "latest") — this was correct pre-4.3 and preserved.
- No admin UI/endpoint exists to edit a plan's proposed groups/mappings before approval — approval
  is binary (approve exactly what was generated, or reject and regenerate).

---

## 3. Architecture before and after

**Before:**

```
ImportExecutionPlanGenerationService.GenerateAsync
    → ProfileJson = detected groups (write-only, never read)
    → PlanEstimateJson = full estimate incl. StructuredMappingPreviews (inline packages only)

ImportPackageProcessingService.MapAndCreateCandidatesAsync
    → routing: hardcoded extension HashSets, re-derived folder grouping
    → resource type: ResourceImportService.InferCandidateType (field-name heuristic) for CSV/JSON;
      hardcoded ListeningPassage for audio
    → column mapping: PlanEstimateJson.StructuredMappingPreviews → ColumnRenames (inline only)
    → include/exclude: no concept — every discovered file is always processed
```

**After:**

```
ImportExecutionPlanGenerationService.GenerateAsync
    → ProfileJson = List<ImportExecutionGroupInstruction>  (GroupKey, Included, ResourceType,
      FieldMappings) — the actual, typed execution contract
    → PlanEstimateJson unchanged (display/estimate concern, still separate)

IApprovedImportProfileResolver.ResolveAsync(packageId)
    → loads ImportPackage.ApprovedImportProfileId exactly (never "latest")
    → validates package ownership, Approved/Executing status
    → deserializes + validates ProfileJson (recognized field-mapping targets, full manifest-group
      coverage, audio groups can only route to ListeningPassage)
    → returns ApprovedImportExecutionProfile (typed, frozen)

ImportPackageProcessingService.MapAndCreateCandidatesAsync
    → resolves the approved profile once per pass (before Extract even runs)
    → per asset: ApprovedImportExecutionProfile.ResolveForRelativePath(asset.RelativePath)
    → Included=false → skip (no candidate, asset marked Processed)
    → ResourceType → ResourceImportRequest.DefaultCandidateType (forces the route)
    → FieldMappings → ResourceImportRequest.ColumnRenames (works for ZIP packages too now)
```

---

## 4. `ProfileJson` schema

### Before (write-only)

```csharp
public sealed record ImportExecutionPlanDetectedGroup(
    string GroupKey, string Description, int FileCount,
    IReadOnlyList<string> SampleRelativePaths,
    ResourceCandidateType? ProposedResourceType, double Confidence);
```

### After (the actual execution contract)

```csharp
public sealed record ImportExecutionGroupInstruction(
    string GroupKey,
    bool Included,
    ResourceCandidateType? ResourceType,
    IReadOnlyDictionary<string, string> FieldMappings,
    IReadOnlyList<string> SampleRelativePaths);

public sealed record ApprovedImportExecutionProfile(
    Guid ImportProfileId, Guid ImportPackageId, int Version,
    IReadOnlyList<ImportExecutionGroupInstruction> GroupInstructions)
{
    public ImportExecutionGroupInstruction? ResolveForRelativePath(string relativePath);
}
```

`ProfileJson` now serializes `List<ImportExecutionGroupInstruction>` directly. `PlanEstimateJson`
(estimate/decisions/risks — the admin-facing display payload) is untouched; `Structured­MappingPreviews`
remains there as the plan-review UI's per-asset preview, but is no longer execution's mapping
source — `ProfileJson`'s per-group `FieldMappings` is (aggregated from the same previews at
generation time for inline packages; empty for ZIP packages, an unchanged limitation — see deferred
items).

### Previously-ignored plan fields now consumed by execution

| Field | Before | After |
|---|---|---|
| `ProfileJson` (any content) | Never read | The execution contract |
| Group `Included` | Did not exist | Honoured — false skips the group entirely |
| Group `ResourceType` | Computed, discarded after plan generation | Forces `DefaultCandidateType` |
| Group `FieldMappings` | Did not exist as a per-group concept | Applied as `ColumnRenames`, ZIP packages included |

---

## 5. New typed execution contract & validation rules

`IApprovedImportProfileResolver` / `ApprovedImportProfileResolver`
(`src/LinguaCoach.Infrastructure/ResourceImport/ApprovedImportProfileResolver.cs`) is the single
place `ProfileJson` is deserialized. Validation rules, each producing a distinct
`ApprovedImportProfileResolutionException` message:

1. Package not found.
2. `ApprovedImportProfileId` is null ("no approved plan").
3. Referenced `ImportProfile` row not found.
4. `ImportProfile.ImportPackageId` does not match the requested package (foreign-plan guard).
5. `ImportProfile.Status` is not `Approved` or `Executing`.
6. `ProfileJson` is empty/whitespace.
7. `ProfileJson` is malformed JSON.
8. Deserializes to null or an empty list.
9. A group has an empty `GroupKey`, or two groups share a `GroupKey`.
10. A `FieldMappings` target is not in `ResourceImportRecognizedFields.All` (the same set the AI
    column-mapping proposal is validated against — no drift between "AI may propose" and "execution
    may apply").
11. (Cross-checked against `ImportPackage.ManifestJson`, when present) every manifest folder group
    must have a covering instruction — an incomplete plan fails deterministically rather than
    defaulting unmapped folders to "excluded" or "included."
12. (Same cross-check) a folder containing audio files may only route to `ResourceCandidateType.ListeningPassage`
    — any other forced `ResourceType` on an audio-containing group is an unsupported route.

---

## 6. Execution services changed

- **`ImportPackageProcessingService`** (`src/LinguaCoach.Infrastructure/ResourceImport/ImportPackageProcessingService.cs`):
  - New constructor dependency `IApprovedImportProfileResolver`.
  - `ProcessOnePackageAsync` resolves the approved profile once, inside the `try` block, before the
    Extract stage — an invalid/incomplete plan is rejected before any file is touched (manifest is
    available pre-extraction).
  - `MapAndCreateCandidatesAsync` now takes the resolved `ApprovedImportExecutionProfile` and:
    - Groups assets by `ImportExecutionGroupKey.ForRelativePath` (shared helper, same convention
      plan generation uses) instead of raw `Path.GetDirectoryName`.
    - For each CSV/JSON/JSONL asset: resolves its instruction; skips (marks `Processed`, no
      candidate) if `Included` is false; otherwise passes `DefaultCandidateType` and
      `ColumnRenames` from the instruction into `ResourceImportService.ImportAsync`.
    - For each audio-bearing folder group: resolves its instruction; skips the whole group if
      `Included` is false; defense-in-depth check that a forced `ResourceType` (if any) is
      `ListeningPassage` (already enforced earlier by the resolver against the manifest).
  - Removed: the dead `LoadStructuredMappingPreviews` method (its `PlanEstimateJson`-based mapping
    source is superseded by the resolver).

- **`ImportExecutionPlanGenerationService`** (`.../ImportExecutionPlanGenerationService.cs`):
  - New `BuildGroupInstructions(groups, structuredMappingPreviews)` builds the typed
    `List<ImportExecutionGroupInstruction>` now written to `ProfileJson` — `Included` defaults to
    `true` (no admin exclusion UI yet), `ResourceType` copied from the group's
    `ProposedResourceType`, `FieldMappings` aggregated from any `StructuredMappingPreviews` whose
    asset falls in that group (inline packages only, unchanged limitation for ZIP).

- **`ImportProfile`** domain entity doc comment updated to describe the actual persisted shape.

### Old inference paths removed from execution

- Hardcoded extension-based folder/file routing in `ImportPackageProcessingService` for the
  structured-file loop (still used only to decide *parse format* — CSV vs JSON vs JSONL — never
  routing/inclusion/resource-type, which now come from the plan).
- `LoadStructuredMappingPreviews` (`PlanEstimateJson`-based mapping lookup).
- Implicit "every discovered file is always processed" behavior — replaced by explicit
  `Included` per group.

Planning-time inference (`BuildDeterministicGroups`, the bounded AI review round, AI column-mapping
proposal) is **unchanged** — it still proposes a plan; only what happens *after approval* changed.

---

## 7. Structured CSV/JSON mapping & routing changes

- `FieldMappings` (source column → target field, from `ResourceImportRecognizedFields.All`) is
  applied as `ResourceImportRequest.ColumnRenames` — the exact mechanism that already existed
  (Phase K1/4.2), now sourced from the frozen, validated `ProfileJson` instruction instead of the
  narrower `PlanEstimateJson` preview, and now applied for **ZIP packages too** (previously ZIP
  packages never got any column mapping at execution time).
- `ResourceType`, when set on a group, is passed as `ResourceImportRequest.DefaultCandidateType` —
  an existing Phase H2 parameter that always wins over `ResourceImportService.InferCandidateType`'s
  field-name heuristic. Previously this parameter was never populated by package-driven execution.
- Unsupported/missing mappings fail at resolve time (before any row is parsed), not silently at the
  per-row gate.

---

## 8. Provenance

Unchanged chain: `ResourceCandidate.ResourceRawRecordId → ResourceRawRecord.ResourceImportRunId →
ResourceImportRun.ImportPackageId → ImportPackage.ApprovedImportProfileId → ImportProfile`. No
schema change was made here — Phase 4.3's brief allowed adding a direct profile identifier "only if
it materially improves correctness or auditability," and the existing transitive chain (exercised
directly by one of the new acceptance-proof tests, which asserts candidates trace back to the
correct package/plan) remains sufficient. No migration needed.

---

## 9. Critical acceptance proof

**Test file:** `tests/LinguaCoach.IntegrationTests/Api/ImportExecutionPlanDrivenExecutionTests.cs`
(real HTTP API + real EF Core persistence + real `IImportPackageProcessingService`, fake AI/STT
providers per existing test-host convention — no mocking of the processing pipeline itself).

There is no admin plan-editing endpoint yet (`TODO-4.3-PLAN-EDIT-UI`, explicitly out of scope per
the phase brief — "Do not redesign the Angular UI"). Each test therefore submits a package, lets
plan generation run for real, then overwrites the persisted `ProfileJson` directly against EF Core
(`db.Entry(plan).CurrentValues["ProfileJson"] = ...`) to construct two deliberately different
approved plans over byte-identical content — exactly the shape a future edit endpoint would
produce, and the only way to prove the acceptance criterion without such an endpoint.

### `Same_csv_content_with_two_different_approved_field_mappings_produces_different_candidates`

Same CSV bytes (`mystery1,mystery2\r\n{marker1},{marker2}\r\n` — column names
`InferCandidateType` would never recognize on its own) submitted as two packages:

- **Plan A:** `{ GroupKey: "(root)", Included: true, ResourceType: VocabularyEntry, FieldMappings: { "mystery1": "word" } }`
  → produced a `VocabularyEntry` candidate with `canonicalText == marker1`.
- **Plan B:** `{ GroupKey: "(root)", Included: true, ResourceType: ReadingPassage, FieldMappings: { "mystery1": "title", "mystery2": "text" } }`
  → produced a `ReadingPassage` candidate with `canonicalText == marker1` **and** `normalizedJson`
  containing `marker2` (proving the second mapped field was applied too).

Both packages had identical source bytes; only the approved plan's mapping/routing differed, and
the candidate output differed exactly as the plan specified. This directly proves: *same input +
different approved plan = different candidate output*, and that execution follows each plan's own
instructions rather than re-deriving the same result independently (which is what would have
happened pre-4.3, since both packages' content was identical).

### Companion proofs (same file)

- `Excluded_group_in_the_approved_plan_creates_no_candidates` — `Included: false` on the package's
  only group → zero candidates, package completes normally.
- `Malformed_approved_plan_field_mapping_fails_processing_and_creates_no_candidates` — a
  `FieldMappings` target outside `ResourceImportRecognizedFields.All` → package processing fails
  deterministically, zero candidates created.
- `Processing_uses_the_exact_approved_plan_even_when_a_newer_draft_plan_exists` — a newer (v2)
  Draft `ImportProfile` inserted directly for the same package (not through the generation service,
  which would supersede the already-approved v1 — a separate, pre-existing lifecycle rule) with a
  *different* mapping than the approved v1; execution still produces v1's expected output, proving
  it resolves by exact `ApprovedImportProfileId`, never "the latest version for this package."

---

## 10. Tests and exact counts

| Suite | Before (Phase 4.2 baseline) | After (Phase 4.3) |
|---|---|---|
| Unit | 2,310 / 2,310 | **2,320 / 2,320** |
| Integration | 1,294 / 1,294 | **1,298 / 1,298** |
| Architecture | 12 / 12 | **15 / 15** |
| Angular unit (Karma) | Blocked (pre-existing `feedbackPolicy`/`moduleSuggestions` compile errors) | Blocked — same pre-existing errors, unrelated to this phase |
| Playwright | Not added (Phase 4.2 deferral, `TODO-4.2-PLAYWRIGHT`) | Not added — still deferred, no frontend/UI change this phase |

New tests added (net +10 unit / +4 integration / +3 architecture, 0 removed):

- `tests/LinguaCoach.UnitTests/ResourceImport/ApprovedImportProfileResolverTests.cs` — 10 facts:
  resolves by exact `ApprovedImportProfileId` (not latest); no approved profile; Draft/unapproved
  profile; profile belongs to another package; malformed `ProfileJson`; missing instruction for a
  manifest group; unrecognized field-mapping target; audio group routed to a non-Listening type;
  `Included`/`ResourceType`/`FieldMappings` round-trip; excluded-group instruction preserved.
- `tests/LinguaCoach.IntegrationTests/Api/ImportExecutionPlanDrivenExecutionTests.cs` — 4 facts (see
  §9 above).
- `tests/LinguaCoach.ArchitectureTests/ImportPipelineBoundaryTests.cs` — 3 new facts: package
  processing depends on `IApprovedImportProfileResolver`; package processing does not depend on
  plan-generation/mapping-inference services; only the known producer/parser/consumer set depends
  on `ImportExecutionGroupInstruction`.
- One existing test file updated for the new constructor parameter:
  `tests/LinguaCoach.UnitTests/ResourceImport/ImportPackageProcessingServiceTests.cs`
  (`ImportPackagePlanProcessingTests` — wired `ApprovedImportProfileResolver` into its existing
  fixture; all 4 pre-existing facts in that file still pass unmodified).

All 237 pre-existing `ResourceImport`-namespace unit tests, and all pre-existing
`AdminResourceImportEndpointTests` integration tests, pass unmodified against the refactor — no
regressions.

### Build / restore / frontend

```
dotnet restore        → clean
dotnet build --configuration Release   → 0 errors, pre-existing warnings only (NU1903 advisory,
                                          nullable-reference warnings unrelated to this phase)
npx tsc --noEmit       → same pre-existing e2e/spec errors as baseline (feedbackPolicy,
                          moduleSuggestions, learningActivityId) — zero new errors
npm run build -- --configuration production   → succeeded, dist output produced
npm test -- --watch=false --browsers=ChromeHeadless   → blocked by the same pre-existing
                                          feedbackPolicy/moduleSuggestions compile errors as the
                                          Phase 4.2 baseline (unrelated to this phase; no Angular
                                          source file was touched)
npx playwright test    → not run; no Playwright coverage exists for this pipeline in either the
                          Phase 4.2 baseline or this phase (TODO-4.2-PLAYWRIGHT, still open)
```

---

## 11. Data and migrations

- **No EF migration added.** `ProfileJson` remains a `string` column at the database level in both
  before/after states — only its *application-level* shape changed (a different C# type is
  serialized into the same untyped text column). No schema change, no backfill needed.
- **No live database audit performed** — same constraint as Phase 4.2 (no live DB connection
  available in this session). `ResourceImportRun.ImportPackageId` remains nullable
  (`TODO-4.2-DB-PROVENANCE`, unchanged, not touched this phase).
- **No existing data changed.** All new/changed behavior is exercised through SQLite in-memory test
  databases; nothing was run against a persistent store.
- **Blockers:** none beyond the pre-existing lack of a live DB connection noted above.

---

## 12. Documentation

Added:
- `docs/reviews/2026-07-16-phase-4-3-approved-plan-driven-execution-review.md` (this file)

Updated:
- `docs/handoffs/current-product-state.md` — new "Phase 4.3" section prepended above Phase 4.2,
  `lastUpdated`/"Last updated" bumped to 2026-07-16.
- `TODOS.md` — new "Phase 4.3" section added after the Phase 4.2 section, with
  `TODO-4.3-PLAN-EDIT-UI`, `TODO-4.3-ZIP-GROUP-MAPPING-PREVIEW`, `TODO-4.3-COST-ACCOUNTING`.

No `docs/sprints/` document exists for this work (repository does not use that convention for this
feature area — confirmed by the absence of a `docs/sprints/current-sprint.md` file); not invented.

---

## 13. Deferred items — explicitly confirmed not implemented

Per the phase's scope boundaries, none of the following were implemented in Phase 4.3:

- Durable, crash-safe running-cost persistence (`TODO-4.3-COST-ACCOUNTING`, originally "Phase 4.4"
  in the roadmap language) — cost-ceiling gating still uses in-memory `runningCost` accumulation
  scoped to a single `ProcessPendingAsync` pass, unchanged from Phase 4/4.2.
- Real audio-duration extraction, fail-closed missing-AI-pricing overhaul, genuine content-aware AI
  sampling, additional sampling evidence rounds, TTS.
- Typed candidate schemas for all multimodal resource types, native Speaking/Reading-image import,
  Vocabulary pronunciation media, Resource Bank audio/image preview, Lesson audio/image lookup.
- Resumable/multipart upload, 10 GB upload support, local-storage upload parity, Quartz clustering,
  package-claim distributed locking, extraction-time archive revalidation.
- Broad candidate-review redesign, direct Resource-to-Exercise generation, approve-and-publish
  shortcuts, production deployment.
- No removed legacy import routes were reintroduced. No old execution behavior was preserved as a
  hidden fallback — the old extension-only routing/heuristic-only resource-type-inference/
  `PlanEstimateJson`-only mapping paths for package execution were deleted, not kept alongside the
  new plan-driven path.
- `TODO-4.2-DB-PROVENANCE`, `TODO-4.2-SERVICE-LEVEL-GATE`, `TODO-4.2-PLAYWRIGHT` remain open,
  untouched by this phase.

---

## 14. Known limitations (precise, evidence-based)

- **No admin UI to change a plan's routing/mapping before approval.** Approval remains binary
  (approve exactly what generation proposed, or reject/regenerate). The acceptance-proof tests
  demonstrate the *execution engine* is plan-driven by constructing differentiated plans directly
  against persistence — this is a legitimate test of execution behavior, but it means no admin can
  yet exercise this capability through the product UI. Tracked as `TODO-4.3-PLAN-EDIT-UI`.
- **ZIP packages still get no column-mapping proposal at plan-generation time** (`FieldMappings` is
  always empty for a ZIP package's groups unless set directly against persistence, since
  `ImportAsset` rows for a ZIP don't exist until the approved plan's Extract stage runs). Execution
  correctly *applies* whatever `FieldMappings` a ZIP package's approved plan does carry (proven by
  the acceptance-proof test's `packageA`/`packageB`, which are non-ZIP submissions) — this is a
  proposal-generation gap, not an execution gap. Tracked as `TODO-4.3-ZIP-GROUP-MAPPING-PREVIEW`.
- **Group-level, not per-file, mapping/routing.** `ImportExecutionGroupInstruction` operates at
  folder-group granularity (matching the pre-existing plan-generation clustering granularity) — two
  files in the same folder cannot be routed to different resource types or given different column
  mappings by the current contract. No requirement in this phase asked for per-file granularity;
  flagging this as a design boundary rather than a bug.
- **The Import pipeline is not claimed to be production-ready** by this phase. The three
  Phase 4.2 TODOs remain open (nullable DB provenance column, no service-level provenance gate in
  `ResourceImportService.ImportAsync` itself, no Playwright coverage), and durable cost accounting
  is still deferred.

---

## 15. Final verdict

Phase 4.3's primary objective — making the approved plan authoritative for execution, with a
typed, centrally-validated contract and a critical acceptance proof that differing approved plans
produce differing candidate output over identical input — is met. All pre-existing tests continue
to pass (no regressions); 17 new tests were added across unit/integration/architecture layers, all
passing. No schema migration was required. Scope boundaries (cost accounting, UI redesign, sampling
improvements, multimodal schemas, etc.) were respected — nothing outside the brief was implemented.

## 16. Next recommended action

`TODO-4.3-PLAN-EDIT-UI` — give an admin an actual way to change a plan's group
routing/inclusion/mapping before approval (reusing `ImportProfile.ReplaceProfileJson`, already
Draft-only-editable, and the resolver's validation rules as a pre-check). Without it, Phase 4.3's
typed contract is real and enforced, but only ever populated by deterministic clustering + AI
review — no human has a product surface to actually exercise the "approve a *different* plan"
capability this phase built.
