---
title: Phase I4 Pass 1 — Backend Rename (LearnItem→Lesson, ActivityDefinition→Exercise, ModuleDefinition→Module)
date: 2026-07-10
related: I-track, implements the decision in docs/architecture/product-language-renaming-i4.md
status: complete
---

# Phase I4 Pass 1 — Backend Rename

**Date:** 2026-07-10
**Type:** implementation phase — backend-only slice of the I4 product-language rename decided in
`docs/architecture/product-language-renaming-i4.md`. Frontend (`src/LinguaCoach.Web/`) is a
separate later pass and was not touched.

## What changed

Pure rename, no data-model change: `LearnItem` → `Lesson`, `ActivityDefinition` → `Exercise`,
`ModuleDefinition` → `Module` (dropping the "Definition" suffix), applied consistently to file
names, class/type/enum names, DTOs, EF configurations, table/column names, API routes, and doc
comments across `src/` and `tests/`. Historical EF migration files (everything under
`src/LinguaCoach.Persistence/Migrations/` except the model snapshot and the new I4 migration) were
deliberately left untouched — they are a historical record of schema at the time they were
authored, consistent with how every prior I-track migration handled this.

### `LearnItem` → `Lesson`

| Old | New |
|---|---|
| `src/LinguaCoach.Domain/Entities/LearnItem.cs` (class `LearnItem`) | `Lesson.cs` (class `Lesson`) |
| `src/LinguaCoach.Domain/Entities/LearnItemResourceLink.cs` | `LessonResourceLink.cs` |
| `src/LinguaCoach.Domain/Entities/ModuleDefinitionLearnItemLink.cs` | `ModuleLessonLink.cs` |
| `src/LinguaCoach.Domain/Enums/LearnItemResourceRole.cs` | `LessonResourceRole.cs` |
| `src/LinguaCoach.Domain/Enums/LearnItemSourceMode.cs` | `LessonSourceMode.cs` |
| `src/LinguaCoach.Application/LearnItems/` (folder) | `Lessons/` |
| `src/LinguaCoach.Application/LearnItems/LearnItemContracts.cs` | `Lessons/LessonContracts.cs` |
| `src/LinguaCoach.Application/LearnItems/LearnItemGenerationContracts.cs` | `Lessons/LessonGenerationContracts.cs` |
| `src/LinguaCoach.Infrastructure/LearnItems/` (folder) | `Lessons/` |
| `.../LearnItemCommandHandlers.cs`, `LearnItemGenerationService.cs`, `LearnItemMappers.cs`, `LearnItemQueryHandlers.cs`, `LearnItemResourceLookup.cs` | `LessonCommandHandlers.cs`, `LessonGenerationService.cs`, `LessonMappers.cs`, `LessonQueryHandlers.cs`, `LessonResourceLookup.cs` |
| `src/LinguaCoach.Api/Controllers/AdminLearnItemController.cs` (route `api/admin/learn-items`) | `AdminLessonController.cs` (route `api/admin/lessons`) |
| `src/LinguaCoach.Persistence/Configurations/LearnItemConfiguration.cs` | `LessonConfiguration.cs` |
| `.../LearnItemResourceLinkConfiguration.cs` | `LessonResourceLinkConfiguration.cs` |
| `.../ModuleDefinitionLearnItemLinkConfiguration.cs` | `ModuleLessonLinkConfiguration.cs` |
| `tests/LinguaCoach.UnitTests/LearnItems/` (folder + files) | `Lessons/` (`LessonCommandHandlerTests.cs`, `LessonGenerationServiceTests.cs`) |
| `tests/LinguaCoach.IntegrationTests/Api/AdminLearnItemEndpointTests.cs` | `AdminLessonEndpointTests.cs` |
| table `learn_items` | `lessons` |
| table `learn_item_resource_links` | `lesson_resource_links` |
| table `module_definition_learn_item_links` | `module_lesson_links` |
| column `learn_item_id` (everywhere it appeared as an FK) | `lesson_id` |

### `ActivityDefinition` → `Exercise`

| Old | New |
|---|---|
| `src/LinguaCoach.Domain/Entities/ActivityDefinition.cs` (class) | `Exercise.cs` (class `Exercise`) |
| `src/LinguaCoach.Domain/Entities/ActivityResourceLink.cs` | `ExerciseResourceLink.cs` |
| `src/LinguaCoach.Domain/Entities/ModuleDefinitionActivityLink.cs` | `ModuleExerciseLink.cs` |
| `src/LinguaCoach.Domain/Entities/StudentActivityDefinitionLaunch.cs` | `StudentExerciseLaunch.cs` |
| `src/LinguaCoach.Domain/Enums/ActivityDefinitionLaunchSource.cs` | `ExerciseLaunchSource.cs` |
| `src/LinguaCoach.Domain/Enums/ActivitySourceMode.cs`* | `ExerciseSourceMode.cs` |
| `src/LinguaCoach.Domain/Enums/ActivityRendererType.cs`* | `ExerciseRendererType.cs` |
| `src/LinguaCoach.Application/ActivityDefinitions/` (folder) | `Exercises/` |
| `.../ActivityDefinitionContracts.cs`, `ActivityGenerationContracts.cs` | `ExerciseContracts.cs`, `ExerciseGenerationContracts.cs` |
| `src/LinguaCoach.Application/ActivityDefinitionLaunch/` (folder) | `ExerciseLaunch/` |
| `.../ActivityDefinitionLaunchContracts.cs`, `ActivityDefinitionLaunchEligibility.cs` | `ExerciseLaunchContracts.cs`, `ExerciseLaunchEligibility.cs` |
| `src/LinguaCoach.Infrastructure/ActivityDefinitions/` (folder) | `Exercises/` |
| `.../ActivityDefinitionCommandHandlers.cs`, `ActivityDefinitionMappers.cs`, `ActivityDefinitionQueryHandlers.cs`, `ActivityGenerationService.cs`* | `ExerciseCommandHandlers.cs`, `ExerciseMappers.cs`, `ExerciseQueryHandlers.cs`, `ExerciseGenerationService.cs` |
| `src/LinguaCoach.Infrastructure/ActivityDefinitionLaunch/ActivityDefinitionLaunchService.cs` | `src/LinguaCoach.Infrastructure/ExerciseLaunch/ExerciseLaunchService.cs` |
| `src/LinguaCoach.Api/Controllers/AdminActivityDefinitionController.cs` (route `api/admin/activities`) | `AdminExerciseController.cs` (route `api/admin/exercises`) |
| `src/LinguaCoach.Persistence/Configurations/ActivityDefinitionConfiguration.cs` | `ExerciseConfiguration.cs` |
| `.../ActivityResourceLinkConfiguration.cs`* | `ExerciseResourceLinkConfiguration.cs` |
| `.../StudentActivityDefinitionLaunchConfiguration.cs` | `StudentExerciseLaunchConfiguration.cs` |
| `tests/.../ActivityDefinitions/` (folder + files) | `Exercises/` (`ExerciseCommandHandlerTests.cs`, `ExerciseGenerationServiceTests.cs`) |
| `tests/.../ActivityDefinitionLaunch/ActivityDefinitionLaunchServiceTests.cs` | `ExerciseLaunch/ExerciseLaunchServiceTests.cs` |
| `tests/.../AdminActivityDefinitionEndpointTests.cs` | `AdminExerciseEndpointTests.cs` |
| `tests/.../ActivityDefinitionLaunchEndpointTests.cs` | `ExerciseLaunchEndpointTests.cs` |
| table `activity_definitions` | `exercises` |
| table `activity_resource_links` | `exercise_resource_links` |
| table `module_definition_activity_links` | `module_exercise_links` |
| table `student_activity_definition_launches` | `student_exercise_launches` |
| column `activity_definition_id` (everywhere it appeared as an FK) | `exercise_id` |
| `IGenerateModuleFromActivityHandler`, `GenerateModuleFromActivityRequest(Body)`, `RequireApprovedActivityAsync`, `ActivityLinkBuilder`, `ModuleActivityLinkDto`/`Input`, `.ActivityLinks` properties | `IGenerateModuleFromExerciseHandler`, `GenerateModuleFromExerciseRequest(Body)`, `RequireApprovedExerciseAsync`, `ExerciseLinkBuilder`, `ModuleExerciseLinkDto`/`Input`, `.ExerciseLinks` |

\* Not in the task's original exhaustive list but discovered mid-implementation to be part of the
same H4 concept (doc comments confirmed each is exclusively about `ActivityDefinition`, not shared
with `LearningActivity`) — included for consistency, flagged below under judgment calls.

### `ModuleDefinition` → `Module`

| Old | New |
|---|---|
| `src/LinguaCoach.Domain/Entities/ModuleDefinition.cs` (class) | `Module.cs` (class `Module`) |
| `src/LinguaCoach.Domain/Enums/ModuleActivityRole.cs`* | `ModuleExerciseRole.cs` |
| `src/LinguaCoach.Application/ModuleDefinitions/` (folder) | `Modules/` |
| `.../ModuleDefinitionContracts.cs` | `Modules/ModuleContracts.cs` |
| `.../ModuleGenerationContracts.cs` (already bare "Module") | `Modules/ModuleGenerationContracts.cs` (folder move only) |
| `src/LinguaCoach.Infrastructure/ModuleDefinitions/` (folder) | `Modules/` |
| `.../ModuleDefinitionCommandHandlers.cs`, `ModuleDefinitionMappers.cs`, `ModuleDefinitionQueryHandlers.cs` | `ModuleCommandHandlers.cs`, `ModuleMappers.cs`, `ModuleQueryHandlers.cs` |
| `.../ModuleGenerationService.cs` (already bare "Module") | `Modules/ModuleGenerationService.cs` (folder move only) |
| `src/LinguaCoach.Api/Controllers/AdminModuleDefinitionController.cs` (route `api/admin/modules`, unchanged) | `AdminModuleController.cs` |
| `src/LinguaCoach.Persistence/Configurations/ModuleDefinitionConfiguration.cs` | `ModuleConfiguration.cs` |
| `.../ModuleDefinitionActivityLinkConfiguration.cs` | `ModuleExerciseLinkConfiguration.cs` |
| `tests/.../ModuleDefinitions/` (folder + files) | `Modules/` (`ModuleCommandHandlerTests.cs`, `ModuleGenerationServiceTests.cs`) |
| `tests/.../AdminModuleDefinitionEndpointTests.cs` | `AdminModuleEndpointTests.cs` |
| table `module_definitions` | `modules` |
| column `module_definition_id` (everywhere it appeared as an FK, including on
  `StudentDailyModuleAssignment`/`StudentPracticeGymModuleAssignment`, which are themselves
  out of scope and unrenamed) | `module_id` |
| enum member `GeneratedFromLearnAndActivities` (on `ModuleSourceMode`) | `GeneratedFromLessonAndExercises` |

\* Judgment call, see below.

`src/LinguaCoach.Domain/Enums/ModuleSourceMode.cs` was already correctly named for the new
"Module" vocabulary and needed no file rename — only its `GeneratedFromLearnAndActivities` member
was renamed for accuracy.

## The EF migration

`src/LinguaCoach.Persistence/Migrations/20260710094217_Phase_I4_RenameLessonExerciseModule.cs`
(+ `.Designer.cs`, generated by `dotnet ef migrations add`).

`dotnet ef migrations add` initially scaffolded this as `DropTable`+`CreateTable` (EF's
default diff heuristic doesn't recognize a table rename when columns inside it also change
names in the same diff) — with the warning "An operation was scaffolded that may result in the
loss of data." That is not acceptable for a rename with real rows behind it, and violates the
task's explicit instruction to use `RenameTable`/`RenameColumn`. The migration's `Up`/`Down` were
hand-rewritten to use only `RenameTable`/`RenameColumn`/`RenameIndex` (kept the EF-generated
`.Designer.cs` target-model snapshot as-is, since that's correct regardless of how the diff is
expressed). Verified via `dotnet ef migrations script <prev> <this>` that the generated SQL is a
clean sequence of `ALTER TABLE ... RENAME TO`/`ALTER TABLE ... RENAME COLUMN`/
`ALTER INDEX ... RENAME TO` statements — no `DROP`/`CREATE`, no data loss. Postgres preserves PK,
FK, and index wiring automatically across a rename, so no FK drop/re-add was needed even for the
`StudentDailyModuleAssignment`/`StudentPracticeGymModuleAssignment` FK-column renames.

Table renames: `learn_items`→`lessons`, `activity_definitions`→`exercises`,
`module_definitions`→`modules`, `learn_item_resource_links`→`lesson_resource_links`,
`activity_resource_links`→`exercise_resource_links`,
`module_definition_learn_item_links`→`module_lesson_links`,
`module_definition_activity_links`→`module_exercise_links`,
`student_activity_definition_launches`→`student_exercise_launches`.

Column renames: `learn_item_id`→`lesson_id` (on `exercises`, `lesson_resource_links`,
`module_lesson_links`, `student_exercise_launches`), `activity_definition_id`→`exercise_id` (on
`exercise_resource_links`, `module_exercise_links`, `student_exercise_launches`),
`module_definition_id`→`module_id` (on `module_lesson_links`, `module_exercise_links`,
`student_exercise_launches`, `student_daily_module_assignments`,
`student_practice_gym_module_assignments`). All associated indexes renamed to match (see the
migration file for the full list).

## Validation

- `dotnet build --configuration Release` — clean, 0 errors, 12 pre-existing warnings (all
  unrelated to this change — nullable-reference warnings on unrelated entities, one xUnit
  analyzer suggestion).
- `dotnet test --configuration Release` — **3,424/3,424 passing, 0 failing** (5 architecture,
  2,107 unit, 1,312 integration) — exact match to the pre-phase baseline. No test logic changed,
  only symbol/namespace/string updates from the rename.
- Final grep sweep of `src/` and `tests/` for `LearnItem`, `ActivityDefinition`, `ModuleDefinition`
  (excluding the `Migrations/` folder): **zero hits** outside historical migration files, which
  are deliberately left as a historical record (consistent with how I0-I3 handled prior
  migrations — never rewritten after the fact).
- Confirmed via targeted grep that every explicitly out-of-scope family is present and completely
  untouched: `LearningActivity` (67 files), `ActivityAttempt` (55), `ActivityFeedbackSignal` (7),
  `ActivitySubmitHandler` (7), `ActivityGetHandler` (4 — file/class name unchanged; its *body*
  was updated to reference the renamed `Exercise`/`ExerciseLaunch` types it legitimately depends
  on, per the task's own note that this is expected), `ActivityController` (2), 
  `IAiActivityGenerator` (7), `AiActivityGeneratorHandler` (3), `ActivityNoveltyPolicy` (5),
  `ActivityContentFingerprintService` (7), `StudentActivityUsageLog` (10),
  `ActivityEvaluationContext` (3), `LearningModule` (29), `ExercisePatternDefinition`/
  `ExerciseTypeDefinition` (still present, e.g. referenced in a doc comment on the new
  `Exercise.cs` entity distinguishing the two concepts — left untouched).
- Did not touch `src/LinguaCoach.Web/`.

## Judgment calls

1. **`ActivitySourceMode`/`ActivityRendererType` renamed to `ExerciseSourceMode`/
   `ExerciseRendererType`.** Not in the task's exhaustive list, but their doc comments
   (`"Phase H4 — how an <see cref="Entities.ActivityDefinition"/>'s ... came to exist"`) confirmed
   they exist exclusively for the `ActivityDefinition`/`Exercise` entity, with no sharing/overlap
   with `LearningActivity`. Renaming them for consistency; flagging since the task's list didn't
   anticipate them.
2. **`ActivityResourceLink`/`ActivityResourceLinkConfiguration` renamed to
   `ExerciseResourceLink`/`ExerciseResourceLinkConfiguration`.** These file names contain
   "Activity" but not literally "ActivityDefinition," so a naive scripted substring match on
   `ActivityDefinition`→`Exercise` missed them on the first pass — caught by a second grep sweep
   for lower/mixed-case residuals before the build was attempted. In scope per the entity's own
   doc comments (it links a resource to an `ActivityDefinition`/`Exercise`, not a
   `LearningActivity`).
3. **`ModuleActivityRole` → `ModuleExerciseRole`.** The task flagged this explicitly as a
   judgment call ("could become `ModuleExerciseRole` for consistency, or use your judgment").
   Chose `ModuleExerciseRole` since `ModuleDefinitionActivityLink`→`ModuleExerciseLink` and the
   enum is solely `ModuleExerciseLink.Role`'s type — keeping "Activity" here while everything
   else around it says "Exercise" would read as inconsistent. Confirmed via grep it has no
   other consumers.
4. **`IGenerateModuleFromActivityHandler` family (interfaces/DTOs on the Module generation
   entry points) renamed to `...FromExercise...`.** Not in the task's list, but these are
   Module-generation-specific types (`GenerateModuleFromActivityRequest`,
   `RequireApprovedActivityAsync`, `ActivityLinkBuilder`, `ModuleActivityLinkDto`/`Input`) whose
   entire purpose is composing a Module from a Lesson + an `ActivityDefinition`/`Exercise` — left
   unrenamed they would have been a glaring, confusing remainder of the exact vocabulary this
   phase exists to retire. Renamed for consistency backend-wide; the matching frontend TS files
   (`admin-module-definition.service.ts`/`.models.ts`, e.g. `GenerateModuleFromActivityRequestBody`)
   were **not** touched, since frontend is out of scope for this pass and will need to line up
   with these new backend names in the frontend pass.
5. **Local variable names left partially inconsistent.** A handful of purely-local variables
   inside `ModuleGenerationService.cs`/`ExerciseLaunchService.cs`/etc. (e.g. `activity`,
   `activities`, `activityId` as parameter names, not the FK column `activityDefinitionId`)
   still read as generic English "activity" rather than "exercise." These are unambiguous,
   locally-scoped, and don't collide with the `LearningActivity` family or violate the final grep
   audit (which was scoped to the actual `LearnItem`/`ActivityDefinition`/`ModuleDefinition`
   identifiers, not the generic word "activity"). Judged not worth an open-ended cosmetic sweep
   given the risk of touching the `LearningActivity` boundary for no functional gain; flagging
   here rather than silently leaving it undocumented.
6. **Doc-comment prose ("Learn Item", "Activity Definition", "Module Definition" with spaces)**
   was swept separately from the identifier-level rename, since XML doc comments and exception
   messages use English prose, not PascalCase identifiers. One instance
   (`ModuleGenerationService.cs`'s class summary) had the phrase "Activity Definitions" split
   across a line wrap and needed a manual fix after the automated prose pass missed it — verified
   by rereading the file after the automated passes completed.
7. **Known future frontend/route naming collision, not addressed here (correctly out of scope):**
   `docs/architecture/product-language-renaming-i4.md` flags that the Angular frontend already has
   a page routed at `/admin/lessons` ("Today Delivery Health" diagnostics, largely inert since
   I2B). This backend pass's new `api/admin/lessons` route lives in a different namespace (backend
   API vs. Angular client route) so there is **no collision at the backend level** — but the
   frontend pass (I4 Pass 2/3) will still need to resolve the Angular route collision per that
   doc's open question before wiring up an "Admin Lessons" page to consume this new API.

## Risks / unresolved questions

- None blocking. The rename is mechanical and behavior-preserving; test count match (3,424/3,424)
  is strong evidence no logic changed.
- The migration has not been run against a live database in this session (no local Postgres was
  running) — verified correctness via `dotnet ef migrations script`, which does not require a live
  DB connection and confirmed the exact SQL that will run. Recommend running it against a real dev
  database before merging, as a final sanity check, since `migrations script` compiles the
  operations but doesn't execute them.
- Frontend (`src/LinguaCoach.Web/`) still references the old names throughout (DTOs, services,
  models, routes) and will not compile/type-check against this backend until the frontend pass
  lands — expected and called out as acceptable in the task brief.

## Final verdict

Complete and verified. Build clean, tests at exact baseline (3,424/3,424), migration is a
lossless rename (confirmed via generated SQL), final grep sweep confirms zero remaining
`LearnItem`/`ActivityDefinition`/`ModuleDefinition` references outside historical migrations, and
every explicitly out-of-scope family (`LearningActivity`, `ActivityAttempt`, `LearningModule`,
`ExercisePattern*`/`ExerciseType*`, etc.) is confirmed present and untouched.

## Next recommended action

I4 Pass 2/3 (frontend rename) per `docs/architecture/product-language-renaming-i4.md`'s suggested
I4c slice — component/service/model/route renames in `src/LinguaCoach.Web/`, resolving the
`/admin/lessons` route collision (judgment call #7 above), and updating the
`IGenerateModuleFromActivityHandler`-family frontend TS names
(`admin-module-definition.service.ts`/`.models.ts`) to match the backend renames made in this
pass. "Daily Lesson" → "Today Plan" (I4d) remains a separate, later slice not touched here.
