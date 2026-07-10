# Phase I2A — Practice Gym Legacy Fallback Deletion (Pass A)

**Date:** 2026-07-10
**Related sprint / feature:** I-track (Import pipeline unification → I1, legacy-fallback deletion
→ I2, final nav consolidation → I3). This is Pass A of I2: the Practice Gym side only. A later
pass (I2B or similar) covers Today's side (`LessonBatchGenerationJob`, `ActivityMaterializationJob`,
`ExercisePrepareHandler`, `LessonBufferRefillJob`, `SessionGeneratorService`, everything under
`src/LinguaCoach.Infrastructure/Sessions/`).

## Context / decision

The product had two content-delivery systems: a legacy AI-generation pipeline (on-demand,
per-request AI calls that produced ~89% of exercise types) and a new bank-first pipeline
(Learn Item → Activity Definition → Module → H10 launch bridge, covering `gap_fill` /
`multiple_choice_single` over vocabulary/grammar only). The user directed: delete the legacy
pipeline now, on the explicit understanding that Practice Gym will only be able to serve the
narrower bank-first content until a future phase expands bank coverage. When nothing eligible
exists, Practice Gym must return a clean "nothing available" response — never fall back to AI
generation.

## Files reviewed

Backend: `ActivityGetHandler.cs`, `ActivityController.cs`, `PracticeGymSuggestionService.cs`,
`LinguaCoachDbContext.cs`, all EF configuration classes referencing `ActivityTemplate`/
`PracticeActivityCache`, `DependencyInjection.cs`, `QuartzConfiguration.cs`,
`AdminReviewQueueQueryHandler.cs`/`AdminReviewQueueContracts.cs`, `AdminHandler.cs`,
`FeatureGateDefinitions.cs`/`FeatureGateEnums.cs`, every test project referencing the deleted
routes/types. Frontend: `app.routes.ts`, `admin-app-layout.component.html`,
`admin-review-queue.component.ts`.

## Findings and decisions, by priority

### P0 — Delete the legacy `ActivityTemplate` entity system

`ActivityTemplate` (the Form.io-pilot template system, distinct from H4's `ActivityDefinition`)
was deleted entirely:

- Domain entity `src/LinguaCoach.Domain/Entities/ActivityTemplate.cs`
- EF config `ActivityTemplateConfiguration.cs`, seeder `ActivityTemplateSeeder.cs` (+ its call
  site in `Program.cs` and `ApiTestFactory.cs`)
- All 10 files under `src/LinguaCoach.Infrastructure/ActivityTemplates/` (handlers, mapper,
  validation rules, instance generator)
- All contracts under `src/LinguaCoach.Application/ActivityTemplates/`
- Controller `AdminActivityTemplateController.cs`
- Frontend pages `admin-activity-templates/`, `admin-activity-template-editor/`, their
  service/model files, and their two routes in `app.routes.ts` (now `redirectTo: () =>
  '/admin/activities'`, matching the existing `RedirectFunction` pattern)
- The "Activity templates" nav item (desktop + mobile drawer) in `admin-app-layout.component.html`
- `IPracticeGymFormIoTemplatePilotSettingsProvider` (Application + Infrastructure) — the settings
  provider behind the `PracticeGymFormIoPilot.Enabled` toggle. Confirmed orphaned: its only
  consumer was the deleted Practice Gym generation job. Deleted along with the entire
  `PracticeGymFormIoTemplatePilot` feature-gate group in `FeatureGateDefinitions.cs` (and the
  now-unused `FeatureGateCategory.PracticeGymFormIoTemplatePilot` enum value) — this admin toggle
  had zero remaining effect once its only reader was gone, and this file's own doc comment states
  "nothing here is invented behavior," so leaving a dead toggle registered would have violated
  that invariant. Any pre-existing `RuntimeSettingOverride` row for that key is now orphaned but
  harmless.

**`AdminReviewQueueComponent`/`AdminReviewQueueController`/`AdminReviewQueueQueryHandler`** —
rewritten, not deleted. The query handler's `ActivityTemplates` union branch and the
`ReviewQueueEntityType.ActivityTemplate` constant were removed; it now covers
`PlacementItemDefinition` only. The frontend `admin-review-queue.component.ts` dropped its
`AdminActivityTemplateService` import/field, the "Activity templates" filter option, and the
entity-type branching in `viewRoute`/`approve`/`reject`. Placement Item review is fully
functional and covered by tests.

**FK entanglement found:** `ActivityFeedbackSignal.SourceTemplateId`,
`StudentActivityReadinessItem.SourceTemplateId`, and `StudentActivityUsageLog.SourceTemplateId`
all had `HasOne<ActivityTemplate>()` FK relations. Decision: remove only the FK relation config
(and the resulting DB FK constraint/index via the migration) — the plain `Guid?` columns stay as
historical data on all three entities. This is the minimal edit that satisfies "don't drop
`StudentActivityUsageLog.StudentActivityReadinessItemId`" and "don't structurally touch
`StudentActivityReadinessItem`" while still compiling. `ActivityNoveltyPolicy`'s
`SourceTemplateId`-based cooldown check (in `StudentActivityUsageLog`) is unchanged in code — it
becomes permanently inert going forward (no writer populates it anymore), exactly mirroring how
it was already inert for legacy freeform-generated content.

### P0 — Delete `PracticeActivityCache` (Practice Gym pre-generation queue/cache)

- Domain entity + `PracticeActivityCacheConfiguration.cs`
- `PracticeGymBufferRefillJob.cs`, `PracticeGymGenerationJob.cs` — deleted entirely, plus their
  Quartz job/trigger registrations in `QuartzConfiguration.cs` and their `AddScoped<>()`
  registrations in `DependencyInjection.cs`
- `IPracticeGymPoolService` (Application) + `PracticeGymPoolService` (Infrastructure) — deleted.
  Confirmed its only 2 live callers were both in `ActivityController.GetPracticeGymNext`
  (rewritten below); `MarkConsumedAsync`/`MarkFailedAsync` were already dead code.
- A leftover `_db.PracticeActivityCache.Where(...).ExecuteDeleteAsync()` call in
  `AdminHandler`'s student-reset command was removed (the table it targeted no longer exists).

### P0 — `ActivityController.GetPracticeGymNext` rewrite

Removed the `IPracticeGymPoolService` pool check and the on-demand-AI-generation fallback
entirely. The existing `PracticeGymNextResponse` record already had a `bool hasActivity` +
`string? reason` shape, so no new DTO was needed — the endpoint now always returns
`hasActivity: false` with an honest reason, pointing implicitly at the H7 module-suggestions flow
(`PracticeGymSuggestionsController`, unaffected by this pass). No server-side redirect was added
(not requested); the client already renders H7's "Recommended module practice" section from the
same suggestions response.

### P0 — `GET api/activity/next` and `ActivityGetHandler`'s `IGetNextActivityHandler`

`ActivityGetHandler` implemented both `IGetNextActivityHandler` (on-demand AI generation / legacy
activity-type routing / practice-cache assignment) and `IGetActivityByIdHandler` (used generally,
including for H10-launched activities). Removed only the `IGetNextActivityHandler` side:

- `HandleAsync(GetNextActivityQuery, ...)` and every private helper reachable *only* from it:
  `HandlePatternKeyedAsync`, `HandleLegacyActivityTypeAsync`, `HandleExerciseTypeKeyedAsync`,
  `TryAssignReadyPracticeCacheAsync`, `ResolveActivityTypeAsync`,
  `EnsureExerciseTypeAvailableAsync`, `EnsureLegacyActivityTypeAvailableAsync`,
  `ResolveCurrentModuleAsync`, `ResolveDefaultInteractionModeAsync`, and the
  `DefaultPatternKeyByActivityType` static dict / cadence constants they used. Confirmed via
  read-through that none of these are reachable from the surviving `IGetActivityByIdHandler` path.
- Fields/ctor params that became unused after removal: `_aiGenerator` (`IAiActivityGenerator`),
  `_pathGenerator`, `_progress`, `_vocabGenerator`, `_exerciseTypes`, `_goalContextResolver`,
  `_routing`. `_listeningAudio`, `_patternRepo`, `_db`, `_logger` stay — still used by
  `IGetActivityByIdHandler`.
- `IGetNextActivityHandler`'s definition (`src/LinguaCoach.Application/Activity/
  ActivityCommands.cs`, alongside `GetNextActivityQuery`) was deleted, and its DI mapping in
  `DependencyInjection.cs` removed.

**`GET api/activity/next` judgment call:** grepped the entire frontend — zero callers of this
route or a TS service method wrapping it. Per the task's explicit instruction ("if nothing calls
it, delete the action entirely"), the controller action was deleted rather than rewritten to
"nothing available." This is a literal API surface removal, not just a behavior change.

**Unexpected fallout:** ~40 backend integration tests across 9 files exercised
`GET /api/activity/next` directly (`ActivityFallbackTests`, `ActivityStructuredFeedbackTests`,
`VocabularyPracticeActivityTests`, `ListeningComprehensionActivityTests`,
`SpeakingRolePlayActivityTests`, `PatternEvaluationSubmitTests`, `LearningPathProgressionTests`,
`LearningPathEndpointTests`, `LearningHistoryTests`) — either testing the endpoint's own routing
logic (now genuinely gone, deleted) or using it as a convenience setup step to obtain a valid
`activityId` (rewritten to seed a `LearningActivity` directly via `LinguaCoachDbContext`, which
every one of these test classes already had scope access to). No production code changed as a
result of these test fixes — purely test-harness adaptation.

### P0 — `PracticeGymSuggestionService` rewrite

`SuggestedItems`/`ContinueItems`/`ReviewItems` on `PracticeGymSuggestionsDto` were 100% sourced
from `StudentActivityReadinessItems` filtered to `ReadinessPoolSource.PracticeGym`, including a
full ranking/dedupe pipeline and the Phase 19C review/scaffold pilot gate. All three lists are now
always empty (`[]`) — the DB query for `ReadinessPoolSource.PracticeGym` rows was removed
entirely (not just discarded after the fact), along with all now-dead private helpers (`ToDto`,
`ItemIdentityKey`, `DedupeByIdentity`, `RankSuggestions`, `BuildTitle`, `BuildCallToAction`,
`Capitalise`, `ParseJsonStringArray`) and the now-unused `IEffectiveReadinessPoolSettingsProvider`
dependency. `ReservedCount` (previously counted `rawItems` with `Reserved` status) is now a
hardcoded `0` for the same reason. `ReadyCount`/`ReviewOnlyCount`/`IsReplenishmentRecommended`
still come from `IReadinessPoolReplenishmentService.GetHealthAsync(..., ReadinessPoolSource
.PracticeGym, ...)` — a separate, unscoped-for-this-pass health computation the task didn't ask to
change; it now reports on a pool source that's no longer surfaced anywhere, but that's a
reporting/observability nuance, not a functional bug, and touching it risked scope creep into the
still-live readiness-pool replenishment engine.

`ModuleSuggestions` (H7, via `IPracticeGymModuleSelectionService`) is untouched — it's already
independent of the readiness pool and is now the sole real content in this DTO.

### P1 — Stale doc comments

Several `<see cref="ActivityTemplate">` / prose references to the deleted entity, in files that
were never functionally coupled to it (`ActivityGetHandler.cs`'s class doc comment,
`AdminActivityDefinitionController.cs`, `ActivityDefinitionLaunchContracts.cs`,
`ActivityDefinitionContracts.cs`, `StudentActivityUsageLog.cs`,
`DependencyInjection.cs`'s H10 registration comment) were updated to stop referencing the removed
type, replacing `<see cref>` tags (which would now be dangling/unresolved) with `<c>` tags or
prose pointing at this review doc.

## Migration

`Phase_I2A_RemoveActivityTemplateAndPracticeActivityCache`
(`src/LinguaCoach.Persistence/Migrations/20260710052456_...`). `Up()` drops exactly: 3 FK
constraints (`activity_feedback_signals`, `student_activity_readiness_items`,
`student_activity_usage_logs`, all `→ activity_templates`), the `activity_templates` and
`practice_activity_cache` tables, and the 3 now-orphaned indexes on `source_template_id`. It does
**not** touch `student_activity_readiness_items`' table, drop any `source_template_id` *column*,
or drop `StudentActivityUsageLog.StudentActivityReadinessItemId`. Reviewed the generated diff for
unexpected drift — none found; scoped exactly as intended.

## Test fixes (grouped)

Deleted entirely (whole file tested only deleted code):
`AdminActivityTemplateEndpointTests.cs`, `AdminActivityTemplateGenerationEndpointTests.cs`,
`ActivityTemplateSeederTests.cs`, `ActivityTemplateValidationRulesTests.cs` (unit),
`ActivityTemplateTests.cs` (unit, Domain), `PracticeGymTemplateGenerationJobTests.cs`,
`PracticeGymBufferRefillJobFingerprintTests.cs`, `PatternKeyedActivityEndpointTests.cs` (100% GET
`/api/activity/next?pattern=` coverage).

Rewritten/trimmed (kept still-valid coverage, removed only the parts testing deleted behavior):
`ActivityEndpointTests.cs`, `LearningHistoryTests.cs`, `SpeakingRolePlayActivityTests.cs`,
`PatternEvaluationSubmitTests.cs`, `ListeningComprehensionActivityTests.cs`,
`VocabularyPracticeActivityTests.cs`, `LearningPathProgressionTests.cs`,
`LearningPathEndpointTests.cs`, `PracticeGymNextEndpointTests.cs` (rewritten to assert the new
honest-empty response), `PracticeGymSuggestionIntegrationTests.cs` (3 tests flipped from
"non-empty section" to "always-empty section" assertions), `PracticeGymSuggestionServiceTests.cs`
(unit — same flip, plus removal of the entire dedupe/pilot-gate test matrix that no longer
applies), `AdminReviewQueueEndpointTests.cs` (rewritten around `PlacementItemDefinition` instead
of `ActivityTemplate`), `AdminRuntimeSettingsEffectiveWiringTests.cs` (2 of 3 pilot-visibility
tests deleted as untestable; the 3rd rewritten to assert the pilot toggle now has *no* effect),
`ActivityNoveltyPolicyTests.cs`, `ActivityDefinitionLaunchServiceTests.cs`,
`DailyLessonModuleSelectionServiceTests.cs`, `PracticeGymModuleSelectionServiceTests.cs`,
`ResourceCandidatePublishServiceTests.cs` (all: removed one now-uncompilable assertion/seed line
each, main test intent unchanged).

Minor DI/config fix required for the `PracticeGymSuggestionService` ctor signature change
(`IEffectiveReadinessPoolSettingsProvider` removed) to propagate into its unit test's `BuildSut`
helper.

## Validation

- `dotnet build --configuration Release`: 0 errors (only pre-existing, unrelated warnings).
- `dotnet test --configuration Release`: **3,734 / 3,734 passing, 0 failing**
  (`ArchitectureTests` 5/5, `UnitTests` 2,249/2,249, `IntegrationTests` 1,480/1,480). Baseline
  before this pass was 3,858/3,858 — the 124-test reduction is expected and comes entirely from
  deleted test files/methods that exercised now-removed legacy behavior (see above), not from any
  coverage loss on surviving functionality.
- `npm run build -- --configuration production`: 0 new TypeScript/Angular errors. The only build
  `[ERROR]` is the pre-existing initial-bundle budget overage (2.56 MB vs the 1 MB `maximumError`
  threshold in `angular.json`) — this predates the session (this pass only deletes frontend code,
  so the bundle can only have shrunk) and the task brief explicitly calls it out as acceptable.
- Final repo-wide grep for `ActivityTemplate`, `PracticeActivityCache`, `IPracticeGymPoolService`,
  `IGetNextActivityHandler`, `IActivityTemplateInstanceGenerator` (excluding `bin/`/`obj/`,
  migration history, and this review doc itself): every remaining hit is either the historical
  migration's `Down()` method (correct — it's meant to reference the dropped schema for rollback),
  a deliberate `ActivityTemplateCandidate` reference (a *different*, unrelated
  `ResourceCandidateType` enum member for the Phase E1 resource-import pipeline, deferred to
  Phase E4 — confirmed not the deleted entity), or a doc comment now updated to stop dangling on
  the removed type.

## Decisions made

1. Keep `SourceTemplateId` columns (on 3 entities) as plain historical data rather than dropping
   them — minimal-risk, avoids touching `StudentActivityReadinessItem` structurally, keeps
   `ActivityNoveltyPolicy` compiling unchanged.
2. Delete `GET api/activity/next` (controller action + `IGetNextActivityHandler` +
   `GetNextActivityQuery`) rather than rewrite it to "nothing available," because the frontend has
   zero callers.
3. Delete the orphaned `PracticeGymFormIoTemplatePilot` feature-gate group and its
   `IPracticeGymFormIoTemplatePilotSettingsProvider`, beyond the task's explicitly-listed file
   list, because they were direct, provably-dead consumers/toggles of the deleted
   ActivityTemplate/PracticeGymGenerationJob pipeline and leaving them would have left a
   nonfunctional admin control.
4. Leave `PracticeGymSuggestionService`'s `ReadyCount`/`ReviewOnlyCount`/
   `IsReplenishmentRecommended` health fields wired to `IReadinessPoolReplenishmentService` as-is
   (unscoped for this pass) rather than zeroing them too, since the task scoped only the three
   suggestion lists.

## Risks / unresolved questions

- `PracticeGymSuggestionsDto.ReadyCount`/`ReviewOnlyCount`/`IsReplenishmentRecommended` now report
  on Practice-Gym-sourced readiness-pool health that is no longer surfaced as actual student
  content — cosmetically confusing on any admin diagnostic surface that reads this DTO, though no
  such surface was found in this pass. Worth revisiting in a later pass alongside the readiness
  pool itself.
- The readiness pool (`StudentActivityReadinessItem`, `IStudentActivityReadinessPoolService`,
  `ReadinessPoolReplenishmentService`) is more entangled with Practice Gym than a first read
  suggests: `ReadinessPoolSource.PracticeGym` still exists as an enum value and the replenishment
  engine can still generate Practice-Gym-sourced rows (nothing in this pass stops that write
  path) — they simply now have zero readers on the Practice Gym side. The next pass touching the
  readiness pool (Today's side) should check whether the replenishment engine should also stop
  generating `PracticeGym`-sourced rows, or whether that's deliberately left for a future
  Practice-Gym-specific bank-first replenishment mechanism.
- `LessonGenerationSettings`' `PracticeGymReadyExercisesPerType`/
  `PracticeGymRefillThresholdPerType`/`PracticeGymRefillCountPerType` fields (surfaced via the
  still-registered `PracticeGymGenerationPerType` feature gate) were left untouched — their own
  description already states "No job currently reads this field — display only" for at least one
  of them, and they're backed by `LessonGenerationSettings` (a table this pass doesn't touch), not
  the deleted `PracticeActivityCache`. Flagging for awareness only; not acted on.

## Final verdict

Pass A complete and verified. Practice Gym no longer has any AI-generation or readiness-pool
fallback path; `GET /api/activity/practice-gym/next` and the Practice Gym suggestions endpoint
both honestly report the narrower bank-first-only reality. Build and full test suite are green.

## Next recommended action

Scope and implement Pass B (Today's side): `LessonBatchGenerationJob`,
`ActivityMaterializationJob`, `ExercisePrepareHandler`, `LessonBufferRefillJob`,
`SessionGeneratorService`, `SessionQueryHandler`, and everything under
`src/LinguaCoach.Infrastructure/Sessions/`. Before starting, resolve the "risks/unresolved
questions" above regarding the readiness pool's continued `PracticeGym`-sourced write path, since
Today's pass will touch the same pool from the other side.
