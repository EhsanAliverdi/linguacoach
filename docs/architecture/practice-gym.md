---
status: current
lastUpdated: 2026-07-08 (Phase C-Final)
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

## Bank-first pattern coverage (as of 2026-07-08, Phase C3)

Phase C1 generalized the Form.io template path from one pilot pattern to a small first batch of
three deterministic patterns; Phase C2 added a second small batch of three more reading-family
patterns; Phase C3 added exactly one pattern, `reorder_paragraphs`. All eight keys are tracked in
`PracticeGymGenerationJob.TemplateMigratedPatternKeys`:

| Pattern key | Skill | Marking | Status |
|---|---|---|---|
| `formio_practice_gym_pilot` | speaking | FormIoScored (dedicated pilot pattern) | Original pilot, unchanged |
| `phrase_match` | vocabulary | KeyedSelection (native), FormIoScored when template-sourced | Migrated in C1 |
| `gap_fill_workplace_phrase` | vocabulary | ExactMatch (native), FormIoScored when template-sourced | Migrated in C1 |
| `reading_multiple_choice_single` | reading | KeyedSelection (native), FormIoScored when template-sourced | Migrated in C1 |
| `reading_multiple_choice_multi` | reading | KeyedSelection (native), FormIoScored when template-sourced | Migrated in C2 |
| `reading_fill_in_blanks` | reading | ExactMatch (native), FormIoScored when template-sourced | Migrated in C2 |
| `reading_writing_fill_in_blanks` | reading | ExactMatch (native), FormIoScored when template-sourced | Migrated in C2 |
| `reorder_paragraphs` | reading | ExactMatch (native), FormIoScored when template-sourced | **Migrated in C3** |

**Everything else in the ~33-row pattern catalog (25 legacy after C3, corrected from the earlier
~28/~21 estimate against the actual `ExercisePatternSeeder` count) remains fully legacy** — the
freeform `IAiActivityGenerator` content-generation path, completely unchanged. This is deliberate,
small-batch migration: each batch is chosen for being deterministic, audio-free, image-free, and
already `SupportsPracticeGym=true`.

## Phase C3 — `reorder_paragraphs` (2026-07-08)

A full re-audit of the remaining 25 legacy pattern keys against their actual content DTOs (not
just catalog flags) found **exactly one** additional safe candidate: `reorder_paragraphs`. Every
other remaining key is excluded for a concrete, code-grounded reason — see "Excluded patterns"
below, which extends the C2 audit with three newly-identified exclusions
(`answer_short_question`, `read_aloud`, `repeat_sentence`).

**What was built:**
- A new `ComponentAnswerScorer` kind, `ScoringRuleKinds.OrderedSequence` ("ordered_sequence"),
  reusing the exact positional-comparison scoring semantics `ExactMatchEvaluator.EvaluateReorderParagraphsAsync`
  already used for the legacy path — a Form.io-scored instance behaves identically to a
  legacy-generated one. Generic: usable by any future reorder-style template, not hardcoded to
  this one pattern. `ComponentScoringRule.CorrectOrder` (`IReadOnlyList<string>?`) carries the
  correct id order — backend-only, never serialized into `FormIoBaseSchemaJson`.
- One seeded template, `reorder_paragraphs_workplace_seed_v1` (B1, reading/`reading.inference`,
  original English-only "onboarding a new team member" 5-paragraph workplace scenario). Uses the
  **stock Form.io `datagrid` component with its built-in `reorder` setting** (drag-to-reorder
  rows) — no custom Form.io component class was needed, unlike `audioPlayer`/`speakingResponse`.
  Each row carries a hidden `itemId` field (stable paragraph id) and a disabled `textarea` field
  (paragraph text). The schema's row display order is deliberately shuffled and never matches the
  correct order; the correct order lives exclusively in `ScoringModelJson`.
- `FormIoSchemaValidationService`'s allow-list extended with `datagrid` and `hidden` — both
  recursively validated the same as any other container/field type, so this carries no more risk
  than the existing `panel`/`columns`/`table` container types already allowed.
- `reorder_paragraphs` added to `PracticeGymGenerationJob.TemplateMigratedPatternKeys`.
- **No frontend code changes were needed.** `ExerciseRendererComponent` already routes to the
  generic `FormioRendererComponent` purely based on `LearningActivity.formIoSchemaJson` being
  populated (the same content-driven mechanism proven since the original pilot) — a stock
  `datagrid`'s submission (`{"paragraphs": [{"itemId": ..., "text": ...}, ...]}` in the student's
  chosen order) is emitted by Form.io's own `submit` event and consumed as-is by
  `ComponentAnswerScorer`'s `ordered_sequence` case, no transformation needed on either side. The
  separate, legacy, non-Form.io `ReorderParagraphsComponent` (up/down-button reordering) is
  untouched and continues to serve any instance without `formIoSchemaJson` populated.

**Excluded patterns (Phase C3 audit findings — supersedes/extends the Phase C2 exclusion list):**
Every remaining legacy key falls into one of:
- **Open-ended AI-evaluated** (`AiStructured`/`AiOpenEnded` marking): `listen_and_answer`,
  `email_reply`, `teams_chat_simulation`, `open_writing_task`, `spoken_response_from_prompt`,
  `speaking_roleplay_turn`, `summarize_written_text`, `write_essay`, `summarize_spoken_text`,
  `respond_to_situation`, `describe_image`, `retell_lecture`, `summarize_group_discussion`.
- **Audio-referencing content DTO despite `RequiresAudio=false`** (the C2-identified "flag lies"
  problem): `listening_multiple_choice_single/multi`, `listening_fill_in_blanks`,
  `select_missing_word`, `highlight_correct_summary`, `highlight_incorrect_words`,
  `write_from_dictation` — all still reference `AudioUrl`/`AudioScript` in their content DTO.
- **Newly identified in the C3 audit — also audio-referencing, AND fuzzy/partial-credit scored**
  (not a binary `ComponentAnswerScorer`-shaped comparison): `answer_short_question` (substring
  "contains" fuzzy match), `read_aloud` and `repeat_sentence` (word-overlap percentage scoring,
  0.60 threshold, partial credit). These were not explicitly named in the C2 exclusion list;
  they're doubly excluded — audio AND non-binary scoring — not simply overlooked.
- **Not Practice-Gym-eligible:** `lesson_reflection` (`SupportsPracticeGym=false`).

No sentence-ordering/matching/exact-match reading-grammar-vocabulary pattern remains that hasn't
already been migrated (C1/C2/C3) or excluded above for a concrete reason.

**Phase C2 selection note:** all three C2 additions are reading-family patterns using only
components the existing `ComponentAnswerScorer` already supports (`single_choice` via radio,
`multiple_choice` via a Form.io `selectboxes` component, and `text_normalized` for the one
free-text word-form pattern). No new scorer kind or frontend component was needed. Several
"listening" patterns (`listening_multiple_choice_single/multi`, `listening_fill_in_blanks`,
`select_missing_word`, `highlight_correct_summary`, `highlight_incorrect_words`,
`write_from_dictation`) were considered but deliberately excluded from C2: although their catalog
`RequiresAudio` flag is `false`, their content DTOs and generation flow are built around an
audio/transcript script (see `ListeningFillInBlanksContent.AudioUrl` and the `ListeningAudioService`
integration in `PracticeGymGenerationJob`), so they do not yet have "strong evidence" of being
audio-free per the migration-batch selection rule. They remain legacy pending a dedicated review
of whether/how a text-only variant could be authored safely. `ReorderParagraphs` was also excluded
from C2 at the time — its `ExactMatch` marking is deterministic, but scoring a paragraph-ordering
interaction had no existing `ComponentAnswerScorer` kind — see "Phase C3" above for how this was
resolved with a new `ordered_sequence` kind.

**Key generalization insight (Phase C1, still true for C2 and C3):** for each newly-migrated pattern, its
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
for Phase C1, Phase C2, or Phase C3 (confirmed again for C3's stock `datagrid` component, which
required no new custom component registration either).

**Safety gates (all still required, same defense-in-depth as the original pilot):**
1. Pattern key must be in `TemplateMigratedPatternKeys` (code-level allow-list — the safe,
   admin-UI-free way to add/remove which patterns attempt the template path).
2. The existing `PracticeGymFormIoPilot.Enabled` runtime setting must be on (the master switch
   for all eight keys now in the allow-list, not just the original pilot — one admin toggle, no
   new UI).
3. An approved (`ReviewStatus.Approved`) + published (`IsPublished`) `ActivityTemplate` must
   exist for the pattern key — `phrase_match_workplace_seed_v1` / `gap_fill_workplace_phrase_seed_v1`
   / `reading_mcq_workplace_seed_v1` (Phase C1), `reading_mcq_multi_workplace_seed_v1` /
   `reading_fill_in_blanks_workplace_seed_v1` / `reading_writing_fill_in_blanks_workplace_seed_v1`
   (Phase C2), and `reorder_paragraphs_workplace_seed_v1` (Phase C3) — see `ActivityTemplateSeeder`.

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
key. This is not a new risk introduced by Phase C1, C2, or C3; it is the same constraint the
original pilot template design already accepted.

## Phase C-Final — closure and full pattern audit (2026-07-08)

Phase C-Final does not migrate any further patterns. It verifies the 8 template-enabled keys are
complete/stable, confirms every remaining key is correctly and intentionally classified as
legacy, and closes Phase C as a track. **Definitive audit table — all 33 pattern rows:**

| Pattern key | Skill | Marking mode | Audio/image/speaking/open-ended | Template-enabled | Reason if legacy | Future phase |
|---|---|---|---|---|---|---|
| `formio_practice_gym_pilot` | Speaking (nominal) | FormIoScored | none | **Yes** (original pilot) | — | — |
| `phrase_match` | Vocabulary | KeyedSelection | none | **Yes** (C1) | — | — |
| `gap_fill_workplace_phrase` | Vocabulary | ExactMatch | none | **Yes** (C1) | — | — |
| `reading_multiple_choice_single` | Reading | KeyedSelection | none | **Yes** (C1) | — | — |
| `reading_multiple_choice_multi` | Reading | KeyedSelection | none | **Yes** (C2) | — | — |
| `reading_fill_in_blanks` | Reading | ExactMatch | none | **Yes** (C2) | — | — |
| `reading_writing_fill_in_blanks` | Reading | ExactMatch | none | **Yes** (C2) | — | — |
| `reorder_paragraphs` | Reading | ExactMatch | none | **Yes** (C3) | — | — |
| `listen_and_answer` | Listening | AiStructured | audio (honestly flagged `RequiresAudio=true`) | No | open-ended AI + genuine audio | Backlog A + C |
| `listen_and_gap_fill` | Listening | ExactMatch | audio (honestly flagged `RequiresAudio=true`) | No | deterministic marking, but genuinely needs audio rendering support — not a "flag lies" case like the rows below | Backlog A |
| `email_reply` | Writing | AiStructured | none | No | open-ended AI | Backlog C |
| `teams_chat_simulation` | Writing | AiStructured | none | No | open-ended AI | Backlog C |
| `spoken_response_from_prompt` | Speaking | AiOpenEnded | speaking response | No | open-ended AI + speaking | Backlog B + C |
| `open_writing_task` | Writing | AiOpenEnded | none | No | open-ended AI | Backlog C |
| `speaking_roleplay_turn` | Speaking | AiOpenEnded | speaking response | No | open-ended AI + speaking | Backlog B + C |
| `lesson_reflection` | Reflection | NoMarking | none | No | `SupportsPracticeGym=false` — not Practice-Gym-eligible at all | N/A (out of Practice Gym scope) |
| `summarize_written_text` | Writing | AiStructured | none | No | open-ended AI | Backlog C |
| `write_essay` | Writing | AiStructured | none | No | open-ended AI | Backlog C |
| `listening_multiple_choice_single` | Listening | KeyedSelection | audio-referencing content DTO despite `RequiresAudio=false` | No | catalog flag "lies" — DTO carries `AudioUrl`/`AudioScript` | Backlog A |
| `listening_multiple_choice_multi` | Listening | KeyedSelection | same "flag lies" issue | No | same | Backlog A |
| `listening_fill_in_blanks` | Listening | ExactMatch | same "flag lies" issue | No | same | Backlog A |
| `select_missing_word` | Listening | KeyedSelection | same "flag lies" issue | No | same | Backlog A |
| `highlight_correct_summary` | Listening | KeyedSelection | same "flag lies" issue | No | same | Backlog A |
| `highlight_incorrect_words` | Listening | KeyedSelection | same "flag lies" issue | No | same | Backlog A |
| `write_from_dictation` | Listening | ExactMatch | same "flag lies" issue | No | same | Backlog A |
| `summarize_spoken_text` | Listening | AiStructured | audio-referencing | No | open-ended AI + audio | Backlog A + C |
| `answer_short_question` | Speaking | ExactMatch | audio-referencing + fuzzy "contains" scoring | No | audio AND non-binary scoring — doubly excluded | Backlog B + D |
| `read_aloud` | Speaking | ExactMatch | audio-referencing + word-overlap scoring | No | audio AND non-binary scoring — doubly excluded | Backlog B + D |
| `repeat_sentence` | Speaking | ExactMatch | audio-referencing + word-overlap scoring | No | audio AND non-binary scoring — doubly excluded | Backlog B + D |
| `respond_to_situation` | Speaking | AiOpenEnded | speaking response | No | open-ended AI + speaking | Backlog B + C |
| `describe_image` | Speaking | AiOpenEnded | image + speaking response | No | open-ended AI + speaking + image | Backlog B + C |
| `retell_lecture` | Listening | AiOpenEnded | audio | No | open-ended AI + audio | Backlog A + C |
| `summarize_group_discussion` | Listening | AiOpenEnded | audio | No | open-ended AI + audio | Backlog A + C |

**8 template-enabled, 25 legacy** (8 + 25 = 33, confirmed against `ExercisePatternSeeder`'s actual
row count and the passing `ExercisePatternPhase1Tests` count assertions — corrects an off-by-one
"26 legacy" figure that had propagated through earlier C3 documentation). "Backlog A/B/C/D" refers
to the four deferred pattern-family items in `docs/backlog/product-backlog.md`'s "Practice Gym —
Deferred Pattern Families" section (A: listening/audio, B: speaking/audio, C: open-ended
AI-evaluated, D: fuzzy/short-answer — several rows above map to more than one family, since e.g.
`answer_short_question` is both audio-referencing and fuzzy-scored).

**One classification correction from the C3 audit**: `listen_and_gap_fill` is deterministic
(`ExactMatch`) but has its `RequiresAudio` flag honestly set to `true` — unlike the seven
"flag lies" listening rows above, this one was never miscategorized; it has simply always been
correctly excluded pending real audio-rendering support in the Form.io path (Backlog item A).

## Phase C-Final verification results (2026-07-08)

All 8 template-enabled keys verified:
- Each has at least one approved (`ReviewStatus.Approved`) + published (`IsPublished`)
  `ActivityTemplate` (7 seeded rows in `ActivityTemplateSeeder`, one per non-pilot key; the pilot
  predates the seeder and has its own dedicated template authored directly).
- `ActivityTemplateSeeder` remains idempotent (`AnyAsync(t => t.Key == seed.Key)` guard,
  confirmed by the existing re-run test).
- Every seeded schema passes `IFormIoSchemaValidationService.ValidateSchema` (existing test
  coverage, unchanged).
- Scoring rules remain backend-only (`ScoringModelJson`, never serialized into
  `FormIoBaseSchemaJson`) — confirmed for all 7 seeds, including `reorder_paragraphs`'s
  `ordered_sequence`/`correctOrder` (Phase C3's dedicated no-leak test).
- Template path gating (allow-list membership, `PracticeGymFormIoPilot.Enabled`, approved+
  published template existence, novelty/cooldown check, legacy fallback on any failure) verified
  end-to-end in `PracticeGymGenerationJob` — all 5 checks present and correctly ordered; exact
  `HashSet<string>` membership test, no fuzzy/prefix matching, no risk of a non-migrated key
  (spot-checked `email_reply`, `listening_fill_in_blanks`) accidentally routing through the
  template path.
- `ActivityFeedbackDto.FeedbackPolicy` wiring (Phase B2) confirmed still intact and populated in
  both `ActivitySubmitHandler` dispatch paths — unaffected by C1/C2/C3.
- No gaps found requiring new tests or code changes — this closure pass is docs/backlog only.

## Migration plan for the rest of the catalog (deferred, tracked as backlog — not a numbered Phase C4)

Phase C is a **sequence** (C1 → C2 → C3 → C-Final), not one large "migrate everything" phase —
each increment was small enough to review and roll back independently. C1, C2, and C3 are all
complete (8 of 33 pattern rows template-enabled — 25 legacy remain, see the audit table above).
**Phase C-Final closes this track.** See `docs/roadmap/road-map.md` §19a for the phase order
relative to Phase D and Phase E, and `docs/backlog/product-backlog.md` for the 4 deferred pattern
families tracked as future backlog items (not started, not scheduled as a numbered Phase C4).

**No further deterministic/simple candidates remain** — see the audit table above. If a future
phase ever revisits Practice Gym pattern migration, it must open genuinely new scope, not another
small batch:
1. A dedicated audio-compatibility review of the listening family (8 keys total: 7 "flag lies" +
   `listen_and_gap_fill`) and the 3 fuzzy-scored speaking keys, including a new scorer kind for
   partial/fuzzy matching and a decision on whether audio-referencing content can be migrated at
   all (Backlog items A and D); or
2. Dedicated Form.io renderer + evaluator support for `AiStructured`/`AiOpenEnded` marking modes
   (open-ended AI-graded text, audio submission scoring, multi-turn speaking evaluation) —
   Backlog items B and C.

**Phase C-Final (closing out the deterministic-pattern migration track at 8/33, with the
remaining 25 formally documented above and tracked as backlog items, not left as an implicit
gap) is complete.** See `docs/roadmap/road-map.md` §19a and the Decision Log for the actual
phase-sequence decision and next-phase recommendation.

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
