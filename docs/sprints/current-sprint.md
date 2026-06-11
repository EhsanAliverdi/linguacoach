---
status: current
lastUpdated: 2026-06-12 12:00
owner: product
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-12

---

## Current priority

**AI Config Overhaul / No-Fallback Rule / Journey Fix** — see full sprint plan:
`docs/sprints/2026-06-11-ai-config-no-fallback-journey-fix-sprint.md`

Triggered by: post-QA audit corrections from product owner (2026-06-11).
Audit report: `docs/testing/deployed-student-e2e-audit-2026-06-11.md`

### What this sprint addresses

1. **No-fallback rule** — All AI failures return 503 + "Service not available" UI. No SystemFallback content ever shown to students.
2. **Admin AI Config overhaul** — Replace 12+ individual feature-key rows with 4 LLM category cards (Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path) + 2 independent TTS cards (Listening TTS, Placement TTS).
3. **Journey page fix** — Replace old LearningPath module cards with LearningSession history (date-grouped, per-step scores).
4. **Audio / TTS 503 handling** — Audio endpoint returns clear 404 when TTS not configured; frontend shows graceful failure. Gemini TTS must use Gemini TTS models (`*-tts`), not normal Gemini LLM models. Activity audio playback must fetch protected audio with Angular `HttpClient` and render a `blob:` URL, not bind the protected API URL directly to `<audio src>`.
5. **Lower-severity QA bugs** — Mobile activity blank page, phrase-match 400, streak "--" display, sidebar layout clipping.

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
