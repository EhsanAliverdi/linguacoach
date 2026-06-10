---
status: current
lastUpdated: 2026-06-10 18:00
owner: product
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-10 18:00

---

## Current priority

**Pattern Evaluation Engine sprint is complete.**

See full sprint plan: `docs/sprints/2026-06-10-pattern-evaluation-engine-sprint.md`

Recommended next: **Dynamic Pattern Selection** — choose Today’s Lesson patterns from weak skills, CEFR, duration, and recent repetition.

---

## Completed sprints (recent)

- Admin UX / Student Management / AI Config Cleanup — complete
- Today’s Lesson / Learning Session (Phases 1–5B) — complete
- Exercise Pattern Engine — complete
- Pattern Evaluation Engine (Phases 1–7) — complete

---

## Current state

All four activity types are implemented. Placement Assessment is complete. The full evaluation stack is live end-to-end:

- Dashboard shows Today’s Lesson card (start / resume / review states)
- `/lesson/:sessionId` step-by-step lesson page with exercise list and progress bar
- Backend `/prepare` endpoint generates activities on demand per exercise step
- Activities open with `activityId` + `returnTo` nav back to lesson
- Pattern-aware evaluators route by `MarkingMode`: `ExactMatch`, `KeyedSelection`, `AiStructured`, `AiOpenEnded`, `NoMarking`
- `StudentSkillProfile` updated from evaluation skill impacts after every pattern attempt
- Compact memory signals from evaluation fed into `StudentLearningMemory` (best-effort, never blocks)
- Pattern-aware result UI with 6 branches: MatchingPairs, GapFill, Chat/Email, ListenAndAnswer, SpokenResponse, ReadOnly
- **865 dotnet tests pass** (451 unit + 414 integration); **111 Playwright tests pass**

Session reflection (`GET /api/sessions/{id}/reflection`) is a 501 stub — deferred.

---

## Pattern Evaluation Engine — complete

All 7 phases shipped on 2026-06-10:

- Phase 1: Contracts + persistence (`PatternEvaluationResult`, `ActivityAttempt` evaluation fields, migration T34)
- Phase 2: Deterministic evaluators (`ExactMatchEvaluator`, `KeyedSelectionEvaluator`, `NoMarkingEvaluator`)
- Phase 3: Pattern router + attempt integration (`IPatternEvaluationRouter`, `ActivitySubmitHandler` wired)
- Phase 4: AI evaluators (`AiStructuredEvaluator`, `AiOpenEndedEvaluator`, `ParseAndNormalise` with markdown-fence fix)
- Phase 5: Skill + memory updates (`PatternSkillUpdateService`, compact memory packet, best-effort wiring)
- Phase 6: Frontend result UI (`PatternEvaluationResultComponent`, 6 result branches, legacy path preserved)
- Phase 7: Documentation + QA (all docs updated, full suite verified)

Architecture reference: `docs/architecture/exercise-pattern-library.md`, `docs/architecture/learning-activity-engine.md`

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

## Next recommended work

1. **Dynamic Pattern Selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
2. **Practice Gym Separation** — let students choose skill / pattern / focus outside Today's Lesson.
3. **Session Reflection AI** — now that evaluation outputs are stable, wire `session_reflection` AI prompt.

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.
