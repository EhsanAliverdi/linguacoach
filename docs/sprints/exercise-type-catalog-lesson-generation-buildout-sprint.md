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
- [ ] `phrase_match` — Vocabulary/Grammar. **Deferred.** No `matching_pairs` scoring kind exists in
  `ComponentAnswerScorer` yet (only single/multiple choice, text, ordered sequence) — needs either
  a new scoring kind or decomposing into N independent `single_choice` sub-questions (one per
  pair). Bigger lift than the other K16 items; picking that design is the first step next time
  this phase is picked up.
- [ ] `reorder_paragraphs` — Reading (`ReadingPassage` only, needs multi-paragraph `PassageText`).
  **Deferred** — not a scoring-infra gap (confirmed: `ScoringRuleKinds.OrderedSequence` /
  `ComponentAnswerScorer.ScoreOrderedSequence` already exist and are exactly what this needs,
  reusing the datagrid+reorder Form.io pattern documented in
  `FormIoSchemaValidationServiceTests.DatagridWithReorder_AndValidNestedComponents_IsValid`: a
  `datagrid` component with `reorder:true`, `disableAddingRemovingRows:true`, row template
  `[{type:"hidden",key:"itemId"},{type:"textarea",key:"text",disabled:true}]`, scored via
  `ComponentScoringRule.CorrectOrder`). The open question is whether the datagrid needs a
  `defaultValue` set on the schema component itself to pre-populate the shuffled rows for display,
  or whether the frontend Form.io renderer needs separate initial-data plumbing not yet
  confirmed by reading code alone — this needs either a source-level trace of the generic Form.io
  renderer or a real browser check before shipping, so it was intentionally not risked in this
  pass.

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
- [ ] `teams_chat_simulation` — Writing, multi-turn `chat_reply` renderer — needs its own design
  pass for what a Resource-Bank-sourced multi-turn scenario looks like; not a simple single-prompt
  composer like the others. Deferred.
- **Note:** `listen_and_answer` / `listen_and_gap_fill` / `gap_fill_workplace_phrase` (old Pattern
  Engine keys) were checked and confirmed functionally redundant with the new `gap_fill`/
  `listening_fill_in_blanks`/`listening_multiple_choice_single` BankFirst types — intentionally
  left disabled rather than building parallel duplicate composers.

### Phase K19 — Decisions needed before touching

- `lesson_reflection` (`reflection` skill, `no_marking` evaluator) — doesn't map to any of the 7
  `PublishedResourceType`s; a reflection prompt would be generated from the *Lesson's own* Body,
  not a Resource Bank row. Different generation entry point than everything else here — needs a
  product decision on whether it belongs in this pipeline at all. **Still open.**
- `formio_practice_gym_pilot` (`ImplementationStatus = planned`, `Pilot` category) — confirm
  whether this pilot is still active work or should be dropped from the catalog. **Still open.**
- The 4 `Legacy`-category catalog entries (`listening_comprehension`, `speaking_roleplay`,
  `vocabulary_practice`, `writing_scenario`) — predate the bank-first resource-type model, don't
  map cleanly to any of the 7 `PublishedResourceType`s. Currently disabled (correct default), but
  whether to delete outright vs. leave as permanently-disabled catalog entries is still an open
  product call — no urgency, they're inert either way. **Still open.**

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

Tracked as Phase K15 through K19 above. **K15 through K18 substantially executed** (2026-07-14 to
2026-07-15, same continuous work session) — 24 of 40 catalog types now have real Lesson-generation
composers and are enabled by default; every other type is either intentionally superseded
(3 old Pattern Engine duplicates), intentionally inert (`formio_practice_gym_pilot`), or blocked
on something outside "write a composer" (new scoring kind, new Form.io component, data-model
change, new backend plumbing, multi-turn design, or a K19 product decision).

## Final verdict

**The bank-first pipeline (Import → Resource Bank → Lesson → Exercise → Module) is now
functionally complete for all 7 `PublishedResourceType`s** — every resource type
(Vocabulary/Grammar/ReadingReference/ReadingPassage/Writing/Listening/Speaking) has at least one
real, tested, deployed Exercise composer a Lesson can generate from, and the Lesson picker
(K15.4) surfaces exactly what's enabled with zero hardcoding. The remaining 16 disabled catalog
entries are not "not started" — each has a specific, documented reason it isn't buildable as a
plain composer right now (see the unchecked items in K16-K19 above). None of them block the core
admin workflow: Import Content → Resource Bank → generate Lesson → generate Exercise(s) → combine
into Module → review → approve.

## Next recommended action

The composer build-out itself has reached its natural stopping point without new external input.
Remaining work needs one of: (a) a scoring-kind design decision (`phrase_match`), (b) frontend
Form.io verification in a real browser (`reorder_paragraphs`, and confirming the `speakingResponse`/
`selectboxes` renderers actually work end-to-end for everything built in K17-K18, since this
session's verification was build+test only, never a live browser check), (c) a new custom Form.io
component (`highlight_incorrect_words`), (d) resource content-model changes for image/audio fields
(`describe_image`, `repeat_sentence`), (e) new backend audio-serving plumbing
(`write_from_dictation`), (f) a multi-turn scenario design (`teams_chat_simulation`), or (g) product
decisions on `lesson_reflection`/the pilot entry/the 4 Legacy entries (K19). **Recommend the user
now manually test the live admin UI** — generate at least one Exercise of each of the 24 enabled
types from a real Lesson, since automated tests cover backend logic/schema shape but not actual
Form.io rendering/submission in the browser.
