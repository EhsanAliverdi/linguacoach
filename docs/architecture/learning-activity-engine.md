---
status: current
lastUpdated: 2026-07-09 (Phase E10)
owner: architecture
supersedes:
supersededBy:
---

# Learning Activity Engine

> **Phase H3 note (2026-07-09):** the new Learn Item foundation
> (`docs/architecture/product-model-realignment-h0.md`) does not touch this engine —
> `LearningActivity`/Today/Practice Gym runtime selection are unchanged. `LearnItem` is a new,
> separate reviewable-content entity upstream of a future Activity/Module layer (H4/H5), not a
> replacement for anything documented below.
>
> **Phase H4 note (2026-07-09):** the new Activity foundation added a `LinguaCoach.Domain.Entities.
> ActivityDefinition` entity — **do not confuse it with `LearningActivity`** (this document's
> subject, a per-student runtime/delivery record) **or with `ActivityTemplate`** (the existing
> admin-authored template already wired into the live Practice Gym Form.io pilot runtime, see
> `PracticeGymGenerationJob.TemplateMigratedPatternKeys`). `ActivityDefinition` is a reusable,
> reviewable practice-task *design* with Resource Bank/Learn Item traceability that neither
> `LearningActivity` nor `ActivityTemplate` has, and it is **not wired into any runtime
> selection/delivery path** — Today materialization, Practice Gym generation, and
> `ActivityTemplate`'s own pilot are all unchanged. See
> `docs/architecture/product-model-realignment-h0.md` for the full H4 detail.
>
> **Phase H5 note (2026-07-09):** the new Module foundation added a
> `LinguaCoach.Domain.Entities.ModuleDefinition` entity — **do not confuse it with
> `LearningModule`** (a per-student thematic group of `LearningActivity` rows within a
> `LearningPath`, tracks its own completion). `ModuleDefinition` is a reusable, reviewable
> learning unit combining Learn Items + Activity Definitions + a module-level feedback plan, and
> it is **not wired into any runtime selection/delivery path** — Today materialization, Practice
> Gym generation, and `LearningModule`/`LearningPath` are all unchanged. H6 (Daily Lesson) and H7
> (Practice Gym) are the planned future runtime consumers, not built yet. See
> `docs/architecture/product-model-realignment-h0.md` and
> `docs/reviews/2026-07-09-phase-h5-module-foundation-review.md` for the full H5 detail.
>
> **Phase H6 note (2026-07-09):** H6 is the first phase to actually touch this engine's Today
> path, and does so **additively**. `SessionQueryHandler.HandleAsync(GetTodaysSessionQuery)`
> still calls `ISessionGeneratorService.GetOrCreateTodaysSessionAsync` completely unchanged —
> `LearningSession`/`SessionExercise`/`LearningActivity` creation is untouched. Separately, in its
> own try/catch, it now also calls the new `IDailyLessonModuleSelectionService` (deterministic, no
> AI call, read-only — selects an Approved `ModuleDefinition` with an Approved `LearnItem` and
> Approved `ActivityDefinition` linked) and attaches the result as an optional
> `TodaysSessionResult.ModuleSection`. A selection failure or "no suitable Module" case never
> affects the exercises returned above — it just leaves `ModuleSection` null. `LearningModule`,
> `LearningPath`, and `ActivityAttempt` are all unchanged; the module selector creates no
> `LearningActivity`/`ActivityAttempt` rows. Practice Gym generation is unchanged (H7 is the
> planned future consumer there). See `docs/architecture/product-model-realignment-h0.md` and
> `docs/reviews/2026-07-09-phase-h6-daily-lesson-module-pipeline-review.md` for the full H6
> detail.
>
> **Phase H7 note (2026-07-09):** H7 does the same additive thing H6 did, but for Practice Gym.
> `PracticeGymSuggestionService.GetSuggestionsForStudentAsync` still builds
> `SuggestedItems`/`ContinueItems`/`ReviewItems` from `StudentActivityReadinessItem` completely
> unchanged — `PracticeActivityCache`, `PracticeGymGenerationJob`, `PracticeGymBufferRefillJob`,
> and the separate legacy `ActivityController.GetPracticeGymNext`/`IPracticeGymPoolService` path
> are all untouched. Separately, in its own try/catch, it now also calls the new
> `IPracticeGymModuleSelectionService` (deterministic, no AI call, read-only — same eligibility
> rule as H6) and attaches the result as an optional `PracticeGymSuggestionsDto.ModuleSuggestions`.
> A selection failure or "no suitable Module" case never affects the sections above. The module
> selector creates no `LearningActivity`/`ActivityAttempt`/`PracticeActivityCache` rows and does
> not touch `StudentActivityReadinessItem`. There is deliberately no "start" flow for a module
> suggestion in H7 — `ActivityDefinition` (H4) still has no attempt/scoring runtime wired to it
> anywhere, unlike `ActivityTemplate`'s live Form.io pilot, which remains the only Practice Gym
> path that actually launches a scored activity. See
> `docs/architecture/product-model-realignment-h0.md` and
> `docs/reviews/2026-07-09-phase-h7-practice-gym-module-pipeline-review.md` for the full H7
> detail.
>
> **Plan-Sync-After-H7 note (2026-07-09, docs-only):** a legacy-structure audit confirmed every
> entity/service/job this document describes — `LearningActivity`/`LearningSession`/
> `SessionExercise`/`LearningModule`, `ActivityMaterializationJob`, `LessonBatchGenerationJob`,
> `PracticeGymGenerationJob`, `ActivityTemplate`, `PracticeActivityCache`,
> `StudentActivityReadinessItem` — is still live core runtime infrastructure, not a legacy
> structure superseded by the H-track's content-studio model. None of it is a candidate for
> removal in the upcoming H8 (admin/nav cleanup only) or even H9 (destructive cleanup) without
> further proof. `ActivityTemplate` in particular cannot be removed until H10 decides how
> `ActivityDefinition` gets a real launch/attempt path — see
> `docs/reviews/2026-07-09-plan-sync-after-h7-legacy-bank-removal-strategy.md`.

## Why `LearningActivity` is the Centre of the Product

SpeakPath's core value proposition is personalised, AI-driven practice that tracks what a student knows, what they struggle with, and what they should do next. Every form of practice — writing, speaking, listening, vocabulary, reading, pronunciation — is modelled as a `LearningActivity`.

This single abstraction means:
- AI generation is consistent: all activity types go through `IAiActivityGenerator` with type-specific prompts.
- Progress tracking is consistent: all attempts go through `ActivityAttempt`, regardless of skill.
- Spaced repetition and mastery scoring work across activity types without knowing the type.
- The dashboard, module progress, and path completion logic are type-agnostic.
- New activity types are added by extending the AI prompt and adding a new Angular component — no domain or DB schema changes needed.

---

## Entity Relationships

```
StudentProfile (1)
    └── LearningPath (1 active at a time)
            └── LearningModule[] (ordered 1..N)
                    └── LearningActivity[] (type-specific, AI-generated)
                            └── ActivityAttempt[] (one per student submission)

StudentProfile (1)
    └── ActivityAttempt[] (all attempts by this student, any activity)
```

### `LearningPath`

- One active path per student at a time.
- Generated by AI at onboarding completion using the student's career context, CEFR level, and skill focus.
- Contains a human-readable `Title` and a `LearnerContextSummary` (snapshot of the profile used to generate the path — useful for re-generation).
- `IsActive` flag — deactivating old path before creating a new one is the upgrade mechanism.

### `LearningModule`

- An ordered thematic unit within a path. E.g. "Email writing for approvals", "Formal telephone calls".
- Has a `Title`, `Description`, and `Order` (1-based).
- The "current module" is determined at query time: lowest-order module where the student has not yet reached the completion threshold.
- Does not hold state itself — progress is derived from `ActivityAttempt` counts.

### `LearningActivity`

- A single practice item. Can be AI-generated (`Source = AiGenerated`) or seeded from legacy data (`Source = SystemFallback`).
- `ActivityType` determines the payload schema stored in `AiGeneratedContentJson` (JSONB).
- `ExercisePatternKey` is nullable and links pattern-generated activities back to the `ExercisePatternDefinition` that selected their renderer, prompt keys, and marking mode.
- `LearningModuleId` is nullable — standalone activities generated outside a module context have no module link. Going forward, all activities generated by `ActivityGetHandler` set this FK.
- `SourceWritingScenarioId` is a legacy FK used only for `SystemFallback` rows seeded from the old `writing_scenarios` table. Will be removed in `T19_DropLegacyWritingTables`.
- `Difficulty` mirrors the student's `CefrLevel` at generation time — allows serving appropriate fallback activities to students at different levels.

### `ActivityAttempt`

- Records one student submission against one `LearningActivity`.
- Stores legacy `SubmittedContent`, `FeedbackJson` (type-specific feedback JSON), and nullable `Score`.
- Pattern Evaluation Engine adds nullable structured evaluation fields so old attempts keep loading unchanged:
  - `SubmittedAnswerJson` stores the renderer-submitted structured answer.
  - `EvaluationResultJson` stores the canonical `PatternEvaluationResult`.
  - `MaxScore`, `Percentage`, `Passed`, and `Completed` make evaluation queryable without parsing JSON.
  - `MarkingMode` records the `ExercisePatternDefinition.MarkingMode` used for audit/debugging.
- FK to both `StudentProfile` (who attempted) and `LearningActivity` (what was attempted).
- Legacy AI feedback is stored in `FeedbackJson`; pattern-aware results are returned through additive DTO fields and stored in `EvaluationResultJson`.

---

## How AI Generation Works

```
ActivityGetHandler.HandleAsync(query)
    │
    ├── Load StudentProfile (career, CEFR, language pair)
    ├── Load active LearningPath → current LearningModule
    │
    ├── Build ActivityGenerationContext:
    │     ActivityType, CefrLevel, CareerContext,
    │     LanguagePairCode, SourceLanguageName, TargetLanguageName,
    │     TopicHint = "{module.Title}: {module.Description}"
    │
    ├── IAiActivityGenerator.GenerateActivityContentAsync(context)
    │     │
    │     ├── IAiContextBuilder.BuildPrompt("activity_generate_{type}")
    │     │     Loads prompt template from AiPrompt table, substitutes variables
    │     │
    │     ├── IAiProviderResolver.Resolve() → active IAiProvider (Gemini / OpenAI / Anthropic)
    │     │
    │     └── Returns JSON string (type-specific payload schema)
    │
    ├── Persist LearningActivity (Source=AiGenerated, LearningModuleId=current)
    │
    └── Return ActivityDto
```

**Prompt key naming convention:** legacy activity generation uses `activity_generate_{activityType}` / `activity_evaluate_{activityType}`. Pattern-generated lesson activities use `activity_generate_{patternKey}` / `activity_evaluate_{patternKey}`.

Examples: `activity_generate_writing`, `activity_evaluate_writing`, `activity_generate_phrase_match`, `activity_evaluate_teams_chat_simulation`.

Each prompt template is versioned in the `ai_prompts` table and managed by the Admin UI.

**Repetition/novelty check (Phase B, 2026-07-08):** `ActivityMaterializationJob` (Today lessons)
and `PracticeGymGenerationJob`'s Form.io template pilot both wrap their generation call with a
content-fingerprint novelty check (`IActivityNoveltyPolicy`) — bounded retry on an exact-content
duplicate, then a safe fallback (serve anyway + warn for Today; fall back to standard generation
for the pilot). This is deterministic/exact-match only, not semantic similarity. Every completed
activity also now writes a `StudentActivityUsageLog` row (real content-usage history) via
`ActivitySubmitHandler`. See **docs/architecture/repetition-and-novelty.md** for the full design
— this diagram above does not yet show that wrapping step.

**Bank-first Today slice (Phase D1, 2026-07-08; expanded Phase D2, 2026-07-08):**
`ActivityMaterializationJob` tries `ITodayBankResourceSelector`/`TodayBankResourceSelector`
before generation, for Today exercises whose `ExercisePatternDefinition.PrimarySkill` is
`"Vocabulary"` (`phrase_match`, `gap_fill_workplace_phrase`) or `"Reading"` — the gate is purely
skill-based, not an explicit pattern-key allow-list, so it already covers every current
Reading-primary pattern: `reading_multiple_choice_single`, `reading_fill_in_blanks`,
`reorder_paragraphs`, and (confirmed/tested explicitly in D2, though always covered by
construction) `reading_multiple_choice_multi` and `reading_writing_fill_in_blanks`. Grammar bank
content (`CefrGrammarProfileEntry`) is pulled in only opportunistically for
`gap_fill_workplace_phrase` (its `Grammar` secondary skill), never as its own pattern gate, since
Today has no dedicated grammar-focused pattern yet — the audit found no other current pattern
lists `Grammar` as a secondary skill without also being a Writing/Speaking/Listening pattern
(AiStructured/AiOpenEnded marking, explicitly out of scope for this bank-first slice).

**Phase D2 selector improvements**: the selector now returns a **balanced bundle** for
Vocabulary-primary patterns — up to 2 vocabulary entries, up to 1 opportunistic grammar entry,
and up to 1 opportunistic short reading excerpt (useful anchor material even for a
vocabulary-focused activity) — capped at 4 total resources; Reading-primary patterns still
receive up to 2 reading references only. The selector queries the published Resource Bank
(`IResourceBankQueryService`, Phase E5) at the exact routing-recommended CEFR level first;
**only when the routing reason is Review/Scaffold/Remediation** (`AllowLowerLevelReview`) and the
exact level has zero rows does it retry one CEFR level down (`CefrLevelConstants.All` ordering)
— it never widens upward and never widens at all for ordinary generation. Each candidate is
run through a synthetic-fingerprint novelty precheck (`"bank-vocab-precheck:{id}"` etc.,
mirroring `PracticeGymGenerationJob`'s per-template precheck) **and** a cheap feedback-signal
check: a student who previously marked an activity `NotUseful` or `DoNotShowSimilarSoon`
(`ActivityFeedbackSignal`) will not have that same bank resource selected again — matched via
`LearningActivity.BankResourceProvenanceJson` (a string-contains check), not
`ActivityFeedbackSignal.SourceBankItemId` (see the provenance note below for why). "Skill/
subskill preference" from the original D2 brief is satisfied structurally — the bank-type
queried (vocabulary/grammar/reading) already *is* the skill match, since none of the three
Cefr* bank entities carries its own skill/subskill column to filter further.

**Structured bank context (Phase D2)**: instead of a single loose sentence, the selector now
builds a clearly-bounded block — `Approved bank resources to use as anchors (do not invent
unrelated vocabulary or content, keep the student's CEFR level at {level}, keep all content
English-only, support-language behavior stays runtime-only): - [Vocabulary] "..." - [Grammar]
"..." - [Reading] "...".` — still appended to the same free-text `TopicHint` field that
`avoidRepeatingHint` already uses (`ActivityGenerationContext` has no dedicated "supporting
material" field, so no AI prompt template changes were needed for either D1 or D2). All other
patterns (Writing/Speaking/Listening/Reflection) are unaffected (`SkippedUnsupportedPattern`);
when no bank rows exist at the applicable CEFR level(s), generation proceeds exactly as before
(`NoSuitableResources`).

**Provenance (Phase D2 — replaces D1's mechanism)**: D1 recorded a single "primary" resource id
via `StudentActivityReadinessItem.SetBankItemProvenance(...)`. **This was discovered during D2 to
be a latent bug**: `SourceBankItemId` on `StudentActivityReadinessItem`/`StudentActivityUsageLog`/
`ActivityFeedbackSignal` is FK-constrained to `PlacementItemDefinition`, not to any Phase E
Cefr* bank table — writing a `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/
`CefrReadingReference` id into that column would throw a foreign-key violation against a real
(FK-enforcing) database the first time a readiness-pool item actually existed at materialization
time; D1's own integration test happened not to exercise that exact path, so it went unnoticed
until D2's audit. **Fixed** by adding a new column instead: `LearningActivity.
BankResourceProvenanceJson` — a JSON array of every selected resource
(`{type, id, sourceId, contentFingerprint, selectionReason}`), set at materialization time before
the activity's first save (migration `Phase_D2_AddLearningActivityBankResourceProvenance`, a
plain nullable `jsonb` column, no default value — deliberately avoiding the exact
Bugfix-D1A default-value trap). The `StudentActivityReadinessItem.SetBankItemProvenance(...)`
call from D1 was removed entirely rather than patched — it was never a safe mechanism for this
data. See `docs/architecture/english-resource-bank-import-platform.md`'s "Relationship to Today
lesson composer" section for the decision-checkpoint history, and `docs/backlog/product-backlog.md`
for what remains explicitly deferred (Speaking/Listening/image/open-ended patterns, a dedicated
grammar-focused Today pattern, semantic/embedding-based selection).

**Plan-Sync-After-D2 (2026-07-09, docs-only): Phase E7 comes before Phase D3.** D2 expanded this
bank-first slice as far as the current bank/resource-type coverage reasonably allows — Today
still has no Grammar-primary pattern, no Speaking/Listening/image/open-ended bank content, and no
semantic/embedding selection, and the bank itself is still only Phase E6's 32/12/10-row internal
seed pack. **Current bank-first coverage remains exactly what D1/D2 established: Vocabulary/
Reading-primary Today patterns only, with legacy `IAiActivityGenerator` freeform generation as
the unchanged fallback for Speaking/Listening/image/open-ended and every other pattern.** A
broader Phase D3 composer migration attempted now would mostly run into missing content/resource
types and thin bank depth, not a limitation of the D1/D2 integration hook itself — so **Phase E7
(deepen and harden the resource platform/content model) is the next recommended implementation
phase, and Phase D3 remains deferred** until after E7 (and E8 if needed). See
`docs/roadmap/road-map.md` Decision Log (2026-07-09, Plan-Sync-After-D2) for the full reasoning.

**Phase E7 (2026-07-09) added a full internal/original English reading passage bank** — a new
`CefrReadingPassage` published bank entity, entirely separate from `CefrReadingReference` (which
remains short-excerpt/citation-style only, unchanged). See
`docs/architecture/english-resource-bank-import-platform.md`'s E7 detail section for the full
design (new bank entity, publish routing by staged-text length, browse/search API + admin page,
10 new full-length passages through the same E1-E6 staging/review/publish pipeline — no
external datasets, no Persian/bilingual content). **This is a resource-platform expansion, not a
Today-composer change**: `TodayBankResourceSelector`/`ActivityMaterializationJob` are untouched
by E7 and still only ever query vocabulary/grammar/short-reading-reference rows — current
bank-first Today coverage remains exactly what D1/D2 established (Vocabulary/Reading-primary
patterns only, legacy fallback everywhere else). Whether/how Today should ever consume full
reading passages is left for a future phase (Phase D3 or later), not decided here. **Phase D3
and PG-v2 implementation remain not started.**

**Phase D3 (2026-07-09) wired the E7 full reading passage bank into the Today bank-first
composer.** This is a **narrow, fallback-safe extension of the D2 slice, not a full Today composer
rewrite**: `TodayBankResourceSelector` now, for Reading-primary patterns, prefers a full
`CefrReadingPassage` anchor when the pattern is one of the comprehension/reorder patterns
(`reading_multiple_choice_single`, `reading_multiple_choice_multi`, `reorder_paragraphs`), and
otherwise (or when no suitable passage exists, or novelty excludes every candidate passage) falls
back to the short `CefrReadingReference` behavior exactly as before. Cloze/fill-in-blanks patterns
(`reading_fill_in_blanks`, `reading_writing_fill_in_blanks`) deliberately keep the short-reference
behavior — they generate their own gapped text and are better anchored by a short excerpt. The
selector receives the concrete `PatternKey` (new `TodayBankSelectionRequest.PatternKey`) to make
this decision; the existing CEFR policy is preserved unchanged (exact level first, one-level-down
widening only for Review/Scaffold/Remediation, never upward). At most **one** full passage is
injected (heavier prompt material than a short excerpt), through a bounded structured TopicHint
block carrying the passage title, CEFR, word count, estimated reading time, the passage text
(delimited, length-capped), and explicit "build questions/tasks from this passage only, keep the
CEFR aligned, English-only" instructions. Full-passage novelty uses a distinct
`bank-reading-passage-precheck:{id}` fingerprint; feedback-based exclusion (NotUseful /
DoNotShowSimilarSoon) applies to passages via the same `LearningActivity.BankResourceProvenanceJson`
match as other bank types. Provenance now records `type=ReadingPassage` plus the passage `id`,
`sourceId`, `contentFingerprint`, `selectionReason`, `cefrLevel`, and `title`. **Legacy fallback
is fully intact**: unsupported patterns, a missing/blocked passage, a selector exception, an AI
generation/validation failure, or an empty bank all still flow through the unchanged
`IAiActivityGenerator` path — D3 removed nothing. Vocabulary-primary behavior is unchanged from D2.
Speaking/listening/image/open-ended remain legacy. No external datasets, no Persian/bilingual seed
content. **Next Today-composer decision: Plan-Sync-After-D3 (2026-07-09, docs-only) resolved this —
Phase E8 (more resource depth/types) comes before Phase D4 (broader composer expansion), since
D1/D2/D3 proved the composer path end to end and the bottleneck is now bank breadth/depth. Phase E8
(2026-07-09) then completed that depth expansion (a second internal seed pack of 40 vocabulary / 20
grammar / 16 short reading references / 8 full reading passages across A1–B2, general-English-default;
it changed no composer/selector code — see docs/architecture/english-resource-bank-import-platform.md).
D4 remains the likely composer phase after E8; PG-v2 later. See docs/roadmap/road-map.md §1 / Decision
Log.**

**Phase D4 (2026-07-09) broadened the Today bank-first composer using the deeper E8 bank** — a
composer/selector expansion, **not a rewrite; every legacy fallback preserved.** `TodayBankResourceSelector`
now assembles **pattern-shaped multi-resource bundles** rather than D2/D3's flatter selection:
- **Vocabulary-primary** patterns: up to 3 vocabulary/usage targets (`role=primary`) + an
  opportunistic grammar hint (only when the pattern lists Grammar as a secondary skill) + an
  opportunistic short reading reference (`supporting`).
- **Reading comprehension/reorder** patterns (`reading_multiple_choice_single`/`_multi`,
  `reorder_paragraphs`): one full `CefrReadingPassage` anchor (`primary`) + up to 2 supporting
  vocabulary targets + optional grammar hint; falls back to a short-reference bundle when no
  suitable passage exists (novelty/context-excluded or none at level).
- **Reading cloze** patterns (`reading_fill_in_blanks`, `reading_writing_fill_in_blanks`): a short
  `CefrReadingReference` (`primary`) + supporting vocabulary/grammar — **never a full passage**.

A compact, centralized `PatternInstruction` helper adds one bounded, deterministic sentence per
pattern family to the prompt block (use-the-passage-only for comprehension; create-a-CEFR-aligned-
gapped-text-do-not-copy-a-passage for cloze; use-the-vocabulary-targets-naturally-do-not-default-to-
workplace for vocabulary/gap-fill). **General English is the default**: full passages whose bank
`ContextTags` mark them workplace-specific are skipped unless the learner's routed goal context is
workplace-specific (new `TodayBankSelectionRequest.PrefersWorkplaceContext`, set by
`ActivityMaterializationJob` from `ResolvedLearningGoalContext.WorkplaceSpecific`). Because the
short vocabulary/grammar/reading-reference bank tables carry no context tags (only
`CefrReadingPassage` stores them), this context filter necessarily applies to full passages only —
a documented limitation, not an oversight. Provenance (`LearningActivity.BankResourceProvenanceJson`)
gained a per-resource `role` (`primary`/`supporting`); it stays a flat JSON array, so the D2/D3
provenance and feedback-exclusion behavior is unchanged and no migration is required. **Preserved
throughout**: exact-CEFR-first / one-level-down-only-for-review / never-upward for every resource
type including supporting ones; the novelty precheck and NotUseful/DoNotShowSimilarSoon feedback
exclusion; and AI as composer/fallback (bank content is still appended to `TopicHint`, never
replacing `IAiActivityGenerator`). **Fallback paths unchanged**: unsupported pattern → legacy AI,
no/blocked bank resource → smaller bundle or legacy AI, selector exception → legacy AI, AI
generation/validation failure → existing retry/fallback. Practice Gym fallback and the
readiness/delivery queue are unchanged.

**D4 limitation `TODO-D4-1` — now closed by Phase E9 (2026-07-09)**: D4's general-English/workplace
context filter could originally only be applied to full reading passages, because `CefrReadingPassage`
was the only published bank entity storing context/focus/subskill/difficulty metadata — the lean
tables (`CefrVocabularyEntry`, `CefrGrammarProfileEntry`, `CefrReadingReference`) kept that metadata
only on the staging `ResourceCandidate`. **Phase E9 added those columns to the three lean published
tables** (publish mapping + idempotent traceable backfill + queryable filters via
`ResourceBankQueryService`), so a selector reading the published bank can now filter **all** bank
types by context/focus/subskill/difficulty — no staging re-query needed. The selector does not yet
*consume* this new lean-table filtering (D4's context filter still only acts on passages in code);
wiring the selector to filter supporting vocabulary/grammar/references by context/focus/subskill/
difficulty was **Phase D5 — Context-Aware Today Bank Selection and Topic Matching**.

**Phase D5 (2026-07-09) wired the selector to consume the E9 metadata — `TODO-E9-1` closed.** The
three lean per-type selectors are unified into a shared `SelectLeanAsync` that applies the E9
`ContextTag`/`FocusTag`/`Subskill`/`DifficultyBand` filters through a **deterministic strict→loose
relaxation ladder** (context kept longest; drop difficulty → focus → subskill → context → general,
de-duping absent-preference steps), each combined with the existing exact-CEFR-first /
review-only-widen-down policy. The first ladder step that yields an allowed candidate wins, so a
missing or unmatched preference relaxes safely rather than emptying the bundle. **General English is
the default across all bank types now**: when the learner is not workplace-routed
(`PrefersWorkplaceContext` false), workplace-tagged vocabulary/grammar/reading-reference rows are
skipped (via the E9 context metadata on the list DTOs) exactly as full passages already were; when
workplace-routed, workplace content is preferred via the E9 context filter. New request fields
`PreferredFocusTags`/`PreferredSubskill`/`PreferredDifficultyBand` feed the ladder;
`ActivityMaterializationJob` supplies `PreferredFocusTags` from
`ResolvedLearningGoalContext.FocusAreaKeys` (subskill/difficulty are supported but left null-fed —
the internal packs only populate difficulty on passages, tracked as `TODO-D5-1`). Topic matching is
**deterministic metadata matching only — no embeddings, no vector search**. D4's pattern-specific
instructions and `primary`/`supporting` roles are preserved; `TodayBankSelectedResource` gained
`AppliedFilters`/`MatchedContextTags`, surfaced in `BankResourceProvenanceJson` and summarized as a
one-line selection-emphasis note in the prompt block. Novelty and NotUseful/DoNotShowSimilarSoon
feedback exclusions still apply after filtering; a fully-relaxed empty result (e.g. only workplace
rows for a general learner) yields no bank bundle and the caller runs the unchanged legacy AI
generator; unsupported patterns still skip to legacy. See
`docs/architecture/english-resource-bank-import-platform.md` (E9 detail section) and
`docs/roadmap/road-map.md` §1 / Decision Log.

**D5-discovered limitation → Phase E10 next (Plan-Sync-After-D5, 2026-07-09, docs-only)**: D5's
selector filtering is only as good as the published bank's metadata. E9 fixed the *schema* (the lean
tables now have the columns) and D5 wired the selector to *consume* them, but the internal E6/E7/E8
lean packs were authored with context tags + subskill only — thin focus/difficulty metadata (only
full passages carry difficulty/focus densely — `TODO-D5-1`) — and `ActivityMaterializationJob`
null-feeds `PreferredSubskill`/`PreferredDifficultyBand` because there is no reliable per-request
source yet. So D5's difficulty/focus filtering relaxes away on the lean tables today. The next
limitation is therefore **content depth / metadata quality, not schema or wiring**, and it would
also bound a future PG-v2 selector. **Plan-Sync-After-D5 resolved to fix this in Phase E10 (Internal
Bank Metadata Depth Expansion for Focus and Difficulty)** — enriching/repairing the existing internal
lean rows' focus/difficulty/subskill metadata through the existing pipeline / safe idempotent
metadata-repair path (no schema change, no external datasets, no direct final-table seeding) — before
a deeper Today topic-matching phase (**Phase D6 — Today Topic Matching and Subskill-Aware Resource
Selection**) or PG-v2.

**Phase E10 (2026-07-09) then delivered that depth — `TODO-D5-1` resolved.** `InternalBankMetadataDepthSeeder`
(idempotent startup step after the E9 backfill) derives the two missing lean-row fields from each
row's own already-published metadata: **difficulty band from CEFR** (A1→1, A2→2, B1→3, B2→4, C1/C2→5)
and a **focus tag from the row's subskill** (e.g. `vocabulary.collocation` → `["collocation"]`). It
touches only `Internal/Original` rows traceable to exactly one published `ResourceCandidate`, fills
only empty fields (never overwrites authored values such as the E8 passages' difficulty/focus),
preserves subskill + context, never inserts a bank row, and is a no-op on rerun. After E10 every
internal lean row carries context + subskill + difficulty + focus, so the D5 selector's
difficulty/focus filters now have data to act on — **the selector code was unchanged; E10 only
enriched the data it reads.** The one residual (`TODO-E10-1`) is that `ActivityMaterializationJob`
still null-feeds `PreferredSubskill`/`PreferredDifficultyBand` at runtime (no reliable per-request
source yet) — a Phase D6 concern, not a data-depth gap. See `docs/roadmap/road-map.md` §1 / Decision
Log and `docs/architecture/english-resource-bank-import-platform.md`.

**Phase D6 (2026-07-09) closed `TODO-E10-1` and made Today bundles topic-aware.** Two changes, both
deterministic (no embeddings/vector/semantic search):

1. **Reliable runtime signal feeding.** `CurriculumRoutingRecommendation` now surfaces the matched
   objective's `Subskill`. `ActivityMaterializationJob` feeds the selector request from routing:
   `PreferredSubskill = routing.Subskill`; `PreferredFocusTags` prefers `routing.FocusTags` (objective
   tags), falling back to the learner's resolved focus areas; `PreferredDifficultyBand` is derived
   conservatively from `StudentProfile.DifficultyPreference` relative to the routed CEFR's normal band
   via the shared `CefrDifficultyBand` helper (Gentle → one band lower same-CEFR, Balanced →
   CEFR-normal band, Challenging → one band higher, unknown/unmappable → null). Subskill/difficulty
   filtering now activates from live routing, not only from tests.
2. **Anchor-context topic matching for supporting resources.** In reading bundles, after the primary
   passage/reference is chosen, its first non-workplace context tag becomes a **topic anchor**:
   `BuildFilterLadder` prepends strict topic-anchor rungs (`ContextTag = anchor`, combined with the
   same focus/subskill/difficulty preferences) ahead of the D5 general ladder, so supporting
   vocabulary/grammar prefer the passage's topic (a travel passage pulls travel vocabulary). Workplace
   is never used as a topic anchor for a general-English learner, and the D5 general-English
   workplace-exclusion still applies to every supporting row. The anchor rungs relax all the way down
   to the general attempt, so topic matching can only narrow, never empty, the bundle — and the caller
   still falls back to legacy AI generation when no bank resource remains. Provenance records the match
   in `AppliedFilters` (e.g. `context=travel(topic-anchor)`); the flat provenance-array shape is
   unchanged. **Residual:** E10's derived difficulty bands are CEFR-uniform, so difficulty narrowing is
   effectively a no-op for Balanced / a relaxation for Gentle/Challenging on today's internal data; the
   mechanism is correct and covered by mixed-band tests, and becomes selective once genuinely
   mixed-difficulty content exists. No schema change, no migration, no selector rewrite.

**Plan-Sync-G0 (2026-07-09, docs-only)** reframes, but does not delete, the readiness-pool
lifecycle this file's fallback/generation flow relies on: `StudentActivityReadinessItem`/
`IStudentActivityReadinessPoolService` is now described as a **"Student Activity Assignment /
Delivery Queue"** rather than an "AI-generated activity cache" — the underlying selected →
assigned → ready → reserved → completed/expired/stale/failed state machine is unchanged and
still fully load-bearing for both Today and Practice Gym. **Phase G0 (done, 2026-07-09,
docs/audit-only)** then audited every admin page/API/job that touches readiness/pool terminology
and confirmed exactly this: `ActivityMaterializationJob`, `LessonBatchGenerationJob`, and the
readiness replenishment/buffer jobs are all classified keep (with the legacy freeform fallback
portions deferred to Phase F, per-pattern), and `StudentActivityReadinessItem` is classified
keep-reframe, never delete. This file's own primary subject remains the AI generation engine, not
the readiness pool's architecture — see `docs/architecture/bank-first-admin-backend-surface-audit.md`
for the full surface classification, `docs/roadmap/road-map.md` §1 and Decision Log (Plan-Sync-G0
and Phase G0 entries) for the decisions, and `docs/architecture/readiness-pool.md` for the pool's
entity/lifecycle detail.

**`GenerationStatus` default-value bugfix (Bugfix-D1A, 2026-07-08):** while building D1's
regression tests, a pre-existing bug was found in `LearningSessionConfiguration`:
`GenerationStatus` was configured with EF `.HasDefaultValue(GenerationStatus.Ready)`. Because
`GenerationStatus.Pending == 0` (the enum's own CLR default), EF Core's "omit CLR-default
property values from the INSERT, let the DB default apply" convention silently discarded an
explicit `session.MarkGenerationPending()` call made before a brand-new session's first
`SaveChangesAsync` — the row always persisted as `Ready` regardless.
`LessonBatchGenerationJob.MaterializeSessionsAsync` uses exactly that construction order
(`new LearningSession(...)` → `MarkGenerationPending()` → `Add` → `SaveChangesAsync`), so
background-generated sessions never actually reached a real `Pending` state in the database.
Practical impact: `StudentReadinessAuditService`'s "no stuck session generation" check (which
looks for sessions stuck in `Pending`/`Failed` for over 30 minutes) could never detect a real
stuck-generation incident, since sessions always read back as `Ready` immediately. **Fixed** by
removing the `HasDefaultValue(...)` configuration (migration
`Bugfix_D1A_RemoveGenerationStatusDefault` — an `ALTER COLUMN ... DROP DEFAULT`, no data change,
no column type change); `LearningSession` instances already default to `Ready` via their own
property initializer in code, so no DB-side default was ever needed for correctness. One
existing test (`LessonBatchGenerationJobTests`) had its assertion corrected from `Ready` to
`Pending` — it had been unknowingly asserting the bug's symptom rather than the intended
behavior, since `ActivityMaterializationJob` (the only thing that ever calls
`MarkGenerationReady`) never actually runs within that test. See
`docs/roadmap/road-map.md` Decision Log (2026-07-08, Bugfix-D1A) for the full audit and fix
rationale, including the review of similar `HasDefaultValue`-on-enum patterns elsewhere in the
codebase (none found to carry the same live risk).

---

## How Fallback Works

If `IAiActivityGenerator.GenerateActivityContentAsync` throws for any reason (network failure, rate limit, malformed response, provider misconfiguration), `ActivityGetHandler` catches the exception, logs a warning, and queries for a `SystemFallback` activity:

```
SELECT * FROM learning_activities
WHERE activity_type = @type
  AND source = 'SystemFallback'
  AND is_active = true
ORDER BY RANDOM()
LIMIT 1
```

`SystemFallback` rows are seeded by `LearningActivitySeeder` at startup from the legacy `writing_scenarios` table. They have no `LearningModuleId`.

**The handler never throws a 500.** If no fallback exists, it throws `InvalidOperationException` with a message that surfaces as a 500 only in the case of a misconfigured environment (no seed data). In production, seed data is always present.

Path generation uses the same pattern: `ILearningPathGenerator` falls back to `DefaultPathFactory` (a hard-coded 5-module Workplace English path) if AI generation fails.

---

## How Future Activity Types Fit

The content schema is intentionally type-specific and stored as JSONB. Adding a new activity type requires:

### 1. New prompt key in `ai_prompts` table

```
activity_generate_speaking
activity_evaluate_speaking
```

Seeded in `AiPromptSeeder`. Admin can edit the template text without a code deploy.

### 2. New content schema (documented on `LearningActivity`)

Each type defines its own JSON shape. Examples:

**`WritingScenario` (current):**
```json
{
  "situation": "...",
  "learningGoal": "...",
  "targetPhrases": ["..."],
  "targetVocabulary": ["..."],
  "exampleText": "...",
  "commonMistakeToAvoid": "...",
  "instructionInSourceLanguage": "..."
}
```

**`SpeakingRolePlay` (planned):**
```json
{
  "scenario": "...",
  "yourRole": "...",
  "otherRole": "...",
  "openingLine": "...",
  "targetPhrases": ["..."],
  "instructionInSourceLanguage": "..."
}
```

**`ListeningComprehension` (planned):**
```json
{
  "passage": "...",
  "questions": [{ "question": "...", "options": ["..."], "correctIndex": 0 }],
  "vocabulary": ["..."],
  "instructionInSourceLanguage": "..."
}
```

**`VocabularyPractice` (planned):**
```json
{
  "words": [{ "word": "...", "definition": "...", "exampleSentence": "..." }],
  "instructionInSourceLanguage": "..."
}
```

**`PronunciationPractice` (planned):**
```json
{
  "targetSentence": "...",
  "phonemesFocus": ["..."],
  "tipInSourceLanguage": "...",
  "instructionInSourceLanguage": "..."
}
```

**`ReadingTask` (planned):**
```json
{
  "passage": "...",
  "comprehensionQuestions": ["..."],
  "vocabularyToNotice": ["..."],
  "instructionInSourceLanguage": "..."
}
```

### 3. Infrastructure dispatch in `ActivityGetHandler` and `ActivitySubmitHandler`

`ActivityGetHandler.MapToDto` switches on `ActivityType` to deserialise the correct schema into `ActivityDto` fields. Add a new `case ActivityType.SpeakingRolePlay:` block.

`ActivitySubmitHandler` similarly switches to build the correct `ActivityEvaluationContext` payload.

### 4. Frontend renderer

For pattern-generated activities, the frontend renders by `ActivityDto.interactionMode`, not by `ActivityType`.

`ActivityLessonComponent` passes the activity into `ExerciseRendererComponent`, which dispatches to the small interaction renderer:

- `ReadOnlyStepComponent`
- `FreeTextEntryComponent`
- `MatchingPairsComponent`
- `GapFillComponent`
- `AudioAndFreeTextComponent`
- `AudioAndGapFillComponent`
- `ChatReplyComponent`

`ActivityType` remains the backend persistence/evaluation bucket. Legacy non-pattern activities continue to render safely through the existing activity UI, and pattern-aware free-text content can fall back to `FreeTextEntryComponent`.

`ActivityDto` now includes:

```typescript
interface ActivityDto {
  activityType: ActivityType;
  interactionMode: InteractionMode | null;
  exercisePatternKey: string | null;
  contentJson?: string | null;
}
```

`contentJson` is returned only for pattern-keyed activities that need raw schema data for the renderer. Legacy listening activities must not expose raw answer-bearing JSON before submission.

### 5. No domain or DB changes

The `ActivityType` enum gets a new value. No table changes. No migration. The JSONB column absorbs the new schema transparently.

---

## Staged Activity Content (`module_stage_v1`)

`LearningActivity.AiGeneratedContentJson` can now store staged module content
for migrated legacy activity types. The staged shape separates teaching,
practice, and evaluation planning:

* `learnContent` contains teaching-only content.
* `practiceContent` contains the actual exercise.
* `feedbackPlan` contains criteria and rubric guidance.

The target staged metadata includes:

* `schemaVersion: "module_stage_v1"`
* `primarySkill`
* `secondarySkills`
* `exerciseType`
* `moduleGoal`

PR1 completed this migration for `ListeningComprehension`. PR2 completed it
for `WritingScenario`. Remaining legacy types and pattern-backed activities are
still pending.

### WritingScenario staged content

New `WritingScenario` generation uses prompt key `activity_generate_writing`
and returns `exerciseType: "writing_scenario"`. The Learn stage teaches writing
structure, tone, useful phrases, common mistakes, and planning strategy only. It
must not include the final writing prompt, textarea content, submitted answer,
expected final answer, answer key, answer controls, or submit/check labels.

The Practice stage owns the writing task. Its `practiceContent.exerciseData`
contains the durable task fields used by the UI and evaluator:

* `prompt`
* `situation`
* `audience`
* `tone`
* optional `expectedLength`
* optional `requiredPhrases`
* optional `targetVocabulary`
* optional `successChecklist`

Old flat WritingScenario JSON remains supported. The API adapts it to
`legacy_adapted_v1` so old activities and history continue rendering and
evaluating.

### SpeakingRolePlay staged content

New `SpeakingRolePlay` generation uses prompt key
`activity_generate_speaking_roleplay` and returns
`exerciseType: "speaking_roleplay"`. The Learn stage teaches speaking strategy,
key phrases, common mistakes, and scenario framing only. It must not include
recording controls, microphone instructions, `startRecording`, `stopRecording`,
or the final task prompt.

The Practice stage owns the speaking task. Its `practiceContent.exerciseData`
contains:

* `role` — student's role in the scenario
* `partnerRole` — the AI/partner role (required)
* `situation` — context for the conversation
* `prompt` — the speaking task instruction (required)
* optional `expectedResponseLength`
* optional `tone`
* optional `requiredPhrases`
* optional `targetVocabulary`
* optional `successChecklist`

Old flat SpeakingRolePlay JSON (fields: `studentRole`, `listenerRole`,
`speakingGoal`, `prompt`) remains supported. The API adapts it to
`legacy_adapted_v1`. Field mapping: `studentRole` → `role`,
`listenerRole` → `partnerRole`.

The prompt token budget increased from 900 to 1600 input tokens and from 800 to
1200 output tokens in `DefaultAiSeeder` to accommodate the larger staged prompt.

`SpeakingRolePlayEvaluator` exposes `ExtractExerciseDataJson` to feed only
`practiceContent.exerciseData` into the evaluation prompt, mirroring the Writing
evaluator pattern.

Future phases still own Practice Gym pre-generation pools, Today background
generation, MinIO/audio lifecycle, new listening exercise types, ModuleRun
persistence, and staged migrations for pattern-backed activities.

## Pattern Evaluation Engine — evaluation flow

`ActivitySubmitHandler` now routes pattern-keyed attempts through `IPatternEvaluationRouter`:

```
ActivitySubmitHandler.HandlePatternEvaluationAsync(command, profile, activity, module, ct)
    │
    ├── IPatternEvaluationRouter.RouteAsync(request)
    │     │
    │     ├── switch MarkingMode:
    │     │     ExactMatch     → ExactMatchEvaluator
    │     │     KeyedSelection → KeyedSelectionEvaluator
    │     │     AiStructured   → AiStructuredEvaluator (AI call)
    │     │     AiOpenEnded    → AiOpenEndedEvaluator  (AI call)
    │     │     NoMarking      → NoMarkingEvaluator
    │     │
    │     └── PatternEvaluationResult { score, maxScore, percentage, passed,
    │                                   completed, itemResults, corrections,
    │                                   skillImpacts, memorySignals, ... }
    │
    ├── Persist ActivityAttempt.EvaluationResultJson / Passed / Completed / ...
    │
    ├── PatternSkillUpdateService.ApplyAsync()   ← best-effort, swallowed
    │     Upserts StudentSkillProfile from skillImpacts
    │     Synthesises from pattern key when impacts empty
    │
    └── StudentMemoryService.UpdateMemoryAsync() ← best-effort, 8s timeout
          Sends compact memory packet (no raw submitted text)
```

`PatternEvaluationResult` is stored as canonical JSON in `ActivityAttempt.EvaluationResultJson`.
Scalar fields (`MaxScore`, `Percentage`, `Passed`, `Completed`, `MarkingMode`) are also persisted for queryability.

The `ActivityFeedbackDto` now includes `patternEvaluation: PatternEvaluationDto | null` as an additive field. Legacy attempts return `null` here; pattern-aware attempts return the full result. Frontend shows `PatternEvaluationResultComponent` when non-null; legacy sections gate on `!patternEvaluation`.

## Activity Type Roadmap

| Type | Status | Notes |
|------|--------|-------|
| `WritingScenario` | Live | AI-generated + SystemFallback seeded from legacy data; cadence picks routed via `open_writing_task` pattern (Step 4) |
| `SpeakingRolePlay` | Live (MVP fake STT) | `FakeSpeechToTextService`; real STT deferred; cadence picks routed via `speaking_roleplay_turn` pattern + `AudioResponse` interaction mode (Step 5) |
| `ListeningComprehension` | Live | TTS audio via `PlacementAudioService`; server-streamed; staged content (`module_stage_v1`) — see below |
| `VocabularyPractice` | Live | Gap-fill and matching patterns via Pattern Engine |
| `PronunciationPractice` | Not started | Needs STT + phoneme comparison |
| `ReadingTask` | Not started | Simplest after Writing — no audio |

---

## Staged Activity Content (`module_stage_v1`)

### Why

The Learn step ("Today's Lesson" / Practice Gym Step 1) must teach a general
strategy and must never leak practice exercise data (audio script, questions,
expected answers, transcript). Before this schema, `AiGeneratedContentJson`
was one flat exercise-shaped payload, and presenters forwarded it unchanged to
both Learn and Practice — so Listening's Learn step rendered the audio player,
"Answer questions" CTA, and a transcript-lock message. See
`docs/reviews/2026-06-15-learn-practice-feedback-structure-investigation.md`
for the original investigation.

### Schema contract

New AI generations for migrated activity types return JSON shaped as:

```json
{
  "schemaVersion": "module_stage_v1",
  "title": "...",
  "moduleGoal": "...",
  "skillFocus": "listening",
  "exerciseType": "listening_comprehension",
  "learnContent": {
    "teachingTitle": "...",
    "explanation": "...",
    "keyPoints": ["..."],
    "examples": [{ "phrase": "...", "meaning": "...", "note": "..." }],
    "strategy": "...",
    "commonMistakes": ["..."],
    "sourceLanguageSupport": null
  },
  "practiceContent": {
    "instructions": "...",
    "scenario": "...",
    "task": null,
    "exerciseData": { "...": "type-specific exercise payload" }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["..."],
    "rubric": [{ "criterion": "...", "description": "...", "weight": 0.4 }],
    "feedbackFocus": "...",
    "successCriteria": ["..."]
  }
}
```

C# types live in `LinguaCoach.Application.Activity.ModuleStageContent`
(`LearnContentDto`, `PracticeContentDto`, `FeedbackPlanDto`, `StageContentDto`,
and the wire-shape `ModuleStageWireDto` used only for deserializing
`learnContent`/`practiceContent` property names before mapping to the
`Learn`/`Practice` domain property names on `StageContentDto`).

For `ListeningComprehension`, `practiceContent.exerciseData` carries
`speakerRole`, `listenerRole`, `audioScript`, `transcriptAvailableAfterSubmit`,
`questions[]`, and `responseTask`. An optional `audioAssetUrl` field is
reserved (not yet populated) for a future pre-generation pipeline that stores
synthesized audio in object storage ahead of time.

### Validation

`ModuleStageContentValidator.Validate(JsonElement, ActivityType)` checks:
- `schemaVersion`, `learnContent`, `practiceContent`, `feedbackPlan` are present and are objects.
- `learnContent` contains none of the forbidden keys: `audioScript`, `audioUrl`,
  `questions`, `expectedAnswer`, `correctAnswer`, `answerKey`, `gaps`, `pairs`,
  `transcriptAvailableAfterSubmit`, `transcript`, `exerciseData`, `interactionMode`.
- `practiceContent`/`exerciseData` contains the required keys for the activity
  type, per `RequiredPracticeKeysByType` (currently only
  `ListeningComprehension → ["audioScript", "questions"]`).

`AiActivityGeneratorHandler` retries generation once on validation failure,
then throws `AiResponseValidationException` if the retry also fails.

### `ActivityDto.StageContent`

Additive, nullable field on `ActivityDto`. Populated by
`ActivityGetHandler.BuildStageContent`:

- `schemaVersion == "module_stage_v1"` → deserialized 1:1 into `StageContentDto`.
- Any other/old flat JSON → `AdaptLegacyListening` produces a
  `legacy_adapted_v1` `StageContentDto`: generic/sparse `learnContent` (no real
  teaching content existed in old rows — this is intentional, not a bug), and
  `practiceContent.exerciseData` is the **entire original flat JSON**
  (`root.Clone()`), so old activities render in Practice exactly as before.
- Activity types not yet migrated → `StageContent` is `null`; their presenters
  are unaffected.

`ActivityController.ToActivityResponse` maps `StageContentDto` to the
`stageContent` field on the API response (camelCase `learn`/`practice`/`feedbackPlan`).

### Evaluation

`ListeningComprehensionEvaluator.ExtractExerciseDataJson` unwraps
`practiceContent.exerciseData` when `schemaVersion` is `module_stage_v1` or
`legacy_adapted_v1`, falling back to the raw JSON for any other shape. Scoring
logic, `MiniLesson`/`NextImprovementStep` text, and `ListeningFeedbackPayload`
are unchanged.

### Frontend

- `activity.models.ts` adds `StageContentDto` and friends, plus
  `ActivityDto.stageContent: StageContentDto | null`.
- `LegacyListeningPresenter.teachContent(activity)` returns
  `{ block: 'stagedLearning', learn: activity.stageContent.learn, ... }` when
  `stageContent` is present, else falls back to the legacy `listeningLearning`
  block (defensive only — the backend adapter always populates `StageContent`
  for `ListeningComprehension`).
- `activity-teach-page` has a new `@case ('stagedLearning')` rendering only
  `learnContent` fields (heading, explanation, key points, examples, strategy,
  common mistakes, source-language support) plus the CTA. The old
  `@case ('listeningLearning')` is marked deprecated, not yet removed.
- `activity-practice-page`'s listening case reads
  `activity.stageContent.practice.{scenario,exerciseData}` via a
  `listeningExerciseData` getter, covering both `module_stage_v1` and
  `legacy_adapted_v1` shapes. Audio fields (`audioUrl`, `audioAvailable`, etc.)
  remain flat `ActivityDto` fields — server-computed, not part of the AI JSON.

### Per-type migration status

| Activity type | `StageContent` populated? |
|---|---|
| `ListeningComprehension` | Yes (`module_stage_v1` for new rows, `legacy_adapted_v1` for old rows) |
| `WritingScenario` | Yes (`module_stage_v1` for new rows, `legacy_adapted_v1` for old rows) |
| `SpeakingRolePlay` | Yes (`module_stage_v1` for new rows, `legacy_adapted_v1` for old rows) |
| All other types/patterns | No (`null`) — see `docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` for the follow-up plan |

---

## Key Design Invariants

- **Domain does not depend on EF Core.** `LearningPath`, `LearningModule`, `LearningActivity`, and `ActivityAttempt` are plain C# classes. EF configurations live in `LinguaCoach.Persistence`.
- **Business logic lives in Infrastructure handlers, not controllers.** `ActivityController` only validates, delegates to handler, and maps exceptions to HTTP status codes.
- **AI failures are never surfaced as 500s.** Every AI call site has a catch-and-fallback or catch-and-graceful-degradation path.
- **Prompts are data, not code.** All prompt templates live in the `ai_prompts` table, versioned, editable by Admin without a deployment.
- **JSONB content is type-specific but schema-less at the DB level.** The application enforces the schema in code. This allows schema evolution without migrations.
- **`instructionInSourceLanguage` is always present in all activity types.** It is the bridge for Persian (or any source-language) students who need the task explained in their native language. It must be rendered with `dir="rtl"` for RTL source languages.

## Exercise Type Catalog

Phase 3A adds `ExerciseTypeDefinition` as the durable catalog for future exercise selection.
Skills and exercise types are separate concepts. A module can target `primarySkill`
and `secondarySkills`, while its Practice stage uses one `exerciseType`.

The catalog is now the source of truth for future generation eligibility. Each row stores:

* stable `Key`
* display metadata
* `PrimarySkill` and `SecondarySkillsJson`
* admin `IsEnabled`
* `ImplementationStatus`
* renderer, evaluator, prompt, legacy `ActivityType`, and pattern mapping keys
* duration, audio, image, Practice Gym, and Today support flags

`IsAvailableForGeneration` is computed by the application from:

```text
IsEnabled && ImplementationStatus == "ready"
```

Admin enable or disable affects future generation only. Existing `LearningActivity`
and `ActivityAttempt` records still load through legacy `/activity` compatibility,
even if their exercise type is later disabled.

Planned future exercise formats are seeded as catalog rows with
`ImplementationStatus = "planned"`. Admins can see them, but enabling them does not
make them runnable until their renderer, evaluator, prompt, and safe generation path
are marked ready.

Generation filters now check the catalog before creating new work:

* Practice Gym pattern requests require enabled, ready, Practice Gym-supported rows.
* Practice Gym background cache queues only enabled, ready, Practice Gym-supported rows.
* Today session generation removes disabled or unavailable deterministic patterns.
* Explicit legacy `ActivityType` requests require at least one enabled ready mapped type.

Renderer and evaluator selection still keep legacy compatibility, but new staged
module work should increasingly select by catalog `exerciseType` rather than skill
or broad `ActivityType` alone.

## Exercise Type Registry

`ExerciseTypeDefinition` is now the routing source for exercise type metadata. `IExerciseTypeRegistry` resolves normalized `exerciseType` keys to the catalog definition, primary skill, secondary skills, renderer key, evaluator key, generation prompt key, legacy `ActivityType`, and optional `ExercisePatternKey`.

New planning and generation code should choose by `exerciseType` first. `ActivityType` remains the persistence and legacy compatibility bucket. `ExercisePatternKey` remains the pattern-engine compatibility mapping for current renderers and evaluators.

The `/api/activity/next` endpoint now accepts `exerciseType=<key>` for ready implemented types. Existing `type=` and `pattern=` query parameters remain supported. Planned planned future exercise format catalog rows are visible in Admin but are not generation-eligible, Practice Gym-routable, or Today-routable until their implementation status becomes `ready`.

Skill selection should map to eligible exercise types. It must not assume one fixed legacy activity class per skill. The registry exposes skill helpers for Practice Gym, Today, pre-generation jobs, and future adaptive planning.

## Practice Gym registry selection

Practice Gym has two selection modes. Skill cards resolve dynamically through
the ExerciseType registry. Exact exercise type cards keep exact
`exerciseType=<key>` routing.

For skill cards, the student selects a primary skill such as Listening, Speaking,
Writing, Reading, Vocabulary, or Grammar. The backend selects only definitions
that are enabled, `implementationStatus = ready`, available for generation,
supported by Practice Gym, and matching the selected `primarySkill`. If no
eligible definition exists, the API returns a safe no-result response and the
frontend does not route to activity generation.

This means skill cards no longer represent one fixed activity type. Listening
can resolve to any ready Practice Gym-supported listening exercise type. Planned
Planned future exercise format rows remain catalog-visible but blocked. They are not generated,
routed, rendered, or evaluated until future implementation work makes them ready.

The current strategy is deterministic and picks the first eligible registry row
by stable ordering. Future Practice Gym pre-generation should reuse these
eligibility rules before selecting from a ready pool. Adaptive selection can then
consider weak skills, recent attempts, variety, spaced repetition, admin
priority, and Today/Gym pool availability.

## Practice Gym pre-generation pool (foundation)

`GET /api/activity/practice-gym/next` is the pool-aware entry point for both
Practice Gym skill cards and exact exercise type cards. It replaces direct
`/activity?exerciseType=...&returnTo=/practice` routing as the click-to-start
flow, while `/api/activity/next` (`exerciseType=`, `type=`, `pattern=`) remains
unchanged for backward compatibility and as the fallback generation path.

Flow:

1. Resolve eligible exercise type(s) via `IExerciseTypeRegistry`
   (`IsEnabled && ImplementationStatus == "ready" && SupportsPracticeGym`),
   either the exact requested `exerciseType` or all types matching the
   requested `primarySkill`.
2. `IPracticeGymPoolService.FindReadyForExerciseTypeAsync` /
   `FindReadyForSkillAsync` look for a `PracticeActivityCache` row for that
   student with `Status == Ready`, a linked `LearningActivity`, and not
   expired. If found, the row is marked `Assigned` (reserved) and its
   `LearningActivityId` is returned with `source: "pool"`.
3. If no ready pool row exists, the endpoint falls back to the existing
   on-demand path (`GetNextActivityQuery` → `ActivityGetHandler`), returning
   `source: "onDemandFallback"`.
4. If neither a skill nor an exercise type is provided, or no eligible/ready
   exercise type exists for the request, the endpoint returns
   `hasActivity: false` with a `reason` string and does not route.

### Design decision — reuse `PracticeActivityCache`, no new pool table

The pool is implemented as a lookup over the existing `practice_activity_cache`
table rather than a new `PracticeGymPoolItem` entity. `PatternKey` doubles as
the exercise type key for pattern-backed exercise types. `PracticeCacheStatus`
gained a `Failed` value (alongside `Pending/Ready/Assigned/Completed/Expired`)
for pool items that could not be served; `Assigned` is the reservation state
(no separate `Reserved` status was introduced). This is an additive enum value
stored as `int`, requiring no schema migration.

Reservation (`ReserveReadyItemAsync`) mirrors
`ActivityGetHandler.TryAssignReadyPracticeCacheAsync`: it selects the oldest
`Ready` row for `(StudentProfileId, PatternKey)`, verifies the linked
`LearningActivity` is still active (else `MarkExpired` and retry), then calls
`MarkAssigned()` with a `DbUpdateConcurrencyException` retry loop. Because both
paths mark rows `Assigned` before returning them, a row can never be served to
two requests concurrently — if the pool service reserves the only `Ready` row,
the on-demand fallback's own cache lookup finds nothing and proceeds to AI
generation.

### Admin-disable behaviour

Both pool lookup methods filter through `IExerciseTypeRegistry`, so disabled or
`planned` exercise types are never returned from the pool or used for fallback
generation — no separate expiry of existing `PracticeActivityCache` rows for
disabled types is needed; they simply stop being selectable.

### Explicitly out of scope for this foundation

* Today lesson pre-generation (`/api/sessions/*` is unaffected).
* MinIO/object-storage audio asset lifecycle changes.
* New planned future exercise renderers or evaluators.
* Background pool-fill changes — `PracticeGymGenerationJob` /
  `PracticeGymBufferRefillJob` already populate `PracticeActivityCache` via the
  same registry eligibility rules and are unchanged.

### VocabularyPractice staged content

`VocabularyPractice` now supports `module_stage_v1` content for new deterministic vocabulary activities. The generator remains database-seeded and deterministic. It selects existing `StudentVocabularyItem` rows and stages them into Learn, Practice, and Feedback sections.

The Learn section contains teaching only: meanings, usage notes, example contexts, memory strategy, and common mistakes. It must not contain answer controls, selected answers, answer keys, matching pairs, blank tasks, or submit/check labels.

The Practice section contains the actual vocabulary task under `practiceContent.exerciseData`. Required practice data is `items` and `practiceMode`. Optional fields such as `options`, `partOfSpeech`, `correctAnswer`, and `example` are not over-constrained.

Old flat vocabulary JSON is adapted to `legacy_adapted_v1`. The adapter teaches from available terms and explanations, while preserving the original vocabulary practice fields inside `practiceContent.exerciseData` so existing student history and attempts continue to render.

`VocabularyPracticeEvaluator` reads expected answers from staged `practiceContent.exerciseData.items` and still falls back to old flat `items`. Feedback shape remains compatible with the legacy vocabulary review UI.

Completed staged migrations: `ListeningComprehension`, `WritingScenario`, `SpeakingRolePlay`, and `VocabularyPractice`. Remaining pattern-backed activities are pending. Planned future exercise formats remain planned and non-runnable unless implemented end-to-end. Today pre-generation and MinIO/audio lifecycle remain future phases.

## Runnable planned future exercise formats

**Phase 8A (2026-06-15):** `reading_multiple_choice_single` — first runnable planned future reading format. Uses `ActivityType.ReadingTask`, `InteractionMode.MultipleChoice`, `MarkingMode.KeyedSelection`. Single-answer; evaluated deterministically.

**Phase 8B (2026-06-15):** `reading_multiple_choice_multi` — second runnable planned future reading format. Uses `ActivityType.ReadingTask`, `InteractionMode.MultipleChoiceMulti` (new enum value 12), `MarkingMode.KeyedSelection`. Multi-answer; evaluated deterministically by comparing the submitted set of option IDs to `correctOptionIds`. Identifies missed correct options and false positives.

**Phase 8C (2026-06-15):** `reading_fill_in_blanks` — third runnable planned future reading format. Uses `ActivityType.ReadingTask`, `InteractionMode.ReadingFillInBlanks` (new enum value 13), `MarkingMode.ExactMatch`. Passage with `{{gapN}}` tokens; per-gap dropdown options; evaluated deterministically by `ExactMatchEvaluator` gap-keyed branch.

**Phase 8D (2026-06-15):** `reorder_paragraphs` — fourth runnable planned future reading format. Uses `ActivityType.ReadingTask`, `InteractionMode.ReorderParagraphs` (new enum value 14), `MarkingMode.ExactMatch`. Student reorders shuffled paragraph blocks using move-up/move-down controls; evaluated deterministically by `ExactMatchEvaluator` position-keyed branch. Submitted answer shape: `{ orderedIds: string[] }`. Per-position scoring; partial credit supported.

**Phase 8E (2026-06-15):** `reading_writing_fill_in_blanks` — fifth runnable planned future reading format; also targets writing (secondary skill). Uses `ActivityType.ReadingTask`, `InteractionMode.ReadingWritingFillInBlanks` (new enum value 15), `MarkingMode.ExactMatch`. Identical JSON schema and dropdown UI to `reading_fill_in_blanks`; AI prompt emphasises word-form and collocation knowledge. `ExactMatchEvaluator` `ParseExpectedItems` branch extended with OR condition. All reading-primary exercise types are now Ready.

All other planned future exercise formats remain `planned` and non-generation-eligible.

## Reference implementation pattern

`reading_multiple_choice_single` (Phase 8A) is the reference implementation
for converting a planned future exercise format (reading, writing, listening,
or speaking) into a runnable one. It uses `ActivityType.ReadingTask` and
`InteractionMode.MultipleChoice` — both pre-existing, unused enum values — and
`MarkingMode.KeyedSelection` with a deterministic, non-AI evaluator branch
(no new MarkingMode or evaluator class).

**Phase 8N (2026-06-16):** Configurable practice item counts foundation (not a
new format). `ExerciseTypeDefinition` gained six count fields —
`MinItemsPerPractice`, `DefaultItemsPerPractice`, `MaxItemsPerPractice`,
`MinOptionsPerItem`, `DefaultOptionsPerItem`, `MaxOptionsPerItem` — with
`min <= default <= max` and non-negative invariants enforced in the entity and
on admin update. Seeded per-key for all ready and planned types; exposed on the
registry entry and admin DTO; editable via admin PATCH and the admin UI.
`AiActivityGeneratorHandler` injects the counts as prompt variables, and
`ModuleStageContentValidator.Validate` accepts an optional `PracticeCountSettings`
to enforce item-count ranges (fill-in-blanks, reorder, highlight-incorrect-words)
and option-count ranges (MCQ, select-missing-word, highlight-correct-summary).
Counts are configuration only and never change readiness/runnable status. See
[practice-item-sets.md](practice-item-sets.md).

The recipe to repeat for the next planned future exercise format:

1. Add an `ExercisePatternKey` constant and a full `ExercisePatternDefinition`
   row (not just a catalog row) — pool reservation in
   `PracticeGymPoolService` requires a non-empty `ExercisePatternKey`.
2. Convert the existing `Planned` row in `ExerciseTypeDefinitionSeeder` to
   `Ready`, keeping the total catalog row count unchanged.
3. Add a `RequiredPracticeKeysByPatternKey` entry (and forbidden Learn-content
   keys) in `ModuleStageContentValidator` — pattern-key entries take
   precedence over `ActivityType`-level requirements.
4. Allow the new `ActivityType` past the generator's unsupported-type guard
   and add it to the staged-validation switch in
   `AiActivityGeneratorHandler`.
5. Add a deterministic branch to the existing evaluator for the chosen
   `MarkingMode` (e.g. `KeyedSelectionEvaluator`, `ExactMatchEvaluator`) keyed
   on `ExercisePatternKey` — avoid new MarkingModes/evaluator classes for
   simple keyed/exact-match shapes.
6. Add a `module_stage_v1` generation prompt in `DefaultAiSeeder` with strict
   Learn-stage exclusion rules for answer-bearing fields.
7. Add a standalone Angular renderer and wire it into
   `exercise-renderer.component`, `activity-lesson.component`, and
   `PatternBackedPresenter.skillBadge`.

Only the single converted format becomes runnable; all other planned rows for
that skill remain `planned` and non-generation-eligible.
