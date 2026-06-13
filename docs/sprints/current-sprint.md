---
status: current
lastUpdated: 2026-06-13 00:00
owner: product
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-13

---

## In progress — Activity 3-page restructure (Teach / Practice / Feedback), full-stack

5-step strangler-fig migration. **Step 1 done (2026-06-13)**: split
`ActivityLessonComponent` (876-line monolith) into a thin orchestrator shell +
3 composed, presentational page components — `ActivityTeachPageComponent`,
`ActivityPracticePageComponent`, `ActivityFeedbackPageComponent` (all under
`src/LinguaCoach.Web/src/app/features/activity/`). Orchestrator keeps all
state/signals/HTTP/recording-lifecycle logic; page components receive
`@Input()`/`@Output()` only. Zero behavior change — `ng build` clean (only
pre-existing unrelated warnings in `pattern-evaluation-result`).

**Step 2 done (2026-06-13)**: added `ActivityPagePresenter` interface under
`src/LinguaCoach.Web/src/app/features/activity/presenters/` with
`PatternBackedPresenter` + 4 `Legacy*Presenter` bridges (`LegacySpeaking`,
`LegacyVocab`, `LegacyListening`, `LegacyWriting`) selected by
`ActivityPresenterFactory.for(activity)`. `TeachViewModel`/`PracticeViewModel`
(`block`, `skillBadge`, `ctaLabel`/`ctaAction`) replace the old boolean-flag
`@Input()`s (`isVocabPractice`, `isListeningComprehension`,
`isSpeakingRolePlay`, `usesExerciseRenderer`, `rendererSkillLabel`) on
`ActivityTeachPageComponent`/`ActivityPracticePageComponent`, which now
`@switch` on `teach.block`/`practice.block`. `PatternBackedPresenter` derives
skill badge/label from `ActivityDto.interactionMode`/`activityType` so new
pattern keys need zero presenter changes. Orchestrator
(`ActivityLessonComponent`) keeps `isSpeakingRolePlay()` for internal
recording-flow branching; the rest of the old `is*()`/`usesExerciseRenderer()`/
`rendererSkillLabel()` methods were removed as dead code. 6 new presenter/
factory unit specs added. `ng build` clean (only pre-existing
`pattern-evaluation-result` warnings); `ng test` — new specs pass, pre-existing
unrelated TestBed-provider failures (Login/Onboarding/Vocabulary/Progress/
ActivityLesson specs missing `ActivatedRoute`/`HttpClient` providers) unchanged
from before this work. Zero behavior change.

Steps 3-5 migrate VocabularyPractice, ListeningComprehension, WritingScenario,
and SpeakingRolePlay onto the pattern engine with a unified `{teach, practice}`
AI content contract — each its own sprint with AI prompt calibration. See
[2026-06-13-activity-3-page-restructure-eng-plan.md](../reviews/2026-06-13-activity-3-page-restructure-eng-plan.md).

**Step 3 done (2026-06-13)** — see
[2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md](2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md).
Slice 1: added per-item `hint` parity to `gap_fill_workplace_phrase`
(`GapFillItem.hint`, "Show hint" toggle in `gap-fill.component`,
`mapGapItems` now reads `obj['hint']`) — backend `GapFillItemDto.Hint` and
the AI prompt schema already supported it. Slice 2: `ActivityGetHandler`'s
cadence-based `VocabularyPractice`/`ListeningComprehension` picks (every
4th/5th attempt, no `?type=`/`?pattern=` override) now route through
`HandlePatternKeyedAsync("gap_fill_workplace_phrase")` /
`HandlePatternKeyedAsync("listen_and_answer")` instead of the legacy
null-`interactionMode` generation — every new activity of these types now
renders via `ExerciseRendererComponent`/`PatternBackedPresenter`. Legacy
`_vocabGenerator`/`_listeningAudio` branches kept for explicit `?type=`
overrides (still used by `ActivityController`). Slice 3: confirmed
`PracticeGymGenerationJob`/`ExercisePrepareHandler` already pattern-based —
no other null-`interactionMode` paths remain. Slice 4 (delete
`LegacyVocabPresenter`/`LegacyListeningPresenter` + template branches) is a
follow-up sprint gated on production data review, per the sprint doc. `ng
build`/`dotnet build` clean.

**Steps 4 & 5 done (2026-06-13)** — see
[2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md](../reviews/2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md).
Step 4: `WritingScenario` cadence picks now route through
`HandlePatternKeyedAsync("open_writing_task")`; added `OpenWritingTask`/
`SpeakingRoleplayTurn` pattern keys, `AudioResponse` interaction mode,
2 new AI prompt pairs, and generalized `AiOpenEndedEvaluator` via
`ResolvePromptKey`. Step 5: `SpeakingRolePlay` cadence picks now route
through `HandlePatternKeyedAsync("speaking_roleplay_turn")`; the
`/speaking-attempt` endpoint is now pattern-aware (new pattern →
`IPatternEvaluationRouter`/`AiOpenEndedEvaluator`, legacy →
unchanged `SpeakingRolePlayEvaluator`). No frontend changes needed —
existing speaking recording UI/state machine is activity-type-keyed,
not pattern-key-keyed, and already serves `AudioResponse`. AI prompt
output for the 4 new prompts is unverified pending live-AI calibration
(follow-up task). All 5 migration steps now landed. `dotnet build`/
`ng build` clean, 51/51 unit tests pass.

Next: live-AI calibration pass for the 4 new prompts, then update
`exercise-pattern-library.md`/`learning-activity-engine.md` roadmap
tables per the Step 4/5 review's implementation tasks.

## Most recently completed sprint

**Practice Gym cache race condition fix** - complete (2026-06-12)

A full code/docs audit found `ActivityGetHandler.TryAssignReadyPracticeCacheAsync` could
let two concurrent `GET /api/activity/next?pattern=...` requests claim the same `Ready`
`PracticeActivityCache` row, returning the same `LearningActivity` to both. Added `xmin`
concurrency token to `PracticeActivityCache` (mirrors existing `LearningPath` config) and
retry-with-exclusion on `DbUpdateConcurrencyException`, falling back to the next ready
row or on-demand generation. 483 unit + 434 integration tests pass. See
`docs/reviews/2026-06-12-practice-gym-cache-race-condition-fix-engineering-review.md`.

**Lesson and Practice classroom alignment** - complete (2026-06-12)

Today's Lesson now consumes ready buffered `LearningSession` rows before falling
back to lazy session generation. The lesson page no longer auto-prepares later
steps just because the student opened the page, selected a step, or completed the
previous step. Existing prepared steps still open directly.

Practice Gym now uses the same classroom framing as Today's Lesson: teach first,
practice with a supported exercise pattern, then show feedback after submit. The
Practice Gym page separates skill practice (Listening, Reading, Speaking,
Writing), vocabulary class, exercise type practice, and future live role play.
Only implemented patterns are active; Reading, vocabulary queue, live AI role
play, and future exercise types remain disabled.

Background Practice Gym caching now has both parts: `PracticeGymBufferRefillJob`
queues pending cache rows for eligible active students, and
`PracticeGymGenerationJob` materializes those rows into ready `LearningActivity`
records. `GET /api/activity/next?pattern=...` consumes ready cache entries before
generating on demand. Phrase-match and gap-fill generation prompts now require a
lesson component before practice, and lesson batch planning is constrained to the
active pattern library instead of defaulting every skill to writing.

**Lesson batch materialization fix** - complete (2026-06-12)

Production logs showed AI lesson planning succeeding, then
`LessonBatchGenerationJob` failing while saving the first generated session with
`DbUpdateConcurrencyException`. The failed save also left the same `DbContext`
dirty, so marking the batch failed could throw again and Quartz reported an
unhandled job exception. Root cause: session/activity `GenerationJobItem` rows
created after the original batch save were attached only through the aggregate's
private item collection, so EF could treat them as updates to missing rows
instead of inserts. The job now explicitly inserts those items, avoids tracked
path/module state during generated module lookup, and clears failed tracked
state before marking a materialization failure. Added integration coverage that
executes `LessonBatchGenerationJob` with a fake lesson-plan provider and verifies
a completed batch plus ready sessions. See
`docs/reviews/2026-06-12-lesson-batch-materialization-fix-engineering-review.md`.

**Admin responsive header polish** — complete (2026-06-12)

`/admin` now keeps dashboard grids tablet-safe between 900px and 930px, where the
desktop sidebar first appears. The admin header now shows only the avatar button;
email and role moved into the avatar flyout menu. Related layout rules are
documented in `docs/architecture/frontend-layout-system.md`.

**Admin stuck batch cancellation** — complete (2026-06-12)

Admins can now cancel queued/running lesson-generation batches from
`/admin/integrations` instead of running one-off SQL. The action marks the batch
`Failed` with the safe reason `Cancelled by admin.` and the background job checks
for that state before and during session materialization so it does not overwrite
an admin cancellation. Recent batches now show a Cancel button for active rows and
the existing Failure column shows the cancellation reason.

**Lesson batch generation concurrency fix** — complete (2026-06-12)

Duplicate concurrent batch triggers for the same student caused
`DbUpdateConcurrencyException` and left `GenerationBatch` rows stuck in
"Running" forever. Admin endpoint now returns 409 if a batch is already
running for the student; job marks itself `Failed` on any unhandled error
during materialization instead of getting stuck. Existing stuck rows can now be
cancelled from Admin Integrations rather than fixed by direct SQL. See
`docs/reviews/2026-06-12-admin-stuck-batch-cancel-engineering-review.md`.

**Quartz JobDataMap string-only fix (background lesson generation)** — complete (2026-06-12)

`LessonBatchGenerationJob.TriggerAsync` stored non-string values in `JobDataMap`,
which throws `JobPersistenceException` under Quartz's `UseProperties = true`
Postgres job store. This broke both the admin "Generate lessons now" button AND
the background buffer-refill pipeline — explaining why all lessons were
generated on-the-fly ("Preparing your lesson...") instead of pre-generated. See
`docs/reviews/2026-06-12-quartz-jobdatamap-string-fix-engineering-review.md`.

**Admin "Generate lessons now" button fix** — complete (2026-06-12)

Button gave no feedback on click (success or error). Frontend now shows a
confirmation with queued session count, or the server error message. See
`docs/reviews/2026-06-12-admin-generate-lessons-button-fix-engineering-review.md`.

**AI Config Overhaul / No-Fallback Rule / Journey Fix** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-11-ai-config-no-fallback-journey-fix-sprint.md`

Triggered by: post-QA audit corrections from product owner (2026-06-11).
Audit report: `docs/testing/deployed-student-e2e-audit-2026-06-11.md`

An audit on 2026-06-12 found that Tracks 1-4 and most of Track 5 had already been
delivered under other sprint names (T36 AiConfigCategories migration, Exercise UX /
Admin Polish, Real TTS). The one genuinely outstanding item — BUG-005 (dashboard streak
showing "--") — was fixed in this pass: `DashboardResult.StreakDays` computed server-side
from consecutive days with an `ActivityAttempt`, wired into the dashboard stat grid and
the header streak pill. See the sprint doc's "Status update" and "Streak implementation"
sections for full detail.

### What this sprint addressed

1. **No-fallback rule** — All AI failures return 503 + "Service not available" UI. No SystemFallback content ever shown to students.
2. **Admin AI Config overhaul** — Replace 12+ individual feature-key rows with 4 LLM category cards (Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path) + 2 independent TTS cards (Listening TTS, Placement TTS).
3. **Journey page fix** — Replace old LearningPath module cards with LearningSession history (date-grouped, per-step scores).
4. **Audio / TTS 503 handling** — Audio endpoint returns clear 404 when TTS not configured; frontend shows graceful failure. Activity audio playback fetches protected audio with Angular `HttpClient` and renders a `blob:` URL.
5. **Lower-severity QA bugs** — Mobile activity blank page, phrase-match 400, streak "--" display, sidebar layout clipping.

---

## Most recently completed sprint

**Exercise Submission Scoring Bug (CRITICAL)** — complete (2026-06-12)

See full review: `docs/reviews/2026-06-12-exercise-submission-scoring-bug-engineering-review.md`

Triggered by a production report: `gap_fill_workplace_phrase` submissions scored 0 with
every `itemResults[].studentAnswer: null`, despite correct `acceptedAnswers` being
present. Escalated by product owner to "every exercise" (not just gap fill).

Root cause: frontend item-id fallbacks (`String(index + 1)`, 1-indexed, unprefixed) did
not match backend deterministic-evaluator key conventions (`gap_{i+1}`, `phrase_{i}`/
`meaning_{i}` 0-indexed).

Fixed:
- `gap_fill_workplace_phrase` — `mapGapItems` fallback id now `gap_${index + 1}`.
- `phrase_match` — two-id-scheme redesign (`phrase_${index}` / `meaning_${index}`) across
  `exercise-renderer.component.ts`, `MatchingPair` interface, `MatchingPairsComponent`,
  and `matching-pairs.component.html`.

Not changed (verified lower risk / separate issue, see review):
- `listen_and_gap_fill` — `ListenAndGapFillItemDto.Id` exists in contract, fallback only
  exercised if AI omits `id`.
- `listen_and_answer` — `QuestionId` mismatch affects per-question feedback labels only,
  not overall AI-judged score.

AI-evaluated patterns (email_reply, teams_chat, spoken_response) were unaffected —
`SubmittedAnswerJson` is forwarded raw to the AI prompt.

Tests: `dotnet test` 480 unit + 430 integration passed; `npm run build` passed.

The separate "lesson structure" complaint (What we learn / Practice / Feedback / Redo →
next, for both Lessons and Practice) raised alongside this bug report is a
product/architecture item — not addressed here, needs its own planning pass with the
product owner.

---

## Most recently completed sprint

**Today's Lesson button / lazy LearningPath generation fix (CRITICAL)** — complete
(2026-06-12)

See full review: `docs/reviews/2026-06-12-todays-lesson-button-fix-engineering-review.md`

Root cause: `SessionGeneratorService` threw when a `CourseReady` student had no active
`LearningPath` (the legacy `/activity` flow was the only place a path got lazily
created). `SessionsController.Today` returned 400, and the dashboard silently
swallowed the error, leaving "Start today's lesson" with a null link — button did
nothing, no session/lesson ever generated for these students.

Fixed: `SessionGeneratorService` now lazily generates a `LearningPath` via
`ILearningPathGenerator` (same handler `ActivityGetHandler` already uses) when none
exists, mirroring the existing fallback there. Dashboard now surfaces session-load
errors instead of swallowing them.

Quartz/background-job config investigated as a possible cause — found correct,
no change needed.

Follow-up implemented same day: `PlacementService` now generates the student's
`LearningPath` proactively right after `CourseReady`, via `ILearningPathGenerator`
(best-effort, never blocks placement). The lazy fallback remains as a safety net
for pre-existing affected students.

Tests: 482 unit + 430 integration passing. `npm run build` passed.

---

## Current priority

**Adaptive Learning Foundation** Phase 2 (numeric `StudentSkillProfile` scores) is done
(2026-06-12) — see `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.
`StudentSkillProfile.ScorePercent` (0-100) replaces the persisted `IsWeak` boolean;
`IsWeak` is now derived (`ScorePercent < 50`). Migration `T42_StudentSkillScorePercent`
backfills existing rows. 482 unit + 430 integration tests pass.

Phase 3 planning pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-plan.md`. Most of Phase 3
(Practice rendering, redo/next loop, per-attempt AI feedback) already exists.

Phase 3 P1 done (2026-06-12): AI evaluation prompts (email_reply, teams_chat,
spoken_response, listen_and_answer) now receive the student's current
`StudentSkillProfile.ScorePercent` for the relevant skill, so `coachSummary` can be
grounded in student progress instead of generic. 482 unit + 430 integration tests pass.

Phase 3 P2 scoping pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-p2-scoping.md`. Full
"What we learn" grammar/vocab/phrases card remains deferred (cross-cutting AI prompt
work). Task 1 (surface existing `teachingNote` as `[goal]` for GapFill/MatchingPairs) and
P2b task 1 (surface learningGoal/skillFocus/targetVocabulary for email_reply/teams_chat)
both implemented same day, frontend-only. See sprint doc's "Phase 3 P2"/"P2b" sections.
suggestedPhrases for spoken_response was already implemented (confirmed
2026-06-12, no change needed). Remaining: targetGrammarPoint (large,
cross-cutting, no schedule).

---

## Adaptive Learning Foundation — vocabulary extraction widened to all activity patterns

**Adaptive Learning Foundation** — planning complete (2026-06-12); first implementation
item (vocabulary extraction) done (2026-06-12)

See `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.

Reviewed and sequenced the remaining tracks (10-14) from the 2026-06-12 product owner
brainstorm: Adaptive Onboarding & Staged Assessment, Configurable Onboarding/Placement,
Multi-Course/Enrolment Model, Estimated Known Words. All confirmed already recorded in
`docs/backlog/product-backlog.md`. Recommended sequencing: vocabulary extraction first
(already speced, independent), then numeric `StudentSkillProfile` scores, then staged
assessment architecture review, then configurable onboarding, then multi-course
(dedicated `/plan-eng-review` required). Three open product questions recorded — see
sprint doc.

Per product owner correction, vocabulary extraction was implemented as a cross-cutting
engine: `VocabularyExtractionService` now extracts from any pattern-evaluated activity
that produces AI `Corrections` (email reply, workplace chat, listen-and-answer, spoken
response), not only legacy writing attempts. Deterministic patterns (gap fill, phrase
match) are unaffected — see implementation note in the sprint doc above. Does not change
current implementation priority below.

---

## Most recently completed sprint

**Exercise UX / Admin Polish** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-12-exercise-ux-admin-polish-sprint.md`

### What was done

All 7 phases shipped on 2026-06-12:

- **Phase 1** — Verified attempt/retry integrity; fixed a pre-existing gap-fill submission shape bug (frontend was sending the wrong JSON shape, causing all answers to score as incorrect).
- **Phase 2** — Workplace Chat: `ChatReplyContent` gained a distinct `learningGoal` field (separate from tone guidance), shown via a `chat-reply-goal` UI element. `activity_evaluate_teams_chat_simulation` prompt updated to evaluate goal-reaching, tone, clarity, and clarification-seeking.
- **Phase 3** — Email Reply: new `InteractionMode.EmailReply`, self-healing `ExercisePatternSeeder`, new `EmailReplyComponent` renderer with subject + body fields, `SubmittedAnswerJson` shape `{ subject, body }`, evaluator prompt updated.
- **Phase 4** — Shared Lesson → Practice → Evaluate framing: new `ExerciseLessonIntroComponent` ("Goal" display) applied to `GapFillComponent`, `MatchingPairsComponent`, `AudioAndFreeTextComponent`, `AudioAndGapFillComponent`. Chat Reply, Email Reply, and Free Text Entry already had equivalent goal displays.
- **Phase 5** — Admin nav: "AI Usage" moved from the (now-removed) "Analytics" group into "AI System", alongside AI Config and Prompts.
- **Phase 6** — Design-token consistency audit of all sprint-touched components — already aligned with `.sp-*` tokens, no changes needed.
- **Phase 7** — Docs close-out (this entry).

### Key constraints preserved

- Lesson → Practice → Evaluate framing applied only to the 6 currently-active exercise renderers, not retrofitted across the 40+ unimplemented patterns in the library.
- No backend changes beyond the `EmailReply` interaction mode (additive, append-only enum).

### Final test results

```
dotnet test tests/LinguaCoach.UnitTests:  477 passed
npm run build:                            passed (0 new errors/warnings)
```

---

## Previously completed sprint

**Real TTS / Placement Onboarding Gap / Today Session Card** — complete (2026-06-11)

See full sprint plan: `docs/sprints/2026-06-10-tts-placement-today-sprint.md`

### What was done

All tracks shipped on 2026-06-11:

- **Track 1 (Real TTS)** — `VoiceName` added to `AiProviderConfig` (T35 migration). `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; never throws. `TtsProviderResolver` reads `tts.listening` / `tts.placement` feature keys from DB, returns `FakeTextToSpeechService` (provider=`fake`) or `OpenAiTextToSpeechService` (provider=`openai`). `ListeningAudioService` and `PlacementAudioService` now resolve TTS at runtime. `DefaultAiSeeder` seeds both keys as `fake/fake/fake` (idempotent). Admin UI updated with voice name field and fake provider support.
- **Track 2 (Onboarding experience step)** — `PATCH /api/onboarding/experience` endpoint added. `StudentProfile.SetExperienceContext()` bypasses state machine. New `step5-experience` Angular component inserted between step-4 and placement. Step-4 now shows "Step 4 of 5" and navigates to step-5. Existing completed students can call the endpoint without error. Non-blocking — API failure still navigates to placement.
- **Track 3 (Today session card)** — previously completed in Practice Gym Activation sprint; confirmed and skipped.

### Key constraints preserved

- `FakeTextToSpeechService` remains default; `dotnet test` does not require `OPENAI_API_KEY`
- OpenAI TTS only activates when admin sets `tts.*` feature key provider to `openai`
- Existing completed students not broken by new experience step
- Practice Gym behaviour unchanged; Pronunciation remains Coming soon

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed (0 errors)
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

---

## Previously completed sprint

**Practice Gym Activation / Pattern-Based Free Practice** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-practice-gym-activation-sprint.md`

### What was done

All phases shipped on 2026-06-10:

- **Phase 2 (backend)** — `GET /api/activity/next` extended with `?pattern=<key>`. `GetNextActivityQuery` has `PreferredPatternKey`. `ActivityGetHandler.HandlePatternKeyedAsync` validates pattern key, loads definition, calls AI with `OverridePromptKey`, sets `ExercisePatternKey` on the created `LearningActivity`. `AiActivityGeneratorHandler` now supports `VocabularyPractice` when pattern-driven. Invalid pattern key returns 400.
- **Phase 3 & 4 (frontend)** — `ActivityService.getNext` accepts `patternKey`. `ActivityLessonComponent` reads `?pattern=` and passes it to the service. Practice Gym activates Phrase Match, Gap Fill, Email, and Workplace Chat as `<a routerLink>` with `pattern=` and `returnTo=/practice`.
- **Phase 5 (return flow)** — `returnTo=/practice` embedded in all four new card links. Existing `nextActivity()` / `backToDashboard()` logic handles it unchanged.
- **Phase 6 (progress verification)** — confirmed: `ActivitySubmitHandler` records `ActivityAttempt` for all pattern types; `PatternSkillUpdateService` runs after each submission; no progress on card open.
- **Phase 7 (tests + docs)** — 8 new backend integration tests; 6 new Playwright tests (4 card activation, Pronunciation still coming soon, Speaking no pronunciation claim). All existing tests still pass.

### Key constraints preserved

- Pronunciation card remains Coming soon
- No fake pronunciation claims
- PatternEvaluationRouter not bypassed
- No new endpoints or routes added
- No seed data deleted, no real user data deleted

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      167 passed
```

---

## Previously completed sprint

**Student UX Alignment / Writing-Assumption Cleanup** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-student-ux-alignment-writing-assumption-cleanup-sprint.md`

### What was done

All 7 phases shipped on 2026-06-10:

- **Phase 2** — Navigation labels/routes: sidebar and mobile nav now show **Today, Journey, Practice, Progress, Profile**. Dashboard label removed. Vocabulary removed from top-level nav. `/journey` route added. `/practice` route added.
- **Phase 3** — Today page alignment: heading "Today's Lesson" added. "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Phase 4** — Journey mixed-skill cleanup: page heading "Learning Journey" added. Memory fallback "workplace writing" → "workplace English". "Continue practising" CTA replaced with safe CTAs.
- **Phase 5** — Practice Gym MVP at `/practice`: functional cards for Vocabulary, Listening, Writing, Speaking. Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. No auto-start on load.
- **Phase 6** — Playwright fixture copy cleanup: generic writing/email-only fixture language updated to mixed-skill workplace English across `core-flow-smoke.spec.ts`, `disabled-actions-cleanup.spec.ts`, `lesson-activity-wiring.spec.ts`, `admin-screenshots.spec.ts`. Valid WritingScenario and email_reply test coverage preserved.
- **Phase 7** — Documentation cleanup: `current-product-state.md`, `current-sprint.md`, `docs/architecture/README.md` updated. Older sprint docs marked historical. Sprint doc closed.

### Key constraints preserved

- No real user data deleted
- No seed rows deleted (`WritingScenarioSeeder`, `LearningActivitySeeder` unchanged)
- Writing and Email remain valid activity types
- `/my-path` still works (backwards compatible with `/journey`)
- No backend files changed in this sprint

### Final test results

```
dotnet test:     865 passed (451 unit + 414 integration) — unchanged
npm run build:   passed
Playwright:      165 passed (21 new Practice Gym tests + 9 new Journey tests)
```

---

## Completed sprints

- Admin UX / Student Management / AI Config Cleanup — complete
- Today's Lesson / Learning Session (Phases 1–5B) — complete
- Exercise Pattern Engine — complete
- Pattern Evaluation Engine (Phases 1–7) — complete
- Student UX Alignment / Writing-Assumption Cleanup (Phases 1–7) — complete
- Real TTS / Placement Onboarding Gap / Today Session Card — complete
- **Exercise UX / Admin Polish (Phases 1–7) — complete**

---

## Current state

All four activity types are implemented. Placement Assessment is complete. The full evaluation stack is live end-to-end. Student nav model is aligned:

- Today (`/dashboard`) is the student home page — Today's Lesson is the primary CTA
- Journey (`/journey`, `/my-path`) shows the learning path with mixed-skill framing
- Practice (`/practice`) is the Practice Gym MVP — free practice by skill or exercise type
- Progress and Profile unchanged
- Pattern-aware evaluators route by `MarkingMode`: `ExactMatch`, `KeyedSelection`, `AiStructured`, `AiOpenEnded`, `NoMarking`
- `StudentSkillProfile` updated from evaluation skill impacts after every pattern attempt
- Compact memory signals from evaluation fed into `StudentLearningMemory`
- Pattern-aware result UI with 6 branches

Session reflection (`GET /api/sessions/{id}/reflection`) is a 501 stub — deferred.

---

## Deferred

- **Dynamic pattern selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history
- **Dynamic Practice Gym session templates** — configurable session templates within Practice Gym (e.g. "30-min vocab session")
- Session reflection AI prompt (`session_reflection`) — requires stable session completion signal
- IFileStorageService / MinIO — not blocking deployment at current scale
- Admin lifecycle reset tools
- Call Mode / Pronunciation scoring
- Real STT provider
- OpenAI TTS (advanced voices)
- Email delivery, payments, organisations

---

## Next recommended work

1. **Dynamic Pattern Selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
   Scoping pass done (2026-06-12): see
   `docs/reviews/2026-06-12-dynamic-pattern-selection-scoping.md`. Recommended first
   slice: per-slot pattern pools + last-N-session repetition avoidance in
   `SessionGeneratorService`/`SessionDurationTemplates`.
2. **Dynamic Practice Gym session templates** — configurable multi-exercise sessions within Practice Gym.
3. **Session Reflection AI** — now that evaluation outputs are stable, wire `session_reflection` AI prompt.

---

## Planned future sprint

**Lesson Buffer / MinIO / Background Generation** - planned.

See: `docs/sprints/2026-06-11-lesson-buffer-minio-background-generation-sprint.md`

This sprint covers pre-generating the next 5-10 lessons, pre-generating a configurable 5-10 Practice Gym exercises per type/pattern, storing audio assets in MinIO, signed URL playback, Quartz.NET background generation jobs, Admin Integrations for MinIO health/configuration, and cached Practice Gym generation.

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.
