# Staged Activity Test Suite Fix

Date: 2026-06-15
Related sprint: docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md

## Background

After PR1 (staged `module_stage_v1` content for Listening), plus the
Writing staged migration, ExerciseTypeDefinition catalog, exercise type
registry/resolver, `/api/activity/exercise-types/select`, and dynamic
Practice Gym skill selection, 20 backend integration tests, 4 Angular unit
tests, and several Playwright e2e tests were failing or testing obsolete
behaviour. This pass made the suites runnable and meaningful again without
adding new product features.

## Files reviewed

- tests/LinguaCoach.IntegrationTests/Api/ExerciseTypeRegistryTests.cs
- tests/LinguaCoach.IntegrationTests/Api/PracticeGymExerciseTypeSelectionEndpointTests.cs
- tests/LinguaCoach.IntegrationTests/Api/PatternKeyedActivityEndpointTests.cs
- tests/LinguaCoach.IntegrationTests/Api/ListeningComprehensionActivityTests.cs
- tests/LinguaCoach.IntegrationTests/Api/LearningHistoryTests.cs
- tests/LinguaCoach.IntegrationTests/Api/LearningPathProgressionTests.cs
- tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs
- src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs
- src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs
- src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs
- src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs
- src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts
- src/LinguaCoach.Web/e2e/listening-comprehension-activity.spec.ts
- src/LinguaCoach.Web/e2e/disabled-actions-cleanup.spec.ts
- src/LinguaCoach.Web/e2e/onboarding-post-placement-dashboard.spec.ts
- src/LinguaCoach.Web/e2e/speaking-role-play-activity.spec.ts
- src/LinguaCoach.Web/e2e/student-nav-structure.spec.ts

## Findings and decisions, by priority

### High — real app bugs fixed

1. **`listening_comprehension` (Legacy) was marked `SupportsPracticeGym = true`**,
   so the Practice Gym selection registry could surface the legacy,
   non-staged activity type ahead of the new ready pattern-based listening
   exercise types (`listen_and_answer`, `listen_and_gap_fill`).
   Fixed by setting `SupportsPracticeGym = false` for the Legacy
   `listening_comprehension` row in `ExerciseTypeDefinitionSeeder`.
   Decision: the Legacy listening type remains `IsEnabled = true` and
   `Ready` (existing activities/history still load) but is no longer
   eligible for new Practice Gym selection.

2. **`activity_generate_writing` prompt seed had `maxInputTokens: 900`**, but
   the rewritten staged (`module_stage_v1`) prompt template now estimates
   ~918 tokens, so every WritingScenario generation request threw
   `TokenBudgetExceededException`, converted to a 503
   `AiServiceUnavailableException`. This affected both `?type=WritingScenario`
   and pattern-keyed Writing requests (email_reply, teams_chat_simulation,
   etc.) wherever `EnsureExerciseTypeAvailableAsync`/generation ran.
   Fixed by raising `maxInputTokens` to 1100 in
   `DefaultAiSeeder.SeedAiPromptsAsync` for `activity_generate_writing`.

### Medium — test fixture / isolation bugs fixed

3. `FakeAiProvider` in `ActivityTestFactory.cs` returned a single fixed
   `module_stage_v1` JSON shaped for listening (`practiceContent.exerciseData`
   had `audioScript`/`questions` but not `prompt`/`situation`/`audience`/`tone`).
   `ModuleStageContentValidator.RequiredPracticeKeysByType` for
   `WritingScenario` requires `prompt, situation, audience, tone` inside
   `exerciseData`, so every staged WritingScenario generation failed
   validation (after retry) with `AiResponseValidationException` → 503.
   Fixed by adding those four fields to the shared fake `exerciseData`
   object. This unblocked 7 of the 20 originally-failing integration tests
   (`PatternKeyedActivityEndpointTests`, `LearningHistoryTests`,
   `VocabularyPracticeActivityTests`, `LearningPathEndpointTests`,
   `ActivityStructuredFeedbackTests`, etc., which were all downstream of
   503s on writing-activity generation).

4. `PracticeGymExerciseTypeSelectionEndpointTests.Select_WhenAllMatchingRowsDisabled_ReturnsSafeNoResult`
   permanently disabled all `listening` `ExerciseTypeDefinition` rows in the
   shared `ActivityTestFactory` database and never re-enabled them, causing
   the later `Select_DoesNotSelectPlannedPteRows` test (and any other
   listening-skill test running after it) to see no ready listening types.
   Fixed by wrapping the disable in `try/finally` and re-enabling the rows
   afterward.

5. `LearningPathProgressionTests.GetCurrentFocusArea_WithToneChangesInFeedback_ReturnsToneCategory`
   used `db.LearningActivities.First()` assuming a prior test in the shared
   fixture had already inserted a `LearningActivity` row. With test
   execution order changes this row no longer reliably existed, causing
   `InvalidOperationException: Sequence contains no elements`.
   Fixed by seeding a minimal `WritingScenario` `LearningActivity` row
   (`FirstOrDefault()` fallback) directly in the test.

### Frontend unit tests fixed

6. `practice-gym.component.spec.ts` (4 tests) used a `jasmine.createSpyObj`
   for `Router` with no `events` observable, but the component template now
   renders `routerLink` directives (`RouterLink` constructor reads
   `router.events.subscribe(...)`), throwing
   `TypeError: Cannot read properties of undefined (reading 'subscribe')`.
   Fixed by using `provideRouter([])` for a real `Router` instance and
   `spyOn(router, 'navigate')` instead of a full spy object.

### Playwright e2e — obsolete expectations updated or removed

7. `listening-comprehension-activity.spec.ts` — the Practice page no longer
   shows the old Learn-page copy "Transcript unlocks after you answer."; the
   staged Practice page now shows "The transcript stays hidden until you
   submit." Updated the assertion to the current copy. Learn/Practice split
   behaviour (Learn shows teaching only, Practice shows audio/questions) was
   already correct and is preserved.

8. `disabled-actions-cleanup.spec.ts` — two tests
   (`practice gym enables implemented practice cards and only marks future
   skills as coming soon`, `practice gym listening card links to the
   listening module`) asserted `href="/module/gym-listening"` /
   `/module/gym-writing` / `/module/gym-speaking` on skill cards. Skill cards
   are now `<button>` elements that call `selectSkill()` for dynamic
   Practice Gym selection (`exerciseType=` routing), not static anchors —
   this is the intended new architecture per
   `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md`.
   **Decision: removed the href assertions.** The dynamic-selection routing
   behaviour (click → `/api/activity/exercise-types/select` → navigate with
   `exerciseType=`) is already covered by `practice-gym.spec.ts`
   ("Listening/Writing/Speaking skill card selects an exerciseType and opens
   activity"). Kept a slimmed "coming soon" test
   (`practice gym marks future skills as coming soon`) which is still valid,
   and added an `/api/activity/exercise-types` mock so the dynamic cards
   render in their enabled state.

9. `onboarding-post-placement-dashboard.spec.ts` —
   "Practice Gym page has skill cards that route to implemented activities
   and pronunciation is disabled" had the same obsolete href assertions.
   Renamed to "...has skill cards for implemented skills and pronunciation is
   disabled", added the `/api/activity/exercise-types` mock (writing,
   listening, speaking ready; no PTE pronunciation type), and changed
   assertions to "not Coming soon" for writing/listening/speaking and
   "Coming soon" + no link for the AI role-play (PTE) placeholder card.

10. `speaking-role-play-activity.spec.ts` — "Practice Gym Speaking card is
    active and links to SpeakingRolePlay" asserted the speaking card is not
    `aria-disabled` and not "Coming soon", but without an
    `/api/activity/exercise-types` mock the catalog call returns nothing and
    the card renders disabled. Added the same exercise-types mock with a
    ready `speaking_roleplay_turn` entry.

11. `student-nav-structure.spec.ts` — "/practice loads Practice Gym page and
    does not auto-start an activity" routed `**/api/activity**`, which now
    also matches the legitimate `/api/activity/exercise-types` catalog call
    made on page load (not an auto-started activity). Narrowed the
    "must not be called" check to `**/api/activity/next**` and added an
    exercise-types mock returning `[]` so the page still renders.

## No tests deleted outright

All previously-failing tests were fixed or had their assertions updated to
match the current `module_stage_v1` / catalog-driven architecture. Two test
bodies in `disabled-actions-cleanup.spec.ts` were collapsed into one
(`practice gym marks future skills as coming soon`) because their only
remaining valid assertions overlapped; the removed href assertions have a
direct, already-passing equivalent in `practice-gym.spec.ts`.

## Verification

- `dotnet build` — 0 errors (pre-existing warnings only).
- `dotnet test tests/LinguaCoach.UnitTests` — 505/505 passed.
- `dotnet test tests/LinguaCoach.IntegrationTests` — 462/462 passed (was 442/462).
- `npx ng test --watch=false --browsers=ChromeHeadless` — 91/91 passed (was 87/91).
- `npx ng build --configuration=production` — succeeded, no Google Fonts
  inlining issue encountered in this environment.
- `npx playwright test` (full suite) — 173/173 passed (was 170/173).

## Remaining environment limitations

None observed. .NET 10 SDK, Chrome (Karma), and Playwright Chromium were all
available in this environment, and the production Angular build completed
without network-related font-inlining failures.

## Confirmation

- No new product features were added; all changes are test fixtures, test
  expectations, two seed-data values (`SupportsPracticeGym` flag,
  `maxInputTokens` budget), and one e2e copy-string assertion update.
- gstack was not used (not required for this test-fix task; this run is the
  non-interactive Codex/CLI workflow).

## Next recommended action

Proceed with the remaining staged-content migration backlog items in
`docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md`
(WritingScenario follow-ups, pattern types, Practice Gym pre-generation
pool, etc.) — the test suite is now green and can be used as a regression
gate for those changes.
