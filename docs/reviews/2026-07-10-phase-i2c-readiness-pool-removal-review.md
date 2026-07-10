# Phase I2C — Readiness Pool Removal (Pass C, final pass of I2)

**Date:** 2026-07-10
**Related sprint / feature:** I-track (Import pipeline unification → I1, legacy-fallback deletion
→ I2, final nav consolidation → I3). This is Pass C — the final pass of I2. Pass A
(`docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md`) deleted the
Practice-Gym-side legacy generation pipeline. Pass B
(`docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md`) deleted the Today-side
legacy generation pipeline and confirmed both `ReadinessPoolSource.PracticeGym` and
`.TodayLesson` had zero live consumers left.

## Context / decision

With both Today and Practice Gym confirmed to have zero readers of the readiness pool after
Passes A and B, this pass deletes `StudentActivityReadinessItem`/
`IStudentActivityReadinessPoolService`/`ReadinessPoolReplenishmentService` entirely, and narrows
`IAiActivityGenerator` to the one method it still needs
(`EvaluateAttemptAsync` — attempt scoring/feedback, unrelated to content generation).

This turned out to be a much larger blast radius than the readiness pool's own service/entity: it
had load-bearing (if largely diagnostic/side-effect) tendrils into `AdminAiOperationsController`,
the runtime feature-gate registry, `StudentReadinessAuditService`/`StudentPilotReadinessRepairService`
(Phase 20D), `LearningPlanService` (Phase 12D), `StudentMasteryEvaluationService` (Phase 10Z),
`PracticeGymSuggestionService`, and several admin frontend pages. Each is covered below.

## Files reviewed

Backend: `StudentActivityReadinessItem.cs`, `StudentActivityReadinessItemConfiguration.cs`,
`IStudentActivityReadinessPoolService.cs`/`StudentActivityReadinessPoolService.cs`,
`ReadinessPoolReplenishmentService.cs`/`ReadinessPoolReplenishmentJob.cs`,
`AdminReadinessPoolController.cs`, `IAiActivityGenerator.cs`/`AiActivityGeneratorHandler.cs`,
`StudentActivityUsageLog.cs`/`StudentActivityUsageLogConfiguration.cs`, `ActivityFeedbackSignal.cs`/
`ActivityFeedbackSignalConfiguration.cs`, `ActivitySubmitHandler.cs`, `ActivityFeedbackHandler.cs`,
`PracticeGymSuggestionService.cs`, `PracticeGymSuggestionsController.cs`,
`StudentReadinessAuditService.cs`, `StudentPilotReadinessRepairService.cs`,
`StudentReadinessRepairActions.cs`, `LearningPlanService.cs`, `StudentMasteryEvaluationService.cs`,
`IStudentMasteryEvaluationService.cs`, `StudentMasteryReport.cs`, `ReadinessDemotionDecision.cs`,
`RuntimeSettingsService.cs`, `FeatureGateDefinitions.cs`, `FeatureGateEnums.cs`,
`AdminAiOperationsController.cs`, `AdminAiOperationsDtos.cs`, `LinguaCoachDbContext.cs`,
`DependencyInjection.cs`, `QuartzConfiguration.cs`, every test file referencing the deleted types.
Frontend: `admin.models.ts`, `admin.api.service.ts`, `admin-ai-operations.component.ts`/`.html`/
`.spec.ts`, `admin-feature-gates.component.ts`/`.spec.ts`, `admin-lessons.component.ts`/`.html`/
`.spec.ts`, `admin-student-detail.component.ts`/`.html`/`.spec.ts`,
`practice-gym-suggestions.service.ts` (read, not modified).

## Findings and decisions, by priority

### P0 — Core deletion: entity, service, replenishment job

Deleted entirely:

- `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs`
- `src/LinguaCoach.Persistence/Configurations/StudentActivityReadinessItemConfiguration.cs`
- `src/LinguaCoach.Domain/Enums/ReadinessPoolSource.cs`, `ReadinessPoolStatus.cs`,
  `ReadinessDemotionDecision.cs`
- `src/LinguaCoach.Application/ReadinessPool/` (whole folder: `IStudentActivityReadinessPoolService`,
  `AggregatePoolHealthSummary`, `PoolHealthSummary`, `ReadinessPoolDtos`,
  `ReadinessPoolReplenishmentOptions`, `ReviewNeedConfidence`, `ReviewScaffoldDryRunSummary`,
  `IEffectiveReadinessPoolSettingsProvider`, `IReadinessPoolReplenishmentService`)
- `src/LinguaCoach.Infrastructure/ReadinessPool/` (whole folder: `StudentActivityReadinessPoolService`,
  `ReadinessPoolReplenishmentService`, `EffectiveReadinessPoolSettingsProvider`)
- `src/LinguaCoach.Infrastructure/Jobs/ReadinessPoolReplenishmentJob.cs`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` (disposition below)

`LinguaCoachDbContext.cs`: removed the `DbSet<StudentActivityReadinessItem>` and its xmin
concurrency-token `OnModelCreating` block. `DependencyInjection.cs`: removed the readiness-pool
service/replenishment-options/job registrations. `QuartzConfiguration.cs`: removed the
`readiness-pool-replenishment` trigger (every 20 min).

`RoutingReason` enum (`Domain/Enums/RoutingReason.cs`) was **not** deleted — confirmed via grep it
is used more broadly by `CurriculumRoutingService`/`CurriculumRoutingRecommendation`, unrelated to
the readiness pool specifically.

### P0 — `AdminReadinessPoolController` disposition: delete + relocate one survivor

Read the full ~700-line controller first. Confirmed via grep that of its ~12 routes, 9 (`GetReadinessPool`,
`GetPoolHealth`, `GetAggregatePoolHealth`, `GetReviewScaffoldDryRun`,
`GetReviewScaffoldPendingReview`, `ApproveReviewScaffoldItem`, `RejectReviewScaffoldItem`,
`ReopenReviewScaffoldItem`, `GetReviewScaffoldPilotSummary`) query the deleted entity/service
directly and are now dead. The remaining 3 (`GetMasteryValidationSummary`, `GetLearningPlan`,
`GetLearningPlanProgress`) are unrelated read-only diagnostics.

**Disposition decisions (explicitly flagged):**

- `GetLearningPlan` (`api/admin/students/{id}/learning-plan`) and `GetLearningPlanProgress`
  (`api/admin/students/{id}/learning-plan/progress`) — grepped the frontend
  (`admin.api.service.ts`) and found `getLearningPlanProgress()` calls a **different** route,
  `api/admin/students/{id}/learning-plan-progress` (hyphenated, no slash), already served by an
  existing, separate controller: `AdminStudentLearningPlanController.cs`. These two routes on
  `AdminReadinessPoolController` are genuine dead code — the frontend never called them. Deleted
  along with the rest of the controller.
- `GetMasteryValidationSummary` (`api/admin/mastery/validation-summary`) — **is** called by the
  frontend (`getMasteryValidationSummary()` in `admin.api.service.ts`, consumed by
  `admin-lessons.component.ts`). Relocated verbatim to a new, minimal
  `src/LinguaCoach.Api/Controllers/AdminMasteryController.cs` (same route, same DTO, same
  dependencies: `IStudentMasteryEvaluationService`, `IStudentLearningLedger`,
  `LinguaCoachDbContext`) rather than left orphaned.

`AdminReadinessPoolController.cs` itself deleted entirely after the relocation.

### P0 — `StudentActivityUsageLog`/`ActivityFeedbackSignal` FK cleanup

Both entities had a `StudentActivityReadinessItemId` FK column. Removed the property, constructor
parameter, and EF `HasOne<StudentActivityReadinessItem>()`/property mapping from both
`StudentActivityUsageLog`/`StudentActivityUsageLogConfiguration` and
`ActivityFeedbackSignal`/`ActivityFeedbackSignalConfiguration`. Fixed their two callers:
`ActivitySubmitHandler.TryWriteUsageLogAsync` (the lookup this item's brief specifically called
out) and `ActivityFeedbackHandler.HandleAsync` (both the create and update-ratings paths), which
both previously threaded `studentActivityReadinessItemId` through from a readiness-item lookup.

`ActivitySubmitHandler.cs` additionally had two more direct readiness-item dependents not
mentioned in the original task list but a direct compile-time (and logical) consequence of
deleting the entity:

- `TryGetReadinessObjectiveKeyAsync` — deleted; its two call sites now assign
  `string? …ObjectiveKey = null;` inline instead.
- `TryConsumeReadinessItemAsync` — deleted along with its 3 call sites (nothing to consume once no
  readiness item can ever exist). The now-unused `IPracticeGymSuggestionService` constructor
  dependency was removed from `ActivitySubmitHandler` as a result.

### P0 — `IAiActivityGenerator` narrowing

Removed `GenerateActivityContentAsync` and `ActivityGenerationContext` from
`IAiActivityGenerator.cs`. Confirmed via grep zero remaining callers in `src/` (every call site
was already inside files deleted by Passes A/B). `EvaluateAttemptAsync`/`ActivityEvaluationContext`
kept untouched — still used by `ActivitySubmitHandler.cs` for attempt scoring/feedback.

In `AiActivityGeneratorHandler.cs`, removed the generation-only private helpers: `StagedPatternKeys`,
the three `Generate*PromptKey` constants, `LoadCountSettingsAsync`, `ValidateStagedContent`/
`TryValidateStagedContent`/`TryValidateJsonOnly`, `ValidateWritingActivityJson`,
`LogValidationFailureAsync`, `ValidateIsJson`. Kept `BuildWritingEvaluationContent`,
`BuildSpeakingEvaluationContent`, `CleanJson`, `EvaluateWritingPromptKey`/
`EvaluateSpeakingRolePlayPromptKey` — all still used by `EvaluateAttemptAsync`. The `LinguaCoachDbContext`
and `ILogger` constructor dependencies became fully unused after removing the generation path and
were dropped.

**Rename decision:** left `IAiActivityGenerator`/`AiActivityGeneratorHandler` named as-is. The
interface now has one method (`EvaluateAttemptAsync`), so "Generator" is a mild misnomer — judged
that renaming (touching the DI registration and every reference) is more churn than value for a
single remaining method. Documented this explicitly in the interface's doc comment so a future
reader isn't confused. Fixed the one test-suite fake implementing this interface
(`AlwaysFailingAiActivityGenerator` in `ActivityEndpointTests.cs`) to drop its now-removed
`GenerateActivityContentAsync` override.

### P0 — `PracticeGymSuggestionService`: gutted to permanent no-ops

`StartSuggestionAsync`/`TryMarkConsumedAsync` queried `_db.StudentActivityReadinessItems` directly
(not via the deleted pool service, so this wasn't in the original scan). `GetSuggestionsForStudentAsync`
called the deleted `IReadinessPoolReplenishmentService.GetHealthAsync`.

**Decision:** since `SuggestedItems`/`ContinueItems`/`ReviewItems` have been permanently empty
since Pass A (Phase I2A), no `readinessItemId` a frontend could ever legitimately obtain refers to
a real row — even before this pass. Gutted rather than removed from the interface:

- `StartSuggestionAsync` → always returns `{ Success = false, FailureReason = "Item not found." }`,
  no DB access.
- `TryMarkConsumedAsync` → no-op (`Task.CompletedTask`), no DB access.
- `GetSuggestionsForStudentAsync` → `ReadyCount`/`ReviewOnlyCount`/`IsReplenishmentRecommended`
  hardcoded to `0`/`0`/`false` (no pool to report on); `_moduleSelector`-driven `ModuleSuggestions`
  (Phase H7) is unaffected and remains the sole real content.

Kept the `IPracticeGymSuggestionService` interface, `PracticeGymSuggestionsController`'s `/start`
and `/complete` routes, and the Angular `practice-gym-suggestions.service.ts` calls **unchanged**
for API-contract stability, rather than doing a broader Practice Gym suggestions UI teardown out
of scope for this pass. **Flagged as a residual**: these are now permanently-unreachable no-op
code paths (route handlers that can never do anything), left for a future cleanup pass.

### P0 — `StudentReadinessAuditService`/`StudentPilotReadinessRepairService` (Phase 20D): surgical strip, not deletion

Both services are genuinely live, still-valuable admin diagnostics (`GET
api/admin/students/{id}/readiness`, `POST .../readiness/repair`) unrelated in concept to the
readiness pool — they answer "can this student use the app end-to-end today?" But several of
their individual checks/actions queried `_db.StudentActivityReadinessItems` or depended on
`IReadinessPoolReplenishmentService`/`IEffectiveReadinessPoolSettingsProvider` directly.

**`StudentReadinessAuditService`** — removed:
- The "Practice Gym pool health" + "pilot gate visibility" checks — replaced with a single
  informational `practicegym.module_based` check noting Practice Gym is now served by the H7
  module pipeline (no per-student pool to audit).
- The "activity content validity" category (pattern-key/malformed-content checks against
  ready/reserved readiness items) — removed entirely (nothing left to check without the pool).
- The listening-activity-audio-for-ready-items check inside "Audio/TTS" — removed; the
  "TTS generation setting" check in the same category survives unchanged.
- The "Feedback/completion" category's stale-reserved-item / stale-readiness-item /
  pending-review-scaffold checks — removed entirely.
- Constructor no longer depends on `IReadinessPoolReplenishmentService`/
  `IEffectiveReadinessPoolSettingsProvider`.

Kept unchanged: account/access, placement/CEFR, Learning Plan existence, course readiness, Today
lesson exercise-type availability, and progress/mastery checks — none of these touch the readiness
pool.

**`StudentPilotReadinessRepairService`** — removed the two readiness-pool-specific repair actions
(`ExpireInvalidReadinessItems`, `ExpireStaleReservedItems`) and their implementations
(`RepairExpireInvalidReadinessItemsAsync`, `RepairExpireStaleReservedItemsAsync`,
`IsBelowCurrentLevel` helper). `RunAllSafeRepairsAsync`'s action list shrank from 4 to 2
(`GenerateLearningPlanIfMissing`, `RefillTodayLessonIfEmpty`, both kept unchanged). Constructor no
longer depends on `IEffectiveReadinessPoolSettingsProvider`.

`StudentReadinessRepairActions.cs` (the static action registry): removed the
`ExpireInvalidReadinessItems`/`ExpireStaleReservedItems` definitions; `RefillPracticeGymIfEmpty`
(already `IsImplemented = false`, unrelated to the entity deletion) left as-is.

### P0 — `LearningPlanService`/`StudentMasteryEvaluationService`: surgical strip of live features

These are the most delicate files in this pass — both are genuinely live, still-used product
features (Learning Plan, mastery evaluation), not legacy generation code, but each had readiness-pool
queries woven into otherwise-independent logic.

**`LearningPlanService.GetProgressAsync`** had exactly one readiness-pool touchpoint: `LessonQueueLength`
was computed by counting `Ready`-status, `LessonBatch`-sourced readiness items. Since
`ReadinessPoolSource.LessonBatch` was already confirmed dead since Pass B (its only writer,
`LessonBatchGenerationJob`, was deleted then), this count was already always `0` before this pass.
Replaced the query with a hardcoded `0` and a comment — **zero behavior change**, this field's
value was already `0` in every live scenario.

**`StudentMasteryEvaluationService`** — determined the readiness-item demotion mechanism
(`EvaluateReadinessItemFitAsync`/`EvaluateAndDemoteReadinessItemsAsync`/`DecideDemotionAsync`/
`ApplyDecision`/`TerminalStatuses`) was a **side effect bolted onto** `EvaluateStudentAsync`, not
part of the core CEFR/objective mastery classification math (`ComputeSignal`/`ClassifyStatus`/
`EvaluateObjectiveMasteryAsync`, all untouched). Its purpose was: "when a student masters an
objective, demote any of their still-Ready/Reserved readiness items targeting it so they don't get
served stale content" — a mechanism with zero effect once no readiness items exist. Removed the
whole demotion mechanism (both interface methods, all four private helpers, the
`LinguaCoachDbContext` dependency it existed solely to support). `EvaluateStudentAsync` now returns
`DemotedCount = 0` unconditionally (previously computed from the now-deleted sweep) —
`StudentMasteryReport.DemotedCount` kept on the record (not removed) to avoid churning every
caller, documented as permanently `0`. `StudentMasteryEvaluationJob.cs` (which logs/aggregates
`DemotedCount`) required **no changes** — it just reads a field that's now always `0`, harmless.

`IStudentMasteryEvaluationService` interface narrowed to `EvaluateStudentAsync`/
`EvaluateObjectiveMasteryAsync` only.

### P1 — Runtime feature gates: two groups deleted, one backing-store rename judgment call

`FeatureGateDefinitions.cs`: deleted the `ReviewScaffoldGeneration` and
`PracticeGymReviewScaffoldPilot` feature-gate groups — every setting they exposed
(`ReadinessPool.EnableReviewScaffoldGeneration`, `.DryRunOnly`, `.RequireAdminReview`,
`.MaxScaffoldItemsPerStudentPerDay`, `.ScaffoldAllowedSources`, `.AllowTodayLessonInsertion`,
`.MinimumConfidenceForReviewNeed`, `.PracticeGymPilotEnabled`, `.PracticeGymPilotLabel`,
`.PracticeGymPilotReason`, `.MaxStudentVisibleScaffoldSuggestions`) was a property on
`ReadinessPoolReplenishmentOptions`, deleted with the rest of the pool. Any pre-existing
`RuntimeSettingOverride` DB rows for those keys are now orphaned but harmless (nothing reads
them) — same disposition Pass A used for the Form.io template pilot gate.

`ActivityFeedbackPolicy` (Phase B2, `ActivityFeedback.TodayPolicy`/`.PracticeGymPolicy`) also uses
`FeatureGateBackingStore.ReadinessPoolOverride` but is **not** readiness-pool-specific — it's a
generic `RuntimeSettingOverride`-backed flag that happens to share the same backing-store enum
value. Left this group and its two settings completely unchanged; `RuntimeSettingsService.cs`'s
`GetReadinessPoolCurrentJson` method (renamed `GetGenericOverrideCurrentJson`) now only handles the
`PracticeGymFormIoPilot.Enabled` and `ActivityFeedback.*` keys, with a doc comment explaining the
backing-store enum name (`ReadinessPoolOverride`) is now a misnomer for its one remaining
consumer. **Judgment call**: did not rename the `FeatureGateBackingStore.ReadinessPoolOverride`
enum value itself — same "not worth the churn for a naming accuracy fix" reasoning as the
`IAiActivityGenerator` decision above.

`FeatureGateCategory.ReviewScaffoldPracticeGymPilot` (backend) and the matching TS union literal
`'reviewScaffoldPracticeGymPilot'` (frontend `admin.models.ts`) removed — no remaining group uses
this category. `FeatureGateCategory.ReadinessPoolLessonGeneration` (backend enum) and its frontend
counterpart kept — still used by 3 live, unrelated `LessonGenerationSettings`-backed groups
(`lesson-generation-buffer`, `tts-generation`, `practice-gym-generation-per-type`).

### P1 — `AdminAiOperationsController`: readiness-pool section removed

`GetSummary` computed `pendingReviewCount`/`approvedCount` from `_db.StudentActivityReadinessItems`
and packaged them with `ReadinessPoolReplenishmentOptions` config into a `ReadinessPoolAiSummary`
section of the dashboard response. Removed the two queries, the `ReadinessPoolReplenishmentOptions`
constructor dependency, the `AiOperationsReadinessPoolSummary` DTO (backend record + frontend
interface), and the `readinessPoolAiSummary` field from `AdminAiOperationsSummaryDto`/
`AdminAiOperationsSummary`. Every other dashboard section (provider usage, speaking/writing
evaluation, generation quality, signal gates, recent failures) is unrelated and untouched.

### P1 — Frontend admin pages

- **`admin-lessons.component.ts`/`.html`** ("Today Delivery Health" page): removed the "Delivery
  queue — aggregate health" card (`AggregatePoolHealthSummary`), the "Review scaffold generation"
  funnel graph-card (`ReviewScaffoldDryRunSummary`), the "Review scaffold — approval" table
  (`ReviewScaffoldItemDetail[]`, approve/reject/reopen actions), and the "Practice Gym review
  scaffold pilot" monitoring card (`ReviewScaffoldPilotSummary`). Kept: generate-lessons-for-student,
  ready-lesson-buffer table, lesson buffer settings form, and the "Mastery validation" graph-card
  (still served by the relocated `AdminMasteryController`, same route).
- **`admin-student-detail.component.ts`/`.html`**: removed the "Assignment / Delivery Queue
  health" card (`StudentReadinessPoolHealth`, ring metrics + breakdown bars for
  Today-lesson/Practice-Gym pool status) and the "Mastery evaluation" card
  (`AdminMasteryPoolSummary`, per-student mastered/needs-review/review-only/skipped counts +
  evaluation metadata). Renamed the bundling loader method `loadPoolHealth` →
  `loadStudentDiagnostics` (it always loaded 4 other unrelated diagnostics alongside the deleted
  pool-health call — practice summary, learning plan progress, progress summary, Daily
  Lesson/Practice Gym module previews — all kept). The Phase 20D "Pilot readiness (readiness +
  repair)" panel is unrelated in concept and **kept fully intact**.
- **`admin-ai-operations.component.html`/`.spec.ts`**: removed the "Delivery queue / review
  scaffold AI generation" card and its `readinessPoolAiSummary`-dependent test.
- **`admin-feature-gates.component.ts`/`.spec.ts`**: removed the
  `reviewScaffoldPracticeGymPilot` category label/filter option; rewrote the spec's mock feature
  group (previously modeled the deleted `practice-gym-review-scaffold-pilot` group) to model the
  surviving `activity-feedback-policy` group instead, updating the affected assertions (string
  dropdown vs. toggle/number-input fields).
- **`admin.models.ts`**: removed `ReadinessPoolSourceHealth`, `AdminMasteryPoolSummary`,
  `StudentReadinessPoolHealth`, `AggregatePoolHealthSummary`, `ReviewScaffoldDryRunSummary`,
  `ReviewScaffoldPendingItem`, `AdminReviewStatus`, `ReviewScaffoldItemDetail`,
  `ReviewScaffoldReviewActionRequest`, `ReviewScaffoldPilotItem`, `ReviewScaffoldPilotSummary`,
  `AiOperationsReadinessPoolSummary`; removed the `readinessPoolAiSummary` field from
  `AdminAiOperationsSummary`; removed `'reviewScaffoldPracticeGymPilot'` from the
  `FeatureGateCategory` union.
- **`admin.api.service.ts`**: removed `getStudentReadinessPoolHealth`, `getStudentMasteryPoolSummary`,
  `getAggregatePoolHealth`, `getReviewScaffoldDryRun`, `getReviewScaffoldPendingReview`,
  `getReviewScaffoldPilotSummary`, `approveReviewScaffoldItem`, `rejectReviewScaffoldItem`,
  `reopenReviewScaffoldItem`. `getMasteryValidationSummary()` kept unchanged — same route, now
  served by `AdminMasteryController`.
- **`practice-gym-suggestions.service.ts`**: left completely unchanged, per the
  `PracticeGymSuggestionService` decision above (API-contract stability).

## Migration

`dotnet ef migrations add Phase_I2C_RemoveReadinessPool` — drops the
`student_activity_readiness_items` table, drops the two FK constraints that referenced it
(`activity_feedback_signals` → readiness item, `student_activity_usage_logs` → readiness item)
before dropping the table, then drops the now-orphaned `student_activity_readiness_item_id`
columns and their indexes from both dependent tables. `Down()` fully reconstructs the table (all
columns, indexes, the one surviving FK to `placement_item_definitions`) for rollback safety.
Verified `LinguaCoachDbContextModelSnapshot.cs` no longer references the entity after generation.

## Test fixes (grouped)

Deleted entirely (whole file tested only deleted code):
`tests/LinguaCoach.UnitTests/ReadinessPool/` (whole folder — `StudentActivityReadinessItemTests`,
`ReadinessPoolReplenishmentServiceEffectiveSettingsTests`, `ReplenishmentOptionsTests`,
`EffectiveReadinessPoolSettingsProviderTests`),
`tests/LinguaCoach.IntegrationTests/ReadinessPool/` (whole folder — `ReadinessPoolIntegrationTests`,
`ReplenishmentIntegrationTests`),
`tests/LinguaCoach.IntegrationTests/Api/AggregatePoolHealthEndpointTests.cs`,
`ReviewScaffoldAdminApprovalTests.cs`, `ReviewScaffoldDryRunTests.cs` (its two mastery-validation
tests relocated to a new `AdminMasteryEndpointTests.cs`, see below),
`ReviewScaffoldPilotSummaryTests.cs`,
`tests/LinguaCoach.IntegrationTests/PracticeGym/ReadinessConsumptionWiringTests.cs` (the whole
"ActivitySubmitHandler wires readiness item consumption" feature it tested no longer exists).

Created: `tests/LinguaCoach.IntegrationTests/Api/AdminMasteryEndpointTests.cs` — preserves the
auth-guard + response-shape coverage for `GET api/admin/mastery/validation-summary` that used to
live inside `ReviewScaffoldDryRunTests.cs`, now pointed at the relocated `AdminMasteryController`.

Rewritten/adapted (surviving functionality, readiness-pool-dependent parts stripped):
`tests/LinguaCoach.IntegrationTests/Api/AdminStudentReadinessEndpointTests.cs` (restored after an
initial over-eager deletion — this tests the **surviving** `AdminStudentReadinessController`, not
the deleted `AdminReadinessPoolController`; its readiness-item-seeding repair scenario was rebuilt
around the surviving `GenerateLearningPlanIfMissing` action instead of the deleted
`ExpireStaleReservedItems`), `AdminAiOperationsSummaryTests.cs` (removed the
`readinessPoolAiSummary`-dependent shape assertion + its dedicated test),
`AdminRuntimeSettingsEndpointTests.cs` (rewritten against `activity-feedback-policy` instead of the
deleted `review-scaffold-generation`/`practice-gym-review-scaffold-pilot` groups — see "Risks"
below for the coverage gap this leaves), `AdminRuntimeSettingsEffectiveWiringTests.cs` (dropped the
scaffold-pilot effective-wiring test, kept the unrelated lesson-generation-buffer + 403 tests),
`DailyLessonModulePipelineEndpointTests.cs` and `PracticeGymModulePipelineEndpointTests.cs` (each
had one `Existing_readiness_pool_endpoint_not_broken` test pointed at the deleted controller,
retargeted at the surviving `AdminStudentReadinessController`'s `/readiness` route instead),
`AdminCurriculumObjectivesIntegrationTests.cs` (removed one test asserting the readiness-item
count didn't change — table no longer exists to count), `PracticeGymSuggestionIntegrationTests.cs`
(removed the 3 tests that seeded real readiness items via
`IStudentActivityReadinessPoolService`/direct entity construction — nothing left to seed; added a
new test confirming `AdminReadinessPoolController`'s routes are genuinely gone, not just
read-only), `PracticeGymSuggestionServiceTests.cs` (unit test rewritten around the gutted
permanently-no-op `StartSuggestionAsync`/`TryMarkConsumedAsync`), `RuntimeSettingsServiceTests.cs`
(rewritten against `activity-feedback-policy`), `StudentPilotReadinessRepairServiceTests.cs`
(rewritten around the 2 surviving actions), `StudentReadinessAuditServiceTests.cs` (rewritten,
dropped the 7 tests for removed checks, added 2 for the new `practicegym.module_based`/TTS-setting
checks), `StudentMasteryEvaluationServiceTests.cs` (removed the 6 demotion-decision tests, kept
the 8 mastery-classification tests unchanged), `StudentMasteryClassificationEdgeCaseTests.cs`
(removed the 2 readiness-pool tests, kept the 8 classification-boundary tests),
`ModuleGenerationServiceTests.cs` (removed one assertion counting the deleted table),
`ExercisePatternPhase2Tests.cs` (removed one test constructing the deleted `ActivityGenerationContext`
record).

## Validation

- `dotnet build --configuration Release`: **0 errors.**
- `dotnet test --configuration Release`: **3,428 / 3,428 passing, 0 failing**
  (`ArchitectureTests` 5/5, `UnitTests` 2,107/2,107, `IntegrationTests` 1,316/1,316). Baseline
  before this pass was 3,640/3,640 (Pass B's post-pass number) — the 212-test reduction is entirely
  from deleted test files/methods exercising now-removed readiness-pool behavior (listed above),
  offset by the ~10 rewritten tests added for surviving/relocated functionality.
- `cd src/LinguaCoach.Web && npm run build -- --configuration production`: **0 new TypeScript/Angular
  errors.** The only `[ERROR]` is the pre-existing initial-bundle-size budget overage (2.56 MB vs.
  the 1 MB `maximumError` threshold) — confirmed via `git diff --stat` on `angular.json` (no
  changes) that this predates this session; this pass only removed frontend code, reducing bundle
  size, never added a dependency.
- `npx ng test` (whole-suite karma run): **could not be run to completion** — same 5 pre-existing,
  unrelated broken spec files Pass B already found and flagged
  (`activity-feedback-page.component.spec.ts`, `activity-lesson-submission.component.spec.ts`,
  `activity-lesson-vocab.component.spec.ts`, `presenters/test-helpers.ts`,
  `practice-gym.component.spec.ts` — all fail to type-check against `activity.models.ts`/
  `practice-gym-suggestions.service.ts`'s `feedbackPolicy`/`moduleSuggestions` required fields).
  Confirmed via `git diff --stat` none of these 5 files were touched by this pass. The frontend
  spec files this pass *did* rewrite (`admin-ai-operations`, `admin-feature-gates`, `admin-lessons`,
  `admin-student-detail`) were verified by careful manual review against the new
  DTO/service/component shapes and by the clean `ng build` type-check, but — same residual risk
  Pass B flagged — could not be executed end-to-end via karma in this session.
- Final repo-wide grep for `StudentActivityReadinessItem`, `ReadinessPoolStatus`,
  `ReadinessPoolSource`, `IStudentActivityReadinessPoolService`,
  `ReadinessPoolReplenishmentService`/`Job`, `AdminReadinessPoolController`,
  `IEffectiveReadinessPoolSettingsProvider`, `GenerateActivityContentAsync`,
  `ActivityGenerationContext`: every remaining hit is either a migration `Designer.cs`/model-snapshot
  file (expected — historical migration record), a doc comment explaining history (left or
  corrected to past tense), or the still-live, unrelated `FeatureGateCategory.ReadinessPoolLessonGeneration`
  (backend/frontend) — confirmed still used by 3 live `LessonGenerationSettings`-backed groups.

## Decisions made

1. `GetMasteryValidationSummary` relocated to a new `AdminMasteryController.cs`;
   `GetLearningPlan`/`GetLearningPlanProgress` confirmed dead (frontend uses a different,
   already-existing controller) and deleted with the rest of `AdminReadinessPoolController`.
2. `IAiActivityGenerator`/`AiActivityGeneratorHandler` **not** renamed despite now being
   evaluate-only — judged the naming-accuracy fix not worth touching the DI registration and every
   reference for a single remaining method. Documented clearly in the interface's doc comment.
3. `PracticeGymSuggestionService.StartSuggestionAsync`/`TryMarkConsumedAsync` gutted to permanent
   no-ops rather than removed from the interface/controller/frontend — preserves API-contract
   stability; flagged as a residual for future cleanup since they can never do anything useful
   again (the three suggestion lists have been empty since Pass A).
4. `StudentReadinessAuditService`/`StudentPilotReadinessRepairService` (Phase 20D) kept alive with
   surgical removal of only their readiness-pool-dependent checks/actions — these are genuinely
   live, valuable, unrelated-in-concept admin diagnostics.
5. `LearningPlanService`'s one readiness-pool touchpoint (`LessonQueueLength`) was already
   permanently `0` since Pass B (its source, `ReadinessPoolSource.LessonBatch`, was already dead) —
   hardcoded with zero behavior change, not a functional regression.
6. `StudentMasteryEvaluationService`'s readiness-item demotion mechanism was judged a side effect
   with zero remaining effect (not part of core mastery classification) and removed cleanly,
   leaving `DemotedCount` on `StudentMasteryReport` as a permanent `0` rather than removing the
   field and churning every caller.
7. `FeatureGateBackingStore.ReadinessPoolOverride` enum value **not** renamed despite
   `ActivityFeedbackPolicy` being its only remaining consumer — same reasoning as decision 2.
8. `AdminStudentReadinessEndpointTests.cs` was initially deleted by mistake (misidentified as
   testing the deleted `AdminReadinessPoolController` when it actually tests the surviving
   `AdminStudentReadinessController`) and restored + correctly adapted — noted here as a caution
   for future passes: `readiness-pool` and `readiness` are similarly-named but distinct concepts
   in this codebase after this pass.

## Risks / unresolved questions

- `AdminRuntimeSettingsEndpointTests.cs`'s `Update_HighRiskWithoutConfirmation_Returns400`/
  `Update_HighRiskWithConfirmation_Succeeds` tests were removed rather than adapted — no remaining
  feature-gate group has `RequiresConfirmation = true` after deleting
  `ReadinessPool.EnableReviewScaffoldGeneration`/`.AllowTodayLessonInsertion` (the only two settings
  that had it). This leaves the `requiresConfirmation` code path in `RuntimeSettingsService.UpdateAsync`
  genuinely untested until a future high-risk setting is added. Flagging rather than fabricating a
  workaround.
- The whole-suite frontend karma run remains blocked by the same 5 pre-existing broken spec files
  Pass B flagged — still unresolved, still out of I-track scope.
- `PracticeGymSuggestionsController`'s `/start`/`/complete` routes and
  `PracticeGymSuggestionService.StartSuggestionAsync`/`TryMarkConsumedAsync` are now permanently
  unreachable no-ops (residual, see decision 3) — a future cleanup pass could remove them from the
  interface/controller/frontend service entirely once confirmed nothing depends on the routes
  existing.
- `AdminGenerationController`'s `LessonGenerationSettings`-backed fields
  (`ReadyLessonBufferSize`/`RefillThreshold`/etc., flagged as inert by Pass B) are untouched by
  this pass — still a candidate for a future cleanup alongside the residuals above.

## Final verdict

Pass C complete and verified: 0 backend build errors, 3,428/3,428 tests passing, 0 new frontend
build errors. The readiness pool (`StudentActivityReadinessItem`,
`IStudentActivityReadinessPoolService`, `ReadinessPoolReplenishmentService`) is fully deleted.
Every downstream dependent — admin diagnostics (Phase 20D), Learning Plan, mastery evaluation,
Practice Gym suggestions, the AI operations dashboard, runtime feature gates — was surgically
stripped of its readiness-pool-dependent parts while preserving genuinely live, unrelated
functionality. `IAiActivityGenerator` is narrowed to evaluation only.

## I2 phase closing summary (Passes A + B + C)

Phase I2 (legacy fallback deletion) is complete across all three passes:

- **Pass A** (Practice Gym): deleted `ActivityTemplate`/`PracticeActivityCache` and their
  generation jobs; confirmed nothing calls the readiness pool with `ReadinessPoolSource.PracticeGym`.
- **Pass B** (Today): deleted `LessonBatchGenerationJob`/`ActivityMaterializationJob`/
  `ExercisePrepareHandler`/`SessionGeneratorService`; collapsed Today to module-only; confirmed
  nothing calls the readiness pool with `ReadinessPoolSource.TodayLesson`/`.LessonBatch`.
- **Pass C** (this pass): deleted the readiness pool itself
  (`StudentActivityReadinessItem`/`IStudentActivityReadinessPoolService`/
  `ReadinessPoolReplenishmentService`) and narrowed `IAiActivityGenerator` to evaluation only.

**Net effect:** Today and Practice Gym are now served exclusively by the bank-first pipeline
(Modules → the H10 launch bridge, for `gap_fill`/`multiple_choice_single` vocabulary/grammar
content only). Every surface that used to paper over gaps with an AI-generation fallback — Today's
lesson selection, Practice Gym suggestions, the per-student readiness audit, the AI operations
dashboard — now honestly reports "nothing available" when the bank has no compatible content for a
student, rather than silently generating free-form fallback content. Expanding bank-first coverage
to the other ~31 exercise types (speaking, listening, writing, matching, reorder, etc.) is
deferred to a future phase (I5 in the broader I-track plan) — not part of I2.

## Next recommended action

I2 is done. Per the I-track plan referenced at the top of this doc, I3 (final nav consolidation)
is next. Separately, and outside the I-track: (1) someone should fix the 5 pre-existing broken
frontend spec files so the whole-suite `ng test` run is usable again; (2) the residual
permanently-unreachable no-op code in `PracticeGymSuggestionService`/
`PracticeGymSuggestionsController` and the untested `requiresConfirmation` code path in
`RuntimeSettingsService` are both good candidates for a small, low-risk future cleanup pass.
