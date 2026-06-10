---
status: current
lastUpdated: 2026-06-10 11:02
owner: product
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-10 11:02

---

## Current priority

**Exercise Pattern Engine sprint is complete.**

See full sprint plan: `docs/sprints/2026-06-10-exercise-pattern-engine-sprint.md`

Today’s Lesson / Learning Session sprint is complete (see `docs/sprints/2026-06-10-today-lesson-learning-session-sprint.md`).

---

## Completed sprints (recent)

- Admin UX / Student Management / AI Config Cleanup — complete
- Today’s Lesson / Learning Session (Phases 1–5B) — complete

---

## Current state

All four activity types are implemented. Placement Assessment is complete. The full `LearningSession` → `SessionExercise` → `LearningActivity` → `ActivityAttempt` stack is live end-to-end:

- Dashboard shows Today’s Lesson card (start / resume / review states)
- `/lesson/:sessionId` step-by-step lesson page with exercise list and progress bar
- Backend `/prepare` endpoint generates activities on demand per exercise step
- Activities open with `activityId` + `returnTo` nav back to lesson
- 90 Playwright e2e tests pass; 645 dotnet tests pass

Session reflection (`GET /api/sessions/{id}/reflection`) is a 501 stub — deferred to after Exercise Pattern Engine.

---

## Exercise Pattern Engine status

The named `ExercisePattern` keys now drive backend prepare/generation and frontend rendering for the 8 MVP patterns:

- `ActivityDto.interactionMode` selects the Angular renderer through `ExerciseRendererComponent`
- `ActivityDto.exercisePatternKey` links generated activities back to their pattern
- Pattern-keyed activities expose bounded `contentJson` for renderer dispatch; legacy listening activities do not expose raw answer-bearing JSON
- MatchingPairs, GapFill, AudioAndFreeText, AudioAndGapFill, ChatReply, ReadOnly, and FreeText renderers are wired
- Full frontend regression suite passes: 97/97 Playwright tests
- Backend tests pass: 762 total (380 unit + 382 integration)

Architecture reference: `docs/architecture/exercise-pattern-library.md`

---

## Recommended next sprint

**Pattern Evaluation Engine** is the best next sprint.

Reason: the product now has pattern definitions, pattern-aware generation, and interaction-mode renderers. The largest remaining gap is that `MarkingMode` is not yet fully expressed in the attempt/evaluation flow:

- deterministic marking for `ExactMatch` and `KeyedSelection`
- structured AI marking for `AiStructured`
- pattern-specific result summaries for MatchingPairs, GapFill, audio answers, and ChatReply
- safer storage of structured answer JSON and evaluation metadata

Dynamic Pattern Selection and Practice Gym Separation remain valuable, but they should build on reliable per-pattern marking.

---

## Deferred

- Session reflection AI prompt (`session_reflection`) — requires stable session completion signal
- Practice Gym separation
- Dynamic pattern selection by weak skill / CEFR / recent repetition
- IFileStorageService / MinIO — not blocking deployment at current scale
- Admin lifecycle reset tools
- Call Mode / Pronunciation
- Real STT provider
- OpenAI TTS (advanced voices)
- Email delivery, payments, organisations

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.
