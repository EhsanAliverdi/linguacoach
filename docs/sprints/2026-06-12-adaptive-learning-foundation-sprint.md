---
status: current
lastUpdated: 2026-06-12 00:00
owner: product
supersedes:
supersededBy:
---

# Sprint: Adaptive Learning Foundation (Planning Only)

Status: **Planning only — no implementation this sprint**

Related brainstorm: product owner 15-track brainstorm (2026-06-12). Tracks 1-9 and 15 were
already delivered by the **Exercise UX / Admin Polish** sprint
(`docs/sprints/2026-06-12-exercise-ux-admin-polish-sprint.md`). This sprint covers the
remaining tracks (10-14), which are forward-looking architecture/product direction, not
ready for implementation.

---

## Product goal

Define a coherent direction for adaptive onboarding, ongoing skill diagnostics, multi-course
enrolment, and vocabulary-size estimation — so future sprints can implement these
incrementally without conflicting designs. No code changes in this sprint.

---

## In scope (planning artifacts only)

1. Confirm and cross-link the four related backlog items already recorded in
   `docs/backlog/product-backlog.md`:
   - Adaptive Onboarding & Staged Assessment
   - Configurable Onboarding and Placement Assessment
   - Multi-Course / Enrolment Model
   - Estimated Known Words
2. Establish a recommended sequencing/dependency order between these items.
3. Identify which existing architecture docs each item will need to extend
   (`placement-assessment-model.md`, `student-learning-memory.md`,
   `course-session-learning-model.md`).
4. Record open product questions that need a decision before any implementation sprint
   can be scoped.

## Out of scope

- Any code, schema, migration, or prompt changes.
- New entities (`Enrolment`, `StudentVocabularyItem`, configurable question schemas).
- Architecture review docs (`/plan-eng-review`) — those happen when an item is picked up
  for implementation.

---

## Track summary and current backlog status

### Track 10/12 — Adaptive Onboarding & Staged Assessment
Backlog: "Adaptive Onboarding & Staged Assessment" (`Not started`).

- Stage 1 (quick onboarding) and Stage 2 (adaptive A1-starting placement) extend the
  existing `placement-assessment-model.md` flow rather than replacing it.
- Stage 3 (ongoing per-skill diagnostic percentages) extends `StudentSkillProfile`
  (`student-learning-memory.md`) — likely needs a numeric 0-100 score per skill in
  addition to the current `is_weak` boolean (already flagged as a deferred item under
  "Student learning memory" in the backlog).
- Stage 4 (adaptive course generation from diagnostics) is largely already the job of
  `AdaptivePathGeneratorHandler` — this stage is an enhancement to its inputs, not a new
  pipeline.

**Dependency:** Stage 3 (numeric skill scores) should land before Stage 4 claims to be
"diagnostic-driven" — otherwise it is cosmetic.

### Track 11 — Configurable Onboarding and Placement Assessment
Backlog: "Configurable Onboarding and Placement Assessment" (`Not started`, P1 after
Placement MVP stabilisation).

- This is the admin-tooling counterpart to Track 10. Staged assessment (Track 10) defines
  *what* questions should exist conceptually; this track makes them editable without code.
- Recommended sequencing: do not build configurability for a question model that is still
  changing. Track 10's staged model should stabilise first, even informally, before
  building admin CRUD around it.

### Track 13 — Multi-Course / Enrolment Model
Backlog: "Multi-Course / Enrolment Model" (`Not started`).

- Conflicts with the current single-track `LearningPath` / `StudentProfile` model.
- Per AGENTS.md scope rules, this requires a dedicated `/plan-eng-review` before any
  implementation — it touches session generation, AI context building, and progress
  tracking across the board.
- Naming/copy guidance already captured: avoid hard-coding "workplace only" in new UI;
  keep "Work English" as the current default course name so "Casual English" can be added
  later without renames.

### Track 14 — Estimated Known Words
Backlog: "Estimated Known Words" (`Not started`).

- Depends on `StudentVocabularyItem` / vocabulary extraction
  (backlog: "Vocabulary extraction from writing attempts", status `Planned`, not started).
- Of all four tracks, this has the clearest incremental path: vocabulary extraction is
  already speced and could be implemented independently of Tracks 10-13.

---

## Recommended sequencing

```
1. Vocabulary extraction from writing attempts (already speced, independent)
       -> unlocks Track 14 (Estimated Known Words) as a near-term follow-up

2. Numeric StudentSkillProfile scores (small extension to existing memory model)
       -> unlocks Track 10 Stage 3 (ongoing diagnostic percentages)

3. Track 10 (Adaptive Onboarding & Staged Assessment) — architecture review required
       -> defines the question/assessment model

4. Track 11 (Configurable Onboarding/Placement) — built against the model from step 3

5. Track 13 (Multi-Course / Enrolment Model) — largest change, dedicated
   /plan-eng-review required, independent of 1-4 but should not be started
   concurrently with Track 10/11 (both touch onboarding/placement flow)
```

Items 1 and 2 are small enough to be candidate "next sprint" implementation work without
further architecture review. Items 3-5 each need their own scoping pass.

---

## Open product questions (need owner decision before implementation scoping)

1. For Track 10 Stage 2 (adaptive placement starting at A1), does this replace the current
   fixed 6-section placement flow, or run as an alternative path selected by Stage 1
   self-check answers?
2. For Track 13, is "Casual English" content actually planned for near-term pilot use, or
   is this purely architectural headroom? This affects how soon the `Enrolment` entity
   work is worth doing.
3. For Track 14, what is the acceptable estimate granularity for the pilot — word-count
   ranges only, or also CEFR-correlated ranges (e.g. "B1 typically 1500-2500 words")?

---

## Implementation note (2026-06-12, post-planning)

Per product owner correction: vocabulary extraction must not be conceptually or
technically limited to writing attempts — it is a cross-cutting "vocabulary engine"
across all activity types/exercise patterns that produce AI feedback.

Implemented:

- `VocabularyExtractionService.BuildExtractionContext` (`src/LinguaCoach.Infrastructure/Vocabulary/VocabularyExtractionService.cs`)
  now parses both the legacy `changes` feedback shape (writing attempts) and the
  `PatternEvaluationResult` `corrections` shape (category/original/suggestion/explanation),
  normalising both into the same `feedbackChanges` AI-prompt input.
- `ActivitySubmitHandler.HandlePatternEvaluationAsync` (`src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs`)
  now calls `_vocabExtraction.ExtractAsync(...)` after the memory update, gated on
  `evalResult.Corrections.Count > 0`. This covers `email_reply`, `teams_chat_simulation`,
  `listen_and_answer`, `spoken_response_from_prompt` (AiStructured/AiOpenEnded patterns).
- Deterministic patterns (`gap_fill_workplace_phrase`, `phrase_match` —
  ExactMatch/KeyedSelection/NoMarking) never populate `Corrections`, so this gate
  guarantees no AI call for them — preserving the no-AI-for-deterministic-evaluators rule.
- New unit tests: `tests/LinguaCoach.UnitTests/Vocabulary/VocabularyExtractionContextTests.cs`
  (3 tests covering legacy shape, pattern-corrections shape, and empty-feedback shape).

Tests: `dotnet test` — 480/480 unit, 430/430 integration, all passing.

This unblocks Track 14 (Estimated Known Words) for vocabulary extracted from any
activity type, not only writing.

## Tasks

- [x] Phase 0: Sprint doc created, cross-referencing existing backlog items
- [x] Confirm no implementation occurred (planning-only sprint)
- [x] Update `current-sprint.md` to record this as a completed planning pass
- [x] Implement cross-cutting vocabulary extraction (see implementation note above)
- [ ] Phase 2: Numeric `StudentSkillProfile` scores (sequencing item 2)
- [ ] Phase 3: Lesson/Practice structure redesign (see note below)

---

## Phase 3 (new, 2026-06-12) — Lesson/Practice structure redesign

Per product owner: the "What we learn / Grammar / Vocab / Phrases → Practice → Feedback
→ Redo→next" lesson structure complaint (raised alongside the
gap_fill/phrase_match scoring bug, see
`docs/reviews/2026-06-12-exercise-submission-scoring-bug-engineering-review.md`) is part
of the Adaptive Learning Foundation, as its own phase — not a separate sprint.

Scope (to be planned, not yet implemented):

- Each Lesson (and each Practice session) should present, per exercise/topic:
  1. **What we learn** — explicit framing of the grammar/vocab/phrases being taught
     (extends the existing "Lesson → Practice → Evaluate" goal framing from Exercise UX
     Phase 4, which currently shows only a single "Goal" line).
  2. **Practice** — the exercise itself (existing renderers).
  3. **Feedback** — not generic/random; grounded in the lesson content AND the student's
     `StudentLearningMemory` / `StudentSkillProfile` characteristics.
  4. **Redo / Next** — a redo loop that leads into the next step, for both Lessons and
     Practice flows.

This phase should be planned (architecture/UX review) before implementation — likely
depends on Phase 2 (numeric skill scores) for the "based on Student characteristic"
feedback requirement. Sequencing: Phase 2 before Phase 3.

## Risks / unresolved questions

- See "Open product questions" above — these block scoping of Track 10 and Track 13
  specifically.

## Final verdict

Planning complete. No code changes. Backlog items for Tracks 10-14 confirmed accurate and
cross-linked; recommended sequencing recorded above.

## Next recommended action

Resume the queued **AI Config Overhaul / No-Fallback Rule / Journey Fix** sprint
(`docs/sprints/2026-06-11-ai-config-no-fallback-journey-fix-sprint.md`), which remains the
current implementation priority per `current-sprint.md`. When ready to pick up adaptive
foundation work, start with sequencing item 1 (vocabulary extraction) as the lowest-risk
entry point.
