# Phase 2 — Exercise Pipeline Boundary and Provenance Consolidation

**Date:** 2026-07-15
**Related sprint/feature:** Follow-up to Phase 1 (`docs/reviews/2026-07-15-phase-1-pipeline-safety-data-integrity-fixes.md`).
Removes the redundant direct Resource-to-Exercise generation pipeline flagged by the original
architecture audit (`docs/reviews/2026-07-15-content-creation-pipeline-architecture-audit.md`,
"Risks and technical debt" #3, and "Questions or ambiguities" #1) and establishes the single
authoritative content flow: Resource Bank → Lesson → Exercise → Module.
**Type:** Implementation + engineering review (removal, schema change, migration, tests, documentation)

## Files reviewed / changed

**Backend contracts and services**
- `src/LinguaCoach.Application/Exercises/ExerciseGenerationContracts.cs` — removed `GenerateActivityFromResourcesRequest`, `IGenerateActivityFromResourcesHandler`, `IGenerateActivityFromResourcesWithAiHandler`; added `IGenerateActivityFromLessonWithAiHandler`
- `src/LinguaCoach.Application/Exercises/ExerciseContracts.cs` — `ExerciseDto.LessonId`/`CreateExerciseCommand.LessonId` changed `Guid?` → `Guid`
- `src/LinguaCoach.Infrastructure/Exercises/ExerciseGenerationService.cs` — removed the resources-only `HandleAsync` entry point; `ComposeAndSaveAsync`'s `lessonId` param is now `Guid` (was `Guid?`); `SourceMode` is now always `GeneratedFromLesson`
- `src/LinguaCoach.Infrastructure/Exercises/AiExerciseGenerationService.cs` — implements `IGenerateActivityFromLessonWithAiHandler` instead of the removed resources-only interface; resolves the Lesson's own resource links itself; internal-only `ExerciseCompositionRequest` record replaces the removed public contract for the shared composition body
- `src/LinguaCoach.Infrastructure/Exercises/LessonExerciseBatchGenerationService.cs` — `BuildResourcesRequestAsync` deleted; both deterministic and AI-preferred batch items now construct the same `GenerateActivityFromLessonRequest` and pick a handler
- `src/LinguaCoach.Infrastructure/Exercises/ExerciseCommandHandlers.cs` — manual Exercise creation now always validates a required Lesson exists
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registrations updated to match
- `src/LinguaCoach.Api/Controllers/AdminExerciseController.cs` — removed `POST /generate-from-resources` and `/generate-from-resources/ai`; removed `GenerateActivityFromResourcesRequestBody`; `CreateExerciseRequestBody.LessonId` is now required

**Domain / schema**
- `src/LinguaCoach.Domain/Entities/Exercise.cs` — `LessonId` property is `Guid` (was `Guid?`); constructor validates `lessonId != Guid.Empty`
- `src/LinguaCoach.Domain/Enums/ExerciseSourceMode.cs` — removed `GeneratedFromResources`
- `src/LinguaCoach.Persistence/Configurations/ExerciseConfiguration.cs` — `lesson_id` column `IsRequired()`
- `src/LinguaCoach.Persistence/Migrations/20260715015159_Phase_2_RequireExerciseLessonId.cs` (new)
- `src/LinguaCoach.Persistence/Migrations/20260715021804_Phase_2_ConvertGeneratedFromResourcesSourceMode.cs` (new)

**Frontend**
- `src/LinguaCoach.Web/src/app/core/services/admin-exercise.service.ts` — removed `generateFromResources`/`generateFromResourcesWithAi` (confirmed unused by any component before removal)
- `src/LinguaCoach.Web/src/app/core/models/admin-exercise.models.ts` — removed `GenerateActivityFromResourcesRequestBody`; `ExerciseDto.lessonId` is `string` (was `string | null`); `ACTIVITY_SOURCE_MODES` no longer includes `'GeneratedFromResources'`

**Tests**
- New: `tests/LinguaCoach.ArchitectureTests/ExercisePipelineBoundaryTests.cs`
- New: `tests/LinguaCoach.UnitTests/Exercises/LessonExerciseBatchGenerationServiceTests.cs` (Phase 1, extended)
- Updated: `ExerciseGenerationServiceTests.cs`, `AiExerciseGenerationServiceTests.cs`, `ExerciseCommandHandlerTests.cs` (Exercises); `ExerciseLaunchServiceTests.cs`; `PracticeGymModuleSelectionServiceTests.cs`, `TodayPlanModuleSelectionServiceTests.cs`; `AdminModulePreviewServiceTests.cs`, `ModuleGenerationServiceTests.cs`, `AiModuleGenerationServiceTests.cs`, `ModuleCommandHandlerTests.cs` (Modules); `AdminExerciseEndpointTests.cs`, `AdminModuleEndpointTests.cs`, `ExerciseLaunchEndpointTests.cs`, `TodayPlanModulePipelineEndpointTests.cs`, `PracticeGymModulePipelineEndpointTests.cs` (integration)

## Investigation — call graph before deleting anything

Traced every entry point before removing code, per the working rules:

- **`IGenerateActivityFromResourcesHandler`** (deterministic) — implemented by `ActivityGenerationService`, registered in DI, called only by `AdminExerciseController.GenerateFromResources` (`POST generate-from-resources`). No other backend caller. No Angular caller (confirmed — see below).
- **`IGenerateActivityFromResourcesWithAiHandler`** (AI) — implemented by `AiExerciseGenerationService`, called by `AdminExerciseController.GenerateFromResourcesWithAi` (`POST generate-from-resources/ai`) **and** internally by `LessonExerciseBatchGenerationService.BuildResourcesRequestAsync` for the five AI-preferred activity types within the Lesson-based batch flow. This was the one legitimate internal caller — refactored (not deleted outright) into the new Lesson-based AI entry point.
- **Angular** — `admin-exercise.service.ts` had `generateFromResources()`/`generateFromResourcesWithAi()` methods, but grepping every component for call sites (`.generateFromResources(`) found zero callers. The Resource Bank UI's own `generateFromResources`/`generateFromResourcesWithAi` methods belong to `admin-lesson.service.ts` (Resource Bank → Lesson, an entirely different, still-supported endpoint) — confirmed by reading both call sites in `admin-resource-bank-unified.component.ts` and `admin-resource-bank-detail.component.ts`. The Exercise-generation methods were dead code.
- **Tests** — `ActivityGenerationServiceTests.cs` (57 call sites) and `AiExerciseGenerationServiceTests.cs` (27 call sites) constructed `GenerateActivityFromResourcesRequest` directly to exercise the shared composition logic for every activity type (gap_fill, multiple_choice_single, reading/listening comprehension variants, writing/speaking prompts, etc.) — this logic is still fully intact post-removal (see "What was preserved" below), so these tests were adapted to route through a Lesson instead of deleted.
- **`GeneratedFromResources`** (`ExerciseSourceMode`) — read only in the two composers' `sourceMode` assignment (both already conditioned on Lesson presence per Phase 1's fix) and in test assertions. No Angular component branches on this value for display logic.
- **Existing DB data** — `linguacoach_dev` had 5 Exercises with `LessonId == null` (all `SourceMode = GeneratedFromResources`, all `IsArchived = true`) — see "Data migration" below.

## What was preserved (not deleted)

- The entire deterministic composition logic in `ActivityGenerationService` (every `ComposeX` method for every activity type) — untouched. Only its public entry point changed from two interfaces down to one.
- The entire AI composition logic in `AiExerciseGenerationService` (prompt building, retry-once-then-throw, gap_fill leak detection, distractor validation, etc.) — untouched.
- `ExerciseResourceLink` — resource provenance on a generated Exercise is unchanged; every generated Exercise still gets its `ExerciseResourceLink` rows exactly as before.
- Direct Resource→Lesson generation (`admin-lesson.service.ts`, `AdminLessonController`) and direct Resource→Module generation (`ModuleGenerationService.HandleAsync(GenerateModuleFromResourceRequest)`, `ModuleSourceMode.GeneratedFromResources`) — both out of scope, both still fully supported, neither touched.
- Manual Exercise creation (`AdminCreateExerciseHandler`) — still supported, now requires a real Lesson (previously optional).

## Final surviving Lesson-to-Exercise flow

```text
One or more Resource Bank items (already linked to a Lesson via LessonResourceLink)
        ↓
IGenerateActivityFromLessonHandler          (deterministic — ActivityGenerationService)
IGenerateActivityFromLessonWithAiHandler    (AI-assisted — AiExerciseGenerationService)
        ↓  both resolve the Lesson's own LessonResourceLinks themselves
Exercise (LessonId always set, ExerciseResourceLink rows created)
```

`LessonExerciseBatchGenerationService` (the `generate-from-lesson/batch` endpoint, admin-picked
count per type) builds one `GenerateActivityFromLessonRequest` per requested item and routes it to
whichever of the two handlers above is appropriate for that activity type — both handlers now take
the identical request shape, so the batch loop no longer needs a separate "build a resources
request" branch.

## Schema change: `Exercise.LessonId` is now required

**Investigation before changing the schema:** queried the live `linguacoach_dev` Postgres database
directly (`docker exec linguacoach-db-1 psql ...`) rather than guessing:

```sql
SELECT COUNT(*) AS total_exercises, COUNT(*) FILTER (WHERE lesson_id IS NULL) AS null_lesson_id
FROM exercises;
--  total_exercises | null_lesson_id
--               18 |              5
```

All 5 orphaned rows were `IsArchived = true`, `SourceMode = GeneratedFromResources` — leftover
dev-testing artifacts from the now-removed direct path. Checked references before deciding
repair vs. delete:

```sql
-- module_exercise_links referencing an orphan: 8 rows
-- exercise_resource_links referencing an orphan: 5 rows (1 each)
-- student_exercise_launches referencing an orphan: 0 rows
```

Then checked whether each orphan's own `ExerciseResourceLink` resource was linked to exactly one
Lesson (via `LessonResourceLink`) — a reliable, unambiguous repair candidate:

```sql
SELECT e.id, erl.resource_type, erl.resource_id, lrl.lesson_id AS candidate_lesson_id
FROM exercises e
JOIN exercise_resource_links erl ON erl.exercise_id = e.id
LEFT JOIN lesson_resource_links lrl ON lrl.resource_type = erl.resource_type AND lrl.resource_id = erl.resource_id
WHERE e.lesson_id IS NULL;
```

**Result: all 5 had exactly one candidate Lesson.** Per the stated data-handling policy (repair >
delete > fail), migration `Phase_2_RequireExerciseLessonId` repairs every row it can find an
unambiguous Lesson for, and only deletes (along with dependent `module_exercise_links`) a row that
remains unrepaired after that — a branch that did not fire against this dataset since all 5 were
repairable. The migration was applied to the live `linguacoach_dev` database and verified:

```sql
SELECT COUNT(*), COUNT(*) FILTER (WHERE lesson_id IS NULL) FROM exercises;
--  18 | 0
```

No FK constraint exists from `exercises.lesson_id` to `lessons` (a deliberate soft reference, per
`ExerciseConfiguration.cs`'s existing comment — matches the `ResourceCandidate.PublishedEntityId`
convention elsewhere in the codebase), so there is no cascade-delete risk: archiving or any future
Lesson lifecycle change cannot silently delete an Exercise.

A second migration, `Phase_2_ConvertGeneratedFromResourcesSourceMode`, converts the 5 repaired
rows' `source_mode` string from `GeneratedFromResources` to `GeneratedFromLesson` (no schema
change — `source_mode` is a plain varchar, not a DB-level enum — but removing the C# enum member
without converting the stored strings would break deserialization of those 5 rows).

## ExerciseSourceMode enum cleanup

`GeneratedFromResources` removed (orphaned after the interface/contract removal — zero remaining
references anywhere in `src/`). `Manual`, `GeneratedFromLesson`, and `Imported` retained: `Manual`
because manual creation is still a supported product capability (now requiring a Lesson);
`Imported` was already present for a potential future direct-import path and was not touched
(no code currently sets it — pre-existing, unrelated to this phase). `LessonSourceMode.GeneratedFromResources`
and `ModuleSourceMode.GeneratedFromResources` are **different enums** for the still-supported
Resource→Lesson and Resource→Module flows — deliberately left untouched.

## Architecture guard

`tests/LinguaCoach.ArchitectureTests/ExercisePipelineBoundaryTests.cs` — three tests:
1. Reflection scan of `Domain`/`Application` assemblies for any type named
   `IGenerateActivityFromResourcesHandler`, `IGenerateActivityFromResourcesWithAiHandler`,
   `GenerateActivityFromResourcesRequest`, or two speculative future names
   (`GenerateExerciseFromResourcesRequest`/`Handler`).
2. Same scan across `Infrastructure`/`Api` assemblies.
3. Scans `AdminExerciseController`'s route attributes for any `generate-from-resources` route
   (excludes `AdminLessonController`/`AdminModuleController`, where that route name is a different,
   still-supported concept).

`ExerciseResourceLink` is untouched by the guard — it is not in the forbidden-name list and the
guard only checks controller/handler/request-contract names, not provenance-link entities.

## Tests added/updated and validation

```text
git status / git diff --check                → clean, no conflict markers
dotnet restore                                → succeeded
dotnet build --configuration Release          → succeeded, 0 errors
dotnet test tests/LinguaCoach.UnitTests        → Passed: 2272, Failed: 0
dotnet test tests/LinguaCoach.IntegrationTests → Passed: 1325, Failed: 0
dotnet test tests/LinguaCoach.ArchitectureTests→ Passed: 8, Failed: 0
```

Angular:

```text
npx tsc --noEmit -p tsconfig.app.json                        → clean, 0 errors
npm run build -- --configuration production                  → succeeded
npm test -- --watch=false --browsers=ChromeHeadless           → 5 pre-existing spec-compile
                                                                  failures, confirmed unrelated
                                                                  to this phase (see below)
npx playwright test                                            → not run (see below)
```

**Angular Karma test suite — pre-existing failure, not introduced by this phase.** Before making
any change, `git stash` was used to run the Karma suite against the untouched checkout; it failed
identically (`feedbackPolicy` missing on `ActivityFeedbackDto` in 4 spec files, `moduleSuggestions`
missing on `PracticeGymSuggestionsResponse` in 1 spec file — all unrelated to Exercises, Lessons,
or Modules). The stash was then restored and this phase's work continued. This is a pre-existing
break in another in-progress feature area, not a regression from this phase. Both
`tsc --noEmit -p tsconfig.app.json` and the production build compile `tsconfig.app.json`'s scope
(app source only — `*.spec.ts` files are deliberately excluded from that config, which is why they
passed clean while `ng test`, which uses a separate spec-inclusive config, hit the pre-existing
failures) and confirm the actual application code, as opposed to these five stale test fixtures, is
sound. Playwright was not run since it depends on a running dev server and this phase changed no
student/admin-visible runtime behavior beyond removing two dead-code Angular service methods.

## Deferred findings

- The five pre-existing Angular Karma spec failures above are unrelated to this phase and were not
  fixed — fixing them would mean guessing at unrelated in-progress work (`ActivityFeedbackPolicyDto`,
  `PracticeGymModuleSuggestionsSection`) without context on what that feature is trying to become.
- `ExerciseSourceMode.Imported` remains defined but unused by any code path (pre-existing, not
  introduced or touched by this phase) — flagged, not removed, since removing an enum member with
  zero current writers but unknown future intent is a product decision, not a cleanup one.
- The broader Exercise-type-authority duplication (`ExerciseTypeDefinition` vs. hardcoded
  `ActivityType*` constants vs. legacy `ExercisePatternDefinition`) flagged by the original
  architecture audit was not addressed — the type system governing surviving Lesson-based
  generation is unchanged (`ActivityGenerationService`'s `ActivityType*` constants), and no code
  became newly orphaned by this phase's removal that would justify touching it.
- `TODO-027` (from Phase 1) asked whether the direct Resource→Exercise path should be kept or
  deprecated — this phase answers that question by removing it entirely; `TODO-027` should be
  marked resolved.

## Known limitations

- `Exercise.LessonId` is validated at the domain constructor (`lessonId == Guid.Empty` throws) with
  a `Guid lessonId = default` parameter rather than a fully non-optional positional parameter —
  chosen to avoid reordering a 20+ parameter constructor used by dozens of call sites across the
  codebase; the runtime guarantee is identical (no Exercise can be persisted with an empty
  LessonId), enforced at construction time, not just at the database boundary.
- No FK constraint was added from `exercises.lesson_id` to `lessons.id` — matches the pre-existing
  soft-reference convention already used for this column (and for `ResourceCandidate.PublishedEntityId`
  elsewhere), so a hard DB-level FK was judged out of scope for this phase's narrower goal
  (mandatory-Lesson invariant, not new referential-integrity infrastructure).

## Explicit confirmation of out-of-scope items not touched

Import (AI segmentation, candidate editing, Skip status), Resource/Lesson/Exercise/Module
versioning, Module publication / Published Module Catalogue, Learn→Practice→Feedback runtime
redesign, broad Exercise-type catalogue redesign, Pattern Engine removal, Module/LearningModule
consolidation, student learning-memory changes, Today/Practice Gym selection redesign, admin visual
redesign — none of these were implemented. Nothing was pushed. Nothing was deployed.

## Final verdict

Direct Resource-to-Exercise generation is completely removed at every layer (interface, contract,
endpoint, DI registration, Angular service method, request-body model, enum value). Lesson-based
generation is the sole surviving path, verified by an architecture-test guard against
reintroduction. Every newly created Exercise has a mandatory Lesson, enforced at domain, EF
configuration, and database (non-nullable column) level. Existing orphaned data was investigated
against the live dev database (not guessed), fully repaired with zero data loss, and re-verified
post-migration. All 3,605 backend tests (2,272 unit + 1,325 integration + 8 architecture) pass.
Angular production build and type-check are clean. Ready to commit locally.
