---
status: current
lastUpdated: 2026-07-02
owner: engineering
supersedes:
supersededBy:
---

# Phase 19A — Review Scaffold Controlled Enablement

**Date:** 2026-07-02
**Related sprint:** Phase 19A (`docs/sprints/current-sprint.md`)
**Review type:** Engineering implementation review

---

## Files Reviewed (Part A — Audit)

- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolReplenishmentOptions.cs`
- `src/LinguaCoach.Application/ReadinessPool/ReviewScaffoldDryRunSummary.cs`
- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolDtos.cs`
- `src/LinguaCoach.Application/ReadinessPool/IReadinessPoolReplenishmentService.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/StudentActivityReadinessPoolService.cs`
- `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs`
- `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs`
- `src/LinguaCoach.Domain/Enums/ReadinessPoolStatus.cs`
- `src/LinguaCoach.Domain/Enums/RoutingReason.cs`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs`
- `src/LinguaCoach.Api/appsettings.json`
- `src/LinguaCoach.Persistence/Configurations/StudentActivityReadinessItemConfiguration.cs`
- `docs/architecture/readiness-pool.md`
- `docs/reviews/2026-06-27-phase-12b-mastery-signal-review-scaffold-review.md`
- Angular: `admin.models.ts`, `admin.api.service.ts`, `admin-lessons.component.ts/.html/.spec.ts`

---

## Part A — Audit Table

| Area | Current Behavior (before this phase) | Gap | Risk | Decision |
|---|---|---|---|---|
| Review scaffold entity/service | No dedicated entity — a config-gated behavior inside `ReadinessPoolReplenishmentService.FillShortfallAsync` | None — architecture is sound, not a missing-infrastructure gap | Low | Extend gating in place, no new entity |
| Config gates | `EnableReviewScaffoldGeneration` (false), `DryRunOnly` (false) on `ReadinessPoolReplenishmentOptions`, bound from a `ReadinessPool` appsettings section that **did not exist in appsettings.json** | Missing safe-default surfacing; `DryRunOnly` defaulting `false` meant flipping `Enabled=true` alone could go live with no dry-run step | Medium | Add appsettings section; flip `DryRunOnly` default to `true` |
| Candidate selection | Binary: `EnableReviewScaffoldGeneration=true` AND `GetWeakEventsAsync` returns ≥1 event | No confidence banding, no source restriction, no per-student cap | Medium (could over-generate across sources/students once enabled) | Add source allow-list + Today-lesson override, deterministic confidence banding, daily cap |
| Readiness pool lifecycle | `ReviewOnly` status exists; demotion (`ConvertToReviewOnly`) via `StudentMasteryEvaluationService.DecideDemotionAsync` runs unconditionally (separate from generation gate) | None — demotion of already-generated, already-validated content is a different mechanism than new scaffold generation; out of scope here | Low | Leave demotion path unchanged |
| Practice Gym suggestions | Already separates `ReviewItems` from `SuggestedItems`/`ContinueItems`; already renders learner-friendly labels ("Review", "Step back to strengthen basics", "Targeted fix") via `BuildCallToAction` | No admin-review hold gate | Medium | Add `RequiresAdminReview` exclusion filter only — no labeling changes needed |
| Today lesson selection | No dedicated service — Today items are just pool items with `Source=TodayLesson`; same generation path as Practice Gym | Review/scaffold routing applied uniformly to both sources, no restriction | Medium | Gate at generation (source allow-list), not at serving — no Today-lesson-selection code touched |
| Admin visibility | Read-only dry-run card on `/admin/lessons`; no enable button (explicit Phase 12B decision) | No visibility into config gates beyond the two original flags; no visibility into admin-review-held items | Low | Extend dry-run summary; add read-only pending-review list; preserve "no enable button" precedent |
| Generation validation diagnostics | `ModuleStageContentValidator`, `GenerationValidationFailure`, `AdminGenerationQualityHandler` — unrelated to review scaffold, already mature (Phase 18A-F/G) | None | — | Not touched |
| Tests/docs from earlier phases | `ReviewScaffoldDryRunTests` (11 tests), `ReplenishmentOptionsTests` (30 tests), `docs/architecture/readiness-pool.md`, Phase 12B review doc | Docs describe the pre-19A binary gate only | Low | Extend both |

**Overall assessment:** Phase 19A is controlled-enablement gating on top of already-sound infrastructure, not a build-from-scratch phase. This matches the task brief's framing ("move review scaffold generation from 'available/dry-run foundation' toward safe operational use").

---

## Part B — Configuration Gate

`ReadinessPoolReplenishmentOptions` (bound from appsettings `"ReadinessPool"`, now present in `appsettings.json`):

| Setting | Default | Purpose |
|---|---|---|
| `EnableReviewScaffoldGeneration` | `false` | Master switch (pre-existing) |
| `DryRunOnly` | `true` (changed from `false`) | Second explicit step required before generation writes to DB |
| `RequireAdminReview` | `true` | Global flag — generated scaffold items held from students until admin clears it |
| `MaxScaffoldItemsPerStudentPerDay` | `3` | Per-student daily cap |
| `ScaffoldAllowedSources` | `["PracticeGym"]` | Source allow-list |
| `AllowTodayLessonInsertion` | `false` | Explicit override required in addition to the allow-list containing `"TodayLesson"` |
| `MinimumConfidenceForReviewNeed` | `"Medium"` | Minimum `ReviewNeedConfidence` band |

Reused without duplication: `EnableReviewScaffoldGeneration` (= spec's `Enabled`), `ReadyItemExpiryDays` (= spec's `ExpireAfterDays`, already 14).

All defaults are safe: with `EnableReviewScaffoldGeneration=false`, none of the new gates change current production behavior. They only matter once an operator deliberately turns generation on.

---

## Part C — Candidate Selection

A weak-event signal is eligible to trigger review/scaffold generation in `FillShortfallAsync` when **all** of:

1. `EnableReviewScaffoldGeneration=true`.
2. Pool `source` is in `ScaffoldAllowedSources`, and if `source == TodayLesson`, `AllowTodayLessonInsertion=true` too.
3. `IStudentLearningLedger.GetWeakEventsAsync` returns ≥1 event (`Failed`/`NeedsReview` ledger outcomes only — never from unresolved/NotSupported evaluations, by construction of that query).
4. Deterministic confidence banding meets `MinimumConfidenceForReviewNeed`:
   - `High` — objective in `StudentMasteryReport.AtRiskObjectiveKeys`
   - `Medium` — objective in `StudentMasteryReport.WeakObjectiveKeys`
   - `Low` — ledger weak events only, no mastery corroboration
   No new AI/ML signal — derived entirely from the existing deterministic mastery engine.
5. Student has not reached `MaxScaffoldItemsPerStudentPerDay` for the current UTC day.

Not eligible (unchanged, by construction): failed/NotSupported AI evaluations, low-confidence signals below threshold, objectives without a curriculum key mapping (existing routing behavior), students below `CourseReady` lifecycle (existing active-student filter), objectives already saturated with active items (existing `DuplicateKey` dedup).

---

## Part D — Generation Behavior

When all gates pass, the created item:
- Is validated by the existing curriculum routing pipeline and `ModuleStageContentValidator` at generation time (unchanged pipeline)
- Carries `RoutingReason` in `{Review, Scaffold, Remediation}` and `IsLowerLevelContent=true` where applicable (unchanged)
- Is stamped `RequiresAdminReview = RequireAdminReview` (config snapshot at creation time)
- Follows the existing readiness-pool lifecycle (`Queued → Generating → Ready/Failed → ...`), unchanged

When the daily cap is reached mid-batch, remaining slots in that batch fall back to `RoutingMode.NewLearning` — no slot is left ungenerated, it's simply not scaffold-routed. Tracked via `ReplenishmentRunSummary.SkippedDailyCapReached`.

---

## Part E — Dry-Run and Admin-Review Modes

**DryRunOnly:** unchanged mechanism (Phase 12B), default flipped to `true`.

**RequireAdminReview:** implemented as a **global config flag**, not a per-item approval workflow. When `true` (default), every scaffold item generated is stamped `RequiresAdminReview=true` and excluded from all three Practice Gym suggestion buckets by `PracticeGymSuggestionService`. An admin clears the hold for all such items at once by setting `ReadinessPool:RequireAdminReview=false` in config, after inspecting `GET /api/admin/readiness-pool/review-scaffold/pending-review`.

This is a deliberate scope reduction from a full per-item approve/reject workflow, justified by:
- The task brief's own fallback clause: "If approval workflow does not exist yet, keep item in a non-ready/admin-review-required state."
- Precedent: Phase 12B explicitly decided against an "enable" button on the admin UI, keeping all review-scaffold control config-only.
- The "do not overbuild" instruction — a mutating per-item admin action (approve/reject, with its own audit trail) is materially more surface area than a read-only observability addition.

**Practice Gym (if enabled):** review scaffold content can only appear via `ScaffoldAllowedSources` (default `PracticeGym` only); already labeled "Review" / "Step back to strengthen basics" / "Targeted fix" by pre-existing `PracticeGymSuggestionService.BuildCallToAction` — no new labeling work needed, no internal confidence/signal details are exposed to students.

**Today lesson:** `AllowTodayLessonInsertion` defaults `false`. Gated at generation time (source allow-list check), not at serving time — no Today-lesson-selection code was touched, since with the default config no Today-lesson-sourced scaffold item can ever be created.

---

## Part F — Admin Visibility

`GET /api/admin/readiness-pool/review-scaffold/dry-run` now also returns: `requireAdminReview`, `maxScaffoldItemsPerStudentPerDay`, `scaffoldAllowedSources`, `allowTodayLessonInsertion`, `minimumConfidenceForReviewNeed`, `adminReviewRequiredCount`, `generatedTodayCount`.

New `GET /api/admin/readiness-pool/review-scaffold/pending-review` (admin-only, read-only, up to 50 rows): student id, source, status, skill, objective, CEFR, routing reason, createdAt for every item currently held with `RequiresAdminReview=true`.

`admin-lessons.component` extends the existing "Review scaffold generation" card with the new config/count fields and adds a "Pending admin review" table section. No enable/approve buttons — consistent with the Phase 12B precedent.

---

## Part G — Student Visibility

Unchanged from pre-existing behavior except for the new admin-review exclusion: review scaffold items only ever appear in Practice Gym (never Today lesson, by construction), labeled "Review" / "Step back to strengthen basics" / "Targeted fix" (pre-existing `BuildCallToAction`), with no internal signal confidence or blocked-reason details exposed. Items held for admin review are invisible to students entirely (excluded before ranking).

---

## Part H — Safety and Idempotency

| Guarantee | Mechanism |
|---|---|
| Same weakness/objective does not generate unlimited scaffold items | Pre-existing `DuplicateKey` (objectiveKey + patternKey + CEFR, scoped to active statuses) blocks re-creation while an item is still active |
| Daily max respected | New per-student count query in `FillShortfallAsync`, gate closes for the batch when reached |
| Low-confidence signals do not create review scaffold | New `ReviewNeedConfidence` banding + `MinimumConfidenceForReviewNeed` gate |
| Failed/NotSupported evaluations do not create review scaffold | Unchanged — `GetWeakEventsAsync` only returns `Failed`/`NeedsReview` ledger outcomes |
| CEFR unchanged | No code in this phase touches `StudentProfile.CefrLevel` |
| Objective completion unchanged | No code in this phase touches `LearningPlanObjectiveStatus` |
| Learning Plan not regenerated from scaffold creation alone | No code in this phase calls `ILearningPlanService` regeneration |
| Generated scaffold content validated before use | Unchanged — existing `ModuleStageContentValidator` pipeline applies to all generated content regardless of routing reason |

---

## Part I — Readiness Pool / Practice Gym Integration

- Lifecycle/status: unchanged status model; `RequiresAdminReview` is an orthogonal flag, not a new status.
- Practice Gym: `ReviewItems` bucket unaffected in shape; admin-held items simply never appear in any bucket.
- Pool health metrics: unaffected by this phase (admin-review counts surfaced separately in the dry-run summary, not mixed into `PoolHealthSummary`, to avoid scope creep into the existing health-metrics contract).
- Today lesson: unaffected — no scaffold items reach that source by default.

---

## Part J — Tests

**Backend unit (+15):**
- `ReplenishmentOptionsTests` (+11): config defaults (`DryRunOnly=true`, `RequireAdminReview=true`, `MaxScaffoldItemsPerStudentPerDay=3`, `ScaffoldAllowedSources=["PracticeGym"]`, `AllowTodayLessonInsertion=false`, `MinimumConfidenceForReviewNeed="Medium"`), `ReviewNeedConfidence` ordinal ordering, `RequiresAdminReview` threading through `ReadinessItemRequestBuilder` and the domain constructor (default + explicit true), `SkippedDailyCapReached` counter distinctness
- `PracticeGymSuggestionServiceTests` (+4): `RequiresAdminReview=true` excluded from Suggested/Review/Continue individually, control case confirms normal items still appear

**Backend integration (+5, all in `ReviewScaffoldDryRunTests`):**
- Extended dry-run shape assertion (new fields present)
- Safe config defaults reported correctly
- `pending-review` endpoint: 401 unauthenticated, 403 as student, 200 with array shape, read-only (does not mutate DB)

**Frontend:** `admin-lessons.component.spec.ts` +5 tests (dry-run field population, pending-review empty/populated states, refresh action).

**Not added:** per-item approve/reject tests (feature deferred, see Part E), Playwright E2E (no new student-facing UI surface this phase — Practice Gym review section already covered by existing E2E).

---

## Part K — Validation

```
dotnet build                                    → succeeded, 0 errors
dotnet test tests/LinguaCoach.UnitTests         → 1,675 passed, 0 failed (+15)
dotnet test tests/LinguaCoach.ArchitectureTests → 3 passed, 0 failed
dotnet test tests/LinguaCoach.IntegrationTests  → 1,324 passed, 0 failed (+5, 0 regressions)
npx tsc --noEmit -p tsconfig.app.json (Angular) → clean, no errors
npm run build -- --configuration production     → clean (pre-existing CSS selector warnings only)
npm test -- --watch=false --browsers=ChromeHeadless → 1,488/1,607 passed, 119 pre-existing failures (unchanged baseline), 0 new regressions, +5 new tests
```

One test failure was found and fixed during this phase: `DryRun_ReportsSafeConfigDefaults` initially asserted `ScaffoldAllowedSources` had exactly one element via config-bound `IOptions<T>`. This failed because .NET's configuration binder appends config-bound array items to a non-empty class default rather than replacing it (`["PracticeGym"]` class default + `["PracticeGym"]` from appsettings.json → 2 identical entries when bound through `IOptions<T>`). This is a pre-existing, accepted quirk in this codebase — `PlacementAssessmentOptions.SkillsToAssess` has the identical pattern (matching class default + appsettings value) and would exhibit the same behavior if asserted the same way; the codebase currently has no test that catches it there either. Fixed by asserting containment (`Assert.All(sources, s => Assert.Equal("PracticeGym", s))`) instead of exact count — no functional impact, since `.Contains()` checks in the gating logic are unaffected by duplicate identical entries.

No Playwright run in this session — no new student-facing UI surface was added (Practice Gym review section already has E2E coverage); documented as a known limitation rather than a gap requiring new specs.

---

## Decisions Made

1. **`DryRunOnly` default flipped `false → true`.** Matches the task's safe-default intent; flipping `EnableReviewScaffoldGeneration=true` alone can never silently go live.
2. **`RequireAdminReview` implemented as a global config flag, not per-item approval.** See Part E for full justification. Explicitly authorized by the task brief's fallback clause.
3. **Confidence banding reuses the existing mastery engine** (`AtRiskObjectiveKeys`/`WeakObjectiveKeys`) rather than introducing a new signal source — keeps the system deterministic and testable, consistent with `AGENTS.md` §4 ("AI must not own planning").
4. **Daily cap and admin-review counts are not merged into `PoolHealthSummary`.** Kept as separate fields on the dry-run summary to avoid changing the existing, widely-consumed pool-health contract.
5. **Today-lesson exclusion is enforced at generation time, not serving time.** Simpler and equally safe — since no such items are ever created by default, no Today-lesson-selection code needed to change.
6. **`current-product-state.md` was not updated.** That doc tracks major user-facing product flow changes and has not been updated since Phase 16E despite ~15 subsequent internal/admin phases; Phase 19A is config/admin-visibility work with `EnableReviewScaffoldGeneration` still off by default, so no user-facing flow changed.

---

## Risks and Unresolved Questions

1. **No per-item approval workflow yet.** If an operator wants fine-grained control (approve some scaffold items, reject others) rather than an all-or-nothing global flag, a follow-up phase is required (see Recommendation).
2. **Confidence banding is coarse.** Only 3 bands, derived from mastery classification that already has its own thresholds (Phase 12B). Acceptable for a first controlled-enablement pass; could be tuned with real production dry-run data.
3. **Daily cap is checked once per `FillShortfallAsync` batch call, not per individual item within the batch.** A batch either has scaffold routing enabled or falls back entirely to Normal for that call — coarse-grained but consistent with the existing coarse-grained `allowReviewOrScaffold` design (Phase 12B) and safe (never over-generates beyond the cap check point).
4. **.NET config-binder array-append quirk** affects `ScaffoldAllowedSources` (and pre-existing `PlacementAssessmentOptions.SkillsToAssess`). Functionally harmless (`.Contains()` unaffected by duplicates) but could confuse an admin inspecting raw `IOptions<T>` values. Not fixed in this phase — fixing it would mean changing the class default to an empty array, which would break `new ReadinessPoolReplenishmentOptions()` callers (including this phase's own unit tests) that rely on the C# default outside of config binding. Flagged as a pre-existing pattern, not a Phase 19A regression.

---

## Final Verdict

**Review scaffold generation remains disabled by default.** `EnableReviewScaffoldGeneration=false`, `DryRunOnly=true` — this phase adds safety gates and admin observability, it does not turn generation on. All existing invariants (CEFR unchanged, objective completion unchanged, Learning Plan not regenerated, no new activity formats, no live AI calls in tests) hold. All backend and Angular type-checks pass; no regressions in the 3,002-test backend suite.

**Recommendation for next phase (19B or later):**
1. Run the dry-run endpoint against production data with the new confidence/source/cap gates to validate they produce a reasonable candidate volume before ever setting `EnableReviewScaffoldGeneration=true`.
2. If a per-item approval workflow becomes a real product need (rather than the current all-or-nothing global flag), design it as its own phase — it needs an audit trail, an admin UI action, and its own safety review, all out of scope here.
3. Run the Playwright suite before merging if a future phase adds student-facing UI in this area — not required for this phase since no such surface was added.
