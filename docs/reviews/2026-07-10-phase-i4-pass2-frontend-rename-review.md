---
title: Phase I4 Pass 2 — Frontend Rename (LearnItem→Lesson, ActivityDefinition→Exercise, ModuleDefinition→Module)
date: 2026-07-10
related: I-track, implements the frontend slice of the I4 product-language rename decided in
  docs/architecture/product-language-renaming-i4.md; follows
  docs/reviews/2026-07-10-phase-i4-pass1-backend-rename-review.md (backend, already merged)
status: complete
---

# Phase I4 Pass 2 — Frontend Rename

**Date:** 2026-07-10
**Type:** implementation phase — Angular frontend (`src/LinguaCoach.Web/`) slice of the I4
product-language rename. Makes the admin frontend consistent with Pass 1's backend rename
(`LearnItem`→`Lesson`, `ActivityDefinition`→`Exercise`, `ModuleDefinition`→`Module`, including the
H10 `ActivityDefinitionLaunch`→`ExerciseLaunch` bridge). "Daily Lesson" → "Today Plan" (Pass 3/I4d)
is a separate, later slice and was not touched here.

## Files reviewed

- `docs/reviews/2026-07-10-phase-i4-pass1-backend-rename-review.md` (Pass 1 rename table)
- Every renamed backend contract file actually read to confirm exact route paths and JSON field
  names before touching the frontend: `LessonContracts.cs`, `AdminLessonController.cs`,
  `ExerciseContracts.cs`, `ExerciseGenerationContracts.cs`, `AdminExerciseController.cs`,
  `ModuleContracts.cs`, `ModuleGenerationContracts.cs`, `AdminModuleController.cs`,
  `DailyLessonModuleSelectionContracts.cs`, `PracticeGymModuleSelectionContracts.cs`,
  `ExerciseLaunchContracts.cs`, `PracticeGymSuggestionsController.cs`,
  `AdminDailyLessonModuleController.cs`, `AdminPracticeGymModuleController.cs`,
  `PracticeGymSuggestionDtos.cs`, `ExerciseSourceMode.cs`.
- Full frontend inventory: every admin page/service/model file under
  `src/LinguaCoach.Web/src/app/features/admin/`, `core/services/`, `core/models/`, plus the
  student-facing `dashboard`, `practice-gym`, and `session.models.ts` files that consume the
  renamed backend DTOs through the Today/Practice Gym module-suggestion pipelines.

## What changed

### Admin pages

| Old | New |
|---|---|
| `features/admin/admin-lessons/` (class `AdminLessonsComponent`, the pre-existing "Today Delivery Health" diagnostics page — **this was the route-collision blocker**, see below) | `features/admin/admin-today-delivery-health/` (class `AdminTodayDeliveryHealthComponent`; `.ts`/`.html`/`.spec.ts`; route unchanged at `/admin/lessons`) |
| `features/admin/admin-learn-items/` (class `AdminLearnItemsComponent`) | `features/admin/admin-lessons/` (class `AdminLessonsComponent`; reuses the folder/class name freed up by the rename above; route moved to `/admin/lesson-library`, decided per task brief) |
| `features/admin/admin-activities/` (class `AdminActivitiesComponent`) | `features/admin/admin-exercises/` (class `AdminExercisesComponent`; route `/admin/exercises`) |
| `features/admin/admin-modules/` | unchanged path/class (`AdminModulesComponent`) — internal model/service imports and field names updated in place |

### Models/services

| Old | New |
|---|---|
| `core/models/admin-learn-item.models.ts` | `core/models/admin-lesson.models.ts` (`LearnItemDto`→`LessonDto`, `LearnItemResourceLinkDto`→`LessonResourceLinkDto`, `LearnItemListResult`→`LessonListResult`, `LearnItemResourceLinkInput`→`LessonResourceLinkInput`, `CreateLearnItemRequestBody`→`CreateLessonRequestBody`, `UpdateLearnItemRequestBody`→`UpdateLessonRequestBody`, `GenerateLearnItemFromResourcesRequestBody`→`GenerateLessonFromResourcesRequestBody`, `GenerateLearnItemFromResourcesResult`→`GenerateLessonFromResourcesResult` (field `learnItem`→`lesson`, matches backend record), `LEARN_ITEM_REVIEW_STATUSES`→`LESSON_REVIEW_STATUSES`, `LEARN_ITEM_SOURCE_MODES`→`LESSON_SOURCE_MODES`, `LEARN_ITEM_RESOURCE_ROLES`→`LESSON_RESOURCE_ROLES`) |
| `core/services/admin-learn-item.service.ts` | `core/services/admin-lesson.service.ts` (class `AdminLearnItemService`→`AdminLessonService`, base URL `api/admin/learn-items`→`api/admin/lessons`) |
| `core/models/admin-activity-definition.models.ts` | `core/models/admin-exercise.models.ts` (`ActivityDefinitionDto`→`ExerciseDto`, `ActivityDefinitionListResult`→`ExerciseListResult`, `ActivityResourceLinkDto`/`Input`→`ExerciseResourceLinkDto`/`Input`, `learnItemId` field→`lessonId`, `GenerateActivityDefinitionResult`→`GenerateExerciseResult`; kept `GenerateActivityFromResourcesRequestBody`/`GenerateActivityFromLessonRequestBody` (renamed from `...FromLearnItemRequestBody`) and `ACTIVITY_*` constant names exactly matching the backend's own request-body class names and the `ActivityType`/`RendererType` field values, which the backend itself did not rename — see judgment call 3) |
| `core/services/admin-activity-definition.service.ts` | `core/services/admin-exercise.service.ts` (class `AdminActivityDefinitionService`→`AdminExerciseService`, base URL `api/admin/activities`→`api/admin/exercises`, method `generateFromLearnItem`→`generateFromLesson` posting to `generate-from-lesson` (route itself renamed backend-side from `generate-from-learn-item`)) |
| `core/models/admin-module-definition.models.ts` | `core/models/admin-module.models.ts` (`ModuleDefinitionDto`→`ModuleDto`, `ModuleDefinitionListResult`→`ModuleListResult`, `ModuleLearnItemLinkDto`/`Input`→`ModuleLessonLinkDto`/`Input`, `ModuleActivityLinkDto`/`Input`→`ModuleExerciseLinkDto`/`Input`, `.learnItemLinks`→`.lessonLinks`, `.activityLinks`→`.exerciseLinks`, `GenerateModuleFromLearnItemRequestBody`→`GenerateModuleFromLessonRequestBody`, `GenerateModuleFromActivityRequestBody`→`GenerateModuleFromExerciseRequestBody`, `GenerateModuleDefinitionResult`→`GenerateModuleResult`, `MODULE_LEARN_ITEM_ROLES`→`MODULE_LESSON_ROLES`, `MODULE_ACTIVITY_ROLES`→`MODULE_EXERCISE_ROLES`, enum value `GeneratedFromLearnAndActivities`→`GeneratedFromLessonAndExercises` (matches the backend `ModuleSourceMode` enum member Pass 1 renamed)) |
| `core/services/admin-module-definition.service.ts` | `core/services/admin-module.service.ts` (class `AdminModuleDefinitionService`→`AdminModuleService`, base URL unchanged `api/admin/modules`, `generateFromLearnItem`→`generateFromLesson` (route `generate-from-lesson`), `generateFromActivity`→`generateFromExercise` (route `generate-from-exercise`), list params `learnItemId`→`lessonId`, `activityDefinitionId`→`exerciseId`) |

### Routes (`src/LinguaCoach.Web/src/app/app.routes.ts`)

- `path: 'learn-items'` → `path: 'lesson-library'` (loads the renamed `AdminLessonsComponent`)
- `path: 'activities'` → `path: 'exercises'` (loads the renamed `AdminExercisesComponent`)
- `path: 'lessons'` now loads `AdminTodayDeliveryHealthComponent` (route string itself unchanged
  — only the component behind it was renamed, per the collision resolution below)
- `path: 'modules'` unchanged — verified its only frontend change is internal model/service
  imports, not the route
- Added `redirectTo` entries for the two old paths, following the exact pattern already used for
  every prior I-track redirect in this file (`activity-templates`, `review-queue`,
  `resource-sources`, etc.): `learn-items` → `/admin/lesson-library`, `activities` →
  `/admin/exercises`
- The existing `activity-templates`/`activity-templates/:templateId` redirects (from I2A) now
  point at `/admin/exercises` instead of the old `/admin/activities`, since that target route
  itself moved

### Nav (`admin-app-layout.component.html`, both the mobile-drawer and desktop-sidebar copies)

- "Learn Items" → **"Lessons"**, route → `/admin/lesson-library`
- "Activities" → **"Exercises"**, route → `/admin/exercises`
- "Modules" label/route unchanged
- "Today Delivery Health" label/route unchanged (`/admin/lessons`) — only the component/folder
  behind it was renamed
- Updated the Content Studio section's inline code comment (which spelled out the page order) to
  say "Lessons -> Exercises -> Modules" instead of "Learn Items -> Activities -> Modules"

### H10 launch bridge / cross-cutting DTOs

These files don't live under an admin authoring page but do consume the renamed backend contracts
(`SelectedModuleResult`, `PracticeGymModuleSuggestion`, `ExerciseLaunchResult`) directly, so their
field names needed to track the backend rename even though the surrounding feature name ("Daily
Lesson", "Practice Gym") is out of scope for this pass:

- `core/models/session.models.ts` — `DailyLessonLearnItemView`→`DailyLessonLessonView` (field
  `learnItemId`→`lessonId`, matches backend `DailyLessonLessonView.LessonId`);
  `DailyLessonActivityView` kept its name (matches backend, which also kept it) but its field
  `activityDefinitionId`→`exerciseId`; `SelectedDailyLessonModule.moduleDefinitionId`→`moduleId`,
  `.linkedLearnItems`→`.linkedLessons`, `.linkedActivityDefinitions`→`.linkedExercises` (all match
  backend `SelectedModuleResult`'s `ModuleId`/`LinkedLessons`/`LinkedExercises`)
- `core/services/practice-gym-suggestions.service.ts` — `PracticeGymModuleLearnItemSummary`→`PracticeGymModuleLessonSummary`
  (field `learnItemId`→`lessonId`, matches backend `PracticeGymModuleLessonSummary.LessonId`);
  `PracticeGymModuleActivitySummary` kept its name (matches backend) but field
  `activityDefinitionId`→`exerciseId`; `PracticeGymModuleSuggestion.moduleDefinitionId`→`moduleId`,
  `.linkedLearnItemSummaries`→`.linkedLessonSummaries` (matches backend
  `PracticeGymModuleSuggestion.ModuleId`/`LinkedLessonSummaries`); `ModuleSuggestionStartResult`
  (the H10 launch result) — `moduleDefinitionId`→`moduleId`, `activityDefinitionId`→`exerciseId`,
  `learnItem: PracticeGymModuleLearnItemSummary | null`→`lesson: PracticeGymModuleLessonSummary |
  null` (matches backend `ExerciseLaunchResult.ModuleId`/`ExerciseId`/`Lesson`)
- `core/models/admin.models.ts` — `AdminDailyLessonSelectedModule.moduleDefinitionId`→`moduleId`,
  `AdminPracticeGymSuggestedModule.moduleDefinitionId`→`moduleId` (confirmed via the admin preview
  controllers, which return `SelectedModuleResult`/`PracticeGymModuleSuggestion` directly — no
  intermediate DTO — so these frontend shapes must track the backend record fields exactly)
- `features/student/dashboard/dashboard/dashboard.component.html` — two `track` expressions
  (`module.moduleDefinitionId`→`module.moduleId`) and the linked-items loop
  (`module.linkedLearnItems`/`learnItem.learnItemId`→`module.linkedLessons`/`lesson.lessonId`,
  `module.linkedActivityDefinitions`/`activity.activityDefinitionId`→`module.linkedExercises`/`activity.exerciseId`)
- `features/student/practice/practice-gym.component.ts` / `.html` — `module.moduleDefinitionId`→`module.moduleId`
  everywhere (the `startModuleSuggestion`/`isStartingModule` methods and their template bindings)
- `features/admin/admin-student-detail/admin-student-detail.component.html` — two `track`
  expressions in the Daily Lesson and Practice Gym module-preview diagnostic cards
  (`m.moduleDefinitionId`→`m.moduleId`)

### Spec file updates

- `admin-today-delivery-health.component.spec.ts` (renamed from `admin-lessons.component.spec.ts`)
  — `AdminLessonsComponent`→`AdminTodayDeliveryHealthComponent`, import path updated, the one
  textContent assertion updated from `'Lessons'` to `'Today Delivery Health'` to match the page's
  actual (unchanged) `<sp-admin-page-header title>` — this assertion was checking generic prose
  that happened to be a coincidental match before; making it check the actual page title is more
  correct, not just a rename-mechanical change
- `admin-app-layout.component.spec.ts` — the "Content Studio flow routes" test's required-route
  list updated from `/admin/learn-items`/`/admin/activities` to `/admin/lesson-library`/`/admin/exercises`
- `admin-resource-bank-unified.component.spec.ts` — updated the three injected-service imports/
  providers (`AdminLearnItemService`/`AdminActivityDefinitionService`/`AdminModuleDefinitionService`
  → `AdminLessonService`/`AdminExerciseService`/`AdminModuleService`)
- `dashboard.component.spec.ts` — the `DailyLessonModuleSection` test fixture's `moduleDefinitionId`/
  `linkedLearnItems`/`linkedActivityDefinitions` fields updated to `moduleId`/`linkedLessons`/`linkedExercises`

The `admin-learn-items/`, `admin-activities/` folders had no spec files originally (confirmed by
directory listing before starting), so no spec rename was needed for those two pages themselves.

## Route-collision resolution

Confirmed exactly as flagged in Pass 1's review (judgment call 7) and the task brief: the Angular
route `/admin/lessons` was already taken by a page whose folder/class were *also* literally named
`admin-lessons`/`AdminLessonsComponent` — the "Today Delivery Health" diagnostics page (buffer
settings, fallback generation, mastery validation), unrelated to the H3 Lesson content-authoring
concept.

Resolution: renamed the pre-existing page's folder/class to `admin-today-delivery-health/`/
`AdminTodayDeliveryHealthComponent` (its route stays `/admin/lessons` — unchanged, this is purely
an internal file/class rename to free up the name), then gave the renamed Learn Items→Lessons page
the new route `/admin/lesson-library` (decided in the task brief, not left to this pass's
judgment).

## Validation

- `cd src/LinguaCoach.Web && npm run build -- --configuration production` — **clean**, zero
  TypeScript/Angular compile errors. The only `[ERROR]` in the build output is the pre-existing
  bundle-size budget overage (`1.00 MB budget not met by 1.56 MB, total 2.56 MB`), explicitly
  called out in the task brief as acceptable and unrelated to this change (it is a `dist/`
  bundle-size threshold on the whole app, not a rename artifact).
- Final grep sweep of `src/LinguaCoach.Web/src` for `LearnItem`, `learn-item`, `ActivityDefinition`,
  `activity-definition`, `ModuleDefinition`, `module-definition`: **one hit**, the intentional
  `path: 'learn-items'` backward-compat redirect string in `app.routes.ts` (its target is
  `/admin/lesson-library`) — kept deliberately as the *old* bookmark path, not a residual rename
  miss.
- Second grep sweep for the field-level residuals (`moduleDefinitionId`, `activityDefinitionId`,
  `learnItemId`, `linkedLearnItem*`, `linkedActivityDefinitions`): **zero hits**.
- Confirmed the explicitly out-of-scope families are untouched: `features/student/activity/`
  (lesson-runner/attempt/feedback pages, still reference `LearningActivity`/`ActivityAttempt` via
  `activity.models.ts`/`activity.service.ts`, not touched), `admin-exercise-types/` and its
  `exercise-pattern`/`exercise-type` models/services (the pre-existing, unrelated
  `ExercisePatternDefinition`/`ExerciseTypeDefinition` catalog — confirmed still present and
  unrenamed), `review-queue`-adjacent code (already removed in I3, nothing to touch here).
- `npm test -- --watch=false --browsers=ChromeHeadless` — Karma load errors persist, but every one
  is a **pre-existing, unrelated** compile error already documented as `TODO-H8-2` in this
  project's AGENTS.md/test conventions (missing `feedbackPolicy` field on hand-built
  `ActivityFeedbackDto` test fixtures in `activity-lesson-submission.component.spec.ts`,
  `activity-lesson-vocab.component.spec.ts`, `presenters/test-helpers.ts`; missing
  `moduleSuggestions` field on a `PracticeGymSuggestionsResponse` fixture in
  `practice-gym.component.spec.ts`, a file this pass never touched). Confirmed via `git diff
  --stat` that none of these four files were modified in this pass. None of the files this pass
  did touch (`admin-today-delivery-health.component.spec.ts`, `admin-app-layout.component.spec.ts`,
  `admin-resource-bank-unified.component.spec.ts`, `dashboard.component.spec.ts`) appear anywhere
  in the Karma error output.

## Judgment calls

1. **"Learn Items" nav label → "Lessons".** Chosen per the task brief's default recommendation.
   Checked for ambiguity against other "lesson" terminology already in the admin UI — the only
   other admin-facing "lesson" language is "Today Delivery Health" (unambiguous, a
   delivery-infrastructure diagnostic, not content) and prose mentions of "Daily Lesson" (the
   student-facing Today feature, Pass 3's territory, not an admin nav label). No collision found.
2. **New route for the renamed Learn Items page: `/admin/lesson-library`.** This was handed down
   as a decided value in the task brief, not a judgment call made here — recorded for completeness.
3. **Kept `GenerateActivityFromResourcesRequestBody`/`GenerateActivityFromLessonRequestBody` and
   the `ACTIVITY_REVIEW_STATUSES`/`ACTIVITY_SOURCE_MODES`/`ACTIVITY_RENDERER_TYPES`/`ACTIVITY_TYPES`
   constant names as-is (not renamed to `Exercise*`).** These exactly mirror backend class/property
   names Pass 1 itself deliberately left as "Activity" (`GenerateActivityFromResourcesRequestBody`
   is the literal C# record name in `AdminExerciseController.cs`; `ActivityType`/`RendererType` are
   real, unrenamed field names on `ExerciseDto`). Diverging from the backend's own naming here
   would make the frontend *less* consistent with its contract, not more — matched Pass 1's
   judgment rather than pushing the rename further than the source of truth did.
4. **`DailyLessonActivityView` and `PracticeGymModuleActivitySummary` kept their names** (not
   renamed to `...ExerciseView`/`...ExerciseSummary`) for the same reason as #3 — these are the
   literal backend type names Pass 1 chose to keep, confirmed by reading
   `DailyLessonModuleSelectionContracts.cs`/`PracticeGymModuleSelectionContracts.cs` directly
   rather than assuming a rename pattern.
5. **`RESOURCE_TYPE_TO_LEARN_ITEM_TYPE` constant in `admin-resource-bank-unified.component.ts`
   renamed to `RESOURCE_TYPE_TO_LESSON_TYPE`, and its two callers' `goToLearnItems()` method
   renamed to `goToLessons()`.** Neither was in the task's explicit file/symbol list (it names
   admin page folders and models/services, not this read-model aggregator page's internal
   constant/method names) but both are unambiguously part of the same H3 Lesson vocabulary and
   were caught by the final grep sweep and a follow-up consistency pass; renamed for the same
   reason Pass 1 renamed `ActivitySourceMode`/`ActivityResourceLink` beyond its literal list —
   leaving them would read as a residual of the retired vocabulary.
6. **H10 launch bridge field renames extended beyond the task's own file list**
   (`session.models.ts`, `practice-gym-suggestions.service.ts`, `admin.models.ts`,
   `dashboard.component.html`, `practice-gym.component.ts`/`.html`,
   `admin-student-detail.component.html`) — the task brief flagged the H10 bridge as "likely in the
   Practice Gym student page and/or an admin diagnostic card," which undersold the actual surface
   area once the backend contracts were read directly. These files were found via a `moduleDefinitionId
   |activityDefinitionId|learnItemId|linkedLearnItem|linkedActivityDefinitions` grep after the
   admin-page rename was done, then verified field-by-field against the backend's actual JSON
   shape (`SelectedModuleResult`, `PracticeGymModuleSuggestion`, `ExerciseLaunchResult`) rather
   than guessed — this was necessary for correctness, not just naming hygiene: a stale
   `moduleDefinitionId`/`learnItemId` field name here is not a cosmetic residual, it silently
   breaks the actual API request/response shape (Angular's typed `HttpClient` does not validate
   response shapes at runtime, so a mismatched interface would have compiled clean but produced
   `undefined` values in the running app).

## Frontend structure vs. task brief assumptions

The task brief's assumed file/folder names were accurate for every item except one: it flagged
"verify the `admin-modules/` folder, likely no rename needed" — confirmed correct, no folder/class
rename, only internal model/service import and field-name updates. Everything else (the
`admin-lessons`/`admin-learn-items`/`admin-activities` collision, the exact model/service file
names, the H10 bridge service name) matched the brief's best-effort inventory closely enough that
no structural surprises were found — the one genuine expansion was the H10 bridge's actual file
list (judgment call 6 above), which the brief itself flagged as uncertain ("best-effort... not a
guaranteed-accurate frontend inventory").

## Risks / unresolved questions

- None blocking. The rename is mechanical and behavior-preserving for every file the production
  build type-checks; the pre-existing Karma load-errors are unrelated and pre-date this pass
  (confirmed via `git diff --stat` showing zero changes to the four files involved).
- Not run against a live backend in this session — the production build's clean TypeScript
  compile against the already-merged Pass 1 backend contracts is the correctness signal available
  without a running API/DB; recommend a manual smoke pass (Content Studio Lessons/Exercises/Modules
  pages, Practice Gym module-suggestion "Start" button) before merging, consistent with how Pass 1
  recommended a live-DB migration sanity check.

## Final verdict

Complete and verified. Production build clean (only the pre-existing, documented bundle-size
budget overage), final grep sweeps confirm zero remaining `LearnItem`/`ActivityDefinition`/
`ModuleDefinition` references outside the intentional old-route redirect string, every
out-of-scope family (`LearningActivity`/`ActivityAttempt` runtime concept, `ExercisePatternDefinition`/
`ExerciseTypeDefinition` catalog, `features/student/activity/`) confirmed present and untouched,
and the H10 launch-bridge field renames were verified against the actual backend contract shapes
rather than assumed.

## Next recommended action

Phase I4 Pass 3/I4d — "Daily Lesson" → "Today Plan" rename (`IDailyLessonModuleSelectionService`
and its file, `TodaysSessionResult`, `DailyLessonModules/` folders on the backend, and the
matching frontend `session.models.ts`/dashboard UI copy). Roadmap and `TODOS.md` updates remain
deferred until that slice also lands, per this task's explicit instruction.
