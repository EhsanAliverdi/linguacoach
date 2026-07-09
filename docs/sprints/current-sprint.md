---
status: current
lastUpdated: 2026-07-09 (Phase D5)
owner: engineering
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-07-09

---

## Active sprint

**Phase D5 — Context-Aware Today Bank Selection and Topic Matching (2026-07-09)** — complete

Wired `TodayBankResourceSelector` to consume the E9 published metadata so Today bank-first selection
is now **context-aware across all bank types** (vocabulary, grammar, short reading references, full
passages), not only full passages. **A selector/composer quality phase — no composer rewrite, no
new content, no migration, no UI, no legacy-fallback removal.**

- **Relaxation ladder**: the three lean per-type selectors were unified into a shared
  `SelectLeanAsync` that applies the E9 `ContextTag`/`FocusTag`/`Subskill`/`DifficultyBand` filters
  through a deterministic strict→loose ladder (context kept longest; drop difficulty → focus →
  subskill → context → general; absent-preference steps de-duped), each combined with the existing
  exact-CEFR-first / review-only-widen-down policy. The first ladder step yielding an allowed
  candidate wins, so a missing/unmatched preference relaxes safely instead of emptying the bundle.
- **General English default across all types**: when the learner is not workplace-routed,
  workplace-tagged vocabulary/grammar/reading-reference rows are now skipped (via the E9 context
  metadata) exactly as full passages already were — closing the D4-era gap. When workplace-routed,
  workplace content is preferred via the E9 context filter.
- **Inputs**: new request fields `PreferredFocusTags`/`PreferredSubskill`/`PreferredDifficultyBand`;
  `ActivityMaterializationJob` feeds `PreferredFocusTags` from
  `ResolvedLearningGoalContext.FocusAreaKeys`. Subskill/difficulty are supported by the selector but
  left null-fed for now (internal packs only carry difficulty on passages — E9 residual).
- **Topic matching is deterministic metadata matching only** — no embeddings, no vector search.
- **Preserved**: D4 pattern-specific instructions and `primary`/`supporting` role provenance;
  novelty precheck + NotUseful/DoNotShowSimilarSoon feedback exclusion (after filtering); AI stays
  composer/fallback. Provenance now also records `appliedFilters` + `matchedContextTags`; the prompt
  block gained a one-line selection-emphasis note.
- **Fallbacks intact**: a fully-relaxed empty result (e.g. only workplace rows for a general
  learner) yields no bank bundle and the caller runs the unchanged legacy AI generator; unsupported
  patterns still skip to legacy; Practice Gym fallback and the readiness/delivery queue unchanged.

**Validation**: `dotnet build --configuration Release` passed (0 errors); `dotnet test
--configuration Release` = 3,639 passed, 0 failed (+14 unit, +3 integration). No frontend files
changed, so no Angular/Playwright gates run. **No external datasets, no new seed content, no
Persian/bilingual content, no direct final-table seeding, no Practice-Gym change, no
readiness/delivery-queue change.** `TODO-E9-1` closed; narrowed residual `TODO-D5-1` tracks the thin
lean-pack difficulty/focus metadata.

**Next: a post-D5 checkpoint** — PG-v2A, further Today composer work, Phase F, or Phase G2/G3. See
`docs/architecture/learning-activity-engine.md` (Phase D5 section) and `docs/roadmap/road-map.md`
§1/§19a.

---

## Previous sprint

**Phase E9 — Published Bank Metadata Parity for Context-Aware Selection (2026-07-09)** — complete

Closed the D4-discovered metadata gap (`TODO-D4-1`). The lean published bank tables now carry the
same selection metadata `CefrReadingPassage` already had, so Today/D5 and a future PG-v2 selector
can filter **all** bank types by context/focus/subskill/difficulty from the published rows — not
only full passages. **A schema/mapping/backfill/discoverability phase; no Today composer change, no
new content, no legacy-fallback removal.**

- **Schema**: added four nullable columns — `subskill` varchar(128), `difficulty_band` int,
  `context_tags_json` text, `focus_tags_json` text — to `CefrVocabularyEntry`,
  `CefrGrammarProfileEntry`, `CefrReadingReference` via a `SetSelectionMetadata(...)` method
  (difficulty validated 1-5, matching `CefrReadingPassage`). Migration
  `Phase_E9_AddLeanBankSelectionMetadata` (12 additive nullable columns, no destructive change).
  Tag columns are **text** (not jsonb) — a documented deviation from `CefrReadingPassage`'s jsonb so
  the query filter can use the portable `.Contains("\"tag\"")` SQL LIKE that `CurriculumObjective`
  already relies on, while keeping the same logical shape.
- **Publish mapping**: `ResourceCandidatePublishService` now carries the candidate's
  context/focus/subskill/difficulty onto the new lean rows at publish (out-of-range difficulty
  dropped to null rather than blocking an otherwise-valid publish; `CefrReadingPassage` unchanged).
- **Backfill**: new idempotent startup step `PublishedBankMetadataBackfillSeeder` repairs pre-E9
  rows from the `ResourceCandidate` that published them — **only** where the row has no metadata yet
  **and** traces to exactly one published candidate carrying metadata. Never overwrites, never
  guesses for untraceable/ambiguous rows, never inserts a bank row.
- **Discoverability**: `ResourceBankQueryService` list/detail for the three lean tables expose the
  metadata read-only and support optional `ContextTag`/`FocusTag`/`Subskill`/`DifficultyBand`
  filters; the three `AdminResourceBankController` list endpoints gained the matching query params.
  Unfiltered browse is unchanged; a row with no metadata never matches a metadata filter.

**Validation**: `dotnet build --configuration Release` passed (0 errors); `dotnet test
--configuration Release` = 3,622 passed, 0 failed (+24 unit, +2 integration). No frontend files
changed, so no Angular/Playwright gates run. **No external datasets, no new seed pack (test fixtures
+ the existing E8 pack through the real pipeline only), no Persian/bilingual content, no direct
final-table seeding, no Today/Practice-Gym change, no student/admin UI redesign, no legacy-fallback
removal, no readiness/delivery-queue change.** `TODO-D4-1` closed for the three lean tables.

**Next: Phase D5 (Context-Aware Today Bank Selection and Topic Matching)** is the likely next phase,
now unblocked by E9's parity. PG-v2, Phase F, and Phase G2/G3 remain later. See
`docs/architecture/english-resource-bank-import-platform.md` (E9 detail) and
`docs/roadmap/road-map.md` §1/§19a.

---

## Previous sprint

**Plan-Sync-After-D4 — Decide Next Phase After Richer Today Bank-First Composition (2026-07-09)** — complete (docs-only)

A docs-only planning/decision sync after Phase D4. **Decision: Phase E9 — Published Bank Metadata
Parity for Context-Aware Selection — comes next, before a deeper Today phase (D5) and before
PG-v2.**

Phase D4 proved richer, pattern-shaped bank composition works, but exposed a real selection
limitation (`TODO-D4-1`): only `CefrReadingPassage` stores enough **published** metadata (context
tags, focus tags, subskill, difficulty band) for context-aware filtering. The lean final tables —
`CefrVocabularyEntry`, `CefrGrammarProfileEntry`, `CefrReadingReference` — carry that richer
metadata only on the staging `ResourceCandidate` (and partially in provenance), not on the
published rows a selector actually queries. That is why D4's general-English/workplace context
filter could only be applied to full passages. The next bottleneck is therefore **published
metadata parity, not composer mechanics** — fixing it first makes any deeper Today context matching
(D5), and eventually PG-v2's own selector, safer and more explainable, and avoids each re-hitting
the same gap.

**Proposed Phase E9 — Published Bank Metadata Parity for Context-Aware Selection**: add metadata
columns (context/focus tags, subskill, difficulty band as appropriate) to
`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` aligned with the existing
`CefrReadingPassage` shape; extend `ResourceCandidatePublishService` to map candidate metadata into
the final tables at publish; backfill existing internal E6/E7/E8 rows from candidate/provenance
metadata where safely traceable; extend the E5 browse/search query surface only where filters
already exist or need a small safe extension; add tests for mapping, backfill, filtering, and
discoverability. **E9 must not**: add a new seed pack (beyond tiny test fixtures), import external
datasets, add Persian/bilingual/support-language content, seed final tables directly, rewrite the
Today composer, start PG-v2, rewrite Practice Gym, remove any legacy fallback, delete the
readiness/delivery queue, or redesign student/admin UI. **Phase D5 — Context-Aware Today Bank
Selection and Topic Matching** is documented as the likely Today phase after E9.

No app code, migrations, schema, seed content, or tests changed — docs only. Today legacy fallback,
Practice Gym fallback, and the readiness/delivery queue all remain. PG-v2, Phase F, and Phase G2/G3
stay sequenced later. See `docs/roadmap/road-map.md` §1, §19 Decision Log, §19a (items 20b–20d).

---

## Previous sprint

**Phase D4 — Broader Today Bank-First Composer Expansion (2026-07-09)** — complete

Used the deeper E8 bank to make Today bank-first composition richer and more pattern-aware,
**without rewriting the Today composer and preserving every legacy fallback.** A composer/selector
expansion phase, not a content-import or UI phase; no migration.

`TodayBankResourceSelector` now assembles **pattern-shaped multi-resource bundles**:
- **Vocabulary-primary** patterns: up to 3 vocabulary/usage targets (role `primary`) + an
  opportunistic grammar hint (when the pattern lists Grammar as a secondary skill) + an
  opportunistic short reading reference (`supporting`).
- **Reading comprehension/reorder** patterns (`reading_multiple_choice_single`/`_multi`,
  `reorder_paragraphs`): a full `CefrReadingPassage` anchor (`primary`) + up to 2 supporting
  vocabulary targets + optional grammar hint; falls back to a short-reference bundle when no
  suitable passage exists.
- **Reading cloze** patterns (`reading_fill_in_blanks`, `reading_writing_fill_in_blanks`): a short
  `CefrReadingReference` (`primary`) + supporting vocabulary/grammar — **never a full passage**.

A compact, centralized **pattern-specific instruction layer** (`PatternInstruction`) adds one
bounded sentence per pattern family to the prompt ("use the passage only," "create a CEFR-aligned
gapped text, do not copy a full passage," "use the vocabulary targets naturally, do not default to
workplace"). **General English stays the default**: full passages tagged workplace-specific are
skipped unless the learner's routed goal context is workplace-specific (new
`TodayBankSelectionRequest.PrefersWorkplaceContext`, fed from
`ResolvedLearningGoalContext.WorkplaceSpecific`). The short vocab/grammar/reading-reference bank
tables carry no context tags, so this filter applies to full passages only (documented limitation).
Provenance now records a per-resource `role` (`primary`/`supporting`); the flat JSON array shape is
unchanged, so D2/D3 provenance/feedback tests still pass and no migration is needed.

**Preserved**: exact-CEFR-first / one-level-down-only-for-review / never-upward for every resource
type including supporting ones; novelty precheck + NotUseful/DoNotShowSimilarSoon feedback
exclusions; AI stays composer/fallback (bank content is appended to `TopicHint`, never replacing
generation). **All fallbacks intact** — unsupported pattern → legacy AI, no/blocked bank resource →
smaller bundle or legacy AI, selector exception → legacy AI, AI generation/validation failure →
existing retry/fallback. Practice Gym fallback and the readiness/delivery queue unchanged.

**Validation**: `dotnet build --configuration Release` passed (0 errors); `dotnet test
--configuration Release` = 3,596 passed, 0 failed (+13 `TodayBankResourceSelectorTests`, +3
`ActivityMaterializationJobBankFirstTests`). No frontend files changed, so no Angular/Playwright
gates run. **No external datasets, no new resource seed content, no Persian/bilingual content, no
direct final-table seeding, no student/admin UI, no new public API, no migration, no legacy-fallback
removal.**

**Next: a post-D4 checkpoint** — Phase PG-v2A, further Today composer work, Phase F, or Phase G2/G3.
See `docs/architecture/learning-activity-engine.md` (Phase D4 section) and `docs/roadmap/road-map.md`
§1/§19a.

---

## Previous sprint

**Phase E8 — Internal Resource Bank Depth Expansion for Grammar, Usage, and Reading Support (2026-07-09)** — complete

Expanded original, English-only internal resource-bank depth through the existing staging →
validation → approval → publish pipeline. **A resource-depth phase, not a composer/selector/
Practice-Gym/UI change; no migration.** A new second seed source
(`InternalResourceSeedPackE8Seeder`, distinct from the E6/E7 pack and idempotent by its own source
name) adds **40 vocabulary + 20 grammar + 16 short reading references + 8 full reading passages,
evenly across A1–B2** (10/10/10/10 vocab, 5/5/5/5 grammar, 4/4/4/4 references, 2/2/2/2 passages),
all flowing through the real `IResourceImportService` → deterministic metadata mapping →
`IResourceCandidateValidationService` → admin `Approve()` → `IResourceCandidatePublishService`
pipeline — never a Cefr* row written directly. Content defaults to **general English** with balanced
daily/social/travel/study contexts; **workplace is a minority tag**, never the default. None of it
duplicates the E6/E7 pack.

A narrow, additive metadata-mapping enhancement (`ResourceImportService.ApplyDeterministicRowMetadata`)
now maps optional `focusTags` and `difficultyBand` row columns onto the candidate (and, for full
passages, onto `CefrReadingPassage`); when those columns are absent — every E6/E7 row and every
prior import — behavior is byte-for-byte unchanged. Publishing routes by staged-text length exactly
as before (≤500 chars → `CefrReadingReference`, over → full `CefrReadingPassage`). Grammar subskills
are constrained to the enforced `CurriculumSubskillConstants` grammar taxonomy. All rows are
discoverable through the existing E5 `ResourceBankQueryService` browse/search APIs and trace back to
their candidate/run/source. Wired into `Program.cs` startup after the E6/E7 seeder.

**Validation**: `dotnet build --configuration Release` passed (0 errors); `dotnet test
--configuration Release` = 3,580 passed, 0 failed (+17 `InternalResourceSeedPackE8SeederTests`). No
frontend files changed, so no Angular/Playwright gates run. **No external datasets, no copied
third-party/test-prep content, no Persian/bilingual/support-language seed content, no direct
final-table seeding, no Today composer/selector change, no Practice Gym change, no student UI, no
legacy-fallback removal, no readiness/delivery-queue change.**

**Next: Phase D4 (broader Today bank-first composer expansion) is the likely next phase**, now that
the bank is deeper; the E8/D4 checkpoint stays open between D4, PG-v2A, and G2/G3. See
`docs/architecture/english-resource-bank-import-platform.md` and `docs/roadmap/road-map.md` §1/§19a.

---

## Previous sprint

**Plan-Sync-After-D3 — Decide Next Bank-First Phase After Full Reading Passage Today Wiring (2026-07-09)** — complete (docs-only)

A docs-only planning/decision sync after Phase D3. **Decision: Phase E8 (more resource depth/types)
comes before Phase D4 (broader Today composer expansion).** D1/D2/D3 have now proven the Today
bank-first selector/composer path end to end, including full reading passage consumption, so the
mechanism is no longer the constraint — **bank breadth/depth is.** Outside vocabulary, grammar
metadata, short reading references, and full reading passages, published bank depth is still thin,
and the highest-value next composer moves (grammar-aware Today activities, richer multi-resource
bundles, better pattern-specific prompt shaping) all depend on deeper published bank content. Doing
E8 first makes D4 safer and more useful and reduces pressure on the AI fallback, improving
bank-first reliability.

**Proposed Phase E8 — Internal Resource Bank Depth Expansion for Grammar, Usage, and Reading
Support**: expand original, English-only bank depth through the existing staging → validation →
approval → publish pipeline — more `CefrVocabularyEntry`/`CefrGrammarProfileEntry` depth across
A1–B2, more `CefrReadingReference` short-reference material, more `CefrReadingPassage` full-passage
material if useful, and better metadata coverage (CEFR, skill, subskill, context/focus tags,
difficulty, estimated time, source/provenance), plus validation proving published resources are
discoverable by the selector. **E8 must not**: import external datasets, add
Persian/bilingual/support-language content, seed final tables directly, rewrite the Today composer,
remove any legacy fallback, delete the readiness/delivery queue, start PG-v2, or redesign student
UI. **Phase D4 — Broader Today Bank-First Composer Expansion** remains the likely composer phase
after E8 (not cancelled): richer grammar/vocabulary/reading bundles, better pattern-specific bank
context, wider safe bank-first coverage — keeping legacy fallback, not rewriting the composer.

No app code, migrations, seed content, API, admin UI, or tests changed — docs only. Readiness pool,
Today legacy fallback, and Practice Gym fallback all remain. PG-v2, Phase F, and Phase G2/G3 stay
sequenced later. See `docs/roadmap/road-map.md` §1, §19 Decision Log, §19a (items 18–20).

---

## Previous sprint

**Phase D3 — Wire Full Reading Passage Bank into Today Bank-First Composer (2026-07-09)** — complete

Extended the D2 Today bank-first composer so Reading-primary Today activities can select and use
the new E7 `CefrReadingPassage` full-passage bank — a **narrow, fallback-safe extension, not a
full Today composer rewrite; nothing deleted or replaced.** `TodayBankResourceSelector` now, for
the comprehension/reorder Reading patterns (`reading_multiple_choice_single`,
`reading_multiple_choice_multi`, `reorder_paragraphs`), prefers a full `CefrReadingPassage` anchor
— reusing E7's existing `IResourceBankQueryService.ListReadingPassagesAsync`/
`GetReadingPassageDetailAsync` (no new query surface), listing at the routed CEFR then fetching
full text only for the finally-selected passage (at most one injected). It falls back to the short
`CefrReadingReference` behavior when the pattern is cloze/fill-in-blanks
(`reading_fill_in_blanks`, `reading_writing_fill_in_blanks`), when no suitable passage exists at
the routed level, or when novelty/feedback excludes every candidate passage. The selector receives
the concrete pattern via a new `TodayBankSelectionRequest.PatternKey`; the D2 CEFR policy is
unchanged (exact level first, one-level-down widening only for Review/Scaffold/Remediation, never
upward). Full-passage novelty uses a distinct `bank-reading-passage-precheck:{id}` fingerprint;
feedback exclusion applies via the same `LearningActivity.BankResourceProvenanceJson` match. The
structured `TopicHint` block gained a bounded, delimited, length-capped full-passage anchor
sub-block (title/CEFR/word-count/reading-time/passage-text + "build tasks from this passage only,
keep CEFR aligned, English-only" instructions). Provenance now records `type=ReadingPassage` with
id/sourceId/contentFingerprint/selectionReason plus `cefrLevel`/`title`.

**Legacy fallback fully intact** — unsupported patterns, missing/blocked passages, selector
exceptions, and AI generation/validation failures all still flow through the unchanged
`IAiActivityGenerator` path; the existing legacy-fallback integration test is unchanged and green.
Vocabulary-primary behavior unchanged from D2; Speaking/listening/image/open-ended remain legacy.
**No migration** (no schema change; provenance reuses the D2 column). +12 backend tests
(3,551 → 3,563: +11 `TodayBankResourceSelectorTests`, +1 `ActivityMaterializationJobBankFirstTests`);
full `dotnet test --configuration Release` re-run, all green. No frontend changed; no external
datasets; no Persian/bilingual seed content. `StudentActivityReadinessItem` / readiness pool /
Practice Gym legacy fallback all kept.

**Next: a Phase E8/D4 decision checkpoint applies** — Phase D4 (broader Today composer expansion),
Phase E8 (resource depth/types), Phase G2/G3 (backend/diagnostics cleanup), or PG-v2. Not started.
See `docs/architecture/learning-activity-engine.md` Phase D3 section and `docs/roadmap/road-map.md`
§19a.

---

## Previous sprint

**Phase G1 — Admin Information Architecture Cleanup (2026-07-09)** — complete

Implemented the low-risk admin-IA quick wins from the completed G0 audit — **labels/nav/
page-composition only; nothing deleted, no route/namespace/entity renamed.** **Nav restructure**
(both desktop sidebar and mobile drawer in `admin-app-layout.component.html`): the overloaded
"Content" section was split into **Content Banks** (Resource sources/import-runs/candidates + the
four Resource Banks + Activity templates + Review queue + Placement items + Onboarding — the
primary content model), **Delivery** (the relabeled Today-delivery surface), and **Learning
Setup** (Curriculum, Exercise Types). **Added the missing Phase E7 reading-passages nav item**
(`/admin/resource-banks/reading-passages`) — routable since E7 but previously unreachable from
the nav. **Reframed `/admin/lessons` in place** (route kept, no deep links broken): title
"Lessons" → "Today Delivery Health"; subtitle/section headings/KPI labels from "readiness
pool"/"pool health" → delivery-queue/assignment language; the manual generate-lessons card
reframed as AI **fallback** generation; a new info banner states the page is delivery
infrastructure (not the content model) and points admins to the Content Banks. **Terminology
cleanup** in the student-detail readiness panel ("Readiness pool health" → "Assignment / Delivery
Queue health") and the AI Operations card. Updated 3 spec assertions (`admin-app-layout`,
`admin-student-detail`) to match new labels and to guard the new reading-passages nav item + IA
sections. **`StudentActivityReadinessItem`, the pool/buffer/materialization jobs,
`PracticeActivityCache`, and the legacy `IAiActivityGenerator` path are all kept — nothing
deleted.**

**Validation**: production `ng build` compiles with no new errors (only the pre-existing
bundle-size-budget failure + pre-existing NG8107 warnings in untouched files). Karma could not
run due to a pre-existing, unrelated TS error in `student/activity/presenters/test-helpers.ts`
(a `feedbackPolicy` type mismatch) that fails the whole spec-bundle compile — pre-existing test
debt, not fixed here per the phase's "no unrelated test debt" rule. No `.cs` changed (backend
3,551 tests, not re-run).

**Next: a Phase E8/D3 decision checkpoint now applies, not resolved by this phase** — Phase D3
(broader Today composer migration), Phase E8 (more resource depth/search), Phase G2/G3
(backend/diagnostics cleanup), or a docs-only plan sync. See
`docs/architecture/bank-first-admin-backend-surface-audit.md` and `docs/roadmap/road-map.md` §19a
for the phase order.

---

## Previous sprint

**Plan-Sync-After-G0 — Choose G1 Admin IA Cleanup Before E8/D3 (2026-07-09)** — complete (docs-only)

Resolved the post-G0 decision: **Phase G1 (Admin Information Architecture Cleanup) comes before
Phase E8 and Phase D3.** G0's highest-value/lowest-risk findings are all admin-IA quick wins, so
making the bank-first model legible in the admin surface comes first. Updated roadmap phase
sequence: Phase E7 → Plan-Sync-G0 → Phase G0 → Plan-Sync-After-G0 → Phase G1 → Phase E8/D3
decision checkpoint → Phase E8 or D3 → PG-v2A/B/C/D later → Phase F → Phase G2/G3 as appropriate.
Committed as `cd49739b`.

---

## Previous sprint

**Phase G0 — Bank-First Admin/Backend Surface Audit (2026-07-09)** — complete (docs/audit-only)

Executed the audit Plan-Sync-G0 planned: a read-only inventory + classification of 31 admin
routes (6 nav sections), ~20 controllers, 8 background jobs + ~6 core services, and 11
terminology terms, saved as `docs/architecture/bank-first-admin-backend-surface-audit.md` — each
surface tagged keep / rename-reframe / move-to-diagnostics / merge / remove-later with a P0/P1/P2
priority and a target phase. Confirmed the readiness/assignment/delivery lifecycle is
load-bearing → `StudentActivityReadinessItem` kept, reframed as "Student Activity Assignment /
Delivery Queue," never deleted. Flagged the P0 `/admin/lessons` overloaded page and the P1
E7-reading-passages-page-missing-from-nav gap (a G1 safe quick win). **No cleanup implemented —
docs only.** Committed as `9dda2d9d`.

---

## Previous sprint

**Plan-Sync-G0 — Bank-First Admin/Backend Surface Cleanup Track (2026-07-09)** — complete (docs-only)

Opens a new roadmap track auditing the pre-bank-first admin/backend surface. **Not a visual
redesign, and not a deletion of the readiness pool.** Records two decisions: (1) the per-student
readiness lifecycle (`StudentActivityReadinessItem`, `IStudentActivityReadinessPoolService`) is
**kept** — its state machine (selected → assigned → ready → reserved → completed/expired/stale/
failed) is still exactly what production needs, only its framing changes from "AI-generated
activity cache" to **"Student Activity Assignment / Delivery Queue"** language; (2) Resource
Banks/Resource Candidates/Activity Templates (E0-E7) are confirmed the **primary content model**
going forward, with AI generation remaining for fallback generation, evaluation, composition,
and cost/diagnostics visibility only. Specific treatment decisions recorded now: Readiness Pool
admin UI reworked/renamed/moved to diagnostics; "Pool Health"/"Lesson readiness" admin pages
renamed/reframed as "Today Delivery Health"/"Assignment Health"/"Delivery Queue Health";
`PracticeActivityCache` audited later, not touched now; AI-generation admin pages narrowed to
fallback/evaluation/composition/cost-diagnostics scope; stale forward-facing wording ("AI-
generated activity cache," "generated pool as main content source," etc.) flagged for a future
removal pass, with historical decision-log entries exempted; legacy generation paths kept until
replacements are proven (reaffirms existing Phase F scope). Added **Phase G0 — Bank-First Admin/
Backend Surface Audit** (audits every admin page/API/job/lifecycle concept, classifying each as
keep/rename-reframe/move-to-diagnostics/merge/remove-later) plus **Phase G1/G2/G3** (act on G0's
classifications for admin IA cleanup, backend legacy cleanup, and diagnostics consolidation
respectively), expanding the roadmap's previously-generic single "Phase G" item. Sequenced
Plan-Sync-G0 → Phase G0 immediately after Phase E7 (done) and before the Phase D3/E8 decision
checkpoint. **No app code, migrations, or config changed.** Full detail:
`docs/roadmap/road-map.md` §1 and Decision Log.

**Next: Phase G0's own audit has not started, and the Phase D3/E8 decision checkpoint remains
unresolved** — start Phase D3 using the deepened bank content, continue Phase E8 for further
resource-platform work, run Phase G0's audit, or a further docs-only plan sync. See
`docs/roadmap/road-map.md` §19a for the full phase order.

---

## Previous sprint

**Phase E7 — Full Internal Reading Passage Bank and Resource Depth Expansion (2026-07-09)** — complete

Resolved the Plan-Sync-After-D2 decision by closing the most concrete gap it identified:
`CefrReadingReference` is short-excerpt/citation-only by design, and Phase E4 explicitly deferred
full-length `ReadingPassage` publishing rather than force it through dishonestly. **New published
bank entity**: `CefrReadingPassage` (migration `Phase_E7_AddCefrReadingPassage`) — `Title`,
`PassageText`, `Summary?`, `CefrLevel`, `DifficultyBand?`, `PrimarySkill`, `Subskill?`, topic/
context/focus tags, computed `WordCount`/`EstimatedReadingMinutes`, a denormalized
`AttributionText?` snapshot, `ContentFingerprint?`, `QualityScore?`. **No new candidate type
needed** — `ResourceCandidateType.ReadingPassage` already existed; `ResourceCandidatePublishService`
now routes by staged-text length (≤500 chars → `CefrReadingReference` unchanged; over 500 chars
→ `CefrReadingPassage` instead of being blocked, requiring a `title` field). **Preview needed no
changes** — E3's `BuildReadingPreview` already rendered full passage text/word count/reading time
regardless of length. **Browse/search**: new `IResourceBankQueryService.ListReadingPassagesAsync`/
`GetReadingPassageDetailAsync`, admin API (`GET /api/admin/resource-banks/reading-passages`
[`/{id}`]), and a new read-only admin page (`/admin/resource-banks/reading-passages`), matching
the E5 pattern exactly. **Content added**: 10 new full-length, 100% original English reading
passages (A1-B2, 458-940 characters) added to `InternalResourceSeedPackSeeder`'s internal source
through the same real staging→validation→approval→publish pipeline as E6's content — no direct-
final-table seeding. **`TodayBankResourceSelector` deliberately not wired to the new bank this
phase** — query methods exposed and tested, Today consumption left for a future phase. +24
backend tests (3,527 → 3,551 total: 5 architecture + 2,070 unit + 1,476 integration). **No
external dataset imported, no Persian/bilingual/support-language content added, Today/Practice
Gym legacy fallback not removed, no direct import-to-final-bank bypass introduced.**

---

## Previous sprint

**Plan-Sync-After-D2 — Choose Phase E7 Before Broader Today Composer Migration (2026-07-09)** — complete (docs-only)

Resolved the Phase D2 follow-on decision: Phase E7 comes before Phase D3. Updated roadmap phase
sequence: Phase D2 → Plan-Sync-After-D2 → Phase E7 → Phase E8 if needed → Phase D3 decision
checkpoint → Phase D3 or PG-v2A → PG-v2A/B/C/D later → Phase F → Phase G. Committed as `b81519f1`.

---

## Previous sprint

**Phase D2 — Expand Today Bank-First Composer Coverage and Provenance (2026-07-08)** — complete

Confirmed the skill-based pattern gate already covers every Vocabulary/Reading-primary Today
pattern; added a balanced vocabulary/grammar/reading resource bundle, CEFR-widening for review/
scaffold routing only, a feedback-signal exclusion, a structured prompt block, and fixed a latent
D1 provenance bug via `LearningActivity.BankResourceProvenanceJson`. +9 backend tests (3,518 →
3,527 passed). Committed as `67c19aeb`.

---

## Previous sprint

**Bugfix-D1A — Fix `LearningSession.GenerationStatus` Default/State Persistence Bug (2026-07-08)** — complete

Fixed the EF default-value bug D1 discovered: `LearningSessionConfiguration`'s
`.HasDefaultValue(GenerationStatus.Ready)` silently discarded an explicit
`MarkGenerationPending()` call before a new session's first save, since `Pending == 0` is also
the enum's CLR default. Removed the default (migration
`Bugfix_D1A_RemoveGenerationStatusDefault`, no data loss); +5 regression tests; one pre-existing
test corrected. 3,513 → 3,518 backend tests passed.

---

## Previous sprint

**Phase D1 — First Bank-First Today Composer Slice (2026-07-08)** — complete

Resolved the third Phase D1 decision checkpoint by starting D1 itself. New
`ITodayBankResourceSelector`/`TodayBankResourceSelector` queries the published Resource Bank
for Vocabulary/Reading-primary-skill Today patterns only; legacy freeform generation is the
unchanged fallback everywhere else. +13 backend tests (3,500 → 3,513 passed). Discovered (fixed
by Bugfix-D1A, above) the `GenerationStatus` default-value bug.

---

## Previous sprint

**Phase E6 — First Real English Resource Depth (2026-07-08)** — complete

Added an original, internally-authored, English-only seed pack — 32 vocabulary entries (A1-B2),
12 grammar profile entries, 10 short reading excerpts — routed through the real staging→
analysis/validation→approval→publish pipeline via `InternalResourceSeedPackSeeder`, not direct
final-table seeding. +14 backend tests (3,486 → 3,500 passed). No external dataset imported, no
Persian/bilingual/support-language content added.

---

## Previous sprint

**Plan-Sync-E6-Decision — Choose E6 before Today composer (2026-07-08)** — complete (docs-only)

Resolved the Phase D1 decision checkpoint opened by Phase E5: continue with Phase E6 before
Phase D1. Updated roadmap phase sequence: E6 → Phase D1 decision checkpoint (second instance) →
either Phase D1 or Phase E7/E8 → PG-v2A/B/C/D later → Phase F → Phase G. Docs-only; no app code,
migrations, or config changed; does not start any implementation.

---

## Previous sprint

**Phase E5 — Published Bank Browsing, Search, and Admin Management (2026-07-08)** — complete

Added `ResourceBankQueryService` (list + detail queries, search/CEFR/source filters, reverse
candidate traceability, read-only) and 3 new admin pages for vocabulary/grammar/reading
references — no edit/delete actions, mutation remains exclusively on Resource Candidates. +31
backend tests (3,455 → 3,486 passed). Committed as `394bb4ff`.

---

## Previous sprint

**Plan-Sync-After-E4 — Move E5 before D1 (2026-07-08)** — complete (docs-only)

Although Phase D1's "E0-E4 before D1" technical gate was met, decided to sequence Phase E5
before Phase D1 — the published banks held only small synthetic/test data with no browsing/
search/admin-management surface. Updated roadmap phase sequence: E5 → Phase D1 decision
checkpoint → either D1 or E6 → E6/E7/E8 as needed → PG-v2A/B/C/D later → Phase F → Phase G.
Committed as `4849875d`.

---

## Previous sprint

**Phase E4 — Publish Approved Resource Candidates to First English Banks (2026-07-08)** — complete

Added `ResourceCandidatePublishService`, every publish gate re-checked live, idempotent.
`VocabularyEntry`/`GrammarProfileEntry` fully supported; short-excerpt `ReadingPassage`
supported; `ActivityTemplateCandidate` publishing deferred entirely. New approve/reject/publish
admin endpoints and UI actions. +16 backend tests (3,430 → 3,455 passed). Committed as `ab4e2d1d`.

---

## Previous sprint

**Phase E3 — Admin Rendered Preview for Resource Candidates (2026-07-08)** — complete

Added `GET .../preview` (`ResourceCandidatePreviewService`) returning a bank-type-specific
rendered model, read-only, with a UI that clearly separates student-visible content from
admin-only metadata. +14 backend tests (3,417 → 3,430 passed). Committed as `c9831599`.

---

## Previous sprint

**Phase E2 — AI Analysis, Rule Validation, Dedup/Fingerprint, and Candidate Quality Gates (2026-07-08)** — complete

Implemented gates 4-6: `ResourceCandidateAnalysisService` (gate 4, advisory-only AI enrichment)
and `ResourceCandidateValidationService` (gates 5-6, fully deterministic, sole authority on
`ValidationStatus`, including exact-fingerprint dedup within-run/within-source/global). New
analyze-one/validate-one/analyze-import-run (batched, capped at 50) endpoints. Admin UI gained
Analyze/Re-validate actions. +21 backend tests (3,396 → 3,417 passed). Committed as `18015671`.

---

## Previous sprint

**Phase E1 — English Resource Source Registry, Import Runs, Raw Records, and Candidate Staging (2026-07-08)** — complete

Implemented the first Phase E slice: `CefrResourceSource` extended as the source registry (no
duplicate entity); new staging entities `ResourceImportRun`/`ResourceRawRecord`/
`ResourceCandidate` (migration `AddResourceImportStaging`); gates 1-3 only (English-only, license/
source-approval, parser); CSV/JSON/JSONL support; admin CRUD/API/UI for Resource Sources, Import
Runs, and Candidates under the existing Content sidebar group. Zero rows published. +17 backend
tests (3,379 → 3,396 passed). Committed as `874ee423`.

---

## Previous sprint

**Plan-Sync-PG-v2 — Add skill-first Practice Gym v2 to later roadmap (2026-07-08)** — complete (docs-only)

Added a future **Practice Gym v2** track to the roadmap after Phase E5-E8 and before Phase F/G:
students should eventually choose or be guided toward a skill/subskill/weak-area/objective/
review/challenge/recommended-practice target rather than a raw internal exercise type, with the
system internally selecting the best format. `ExerciseTypeDefinition`/`ExercisePatternDefinition`
are NOT deleted — reframed as an internal capability registry. Docs-only; does not reopen the
Phase C-Final closure. Committed as `23aa3e2c`.

---

## Previous sprint

**Phase E0 — English Resource Bank Import Platform final model and implementation plan (2026-07-08)** — complete (planning/docs only)

Finalized the technical model for the not-yet-started Phase E platform: the source registry
reuses the existing `CefrResourceSource` entity directly (supersedes the earlier informal proposal
for a separate `ResourceImportSource`); new staging entities
`ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate`; published resources use a hybrid
model reusing the existing typed `CefrVocabularyEntry`/`CefrGrammarProfileEntry` for E4. Finalized
a 7-gate status/gate model reusing `AdminReviewStatus`, `IFormIoSchemaValidationService`,
`IActivityContentFingerprintService`, `IFileStorageService`. Defined E1's exact scope and E2-E4
boundaries, plus an admin nav plan. Committed as `0fa92a25`.

---

## Previous sprint

**Phase C-Final — Practice Gym bank-first migration closure and readiness audit (2026-07-08)** — complete

Verification/closure pass after C1-C3 — no new pattern migrations. Confirmed all 8
template-enabled keys have approved/published templates, idempotent seeders, leak-safe schemas,
and intact gating/fallback/novelty/feedback-policy wiring. Full re-audit produced a definitive
33-row pattern table (8 template-enabled, 25 legacy) and corrected an off-by-one "26 legacy"
figure that had propagated through Phase C3's docs. Added 4 explicit backlog entries
(`docs/backlog/product-backlog.md`) for the deferred pattern families (listening/audio,
speaking/audio, open-ended AI-evaluated, fuzzy/short-answer). Committed as `5279c083`.

---

## Previous sprint

**Phase B2 — Activity Feedback, Repeat Policy, and Calibration Signals (2026-07-08)** — complete (foundation)

Implemented the persistence/API/minimal-UI foundation for explicit student feedback on completed
activities, across both Today and Practice Gym: new `ActivityFeedbackSignal` entity + migration
(`AddActivityFeedbackSignal`), admin Off/Optional/Required policy per surface via the existing
feature-gate/runtime-settings system, `ISubmitActivityFeedbackHandler`/`ActivityFeedbackHandler`,
new endpoint `POST /api/activity/attempt/{attemptId}/feedback`, `FeedbackPolicy` added to the
existing attempt-submission response DTO, and a minimal `activity-feedback-prompt` Angular
component. +14 backend tests (3,357 → 3,371 passed). Committed as `08de5c70`.

---

## Previous sprint

**Plan-Sync-B2 — Add Activity Feedback / Repeat / Calibration phase to roadmap (2026-07-08)** — complete (docs-only)

Docs-only phase inserting Phase B2 into the sequence between Phase C2 and Phase C3: created
`docs/architecture/activity-feedback-and-calibration.md`; updated `docs/roadmap/road-map.md`
§19a phase sequence and Decision Log, `docs/architecture/README.md`, and
`docs/architecture/repetition-and-novelty.md`. No app code, migrations, or config changed.
Committed as `5536ad07`.

---

## Previous sprint

**Phase C2 — Expand Practice Gym bank-first template coverage to the next safe batch (2026-07-08)** — complete

Extended the bank-first Form.io template path from 4 to 7 total pattern keys, adding
`reading_multiple_choice_multi`, `reading_fill_in_blanks`, `reading_writing_fill_in_blanks`
(reading-family, deterministic KeyedSelection/ExactMatch marking, audio-free, image-free).
Seeded 3 more approved/published `ActivityTemplate` rows (`reading_mcq_multi_workplace_seed_v1`,
`reading_fill_in_blanks_workplace_seed_v1`, `reading_writing_fill_in_blanks_workplace_seed_v1`),
reusing existing `ComponentAnswerScorer` kinds (`single_choice`, `multiple_choice` via a Form.io
`selectboxes` component, `text_normalized`) — no new scorer or frontend component needed.
Listening patterns and `ReorderParagraphs` were evaluated and deliberately excluded (see
`docs/architecture/practice-gym.md`). ~21 of ~28 Practice Gym patterns and all Today lessons
remain untouched legacy generation. Full design: `docs/architecture/practice-gym.md`
("Bank-first pattern coverage" section). Committed as `c84279a0`.

---

## Previous sprint

**Plan-Sync-After-C1 — Roadmap and Phase E plan alignment (2026-07-08)** — complete (docs-only)

Brought the roadmap, current-sprint, and architecture docs in line with two plan corrections
made after Phase C1 landed: (1) Phase C continues as a sequence (C2 → C3 → C4 → C-Final), not
one large "migrate the rest of Practice Gym" phase; (2) Phase E was re-planned from an informal
"seed CEFR-J/UniversalCEFR data" task into a full English-only resource
import/review/preview/publishing platform (E0-E8) — see
`docs/architecture/english-resource-bank-import-platform.md`. No app code, migrations, or
config changed — docs only. Committed as `2b099e5b`.

---

## Previous sprint

**Phase C1 — Generalize the Form.io Practice Gym pilot (2026-07-08)** — complete

Extended the bank-first Form.io template path from 1 pilot pattern to 4 total
(`formio_practice_gym_pilot`, `phrase_match`, `gap_fill_workplace_phrase`,
`reading_multiple_choice_single`). Seeded 3 new approved/published `ActivityTemplate` rows
(`ActivityTemplateSeeder`, idempotent, original English-only content). Found and fixed a real
generalization bug: `ActivitySubmitHandler`'s Form.io-scored evaluation dispatch was
pattern-driven (would have broken legacy fallback for the same pattern key) — changed to
content-driven (checks `LearningActivity.FormIoSchemaJson` presence instead). ~24 of ~28
Practice Gym patterns and all Today lessons remain untouched legacy generation. Full design:
`docs/architecture/practice-gym.md` ("Bank-first pattern coverage" section).

---

## Previous sprint

**Phase B — Repetition/Novelty Foundation (2026-07-08)** — complete

Added the real content-usage/cooldown layer: `StudentActivityUsageLog` entity, deterministic
`IActivityContentFingerprintService`, cooldown-based `IActivityNoveltyPolicy`. Wired into
`ActivitySubmitHandler` (usage logging), `PracticeGymGenerationJob`'s Form.io pilot, and
`ActivityMaterializationJob` (Today lessons) — both job integrations bounded-retry and fail
open. Full design: `docs/architecture/repetition-and-novelty.md`.

---

## Previous sprint

**Cleanup closure — Clean-A / Clean-A2 (2026-07-08)** — complete

Last completed feature phase before this: **2026-07-07 bank-first AI teaching architecture
(Phases 1-10)** — Subskill taxonomy, CEFR Resource Bank schema (no data imported yet),
`ActivityTemplate` bank + admin CRUD + review/publish workflow, template-bound AI generation +
validation pipeline, `StudentActivityReadinessItem` template provenance, `PlacementItemDefinition`
calibration/review fields, `StudentLearningEvent.CurriculumObjectiveKey`, cross-entity admin
review queue, and one feature-flagged Form.io Practice Gym pilot pattern
(`formio_practice_gym_pilot`). See `docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`.

**Cleanup status:**
- **Clean-A (2026-07-08)** — complete. Removed dead onboarding enums
  (`OnboardingAnswerMapping`, `OnboardingStepRequirementType`, `OnboardingStepTypeV2`), an
  orphaned `OnboardingShellComponent`, dead route aliases (`admin/careers`, `admin/ai-usage`,
  `students/new`, `students/create`), and the fully-orphaned admin career/word authoring
  API/UI chain (`AdminCareersComponent` + service methods + `IAdminCurriculumHandler` +
  controller actions/DTOs). Fixed `PracticeActivityCache.ContentFingerprint` so it is no longer
  misleadingly `Guid`-salted (still a queue-slot key only, not content-level dedup).
- **Clean-A2 (2026-07-08)** — complete. Doc-root remediation, Karma fixture fix, e2e onboarding
  smoke test skipped with a documented reason, and deleted `LearningTrack`/`WritingSubmission`
  (entities, config, DbSets, dead API surface) after confirming zero live dependents. See
  `docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md` for full detail.

---

## Previous sprint

**Phase 20D — Student Data Readiness, Backfill & Pilot Cleanup** — complete (2026-07-02)

Adds a read-only per-student "can this student safely use the app
end-to-end today?" audit (`IStudentReadinessAuditService`, ~20 checks
across account, placement/CEFR, Learning Plan, Today lesson, Practice
Gym, activity content, audio/TTS, review scaffold, progress) plus a small
set of explicit, idempotent, audited repair actions
(`IStudentPilotReadinessRepairService`): generate a missing Learning Plan,
refill an empty Today lesson, expire CEFR-invalid readiness items, expire
stale reserved items, and run all four together. 5 further suggested
repair actions are registered as "Not implemented yet" with a documented
reason rather than invented, tracked as `TODO-20D-1..4`. No AI scoring,
CEFR, objective-completion, or Learning Plan regeneration logic changed;
no historical attempt/submission/evaluation ever deleted; every real
repair requires a reason and writes one `AdminAuditLog` row.

**New Application:**
- `Application/Admin/StudentReadiness/` — enums, DTOs,
  `StudentReadinessRepairActions` registry (10 keys, 5 implemented),
  `IStudentReadinessAuditService`, `IStudentPilotReadinessRepairService`

**New Infrastructure:**
- `StudentReadinessAuditService` — ~20 read-only checks, never mutates;
  Learning Plan existence checked via direct DB query (never
  `GetOrCreatePlanAsync`, which auto-generates)
- `StudentPilotReadinessRepairService` — 4 real repair actions + run-all,
  each reusing an existing safe service method or entity mutator; requires
  a reason for real repairs; writes one `AdminAuditLog` row per real
  repair; idempotent

**New API:**
- `AdminStudentReadinessController` — `GET .../readiness`,
  `POST .../readiness/repair`, `POST .../readiness/repair-safe-all`
  (admin-only)

**New Angular:**
- "Pilot readiness" card on Admin Student Detail (Ready/NeedsAttention/
  Blocked badge, expandable checklist, recommended actions) + a
  reason-required repair slide-over reusing the existing
  `sp-admin-slide-over` pattern

**Tests added:**
- `StudentReadinessAuditServiceTests` (unit, 11 tests)
- `StudentPilotReadinessRepairServiceTests` (unit, 8 tests) — proves
  dry-run makes no DB changes, real repair requires a reason, one audit
  log per repair, idempotency, and that repairs never touch
  `ActivityAttempt`/`AudioAsset`/evaluation tables
- `AdminStudentReadinessEndpointTests` (integration, 8 tests) — auth,
  404 unknown student, dry-run and real repair via the API, a real repair
  visibly flips a check from warning to pass, no secrets/prompts in the
  response
- 10 new tests on `admin-student-detail.component.spec.ts` (panel load,
  badge/count rendering, checklist expand, recommended actions, dry-run,
  reason validation, real repair + refresh, loading/error states)

Two logic bugs were found and fixed while writing the audit-service unit
tests (Practice Gym "stuck" check was unreachable due to a redundant
condition; review-scaffold `pending_not_visible` check incorrectly failed
on the normal `PendingReview` queue state) — see the review doc for
details.

**Build/test totals:** Backend unit: 1,750 (+20, 0 regressions).
Integration: 1,378 (+8, 0 regressions). Architecture: 3 (unchanged).
Angular unit: 1,548/1,668 (120 pre-existing failures, unchanged baseline
— 0 new regressions; +10 new tests pass). Production build: clean.

See `docs/architecture/student-readiness-and-backfill.md` for the design
and `docs/reviews/2026-07-02-phase-20d-student-data-readiness-backfill-pilot-cleanup-review.md`
for the full review.

---

**Phase 20C — Runtime Settings Effective Wiring** — complete (2026-07-02)

Wires the Phase 20B admin-editable review-scaffold/Practice-Gym-pilot settings into the
actual runtime services, so admin overrides take effect on the next job run/request without
a redeploy — closing the gap Phase 20B explicitly deferred. Also fixes a pre-existing gap
where `DryRunOnly` was defined and displayed but never actually enforced in the generation
path. Careful and limited: no learning-behaviour change beyond honoring existing safe
settings, no dangerous AI signal-safety gate made editable or wired, defaults unchanged when
no override exists.

**New Application:**
- `IEffectiveReadinessPoolSettingsProvider` (`Application/ReadinessPool/`) — resolves
  effective `ReadinessPoolReplenishmentOptions` (appsettings + active `RuntimeSettingOverride`
  rows), fails safe on DB/parse errors

**New Infrastructure:**
- `EffectiveReadinessPoolSettingsProvider` — clone-then-apply-overrides resolution, mirrors
  `RuntimeSettingsService`'s key/JSON encoding

**Modified Infrastructure:**
- `ReadinessPoolReplenishmentService` — now depends on the new provider instead of
  `IOptions<ReadinessPoolReplenishmentOptions>`; resolves effective settings once at the top
  of `RunAsync`/`GetHealthAsync`; added the missing `DryRunOnly` enforcement in
  `FillShortfallAsync` (skips persisting scaffold/review items when
  `EnableReviewScaffoldGeneration && DryRunOnly`, normal generation unaffected)
- `PracticeGymSuggestionService` — same provider swap, resolved at the top of
  `GetSuggestionsForStudentAsync`

**Modified Application (Phase 20B registry, additive):**
- `FeatureGateSettingDefinition`/`FeatureGateSettingValueDto` — new `IsRuntimeEffective` flag,
  set per-setting in `FeatureGateDefinitions` (true for all review-scaffold/pilot settings and
  the lesson-generation fields jobs already consume; false — "display only, no consuming code
  path exists yet" — for `MaxGenerationAttempts`/`GenerationTimeoutSeconds`/
  `MaxConcurrentGenerationJobs`/`EnableTtsGeneration`/`TtsTimeoutSeconds`/
  `MaxConcurrentTtsJobs`/`PracticeGymReadyExercisesPerType`, none of which any job reads today)

**New Angular:**
- Feature-gates drawer shows a "Runtime effective" / "Display only — requires deployment"
  badge per editable setting

**Tests added:**
- `EffectiveReadinessPoolSettingsProviderTests` (unit, 9 tests) — no-override baseline,
  per-type override application, inactive-override ignored, corrupt-value fail-safe, DB-failure
  fail-safe, registry/provider key sync check
- `ReadinessPoolReplenishmentServiceEffectiveSettingsTests` (unit, 5 tests, with lightweight
  fakes for ledger/mastery/routing/goal-context) — baseline-false blocks generation,
  `DryRunOnly=true` computes-but-doesn't-persist, `DryRunOnly=false` persists,
  `RequireAdminReview` flows through, `MaxScaffoldItemsPerStudentPerDay=0` blocks from the
  first attempt
- `AdminRuntimeSettingsEffectiveWiringTests` (integration, 5 tests) — admin PUT of
  `PracticeGymPilotEnabled` through the real API makes an approved scaffold item visible via
  the real suggestion service in a fresh DI scope; label/reason PUT reflected in the
  suggestion DTO; DELETE override reverts to appsettings default; lesson-generation-buffer PUT
  reflected in the `LessonGenerationSettings` row; student role → 403
- Extended `PracticeGymSuggestionServiceTests` (`BuildSut` now injects a stub
  `IEffectiveReadinessPoolSettingsProvider`) — existing Phase 19C pilot tests now exercise the
  new wiring path unchanged

**Build/test totals:** Backend unit: 1,731 (+14, 0 regressions). Integration: 1,370 (+5, 0
regressions). Architecture: 3 (unchanged). Angular unit: 1,538/1,658 (120 pre-existing
failures, unrelated, unchanged from Phase 20B — 0 new regressions; +1 new badge test passes).
Production build: clean.

Explicitly deferred (`TODO-20C-1`): wiring the 7 lesson-generation settings with no consuming
job today would require building new enforcement behavior (timeout wrapper, concurrency
limiter, retry counter) — out of this phase's safe/limited scope.

See `docs/reviews/2026-07-02-phase-20c-runtime-settings-effective-wiring-review.md` for the
full design rationale.

---

**Phase 20B — Admin Runtime Settings & Feature Gates** — complete (2026-07-02)

Builds the first admin control plane for operational flags: a typed feature-gate
registry, an admin settings page with a slide-in drawer, server-side validation, and
audit logging, so review-scaffold/Practice-Gym/lesson-generation flags become
viewable, editable (where safe), and auditable without an appsettings edit + redeploy.
Control-plane only: no change to AI scoring, CEFR update logic, objective completion,
Learning Plan regeneration, or actual review-scaffold/Practice-Gym runtime behavior.
`ReadinessPoolReplenishmentService` still reads only `IOptions<ReadinessPoolReplenishmentOptions>`
(deferred wiring, see `TODOS.md`).

**New Domain:**
- `RuntimeSettingOverride` — generic key/value/reason/who/when override row (new table), used only for the `ReadinessPoolReplenishmentOptions`-backed review-scaffold/Practice-Gym-pilot settings, which had no DB override mechanism before this phase
- `LessonGenerationSettings.ResetToDefaults()` — new method on the existing DB-backed settings entity

**New migration:** `T_Phase20B_RuntimeSettingOverride` (via `dotnet ef migrations add`)

**New Application (`Application/Admin/RuntimeSettings/`):**
- `FeatureGateDefinitions` — static registry of 8 groups (`review-scaffold-generation`, `practice-gym-review-scaffold-pilot`, `lesson-generation-buffer`, `tts-generation`, `practice-gym-generation-per-type`, `ai-signal-safety-speaking`, `ai-signal-safety-writing`, `learning-plan-regeneration`) built only from properties that already exist on `ReadinessPoolReplenishmentOptions`, `LessonGenerationSettings`, `SpeakingEvaluationOptions`, `WritingEvaluationOptions`
- `IFeatureGateRegistry`, `IRuntimeSettingsService`, and supporting DTOs/enums

**New Infrastructure:**
- `FeatureGateRegistryService`, `RuntimeSettingsService` — effective-value resolution (DB override → appsettings/DB row → default), server-side validation (range/maxLength/allowed-values/typed `CONFIRM` for High/Critical risk), `AdminAuditLog` writes on every change/reset

**New API:**
- `AdminRuntimeSettingsController` (admin-only): `GET /api/admin/runtime-settings/feature-gates`, `GET .../feature-gates/{key}`, `PUT .../feature-gates/{key}/settings`, `DELETE .../feature-gates/{key}/override`

**New Angular:**
- `admin.models.ts` — `FeatureGateGroup`/`FeatureGateSettingValue` + supporting types
- `admin.api.service.ts` — `getFeatureGates()`, `getFeatureGate()`, `updateFeatureGate()`, `resetFeatureGateOverride()`
- `admin-feature-gates.component.ts`/`.html` (new page at `/admin/settings/feature-gates`) — category/search/risk/status filters, `sp-admin-drawer` for view/edit/reset, `?gate=` deep-link support
- `app.routes.ts` / `admin-app-layout.component.html` — new route + "Feature Gates" nav item under **System**
- Admin Lessons and Admin AI Operations pages — "Configure"/"Open settings" CTAs replacing static "enable in config" text, deep-linking into the relevant gate's drawer

**Tests added:**
- `RuntimeSettingsServiceTests` (unit, 13 tests) — effective-value resolution, validation rejection, locked-gate rejection, confirmation requirement, reset behavior, cross-field validation reuse from `LessonGenerationSettings.Update()`
- `AdminRuntimeSettingsEndpointTests` (integration, 14 tests) — auth guards, list/detail, update (valid/invalid/unknown-key/locked/confirmation), reset + audit, no secrets in response
- `admin-feature-gates.component.spec.ts` (19 tests) — load/filter/drawer/save/reset/validation/deep-link
- Extended `admin-lessons.component.spec.ts` and `admin-ai-operations.component.spec.ts` for the new CTAs

**Build/test totals:** Backend unit: 1,717 (+13, 0 regressions). Integration: 1,365 (+14, 0 regressions). Architecture: 3 (unchanged). Angular unit: 1,537/1,657 (120 pre-existing failures in `AdminStudentDetailComponent`/`AdminAiConfigComponent`, unrelated to this phase — 0 new regressions; all touched/new specs pass). Production build: clean.

See `docs/reviews/2026-07-02-phase-20b-admin-runtime-settings-feature-gates-review.md` for the full design rationale.

---

**Phase 20A — Admin AI Operations Dashboard** — complete (2026-07-02)

Adds a read-only admin dashboard aggregating existing AI/evaluation/generation data (speaking evaluation, writing evaluation, generation quality, AI provider usage, readiness-pool/review-scaffold pilot config, signal safety gates) into one operational view at `/admin/ai-operations`. Purely additive: no new AI behaviour, scoring, CEFR update, objective completion, Learning Plan regeneration, review-scaffold behaviour, or student-facing flow. Every field is sourced from an existing, already-DI-registered query/service — no parallel query logic was written except two lightweight readiness-pool counts and a small combined recent-failures lookup.

**New Application:**
- `AdminAiOperationsDtos.cs` — `AdminAiOperationsSummaryDto` + 8 supporting record types (provider usage, speaking/writing summaries, generation quality, readiness-pool AI summary, signal gate summary, recent-failure item)

**New API:**
- `GET /api/admin/ai-operations/summary` (`AdminAiOperationsController`, admin-only) — aggregates `IAdminAiUsageHandler`, `ISpeakingEvaluationQualityQuery`/`ISpeakingEvaluationSignalApplicationService`, `IAdminWritingEvaluationQuery`/`IWritingEvaluationSignalApplicationService`, `IAdminGenerationQualityHandler`, and `ReadinessPoolReplenishmentOptions`; computes `OverallStatus` (Healthy/Degraded/AttentionNeeded) and a combined recent-failures table (speaking + writing + generation, capped at 15, newest first)

**New Angular:**
- `admin.models.ts` — `AdminAiOperationsSummary` + 9 supporting interfaces
- `admin.api.service.ts` — `getAiOperationsSummary()`
- `admin-ai-operations.component.ts`/`.html` (new page) — overall status, provider/model usage, speaking/writing operational counts, generation quality, readiness-pool/pilot state summary, signal safety gates (10 explicit per-pipeline booleans + invariant-violation flag), combined recent-failures table, "Not implemented yet" placeholders for genuinely unavailable metrics (job-queue depth, zero-cost provider cost estimation)
- `app.routes.ts` — new `admin/ai-operations` route
- `admin-app-layout.component.html` — new "AI Operations" nav item in the existing "AI System" sidebar section

**Tests added:**
- `AdminAiOperationsSummaryTests` (integration, new file) — 9 tests: auth guards, safe empty-database response, seeded speaking/writing failure counts, review-scaffold/pilot config state, signal gate defaults, no secret/prompt/provider-payload leakage
- `admin-ai-operations.component.spec.ts` — 14 tests: load/loading/error/empty states, status badge, provider usage, speaking/writing counts, safety gate rendering, invariant-violation banner, recent-failures table, "Not implemented yet" rendering, no leaked sensitive text, refresh

**Build/test totals:** Backend unit: 1,704 (unchanged — no new unit tests this phase). Integration: 1,351 (+9, 0 regressions). Architecture: 3 (unchanged). Angular unit: 1,520/1,640 (120 pre-existing failures in `AdminStudentDetailComponent`, unrelated to this phase — 0 new regressions; the new spec file: 14/14 passing). Production build: clean. Playwright: not run this phase — same rationale as Phase 19B/19C (no existing spec to extend cheaply).

Deferred (explicitly out of scope per the phase brief): provider health-check/ping endpoint, retry tooling for failed evaluations, real-time job-queue depth (no dedicated queue table exists), cost estimation for zero-cost/NoOp provider calls.

See `docs/reviews/2026-07-02-phase-20a-admin-ai-operations-dashboard-review.md` for the full design rationale.

---

**Phase 19C — Review Scaffold Practice Gym Pilot Rollout** — complete (2026-07-02)

Adds a dedicated `PracticeGymPilotEnabled` gate on top of the Phase 19A/19B generation + per-item approval pipeline, so an admin can run generation and approval in production without any new content reaching students until a single, instantly-reversible flag flips. Approved scaffold items get friendly, configurable, non-negative student-facing copy (`PracticeGymPilotLabel`/`PracticeGymPilotReason`) and a per-response visibility cap (`MaxStudentVisibleScaffoldSuggestions`). Today lesson insertion remains untouched and disabled by default; no CEFR/objective/Learning Plan changes; no new activity formats; no Practice Gym rewrite.

**New/modified Application:**
- `ReadinessPoolReplenishmentOptions` — new `PracticeGymPilotEnabled` (default false), `PracticeGymPilotLabel` (default "Review"), `PracticeGymPilotReason` (default "This helps you practise a skill you are building."), `MaxStudentVisibleScaffoldSuggestions` (default 2)
- `ReadinessPoolDtos` — new `ReviewScaffoldPilotSummaryDto`, `ReviewScaffoldPilotItemDto`

**Modified Infrastructure:**
- `PracticeGymSuggestionService` — scaffold items (`RequiresAdminReview=true`) in the ReviewItems, ContinueItems, and (defensively) SuggestedItems buckets are now additionally gated on `PracticeGymPilotEnabled`, capped at `MaxStudentVisibleScaffoldSuggestions`, and have their `CallToAction`/`Explanation` overridden with the configured pilot label/reason

**New/modified API (`AdminReadinessPoolController`):**
- `GET /api/admin/readiness-pool/review-scaffold/pilot-summary` — pilot/Today-insertion status, approved/student-visible/pending/rejected/consumed/skipped-or-expired counts, recent student-visible/consumed items (no admin diagnostics)
- `ReviewScaffoldItemDetailDto.IsStudentVisible`/`IsPracticeGymEligible` deliberately left unchanged (structural eligibility, not pilot-gated) to keep the Phase 19B approval test contract intact — see the review doc §12 for why

**Modified Angular:**
- `admin.models.ts` — new `ReviewScaffoldPilotSummary`, `ReviewScaffoldPilotItem`
- `admin.api.service.ts` — `getReviewScaffoldPilotSummary`
- `admin-lessons.component.ts`/`.html` — new "Practice Gym review scaffold pilot" monitoring card (reuses existing `sp-admin-graph-card`/`sp-admin-breakdown-bars`/`sp-admin-badge`, no new design-system components), refreshed after approve/reject actions
- `practice-gym.component.ts`/`.html` — no changes needed; existing "Review queue" section and card layout already satisfy the visual-distinguishability and no-diagnostics-leak requirements

**Tests added:**
- `PracticeGymSuggestionServiceTests` — +8 tests (19–26): pilot on/off, pending/rejected hidden, cap enforcement, label override, rollback (Continue bucket), cross-student isolation, Today-lesson-source exclusion proof
- `ReplenishmentOptionsTests` — +3 tests (42–44): pilot default off, friendly/non-negative default copy, conservative visible cap
- `ReviewScaffoldPilotSummaryTests` (integration, new file) — 4 tests: auth guards, default-config safe reporting, approved-vs-student-visible count distinction while pilot is off
- `admin-lessons.component.spec.ts` — +3 tests: pilot summary loads on init and renders disabled/enabled status + counts with no diagnostics text, refresh reloads
- `practice-gym.component.spec.ts` — +2 tests: pilot label/reason render with no negative wording or diagnostics, review queue empty state still works when API returns no review items

**Build/test totals:** Backend unit: 1,715 (+11, 0 regressions). Integration: 1,342 (+4, 0 regressions). Architecture: 3 (unchanged). Angular unit: 1,505/1,626 (121 pre-existing failures in `AdminStudentDetailComponent`/`AdminAiConfigComponent`, unrelated to this phase — 0 new regressions; the two files this phase touched: 116/116 passing). Production build: clean. Playwright: not run this phase — same rationale as Phase 19B (no existing spec to extend); backend + Angular unit coverage documented as the substitute.

See `docs/reviews/2026-07-02-phase-19c-review-scaffold-practice-gym-pilot-rollout-review.md` for the full audit table and design rationale.

---

**Phase 19B — Review Scaffold Per-Item Admin Approval** — complete (2026-07-02)

Adds a per-item admin approval workflow on top of the Phase 19A global admin-review hold. Admins can now approve/reject/reopen individual review scaffold items instead of only flipping the global `RequireAdminReview` flag. All Phase 19A global safety gates remain intact and unchanged: `EnableReviewScaffoldGeneration=false`, `DryRunOnly=true`, `RequireAdminReview=true`, `AllowTodayLessonInsertion=false` by default. No CEFR/objective/Learning Plan changes, no new activity formats, no global "enable" toggle added to the UI.

**New Domain:**
- `AdminReviewStatus` enum — `NotRequired` / `PendingReview` / `Approved` / `Rejected`
- `StudentActivityReadinessItem` — new fields `AdminReviewStatus`, `AdminReviewedAtUtc`, `AdminReviewedByUserId`, `AdminReviewReason`, `AdminReviewNotes`; new transition methods `ApproveAdminReview`, `RejectAdminReview`, `ReopenAdminReview` (idempotent, enforce lifecycle/consumption guards), new `PassesAdminReviewGate` computed property

**Modified Persistence:**
- `StudentActivityReadinessItemConfiguration` — maps new admin-review columns + composite index `(RequiresAdminReview, AdminReviewStatus)`
- Migration `T72_ReviewScaffoldAdminApproval` — adds columns + backfills existing `RequiresAdminReview=true` rows to `AdminReviewStatus=PendingReview` (they default to `NotRequired` otherwise)

**Modified Infrastructure:**
- `PracticeGymSuggestionService` — exclusion updated from `!RequiresAdminReview` to `!RequiresAdminReview || AdminReviewStatus == Approved`

**New/modified API (`AdminReadinessPoolController`):**
- `GET /api/admin/readiness-pool/review-scaffold/pending-review` — now returns full `ReviewScaffoldItemDetailDto` rows (all admin review statuses, not just pending) so history/reopen is visible
- `POST .../{itemId}/approve`, `POST .../{itemId}/reject` (`{reason, notes?}`), `POST .../{itemId}/reopen` (`{notes?}`) — admin-only, safe 404/409/400, writes `AdminAuditLog`
- `ReviewScaffoldDryRunSummary.AdminReviewRequiredCount` now counts only `AdminReviewStatus=PendingReview` items (previously counted all `RequiresAdminReview=true` items regardless of decision)

**Modified Angular:**
- `admin.models.ts` — new `ReviewScaffoldItemDetail`, `ReviewScaffoldReviewActionRequest`, `AdminReviewStatus` type
- `admin.api.service.ts` — `approveReviewScaffoldItem`, `rejectReviewScaffoldItem`, `reopenReviewScaffoldItem`
- `admin-lessons.component.ts`/`.html` — "Review scaffold — approval" table replaces the old read-only "pending admin review" table; per-row Approve/Reject/Reopen actions using existing `window.confirm`/`window.prompt` pattern (no new modal component), status/visibility badges

**Tests added:**
- `StudentActivityReadinessItemTests` — 18 new tests (23–40): create stamps `PendingReview`/`NotRequired`, approve/reject/reopen happy paths, idempotency, expired/failed/stale cannot approve, consumed cannot reject/reopen, not-required cannot reject, approval doesn't mutate lifecycle/CEFR fields
- `ReviewScaffoldAdminApprovalTests` (integration, new file) — 14 tests: auth guards (401/403), safe 404, approve/reject/reopen happy paths + idempotency, reject requires reason (400), CEFR untouched, `AdminAuditLog` written, Practice Gym gate reflects rejection, pending-review list includes admin review fields
- `admin-lessons.component.spec.ts` — extended with approve/reject/reopen action tests, badge rendering tests, non-actionable-item action-hiding test

**Build/test totals:** Backend unit: 1,693 (+18, 0 regressions). Integration: 1,338 (+14, 0 regressions). Architecture: 3 (unchanged). Angular unit: 1,494/1,614 (120 pre-existing failures in `AdminStudentDetailComponent`/`AdminAiConfigComponent`, unrelated to this phase — 0 new regressions; `admin-lessons.component.spec.ts` itself: 27/27 passing, +9 net new tests). Production build: clean. Playwright: not run this phase — backend integration + Angular unit coverage documented above as the substitute (existing Playwright suite has no admin-lessons spec to extend).

---

**Phase 19A — Review Scaffold Controlled Enablement** — complete (2026-07-02)

Adds the missing safety gates around the existing (Phase 12B–12D) review scaffold generation infrastructure: source restriction, per-student daily cap, deterministic confidence banding, and a global admin-review hold. `EnableReviewScaffoldGeneration` remains `false` by default — this phase does not turn generation on in production. No CEFR/objective/LP changes, no new activity formats, no per-item admin approval workflow (deferred), no live AI calls in tests.

**Audit finding:** Review scaffold generation, dry-run simulation, and Practice Gym review-item separation/labeling already existed from Phases 12B–12D. This phase is additive gating + config visibility, not new infrastructure.

**Modified Application:**
- `ReadinessPoolReplenishmentOptions` — added `RequireAdminReview` (default true), `MaxScaffoldItemsPerStudentPerDay` (default 3), `ScaffoldAllowedSources` (default `["PracticeGym"]`), `AllowTodayLessonInsertion` (default false), `MinimumConfidenceForReviewNeed` (default `"Medium"`); `DryRunOnly` default flipped `false → true`
- New `ReviewNeedConfidence` enum (Low/Medium/High) — deterministic, derived from existing `StudentMasteryReport.AtRiskObjectiveKeys`/`WeakObjectiveKeys`, no new AI signal
- `ReplenishmentRunSummary.SkippedDailyCapReached` — new counter
- `CreateReadinessItemRequest` / `ReadinessItemRequestBuilder.FromRoutingRecommendation` — thread `RequiresAdminReview` through
- `ReviewScaffoldDryRunSummary` — extended with config fields + `AdminReviewRequiredCount` + `GeneratedTodayCount`
- New `ReviewScaffoldPendingItemDto`

**Modified Domain:**
- `StudentActivityReadinessItem.RequiresAdminReview` — creation-time config snapshot, not a mutable per-item approval state

**Modified Persistence:**
- `StudentActivityReadinessItemConfiguration` — maps `requires_admin_review` column
- Migration `T71_ReviewScaffoldAdminReviewFlag`

**Modified Infrastructure:**
- `ReadinessPoolReplenishmentService.FillShortfallAsync` — source gate (allow-list + Today-lesson override), confidence-band gate, per-student daily cap gate, stamps `RequiresAdminReview` on created scaffold items
- `PracticeGymSuggestionService` — excludes `RequiresAdminReview=true` items from Suggested/Continue/Review buckets entirely

**Modified API:**
- `AdminReadinessPoolController.GetReviewScaffoldDryRun` — returns new config fields + counts
- New `GET /api/admin/readiness-pool/review-scaffold/pending-review` — read-only, up to 50 held-back items; no approve/reject action this phase
- `appsettings.json` — new `ReadinessPool` section (previously missing; only class defaults applied before this phase)

**Modified Angular:**
- `admin.models.ts` — extended `ReviewScaffoldDryRunSummary`, new `ReviewScaffoldPendingItem`
- `admin.api.service.ts` — `getReviewScaffoldPendingReview()`
- `admin-lessons.component.ts` / `.html` — extended review-scaffold card with config/counts; new read-only "Pending admin review" table section

**Scope decision — deferred per-item approval:** `RequireAdminReview` is a global config flag (all scaffold items held or none), not a per-item approve/reject workflow. Consistent with the existing "no enable button" precedent (Phase 12B) and the "do not overbuild" rule. Admin clears the hold by setting `ReadinessPool:RequireAdminReview=false` after inspecting the pending-review list.

**Tests added:**
- `ReplenishmentOptionsTests` — 11 new tests: config defaults, `ReviewNeedConfidence` ordering, `RequiresAdminReview` threading through request builder and domain constructor, `SkippedDailyCapReached` counter
- `PracticeGymSuggestionServiceTests` — 4 new tests: `RequiresAdminReview` exclusion from all three buckets, control case
- `ReviewScaffoldDryRunTests` (integration) — 5 new tests: extended dry-run field shape, safe config defaults, pending-review endpoint auth (401/403)/shape/read-only guarantee

**Build/test totals:** Backend unit: 1,675 (+15). Integration: 1,324 (+5, 0 regressions). Arch: 3. Angular unit: 1,488/1,607 (119 pre-existing failures, 0 new regressions, +5 new tests). Production build: clean.

Review: `docs/reviews/2026-07-01-phase-19a-review-scaffold-controlled-enablement-review.md`.

---

**Phase 18B — Advanced Feedback UX** — complete (2026-07-01)

Improves student feedback experience across all activity types — UX and presentation upgrade only. No new AI scoring, no new activity formats, no CEFR/objective/LP changes, no live AI calls in tests.

**New Angular components (src/app/features/student/activity/feedback/):**
- `FeedbackAiDisclaimerComponent` — inline AI disclaimer, configurable text, generic across all AI-evaluated types
- `FeedbackPendingStateComponent` — reusable pending/failed/not-supported state for both speaking and writing evaluation
- `FeedbackWritingEvalComponent` — writing evaluation scores grid (overall, grammar, vocabulary, coherence, task completion), feedback text, corrected text, suggested improvement, AI disclaimer; handles all `EvaluationStatus` states
- `FeedbackNextStepsComponent` — context-aware action buttons: Improve (writing only), Try Again (not speaking/pronunciation), Next Activity, Back to Dashboard; all 4 outputs wired
- `FeedbackSkillContextComponent` — displays primarySkill, exerciseType, difficulty badges when stageContent is present
- `FeedbackSupportLangComponent` — generic "help in your language" collapsible toggle, not hardcoded to Persian

**Modified Angular:**
- `ActivityFeedbackPageComponent` — 2 new inputs: `activityType`, `stageContent`; writing evaluation polling (8s interval, max 15 polls) via `getWritingEvaluation`; replaced hardcoded Persian toggle with `FeedbackSupportLangComponent`; replaced hardcoded action buttons with `FeedbackNextStepsComponent`; added `FeedbackSkillContextComponent` header; added AI disclaimer after coachSummary; renders `nextPracticeSuggestion` (previously unreachable); added `pronunciationScore` to speaking scores grid
- `activity-lesson.component.html` — passes `activityType` and `stageContent` to `<app-activity-feedback-page>`
- `PatternEvaluationResultComponent` — imports and renders `FeedbackAiDisclaimerComponent` at end of chat/email section and spoken response section
- `ActivityService` — `getWritingEvaluation` method added
- `activity.models.ts` — `WritingEvaluationDto`, `EvaluationStatus` added

**Backward compatibility:** All old `ActivityFeedbackDto` fields still render. `SpeakingEvaluationDto` polling unchanged. `feedbackInSourceLanguage` still works; label is now generic.

**Tests added (Angular unit — 69 new specs):**
- `feedback-ai-disclaimer.component.spec.ts` — 2 tests
- `feedback-pending-state.component.spec.ts` — 6 tests
- `feedback-next-steps.component.spec.ts` — 12 tests
- `feedback-skill-context.component.spec.ts` — 5 tests
- `feedback-support-lang.component.spec.ts` — 7 tests
- `feedback-writing-eval.component.spec.ts` — 15 tests
- `activity-feedback-page.component.spec.ts` — 22 tests

**Build/test totals:** Angular build: clean. Angular unit: 1,483/1,602 (119 pre-existing failures, 0 new regressions, 69 new tests added).

Review: `docs/reviews/2026-07-01-phase-18b-advanced-feedback-ux-review.md`.

---

**Phase 18A-G — Generation Diagnostics Hardening** — complete (2026-07-01)

Hardens generation diagnostics with provider/model traceability, SHA-256 content hashing for prompt versioning, configurable retention (default 90 days) with a Quartz prune job, objective/student context threading, abandoned-generation rate warning, and enriched admin UI. No new activity formats, no player changes, no CEFR updates from AI, no objective completion from AI, no LP regeneration from AI, no live AI calls in tests.

**Modified Domain:**
- `AiPrompt.ContentHash` — SHA-256 hex (64 chars) computed on construction; nullable for pre-T70 rows

**Modified Persistence:**
- `AiPromptConfiguration` — maps `content_hash` column (varchar 64, nullable)
- Migration T70_AiPromptContentHash — adds `content_hash` to `ai_prompts`

**Modified Application:**
- `ActivityGenerationContext` — added 3 optional fields: `ObjectiveKey`, `StudentProfileId`, `GenerationSource`
- `ValidationFailureItem` — added `ProviderName`, `ModelName`, `GenerationSource`, `CorrelationId`
- `PromptTemplateItem` — added `ContentHashShort` (first 8 chars of SHA-256)
- New DTOs: `ProviderModelBreakdownItem`, `AbandonedGenerationWarning`
- `GenerationQualitySummary` — added `ProviderBreakdown`, `AbandonedWarning`, `RetentionDays`

**Modified Infrastructure:**
- `AiExecutionService` — new `ExecuteWithMetaAsync` returning `AiExecutionResult` (provider, model, isFallback); existing `ExecuteAsync` unchanged
- `AiActivityGeneratorHandler` — now calls `ExecuteWithMetaAsync`; threads `correlationId`, `providerName`, `modelName`, `objectiveKey`, `studentProfileId` into failure log
- `AdminGenerationQualityHandler` — rewritten; reads `IConfiguration` for retention/threshold config; computes provider breakdown, abandoned warning, content hash short forms
- New `GenerationValidationFailurePruneJob` — Quartz daily job; clamps retention [7,365]; non-blocking exception handling; uses `ToListAsync + RemoveRange + SaveChanges`

**Modified API:**
- `GET /api/admin/generation-quality/summary` — returns enriched response with provider breakdown, abandoned warning, retention days, content hash short, correlation IDs
- `QuartzConfiguration` — registers prune job with daily trigger
- `appsettings.json` — `GenerationQuality` section: `RetentionDays`, `RecentWindowDays`, `AbandonedFailureRateWarningThreshold`, `MinimumFailuresForWarning`

**Modified Angular:**
- `GenerationQualityService` — updated interfaces for all new fields
- `AdminDiagnosticsComponent` — new computed signals for provider breakdown, abandoned warning, retention days
- `admin-diagnostics.component.html` — abandoned warning banner, provider/model column in failures table, provider breakdown section, hash column in prompt summary, retention days in heading
- `admin-diagnostics.component.spec.ts` — updated `makeQualitySummary` helper with new required fields, fixed `PromptMetaItem` test object

**Tests added (unit):**
- `GenerationQualityHandlerTests` — +15 tests: provider breakdown grouping, null provider exclusion, content hash determinism/length/variation, hash short form, retention config, abandoned warning active/inactive/threshold/minimum, objective key threading
- `GenerationValidationFailurePruneJobTests` — 4 tests: backdating via `ExecuteSqlAsync` + `ChangeTracker.Clear()` pattern; prune logic, boundary condition, job execution

**Build/test totals:** Backend unit: 1,660 (+20). Integration: 1,319 (8 pre-existing AI-provider failures, no regressions). Arch: 3. Angular build: clean. Angular unit: 1,414/1,533 (119 pre-existing failures, 0 new regressions).

Review: `docs/reviews/2026-07-01-phase-18a-g-generation-diagnostics-hardening-review.md`.

---

**Phase 18A-F — Generation Quality Admin Visibility** — complete (2026-07-01)

Adds lightweight admin visibility for prompt versions, generation validation failures, and content quality diagnostics. No new activity formats, no player changes, no CEFR updates from AI, no objective completion from AI, no LP regeneration from AI.

**New Domain:**
- `GenerationValidationFailure` — append-only entity for content validation failures, with patternKey, activityTypeName, cefrLevel, validationErrors, attemptNumber

**New Persistence:**
- `GenerationValidationFailureConfiguration` — EF config
- `DbSet<GenerationValidationFailure>` added to DbContext
- Migration T69_GenerationValidationFailures

**Modified Infrastructure:**
- `AiActivityGeneratorHandler` — persists validation failure records on first-attempt and retry failures (non-blocking; exceptions caught and logged)
- `AdminGenerationQualityHandler` — new handler implementing `IAdminGenerationQualityHandler`
- `AdminHandler.ListPromptsAsync` — now includes `SeededAtUtc` in PromptTemplateItem

**New Application:**
- `PromptTemplateItem` — extended with `SeededAtUtc`
- `ValidationFailureItem`, `PatternFailureBreakdownItem`, `CefrFailureBreakdownItem`, `GenerationQualitySummary` DTOs
- `IAdminGenerationQualityHandler` interface

**New API:**
- `GET /api/admin/generation-quality/summary` — admin-only; returns prompt version summary and validation failure diagnostics; no provider secrets or storage keys exposed

**New Angular:**
- `GenerationQualityService` — typed HTTP service
- Diagnostics page — Generation Quality card with KPI strip, failures table, pattern/CEFR breakdown, prompt summary

**Tests added:**
- `GenerationQualityHandlerTests` (unit) — 7 tests: empty state, failure counts, pattern breakdown, CEFR breakdown, latest failures limit, safe fields, active-only prompts
- `AdminGenerationQualityEndpointTests` (integration) — 8 tests: auth guard (401, 403), response shape, prompt summary, failure counts, pattern breakdown, no secrets exposed, invalid recentDays
- `admin-diagnostics.component.spec.ts` — 6 new tests for generation quality section (+1 fixed pre-existing)

**Build/test totals:** Backend unit: 1,640 (+7). Integration: 1,310 (9 pre-existing AI-provider failures; no new regressions). Arch: 3. Angular: 33/33 diagnostics spec (119 pre-existing failures across all Angular specs, down 1 from 120 baseline).

Review: `docs/reviews/2026-07-01-phase-18a-f-generation-quality-admin-visibility-review.md`.

---

**Phase 18A — Lesson Quality and Content Generation Upgrade** — complete (2026-07-01)

Improves generated lesson content quality across all activity types. No new activity formats, no player changes, no CEFR updates from AI, no objective completion from AI, no LP regeneration from AI, no live provider calls in tests.

**Modified Application:**
- `ModuleStageContentValidator` — added empty-string validation for 12 critical string fields (prompt, audioScript, passage, question, instructions, incompleteText, incomingMessage, partnerTurn, sourceText, chatHistory, displayTranscript); added option ID consistency validation for single-answer MC (correctOptionId present in options, no duplicate IDs) and multi-answer MC (all correctOptionIds present in options)

**Modified Persistence / Seed:**
- `DefaultAiSeeder.ActivityGenerateWritingContent` — added CEFR calibration table (A1–B2 prompt complexity, expected output length, grammar scope)
- `DefaultAiSeeder.ActivityGenerateListeningContent` — added CEFR calibration table (audio script length, vocabulary complexity, question type per level); changed `"sourceLanguageSupport": null` to optional instruction
- `DefaultAiSeeder.ActivityGenerateSpeakingRolePlayContent` — added CEFR calibration table (response complexity, length, language features per level); changed `"sourceLanguageSupport": null` to optional instruction
- `DefaultAiSeeder.LessonBatchPlanContent` — added CEFR-aware pattern selection guidance table
- `DefaultAiSeeder.ActivityGeneratePhraseMatchContent` — changed `"sourceLanguageSupport": null` to optional instruction
- `DefaultAiSeeder.ActivityGenerateGapFillContent` — changed `"sourceLanguageSupport": null` to optional instruction
- `DefaultAiSeeder.ActivityGenerateListenAndAnswerContent` — changed `"sourceLanguageSupport": null` to optional instruction
- `DefaultAiSeeder.ActivityGenerateListenAndGapFillContent` — changed `"sourceLanguageSupport": null` to optional instruction
- `DefaultAiSeeder.ActivityGenerateTeamsChatContent` — changed `"sourceLanguageSupport": null` to optional instruction
All prompt changes use SHA256 hash-based upgrade mechanism — no migration required.

**Tests added:**
- `ModuleStageContentValidatorTests` (unit) — 7 new tests: empty prompt fails, empty audioScript fails, empty passage fails, valid MC single passes, duplicate option ID fails, correctOptionId not in options fails, correctOptionIds containing unknown ID fails

**Backward compatibility:** All changes are additive. Existing persisted content with populated strings passes unchanged. Support language field previously null stays null until AI regenerates.

**Build/test totals:** Backend unit: 1,633 (+6 net new, 1 pre-existing integration baseline). Integration: 1,311 (8 pre-existing AI-provider failures; no regressions introduced). Arch: 3. Angular: unchanged.

Review: `docs/reviews/2026-07-01-phase-18a-lesson-quality-content-generation-upgrade-review.md`.

---

**Phase 17C — Writing Mastery Signal Controlled Integration** — complete (2026-06-30)

Adds the full controlled-integration pipeline for writing mastery signals. Mirrors Phase 16I speaking pattern. No CEFR updates, no objective completion, no Learning Plan regeneration — all structurally disabled.

**New Domain:**
- `WritingEvaluationAppliedSignal` — immutable audit record, one per evaluation, rule version "17C-v1"
- `LearningEventSource.WritingEvaluation` — new enum value

**New Application:**
- `IWritingEvaluationSignalApplicationService` — 3-method interface
- `WritingSignalApplicationBatchResult` — batch processing counters record
- `WritingSignalApplicationSummaryDto` — admin dashboard metrics record
- `WritingSignalSafetySummaryDto` — invariant confirmation record
- `WritingEvaluationOptions` — 4 new config properties: `ApplyMasterySignals` (default false), `MinimumConfidenceForMasterySignal` (default "High"), `AllowReviewSignals` (default true), `AllowPositiveSignals` (default false)

**New Infrastructure:**
- `WritingEvaluationSignalApplicationService` — 5-gate pipeline: status, dry-run, config, confidence, signal-type + idempotency; creates `StudentLearningEvent` and `StudentSkillProfile` side-effects; writes `WritingEvaluationAppliedSignal` audit record

**New Background Job:**
- `WritingEvaluationSignalApplicationJob` — `[DisallowConcurrentExecution]`, runs every 10 minutes, batch size 20

**New Persistence:**
- `WritingEvaluationAppliedSignalConfiguration` — table `writing_evaluation_applied_signals`, unique index on `evaluation_id`
- Migration `T68_WritingEvaluationAppliedSignal`

**Modified API:**
- `AdminWritingEvaluationController` — added `GET /api/admin/writing-evaluation/applied-signals-summary` and `GET /api/admin/writing-evaluation/signal-safety-summary`
- `QuartzConfiguration` — writing signal application job trigger (every 10 minutes)
- `appsettings.json` — 4 new `WritingEvaluation` config keys

**Modified Angular:**
- `admin.models.ts` — `AdminWritingEvaluationItemDto`, `WritingSignalApplicationSummaryDto`, `WritingSignalSafetySummaryDto` interfaces
- `admin.api.service.ts` — `getStudentWritingEvaluations()`, `getWritingAppliedSignalsSummary()`, `getWritingSignalSafetySummary()` methods
- `admin-student-detail.component.ts` — writing evaluations signals + `loadWritingEvaluations()` + `writingStatusTone()` helper
- `admin-student-detail.component.html` — Writing Evaluations card with invariant labels (data-testid: writing-evaluations-card, writing-no-cefr, writing-no-objectives, writing-no-lp-regen, writing-evaluation-row)

**Tests added:**
- `WritingSignalApplicationServiceTests.cs` (unit) — 13 tests: all 5 gates, idempotency, config defaults, safety invariants, summary counts
- `WritingSignalApplicationTests.cs` (integration) — 8 tests: auth (401/403 for both endpoints), admin 200 with correct shape, invariants confirmed, per-student writing evaluations auth

**Build/test totals:** Backend unit: 1,626 (+13). Integration: 1,311 (+8). Arch: 3. Angular: production build clean.

Review: `docs/reviews/2026-06-30-phase-17c-writing-mastery-signal-controlled-integration-review.md`.

---

**Phase 17B — Writing Evaluation Quality Validation and Mastery Signal Dry-Run** — complete (2026-06-30)

Adds dry-run signal computation, pipeline quality summary endpoints, and Angular service hooks for the writing evaluation pipeline. Mirrors Phase 16H speaking dry-run pattern. No mastery, CEFR, objective, or Learning Plan state is modified by this phase.

**New Domain enums:**
- `WritingDryRunSignalOutcome` — CandidatePositiveSignal, CandidateReviewSignal, CandidateNoSignal, and five Blocked* variants
- `WritingDryRunConfidenceBand` — Low, Medium, High

**New Application layer:**
- `WritingEvaluationDryRunSignal` — computed-only class (never persisted, never applied to mastery)
- `WritingDryRunSignalMapper` — pure static mapper (no DB, no side effects)
- `WritingEvaluationQualitySummaryDto` — pipeline-wide quality metrics DTO
- `WritingEvaluationDryRunSignalDto` — serializable dry-run signal DTO
- `WritingEvaluationWithDryRunDto` — evaluation + signal combo DTO
- `IAdminWritingEvaluationQuery` — 2 methods added: `GetQualitySummaryAsync`, `GetWithDryRunAsync`

**Modified Infrastructure:**
- `AdminWritingEvaluationHandler` — implements new query methods; injects `WritingEvaluationOptions` for config state; uses mapper to compute dry-run signals

**Modified API:**
- `AdminWritingEvaluationController` — added `GET /api/admin/writing-evaluation/quality-summary` and `GET /api/admin/writing-evaluation/{id}/dry-run`

**Modified Angular:**
- `admin.models.ts` — `WritingEvaluationQualitySummaryDto`, `WritingEvaluationDryRunSignalDto`, `WritingEvaluationWithDryRunDto` interfaces
- `admin.api.service.ts` — `getWritingEvaluationQualitySummary()` and `getWritingEvaluationWithDryRun(id)` methods

**Tests added:**
- `WritingDryRunSignalMapperTests.cs` (unit) — 15 tests: status gates, confidence bands, outcome classification, corrected text impact, no-mastery/no-CEFR/no-objective invariants
- `WritingEvaluationQualitySummaryTests.cs` (integration) — 6 tests: auth (401/403), empty state, quality summary with seeded data, dry-run endpoint 404/200, anonymous 401

**Build/test totals:** Backend unit: 1,613 (+15). Integration: 1,302 (+6). Arch: 3. Angular unit: 1,527. Production build: clean.

Review: `docs/reviews/2026-06-30-phase-17b-writing-evaluation-quality-validation-review.md`.

---

**Phase 16J — Speaking Signal Quality Tuning and Production Dry-Run Review** — complete (2026-06-30)

Validation and tuning phase only. No new AI features, no CEFR updates, no objective completion, no Learning Plan regeneration from speaking AI. Strict invariants preserved throughout.

**New Application layer:**
- `SpeakingSignalThresholds` — value type carrying 6 configurable thresholds; `Default` and `FromOptions()` factory
- `ISpeakingEvaluationSignalApplicationService` — added `GetSignalSafetySummaryAsync()`
- `SpeakingSignalSafetySummaryDto` — invariant confirmation record
- `SpeakingEvaluationQualitySummaryDto` — expanded with 13 new metrics: applied/blocked breakdown, provider distribution, latest blocked reasons, avg pronunciation score, dry-run candidate counts
- `SpeakingEvaluationOptions` — 6 new threshold properties (positive ≥80 overall/relevance/completeness; review max ≤55 overall/relevance/completeness)
- `AdminStudentSpeakingAttemptDto` — 7 new fields: `IsApplied`, `AppliedSignalType`, `AppliedSignalConfidence`, `AppliedSignalBlockedReason`, `AppliedAt`, `SignalUpdatesCefr` (always false), `SignalCompletesObjectives` (always false)

**Modified Application:**
- `SpeakingDryRunSignalMapper` — thresholds now explicit via `SpeakingSignalThresholds` param (default = `SpeakingSignalThresholds.Default`); removed 3 hardcoded consts; `ClassifyScore()` helper; review direction now `score <= MaxReviewOverall` (was `score >= 40`); creates middle band (56–79) = NoSignal

**Modified Infrastructure:**
- `SpeakingEvaluationQualityHandler` — takes `IOptions<SpeakingEvaluationOptions>`; passes thresholds to mapper; computes all new metric fields
- `SpeakingEvaluationSignalApplicationService` — `RuleVersion` → "16J-v1"; passes thresholds from config; `GetSummaryAsync` accurately counts blocked breakdowns; implements `GetSignalSafetySummaryAsync`
- `AdminStudentSpeakingAttemptsHandler` — populates 7 new per-attempt applied signal fields; always sets `SignalUpdatesCefr = false`, `SignalCompletesObjectives = false`

**Modified API:**
- `AdminSpeakingEvaluationController` — added `GET /api/admin/speaking-evaluation/signal-safety-summary`; expanded quality summary DTO with threshold config fields; `ResolveConfigStatus()` returns "DryRunOnly" vs "Enabled" correctly
- `appsettings.json` — 6 new threshold config keys

**Modified Angular:**
- `admin.models.ts` — `SpeakingProviderModelCount` interface; expanded `SpeakingEvaluationQualityMetrics` (13 new fields); `AdminSignalSafetySummary` interface; `AdminStudentSpeakingAttemptAppliedSignal`; 7 new fields on `AdminStudentSpeakingAttempt`
- `admin.api.service.ts` — `getSignalSafetySummary()` method
- `admin-student-detail.component.html` — applied signal badge with invariant labels (data-testid="applied-signal-badge", "signal-no-cefr", "signal-no-objectives")

**Domain:**
- `SpeakingEvaluationAppliedSignal.AppliedRuleVersion` — "16I-v1" → "16J-v1"

**Tests added/updated:**
- `SpeakingDryRunSignalMapperTests.cs` — 8 tests updated for new thresholds; 7 new Phase 16J threshold-boundary tests; 1 custom-threshold test
- `SpeakingEvaluationSignalApplicationTests.cs` — 2 tests updated; 16 new Phase 16J tests (safety summary, quality metrics, threshold behavior)
- `SpeakingEvaluationProviderIntegrationTests.cs` — factory method gains `completenessScore`/`relevanceScore` params
- `SpeakingEvaluationQualityIntegrationTests.cs` — existing positive-signal test updated for new thresholds; 4 new safety summary integration tests (401, CEFR disabled, objective completion disabled, LP regen disabled, no invariant violations)
- `admin-student-detail.component.spec.ts` — 2 new Angular tests (applied badge visible/hidden)

**Build/test totals:** Backend unit: 1,581 (+16). Integration: 1,281 (+4). Arch: 3. Angular: 124 (+2). Production build: clean.

Review: `docs/reviews/2026-06-30-phase-16j-speaking-signal-quality-tuning-review.md`.

---

**Phase 16I — Speaking Evaluation Mastery Signal Controlled Integration** — complete (2026-06-30)

Introduces a configurable, conservative integration path from high-confidence speaking evaluation signals into real student learning state. Disabled by default. Strict invariants: CEFR never updated, objective completion never triggered, Learning Plan never regenerated, failed/NotSupported evaluations never affect mastery.

**New domain:**
- `SpeakingEvaluationAppliedSignal` — immutable audit entity; one record per applied evaluation (unique index on `evaluation_id`)
- `LearningEventSource.SpeakingEvaluation` (value 4) added to enum

**New Application layer:**
- `ISpeakingEvaluationSignalApplicationService` — `ApplyPendingSignalsAsync(int maxBatch)` and `GetSummaryAsync()`
- `SpeakingSignalApplicationBatchResult` / `SpeakingSignalApplicationSummaryDto` records
- `SpeakingEvaluationOptions` — 4 new properties: `ApplyMasterySignals` (default false), `MinimumConfidenceForMasterySignal` (default "High"), `AllowReviewSignals` (default true), `AllowPositiveSignals` (default false); plus computed `AllowObjectiveCompletion = false` and `AllowCefrUpdate = false`

**New Infrastructure:**
- `SpeakingEvaluationSignalApplicationService` — applies signals with 5-gate pipeline (blocked, no-signal, config, confidence, signal-type); writes `StudentLearningEvent` + `StudentSkillProfile.MarkWeak` + `SpeakingEvaluationAppliedSignal`
- `SpeakingEvaluationSignalApplicationJob` — `[DisallowConcurrentExecution]` Quartz job, runs every 10 minutes

**New Persistence:**
- `SpeakingEvaluationAppliedSignalConfiguration` — maps to `speaking_evaluation_applied_signals`; unique index on `evaluation_id`
- Migration `20260630140000_T66_SpeakingEvaluationAppliedSignal`

**Modified API:**
- `AdminSpeakingEvaluationController` — added `GET /api/admin/speaking-evaluation/applied-signals`; returns `AdminSpeakingAppliedSignalSummaryDto` with all mastery integration status fields
- `QuartzConfiguration` — signal application job registered (10 min trigger)
- `appsettings.json` — 4 new `SpeakingEvaluation` config keys

**Modified Angular:**
- `admin.models.ts` — `AdminSpeakingAppliedSignalSummary` interface
- `admin.api.service.ts` — `getSpeakingAppliedSignalSummary()` method

**Tests added:**
- `SpeakingEvaluationSignalApplicationTests.cs` (unit) — 21 tests: config gate, confidence bands, idempotency, review/positive gating, FK seeding, audit record correctness, CEFR/Learning Plan invariants
- `SpeakingEvaluationSignalApplicationIntegrationTests.cs` (integration) — 9 tests: auth (401/403), config-disabled, config-enabled review signal, idempotency, failed eval, admin endpoint counts, CEFR invariant; assertions scoped by `activityId` → `EvaluationId`

**Build/test totals:** Backend unit: 1,565 (+37). Integration: 1,281 (+21). Arch: 3. Production build: clean.

Review: `docs/reviews/2026-06-30-phase-16i-speaking-evaluation-mastery-signal-controlled-integration-review.md`.

---

**Phase 16H — Speaking Evaluation Quality Validation and Mastery Signal Dry-Run** — complete (2026-06-30)

Adds quality metrics, a dry-run learning signal layer, and admin visibility over AI speaking evaluation output. No mastery, CEFR, or Learning Plan state is modified. Evaluation remains disabled by default.

**New domain enums:**
- `SpeakingDryRunSignalOutcome` — CandidatePositiveSignal, CandidateReviewSignal, CandidateNoSignal, and five Blocked* variants
- `SpeakingDryRunConfidenceBand` — Low, Medium, High

**New Application layer:**
- `SpeakingEvaluationDryRunSignal` — computed-only record DTO (never persisted, never applied to mastery)
- `SpeakingDryRunSignalMapper` — pure static mapper with `Map(SpeakingEvaluation)` and `MapFromFields(...)` overloads
- `ISpeakingEvaluationQualityQuery` / `SpeakingEvaluationQualitySummaryDto` — quality metrics interface and DTO

**New Infrastructure:**
- `SpeakingEvaluationQualityHandler` — queries all evaluations, computes counts/rates/averages and dry-run signal totals

**Modified API:**
- `AdminSpeakingEvaluationController` — added `GET /api/admin/speaking-evaluation/quality-summary`; returns config status, provider, and full quality metrics

**Modified Admin Application/Infrastructure:**
- `AdminStudentSpeakingAttemptDto` — 4 new dry-run fields: `DryRunOutcome`, `DryRunConfidence`, `DryRunCandidateSkill`, `DryRunBlockedReason`
- `AdminStudentSpeakingAttemptsHandler` — calls `SpeakingDryRunSignalMapper.MapFromFields` per attempt

**Modified Angular:**
- `admin.models.ts` — 4 dry-run fields on `AdminStudentSpeakingAttempt`; new `SpeakingEvaluationQualityMetrics` and `AdminSpeakingEvaluationQualitySummary` interfaces
- `admin.api.service.ts` — `getSpeakingEvaluationQualitySummary()` method
- `admin-student-detail.component.html` — "Dry-run signal" column with outcome, confidence, skill, and blocked reason

**Tests added:**
- `SpeakingDryRunSignalMapperTests.cs` (unit) — 15 tests: status gates, confidence bands, outcome thresholds, pronunciation does not block, IsDryRunOnly always true, entity/fields parity
- `SpeakingEvaluationQualityIntegrationTests.cs` (integration) — 10 tests: auth, empty state, status counts, dry-run positive/blocked signal counts, per-attempt field presence, mastery/CEFR unchanged

**Known gaps:** latency, audio duration, and cost/usage are not yet stored on `SpeakingEvaluation`. Documented in review.

**Build/test totals:** Backend unit: 1,528 (+15). Integration: 1,260 (+10). Arch: 3. Angular unit: 1,525. Production build: clean.

Review: `docs/reviews/2026-06-30-phase-16h-speaking-evaluation-quality-validation-review.md`.

---

**Phase 16F — AI Speaking Evaluation Foundation** — complete (2026-06-28)

Adds an asynchronous AI-based speaking evaluation pipeline. Conservative scope: no real-time voice, no advanced pronunciation scoring claims, no new speaking formats. Failures never block the student learning flow. No raw storage keys in any API response.

**New domain:**
- `SpeakingEvaluationStatus` enum (Pending/Evaluating/Completed/Failed/Skipped/NotSupported)
- `SpeakingEvaluation` entity with factory `CreatePending` and lifecycle methods `MarkEvaluating`, `MarkCompleted`, `MarkFailed`, `MarkNotSupported`
- Migration `20260628120000_T65_SpeakingEvaluationFoundation` — `speaking_evaluations` table, 3 indexes

**New Application layer:**
- `ISpeakingEvaluationProvider` — narrow interface (audio ≠ text prompt), separate from `IAiProvider`
- `ISpeakingEvaluationService` with `RequestEvaluationAsync` (non-fatal), `GetEvaluationAsync`, `ProcessPendingAsync`
- `SpeakingEvaluationDto`, `SpeakingEvaluationOptions` (default: Enabled=false, Provider=NoOp)

**New Infrastructure:**
- `NoOpSpeakingEvaluationProvider` — IsSupported=false, resolves immediately as NotSupported
- `SpeakingEvaluationService` — processes Pending batch, respects MaxRetries, wraps RequestEvaluationAsync in try/catch
- `SpeakingEvaluationJob` (Quartz, every 5 minutes, DisallowConcurrentExecution)

**Modified API:**
- `ActivityController` — fires `RequestEvaluationAsync` after audio upload (non-fatal, wrapped in try/catch)
- New `GET /api/activity/{activityId}/attempts/{attemptId}/evaluation` — returns DTO or 404; student ownership enforced

**Modified Admin:**
- `AdminStudentSpeakingAttemptDto` — extended with evaluation fields (status, provider, model, feedback, score, etc.)
- `AdminStudentSpeakingAttemptsHandler` — left-join SpeakingEvaluations; FailureReason only exposed on Failed status
- Admin UI — speaking submissions table shows AI score, provider/model, feedback, improvement suggestion, failure reason

**Modified Student UI:**
- `ActivityFeedbackPageComponent` — loads and polls evaluation (every 10s, max 12 polls) when `!hasFeedbackContent` and attemptId known; shows Completed/Pending/Failed/NotSupported states
- `activity.models.ts` — new `SpeakingEvaluationDto` interface
- `activity.service.ts` — new `getAttemptEvaluation` method

**Config:** `appsettings.json` `SpeakingEvaluation` section added (Enabled: false, Provider: NoOp, MaxBatchSize: 10, MaxRetries: 3)

**Tests:**
- `SpeakingEvaluationTests.cs` (NEW unit) — 7 tests: CreatePending, empty-ID validation, MarkEvaluating, MarkCompleted, MarkFailed retry count, MarkNotSupported, retry accumulation
- `SpeakingEvaluationEndpointTests.cs` (NEW integration) — 6 tests: 401 unauthenticated, 404 no eval record, ownership enforcement, happy path shape, storage key not exposed, submission non-blocking
- `admin-student-detail.component.spec.ts` — fixed 2 literal objects to include new evaluation fields

**Build/test totals:** Backend unit: 1,513 pass (+9). Integration: 1,248 pass (+6). Arch: 3. Angular unit: 1,525 (unchanged). Production build: clean.

Review: `docs/reviews/2026-06-28-phase-16f-speaking-evaluation-foundation-review.md`.

---

**Phase 16E — Speaking Submission Review Visibility and Pending Feedback** — complete (2026-06-28)

Makes submitted speaking recordings visible and reviewable before any AI evaluation is added. No AI scoring, no speech-to-text, no new speaking formats.

**New endpoints:**
- `GET /api/admin/students/{studentProfileId:guid}/speaking-attempts` — returns list of audio-submission attempts for a student, with status derived from `promptKey`/`score`. Storage keys never exposed.
- `GET /api/admin/students/{studentProfileId:guid}/speaking-attempts/{attemptId:guid}/audio` — streams audio bytes to admin; verifies ownership (`attempt.StudentProfileId == studentProfileId`) before streaming.

**Status model (no migration):** `promptKey == "audio_submission_pending"` → `PendingEvaluation`; `score.HasValue` → `Evaluated`; else → `Submitted`.

**Admin UI:** Speaking Submissions card added to `admin-student-detail`. Shows loading, error, empty, and attempt-row states. Playback deferred (informational text shown; Bearer-token-aware streaming not yet wired).

**Security:** `AudioStorageKey` never projected into any DTO. Integration test asserts `"speaking-recordings/"` does not appear in any response body. Ownership double-checked on audio stream endpoint. Unknown student returns `{ status: "NotFound" }`, never 500.

**Tests added:**
- `AdminStudentSpeakingTests.cs` (NEW) — 8 integration tests: 401/403 for both endpoints, empty for no recordings, not-500 for unknown student, no storage path in body, 404 for unknown audio attempt.
- `admin-student-detail.component.spec.ts` (EDITED) — 6 new Angular unit tests: API called on init, card renders, empty state, attempt rows, PendingEvaluation badge, error resilience.

**Bug fixed during validation:** `speakingStatusTone()` return type was `string`; changed to `SpAdminBadgeTone` to pass strict Angular template type check (`NG2` error).

**Build/test totals:** Angular unit: 1,525 pass (+6). Backend integration: 1,242 pass (+8). Backend unit: 1,504 (unchanged). Arch tests: 3 (unchanged). Production build: clean (warnings pre-existing).

Review: `docs/reviews/2026-06-28-phase-16e-speaking-submission-visibility-review.md`.

---

**Phase 16D — Voice Recording and Speaking Submission Foundation** — complete (2026-06-28)

Safe browser voice recording and audio submission infrastructure for speaking activities. No AI evaluation, no pronunciation scoring, no new activity formats.

**New endpoint:** `POST /api/activity/{id}/audio-attempt` — stores audio via `SpeakingAudioService` (same MIME/size/ownership validation as `speaking-attempt`), creates `ActivityAttempt` with `feedbackJson="{}"` and `promptKey="audio_submission_pending"`, returns empty `ActivityFeedbackDto` (all null/empty → `hasFeedbackContent=false` → Phase 16B pending card shown automatically).

**Removed constraint:** `GET /api/activity/{id}/attempts/{attemptId}/audio` — removed `ActivityType.SpeakingRolePlay` guard so audio from `audio-attempt` submissions is also retrievable.

**New component:** `VoiceRecorderComponent` (`app-voice-recorder`) — encapsulates `MediaRecorder` lifecycle, `RecorderState` type (`idle | requesting-permission | permission-denied | unsupported | recording | recorded`), stream cleanup on `stopRecording()` and `ngOnDestroy()`, preview URL via `URL.createObjectURL`, `reRecord()` revokes old object URL and resets to idle. Emits `(recorded)` with `{ blob, mimeType, durationSeconds, previewUrl }`.

**New component:** `AudioResponseComponent` (`app-audio-response`) — thin shell: wraps `VoiceRecorderComponent`, holds recorded audio in a signal, shows Submit button only after recording completes. Emits `(submitted)` with `{ blob, mimeType, durationSeconds }`.

**ExerciseRendererComponent** — added `AudioResponseComponent` import, `| { kind: 'audioResponse'; blob; mimeType; durationSeconds }` to `ExerciseAnswerPayload`, `audioResponseContent` getter (reads `prompt`/`situation` from staged/raw JSON or activity fields), `onAudioResponseSubmitted()` handler, and `@case ('audioResponse')` in the HTML switch.

**ActivityService** — added `submitAudioAttempt(activityId, blob, mimeType, durationSeconds?)` → `POST /audio-attempt` multipart.

**ActivityLessonComponent** — added `audioResponse` early-return branch in `onRendererSubmit` calling `submitAudioAttempt`. Works in both Today Lesson and Practice Gym paths (both share exercise-renderer).

**Tests added:**
- `audio-attempt-endpoint-tests.cs` (NEW) — 9 backend integration tests: auth, MIME validation, missing file, unknown activity, happy-path 200 with pending DTO, DB persistence with `speaking-recordings/` key, audio retrieval, wrong-owner 404, no storage key in response body.
- `voice-recorder.component.spec.ts` (NEW) — 14 Angular unit tests: create, idle state, permission-denied, requesting-permission, stream cleanup, reRecord/URL revoke, recorded event, all state-driven DOM visibility, disabled guard, ngOnDestroy cleanup.
- `audio-response.component.spec.ts` (NEW) — 9 Angular unit tests: create, no submit before recording, submit shown after recording, submitted event with correct payload, no emit without recording, disabled guard, submitting label.

**Build/test totals:** Angular unit: 1,519 pass (23 new). Backend integration: 1,234 pass (9 new). Backend unit and arch tests unchanged. Playwright: unchanged. Production build: clean.

Review: `docs/reviews/2026-06-28-phase-16d-audio-submission-foundation-review.md`.

---

**Phase 16C — Audio/TTS and Listening Activity Reliability** — complete (2026-06-28)

Hardening-only pass targeting audio playback, TTS fallback, and listening activity state reliability across Today and Practice. No new exercise formats, AI scoring, lesson player redesign, or routing/mastery/placement/learning-plan changes.

**P0 fix:** `repeat-sentence.component.html` — `audioUrl` was present on the item model but no audio UI was ever rendered. Added conditional `<app-audio-player>` block before the sentence text div.

**P1 fix:** `listening-fill-in-blanks`, `highlight-correct-summary`, `highlight-incorrect-words` — all three conditioned `app-audio-player` display on `@if (content.audioScript)`. When `audioUrl` was set but `audioScript` was null, the player was hidden. Fixed to `@if (content.audioScript || content.audioUrl)`.

**P1 fix:** `retell-lecture` and `summarize-group-discussion` — both used raw `<audio controls>` bypassing `AudioPlayerComponent`. Migrated to `<app-audio-player>` with proper import.

**P1 fix:** `AudioPlayerComponent` — added `AudioLoadState` type (`'idle' | 'loading' | 'ready' | 'failed'`), loading indicator (`data-testid="audio-loading"`), failed state (`data-testid="audio-failed"`), retry button (`data-testid="audio-retry-btn"`), and `@for (key of [retryKey]; track key)` DOM re-creation pattern for retry.

**Tests added:**
- `audio-player.component.spec.ts` (NEW) — 17 unit tests covering all state transitions, fallback script, retry, unavailable, and label/helpText rendering.
- `summarize-group-discussion.component.spec.ts` — 4 tests updated for stale testids after migration from raw `<audio>`.
- `exercise-pattern-renderers.spec.ts` — 3 new Playwright tests: audio player section visible when url set, unavailable state when no url, activity submittable with no audio.

**Build/test totals:** Angular unit: 1,496 pass (17 new). Backend: 2,732 pass (unchanged). Playwright E2E: 262 pass, 3 skipped. Production build: clean.

Review: `docs/reviews/2026-06-28-phase-16c-audio-tts-listening-reliability-review.md`.

---

**Phase 16B — Activity Completion and Feedback Loop Hardening** — complete (2026-06-28)

Hardening-only pass verifying the full real activity completion loop: Activity rendered → Student submits → Backend records attempt → Feedback returned → Session/mastery updated → Dashboard/Practice/Journey reflect the change. No new exercise formats, AI logic, UI redesign, or routing changes.

**P0 fix:** `module-redirect.guard.ts` — UUID split bug. The guard used `lastIndexOf('-')` to split `session-{sessionId}-{exerciseId}`. With real UUID IDs (4 hyphens each) this finds the last hyphen inside the exerciseId, producing two wrong IDs. Fixed with UUID pair regex, fallback to `lastIndexOf` for short test IDs. Existing tests used short IDs (`sess1`/`ex1`) and never caught this.

**P1 fix:** `activity-feedback-page.component.ts` — mojibake `â€"` → `—` in `scoreImprovementMessage()` (3 occurrences). Same encoding issue as Phase 15H profile fix.

**P1 fix:** `activity-feedback-page.component.html` — honest empty-feedback state. When AI evaluation returns an empty DTO (all fields null), the feedback page now shows a `data-testid="feedback-pending"` card: "Your response was saved. Feedback will appear after this activity is evaluated." Driven by the new `hasFeedbackContent` getter.

**Verified (no change needed):**
- `disabled` wiring: `ActivityLessonComponent.state === 'submitting'` → `ActivityPracticePageComponent` → `ExerciseRendererComponent[disabled]` — correct.
- Session reload on return: Angular destroys/recreates `LessonComponent` on route change — `loadSession()` in `ngOnInit()` always fires.
- Practice gym suggestions reload: `PracticeGymComponent.ngOnInit()` always fires on return from activity.
- Legacy activity types (VocabularyPractice, ListeningComprehension, WritingScenario) intentionally do not auto-complete exercises — by design.

**Tests added:**
- `module-redirect.guard.spec.ts` — 1 new test: UUID-format ID split.
- `activity-lesson-submission.component.spec.ts` — 13 new unit tests: state transitions, all key payload shapes, error handling, navigation.
- `today-lesson.spec.ts` — today completion smoke test: GapFill activity → feedback → "Next activity" → `/lesson/SESSION_ID`.
- `practice-gym.spec.ts` — practice completion smoke test: ChatReply activity → feedback → "Next activity" → `/practice` → suggestions visible.

**Build/test totals:** Angular unit: 1,479 pass (35 new). Backend: 2,732 pass (unchanged). Production build: clean.

Review: `docs/reviews/2026-06-28-phase-16b-activity-completion-feedback-loop-hardening.md`.

---

**Phase 15I — Student UI Visual Rehaul and Design-System Finalization** — complete (2026-06-28)

Visual-only polish pass across all main student routes. No backend changes. No new features. No routing or logic changes.

**Findings fixed (8 atomic commits):**

- **FINDING-001** (`style(design)`): Mobile greeting nowrap — `sp-greet-lg` gains `white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width:100px` at ≤899px. Prevents name wrapping in header.
- **FINDING-002** (`style(design)`): Added `sp-skeleton` shimmer class to `styles.css`. Replaces static pulse where richer loading feedback is needed. Used by progress, profile, vocabulary, and (via FINDING-006/007) dashboard and journey.
- **FINDING-003** (`style(design)`): `sp-stat-grid` changed from `repeat(3,1fr)` to `repeat(auto-fit,minmax(110px,1fr))` — handles 4-item stat grids on the progress page without wrapping.
- **FINDING-004** (`style(design)`): Profile loading state was bare text ("Loading profile..."). Replaced with 5 `sp-skeleton` cards that mirror the section heights.
- **FINDING-005** (`style(design)`): Mobile FAB Practice button had no label. Added `<span class="sp-practice-label">Practice</span>` with `margin-top:22px` to position it below the circle, matching other nav items.
- **FINDING-006/007** (`style(design)`): Dashboard and journey loading states changed from `sp-loading-pulse` (pulse opacity) to `sp-skeleton` (shimmer). Removed Tailwind height utility classes (`h-48`, `h-32`, `h-40`) in favour of inline heights on `sp-skeleton`.
- **Part M** (`refactor(design)`): Practice gym local `.sp-chip` class renamed to `.sp-skill-tag` — eliminates collision with global interactive `.sp-chip` definition in styles.css. Dead modifier classes `sp-chip--xs` and `sp-chip--muted` removed.

**No tests broken.** Angular unit: 1,464 pass. .NET: 2,732 pass. Playwright E2E: 247 pass, 3 skipped.

Review: `docs/reviews/2026-06-28-phase-15i-student-visual-rehaul-review.md`.

---

**Phase 15H — Student Experience QA and Flow Hardening** — complete (2026-06-28)

Hardening-only pass across all six student pages. No new features, no visual redesign.

**Bug fix (P1):** `profile.component.ts` — `CEFR_EXPLANATIONS` constant (lines 57–64) and fallback dash (line 123) contained garbled UTF-8 mojibake (`â€"` instead of `—`). Both fixed. Regression guard added in student-smoke.spec.ts.

**New E2E smoke suite (Part H):** `e2e/student-smoke.spec.ts` — 18 tests across 6 groups:
- A (5 tests): Unauthenticated → `/dashboard`, `/journey`, `/practice`, `/progress`, `/profile` all redirect to `/login`.
- B (2 tests): Student JWT blocked from `/admin` → `/dashboard`; CourseReady student blocked from `/placement` → `/dashboard`.
- C (2 tests): `PlacementRequired` → `/practice`; `PlacementInProgress` → `/journey` — both redirect to `/placement`.
- D (5 tests): Browser refresh (`page.reload()`) on all five main student routes — auth persists, page re-renders correctly.
- E (3 tests): Mobile 390×844 — `/dashboard`, `/journey`, `/practice` have no horizontal overflow, bottom-nav visible.
- F (1 test): Profile CEFR explanation does not contain garbled encoding (`â€"`).

All 18 tests use mocked APIs. Zero live-backend dependency.

**Route guard audit (Part B):** All guards confirmed correct. `/dashboard`, `/progress`, `/profile` have `authGuard` only (intentional). `/journey`, `/practice`, `/activity`, `/lesson/:id` have `authGuard` + `placementRequiredRedirectGuard`. `/placement` has `authGuard` + `placementAccessGuard`. `/admin/**` has `adminGuard`.

**Admin parity fix (Part I):** `e2e/admin-student-detail.spec.ts` mock was missing handlers for `/practice-summary` (Phase 15D) and `/progress-summary` (Phase 15F). The `{}` fallback left cards in uncertain state, causing intermittent test failure. Added explicit minimal-DTO mocks for both endpoints. All 3 admin-student-detail tests now pass consistently.

**Build/test totals:** 0 errors. Backend: 2,732 (unchanged). Angular unit: 1,464 (unchanged). Playwright E2E: 247 (18 new + 1 fixed, all pass).

Review: `docs/reviews/2026-06-28-phase-15h-student-qa-flow-hardening-review.md`.

---

**Phase 15G — Student Profile and Preferences Functional Integration** — complete (2026-06-28)

Connected the student Profile page to real backend data. Final main student page delivered, completing the full student navigation set (Dashboard, Today, Practice, Journey, Progress, Profile).

**CEFR read-only (Part B):** CEFR level is read-only everywhere in the student UI. Backend `UpdateLearningPreferencesCommand` excludes `CefrLevel`. Template has no input/select in `level-section`. Explainer text: "Your level is updated through placement, learning progress, and teacher/admin review." Enforced at all three layers.

**Learning preferences (Part C):** Students can update goals, focus areas, support language, translation preference, difficulty, and session length via PUT `/api/profile/preferences`. On save, `ProfileCommandHandler` triggers `RegeneratePlanAsync("preference_change")` fire-and-forget — regeneration failure is non-fatal.

**Placement summary in profile (Part F):** Profile component calls `PlacementService.getAdaptiveCurrent()` and `PlacementService.getPlacementConfig()` in `ngOnInit`. Both wrapped with `catchError(() => of(null))` — placement load failure does not break the rest of the profile. Shows confirmed/provisional badge, skill breakdown table, and placement date. Retake button gated by `allowPlacementRetake` config (default false → shows "not available yet" text).

**Admin parity (Part H):** Admin already has access to all preference values via existing `GET /api/admin/students/{id}/profile`. No new endpoint needed.

**Pre-existing admin spec fix:** 110 Angular tests in `admin-student-detail.component.spec.ts` were failing from Phase 15F (missing `getStudentProgressSummary` mock). Fixed by updating all 15 Jasmine spy setups.

**Tests (Parts K–L):** 13 new Angular unit tests in `profile.component.spec.ts` (38 total). New `e2e/profile.spec.ts` with 10 Playwright tests (CEFR read-only, placement summary, retake gating, notification prefs, save, no raw JSON).

**Build/test totals:** 0 errors. 3 arch + 1,504 unit + 1,225 integration = **2,732 backend**. 1,464 Angular unit tests. 229 Playwright E2E (+ 3 skipped). All pass.

Review: `docs/reviews/2026-06-28-phase-15g-student-profile-preferences-review.md`.

---

**Phase 15D — Adaptive Practice Gym Experience** — complete (2026-06-28)

Exposed existing backend adaptive Practice Gym capabilities in the student UI. No new algorithms or exercise formats — wiring only.

**Explanation render (Part B):** `PracticeGymSuggestionItemDto.Explanation` was populated server-side but never rendered. Added `@if (item.explanation)` block in `practice-gym.component.html` showing the recommendation reason (e.g. "Listening is your weakest skill") below the card description. `data-testid="suggestion-reason"`.

**Review queue empty state (Part C):** Review section was hidden when empty. Changed to always-visible with "You're all caught up. Nothing needs review right now." `data-testid="review-queue-empty"`.

**Retry button (Part D):** Error state now has `data-testid="suggestions-retry"` button. `loadSuggestions()` visibility changed from `private` to accessible so template can call it directly.

**Admin parity (Part E):** New `GET /api/admin/students/{id}/practice-summary` endpoint. Reuses `IPracticeGymSuggestionService`. Returns `AdminStudentPracticeResult` with status, review queue count, reserved count, weakest skill, top suggestion, and replenishment flag. Admin student detail page shows a Practice Gym summary card.

**Tests (Parts F–I):** 6 new Karma tests in `practice-gym.component.spec.ts`. 7 new Karma tests + full spy setup update in `admin-student-detail.component.spec.ts`. 4 new backend integration tests in `AdminStudentPracticeTests.cs`. Suggestions API mock added to `mockPracticeRoute()` in `practice-gym.spec.ts` with 2 new E2E tests.

**Build/test totals:** 0 errors. 3 arch + 1,504 unit + 1,212 integration = **2,719 backend**. 1,427 Angular unit tests. All pass. Playwright E2E pending manual/CI run.

Review: `docs/reviews/2026-06-28-phase-15d-adaptive-practice-gym-experience-review.md`.

---

**Phase 15C — Today Lesson Player Foundation** — complete (2026-06-28)

Today lesson player wired to real session and activity data. Audit confirmed core lesson infrastructure (session stepper, prepare flow, moduleRedirectGuard→/activity, completion lifecycle, placement guard) was already in place. This phase closes the remaining gaps.

**CEFR in session detail (Parts B/C):** `SessionDetailResult` DTO gains `string? CefrLevel`. Populated from `profile.CefrLevel` in `SessionQueryHandler`. TypeScript `SessionDetailResponse` gains `cefrLevel: string | null`. Lesson header renders `data-testid="lesson-cefr-level"` badge when non-null.

**Unsupported activity shell (Part E):** `ExerciseRendererComponent @default` block changed from silent FreeTextEntry fallback to honest "Activity not available — not available in the lesson player yet" message with `data-testid="unsupported-activity-type"`.

**Explicit Today-page state model (Part B):** `DashboardComponent.todaySessionState()` method added, returning one of 7 named states: `PlacementRequired | Preparing | Ready | InProgress | CompletedToday | NotAvailable | Error`. Derives state from `error()`, `lifecycleStage`, and `todaysSession()` signal.

**Dashboard preparing testid (Part H):** Added `data-testid="session-preparing"` to the dashboard preparing block. Copy updated from "Your first lesson is being prepared" to "Your lesson is being prepared".

**Tests (Part I):** 1 new unit test in `exercise-renderer.component.spec.ts` — unknown `interactionMode` shows unsupported message. 5 new Playwright tests in `today-lesson.spec.ts` — preparing state, placement redirect, CEFR header, review panel, error containment.

**Build/test totals:** 0 errors. 3 arch + 1,504 unit + 1,208 integration = **2,715 backend**. 1,414 Angular unit tests. 156 Playwright E2E tests. All pass.

Review: `docs/reviews/2026-06-28-phase-15c-today-lesson-player-foundation-review.md`.

---

**Phase 15B — Consolidated Student Dashboard Summary API** — complete (2026-06-27)

Single consolidated endpoint replacing 4–5 separate HTTP calls on dashboard load. Backend orchestrator wraps core dashboard, today's session, practice suggestions, and learning memory. Each optional section degrades gracefully — failure maps to a named status state, never to 500.

**New endpoint:** `GET /api/student/dashboard/summary` — authenticated student only. Returns 8 named sections: Profile, CourseReadiness, TodaySession, LearningPlan, Practice, Progress, QuickStats, Warnings.

**Backend (Parts B–D):** `StudentDashboardSummaryQuery.cs` — 8 result records plus interface. `StudentDashboardSummaryHandler.cs` — orchestrates 4 existing handlers; each optional call wrapped in `try/catch`; session/practice/memory failures map to status strings `"Preparing"` / `"NotAvailable"`. `StudentDashboardController.cs` — new controller at `GET /api/student/dashboard/summary`.

**Session status mapping:** `SessionStatus.Completed → "Completed"`, `InProgress → "InProgress"`, else `"Ready"`. Non-active lifecycle or failure → `"NotAvailable"`. Missing session → `"Preparing"`.

**Practice status mapping:** service throws → `"NotAvailable"` (leaves signal null in component for template parity). Empty suggestions → `"Preparing"`. Active items → `"Ready"`.

**Frontend (Parts A/E):** `dashboard-summary.models.ts` — 8 TypeScript interfaces. `dashboard-summary.service.ts` — single `getSummary()` call. `dashboard.component.ts` — replaced 4 injected services with `DashboardSummaryService` as sole data source. `applyFromSummary()` synthesizes all 4 existing signal types so the HTML template is **unchanged**. `practice.status === "NotAvailable"` leaves `practiceSuggestions` signal null to preserve template `practice-preparing` vs `practice-empty` distinction.

**Tests (Part G):** 9 backend integration tests in `StudentDashboardSummaryTests` (2 test classes — `IClassFixture` pattern required for SQLite init). 13 Karma/Jasmine unit tests in `dashboard.component.spec.ts` (rewritten for new service). 24 Playwright tests updated — `today-lesson.spec.ts` and `today-page-identity.spec.ts` now mock `**/api/student/dashboard/summary` instead of the 3 old individual endpoints.

**Build/test totals:** 0 errors. 3 arch + 1,504 unit + 1,208 integration = **2,715 backend**. 1,413 Angular unit tests. 24 Playwright E2E tests. All pass.

Review: `docs/reviews/2026-06-27-phase-15b-dashboard-summary-api-review.md`.

---

**Phase 15A — Learning Plan Powered Student Dashboard** — complete (2026-06-27)

**Phase 14B — CourseReady Transition and First Lesson Dashboard Smoke** — complete (2026-06-27)

Closure phase that closes the post-placement lifecycle gap. After successful placement, students now transition to `CourseReady` (instead of staying at `PlacementCompleted`). The dashboard shows a preparing card while the learning plan is generated. Admin detail page surfaces learning readiness fields. Full Playwright smoke test covers placement → dashboard.

**Lifecycle fix (Part B):** `FinalizeCompletionAsync` now transitions `PlacementCompleted → CourseReady` when learning plan regeneration succeeds. Idempotent and guarded — stays at `PlacementCompleted` when plan fails.

**Dashboard preparing message (Part B):** `DashboardQueryHandler` now has a `PlacementCompleted` case with an honest "being prepared" message.

**Admin parity (Part F):** `AdminStudentDetailDto` gains `IsLearningReady`, `LastPlacementCompletedAt`, `LearningPlanExists`. Admin detail component surfaces these with status badges.

**Dashboard null-session fallback (Part D):** Changed misleading "Your lesson is ready" to "Your first lesson is being prepared / Check back in a moment".

**Tests (Part H):** +5 new backend integration tests in `StudentPlacementCourseReadyTests`. `FailingLearningPlanFactory` verifies `PlacementCompleted` is preserved on plan failure.

**Playwright (Part G):** 3 smoke tests in `e2e/student-placement-dashboard.spec.ts` — full placement flow → CourseReady dashboard, PlacementCompleted preparing card, journey guard redirect.

**Build/test totals:** 0 errors. 3 arch + 1,504 unit + 1,199 integration = **2,706 backend**. 1,399 Angular unit tests. 3 Playwright E2E smoke tests. All pass.

Review: `docs/reviews/2026-06-27-phase-14b-courseready-transition-review.md`.

---

## Previous sprint

**Phase 14A — Student Placement Journey (End-to-End)** — complete (2026-06-27)

First complete student-facing vertical slice on top of the adaptive placement engine. Full flow: First Login → Placement Assessment → Adaptive Questions → Placement Complete → Learning Plan Generated → Today Lesson Unlocked.

**Config (Part B):** `PlacementAssessmentOptions` gains 5 flags: `PlacementRequiredBeforeLearning`, `AllowSkipPlacement`, `AllowPlacementRetake`, `ResumeInterruptedPlacement`, `AutoStartPlacement`. Defaults wired in `appsettings.json`.

**Lifecycle fix:** `PlacementAssessmentService` now transitions `PlacementRequired → PlacementInProgress` on `StartAssessmentAsync` and `PlacementInProgress → PlacementCompleted` on `FinalizeCompletionAsync`. Without this, guards trapped students at `/placement` forever after completion.

**Student API (Part C):** New `StudentPlacementController` at `GET/POST api/student/placement/*` — config, current, next item, start (201), resume, respond, complete. Ownership enforced on every assessment lookup. Returns 409 when completed and retake disabled.

**Admin actions (Part G):** `AdminPlacementController` gains `POST .../abandon` and `POST .../expire`. `admin.api.service.ts` and `admin-student-detail` component updated with Abandon/Expire buttons (visible on InProgress assessments only).

**Angular placement flow (Parts D/E):** `placement.component.ts` rewritten as adaptive state machine (`loading → welcome → question → submitting → completing → done/error`). `placement.component.html` rewritten: progress bar, MCQ choice buttons with indigo ring, gap_fill input, CEFR result card, 2-col skill grid. `placement.service.ts` gains 7 new adaptive methods; all old methods kept (guards still call old `getStatus()`).

**Guard fix (Part E):** `placementAccessGuard` now redirects `PlacementCompleted`/`CourseReady`/`InLesson`/`ActiveLearning`/`Paused`/`Archived` stages to `/dashboard` instead of allowing entry to `/placement`.

**Tests (Parts I/J):** +17 backend integration tests (`StudentPlacementControllerTests`). +19 Karma/Jasmine unit tests (placement component spec rewritten for adaptive flow). All pass.

**Build/test totals:** 0 errors. 3 arch + 1,493 unit + 1,194 integration = **2,690 backend**. No migration needed.

Review: `docs/reviews/2026-06-27-phase-14a-student-placement-journey-review.md`.

---

**Phase 13A — Adaptive Placement Engine Foundation** — complete (2026-06-27)

Delivers the domain model, deterministic scoring algorithm, admin API, persistence migration (T62), and admin Angular read-only UI for the adaptive placement assessment system. No AI calls — fully deterministic 72-item seeded bank covers 6 skills × 4 CEFR levels.

**Domain (Parts B):** `PlacementStatus` extended (`Abandoned=3`, `Expired=4`, `Failed=5`). `PlacementAssessment` gains 7 new fields, `CreateAdaptive()` factory, `Abandon()`, `Expire()`, and `CompleteAdaptive()`. Two new entities: `PlacementAssessmentItem` (stores individual test items with correct answer for deterministic scoring) and `PlacementSkillResult` (per-skill CEFR + confidence after evaluation).

**Application (Parts C):** `PlacementAssessmentOptions` config class. Three DTO records: `PlacementSkillResultDto`, `PlacementAssessmentSummaryDto`, `PlacementHistoryItemDto`. `IPlacementAssessmentService` interface with 5 methods.

**Infrastructure/Algorithm (Parts D+E+F):** `PlacementAssessmentService` implements deterministic adaptive scoring: 70% pass rate advances level, below 40% fails level. Confidence = min(evidenceCount/6, correctRatio). Overall CEFR = minimum of per-skill estimates (conservative). Updates `StudentProfile.CefrLevel` when confidence >= 0.6. Triggers `ILearningPlanService.RegeneratePlanAsync("placement_completed")` — failure is caught/logged, never blocks placement completion.

**Persistence (Parts I+migration T62):** Two new EF configurations. Two new `DbSet<T>`. Hand-authored migration T62 adds 7 columns to `placement_assessments`, creates `placement_assessment_items` and `placement_skill_results` tables.

**Admin API (Part G):** `AdminPlacementController` with 4 endpoints: GET latest, GET history, POST start, POST complete.

**Angular UI (Part H):** `admin.models.ts` gains 3 new interfaces. `admin.api.service.ts` gains 2 new methods. Admin student detail page shows a read-only placement section.

**Tests (Part J):** +28 tests (18 unit domain, 10 integration). All 2661 tests pass (3 arch + 1493 unit + 1165 integration).

Review: `docs/reviews/2026-06-27-phase-13a-adaptive-placement-engine-foundation-review.md`.

---

**Phase 12G — Real-Time Learning Plan Progress Integration** — complete (2026-06-27)

Wires learning plan objective updates directly into the activity submission path. Plan progress now updates immediately after each pattern-keyed activity attempt; no wait for nightly mastery sweep. Background jobs become reconciliation-only.

**Real-time pipeline (Parts B/C):** `ILearningPlanService` gains `TryUpdateObjectiveProgressAsync` — evaluates mastery for a single objective and transitions the plan if evidence is sufficient. Never throws; returns `LearningPlanObjectiveProgressUpdate` with `StatusChanged`, `PreviousStatus`, `NewStatus`, and `Reason`.

**ActivitySubmitHandler wiring (Parts D/E):** `ILearningPlanService` added as a dependency. Two injection points added: after `_learningLedger.RecordAsync` in the pattern evaluation path and the legacy writing path. VocabularyPractice and ListeningComprehension excluded (no learning event recorded in those paths). Helper `TryUpdateLearningPlanProgressAsync` guards on null/empty key and logs when StatusChanged=true.

**Progress summary (Part F):** `LearningPlanProgressSummary` gains three new fields: `CurrentObjectiveKey` (InProgress, or first Active), `NextObjectiveKey` (first Active when InProgress exists; second Active otherwise), and `ObjectivesCompletedToday` (Completed/Mastered since midnight UTC). `GetProgressAsync` populates all three.

**Tests (Part J):** 15 new unit tests in `LearningPlanRealtimeProgressTests`. Updated `LearningPlanDomainTests` test 23 and `LearningPlanCompletionTests` test 8 for expanded record signature.

**Build/test totals:** 0 errors, 0 failures. 1475 unit + 1155 integration + 3 architecture = 2633 total.

Review: `docs/reviews/2026-06-27-phase-12g-realtime-learning-plan-progress-review.md`.

---

## Previous sprint

**Phase 12F — Learning Plan Completion Lifecycle** — complete (2026-06-27)

Closes the Learning Plan lifecycle loop. Objectives now transition deterministically through `Active → InProgress → Completed → Mastered` driven by existing mastery evaluation evidence.

**Completion service (Parts B/C):** `ILearningPlanService` gains `MarkObjectiveCompletedAsync` and `MarkObjectiveMasteredAsync`. Both are idempotent: already-Completed → no-op for a second Completed call; already-Mastered → no-op regardless of incoming signal. Implemented in `LearningPlanService` via shared `TransitionObjectiveAsync` helper. Logs plan exhaustion when no Active/InProgress objectives remain.

**Mastery report (Part B):** `StudentMasteryReport` gains `CompletedObjectiveKeys` — objectives with `NeedsReview` mastery signal (consecutive successes >= 1, avg score 50-79). These also remain in `WeakObjectiveKeys`. `StudentMasteryEvaluationService.EvaluateStudentAsync` populates the new field.

**Event integration (Part D):** `StudentMasteryEvaluationJob` now calls `MarkObjectiveMasteredAsync` for each mastered key and `MarkObjectiveCompletedAsync` for each completed key before triggering `RegeneratePlanAsync`. All completion calls are warning-only; generation continues regardless.

**Progress calculation (Part F):** `LearningPlanProgressSummary` expanded with `TotalObjectives`, `ObjectivesMastered`, `ObjectivesInProgress`, `DeferredObjectives`, `CompletionPercentage` (Completed+Mastered / Total), and `LastCompletedAt`. `MasteryPercentage` now reflects Mastered / Total specifically. `DeterminePhase` uses completion percentage.

**Background jobs (Part I):** No changes required. `GetNextPlannedObjectiveAsync` already filters to `Active` only — completed and mastered objectives are excluded automatically.

**Admin API (Part G):** `GET /api/admin/students/{id}/learning-plan/progress` now returns expanded summary with all new fields. No new endpoints.

**Tests (Part K):** 16 new unit tests in `LearningPlanCompletionTests`. `LearningPlanDomainTests` test 23 updated for new record signature. All 2618 tests pass.

**Build/test totals:** 0 errors, 0 failures. 1460 unit + 1155 integration + 3 architecture = 2618 total.

Review: `docs/reviews/2026-06-27-phase-12f-learning-plan-completion-lifecycle-review.md`.

---

## Previous sprint

**Phase 12E — Learning Plan Guided Routing** — complete (2026-06-27)

Closes the Phase 12D gap where `PreferredObjectiveKey` was passed to routing but never consumed. Curriculum routing now selects the planned objective first when all safety checks pass, and falls back silently to existing routing when they do not.

**Routing (Part B):** `CurriculumRoutingService.RecommendAsync` gains a new pre-step (Step 2c) after `FilterByMastered`. When `PreferredObjectiveKey` is non-null, `TrySelectPreferredObjectiveAsync` validates it against five safety rules (exists in syllabus, CEFR match or one-level-lower with AllowReviewOrScaffold, skill compatibility, runnable, mastery exclusion). Accepted → `RoutingReason.LearningPlan`. Rejected → log only, fall through to existing pipeline unchanged. New enum value `RoutingReason.LearningPlan = 5`.

**Status lifecycle (Part C):** `LearningPlanObjectiveStatus.InProgress = 6` added. `StudentLearningPlanObjective.MarkInProgress()` transitions `Active → InProgress`. `ILearningPlanService.MarkObjectiveInProgressAsync` added and implemented in `LearningPlanService`.

**Background jobs (Part D):** `LessonBatchGenerationJob` and `PracticeGymGenerationJob` both call `MarkObjectiveInProgressAsync` after routing returns `LearningPlan` reason. Failures are warning-only; generation continues regardless.

**Admin routing preview (Part E):** `AdminRoutingPreviewRequest` gains optional `PreferredObjectiveKey`. `PreviewRoutingAsync` passes it through and returns `PreferredObjectiveDisposition` ("accepted" / "rejected" / "fallback_used" / null) in `AdminRoutingPreviewResult`. Rejected hint adds a warning to the response.

**Tests (Part F):** 15 new unit tests in `CurriculumRoutingServicePreferredObjectiveTests` covering all acceptance/rejection rules, fallback, CEFR safety, mastery exclusion, and override-of-score-based-selection. `LearningPlanDomainTests` updated for new enum count (6 → 7).

**Build/test totals:** 0 errors, 0 failures. 1444 unit + 1155 integration + 3 architecture = 2602 total.

Review: `docs/reviews/2026-06-27-phase-12e-learning-plan-guided-routing-review.md`.

---

## Previous sprint

**Phase 12D — Learning Plan Orchestrator Foundation** — complete (2026-06-27)

Introduces a deterministic Learning Plan Orchestrator that coordinates curriculum routing, mastery evaluation, and readiness pool replenishment into a coherent per-student objective sequence. No AI calls in the plan layer. No student UI changes. No ReviewScaffold global enable. Reuses and wraps existing routing/mastery/readiness infrastructure.

**Domain (new):** `StudentLearningPlan` + `StudentLearningPlanObjective` entities with lifecycle methods (`MarkReady`, `Supersede`, `StartRegeneration`, `MarkCompleted`, `MarkMastered`, `Unblock`, etc.). Enums: `LearningPlanStatus` (Active/Regenerating/Superseded), `LearningPlanObjectiveStatus` (Active/Completed/Mastered/Blocked/Deferred/Review).

**Application (new):** `ILearningPlanService` interface with `GetOrCreatePlanAsync`, `RegeneratePlanAsync`, `GetProgressAsync`, `GetNextPlannedObjectiveAsync`, `GetPracticeGymObjectivesAsync`. DTOs: `LearningPlanSummary`, `LearningPlanProgressSummary`, `PlannedObjectiveContext`. `LearningPlanOptions` config class (`PlannedLessonCount=10`, `MasteryCompletionThreshold=70`). Added `PlanGeneration=5` to `MasteryEvaluationReason` enum. Added `PreferredObjectiveKey` to `CurriculumRoutingRequest` + factory.

**Infrastructure (new):** `LearningPlanService` — deterministic implementation. Builds 10-objective sequence from skill rotation, inserts review objectives for weak/mastered keys, prevents duplicates, persists plan + objectives.

**Persistence:** EF Core configurations for both new entities (`student_learning_plans`, `student_learning_plan_objectives`). Migration T61 (`T61_LearningPlanOrchestrator`). Two new `DbSet<>` properties on `LinguaCoachDbContext`.

**Regeneration triggers (F/G/H):** `StudentMasteryEvaluationJob` calls `RegeneratePlanAsync("mastery_sweep")` when mastery changed (demoted or newly mastered). `ProfileCommandHandler` calls `RegeneratePlanAsync("preference_change")` after student preference update. `AdminHandler.SetStudentCefrAsync` calls `RegeneratePlanAsync("cefr_change")` when CEFR level changes. All triggers are fire-and-forget with warning-only failure logging.

**Admin visibility (I):** `GET /api/admin/students/{id}/learning-plan` and `GET /api/admin/students/{id}/learning-plan/progress` endpoints on `AdminReadinessPoolController`. Read-only, admin-only, no side effects.

**Background job integration (J):** `LessonBatchGenerationJob` calls `GetNextPlannedObjectiveAsync` and passes `preferredObjectiveKey` to routing. `PracticeGymGenerationJob` calls `GetPracticeGymObjectivesAsync(maxCount:1)` and passes `preferredObjectiveKey`. Both fall back to free routing on plan failure.

**Tests:** 30 unit tests (`LearningPlanDomainTests`) + 8 integration tests (`LearningPlanIntegrationTests`). All 2587 tests pass (3 arch + 1429 unit + 1155 integration). No Angular changes.

Review: `docs/reviews/2026-06-27-phase-12d-learning-plan-orchestrator-foundation-review.md`.

---

**Phase 12C — Prepared Lesson Pipeline and Readiness Lifecycle** — complete (2026-06-27)

Configurable buffer bounds and enhanced pool observability. Added `MinimumReadyThreshold` (default 3) and `MaxBufferCount` (default 20) to `ReadinessPoolReplenishmentOptions`. `MaxBufferCount` enforces a hard cap on active items (Queued + Generating + Ready + Reserved) per student per source — `FillShortfallAsync` returns `(0, 0, toCreate)` immediately when already at cap, and caps `toCreate = min(toCreate, MaxBufferCount - activeCount)` otherwise. `ReplenishmentRunSummary` extended with `SkippedAtMaxBuffer`, `ElapsedMs` (computed), and `GenerationSuccessRate` (computed). Replenishment completion log includes `elapsedMs` and `successRate` for per-run observability. `AggregatePoolHealthSummary` extended with `StudentsBelowMinimumThreshold` (students with Ready < `MinimumReadyThreshold`, including zero-ready) and `AverageReadyPerStudent`. Admin Lessons UI displays both new metrics in the aggregate pool health stat grid. 17 new tests (12 unit + 5 integration) — 1399 unit + 1147 integration + 3 arch + 1384 Angular = 3933 all passing. No migration. No student UI changes. No ReviewScaffold global enable. Review: `docs/reviews/2026-06-27-phase-12c-prepared-lesson-pipeline-readiness-lifecycle-review.md`.

---

**Phase 12A — Production Gap Closure: Pool Health and Welcome Email** — complete (2026-06-27)

Closed two production gaps found in Phase 11E live admin QA. F-04: Added `GET /api/admin/readiness-pool/health` endpoint returning `AggregatePoolHealthSummary` (all status counts + per-status student counts, computed in one DB round-trip). Admin Lessons page replaced placeholder card with real stat grid — loading/error states, 8 status metrics, failure alert, refresh button. F-03: Audited `CreateStudentHandler` — welcome email already wired via `QueueEmailAsync` using `account.student_created` template; no backend changes. Part 0 audit: `RoutingEmailSender`, `ResendEmailSender`, `SendGridEmailSender`, `NotificationChannelConfigResolver`, DI registrations, and Angular admin integrations UI — all clean, no raw passwords logged or emailed. Added 6 email routing unit tests (`TrackingServiceProvider` pattern), 5 aggregate pool health integration tests, 3 Angular pool health component tests. Fixed 3 pre-existing test gaps: missing `provider: 'Smtp'` in notifications spec, stale `'SMTP / Email'` assertion in integrations spec, missing `getAggregatePoolHealth` spy in lessons spec. 31 new tests total — 1362 unit + 1113 integration + 3 arch + 1384 Angular = 3862 all passing. No migration. No student UI. Review: `docs/reviews/2026-06-27-phase-12a-pool-health-and-welcome-email-review.md`.

---

**Phase 11B — Curriculum Objective Coverage and Mapping Hardening** — complete (2026-06-26)

Curriculum hardening across Parts A–K. Seed expanded from 22 to 33 objectives, closing all A1–B2 × {speaking, listening, reading, writing, grammar, vocabulary, pronunciation} coverage gaps. New: `ICurriculumValidationService` (Application) + `CurriculumValidationService` (Infrastructure) with 10 validation checks (duplicate keys, invalid CEFR/skill, missing fields, dangling/circular/disabled prerequisites, invalid context/focus tags, coverage gaps, non-runnable skill warnings). `ActivityCompatibilityConstants` documents runnable vs planned skill/exercise format mapping. Routing hardened: `CurriculumRoutingRequest` adds `MasteredObjectiveKeys` and `AllowReviewOfMastered` fields so mastered objectives can be excluded from new-learning routes. Two new admin endpoints: `GET /api/admin/curriculum/validation` and `GET /api/admin/curriculum/coverage`. Admin curriculum UI at `/admin/curriculum` shows live validation summary card, error/warning alerts, and coverage gap list — all from real backend data. 12 new unit tests + 3 routing tests + 2 integration tests + 10 Angular tests = 27 new tests. All 1344 unit / 1103 integration / 3 arch / 1381 Angular tests pass. No migration. No student UI. No admin visual redesign. Review: `docs/reviews/2026-06-26-phase-11b-curriculum-objective-coverage-mapping-hardening-review.md`.

---

**Phase 11A — Admin Onboarding Builder** — complete (2026-06-26)

Admin-configurable onboarding system. Admins can create onboarding flow configurations, define/manage/reorder step definitions, and activate a flow from `/admin/onboarding`. No student UI changes. Backend: 7 new handler interfaces + implementations (list flows, create flow, activate flow, add/update/remove/reorder steps), 9 new API endpoints on `AdminOnboardingController`, `Navigation().HasField("_steps")` EF config fix, domain methods `Deactivate`/`RemoveStep`/`ReorderSteps`/`Update`/`SetOrder` added. Frontend: `admin-onboarding.models.ts`, `admin-onboarding.service.ts`, `admin-onboarding` feature page with KPI strip + step table + add/edit slide-over, `/admin/onboarding` route, sidebar nav item. 8 new integration tests. All 2418 tests pass. No migration required (schema pre-existing). Review: `docs/reviews/2026-06-26-phase-11a-admin-onboarding-builder-review.md`.

---

**Phase 10Z — Mastery Re-evaluation Engine** — complete (2026-06-26)

Deterministic mastery engine evaluating student skill/objective mastery from learning event history. No AI calls. Adds `MasteryStatus` and `ReadinessDemotionDecision` enums (Domain), `ObjectiveMasterySignal`/`StudentMasteryReport`/`MasteryOptions` types (Application), `IStudentMasteryEvaluationService` interface (Application), `StudentMasteryEvaluationService` Infrastructure implementation, `StudentMasteryEvaluationJob` Quartz job (daily sweep), `MasteryOptions` config section in appsettings.json, `MasteredCount`/`NeedsReviewCount`/`LastEvaluatedAtUtc` added to `ReadinessPoolSummary` DTO, DI registration in `DependencyInjection.cs`, and Quartz schedule in `QuartzConfiguration.cs`. 11 new unit tests — all 1329 unit tests pass. Build clean. No migration needed (no new DB columns). No student UI changes. No admin UI changes. No AI calls. Review: `docs/reviews/2026-06-26-phase-10z-mastery-engine-review.md`.

---

**Phase 10Y — Learning Activity Lifecycle Completion** — complete (2026-06-26)

Backend-only lifecycle hardening for the readiness pool. Gaps filled: `Skipped` terminal status (mastered/irrelevant items), CEFR mismatch stale demotion sweep in replenishment service, `ReservedCount` and `SkippedCount` surfaced in pool health and admin diagnostics, `LastEvaluatedAtUtc` timestamp for incremental evaluation tracking, EF migration T60. 13 new tests (8 unit + 5 integration) — 1318 unit + 64 targeted integration all pass. No student UI changes. No admin visual redesign. No fake data. Review: `docs/reviews/2026-06-26-phase-10y-learning-activity-lifecycle-completion-review.md`.

---

**Phase 10UI-STYLE-1 — Admin Reusable Component Visual Alignment** — complete (2026-06-23)

Admin design tokens updated to match SpeakPath brand: text/muted/dim colours → warm purple ink palette; green → brand green (`#13B07C`); shadows → purple-tinted. KPI card: value 28px/800, label 800 weight, icon 40px. Card: title CSS token style, default radius 14px. Badge: font-weight 700. Data table header: muted colour, 800 weight. Page header: tighter letter-spacing. Global styles: 11 new `.sp-adm-*` slide-over/form utility classes. Notifications: 29 Tailwind literals in slide-overs replaced. Integrations: 3 Tailwind literals replaced. All 14 admin routes benefit automatically. 1253/1253 PASS. Build clean. No backend changes. No data logic changes.

---

**Phase 10UI-REDESIGN-FINAL — Admin UI Reference Alignment Closure Audit** — complete (2026-06-23)

Route-by-route closure audit of the full admin UI redesign epic (REDESIGN-1 through REDESIGN-8). One structural bug found and fixed: notifications `sp-admin-page-header` was incorrectly nested inside `sp-admin-page-body` (same fix as Security in REDESIGN-8). 2 new tests added. Full audit confirmed: no fake production data anywhere, no secrets displayed, all 14 admin routes use correct `sp-admin-*` components, all KPI strips derive from real backend data or show explicit "Backend not available yet" labels. 1253/1253 PASS. Production build clean. Admin UI redesign epic closed. Review: `docs/reviews/2026-06-23-phase-10ui-redesign-final-admin-ui-reference-closure-audit.md`.

---

**Phase 10UI-REDESIGN-3 — Student Detail Reference Redesign** — complete (2026-06-23)

Hero section added: coloured initials avatar (hash-based colour, 8-palette), student display name with email/name fallbacks, lifecycle/onboarding/CEFR/support-language badges, action group (Edit, Reset password, Send reset link, Pause/Unpause/Reactivate). KPI strip upgraded from sp-admin-stat-card to sp-admin-kpi-card with icon tiles (Lifecycle, Onboarding, CEFR, Pool health). Danger zone card added as full-width last section (Reset data, Archive, Reactivate — all wired to existing modal flows). Back-to-students link in page header. 30 new tests. 1168/1168 pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-redesign-3-student-detail-reference-redesign.md

---

**Phase 10UI-REDESIGN-2 — Students List and Create Student Reference Redesign** — complete (2026-06-23)

Students list redesigned: 4-tile KPI summary strip (Total students, Onboarded, Activities tracked, Showing this page) using real `getStats()` data, rows-per-page selector (10/25/50/100), filter bar aligned to reference. Create Student redesigned: two-column layout (multi-section form cards left, sticky "What happens next" aside right), back-to-students link, security note about one-time password, Welcome email not-available-yet note. All original fields, validation, submit payload, and modal actions preserved. 21 new tests + 8 migration spec spy fixes. 1138/1138 pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-redesign-2-students-create-reference-redesign.md

---

**Phase 10UI-REDESIGN-1 — Admin Dashboard Reference Redesign** — complete (2026-06-23)

Dashboard fully redesigned to match the SpeakPath admin reference layout. Dark hero weekly-snapshot banner (2 real slots, 2 placeholders). 5-tile KPI icon row (3 real, 1 live AI config, 1 placeholder). Onboarding funnel, at-risk students, and CEFR distribution derived from students list. All unavailable sections (activity chart, score distribution, AI spend, streak, avg session, live events) show explicit "Not implemented" / "Backend not available yet" placeholders. No fake data. 31 new tests + 1 migration spec updated. 1117/1117 pass.

Review: docs/reviews/2026-06-23-phase-10ui-redesign-1-dashboard-reference-redesign.md

---

**Phase 10UI-REDESIGN-0 — Admin Reference Redesign Rollout Plan** — complete (2026-06-23)

Full route-by-route redesign plan for all 14 admin routes. Reference design files read and compared against current Angular components. Page redesign matrix, component gap matrix, and phase sequence produced. No Angular source changes. Two P1 gaps confirmed: dashboard (Old layout — 4 stat cards only) and integrations (domain mismatch — reference SMTP/Webhook/Slack/Analytics/Admin API not surfaced). Next phase: **10UI-REDESIGN-1** (dashboard reference redesign).

Review: docs/reviews/2026-06-23-phase-10ui-redesign-0-admin-reference-redesign-rollout-plan.md

---

**Phase 10UI-FIX-8 — Notifications, Security, and Integrations Admin Polish** — complete (2026-06-23)

SMS channel and config card: Foundation-only badge and warning alert. Security: deferred capabilities card listing MFA, enterprise SSO, distributed rate limiting, captcha, CSP/HSTS, SMS notifications, admin session management. Integrations: readiness pool aggregate placeholder card. 12 new tests. 1086/1086 pass.

Review: docs/reviews/2026-06-23-phase-10ui-fix-8-notifications-security-integrations-polish.md

---

**Phase 10UI-FIX-7 — Student Detail Reference Alignment, Readiness Pool, Activity History** — complete (2026-06-23)

Full wrapper migration for `/admin/students/:id`. Page header migrated to `sp-admin-page-header` with projected action buttons. All `<section class="sp-admin-table-card">` cards replaced with `sp-admin-card`. All raw badge class strings replaced with `sp-admin-badge` wrappers using `lifecycleTone`/`onboardingTone` utils. Activity and audit history tables migrated to `sp-admin-table`. Error states migrated to `sp-admin-alert`. New KPI strip (lifecycle, onboarding, CEFR, pool health stat cards). New readiness pool health section (TODO-UI-02) wiring `GET /api/admin/students/{id}/readiness-pool/health` with today/gym breakdown and replenishment badges. `StudentReadinessPoolHealth` DTO added to `admin.models.ts`; `getStudentReadinessPoolHealth()` added to `AdminApiService`. 9 new pool health tests. 1074/1074 Angular tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-7-student-detail-reference-alignment.md

---

**Phase 10UI-FIX-6 — Students Header, Create Student Wrapper Migration, Curriculum Filter Bar** — complete (2026-06-23)

Students list "Create student" header action migrated from raw `<a class="sp-admin-btn-primary">` to `sp-admin-button`. Create Student page fully migrated to `sp-admin-*` wrappers (page-header, page-body, card, form-field, input, alert, button); native selects retained for `[ngValue]` number/null bindings. Curriculum filter bar migrated from raw `<select class="sp-input">` to `sp-admin-select` with computed option arrays; page body wrapped in `sp-admin-page-body`. 20 new tests. 1065/1065 Angular tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-6-students-create-curriculum-alignment.md

---

**Phase 10UI-FIX-5 — Careers Route Redirect and Dashboard AI Provider Accuracy** — complete (2026-06-23)

Deleted stale `AdminUsageComponent` (emoji placeholder, never routed). Redirected `/admin/careers` → `/admin/curriculum`. Replaced static dashboard "AI provider: Configured" stat card with live status derived from `listAiCategories()` — shows "Configured" / "N/M configured" / "Not configured" / "Unknown" based on real backend state. Replaced hardcoded "Active" AI System card rows with live per-category loop showing real provider names. 10 new dashboard tests + 2 wrapper-migration mock fixes. 1045/1045 Angular tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-5-careers-dashboard-provider-accuracy.md

---

**Phase 10UI-FIX-4 — Admin Page-Level Spot-Check and Alignment Plan** — complete (2026-06-23)

All 14 admin routes inspected against reference design, backend capability, wrapper usage, and stale/misleading UI. Tiny safe fixes applied: tab bar `blue-600`→`indigo-600` on notifications page; `#4338CA`/`#465fff` CSS literals replaced with `var(--sp-admin-primary,#5B4BE8)` token in dashboard, careers, student-detail (x2), students, and usage-policies components. Four planning matrices produced: page matrix (A), wrapper misuse matrix (B), stale UI matrix (C), and recommended phases (D). Next phases: 10UI-FIX-5 (dashboard static cards + careers orphan), 10UI-FIX-6 (student list + create-student + curriculum wrapper migration), 10UI-FIX-7 (student detail full wrapper migration). 1035/1035 tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-4-admin-page-level-spot-check.md

---

**Phase 10UI-FIX-3 — Admin Core Component Visual Alignment** — complete (2026-06-23)

All reusable `sp-admin-*` component CSS aligned to SpeakPath indigo token palette. Replaced TailAdmin-blue hardcoded literals (`#465fff`, `#ecf3ff`, `#93c5fd`, `#e5e7eb`, `#f9fafb`, `#111827`, `#6b7280`, `#374151`) with `var(--sp-admin-*)` tokens throughout: card, button, badge, stat-card, empty-state, table, input, select, pagination, slide-over, modal, textarea, number-input, checkbox, drawer, copyable-text, code-pill, alert, kpi-card, section-card, section-header, table-actions, status-card. Dark-mode panel backgrounds (`#111827`, `#1f2937`) preserved as intentional. 1035/1035 tests pass. Production build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-3-admin-core-component-visual-alignment.md

---

**Phase 10UI-FIX-2 — Admin Shell, Sidebar, and Header Visual Alignment** — complete (2026-06-23)

Shell/sidebar/header aligned to SpeakPath reference design. Brand palette shifted to `#5B4BE8` family. Sidebar narrowed 290→240px / 90→64px collapsed. Header height fixed to 60px. Border color to lavender `#ECE9F5`. Background to warm `#F6F4FB`. Active nav indicator bar (left stripe) added. Nav item replaced TailAdmin `menu-item` utilities with `sp-nav-*` SpeakPath classes. Section labels tightened. All 12 required nav routes confirmed present in both desktop sidebar and mobile drawer. Logout confirmed absent from sidebar. 1035/1035 Angular tests pass.

Review: docs/reviews/2026-06-23-phase-10ui-fix-2-admin-shell-sidebar-header-alignment.md

---

**Phase 10UI-FIX-1 — Admin Navigation P0 Fix and Design Reference Registration** — complete (2026-06-23)

Added sidebar nav links for `/admin/usage-policies` (Usage Policies) and `/admin/curriculum` (Curriculum) to both desktop sidebar and mobile drawer in `admin-app-layout.component.html`. Registered SpeakPath reference design pack as source-of-truth in `docs/design/admin-reference-alignment.md`. Production build passes. TODOS updated (TODO-UI-01 closed, TODO-UI-01B + design reference TODOs added).

---

**Phase 10UI-AUDIT-0 — Full UI / Backend Capability Reconciliation** — complete (2026-06-23)

Audit-only phase. No code changed. Route-by-route and capability-by-capability reconciliation across all admin and student routes against backend capabilities.

Key findings:
- P0: `/admin/usage-policies` and `/admin/curriculum` are complete production pages with NO sidebar nav links. Admins cannot find them without typing the URL directly.
- P1: Student detail page missing readiness pool health, activity history section, and onboarding flow viewer.
- P1: Orphan `AdminUsageComponent` (admin-usage folder) renders stale placeholder — dead code contradicting the real AI usage page.
- P1: Admin dashboard "AI provider: Configured" stat card is always static.
- P2: SMS shown in notification config without "foundation only" label. CEFR not shown on student progress page. No Google login button on /login.

Next recommended phase: **10UI-FIX-1** — add three missing nav links (single HTML file, 1-2 hours).

Review: docs/reviews/2026-06-23-phase-10ui-audit-0-ui-backend-capability-reconciliation.md
TODOs added: TODO-UI-01 through TODO-UI-10 in TODOS.md.
Gates: no code changed.

---

**Phase 10Auth-F-FINAL — Enterprise Auth/Security Closure Audit** — complete (2026-06-23)

Audit-only phase. All 6 implementation phases (F-1 through F-6) verified closed. 23 auth event types confirmed. All security invariants confirmed: no secrets/tokens in audit metadata, logs, or API responses. Refresh tokens hash-only with rotation and reuse detection. Session revocation wired into password change, reset, and logout. Deferred items documented (CSP, HSTS, distributed rate limiting, MFA, SMS, cloud KMS). Next recommended phase: 10UI-AUDIT-0.

Gates: 2369/2369 .NET | 1025/1025 Angular | build clean.
Review: docs/reviews/2026-06-23-phase-10auth-f-final-enterprise-auth-security-closure-audit.md

---

**Phase 10Auth-F-6 — Admin Security Settings UI and Read-Only Auth Visibility** — complete (2026-06-23)

`GET /api/admin/security/settings` read-only endpoint (Admin-role only) returning password policy, lockout, rate limit summary, JWT/refresh token config, security headers status, Google external login config — no secrets returned. `GET /api/admin/security/auth-events` aliasing the existing F-2 auth event handler. Angular admin page at `/admin/security` with Overview and Auth Events tabs, using sp-admin-* wrapper components. Security nav item added to admin sidebar (System section). No new migrations. 16 new frontend unit tests.

Review: docs/reviews/2026-06-23-phase-10auth-f-6-admin-security-settings-ui.md

---

**Phase 10Auth-F-5 — Google OAuth / External Login Foundation** — complete (2026-06-23)

`IGoogleTokenValidator` abstraction (testable without real Google API calls), `IExternalLoginService`, `ExternalLoginService` (link/provision/issue JWT + refresh tokens). `GoogleExternalLoginOptions` bound from `Authentication:ExternalProviders:Google` (disabled by default, no public auto-provisioning). Endpoint: `POST /api/auth/external/google`, rate-limited `AuthExternalLogin` (20/5min/IP). Account linking via Identity `AspNetUserLogins` — no new migration. Auto-link by email when `AllowAutoLinkByEmail=true`. Domain restriction via `AllowedDomains`. 7 new audit event types. `account.external_login_linked` notification templates (InApp+Email). 20 new integration tests with `FakeGoogleTokenValidator`. 2369/2369 pass.

Review: docs/reviews/2026-06-23-phase-10auth-f-5-google-oauth-external-login-foundation.md

---

**Phase 10Auth-F-4 — Refresh Tokens and Session Management** — complete (2026-06-23)

`UserRefreshToken` entity, migration T59, `IRefreshTokenService` (hash-only storage, rotation, reuse detection, revoke-all). Login issues refresh token. Password change/reset revokes all sessions. Endpoints: `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/revoke-sessions`. `AuthRefresh` rate limiter (30/5min/IP). 6 new audit event types. 20 new integration tests. 2349/2349 pass. No frontend changes.

Review: docs/reviews/2026-06-23-phase-10auth-f-4-refresh-tokens-session-management.md

---

**Phase 10Auth-F-3 — Security Notifications** — complete (2026-06-23)

Security notifications wired into all auth flows. Templates added: `account.password_changed` (InApp+Email), `account.password_reset_requested` (InApp only — reset-link email serves the email role), `account.password_reset_succeeded` (InApp+Email), `account.locked_out` (InApp+Email). All use `NotificationCategory.Account` (mandatory, bypasses opt-out). Lockout notification fires only on transition — not on every locked-out login attempt. Notification failures are non-fatal. No new migration. 11 new integration tests. 2330/2330 pass.

Review: docs/reviews/2026-06-23-phase-10auth-f-3-security-notifications.md

---

**Phase 10Auth-F-2 — Auth Event Audit Log** — complete (2026-06-23)

`AuthSecurityEvent` entity, migration T58, `IAuthSecurityAuditService` (non-fatal, never logs secrets), audit integration in LoginHandler/ChangePasswordHandler/PasswordResetHandler/CreateStudentHandler. `GET /api/admin/auth-events` (paginated, filterable by userId, email, eventType, outcome, date range). Rate limiter split: `AuthReset` (3/15min, unauthenticated reset-link) and `AuthChangePassword` (10/5min, authenticated, keyed on userId). 16 new integration tests. 2319/2319 pass.

Review: docs/reviews/2026-06-23-phase-10auth-f-2-auth-event-audit-log.md

---

**Phase 10Auth-F-1 — Immediate Auth/Security Hardening** — complete (2026-06-23)

Lockout (5 attempts / 15 min), IP rate limiting on login (10 req/5 min) and reset/change-password (3 req/15 min), password policy hardened (10 chars, upper+lower+digit+special), `SecurityHeadersMiddleware` (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy). `LoginHandler` updated to use `AccessFailedAsync`/`IsLockedOutAsync`/`ResetAccessFailedCountAsync`. 13 new auth security integration tests. 2304/2304 tests pass. No migrations, no UI changes.

Review: docs/reviews/2026-06-23-phase-10auth-f-1-immediate-auth-security-hardening.md

---

**Phase 10Auth-F-0 — Enterprise Auth/Security Gap Check** — complete (2026-06-23)

Audit of authentication, authorization, password, session, token, reset-password, and account-security implementation. No code changes. Roadmap defined for 10Auth-F-1 through 10Auth-F-FINAL. Critical gap: no brute-force/lockout protection. Next phase: 10Auth-F-1 (lockout + rate limiting + password policy hardening, ~1 day, no migration).

Review: docs/reviews/2026-06-23-phase-10auth-f-0-enterprise-auth-security-gap-check.md

---

**Phase 10W-FINAL-2 — Notification Platform Re-closure Audit** — complete (2026-06-23)

Re-audit after 10W-5C-2 through 10W-5C-5 (DB config, secret encryption, key persistence, key-at-rest cert protection). One stale comment corrected in `AdminNotificationHandler.cs`. All 24 audit checks pass. 2291 .NET / 1004 Angular tests pass. Platform is production-ready for in-app and email on single-host Docker.

Review: docs/reviews/2026-06-23-phase-10w-final-2-notification-platform-reclosure-audit.md

---

**Phase 10W-5C-5 — Data Protection Key-at-Rest Certificate Protection** — complete (2026-06-23)

`DataProtectionKeyMode` enum (None/Certificate). `ProtectKeysWithCertificate` when mode=Certificate. PFX file via `X509CertificateLoader` or Windows store thumbprint. Fail-fast on misconfiguration. `.gitignore` updated for `*.pfx`/`*.p12`. 10 new unit tests. 2291 .NET / 1004 Angular pass.

Review: docs/reviews/2026-06-23-phase-10w-5c-5-data-protection-key-encryption-hardening-review.md

---

**Phase 10W-5C-4 — Data Protection Key Persistence** — complete (2026-06-23)

`NotificationKeyProtectionOptions` bound from `DataProtection` appsettings section. `PersistKeysToFileSystem` at DI registration time. Directory auto-created; degrades gracefully on error. Docker `dp_keys` named volume. `.gitignore` updated. 5 new unit tests. 2281 .NET / 1004 Angular pass.

Review: docs/reviews/2026-06-23-phase-10w-5c-4-data-protection-key-persistence-review.md

---

**Phase 10W-5C-3 — Runtime Notification Config Resolver + Secret Encryption** — complete (2026-06-23)

`ISecretProtector`/`DataProtectionSecretProtector` (ASP.NET Core Data Protection). `INotificationChannelConfigResolver`/`NotificationChannelConfigResolver` (DB wins over appsettings). `SmtpEmailSender` decoupled from `IOptions<EmailOptions>` — resolves config at send time. `TestEmailAsync` uses resolver. Base64 fallback on unprotect for backward compat. 7 new unit tests. 2276 .NET / 1004 Angular pass.

Review: docs/reviews/2026-06-23-phase-10w-5c-3-runtime-config-resolver-secret-encryption-review.md

---

**Phase 10W-5C-2 — DB-Backed Notification Channel Configuration** — complete (2026-06-23)

`NotificationChannelConfig` entity + migration `T57`. Hybrid config resolution (DB wins, appsettings fallback). `GET notifications/config` returns V2 with `source` field. `PUT /email`, `/sms`, `/in-app` endpoints. Secrets write-only (hasPassword/hasApiKey booleans only in API). Admin UI editable forms, source badge, secret replace-only UX. 25 backend integration + 9 frontend tests. 978 .NET integration / 1004 Angular pass.

Review: docs/reviews/2026-06-22-phase-10w-5c-2-db-backed-notification-channel-configuration-review.md

---

**Phase 10W-FINAL — Notification Platform Closure Audit** — complete (2026-06-22)

Full audit of the 10W notification platform. All 14 sub-phases verified closed. 2246 .NET + 1011 Angular tests pass. No bugs found. Docs updated. Deferred TODOs: TODO-10W-5D-UNIQUE-CONSTRAINT, TODO-10W-PHONE, TODO-10W-SMS-PROVIDER.

Review: docs/reviews/2026-06-22-phase-10w-final-notification-platform-closure-audit.md

---

**Phase 10W-6 — SMS Provider Foundation** — complete (2026-06-22)

`ISmsSender` / `SmsMessage` / `SmsSendResult` abstraction. `DisabledSmsSender` always skips safely. `SmsOptions` config class (ApiKey never returned to frontend, `HasApiKey` bool only). `AdminSmsConfigStatus` DTO. `NotificationDispatchService` wired with `ISmsSender`. Admin config tab shows SMS detail card.

Review: docs/reviews/2026-06-22-phase-10w-6-sms-provider-foundation-review.md

---

**Phase 10W-PREFS — Notification Preferences Foundation** — complete (2026-06-22)

`NotificationPreference` entity + migration T56. `INotificationPreferenceService`. Account/System categories required (cannot be disabled). SMS deferred (always false). User GET/PUT API. Admin read API. Profile section with category×channel table, SMS "Coming soon", required badge, save. 12 backend + 11 frontend tests.

Review: docs/reviews/2026-06-22-phase-10w-prefs-notification-preferences-foundation-review.md

---

**Phase 10W-5D-RESET-INTEGRATION — System emails use templates** — complete (2026-06-22)

`PasswordResetHandler` and `CreateStudentHandler` use `account.password_reset` / `account.student_created` Email templates. Safe fallback to hard-coded content when template missing/inactive. Token never in metadata/audit/log/API response.

Review: docs/reviews/2026-06-22-phase-10w-5d-reset-template-integration-review.md

---

**Phase 10W-5D — Notification Templates Foundation** — complete (2026-06-22)

`NotificationTemplate` entity + migration T55. `INotificationTemplateRenderer` (`{{VarName}}` replacement). `IAdminTemplateHandler` CRUD + preview. 4 default templates seeded. Admin Templates tab: list, create/edit slide-over, preview panel.

Review: docs/reviews/2026-06-22-phase-10w-5d-notification-templates-foundation-review.md

---

**Phase 10W-4C — Notification Dropdown Source Control Fix** - complete (2026-06-21)

Goal: move live notification bell/dropdown from gitignored vendor template path into committed app source so it survives a fresh clone.

### Delivered

- `NotificationDropdownComponent` at `src/app/design-system/student/notification-dropdown/` (committed, selector `sp-notification-dropdown`).
- Full 10W-3 behavior: live list, unread count, mark read, mark all read, archive/dismiss, loading/empty/error/retry states, deep-link navigation.
- Click-outside via `@HostListener` — no dependency on gitignored `DropdownComponent`.
- `StudentAppLayoutComponent` updated: imports + uses `<sp-notification-dropdown>` replacing static bell button.
- 17 new frontend unit tests. Gates: `npm run build` clean, 942/942 Angular tests pass.
- Gitignored `src/app/templates/` no longer depended on by any committed code.

---

**Phase 10W-4B — Token-Based Password Reset Link** - complete (2026-06-21)

Goal: admin triggers a password reset link email to a student. Student clicks link and sets new password via a public Angular page. Token never returned to admin, never stored in logs or metadata.

### Delivered

- `IPasswordResetService` + `SendPasswordResetLinkCommand` + `CompletePasswordResetCommand` + `CompletePasswordResetResult` in `LinguaCoach.Application/Auth/`.
- `PasswordResetHandler` in `LinguaCoach.Infrastructure/Auth/`: generates ASP.NET Identity token, Base64Url-encodes it, builds reset link with `PublicApp:BaseUrl`, queues email outbox item. Token never logged. Generic error returned on failure (no info leak).
- `POST /api/admin/students/{id}/send-reset-link` (admin auth required). Returns 204.
- `POST /api/auth/reset-password` (public, no auth). Validates passwords match, calls `CompleteResetAsync`, clears `MustChangePassword` on success. Generic errors on all failure paths.
- `PublicApp:BaseUrl` added to `appsettings.json` (default `http://localhost:4200`).
- DI: `services.AddScoped<IPasswordResetService, PasswordResetHandler>()`.
- Frontend: `ResetPasswordComponent` at `/reset-password` — reads `userId`+`token` from query params, validates, calls `auth.resetPassword()`. Success/error states with signals.
- Admin: `sendResetLink(student)` method + `sendingResetLink`/`resetLinkSent` signals in `AdminStudentDetailComponent`. "Send reset link" button in student detail header actions.
- `AdminApiService.sendStudentResetLink()` added.
- `AuthService.resetPassword()` + `ResetPasswordRequest` model added.
- Frontend specs: `reset-password.component.spec.ts` (7 tests), `admin-student-detail` describe block (2 tests).
- Backend integration tests: `PasswordResetEndpointTests.cs` (8 tests — auth gates, outbox queued, body contains link, metadata safety, generic errors).
- Gates: `dotnet build Release` clean, `dotnet test Release` 2159/2159 pass, `npm run build --configuration production` clean, `npm test` 925/925 pass.

### Security invariants

- Token not logged, not in notification metadata, not returned to admin.
- Public endpoint returns identical generic error for user-not-found, bad token, and expired token.
- Raw password never emailed or stored.
- Admin endpoint requires admin role JWT.

---

**Phase 10W-4 — Email Provider + Reset Password Wiring + Dispatch Job** - complete (2026-06-21)

Goal: add email delivery foundation and wire password reset / student creation emails through the notification/outbox pipeline. Add Quartz dispatch job. Keep SMS deferred.

### Delivered

- `IEmailSender` + `EmailMessage` + `EmailSendResult` in Application layer.
- `EmailOptions` (bound from `"Email"` config section; `Enabled=false` by default — no startup crash if unconfigured).
- `DisabledEmailSender` (safe no-op; used when email not configured).
- `SmtpEmailSender` (via `System.Net.Mail`; returns Skipped if disabled/no-host; catches all SMTP exceptions — never throws).
- `IEmailSender` registered in DI: SmtpEmailSender if `Enabled && Host` set, else DisabledEmailSender.
- `NotificationDispatchService` extended: Email outbox items resolved via `UserManager`, sent via `IEmailSender`. Success → Delivered. Skipped → counted as skipped. Failed → counted as failed. SMS still skipped.
- `NotificationDispatchJob` (Quartz, `[DisallowConcurrentExecution]`, every 2 minutes, persisted).
- `AdminHandler.ResetStudentPasswordAsync` queues email notification after reset — body does NOT include raw password.
- `CreateStudentHandler.HandleAsync` queues welcome email after student creation — body does NOT include temp password.
- Both operations tolerate notification queue failure (log warning, continue).
- Appsettings `"Email"` section with safe disabled defaults.
- 11 unit tests + 11 integration/dispatch/job tests (+20 new); existing dispatch tests updated for new constructor.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2150/2150 — 3 arch + 1287 unit + 860 integration; +20)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (916/916)

No migration added. No frontend changes.

See: `docs/reviews/2026-06-21-phase-10w-4-email-provider-reset-password-dispatch-job-review.md`

---

## Previous sprint

**Phase 10W-3 — Live Notification Bell UI Wiring** - complete (2026-06-21)

Goal: replace hard-coded demo notification dropdown with live Angular service + signals-based component wired to the Phase 10W-2 notification APIs.

### Delivered

- `NotificationService` (`core/services/notification.service.ts`): wraps all 5 backend endpoints (`list`, `getUnreadCount`, `markRead`, `markAllRead`, `archive`). Exports `NotificationItem`, `NotificationListResponse`, `UnreadCountResponse`.
- `NotificationDropdownComponent` rewritten: signals-based state (`notifications`, `unreadCount`, `loading`, `error`, `hasUnread`). Loading/error/empty/list states via `@if`/`@else if`/`@for`. Unread badge on bell. Mark-all-read. Per-item archive. Deep-link navigation.
- Demo data (Terry Franci, Nganter App etc.) fully removed from template.
- 6 service spec tests + 16 component spec tests.
- Two bugs fixed: Angular template parse error from `[class.dark:bg-blue-900\/10]` binding; ChunkLoadError from dynamic `import('rxjs')` in spec.

### Gates

- `git diff --check`: PASS
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (916/916)

See: `docs/reviews/2026-06-21-phase-10w-3-live-notification-bell-ui-wiring-review.md`

---

## Previous sprint

**Phase 10W-2 — In-App Notification APIs + Dispatch Foundation** - complete (2026-06-21)

Goal: authenticated current-user notification APIs, paged listing with filters, mark-read/read-all/archive, and outbox dispatch service (InApp delivered; Email/SMS safely skipped).

### Delivered

- `NotificationDto`, `PagedNotificationResult`, `NotificationListQuery` in Application layer.
- `INotificationQueryService` / `NotificationQueryService`: list (paged, filtered, expires-excluded, archived-excluded), unread-count, mark-read, mark-all-read, archive. Current-user isolation enforced.
- `INotificationDispatchService` / `NotificationDispatchService`: processes due outbox items in batches. InApp → delivered. Email/SMS → skipped with error (no provider yet).
- `NotificationsController`: 5 endpoints — `GET /api/notifications`, `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`, `POST /api/notifications/{id}/archive`.
- 16 API integration tests + 9 dispatch tests (+25 total).
- No migration. No frontend changes. No external email/SMS delivery.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2131/2131 — 3 arch + 1278 unit + 850 integration; +23)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (896/896)

See: `docs/reviews/2026-06-21-phase-10w-2-in-app-notification-apis-dispatch-foundation-review.md`

---

## Previous sprint

**Phase 10W-1 — Backend Notification Foundation** - complete (2026-06-21)

Goal: domain entities, EF persistence, Application service abstraction, Infrastructure implementation, migration, DI registration, and full test coverage for the notification platform foundation.

### Delivered

- 4 new domain enums: `NotificationChannel`, `NotificationStatus`, `NotificationSeverity`, `NotificationCategory`.
- 2 new domain entities: `Notification` (factory method, state transitions), `NotificationOutboxItem` (attempt tracking, retry backoff).
- `INotificationService` interface in `LinguaCoach.Application.Notifications` with `QueueAsync`, `QueueInAppAsync`, `QueueEmailAsync`, `QueueSmsAsync`.
- `NotificationService` implementation in `LinguaCoach.Infrastructure.Notifications`.
- EF configurations: `NotificationConfiguration`, `NotificationOutboxItemConfiguration`. Enums stored as strings.
- `LinguaCoachDbContext` extended with `Notifications` and `NotificationOutboxItems` DbSets.
- Migration `T54_NotificationFoundation`: tables `notifications`, `notification_outbox_items`, 6 indexes.
- `INotificationService` registered as scoped in `DependencyInjection`.
- 20 unit tests (`NotificationEntityTests`) + 13 integration tests (`NotificationServiceTests`).
- No external email/SMS delivery. No API endpoints. No frontend changes. No dispatch worker.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2108/2108 — 3 arch + 1278 unit + 827 integration; +28)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (896/896)

See: `docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md`

---

## Previous sprint

**Phase 10W-0 — Enterprise Notification Platform Gap Check** - complete (2026-06-21)

Goal: audit existing notification/email/SMS/in-app capability and define the 10W roadmap.

### Delivered

- Full gap check across all notification-related areas: email, in-app, SMS, event producers, data model, enterprise requirements.
- Review saved: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`
- TODOS.md updated with `TODO-10W-1` through `TODO-10W-FINAL` entries.
- No code changes. Audit only.

### Findings summary

- Email: not implemented. No IEmailSender, no SMTP config, no reset-password email flow.
- In-app: UI placeholder only. `NotificationDropdownComponent` has hard-coded demo items, no backend.
- SMS: not implemented.
- Only existing delivery: transient `ToastService` (auto-dismiss, no persistence).
- All job failures and AI alerts are logged via `ILogger` only — no user delivery.
- Zero entity/table for persistent notifications.

### Gates

- No code changed. `git diff --check`: not required.
- Audit-only phase.

See: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`

---

## Previous sprint

**Phase 10V-3B — AI Pricing Zero-Cost Alert UI** - complete (2026-06-21)

Goal: admin visibility for missing-pricing / zero-cost AI calls in the filtered AI Usage summary.

### Delivered

- `AiUsageSummaryDto` extended with `ZeroCostCallCount` and `ZeroCostTotalTokens`.
- `AiUsageHandler.GetSummaryAsync` computes zero-cost rows: `CostUsd == 0 AND (InputTokens + OutputTokens) > 0`. Respects all active filters (date, provider, model, featureKey, status, studentId).
- `AiUsageController` summary endpoint exposes `zeroCostCallCount` and `zeroCostTotalTokens` in JSON.
- `AiUsageSummary` TypeScript interface extended with both fields.
- AI Usage page template: `sp-admin-alert variant="warning"` shown when `zeroCostCallCount > 0`. Alert includes call count, token total, and explanation. Disappears when count is 0. Updates on every filter/date reload.
- No migration. No pricing calculation change. No provider routing change. No usage governance change. No historical recalculation.
- Backend: +5 integration tests (`AiUsageSummaryFilterTests`), +2 unit tests (`AiUsageSummaryTests`).
- Frontend: +5 Karma tests (`admin-ai-usage.component.spec.ts`). `makeSummary` factory updated with zero-cost defaults.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2080/2080 — 3 arch + 1262 unit + 815 integration)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (896/896)

See: `docs/reviews/2026-06-21-phase-10v-3b-ai-pricing-zero-cost-alert-ui-review.md`

---

## Previous sprint

**Phase 10U-FINAL — AI Usage / AI Config Closure Audit** - complete (2026-06-21)

Goal: final validation and documentation pass for the full 10U work. No new features. Small fixes and doc updates only.

### Audit findings

- Backend: no bugs found. All four endpoints (summary, recent, trends, export.csv) correctly apply date filter + column filters. `ApplyDateFilter` (from ≥, to <, UTC) and `ApplyColumnFilter` (provider, model, featureKey, status, studentId) are shared and consistent across all handlers.
- Frontend: no bugs found. `buildRange('custom')` correctly converts `yyyy-MM-dd` to UTC exclusive upper bound. `clearRecentFilters` correctly preserves custom date range. `applyCustomRange` guards both required + from ≤ to before calling API.
- Pricing: `AiPricingOptions` reads `{Provider}:Pricing:{ModelName}:InputPer1KTokens/OutputPer1KTokens` from config — matches `appsettings.json` exactly. No hardcoded pricing in production C#.
- Playwright: no AI Usage Playwright tests exist — no Playwright run.
- TODOS.md: `TODO-10U` marked done; `TODO-022` marked done with note; `TODO-10U-GAP-6` stale reference to "10U-8 pricing admin" corrected.
- `docs/handoffs/current-product-state.md`: updated to 10U-FINAL with full feature summary.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2041/2041 — 3 arch + 1248 unit + 790 integration)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (872/872)

See: `docs/reviews/2026-06-21-phase-10u-final-ai-usage-config-closure-audit.md`

---

## Previous sprint

**Phase 10U-10 — AI Usage Custom Date Range Picker** - complete (2026-06-21)

Goal: add Custom range period option to AI Usage admin page so admins can inspect any chosen date window.

### Delivered

- `PeriodPreset` extended with `'custom'`.
- `SpAdminInputComponent` added to component imports.
- `customFrom`, `customTo`, `customRangeError` signals; `customFromValue`/`customToValue` two-way-bind mirrors.
- `periodOptions` extended with `{ value: 'custom', label: 'Custom range' }`.
- `onPeriodChange('custom')` defers reload until Apply; switching away clears error and reloads with preset.
- `applyCustomRange()`: validates both required, from <= to; calls `load()` on valid input; resets page to 1.
- `clearCustomRange()`: resets all custom date state.
- `buildRange('custom')`: from = `T00:00:00Z`, to = start of day+1 (exclusive, consistent with 10U-3).
- Template: From/To `sp-admin-input type="date"` + Apply/Clear dates buttons shown when `periodPreset() === 'custom'`; `sp-admin-alert` for validation error.
- No backend changes. No migration. No provider routing change. No usage governance change.
- Frontend: +13 new tests. 872/872 pass. Production build clean.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: not run (no backend changes)
- `dotnet test --configuration Release`: not run (no backend changes)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (872/872)

See: `docs/reviews/2026-06-21-phase-10u-10-ai-usage-custom-date-range-picker-review.md`

---

## Previous sprint

**Phase 10U-9 — AI Usage Trend Summary** - complete (2026-06-21)

Goal: lightweight daily trend table on the AI Usage admin page, reusing all existing filters and date period presets.

### Delivered

- `AiUsageTrendBucket` DTO record in `LinguaCoach.Application/Admin/AiUsageQueries.cs`.
- `IAdminAiUsageHandler.GetTrendsAsync(dateFilter, columnFilter)` — new interface method.
- `AiUsageHandler.GetTrendsAsync`: client-side grouping by `DateOnly.FromDateTime(l.CreatedAt)`; reuses `ApplyDateFilter` + `ApplyColumnFilter`; zero-fills missing dates within `From→To` range when >= 2 real buckets exist.
- `GET /api/admin/ai-usage/trends`: same filter params and 400 validation as `/summary`, `/recent`, `/export.csv`. Returns `date (yyyy-MM-dd)`, `callCount`, `successCount`, `failureCount`, `fallbackCount`, `inputTokens`, `outputTokens`, `totalTokens`, `costUsd`.
- `AiUsageTrendBucket` interface + `getTrends(range?, filters?)` in `AiUsageService`.
- Component: `trendBuckets`, `loadingTrends`, `trendError` signals; `loadTrends()` called from `load()` on every filter/period change and clear.
- Template: "Usage trend" `sp-admin-card` with loading/error/empty/data states; zero-count rows muted via `.sp-au-trend-zero`.
- Backend: +15 integration tests (`AiUsageTrendTests`). 790/790 integration.
- Frontend: +11 component tests (loading/error/empty/data/filter-reload/clear/period). 858/858 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2041/2041)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (858/858)

See: `docs/reviews/2026-06-21-phase-10u-9-ai-usage-trend-summary-review.md`

---

## Previous sprint

**Phase 10U-8 — AI Usage CSV Export** - complete (2026-06-21)

Goal: CSV export of AI usage recent calls using the currently active filters, for admin reporting and reconciliation.

### Delivered

- `IAdminAiUsageHandler.GetExportAsync(dateFilter, columnFilter, maxRows)` — new method.
- `AiUsageHandler.GetExportAsync`: reuses `ApplyDateFilter` + `ApplyColumnFilter`; `Take(10_000)` cap; newest-first sort.
- `GET /api/admin/ai-usage/export.csv`: same filter params as `/summary` and `/recent`; same 400 validation. `BuildCsv` + `CsvEscape` helpers (RFC 4180). `text/csv` + `Content-Disposition: attachment; filename=ai-usage-{timestamp}.csv`.
- CSV columns: `CreatedAt, Provider, Model, FeatureKey, StudentId, WasSuccessful, IsFallback, FailureReason, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, CorrelationId`. No PII beyond opaque StudentId GUID.
- `AiUsageService.exportUsageCsv(range?, filters?)` returns `Observable<Blob>`. No page/pageSize.
- Component: `exporting` + `exportError` signals; `exportCsv()` triggers browser download via `URL.createObjectURL` + anchor click (no `file-saver` dep); `SpAdminAlertComponent` shows error on failure.
- Template: "Export CSV" / "Exporting…" button in filter bar; error alert above recent calls card.
- Backend: +16 integration tests (`AiUsageExportTests`). 775/775 integration.
- Frontend: +6 component tests. 847/847 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2026/2026)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (847/847)

See: `docs/reviews/2026-06-20-phase-10u-8-ai-usage-csv-export-review.md`

---

## Previous sprint

**Phase 10U-7 — AI Usage Summary Filter Alignment** - complete (2026-06-20)

Goal: make summary cards and breakdowns respect the same column filters as recent calls (provider, model, featureKey, status, studentId, date range).

### Delivered

- `IAdminAiUsageHandler.GetSummaryAsync` accepts `AiUsageRecentFilter? columnFilter` (new second param).
- `ApplyRecentFilter` renamed `ApplyColumnFilter` — shared by both `GetSummaryAsync` and `GetRecentAsync`.
- `/summary` endpoint: added `provider`, `model`, `featureKey`, `status`, `studentId` query params; same validation (400 on invalid status, 400 on non-GUID studentId) as `/recent`.
- `AiUsageService.getSummary(range?, filters?)` — forwards column filters to backend.
- `buildColumnFilters()` private helper on component — reads filter signals once, used by both `load()` and `loadRecent()`.
- All filter change handlers now call `load()` — reloads summary + recent together on every column filter change.
- `clearRecentFilters()` calls `load()` — keeps date period unchanged.
- Template: helper text "Summary totals reflect active filters." shown when any column filter is active.
- Backend: +14 integration tests (`AiUsageSummaryFilterTests`). 759/759 integration.
- Frontend: +10 new component tests; 3 existing updated for new `getSummary` 2-arg signature. 841/841 pass.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2010/2010)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (841/841)

See: `docs/reviews/2026-06-20-phase-10u-7-ai-usage-summary-filter-alignment-review.md`

---

## Previous sprint

**Phase 10U-6 — AI Usage Student Filter** - complete (2026-06-20)

Goal: server-side `studentId` filter on the AI Usage recent calls endpoint; student select in the filter bar loaded from `adminApi.listStudents`.

### Delivered

- `AiUsageRecentFilter` record: added `StudentId` (nullable Guid).
- `AiUsageHandler.ApplyRecentFilter`: LINQ `Where(l => l.StudentProfileId == filter.StudentId.Value)` clause.
- `/recent` endpoint: `studentId` query param; 400 on non-GUID; unknown GUID returns empty paged result.
- `AiUsageRecentCallFilter` Angular interface: added `studentId?: string`.
- `AdminAiUsageComponent`: `studentOptions` signal loaded from `adminApi.listStudents({ pageSize: 50 })` on init; `onRecentStudentChange`; `clearRecentFilters` includes student; `hasActiveRecentFilters` includes student.
- Template: student select rendered conditionally on `studentOptions().length > 0`.
- Backend: +8 integration tests (`AiUsageStudentFilterTests`). Real student profile rows created via `CreateStudentAndGetTokenAsync` + DB join to resolve `StudentProfile.Id`. 745/745 integration + 1248/1248 unit + 3/3 arch.
- Frontend: +8 component tests. `admin-wrapper-migration.spec.ts` AI Usage test updated with `AdminApiService` mock. 831/831 pass.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1996/1996)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (831/831)

See: `docs/reviews/2026-06-20-phase-10u-6-ai-usage-student-filter-review.md`

---

## Previous sprint

**Phase 10U-5 — AI Usage Recent Calls Filters** - complete (2026-06-20)

Goal: server-side provider/model/featureKey/status filters for the AI Usage recent calls table.

### Delivered

- `AiUsageRecentFilter` record in Application layer with `HasInvalidStatus` guard.
- `IAdminAiUsageHandler.GetRecentAsync` updated to accept `AiUsageRecentFilter`.
- `AiUsageHandler.ApplyRecentFilter`: filters applied after date filter, before count/skip/take.
- `/recent` endpoint: `provider`, `model`, `featureKey`, `status` query params; 400 on invalid status.
- Status semantics: `success` = WasSuccessful && !IsFallback; `failed` = !WasSuccessful; `fallback` = IsFallback.
- `AiUsageRecentCallFilter` interface in Angular service; `getRecent` 4th param.
- Four-filter bar above recent calls table: provider, model, feature, status selects. "Clear filters" button when any filter active.
- Filter option sources: provider from summary.byProvider + items; model from items; feature from summary.byFeature + items.
- `clearRecentFilters` clears column filters only; does not reset date period.
- Pagination preserves active filters on page change.
- Summary totals unchanged — not affected by column filters.
- Backend: +12 integration tests (`AiUsageRecentFilterTests`). 1988/1988 pass.
- Frontend: +10 component tests (12 new, 3 replaced). 823/823 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1988/1988)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (823/823)

See: `docs/reviews/2026-06-20-phase-10u-5-ai-usage-recent-calls-filters-review.md`

---

## Previous sprint

**Phase 10U-4 — AI Usage Recent Calls Pagination** - complete (2026-06-20)

Goal: server-side pagination for the AI Usage recent calls/history list to handle enterprise-scale logs.

### Delivered

- `AiUsagePagedResult` record in Application layer: `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`.
- `IAdminAiUsageHandler.GetRecentAsync(page, pageSize, filter)`: replaced old `limit` param.
- `AiUsageHandler`: `CountAsync` + `Skip`/`Take` with page clamp (never exceeds totalPages) and pageSize clamp (1–100).
- `AiUsageController /recent`: replaced `limit` with `page`/`pageSize`; response envelope is `{ items, totalCount, page, pageSize, totalPages }`.
- Angular `AiUsageRecentResponse` interface updated to paged shape.
- `AiUsageService.getRecent(page, pageSize, range?)` signature.
- Component: `loadRecent()` extracted; `onRecentPageChange()` added; `recentTotalCount`/`recentTotalPages` signals driven by server response; client-side slice removed.
- Template: iterates `filteredRecentItems()` (current page); pagination wired to `onRecentPageChange`.
- Summary totals unaffected — `GetSummaryAsync` unchanged.
- Date filter + pagination compose correctly — filter applied before count/skip/take.
- Backend: +10 integration tests (`AiUsagePaginationTests`). 1977/1977 pass.
- Frontend: +8 component tests. 813/813 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1977/1977)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (813/813)

See: `docs/reviews/2026-06-20-phase-10u-4-ai-usage-recent-calls-pagination-review.md`

---

## Previous sprint

**Phase 10U-3 — AI Usage Date Filtering** - complete (2026-06-20)

Goal: add `from`/`to` UTC date-range filtering to the AI usage summary and recent endpoints; add period preset select (All time, Today, Last 7 days, Last 30 days, This month) to the Angular admin usage page.

### Delivered

- `AiUsageDateFilter` record in Application layer: `From` (inclusive `>=`), `To` (exclusive `<`), `IsInverted` guard.
- `IAdminAiUsageHandler` interface: both methods accept optional `AiUsageDateFilter`.
- `AiUsageHandler`: `ApplyDateFilter` helper wires filter into EF Core LINQ query.
- `AiUsageController`: `from`/`to` query params on both `/summary` and `/recent`; returns 400 when range is invalid (From >= To).
- `AiUsageDateRange` interface in Angular service; both `getSummary` and `getRecent` forward range as HTTP query params.
- `PeriodPreset` type exported from component; `periodOptions`, `buildRange()`, `onPeriodChange()`, `load()` added to component.
- Period preset `sp-admin-select` in `sp-admin-filter-bar` above stat grid.
- Backend: +12 integration tests (`AiUsageDateFilterTests`). 1967/1967 pass.
- Frontend: +11 component tests (period preset coverage). 805/805 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1967/1967)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (805/805)

See: `docs/reviews/2026-06-20-phase-10u-3-ai-usage-date-filtering-review.md`

---

## Previous sprint

**Phase 10U-1/10U-2 — AI Pricing Config Seed + Usage Token Totals** - complete (2026-06-20)

Goal: fix silent $0 AI cost bug (missing pricing config keys) and surface token totals on /admin/usage summary cards.

### Delivered

- `appsettings.json`: Added `OpenAI:Pricing`, `Gemini:Pricing`, `Anthropic:Pricing` sections with per-model pricing for 12 models. Unblocks `AiUsageLog.CostUsd` from always being $0. Values are operational defaults — override in production via env secrets.
- `AiUsageSummaryDto`: Extended with `TotalInputTokens`, `TotalOutputTokens`, `TotalTokens` (long).
- `AiUsageHandler`: Aggregates token totals from `AiUsageLog` rows.
- `AiUsageController`: Exposes token fields in summary JSON (additive, no breaking change).
- Angular `AiUsageSummary` interface: Updated with three token fields.
- `/admin/usage` summary grid: Three new stat cards (Input tokens / Output tokens / Total tokens). Grid updated to 4-col at 900px, 8-col at 1200px.
- Backend tests: +11 (pricing Theory tests + summary DTO tests). 1955/1955 pass.
- Frontend tests: +3 (token card assertions). 794/794 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1955/1955)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (794/794)

See: `docs/reviews/2026-06-20-phase-10u-1-2-ai-pricing-config-token-totals-review.md`

---

## Previous sprint

**Phase 10Students-F-H — Student Management Final Validation** - complete (2026-06-20)

Goal: validate all enterprise student management work (10Students-F-A through 10X-L) end-to-end.

### Delivered

- All backend gates passed: 1944/1944 tests (3 arch + 1237 unit + 704 integration).
- All frontend gates passed: 791/791 Angular tests, production build clean.
- Playwright: 6/6 tests pass after mock fix.
- One issue found and fixed: E2E reset spec used old flat-array mock shape for the student list endpoint. Updated to `PagedResponse` shape (`{ items, totalCount, page, totalPages }`) matching Phase 10F breaking change. Also added detail/audit-history stubs within the same wildcard intercept.
- No backend or product contract gaps found.
- No new features added. No student-facing changes. No unrelated refactors. No commit/push.

See: `docs/reviews/2026-06-20-phase-10students-f-h-student-management-final-validation-review.md`

---

## Previous sprint

**Phase 10X-L — Shared Admin Overlay, Slide-Over, and Table Action Fixes** - complete (2026-06-20)

Goal: fix shared admin UI issues on /admin/students and /admin/students/{id} at the shared component level.

### Delivered

- `sp-admin-slide-over`: `closeOnBackdrop` default changed `true` → `false`. z-index raised from 400/401 to 1000/1001. New `stackIndex` input for stacked panels (each level adds 50 to z-index). Bindings applied via `[style.z-index]`. Escape key and accessibility already correct.
- `admin-student-detail`: Set CEFR flow converted from `.sp-admin-modal` centered div to `sp-admin-slide-over` (size=sm, stackIndex=1). Assign policy flow converted the same way. View preferences already used slide-over; confirmed intact. Edit student / Reset password / Reset data / Lifecycle modals left as-is (destructive/high-stakes; out of scope).
- `sp-admin-table-actions`: dropdown now uses `position:fixed` + `getBoundingClientRect()`. Escapes overflow parents in table containers. Flip-up logic when near viewport bottom. Closes on window scroll. Removes the vertical scroll-on-open bug.
- Specs: slide-over spec updated (closeOnBackdrop=false default, backdrop/escape tests, stacking tests). Student detail spec: new 10X-L describe block with 6 targeted tests. Table-actions spec created from scratch (18 tests).
- 791/791 tests pass. Build green. No backend change. No product behaviour changed.

See: `docs/reviews/2026-06-20-phase-10x-l-shared-admin-overlay-slide-over-table-actions-review.md`

---

**Phase 10Students-F-G — Student List Filter Select UI** - complete (2026-06-20)

Goal: add lifecycle stage, onboarding status, and CEFR level filter selects to the admin student list filter bar. Backend params were already wired in Phase 10Students-F-F.

### Delivered

- `filterLifecycleStage`, `filterOnboardingStatus`, `filterCefrLevel` signals added to `AdminStudentsComponent`.
- Three `sp-admin-select` instances inserted in the filter bar for lifecycle stage (12 options), onboarding status (4 options), and CEFR level (A1–C2).
- Each filter change resets page to 1 and calls `load()`. `load()` passes filters as `undefined` when empty.
- Clear filters button: visible when any of searchTerm/lifecycleStage/onboardingStatus/cefrLevel is set. Clears all four, resets page to 1, does not touch `includeArchived`.
- `SpAdminSelectComponent` added to component imports array.
- 7 new tests + all 25 existing tests pass (32 total). Build green.
- No backend change. No student-facing change. No bulk operations.

See: `docs/reviews/2026-06-20-phase-10students-f-g-student-list-filter-select-ui-review.md`

---

**Phase 10Students-F-F — Server-Side Student Pagination, Filtering, Sorting** - complete (2026-06-19)

Goal: make the admin student list enterprise-ready by moving all pagination, filtering, searching, and sorting to the backend. Breaking change to the list endpoint response shape.

### Delivered

- `StudentListQuery` and `PagedResponse<T>` records added to `AdminQueries.cs`.
- `ListStudentsPagedAsync(StudentListQuery)` added to `IAdminStudentQuery` interface and `AdminHandler` implementation. Applies archived filter, lifecycle/onboarding/CEFR exact-match filters, case-insensitive search across email/displayName/firstName/lastName, sorting on name/email/onboardingStatus/lifecycleStage/cefrLevel/createdAt, Skip/Take pagination. `pageSize` capped at 100.
- `GET /api/admin/students` updated to accept `page`, `pageSize`, `search`, `includeArchived`, `lifecycleStage`, `onboardingStatus`, `cefrLevel`, `sortBy`, `sortDir` query params. Returns `PagedResponse<StudentListItem>` (breaking change from `StudentListItem[]`).
- `GET /api/admin/students/{id}` (detail endpoint) unchanged.
- `StudentListQuery` and `PagedResponse<T>` TypeScript interfaces added to `admin.models.ts`.
- `AdminApiService.listStudents(query)` updated to accept `StudentListQuery` and return `Observable<PagedResponse<StudentListItem>>`.
- `admin-students.component.ts` refactored: client-side computed signals removed, server-driven `load()` on every param change (page, search, includeArchived, sort). Row actions reload current page after success.
- `admin-dashboard.component.ts` updated to read `r.items` from paged response.
- All spec files updated: `admin-students.component.spec.ts` (full rewrite for server-driven pattern), `admin-dashboard.component.spec.ts`, `admin-wrapper-migration.spec.ts`, `AdminManagementEndpointTests.cs`.
- 12 new integration tests in `AdminEndpointTests.cs`. 2 pre-existing tests in `AdminManagementEndpointTests.cs` fixed for new response shape.
- Backend: 1944 tests pass. Frontend: 756 tests pass.
- No migration. No student-facing changes. No bulk operations.

See: `docs/reviews/2026-06-19-phase-10students-f-f-server-side-student-pagination-filtering-sorting-review.md`

---

**Phase 10Students-F-E — Student Audit / History Tab** - complete (2026-06-19)

Goal: surface student-specific admin action history in the admin student detail page. Combines AdminAuditLog and StudentResetLog entries, newest-first, capped at 50.

### Delivered

- `StudentAuditHistoryItemDto` record in `AdminQueries.cs`.
- `GetStudentAuditHistoryAsync(Guid studentProfileId)` added to `IAdminStudentQuery` interface and `AdminHandler` implementation. Queries both `AdminAuditLogs` (by `TargetStudentId`) and `StudentResetLogs` (by `StudentProfileId`). Combined in memory, sorted newest-first, capped at 50. Returns null when student not found (→ 404).
- `GET /api/admin/students/{id}/audit-history` in `AdminController` — admin-only, 200 / 404.
- No migration required — both tables already existed.
- `StudentAuditHistoryItem` TypeScript interface added to `admin.models.ts`.
- `AdminApiService.getStudentAuditHistory(id)` in Angular service.
- Audit History section added at bottom of `admin-student-detail.component`: loading / error / empty states, table rows with action badge, source, actor ID prefix, reason, old→new value, details. Long details (>80 chars) open `sp-admin-slide-over`. No edit/delete controls on rows. No password fields.
- 7 backend integration tests + 8 frontend unit tests. All gates green.
- No student-facing changes. No global audit search. No server-side student list pagination.

See: `docs/reviews/2026-06-19-phase-10students-f-e-student-audit-history-tab-review.md`

---

**Phase 10Students-F-D — Admin CEFR Management** - complete (2026-06-19)

Goal: allow admins to set or clear a student's CEFR level from the admin student detail page. Students cannot edit their own CEFR.

### Delivered

- `AdminSetCefrLevel(string? level)` domain method on `StudentProfile`: normalises, validates, or clears CEFR.
- `SetStudentCefrCommand` record and `SetStudentCefrAsync` in `IAdminStudentQuery` and `AdminHandler`.
- `AdminHandler.SetStudentCefrAsync`: writes `AdminAuditLog` with action `SetCefr`, old/new value JSON, and reason.
- `PUT /api/admin/students/{id}/cefr` in `AdminController` — admin-only, 200/400/404.
- `SetStudentCefrRequest` DTO: `CefrLevel` (string|null) + optional `Reason`.
- `AdminApiService.updateStudentCefr(id, cefrLevel, reason?)` in Angular service.
- CEFR badge display with "Set CEFR" button in admin student detail profile section.
- Modal with A1–C2 dropdown + "Clear / Not set" option + optional reason field.
- On success: student detail reloaded, toast shown. On error: inline error in modal.
- Helper text: "CEFR is controlled by assessment and admin. Students cannot edit this."
- 5 backend integration tests + 9 frontend unit tests. All gates green.
- No migration added. No student-facing changes. No placement logic touched.

See: `docs/reviews/2026-06-19-phase-10students-f-d-admin-cefr-management-review.md`

---

**Phase 10Students-F-B — Dedicated Student Detail Endpoint + Onboarding Progress** - complete (2026-06-19)

Goal: dedicated `GET /api/admin/students/{id}` endpoint returning full student detail with onboarding progress. Fix SQLite integration test blocker. Wire Angular component to dedicated endpoint.

### Delivered

- `GetStudentDetailAsync` in `AdminHandler` queries `StudentOnboardingProgress` by `UserId` (no ORDER BY — unique index, at most one row; avoids SQLite DateTimeOffset incompatibility).
- `AdminStudentDetailDto` and `StudentOnboardingProgressInfo` records in `AdminQueries.cs`.
- `GET /api/admin/students/{studentId:guid}` in `AdminController`.
- `OnboardingFlowSeeder.SeedAsync` added to `ApiTestFactory.EnsureCreatedAsync` — fixes FK constraint in integration tests.
- 6 integration tests: expected fields, preference fields, null onboarding, onboarding row exists, 404, 403.
- Angular: `getStudent(id)` in `AdminApiService`; `AdminStudentDetail` and `StudentOnboardingProgressInfo` models; component loads from dedicated endpoint; onboarding progress section with status badge, step, percentage, empty state.
- Frontend spec: `Subject<AdminStudentDetail>` replaces `require('rxjs')` hack; `displayName: null` override fix.
- Review doc: `docs/reviews/2026-06-19-phase-10students-f-b-dedicated-student-detail-endpoint-onboarding-progress-review.md`

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1911/1911)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (719/719)

---

**Phase 10Students-F-A — Admin Read: Student Learning Preferences** - complete (2026-06-19)

Goal: surface student learning preferences to admins in a read-only view on the student detail page. Deliver `sp-admin-slide-over` as the design-system foundation for admin secondary detail panels.

### Delivered

- `sp-admin-slide-over` component added to admin design system (`src/app/admin/components/slide-over/`). Inputs: `open`, `title`, `subtitle`, `size` (sm/md/lg/xl), `loading`, `loadingMessage`, `error`, `errorTitle`, `closeOnBackdrop`. Output: `closed`. Slots: `[slot=header-actions]`, body, `[slot=footer]`. Escape and backdrop close. ARIA: `role=dialog`, `aria-modal`. Responsive.
- Exported from `src/app/admin/index.ts` barrel.
- "Student preferences" section added to `admin-student-detail.component`: summary card shows all preference fields; "View preferences" button opens slide-over with full detail.
- `hasAnyPreference()` helper guards empty state.
- No new backend endpoint or migration required — preference fields already returned by `GET /api/admin/students` from Phase 10R-J.
- Admin edit of preferences intentionally not implemented (read-only scope).
- 16 unit tests for `sp-admin-slide-over`. 6+ unit tests for preferences section in `admin-student-detail.component.spec.ts`.
- Gap check review: `docs/reviews/2026-06-19-phase-10students-f-0-enterprise-student-management-gap-check.md`
- Slide-over panel review: `docs/reviews/2026-06-19-phase-10students-f-foundation-admin-slide-over-panel-review.md`
- Phase review: `docs/reviews/2026-06-19-phase-10students-f-a-admin-read-student-preferences-review.md`

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (708/708)

### Remaining TODOs

- `TODO-10X-DRAWER`: typed drawer payloads for student detail, usage policy editor, prompt preview.
- `TODO-10X-DARKMODE`, `TODO-10X-MODAL`, `TODO-10X-TOAST`: admin shell polish items.
- `TODO-10U`: full AI usage/config redesign.
- `TODO-10V`: prompt playground.
- Admin edit of student preferences: not scoped, requires product decision.

---

**Phase 10R-J — Student Usage Policy Assignment Admin UI** - complete (2026-06-19)

Goal: allow admins to view, assign, and reset a student's usage policy from the student detail page.

### Delivered

- `RemoveStudentPolicyAssignmentAsync` on `IUsageGovernanceAdminService` and implementation — deactivates active assignment, writes audit log, safe no-op if none exists.
- `StudentEffectivePolicyResult` record — wraps `UsagePolicy` with `IsOverride`, `AssignedAt`, `AssignedByAdminUserId`, `Reason`.
- `GetStudentEffectivePolicyAsync` updated to return `StudentEffectivePolicyResult?` instead of bare `UsagePolicy?`.
- `GET /api/admin/students/{id}/usage-policy` response extended with `isOverride`, `assignedAt`, `assignedByAdminUserId`, `reason` fields.
- `DELETE /api/admin/students/{id}/usage-policy` endpoint added — 204 on success, safe no-op if no active assignment.
- `StudentEffectivePolicy` TypeScript interface added to `usage-governance.service.ts`.
- `getStudentEffectivePolicy(studentId)` and `removeStudentPolicy(studentId)` added to `UsageGovernanceService`.
- Usage Policy section added to `admin-student-detail.component`: policy name, scope, override/default badge, assigned date, reason (when override), active rule count.
- "Assign Policy" action: opens modal, loads active policies, lets admin pick policy and enter reason, calls `assignStudentPolicy`, refreshes.
- "Reset to Default" action: visible only when override is active, confirm dialog, calls `removeStudentPolicy`, refreshes.
- `admin-student-detail.component.spec.ts` created: 9 tests covering render, badge, modal open, assign call, assign error, remove confirm, remove cancel, remove error.
- `TODO-10R-STUDENT-ASSIGN` closed.
- `StudentListItem` test fixtures in `admin-dashboard.component.spec.ts` and `admin-students.component.spec.ts` updated with new learning preferences fields (`preferredName`, `supportLanguageCode`, `supportLanguageName`, `difficultyPreference`, `translationHelpPreference`, `focusAreas`, `customFocusArea`, `learningGoals`, `customLearningGoal`, `learningPreferencesUpdatedAt`) — all 708 frontend tests passing.

See: `docs/reviews/2026-06-19-phase-10r-j-student-usage-policy-assignment-admin-ui-review.md`

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors, 7 pre-existing warnings)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (708/708)

### Remaining TODOs

- `TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT`: Optional DB unique index on `(UsagePolicyId, FeatureKey)`.
- `TODO-10U`: full AI usage/config redesign.
- `TODO-10V`: prompt playground.

---

## Previous sprint

**Phase 10R-H — Usage Policy Rule Editor Admin UI** - complete (2026-06-19)

Goal: add admin UI for creating, editing, and deleting individual usage policy rules using the 10R-G backend.

### Delivered

- Rule editor modal (`sp-admin-modal variant="form"`) with full form: feature select/input, enforcement mode, unit type, all limit fields, warning threshold, tracking enabled, active toggles.
- Delete confirmation modal (`sp-admin-modal variant="danger"`) with rule key display.
- Add rule button in each expanded policy row. Edit/Delete buttons per rule row.
- Signal-per-field form state; client-side validation with inline error display.
- `openAddRule`, `openEditRule`, `closeRuleModal`, `saveRule`, `openDeleteRule`, `closeDeleteModal`, `confirmDelete` methods.
- Local state helpers `addRuleInPlace`, `updateRuleInPlace`, `removeRuleInPlace` — no full reload after rule CRUD.
- Feature key shown as read-only in edit mode with explanatory note.
- Build: clean. Tests: 670/670.

See: `docs/reviews/2026-06-19-phase-10r-h-usage-policy-rule-editor-admin-ui-review.md`

---

## Previous sprint

**Phase 10R-G — Usage Policy Rule CRUD Backend Foundation** - complete (2026-06-19)

Goal: add backend/domain/API support for individual usage policy rule create, update, and delete.

### Delivered

- `UsagePolicyRule.Update(...)` domain method with full validation (mirroring constructor invariants).
- `AddUsagePolicyRuleRequest` and `UpdateUsagePolicyRuleRequest` application DTOs.
- `AddRuleAsync`, `UpdateRuleAsync`, `DeleteRuleAsync` on `IUsageGovernanceAdminService` + implementation.
- Duplicate-key guard in `AddRuleAsync`: one rule per `(policyId, featureKey)` enforced at application layer.
- Three new admin API endpoints: `POST/PUT/DELETE /api/admin/usage-policies/{policyId}/rules[/{ruleId}]`.
- `MapRule` helper on controller; all three endpoints admin-auth protected.
- Frontend `UsageGovernanceService`: `addRule`, `updateRule`, `deleteRule` methods + two new request interfaces.
- 4 unit tests, 16 integration tests (8 service + 8 endpoint), 3 Angular service tests — all pass.
- No migration needed; no UI rule editor built yet.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1905/1905)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (670/670)

### Remaining TODOs

- `TODO-10R-RULE-MGMT-UI`: Inline rule editor UI in admin-usage-policies page (next phase).
- `TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT`: Optional DB unique index on `(UsagePolicyId, FeatureKey)`.

---

## Previous sprint

**Phase 10R-F — Usage Governance Admin UX Foundation** - complete (2026-06-19)

Goal: make the Usage Policies admin page production-usable using existing API and admin design system wrappers.

### Delivered

- Summary stat cards: Total Policies, Active count, Default policy name.
- Expandable rule detail rows: feature key (code pill), feature display name, enforcement mode badge (typed), unit type, active state, limit summary (daily/weekly/monthly count and cost).
- Feature name lookup via computed `featureNameMap` from the feature definitions API.
- Fixed `enforcementBadgeTone` return type to `SpAdminBadgeTone` (was unconstrained `string`).
- Added `SpAdminStatCardComponent`, `SpAdminSectionCardComponent`, `SpAdminCodePillComponent`.
- Scope type now shown as neutral badge. Description shown as muted subtitle in table row.
- 9 new behavioral tests added; full suite 667/667 pass.

### Gates

- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (667/667)
- Backend unchanged; .NET build/test not required.

### Remaining TODOs

- `TODO-10R-RULE-MGMT`: Rule create/edit/delete UI blocked until a per-rule API endpoint is added.
- ~~`TODO-10R-STUDENT-ASSIGN`~~: Done in Phase 10R-J.

---

## Previous sprint

**Phase 10X-J-T — Frontend Test Cleanup: Remove Brittle CSS/Class Assertions** - complete (2026-06-19)

Goal: clean up frontend tests that asserted Tailwind, TailAdmin, BEM, wrapper implementation,
border/radius/spacing classes, inline styles, and other visual implementation details while the
admin UI remains under active iteration.

### Delivered

- Angular specs now avoid brittle CSS/class assertions for admin wrappers, admin layout, student
  chips/badges, profile chips, and wrapper-migration coverage.
- Playwright tests now use accessible buttons, roles, `aria-pressed`, `data-testid`, visible text,
  and smoke-flow assertions instead of Tailwind/TailAdmin/internal class selectors.
- Removed style-only test cases that only checked CSS implementation details.
- Kept behavior coverage for rendering, projected content, form/CVA binding, sorting events,
  dropdown/modal open and close, row actions, navigation, and disabled/loading state.
- Documented the frontend testing rule in `docs/architecture/admin-ui-design-system.md`.
- Added TODO-10X-J-T-VISUAL-BASELINE for a future proper visual regression baseline.

### Gates

- Frontend gates required for this phase:
  - `cd src/LinguaCoach.Web && npm ci`
  - `cd src/LinguaCoach.Web && npm run build -- --configuration production`
  - `cd src/LinguaCoach.Web && npm test -- --watch=false --browsers=ChromeHeadless`
  - `npx playwright test --workers=1 --reporter=dot`
  - `git diff --check`
- Backend gates intentionally not run; this phase is frontend-only.

### Review doc

`docs/reviews/2026-06-18-phase-10x-j-t-frontend-test-cleanup-review.md`

---

**Phase 10X-I — Migrate Remaining Admin Forms and Modals to CVA Wrappers** - complete (2026-06-19)

Goal: migrate the three deferred admin form/modal targets to `sp-admin-*` CVA wrappers after the
CVA foundation (10X-H) and layout blocker fix (10X-LAYOUT-BLOCKER) unblocked this work.

### Delivered

- **AI Config:** provider/model/voice category selects kept as native `<select>` inside
  `<sp-admin-form-field>` (incompatible with `sp-admin-select` due to `[ngValue]="null"`).
  TTS voice text input, model name input, API key (password) input, Qwen endpoint input all
  migrated to `<sp-admin-input>`. Add/Test/Save/Clear buttons migrated to `<sp-admin-button>`.
  Removed page-local `.sp-ai-select`/`.sp-ai-model-select` CSS; added `.sp-adm-native-select`.
- **Integrations:** storage display fields migrated to `<sp-admin-input [disabled]="true">` inside
  `<sp-admin-form-field>`. Generation settings number inputs kept native `<input type="number">`
  (CVA writes strings — numeric domain integrity preserved); wrapped in `<sp-admin-form-field>`.
  Test/Save/Cancel/Retry/Generate buttons migrated to `<sp-admin-button>`. Tables rewrote to
  TailAdmin Tailwind classes directly.
- **Student modals (all 3):** edit, reset-password, and reset-data page-local modals replaced with
  `<sp-admin-modal>`. All text/password inputs inside use `<sp-admin-input>`. Textareas use
  `<sp-admin-textarea>`. Submit/cancel actions use `<sp-admin-button>`. Removed all page-local
  `.sp-admin-modal-backdrop`/`.sp-admin-modal`/`.sp-admin-modal-header`/`.sp-admin-edit-grid` CSS.
- **`sp-admin-modal`:** added `maxWidth` `@Input()` (default `520px`); student edit modal uses `720px`.
- **`sp-admin-input`:** added `@Input() value` getter/setter for one-way display binding.
- **`sp-admin-layout`:** content area changed from `<div>` to `<main>` for `role="main"` semantics,
  fixing Playwright `getByRole('main')` locator failures.
- 11 new Angular unit tests for the migrated components.

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build (production): clean; Angular tests: 421 passed (up from 411)
- Playwright: `getByRole('main')` locator issue resolved by `<main>` layout fix

### Closed TODOs

- TODO-10X-G-AICONFIG-FORMS: done
- TODO-10X-G-INTEGRATIONS-FORMS: done
- TODO-10X-D-MODAL: done
- TODO-10X-I: done

### Review doc

`docs/reviews/2026-06-19-phase-10x-i-admin-form-modal-migration-review.md`

---

## Previous sprint

**Phase 10X-LAYOUT-BLOCKER — TailAdmin Layout One Shell Parity** - complete (2026-06-19)

Goal: make admin shell/sidebar/header/content visually match TailAdmin Layout One.
Blocked 10X-I until resolved.

### Delivered

- Imported TailAdmin `@utility` classes (`menu-item`, `menu-item-active`, `menu-item-inactive`,
  `menu-item-icon-*`, `menu-dropdown-*`, `no-scrollbar`) and `@theme` tokens into global `styles.css`.
- Rewrote `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header` wrappers to use exact
  TailAdmin Tailwind classes with no competing custom CSS class names.
- Rewrote `admin-app-layout.component.html` to use TailAdmin grouped nav section structure
  (`menu-item`/`menu-item-active`/`menu-item-inactive` classes, `h2` section headings).
- Replaced old mobile drawer (custom CSS) with TailAdmin `fixed inset-0 z-40` backdrop
  and `xl:hidden -translate-x-full / translate-x-0` aside pattern.
- Stripped all competing layout CSS from `admin-app-layout.component.css`.
- Added `body.admin-layout` class (OnInit/OnDestroy) for TailAdmin gray-50 background
  without affecting student layout.
- 20 new Angular tests for Layout One shell structure.
- Updated 13 existing tests to validate TailAdmin classes instead of old CSS class names.
- All CI gates pass: 1885 .NET tests, 411 Angular tests, 183+ Playwright tests.

### Root causes fixed

1. TailAdmin `@utility` menu classes missing from SpeakPath Tailwind build.
2. `admin-app-layout.component.css` margin-left override fighting Tailwind `xl:ml-*`.
3. `sp-admin-layout/sidebar/header` CSS classes conflicting with Tailwind classes.
4. Body background not overridden for admin context.
5. Nav items using old `sp-admin-nav-item` instead of `menu-item` utility.

### Review doc

`docs/reviews/2026-06-19-phase-10x-layout-blocker-tailadmin-shell-parity-review.md`

---

## Previous sprint

**Phase 10X-H — Admin Form Wrapper CVA + Remaining Form/Modal Migration Foundation** - complete (2026-06-19)

Goal: make TailAdmin-backed admin form wrappers safe for real Angular forms by adding
`ControlValueAccessor`, preparing the foundation for future enterprise admin screens.

### Delivered

- `sp-admin-input`: `ControlValueAccessor` via `NG_VALUE_ACCESSOR` + `forwardRef`. Supports
  `[(ngModel)]`, reactive `[formControl]`/`formControlName`, `setDisabledState`, touched-on-blur.
  Pass-through `type`, `placeholder`, `autocomplete`, `readonly`, `required`, `invalid`.
- `sp-admin-select`: `ControlValueAccessor`. Options via `[options]` or projected `<option>`,
  `placeholder` disabled default option, `required`, `invalid`, disabled propagation, touched-on-blur.
- `sp-admin-textarea`: new wrapper with `ControlValueAccessor`. `rows`, `placeholder`, `readonly`,
  `required`, `invalid`, disabled propagation, touched-on-blur. Exported from `admin/index.ts`.
- `sp-admin-form-field`: red `*` required marker via `[required]`; label/hint/error structure.
- Tests: 15 new wrapper specs (ngModel write/propagate, reactive FormControl bind, disabled
  propagation, touched-on-blur for input/select/textarea; form-field label/hint/error/required).

### Deferred (CVA foundation now unblocks these)

- AI Config dense provider-credentials grid (TODO-10X-G-AICONFIG-FORMS) — kept native this phase to
  avoid silent save regressions across the high-field-count ngModel-driven grid.
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS) — per-field migration pass deferred.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build (production): clean; Angular tests: 394 passed (up from 379)
- Playwright: 188 passed

See: `docs/reviews/2026-06-19-phase-10x-h-admin-form-cva-modal-foundation-review.md`

---

## Previous sprint

**Phase 10X-G-F — Finish Remaining Admin Page Refactor to TailAdmin-backed Wrappers** - complete (2026-06-18)

Goal: finish wrapper consistency on the remaining admin pages after 10X-G.

### Delivered

- Students: row table wrapped in `sp-admin-table` (projection mode); lifecycle/onboarding/CEFR pills
  migrated from raw `.sp-admin-badge` class to the `sp-admin-badge` wrapper. Removed obsolete
  page-local pagination, row-action link, and `.sp-admin-row-actions` CSS.
- Curriculum: create/edit and routing-preview form fields migrated to `sp-admin-form-field`
  (closes TODO-10X-G-CURRICULUM-FORMS). Native ngModel controls retained inside each field because
  `sp-admin-input`/`sp-admin-select` have no ControlValueAccessor.
- Verified AI Usage, Prompts, Exercise Types, Diagnostics, Usage Policies, and Integrations cards
  were already wrapper-migrated in 10X-B/10X-G. No further raw badge/table legacy in those pages.
- Tests: 2 new wrapper-migration specs (Students table/badge/actions; Curriculum form fields).

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build: clean; Angular tests: 379 passed (up from 377)
- Playwright: 188 passed

### Not implemented in 10X-G-F (deferred)

- AI Config dense provider-credentials form fields (TODO-10X-G-AICONFIG-FORMS).
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS).
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).
- Admin-only dark-mode class boundary (TODO-10X-G-DARKMODE).
- Usage governance UX (10R-F), AI Usage redesign (10U), prompt playground (10V), notification
  platform, enterprise auth/security, observability, billing, StudentProfile.CefrLevel migration,
  full placement engine, full mastery engine.

See: `docs/reviews/2026-06-18-phase-10x-g-f-finish-admin-page-refactor-review.md`

---

## Earlier sprint

**Phase 10X-G — Full Admin Page Refactor to TailAdmin-backed Wrappers** - complete (2026-06-18)

Goal: refactor the highest-legacy admin pages onto `sp-admin-*` wrappers, wire the admin
header user menu through `sp-admin-dropdown`, and reduce duplicated page-local CSS.

### Delivered

- Dashboard: KPI tiles → `sp-admin-stat-card`; sections → `sp-admin-card` (incl. dashed
  placeholders); status pills → `sp-admin-badge`. Removed ~50 lines of page-local CSS
  (KPI card, status card, badge, table-card) now owned by wrappers.
- AI Config: removed duplicate in-card `<h2>` headings (card title is canonical); LLM and
  TTS category Save/Test actions → `sp-admin-button`.
- Curriculum: create/edit and routing-preview panels → `sp-admin-card`; form and preview
  actions → `sp-admin-button` (replaced student-design `.sp-card`/`.sp-btn`).
- Admin header user/profile menu → `sp-admin-dropdown` (open state, click-outside, Escape
  owned by the wrapper). Removed `profileMenuOpen`, `toggleProfileMenu`, and the document
  click handler from `AdminAppLayoutComponent`.
- Tests: new `admin-app-layout.component.spec.ts` (4 header-dropdown tests); updated dashboard
  KPI assertion to target `sp-admin-stat-card`; curriculum create test asserts `sp-admin-card`.
- Docs: design-system page-refactor rules + header-dropdown section; adapter inventory 10X-G
  phase; product-state; TODOs.

### Gates

- git diff --check: ✅ clean
- Angular build: ✅ passed
- Angular tests: ✅ 377 passed (up from 373)
- .NET build: ✅ 0 errors
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ 188 passed

### Not implemented in 10X-G (deferred)

- Migrating remaining page-local form fields (`.sp-ai-select`, `.sp-input`, Integrations
  operational forms) to `sp-admin-form-field`/`sp-admin-select` — see TODOs.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).
- Usage governance UX (TODO-10R-F), AI Usage redesign (TODO-10U), prompt playground (TODO-10V).
- Notification platform, enterprise auth/security, observability stack, billing,
  StudentProfile.CefrLevel migration, full placement engine, full mastery engine.

See: `docs/reviews/2026-06-18-phase-10x-g-full-admin-page-refactor-review.md`

---

## Previous sprint

**Phase 10X-F — Admin Wrapper Capability Completion** - complete (2026-06-18)

Goal: add missing reusable admin wrapper capabilities: sortable tables, row action dropdowns, admin dropdown wrapper, theme toggle, improved filter-bar and header slots.

### Delivered

- `sp-admin-dropdown` — TailAdmin-backed dropdown with trigger/menu content projection, click-outside + Escape close, align and width inputs.
- `sp-admin-table-actions` — row action dropdown (three-dot trigger). Generic `[actions]` array API + content projection for conditional per-row actions. Danger item red styling.
- `sp-admin-theme-toggle` — admin-only dark/light toggle based on TailAdmin ThemeToggleButtonComponent. Uses `AdminThemeService` (isolated `adminTheme` localStorage key).
- `AdminThemeService` — admin-scoped theme service mirroring TailAdmin pattern.
- `sp-admin-table` updated: sortable column flag, `sortColumn`/`sortDirection` inputs, `(sortChange)` output, `↕/▲/▼` icons, `aria-sort`, keyboard-accessible headers.
- `sp-admin-header` updated: named `[left]` and `[actions]` content slots. Theme toggle auto-rendered in right action zone.
- `sp-admin-filter-bar` updated: named `[search]`, `[filters]`, `[actions]` slots. Left/right zone split. Backward-compat general projection retained.
- Admin barrel (`admin/index.ts`) updated with 4 new exports.
- `admin-students` page row actions migrated to `sp-admin-table-actions` (projected content with conditional actions).
- 24 new Angular tests added in `admin-components.spec.ts` (Phase 10X-F block).
- Playwright tests updated to open row action dropdown before interacting with action items (reset, archive, view, edit).

### Gates

- Angular build: ✅ passed
- Angular tests: ✅ 373 passed (up from 349)
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ 188 passed

### Not implemented in 10X-F

- Full admin page refactor/redesign (10X-G)
- Usage governance UX (TODO-10R-F), AI Usage redesign (TODO-10U), prompt playground (TODO-10V)
- Notification platform, enterprise auth, billing, placement/mastery engine

---

## Previous sprint

**Phase 10X-E — Adapt Real TailAdmin Patterns into sp-admin-* Wrappers** - complete (2026-06-18)

Goal: replace custom CSS approximations in all 15 `sp-admin-*` wrappers with actual TailAdmin free Angular template class structures. Admin feature pages unchanged — they continue using `sp-admin-*` only.

### Delivered

- All 15 wrapper components adapted to TailAdmin source patterns:
  layout (`min-h-screen xl:flex`), sidebar (`fixed w-[290px]/w-[90px]`), header (`sticky top-0 z-[99999]`),
  button (brand-500 primary / outline), badge (light variant color map), card (`rounded-2xl border border-gray-200`),
  stat-card, input (`h-11 rounded-lg border border-gray-200`), select, form-field, modal (`rounded-3xl`, `bg-gray-400/50 backdrop-blur-sm`),
  table (`rounded-2xl`, th `text-xs bg-gray-50`), pagination, filter-bar, drawer.
- Fixed TypeScript parse error: HTML comment inside `@Component({})` decorator in `sp-admin-badge.component.ts`
  (converted to `//` TS comments above decorator).
- Added 15 new TailAdmin-backed pattern tests in `admin-components.spec.ts` (Angular: 334 → 349).
- Updated `docs/architecture/admin-tailadmin-adapter-inventory.md`: all adapted wrappers marked `✅ Done`.
- Updated `docs/architecture/admin-ui-design-system.md`: Phase 10X-E note, wrapper API stability.
- Engineering review saved to `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md`.

### Gates

- Angular build: ✅ passed
- Angular tests: ✅ 349 passed (0 failed)
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ (see Playwright gate status in product-state)

### Not implemented in 10X-E

- Table sorting, dropdown, theme toggle wiring (10X-F)
- Usage governance UX, AI Usage redesign, prompt playground, notification platform, enterprise auth, billing, placement engine

---

## Previous sprint

**Phase 10X-D — TailAdmin Template Import & Adapter Plan** - complete (2026-06-18)

Goal: import the free TailAdmin Angular template as a vendor reference, document the adapter boundary, and create the mapping inventory that drives 10X-E/10X-F.

### Delivered

- Cloned TailAdmin free Angular template into `templates/tailadmin/free-angular-tailwind-dashboard/` (commit da992cf, MIT license).
- Removed nested `.git` directory; added `.gitignore` to exclude `node_modules/dist/.angular/coverage`.
- Added `templates/README.md` and `templates/tailadmin/README.md` with source URL, commit, license, allowed/disallowed adapter rules, and update process.
- Updated `docs/architecture/admin-ui-design-system.md`: closed TODO-10X-ASSETS, updated folder structure, vendor source location, and adapter reference.
- Created `docs/architecture/admin-tailadmin-adapter-inventory.md`: full mapping table (TailAdmin pattern → sp-admin-* wrapper, phase, status, notes).
- Updated `docs/sprints/current-sprint.md` and `docs/handoffs/current-product-state.md`.
- Created engineering review at `docs/reviews/2026-06-18-phase-10x-d-tailadmin-template-import-adapter-plan-review.md`.
- Angular build, Angular tests, Playwright, and .NET tests all passed (gates below).

### Not implemented in 10X-D

- Full wrapper replacement using TailAdmin source (10X-E)
- Full admin page refactor (10X-F)
- Usage governance UX, AI Usage redesign, prompt playground, notification platform, enterprise auth/security, observability stack, billing, StudentProfile.CefrLevel migration, full placement engine, full mastery engine

---

## Previous sprint

**Phase 10X-C-F — TailAdmin Layout One Gate Closure** - complete (2026-06-18)

Goal: close Phase 10X-C, fix the critical ViewEncapsulation bug, verify all gates,
and confirm the admin shell now matches TailAdmin Angular Layout One structurally.

### Delivered

- Fixed `AdminAppLayoutComponent` to use `ViewEncapsulation.None` so shell CSS
  (sidebar, nav, header, drawer, profile flyout) reaches child component DOM.
- Raised `anyComponentStyle` budget in `angular.json` from 8kB → 12kB warning / 20kB error
  to accommodate the legitimate admin shell CSS.
- Angular production build: passing.
- Angular unit tests: 334 passed.
- Playwright: 188 passed.
- .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration).
- Visual screenshots confirmed: sidebar left, content right, header sticky, collapsed state,
  mobile drawer — all match TailAdmin Layout One structure.
- Updated `docs/architecture/admin-ui-design-system.md` with TailAdmin as visual source of truth,
  ViewEncapsulation note, asset status, legacy style rules, and migration checklist.
- Updated `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`, TODOS.md.
- Engineering review saved to `docs/reviews/`.

### Remaining legacy areas (unchanged from 10X-B)

- Dashboard still has large component-local CSS for action cards, KPI layout, and placeholders.
- Student edit/reset modals still use page-local modal markup.
- AI Config and Integrations still retain page-local form internals inside wrapper cards.
- Curriculum create/edit/preview subviews still have page-local form markup.

---

## Previous sprint

**Phase 10X-B - Admin Core Page Migration to Design System** - complete (2026-06-18)

Goal: migrate existing admin pages to the reusable SpeakPath admin wrapper system without adding new business features.

### In scope

- Migrate core admin pages to `sp-admin-*` wrappers where feasible.
- Reduce direct feature-page use of common TailAdmin-style utility lists.
- Improve wrapper capability only where required by migration.
- Preserve existing admin functionality.
- Add Angular wrapper-presence tests.
- Add a stable Playwright mobile overflow check for migrated admin pages.
- Update architecture, handoff, review, and TODO docs.

### Out of scope

Full usage-governance UX, full AI Usage redesign, notification platform, enterprise auth, prompt playground, observability stack, billing, StudentProfile CEFR migration, full placement engine, full mastery engine, and new backend business logic.

### Architecture decisions

- Feature pages import wrappers from `src/app/admin`.
- TailAdmin-specific style decisions stay behind wrappers and tokens.
- Student UI remains separate.
- Tables that need custom projected rows use `sp-admin-table` projection.
- Page-local CSS remains allowed for unique workflows, but common cards, headers, badges,
  filters, buttons, tables, empty, loading, and error states should use wrappers.

### Delivered

- Wrapper improvements: projected table mode, `primary` badge tone, empty-state title,
  and improved filter-bar alignment.
- Migrated pages: Dashboard, Students, AI Config, AI Usage, Prompts, Exercise Types,
  Integrations, Diagnostics, Curriculum, and Usage Policies.
- Added `admin-wrapper-migration.spec.ts` plus wrapper assertions in existing admin specs.
- Added Playwright mobile overflow coverage for `/admin/students` and `/admin/ai-config`.

### Remaining legacy internals

- Dashboard still carries large inline component CSS.
- Student edit/reset/archive modals still use page-local modal markup.
- AI Config and Integrations still retain page-local form internals inside wrapper cards.
- Curriculum create/edit/preview subviews still retain page-local form internals.

See: `docs/architecture/admin-ui-design-system.md`
See: `docs/reviews/2026-06-18-phase-10x-b-admin-core-page-migration-review.md`

---

## Most recently completed sprint

**Phase 10R — Usage Governance, Token Tracking & Quota Enforcement** — complete (2026-06-18)

Phase 10R introduces enterprise-grade usage governance. Every AI feature call is tracked per student. Admins can define quota policies with daily limits per feature. Expensive AI calls are blocked before they incur cost when a student's quota is exhausted. The system distinguishes prepared learning (always allowed) from expensive on-demand AI (gated).

### What was built

**Domain**
- 4 new enums: `FeatureCategory`, `EnforcementMode`, `UsageUnitType`, `UsagePolicyScopeType`.
- 7 new entities: `FeatureDefinition`, `UsagePolicy`, `UsagePolicyRule`, `StudentPolicyAssignment`, `UsageEvent` (append-only ledger), `StudentUsageDaily` (upserted aggregate), `AdminAuditLog`.

**Persistence**
- 7 new EF Core configurations.
- EF migration `Phase10R_UsageGovernance`.
- `UsageGovernanceSeeder` — seeds 16 feature definitions and 3 policies idempotently.

**Application layer**
- `IUsageQuotaService` — `CheckAsync`, `RecordAsync`, `GetUsageSummaryAsync`, `GetEffectivePolicyAsync`.
- `QuotaDecision` — result with Allowed flag, limits, AvailableAlternatives.
- `QuotaExceededException` — wraps QuotaDecision for middleware mapping.
- `IUsageGovernanceAdminService` — feature definitions, policy CRUD, student assignment.

**Infrastructure**
- `UsageQuotaService` — policy resolution (student → global default), HardLimit enforcement against `StudentUsageDaily`, event recording.
- `UsageGovernanceAdminService` — policy CRUD with audit log writes.
- `AiExecutionService` — pre-call `CheckAsync` for 8 expensive features; post-call `RecordAsync`; failed calls not recorded.

**API**
- `AdminUsageGovernanceController` — 8 endpoints under `/api/admin/` (feature definitions, policy CRUD, student assignment, usage summary).
- `GlobalExceptionMiddleware` — `QuotaExceededException` → HTTP 429 with structured body.

**Angular**
- `UsageGovernanceService` — HTTP client for all 8 admin endpoints.
- `AdminUsagePoliciesComponent` — create/edit policy form with signals; policies table.
- Route: `/admin/usage-policies`.

**Tests**
- 16 DB-layer integration tests (`UsageGovernanceDbTests`): seeding idempotency, record+aggregate, HardLimit blocking, TrackOnly pass-through, policy resolution, audit log, daily rollup, usage summary, admin CRUD.
- 5 HTTP endpoint integration tests (`UsageGovernanceEndpointTests`): list features, 403, create policy, student usage, non-admin 403.
- 8 Angular component unit tests + 6 service HTTP tests.

### Gates at completion
- Architecture: 3 passed
- Unit: 1233 passed
- Integration: 649 passed
- Total .NET: 1885 passed, 0 failed
- Angular: 302 passed, 0 failed
- Angular prod build: clean

### What is intentionally NOT in Phase 10R
Workspace/cohort policy inheritance, billing integration, provider pricing tables, monthly/weekly limit enforcement, CSV export, student-facing usage widget, notification platform, enterprise auth overhaul.

See: `docs/reviews/2026-06-18-phase-10r-usage-governance-token-quota-review.md`
See: `docs/architecture/usage-governance.md`

---

## Previously most recently completed sprint

**Phase 10Q — Admin Curriculum, Format & Context Controls** — complete (2026-06-18)

Phase 10Q gives admins full CRUD control over the curriculum syllabus used for CEFR-aware activity routing. Includes a non-mutating routing preview, seeder protection for admin-edited objectives, and a full Angular admin UI.

### What was built

**Domain**
- `CurriculumObjective` — added `ExamplePrompts` (string?) and `AdminUpdatedAt` (DateTimeOffset?) properties.
- Added `AdminUpdate()` method: validates CEFR, skill, difficulty band, self-prerequisites; sets `AdminUpdatedAt = UtcNow`.
- Existing `UpdateDetails()` preserved as seeder-safe (never sets `AdminUpdatedAt`).

**EF Migration**
- `T52_CurriculumObjectiveAdminFields` — adds nullable `example_prompts` and `admin_updated_at` columns to `curriculum_objectives`.

**Application layer**
- `AdminCurriculumContracts.cs` — `AdminCurriculumObjectiveDto`, `AdminCurriculumObjectiveUpsertRequest`, `CurriculumTaxonomyDto`, `AdminRoutingPreviewRequest`, `AdminRoutingPreviewResult`, `ICurriculumObjectiveWriteService`, `IAdminCurriculumSyllabusQuery`.

**Infrastructure**
- `CurriculumObjectiveWriteService` — Create, Update, Activate, Deactivate, PreviewRouting. Full validation: slug key format, CEFR, skill, context tag, difficulty band 1–5, self-prerequisite, dangling prerequisite. `PreviewRoutingAsync` is read-only: calls routing service but never calls `SaveChangesAsync` or mutates student data.
- `CurriculumSyllabusQueryService` — now implements both `ICurriculumSyllabusQuery` and `IAdminCurriculumSyllabusQuery`. `GetAllObjectivesForAdminAsync` returns active and inactive objectives with optional filters.
- `DependencyInjection.cs` — dual-interface registration for `CurriculumSyllabusQueryService`; registered `ICurriculumObjectiveWriteService`.

**Seeder**
- `CurriculumObjectiveSeeder` — changed from upsert to seed-only-missing: `if (existing.ContainsKey(def.Key)) continue;`. Admin-edited objectives are never overwritten on startup.

**API**
- `AdminCurriculumController` — 8 endpoints:
  - `GET /api/admin/curriculum/objectives` (cefrLevel, skill, isActive filters)
  - `GET /api/admin/curriculum/objectives/{key}`
  - `GET /api/admin/curriculum/taxonomy`
  - `POST /api/admin/curriculum/objectives`
  - `PUT /api/admin/curriculum/objectives/{key}`
  - `POST /api/admin/curriculum/objectives/{key}/activate`
  - `POST /api/admin/curriculum/objectives/{key}/deactivate`
  - `POST /api/admin/curriculum/routing-preview`

**Angular**
- `CurriculumService` — Angular service with all TypeScript interfaces and 8 methods matching the backend API.
- `AdminCurriculumComponent` — single standalone component with `view` signal (`list` | `create` | `edit` | `preview`). List with CEFR/skill/active filters; create/edit form with all fields; non-mutating routing preview panel. No TailAdmin migration.
- `app.routes.ts` — added `/admin/curriculum` route.
- `AdminShellComponent` — added Curriculum nav link.

**Tests**
- 20 unit tests in `AdminCurriculumObjectiveUnitTests`: AdminUpdate, AdminUpdatedAt, ExamplePrompts, DifficultyBand, self-prerequisite, seeder-safe UpdateDetails, general_english/workplace constants.
- 20 integration tests in `AdminCurriculumObjectivesIntegrationTests`: list/filter, taxonomy, create (valid/invalid CEFR/skill/self-prereq/dangling-prereq), update, deactivate/reactivate, non-admin 403, routing preview non-mutation, general_english default, seeder idempotent.
- 16 Angular unit tests in `admin-curriculum.component.spec.ts`: list renders, filter calls API, activate/deactivate, create/edit navigation, form population, routing preview, error state, taxonomy loading.

### Product rules enforced
- `general_english` remains default/fallback — `workplace` is never silently added as default.
- Deactivating an objective does NOT delete historical records (soft lifecycle only).
- Seeder never overwrites admin-edited objectives.
- Routing preview is non-mutating: no AI calls, no student state changes.

---

## Previous sprint

**Phase 10P — Multi-skill Scoring & Progress Updates** — complete (2026-06-18)

Phase 10P adds a safe, testable multi-skill progress update foundation. Activity attempts now update `StudentSkillProfile` rows for all skills trained by an exercise, not just the primary skill.

### What was built

**Application layer**
- `IMultiSkillProgressService` — new interface in `LinguaCoach.Application.Activity` with `ApplyAsync` and `BuildRequest` methods.
- `MultiSkillProgressUpdateRequest` / `MultiSkillProgressUpdateResult` — input/output contracts.

**Infrastructure layer**
- `MultiSkillProgressService` — implementation in `LinguaCoach.Infrastructure.Activity`.
  - Skill registry: 19 known keys (9 general + 10 workplace-specific). No workplace default.
  - Weighting: primary 70%, secondaries share 30% equally. Primary-only → 100%.
  - Score-to-delta: 0-100 score mapped to −1..1 delta centred on 60; scaled by 10 points/unit.
  - ActivityType fallback map for 6 activity types when no pattern metadata available.
  - Incomplete attempts skipped (no skill writes on failed/abandoned submissions).
  - Best-effort: exceptions swallowed, never blocks activity submission.
- `ActivitySubmitHandler` — injected `IMultiSkillProgressService`. Called after both the pattern evaluation path and the legacy AI path.
  - Pattern path: uses `ExercisePatternDefinition.PrimarySkill` + `SecondarySkillsJson`.
  - Legacy path: falls back to `ActivityType` fallback map.

**Tests**
- 20 unit tests in `MultiSkillProgressServiceTests`: weighting splits, deduplication, unknown keys, incomplete attempts, ActivityType fallback, no-workplace-default, lower-level content.
- 10 integration tests in `MultiSkillProgressServiceIntegrationTests`: DB writes, multi-skill rows, listening+writing, speaking roleplay, writing+grammar+vocab, pattern override, idempotent update.

### Previous sprint

**Phase 10O-F — Practice Gym UI Integration & Completion Consumption Wiring** — complete (2026-06-18)

Phase 10O-F connects the 10O backend suggestion API to the Angular Practice Gym UI and wires completed activities back to readiness pool consumption.

### What was built

**Angular UI**
- `PracticeGymSuggestionsService` — new Angular service with `getSuggestions()`, `startSuggestion()`, `completeSuggestion()` methods targeting `/api/practice-gym/suggestions`.
- `PracticeGymComponent` — extended with suggestion state signals, `loadSuggestions()`, `startSuggestion()` flow, and `routingLabel()` for student-friendly reason display.
- Practice Gym template — added Suggested for you, Continue practice, and Review practice sections with cards showing title, skill, CEFR level, estimated duration, context tags, and routing label. Empty/loading/error states. Existing By skill and By exercise type sections preserved.

**Backend wiring (TODO-014)**
- `ActivitySubmitHandler` — injected `IPracticeGymSuggestionService`. Added `TryConsumeReadinessItemAsync` helper called best-effort after all activity completion paths (WritingScenario/AI, VocabularyPractice, ListeningComprehension, pattern evaluation). Consumption only fires when `evalResult.Completed` is true for pattern path.
- Idempotent: `TryMarkConsumedAsync` no-ops if item is already consumed. Exceptions swallowed so completion response is never blocked.

**Tests**
- 4 new integration tests in `ReadinessConsumptionWiringTests`: completion marks item consumed, idempotent, no-item path succeeds, consumed item absent from suggestions.
- 12 new Angular unit tests in `practice-gym.component.spec.ts`: suggestions load, empty/error states, section rendering, start navigation, routing labels, existing sections preserved under error.

### Previous sprint

**Phase 10O — Practice Gym Suggested Practice & Pool Serving** — complete (2026-06-18)

Phase 10O connects the readiness pool to the student-facing Practice Gym. Students now receive personalised suggestion cards from their pre-filled pool, organised into Suggested, Continue, and Review sections, instead of only the static skill/exercise-type launcher.

### What was built

**Application layer**
- `IPracticeGymSuggestionService` — interface: `GetSuggestionsForStudentAsync`, `StartSuggestionAsync`, `TryMarkConsumedAsync`.
- `PracticeGymSuggestionsDto` / `PracticeGymSuggestionItemDto` / `StartSuggestionResult` — student-facing DTOs with full routing metadata, call-to-action labels, and navigation targets.

**Infrastructure layer**
- `PracticeGymSuggestionService` — pool query, in-memory ranking (focus match → context match → priority → expiry → FIFO), section partitioning, reservation with concurrency retry, best-effort consumption.

**API layer**
- `PracticeGymSuggestionsController` — three new student-facing endpoints:
  - `GET /api/practice-gym/suggestions` — personalised suggestion cards.
  - `POST /api/practice-gym/suggestions/{id}/start` — reserve item, return navigation target.
  - `POST /api/practice-gym/suggestions/{id}/complete` — best-effort mark consumed.
- Existing `GET /api/activity/practice-gym/next` unchanged.

**DI**
- `IPracticeGymSuggestionService` registered as Scoped in `DependencyInjection.cs`.

### Tests
- 14 new unit tests in `PracticeGymSuggestionServiceTests.cs`: exclusion rules, section assignment, reservation idempotency, consumed/failed status handling, TryMarkConsumed.
- 10 new integration tests in `PracticeGymSuggestionIntegrationTests.cs`: DI registration, GET suggestions sections, POST start/complete lifecycle, smoke tests for existing paths, admin write-endpoint absence.

### Gates at completion
- Architecture: 3 passed
- Unit: 1174 passed (was 1160, +14)
- Integration: 597 passed (was 587, +10)
- Total: 1774 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in Phase 10O.

### What is intentionally NOT in Phase 10O
Angular/frontend integration (API is ready; deferred to a follow-on frontend phase), `TryMarkConsumedAsync` wiring in `ActivitySubmitHandler` (deferred — see TODOS.md TODO-010), admin write endpoints, full mastery/placement engine, `StudentProfile.CefrLevel` migration, plus-level persistence, notification system, usage/quota enforcement.

See: `docs/reviews/2026-06-18-phase-10o-practice-gym-suggested-practice-review.md`

---

## Previously most recently completed sprint

**Phase 10N — Background Replenishment Pipeline** — complete (2026-06-17)

Phase 10N builds the background engine that keeps student readiness pools healthy. Pool items are now swept for expiry, orphaned generating items are recovered, failed items are retried within attempt limits, and shortfalls are filled for Today lesson and Practice Gym pools.

### What was built

**Application layer**
- `ReadinessPoolReplenishmentOptions` — configuration bound from `appsettings.json` under `"ReadinessPool"`. Defaults: target 10 items per pool, 14-day expiry, 2-hour reserved timeout, 30-min generating timeout, 60-min retry delay, 50 max items per run. `EnableReviewScaffoldGeneration=false` (conservative default).
- `PoolHealthSummary` — lightweight health DTO: ready count, in-flight count, shortfall, target, needsReplenishment flag.
- `IReadinessPoolReplenishmentService` + `ReplenishmentRunSummary` — service contract for running the full maintenance cycle and querying health per student/source.

**Infrastructure layer**
- `ReadinessPoolReplenishmentService` — full engine:
  - Sweeps expired ready items (past `ReadyItemExpiryDays`).
  - Sweeps expired reserved items (past `ReservedItemExpiryHours`).
  - Recovers orphaned generating items (past `GeneratingTimeoutMinutes` → Failed).
  - Retries failed items (`AttemptCount < MaxGenerationAttempts` and past delay → new Queued item).
  - Fills shortfalls for each active student × source using routing recommendations.
  - Prevents duplicates: skips items already Queued/Generating/Ready/Reserved for same objective/pattern/CEFR.
  - Review/scaffold only enabled when `EnableReviewScaffoldGeneration=true` AND ledger weak events exist.
  - B2 students never silently receive B1 Normal content.
  - `general_english` remains fallback; workplace is not default.
- `ReadinessPoolReplenishmentJob` — Quartz job, every 20 minutes, `[DisallowConcurrentExecution]`.

**DI + Quartz**
- `AddInfrastructure(IConfiguration?)` — now accepts optional config to bind `ReadinessPoolReplenishmentOptions`.
- `QuartzConfiguration` — `ReadinessPoolReplenishmentJob` trigger added (every 20 min).
- `DependencyInjection.cs` — `IReadinessPoolReplenishmentService` registered as Scoped.

**Admin API**
- `GET /api/admin/students/{studentId}/readiness-pool/health` — new read-only endpoint. Returns health for `TodayLesson` and `PracticeGym` pools: target, ready, in-flight, failed, stale, expired, shortfall, needsReplenishment.

### Tests
- 16 new unit tests in `ReplenishmentOptionsTests.cs`: options defaults, pool health math, status exclusion rules, lower-level content guard, routing snapshot preservation, general_english fallback, retry attempt gating.
- 11 new integration tests in `ReplenishmentIntegrationTests.cs`: service DI, health counts by status, ReviewOnly exclusion, expired exclusion, queued/generating in-flight counting, retry path, admin health endpoint, admin pool endpoint (smoke), 10M lifecycle smoke, unknown student zero counts, two-student isolation.

### Gates at completion
- Architecture: 3 passed
- Unit: 1160 passed (was 1144, +16)
- Integration: 587 passed (was 576, +11)
- Total: 1750 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in Phase 10N.

### What is intentionally NOT in Phase 10N
Practice Gym suggested UI redesign, admin write endpoints, `StudentProfile.CefrLevel` migration, plus-level persistence, full mastery engine, full placement engine, serving from pool on user-facing paths (deferred to 10O), `AllowReviewOrScaffold=true` enabled by default, per-student review/scaffold signal, ledger-weighted skill rotation.

See: `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md`

---

## Previously most recently completed sprint

**Phase 10M — Student Activity Readiness Pool Foundation** — complete (2026-06-17)

Phase 10M introduces the persisted lifecycle model for pre-generated Today lessons and Practice Gym activities. Activities are no longer treated as simple one-off outputs; every generated item is tracked in a student-specific readiness pool with lifecycle status, routing snapshot, and personalisation metadata.

### What was built

**Domain layer**
- `ReadinessPoolStatus` enum: Queued, Generating, Ready, Reserved, Consumed, Expired, Failed, Stale, ReviewOnly.
- `ReadinessPoolSource` enum: TodayLesson, PracticeGym, LessonBatch, Review, Remediation, OnDemand.
- `RoutingReason` enum — moved from `Application.Curriculum` namespace to `Domain.Enums` so it can be used by the domain entity without a circular dependency.
- `StudentActivityReadinessItem` entity with full lifecycle transition methods (MarkGenerating, MarkReady, MarkFailed, Reserve, MarkConsumed, Expire, MarkStale, MarkReviewOnly, LinkMaterializedIds).
- Guard: `IsLowerLevelContent=true` requires a non-Normal RoutingReason. B2 students cannot silently receive B1 content as Normal.
- `IsServableAsNormalContent` and `IsServableAsReview` helper properties.

**Persistence layer**
- `StudentActivityReadinessItemConfiguration` — EF table `student_activity_readiness_items`, snake_case columns, 4 indexes (student/status/source, student/status/priority, activity id, session id).
- Optimistic concurrency token (PostgreSQL xmin) registered in `OnModelCreating`.
- `DbSet<StudentActivityReadinessItem>` added to `LinguaCoachDbContext`.
- EF migration `T51_StudentActivityReadinessPool`.

**Application layer**
- `IStudentActivityReadinessPoolService` — interface with full lifecycle + query methods.
- `CreateReadinessItemRequest` DTO.
- `ReadinessPoolSummary` / `ReadinessItemDto` — for admin inspection.
- `ReadinessItemRequestBuilder.FromRoutingRecommendation` — helper to build pool item request from a `CurriculumRoutingRecommendation` + student context snapshot.

**Infrastructure layer**
- `StudentActivityReadinessPoolService` — implementation with optimistic concurrency retry loop on reservation.
- Registered as `AddScoped<IStudentActivityReadinessPoolService, StudentActivityReadinessPoolService>()`.

**Integration points**
- `PracticeGymGenerationJob` — creates Queued→Generating→Ready pool item with routing snapshot per materialized cache row.
- `LessonBatchGenerationJob` — creates Queued→Generating→Ready pool item per materialized session. `BuildCompactSummaryAsync` now returns `(summaryJson, routing)` tuple.
- `ActivityMaterializationJob` — links generated `LearningActivityId` and `SessionExerciseId` to any matching pool item by `LearningSessionId`.

**Admin endpoint**
- `GET /api/admin/students/{studentId}/readiness-pool` — read-only pool inspection (Admin role only). No write endpoints.

### Tests
- 20 new unit tests in `ReadinessPool/StudentActivityReadinessItemTests.cs`.
- 11 new integration tests in `ReadinessPool/ReadinessPoolIntegrationTests.cs`.
- 1 existing integration test updated (`LessonBatchGenerationJobTests`) to pass pool service.
- 1 existing unit test file updated (`CurriculumRoutingServiceTests`) and 1 integration test file (`CurriculumRoutingIntegrationTests`) to use `RoutingReason` from `Domain.Enums`.

### Gates at completion
- Architecture: 3 passed
- Unit: 1144 passed (was 1124, +20)
- Integration: 576 passed (was 565, +11)
- Total: 1723 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in this phase.

### What is intentionally NOT in this phase
Full background replenishment engine, Practice Gym suggested UI redesign, admin write endpoints for pool, `StudentProfile.CefrLevel` migration, plus-level persistence, full placement engine, full mastery engine, `AllowReviewOrScaffold=true` enabling in handlers. See TODOS.md.

See: `docs/reviews/2026-06-17-phase-10m-student-activity-readiness-pool-review.md`

---

## Previously most recently completed sprint

**Phase 10L — CEFR-Aware Activity Routing** - complete (2026-06-17)

Phase 10L introduces a pure application-layer routing policy that selects suitable
CEFR bands and curriculum objectives before every AI activity generation call.

### What was built

**Routing models (Application layer)**
- `CurriculumRoutingRequest` — input: student context, CEFR, skill, source, goal context, preferences.
- `CurriculumRoutingRecommendation` — output: target CEFR, allowed levels, objective key/title, context/focus tags, difficulty band, `RoutingReason`, `IsLowerLevelContent`, explanation.
- `ICurriculumRoutingService` — interface with `RecommendAsync` and `NormalizeCefrLevel`.

**Routing service (Infrastructure layer)**
- `CurriculumRoutingService` — CEFR normalization (B2+ → B2), candidate selection from `ICurriculumSyllabusQuery`, skill filter, difficulty band mapping, lower-level guard (never silently lowers level), non-workplace fallback.
- `CurriculumRoutingRequestFactory` — static helper building requests from `StudentProfile` + resolved goal context.

**AI prompt integration**
- `ActivityGenerationContext` extended: `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold`.
- `AiActivityGeneratorHandler` injects `routingContext` and `routingReason` into AI variables.
- `DbPromptAiContextBuilder` appends routing context before "Return ONLY" in rendered prompt.

**Integration points wired**
- `ActivityGetHandler.HandlePatternKeyedAsync` — on-demand and Practice Gym pattern routing.
- `ExercisePrepareHandler` — Today's Lesson exercise preparation.
- `PracticeGymGenerationJob.MaterializeAsync` — background Practice Gym generation.
- `ActivityMaterializationJob.MaterializeExerciseAsync` — background lesson batch materialization.
- `LessonBatchGenerationJob.BuildCompactSummaryAsync` — AI lesson planning summary now includes routing metadata.

**DI:** `ICurriculumRoutingService` registered as Scoped.

### Tests

- 16 new unit tests in `CurriculumRoutingServiceTests.cs`.
- 7 new integration tests in `CurriculumRoutingIntegrationTests.cs`.
- 1 existing integration test updated (`LessonBatchGenerationJobTests`) to pass routing service.

### Gates at completion
- Architecture: 3 passed
- Unit: 1124 passed (was 1098, +26)
- Integration: 565 passed (was 555, +10)
- Total: 1692 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed.

### What is intentionally NOT in this phase
Readiness pools, background replenishment lifecycle, Practice Gym suggested UI redesign, admin curriculum write UI, `StudentProfile.CefrLevel` migration, plus-level routing persistence, full placement engine, `AllowReviewOrScaffold=true` in handlers (built but always false — enablement belongs to 10M adaptive routing), session length influence on candidate count, CEFR-aware format matrix.

See: `docs/reviews/2026-06-17-phase-10l-cefr-aware-activity-routing-review.md`

---

## Previously most recently completed sprint

**Phase 10K-F — Profile Preference Enforcement Audit & Routing Fix** - complete (2026-06-17)

Phase 10K-F audits and fixes preference propagation across all AI generation surfaces before Phase 10L CEFR-aware routing.

### What was built

**Audit findings**
- Full preference propagation audit across 5 AI generation surfaces, evaluation path, and background jobs.
- `LearnerPreferenceContextFormatter`, `LearningGoalContextResolver`, `CurriculumContextMapper` all confirmed correct for generation paths.
- Three gaps found and fixed.

**Fix P1 — evaluation context carries zero preference data**
- Added `LearnerPreferenceContext` and `LearningGoalContext` optional fields to `ActivityEvaluationContext` (`IAiActivityGenerator.cs`).
- `ActivitySubmitHandler` now passes both fields (formatter + resolver output) into evaluation context.
- `AiActivityGeneratorHandler.EvaluateAttemptAsync` now includes `learnerPreferences` and `learningGoalContext` in prompt variable dict.
- AI evaluation for `WritingScenario` and `SpeakingRolePlay` can now reflect student difficulty preference, support language, and learning goals.

**Fix P2 — vocabulary cadence unconditionally routed to `GapFillWorkplacePhrase`**
- `ActivityGetHandler` vocabulary cadence pick now gates on `WorkplaceSpecific` from `ILearningGoalContextResolver`.
- Non-workplace students receive `PhraseMatch`; workplace students receive `GapFillWorkplacePhrase`.
- A student with Day-to-day English / Travel / Social goals will no longer receive workplace-labelled vocabulary by default.

**Fix P3 — `PreferredSessionDurationMinutes` absent from batch generation summary**
- Added `preferredSessionDurationMinutes` to `learnerPreferences` object in `LessonBatchGenerationJob.BuildCompactSummaryAsync`.
- AI lesson planner now receives session length preference as a hint.

**Tests**
- 20 new unit tests in `PreferenceEnforcementTests.cs`.
- Covers: general English fallback, workplace/non-workplace goal gates, formatter output for goals/focus/support language/difficulty, `CurriculumContextMapper` null guard, vocabulary cadence pattern key selection.

### Gates at completion
- Backend: 1098 unit + 555 integration + 3 architecture = 1656 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed.

### What is intentionally NOT in this phase
Full 10L CEFR-aware routing, exercise format locking, readiness pools, background replenishment lifecycle, Practice Gym suggested UI redesign, admin curriculum write UI, StudentProfile.CefrLevel migration, plus-level routing, full placement engine.

See: `docs/reviews/2026-06-17-phase-10k-f-profile-preference-enforcement-review.md`

---

## Previously most recently completed sprint

**Phase 10K — Curriculum Boundary / Level Syllabus Foundation** - complete (2026-06-17)

Phase 10K introduces the curriculum syllabus data model and query foundation. It defines what the system is recommended to teach at each CEFR level, skill, learner goal/context, and focus area. No CEFR-aware routing, readiness pools, or background generation were implemented — this phase is foundation only.

### What was built

**Domain constants**
- `CefrLevelConstants` — canonical A1/A2/B1/B2/C1/C2 string constants with `IsValid()` helper.
- `CurriculumSkillConstants` — canonical skill identifiers (writing, reading, listening, speaking, vocabulary, grammar, pronunciation, fluency, confidence).
- `CurriculumContextTagConstants` — canonical learner context tags (general_english, day_to_day, travel, study_academic, migration_settlement, job_interviews, social_conversation, workplace, pronunciation, listening_confidence, writing_confidence, exam_inspired, custom). `workplace` is one tag among many, not the default.

**Domain entity**
- `CurriculumObjective` — single flat entity with: Key (stable), Title, Description, CefrLevel, PrimarySkill, SecondarySkillsJson, ContextTagsJson, FocusTagsJson, PrerequisiteKeysJson, RecommendedOrder, DifficultyBand (1-5), IsActive, IsReviewable, IsExamInspired, TeachingNotes.
- Full constructor validation: invalid CEFR rejected, invalid skill rejected, self-prerequisite rejected, DifficultyBand 1-5 enforced.
- `Activate()` / `Deactivate()` / `UpdateDetails()` methods.

**Persistence**
- `CurriculumObjectiveConfiguration` — EF Core config, snake_case columns, unique index on Key, composite index on (cefr_level, primary_skill, is_active).
- Migration `T50_CurriculumSyllabusFoundation` — creates `curriculum_objectives` table.
- `CurriculumObjectiveSeeder` — 22 starter objectives across A1/A2/B1/B2, all major skills, multiple learner contexts. Upserts on Key (idempotent). Post-seed prerequisite integrity validation.

**Application layer**
- `ICurriculumSyllabusQuery` — query interface: GetActiveObjectives, GetByCefr, GetByCefrAndSkill, GetByCefrAndContext, GetByCefrAndFocusArea, GetPrerequisites, GetCandidatesForStudent, GetByKey.
- `CurriculumContextMapper` — static null-safe mapper from `ResolvedLearningGoalContext` to curriculum context tags. Null input returns `[general_english]`. Non-workplace context never defaults to workplace.

**Infrastructure**
- `CurriculumSyllabusQueryService` — implements `ICurriculumSyllabusQuery` using EF Core with `AsNoTracking()`. `GetCandidatesForStudent` returns ordered candidates only — does NOT select activities or formats.

**API**
- `AdminCurriculumController` — read-only: `GET /api/admin/curriculum/objectives` (with optional `cefrLevel` and `skill` filters), `GET /api/admin/curriculum/objectives/{key}`. Admin-only. No write endpoints.

**Tests**
- Unit: 22 tests in `CurriculumObjectiveTests` and `CurriculumContextMapperTests`.
- Integration: 14 tests in `CurriculumSyllabusIntegrationTests` — seeder, query service, admin endpoint, regression smoke.

**Docs**
- `docs/reviews/2026-06-17-phase-10k-curriculum-syllabus-eng-review.md`
- `TODOS.md` — 3 deferred items: CEFR plus-levels, StudentProfile.CefrLevel migration, admin curriculum builder.

### Gates at completion
- Backend: all tests passed (architecture + unit + integration)
- Angular unit: 261 passed
- Angular build: clean
- Playwright: 187 passed
- No breaking changes to existing learner-facing behavior

### What is intentionally NOT in this phase
CEFR-aware activity routing, exercise format locking, readiness pools, background generation, Practice Gym suggested practice, admin write UI, StudentProfile CEFR migration, plus-level routing.

---

## Previously completed sprint

**Phase 10J-F — Student App Design System & Responsive UI Foundation** - complete (2026-06-17)

Phase 10J-F refactors the student-facing Angular UI into a more maintainable design-system foundation. No product behaviour changed. No backend changes.

### What was built

**Design tokens (styles.css)**
- Added `--sp-brand`, `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, `--sp-sidebar-w-collapsed`, `--sp-content-max`, `--sp-content-max-desktop`, z-index layer tokens.
- Added `sp-card-hover` utility class (was used but missing).
- Added `sp-pref-chip` / `sp-pref-chip--on` — centralised preference chip class replacing scattered inline chipStyle() strings.
- Removed duplicate `sp-bottomnav` / `sp-navbtn` definition (kept in component CSS only).

**Profile page**
- Replaced `chipStyle()` inline style method with `sp-pref-chip` CSS class bindings.
- Added `aria-pressed` to all chip buttons (learning goals, focus areas, session length, difficulty).
- Added `data-testid` per chip for test targeting.
- Added `focus-visible` outline for keyboard accessibility.

**Progress component**
- Replaced all hardcoded hex color values with design tokens (`--sp-success`, `--sp-warn`, `--sp-writing-ink`, `--sp-success-soft`, `--sp-warn-soft`, `--sp-speaking`, `--sp-canvas2`, `--sp-muted`).
- Fixed `var(--sp-writing-bg)` and `var(--sp-warn-bg)` references (those tokens don't exist).

**Practice Gym CSS**
- Replaced all `var(--sp-primary)` references with `var(--sp-brand)`.

**Shared student UI components**
- `StudentChipComponent` (`sp-chip` selector) — reusable toggle chip with `selected`, `disabled`, `toggle`.
- `StudentBadgeComponent` (`sp-badge` selector) — reusable badge with variant input.
- `src/app/shared/student-ui/index.ts` barrel export.

**Tests**
- Angular: 261 tests, 261 passed.
- Playwright: 187 tests, 187 passed (12 new in `e2e/design-system-10jf.spec.ts`).
- Backend: 1565 passed across all suites.

---

## Previously completed sprint

**Phase 10I - Configurable Multi-step Onboarding / Assessment v2 Foundation** - complete (2026-06-17)

Phase 10I introduces the v2 onboarding system running in parallel with the existing v1 state machine. Existing students are unaffected. New students receive a configurable, flow-driven onboarding experience with preliminary CEFR scoring.

### What was built

**Domain layer**
- `OnboardingFlowDefinition` entity — versioned, immutable once students have progress; single active flow enforced via PostgreSQL partial unique index.
- `OnboardingStepDefinition` entity — system-required vs admin-configured steps, typed `OnboardingAnswerMapping` enum (serialised as string in DB).
- `StudentOnboardingProgress` entity — per-student progress record with `PreliminaryCefrLevel`; xmin concurrency token; unique `(progress_id, step_key)` constraint on responses.
- `StudentOnboardingResponse` entity — idempotent answer store (upsert pattern).
- `PreliminaryCefrCalculator` — static weighted scoring (A1–C2); never overwrites a real PlacementAssessment CEFR level.

**Persistence layer**
- EF Core configurations for all 4 new entities.
- Migration T47_OnboardingV2.
- `OnboardingFlowSeeder` — idempotent; seeds 10 SystemRequired steps + 1 AdminConfigured disabled step.

**Application layer**
- Contracts: `OnboardingV2Contracts.cs` — student DTOs (no `AssessmentMetadataJson`), admin DTOs, interfaces, commands/queries/results.

**Infrastructure layer**
- `OnboardingV2QueryHandler` — lazy-creates progress; detects v1-complete students and auto-initialises as complete.
- `OnboardingV2StepHandler` — validates and applies answers; percentage counts only SystemRequired+enabled steps.
- `OnboardingV2CompleteHandler` — scores assessment responses; sets `PreliminaryCefrLevel`; calls `SetCefrLevel()` only if CefrLevel is null; transitions lifecycle to `PlacementRequired`.
- `AdminOnboardingFlowQueryHandler` — read-only GET, excludes `AssessmentMetadataJson`.

**API layer**
- `OnboardingController` extended: `GET /api/onboarding`, `POST /api/onboarding/steps/{stepKey}`, `POST /api/onboarding/complete`.
- `AdminOnboardingController` added: `GET /api/admin/onboarding/flow` (Admin role only).
- `OnboardingFlowSeeder` wired into app startup after `ExerciseTypeDefinitionSeeder`.

**Angular layer**
- `onboarding-v2.models.ts` — TypeScript interfaces for all v2 DTOs.
- `OnboardingV2Service` — `getStatus()`, `submitStep()`, `complete()`.
- `OnboardingV2Component` — shell with progress bar, dynamic step dispatch by `stepType`.
- 11 step renderer components: Welcome, PreferredName, SupportLanguage, LearningGoals, FocusAreas, DifficultyPreference, SingleChoice, MultipleChoice, FreeText, AssessmentQuestion, Summary.
- Route: `/onboarding/v2` added to `app.routes.ts`.

### Tightening rules applied (all 10)

1. `PreliminaryCefrLevel` stored separately — real `CefrLevel` only updated when null.
2. `OnboardingAnswerMapping` typed enum, not raw string keys.
3. Flow versions immutable once students have progress; admin edits create new versions.
4. Single active flow enforced by DB partial unique index.
5. Student API never exposes `AssessmentMetadataJson`, correct answers, or scoring weights.
6. Percentage counts SystemRequired+enabled steps only.
7. Post-onboarding lifecycle → `PlacementRequired`.
8. Unique `(progress_id, step_key)` constraint on `StudentOnboardingResponse`.
9. v1 `OnboardingStatus`/`OnboardingStep` fields preserved as legacy compatibility.
10. Documented limitations: no full CEFR engine, no admin visual builder, no curriculum routing.

### Final test counts (CI green)

- Backend: 863 unit + 511 integration passed (net gain from v2 handler tests).
- Angular: production build clean (CSS warnings pre-existing, no errors).
- Architecture tests: not rebuilt (build artefact absent; unit/integration cover coverage).

Known limitations: no Playwright E2E spec for v2 onboarding flow (no test user seeded with v2 progress); no admin visual flow builder; preliminary CEFR is a simple weight band, not a full adaptive engine; no curriculum routing post-onboarding.

---

## Previously most recently completed sprint

**Phase 10H-F - Practice Gym Playwright Fixture Stabilisation** - complete (2026-06-17)

Phase 10H-F restored the full Playwright suite as a reliable gate after the
catalog-driven Practice Gym UI and related renderer/result copy had moved ahead
of older E2E fixtures.

### Failure categories found

- Selector drift: tests still looked for old fixed Practice Gym card IDs such
  as `practice-card-listening` and `speaking-card`.
- Fixture/test data drift: Practice Gym tests did not mock the current exercise
  type catalog shape, including planned non-runnable AI role play rows.
- Copy/label drift: the landing hero and perfect-score feedback labels had
  intentionally changed.
- Shared component selector drift: listening fallback text moved into the shared
  `app-audio-player` unavailable state.

No backend/API failures, timing issues, real UI regressions, or environment-only
failures remained after the fixes.

### What was fixed

- Practice Gym E2E tests now use current catalog-driven `practice-format-*`
  selectors.
- Practice Gym mocked exercise-type data now includes ready runnable formats and
  planned locked formats.
- Practice Gym routing assertions now verify activity startup through
  `activityId` navigation instead of obsolete module-link assumptions.
- Listening renderer tests now assert the shared `audio-unavailable` fallback.
- Landing and score-band assertions now match current user-facing copy.

### Final test counts

- Targeted affected Playwright specs: 62 passed.
- Full Playwright: 175 passed.

Known limitations: none.

---

## Previously most recently completed sprint

**Phase 10H - AI Context Personalisation from Learning Preferences** - complete (2026-06-17)

Phase 10H wires Phase 10G learning preferences into AI generation context for
Today lesson activities, Practice Gym activities, background Practice Gym
materialization, buffered lesson activity materialization, and lesson batch
planning summaries.

### What was added

- `LearnerPreferenceContextFormatter` for compact, bounded preference context.
- AI prompt rendering inserts learner preferences before JSON return instructions.
- Generation context now carries `LearnerPreferenceContext` and `LearningGoalContext`.
- Ledger events now store `LearningGoalContext` when a clear value exists.
- Dynamic pattern selection uses preference-backed goal context before legacy fallback.
- Practice Gym generic background topic fallback changed to "English class practice".

### Tests added

- Formatter tests for included fields, missing preferences, excluded admin fields,
  CEFR system-estimated wording, goal fallback order, and no workplace default.
- Prompt builder tests for learner preference insertion and token-budget failure.

See: `docs/reviews/2026-06-17-phase-10h-ai-context-personalisation-from-learning-preferences.md`

---

## Previously most recently completed sprint

**Phase 10G — Student Profile & Learning Preferences v2** — complete (2026-06-17)

Extended student profile with editable learning preferences: preferred name, support language, translation help, learning goals (multi-select + custom), focus areas (multi-select + custom), difficulty preference. New API endpoints `GET /api/profile` and `PUT /api/profile/preferences`. Angular profile page redesigned with 6 sections. CEFR remains read-only.

### What was added

- `TranslationHelpPreference` and `DifficultyPreference` enums (Domain)
- `UpdateLearningPreferences` method on `StudentProfile` — student-editable only, validates constraints
- EF migration T46 — 10 new columns on `student_profiles`, JSON columns for lists
- `GetStudentProfileQuery` + handler (Application + Infrastructure)
- `UpdateLearningPreferencesCommand` + handler (Application + Infrastructure)
- `ProfileController` with `GET /api/profile` and `PUT /api/profile/preferences`
- `ProfileService` in Angular with `getProfile()` and `updatePreferences()`
- Profile page rewritten: 6 sections (Account, Level, Goals, Focus, Support language, Preferences)
- CEFR shown read-only with per-level explanation text

### Final test counts

- Backend unit: 996 (was 985)
- Backend integration: 539 (was 534)
- Architecture: 3
- Angular unit: 243 (was 229)

See: `docs/reviews/2026-06-17-phase-10g-student-profile-learning-preferences-v2.md`

---

## Previously most recently completed sprint

**Phase 10D — Activity Quality / Workload Validation** — complete (2026-06-17)

Added quality and workload validation to ensure generated activities are meaningful and feedback wording matches the student's score.

### What was added

- `WorkloadModeRegistry` in `ModuleStageContentValidator` — classifies pattern keys as `SingleSubstantialTask` (one item is the full exercise) or `MultiItem` (multiple items expected).
- `EnforceWorkloadSanity` method — fires when `countSettings` is provided; fails multi-item formats with item count below `MinItemsPerPractice`.
- Extended `ItemCountArrayByPattern` — added `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`, `phrase_match` entries.
- `ExerciseTypeDefinitionSeeder.CountOverrides` — added `MinItemsPerPractice >= 2` for `phrase_match`, `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`.
- Score-aware feedback in `PatternEvaluationResultComponent`: four-tier `scoreBandLabel`, `scoreRingColour`, new `scoreBandInstruction()`, `showImprovementPrompt` getter.
- 100% score no longer shows "Improve your answer" or "Review the corrections".

### Tests added

- 23 backend unit tests: workload validation, single-substantial-task exemptions, registry classification, item-count config enforcement, no-workplace-default check.
- 25 Angular unit tests: score-aware labels at all four tiers, 100% wording contract, `showImprovementPrompt` logic, ring colour.

### Final test counts

- Backend unit: 974 (was 951)
- Backend integration: 534
- Architecture: 3
- Angular unit: 229 (was 204)

See: `docs/reviews/2026-06-17-phase-10d-activity-quality-workload-validation.md`

---

## Previously most recently completed sprint

**Phase 10C — Ledger-aware Dynamic Pattern Selection** — complete (2026-06-17)

See commit `e01680a`.

---

## Previously completed sprint

**Phase 10B — Student Learning Memory / Taught-Content Ledger** — complete (2026-06-17)

New structured `StudentLearningEvent` ledger written after every activity submission from Today lessons and Practice Gym. Foundation for ledger-aware dynamic pattern selection.

### What was added

- `StudentLearningEvent` domain entity + `LearningEventSource` / `LearningEventOutcome` enums
- EF migration `T45_StudentLearningEvents` (new `student_learning_events` table with 3 indexes)
- `IStudentLearningLedger` application interface + `StudentLearningLedgerService` infrastructure implementation
- Hooked into `ActivitySubmitHandler` at both pattern evaluation path and legacy AI path
- Query helpers: `GetRecentAsync`, `GetRecentPatternKeysAsync`, `GetWeakEventsAsync`, `GetRecentByPatternKeysAsync`
- Best-effort write — never blocks or fails student's activity submission
- No workplace default forced when context is null

### Tests added

- 10 unit tests: `StudentLearningEventTests` (domain entity validation, field storage, no-workplace-default)
- 9 integration tests: `StudentLearningLedgerServiceTests` (write/read/query/isolation)
- 4 API integration tests: ledger event written from Practice Gym and Today lesson, exercise type/pattern key captured, skill profile update still works

### Final test counts

- Backend unit: 941 (was 931)
- Backend integration: 531 (was 517)
- Architecture: 3

See: `docs/reviews/2026-06-17-phase-10b-student-learning-memory-ledger.md`

---

## Previously most recently completed sprint

**Phase 9J — Speaking/Listening Family QA & Hardening** — complete (2026-06-16)

Performed end-to-end QA and hardening across all speaking/listening formats from Phases 9A–9I.

### What was fixed
- **P0 bug:** `summarize_group_discussion` submission serialization was missing from `activity-lesson.component.ts`. The `else` fallback was serializing the entire payload object instead of `{ items: [...] }`, causing malformed `SubmittedAnswerJson` to reach the evaluator.
- **Stale comment** in `ExerciseTypeDefinitionSeeder.CountOverrides` incorrectly labelled Phase 9 speaking formats as "planned, non-runnable". Updated to "Speaking (Ready)".

### Tests added
- 3 backend unit tests: `summarize_group_discussion` AiOpenEnded evaluator coverage (high score, low score, compactContent exclusion).
- 23 Angular unit tests: new `exercise-renderer.component.spec.ts` covering renderer dispatch, content extraction, submission payload shape, and audioScript fallback for all 7 Phase 9 formats.

### Final test counts
- Backend unit: 907 (was 904)
- Backend integration: 517
- Architecture: 3
- Angular: 204 (was 181)

See: `docs/reviews/2026-06-16-phase-9j-speaking-listening-family-hardening.md`

---

## Previously most recently completed sprint

**CI/CD stabilization (post Phase 7A-7D)** — complete (2026-06-15)

Fixed all 27 failing integration tests (token budget regressions from
module_stage_v1 prompt growth + FakeAiProvider fixture missing
pattern-specific exerciseData keys) and the empty
`LinguaCoach.ArchitectureTests` project (added real NetArchTest layer-boundary
tests). Backend: 472/472 integration+unit tests passing, 3/3 architecture
tests passing. Angular: 103/103 unit tests passing, dev/prod builds succeed.
See `docs/reviews/2026-06-15-ci-cd-stabilization-review.md`.

## Previously completed sprint

**Activity 3-page restructure (Teach / Practice / Feedback), full-stack** — complete (2026-06-13)

5-step strangler-fig migration, all steps landed:

- **Step 1** — split `ActivityLessonComponent` (876-line monolith) into a thin
  orchestrator shell + 3 composed page components (`ActivityTeachPageComponent`,
  `ActivityPracticePageComponent`, `ActivityFeedbackPageComponent`). Zero
  behavior change.
- **Step 2** — added `ActivityPagePresenter` interface + `PatternBackedPresenter`
  / 4 `Legacy*Presenter` bridges, selected by `ActivityPresenterFactory.for(activity)`.
  `TeachViewModel`/`PracticeViewModel` replace boolean-flag `@Input()`s.
- **Step 3** — `gap_fill_workplace_phrase` hint parity; `VocabularyPractice`/
  `ListeningComprehension` cadence picks now route through
  `HandlePatternKeyedAsync`. See
  [2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md](2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md).
- **Steps 4 & 5** — `WritingScenario`/`SpeakingRolePlay` cadence picks now
  route through `open_writing_task`/`speaking_roleplay_turn`; new
  `AudioResponse` interaction mode; `/speaking-attempt` is pattern-aware
  (new pattern → `IPatternEvaluationRouter`/`AiOpenEndedEvaluator`, legacy →
  unchanged `SpeakingRolePlayEvaluator`). No frontend changes needed — existing
  speaking recording UI is activity-type-keyed and already serves
  `AudioResponse`. See
  [2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md](../reviews/2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md).

Eng plan: [2026-06-13-activity-3-page-restructure-eng-plan.md](../reviews/2026-06-13-activity-3-page-restructure-eng-plan.md).

`dotnet build`/`ng build` clean, 51/51 unit tests pass.

**Follow-ups (not blocking, tracked separately):**
- Live-AI calibration pass for the 4 new prompts (`open_writing_task`,
  `speaking_roleplay_turn` generate/evaluate) — output unverified against a
  live AI provider.
- Retire `LegacyVocabPresenter`/`LegacyListeningPresenter`/
  `LegacyWritingPresenter`/`LegacySpeakingPresenter` + their template branches
  once production activity rows are regenerated under the pattern engine
  (gated on production data review).

---

## Previously completed sprint

**Practice Gym cache race condition fix** - complete (2026-06-12)

A full code/docs audit found `ActivityGetHandler.TryAssignReadyPracticeCacheAsync` could
let two concurrent `GET /api/activity/next?pattern=...` requests claim the same `Ready`
`PracticeActivityCache` row, returning the same `LearningActivity` to both. Added `xmin`
concurrency token to `PracticeActivityCache` (mirrors existing `LearningPath` config) and
retry-with-exclusion on `DbUpdateConcurrencyException`, falling back to the next ready
row or on-demand generation. 483 unit + 434 integration tests pass. See
`docs/reviews/2026-06-12-practice-gym-cache-race-condition-fix-engineering-review.md`.

**Lesson and Practice classroom alignment** - complete (2026-06-12)

Today's Lesson now consumes ready buffered `LearningSession` rows before falling
back to lazy session generation. The lesson page no longer auto-prepares later
steps just because the student opened the page, selected a step, or completed the
previous step. Existing prepared steps still open directly.

Practice Gym now uses the same classroom framing as Today's Lesson: teach first,
practice with a supported exercise pattern, then show feedback after submit. The
Practice Gym page separates skill practice (Listening, Reading, Speaking,
Writing), vocabulary class, exercise type practice, and future live role play.
Only implemented patterns are active; Reading, vocabulary queue, live AI role
play, and future exercise types remain disabled.

Background Practice Gym caching now has both parts: `PracticeGymBufferRefillJob`
queues pending cache rows for eligible active students, and
`PracticeGymGenerationJob` materializes those rows into ready `LearningActivity`
records. `GET /api/activity/next?pattern=...` consumes ready cache entries before
generating on demand. Phrase-match and gap-fill generation prompts now require a
lesson component before practice, and lesson batch planning is constrained to the
active pattern library instead of defaulting every skill to writing.

**Lesson batch materialization fix** - complete (2026-06-12)

Production logs showed AI lesson planning succeeding, then
`LessonBatchGenerationJob` failing while saving the first generated session with
`DbUpdateConcurrencyException`. The failed save also left the same `DbContext`
dirty, so marking the batch failed could throw again and Quartz reported an
unhandled job exception. Root cause: session/activity `GenerationJobItem` rows
created after the original batch save were attached only through the aggregate's
private item collection, so EF could treat them as updates to missing rows
instead of inserts. The job now explicitly inserts those items, avoids tracked
path/module state during generated module lookup, and clears failed tracked
state before marking a materialization failure. Added integration coverage that
executes `LessonBatchGenerationJob` with a fake lesson-plan provider and verifies
a completed batch plus ready sessions. See
`docs/reviews/2026-06-12-lesson-batch-materialization-fix-engineering-review.md`.

**Admin responsive header polish** — complete (2026-06-12)

`/admin` now keeps dashboard grids tablet-safe between 900px and 930px, where the
desktop sidebar first appears. The admin header now shows only the avatar button;
email and role moved into the avatar flyout menu. Related layout rules are
documented in `docs/architecture/frontend-layout-system.md`.

**Admin stuck batch cancellation** — complete (2026-06-12)

Admins can now cancel queued/running lesson-generation batches from
`/admin/integrations` instead of running one-off SQL. The action marks the batch
`Failed` with the safe reason `Cancelled by admin.` and the background job checks
for that state before and during session materialization so it does not overwrite
an admin cancellation. Recent batches now show a Cancel button for active rows and
the existing Failure column shows the cancellation reason.

**Lesson batch generation concurrency fix** — complete (2026-06-12)

Duplicate concurrent batch triggers for the same student caused
`DbUpdateConcurrencyException` and left `GenerationBatch` rows stuck in
"Running" forever. Admin endpoint now returns 409 if a batch is already
running for the student; job marks itself `Failed` on any unhandled error
during materialization instead of getting stuck. Existing stuck rows can now be
cancelled from Admin Integrations rather than fixed by direct SQL. See
`docs/reviews/2026-06-12-admin-stuck-batch-cancel-engineering-review.md`.

**Quartz JobDataMap string-only fix (background lesson generation)** — complete (2026-06-12)

`LessonBatchGenerationJob.TriggerAsync` stored non-string values in `JobDataMap`,
which throws `JobPersistenceException` under Quartz's `UseProperties = true`
Postgres job store. This broke both the admin "Generate lessons now" button AND
the background buffer-refill pipeline — explaining why all lessons were
generated on-the-fly ("Preparing your lesson...") instead of pre-generated. See
`docs/reviews/2026-06-12-quartz-jobdatamap-string-fix-engineering-review.md`.

**Admin "Generate lessons now" button fix** — complete (2026-06-12)

Button gave no feedback on click (success or error). Frontend now shows a
confirmation with queued session count, or the server error message. See
`docs/reviews/2026-06-12-admin-generate-lessons-button-fix-engineering-review.md`.

**AI Config Overhaul / No-Fallback Rule / Journey Fix** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-11-ai-config-no-fallback-journey-fix-sprint.md`

Triggered by: post-QA audit corrections from product owner (2026-06-11).
Audit report: `docs/testing/deployed-student-e2e-audit-2026-06-11.md`

An audit on 2026-06-12 found that Tracks 1-4 and most of Track 5 had already been
delivered under other sprint names (T36 AiConfigCategories migration, Exercise UX /
Admin Polish, Real TTS). The one genuinely outstanding item — BUG-005 (dashboard streak
showing "--") — was fixed in this pass: `DashboardResult.StreakDays` computed server-side
from consecutive days with an `ActivityAttempt`, wired into the dashboard stat grid and
the header streak pill. See the sprint doc's "Status update" and "Streak implementation"
sections for full detail.

### What this sprint addressed

1. **No-fallback rule** — All AI failures return 503 + "Service not available" UI. No SystemFallback content ever shown to students.
2. **Admin AI Config overhaul** — Replace 12+ individual feature-key rows with 4 LLM category cards (Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path) + 2 independent TTS cards (Listening TTS, Placement TTS).
3. **Journey page fix** — Replace old LearningPath module cards with LearningSession history (date-grouped, per-step scores).
4. **Audio / TTS 503 handling** — Audio endpoint returns clear 404 when TTS not configured; frontend shows graceful failure. Activity audio playback fetches protected audio with Angular `HttpClient` and renders a `blob:` URL.
5. **Lower-severity QA bugs** — Mobile activity blank page, phrase-match 400, streak "--" display, sidebar layout clipping.

---

## Most recently completed sprint

**Exercise Submission Scoring Bug (CRITICAL)** — complete (2026-06-12)

See full review: `docs/reviews/2026-06-12-exercise-submission-scoring-bug-engineering-review.md`

Triggered by a production report: `gap_fill_workplace_phrase` submissions scored 0 with
every `itemResults[].studentAnswer: null`, despite correct `acceptedAnswers` being
present. Escalated by product owner to "every exercise" (not just gap fill).

Root cause: frontend item-id fallbacks (`String(index + 1)`, 1-indexed, unprefixed) did
not match backend deterministic-evaluator key conventions (`gap_{i+1}`, `phrase_{i}`/
`meaning_{i}` 0-indexed).

Fixed:
- `gap_fill_workplace_phrase` — `mapGapItems` fallback id now `gap_${index + 1}`.
- `phrase_match` — two-id-scheme redesign (`phrase_${index}` / `meaning_${index}`) across
  `exercise-renderer.component.ts`, `MatchingPair` interface, `MatchingPairsComponent`,
  and `matching-pairs.component.html`.

Not changed (verified lower risk / separate issue, see review):
- `listen_and_gap_fill` — `ListenAndGapFillItemDto.Id` exists in contract, fallback only
  exercised if AI omits `id`.
- `listen_and_answer` — `QuestionId` mismatch affects per-question feedback labels only,
  not overall AI-judged score.

AI-evaluated patterns (email_reply, teams_chat, spoken_response) were unaffected —
`SubmittedAnswerJson` is forwarded raw to the AI prompt.

Tests: `dotnet test` 480 unit + 430 integration passed; `npm run build` passed.

The separate "lesson structure" complaint (What we learn / Practice / Feedback / Redo →
next, for both Lessons and Practice) raised alongside this bug report is a
product/architecture item — not addressed here, needs its own planning pass with the
product owner.

---

## Most recently completed sprint

**Today's Lesson button / lazy LearningPath generation fix (CRITICAL)** — complete
(2026-06-12)

See full review: `docs/reviews/2026-06-12-todays-lesson-button-fix-engineering-review.md`

Root cause: `SessionGeneratorService` threw when a `CourseReady` student had no active
`LearningPath` (the legacy `/activity` flow was the only place a path got lazily
created). `SessionsController.Today` returned 400, and the dashboard silently
swallowed the error, leaving "Start today's lesson" with a null link — button did
nothing, no session/lesson ever generated for these students.

Fixed: `SessionGeneratorService` now lazily generates a `LearningPath` via
`ILearningPathGenerator` (same handler `ActivityGetHandler` already uses) when none
exists, mirroring the existing fallback there. Dashboard now surfaces session-load
errors instead of swallowing them.

Quartz/background-job config investigated as a possible cause — found correct,
no change needed.

Follow-up implemented same day: `PlacementService` now generates the student's
`LearningPath` proactively right after `CourseReady`, via `ILearningPathGenerator`
(best-effort, never blocks placement). The lazy fallback remains as a safety net
for pre-existing affected students.

Tests: 482 unit + 430 integration passing. `npm run build` passed.

---

## Current priority

**Adaptive Learning Foundation** Phase 2 (numeric `StudentSkillProfile` scores) is done
(2026-06-12) — see `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.
`StudentSkillProfile.ScorePercent` (0-100) replaces the persisted `IsWeak` boolean;
`IsWeak` is now derived (`ScorePercent < 50`). Migration `T42_StudentSkillScorePercent`
backfills existing rows. 482 unit + 430 integration tests pass.

Phase 3 planning pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-plan.md`. Most of Phase 3
(Practice rendering, redo/next loop, per-attempt AI feedback) already exists.

Phase 3 P1 done (2026-06-12): AI evaluation prompts (email_reply, teams_chat,
spoken_response, listen_and_answer) now receive the student's current
`StudentSkillProfile.ScorePercent` for the relevant skill, so `coachSummary` can be
grounded in student progress instead of generic. 482 unit + 430 integration tests pass.

Phase 3 P2 scoping pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-p2-scoping.md`. Full
"What we learn" grammar/vocab/phrases card remains deferred (cross-cutting AI prompt
work). Task 1 (surface existing `teachingNote` as `[goal]` for GapFill/MatchingPairs) and
P2b task 1 (surface learningGoal/skillFocus/targetVocabulary for email_reply/teams_chat)
both implemented same day, frontend-only. See sprint doc's "Phase 3 P2"/"P2b" sections.
suggestedPhrases for spoken_response was already implemented (confirmed
2026-06-12, no change needed). Remaining: targetGrammarPoint (large,
cross-cutting, no schedule).

---

## Adaptive Learning Foundation — vocabulary extraction widened to all activity patterns

**Adaptive Learning Foundation** — planning complete (2026-06-12); first implementation
item (vocabulary extraction) done (2026-06-12)

See `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.

Reviewed and sequenced the remaining tracks (10-14) from the 2026-06-12 product owner
brainstorm: Adaptive Onboarding & Staged Assessment, Configurable Onboarding/Placement,
Multi-Course/Enrolment Model, Estimated Known Words. All confirmed already recorded in
`docs/backlog/product-backlog.md`. Recommended sequencing: vocabulary extraction first
(already speced, independent), then numeric `StudentSkillProfile` scores, then staged
assessment architecture review, then configurable onboarding, then multi-course
(dedicated `/plan-eng-review` required). Three open product questions recorded — see
sprint doc.

Per product owner correction, vocabulary extraction was implemented as a cross-cutting
engine: `VocabularyExtractionService` now extracts from any pattern-evaluated activity
that produces AI `Corrections` (email reply, workplace chat, listen-and-answer, spoken
response), not only legacy writing attempts. Deterministic patterns (gap fill, phrase
match) are unaffected — see implementation note in the sprint doc above. Does not change
current implementation priority below.

---

## Most recently completed sprint

**Exercise UX / Admin Polish** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-12-exercise-ux-admin-polish-sprint.md`

### What was done

All 7 phases shipped on 2026-06-12:

- **Phase 1** — Verified attempt/retry integrity; fixed a pre-existing gap-fill submission shape bug (frontend was sending the wrong JSON shape, causing all answers to score as incorrect).
- **Phase 2** — Workplace Chat: `ChatReplyContent` gained a distinct `learningGoal` field (separate from tone guidance), shown via a `chat-reply-goal` UI element. `activity_evaluate_teams_chat_simulation` prompt updated to evaluate goal-reaching, tone, clarity, and clarification-seeking.
- **Phase 3** — Email Reply: new `InteractionMode.EmailReply`, self-healing `ExercisePatternSeeder`, new `EmailReplyComponent` renderer with subject + body fields, `SubmittedAnswerJson` shape `{ subject, body }`, evaluator prompt updated.
- **Phase 4** — Shared Lesson → Practice → Evaluate framing: new `ExerciseLessonIntroComponent` ("Goal" display) applied to `GapFillComponent`, `MatchingPairsComponent`, `AudioAndFreeTextComponent`, `AudioAndGapFillComponent`. Chat Reply, Email Reply, and Free Text Entry already had equivalent goal displays.
- **Phase 5** — Admin nav: "AI Usage" moved from the (now-removed) "Analytics" group into "AI System", alongside AI Config and Prompts.
- **Phase 6** — Design-token consistency audit of all sprint-touched components — already aligned with `.sp-*` tokens, no changes needed.
- **Phase 7** — Docs close-out (this entry).

### Key constraints preserved

- Lesson → Practice → Evaluate framing applied only to the 6 currently-active exercise renderers, not retrofitted across the 40+ unimplemented patterns in the library.
- No backend changes beyond the `EmailReply` interaction mode (additive, append-only enum).

### Final test results

```
dotnet test tests/LinguaCoach.UnitTests:  477 passed
npm run build:                            passed (0 new errors/warnings)
```

---

## Previously completed sprint

**Real TTS / Placement Onboarding Gap / Today Session Card** — complete (2026-06-11)

See full sprint plan: `docs/sprints/2026-06-10-tts-placement-today-sprint.md`

### What was done

All tracks shipped on 2026-06-11:

- **Track 1 (Real TTS)** — `VoiceName` added to `AiProviderConfig` (T35 migration). `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; never throws. `TtsProviderResolver` reads `tts.listening` / `tts.placement` feature keys from DB, returns `FakeTextToSpeechService` (provider=`fake`) or `OpenAiTextToSpeechService` (provider=`openai`). `ListeningAudioService` and `PlacementAudioService` now resolve TTS at runtime. `DefaultAiSeeder` seeds both keys as `fake/fake/fake` (idempotent). Admin UI updated with voice name field and fake provider support.
- **Track 2 (Onboarding experience step)** — `PATCH /api/onboarding/experience` endpoint added. `StudentProfile.SetExperienceContext()` bypasses state machine. New `step5-experience` Angular component inserted between step-4 and placement. Step-4 now shows "Step 4 of 5" and navigates to step-5. Existing completed students can call the endpoint without error. Non-blocking — API failure still navigates to placement.
- **Track 3 (Today session card)** — previously completed in Practice Gym Activation sprint; confirmed and skipped.

### Key constraints preserved

- `FakeTextToSpeechService` remains default; `dotnet test` does not require `OPENAI_API_KEY`
- OpenAI TTS only activates when admin sets `tts.*` feature key provider to `openai`
- Existing completed students not broken by new experience step
- Practice Gym behaviour unchanged; Pronunciation remains Coming soon

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed (0 errors)
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

---

## Previously completed sprint

**Practice Gym Activation / Pattern-Based Free Practice** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-practice-gym-activation-sprint.md`

### What was done

All phases shipped on 2026-06-10:

- **Phase 2 (backend)** — `GET /api/activity/next` extended with `?pattern=<key>`. `GetNextActivityQuery` has `PreferredPatternKey`. `ActivityGetHandler.HandlePatternKeyedAsync` validates pattern key, loads definition, calls AI with `OverridePromptKey`, sets `ExercisePatternKey` on the created `LearningActivity`. `AiActivityGeneratorHandler` now supports `VocabularyPractice` when pattern-driven. Invalid pattern key returns 400.
- **Phase 3 & 4 (frontend)** — `ActivityService.getNext` accepts `patternKey`. `ActivityLessonComponent` reads `?pattern=` and passes it to the service. Practice Gym activates Phrase Match, Gap Fill, Email, and Workplace Chat as `<a routerLink>` with `pattern=` and `returnTo=/practice`.
- **Phase 5 (return flow)** — `returnTo=/practice` embedded in all four new card links. Existing `nextActivity()` / `backToDashboard()` logic handles it unchanged.
- **Phase 6 (progress verification)** — confirmed: `ActivitySubmitHandler` records `ActivityAttempt` for all pattern types; `PatternSkillUpdateService` runs after each submission; no progress on card open.
- **Phase 7 (tests + docs)** — 8 new backend integration tests; 6 new Playwright tests (4 card activation, Pronunciation still coming soon, Speaking no pronunciation claim). All existing tests still pass.

### Key constraints preserved

- Pronunciation card remains Coming soon
- No fake pronunciation claims
- PatternEvaluationRouter not bypassed
- No new endpoints or routes added
- No seed data deleted, no real user data deleted

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      167 passed
```

---

## Previously completed sprint

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
- Student UX Alignment / Writing-Assumption Cleanup (Phases 1–7) — complete
- Real TTS / Placement Onboarding Gap / Today Session Card — complete
- **Exercise UX / Admin Polish (Phases 1–7) — complete**

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
- **Dynamic Practice Gym session templates** — configurable session templates within Practice Gym (e.g. "30-min vocab session")
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
   Scoping pass done (2026-06-12): see
   `docs/reviews/2026-06-12-dynamic-pattern-selection-scoping.md`. Recommended first
   slice: per-slot pattern pools + last-N-session repetition avoidance in
   `SessionGeneratorService`/`SessionDurationTemplates`.
2. **Dynamic Practice Gym session templates** — configurable multi-exercise sessions within Practice Gym.
3. **Session Reflection AI** — now that evaluation outputs are stable, wire `session_reflection` AI prompt.

---

## Planned future sprint

**Lesson Buffer / MinIO / Background Generation** - planned.

See: `docs/sprints/2026-06-11-lesson-buffer-minio-background-generation-sprint.md`

This sprint covers pre-generating the next 5-10 lessons, pre-generating a configurable 5-10 Practice Gym exercises per type/pattern, storing audio assets in MinIO, signed URL playback, Quartz.NET background generation jobs, Admin Integrations for MinIO health/configuration, and cached Practice Gym generation.

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.

---

## Phase 10J — Learning Goal Context Resolver (2026-06-17)

**Goal:** Normalize `LearningGoalContext` across all generation and ledger paths via a consistent, testable resolver.

**Delivered:**

- `ResolvedLearningGoalContext` value object (`src/LinguaCoach.Application/Learning/`)
- `ILearningGoalContextResolver` interface with `LearningGoalResolutionContext` call context
- `LearningGoalContextResolver` implementation (`src/LinguaCoach.Infrastructure/Learning/`)
- DI registration: `AddSingleton<ILearningGoalContextResolver, LearningGoalContextResolver>()`
- 7 call sites migrated from `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` to resolver:
  - `ActivityGetHandler` (3 sites)
  - `ActivitySubmitHandler` (2 ledger record sites)
  - `ExercisePrepareHandler` (1 site)
  - `PracticeGymGenerationJob` (1 site)
  - `SessionGeneratorService` (1 site)
  - `ActivityMaterializationJob` (1 site)
  - `LessonBatchGenerationJob` (1 site)
- 18 unit tests, 2 integration tests — all pass

**Priority chain (strict order):**
1. `ExplicitGoalOverride` from call context
2. `LearningGoals` + `FocusAreas` (Phase 10G/10I structured fields)
3. `CustomLearningGoal` / `CustomFocusArea`
4. `LearningGoalDescription` → `LearningGoal` → `CareerContext` (legacy)
5. `"general English communication"` — never workplace-only

**Not implemented:** curriculum routing, readiness pools, CEFR routing, background generation.

**Backward compatible:** `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` kept intact. Old ledger records without goal context do not throw.

---

## Phase 10X-J — Admin Wrapper Variant API (2026-06-19)

**Goal:** Make `sp-admin-*` wrapper components robust and flexible so admin pages become consumers of design-system components, not owners of styling/layout logic.

**Delivered:**

- `sp-admin-button`: `appearance` (solid/outline/soft/ghost/link), `size` (xs/sm/md/lg), `fullWidth`, `iconOnly`. Legacy `variant="ghost"` compat alias preserved.
- `sp-admin-badge`: `appearance` (soft/solid/outline), `size` (sm/md), `dot`, `purple` tone added.
- `sp-admin-card`: `variant` (default/bordered/elevated/flat/metric/section), `padding` (none/sm/md/lg), `radius` (md/lg/xl/2xl), `headerDivider`, `hover`, `loading`.
- `sp-admin-stat-card`: `size` (sm/md/lg), unified tone names (primary/success/warning/danger/info/neutral alias the legacy names), `loading` skeleton, `[slot=trend]`.
- `sp-admin-table`: `variant` (basic/data/bordered/striped/simple/card), `density` (compact/comfortable/spacious), `selectable`, `stickyHeader`, column `width`/`align`.
- `sp-admin-filter-bar`: `layout` (inline/stacked/responsive), `density` (compact/comfortable).
- `sp-admin-form-field`: `layout` (vertical/horizontal/inline), `size` (sm/md/lg).
- `sp-admin-input`: `size` (sm/md/lg), `state` (default/error/success/disabled), `fullWidth`. CVA preserved.
- `sp-admin-select`: `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved.
- `sp-admin-textarea`: `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved.
- `sp-admin-modal`: `size` (sm/md/lg/xl/full), `variant` (default/danger/form/confirm), `showCloseButton`. `maxWidth` still works (overrides size).
- `sp-admin-drawer`: `side` (left/right), `size` (sm/md/lg/xl), `closeOnBackdrop`.
- 18 new Phase 10X-J unit tests. 439 Angular tests pass (0 failures).
- Proof usage applied: Students page `variant="data" density="compact"`, modal buttons `size="sm"`. Dashboard stat-cards `size="md" [loading]`, AI System card `variant="metric"`.
- .NET: 1885 tests pass. Angular build clean. Playwright: pending (run from `src/LinguaCoach.Web`).

**Not implemented:** 10R-F usage governance UX, 10U AI Usage redesign, 10V prompt playground, notification platform, enterprise auth/security, observability stack, billing, StudentProfile.CefrLevel migration, full placement engine, full mastery engine.

---

## Phase 10Students-F-C — Targeted Lifecycle Controls (2026-06-19)

**Goal:** Add Pause, Unpause, and Reactivate admin lifecycle controls for students.

**Delivered:**

- `ReactivateStudentCommand`, `PauseStudentCommand`, `UnpauseStudentCommand` records (with `AdminUserId`)
- `IAdminStudentQuery` extended with `ReactivateStudentAsync`, `PauseStudentAsync`, `UnpauseStudentAsync`
- `AdminHandler` implements all three — each writes `AdminAuditLog` entry
- Guard rules: Reactivate requires Archived; Pause rejects Archived/already Paused; Unpause requires Paused
- Reactivate sets `user.EmailConfirmed = true`; Unpause/Reactivate both land on `OnboardingRequired`
- `POST /api/admin/students/{id}/reactivate`, `/pause`, `/unpause` endpoints
- Frontend service: `reactivateStudent`, `pauseStudent`, `unpauseStudent`
- Detail component: context-sensitive buttons (Reactivate when Archived, Unpause when Paused, Pause otherwise)
- Inline confirm modal with cancel/confirm/error/saving state; refreshes student on success
- 9 new integration tests, 16 new frontend tests — all pass
- No migration added (Paused enum value 10 already existed)

**Not implemented:** audit log for Archive (pre-existing gap), window.confirm replacement for Archive/RemovePolicy.

**Test counts:** .NET 680 integration + 1237 unit + 3 architecture = 1920 total. Angular 734.

---

## Phase 10V-0 — AI Pricing Admin Gap Check (2026-06-21)

**Goal:** Audit current AI pricing configuration to define the smallest safe path to admin-manageable pricing.

**Delivered:**

- Full gap audit: pricing source, admin UI gap, persistence gap, risk areas, migration impact.
- Review saved: `docs/reviews/2026-06-21-phase-10v-0-ai-pricing-admin-gap-check.md`
- Confirmed: pricing is config-only (`appsettings.json` via `AiPricingOptions`), not admin-editable.
- Confirmed: `AiConfigCategory` and `AiProviderConfig` entities have no pricing fields.
- Confirmed: zero-cost logs occur silently when model not found in config.
- Recommended design: hybrid config seed + `AiModelPricingOverride` DB table.
- Recommended next: 10V-1 = read-only pricing visibility (no migration), 10V-2 = DB-backed editable pricing.
- No code changed. `git diff --check`: clean.

**Not implemented:** 10V-1 read-only panel, 10V-2 pricing CRUD, 10V-3 zero-cost alert, prompt playground.

---

## Phase 10V-1 — Read-Only AI Pricing Panel (2026-06-21)

**Goal:** Expose config-based AI model pricing in the admin UI for visibility. No migration, no DB writes.

**Delivered:**

- `GET /api/admin/ai/pricing` endpoint — reads all provider pricing from `IConfiguration`, returns `AiModelPricingItem[]`
- `AiModelPricingItem` DTO: `providerName`, `modelName`, `inputPer1KTokens`, `outputPer1KTokens`, `currency`, `source`, `isConfigured`
- `IAdminAiConfigHandler.ListPricing()` added; implemented in `AdminHandler` with `IConfiguration` injection
- Admin AI Config page: Section 4 "Model Pricing" — read-only table grouped by provider, info alert, empty state
- `listAiPricing()` added to `AdminApiService`, `AiModelPricingItem` added to `admin.models.ts`
- 5 new backend integration tests (401/403/200/fields/spot-check price value)
- 8 new frontend unit tests; 2 existing mocks fixed
- No migration. No pricing calculation change. No provider routing change.
- All gates pass: 2046 .NET tests, 880 Angular tests, prod build clean.

**Test counts:** .NET 795 integration + 1248 unit + 3 arch = 2046 total. Angular 880.

**Not implemented:** missing-model detection (10V-3), DB-backed editable pricing (10V-2), prompt playground (10V).

---

## Phase 10V-2 — AI Pricing Override Backend Foundation (2026-06-21)

**Goal:** Add DB-backed AI model pricing overrides with full CRUD API and a pricing resolver service.

**Delivered:**

- `AiModelPricingOverride` domain entity with validation, Update/Deactivate methods, and audit fields
- EF configuration + migration `T53_AiModelPricingOverrides` (table `ai_model_pricing_overrides`)
- `IAiPricingResolver` / `AiPricingResolver`: DB override first, config fallback, null third
- `IAdminAiConfigHandler` extended with 4 pricing override methods
- `AdminHandler` implements: `ListPricingOverridesAsync`, `CreatePricingOverrideAsync`, `UpdatePricingOverrideAsync`, `DeactivatePricingOverrideAsync`
- Endpoints: `GET/POST /api/admin/ai/pricing/overrides`, `PUT/DELETE /api/admin/ai/pricing/overrides/{id}`
- Audit log entries on create/update/deactivate
- Soft-delete (deactivate) pattern consistent with project style
- `IAiPricingResolver` registered in DI
- 13 integration tests + 11 domain unit tests; all pass
- Provider runtime cost calculation NOT changed (deferred to 10V-3 for safety)
- Config fallback preserved; no historical cost recalculation
- No frontend edit UI added

**Test counts:** .NET 810 integration + 1260 unit + 3 arch = 2073 total. Angular 880.

**Not implemented:** provider runtime wiring (10V-3), frontend override UI, zero-cost alert, unique override constraint.

---

## Phase 10V-3 — AI Pricing Runtime Resolver Wiring + Override Management UI (2026-06-21)

**Goal:** Wire `IAiPricingResolver` into all three AI providers for runtime cost calculation, and add override management UI to AI Config page.

**Delivered:**

**Part A — Runtime resolver wiring:**
- `IAiPricingResolver` injected into `OpenAiProvider`, `GeminiProvider`, `AnthropicProvider`
- Direct `AiPricingOptions.GetOpenAiPricing` / `GetGeminiPricing` / `GetProviderPricing` calls replaced with `await _pricingResolver.ResolveAsync(ProviderName, modelToUse, ct)`
- Cost formula: `(inputTokens / 1000m) * resolved.InputPer1KTokens + (outputTokens / 1000m) * resolved.OutputPer1KTokens`
- Missing pricing still logs 0m cost — no throw, unchanged behavior
- No DI changes needed — `IAiPricingResolver` already registered as scoped in 10V-2
- Unit test helpers `NullPricingResolver` / `NullPricingResolverForResolver` added to fix two existing test fixtures

**Part B — Zero-cost visibility:**
- Deferred. Existing null-cost log messages sufficient. Tracked as `TODO-10V-3B`.

**Part C — Frontend override management UI:**
- `AiModelPricingOverrideItem`, `CreatePricingOverrideRequest`, `UpdatePricingOverrideRequest` added to `admin.models.ts`
- `listAiPricingOverrides`, `createAiPricingOverride`, `updateAiPricingOverride`, `deactivateAiPricingOverride` added to `AdminApiService`
- AI Config Section 4 extended: config pricing table (read-only, unchanged) + DB overrides table + inline create/edit form + deactivate with confirm
- Validation: provider required, model required, prices >= 0, effectiveTo after effectiveFrom when provided
- After create/edit/deactivate: signal updated in place (no full reload needed)
- 11 new frontend tests; existing mocks in `admin-wrapper-migration.spec.ts` and `admin-ai-config.component.spec.ts` updated

**Constraints respected:**
- No provider routing change
- No usage governance change
- No historical AiUsageLog recalculation
- No new migration
- No AI Config page redesign

**Test counts:** .NET 810 integration + 1260 unit + 3 arch = 2073 total. Angular 891.

**Not implemented:** zero-cost alert UI (TODO-10V-3B), unique override constraint (TODO-10V-UNIQUE-CONSTRAINT).

---

## Phase 10V-FINAL — AI Pricing Admin Closure Audit (2026-06-21)

**Goal:** Close 10V cleanly — verify 10V-3 commit, fix missing docs, run final audit.

**Delivered:**

- TODOS.md: `TODO-10V-3` marked done; `TODO-10V-3B` (zero-cost alert) added as new deferred item
- `docs/sprints/current-sprint.md`: 10V-3 and 10V-FINAL sections added
- `docs/reviews/2026-06-21-phase-10v-3-ai-pricing-runtime-and-override-ui-review.md`: created in 10V-3
- `docs/reviews/2026-06-21-phase-10v-final-ai-pricing-admin-closure-audit.md`: created in this phase
- All gates verified green before commit

**Remaining AI pricing TODOs:**
- `TODO-10V-3B`: zero-cost alert UI in AI Usage or AI Config
- `TODO-10V-UNIQUE-CONSTRAINT`: optional unique index on `(ProviderName, ModelName, EffectiveFromUtc)`

---

## Phase 20E — Controlled Student Pilot Smoke QA (2026-07-02)

**Goal:** Use the Phase 20D readiness/repair tooling to prove one real
student can complete the intended learning flow end-to-end without
developer hand-holding. Ran against production, by explicit user decision.

**Delivered:**

- Created a real pilot student via the admin Create Student flow
  (`pilot.student.20e@speakpath.app`) and walked login → forced password
  change → onboarding (5 steps) → placement → dashboard/journey/practice/
  progress/profile by hand.
- **P0 found, fixed:** `StudentProgressSummaryHandler` and
  `AdminStudentProgressHandler` raced `Task.WhenAll(...)` across loaders
  sharing one scoped `DbContext` — a genuine EF Core concurrency bug that
  leaked a raw exception message to the student on `/progress`. Fixed by
  awaiting sequentially in both handlers.
- **P2 found, fixed:** 4 more instances of the Phase 15H UTF-8 mojibake
  bug on the pilot path (onboarding step 5 experience labels, activity
  feedback retry text, CEFR assessment prompt, onboarding-v2 summary).
- **P0 found, NOT fixed (documented only, by explicit user decision):** a
  production-only `PostgresException` blocks `POST /api/student/placement/start`
  and several related endpoints for every student, plus two recurring
  background job failures. This is the reason the golden path could not be
  walked past placement, and the reason the Phase 20D readiness audit
  itself could not be exercised in this session (its own endpoint 500s).
  Filed as `TODO-20E-1` (urgent, needs production DB/log access).
- New `docs/pilot/student-pilot-runbook.md` — how to create/select a pilot
  student, run the readiness audit, dry-run and real repairs, which routes
  to check, pass/fail criteria, known limitations, and a final "ready to
  invite one student?" checklist (currently: **No**).

**What is NOT changed:**

- No AI scoring, CEFR update, objective-completion, or Learning Plan
  regeneration behavior changed.
- No runtime setting changed in production.
- No existing student's data modified; no attempts/submissions/evaluations
  deleted anywhere.
- Phase 20D's readiness/repair logic itself was not modified.

**Test coverage:** 1,750 backend unit (unchanged), 1,378 backend
integration (unchanged), 3 architecture tests all pass. 1,548/1,668
Angular unit tests pass (120 pre-existing failures, unchanged baseline; 0
new regressions). Production build clean. No new Playwright smoke — the
intended golden-path flow cannot complete against production until
`TODO-20E-1` is fixed.

**Final verdict:** Not ready for a controlled student pilot. Blocked on
`TODO-20E-1`.

Review: `docs/reviews/2026-07-02-phase-20e-controlled-student-pilot-smoke-qa-review.md`.

---

## Phase 20F — Production Placement/Readiness P0 Unblocker (2026-07-02)

**Goal:** Diagnose and fix the production `PostgresException` blocking
placement start, the Phase 20D readiness audit, and two background jobs
(`TODO-20E-1`), without any new features, UI redesign, or destructive
production data changes.

**Root cause:** 6 EF Core migration classes (`T62_AdaptivePlacementEngine`,
`T63_PlacementResponseSubmission`, `T65_SpeakingEvaluationFoundation`,
`T66_SpeakingEvaluationAppliedSignal`, `T67_WritingEvaluationTables`,
`T68_WritingEvaluationAppliedSignal`) had no `.Designer.cs` companion
file — the file carrying the `[Migration("id")]` attribute EF Core's
discovery relies on. Without it, a migration is silently invisible to
`dotnet ef database update`/`Database.Migrate()` on every environment,
forever — not a failure, just never applied. Compounded by 3 pairs of
migrations independently creating the same table, latent because the
"invisible" half of each pair had never run anywhere.

**Delivered:**

- Added the 6 missing `.Designer.cs` files.
- Made 5 affected migrations' `Up()` idempotent (`ADD COLUMN`/
  `CREATE TABLE`/`CREATE INDEX ... IF NOT EXISTS`) so whichever migration
  of a duplicate pair runs first doesn't conflict with the other, in any
  environment's specific migration history.
- New regression test `tests/LinguaCoach.ArchitectureTests/MigrationDiscoveryTests.cs`
  — reflects over every `Migration`-derived class and asserts each has a
  `[Migration]` attribute. Caught a real bug on its first run
  (`T68_WritingEvaluationAppliedSignal`, missed by manual review).
- Validated against a from-scratch fresh local Postgres (all 64
  migrations apply, 0 errors) and a local sandbox independently drifted
  to match production's exact symptom pattern (previously-invisible
  migrations now apply; `POST /api/student/placement/start` → 201;
  `GET /api/admin/students/{id}/readiness` → 200; both previously-failing
  background jobs now complete with `Failed=0`).
- No production DB/SSH/log access was available in this session — the
  fix was pushed via the normal `main` → CI/CD deploy pipeline (`gh run
  28570470545`, ~2.5 min), which ran `Database.Migrate()` against the real
  production database automatically on next API startup.
- **Confirmed live against `https://speakpath.app` immediately after
  deploy:** readiness audit for `pilot.student.20e@speakpath.app` → 200
  (was 500); `POST /api/student/placement/start` → 201 (was 500);
  placement UI rendered and answered a real Question 1; zero errors in
  production diagnostics in the 15-minute window spanning the deploy and
  this check. `TODO-20F-1` resolved.

**What is NOT changed:**

- No AI scoring, CEFR update, objective-completion, or Learning Plan
  regeneration behavior changed.
- No application/business-logic file changed — only migration files and a
  new regression test.
- No attempts/submissions/evaluations deleted or modified anywhere.
- No `DROP` or destructive SQL anywhere in this fix; every change is
  additive-and-idempotent by construction.

**Test coverage:** 1,750 backend unit (unchanged), 1,378 backend
integration (unchanged — SQLite `EnsureCreated()`-based tests don't
exercise migration files, which is exactly why this bug was invisible to
CI), 5/5 architecture tests (+2 new). No frontend code changed this phase.

**Final verdict:** Root cause fixed and **confirmed live in production.**
The Phase 20E pilot student can resume the walkthrough from placement
onward.

Review: `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.

---

## Phase 20G — Live Student Pilot Golden Path Completion (2026-07-02)

**Goal:** Resume the live pilot walkthrough and complete the real student
golden path — placement through activity completion, feedback, Practice
Gym, Journey, Progress, and Profile — against production.

**Delivered — all live, against `https://speakpath.app`:**

- Completed the pilot student's placement assessment in full (19 adaptive
  items) → CEFR **B2**, real Learning Plan generated (5 objectives).
- **P0 found + fixed live:** gap-fill activities rendered with zero
  fillable blanks — `ExerciseRendererComponent.gapFillContent` never
  unwrapped `practiceContent.exerciseData` for pattern-engine content.
  Fixed, deployed, reloaded live — real content + word bank rendered.
- **Completed the activity end-to-end**: submitted answers, got scored
  feedback (4/6, 67%) with per-item corrections. Dashboard streak and
  plan progress updated correctly afterward.
- **P0 found + fixed live:** `/api/placement/result` 400'd on every
  dashboard/profile load after placement completion — the adaptive
  completion path never populated the legacy `ResultJson` field the
  handler required. Fixed with an adaptive-aware DTO builder + assessment
  lookup ordering fix.
- **P0 found + fixed live:** `/journey` always showed "complete your
  placement" regardless of real state — the controller passed the JWT
  user ID to a method expecting a StudentProfile ID (different GUIDs).
  Added `GetJourneyForUserAsync` to resolve correctly.
- Validated live: Dashboard, Today, Practice Gym, Journey, Progress,
  Profile, and 7 admin routes (dashboard, students list, student detail,
  lessons, feature gates, AI operations, diagnostics) — all load with
  real data, no crashes.
- **P0 found, documented, NOT fixed:** readiness audit 500s again for the
  pilot student specifically (isolated — confirmed a different student
  returns 200) after placement + activity completion. Root cause not
  identified; needs production DB/log access. `TODO-20G-3`.
- **P1 found, documented:** Practice Gym shows the same suggestion 6
  times (real duplicate backend data, one objective, no diversification).
  `TODO-20G-1`.

**What is NOT changed:**

- No AI scoring, CEFR update, objective completion, Learning Plan
  regeneration, or review scaffold behavior changed.
- No runtime setting changed in production.
- No attempts/submissions/evaluations deleted anywhere.

**Test coverage:** 1,750 backend unit (unchanged), 1,380 backend
integration (+2 new regression tests), 5/5 architecture tests (unchanged).
1,551/1,671 Angular unit tests pass (+3 new; same 120 pre-existing
failures — 0 new regressions). Production build clean.

**Final verdict:** Ready for one controlled student pilot — conditionally
yes. Every student-facing route and the full activity-completion loop
work live. One admin-only regression (`TODO-20G-3`) remains open and does
not block the student experience.

Review: `docs/reviews/2026-07-02-phase-20g-live-student-pilot-golden-path-review.md`.

## Phase 20H — Live Pilot Stabilization: Readiness Edge Case + Practice Gym Deduplication (2026-07-03)

**Goal:** Remove the last two known blockers/risks from Phase 20G before
inviting a real controlled pilot student — the admin readiness audit 500
for `pilot.student.20e@speakpath.app` (`TODO-20G-3`), and duplicate
Practice Gym "Suggested for you" cards (`TODO-20G-1`). Stabilization
only — no new AI scoring, CEFR update, objective completion, Learning
Plan regeneration, activity types, or UI redesign.

**Root cause — readiness audit 500 (`TODO-20G-3`):** 4 of
`StudentReadinessAuditService`'s 10 check-category methods
(`AddPracticeGymChecksAsync`, `AddActivityContentChecksAsync`,
`AddAudioTtsChecksAsync`, `AddFeedbackAndReviewScaffoldChecksAsync`) had
zero exception handling, unlike the other 6 which already caught failures
and converted them into structured `Warning` checks. Any unexpected data
shape in those four crashed the whole audit with a raw 500 for that one
student.

**Root cause — Practice Gym duplicates (`TODO-20G-1`):**
`ReadinessPoolReplenishmentService.FillShortfallAsync`'s duplicate-key
included `PatternKey`, but `PatternKey` is only assigned during
materialization (after an item is queued) — so the queue-time key was
always `(objective, null, cefr)` and could never match a materialized
item's `(objective, "real pattern", cefr)`. Replenishment kept re-queuing
duplicates for the same objective/level forever.

**Fixes applied:**

- Wrapped all 4 unguarded check methods in try/catch matching the
  existing pattern — failures now become a structured `Warning` check
  (`*.check_failed`), never a raw exception; no stack trace or exception
  message leaked, only `ex.GetType().Name` in `TechnicalDetail`. Also
  hardened `AddAudioTtsChecksAsync`'s `AudioAssets` query with an explicit
  null-FK filter.
- Dropped `PatternKey` from `ReadinessPoolReplenishmentService`'s
  `DuplicateKey` — now `(ObjectiveKey, CefrLevel)` only.
- Added defense-in-depth dedupe in `PracticeGymSuggestionService`: a
  single item can never appear in more than one bucket
  (Continue/Review/Suggested), Continue wins ties, caps still applied
  after dedupe.

**New tests:** `StudentReadinessAuditServiceTests.cs` (structured Warning
on collaborator exception), `PracticeGymSuggestionServiceTests.cs` (3
dedupe/cap tests), `ReadinessPoolReplenishmentServiceEffectiveSettingsTests.cs`
(reproduces the bug pre-fix, confirms fixed post-fix),
`AdminStudentReadinessEndpointTests.cs` (integration test reproducing the
exact reported production shape — 49 duplicate Practice Gym readiness
items for one objective, a `speaking` objective linked to a
`ListeningComprehension`-typed activity — asserts 200 with structured
checks).

**Test coverage:** 1,755 backend unit tests pass, 1,381 backend
integration tests pass (+includes the new production-shape reproduction
test), 5/5 architecture tests pass. Angular production build succeeds (no
Angular files touched this phase).

**What is NOT changed:** No AI scoring, CEFR update, objective
completion, Learning Plan regeneration, or review scaffold behavior
changed. No runtime setting changed in production. No
attempts/submissions/evaluations deleted anywhere.

Commits: `4dc49cc` (fix), `8d216fd` (docs), `80cb0eb` (follow-up dedupe-key
fix — see below). Deployed via the existing CI/CD pipeline in two runs
(`28621275227`, `28622255816`), both green.

**Live validation against `https://speakpath.app`, 2026-07-03:** readiness
audit for `pilot.student.20e@speakpath.app` returns 200 with a structured
`activities.check_failed` warning (`technicalDetail: "PostgresException"`)
— direct confirmation the original 500's exact failure mode is now caught
safely. First Practice Gym check post-deploy still showed 6 cards for one
objective, but each had a **distinct** materialized activity id (queued
before the replenishment dedup-key fix caught up) — the suggestion
service's same-activity/same-item dedupe didn't collapse those. Follow-up
commit `80cb0eb` reprioritizes the dedupe key to group by
`(CurriculumObjectiveKey, PatternKey, ActivityType)` first, redeployed,
re-checked: the same 6 items now show 6 distinct patterns/activity types
— zero literal duplicate rows. Dashboard/Today/Journey/Progress/Profile
all return 200 on both deploys. Both `TODO-20G-1` and `TODO-20G-3` are
**confirmed RESOLVED live.** New follow-up `TODO-20H-1` logged for a
separately-scoped observation (Suggested list doesn't diversify across
objectives — not a duplicate-data bug, out of this phase's scope).

**Final verdict: ready to invite one real controlled pilot student — YES.**

Review: `docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`.
