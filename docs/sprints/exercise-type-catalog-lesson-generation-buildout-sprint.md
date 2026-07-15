# Exercise Type Catalog → Lesson Generation Build-Out

**Date:** 2026-07-14
**Related sprint/feature:** Admin Content Studio, Phase K (bank-first pipeline: Import → Resource Bank → Lesson → Exercise → Module)
**Type:** Implementation plan (phased sprint plan)

## Context

Two exercise-type systems currently exist side by side and were never merged:

1. **Exercise Types admin catalog** (`exercise_type_definitions` table, `/admin/exercise-types` page,
   `IExerciseTypeCatalogService`) — 37 entries seeded for the old on-demand generation pipeline
   (`SessionGeneratorService`, deleted in Phase I2B; `GetPracticeGymNext` on-demand generation
   disabled in Phase I2A). Each entry has `RendererKey`/`EvaluatorKey`/`GenerationPromptKey`
   metadata and a matching `activity_generate_*` AI prompt already seeded and active in
   `AiPrompts` — but **zero code anywhere calls `ResolveGenerationPromptKeyAsync`,
   `ResolveRendererKeyAsync`, or `ResolveEvaluatorKeyAsync`**. The metadata and prompts exist;
   nothing wires them up. Confirmed via full-repo grep — these three methods have no callers.

2. **Lesson → Generate Exercises** (`ActivityGenerationService` / `AiExerciseGenerationService`,
   Phase K5–K14, actively used) — a separate hardcoded 3-type list (`gap_fill`,
   `multiple_choice_single`, `short_answer`) with real composer logic: builds Form.io
   `FormSchemaJson`, deterministic `AnswerKeyJson`/`ScoringRulesJson` from `ScoringRulesDocument`/
   `ComponentScoringRule`, reads directly from published `ResourceBankItem` rows via
   `LessonResourceLookup`. This is the system that actually runs today.

The user's request: the Exercise Types catalog should be the single source of truth for what
Lesson generation can produce, filtered by skill, with all currently-catalogued types actually
usable — "backend should support all these." That requires writing real composer logic (Form.io
schema + answer key + scoring rule, or an AI-assisted equivalent per the project's
"AI never supplies the correct answer" principle) for the ~34 types that currently have no
implementation at all, plus retiring/consolidating catalog entries that don't fit the bank-first
model.

Also decided in this pass (AskUserQuestion): the `Surfaces` concept
(`SupportsPracticeGym`/`SupportsTodayLesson`) is being **removed entirely**, not just hidden from
the admin UI — see Phase K15 below. Reason: `SupportsPracticeGym` currently only gates a Practice
Gym "Formats" card grid whose `startFormat()` action calls `GetPracticeGymNext`, which Phase I2A
already made a permanent no-op ("On-demand Practice Gym generation is no longer available"). The
grid is dead UI. `SupportsTodayLesson` only gates `DynamicPatternSelector`, already documented in
its own file as "orphaned (zero production callers)" since Phase I2B. Removing both is cleanup of
already-dead functionality, not a live-behavior change.

## Files reviewed

- `src/LinguaCoach.Application/Admin/ExerciseTypeCatalogQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/ExerciseTypeCatalogService.cs`
- `src/LinguaCoach.Domain/Entities/ExerciseTypeDefinition.cs`
- `src/LinguaCoach.Persistence/Configurations/ExerciseTypeDefinitionConfiguration.cs`
- `src/LinguaCoach.Application/Activity/ExerciseTypeRegistry.cs` (+ Infrastructure impl)
- `src/LinguaCoach.Application/Sessions/DynamicPatternSelection.cs`
- `src/LinguaCoach.Infrastructure/Sessions/DynamicPatternSelector.cs`
- `src/LinguaCoach.Api/Controllers/ActivityController.cs` (`exercise-types/select`, `practice-gym/next`)
- `src/LinguaCoach.Infrastructure/Admin/StudentReadinessAuditService.cs`
- `src/LinguaCoach.Web/.../features/admin/admin-exercise-types/*`
- `src/LinguaCoach.Web/.../features/student/practice/practice-gym.component.ts`
- `src/LinguaCoach.Infrastructure/Exercises/ExerciseGenerationService.cs` (the pattern every new composer should follow)
- `src/LinguaCoach.Infrastructure/Exercises/AiExerciseGenerationService.cs`
- Live DB (`exercise_type_definitions`, `ai_prompts`) via psql, to confirm which prompts are already seeded and active.

## Current catalog inventory (37 entries)

| Skill | Category | Keys |
|---|---|---|
| listening | Pattern (12), Legacy (1) | highlight_correct_summary, highlight_incorrect_words, listen_and_answer, listen_and_gap_fill, listening_fill_in_blanks, listening_multiple_choice_multi, listening_multiple_choice_single, retell_lecture, select_missing_word, summarize_group_discussion, summarize_spoken_text, write_from_dictation, **listening_comprehension (Legacy)** |
| reading | Pattern (5) | reading_fill_in_blanks, reading_multiple_choice_multi, reading_multiple_choice_single, reading_writing_fill_in_blanks, reorder_paragraphs |
| reflection | Pattern (1) | lesson_reflection |
| speaking | Pattern (7), Pilot (1), Legacy (1) | answer_short_question, describe_image, read_aloud, repeat_sentence, respond_to_situation, speaking_roleplay_turn, spoken_response_from_prompt, formio_practice_gym_pilot (planned), **speaking_roleplay (Legacy)** |
| vocabulary | Pattern (2), Legacy (1) | gap_fill_workplace_phrase, phrase_match, **vocabulary_practice (Legacy)** |
| writing | Pattern (5), Legacy (1) | email_reply, open_writing_task, summarize_written_text, teams_chat_simulation, write_essay, **writing_scenario (Legacy)** |

All `activity_generate_*` AI prompts for the Pattern-category entries are already seeded and
active in `AiPrompts` — prompt authoring is largely done; wiring is not.

## Phased plan

### Phase K15 — Foundation cleanup (small, do first, unblocks everything else) — `Done` (2026-07-14)

1. Remove `SupportsPracticeGym`/`SupportsTodayLesson` end to end:
   - `ExerciseTypeDefinition` entity, `ExerciseTypeDefinitionConfiguration`, EF migration dropping
     `supports_practice_gym`/`supports_today_lesson` columns.
   - `ExerciseTypeDefinitionDto`/`UpdateExerciseTypeDefinitionCommand`, `ExerciseTypeCatalogService`.
   - `IExerciseTypeRegistry.GetForPracticeGymAsync`/`GetForTodayAsync`/`SelectForPracticeGymSkillAsync`/
     `ExerciseTypeSupportContext` — delete; `GetGenerationEligibleAsync`/`GetEligibleExerciseTypesForSkillAsync`
     (skill-only, no support-context filter) stay.
   - `ActivityController.SelectExerciseType` (`/exercise-types/select`) — delete along with
     `ExerciseTypeSelectionResponse`/`ToExerciseTypeSelectionDto`; it only served the dead
     `startFormat()` card grid.
   - `StudentReadinessAuditService` — its `GetForTodayAsync` readiness check becomes
     `GetGenerationEligibleAsync` (Ready+enabled, no surface gate), or the whole
     "exercise-types-ok" readiness signal is retired if it no longer means anything without
     Surfaces — needs a one-line product call, flag for review during implementation.
   - `PatternCatalogEntry.SupportsTodayLesson`, `DynamicPatternSelector.BuildApprovedKeySet` —
     already-orphaned code; drop the field, gate on `IsEnabled && IsReady` only, or delete the
     whole orphaned file pair if nothing else references it (recommend delete — it has been
     dead since Phase I2B).
   - `practice-gym.component.ts`/`.html` — delete the `skillGroups`/`runnableCount`/`FormatCard`/
     `startFormat`/`toCard` "Formats" card grid section entirely (dead since Phase I2A); keep the
     `moduleSuggestions`/`suggestedItems`/`continueItems`/`reviewItems` sections, which are the
     real, live Practice Gym experience.
   - `admin.models.ts` `ExerciseTypeDefinition` — drop the two fields.
2. Exercise Types admin page (`admin-exercise-types.component.ts`/`.html`):
   - Remove the "Surfaces" table column and `surfaceBadges()`/`surfaceOptions`/`isSurfaceSupported()`.
   - Remove the "Surfaces" checkbox group from the Configure slide-over.
   - Change the "Enabled" checkbox label from "Available for lesson and gym generation" to
     "Available for Lesson-based Exercise generation" (single purpose now).
   - `saveConfig()`/`updateExerciseType()` stop sending `supportsPracticeGym`/`supportsTodayLesson`.
3. Retire the 4 `Legacy`-category entries (`listening_comprehension`, `speaking_roleplay`,
   `vocabulary_practice`, `writing_scenario`) — set `is_enabled = false` via a seed/migration data
   fix, or delete outright if nothing references their `LegacyActivityType` enum values anymore
   (check `ActivityType` enum usages first). They predate the bank-first resource-type model and
   don't map cleanly to any of the 7 `PublishedResourceType`s.
4. Replace the Lesson detail page's hardcoded `supportedActivityTypesForThisLesson()`
   (`admin-lesson-detail.component.ts`, K13) with a live query against the catalog:
   `GetByPrimarySkillAsync(lesson.skill)` filtered to `isAvailableForGeneration` — this is what
   makes every subsequent phase below show up in the picker automatically as composers ship,
   with zero further frontend changes.

**K15 implementation notes** (deviations from the plan above, for anyone picking up K16+):
- `DynamicPatternSelector`/`DynamicPatternSelection.cs` were **not deleted** — only the
  `SupportsTodayLesson` field was stripped (gate is now `IsEnabled && IsReady`). Kept in place
  since deleting an orphaned-but-still-compiling file with its own test suite was judged higher
  risk than the field removal alone; still flagged orphaned/dead, still a candidate for deletion
  in a future pass.
- The Practice Gym "Formats" card grid (`practice-gym.component.ts`) was **not deleted** either —
  it's still dead UI (its `startFormat()` action hits the permanently-disabled
  `GetPracticeGymNext` endpoint), but removing student-facing UI without a browser to verify
  against felt like the wrong risk tradeoff for this pass. It now filters out `category ===
  'BankFirst'` catalog rows so the 3 new Lesson-generation types don't show up there as a
  card that errors on click.
- The 4 "Legacy"-category catalog entries were not specially deleted or given individual
  attention — they're covered by the same blanket `category <> 'BankFirst' → disabled` data fix
  as the other 33 legacy/pattern rows. `ActivityType` legacy enum values were left completely
  untouched (confirmed widely used elsewhere in the attempt/evaluation pipeline, out of scope).
- `StudentReadinessAuditService`'s "Today lesson" readiness check now calls
  `GetGenerationEligibleAsync` instead of the deleted `GetForTodayAsync` — same Ready+enabled
  gate, just without the Surfaces filter. Its check key/copy (`today.exercise_types_available`)
  was left as-is rather than renamed, since renaming a readiness check key has its own
  migration/dashboard-compat considerations outside this pass's scope.
- One-time data fix (`UPDATE exercise_type_definitions SET is_enabled = false WHERE category <>
  'BankFirst'`) is baked into the K15 migration's `Up()` — necessary because
  `SyncCatalogMetadata` deliberately never overwrites `IsEnabled` on existing rows on reseed (to
  protect a future admin's manual toggle), so a fresh reseed alone would not have picked up the
  new "35 legacy/pattern rows start disabled" default on an already-seeded database.

### Phase K16 — Deterministic extensions, Vocabulary/Grammar/Reading (low risk)

Reuses the existing `ScoringRulesDocument`/`ComponentScoringRule` infra already proven by
`gap_fill`/`multiple_choice_single` (confirmed: `ComponentAnswerScorer` already implements
`single_choice`/`multiple_choice`/`text_exact`/`text_normalized`/`ordered_sequence` — no new
scoring kinds needed for anything in this phase).

- [x] **`reading_fill_in_blanks`** — Reading (`ReadingPassage`/`ReadingReference`) — `Done`
  (2026-07-14). `ActivityGenerationService.ComposeReadingFillInBlanksAsync` blanks out up to 4
  content words (length ≥5, alphabetic, deduped) from the resource's own excerpt/passage text,
  `text_normalized` scoring per blank — same shape as `gap_fill`. For `ReadingPassage`, re-fetches
  the full `PassageText` directly (`FindFullPassageTextAsync`) since `LessonResourceSnapshot.Body`
  prefers the shorter `Summary` when present, which is too thin for a meaningful cloze. Rejects
  (doesn't degrade) when fewer than 2 usable content words exist — same discipline as
  `multiple_choice_single`'s distractor check. Catalog row converted from the disabled-by-default
  Pattern bucket to `Category = "BankFirst"` + `IsEnabled = true`, same key
  (`reading_fill_in_blanks`) — no duplicate catalog entry. 5 new unit tests, all passing;
  `reading_writing_fill_in_blanks` intentionally **not** included in this slice (kept disabled) —
  it implies a distinct read+write interaction, not just cloze, and needs its own design pass.
- [x] **`phrase_match`** — Vocabulary/Grammar — `Done` (2026-07-15). Resolved the "no
  `matching_pairs` scoring kind" gap by decomposing "matching" into N independent `single_choice`
  sub-questions (one radio per term), each offering every pulled term's own definition as an
  option — a definition used as the correct answer for one term is a live distractor for every
  other term in the same exercise, reproducing genuine matching semantics with zero new scoring
  infra. New `ComposePhraseMatchAsync` + `FindSiblingTermDefinitionsAsync` (same sibling-lookup
  pattern as `multiple_choice_single`'s distractors, but keeps each sibling's title too). Needs ≥2
  total terms (primary + siblings). Catalog row converted to `BankFirst`/enabled. 3 new unit
  tests, all passing.
- [x] **`reorder_paragraphs`** — Reading (`ReadingPassage` in practice) — `Done` (2026-07-15).
  Implemented the datagrid+reorder pattern exactly as documented in
  `FormIoSchemaValidationServiceTests.DatagridWithReorder_AndValidNestedComponents_IsValid`, with
  a `defaultValue` array on the datagrid component to pre-populate the deterministically-shuffled
  rows (fixed-seed `Random`, reproducible). New `ComposeReorderParagraphsAsync`. Needs ≥3 distinct
  paragraphs (split on blank lines). **The one open risk from the original deferral —
  whether the frontend Form.io renderer actually honors `defaultValue` for datagrid
  pre-population — was NOT resolved by this implementation; it's a real assumption, not a
  confirmed fact.** Flagged explicitly in the code doc comment and the catalog row's own
  description; needs manual browser verification before being fully trusted. 3 new unit tests
  (backend schema/scoring shape only — cannot verify actual rendering), all passing. **Both K16
  items done — 26 of 40 catalog types enabled (65%). K16 is fully complete.**

### Phase K17 — AI-assisted structured types (reuses `AiExerciseGenerationService` pattern from K14)

**Revised principle, decided this phase (AskUserQuestion):** the K14 "AI never supplies the
correct answer" principle holds for `gap_fill`/`multiple_choice_single` (Vocabulary/Grammar have a
definition field to derive the correct answer from) but does **not** hold structurally for reading
comprehension — a `ReadingReference`/`ReadingPassage` row has no "the answer is X" field, so a real
comprehension question requires the AI to judge the correct answer from the passage text itself.
Confirmed via AskUserQuestion: AI may supply the correct answer for this class of types, as a
scoped, documented exception — the existing PendingReview admin-approval gate (already required
for every generated Exercise) is the safety net, not a new one. This reasoning applies to every
item below in this phase, not just the first one implemented.

- [x] **`reading_multiple_choice_single`** — Reading (`ReadingReference`/`ReadingPassage`) —
  `Done` (2026-07-14). `AiExerciseGenerationService.ComposeReadingMultipleChoiceSingle` — AI
  returns `promptText` (the question), `correctAnswerText`, and up to 3 `distractors`, all judged
  from the resource's own excerpt/passage text (truncated to 2000 chars, same limit as the
  existing prompt). `exercise_generate_from_resources` prompt template extended with a new
  `correctAnswerText` field and a `reading_multiple_choice_single` rules block (existing
  gap_fill/multiple_choice_single/short_answer rules unchanged) — prompt auto-upgraded via content
  hash, no manual DB step needed beyond the usual seed run. No deterministic path exists or is
  possible for this type — it's routed straight to the AI handler by
  `LessonExerciseBatchGenerationService.AiOnlyOrAiPreferredTypes`, same mechanism K14 used for
  `multiple_choice_single`. Catalog row converted from disabled-Pattern to `BankFirst`/enabled,
  same key. 4 new unit tests, all passing.
- [x] **`reading_multiple_choice_multi`** — Reading (`ReadingReference`/`ReadingPassage`) — `Done`
  (2026-07-14). `AiExerciseGenerationService.ComposeReadingMultipleChoiceMulti` — same reasoning
  and mechanism as `reading_multiple_choice_single`, multi-select variant
  (`ScoringRuleKinds.MultipleChoice`/`selectboxes`, both already implemented). Requires ≥2 distinct
  `correctAnswersText` entries (a "select all" question with only 1 correct answer isn't really
  multi) and 1-3 usable distractors after filtering. Catalog row converted from disabled-Pattern to
  `BankFirst`/enabled, same key. 3 new unit tests, all passing.
- [x] **`listening_fill_in_blanks`** — Listening resources — `Done` (2026-07-14).
  `ActivityGenerationService.ComposeListeningFillInBlanks` — the deterministic cloze algorithm
  from `reading_fill_in_blanks` was extracted into a shared `BuildCloze` helper and reused verbatim
  here; Listening's `Snapshot.Body` already carries the transcript directly (no
  Summary-vs-full-text divergence like `ReadingPassage`, so no DB re-fetch needed). A Listening
  resource published without a transcript (audio-only) is valid data — rejects rather than
  degrading, same discipline as everywhere else.
- [x] **`listening_multiple_choice_single` / `listening_multiple_choice_multi`** — Listening
  resources — `Done` (2026-07-14). Reuse the exact same `AiExerciseGenerationService` compose
  methods as their reading counterparts (`ComposeReadingMultipleChoiceSingle`/`Multi`) — identical
  shape (radio/selectboxes + single_choice/multiple_choice scoring), only the source text
  (transcript vs excerpt) differs. `AiExerciseGenerationService`'s category logic refactored from
  binary `isDefinitional` to a resource-type switch (same refactor `ActivityGenerationService` got
  in the Writing section below), since Listening has no deterministic equivalent at all — only
  these two AI-assisted types. `exercise_generate_from_resources` prompt extended with
  `listening_multiple_choice_single`/`_multi` rule blocks (near-identical wording to the reading
  ones, "transcript" instead of "excerpt/passage").
- [x] **`highlight_correct_summary`** — Listening resources — `Done` (2026-07-15). Reuses
  `AiExerciseGenerationService.ComposeReadingMultipleChoiceSingle` verbatim (same AI-supplies-the-
  answer exception as the reading/listening MC types) — AI writes one accurate summary sentence as
  the correct answer plus 3 plausible-but-wrong summaries as distractors, all judged from the
  transcript. New prompt rule block added; no new compose code needed.
- [x] **`select_missing_word`** — Listening resources — `Done` (2026-07-15). The odd one out: the
  correct answer IS deterministic here — `ActivityGenerationService.PickBlankWord` (new shared
  public static helper) picks the first eligible content word from the transcript and blanks it
  out *before any AI call*, computed the same way as the cloze composers' word-eligibility check.
  AI is only asked for 3 plausible-but-wrong word distractors — same safe shape as
  `multiple_choice_single`, not the "AI supplies the answer" exception the other Listening MC
  types use. Rejects before any AI call when no eligible word exists in the transcript (e.g. all
  words too short). New `ComposeSelectMissingWord` method; `TryParseAndValidateOutput`'s signature
  extended with a `knownCorrectAnswer` parameter so it can filter AI distractors against the
  already-known correct word.
- [ ] `highlight_incorrect_words` — still open, genuinely blocked: it needs word-level selection
  within displayed text, which has no existing Form.io component in the allow-list
  (`textfield`/`textarea`/`radio`/`selectboxes`/`content`/`datagrid`/etc.) — would need a new
  custom component, out of scope for a backend-only pass. **This is the only remaining K17 item.**
- [x] **`email_reply` / `open_writing_task` / `write_essay`** — Writing resources — `Done`
  (2026-07-14). `ActivityGenerationService.ComposeWritingPrompt` — open-ended, honestly marked
  `RequiresManualOrAiEvaluation = true` exactly like `short_answer`, shows the Writing resource's
  own `PromptText` verbatim, no correct-answer trust question at all here since nothing is scored.
  Deterministic (not routed through AI) — no reason to spend an AI call on a prompt that's just
  copied verbatim. **Required a foundational fix first**: `LessonResourceLookup.FindAsync` never
  had a case for `PublishedResourceType.Writing`/`Listening`/`Speaking` — despite being
  importable/publishable since Phases J5a/J5c/J5d, Lessons could never be built from them at all
  (silently returned null, every generation call against one failed with "Resource ... was not
  found"). Added all three cases (Writing/Listening/Speaking → `LessonResourceSnapshot`), which
  also unblocks K18's Listening/Speaking composers below. `ActivityGenerationService`'s category
  logic refactored from a binary `isDefinitional` (Vocab/Grammar vs everything else) split to a
  resource-type switch, since Writing needed its own supported-type bucket distinct from Reading's.
  3 catalog rows converted from disabled-Pattern to `BankFirst`/enabled. 8 new unit tests (5
  composer + 3 `LessonResourceLookup`), all passing.
- [x] **`summarize_written_text`** — Writing skill, sourced from a **Reading** resource
  (`ReadingReference`/`ReadingPassage`) — `Done` (2026-07-14).
  `ActivityGenerationService.ComposeSummarizeWrittenText` — same shape as `short_answer` (excerpt
  shown, open-ended textarea, honestly unscored), added to the Reading resource-type bucket rather
  than the Writing one since generation must be rejected for a Writing-resource Lesson (backend
  validates the actual resource type, not just the catalog's declared skill). Catalog row converted
  from disabled-Pattern to `BankFirst`/enabled, same key — `PrimarySkill = "writing"`,
  `SecondarySkills = ["reading"]` unchanged, so it surfaces in the Lesson picker for both
  Writing-skill and Reading-skill Lessons (the backend is the real gate on which resource type it
  actually works with). 3 new unit tests, all passing. **This completes K17's Writing section** —
  all 4 originally-scoped Writing types (`email_reply`, `open_writing_task`, `write_essay`,
  `summarize_written_text`) now have real composers.

**K17 implementation notes:**
- On the existing dev database, the `reading_multiple_choice_single` catalog row's `IsEnabled`
  needed a one-time manual flip after redeploy — same reseed limitation documented under K16/K15
  (`SyncCatalogMetadata` never touches `IsEnabled` on an existing row). A genuinely fresh database
  seeds it enabled correctly on first run.
- `ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle` lives in the deterministic
  composer's constants file even though no deterministic case exists for it — kept there
  (rather than a new file) since every other activity-type constant and the AI composer's
  `supportedForCategory` checks already reference `ActivityGenerationService.ActivityType*`
  uniformly; splitting would fragment that single source of truth for activity-type strings.
- Every new BankFirst catalog row hits the same one-time `IsEnabled` flip issue on this
  already-seeded dev database (13 rows total by the end of K17) — always check
  `SELECT key, is_enabled FROM exercise_type_definitions WHERE category='BankFirst'` after a
  redeploy that adds new BankFirst rows, and flip any still `f` with a direct `UPDATE`. A fresh
  database seeds correctly on first run; this is purely a reseed-preserves-admin-edits artifact.

### Phase K18 — Speaking + audio-dependent types (needs existing speaking/audio pipeline)

**Investigated before starting** (dedicated research pass, not assumed): is there a working,
generic (non-placement-specific) speaking-audio scoring pipeline the bank-first Exercise flow
could reuse? **No.** `ComponentAnswerScorer.Score()` (the actual scorer for Exercise/Activity
attempts, confirmed via `ActivitySubmitHandler` → `FormIoPatternEvaluator` →
`IPlacementScoringService`) has no case for `ScoringRuleKinds.Speaking` — it silently falls into
the "wrong" default branch. Real audio scoring (`IPlacementSpeakingScorer` →
`ISpeakingEvaluationProvider`) is hardwired to the Placement flow only (item/attempt IDs,
`PlacementScoreResult`, etc.) and isn't reusable as-is. A *third*, older pipeline
(`SpeakingRolePlayEvaluator`/`ISpeakingEvaluationService`/`ISpeechToTextService`) belongs to the
legacy `LearningActivity`/`ActivityController` flow, async/job-queue based, also not reusable
inline. **Decision:** build these 5 types the same honest way as `short_answer`/`email_reply` —
deterministic, `RequiresManualOrAiEvaluation = true` — rather than scope-creep into generalizing
`IPlacementSpeakingScorer` for the bank-first pipeline (a separately-scoped follow-up).

- [x] **`spoken_response_from_prompt` / `respond_to_situation` / `answer_short_question` /
  `speaking_roleplay_turn` / `read_aloud`** — Speaking resources only — `Done` (2026-07-15).
  `ActivityGenerationService.ComposeSpeakingPrompt` — shows the resource's own PromptText
  verbatim, student responds via the stock `speakingResponse` Form.io component (same one already
  used by placement/onboarding speaking items — exact submitted-value shape confirmed:
  `{"storageKey": "<string>"}`), honestly marked `RequiresManualOrAiEvaluation`.
  `ScoringRuleKinds.Speaking` is used as the rule kind (matches the semantic intent) but the
  `RequiresManualOrAiEvaluation` flag is what actually short-circuits `ComponentAnswerScorer`
  before it would hit the unhandled-kind default branch. 5 catalog rows converted from
  disabled-Pattern to `BankFirst`/enabled. 7 new unit tests, all passing. **20 BankFirst types
  enabled total** — exactly half the 40-entry catalog.
- [ ] `describe_image` — deferred. `SpeakingPromptContent` has no image field, so there's nothing
  to actually describe yet — needs the resource content model extended with an image reference
  first (or a decision that admin notes/PromptText can substitute), a data-model change out of
  scope for a composer-only pass.
- [ ] `repeat_sentence` — deferred. Needs the resource to provide a *spoken* sentence for the
  student to repeat, not just text — `SpeakingPromptContent` has no audio field. Framing it as
  "read this sentence and repeat it" would just duplicate `read_aloud`; a real "listen then repeat"
  exercise needs TTS-generated audio from the prompt text, which is a build step, not just a
  composer.
- [ ] `write_from_dictation` — deferred, genuinely blocked. Investigated (dedicated research pass):
  building this as originally imagined (hear audio, type what you heard) requires NEW backend
  plumbing that doesn't exist anywhere in the bank-first pipeline — `Exercise` has no audio field,
  `ListeningAudioService` is hardwired to the legacy `LearningActivity` entity, and the
  `audioPlayer` Form.io component has no schema-embeddable source (existing usage resolves the URL
  via a bespoke Placement-only JS side-channel, not a generic convention). Faking it as "read the
  visible transcript and retype it" would just duplicate `reading_writing_fill_in_blanks`/cloze,
  not deliver a real dictation exercise — rejected rather than built dishonestly, same discipline
  as everywhere else in this project.
- [x] **`retell_lecture` / `summarize_group_discussion` / `summarize_spoken_text`** — Listening
  resources — `Done` (2026-07-15). Reuse `ComposeSpeakingPrompt`/`ComposeWritingPrompt` completely
  unchanged against the transcript — zero new compose code, just new instructions text and 3
  catalog rows converted to `BankFirst`/enabled. 4 new unit tests, all passing.
- [x] **`reading_writing_fill_in_blanks`** — Reading resources — `Done` (2026-07-15). "Choose the
  correct word" variant of `reading_fill_in_blanks`: same word-selection algorithm, but each blank
  renders as a radio choice (correct word + 2 distractors drawn from the same text's other content
  words, deterministic cyclic selection) instead of a free-text field, scored per-blank via
  `single_choice`. Needs ≥3 distinct content words. New `ComposeReadingWritingFillInBlanksAsync`
  composer. Catalog row converted from disabled-Pattern to `BankFirst`/enabled. 3 new unit tests,
  all passing. **24 of 40 catalog types enabled — 60% of the full catalog.**
- **Note:** `listen_and_answer` / `listen_and_gap_fill` / `gap_fill_workplace_phrase` (old Pattern
  Engine keys) were checked and confirmed functionally redundant with the new `gap_fill`/
  `listening_fill_in_blanks`/`listening_multiple_choice_single` BankFirst types — intentionally
  left disabled rather than building parallel duplicate composers.

### Phase K19 — `lesson_reflection` (2026-07-15, done)

`lesson_reflection` (`reflection` skill, `no_marking` evaluator) doesn't map to any
`PublishedResourceType` — it's generated from the *Lesson's own* Body, not a Resource Bank row.
Resolved by special-casing `ActivityTypeLessonReflection` at the top of
`HandleAsync(GenerateActivityFromLessonRequest ...)`, before the "must have linked resources"
check, so it never needs a `LessonResourceLink`. New `ComposeAndSaveLessonReflectionAsync` builds
the `Exercise` directly from `lesson.Body`/`lesson.Title`, `Skill="Reflection"`, zero
`ExerciseResourceLink`s. No defensive "is Body empty" guard — `Lesson`'s own constructor
(`ValidateAuthorableFields`) already guarantees a non-whitespace `Body`, so that state can't occur
(a test asserting the guard was written, found to test an impossible domain state, and removed).
Catalog row converted to `BankFirst`/enabled. Lesson picker's `openGenerate()` filter updated so
`lesson_reflection` always appears regardless of skill filter (it isn't skill-gated like every
other type).

### Phase K20 — `describe_image` + `teams_chat_simulation` (2026-07-15, done)

- `describe_image` (Speaking) — needed image support without building file-upload
  infrastructure. Solved by adding a plain `ImageUrl` string field to `SpeakingPromptContent`
  (admin pastes a URL), threaded through `UpdateResourceBankItemCommand`,
  `ResourceBankItemEditDto`, `ResourceBankItemUpdateHandler`, `ResourceBankQueryService`,
  `AdminResourceBankController`, and a new "Image URL" field on the Speaking case of the
  Resource Bank edit page. New `ComposeDescribeImageAsync` rejects (does not degrade) when the
  resource has no `ImageUrl` set: `"Resource '{title}' has no image set — add one on the Resource
  Bank edit page, or use a different Exercise type."` Otherwise builds a `content`-type HTML `<img>`
  component plus a `speakingResponse` component.
- `teams_chat_simulation` (Writing) — original ambition was a multi-turn `chat_reply` scenario, but
  no multi-turn Form.io component exists anywhere in the codebase (`chat_reply` was never
  allow-listed). Deliberately simplified to single-turn: reuses `ComposeWritingPrompt` unchanged
  ("Read the chat message below and write a concise, professional reply."), same as
  `email_reply`/`open_writing_task`/`write_essay`. Documented as a deliberate simplification, not a
  silent under-delivery.

Both catalog rows converted `Ready` → `BankFirst`/enabled. Full test suite green after each
change (Unit 2255, Integration 1322, Architecture 5 — all passing). **29 of 40 catalog types
(72.5%) now enabled by default.**

### Phase K21 — `highlight_incorrect_words` + audio-serving bridge (2026-07-15, done)

K21 opened with a product decision on the 4 Legacy-category catalog entries
(`listening_comprehension`, `speaking_roleplay`, `vocabulary_practice`, `writing_scenario`):
**keep as permanently-disabled catalog entries, do not delete** — their `ActivityType` enum
values are general-purpose and still used elsewhere in the domain, so removing the rows risks
orphaning pre-bank-first Activity data for no benefit. Documented directly in
`ExerciseTypeDefinitionSeeder.cs`. `formio_practice_gym_pilot` was already correctly documented as
intentionally inert (`Planned`, references the K18-era architecture-plan review) — no change
needed.

Initial investigation found `highlight_incorrect_words` needed more than "a new Form.io
component": it also needs audio playback, and **no student-facing endpoint existed to serve a
published Listening resource's stored audio to a bank-first (FormIoSchemaJson) Exercise** — the
only precedent, `ResourceCandidateAudioService`, is pre-publish and admin-only, and the existing
`/api/activity/{id}/audio` endpoint only serves TTS-generated audio for the legacy
AiGeneratedContentJson pipeline (gated on `ActivityType.ListeningComprehension`, which a
bank-first activity never meaningfully has — see `ExerciseLaunchService.MapToLearningActivityType`).
This same gap blocks `write_from_dictation`/`repeat_sentence` too.

Separately discovered: dedicated Angular renderer components for all three
(`highlight-incorrect-words`, `write-from-dictation`, `repeat-sentence`) already exist in
`exercise-renderer.component.ts`, reading from the legacy `raw`/`stagedExerciseData` content
shape — but every K15-K20 composer exclusively emits `FormIoSchemaJson`, which
`ActivityGetHandler` short-circuits on before any of that legacy-shape logic runs. Those renderers
are effectively orphaned dead code for the bank-first pipeline. Decided via AskUserQuestion to
keep every bank-first type on one consistent Form.io-based rendering mechanism rather than bridge
into the legacy renderer shape — built a new custom Form.io component instead.

Built, this phase:
- **Audio-serving bridge**: `ActivityController.GetResourceAudio` (`GET
  /api/activity/{activityId}/resource-audio`) — resolves `StudentExerciseLaunch` (by
  `LearningActivityId` + `StudentId`, proving ownership the same way the H10 launch bridge already
  does) → `ExerciseId` → `ExerciseResourceLink` (Listening) → `ResourceBankItem.ContentJson` →
  `ListeningPassageContent.AudioStorageKey`, streamed via the existing `IFileStorageService`.
  `ExerciseRendererComponent.formIoResourceAudioUrl` passes this URL into
  `FormioRendererComponent`'s already-generic `audioSrc` input for every FormIoSchemaJson
  activity — a no-op when no `audioPlayer` component is present or the endpoint 404s (no Listening
  resource linked).
- **New Form.io component**: `highlightWords` (`shared/formio/components/highlight-words.component.ts`)
  — renders schema-authored word tokens as clickable spans, submits a plain string array of
  selected token ids, scored via the existing `ScoringRuleKinds.MultipleChoice` kind (unordered
  set-equality against backend-only `CorrectAnswers`) — no new scoring kind needed. Added to
  `FormIoSchemaValidationService`'s allow-list.
- **New composer**: `ComposeHighlightIncorrectWords` — picks up to 3 distinct eligible content
  words (same length/alphabetic/distinct filter as `BuildCloze`) from the resource's own
  transcript, then **rotates their text among each other's positions** (word A takes word B's
  slot, wrapping around) instead of generating synthetic wrong words — every "wrong" word shown is
  a real word actually said elsewhere in the same transcript, fully deterministic, no AI call.
  Rejects (doesn't degrade) when fewer than 2 eligible content words exist. 2 new unit tests.

Catalog row converted `Ready` → `BankFirst`/enabled. Full suite green (Architecture 5, Unit 2257,
Integration 1322). **30 of 40 catalog types (75%) now enabled by default.**

### Phase K22 — `write_from_dictation` + `repeat_sentence` (2026-07-15, done)

- `write_from_dictation` (Listening) — built on the K21 audio-serving bridge. The resource's own
  transcript is split into up to 3 sentences (shared `SplitIntoSentences` helper, min length 10
  chars); the student hears the resource's own full, unaltered audio (same `audioPlayer` +
  `GetResourceAudio` streaming mechanism as `highlight_incorrect_words` — no per-sentence audio
  clip segmentation exists or was needed, since the transcript's sentence boundaries only size the
  typed-answer blanks) and types each sentence exactly as heard, one `textfield` per sentence,
  scored deterministically per-sentence (`text_normalized`, same leniency precedent as
  `BuildCloze`). Rejects when the transcript has fewer than 2 usable sentences.
- `repeat_sentence` (Speaking) — same honest, unscored `speakingResponse` shape as
  `read_aloud`/`answer_short_question` (no real speech scoring exists anywhere in the bank-first
  pipeline yet). Splits the Speaking resource's own PromptText into up to 5 sentences via the same
  shared helper, one `speakingResponse` component per sentence, each showing the sentence text as
  the prompt to repeat aloud. The catalog's own description ("Hear or read a short sentence, then
  repeat it") already permits a text-only prompt, so this doesn't depend on the K21 audio bridge.
  Rejects when the source text has fewer than 3 usable sentences.

Both catalog rows converted `Ready` → `BankFirst`/enabled. Full test suite green after each change
(Unit 2261, Integration 1324, Architecture 5 — all passing), including 4 new unit tests and 2 new
integration confirmation tests. **32 of 40 catalog types (80%) now enabled by default.**

This closes out the composer build-out: every catalog entry that can be built as a plain
deterministic-or-honestly-unscored composer now has one. The remaining 8 disabled entries are all
intentional, permanent states, not "not started":
- 4 `Legacy`-category entries (K21 decision: kept, not deleted, permanently disabled).
- 3 old Pattern Engine keys (`listen_and_answer`, `listen_and_gap_fill`,
  `gap_fill_workplace_phrase`) confirmed functionally redundant with already-built BankFirst types
  (K18) — intentionally left disabled rather than duplicated.
- `formio_practice_gym_pilot` — intentionally inert pilot entry (`Planned`, not `Ready`), see
  `docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`.

## Decisions made this pass

- Surfaces (`SupportsPracticeGym`/`SupportsTodayLesson`) removed everywhere, not just hidden in
  admin UI — confirmed via AskUserQuestion ("Full removal: deprecate Surfaces concept
  everywhere"), justified by both gated code paths already being dead (Phase I2A/I2B).
- Full phased build-out plan requested over "disable everything not implemented" or "widen picker
  with deterministic-only variants" — confirmed via AskUserQuestion ("Scope a phased build-out
  now").

## Risks / unresolved questions

- `StudentReadinessAuditService`'s readiness signal built on `GetForTodayAsync` needs a decision
  on what it means once Surfaces is gone (see K15.1). — **Resolved** in K15: switched to
  `GetGenerationEligibleAsync`.
- Whether `ComponentAnswerScorer` already has scoring-kind support for `matching_pairs`/cloze
  blank sets needs to be confirmed before K16 starts, not assumed from the doc comment alone. —
  **Resolved**: confirmed `single_choice`/`multiple_choice`/`text_exact`/`text_normalized`/
  `ordered_sequence` all exist; no `matching_pairs` kind (still blocks `phrase_match`, see K16).
- ~~The `Listening`/`Writing`/`Speaking` `ResourceBankItem` content models' exact field shapes~~ —
  **Resolved** in K17: `WritingPromptContent.PromptText`, `ListeningPassageContent.Transcript`,
  `SpeakingPromptContent.PromptText` all confirmed. Bigger finding while confirming this:
  `LessonResourceLookup.FindAsync` had never been extended to resolve any of these 3 types at all
  — fixed as part of K17, see that section.
- `ActivityType` legacy enum values tied to the 4 Legacy-category catalog entries need a usage
  check before deciding delete vs. disable in K15.3.
- `ListeningPassageContent.Transcript` is nullable (`string?`) — a Listening resource published
  without a transcript exists as valid data (audio-only). K18's Listening composers need to decide
  whether to reject generation when `Transcript` is null (same "reject, don't degrade" discipline
  as `multiple_choice_single`'s distractor check) — almost certainly yes, since none of the K18
  types can work from audio alone without an actual STT/audio-processing step, but worth stating
  explicitly before K18 starts.

## Implementation tasks produced

Tracked as Phase K15 through K22 above, **all executed** (2026-07-14 to 2026-07-15, same
continuous work session) — 32 of 40 catalog types now have real Lesson-generation composers and
are enabled by default; every other type is intentionally left disabled (3 old Pattern Engine
duplicates, `formio_practice_gym_pilot`, or the 4 permanently-disabled Legacy entries), not
"not started." The composer build-out is complete.

## Final verdict

**The bank-first pipeline (Import → Resource Bank → Lesson → Exercise → Module) is functionally
complete for all 7 `PublishedResourceType`s, plus the Lesson-Body-sourced `lesson_reflection`
type, and now includes a real audio-serving bridge for Listening resources.** 32 of 40 catalog
types (80%) are enabled by default, every resource type
(Vocabulary/Grammar/ReadingReference/ReadingPassage/Writing/Listening/Speaking) has multiple real,
tested, deployed Exercise composers a Lesson can generate from, and the Lesson picker (K15.4)
surfaces exactly what's enabled with zero hardcoding. **The composer build-out is complete** — the
remaining 8 disabled catalog entries are all deliberate, permanent, documented decisions (3
confirmed-redundant legacy Pattern Engine keys, the intentionally-inert pilot entry, and the 4
permanently-disabled Legacy entries), not unfinished work. None of them block the core admin
workflow: Import Content → Resource Bank → generate Lesson → generate Exercise(s) → combine into
Module → review → approve.

## Next recommended action

The composer build-out is now complete — every catalog entry that can be built as a plain
deterministic-or-honestly-unscored composer has one. What remains is exclusively (a) frontend
Form.io verification in a real browser — `reorder_paragraphs`'s `defaultValue` pre-population is a
real, stated, unconfirmed assumption, and more broadly, confirming the
`speakingResponse`/`selectboxes`/`datagrid`/`content`(image)/`audioPlayer`/`highlightWords`
renderers actually work end-to-end for everything built in K16-K22, since this session's
verification was build+test only, never a live browser check — or (b) deliberate, documented,
permanent decisions (the 4 Legacy entries, the 3 redundant Pattern Engine keys, the pilot entry).
**Recommend the user now manually test the live admin UI** — generate at least one Exercise of
each of the 32 enabled types from a real Lesson, since automated tests cover backend logic/schema
shape but not actual Form.io rendering/submission in the browser. `reorder_paragraphs`, the K18
speaking types, `describe_image`'s image `<img>` rendering, and `highlight_incorrect_words`'s /
`write_from_dictation`'s audio playback are the highest-value ones to check first, since they're
the least similar to the original, already browser-tested `gap_fill`/`multiple_choice_single`
shape.
