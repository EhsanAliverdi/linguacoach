---
status: complete
lastUpdated: 2026-06-10
owner: product
supersedes:
supersededBy:
---

# Sprint: Today's Lesson / Learning Session

**Date:** 2026-06-10
**Sprint name:** Today's Lesson / Learning Session
**Preceding sprint:** Admin UX / Student Management / AI Config Cleanup (complete)
**Product goal:** SpeakPath must feel like a structured AI-powered English class, not a random activity launcher.

---

## Context

All four activity types are implemented. Placement Assessment is complete. The architecture for `LearningSession` → `SessionExercise` → `LearningActivity` → `ActivityAttempt` is fully designed (see `docs/architecture/course-session-learning-model.md`). This sprint makes that architecture real.

The student currently arrives at a lifecycle-aware dashboard with no structured lesson to start. This sprint adds the `LearningSession` layer and replaces the dashboard's activity-card surface with a guided "Today's Lesson" card.

---

## What a LearningSession Is

A `LearningSession` is one complete English class — equivalent to one lesson.

It is **ordered**, **time-bounded**, and **sequenced** using communicative language teaching principles (warm-up → input → language focus → controlled practice → productive task → reflection).

It is **not** a collection of independently selected activities. The backend determines the structure; AI only generates content within each step.

A session belongs to a `LearningModule`, which belongs to a `LearningPath`.

A student has exactly one "today's session" at any given time — the next `NotStarted` session in their current module (or the `InProgress` one if they left mid-lesson).

---

## Database: New Tables Required

Two new tables are needed. They are additive (no breaking changes to existing tables).

### `learning_sessions`

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| learning_module_id | uuid FK | → `learning_modules` |
| title | text | e.g. "Explaining a Delay Professionally" |
| topic | text | e.g. "Professional delay communication" |
| duration_minutes | int | 10 / 15 / 20 / 30 |
| focus_skill | text | Listening / Writing / Speaking / Vocabulary |
| secondary_skills_json | text | JSON array |
| session_goal | text | One-sentence student-facing goal |
| order | int | Within module |
| status | text | NotStarted / InProgress / Completed |
| started_at_utc | timestamptz? | |
| completed_at_utc | timestamptz? | |
| generated_from_memory_snapshot_json | text? | Compact memory state used at generation time |
| created_at_utc | timestamptz | |

### `session_exercises`

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| learning_session_id | uuid FK | → `learning_sessions` |
| order | int | Step number within lesson |
| exercise_pattern_key | text | e.g. "listen_and_gap_fill" |
| primary_skill | text | |
| secondary_skills_json | text | JSON array |
| estimated_minutes | int | |
| instructions | text | Student-facing instructions |
| learning_activity_id | uuid? | Null until activity is generated for this step |
| status | text | NotStarted / InProgress / Completed / Skipped |
| completed_at_utc | timestamptz? | |

**Why not compose from existing records?**

`LearningActivity` records have no ordering, no session grouping, no teaching sequence, and no session-level goal. Composing a "lesson" from loose activity records would require retrofitting ordering and grouping concepts onto a table not designed for them. The two new tables are small, clean, and additive. This is the right call.

---

## Student UX Flow

### 1. Dashboard → Today's Lesson card

After placement, the student's dashboard shows a prominent **Today's Lesson** card:

```
┌─────────────────────────────────────────────────────┐
│  TODAY'S LESSON                                     │
│  Explaining a Delay Professionally        15 min    │
│  Focus: Writing · Vocabulary                        │
│  ── ── ── ── ── (5 steps)                           │
│                                                     │
│              [ Start Lesson → ]                     │
└─────────────────────────────────────────────────────┘
```

If a session is already `InProgress`, the button reads **Continue Lesson →**.

If today's session is complete, the card shows a completion state and a preview of the next session.

Secondary placement/activity content is below the fold or in a separate tab.

### 2. Start / Resume Lesson

Clicking the card navigates to `/lesson/:sessionId`.

Backend ensures the session record exists (generates on-demand if not yet created for the current module position).

### 3. Step-by-Step Lesson Page (`/lesson/:sessionId`)

```
┌── Header ──────────────────────────────────────────┐
│  Explaining a Delay Professionally                  │
│  ●●●○○  Step 2 of 5                    12 min left  │
└─────────────────────────────────────────────────────┘

[ Step content: current SessionExercise rendered as activity ]

[ ← Back ]                            [ Next Step → ]
```

Each `SessionExercise` renders into the existing activity component for its `ActivityType`. When a step has no `LearningActivityId` yet, the backend generates the activity on demand before rendering.

Micro-lesson steps (no submission required) show read-only content with a **Got it →** button.

Progress dots show completed / current / remaining steps.

### 4. Completion Summary

After the last step, the student sees a lesson summary:

```
┌────────────────────────────────────────────────────┐
│  🎉  Lesson Complete!                               │
│  You practised: Writing, Vocabulary, Listening      │
│  AI Coach note: "Your tone in the delay email was   │
│  professional. Focus next on sentence variety."     │
│                                                     │
│  Next lesson: Responding to a Complaint (15 min)    │
│                            [ See Today → ]          │
└────────────────────────────────────────────────────┘
```

The summary is generated by AI using the session's `ActivityAttempt` feedback items.

---

## Backend Endpoints

### Session Management

| Method | Path | Description |
|---|---|---|
| GET | `/api/sessions/today` | Returns today's session for the current student (creates if needed) |
| GET | `/api/sessions/{sessionId}` | Returns session detail with ordered `SessionExercise` list |
| POST | `/api/sessions/{sessionId}/start` | Sets status → `InProgress`, records `StartedAtUtc` |
| POST | `/api/sessions/{sessionId}/complete` | Sets status → `Completed`, records `CompletedAtUtc`, triggers reflection |
| GET | `/api/sessions/{sessionId}/reflection` | Returns AI-generated session summary (generated once, cached on session) |

### Exercise / Step Management

| Method | Path | Description |
|---|---|---|
| GET | `/api/sessions/{sessionId}/exercises/{exerciseId}/activity` | Returns (or generates) the `LearningActivity` for this step |
| POST | `/api/sessions/{sessionId}/exercises/{exerciseId}/complete` | Marks exercise complete; advances session progress |

### Session Generation (internal / admin-triggerable)

| Method | Path | Description |
|---|---|---|
| POST | `/api/sessions/generate` | Generates the next session for a student (admin or background trigger) |

---

## Frontend Pages / Components

### New pages

| Route | Component | Description |
|---|---|---|
| `/lesson/:sessionId` | `LessonPage` | Main step-by-step lesson experience |
| `/lesson/:sessionId/complete` | `LessonCompletePage` | Completion summary |

### New components

| Component | Location | Description |
|---|---|---|
| `TodaysLessonCard` | dashboard | Card showing today's session, start/continue CTA |
| `LessonProgressBar` | lesson page header | Step dots + time remaining |
| `MicroLessonStep` | lesson step | Read-only teaching content, "Got it" button |
| `LessonReflectionSummary` | completion page | AI coach note + next session preview |

### Modified components

| Component | Change |
|---|---|
| `DashboardComponent` | Add `TodaysLessonCard` above activity grid; hide grid if session-ready student |
| `ActivityShellComponent` | Accept optional `sessionId` + `exerciseId` context; post completion to exercise endpoint |

---

## How It Uses Existing Data

### Placement result
`PlacementResult.CefrLevel` drives `LanguageDifficulty` for all AI content generation calls within sessions. It is not re-derived from self-reported data.

### Current module
The session generator looks at the student's current `LearningModule` (position in `LearningPath`) to determine topic domain and available exercise patterns. It does not skip or jump modules.

### Student memory (`UserLearningSummary` / `StudentSkillProfile`)
The generator reads:
- `WeakSkills` → prioritise patterns that target those skills
- `CoveredScenarios` → avoid repeating same scenario type + skill + audience combinations
- `VocabularyQueue` → inject vocabulary review items into `phrase_match` or `gap_fill` steps
- `RecurringMistakes` → prefer `micro_lesson_mistake` warm-up before correction exercises

### Weak skills
Weak skills from `StudentSkillProfile` directly influence which `ExercisePattern` keys are selected and how many steps of each type are included.

### Preferred session duration
Stored in onboarding data (`UserProfile.PreferredSessionDurationMinutes`). The generator uses this to select the number of exercises and which patterns to include (see `course-session-learning-model.md` — 10 / 15 / 20 / 30 min templates).

### Workplace context
`ProfessionalExperienceLevel` and `RoleFamiliarity` are passed to every AI content generation call as `{{DomainComplexity}}` and `{{WorkplaceSeniority}}`. The generator enforces the domain complexity cap: no scenario may exceed the student's seniority by more than one level without a preceding `micro_lesson_*` step.

---

## MVP Scope

### In scope

- `LearningSession` and `SessionExercise` DB tables and EF entities
- EF migration
- Session generator: deterministic exercise selection from `ExercisePattern` keys based on student profile
- `GET /api/sessions/today` — returns or creates today's session
- `GET /api/sessions/{id}` — returns session with exercises
- `POST /api/sessions/{id}/start` and `complete`
- `GET /api/sessions/{id}/exercises/{eid}/activity` — generates activity on demand
- `POST /api/sessions/{id}/exercises/{eid}/complete`
- `GET /api/sessions/{id}/reflection` — AI-generated lesson summary
- `TodaysLessonCard` on dashboard
- `LessonPage` step-by-step UI
- `LessonCompletePage` with AI reflection
- `MicroLessonStep` component (read-only, no submission)
- Session progress tracking persisted to DB
- Lifecycle stage transitions: `CourseReady` → `InLesson` → `ActiveLearning`

### Out of scope (do not build in this sprint)

- Call Mode
- Pronunciation scoring
- Advanced TTS voices (OpenAI TTS / multi-voice)
- Configurable onboarding flows
- Full gamification (streaks, badges, XP)
- Mobile app
- Weekly plan calendar view (Today only, not full week schedule)
- Exercise pattern engine UI (patterns are hardcoded in generator for MVP)
- Practice Gym separation (it continues to work as-is)
- Admin session editing / curriculum management

---

## Implementation Phases

### Phase 1 — Data layer (1–2 days)

1. Add `LearningSession` and `SessionExercise` entities to Domain
2. Add EF configuration and `DbContext` registration
3. Write and apply EF migration
4. Add `ILearningSessionRepository` and EF implementation
5. Unit tests: entity creation, status transitions

### Phase 2 — Session generator (complete — 2026-06-10)

1. ✅ `ExerciseKind` enum added to Domain (`VocabularyWarmup`, `ContextInput`, `ListeningInput`, `ReadingInput`, `WritingTask`, `SpeakingTask`, `Review`)
2. ✅ `ISessionGeneratorService` interface + DTOs added to `LinguaCoach.Application/Sessions/`
3. ✅ `SessionDurationTemplates` static data: 10/15/20/30-minute templates with correct step sequences
4. ✅ `SessionGeneratorService` implemented in `LinguaCoach.Infrastructure/Sessions/`
   - Returns today's existing session if one already exists (idempotent)
   - Selects template by `PreferredSessionDurationMinutes`
   - Applies weak-skill substitution (Speaking → promotes SpeakingTask; Listening → ensures ListeningInput)
   - Resolves current module from active `LearningPath` using completed-session count (5 sessions per module threshold)
   - Captures memory snapshot JSON at generation time
   - No AI involvement in step selection
5. ✅ Registered `ISessionGeneratorService` → `SessionGeneratorService` in `DependencyInjection`
6. ✅ Tests: 37 unit tests (template structure) + 28 integration tests (full service against SQLite) = 65 new tests

### Phase 3 — Backend endpoints (complete — 2026-06-10)

1. ✅ `SessionsController` added at `src/LinguaCoach.Api/Controllers/SessionsController.cs`
   - `GET /api/sessions/today` — idempotent, creates today's session if none exists
   - `GET /api/sessions/{id}` — session detail with ordered exercises
   - `POST /api/sessions/{id}/start` — CourseReady → InLesson lifecycle transition (idempotent)
   - `POST /api/sessions/{id}/complete` — InLesson → ActiveLearning lifecycle transition (idempotent)
   - `POST /api/sessions/{id}/exercises/{eid}/complete` — marks exercise done, returns SessionComplete flag
   - `GET /api/sessions/{id}/reflection` — 501 stub (deferred to Phase 4; requires AI reflection prompt)
2. ✅ `SessionQueryHandler` in `LinguaCoach.Infrastructure/Sessions/` — implements `IGetTodaysSessionHandler`, `IGetSessionHandler`
3. ✅ `SessionLifecycleHandler` in `LinguaCoach.Infrastructure/Sessions/` — implements `IStartSessionHandler`, `ICompleteSessionHandler`, `ICompleteExerciseHandler`
4. ✅ All 5 handler interfaces registered in `DependencyInjection.cs` with correct factory pattern (shared concrete instance per scope)
5. ✅ 27 integration tests added to `tests/LinguaCoach.IntegrationTests/Api/SessionEndpointTests.cs`
   - Auth guard (401), session generation (idempotency, step order, first=VocabularyWarmup, last=Review)
   - Get by ID (ownership check, 403 cross-student, 404 missing)
   - Start (InProgress, lifecycle transition, idempotency)
   - Complete (Completed, ActiveLearning lifecycle, idempotency)
   - CompleteExercise (partial, all complete → SessionComplete=true, idempotency, missing=400)
   - Reflection (501)
6. All 629 tests pass (328 unit + 301 integration)

### Phase 4 — Frontend: Today card + Lesson page (complete — 2026-06-10)

1. ✅ Today's Lesson card added to dashboard (`DashboardComponent`) — visible for `CourseReady`, `InLesson`, `ActiveLearning` lifecycle stages
   - Calls `GET /api/sessions/today` on load
   - Shows title, duration, skill focus, step count
   - Dynamic button: "Start today's lesson" / "Resume lesson" / "Review today's lesson"
   - Status badge: Not started / In progress / Completed
   - Practice Gym remains visible and secondary
2. ✅ `LessonComponent` created at `src/app/features/lesson/lesson.component.ts`
   - Route: `/lesson/:sessionId` (guarded by `placementRequiredRedirectGuard`)
   - Loads `GET /api/sessions/{id}`, shows: title, goal, status, duration, progress bar
   - Ordered exercise list — first selected automatically (first incomplete)
   - Per-exercise expanded panel with instructions + placeholder state for activities not yet generated
   - "Mark step complete" → `POST /api/sessions/{id}/exercises/{eid}/complete`
   - Auto-advances to next incomplete step after completion
3. ✅ `SessionService` created at `src/app/core/services/session.service.ts`
4. ✅ Session TypeScript models at `src/app/core/models/session.models.ts`
5. ✅ Completion flow: "Complete lesson" button appears when all steps done; calls `POST /api/sessions/{id}/complete`; shows completion summary with back-to-dashboard link
6. ✅ 14 Playwright e2e tests in `e2e/today-lesson.spec.ts` — all pass
   - Dashboard Today's Lesson card visibility, status badges, button labels for all 3 states
   - Navigation to lesson page
   - Lesson page: title, exercise list, step order, Start button
   - Start/resume flow, exercise completion, session completion summary
7. ✅ `npm run build` passes (0 errors, pre-existing warnings only)
8. ✅ 81/81 Playwright tests pass (no regressions)

**Out of scope (deferred):**
- Activity generation per exercise step (next phase — requires `GET /api/activity/next` context wiring)
- Session reflection (`GET /api/sessions/{id}/reflection` returns 501 stub)
- `ActivityShellComponent` refactor to accept sessionId/exerciseId context

### Phase 5A — Backend: wire SessionExercise to LearningActivity generation (complete — 2026-06-10)

1. ✅ `POST /api/sessions/{sessionId}/exercises/{exerciseId}/prepare` endpoint added to `SessionsController`
2. ✅ `PrepareExerciseCommand`, `PrepareExerciseResult`, `IPrepareExerciseHandler` added to `SessionHandlers.cs`
3. ✅ `ExercisePrepareHandler` created at `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs`
   - Ownership check: session module on student's active path
   - Idempotency: if `LearningActivityId` already set, returns existing activity without re-generating
   - ExerciseKind → ActivityType deterministic mapping (see table below)
   - VocabularyPractice: no AI generation (not supported by `IAiActivityGenerator`); creates SystemFallback placeholder
   - Review step: creates lightweight reflection placeholder (no AI call), returns `IsReview=true`
   - WritingScenario / ListeningComprehension / SpeakingRolePlay: full AI generation via `IAiActivityGenerator`
   - General `NotSupportedException` fallback for future activity types not yet in AI generator
4. ✅ `IPrepareExerciseHandler` registered in `DependencyInjection.cs`
5. ✅ 16 tests added in `tests/LinguaCoach.IntegrationTests/Api/ExercisePrepareEndpointTests.cs`
   - Auth guard (401), create + assign activity, FK set on exercise row
   - Idempotency (same exercise twice → same activityId), no duplicate activity rows
   - Wrong student (403), wrong exercise (400)
   - Review step: `isReview=true`, valid activityId, idempotent
   - Unit tests: ExerciseKind → ActivityType mapping for all 6 non-Review kinds + Review throws
6. ✅ 645 tests pass (328 unit + 317 integration)

**ExerciseKind → ActivityType mapping:**

| ExerciseKind | ActivityType |
|---|---|
| VocabularyWarmup | VocabularyPractice |
| ContextInput | WritingScenario |
| ListeningInput | ListeningComprehension |
| ReadingInput | ReadingTask |
| WritingTask | WritingScenario |
| SpeakingTask | SpeakingRolePlay |
| Review | (no ActivityType — reflection placeholder) |

**Out of scope for Phase 5A (deferred):**
- Frontend: `LessonComponent` not yet wired to call `/prepare` before launching `ActivityShellComponent`
- `ActivityShellComponent` refactor to accept sessionId/exerciseId context
- Practice Gym changes
- Session reflection AI prompt
- Advanced TTS

### Phase 5B — Frontend: wire LessonPage to activity (complete — 2026-06-10)

1. ✅ `PrepareExerciseResponse` model added to `session.models.ts`
2. ✅ `prepareExercise()` added to `SessionService`
3. ✅ `getById(activityId)` added to `ActivityService`
4. ✅ `GET /api/activity/{id}` endpoint added to `ActivityController` (backed by `IGetActivityByIdHandler` on `ActivityGetHandler`)
5. ✅ `ActivityLessonComponent` updated:
   - Supports `?activityId=<id>` query param → loads specific activity via `getById()` instead of `getNext()`
   - Supports `?returnTo=<path>` → `backToDashboard()` and `nextActivity()` navigate to `returnTo` URL in lesson context
6. ✅ `LessonComponent` updated:
   - `prepareIfNeeded()` — calls `/prepare` when exercise panel opens if no activityId set and kind ≠ review
   - Auto-prepares the first active exercise on page load
   - `resolvedActivityId()` — returns server value or local prepare result
   - `localActivityIds` signal — stores prepare results in-memory for current session without refresh
   - `activityUrl()` — builds `/activity?activityId=<id>&returnTo=/lesson/<sessionId>`
   - Review step: shows reflection panel + "Mark complete" only (no /prepare call, no Open activity button)
   - Non-review with activityId: shows "Open activity" (link) + "Mark complete" (ghost button)
   - Non-review preparing: shows loading pulse while /prepare is in-flight
   - Fallback: "Mark step complete" if prepare fails
7. ✅ Server-assigned `learningActivityId` (from page refresh) shows Open activity without re-calling /prepare
8. ✅ 8 new Playwright e2e tests in `e2e/lesson-activity-wiring.spec.ts` — all pass
   - /prepare called on load for first active exercise
   - Open activity button appears with correct href (activityId + returnTo)
   - Review step shows review panel, not Open activity
   - Review step does not call /prepare
   - Refresh preserves activityId (server-set = no re-prepare)
   - Marking review complete triggers session complete summary
   - Exercise with activity shows both Open + Mark complete buttons
   - Marking complete after activity advances to next exercise
9. ✅ 90/90 Playwright tests pass (no regressions); 645 dotnet tests pass

**Out of scope (deferred):**
- Session reflection AI prompt (`GET /api/sessions/{id}/reflection` still 501)
- `ActivityShellComponent` embedded inline in lesson page
- Practice Gym changes

---

## Files Likely to Change

### Backend — new files
- `src/LinguaCoach.Domain/Entities/LearningSession.cs`
- `src/LinguaCoach.Domain/Entities/SessionExercise.cs`
- `src/LinguaCoach.Domain/Enums/SessionStatus.cs`
- `src/LinguaCoach.Domain/Enums/ExerciseStatus.cs`
- `src/LinguaCoach.Application/Services/SessionGeneratorService.cs`
- `src/LinguaCoach.Application/Services/ISessionGeneratorService.cs`
- `src/LinguaCoach.Infrastructure/Repositories/LearningSessionRepository.cs`
- `src/LinguaCoach.Api/Controllers/SessionsController.cs`
- `src/LinguaCoach.Api/Controllers/SessionExercisesController.cs`
- `Migrations/` — new migration file

### Backend — modified files
- `src/LinguaCoach.Infrastructure/Data/AppDbContext.cs` — add DbSets
- `src/LinguaCoach.Domain/Enums/StudentLifecycleStage.cs` — confirm `InLesson` is present
- `src/LinguaCoach.Application/Services/ActivityGenerationService.cs` — accept optional exercise context

### Frontend — new files
- `src/app/lesson/lesson.page.ts` + `.html`
- `src/app/lesson/lesson-complete.page.ts` + `.html`
- `src/app/shared/components/todays-lesson-card/`
- `src/app/shared/components/lesson-progress-bar/`
- `src/app/shared/components/micro-lesson-step/`
- `src/app/shared/components/lesson-reflection-summary/`

### Frontend — modified files
- `src/app/dashboard/dashboard.component.ts` + `.html`
- `src/app/activity/activity-shell.component.ts`
- `src/app/app.routes.ts` — add `/lesson/:sessionId` and `/lesson/:sessionId/complete`

### Docs
- `docs/architecture/README.md` — update implementation state table
- `docs/handoffs/current-product-state.md` — update what is built
- `docs/sprints/current-sprint.md` — update to this sprint

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Session generator produces repetitive sessions if memory data is sparse | Medium | Medium | Default to full template diversity when memory is thin; log generation decisions |
| AI reflection prompt produces generic output | Medium | Low | Inject specific attempt feedback items from the session; test prompts before shipping |
| `ActivityShellComponent` refactor breaks existing standalone activity flow (Practice Gym) | Low | High | Keep `sessionId` param optional; full Playwright regression on activity types |
| On-demand activity generation is slow (AI call on first step) | Medium | Medium | Pre-generate first exercise at session creation time; lazy-generate remaining steps |
| Lifecycle stage transition race (student opens two tabs) | Low | Low | Idempotent `start` endpoint; status transitions only forward |

---

## Test Plan

### Unit tests (xUnit)
- `SessionGeneratorService`: 10-min / 15-min / 20-min / 30-min duration templates produce correct step counts
- `SessionGeneratorService`: domain complexity cap enforces `micro_lesson_*` insertion
- `SessionGeneratorService`: weak skills produce correct pattern selection bias
- `LearningSession` status transitions (NotStarted → InProgress → Completed only, no regressions)
- `SessionExercise` status transitions

### Integration tests (xUnit + TestServer)
- `GET /api/sessions/today` returns a valid session for a `CourseReady` student
- `GET /api/sessions/today` returns the same session on repeated calls (idempotent)
- `POST /api/sessions/{id}/start` transitions status and lifecycle stage
- `GET /api/sessions/{id}/exercises/{eid}/activity` generates and returns activity
- `POST /api/sessions/{id}/exercises/{eid}/complete` advances progress
- `POST /api/sessions/{id}/complete` triggers reflection generation
- `GET /api/sessions/{id}/reflection` returns AI summary

### Playwright (E2E)
- Full happy path: CourseReady student → dashboard shows Today card → Start Lesson → complete all steps → see completion summary
- Resume path: Start lesson → close browser → return → Continue Lesson → picks up at correct step
- Micro-lesson step: renders read-only content, "Got it" advances without submission
- Dashboard regression: WritingScenario / ListeningComprehension / VocabularyPractice / SpeakingRolePlay still work from Practice Gym
- Lifecycle display: `CourseReady` shows "Start Lesson"; `ActiveLearning` shows "Continue Lesson"; completed session shows next-session preview

---

## Acceptance Criteria

- [ ] A `CourseReady` student sees a Today's Lesson card on the dashboard with a topic, duration, and Start button
- [ ] Clicking Start navigates to the step-by-step lesson page
- [ ] Progress dots update as each step is completed
- [ ] Micro-lesson steps render without a submission form; "Got it" advances
- [ ] The lesson page resumes at the correct step if the student leaves and returns
- [ ] After the final step, the student sees a completion summary with an AI coach note
- [ ] The completion summary shows a next-session preview
- [ ] All four existing activity types continue to work in Practice Gym (regression)
- [ ] `dotnet test` passes (437+ passing, zero regressions)
- [ ] `npm run build` passes
- [ ] Playwright suite passes including new lesson flow tests

---

## Documentation Impact

Docs to update when this sprint is implemented:

- `docs/architecture/README.md` — update implementation state table (LearningSession: ✅ Done)
- `docs/handoffs/current-product-state.md` — add Today page and session flow to "what is built"
- `docs/sprints/current-sprint.md` — update to this sprint

Docs that are authoritative inputs (read, not modified):
- `docs/architecture/course-session-learning-model.md`
- `docs/architecture/exercise-pattern-library.md`
- `docs/architecture/professional-experience-domain-complexity.md`
- `docs/architecture/student-learning-memory.md`

---

## Session Template Design Decision

### Phase 2 uses code-owned deterministic templates — this is intentional

`SessionDurationTemplates.cs` contains hardcoded step sequences for the 10/15/20/30-minute duration buckets. This is a deliberate MVP choice, not a shortcut to fix later in this sprint.

**Why code-owned templates are correct for MVP:**
- Templates are evaluated at generation time with zero DB reads — fast and simple
- The step sequences are stable: they reflect communicative language teaching principles that will not change week to week
- There are only four templates; managing them in a DB at this stage adds schema, migrations, and seeding complexity with no product benefit
- The generator (`SessionGeneratorService`) is already structured to accept any `IReadOnlyList<ExerciseStepTemplate>` — swapping from static data to DB-loaded records is a one-line change to `GetTemplate()` when the time comes

**What is explicitly deferred:**
- Admin UI for editing session templates
- DB-backed template records (`lesson_session_templates` / `lesson_session_template_steps` tables)
- Template versioning
- Admin preview/test mode for templates
- Custom-duration templates

These are captured in **`docs/backlog/deferred-work.md` — TODO-8: Configurable Learning Session Templates**.

**Do not build any of the above in this sprint.**

---

## Decisions Made

1. **Two new tables are required.** Composing a session from loose `LearningActivity` records was rejected — no ordering, grouping, or teaching-sequence concept exists in that layer.
2. **Session structure is backend-determined, not AI-determined.** AI only generates content within exercises. Pattern selection is deterministic code.
3. **Activity generation is on-demand per step**, with the first step pre-generated at session creation to avoid a cold-start delay.
4. **Weekly plan calendar view is deferred.** MVP shows only today's session — the weekly rhythm comes in a follow-up sprint.
5. **Practice Gym continues unchanged.** It remains the on-demand activity surface; this sprint does not merge or replace it.
