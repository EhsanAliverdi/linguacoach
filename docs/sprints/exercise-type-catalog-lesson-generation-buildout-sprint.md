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

Same principle as the K14 multiple-choice-distractor fix: AI supplies framing/distractors/options,
the correct answer is always derived from the resource's own field, never AI-supplied.

- `reading_multiple_choice_single` / `reading_multiple_choice_multi` — AI-generated
  comprehension question against the passage excerpt; answer verified against excerpt content.
- `listening_fill_in_blanks` / `listening_multiple_choice_single` / `listening_multiple_choice_multi`
  / `select_missing_word` / `highlight_incorrect_words` / `highlight_correct_summary` — same
  pattern, sourced from the `Listening` resource type's transcript field instead of a
  `ReadingPassage` excerpt. Confirm `Listening` resource content model has a transcript field
  suitable as source text (referenced in the K12/K14 summary as already used for
  Listening-resource Lessons).
- `email_reply` / `open_writing_task` / `summarize_written_text` / `write_essay` — Writing.
  Open-ended, honestly marked `RequiresManualOrAiEvaluation = true` exactly like the existing
  `short_answer` composer — no new scoring risk, just new instructions/prompt per type sourced
  from the `Writing` resource's prompt text.

### Phase K18 — Speaking + audio-dependent types (needs existing speaking/audio pipeline)

- `answer_short_question` / `respond_to_situation` / `describe_image` / `spoken_response_from_prompt`
  / `speaking_roleplay_turn` — Speaking. Reuse the existing `_speakingEvaluator`/STT pipeline
  already wired into `ActivityController` for the old flow — confirm it's reusable standalone
  from the new Lesson-based composer rather than tied to the deleted session generator.
- `read_aloud` / `repeat_sentence` — Speaking, `exact_match` evaluator against a transcript —
  needs TTS for the prompt audio if not already available from the `Speaking` resource's own
  recorded content.
- `write_from_dictation` — Listening, `exact_match` against a `Listening` resource's transcript —
  same shape as `read_aloud`/`repeat_sentence` but audio-in/text-out instead of text-in/audio-out.
- `retell_lecture` / `summarize_group_discussion` / `summarize_spoken_text` — Listening/Speaking,
  fully open-ended AI-graded (`ai_open_ended`/`ai_structured` evaluator keys already seeded) —
  hardest tier, do last.
- `teams_chat_simulation` — Writing, multi-turn `chat_reply` renderer — needs its own design pass
  for what a Resource-Bank-sourced multi-turn scenario looks like; not a simple single-prompt
  composer like the others.

### Phase K19 — Decisions needed before touching

- `lesson_reflection` (`reflection` skill, `no_marking` evaluator) — doesn't map to any of the 7
  `PublishedResourceType`s; a reflection prompt would be generated from the *Lesson's own* Body,
  not a Resource Bank row. Different generation entry point than everything else here — needs a
  product decision on whether it belongs in this pipeline at all.
- `formio_practice_gym_pilot` (`ImplementationStatus = planned`, `Pilot` category) — confirm
  whether this pilot is still active work or should be dropped from the catalog.

## Decisions made this pass

- Surfaces (`SupportsPracticeGym`/`SupportsTodayLesson`) removed everywhere, not just hidden in
  admin UI — confirmed via AskUserQuestion ("Full removal: deprecate Surfaces concept
  everywhere"), justified by both gated code paths already being dead (Phase I2A/I2B).
- Full phased build-out plan requested over "disable everything not implemented" or "widen picker
  with deterministic-only variants" — confirmed via AskUserQuestion ("Scope a phased build-out
  now").

## Risks / unresolved questions

- `StudentReadinessAuditService`'s readiness signal built on `GetForTodayAsync` needs a decision
  on what it means once Surfaces is gone (see K15.1).
- Whether `ComponentAnswerScorer` already has scoring-kind support for `matching_pairs`/cloze
  blank sets needs to be confirmed before K16 starts, not assumed from the doc comment alone.
- The `Listening`/`Writing`/`Speaking` `ResourceBankItem` content models' exact field shapes
  (transcript, prompt text) need re-confirming against the actual `ResourceBankItemContent`
  subtypes before K17/K18 composer code is written.
- `ActivityType` legacy enum values tied to the 4 Legacy-category catalog entries need a usage
  check before deciding delete vs. disable in K15.3.

## Implementation tasks produced

Tracked as Phase K15 through K19 above; K15 is the immediate next unit of work (small,
self-contained, unblocks the Lesson-detail picker from further hardcoding). K16–K19 are separate
future sessions — each phase should get its own review doc under `docs/reviews/` when implemented,
per the documentation persistence rule.

## Final verdict

Plan approved in principle by the user (chose "scope a phased build-out now" over disabling
unimplemented types or a narrower deterministic-only widening). Not yet implemented — this session
ends at the plan; K15 is next.

## Next recommended action

Start Phase K15 (Surfaces removal + catalog-driven Lesson picker) in the next work session — it's
scoped, low-risk (both gated code paths are already confirmed dead), and is the dependency every
later phase needs (K15.4's catalog-driven picker is what makes K16+ composers show up in the admin
UI without further frontend work).
