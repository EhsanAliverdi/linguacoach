---
status: current
lastUpdated: 2026-06-10 22:00
owner: product
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-10 22:00

---

## Current priority

**No active sprint.** All planned sprints are complete. Next priority: Dynamic Pattern Selection.

---

## Most recently completed sprint

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
- **Student UX Alignment / Writing-Assumption Cleanup (Phases 1–7) — complete**

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
- **Practice Gym expansion** — Workplace Chat, Email, Gap Fill, Phrase Match unlock; dynamic session templates within Practice Gym
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
2. **Practice Gym Expansion** — unlock Workplace Chat, Email, Gap Fill, Phrase Match with real pattern routes; dynamic session template selection.
3. **Session Reflection AI** — now that evaluation outputs are stable, wire `session_reflection` AI prompt.

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.
