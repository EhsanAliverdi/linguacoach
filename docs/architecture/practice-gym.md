---
status: current
lastUpdated: 2026-07-08 (Plan-Sync-After-C1)
owner: architecture
supersedes:
supersededBy:
---

# Practice Gym

## Purpose

The Practice Gym is a secondary, on-demand practice space separate from the guided course.

While the guided course delivers structured lessons aligned to the student's placement, level, and career context, the Practice Gym lets students practise any skill on demand without following the lesson sequence.

---

## Position in Product

The Practice Gym is not the main product. The main product is the guided course (Today / My Course).

The Practice Gym exists because:
- Students sometimes want to practise one skill immediately (e.g. write an email before a real meeting)
- Vocabulary review should be available on demand
- Listening practice may be done independently of the lesson schedule
- It serves as a sandbox that keeps existing activity flows working

---

## Navigation Placement

In the future navigation model:

```
Today         ← primary (today's lesson)
My Course     ← guided course structure
Practice      ← Practice Gym (this section)
Vocabulary    ← vocabulary queue
Progress      ← skill progress
Profile       ← preferences
```

The Practice tab replaces the current activity-card dashboard layout.

---

## Contents of Practice Gym

| Skill | Activity Type | Status |
|---|---|---|
| Writing | WritingScenario | Live |
| Vocabulary | VocabularyPractice | Live |
| Listening | ListeningComprehension | Live |
| Speaking | SpeakingRolePlay | Live (MVP) |
| Reading | ReadingTask | Not started |
| Teams Chat | TeamsChatSimulation | Planned |
| Pronunciation | PronunciationPractice | Not started |

---

## How Existing Routes Are Preserved

The current `/activity?type=...` route continues to work as the Practice Gym entry point.

No changes to existing routing are required for the Practice Gym concept. The dashboard activity cards simply move under a Practice tab.

---

## Relationship to Guided Course

Practice Gym activities:
- are tracked in `ActivityAttempt`
- contribute to `StudentSkillProfile` and learning memory
- do not advance sessions or modules in the guided course
- may feed vocabulary into the student's vocabulary queue

Guided course activities:
- are always part of a `SessionExercise` within a `LearningSession`
- advance session progress
- are selected by the session generator based on teaching sequence

The two paths share the same `LearningActivity` and `ActivityAttempt` infrastructure.

---

## TeamsChatSimulation in Practice Gym

`TeamsChatSimulation` can be used in both:
- Practice Gym: student selects a Teams chat scenario on demand
- Guided lesson session: Teams chat step is included in a teaching sequence

In the Practice Gym, the student picks a workplace scenario topic and gets a Teams-style chat exercise.

The same content format and evaluation logic is reused in both contexts.

---

## Out of Scope

- Practice Gym progress tracking separate from the course (all attempts go into the same history)
- Practice Gym recommendations or scheduling (ad-hoc only)
- Pronunciation in Practice Gym (still not started)
- Reading in Practice Gym (not started)

---

## Content repetition (as of 2026-07-08, Phase B)

`PracticeGymBufferRefillJob` queues `PracticeActivityCache` rows with a `ContentFingerprint`
that is a **queue-slot uniqueness key only** (deterministic from student/pattern/level/domain/
run-timestamp/slot-index) — it satisfies the DB's unique index and makes queuing reproducible,
but it is **not** a content-level dedup signal, since no activity content exists yet at queue
time. Do not confuse it with the real content fingerprint below.

Real content-level repetition/novelty avoidance now exists — see
**docs/architecture/repetition-and-novelty.md** for the full design. In summary:
`StudentActivityUsageLog` records every real content consumption; `IActivityNoveltyPolicy`
enforces exact-fingerprint/same-template/same-topic cooldowns; the Form.io Practice Gym pilot's
`TryMaterializeFromTemplateAsync` checks the template cooldown before generating and the content
fingerprint after, with a small bounded retry before falling back to standard generation. This is
**deterministic/exact-match only** — no embeddings, no semantic near-duplicate detection.

## Bank-first pattern coverage (as of 2026-07-08, Phase C1)

Phase C1 generalized the Form.io template path from one pilot pattern to a small first batch of
four pattern keys total, tracked in `PracticeGymGenerationJob.TemplateMigratedPatternKeys`:

| Pattern key | Skill | Marking | Status |
|---|---|---|---|
| `formio_practice_gym_pilot` | speaking | FormIoScored (dedicated pilot pattern) | Original pilot, unchanged |
| `phrase_match` | vocabulary | KeyedSelection (native), FormIoScored when template-sourced | **Migrated in C1** |
| `gap_fill_workplace_phrase` | vocabulary | ExactMatch (native), FormIoScored when template-sourced | **Migrated in C1** |
| `reading_multiple_choice_single` | reading | KeyedSelection (native), FormIoScored when template-sourced | **Migrated in C1** |

**Everything else in the ~28-pattern catalog remains fully legacy** — the freeform
`IAiActivityGenerator` content-generation path, completely unchanged. This was a deliberate,
small first batch chosen for being deterministic, audio-free, and already
`SupportsPracticeGym=true`; broader migration is future work (Phase C2+, not started).

**Key generalization insight (Phase C1):** for the 3 newly-migrated patterns, their
`ExercisePatternDefinition.MarkingMode` stays at its legacy value (`KeyedSelection`/`ExactMatch`)
— it is NOT changed to `FormIoScored`, because that field is shared, static config also used by
the legacy fallback for the same pattern key. Instead, `ActivitySubmitHandler` now decides
Form.io-scored evaluation **per activity instance**, based on whether
`LearningActivity.FormIoSchemaJson` is actually populated (set only by
`ActivityTemplate`-personalized instances via `SetFormIoContent`) — never by the pattern's
nominal marking mode alone. This is what lets the SAME pattern key serve both template-sourced
and legacy-generated instances correctly, side by side, without a global marking-mode change
breaking the legacy fallback. The frontend renderer (`ExerciseRendererComponent.formIoSchema`)
was already content-driven this way since the original pilot — no frontend changes were needed
for Phase C1.

**Safety gates (all three still required, same defense-in-depth as the original pilot):**
1. Pattern key must be in `TemplateMigratedPatternKeys` (code-level allow-list — the safe,
   admin-UI-free way to add/remove which patterns attempt the template path).
2. The existing `PracticeGymFormIoPilot.Enabled` runtime setting must be on (now the master
   switch for all four keys, not just the original pilot — one admin toggle, no new UI).
3. An approved (`ReviewStatus.Approved`) + published (`IsPublished`) `ActivityTemplate` must
   exist for the pattern key — `phrase_match_workplace_seed_v1`/
   `gap_fill_workplace_phrase_seed_v1`/`reading_mcq_workplace_seed_v1` (see
   `ActivityTemplateSeeder`) satisfy this for the 3 new patterns.

If any gate fails, or the Phase B novelty policy blocks the template/content as a recent
duplicate after a small bounded retry, generation falls back to the legacy freeform path for
that specific attempt — logged, never silently duplicated, never an infinite loop.

**Known limitation (shared with the original pilot's design):** `ActivityTemplateInstanceGenerator`
personalizes `FormIoBaseSchemaJson` via AI, but `ScoringModelJson` is never regenerated — it is
applied as-is, keyed by component `key`. The seeded templates' `GenerationInstructions`
therefore explicitly forbid the AI from renaming component keys or changing which
option/value is correct. This is enforced by instruction only (plus
`ActivityTemplateValidationRules.requiredComponentKeys` catching a renamed/missing key) — there
is no automated check that the AI didn't shift which answer is correct while keeping the same
key. This is not a new risk introduced by Phase C1; it is the same constraint the original pilot
template design already accepted.

## Migration plan for the rest of the catalog (Phase C2+)

Phase C is a **sequence** (C2 → C3 → C4 → C-Final), not one large "migrate everything" phase —
each increment should be small enough to review and roll back independently, the same discipline
Phase C1 followed. See `docs/roadmap/road-map.md` §19a for the phase order relative to Phase D
and Phase E.

**Continue in small batches**, applying the same selection criteria Phase C1 used: deterministic
or mostly-deterministic marking, no audio/image requirement, `SupportsPracticeGym=true` already
in production, and an existing simple scoring shape (`single_choice`/`multiple_choice`/
`text_exact`/`text_normalized` via `ComponentAnswerScorer`).

**Do not migrate complex speaking, listening, or open writing/AI-evaluated patterns
(`AiStructured`/`AiOpenEnded` marking modes, or any pattern requiring audio recording/playback)
until dedicated Form.io renderer and evaluator support for those interaction types exists and is
proven safe.** The current Form.io path only has a working, tested story for
single-component/multi-component deterministic scoring (`FormIoPatternEvaluator` +
`ComponentAnswerScorer`) — it has no story yet for AI-graded open-ended text, audio submission
scoring, or multi-turn speaking evaluation. Attempting those patterns before that renderer/
evaluator work exists would either silently degrade grading quality or require inventing new,
unreviewed evaluation logic under time pressure — exactly the risk this phased approach is
designed to avoid.
