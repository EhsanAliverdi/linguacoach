---
title: Phase I4 Pass 3 — "Daily Lesson" → "Today Plan" Rename (final pass, closes Phase I4)
date: 2026-07-10
related: I-track, implements the final slice of the I4 product-language rename decided in
  docs/architecture/product-language-renaming-i4.md; follows
  docs/reviews/2026-07-10-phase-i4-pass1-backend-rename-review.md (backend) and
  docs/reviews/2026-07-10-phase-i4-pass2-frontend-rename-review.md (frontend)
status: complete
---

# Phase I4 Pass 3 — "Daily Lesson" → "Today Plan" Rename

**Date:** 2026-07-10
**Type:** implementation phase — the final I4 slice. Renames the H6-era "Daily Lesson module
pipeline" concept (the bank-first, deterministic daily content selector behind the student's
"Today" page) to **Today Plan**, across backend and frontend, including the
`TodaysSessionResult.ModuleSection` field and the H6 assignment-bookkeeping entity
`StudentDailyModuleAssignment`. This is the last of the three I4 passes and closes the whole
phase; see the "Phase I4 closing summary" section below for the recap across all three passes.

## Files reviewed

- `docs/architecture/product-language-renaming-i4.md` (the I4 decision doc)
- `docs/reviews/2026-07-10-phase-i4-pass1-backend-rename-review.md` and
  `docs/reviews/2026-07-10-phase-i4-pass2-frontend-rename-review.md` (Pass 1/2 records — confirmed
  what they had already touched, since Pass 2 had already renamed some field names inside the
  Daily Lesson-adjacent types, e.g. `moduleDefinitionId`→`moduleId`)
- Every file matching a fresh case-sensitive grep for `DailyLesson` across `src/`, `tests/`, and
  `src/LinguaCoach.Web/src` before starting — the task brief's file list was confirmed accurate
  with one addition found mid-implementation (see judgment calls).

## The rename table

### Backend — Application layer

| Old | New |
|---|---|
| `src/LinguaCoach.Application/DailyLessonModules/` (folder) | `TodayPlanModules/` |
| `.../DailyLessonModuleSelectionContracts.cs` | `TodayPlanModuleSelectionContracts.cs` |
| `DailyLessonModuleSelectionRequest` | `TodayPlanModuleSelectionRequest` |
| `DailyLessonModuleSelectionResult` | `TodayPlanModuleSelectionResult` |
| `DailyLessonLessonView` | `TodayPlanLessonView` |
| `DailyLessonActivityView` | `TodayPlanActivityView` |
| `IDailyLessonModuleSelectionService` | `ITodayPlanModuleSelectionService` |
| `IDailyLessonModuleAssignmentRecorder` | `ITodayPlanModuleAssignmentRecorder` |
| `SelectedModuleResult` | unchanged (already bare, no `DailyLesson` prefix to begin with) |

### Backend — Infrastructure layer

| Old | New |
|---|---|
| `src/LinguaCoach.Infrastructure/DailyLessonModules/` (folder) | `TodayPlanModules/` |
| `.../DailyLessonModuleSelectionService.cs` (class `DailyLessonModuleSelectionService`) | `TodayPlanModuleSelectionService.cs` (class `TodayPlanModuleSelectionService`) |
| `.../DailyLessonModuleAssignmentRecorder.cs` (class `DailyLessonModuleAssignmentRecorder`) | `TodayPlanModuleAssignmentRecorder.cs` (class `TodayPlanModuleAssignmentRecorder`) |

### Backend — API layer

| Old | New |
|---|---|
| `src/LinguaCoach.Api/Controllers/AdminDailyLessonModuleController.cs` (class `AdminDailyLessonModuleController`) | `AdminTodayPlanModuleController.cs` (class `AdminTodayPlanModuleController`) |
| Route `GET api/admin/daily-lesson/modules/preview` | `GET api/admin/today-plan/modules/preview` |
| Route `GET api/admin/daily-lesson/students/{studentId}/assignments` | `GET api/admin/today-plan/students/{studentId}/assignments` |

### Backend — Domain / Persistence (the H6 bookkeeping entity)

| Old | New |
|---|---|
| `src/LinguaCoach.Domain/Entities/StudentDailyModuleAssignment.cs` (class `StudentDailyModuleAssignment`) | `StudentTodayPlanModuleAssignment.cs` (class `StudentTodayPlanModuleAssignment`) |
| `src/LinguaCoach.Domain/Enums/DailyModuleAssignmentStatus.cs` (enum `DailyModuleAssignmentStatus`) | `TodayPlanModuleAssignmentStatus.cs` (enum `TodayPlanModuleAssignmentStatus`) |
| `src/LinguaCoach.Persistence/Configurations/StudentDailyModuleAssignmentConfiguration.cs` | `StudentTodayPlanModuleAssignmentConfiguration.cs` |
| table `student_daily_module_assignments` | `student_today_plan_module_assignments` |
| index `ix_daily_module_assignments_status` | `ix_today_plan_module_assignments_status` |
| index `ix_daily_module_assignments_student_date` | `ix_today_plan_module_assignments_student_date` |
| index `ix_daily_module_assignments_student_module` | `ix_today_plan_module_assignments_student_module` |
| index `IX_student_daily_module_assignments_module_id` | `IX_student_today_plan_module_assignments_module_id` |
| `LinguaCoachDbContext.StudentDailyModuleAssignments` (`DbSet<StudentDailyModuleAssignment>`) | `.StudentTodayPlanModuleAssignments` (`DbSet<StudentTodayPlanModuleAssignment>`) |

The enum's *member values* (`Selected`, `Presented`, `Skipped`, `Consumed`, `Expired`,
`FallbackOnly`) were unchanged — none of them literally said "Daily," so the stored string
values (`HasConversion<string>()`) are untouched by this pass; only the C# type name changed.

### Backend — `TodaysSessionResult` field rename (judgment call)

| Old | New |
|---|---|
| `TodaysSessionResult(bool Available, DailyLessonModuleSelectionResult? ModuleSection)` | `TodaysSessionResult(bool Available, TodayPlanModuleSelectionResult? TodayPlan)` |

`ModuleSection` → **`TodayPlan`**. The task brief flagged this as a judgment call with `TodayPlan`
as the "likely candidate." Chosen exactly as suggested: `TodaysSessionResult.TodayPlan` reads as
"give me today's plan," which is both more natural English and matches the new product vocabulary
directly, whereas `ModuleSection` was itself a residual of the old internal "Daily Lesson module"
terminology. Every consumer was updated to match: `SessionQueryHandler` (local variable
`moduleSection`→`todayPlan`), `StudentDashboardSummaryHandler.BuildTodaySession`
(`session.ModuleSection`→`session.TodayPlan`), the JSON field the frontend deserializes
(`moduleSection`→`todayPlan`, camelCase per ASP.NET's default JSON naming policy), and every test
that asserted on the JSON shape (`TodayPlanModulePipelineEndpointTests`,
`SessionEndpointTests.Today_CourseReadyStudent_ReturnsHonestShape`).

### Backend — `ExerciseLaunchSource` enum member (found via grep, in scope)

| Old | New |
|---|---|
| `ExerciseLaunchSource.DailyLesson` | `ExerciseLaunchSource.TodayPlan` |

Not in the task brief's explicit list, but a direct hit on the initial `DailyLesson` grep sweep
(`src/LinguaCoach.Domain/Enums/ExerciseLaunchSource.cs`). This H10 enum tracks which surface
triggered a launch (`PracticeGym`/`DailyLesson`/`AdminPreview`) — `DailyLesson` unambiguously
refers to the same Today concept this pass renames, and had zero live consumers beyond its own
declaration (confirmed via grep — `ExerciseLaunchSource.DailyLesson` is defined but not yet
referenced anywhere else in the codebase, consistent with `TODO-H10-2`'s note that Today's
dashboard module card is still display-only and hasn't wired a Start action yet). The stored value
is `HasConversion<string>()` on `StudentExerciseLaunch.Source`; renaming the member changes the
string that would be written for future `TodayPlan`-sourced launches, acceptable per this project's
no-compatibility-shim convention (not yet deployed, no live rows use this value yet).

### Backend — doc-comment updates (no symbol change)

`PracticeGymModuleSelectionContracts.cs`, `PracticeGymModuleSelectionService.cs`,
`PracticeGymModuleAssignmentRecorder.cs`, `PracticeGymModuleSelectionServiceTests.cs`,
`SessionLifecycleHandler.cs` all had prose references to "Daily Lesson"/`DailyLessonModuleSelectionRequest`/
`DailyLessonModuleAssignmentRecorder`/`DailyLessonModuleSelectionServiceTests` in XML doc comments
(H7's Practice Gym pipeline explicitly documents itself as "mirroring H6's" selector/recorder) —
updated to the new names for accuracy, since a stale doc-comment reference to a renamed type is
exactly the kind of residual this phase exists to eliminate.

### Backend — tests

| Old | New |
|---|---|
| `tests/LinguaCoach.UnitTests/DailyLessonModules/` (folder) | `TodayPlanModules/` |
| `.../DailyLessonModuleSelectionServiceTests.cs` (class `DailyLessonModuleSelectionServiceTests`) | `TodayPlanModuleSelectionServiceTests.cs` (class `TodayPlanModuleSelectionServiceTests`) |
| `tests/LinguaCoach.IntegrationTests/Api/DailyLessonModulePipelineEndpointTests.cs` (class `DailyLessonModulePipelineEndpointTests`) | `TodayPlanModulePipelineEndpointTests.cs` (class `TodayPlanModulePipelineEndpointTests`) |

`TodayPlanModulePipelineEndpointTests` also had its JSON assertions updated
(`moduleSection`→`todayPlan`) and its admin-preview route strings updated
(`/api/admin/daily-lesson/modules/preview`→`/api/admin/today-plan/modules/preview`).

`SessionEndpointTests.cs` (not in the task's file list, found via the post-rename build failure)
had one JSON-property assertion (`body.TryGetProperty("moduleSection", ...)`) and two doc-comment
references to "Daily Lesson Module" — both updated. This file lives outside the
`DailyLessonModules`-named folders the initial grep targeted, so it was the one genuine miss caught
only by the build/test loop rather than the grep sweep — flagged in judgment calls below.

`PracticeGymModuleSelectionServiceTests.cs` had one test
(`Today_module_pipeline_remains_unaffected`) directly constructing a
`StudentDailyModuleAssignment`/`DailyModuleAssignmentStatus` and querying
`_db.StudentDailyModuleAssignments` — updated to the renamed entity/enum/DbSet.

### Frontend

| Old | New |
|---|---|
| `session.models.ts`: `DailyLessonLessonView` | `TodayPlanLessonView` |
| `DailyLessonActivityView` | `TodayPlanActivityView` |
| `SelectedDailyLessonModule` | `SelectedTodayPlanModule` |
| `DailyLessonModuleSection` | `TodayPlanModuleSection` |
| `TodaysSessionResponse.moduleSection` | `.todayPlan` (matches the backend field rename) |
| `admin.models.ts`: `AdminDailyLessonSelectedModule` | `AdminTodayPlanSelectedModule` |
| `AdminDailyLessonModulePreview` | `AdminTodayPlanModulePreview` |
| `admin.api.service.ts`: `getDailyLessonModulePreview()` | `getTodayPlanModulePreview()` (calls `${api}/today-plan/modules/preview`, matches the backend route rename) |
| `dashboard.component.ts`: signal `dailyLessonModuleSection` | `todayPlan` (matches the `TodaysSessionResult.TodayPlan` field name for consistency top-to-bottom) |
| `admin-student-detail.component.ts`: signals `dailyLessonModulePreview`/`Loading`/`Error` | `todayPlanModulePreview`/`Loading`/`Error` |

### Frontend — UI copy (student-facing, not just variable names)

| Location | Old text | New text |
|---|---|---|
| `dashboard.component.html` `<h1>` | "Today's Lesson" | "Today's Plan" |
| `dashboard.component.html` eyebrow `<p>` | "Today's lesson" | "Today's plan" |
| `dashboard.component.html` `data-testid` | `daily-lesson-module-card` | `today-plan-module-card` |
| `dashboard.component.html` comment | "bank-first Daily Lesson Module" | "bank-first Today Plan Module" |
| `admin-student-detail.component.html` card title | "Daily Lesson module selection" | "Today Plan module selection" |
| `admin-student-detail.component.html` `data-testid` | `daily-lesson-module-preview-card` | `today-plan-module-preview-card` |
| loading/error/empty state text | "Loading Daily Lesson module preview" / "Daily Lesson module preview unavailable" / "Daily Lesson module preview not available." | "Loading Today Plan module preview" / "Today Plan module preview unavailable" / "Today Plan module preview not available." |

**Judgment call:** the task brief explicitly asked for UI-copy wording review, not just
variable/testid renames. The dashboard's page `<h1>` ("Today's Lesson") and its card eyebrow
("Today's lesson") were not literal instances of the string "Daily Lesson," but they are the
literal student-facing label for the exact H6 concept this pass renames — leaving them as "Today's
Lesson" while every internal name became "Today Plan" would read as an inconsistent half-rename to
a future reader/user. Renamed to "Today's Plan" for both, matching the new product vocabulary
directly. The generic empty-state copy ("We don't have a lesson ready for you right now.") was left
as-is — it doesn't name the H6 concept, reads fine standalone, and rewriting it risked scope creep
into unrelated copy polish.

### Frontend — tests

`dashboard.component.spec.ts`: `DailyLessonModuleSection` type import → `TodayPlanModuleSection`;
`TodaysSessionResponse` fixtures' `moduleSection` field → `todayPlan`; the
`daily-lesson-module-card` query selector → `today-plan-module-card`.

## The EF migration

`src/LinguaCoach.Persistence/Migrations/20260710103845_Phase_I4_Pass3_RenameDailyLessonToTodayPlan.cs`
(+ `.Designer.cs`, generated by `dotnet ef migrations add`).

Same pattern as Pass 1: `dotnet ef migrations add` initially scaffolded this as `DropTable`+
`CreateTable` (EF's diff heuristic doesn't recognize a table rename when the table's own name
changes in the same diff), with the data-loss warning. Hand-rewrote `Up`/`Down` to use only
`RenameTable`/`RenameIndex` — and, matching Pass 1's own convention exactly (confirmed by
re-reading Pass 1's migration before writing this one), deliberately did **not** rename the
`PK_student_daily_module_assignments` primary-key constraint or the
`FK_student_daily_module_assignments_modules_module_id` foreign-key constraint names, since Pass 1
left analogous `PK_`/`FK_` constraint names unrenamed across every one of its own table renames
(only `ix_`-prefixed custom indexes and default-named `IX_...` FK-lookup indexes were renamed).
Postgres doesn't require constraint names to match their owning table's current name, so this is
purely cosmetic and consistent with the established project convention, not a correctness gap.

Verified via `dotnet ef migrations script 20260710094217_Phase_I4_RenameLessonExerciseModule
20260710103845_Phase_I4_Pass3_RenameDailyLessonToTodayPlan` that the generated SQL is exactly:

```sql
ALTER TABLE student_daily_module_assignments RENAME TO student_today_plan_module_assignments;
ALTER INDEX ix_daily_module_assignments_status RENAME TO ix_today_plan_module_assignments_status;
ALTER INDEX ix_daily_module_assignments_student_date RENAME TO ix_today_plan_module_assignments_student_date;
ALTER INDEX ix_daily_module_assignments_student_module RENAME TO ix_today_plan_module_assignments_student_module;
ALTER INDEX "IX_student_daily_module_assignments_module_id" RENAME TO "IX_student_today_plan_module_assignments_module_id";
```

No `DROP`/`CREATE`, no data loss. This is the only table this pass touches — Pass 1 already
covered every other renamed table (`lessons`, `exercises`, `modules`, etc.), and
`student_practice_gym_module_assignments` (H7) is explicitly out of scope.

## Validation

- `dotnet build --configuration Release` — clean, 0 errors (one intermediate build had 4 errors
  from a residual `StudentDailyModuleAssignment`/`DailyModuleAssignmentStatus` reference in
  `PracticeGymModuleSelectionServiceTests.cs` that the initial grep-driven rename pass missed; the
  build/test loop caught it, fixed, rebuilt clean).
- `dotnet ef migrations script` — confirmed lossless rename SQL, see above.
- `dotnet test --configuration Release` — **3,424/3,424 passing, 0 failing** (5 architecture,
  2,107 unit, 1,312 integration) — exact match to the Pass 1/2 baseline, confirming this pass is
  pure rename with zero test-count-changing logic. One integration test
  (`SessionEndpointTests.Today_CourseReadyStudent_ReturnsHonestShape`) failed on the first full run
  after the C# rename because it asserted on the pre-rename JSON property name `moduleSection`;
  fixed to assert on `todayPlan` and the rerun passed clean.
- `cd src/LinguaCoach.Web && npm run build -- --configuration production` — clean, zero new
  TypeScript/Angular compile errors. The only `[ERROR]` in the build output is the pre-existing
  bundle-size budget overage (`1.00 MB budget not met by 1.56 MB, total 2.56 MB`), identical to
  Pass 2's build and explicitly called out in the task brief as acceptable and unrelated to this
  change.
- Final grep sweep of `src/`, `tests/`, and `src/LinguaCoach.Web/src` for `DailyLesson`: the only
  remaining hits are (a) the historical H6 migration file
  `20260709104812_Phase_H6_AddDailyLessonModulePipeline.cs`/`.Designer.cs`, deliberately left
  untouched per this project's convention of never rewriting historical migrations, (b) this
  pass's own new migration's class name
  (`Phase_I4_Pass3_RenameDailyLessonToTodayPlan`), which intentionally encodes the old name in the
  migration's own identity as a historical record of what it renamed, and (c) one intentional
  doc-comment note in `SessionGeneratorCommands.cs` and `dashboard.component.ts` explaining that a
  field/signal was "renamed from `ModuleSection`/`dailyLessonModuleSection`" — kept for future
  readers tracing history, not a residual.
- Confirmed `StudentPracticeGymModuleAssignment` (H7's Practice Gym equivalent bookkeeping table)
  and `IPracticeGymModuleSelectionService`/`PracticeGymModuleSelectionService` (H7's selector) are
  present and **completely untouched** — grep confirms zero changes to either file's symbol names,
  table name, or column names in this pass; only their doc comments referencing H6's renamed types
  by name were updated (see "doc-comment updates" above), which is expected and correct (a stale
  cross-reference to a renamed sibling type is a real residual, not scope creep into H7 itself).

## Judgment calls

1. **`TodaysSessionResult.ModuleSection` → `.TodayPlan`.** Chosen exactly as the task brief's
   suggested candidate. See the dedicated section above for the reasoning.
2. **`StudentDailyModuleAssignment` → `StudentTodayPlanModuleAssignment`** (not a shorter
   `StudentTodayModuleAssignment`). The task brief offered both as options. Chose the longer form
   to keep perfect symmetry with every other renamed type in this pass
   (`ITodayPlanModuleSelectionService`, `TodayPlanModuleSelectionResult`,
   `TodayPlanModuleAssignmentStatus`) — a reader scanning the `TodayPlanModules` namespace/folder
   alongside this entity should see one consistent `TodayPlan`-prefixed family, not a
   shortened outlier. The table name (`student_today_plan_module_assignments`) and index names
   follow the same full-length convention.
3. **`ExerciseLaunchSource.DailyLesson` → `.TodayPlan`.** Not in the task's explicit list but a
   direct grep hit and unambiguously the same concept (see the dedicated section above). Confirmed
   zero live consumers before renaming, so this carried zero behavioral risk.
4. **UI copy: "Today's Lesson"/"Today's lesson" → "Today's Plan"/"Today's plan".** The task brief
   asked for UI-copy review beyond variable renames; this was the one place a literal student-facing
   label needed updating even though it didn't contain the literal string "Daily Lesson." See the
   dedicated section above.
5. **Did not rename `PK_`/`FK_` constraint names in the migration**, matching Pass 1's own
   established (if implicit) convention rather than introducing a new one — see "The EF migration"
   section above.
6. **`SelectedModuleResult` left unrenamed.** It has no "Daily"/"DailyLesson" prefix to begin with
   (was already a bare, shared-feeling name even before this phase), and it's referenced by both
   `TodayPlanModuleSelectionResult.SelectedModules` and admin preview responses — renaming it was
   never in scope and would have been a gratuitous, unrequested rename.

## Frontend structure vs. task brief assumptions

The task brief's assumed file/folder names and field shapes were confirmed accurate by grepping
fresh rather than trusting memory, per its own explicit instruction. One correction: the task
described `DailyLessonLearnItemView`/`DailyLessonActivityView` as possibly-already-renamed by
Pass 2's `LearnItem`→`Lesson`/field-rename work; on inspection the actual Pass-2-era names were
`DailyLessonLessonView` (already renamed from `DailyLessonLearnItemView` by Pass 2's `LearnItem`→
`Lesson` symbol rename, but its own file/type name still carried the `DailyLesson` prefix this pass
needed to finish) and `DailyLessonActivityView` (Pass 2 explicitly chose to keep this name as-is,
per that pass's own judgment call 4, since it matches the backend's literal type name at the time —
this pass now renames both `DailyLesson*` types to `TodayPlan*` on both backend and frontend in
lockstep).

## Phase I4 closing summary — all 3 passes

Phase I4 renamed SpeakPath/LinguaCoach's internal H-track implementation vocabulary
(`LearnItem`/`ActivityDefinition`/`ModuleDefinition`/"Daily Lesson") into product-friendly language
(`Lesson`/`Exercise`/`Module`/"Today Plan"), decided in
`docs/architecture/product-language-renaming-i4.md` on 2026-07-10 right after Phase I2 made
bank-first Lesson/Exercise/Module the sole content-delivery model with no legacy fallback standing
behind these names anymore. Delivered as 3 independently-verified, sequential commits:

- **Pass 1 (backend, `4f58d539`)** — `LearnItem`→Lesson, `ActivityDefinition`→Exercise,
  `ModuleDefinition`→Module across every Domain entity, Application contract, Infrastructure
  service, API controller/route, EF configuration, and one lossless rename migration. File and
  folder names renamed to match symbol names throughout. 3,424/3,424 backend tests passing (exact
  baseline match — pure rename, zero logic change).
- **Pass 2 (frontend, `12c85c8b`)** — the matching Angular slice: admin
  component/service/model/route renames, resolved the pre-existing `/admin/lessons` route
  collision (the unrelated "Today Delivery Health" diagnostics page was relocated to
  `admin-today-delivery-health/`, freeing the route for the renamed Lesson-library page at
  `/admin/lesson-library`), nav label updates, and the H10 launch-bridge DTO field renames that
  the initial scope survey underestimated the size of. Production build clean.
- **Pass 3 (this pass)** — the final slice: "Daily Lesson"→"Today Plan" across the H6 bank-first
  daily-selection pipeline (`IDailyLessonModuleSelectionService` and its whole
  `DailyLessonModules/` namespace on both Application and Infrastructure, the admin diagnostics
  controller and its routes, the `StudentDailyModuleAssignment` bookkeeping entity/table, the
  `TodaysSessionResult.ModuleSection`→`.TodayPlan` field, and the student-facing dashboard card's
  visible copy). One more lossless rename migration.

Across all three passes: every backend layer (Domain, Application, Infrastructure, Persistence,
Api), every file/folder name matching its symbol name, every API route, and every frontend
page/component/service/model/nav-label that referenced the old vocabulary was updated — confirmed
via repeated final grep sweeps at the end of each pass. The composition model in the new language
is now consistent top-to-bottom: a **Module** contains a **Lesson** + an **Exercise** + Feedback; a
**Today Plan** contains several **Modules**. `LearningActivity`/`ActivityAttempt`/`LearningModule`
(the actual runtime attempt/scoring engine, a different and older concept),
`ExercisePatternDefinition`/`ExerciseTypeDefinition` (the legacy pattern-catalog, also different),
and `StudentPracticeGymModuleAssignment`/`IPracticeGymModuleSelectionService` (H7's Practice Gym
equivalent, explicitly a sibling concept and not part of this rename) were confirmed untouched at
every pass boundary.

## Risks / unresolved questions

- None blocking. The rename is mechanical and behavior-preserving; the one test failure encountered
  mid-pass (`SessionEndpointTests`) was a stale JSON-property assertion, not a logic regression, and
  was caught and fixed by the standard build/test iteration loop rather than shipped undetected.
- Like Pass 1's migration, this pass's migration has not been run against a live database in this
  session (no local Postgres running) — verified correctness via `dotnet ef migrations script`,
  which compiles the exact SQL without requiring a live DB connection. Recommend a live-DB sanity
  check before merging/deploying, consistent with Pass 1's own recommendation.
- `TODO-H10-2` (wiring a Today module-card Start action using
  `ExerciseLaunchSource.TodayPlan`/`AdminPreview`) remains open and unaffected by this rename — its
  file-path references in `TODOS.md` predate Pass 1 and were already somewhat stale before this
  pass; not rewritten here since it's outside this pass's explicit scope (documentation of an
  already-open, unrelated future task, not a residual of this rename).

## Final verdict

Complete and verified. Backend build clean, EF migration confirmed lossless via
`dotnet ef migrations script`, frontend production build clean (only the pre-existing bundle-size
budget warning), and a final grep sweep confirms zero remaining `DailyLesson`/`daily-lesson`
references outside the historical H6 migration file, this pass's own migration's identity, and two
intentional "renamed from" doc-comment breadcrumbs. `StudentPracticeGymModuleAssignment`/
`IPracticeGymModuleSelectionService` (H7) confirmed completely untouched. This closes Phase I4.

## Next recommended action

Phase I4 is fully complete. Per the roadmap, remaining I-track work is language/coverage, not
structure: I5 (expand bank-first coverage beyond vocab/grammar `gap_fill`/`multiple_choice_single`
to the other ~31 exercise types) and I6 (real AI-driven generation) are the next candidates.
