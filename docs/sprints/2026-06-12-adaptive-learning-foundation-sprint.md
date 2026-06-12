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
- [x] Phase 2: Numeric `StudentSkillProfile` scores (sequencing item 2) — see note below
- [x] Phase 3 planning pass (see "Phase 3 planning pass" section below)
- [x] Phase 3 P1: StudentSkillProfile-grounded feedback (implementation) — see note below
- [x] Phase 3 P2 scoping pass — see note below
- [x] Phase 3 P2 task 1: surface `teachingNote` as `[goal]` for GapFill/MatchingPairs

---

## Phase 2 (2026-06-12) — Numeric StudentSkillProfile scores

Implemented sequencing item 2: `StudentSkillProfile` now stores a 0-100
`ScorePercent` instead of a persisted `IsWeak` boolean.

- `StudentSkillProfile` (`src/LinguaCoach.Domain/Entities/StudentSkillProfile.cs`):
  - New `ScorePercent` (clamped 0-100), `LastUpdatedUtc` unchanged.
  - `IsWeak` is now a computed property (`ScorePercent < 50`), not persisted.
  - `ApplyScoreDelta(int delta)` — incremental update, clamped.
  - `MarkWeak(bool)` kept for backward compatibility — snaps to 40/60.
  - Bool constructor kept — maps `isWeak` to 40/60 starting score.
- `PatternSkillUpdateService.ApplyAsync`: SkillImpact delta (-1..1) scaled to
  +/-10 score points via `ApplyScoreDelta`, instead of snapping `IsWeak`.
- `StudentMemoryService.UpsertSkillProfilesAsync`: AI memory-update weak/strong
  deltas now apply +/-10 score nudges via `ApplyScoreDelta`.
- `StudentSkillProfileDto` and `ProgressSkillDto` gained `ScorePercent`.
- Frontend: `ProgressSkill` / `StudentSkillProfile` models gained `scorePercent`;
  Progress page now shows the numeric percentage instead of "Needs work"/"Good".
- Migration `T42_StudentSkillScorePercent`: adds `score_percent` (default 50),
  backfills from old `is_weak` (weak -> 40, strong -> 60), drops `is_weak`.
  `Down` reverses this (score < 50 -> weak).
- Two LINQ-to-SQL spots (`SessionGeneratorService`, `StudentMemoryService`
  adaptive-context builders) updated to filter/project on `ScorePercent`
  directly, since `IsWeak` is a non-mapped computed property.

Test changes:
- `StudentSkillProfileTests`: added `ApplyScoreDelta` clamping and `IsWeak`
  derivation tests.
- `PatternSkillUpdateServiceTests.ExplicitSkillImpacts_UpdatesExistingProfile`:
  updated for incremental scoring — a single +0.9 impact moves a weak skill
  from 40 to 49 (still weak, improved), not directly to "not weak" as the old
  snap-to-bool behavior did. This is the intended Phase 2 change.

Tests: `dotnet test` — 482/482 unit, 430/430 integration. `npm run build` passed
(pre-existing warnings only).

This unlocks Track 10 Stage 3 (ongoing per-skill diagnostic percentages) — the
numeric score is now available for display and for future diagnostic UI.

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

### Phase 3 planning pass (2026-06-12)

See `docs/reviews/2026-06-12-lesson-practice-structure-phase3-plan.md` for full review.

Summary: most of Phase 3 already exists — Practice rendering, the redo/next loop
(`tryAgain`/`improveAnswer`/`nextActivity` + `attemptCount`), and per-attempt AI
feedback are functional for both Lesson and Practice. The genuine gap is grounding
feedback in `StudentSkillProfile.ScorePercent` (now available from Phase 2). Phase 3 is
split into:

- **P1 (next)** — feed relevant `StudentSkillProfile.ScorePercent` into the evaluation
  prompt context so `coachSummary` can reference student progress; optional small UI
  indicator in `pattern-evaluation-result.component.html`.
- **P2 (deferred, own phase)** — structured "What we learn" (grammar/vocab/phrases)
  breakdown, replacing the single `learningGoal` line. Cross-cutting prompt-engineering
  effort across activity types — needs its own scoping pass.
- **P3** — no code change; existing redo/next UX likely already satisfies the brief,
  pending product confirmation.

### Phase 3 P1 (2026-06-12) — StudentSkillProfile-grounded feedback

Implemented:

- `PatternEvaluationRequest` (`src/LinguaCoach.Application/Activity/PatternEvaluationContracts.cs`)
  gained `StudentSkillContext` (optional string).
- `PatternSkillUpdateService.GetPrimarySkillKey(string?)` — new public static helper
  exposing the existing pattern-key -> skill-key map for use outside skill-update.
- `ActivitySubmitHandler.BuildStudentSkillContextAsync` — for AI-marked patterns
  (AiStructured/AiOpenEnded) only, looks up the student's `StudentSkillProfile` for the
  pattern's primary skill and formats a one-line standing summary
  (`"<label> (<score>/100): <encouraging/developing/strong guidance>."`). Deterministic
  patterns (gap fill, phrase match) get `null` — no behaviour change for them.
- `AiStructuredEvaluator` and `AiOpenEndedEvaluator` pass `studentSkillContext` into
  prompt variables (falls back to "No specific skill history available yet." when null).
- Prompts updated (hash-based auto-upgrade, no version bump needed):
  `activity_evaluate_email_reply`, `activity_evaluate_teams_chat_simulation`,
  `activity_evaluate_spoken_response_from_prompt`, `activity_evaluate_listen_and_answer`
  — each now includes `{{studentSkillContext}}` and an instruction to use it for a
  specific, non-generic `coachSummary`.

Tests: `dotnet test` — 482/482 unit, 430/430 integration, no test changes needed (no
existing test asserted on these prompts' exact text).

P2 (structured "What we learn") remains deferred to its own phase/sprint.

### Phase 3 P2 scoping pass (2026-06-12)

See `docs/reviews/2026-06-12-lesson-practice-structure-phase3-p2-scoping.md` for full
review (scoping only, no code changes).

Summary: a full "What we learn" grammar/vocab/phrases card requires new AI-prompt
fields per pattern — genuinely cross-cutting, deferred indefinitely, picked up
pattern-by-pattern. However, scoping found one concrete near-term win: gap_fill and
phrase_match generation prompts already produce a `teachingNote` field
("language pattern practised") that is generated but never surfaced — confirmed
unused anywhere outside `DefaultAiSeeder.cs`. Surfacing it as the existing `[goal]`
input on `ExerciseLessonIntroComponent` is a small, frontend-only change
(`GapFillContent`/`MatchingPairsContent`: `content.learningGoal ?? content.teachingNote`)
with zero new AI prompt work.

Recorded as task 1 above. Remaining P2 scope (richer "What we learn" card,
`targetGrammarPoint`, vocab/phrases for pattern-driven generators) stays deferred,
no own phase scheduled yet.

### Phase 3 P2 task 1 (2026-06-12) — teachingNote surfaced as goal

Implemented (frontend-only, Option (a) from scoping doc):

- `GapFillContent` / `MatchingPairsContent`
  (`src/LinguaCoach.Web/src/app/features/activity/renderers/{gap-fill,matching-pairs}/*.component.ts`)
  gained `teachingNote?: string | null`.
- Both renderers' `<app-exercise-lesson-intro [goal]="...">` binding now reads
  `content.learningGoal ?? content.teachingNote`.
- No backend changes — AI already generates `teachingNote` for phrase_match and
  gap_fill_workplace_phrase content; this surfaces it for the first time.

`npm run build` passed (pre-existing CSS warnings only).

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
