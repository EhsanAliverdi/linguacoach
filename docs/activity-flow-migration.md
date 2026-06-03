# Activity Flow Migration

## Status: Complete

The old `/api/writing/*` flow has been replaced by the new `/api/activity` flow.

## What was removed

### Backend
- `WritingExerciseController` (`GET /api/writing/scenarios`, `GET /api/writing/exercise/{id}`, `POST /api/writing/exercise/submit`)
- `LinguaCoach.Application.Writing` — `WritingExerciseCommand.cs` (DTOs, queries, handler interfaces)
- `LinguaCoach.Infrastructure.Writing` — `WritingExerciseGetHandler.cs`, `WritingExerciseSubmitHandler.cs`
- DI registrations for `IGetWritingScenariosHandler`, `IGetWritingExerciseHandler`, `ISubmitWritingDraftHandler`

### Angular
- `/writing` route (scenario list + exercise view)
- `WritingScenarioListComponent`, `WritingExerciseComponent`
- `writing.service.ts`, `writing.models.ts`
- "Browse scenario library" secondary link on dashboard

### Tests
- `WritingExerciseEndpointTests.cs` (11 integration tests for old `/api/writing` endpoints)
- `WritingFeedbackParserTests.cs` (unit tests for old feedback parser)

## What is now the official flow

`GET /api/activity/next` → `POST /api/activity/{id}/attempt`

- AI generates a fresh personalised scenario on each request (primary path)
- SystemFallback activities from seed data are used only if AI generation fails
- Angular route: `/activity` → `ActivityLessonComponent`
- Dashboard "Start activity" button links to `/activity`

## What remains in the database (intentionally)

| Table | Status | Reason |
|-------|--------|--------|
| `writing_scenarios` | Kept | Source data for SystemFallback `learning_activities` rows; archive |
| `writing_submissions` | Kept | Historical student submission data |
| `learning_activities` | Active | New activity store (AI-generated + SystemFallback) |
| `activity_attempts` | Active | New attempt/feedback store |

`writing_scenarios` and `writing_submissions` tables should be dropped in a future cleanup migration **after**:
1. A database backup/export has been taken
2. The product owner confirms no historical submission data is needed
3. A separate migration named `T19_DropLegacyWritingTables` is created and reviewed

## Shared test infrastructure

`ActivityTestFactory` (in `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs`) replaces the old `WritingExerciseTestFactory`. It provides:
- Fake AI provider (no real API calls in tests)
- `CreateOnboardedStudentAsync()` helper
- WritingScenario and CurriculumWordList seed data (needed for SystemFallback path)

`ActivityFallbackTestFactory` inherits from `ActivityTestFactory`.
