---
status: current
lastUpdated: 2026-06-18 22:00
owner: product
supersedes:
supersededBy:
---

# SpeakPath â€” Current Product State

Last updated: 2026-06-18

---

## Admin UI foundation, core migration, and gate closure (Phase 10X-A / 10X-B / 10X-C-F)

The admin app now has a SpeakPath wrapper component layer aligned with TailAdmin Angular Layout One.

**Visual source of truth:** TailAdmin Angular Layout One (https://angular-demo.tailadmin.com/layout-one).
Actual TailAdmin source/assets are not present. Styling approximates TailAdmin Layout One.
See `docs/architecture/admin-ui-design-system.md` for the full architecture and asset status.

**Critical fix in 10X-C-F:** `AdminAppLayoutComponent` now uses `ViewEncapsulation.None` so
shell CSS (sidebar, nav items, header, drawer, profile flyout) reaches child component DOM.
Before this fix, the CSS existed but was blocked by Angular's default emulated encapsulation.
All admin pages now render with sidebar left, content right, header sticky — matching TailAdmin Layout One.

- Admin tokens: `src/app/admin/tokens/admin-tokens.css`.
- Barrel: `src/app/admin/index.ts`.
- Shell wrappers: `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header`.
- Page wrappers: page header, card, stat card, button, badge, table, state components, form controls, pagination, filter bar, modal, drawer, and toast outlet.
- Service foundations: admin toast, modal confirm state, drawer state.
- All core admin pages migrated to wrapper layer: Dashboard, Students, AI Config, AI Usage, Prompts,
  Exercise Types, Integrations, Diagnostics, Curriculum, and Usage Policies.

Remaining admin polish is scoped to page-local legacy internals, not new product features.
Dashboard inline CSS, AI Config form internals, Integrations internals, Curriculum create/edit/preview
forms, and student management modals remain future cleanup areas.

See: `docs/architecture/admin-ui-design-system.md`

## What is built and verified

The following end-to-end flow is implemented and verified:

```
Admin logs in
â†’ Admin creates student (temp password shown once)
â†’ Student logs in
â†’ Student changes temporary password (enforced server-side)
â†’ Student completes onboarding (language pair, career profile, experience level)
â†’ Student reaches Today page (the student home/dashboard)
â†’ Student starts Today's Lesson or navigates to Journey, Practice, Progress, or Profile
â†’ Student starts an activity (Writing / Listening / Vocabulary / Speaking)
â†’ Student submits draft or recording
â†’ Student sees structured AI feedback
â†’ Student retries or continues to next activity
â†’ Student can revisit learning history
```

## Playwright gate status

Phase 10H-F restored the full Playwright suite after Practice Gym fixture drift.

Failure categories found and fixed:

- Selector drift from old fixed Practice Gym card IDs to catalog-driven
  `practice-format-*` cards.
- Fixture/test data drift around ready runnable exercise types and planned
  non-runnable AI role play rows.
- Copy/label drift for the landing hero and perfect-score result label.
- Shared audio fallback selector drift after listening formats moved to the
  shared audio player.

Final Playwright result: 175 passed. No tests remain failing or skipped from this
stabilisation pass. No product behaviour changed.

## Usage governance (Phase 10R)

All AI feature calls are tracked per student. Admins can define and assign quota policies with per-feature daily limits. Expensive on-demand AI calls are blocked (HTTP 429) before they incur cost when a student exhausts their quota. Prepared/pre-generated content is always allowed.

- Feature registry: 16 features across PreparedLearning, DynamicAi, ExpensiveAi categories.
- 3 seeded policies: Default Pilot Student (TrackOnly all), Low Cost Student (HardLimit 5/day writing, 3/day speaking, 5/day TTS), Test Unlimited (TrackOnly, for testing).
- Admin UI at `/admin/usage-policies` — create, edit, assign policies.
- HTTP 429 response includes `featureKey`, `availableAlternatives`, `resetAt`.
- Audit log written for every policy assignment change.

Deferred: workspace/cohort inheritance, billing, monthly/weekly limits, student-facing widget.

See: `docs/architecture/usage-governance.md`

---

## Learning preferences in AI context

Student profile preferences are used by AI generation and evaluation.
Generated Today lesson activities, Practice Gym activities, background Practice
Gym activities, buffered lesson activities, lesson batch planning summaries, and
AI activity evaluation (WritingScenario, SpeakingRolePlay) all receive compact
learner preference context when fields are present.

The context can include preferred name, learning language, support language,
translation help preference, learning goals, custom goal, focus areas, custom
focus, difficulty preference, and current CEFR level as system-estimated.

Prompt-editing fields, admin-only profile names, roles, quotas, lifecycle state,
account details, raw submitted text, and any student-editable CEFR override are
excluded. Missing preferences create no fake defaults.

`LearningGoalContext` uses custom goal first, then selected goals, then legacy
goal fields, then career context. If none are present, it remains null and does
not default to workplace.

## Preference enforcement rules (Phase 10K-F)

- Vocabulary cadence picks gate on `WorkplaceSpecific` from the resolved goal context.
  Non-workplace students (Day-to-day, Travel, Social, etc.) receive `PhraseMatch`.
  Workplace students receive `GapFillWorkplacePhrase`.
- Lesson batch generation compact summary includes `preferredSessionDurationMinutes`
  as a hint to the AI planner. `SessionDurationTemplates` in `SessionGeneratorService`
  is the authoritative session length gate.
- AI evaluation prompts receive `learnerPreferences` and `learningGoalContext`
  variable slots. Current evaluation prompt templates do not yet reference these
  variables — a prompt-engineering pass is needed to activate them.

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
- **Practice Gym** (`/practice`) is the student-facing landing for classroom-style free practice by skill, vocabulary class, exercise type, and future live practice. It does not auto-start an activity on load.
- Vocabulary is accessible from Practice Gym and Progress â€” it is not a top-level nav item.
- Writing and Email are valid activity types within Practice Gym and lessons. The student product is not writing/email-first.

## Implemented activity types

| Type | Status |
|---|---|
| `WritingScenario` | âœ… implemented |
| `ListeningComprehension` | âœ… implemented (with TTS audio) |
| `VocabularyPractice` | âœ… implemented |
| `SpeakingRolePlay` | âœ… implemented (MVP â€” fake STT) |

All four activity types use the unified `/activity` path.
`/api/writing/*` endpoints have been removed. See `docs/decisions/activity-flow-migration.md`.

## Practice Gym - activated pattern cards

Skill cards call `GET /api/activity/practice-gym/next?skill=<skill>`. Exact
exercise type cards call `GET /api/activity/practice-gym/next?exerciseType=<key>`.
Both serve a ready pre-generated activity from the pool (`source: "pool"`) when
available, or fall back to on-demand generation (`source: "onDemandFallback"`)
and route to `/activity?activityId=<id>&returnTo=/practice`.

`GET /api/activity/next` still accepts canonical `?exerciseType=<key>` plus legacy `?pattern=<key>` and `?type=` query parameters, unchanged, as the underlying fallback/compatibility path.

| Practice Gym card | Selection | Status |
|---|---|---|
| Vocabulary class | `/activity?exerciseType=phrase_match&returnTo=/practice` (module link, unaffected) | functional word-card lesson + matching practice |
| Listening | pool-aware skill selection | functional |
| Reading | pool-aware skill selection (`reading_multiple_choice_single`, `reading_multiple_choice_multi`, `reading_fill_in_blanks`, `reorder_paragraphs`) | functional |
| Writing | pool-aware skill selection | functional |
| Speaking | pool-aware skill selection | functional recorded prompt, no pronunciation claim |
| Matching | `/activity?exerciseType=phrase_match&returnTo=/practice` (module link, unaffected) | functional |
| Fill in the blanks | `/activity?exerciseType=gap_fill_workplace_phrase&returnTo=/practice` (module link, unaffected) | functional |
| Email | `/activity?exerciseType=email_reply&returnTo=/practice` (module link, unaffected) | functional |
| Workplace Chat | `/activity?exerciseType=teams_chat_simulation&returnTo=/practice` (module link, unaffected) | functional |
| Multiple choice | covered by Reading (`reading_multiple_choice_single` single, `reading_multiple_choice_multi` multi) | functional |
| Sentence transformation | - | Coming soon |
| Error correction | - | Coming soon |
| Word formation | - | Coming soon |
| Unscrambling | - | Coming soon |
| AI role play | - | Coming soon, live AI |
| Pronunciation | - | Coming soon, no STT/scoring support |


Practice Gym skill cards now use the ExerciseType registry. A skill card no
longer means one fixed activity type. The selected skill resolves to an enabled,
ready, generation-eligible, Practice Gym-supported exercise type with the same
`primarySkill`, then routes through canonical `exerciseType=<key>`. If no
eligible row exists, the frontend shows a safe unavailable message and does not
start broken generation. Planned future exercise format rows remain blocked. This is not the
final Practice Gym pre-generation pool; the future pool should reuse the same
registry selection rules.

All pattern-keyed activities go through `PatternEvaluationRouter`. Progress updates only after a submitted attempt. Returning from any pattern card goes back to `/practice` via `returnTo`. Ready Practice Gym cache entries are consumed before on-demand AI generation.

## Real TTS via Admin AI Config (complete â€” 2026-06-11)

`ListeningAudioService` and `PlacementAudioService` now resolve TTS provider at request time via `TtsProviderResolver`:

- `AiProviderConfig` rows `tts.listening` and `tts.placement` control which TTS service runs
- Default seed: `provider=fake, model=fake, voice=fake` â†’ silent WAV (tests never need `OPENAI_API_KEY`)
- Admin can switch to real TTS providers in Admin AI Config UI:
  - OpenAI: `provider=openai`, model `tts-1` or `tts-1-hd`, voice such as `onyx`
  - Gemini: `provider=gemini`, model must be a Gemini TTS model such as `gemini-2.5-flash-preview-tts`, voice such as `Kore`
  - Qwen: `provider=qwen`, model `cosyvoice-v2`, voice such as `longxiaochun_v2`
- TTS category saves reject non-TTS models. Existing Gemini TTS configs with a normal text model are defensively routed to the default Gemini TTS model by `GeminiTextToSpeechService`.
- `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; returns `audio/mpeg`; never throws
- `GeminiTextToSpeechService` calls the Gemini `generateContent` TTS path with `responseModalities=["AUDIO"]`, `speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName`, and returns `audio/wav`; never throws
- Activity audio endpoints remain JWT-protected. Angular fetches listening audio through `HttpClient` and converts it to a temporary `blob:` URL before rendering `<audio>`, so browser media requests do not hit `/api/activity/{id}/audio` anonymously.
- `PlacementAudioService` checks both `.wav` and `.mp3` on disk (backward compat with pre-existing files)
- T35 migration adds nullable `voice_name varchar(100)` to `ai_provider_configs`

## Onboarding experience step (complete â€” 2026-06-11)

A new step-5 collects professional context before placement:

- `PATCH /api/onboarding/experience` â€” sets `ProfessionalExperienceLevel`, `RoleFamiliarity`, computes `WorkplaceSeniority`
- Uses `StudentProfile.SetExperienceContext()` â€” bypasses onboarding state machine; can be called at any stage
- Angular: `step5-experience` component inserted between step-4 and `/placement`
- Step-4 now shows "Step 4 of 5"; navigates to `/onboarding/step-5` on finish
- Non-blocking: API failure still navigates to `/placement`; "Skip for now" skips without calling API
- Existing completed students not broken â€” endpoint accepts any auth token regardless of onboarding state

## Onboarding v2 foundation (complete — 2026-06-17, Phase 10I)

A configurable multi-step onboarding system (v2) runs in parallel with the existing v1 state machine. Existing students and v1 code are untouched.

### New API endpoints

- `GET /api/onboarding` — returns `OnboardingV2StatusDto`: current step, completed steps, percentage, preliminary CEFR level. Lazy-creates a `StudentOnboardingProgress` record on first call. Students who completed v1 onboarding are auto-marked complete.
- `POST /api/onboarding/steps/{stepKey}` — submits an answer for one step. Validates answer against step type (max length, valid option keys, max selections). Applies typed `OnboardingAnswerMapping` to `StudentProfile.UpdateLearningPreferences()`. Idempotent — upserts `StudentOnboardingResponse`.
- `POST /api/onboarding/complete` — validates all SystemRequired+enabled steps are done, scores assessment answers against server-side metadata, stores `PreliminaryCefrLevel` on progress, transitions `LifecycleStage` → `PlacementRequired`. Does **not** overwrite a real `CefrLevel` from PlacementAssessment.
- `GET /api/admin/onboarding/flow` (Admin role) — read-only view of the active `OnboardingFlowDefinition` including steps and answer mappings. Never exposes `AssessmentMetadataJson`, correct answers, or scoring weights.

### Architecture decisions

- v2 is parallel — v1 `OnboardingStatus`/`OnboardingStep` fields on `StudentProfile` remain as legacy compatibility.
- Single active flow enforced by PostgreSQL partial unique index (`WHERE is_active = true`).
- Flow versions are immutable once students have progress; admin edits must create a new version.
- `PreliminaryCefrLevel` stored on `StudentOnboardingProgress` only — never overwrites `StudentProfile.CefrLevel` unless it is null.
- `AssessmentMetadataJson` (correct answers, scoring weights) is server-side only — never returned to student or admin APIs.
- Percentage counts SystemRequired+IsEnabled steps only.
- Post-onboarding lifecycle → `PlacementRequired` (no “OnboardingComplete” stage exists).
- Unique `(progress_id, step_key)` constraint on `StudentOnboardingResponse`.

### Angular route

`/onboarding/v2` — standalone shell component with 11 step renderers (Welcome, PreferredName, SupportLanguage, LearningGoals, FocusAreas, DifficultyPreference, SingleChoice, MultipleChoice, FreeText, AssessmentQuestion, Summary).

### Known limitations

- No admin visual flow builder — flow is seeded via `OnboardingFlowSeeder`.
- Preliminary CEFR is a simple weight-band calculation, not a full adaptive placement engine.
- No curriculum routing or readiness pool based on v2 outcome.
- No Playwright E2E spec for v2 flow (no test user seeded with v2 progress).

### Migration

T47_OnboardingV2 — adds `onboarding_flow_definitions`, `onboarding_step_definitions`, `student_onboarding_progress`, `student_onboarding_responses`.

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
- Integrations can trigger lesson generation, inspect recent generation batches, view ready lesson buffers, retry failed/partial batches, and cancel queued/running batches stuck from background generation failures
- Background lesson generation now materializes AI lesson plans into ready `LearningSession` rows instead of failing on generated `GenerationJobItem` tracking state
- Practice Gym background caching queues and materializes ready pattern-keyed activities for eligible active students
- Admin shell header is avatar-only; user email, role, profile placeholder, and sign out live in the avatar flyout menu
- Curriculum is hidden from admin navigation while its future purpose is redefined

## Placement Assessment â€” current state

Placement Assessment MVP is implemented:
- 6-section structured assessment (`PlacementAssessment`, `PlacementSection` entities)
- AI evaluation â†’ `PlacementResult` as CEFR source of truth
- Listening section uses **server-side TTS audio** (`PlacementAudioService`), not browser SpeechSynthesis
- `GET /api/placement/audio/{assessmentId}/listening` streams authenticated audio
- Frontend shows native `<audio controls>` when server audio is available; graceful fallback if not
- Transcript hidden by default behind "Show transcript"

## LearningSession data layer (Phase 1 complete â€” 2026-06-10)

- `LearningSession` and `SessionExercise` domain entities implemented
- `SessionStatus` and `ExerciseStatus` enums added
- EF configurations and migration T32 applied (`learning_sessions`, `session_exercises` tables)
- `LinguaCoachDbContext` updated with `LearningSessions` and `SessionExercises` DbSets
- 52 new tests added (284 unit, 247 integration â€” 531 total)

## LearningSession generator (Phase 2 complete â€” 2026-06-10)

- `ExerciseKind` enum added (`VocabularyWarmup`, `ContextInput`, `ListeningInput`, `ReadingInput`, `WritingTask`, `SpeakingTask`, `Review`)
- `ISessionGeneratorService` / `SessionGeneratorService` implemented
- Duration templates: 10 min (3 steps), 15 min (4 steps), 20 min (4 steps), 30 min (5 steps)
- Weak-skill substitution: Speaking weak â†’ SpeakingTask promoted; Listening weak â†’ ListeningInput enforced
- Idempotent: calling twice on the same day returns the same session
- Module progression: advances to next module after 5 completed sessions
- 65 new tests added in Phase 2 (609 total: 328 unit, 281 integration)

## LearningSession backend endpoints (Phase 3 complete â€” 2026-06-10)

- `SessionsController` at `src/LinguaCoach.Api/Controllers/SessionsController.cs`
- Endpoints: `GET /today`, `GET /{id}`, `POST /{id}/start`, `POST /{id}/complete`, `POST /{id}/exercises/{eid}/complete`, `GET /{id}/reflection` (501 stub)
- `SessionQueryHandler` and `SessionLifecycleHandler` in `LinguaCoach.Infrastructure/Sessions/`
- Lifecycle transitions: `CourseReady` â†’ `InLesson` (start), `InLesson` â†’ `ActiveLearning` (complete)
- All operations idempotent; ownership verified on every request
- 27 new integration tests added in Phase 3 (629 total: 328 unit, 301 integration)

## LearningSession frontend (Phase 4 complete â€” 2026-06-10)

- Today's Lesson card on dashboard â€” visible for `CourseReady`, `InLesson`, `ActiveLearning` lifecycle stages
  - Shows title, duration, skill focus, step count, status badge
  - Button label adapts: "Start today's lesson" / "Resume lesson" / "Review today's lesson"
  - Practice Gym remains secondary but visible
- `LessonComponent` at `/lesson/:sessionId` â€” Angular standalone component
  - Session detail loaded from `GET /api/sessions/{id}`
  - Ordered exercise steps, progress bar, per-step panel with instructions
  - Prepared buffered steps open directly; unprepared old-session steps show an explicit load action
  - Start, complete exercise, complete lesson flows fully wired
  - Completion summary shown on lesson complete
- `SessionService` + TypeScript models added to frontend core
- 14 new Playwright e2e tests â€” 81/81 pass total (no regressions)

## Exercise activity wiring (Phase 5A complete â€” 2026-06-10)

- `POST /api/sessions/{sessionId}/exercises/{exerciseId}/prepare` endpoint added
- Idempotent: calling twice returns the same `LearningActivity`
- ExerciseKind â†’ ActivityType deterministic mapping: VocabularyWarmupâ†’VocabularyPractice, ContextInputâ†’WritingScenario, ListeningInputâ†’ListeningComprehension, ReadingInputâ†’ReadingTask, WritingTaskâ†’WritingScenario, SpeakingTaskâ†’SpeakingRolePlay
- Review step returns a lightweight reflection placeholder (`isReview: true`), no AI generation
- VocabularyPractice and `ReadingTask` (not yet in `IAiActivityGenerator`) use `SystemFallback` placeholders
- 16 new integration + unit tests; 645 total (328 unit + 317 integration)

## Exercise activity wiring â€” frontend (Phase 5B complete â€” 2026-06-10)

- `LessonComponent` now calls `POST /api/sessions/{id}/exercises/{eid}/prepare` when student opens an exercise
- "Open activity" button navigates to `/activity?activityId=<id>&returnTo=/lesson/<sessionId>`
- `ActivityLessonComponent` supports `?activityId=<id>` (loads specific prepared activity) and `?returnTo=<path>`
- Review steps show a reflection prompt + "Mark complete" â€” no activity generated
- Server-assigned `learningActivityId` (persists across refresh) skips re-prepare
- `GET /api/activity/{id}` backend endpoint added
- 8 new Playwright tests; 90/90 pass

## Exercise Pattern Engine (complete â€” 2026-06-10)

- `exercise_patterns` table is seeded with the 8 MVP patterns.
- `LearningActivity.ExercisePatternKey` stores the durable pattern link.
- Pattern-aware prepare/generation sets `exercisePatternKey` and returns `interactionMode` on `ActivityDto`.
- Pattern-keyed activity responses include bounded `contentJson` for frontend renderers; legacy listening activities do not expose raw answer-bearing JSON before submission.
- `ActivityLessonComponent` now routes pattern-keyed activities through `ExerciseRendererComponent`.
- MVP renderers are wired: ReadOnly, FreeTextEntry, MatchingPairs, GapFill, AudioAndFreeText, AudioAndGapFill, ChatReply, EmailReply.
- All 7 active renderers (excluding ReadOnly) follow a Lesson â†’ Practice â†’ Evaluate structure: a "Goal" element (`learningGoal`) shown via `ChatReplyComponent`'s own goal display, `EmailReplyComponent`/`FreeTextEntryComponent`'s `coachNote`, or the shared `ExerciseLessonIntroComponent` (GapFill, MatchingPairs, AudioAndFreeText, AudioAndGapFill).
- Frontend renderer coverage added; full Playwright suite passes 97/97.
- Backend baseline: 762 tests pass (380 unit + 382 integration).
- `npm run build` passes; known non-blocking Angular warnings remain for admin CSS budgets and skipped selectors.

## Pattern Evaluation Engine (complete â€” 2026-06-10)

All 7 phases complete. `MarkingMode` is now first-class in the evaluation flow.

- **Evaluators**: `ExactMatchEvaluator` (gap_fill, listen_and_gap_fill), `KeyedSelectionEvaluator` (phrase_match), `NoMarkingEvaluator` (lesson_reflection), `AiStructuredEvaluator` (listen_and_answer, email_reply, teams_chat_simulation), `AiOpenEndedEvaluator` (spoken_response_from_prompt)
- **Router**: `IPatternEvaluationRouter` dispatches by `MarkingMode`; wired into `ActivitySubmitHandler`
- **Persistence**: `ActivityAttempt` stores structured `SubmittedAnswerJson`, `EvaluationResultJson`, `MaxScore`, `Percentage`, `Passed`, `Completed`, `MarkingMode`; EF migration T34 adds nullable columns only
- **Skill update**: `PatternSkillUpdateService` upserts `StudentSkillProfile` from `skillImpacts`; validates key allowlist, clamps delta, synthesises fallback from pattern key when impacts absent
- **Memory update**: compact memory packet (exercisePatternKey, score, coachSummary, top 3 corrections, top 5 impacts, top 3 signals) sent to `StudentMemoryService.UpdateMemoryAsync` â€” never includes raw submitted text; swallowed on failure
- **Frontend result UI**: `PatternEvaluationResultComponent` with 6 branches (MatchingPairs, GapFill, Chat/Email, ListenAndAnswer, SpokenResponse, ReadOnly); legacy non-pattern paths unchanged
- **Test counts**: 865 dotnet (451 unit + 414 integration) + 111 Playwright â€” all pass

## Student UX Alignment / Writing-Assumption Cleanup (complete â€” 2026-06-10)

All 7 phases complete. The student UI no longer implies SpeakPath is a writing/email-only app.

- **Nav**: student sidebar and mobile nav show Today, Journey, Practice, Progress, Profile. Dashboard label removed. Vocabulary removed from top-level nav.
- **Today** (`/dashboard`): motivational home page. Heading: "Today's Lesson". "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Journey** (`/journey`, `/my-path`): page heading "Learning Journey". Memory fallback copy updated from "workplace writing" to "workplace English". "Continue practising" CTA replaced with safe CTAs to `/dashboard` and `/practice`.
- **Practice Gym** (`/practice`): MVP landing page. Functional cards: Vocabulary (â†’`/vocabulary`), Listening, Writing, Speaking (â†’`/activity?type=X`). Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. Does not auto-start on load.
- **Fixture cleanup**: generic writing/email-only fixture copy in Playwright tests updated to mixed-skill workplace English. Valid WritingScenario and email_reply test coverage preserved. No seed data deleted.
- **Test counts**: 865 dotnet (unchanged) + 165 Playwright â€” all pass

## Known gaps / not yet built

- Session reflection (`GET /api/sessions/{id}/reflection` returns 501; needs AI prompt key `session_reflection`)
- `ActivityShellComponent` not yet embedded inline in lesson page (navigates away instead)
- No real STT provider (SpeakingRolePlay uses `FakeSpeechToTextService`)
- No email delivery for temp passwords (admin copies manually)
- No admin CRUD for career profiles / learning tracks (seed data only)
- No audio cleanup job (50-file soft ceiling in place as mitigation)
- Dynamic pattern selection (week skills â†’ pattern choice) not yet implemented

See `docs/backlog/deferred-work.md` for the full deferred work list.

## Next recommended work

1. **Dynamic Pattern Selection** â€” choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
2. **Practice Gym Expansion** â€” deep pattern/skill selection within Practice Gym (Workplace Chat, Email, Gap Fill, Phrase Match unlock; dynamic session template).
3. **Session Reflection AI** â€” evaluation outputs now stable; wire `session_reflection` prompt.

See `docs/sprints/current-sprint.md` for the active sprint scope.

## Exercise Type Catalog foundation (Phase 3A)

The platform now has a durable exercise type catalog for future generation control.
Skills and exercise types are separate: a module can target primary and secondary
skills, while its Practice stage uses a catalog `exerciseType`.

Admins can list and enable or disable exercise types from Admin Exercise Types.
Disable affects future Today and Practice Gym generation only. Existing activities,
attempts, and history remain readable.

Planned future exercise formats are visible in the catalog as planned entries.
They are not generation-eligible until implementation status becomes ready, even
if an admin enables them.

## Phase 3B ExerciseType routing foundation

The backend now has an `IExerciseTypeRegistry` backed by the persisted exercise type catalog. It resolves `exerciseType` keys to renderer, evaluator, generation prompt, legacy `ActivityType`, and `ExercisePatternKey` metadata.

`GET /api/activity/next?exerciseType=<key>` is supported for ready runnable types. Existing `/activity?type=...` and `/activity?pattern=...` links still work. Practice Gym now routes implemented cards with `exerciseType` where safe. Today session generation validates deterministic pattern keys through the registry before creating steps.

Planned future exercise formats remain visible in Admin. They are not generation-eligible or routable to student activity flows until implementation status is `ready`.

## SpeakingRolePlay staged migration (Phase 5 — 2026-06-15)

`SpeakingRolePlay` now generates and serves `module_stage_v1` staged content,
matching the pattern established by `WritingScenario` and `ListeningComprehension`.

**What changed:**

- Generation prompt (`activity_generate_speaking_roleplay`) rewritten to produce
  `module_stage_v1` with `learnContent`, `practiceContent`, and `feedbackPlan`.
  Token budget increased: `maxInputTokens` 900 → 1600, `maxOutputTokens` 800 → 1200.
- `learnContent` explicitly forbids recording controls, microphone instructions,
  `startRecording`, and `stopRecording`.
- `practiceContent.exerciseData` requires: `role`, `partnerRole`, `situation`, `prompt`.
- `AiActivityGeneratorHandler` validates `SpeakingRolePlay` as staged (retry-once-then-fail).
- `ActivityGetHandler` detects legacy flat speaking JSON and adapts it to `legacy_adapted_v1`
  via `AdaptLegacySpeaking`. Old student data and history continue working unchanged.
- `SpeakingRolePlayEvaluator.ExtractExerciseDataJson` feeds only `practiceContent.exerciseData`
  into the evaluation prompt.
- Frontend `LegacySpeakingPresenter` returns `stagedLearning` block when `stageContent.learn`
  exists; falls back to legacy `speakingScenario` block for old rows.

**What was NOT changed:**

- No planned speaking format rows made runnable.
- No Practice Gym pre-generation changes.
- No Today pre-generation changes.
- No MinIO / audio lifecycle changes.
- No new planned future exercise renderer or evaluator.
- `/activity` endpoint and old compatibility params remain.

**Remaining staged migrations:** pattern-backed activities.

## Phase 6 — VocabularyPractice staged migration, completed

`VocabularyPractice` now uses `module_stage_v1` for newly generated deterministic vocabulary activities. The migration keeps the existing seeded vocabulary source. It does not add broad AI vocabulary generation.

The staged vocabulary module has exactly three pages: Learn, Practice, and Feedback. Learn teaches vocabulary meaning, usage, word form, example context, memory strategy, and common mistakes. Practice contains the fill-blank vocabulary task through `practiceContent.exerciseData`. Feedback uses the existing deterministic vocabulary evaluator with staged `practiceContent.exerciseData` support and legacy flat JSON fallback.

Completed staged migrations:

- `ListeningComprehension`
- `WritingScenario`
- `SpeakingRolePlay`
- `VocabularyPractice`

Remaining staged migrations are pattern-backed activities. Planned future exercise formats made runnable so far: `reading_multiple_choice_single` (Phase 8A), `reading_multiple_choice_multi` (Phase 8B), `reading_fill_in_blanks` (Phase 8C), `reorder_paragraphs` (Phase 8D), `reading_writing_fill_in_blanks` (Phase 8E), `summarize_written_text` (Phase 8F), `write_essay` (Phase 8G), `listening_multiple_choice_single` (Phase 8H — first runnable listening-primary format), `listening_multiple_choice_multi` (Phase 8I — second runnable listening-primary format), `listening_fill_in_blanks` (Phase 8J — third runnable listening-primary format, first runnable listening+writing format), `select_missing_word` (Phase 8K — fourth runnable listening-primary format), `highlight_correct_summary` (Phase 8L — fifth runnable listening-primary format, first runnable listening+reading format), `highlight_incorrect_words` (Phase 8M — sixth runnable listening-primary format, second runnable listening+reading format), `write_from_dictation` (Phase 8O — seventh runnable listening-primary format), and `summarize_spoken_text` (Phase 8Q — eighth runnable listening-primary format, first AI-evaluated listening+writing format). All reading-primary, writing, and listening planned future formats are now ready. All remaining planned future exercise formats are the speaking formats (`read_aloud`, `repeat_sentence`, `describe_image`, `respond_to_situation`, `retell_lecture`, `summarize_group_discussion`, `answer_short_question`), which remain planned and non-runnable. Today pre-generation remains a future phase. Phase 8P (2026-06-16) wired the audio lifecycle for all 9 listening pattern keys. `HandlePatternKeyedAsync` now calls `EnsureAudioAsync` after creating pattern-keyed listening activities. `ActivityDto` gains an `AudioStatus` string field (`"ready"` / `"pending"` / `"unavailable"`). A shared `app-audio-player` Angular component was created and all 5 listening renderer HTML templates now use it instead of inline `<audio>` tags. The exercise-renderer getters for `listeningFillInBlanks`, `highlightCorrectSummary`, and `highlightIncorrectWords` now fall back to `activity.audioUrl` from the API when `ed['audioUrl']` is absent from the content JSON. Audio is now generated on first fetch for all listening patterns; `audioUrl` will be non-null when TTS succeeds. Phase 8Q (2026-06-16) added `summarize_spoken_text` to `ListeningAudioService.ListeningPatternKeys` (now 10 keys) so it reuses the same shared audio lifecycle and `app-audio-player`. Its evaluation reuses the existing `AiStructuredEvaluator` AI path (same as `summarize_written_text` / `write_essay`); `learnContent` and the expected-answer `keyPoints` are never sent to the AI before submission.

Phase 8N (2026-06-16) added configurable practice item counts as a foundation (not a new format). Every `ExerciseTypeDefinition` now carries `MinItemsPerPractice`/`DefaultItemsPerPractice`/`MaxItemsPerPractice` and `MinOptionsPerItem`/`DefaultOptionsPerItem`/`MaxOptionsPerItem`, seeded per type, editable in the admin exercise-types page (with inline `min <= default <= max` and non-negative validation) and via admin PATCH. Counts feed generation prompt context and optional validator count enforcement. Counts are configuration only and never change readiness; no format was made runnable. See [practice-item-sets.md](../architecture/practice-item-sets.md).

## Phase 10O — Practice Gym Suggested Practice & Pool Serving, completed (2026-06-18)

Phase 10O connects the readiness pool to the student-facing Practice Gym. The pool built in 10M/10N is now surfaced as personalised suggestion cards via a new student API.

### New student-facing API endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/practice-gym/suggestions` | Returns SuggestedItems, ContinueItems, ReviewItems from the readiness pool |
| POST | `/api/practice-gym/suggestions/{id}/start` | Reserves an item; returns LearningActivityId / LearningSessionId for navigation |
| POST | `/api/practice-gym/suggestions/{id}/complete` | Best-effort marks item consumed |

### Sections returned

- **SuggestedItems** — Ready items ranked by focus-area match → goal context match → pool priority → expiry → FIFO. Max 6.
- **ContinueItems** — Reserved (in-progress) items not past expiry. Max 3.
- **ReviewItems** — ReviewOnly status items or Ready+lower-level content with review/scaffold/remediation routing reason. Max 4.

### Labels / wording

Normal → "Recommended for your current goal" | Review → "Review" | Scaffold → "Step back to strengthen basics" | Remediation → "Targeted fix" | Fallback → "General practice".

### What is NOT done yet (after 10O-F)

- Existing `GET /api/activity/practice-gym/next` (by skill/exercise type) is unchanged.
- Session/SessionExercise completion paths do not yet wire consumption (linked via LearningActivityId only).

### Tests (10O only)
+14 unit tests, +10 integration tests. Total: 1774 passed, 0 failed.

---

## Phase 10O-F — Practice Gym UI Integration & Completion Consumption Wiring, completed (2026-06-18)

Phase 10O-F connects the 10O backend API to the Angular Practice Gym page and wires completed activities back to readiness pool consumption.

### Angular UI changes

- **`PracticeGymSuggestionsService`** — new Angular service: `getSuggestions()`, `startSuggestion(id)`, `completeSuggestion(id)`.
- **`PracticeGymComponent`** — extended: suggestion signals, `loadSuggestions()`, `startSuggestion()` with loading/disabled state, `routingLabel()` helper.
- **Practice Gym template** — new sections: Suggested for you, Continue practice, Review practice. Cards show title, skill, CEFR level, estimated duration, context tags, routing label. Empty/loading/error states present. Existing By skill and By exercise type sections preserved.
- Student-friendly routing labels: Normal → "Recommended for your current goal", Review → "Review", Scaffold → "Step back to strengthen basics", Remediation → "Targeted fix", Fallback → "General practice".
- Lower-level content labelled with muted chip. No silent downgrade.

### Backend consumption wiring (TODO-014 resolved)

- `ActivitySubmitHandler` — injected `IPracticeGymSuggestionService`. `TryConsumeReadinessItemAsync` called best-effort after all completion paths:
  - WritingScenario / AI-evaluated: always called after save.
  - VocabularyPractice: called after deterministic evaluation.
  - ListeningComprehension: called after deterministic evaluation.
  - Pattern evaluation: called only when `evalResult.Completed == true`.
- Lookup scoped to `studentProfileId + activityId + Reserved status`. Exception swallowed — completion response never blocked.
- Idempotent: `TryMarkConsumedAsync` no-ops on already-consumed items.

### Tests added

- 4 integration tests (`ReadinessConsumptionWiringTests`): completion marks consumed, idempotent, no-item path safe, consumed item absent from suggestions.
- 12 Angular unit tests in `practice-gym.component.spec.ts`: load, empty, error, section rendering, start navigation, labels, existing sections preserved.

### Total test counts

- Architecture: 3 passed
- Unit: 1174 passed
- Integration: 601 passed (was 597 before this phase)
- Angular: 272 passed (was 247 before this phase)
- Total backend: 1778 passed, 0 failed

### TODOs closed

- TODO-014 — TryMarkConsumedAsync wired into ActivitySubmitHandler. Done.
- TODO-015 — Angular Practice Gym suggestion UI implemented. Done.

---

## Phase 10N — Background Replenishment Pipeline, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No Angular source changed.

**What was added:**

- `IReadinessPoolReplenishmentService` / `ReadinessPoolReplenishmentService` — background engine that sweeps expired/reserved items, recovers orphaned generating items, retries failed items, and fills pool shortfalls for all active students.
- `ReadinessPoolReplenishmentOptions` — appsettings-bound configuration (target count, expiry days, retry delay, max items per run, review/scaffold flag).
- `PoolHealthSummary` — health snapshot DTO: ready count, in-flight count, shortfall, `needsReplenishment` flag.
- `ReadinessPoolReplenishmentJob` — Quartz job running every 20 minutes.
- `GET /api/admin/students/{studentId}/readiness-pool/health` — new read-only admin endpoint showing health for both TodayLesson and PracticeGym pools.

**Key rules preserved:**
- `general_english` is the fallback context; workplace is not default.
- B2 students cannot silently receive B1 content as Normal.
- `EnableReviewScaffoldGeneration=false` by default; review/scaffold requires explicit flag AND ledger weakness signals.
- Existing on-demand generation paths unchanged (pool serving deferred to Phase 10O).
- No admin write endpoints added.

**Tests:** 16 new unit + 11 new integration = +27. Total: 1750 passed, 0 failed.

See: `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md`

---

## Phase 10L — CEFR-Aware Activity Routing, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No Angular source changed.

**What was added:**

- `ICurriculumRoutingService` / `CurriculumRoutingService` — pure application-layer routing policy.
  Selects suitable CEFR band and curriculum objectives before every AI generation call.
  Does not call AI. Does not modify student data. Always returns a recommendation.
- `CurriculumRoutingRequest` — input: student context, raw CEFR level, primary skill, source label, resolved goal context, learner preferences, `AllowReviewOrScaffold`.
- `CurriculumRoutingRecommendation` — output: `TargetCefrLevel` (normalized), `CurriculumObjectiveKey/Title`, `ContextTags`, `FocusTags`, `DifficultyBand`, `RoutingReason`, `IsLowerLevelContent`, `Explanation`, `RoutingContextSummary` (for AI prompt injection).
- `CurriculumRoutingRequestFactory` — builds request from `StudentProfile` + `ResolvedLearningGoalContext`.
- CEFR normalization: `B2+` → `B2`, `C1-` → `C1`, null/unknown → `A1`. Does not modify `StudentProfile.CefrLevel`.
- `RoutingReason` enum: `Normal`, `Review`, `Scaffold`, `Remediation`, `Fallback`.

**Routing wired into all 5 generation handlers:**
- `ActivityGetHandler.HandlePatternKeyedAsync` (on-demand + Practice Gym)
- `ExercisePrepareHandler` (Today's Lesson)
- `PracticeGymGenerationJob.MaterializeAsync` (background Practice Gym)
- `ActivityMaterializationJob.MaterializeExerciseAsync` (background lesson batch)
- `LessonBatchGenerationJob.BuildCompactSummaryAsync` (AI lesson planning summary)

**AI prompt integration:**
- `ActivityGenerationContext` extended with `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold`.
- `DbPromptAiContextBuilder` appends routing context before "Return ONLY".
- `cefrLevel` passed to AI is now the routing-normalized level, not raw profile value.

**Core rules enforced:**
- Routing never silently lowers CEFR level. Lower-level content requires `AllowReviewOrScaffold=true` and produces `RoutingReason.Review` + `IsLowerLevelContent=true`.
- Routing never defaults to workplace context. Non-workplace profiles always get `general_english` or goal-specific tags.
- DifficultyPreference maps to DifficultyBand: Gentle→1, Balanced→2, Challenging→4.

**What is NOT implemented (deferred to 10M+):**
`AllowReviewOrScaffold=true` in any handler (built but always false — enablement needs adaptive/ledger signals), session length → candidate count, CEFR-aware format matrix, readiness pools, background replenishment, Practice Gym UI redesign, admin write UI, `StudentProfile.CefrLevel` migration.

**Tests:** 16 new unit tests + 7 new integration tests. Total: 1692 passed (was 1656).

See: `docs/reviews/2026-06-17-phase-10l-cefr-aware-activity-routing-review.md`
See: `docs/architecture/curriculum-routing.md`

---

## Phase 10K — Curriculum Boundary / Level Syllabus Foundation, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No CEFR-aware routing implemented.

**What was added:**

- `CurriculumObjective` domain entity — scoped by CEFR level (A1–C2), primary skill, context tags, focus tags, prerequisite keys, recommended order, difficulty band (1-5), active/reviewable/exam-inspired flags.
- `CefrLevelConstants`, `CurriculumSkillConstants`, `CurriculumContextTagConstants` — canonical validated string sets. `workplace` is one context tag among 13; it is not the default for any objective.
- `CurriculumObjectiveSeeder` — 22 starter objectives across A1/A2/B1/B2, all major skills, multiple learner contexts. **Seed-only-missing** (Phase 10Q): skips any key already in the DB so admin-edited objectives are never overwritten on startup.
- `ICurriculumSyllabusQuery` / `CurriculumSyllabusQueryService` — read-only query service: by CEFR, by CEFR+skill, by CEFR+context tag, by CEFR+focus area, prerequisites, and `GetCandidatesForStudent`. Candidates only — no activity selection.
- `IAdminCurriculumSyllabusQuery` — admin extension returning active and inactive objectives with optional filters (Phase 10Q).
- `CurriculumContextMapper` — maps `ResolvedLearningGoalContext` to curriculum context tags. Null-safe; fallback is `general_english`. Non-workplace profiles never default to `workplace`.
- `CurriculumObjective.ExamplePrompts` / `AdminUpdatedAt` — Phase 10Q audit fields. `AdminUpdate()` sets `AdminUpdatedAt`; seeder uses `UpdateDetails()` which does not.
- `ICurriculumObjectiveWriteService` / `CurriculumObjectiveWriteService` — full admin CRUD with validation (Phase 10Q): slug key, CEFR, skill, context tag, difficulty band 1–5, self-prereq, dangling prereq. `PreviewRoutingAsync` is read-only — no student state mutation.
- Admin API endpoints (Phase 10Q): `GET /objectives`, `GET /objectives/{key}`, `GET /taxonomy`, `POST /objectives`, `PUT /objectives/{key}`, `POST /objectives/{key}/activate`, `POST /objectives/{key}/deactivate`, `POST /routing-preview`.
- Angular `AdminCurriculumComponent` — list with filters, create/edit form, non-mutating routing preview panel (Phase 10Q).
- Migrations: `T50_CurriculumSyllabusFoundation`, `T52_CurriculumObjectiveAdminFields`.

**What is NOT implemented (deferred):**

Exercise format locking by level, `StudentProfile.CefrLevel` type migration.

**TODOS added:** See `TODOS.md` — TODO-001 (plus-levels), TODO-002 (StudentProfile.CefrLevel migration).

---

## Phase 10J-F — Student App Design System & Responsive UI Foundation, completed (2026-06-17)

Frontend-only phase. No product behaviour, API contracts, or backend logic changed.

**Design tokens extended (`styles.css`):**
- `--sp-brand` (solid brand colour, `#5B4BE8`), `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, `--sp-sidebar-w-collapsed`, `--sp-content-max`, `--sp-content-max-desktop`, z-index layer tokens added to `:root`.
- `sp-card-hover` utility class added (transition, hover lift, active scale).
- `sp-pref-chip` / `sp-pref-chip--on` added for all preference chip toggles.
- Duplicate `sp-bottomnav` / `sp-navbtn` removed from global CSS (canonical definition in `student-app-layout.component.css`).

**Profile page:**
- All chip buttons (learning goals, focus areas, session length, difficulty) now use `sp-pref-chip--on` CSS class binding instead of inline `chipStyle()` method.
- `aria-pressed` attribute added to all chip buttons. `data-testid` added per chip.
- `focus-visible` keyboard ring included in chip CSS.
- `chipStyle()` method removed.

**Progress component:**
- All hardcoded hex colors replaced with design tokens (`--sp-success`, `--sp-warn`, `--sp-speaking`, `--sp-writing-ink`, `--sp-success-soft`, `--sp-warn-soft`, `--sp-canvas2`, `--sp-muted`).

**Practice Gym CSS:**
- `var(--sp-primary)` references (non-existent token) replaced with `var(--sp-brand)`.

**Shared student UI components (`src/app/shared/student-ui/`):**
- `StudentChipComponent` (`sp-chip`) — reusable toggle chip.
- `StudentBadgeComponent` (`sp-badge`) — reusable badge with variant input.

**Tests:** Angular 261 passed. Playwright 187 passed (12 new in `e2e/design-system-10jf.spec.ts`). Backend 1565 passed.

## Phase 10J — Learning Goal Context Resolver, completed (2026-06-17)

`ILearningGoalContextResolver` / `LearningGoalContextResolver` now provides a single consistent priority chain for resolving learning goal context from any `StudentProfile`. All 7 generation and ledger call sites use it. `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` is kept but no longer called externally. Generic fallback is `"general English communication"` — never workplace-biased. `WorkplaceSpecific` flag is derived from keyword detection, not assumed. `LegacyFallbackUsed` flag enables future migration tracking.
