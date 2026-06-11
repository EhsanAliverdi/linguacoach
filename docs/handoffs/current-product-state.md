---
status: current
lastUpdated: 2026-06-11
owner: product
supersedes:
supersededBy:
---

# SpeakPath — Current Product State

Last updated: 2026-06-11

---

## What is built and verified

The following end-to-end flow is implemented and verified:

```
Admin logs in
→ Admin creates student (temp password shown once)
→ Student logs in
→ Student changes temporary password (enforced server-side)
→ Student completes onboarding (language pair, career profile, experience level)
→ Student reaches Today page (the student home/dashboard)
→ Student starts Today's Lesson or navigates to Journey, Practice, Progress, or Profile
→ Student starts an activity (Writing / Listening / Vocabulary / Speaking)
→ Student submits draft or recording
→ Student sees structured AI feedback
→ Student retries or continues to next activity
→ Student can revisit learning history
```

## Student navigation model

The student app has five top-level sections:

| Section | Route | Question answered |
|---|---|---|
| **Today** | `/dashboard` | What should I do now? |
| **Journey** | `/journey` (also `/my-path`) | Where am I in my course? |
| **Practice** | `/practice` | What can I practise freely? |
| **Progress** | `/progress` | How am I improving? |
| **Profile** | `/profile` | What are my settings? |

- The student-facing label for the home page is **Today**, not Dashboard. The route `/dashboard` is preserved.
- `/journey` and `/my-path` both load the Learning Journey page. `/my-path` is kept for backwards compatibility.
- **Practice Gym** (`/practice`) is the student-facing landing for free practice by skill or exercise type. It does not auto-start an activity on load.
- Vocabulary is accessible from Practice Gym and Progress — it is not a top-level nav item.
- Writing and Email are valid activity types within Practice Gym and lessons. The student product is not writing/email-first.

## Implemented activity types

| Type | Status |
|---|---|
| `WritingScenario` | ✅ implemented |
| `ListeningComprehension` | ✅ implemented (with TTS audio) |
| `VocabularyPractice` | ✅ implemented |
| `SpeakingRolePlay` | ✅ implemented (MVP — fake STT) |

All four activity types use the unified `/activity` path.
`/api/writing/*` endpoints have been removed. See `docs/decisions/activity-flow-migration.md`.

## Practice Gym — activated pattern cards (as of practice-gym-activation-sprint — 2026-06-10)

`GET /api/activity/next` now accepts an optional `?pattern=<key>` query parameter in addition to `?type=`.

| Practice Gym card | Route | Status |
|---|---|---|
| Vocabulary | `/vocabulary` | ✅ functional |
| Listening | `/activity?type=ListeningComprehension` | ✅ functional |
| Writing | `/activity?type=WritingScenario` | ✅ functional |
| Speaking | `/activity?type=SpeakingRolePlay` | ✅ functional (no pronunciation claim) |
| Phrase Match | `/activity?pattern=phrase_match&returnTo=/practice` | ✅ functional |
| Gap Fill | `/activity?pattern=gap_fill_workplace_phrase&returnTo=/practice` | ✅ functional |
| Email | `/activity?pattern=email_reply&returnTo=/practice` | ✅ functional |
| Workplace Chat | `/activity?pattern=teams_chat_simulation&returnTo=/practice` | ✅ functional |
| Pronunciation | — | ⏳ Coming soon (no STT/scoring support) |

All pattern-keyed activities go through `PatternEvaluationRouter`. Progress updates only after a submitted attempt. Returning from any pattern card goes back to `/practice` via `returnTo`.

## Real TTS via Admin AI Config (complete — 2026-06-11)

`ListeningAudioService` and `PlacementAudioService` now resolve TTS provider at request time via `TtsProviderResolver`:

- `AiProviderConfig` rows `tts.listening` and `tts.placement` control which TTS service runs
- Default seed: `provider=fake, model=fake, voice=fake` → silent WAV (tests never need `OPENAI_API_KEY`)
- Admin can switch to `provider=openai, model=tts-1, voice=onyx` in Admin AI Config UI
- `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; returns `audio/mpeg`; never throws
- `PlacementAudioService` checks both `.wav` and `.mp3` on disk (backward compat with pre-existing files)
- T35 migration adds nullable `voice_name varchar(100)` to `ai_provider_configs`

## Onboarding experience step (complete — 2026-06-11)

A new step-5 collects professional context before placement:

- `PATCH /api/onboarding/experience` — sets `ProfessionalExperienceLevel`, `RoleFamiliarity`, computes `WorkplaceSeniority`
- Uses `StudentProfile.SetExperienceContext()` — bypasses onboarding state machine; can be called at any stage
- Angular: `step5-experience` component inserted between step-4 and `/placement`
- Step-4 now shows "Step 4 of 5"; navigates to `/onboarding/step-5` on finish
- Non-blocking: API failure still navigates to `/placement`; "Skip for now" skips without calling API
- Existing completed students not broken — endpoint accepts any auth token regardless of onboarding state

## Test suite baseline (as of tts-placement-today-sprint — 2026-06-11)

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

## Admin capabilities

- Create students with temporary passwords
- Configure AI providers, model assignments, and prompt templates via Admin UI
- AI provider credentials stored securely in DB (never returned to client)
- AI usage logs accessible
- Student list is the admin entry point for create/edit/archive student management
- Create student returns to Students with a toast after success
- Archive uses `StudentLifecycleStage.Archived`, hides archived students by default, and disables sign-in
- AI Config shows category-level provider/model routing: Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path, Listening TTS, and Placement TTS
- Curriculum is hidden from admin navigation while its future purpose is redefined

## Placement Assessment — current state

Placement Assessment MVP is implemented:
- 6-section structured assessment (`PlacementAssessment`, `PlacementSection` entities)
- AI evaluation → `PlacementResult` as CEFR source of truth
- Listening section uses **server-side TTS audio** (`PlacementAudioService`), not browser SpeechSynthesis
- `GET /api/placement/audio/{assessmentId}/listening` streams authenticated audio
- Frontend shows native `<audio controls>` when server audio is available; graceful fallback if not
- Transcript hidden by default behind "Show transcript"

## LearningSession data layer (Phase 1 complete — 2026-06-10)

- `LearningSession` and `SessionExercise` domain entities implemented
- `SessionStatus` and `ExerciseStatus` enums added
- EF configurations and migration T32 applied (`learning_sessions`, `session_exercises` tables)
- `LinguaCoachDbContext` updated with `LearningSessions` and `SessionExercises` DbSets
- 52 new tests added (284 unit, 247 integration — 531 total)

## LearningSession generator (Phase 2 complete — 2026-06-10)

- `ExerciseKind` enum added (`VocabularyWarmup`, `ContextInput`, `ListeningInput`, `ReadingInput`, `WritingTask`, `SpeakingTask`, `Review`)
- `ISessionGeneratorService` / `SessionGeneratorService` implemented
- Duration templates: 10 min (3 steps), 15 min (4 steps), 20 min (4 steps), 30 min (5 steps)
- Weak-skill substitution: Speaking weak → SpeakingTask promoted; Listening weak → ListeningInput enforced
- Idempotent: calling twice on the same day returns the same session
- Module progression: advances to next module after 5 completed sessions
- 65 new tests added in Phase 2 (609 total: 328 unit, 281 integration)

## LearningSession backend endpoints (Phase 3 complete — 2026-06-10)

- `SessionsController` at `src/LinguaCoach.Api/Controllers/SessionsController.cs`
- Endpoints: `GET /today`, `GET /{id}`, `POST /{id}/start`, `POST /{id}/complete`, `POST /{id}/exercises/{eid}/complete`, `GET /{id}/reflection` (501 stub)
- `SessionQueryHandler` and `SessionLifecycleHandler` in `LinguaCoach.Infrastructure/Sessions/`
- Lifecycle transitions: `CourseReady` → `InLesson` (start), `InLesson` → `ActiveLearning` (complete)
- All operations idempotent; ownership verified on every request
- 27 new integration tests added in Phase 3 (629 total: 328 unit, 301 integration)

## LearningSession frontend (Phase 4 complete — 2026-06-10)

- Today's Lesson card on dashboard — visible for `CourseReady`, `InLesson`, `ActiveLearning` lifecycle stages
  - Shows title, duration, skill focus, step count, status badge
  - Button label adapts: "Start today's lesson" / "Resume lesson" / "Review today's lesson"
  - Practice Gym remains secondary but visible
- `LessonComponent` at `/lesson/:sessionId` — Angular standalone component
  - Session detail loaded from `GET /api/sessions/{id}`
  - Ordered exercise steps, progress bar, per-step panel with instructions
  - Placeholder state for steps without activity (next phase wires generation)
  - Start, complete exercise, complete lesson flows fully wired
  - Completion summary shown on lesson complete
- `SessionService` + TypeScript models added to frontend core
- 14 new Playwright e2e tests — 81/81 pass total (no regressions)

## Exercise activity wiring (Phase 5A complete — 2026-06-10)

- `POST /api/sessions/{sessionId}/exercises/{exerciseId}/prepare` endpoint added
- Idempotent: calling twice returns the same `LearningActivity`
- ExerciseKind → ActivityType deterministic mapping: VocabularyWarmup→VocabularyPractice, ContextInput→WritingScenario, ListeningInput→ListeningComprehension, ReadingInput→ReadingTask, WritingTask→WritingScenario, SpeakingTask→SpeakingRolePlay
- Review step returns a lightweight reflection placeholder (`isReview: true`), no AI generation
- VocabularyPractice and `ReadingTask` (not yet in `IAiActivityGenerator`) use `SystemFallback` placeholders
- 16 new integration + unit tests; 645 total (328 unit + 317 integration)

## Exercise activity wiring — frontend (Phase 5B complete — 2026-06-10)

- `LessonComponent` now calls `POST /api/sessions/{id}/exercises/{eid}/prepare` when student opens an exercise
- "Open activity" button navigates to `/activity?activityId=<id>&returnTo=/lesson/<sessionId>`
- `ActivityLessonComponent` supports `?activityId=<id>` (loads specific prepared activity) and `?returnTo=<path>`
- Review steps show a reflection prompt + "Mark complete" — no activity generated
- Server-assigned `learningActivityId` (persists across refresh) skips re-prepare
- `GET /api/activity/{id}` backend endpoint added
- 8 new Playwright tests; 90/90 pass

## Exercise Pattern Engine (complete — 2026-06-10)

- `exercise_patterns` table is seeded with the 8 MVP patterns.
- `LearningActivity.ExercisePatternKey` stores the durable pattern link.
- Pattern-aware prepare/generation sets `exercisePatternKey` and returns `interactionMode` on `ActivityDto`.
- Pattern-keyed activity responses include bounded `contentJson` for frontend renderers; legacy listening activities do not expose raw answer-bearing JSON before submission.
- `ActivityLessonComponent` now routes pattern-keyed activities through `ExerciseRendererComponent`.
- MVP renderers are wired: ReadOnly, FreeTextEntry, MatchingPairs, GapFill, AudioAndFreeText, AudioAndGapFill, ChatReply.
- Frontend renderer coverage added; full Playwright suite passes 97/97.
- Backend baseline: 762 tests pass (380 unit + 382 integration).
- `npm run build` passes; known non-blocking Angular warnings remain for admin CSS budgets and skipped selectors.

## Pattern Evaluation Engine (complete — 2026-06-10)

All 7 phases complete. `MarkingMode` is now first-class in the evaluation flow.

- **Evaluators**: `ExactMatchEvaluator` (gap_fill, listen_and_gap_fill), `KeyedSelectionEvaluator` (phrase_match), `NoMarkingEvaluator` (lesson_reflection), `AiStructuredEvaluator` (listen_and_answer, email_reply, teams_chat_simulation), `AiOpenEndedEvaluator` (spoken_response_from_prompt)
- **Router**: `IPatternEvaluationRouter` dispatches by `MarkingMode`; wired into `ActivitySubmitHandler`
- **Persistence**: `ActivityAttempt` stores structured `SubmittedAnswerJson`, `EvaluationResultJson`, `MaxScore`, `Percentage`, `Passed`, `Completed`, `MarkingMode`; EF migration T34 adds nullable columns only
- **Skill update**: `PatternSkillUpdateService` upserts `StudentSkillProfile` from `skillImpacts`; validates key allowlist, clamps delta, synthesises fallback from pattern key when impacts absent
- **Memory update**: compact memory packet (exercisePatternKey, score, coachSummary, top 3 corrections, top 5 impacts, top 3 signals) sent to `StudentMemoryService.UpdateMemoryAsync` — never includes raw submitted text; swallowed on failure
- **Frontend result UI**: `PatternEvaluationResultComponent` with 6 branches (MatchingPairs, GapFill, Chat/Email, ListenAndAnswer, SpokenResponse, ReadOnly); legacy non-pattern paths unchanged
- **Test counts**: 865 dotnet (451 unit + 414 integration) + 111 Playwright — all pass

## Student UX Alignment / Writing-Assumption Cleanup (complete — 2026-06-10)

All 7 phases complete. The student UI no longer implies SpeakPath is a writing/email-only app.

- **Nav**: student sidebar and mobile nav show Today, Journey, Practice, Progress, Profile. Dashboard label removed. Vocabulary removed from top-level nav.
- **Today** (`/dashboard`): motivational home page. Heading: "Today's Lesson". "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Journey** (`/journey`, `/my-path`): page heading "Learning Journey". Memory fallback copy updated from "workplace writing" to "workplace English". "Continue practising" CTA replaced with safe CTAs to `/dashboard` and `/practice`.
- **Practice Gym** (`/practice`): MVP landing page. Functional cards: Vocabulary (→`/vocabulary`), Listening, Writing, Speaking (→`/activity?type=X`). Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. Does not auto-start on load.
- **Fixture cleanup**: generic writing/email-only fixture copy in Playwright tests updated to mixed-skill workplace English. Valid WritingScenario and email_reply test coverage preserved. No seed data deleted.
- **Test counts**: 865 dotnet (unchanged) + 165 Playwright — all pass

## Known gaps / not yet built

- Session reflection (`GET /api/sessions/{id}/reflection` returns 501; needs AI prompt key `session_reflection`)
- `ActivityShellComponent` not yet embedded inline in lesson page (navigates away instead)
- No real STT provider (SpeakingRolePlay uses `FakeSpeechToTextService`)
- No email delivery for temp passwords (admin copies manually)
- No admin CRUD for career profiles / learning tracks (seed data only)
- No audio cleanup job (50-file soft ceiling in place as mitigation)
- Dynamic pattern selection (week skills → pattern choice) not yet implemented

See `docs/backlog/deferred-work.md` for the full deferred work list.

## Next recommended work

1. **Dynamic Pattern Selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
2. **Practice Gym Expansion** — deep pattern/skill selection within Practice Gym (Workplace Chat, Email, Gap Fill, Phrase Match unlock; dynamic session template).
3. **Session Reflection AI** — evaluation outputs now stable; wire `session_reflection` prompt.

See `docs/sprints/current-sprint.md` for the active sprint scope.
