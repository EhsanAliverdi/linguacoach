# Phase 1 — Pipeline Safety and Data Integrity Fixes

**Date:** 2026-07-15
**Related sprint/feature:** Follow-up implementation phase for the two confirmed correctness
findings in `docs/reviews/2026-07-15-content-creation-pipeline-architecture-audit.md` ("Risks and
technical debt" items #1 and #2). Not a redesign — a narrowly scoped safety/data-integrity fix on
the existing Resource Bank → Lesson → Exercise → Module → student delivery pipeline.
**Type:** Implementation + engineering review (fix, tests, documentation)

## Files reviewed / changed

**Part A — archived Module delivery safety**
- `src/LinguaCoach.Domain/Entities/Module.cs` (read-only, confirmed `IsArchived`/`ReviewStatus` fields)
- `src/LinguaCoach.Application/Modules/ModuleEligibility.cs` (new)
- `src/LinguaCoach.Infrastructure/PracticeGymModules/PracticeGymModuleSelectionService.cs`
- `src/LinguaCoach.Infrastructure/TodayPlanModules/TodayPlanModuleSelectionService.cs`
- `src/LinguaCoach.Infrastructure/ExerciseLaunch/ExerciseLaunchService.cs`
- `src/LinguaCoach.Infrastructure/PracticeGymModules/PracticeGymModuleAssignmentRecorder.cs` (read-only, confirmed it derives purely from already-filtered selection results)
- `src/LinguaCoach.Infrastructure/TodayPlanModules/TodayPlanModuleAssignmentRecorder.cs` (read-only, same confirmation)
- `tests/LinguaCoach.UnitTests/PracticeGymModules/PracticeGymModuleSelectionServiceTests.cs`
- `tests/LinguaCoach.UnitTests/TodayPlanModules/TodayPlanModuleSelectionServiceTests.cs`
- `tests/LinguaCoach.UnitTests/ExerciseLaunch/ExerciseLaunchServiceTests.cs`

**Part B — Lesson-to-Exercise provenance integrity**
- `src/LinguaCoach.Application/Exercises/ExerciseGenerationContracts.cs` (`GenerateActivityFromResourcesRequest` gains `Guid? LessonId = null`)
- `src/LinguaCoach.Infrastructure/Exercises/ExerciseGenerationService.cs` (threads `request.LessonId` instead of hardcoded `null` in the resources-entry handler)
- `src/LinguaCoach.Infrastructure/Exercises/AiExerciseGenerationService.cs` (threads `request.LessonId`/derives `SourceMode` instead of hardcoding `null`/`GeneratedFromResources`)
- `src/LinguaCoach.Infrastructure/Exercises/LessonExerciseBatchGenerationService.cs` (`BuildResourcesRequestAsync` now passes `LessonId: lesson.Id`; added a defensive post-generation check)
- `tests/LinguaCoach.UnitTests/Exercises/AiExerciseGenerationServiceTests.cs` (new LessonId-provenance tests)
- `tests/LinguaCoach.UnitTests/Exercises/LessonExerciseBatchGenerationServiceTests.cs` (new file)

## Findings — verified against current code, not just the audit

Both audit findings were independently re-verified against the live code before any change was
made (per the working rules — audit report is not treated as ground truth on its own).

### Part A — exact root cause

Student-facing Module eligibility is decided independently at three call sites, and none of them
checked `Module.IsArchived` before this phase:

1. `PracticeGymModuleSelectionService.SelectAsync` (`PracticeGymModuleSelectionService.cs:45`, pre-fix) — new Practice Gym suggestion query.
2. `TodayPlanModuleSelectionService.SelectAsync` (`TodayPlanModuleSelectionService.cs:41`, pre-fix) — new Today Plan selection query.
3. `ExerciseLaunchService.LaunchAsync` (`ExerciseLaunchService.cs:45`, pre-fix) — the launch-time re-validation gate, the only one of the three that actually materializes runtime state (`LearningActivity` + `StudentExerciseLaunch`).

All three checked only `ReviewStatus == Approved`. `Module.IsArchived`'s own doc comment
(`Module.cs:62-66`) only promises that archiving "never cascades" and existing assignments "keep
resolving" — it does not say archiving blocks new selection/assignment/launch, and no other
in-repo document stated an explicit policy either way. The two write paths that record assignments
(`PracticeGymModuleAssignmentRecorder`, `TodayPlanModuleAssignmentRecorder`) do not independently
query `Modules` — they only persist rows for modules already present in a selection result — so
they needed no direct change; they inherit the fix from the selection services.

**Every Module selection/assignment/launch path inspected:**
`PracticeGymModuleSelectionService`, `TodayPlanModuleSelectionService`, `ExerciseLaunchService`,
`ExerciseLaunchEligibility` (Exercise-level only, confirmed it never receives Module archival
status so the gap could not be closed there), `PracticeGymModuleAssignmentRecorder`,
`TodayPlanModuleAssignmentRecorder`, `AdminPracticeGymModuleController.PreviewSelection` and
`AdminTodayPlanModuleController.PreviewSelection` (read-only admin preview surfaces, no
independent Module query — they call the selection services above and inherit the fix), and the
admin authoring surfaces `ModuleQueryHandlers`/`ModuleRepairService` (already correctly filtered
`!IsArchived` for the admin list/repair UI — confirmed as a pre-existing correct pattern, not a
gap). No standalone `CreateAssignmentCommand`/`AssignModuleCommand` exists; assignment only
happens via `selection service → assignment recorder → ExerciseLaunchService` — there is no
bypassable direct-assignment endpoint.

### Part B — exact root cause

`LessonExerciseBatchGenerationService` (the Lesson-detail batch "Generate Exercises" flow) routes
five AI-preferred activity types (`multiple_choice_single`,
`reading_multiple_choice_single`/`_multi`, `listening_multiple_choice_single`/`_multi`) through
`IGenerateActivityFromResourcesWithAiHandler` (`AiExerciseGenerationService`) instead of the
Lesson-aware deterministic handler. Its `BuildResourcesRequestAsync` helper
(`LessonExerciseBatchGenerationService.cs:130-149`, pre-fix) received `lessonId` as a parameter,
used it only to look up the Lesson and its resource links, and built a
`GenerateActivityFromResourcesRequest` that had **no `LessonId` property to carry the value** —
every other Lesson-derived field (title, CEFR, skill, subskill, tags, difficulty) survived the
trip; only the Lesson id itself was structurally unable to. `AiExerciseGenerationService`
(`AiExerciseGenerationService.cs:277`, pre-fix) then hardcoded `lessonId: null` unconditionally on
every `new Exercise(...)` it constructed, because its input contract had nowhere to receive one.
The bug was type-dependent within a single batch call: deterministic-type items in the same batch
correctly got `LessonId`; AI-preferred-type items did not.

The deterministic Lesson handler (`ActivityGenerationService.HandleAsync(GenerateActivityFromLessonRequest...)`)
and its shared composer (`ComposeAndSaveAsync`) were already correct — the leak was isolated to the
AI-resources hop.

## Implementation

### Part A

- Added `LinguaCoach.Application.Modules.ModuleEligibility` — the one shared eligibility
  predicate, in both EF-translatable (`Expression<Func<Module,bool>>`, used in `.Where(...)`
  against `IQueryable<Module>`) and compiled (`Func<Module,bool>`, used against an
  already-materialized entity) forms:
  `module.ReviewStatus == AdminReviewStatus.Approved && !module.IsArchived`.
- `PracticeGymModuleSelectionService`/`TodayPlanModuleSelectionService` now filter with
  `ModuleEligibility.AvailableForNewStudentDeliveryExpr` in their initial `_db.Modules.Where(...)`
  query — archived modules never enter the candidate pool for a new suggestion/selection.
- `ExerciseLaunchService.LaunchAsync` now has an explicit second guard after the existing
  approval check: `if (!ModuleEligibility.IsAvailableForNewStudentDelivery(module)) return
  Unsupported(..., "This module has been archived and is no longer available for new
  practice.")`. This runs even if a stale client still holds a suggestion for a module archived
  after being suggested — the launch-time re-check is the actual enforcement point since it is
  the only one of the three sites that creates real runtime rows.
- **Existing-assignment policy (explicit, as required by the working rules):** archiving a Module
  blocks only *new* selection/assignment/launch. It does not invalidate, hide, or block completion
  of any `StudentTodayPlanModuleAssignment`/`StudentPracticeGymModuleAssignment`/
  `StudentExerciseLaunch` row created before archival, and does not touch the `LearningActivity`
  a prior launch already materialized. This matches `Module.IsArchived`'s existing doc comment
  ("archiving never deletes the row... existing assignments... keep resolving") and is the least
  destructive of the two readings raised as an open question in the architecture audit. Verified
  by `Archiving_module_after_launch_preserves_existing_launch_bridge_row` — a module is launched
  successfully, archived, and the prior `StudentExerciseLaunch`/`LearningActivity` rows are
  confirmed still present and unchanged while a *second* launch attempt against the same module is
  rejected.

### Part B

- `GenerateActivityFromResourcesRequest` gained an additional trailing `Guid? LessonId = null`
  parameter — `null` for a direct "generate from resources" call with no Lesson context (unchanged
  behavior for that entry point), set when the request is Lesson-derived.
- `ActivityGenerationService.HandleAsync(GenerateActivityFromResourcesRequest...)` now passes
  `lessonId: request.LessonId` instead of a hardcoded `null` into its shared `ComposeAndSaveAsync`
  composer (this composer already correctly branched `SourceMode` on `lessonId.HasValue`, so this
  one-line change alone made the deterministic resources-entry path Lesson-aware too, with no
  behavior change for direct/non-Lesson callers since they leave `LessonId` unset).
- `AiExerciseGenerationService.HandleAsync` now derives `sourceMode` from
  `request.LessonId.HasValue` (mirroring the deterministic composer) and passes
  `lessonId: request.LessonId` into the `Exercise` constructor instead of the previous hardcoded
  `ExerciseSourceMode.GeneratedFromResources` / `lessonId: null`.
- `LessonExerciseBatchGenerationService.BuildResourcesRequestAsync` now passes `LessonId:
  lesson.Id` when constructing the `GenerateActivityFromResourcesRequest` it hands to the AI
  resources handler.
- Added a defensive check in the batch loop: after each Exercise is created (deterministic or AI
  path), if `activity.LessonId != request.LessonId` the whole (already-transacted) batch call
  throws `ExerciseValidationException` rather than silently persisting an orphaned Exercise —
  satisfying "fail clearly rather than persisting an Exercise with a missing Lesson ID."
- Direct Resource→Exercise generation (no Lesson context) is unchanged: `LessonId` stays `null`
  for that entry point, and the handler/interface/API surface was not modified or removed.

## Tests added

- **Part A** (`tests/LinguaCoach.UnitTests`):
  - `PracticeGymModuleSelectionServiceTests.Approved_archived_module_not_selected`
  - `TodayPlanModuleSelectionServiceTests.Approved_archived_module_not_selected`
  - `ExerciseLaunchServiceTests.Launch_rejected_for_archived_module`
  - `ExerciseLaunchServiceTests.Archiving_module_after_launch_preserves_existing_launch_bridge_row`
  - Existing tests for pending/rejected modules, non-archived approval, and Exercise-level
    eligibility (`ExerciseLaunchEligibility`) continue to pass unchanged, confirming no regression
    to the non-archived path.
- **Part B** (`tests/LinguaCoach.UnitTests/Exercises`):
  - `AiExerciseGenerationServiceTests.Request_with_lesson_id_creates_exercise_retaining_lesson_id`
  - `AiExerciseGenerationServiceTests.Request_without_lesson_id_creates_exercise_with_null_lesson_id`
  - `LessonExerciseBatchGenerationServiceTests` (new file):
    `Deterministic_type_generated_from_lesson_retains_lesson_id`,
    `Ai_preferred_type_generated_from_lesson_retains_lesson_id`,
    `Batch_mixing_deterministic_and_ai_preferred_types_all_retain_lesson_id`,
    `Exercise_retrieval_by_lesson_returns_all_exercises_from_batch`,
    `Batch_result_reports_module_auto_link_for_ai_preferred_exercise`.

## Validation

```text
dotnet build --configuration Release        → Build succeeded, 0 errors (pre-existing warnings only)
dotnet test tests/LinguaCoach.UnitTests --configuration Release
    → Passed: 2272, Failed: 0, Skipped: 0
dotnet test tests/LinguaCoach.IntegrationTests --configuration Release
    → Passed: 1324, Failed: 0, Skipped: 0
dotnet test tests/LinguaCoach.ArchitectureTests --configuration Release
    → Passed: 5, Failed: 0, Skipped: 0
```

No Angular/`LinguaCoach.Web` source files were changed this phase, so `npm run build`/`npm
test`/Playwright were not run — nothing in this phase has a frontend runtime surface to exercise
(the student/admin UIs already call the same backend endpoints; only their eligibility and
provenance logic changed).

## Deferred findings (documented, not implemented — out of this phase's scope)

- The direct Resource→Exercise generation path (`IGenerateActivityFromResourcesHandler`/
  `IGenerateActivityFromResourcesWithAiHandler` called with no `LessonId`) remains fully live and
  API-reachable, matching the architecture audit's open question #1 about whether it's an
  intentional supported alternative or vestigial. Not resolved here — no removal or redesign was
  in scope for Phase 1.
- `ModuleGenerationService`/`AiModuleGenerationService`/`ModuleLinkBuilder` filter *source*
  Lesson/Exercise rows by `ReviewStatus == Approved` when composing a **new** Module draft, but do
  not additionally check `Lesson.IsArchived`/`Exercise.IsArchived` at that authoring-time step.
  This is a different code path from the Part A student-delivery leak (it affects what an admin
  can compose a Module *from*, not what's offered to students) and was out of this phase's stated
  scope (`ExerciseLaunchService`/selection services only). Flagged for a future pass if archived
  Lessons/Exercises being composable into new Modules is judged undesirable.
- The "duplicated what's-published logic" maintainability risk the architecture audit raised is
  now reduced but not eliminated for Module (three call sites now share `ModuleEligibility` instead
  of inlining the predicate) — Lesson/Exercise approval-status checks elsewhere in the codebase
  still duplicate their own predicates independently. Broader consolidation across those types was
  not in scope.
- A `Modules` table index on `IsArchived` was not added — the existing predicate is a simple boolean
  AND on an already-narrow `ReviewStatus`-filtered set; not judged necessary at current data volume,
  but worth revisiting if Module counts grow significantly.

## Known limitations

- `ModuleEligibility.IsAvailableForNewStudentDelivery` governs *new* selection/assignment/launch
  only, by design. It intentionally does not gate re-resolution/completion of any assignment or
  launch created before archival — this is the explicit, tested policy documented above, not an
  oversight.
- The Part B fix closes the specific hop where `LessonId` was dropped (the AI-resources handoff
  inside the Lesson batch flow). It does not add a database-level constraint enforcing
  `Exercise.LessonId` non-null for `SourceMode == GeneratedFromLesson` — the defensive
  application-level check in `LessonExerciseBatchGenerationService` and the two composer-level
  fixes are judged sufficient for this phase; a schema-level CHECK constraint was not pursued
  (would require a migration and is a larger, more invasive change than "keep the fix narrowly
  scoped" calls for).

## Final verdict

Both confirmed audit findings were reproduced against current code, root-caused precisely, and
fixed with the least destructive change available in each case — a shared eligibility predicate
reused at the three archived-Module gaps, and a single missing field threaded through the existing
Lesson→AI-resources handoff for provenance. No architecture, versioning, publishing, catalogue,
Import, or legacy-system changes were made. All 3,601 backend tests (unit + integration +
architecture) pass. Ready to commit locally.

## Next recommended action

Address the deferred findings above only if/when they become a stated priority — none of them are
correctness bugs in the sense Part A/B were; they're either intentional-but-undocumented design
questions (direct Resource→Exercise path) or a narrower, lower-severity variant of the same
duplication pattern (Module authoring-time source filtering) already flagged by the original
architecture audit.
