---
status: current
lastUpdated: 2026-07-09 (Phase H1)
owner: product
supersedes:
supersededBy:
---

# SpeakPath Product Backlog

Status labels: `Not started` Â· `Planned` Â· `Blocked` Â· `Done`

Items are grouped by theme. Each item is a discrete unit of work; sub-bullets are acceptance criteria or notes.

---

## Product Model Realignment — Phase H0-H8 `H0-H1 Done, H2-H8 Planned` (2026-07-09)

**Phase H0 (docs-only, done 2026-07-09)** defined the intended product model — `Resource Bank Item
→ Learn Item/Activity → Module → Daily Lesson/Practice Gym → Attempt → Feedback + Rating →
Learner Memory` — and a new H-track. Full detail: `docs/architecture/product-model-realignment-h0.md`.
This does **not** replace or invalidate the Bank-First Admin/Backend Surface Cleanup (G0-G3) or
Practice Gym v2 (PG-v2A-D) tracks below — H1-H8 are additive, sequenced on top of the same
E1-E10/D1-D6 substrate, and G2/G3/PG-v2 remain valid, separately-scoped tracks.

- [x] **Phase H0 — Product Model Realignment** `Done` (2026-07-09, docs-only) — model, import flow,
  unified-Resource-Bank direction (Option B recommended), Learn/Activity/Module/Lesson/Practice-Gym
  field requirements, mismatch audit, target admin IA, H1-H8 roadmap.
- [x] **Phase H1 — Unified Resource Bank Admin Read Model** `Done` (2026-07-09) — one admin-facing
  Resource Bank API/page (`GET /api/admin/resource-bank`, `/admin/resource-bank`) aggregating the
  four existing typed published bank tables; no physical consolidation, no schema/migration; disabled
  "coming soon" Generate Learn/Activity/Module row actions; all four typed pages/APIs/tables
  unchanged and remain fully reachable. +22 backend tests (3,693 total).
- [ ] **Phase H2 — Import Content UX v1** `Planned` — admin upload/paste/import page; AI
  analyze/mapping preview; creates pending Resource Candidates/Bank rows through the existing E1-E9
  pipeline; no student assignment.
- [ ] **Phase H3 — Learn Item Foundation** `Planned` — new `Learn Item` entity/API/admin review;
  generated from selected Resource Bank rows; approval lifecycle.
- [ ] **Phase H4 — Activity Foundation with Form.io** `Planned` — align/extend `Activity` (building
  on `ActivityTemplate`) as an editable generated exercise; approval lifecycle.
- [ ] **Phase H5 — Module Foundation** `Planned` — `Module` = Learn + Activity/Activities + Feedback
  Plan; generated from selected resources/Learn Items/Activities; approval lifecycle.
- [ ] **Phase H6 — Daily Lesson Module Pipeline** `Planned` — Daily Lesson becomes several Modules;
  preserve Today fallback until proven replacement.
- [ ] **Phase H7 — Practice Gym Module Pipeline** `Planned` — Practice Gym becomes skill/weakness
  Module selection; preserve legacy Practice Gym fallback until proven replacement.
- [ ] **Phase H8 — Admin IA Simplification** `Planned` — move technical pages under
  Advanced/Diagnostics; Content Studio becomes the main admin surface.

---

## Bank-First Admin/Backend Surface Cleanup — Phase G0/G1/G2/G3 `Planned`

Plan-Sync-G0 (2026-07-09, docs-only) opened a new roadmap track auditing every admin page, API,
background job, and backend lifecycle concept for consistency with the now-primary bank-first
content model (Resource Banks/Resource Candidates/Activity Templates). This is a **planning/
architecture-cleanup track, not a visual redesign**, and it does **not** delete the per-student
readiness lifecycle — see the treatment decisions below. Sequenced immediately after Phase E7
(done) and before the Phase D3/E8 decision checkpoint; see `docs/roadmap/road-map.md` §19a for
the full phase order and Decision Log for the full rationale. **Phase G0 (the audit itself) is
now done (2026-07-09, docs/audit-only)** — the full surface-by-surface inventory and
classification lives in `docs/architecture/bank-first-admin-backend-surface-audit.md`; G1/G2/G3
below are the implementation phases that act on its findings and have **not** started.
**Plan-Sync-After-G0 (2026-07-09, docs-only) then selected Phase G1 (admin IA cleanup) as the
next implementation phase, ahead of Phase E8 and Phase D3** — G0's highest-value/lowest-risk
findings are all admin-IA quick wins, so making the bank-first model legible in the admin surface
comes first. G2/G3 stay sequenced late (after Phase F); G1 is labels/nav/page-structure only and
must not delete the readiness pool, remove legacy generation, or touch backend
namespaces/entities/routes.

- [x] **Phase G0 — Bank-First Admin/Backend Surface Audit** `Done` (2026-07-09, docs/audit-only)
  - **Delivered**: `docs/architecture/bank-first-admin-backend-surface-audit.md` — inventory +
    classification of 31 admin routes, ~20 controllers, 8 jobs + ~6 services, 11 terminology
    terms, each tagged keep / rename-reframe / move-to-diagnostics / merge / remove-later with a
    P0/P1/P2 priority and a target phase.
  - **No cleanup implemented** — no routes renamed, no code moved, no pages deleted, no
    migrations; docs only.
  - **Confirmed**: the readiness/assignment/delivery lifecycle is load-bearing →
    `StudentActivityReadinessItem` kept, reframed as "Student Activity Assignment / Delivery
    Queue," never deleted.
  - **Headline findings**: P0 `/admin/lessons` conflates delivery-queue health + manual
    generation + buffer settings + diagnostics ("readiness pool health" subtitle); P1 the E7
    reading-passages admin page (`/admin/resource-banks/reading-passages`) is routable but
    missing from the sidebar nav (a G1 safe quick win, deliberately not fixed in G0); P1 the
    "Content" nav section is overloaded.
- [x] **Phase G1 — Admin Information Architecture Cleanup** `Done` (2026-07-09)
  - **Delivered** (labels/nav/page-composition only, nothing deleted): split the overloaded
    "Content" nav into **Content Banks / Delivery / Learning Setup** in both the desktop sidebar
    and the mobile drawer; added the missing E7 reading-passages nav item
    (`/admin/resource-banks/reading-passages`, previously routable but unreachable); reframed
    `/admin/lessons` **in place** (route kept — "Today Delivery Health"; readiness/pool →
    delivery-queue/assignment language; manual generation reframed as AI **fallback** generation;
    new info banner pointing admins to the Content Banks); relabeled the student-detail readiness
    panel ("Readiness pool health" → "Assignment / Delivery Queue health") and the AI Operations
    card; updated 3 spec assertions to match.
  - **Kept, not deleted**: `StudentActivityReadinessItem`, the pool/buffer/materialization jobs,
    `PracticeActivityCache`, and the legacy `IAiActivityGenerator` path. No route/DTO/namespace
    renamed, no backend `.cs` changed. The full `/admin/lessons` **route split** remains deferred
    to G2 (see audit §10).
  - **Validated** by production `ng build` (no new errors; only the pre-existing bundle-size
    budget failure + pre-existing NG8107 warnings in untouched files). Karma not run — a
    pre-existing unrelated TS error in `student/activity/presenters/test-helpers.ts` blocks the
    spec-bundle compile; not fixed here per the phase's no-unrelated-test-debt rule.
- [ ] **Phase G2 — Backend Legacy Surface Cleanup** `Not started`
  - Acts on Phase G0's "remove-later"/"merge" classifications for backend code (jobs, services,
    dead admin API routes); completes the endpoint-by-endpoint sweep G0 flagged as its own
    limitation; decides whether the `Application.ReadinessPool` namespace / `PracticeActivityCache`
    names are worth renaming (only if proven low-risk). No delivery-lifecycle behaviour change.
- [ ] **Phase G3 — Delivery/Bank/AI Diagnostics Consolidation** `Not started`
  - Consolidates the "keep as diagnostics" pieces G0 identified (`AdminAiOperationsController`,
    `AdminGenerationQualityController`, pool/delivery health endpoints, `StudentReadinessAuditService`
    output, generation-validation-failure summaries, AI cost/usage) into one coherent diagnostics
    area anchored on `/admin/diagnostics`.

**Treatment decisions recorded now** (product decisions made in this docs pass, not audit
findings — G0 applies these, it does not re-decide them):

- [x] **`StudentActivityReadinessItem` / `IStudentActivityReadinessPoolService`** `Kept, reframed`
  — kept, not deleted; reframed as "Student Activity Assignment / Delivery Queue" rather than
  "AI-generated activity cache." The lifecycle state machine (selected → assigned → ready →
  reserved → completed → expired/stale/failed) is unchanged.
- [x] **Readiness Pool admin UI** `Relabeled in G1 (2026-07-09)` — admin labels reframed away from
  the "content generation cache" framing (delivery-queue/assignment language on `/admin/lessons`,
  student-detail, and AI Operations). A deeper rework/move to diagnostics remains a G3 concern.
- [x] **"Pool Health" / "Lesson readiness" admin pages** `Relabeled in G1 (2026-07-09)` —
  `/admin/lessons` reframed in place as "Today Delivery Health" with delivery-queue/assignment
  wording; route kept, full route split deferred to G2.
- [ ] **`PracticeActivityCache` and related practice-cache logic** `Not started, deferred` —
  audited in a future phase; may shrink or be removed after PG-v2, but not deleted now.
- [x] **AI generation admin pages** `Scope confirmed` — kept only for fallback generation,
  evaluation, composition, and cost visibility/diagnostics. AI generation is no longer the
  primary content model and should not be presented as such going forward.
- [x] **Resource Banks / Resource Candidates / Activity Templates** `Confirmed primary` — these
  ARE the main admin content surfaces going forward (already exist from Phase E0-E7 and earlier
  Activity Template work).
- [ ] **Stale wording flagged for future removal** `Not started` — "AI-generated activity
  cache," "generated pool as main content source," "random activity cache," "pre-generated
  per-student cache" — flagged for removal in a future docs/UI/admin-label pass where it is
  misleading about the *current* architecture. Historical decision-log entries describing what a
  *past* phase actually built are explicitly exempt and stay as accurate history.
- [x] **Legacy generation paths (freeform `IAiActivityGenerator`, etc.)** `Unchanged — Phase F
  scope reaffirmed` — kept until replacements are proven; retirement stays pattern-by-pattern /
  surface-by-surface in a later cleanup phase, per existing Phase F scope (not changed by this
  item).

---

## Resource Bank — Deferred Publish Targets (Post Phase E4) `Partially resolved (E7)`

Phase E4 (2026-07-08) implemented controlled publishing from staged `ResourceCandidate` rows
into `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/short-excerpt `CefrReadingReference`. Two
candidate-type publish targets were evaluated and deliberately deferred rather than forced
through with a lossy or dishonest shortcut — tracked here as future backlog items. **Published-
bank browsing/search/admin management for the banks E4 can already publish into is done as of
Phase E5** (2026-07-08, `ResourceBankQueryService`, read-only, reverse candidate traceability).
**Phase E6 (2026-07-08) added the first real English content depth**: an original,
internally-authored, English-only seed pack (32 vocabulary / 12 grammar / 10 reading excerpts)
published through the real staging→validation→approval→publish pipeline — no direct-final-table
seeding, no external dataset, no Persian/bilingual content. **Phase D1 (2026-07-08) resolved the
third decision checkpoint by starting D1 itself** — see the "Bank-First Today Composer" section
below. **Phase E7 (2026-07-09) resolved the full-length `ReadingPassage` item below** — see its
entry for what was actually built. `ActivityTemplateCandidate` publishing remains the one
deferred item. Real external-dataset import remains a later, separately-scoped step, gated on
licensing review — not automatically in scope for any future phase unless explicitly re-scoped
when that phase starts.

- [ ] **`ActivityTemplateCandidate` → `ActivityTemplate` publishing** `Not started`
  - **Purpose**: allow an imported/staged Form.io-shaped candidate to become a real, usable
    `ActivityTemplate` (Practice Gym bank-first content), not just the Cefr* reference banks.
  - **Blocker**: `ActivityTemplate` requires a stable unique `Key`, a curriculum-taxonomy-valid
    Skill/Subskill pair, and real hand-authored `GenerationInstructions` prose (required for
    `ActivityTemplateInstanceGenerator` to function). A row staged from a simple CSV/JSON import
    was never designed to carry a curriculum designer's generation instructions.
  - **What would need to change**: either a richer import/staging shape specifically for
    activity-template candidates (capturing generation instructions as a real staged field, not
    inferred or invented), or a distinct admin authoring step between "candidate" and "published
    template" that lets a human write the missing instructions before publish — not a Phase E4
    shortcut.
  - **Out of scope for this item**: inventing placeholder `GenerationInstructions` to force
    existing candidates through (rejected in E4 as dishonest — see
    `docs/architecture/english-resource-bank-import-platform.md`'s E4 section).

- [x] **Full-length `ReadingPassage` → reading-content bank** `Done` (Phase E7, 2026-07-09)
  - **Resolved by**: a new `CefrReadingPassage` entity (migration
    `Phase_E7_AddCefrReadingPassage`), distinct from `CefrReadingReference`, built to hold full
    passage text plus title/summary/tags/word-count/reading-time/attribution/quality metadata.
    `ResourceCandidatePublishService` now routes a `ReadingPassage` candidate by staged-text
    length: ≤500 chars still publishes to `CefrReadingReference` unchanged; over 500 chars
    publishes to `CefrReadingPassage` instead of being blocked. 10 new full-length original
    passages published through the real pipeline. See
    `docs/architecture/english-resource-bank-import-platform.md`'s E7 detail section.
  - **Still deferred from this item's original scope**: `TodayBankResourceSelector` consumption
    of `CefrReadingPassage` (E7 was a pure resource-platform expansion, not a Today-composer
    change — see the "Bank-First Today Composer" section below).

---

## EF Default-Value Audit Follow-Up (Post Bugfix-D1A) `Not started`

Bugfix-D1A (2026-07-08) fixed `LearningSession.GenerationStatus`'s EF default-value bug and
audited every enum-typed `HasDefaultValue(...)` configuration in `Configurations/*.cs` for the
same collision class (a configured default that differs from the property type's CLR default,
combined with application code that can legitimately construct-then-explicitly-set the property
to that CLR default before its first save). No other enum instance was found —
`AdminReviewStatus.NotRequired` and `FormRendererKind.FormIo` both already default to ordinal 0.

- [ ] **Numeric `HasDefaultValue(1)`-style properties not individually call-site-audited** `Not started`
  — `ActivityTemplate.VersionNumber`, `ExerciseTypeDefinition`'s `MinItemsPerPractice`/
  `DefaultItemsPerPractice`/`MaxItemsPerPractice`, `PlacementItemDefinition.DifficultyBand`/
  `ItemVersion` all configure a non-zero DB default for a plain `int` property (CLR default 0).
  The same class of bug would apply if any code path ever explicitly constructs one of these
  entities and sets the property to exactly `0` before its first `SaveChangesAsync`, intending
  that value to persist. Not confirmed as a live bug — lower priority than the enum case since
  none of these fields' "0" value carries an obviously distinct real-world meaning the way
  `GenerationStatus.Pending` did. Worth a dedicated call-site audit if a similar symptom
  (a value silently reverting to its configured default) is ever reported for one of them.

---

## Bank-First Today Composer — Phase D1/D2 `Done` (2026-07-08)

Phase D1 implemented the first narrow, fallback-safe consumer of the Resource Bank platform:
`ActivityMaterializationJob` tries `ITodayBankResourceSelector`/`TodayBankResourceSelector`
before AI generation, for Today exercises whose `ExercisePatternDefinition.PrimarySkill` is
`"Vocabulary"` or `"Reading"` — a purely skill-based gate (not an explicit pattern-key
allow-list), confirmed by Phase D2's audit to already cover every current pattern in both
families, including `reading_multiple_choice_multi`/`reading_writing_fill_in_blanks` which D1's
own docs never explicitly named. Phase D2 then improved selection quality (balanced vocabulary/
grammar/reading bundle, CEFR-widening for review/scaffold routing only, a feedback-signal
exclusion), bank-context clarity (structured prompt block), and provenance (full resource list
on `LearningActivity.BankResourceProvenanceJson`, fixing a latent D1 bug where
`StudentActivityReadinessItem.SetBankItemProvenance(...)` was FK-mismatched to
`PlacementItemDefinition` rather than any Phase E Cefr* bank table). See
docs/architecture/learning-activity-engine.md for the full design. **Plan-Sync-After-D2
(2026-07-09, docs-only) deferred Phase D3** (broader Today composer migration) until after Phase
E7 (resource depth/type expansion, done 2026-07-09) and E8 if needed — every item below is gated
on the bank having more/different content and resource types, not on more selector engineering.
Explicitly **not** part of D1/D2's scope — tracked here for a future Phase D3 or larger Today
composer migration:

- [ ] **Grammar-focused Today pattern** `Not started` — Today has no pattern whose
  `PrimarySkill` is `"Grammar"`; D1/D2 only pull grammar bank content in opportunistically for
  `gap_fill_workplace_phrase`'s `Grammar` secondary skill. A dedicated grammar-focused pattern
  would let the selector target `CefrGrammarProfileEntry` directly instead of piggybacking.
- [ ] **Speaking/Listening/image/open-ended Today patterns** `Not started` — D1/D2 deliberately
  scoped to Vocabulary/Reading only, matching the E6 seed pack's actual content types. These
  pattern families need their own bank-content shape (audio, image, rubric) before a selector
  can meaningfully serve them.
- [x] **`TodayBankResourceSelector` consumption of `CefrReadingPassage`** `Done` (Phase D3,
  2026-07-09) — the selector now prefers a full `CefrReadingPassage` anchor for the
  comprehension/reorder Reading patterns (`reading_multiple_choice_single`/`_multi`,
  `reorder_paragraphs`), reusing E7's existing query methods, and falls back to the short
  `CefrReadingReference` behavior for cloze patterns, missing passages, or novelty-blocked
  passages. At most one full passage is injected, through a bounded, delimited, length-capped
  `TopicHint` block; provenance records `type=ReadingPassage` + cefr/title. Legacy fallback fully
  intact. See docs/architecture/learning-activity-engine.md Phase D3 section.
- [x] **CEFR-level widening** `Done` (Phase D2, 2026-07-08) — the selector now retries one CEFR
  level down, but **only** when the routing reason is Review/Scaffold/Remediation and the exact
  level has zero bank rows; it never widens upward and never widens at all for ordinary
  generation.
- [x] **Full per-resource provenance** `Done` (Phase D2, 2026-07-08) — every selected resource
  (type/id/sourceId/contentFingerprint/selectionReason) is now recorded as a JSON array on
  `LearningActivity.BankResourceProvenanceJson`, not just a single "primary" id in a log line.
- [ ] **Semantic/embedding-based resource selection** `Not started` — explicitly out of scope
  for D1/D2 per their own product brief (deterministic bank queries + exact-fingerprint novelty
  + cheap feedback-signal checks only); would belong to a future Phase E8-adjacent effort if ever
  pursued.
- [x] **Discovered bug — `LearningSession.GenerationStatus` EF default-value bug** `Done` (Bugfix-D1A, 2026-07-08):
  fixed by removing `LearningSessionConfiguration`'s `.HasDefaultValue(GenerationStatus.Ready)`
  (migration `Bugfix_D1A_RemoveGenerationStatusDefault`, no data loss). See
  docs/architecture/learning-activity-engine.md for the full root-cause writeup and
  docs/roadmap/road-map.md's Decision Log (Bugfix-D1A entry) for the fix rationale.
- [x] **Discovered bug — `StudentActivityReadinessItem.SetBankItemProvenance` FK mismatch**
  `Done` (Phase D2, 2026-07-08): that column is FK-constrained to `PlacementItemDefinition`, not
  any Phase E Cefr* bank table; D1's call to it with a Cefr* resource id would have thrown a
  foreign-key violation against a real database the first time a readiness-pool item existed at
  materialization time. Fixed by removing the call entirely and recording provenance on
  `LearningActivity.BankResourceProvenanceJson` instead (see above).

---

## Bank-First Next Phase — E8 then D4 (Plan-Sync-After-D3, 2026-07-09) `Planned`

Phase D3 (2026-07-09, `4fced4c7`) wired the E7 full reading passage bank into the Today bank-first
composer, proving D1/D2/D3's selector/composer path can consume deep internal content end to end.
**Plan-Sync-After-D3 (2026-07-09, docs-only) then decided Phase E8 (more resource depth/types)
comes before Phase D4 (broader Today composer expansion)** — the bottleneck is now bank
breadth/depth, not the composer mechanism. See docs/roadmap/road-map.md §1, Decision Log
(Plan-Sync-After-D3), and §19a items 18–20.

- [x] **Phase E8 — Internal Resource Bank Depth Expansion for Grammar, Usage, and Reading Support**
  `Done` (2026-07-09) — added `InternalResourceSeedPackE8Seeder` (second internal source,
  idempotent) through the existing staging → validation → approval → publish pipeline:
  - [x] **40** new `CefrVocabularyEntry` across A1–B2 (10 per level)
  - [x] **20** new `CefrGrammarProfileEntry` across A1–B2 (5 per level)
  - [x] **16** new `CefrReadingReference` short-reference rows across A1–B2 (4 per level)
  - [x] **8** new `CefrReadingPassage` full passages across A1–B2 (2 per level)
  - [x] Metadata coverage: CEFR/skill/subskill/context tags for all; plus focus tags + difficulty
    band on full passages (via a narrow additive `ApplyDeterministicRowMetadata` mapping of optional
    `focusTags`/`difficultyBand` columns); reading time/word count computed on `CefrReadingPassage`;
    source/provenance traceable
  - [x] Validation coverage (+17 tests) proving discoverability via `ResourceBankQueryService` and
    trace-back with no direct-final-table bypass
  - General-English-default, workplace a minority context. No external datasets, no
    Persian/bilingual content, no direct final-table seeding, no composer/selector/Practice-Gym/UI
    change, no migration. See docs/architecture/english-resource-bank-import-platform.md (E8 detail).
- [x] **Phase D4 — Broader Today Bank-First Composer Expansion** `Done` (2026-07-09) —
  `TodayBankResourceSelector` now assembles pattern-shaped multi-resource bundles on the deeper E8
  bank (no composer rewrite; all legacy fallbacks preserved):
  - [x] Vocabulary-primary: up to 3 vocab targets (primary) + opportunistic grammar + short reading
    reference (supporting)
  - [x] Reading comprehension/reorder: full `CefrReadingPassage` anchor (primary) + up to 2
    supporting vocab + optional grammar; short-reference fallback when no passage
  - [x] Reading cloze: short `CefrReadingReference` (primary) + supporting vocab/grammar, never a
    full passage
  - [x] Compact pattern-specific instruction layer (`PatternInstruction`)
  - [x] General-English-by-default: workplace-tagged full passages skipped unless routed workplace
    (`PrefersWorkplaceContext` from `ResolvedLearningGoalContext.WorkplaceSpecific`)
  - [x] Per-resource `role` (primary/supporting) provenance; flat JSON array preserved, no migration
  - [x] Exact-CEFR/never-upward, novelty, and feedback exclusions preserved; AI stays composer/fallback
  - [x] +16 backend tests. No new content, no external datasets, no UI, no legacy-fallback removal.
    See docs/architecture/learning-activity-engine.md (Phase D4 section).
- [x] **Post-D4 next-step checkpoint** `Resolved (Plan-Sync-After-D4, 2026-07-09)` — decided **Phase
  E9 (Published Bank Metadata Parity for Context-Aware Selection) comes before Phase D5 and PG-v2**,
  because D4 exposed `TODO-D4-1` (only `CefrReadingPassage` carries enough published metadata for
  context-aware filtering). See below.
- [x] **Phase E9 — Published Bank Metadata Parity for Context-Aware Selection** `Done` (2026-07-09) —
  gave the lean published bank tables the same selection metadata `CefrReadingPassage` has; closed
  `TODO-D4-1` for those tables:
  - [x] Added `subskill`/`difficulty_band`/`context_tags_json`/`focus_tags_json` (nullable) to
    `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` (migration
    `Phase_E9_AddLeanBankSelectionMetadata`; tag columns text, aligned in shape with
    `CefrReadingPassage`, filterable via the portable `.Contains` pattern)
  - [x] `ResourceCandidatePublishService` maps candidate metadata onto the lean rows at publish
    (out-of-range difficulty dropped to null; passage mapping unchanged)
  - [x] Idempotent `PublishedBankMetadataBackfillSeeder` repairs pre-E9 rows only where they have no
    metadata and trace to exactly one published candidate (never overwrites/guesses/inserts)
  - [x] `ResourceBankQueryService` + the three admin list endpoints expose the metadata read-only
    and support optional context/focus/subskill/difficulty filters; unfiltered browse unchanged
  - [x] +26 backend tests (mapping, backfill, filtering, discoverability, end-to-end API)
  - No new content/seed pack, no external datasets, no direct final-table seeding, no composer/UI
    change, no legacy-fallback removal. See docs/architecture/english-resource-bank-import-platform.md
    (E9 detail).
  - **Residual (narrowed, not a bug)**: E6/E7/E8-authored lean rows carry only the metadata their
    authors supplied (e.g. context tags + subskill but no difficulty band); a future content pass
    could enrich the lean packs if difficulty/focus filtering is ever needed on those types.
- [x] **Phase D5 — Context-Aware Today Bank Selection and Topic Matching** `Done` (2026-07-09) —
  wired `TodayBankResourceSelector` to consume E9's metadata; closed `TODO-E9-1`:
  - [x] Shared `SelectLeanAsync` applies E9 `ContextTag`/`FocusTag`/`Subskill`/`DifficultyBand`
    filters through a deterministic strict→loose relaxation ladder (drop difficulty → focus →
    subskill → context → general), combined with exact-CEFR-first / review-only-widen-down
  - [x] General-English default extended to all bank types: workplace-tagged vocabulary/grammar/
    reading-reference rows skipped for general learners (matching passages); workplace preferred
    when workplace-routed
  - [x] New request fields `PreferredFocusTags`/`PreferredSubskill`/`PreferredDifficultyBand`; focus
    fed from `ResolvedLearningGoalContext.FocusAreaKeys`
  - [x] Provenance records `appliedFilters` + `matchedContextTags`; prompt block gained a
    selection-emphasis note; D4 pattern instructions + roles + novelty/feedback exclusions preserved
  - [x] Deterministic metadata matching only (no embeddings/vector search); legacy fallback intact
  - [x] +17 backend tests. No composer rewrite, no content, no migration, no UI.
- [x] **Phase E10 — Internal Bank Metadata Depth Expansion for Focus and Difficulty** `Done`
  (2026-07-09) — enriched existing internal published lean rows; resolved `TODO-D5-1`:
  - [x] `InternalBankMetadataDepthSeeder` (idempotent startup step after the E9 backfill) derives
    **difficulty band from CEFR** (A1→1…B2→4, C1/C2→5) and a **focus tag from the row's subskill**
    onto `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`
  - [x] Touches only `Internal/Original` rows traceable to exactly one published candidate; fills only
    empty fields (never overwrites authored values); skips non-internal/untraceable/ambiguous rows;
    preserves subskill + context; never inserts a row; no-op on rerun; English-only, source-traceable
  - [x] +20 backend tests (coverage, traceability, idempotency, no-insert, no-overwrite, valid
    subskill/band, discoverability via the E9 filters, end-to-end admin API)
  - No schema/migration (E9's columns exist), no external datasets, no new content pack, no composer/
    selector change, no UI. See docs/architecture/english-resource-bank-import-platform.md (E10 detail).
- [x] **Phase D6 — Today Topic Matching and Subskill-Aware Resource Selection** `Done (2026-07-09)` —
  closed `TODO-E10-1` and made Today bank-first bundles topic-aware, deterministic metadata matching
  only (no embeddings/vector). `CurriculumRoutingRecommendation` surfaces the matched objective's
  `Subskill`; `ActivityMaterializationJob` feeds `PreferredSubskill`/`PreferredFocusTags`/
  `PreferredDifficultyBand` (the last derived from `StudentProfile.DifficultyPreference` relative to
  the routed CEFR's normal band via the shared `CefrDifficultyBand` helper) into
  `TodayBankResourceSelector`. Reading bundles anchor supporting vocabulary/grammar on the primary
  passage/reference's context tag (strict topic-anchor rungs prepended to the D5 relaxation ladder).
  D5 relaxation, CEFR policy, workplace-exclusion, provenance shape, and legacy fallback preserved.
  +12 backend tests (3,671 total). No schema/migration, no content, no UI, no PG-v2. Residual: E10
  difficulty bands are CEFR-uniform, so difficulty narrowing is a no-op for Balanced / a relaxation
  otherwise until mixed-difficulty content lands. See
  docs/architecture/learning-activity-engine.md (Phase D6 notes).

---

## Practice Gym v2 — Skill-first selector and UX `Planned` (Plan-Sync-PG-v2)

Practice Gym should eventually become **skill/subskill/objective-first**, not
activity-type-first. Students should choose or be guided toward a skill/subskill/weak-area/
objective/review/challenge/recommended-practice target — never a raw internal exercise type
(gap fill, phrase match, reorder paragraphs, etc.) — with the system internally selecting the
best `ActivityTemplate`/resource/format. Sequenced deliberately late (after Phase E5-E8, before
Phase F/G — see `docs/roadmap/road-map.md` §19a) because a good selector needs mature
bank/resource search/selector coverage to have real content to choose from. See
`docs/architecture/practice-gym.md`'s "Future target: skill-first Practice Gym" section for full
design rationale. **`ExerciseTypeDefinition`/`ExercisePatternDefinition` are never deleted** by
any item below — they become an internal capability registry.

- [ ] **PG-v2A: Backend skill/objective-first Practice Gym selector** `Planned`
  - **Purpose**: given a target (skill/subskill/weak-area/objective/review/challenge/recommended),
    the student's CEFR, weakness/evidence signals (mastery scores, placement confidence,
    `ActivityFeedbackSignal` ratings), novelty/cooldown state, and available published bank
    items/templates, select the single best `ActivityTemplate`/resource/activity instance to
    serve — without the student ever choosing a raw pattern/type name.
  - **Acceptance criteria**: selector returns a suitable activity for a given skill/subskill/CEFR
    combination when a compatible published template/bank item exists; falls back to legacy
    freeform generation when no suitable bank-first option exists (never a hard failure); respects
    `IActivityNoveltyPolicy` cooldowns and `ActivityFeedbackSignal` repeat-preference signals;
    queries the existing `ExerciseTypeDefinition`/`ExercisePatternDefinition` capability registry
    rather than a new parallel classification system.
  - **Out of scope**: any UI changes (PG-v2B); retiring the existing type-first Practice Gym
    entry points (PG-v2D, only after this is proven); embeddings/semantic matching (still
    deferred per Phase E8's scope discipline) — selection logic is rule-based/deterministic plus
    existing evidence signals, not a new ML-driven recommender.

- [ ] **PG-v2B: Student Practice Gym UI simplified around skills, weak areas, review, challenge, recommended practice** `Planned`
  - **Purpose**: replace (or sit alongside, during transition) the current type-first Practice
    Gym entry UI with a skill/objective-first entry point — student picks "Reading", "a weak
    area", "review", "challenge", or "recommended for you," never a raw pattern name.
  - **Acceptance criteria**: student can start a practice session by skill/subskill/weak-area/
    review/challenge/recommended without seeing internal pattern/type keys anywhere in the UI;
    the resulting activity still renders via the existing `ExerciseRendererComponent`/
    `FormioRendererComponent` paths (no new renderer); existing Practice Gym routes/deep-links
    keep working during the transition (PG-v2D governs full retirement, not this item).
  - **Out of scope**: backend selector logic (PG-v2A, prerequisite); deleting the existing
    type-first UI (PG-v2D); a full visual redesign of the result/feedback screens (those are
    Phase B2's/existing surfaces, reused as-is).

- [ ] **PG-v2C: Admin capability-registry cleanup / internal pattern management** `Planned`
  - **Purpose**: reframe the admin experience around `ExerciseTypeDefinition`/
    `ExercisePatternDefinition` as an internal **capability registry** (renderer capability,
    scorer/evaluator capability, audio/image/speaking/open-ended requirements, Form.io
    compatibility, supported skills/subskills, CEFR suitability, Practice Gym/Today
    compatibility, fallback/generation capability) rather than a student-facing catalog admins
    manage the same way as `ActivityTemplate`.
  - **Acceptance criteria**: admin can view/edit pattern capability flags without implying
    they're editing "the Practice Gym menu"; no change to the underlying `ExercisePatternKey`
    catalog's actual capability data, only to its admin framing/UI; existing
    `ExerciseTypeDefinitionSeeder`/pattern catalog remain the single source of truth (no
    duplicate registry created).
  - **Out of scope**: deleting or renaming `ExerciseTypeDefinition`/`ExercisePatternDefinition`
    (never in scope, at any PG-v2 item); building a brand-new admin capability-registry entity —
    this is a UI/framing cleanup of the existing catalog, not a new data model.

- [ ] **PG-v2D: Legacy type-driven Practice Gym path retirement after proof** `Planned`
  - **Purpose**: once PG-v2A/B are proven (real students successfully completing skill-first
    practice sessions, selector reliably choosing suitable activities), retire the legacy
    type-first Practice Gym entry UI — not the underlying pattern/template/legacy-generation
    infrastructure, just the direct type-selection entry point.
  - **Acceptance criteria**: retirement only proceeds after an explicit product decision
    confirming PG-v2A/B are working well in practice (this item does not define its own success
    metric — that's a product call at the time); legacy generation fallback and
    `ExerciseTypeDefinition`/`ExercisePatternDefinition` remain fully intact and functional
    throughout — this item retires a UI entry point, not backend capability.
  - **Out of scope**: deleting any pattern/template/legacy-generation code or data; a hard cutover
    date — this is explicitly gated on proof, not a calendar deadline.

---

## Practice Gym — Deferred Pattern Families (Post Phase C-Final) `Not started`

Phase C1→C2→C3 migrated 8 of 33 Practice Gym pattern rows to the bank-first Form.io template
path (deterministic, audio-free, image-free patterns only). Phase C-Final (2026-07-08) closed
that track without forcing further migration — the remaining 25 legacy keys need genuinely new
scope, not another small batch. See `docs/architecture/practice-gym.md` for the full audit and
per-pattern classification. Tracked here as future backlog items, not started:

- [ ] **A. Listening/audio family** (`listen_and_answer`, `listen_and_gap_fill`,
  `listening_multiple_choice_single/multi`, `listening_fill_in_blanks`, `select_missing_word`,
  `highlight_correct_summary`, `highlight_incorrect_words`, `write_from_dictation`,
  `summarize_spoken_text`) `Not started`
  - Needs a dedicated audio-DTO compatibility review — every one of these carries `AudioUrl`/
    `AudioScript` in its content DTO despite a catalog `RequiresAudio=false` flag.
  - Needs audio asset/transcript preview or rendering support in the Form.io path (none exists
    today — Form.io templates are currently text/schema-only).
  - Deterministic scoring (`ComponentAnswerScorer`) is only relevant for the KeyedSelection/
    ExactMatch subset (6 of the 9); the `AiStructured` ones (`listen_and_answer`,
    `summarize_spoken_text`) need the open-ended AI-evaluated support in item C below.
  - No migration until the audio path is confirmed safe end-to-end (upload/storage, playback,
    no leaked transcript in the student-safe schema).

- [ ] **B. Speaking/audio family** (`answer_short_question`, `read_aloud`, `repeat_sentence`,
  `spoken_response_from_prompt`, `speaking_roleplay_turn`, `respond_to_situation`,
  `describe_image`, `retell_lecture`, `summarize_group_discussion`) `Not started`
  - Needs speaking-response renderer/evaluator support beyond the existing `speakingResponse`
    Form.io component (which only carries an audio storage-key reference, not a scoring path).
  - Needs microphone/upload flow compatibility confirmed for the Form.io rendering path
    specifically (currently only proven in the legacy exercise-renderer flow).
  - Needs an AI or rubric evaluation path — `answer_short_question`/`read_aloud`/
    `repeat_sentence` currently use fuzzy/word-overlap scoring (see item D); the rest are
    `AiOpenEnded` (see item C).
  - Not part of the deterministic template-migration track; do not attempt without dedicated
    renderer/evaluator work first.

- [ ] **C. Open-ended writing/chat/roleplay family** (`email_reply`,
  `teams_chat_simulation`, `open_writing_task`, `summarize_written_text`, `write_essay`,
  `spoken_response_from_prompt`, `speaking_roleplay_turn`, `respond_to_situation`,
  `describe_image`, `retell_lecture`, `summarize_group_discussion`, `listen_and_answer`,
  `summarize_spoken_text`) `Not started`
  - Needs dedicated Form.io + AI-evaluated support — the current Form.io path
    (`FormIoPatternEvaluator`/`ComponentAnswerScorer`) only has a working story for
    deterministic single/multi-component scoring, not AI-graded open-ended text or speech.
  - Needs rubric/feedback-plan integration (mirroring the legacy `AiStructured`/`AiOpenEnded`
    evaluation flow) so a template-sourced instance gets equivalent coaching feedback quality.
  - No deterministic-scorer assumption should be made for this family — do not attempt to force
    a binary `ComponentAnswerScorer` kind onto genuinely open-ended content.

- [ ] **D. Fuzzy/short-answer family** (`answer_short_question`, `read_aloud`,
  `repeat_sentence`) `Not started`
  - Needs a partial-credit/fuzzy `ComponentAnswerScorer` kind — these currently use
    substring-"contains" matching (`answer_short_question`) or word-overlap percentage scoring
    with a 0.60 threshold (`read_aloud`/`repeat_sentence`) in `ExactMatchEvaluator`, neither of
    which fits the existing binary single_choice/multiple_choice/text_exact/text_normalized/
    ordered_sequence kinds.
  - Needs an answer-normalization policy (stemming/synonym tolerance, or an explicit
    alternate-answer list) rather than exact/positional matching.
  - Needs confidence/alternate-answer support in the scoring rule shape if partial credit is to
    be backend-only and leak-safe like the existing kinds.
  - Also audio-referencing (see item B) — this family sits at the intersection of B and D and
    should be scoped as one combined review, not two separate ones.

---

## Phase 18B — Advanced Feedback UX `Done`

Sprint doc: [sprints/current-sprint.md](../sprints/current-sprint.md)
Review: [reviews/2026-07-01-phase-18b-advanced-feedback-ux-review.md](../reviews/2026-07-01-phase-18b-advanced-feedback-ux-review.md)

- [x] Writing evaluation scores loaded and displayed for writing activities. `Done`
- [x] Support-language help collapsible — generic label, not hardcoded to Persian. `Done`
- [x] Context-aware next-step action buttons (Improve for writing only; Try Again hidden for speaking). `Done`
- [x] Skill/objective context header from stageContent. `Done`
- [x] AI disclaimer in coach feedback, chat/email, and spoken response sections. `Done`
- [x] `nextPracticeSuggestion` rendered (was previously unreachable). `Done`
- [x] `pronunciationScore` added to speaking eval scores grid. `Done`
- [x] 69 new Angular unit tests; 0 regressions. `Done`

---

## Phase 18A-G — Generation Diagnostics Hardening `Done`

Sprint doc: [sprints/current-sprint.md](../sprints/current-sprint.md)
Review: [reviews/2026-07-01-phase-18a-g-generation-diagnostics-hardening-review.md](../reviews/2026-07-01-phase-18a-g-generation-diagnostics-hardening-review.md)

- [x] Provider/model traceability in admin diagnostics. `Done`
- [x] SHA-256 content hashing for prompt versioning. `Done`
- [x] Configurable data retention with Quartz prune job. `Done`
- [x] Objective/student context threading into failure log. `Done`
- [x] Abandoned-generation rate warning. `Done`

---

## Documentation Governance `Done`

- [x] Documentation impact review rule for all code changes. `Done`
  - AGENTS.md now requires a documentation impact review in every code-change final report.
  - Major source-of-truth docs now carry freshness metadata.

---

## Admin UX, Student Management & AI Config Cleanup `Done`

Sprint doc: [admin-ux-student-management-ai-config-cleanup-sprint.md](../sprints/admin-ux-student-management-ai-config-cleanup-sprint.md)

- [x] Fix admin content width and dashboard responsiveness. `Done`
- [x] Remove permanent Create student sidebar item; Students page owns create action. `Done`
- [x] Add reusable toast service/component and create-student success toast. `Done`
- [x] Add admin student profile edit flow. `Done`
- [x] Add soft archive using `StudentLifecycleStage.Archived`; archived students are hidden by default and cannot sign in. `Done`
- [x] Hide Curriculum from admin navigation/dashboard while keeping route/API/data intact. `Done`
- [x] Complete AI feature routing rows for active runtime keys. `Done`
- [x] Add fallback provider/model/enabled controls to AI Config. `Done`

Deferred follow-ups:

- [ ] Redefine or remove Curriculum when LearningSession / ExercisePattern implementation decides whether curated seed/fallback content is needed. `Planned`
- [x] Add secure admin password reset flow for students. `Done`
- [x] Add student detail page with learning memory and reset tools. `Done` (2026-06-14)
  - Route `/admin/students/:id`, see `docs/sprints/2026-06-14-admin-student-detail-page.md`
  - Activity history not included — separate item below.
- [x] Add activity history to admin student detail page. `Done` (2026-06-14)

---

## Onboarding & Post-Placement UX Alignment `Done`

Engineering review complete (2026-06-09). See: [2026-06-09-onboarding-post-placement-ux-engineering-review.md](../reviews/2026-06-09-onboarding-post-placement-ux-engineering-review.md)
Sprint doc: [onboarding-post-placement-ux-alignment-sprint.md](../sprints/onboarding-post-placement-ux-alignment-sprint.md)

- [x] T1: Domain - `LearningGoalDescription`, `DifficultSituationsText` fields; `SetCareerContextText()` and extended skill method. `Done`
- [x] T2: Migration T31 - add two new varchar columns. `Done`
- [x] T3: Application - new `SetCareerContextTextRequest`, extend skill command with goal fields. `Done`
- [x] T4: API - extend `OnboardingStepDto` + controller dispatch for text career and skill+goal. `Done`
- [x] T5: API - add `lifecycleStage` to `DashboardResponse` and handler. `Done`
- [x] T6: Frontend step 3 - replace career list with free-text input. `Done`
- [x] T7: Frontend step 4 - add Listening, learning goal textarea, navigate to `/placement`. `Done`
- [x] T8: Frontend guard - redirect pre-onboarding to `/onboarding/resume` not `/dashboard`. `Done`
- [x] T9: Frontend dashboard - lifecycle-aware states (PlacementRequired CTA, CourseReady summary, Practice Gym section). `Done`
- [x] T10: Backend integration tests. `Done`
- [x] T11: Playwright E2E tests. `Done`

Completion notes:

- Onboarding supports free-text career context and native-language learning goals.
- After onboarding, students go to `/placement`; onboarding no longer starts background learning-path generation.
- Dashboard is lifecycle-aware and keeps Practice Gym secondary until Today / `LearningSession` is implemented.
- Verification passed: `dotnet test LinguaCoach.slnx`, `npm run build`, `npx playwright test`.

---

## Adaptive Onboarding & Staged Assessment `Not started`

From product owner brainstorm (2026-06-12). Architecture/planning notes only — not scoped for implementation.

**Direction:**

- Stage 1: Initial onboarding — quick entry, basic profile (language, goals, work/casual preference, confidence).
- Stage 2: Initial placement — starts at A1/A2, adapts difficulty based on answers, stops when confidence threshold reached.
- Stage 3: Ongoing diagnostic progress — grammar, vocabulary, listening, speaking, writing, reading, workplace communication, casual conversation each tracked as percentage completion via lessons over time.
- Stage 4: Adaptive course generation — lessons become more accurate as diagnostics improve.

**A1 support requirements:**

- Simple English in onboarding/placement questions.
- Optional native-language instruction support.
- Early tasks favor matching/short-phrase/listening over open writing.

**Relationship to [Configurable Onboarding and Placement Assessment](#configurable-onboarding-and-placement-assessment-not-started) below:** that item covers making *existing* onboarding/placement questions admin-configurable. This item is the larger product direction (staged assessment model) that configurable questions would eventually plug into. Do not implement either without further scoping — both require dedicated architecture review.

---

## Teacher Role (minimal, read-only) `Not started`

Deferred — Phase 21A precursor (2026-07-03). Full implementation plan:
`docs/architecture/teacher-role-and-read-access.md`.

Adds `UserRole.Teacher` so instructors can log in and see a read-only view
of the full student roster, without the full Organisation/Cohort model
(that remains Phase 21A). Admin-provisioned only, no self-signup, no
per-teacher scoping. See also the related, already-tracked items below:
"Admin / teacher progress view per student" (line ~881) and "Teacher /
admin review of AI feedback quality" (line ~903) — this item is the
access-control prerequisite for both.

---

## "View As User" — Admin Impersonation `Not started`

Deferred — Phase 21A precursor (2026-07-03). Design notes:
`docs/architecture/view-as-user-impersonation.md`.

Lets an Admin view the app as a specific student, without a second login,
to verify the effect of admin-side changes from the student's perspective.
Standard enterprise pattern (Stripe/Zendesk/Intercom-style): short-lived,
audited, admin-only, Student-target-only impersonation token with a
persistent "Viewing as X — Return to Admin" banner. Interim workaround in
use today: separate browser contexts (incognito/second profile) per role —
requires no code, since JWTs are stored per browser context.

---

## Multi-Course / Enrolment Model `Not started`

From product owner brainstorm (2026-06-12). Future architecture direction — not current sprint implementation.

**Direction:**

```
Student
  -> Enrolments
      -> Course: Casual English
      -> Course: Workplace English
      -> Future: Academic English, Interview English, etc.
```

Today/Journey/Practice Gym and activity generation would be scoped to the student's active enrolment/course, allowing one student to study multiple English tracks with separate vocabulary, scenarios, tone, and progress tracking. AI prompts would receive course context.

**Notes:**

- Conflicts with current single-track `LearningPath`/`StudentProfile` model — would require an `Enrolment` entity and significant changes to session/path generation, AI context building, and progress tracking.
- Do not begin without a dedicated architecture review (`/plan-eng-review`).

---

## Estimated Known Words `Not started`

From product owner brainstorm (2026-06-12).

**Direction:** Show an estimated vocabulary range (e.g. "Estimated vocabulary: about 400–600 words"), workplace phrases known, recently learned, and needs-review counts — framed as an estimate/range, not a fake-precise number.

**Possible calculation basis:**

- Count mastered `StudentVocabularyItem` rows (status/strength-based).
- Add an estimated baseline range from CEFR level.
- Adjust from successful vocabulary/listening/reading attempt history.

**Notes:**

- Depends on `StudentVocabularyItem` / vocabulary extraction work (see [Vocabulary extraction from writing attempts](#vocabulary-extraction-from-writing-attempts-in-sprint-vocabulary-extraction-from-writing-attempts-sprint)) being further along.
- Display as a range, never a single precise count, per product direction.

---

## Configurable Onboarding and Placement Assessment `Not started`

**Priority:** P1 — after Placement MVP stabilisation, before serious pilot expansion

**Reason:** We are still learning what onboarding and placement should ask. Hardcoded questions slow iteration and make the product hard to improve without code changes.

**Description:**

Onboarding and placement questions should be configurable from admin/product configuration rather than hardcoded in Angular/backend. Admins should be able to modify and improve onboarding steps, onboarding questions, placement sections, placement questions, answer options, helper text, examples, and scoring/evaluation prompts without code changes.

**Scope:**

- [ ] Configurable onboarding steps (order, labels, instructions). `Not started`
- [ ] Configurable onboarding questions (per step: type, prompt, options, helper text). `Not started`
- [ ] Configurable placement sections (order, title, instructions, section type). `Not started`
- [ ] Configurable placement questions (per section: type, prompt, answer options, correct answer, scoring weight). `Not started`
- [ ] Configurable answer options (add/remove/reorder without code changes). `Not started`
- [ ] Configurable examples and helper text for each question. `Not started`
- [ ] Configurable skill tags per section (which skills a section scores). `Not started`
- [ ] Configurable listening scripts or script templates (replace hardcoded placement audio script). `Not started`
- [ ] Configurable placement evaluation prompt/template via DB prompt system (extends existing AI prompt infrastructure). `Not started`
- [ ] Versioning of assessment/question sets (track which version a student started). `Not started`
- [ ] Safe migration path for students who started an older version (resume on same version or notify admin of mismatch). `Not started`
- [ ] Admin preview/test mode (preview placement as a student without committing results). `Not started`

**Notes:**

- Current hardcoded location: `PlacementContent.cs` (backend) and `PlacementContent` static class
- Placement audio script is currently hardcoded in the `listening` section definition
- The existing AI prompt DB infrastructure (`AiPromptTemplate`, `DefaultAiSeeder`) is the natural extension point for configurable evaluation prompts
- Do not implement as part of Placement MVP stabilisation — backlog only until pilot feedback confirms which questions need to change

---

## Course Session & Placement Redesign â€” Implementation Phases

Architecture sprint complete (2026-06-09). See sprint doc: [course-session-placement-redesign-sprint.md](../sprints/course-session-placement-redesign-sprint.md)

### Phase 1 â€” Placement Assessment MVP `Partially done`

- [x] Add `StudentLifecycleStage` enum and column to `StudentProfile` + migration. `Done` (T29)
- [x] Add `PreferredSessionDurationMinutes` to `StudentProfile` + migration. `Done` (T29)
- [x] Add `ProfessionalExperienceLevel` enum and column to `StudentProfile` + migration. `Done` (T29)
- [x] Add `RoleFamiliarity` enum and column to `StudentProfile` + migration. `Done` (T29)
- [x] Add `PlacementAssessment` entity, EF config, migration. `Done` (T29)
- [x] Implement placement section handlers and `PlacementService`. `Done`
- [x] Add placement flow to Angular (6 sections, progress, result screen). `Done`
- [x] Add lifecycle-aware routing guard. `Done` (placement.guard.ts â€” guard fix pending in UX alignment sprint)
- [x] Add backend integration tests for placement flow. `Done`

### Phase 1 â€” Placement Assessment MVP `Not started` (remaining)

- [ ] Add `StudentLifecycleStage` enum and column to `StudentProfile` + migration. `Not started`
- [ ] Add `PreferredSessionDurationMinutes` to `StudentProfile` + migration. `Not started`
- [ ] Add `ProfessionalExperienceLevel` enum and column to `StudentProfile` + migration. `Not started`
- [ ] Add `RoleFamiliarity` enum and column to `StudentProfile` + migration. `Not started`
- [ ] Add `WorkplaceSeniority` (DomainComplexity) computed column or property to `StudentProfile`. `Not started`
- [ ] Update onboarding to collect session duration preference, professional experience level, and role familiarity. `Not started`
- ~~Add `ModuleType` column to `LearningModule` (Standard, Placement)~~ `Superseded` â€” Placement is a standalone `PlacementAssessment` entity, not a LearningModule. No ModuleType column needed.
- [ ] Add `PlacementAssessment` entity, EF config, migration. `Not started`
- [ ] Add `PlacementSection` entity, EF config, migration. `Not started`
- [ ] Add `placement_assessment_evaluate` AI prompt seed. `Not started`
- [ ] Implement placement section handlers (self-check, vocab/grammar, reading, listening, writing, speaking). `Not started`
- [ ] Implement `PlacementResultGeneratorService` (AI-evaluated result). `Not started`
- [ ] Feed placement result into `StudentSkillProfile` and `UserLearningSummary`. `Not started`
- [ ] Add placement flow to Angular (6 sections, progress, result screen). `Not started`
- [ ] Add lifecycle-aware routing guard (redirect to correct stage). `Not started`
- [ ] Add backend integration tests for placement flow. `Not started`
- [ ] Add Playwright tests for placement flow. `Not started`

### Phase 2 â€” Course Session MVP `Not started`

- [ ] Add `LearningSession` entity, EF config, migration. `Not started`
- [ ] Add `SessionExercise` entity, EF config, migration. `Not started`
- [ ] Implement session generator (backend-driven, not AI-driven). `Not started`
- [ ] Generate sessions based on duration, level, career context, learning memory. `Not started`
- [ ] Add Today page (replaces activity-card dashboard as primary student entry point). `Not started`
- [ ] Add session progress component to Today page. `Not started`
- [ ] Add session completion tracking. `Not started`
- [ ] Add backend integration tests for session generation. `Not started`
- [ ] Add Playwright tests for Today page and session flow. `Not started`

- [ ] Activity Teach page (Page 1) micro-lesson content - see `docs/sprints/2026-06-15-activity-teach-page-microlesson-content-sprint.md`. `Planned`

### Phase 3 â€” Exercise Pattern Engine `Not started`

- [ ] Define exercise pattern library in code (pattern key â†’ pattern config). `Not started`
- [ ] Implement session generator pattern selection logic. `Not started`
- [ ] Implement `teams_chat_simulation` pattern (content model, UI, evaluation). `Not started`
- [ ] Implement `read_and_answer`, `gap_fill_with_workplace_phrase`, `phrase_match`, `collocation_match`. `Not started`
- [ ] Link `SessionExercise` to `LearningActivity` via pattern-to-activity mapping. `Not started`
- [ ] Add pattern-level integration tests. `Not started`

### Phase 4 â€” Practice Gym `Not started`

- [ ] Add Practice tab to student navigation. `Not started`
- [ ] Move dashboard activity cards under Practice tab. `Not started`
- [ ] Today page becomes primary student home. `Not started`
- [ ] Keep existing `/activity?type=...` routing unchanged. `Not started`
- [ ] Add Playwright tests for Practice tab navigation. `Not started`

### Phase 5 â€” MinIO File Storage `Not started`

- [ ] Define `IFileStorageService` interface in Application. `Not started`
- [ ] Implement `LocalFileStorageService` in Infrastructure. `Not started`
- [ ] Implement `MinioFileStorageService` in Infrastructure (Minio .NET SDK). `Not started`
- [ ] Migrate `ListeningAudioService` to use `IFileStorageService`. `Not started`
- [ ] Migrate `SpeakingAudioService` to use `IFileStorageService`. `Not started`
- [ ] Migrate `PlacementAudioService` to use `IFileStorageService`. `Not started`
- [ ] Update audio streaming endpoints to use `IFileStorageService`. `Not started`
- [ ] Add MinIO to Docker Compose (staging). `Not started`
- [ ] Add unit tests for both file storage implementations. `Not started`
- [ ] Verify audio playback end-to-end in staging with MinIO. `Not started`
- [ ] **Fix placement audio volume permissions in Docker.** `Blocked — deferred to MinIO migration` The named Docker volume mounted at `/app/audio-data` is not writable by the container user (non-root). Placement TTS audio generation fails with `Permission denied` in production. The frontend correctly shows the fallback message. Fix options: (a) set correct ownership in the Dockerfile (`RUN mkdir -p /app/audio-data && chown app:app /app/audio-data`), or (b) migrate to MinIO (the planned Phase 5 path, already has a shared MinIO instance running on the VPS). Option (b) is preferred — do not spend time on option (a) unless MinIO migration is blocked.

### Phase 6 â€” Admin Reset Tools `Not started`

- [ ] Add `StudentResetLog` entity, EF config, migration. `Not started`
- [ ] Implement `POST /api/admin/students/{id}/reset` endpoint. `Not started`
- [ ] Implement lifecycle stage transition logic in reset handler. `Not started`
- [ ] Implement audio file cleanup via `IFileStorageService` on reset. `Not started`
- [ ] Add admin UI: lifecycle stage badge on student detail page. `Not started`
- [ ] Add admin UI: reset modal with confirmation and reason input. `Not started`
- [ ] Add backend integration tests for reset endpoint. `Not started`
- [ ] Add Playwright tests for admin reset flow. `Not started`

---

## Professional Experience Level & Domain Complexity `Not started`

Architecture doc: [professional-experience-domain-complexity.md](../architecture/professional-experience-domain-complexity.md)

Priority: P0/P1 â€” affects onboarding, placement, and session generation quality. Without this, SpeakPath may give students linguistically appropriate tasks that are professionally inappropriate.

- [ ] Define `ProfessionalExperienceLevel` and `RoleFamiliarity` enums in domain. `Not started`
- [ ] Define `DomainComplexity` enum (`BasicWorkplace`, `JuniorRole`, `IndependentContributor`, `SeniorSpecialist`, `LeadOrManager`). `Not started`
- [ ] Add experience level and role familiarity steps to Angular onboarding flow. `Not started`
- [ ] Implement `WorkplaceSeniority` computation (experience level Ã— role familiarity â†’ DomainComplexity). `Not started`
- [ ] Add `WorkplaceSeniority` field to `StudentProfile` (stored, updated after onboarding). `Not started`
- [ ] Add `{{DomainComplexity}}` and `{{ProfessionalExperienceLevel}}` prompt variables to all AI content generation prompts. `Not started`
  - `activity_generate_writing`
  - `activity_generate_listening`
  - `activity_generate_speaking_roleplay`
  - `placement_assessment_evaluate`
  - `learning_path_generate`
  - `learning_path_generate_adaptive`
- [ ] Add domain complexity rule to all prompts: do not introduce concepts beyond student's DomainComplexity unless a micro-lesson teaches it first. `Not started`
- [ ] Update session generator to filter workplace scenario topics by `WorkplaceSeniority`. `Not started`
- [ ] Update placement assessment prompt to use `BasicWorkplace`/`JuniorRole` domain complexity by default. `Not started`
- [ ] Add domain complexity override option to Practice Gym (simple / normal / challenge). `Not started`
- [ ] Add `AvoidedDomainComplexity` tracking: when a new concept is introduced, mark it as "introduced" so it can be reused without a micro lesson. `Not started`
- [ ] Add backend integration tests for WorkplaceSeniority computation and prompt variable inclusion. `Not started`
- [ ] Add Playwright tests for onboarding experience level and role familiarity steps. `Not started`

---

## Competitive Gap â€” P1 Features `Not started`

From competitive gap review (2026-06-09). See sprint doc for full matrix.

### TeamsChatSimulation (P1)

- [ ] Design `teams_chat_simulation` content model and API response shape. `Not started`
- [ ] Implement `TeamsChatSimulationGenerator` (AI-generated Teams chat scenario). `Not started`
- [ ] Implement `TeamsChatSimulationEvaluator` (tone, phrase use, conciseness, completeness). `Not started`
- [ ] Add Teams chat UI to Angular activity-lesson (chat bubble layout, word counter, hint phrases). `Not started`
- [ ] Add TeamsChatSimulation to Practice Gym. `Not started`
- [ ] Add backend integration tests for TeamsChatSimulation. `Not started`
- [ ] Add Playwright tests for Teams chat flow. `Not started`

### Vocabulary Queue Cards (P1)

- [ ] Design vocabulary card types: cloze, collocation, phrase, use-in-sentence. `Not started`
- [ ] Implement card queue scheduling (new/weak/mastered spaced repetition). `Not started`
- [ ] Add `/vocabulary` card mode UI (swipe-style or inline card deck). `Not started`
- [ ] Add collocation card generation from student's existing vocabulary queue. `Not started`
- [ ] Add backend integration tests for vocabulary card scheduling. `Not started`

### Micro Lessons (P1)

- [ ] Implement `micro_lesson_phrases` pattern: AI generates 3â€“5 target phrases with usage examples before a lesson session. `Not started`
- [ ] Implement `micro_lesson_dialogue` pattern: AI generates a short workplace dialogue to model before speaking/writing tasks. `Not started`
- [ ] Implement `micro_lesson_mistake` pattern: pulls a recurring mistake from student memory and explains it before a correction exercise. `Not started`
- [ ] Add micro lesson step to Angular session exercise flow (read-only, no submission, auto-advance). `Not started`
- [ ] Add micro lesson AI prompts to seed data. `Not started`

### Weekly Plan (P1, part of Phase 2 session model)

- [ ] Add weekly session schedule generation after placement completes. `Not started`
- [ ] Store weekly plan as pre-generated `LearningSession` slots for the coming week. `Not started`
- [ ] Add Today page weekly calendar strip (days of week, completed/upcoming indicators). `Not started`
- [ ] Respect student's preferred practice frequency from onboarding. `Not started`

---

## Competitive Gap â€” P2 Features `Not started`

### Call Mode / Open AI Speaking (P2)

- [ ] Design Call Mode product spec: multi-turn AI-first voice conversation. `Not started`
- [ ] Implement `call_mode_single_turn` pattern (AI speaks, student responds). `Not started`
- [ ] Implement `call_mode_multi_turn` pattern (3â€“5 AI/student turns, post-call transcript + feedback). `Not started`
- [ ] Add Call Mode UI to Practice Gym (phone-style interface, AI speaks first). `Not started`
- [ ] Add post-call feedback screen (transcript, per-turn coaching, vocabulary, tone summary). `Not started`
- [ ] Wire real STT provider (OpenAI Whisper or Azure Speech) for Call Mode transcription. `Not started`
- [ ] Add backend integration tests for call mode flow. `Not started`
- [ ] Add Playwright tests for Call Mode UI. `Not started`
- **Note:** Call Mode requires real STT. Do not implement with fake STT only.

### Pronunciation MVP (P2)

- [ ] Design pronunciation engine product spec (problem words, repeat-after-me, word stress, intonation). `Not started`
- [ ] Evaluate STT/ASR providers for phoneme-level feedback (ELSA-style vs simpler). `Not started`
- [ ] Implement `PronunciationPractice` activity type (backend + frontend). `Not started`
- [ ] Add pronunciation patterns to exercise library: problem word drills, repeat-after-me, stress/intonation. `Not started`
- [ ] Add Pronunciation section to Practice Gym. `Not started`
- **Note:** Pronunciation is separate from speaking communication. Do not conflate with SpeakingRolePlay.

### Real STT Provider (P2)

- [ ] Evaluate OpenAI Whisper vs Azure Speech vs Google STT for accuracy and cost. `Not started`
- [ ] Add real STT provider implementation behind `ISpeechToTextService`. `Not started`
- [ ] Wire into SpeakingRolePlay and Call Mode flows. `Not started`
- [ ] Add STT usage cost tracking. `Not started`

### Real TTS Provider (P2)

- [ ] Evaluate OpenAI TTS vs Azure TTS vs Google TTS for quality and cost. `Not started`
- [ ] **Implement `OpenAiTextToSpeechService` behind `ITextToSpeechService`.** `Not started` OpenAI TTS (`tts-1` model, `onyx` or `echo` voice) is the preferred first provider. `OPENAI_API_KEY` is already wired in production compose. Add `TTS_PROVIDER=OpenAI` env var and register via `DependencyInjection.cs` based on config. This will make placement listening audio audible in production — currently `FakeTextToSpeechService` generates silent WAV (correct for tests, silent in prod).
- [ ] Wire into listening activity generation (`ListeningAudioService`). `Not started`
- [ ] Wire into placement listening audio (`PlacementAudioService`). `Not started`
- [ ] Add TTS usage cost tracking. `Not started`
- [ ] Add TTS audio cache cleanup job to `LinguaCoach.Worker`. `Not started`

### Advanced TTS Voice Configuration (P2)

**Priority:** P2 — after Real TTS Provider is wired and audible in production

**Reason:** Once a real TTS provider is live, product quality depends on voice selection matching the professional tone and learning context. Hardcoded voice choices are a short-term expedient; configurable per-feature voice settings are needed before scaling to multiple activity types and student cohorts.

**Scope:**

- [ ] Define voice configuration model: accent, gender, voice style (e.g. neutral, warm, authoritative), speech rate. `Not started`
- [ ] Add per-feature TTS voice assignment: each AI feature key (`activity_generate_listening`, `placement_assessment_evaluate`) can have its own default voice. `Not started`
- [ ] Add fallback voice/provider: if the primary TTS voice is unavailable, fall back to a configured secondary voice or provider. `Not started`
- [ ] Expose voice configuration in Admin AI Config UI: admin can select voice, accent, style, and speed per feature. `Not started`
- [ ] Add admin preview: play a sample sentence using the configured voice without triggering real activity generation. `Not started`
- [ ] Add regeneration rule: when voice settings change for a feature, mark cached audio for that feature as stale and regenerate on next use. `Not started`
- [ ] Add backend support for `voiceId`, `accent`, `style`, `speedFactor` fields in `AiProviderConfig` or a new `TtsVoiceConfig` entity. `Not started`
- [ ] Evaluate provider-specific voice IDs for OpenAI TTS (`alloy`, `echo`, `fable`, `onyx`, `nova`, `shimmer`) and Azure Neural Voices. `Not started`
- [ ] Document voice/accent choices and their intended use case (e.g. `onyx` for listening exercises — neutral professional male). `Not started`

**Notes:**

- Do not implement before `OpenAiTextToSpeechService` is wired and audible (see Real TTS Provider above).
- Voice config is separate from AI model config — TTS provider selection is environment-configured; voice selection is content/UX policy.
- Current hardcoded voice: `onyx` / `echo` in `PlacementAudioService` and `ListeningAudioService`.

---

### AI Tutor Persona (P2)

- [ ] Define AI teacher name and voice persona (e.g. "Alex" â€” encouraging, professional tone). `Not started`
- [ ] Add AI teacher voice to session-opening micro lessons (text first, TTS audio when provider available). `Not started`
- [ ] Add tutor persona to lesson_reflection step output. `Not started`
- [ ] Avatar is P3 â€” do not design now. `Not started`

---

## Competitive Gap â€” P3 Features `Not started`

- [ ] AI avatar / visual tutor interface. `Not started`
- [ ] Video micro lessons. `Not started`
- [ ] Multimodal workplace uploads (email/doc/screenshot â†’ AI converts to exercise). `Not started`
- [ ] Advanced enterprise analytics (employer dashboard, cohort progress). `Not started`
- [ ] Organisations / teams / employer accounts. `Not started`

---

## SpeakingRolePlay activity MVP (in sprint: speaking-role-play-mvp-sprint) â€” **COMPLETE**

> SpeakingRolePlay MVP was delivered in the speaking-role-play-mvp-sprint (2026-06-08).
> All items below are Done. The Speaking dashboard card is active.

- [x] Add sprint documentation for SpeakingRolePlay MVP. `Done`
- [x] Add `ISpeechToTextService` interface and `FakeSpeechToTextService`. `Done`
- [x] Add `SpeakingAudioService` (store, commit, serve, per-student DB count limit). `Done`
- [x] Add `SpeakingRolePlayEvaluator` (AI evaluation of transcript). `Done`
- [x] Add `activity_generate_speaking_roleplay` prompt seed. `Done`
- [x] Add `activity_evaluate_speaking_roleplay` prompt seed. `Done`
- [x] Add SpeakingRolePlay to `AiActivityGeneratorHandler` (generation + evaluation guards). `Done`
- [x] Add SpeakingRolePlay branch to `ActivityGetHandler` (AI + inline fallback + typed routing guard). `Done`
- [x] Add SpeakingRolePlay branch to `ActivitySubmitHandler` (STT â†’ evaluator dispatch). `Done`
- [x] Extend `ActivityDto` with 8 speaking fields. `Done`
- [x] Extend `ActivityFeedbackDto` with 4 speaking feedback fields. `Done`
- [x] Add `POST /api/activity/{id}/speaking-attempt` (multipart) to `ActivityController`. `Done`
- [x] Add `GET /api/activity/{id}/attempts/{attemptId}/audio` to `ActivityController`. `Done`
- [x] Add `AudioStorageKey` nullable column to `ActivityAttempt` + migration. `Done`
- [x] Add SpeakingRolePlay branch to `ActivityAttemptsHandler` (history). `Done`
- [x] Add speaking states to Angular `activity-lesson` PageState union. `Done`
- [x] Implement speaking recording UI (record, stop, preview, submit). `Done`
- [x] Implement speaking feedback view (transcript, coach summary, strengths, improvements). `Done`
- [x] Update activity history component for SpeakingRolePlay attempts. `Done`
- [x] Activate dashboard Speaking card (remove "Coming soon"). `Done`
- [x] Add config defaults and `.env.example` entries for STT and speaking audio. `Done`
- [x] Add backend integration tests for SpeakingRolePlay. `Done`
- [x] Add Angular unit tests for speaking states. `Done`
- [x] Add Playwright E2E tests for speaking flow. `Done`

---

## VocabularyPractice activity (in sprint: vocabulary-practice-activity-sprint)

- [x] Add `StrengthScore` to `StudentVocabularyItem` + migration T27. `Done`
- [x] Extend `ActivityDto` + `SubmitActivityAttemptCommand` for VocabularyPractice fields. `Done`
- [x] Add `VocabularyPracticeGenerator` (deterministic fill-blank, no AI). `Done`
- [x] Add VocabularyPractice selection logic to `ActivityGetHandler`. `Done`
- [x] Add `VocabularyPracticeEvaluator` (deterministic scoring + vocab status updates). `Done`
- [x] Update `ActivitySubmitHandler` for VocabularyPractice evaluation. `Done`
- [x] Update `ActivityController` response to include VocabularyPractice fields. `Done`
- [x] Update Angular activity-lesson component to render VocabularyPractice. `Done`
- [x] Update activity-history component for VocabularyPractice attempts. `Done`
- [x] Add backend integration tests for VocabularyPractice. `Done`
- [x] Add Angular unit tests and Playwright tests. `Done`

---

## Full app verification and disabled actions cleanup (in sprint: full-app-verification-disabled-actions-cleanup-sprint)

- [x] Add sprint documentation for full app verification and disabled-actions cleanup. `Done`
- [x] Enable implemented Writing, Listening, and Vocabulary dashboard entry points. `Done`
- [x] Keep only unimplemented Speaking and Pronunciation dashboard cards marked "Coming soon". `Done`
- [x] Route dashboard Writing/Listening cards through typed `/activity` requests. `Done`
- [x] Remove stale listening/vocabulary "coming soon" copy from profile and landing surfaces. `Done`
- [x] Add Playwright coverage for implemented dashboard actions. `Done`
- [x] Add Playwright coverage for admin AI usage loading. `Done`
- [x] Add Playwright coverage for student denial on admin-only routes. `Done`

---

## ListeningComprehension text MVP (in sprint: listening-comprehension-text-mvp-sprint)

- [x] Add sprint documentation for the text-based listening MVP. `Done`
- [x] Add `activity_generate_listening` prompt seed. `Done`
- [x] Extend `ActivityDto` and attempt submission contract for listening fields. `Done`
- [x] Add `ListeningComprehension` selection rule in `ActivityGetHandler`. `Done`
- [x] Hide transcript/audioScript and expected answers before submit. `Done`
- [x] Add deterministic listening comprehension evaluation. `Done`
- [x] Reveal transcript and expected answer summaries after submit. `Done`
- [x] Update Angular `/activity` to render listening comprehension. `Done`
- [x] Add backend integration tests for selection, safe DTO, scoring, transcript reveal, and ownership. `Done`
- [ ] Add richer activity history UI for listening attempts. `Planned`
- [ ] Add Playwright coverage for listening activity flow. `Planned`
- [ ] Add real generated audio/TTS. `Not started`
- [ ] Add audio player, replay controls, speed controls, timed captions. `Not started`
- [ ] Add listening-specific memory and skill profile updates. `Not started`

---

## Listening audio/TTS (in sprint: listening-audio-tts-sprint)

- [x] Add sprint documentation for listening audio/TTS. `Done`
- [x] Upgrade `ITextToSpeechService` for generated listening audio. `Done`
- [x] Add fake deterministic TTS provider for tests and local development. `Done`
- [x] Add local audio storage service for listening activities. `Done`
- [x] Store audio metadata in listening activity content. `Done`
- [x] Add authenticated `GET /api/activity/{activityId}/audio` endpoint. `Done`
- [x] Hide transcript and expected answers before submit while exposing only audio URL. `Done`
- [x] Add native audio player to `/activity`. `Done`
- [x] Add listening audio support to activity history. `Done`
- [x] Add config defaults and `.env.example` entries. `Done`
- [x] Add backend tests for safe DTO and audio endpoint auth/ownership/content type. `Done`
- [x] Add Playwright coverage for audio player and no-audio fallback. `Done`
- [ ] Integrate real provider-backed TTS. `Not started`
- [ ] Add audio replay limits, speed controls, and timed captions. `Not started`
- [ ] Add audio cache cleanup. `Not started`
- [ ] Add admin audio usage reporting. `Not started`

---

## Vocabulary extraction (cross-cutting engine, in sprint: vocabulary-extraction-from-writing-attempts-sprint)

> Note: despite the sprint name, extraction is not limited to writing attempts — it fires
> from any activity that produces AI-generated `Corrections` (AiStructured/AiOpenEnded
> patterns), not only legacy `WritingScenario`. See
> `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.

- [x] Add `StudentVocabularyItem` entity, EF config, and migration. `Done`
- [x] Add `vocabulary_extract_from_attempt` AI prompt to `DefaultAiSeeder`. `Done`
- [x] Add `VocabularyExtractionService` (best-effort, post-submit). `Done`
- [x] Wire extraction into `ActivitySubmitHandler` for legacy writing attempts and
      pattern-evaluated activities (`HandlePatternEvaluationAsync`, gated on
      `Corrections.Count > 0`). `Done` (2026-06-12)
- [ ] Add `GET /api/vocabulary` and `PATCH /api/vocabulary/{id}/status` endpoints. `Planned`
- [ ] Add Angular `/vocabulary` page with summary cards, filters, and status buttons. `Planned`
- [ ] Add vocabulary preview section to progress page. `Planned`
- [ ] Add vocabulary nav item to sidebar. `Planned`
- [ ] Add backend integration tests for vocabulary endpoints and extraction. `Planned`
- [ ] Add frontend unit tests and Playwright tests for vocabulary page. `Planned`

---

## Real progress page (in sprint: real-progress-page-sprint)

- [ ] Add `GET /api/progress` endpoint returning summary stats, score trend, skill profile, module progress, learning focus. `Planned`
- [ ] Replace placeholder progress component with real data-driven UI. `Planned`
- [ ] Add backend integration tests for `/api/progress`. `Planned`
- [ ] Add frontend unit tests for progress component. `Planned`
- [ ] Add Playwright tests for progress page (desktop + mobile). `Planned`

---

## Live AI quality review (in sprint: live-ai-quality-review-prompt-calibration-sprint)

- [x] Document live AI quality review sprint plan. `Done`
  - Created `docs/sprints/live-ai-quality-review-prompt-calibration-sprint.md`
- [x] Add live AI quality review report template. `Done`
  - Created `docs/testing/live-ai-quality-review-report.md`
- [x] Add synthetic answer fixtures for manual prompt review. `Done`
  - Created `docs/testing/live-ai-quality-fixtures.md`
  - Fixtures cover direct tone, long unclear answer, missing articles/tense issue, overly casual customer reply, and improved second attempt
- [ ] Run live AI review for Project Planner persona. `Blocked`
  - Requires staging/production access and configured live AI provider credentials
  - Use Project Planner career if seeded; otherwise document Document Controller proxy run
- [ ] Run live AI review for Customer Support Officer persona. `Blocked`
  - Requires staging/production access and configured live AI provider credentials
  - Use Customer Support Officer career if seeded; otherwise document proxy career used
- [ ] Calibrate `learning_path_generate` prompt only if live path evidence shows generic, repetitive, wrong-level, or career-mismatched modules. `Not started`
- [ ] Calibrate `activity_generate_writing` prompt only if live activities lack audience/tone/length clarity or repeat task types. `Not started`
- [ ] Calibrate `activity_evaluate_writing` prompt only if live feedback misses important issues, overwhelms learners, or frames improved text as the answer. `Not started`
- [ ] Calibrate `student_memory_update` prompt only if live memory is noisy, exaggerated, bloated, or drifts after minimal evidence. `Not started`
- [ ] Calibrate `learning_path_generate_adaptive` prompt only if live adaptive modules ignore memory, repeat fingerprints, or generate a generic full path. `Not started`

---

## End-to-end product validation (in sprint: end-to-end-product-validation-learning-quality-sprint)

- [x] Document validation sprint plan and quality criteria. `Done`
  - Created `docs/sprints/end-to-end-product-validation-learning-quality-sprint.md`
- [x] Add UI QA validation report for the writing-learning loop. `Done`
  - Created `docs/testing/e2e-learning-journey-validation-report.md`
- [x] Extend Playwright full-flow coverage through retry, history, memory, module completion, and adaptive generation. `Done`
- [x] Add adaptive module guardrail tests for reason, focusSkill, difficulty, and fingerprint persistence. `Done`
- [x] Add duplicate adaptive fingerprint rejection test. `Done`
- [ ] Seed additional pilot career profiles for validation personas. `Not started`
  - Project Planner
  - Customer Support Officer
  - Deferred because current seed model is reference data, not demo users, and the admin-created student flow must remain part of validation
- [ ] Run live AI quality review with the two validation personas in staging/production. `Not started`
  - Record repetitive activities, generic feedback, noisy memory, or weak adaptive module reasons before changing prompts

---

## Student learning memory (in sprint: student-learning-memory-adaptive-curriculum-sprint)

- [x] Extend `UserLearningSummary` with 6 new JSON fields. `Planned`
  - `journey_summary`, `strong_skills_json`, `weak_skills_json`,
    `recurring_mistakes_json`, `covered_scenarios_json`, `next_focus_json`
  - Domain method `ApplyDelta(MemoryUpdateDeltaDto)` enforces list caps
- [x] Add `StudentSkillProfile` table (skill_key, is_weak per student). `Planned`
  - 10 skill keys: grammar_accuracy, formal_tone, sentence_clarity, message_structure,
    workplace_vocabulary, concise_writing, softening_language, summarising_information,
    clarifying_questions, escalation_language
- [x] Add `fingerprint_json` column to `LearningModule`. `Planned`
  - Fields: communicationMode, scenarioType, audience, tone, difficulty, grammarFocus, vocabularyTheme
- [x] Add xmin concurrency token to `LearningPath`. `Planned`
- [x] Extract `AiExecutionService` shared fallback pattern. `Planned`
- [x] Extract `LearningPathDtoBuilder` shared DTO builder. `Planned`
- [x] Add `student_memory_update` AI prompt to seed data. `Planned`
- [x] Implement `StudentMemoryService` with best-effort update. `Planned`
- [x] Wire memory update into `ActivitySubmitHandler` (8s timeout). `Planned`
- [x] Add `learning_path_generate_adaptive` AI prompt. `Planned`
- [x] Implement `AdaptivePathGeneratorHandler`. `Planned`
- [x] `POST /api/learning-path/generate-next` endpoint. `Planned`
- [x] `GET /api/learning-path/memory` endpoint. `Planned`
- [x] `GET /api/admin/students/{id}/learning-memory` endpoint. `Planned`
- [x] "Your learning focus" panel on dashboard / /my-path. `Planned`
- [x] Module card enrichment (focusSkill, reason, difficulty). `Planned`
- [x] "Generate next modules" button with loading / error states. `Planned`
- [ ] Add staleness flag / alert when memory update fails repeatedly. `Not started`
  - If `UserLearningSummary.UpdatedAt` is > 7 days old and student has recent attempts,
    surface an admin alert or background refresh attempt
- [ ] Add numeric skill score tracking (0â€“100) to `StudentSkillProfile`. `Not started`
  - Deferred from current sprint â€” add after validating the is_weak approach
- [ ] Admin curriculum map editor. `Not started`
  - Currently curriculum is seeded/static for Workplace Writing B1/B1+/B2
  - Future: admin can add career-specific curriculum maps
- [ ] Move memory update to background job. `Not started`
  - Currently synchronous with 8-second timeout in ActivitySubmitHandler
  - When student volume grows, move to LinguaCoach.Worker queue

---

## Progress and activity tracking

- [ ] Implement real practice streak tracking. `Not started`
  - Persist a streak counter per student in the database
  - Increment on each day a new ActivityAttempt is submitted
  - Reset if a calendar day is skipped
- [ ] Track minutes practised this week. `Not started`
  - Derive from ActivityAttempt.CreatedAt timestamps
  - Approximate based on activity type (e.g. WritingScenario â‰ˆ 8 min)
- [ ] Track total activities completed per student. `Not started`
  - Count of `ActivityAttempt` rows per `StudentProfileId`
- [ ] Replace dashboard stat tile placeholders with real backend data. `Not started`
  - Dashboard API (`GET /api/dashboard`) must return: `streakDays`, `minutesThisWeek`, `activitiesDone`
  - Remove `â€”` placeholders once endpoint delivers values
- [ ] Add progress history data for the Progress page. `Not started`
  - `GET /api/progress` or extend dashboard endpoint
  - Return recent ActivityAttempts with score, date, activity type
- [ ] Add per-skill progress values. `Not started`
  - Return progress percentage for: Writing, Speaking, Listening, Vocabulary, Pronunciation
  - Implemented activity types: WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay
  - Pronunciation is not yet implemented; return `null` â€” UI shows "Not started"
  - UI must never fake data

---

## Coach insights

- [ ] Store and retrieve latest AI coach feedback summaries. `Not started`
  - After each ActivityAttempt, persist `whatYouDidWell`, `mainMistakes`, `toneExplanation` to a `CoachInsight` or similar table
  - Retrieve most recent N coach messages per student
- [ ] Show "Latest from your coach" using real recent ActivityAttempt feedback. `Not started`
  - Dashboard coach card currently shows placeholder text
  - Replace with last feedback `feedbackSummary` or first item of `whatYouDidWell`
  - Include score, activity title, and timestamp
- [ ] Show latest completed activity with score and tone summary. `Not started`
  - The completed activity card inside the coach card is currently a placeholder
  - Bind to the most recent `ActivityAttempt` for the student
- [ ] Add coaching trend insights over time. `Not started`
  - Future: "Your tone is improving" / "Grammar errors decreasing" type summaries
  - Deferred until sufficient attempt history exists (suggest 5+ attempts minimum)

---

## Streak system

- [ ] Add daily practice streak persistence. `Not started`
  - Store `LastPracticeDate` and `CurrentStreak` on `StudentProfile` or a separate `StreakRecord` table
  - Increment on new ActivityAttempt, reset if gap > 1 calendar day
- [ ] Add weekly streak calendar data to the API. `Not started`
  - Return an array of 7 booleans (`[M, T, W, T, F, S, S]`) for the current ISO week
  - Dashboard streak calendar will render filled dots for `true` days
- [ ] Add streak reset/continuation logic. `Not started`
  - Grace period: streak continues if the student practises before midnight of the missed day's timezone
  - Timezone must be stored with the student profile
- [ ] Replace empty streak calendar placeholder with real data. `Not started`
  - Currently all 7 dots are empty with "Coming soon" caption
  - Remove caption and bind to real weekly data once available

---

## Future activity types

- [x] Implement SpeakingRolePlay activity type. `Done`
  - Backend: ActivityType value, prompt templates, AI handler, audio upload endpoint, fake STT
  - Frontend: recording UI, transcript, feedback view, history support
  - Dashboard Speaking card active â€” routes to `/activity?type=SpeakingRolePlay`
  - See sprint: speaking-role-play-mvp-sprint.md
- [x] Implement ListeningComprehension text MVP activity type. `Done`
  - Backend: hidden transcript generation, comprehension questions, deterministic scoring
  - Frontend: text-based listening task with transcript reveal after submit
  - Real audio/TTS remains deferred
- [x] Implement VocabularyPractice activity type. `Done`
  - Backend: deterministic practice generation and evaluation
  - Frontend: vocabulary practice rendering, submission, and history support
  - Dashboard links to the implemented vocabulary experience
- [ ] Implement PronunciationPractice activity type. `Not started`
  - Backend: target word/sentence selection, pronunciation scoring via AI or speech API
  - Frontend: microphone UI, waveform or score display
  - Keep Pronunciation card as "Coming soon" until fully implemented
- [ ] Implement ReadingTask activity type. `Not started`
  - Backend: workplace text generation, comprehension questions
  - Frontend: reading + Q&A layout
  - Keep Reading card (if surfaced in UI) as "Coming soon" until implemented
- [ ] Keep unimplemented skill cards (Pronunciation, Reading) visually present but disabled. `Planned`
  - Writing, Listening, Vocabulary, Speaking are implemented and active
  - Pronunciation and Reading remain "Coming soon"
  - Remove "Coming soon" label only when the backend feature is fully wired

---

## Profile page

- [ ] Replace placeholder profile rows with real user/profile data. `Not started`
  - Learning goal: read from `StudentProfile.LearningGoal` or `LearningTrack.Name`
  - Current level: read from `PlacementResult.estimatedOverallLevel` (source of truth); fall back to `StudentProfile.CefrLevel` only if placement not yet completed
  - Practising: read from `LanguagePair.TargetName` + skill focus
  - Career context: read from `CareerProfile.Name`
- [ ] Add editable learning preferences if needed. `Not started`
  - Deferred â€” read-only display is sufficient for pilot phase
- [ ] Add language pair and career context display. `Not started`
  - Show "Persian â†’ English Â· Document Controller" or equivalent
  - Read from `StudentProfile` joined to `LanguagePair` and `CareerProfile`
- [ ] Add account/security links if needed. `Not started`
  - Change password link at minimum
  - Privacy / data deletion for compliance if required later

---

## Progress page

- [ ] Replace placeholder stat tiles with real ActivityAttempt summaries. `Not started`
  - Day streak: from streak system (see Streak system section)
  - Activities done: count of `ActivityAttempt` rows
  - Avg score: mean of `ActivityAttempt.Score` values (exclude null)
- [ ] Add skill progress bars with real values. `Not started`
  - Writing progress: derive from number of completed Writing activities vs. module target
  - Other skills: return `0` or `null` with "Not started" label until implemented
- [ ] Add module completion history. `Not started`
  - Show completed modules with completion date
  - Show in-progress module with current activity count
- [ ] Add recent scores list with improvement trend. `Not started`
  - Show last 5â€“10 ActivityAttempts: title, score, skill badge, date
  - Optionally show trend arrow (improving / stable / needs work) if 3+ results available

---

## My Path improvements

- [ ] Add richer module details from real progress data. `Not started`
  - `isCurrent`, `completedActivities`, `totalActivities` already returned by API
  - Ensure `moduleStatus()` in `LearningPathComponent` reflects real data correctly
- [ ] Add ability to tap a module card to view its activities. `Not started`
  - Currently module cards link to `/activity` generically
  - Future: `/my-path/modules/:moduleId` showing the activity list for that module
- [ ] Add path regeneration or adjustment capability. `Not started`
  - Admin or AI trigger to regenerate path based on progress
  - Not needed for pilot phase â€” defer

---

## Design system follow-ups

- [x] Extract repeated app shell into layout components. `Done`
  - Shell HTML removed from all page components
  - `StudentAppLayoutComponent` owns sidebar, header, bottom nav
  - `AdminAppLayoutComponent` owns left sidebar, header
  - `PublicLayoutComponent` owns centered background wrapper
  - Pages render content only â€” no shell duplication remains
- [ ] Extract `StatCard` component. `Planned`
  - Repeated 3Ã— on dashboard and progress page
  - Input: `icon`, `value`, `label`, `color`, `bg`
- [ ] Extract `SkillCard` component. `Planned`
  - Repeated on dashboard and progress page
  - Input: `skill`, `level`, `pct`, `active`, `routerLink`
- [ ] Extract `ModuleCard` component. `Planned`
  - Used on My Path page; candidate for Activity selection page later
- [ ] Extract `CoachCard` / `CoachMessage` component. `Not started`
  - Used on dashboard right column and feedback phase
- [ ] Extract `ScoreRing` component. `Not started`
  - SVG ring used in My Path header and feedback phase
  - Input: `value`, `size`, `stroke`, `color`
- [ ] Keep `sp-*` utility classes documented in `speakpath-design-system.md`. `Planned`
  - Update whenever new classes are added to `styles.css`
- [ ] Add screenshot/visual reference notes for future agents. `Not started`
  - Annotated screenshots of prototype screens in `docs/design/references/`
  - Reference prototype JSX files when making UI decisions

---

## Admin dashboard â€” real data

- [ ] Replace KPI card placeholders with real counts. `Not started`
  - "Total students" already live from `GET /api/admin/students` count
  - "Onboarded" already live from same endpoint (filter by `onboardingStatus === 'Complete'`)
  - "Activities tracked": requires `GET /api/admin/stats` endpoint returning `totalActivityAttempts`
  - "AI provider": hardcoded "Configured" â€” wire to check if at least one provider has a non-null API key
- [ ] Implement real usage analytics. `Not started`
  - AI token usage per provider per day: log `promptTokens` + `completionTokens` from AI responses
  - Cost estimate per student: requires provider pricing table in DB
  - Expose via `GET /api/admin/usage?from=&to=`
- [ ] Implement activity completion trends. `Not started`
  - `GET /api/admin/analytics/activity-completion` returning daily counts over a date range
  - Chart on admin analytics page once data exists
- [ ] Implement feedback quality review. `Not started`
  - Score distribution histogram across all ActivityAttempts
  - Average score per skill type, per career profile
  - Flag unusually low-quality feedback (score = null more than X% of attempts)
- [ ] Add system health / API health card to admin dashboard. `Not started`
  - Real-time ping to each configured AI provider (or display last test result from ai-config)
  - Show green/amber/red per provider
  - Do not fake â€” only show if a recent test result is stored
- [ ] Build admin settings page. `Not started`
  - Route: `/admin/settings`
  - Planned content: platform name/branding config, pilot programme dates, allowed email domains
  - Currently a placeholder / disabled nav item
- [x] Improve admin student list page. `Done` (2026-06-14)
  - [x] Add search/filter by email and name
  - [x] Add sort by name, onboarding status, or joined date
  - [x] Add pagination (25 per page)
  - [x] Add ability to view individual student (detail page with learning memory). `Done` (2026-06-14)
  - [x] Activity history view. `Done` (2026-06-14)
- [x] Add admin student learning memory view. `Done` (2026-06-14)
  - `GET /api/admin/students/{id}/learning-memory` now consumed by `/admin/students/:id`
  - Shows journey summary, strengths, weaknesses, recurring mistakes, next focus, covered scenario count, skill profile
- [x] Design system: use `sp-admin-*` classes consistently across all admin components. `Done` (2026-06-14)
  - `admin-prompts` and `admin-careers` migrated from raw Tailwind to sp-admin-* (sp-admin-table, sp-admin-form-card, sp-admin-field-grid, sp-input, sp-admin-btn-primary, sp-admin-badge)
- [x] Improve admin mobile drawer. `Done` (2026-06-14)
  - Added swipe-to-close gesture
  - Added route-change auto-close (NavigationStart subscription in AdminAppLayoutComponent)
  - Added keyboard Escape to close drawer and profile menu
- [ ] Add admin mobile bottom navigation or persistent tab bar as alternative to drawer. `Not started`
  - Drawer pattern is sufficient for a desktop-first admin tool used on mobile rarely
  - If admin mobile usage is significant, consider a simplified bottom tab bar for admin

---

## Learning path progression (post Learning Path Progression sprint)

- [ ] Persist explicit module completion confirmation to a dedicated `ModuleCompletion` table instead of `CompletedAt` column, to support future multi-path learners. `Not started`
- [ ] Show per-module score history chart (last 5 attempts per module). `Not started`
- [ ] Module completion certificates or achievement badges. `Not started`
- [ ] Admin / teacher progress view per student (current module, focus area, average score). `Not started`
- [ ] Long-term trend chart: score progression over 10+ attempts. `Not started`
- [ ] Spaced repetition for repeated mistakes (re-surface activities in weak categories). `Not started`
- [ ] Auto-advance module when ready without explicit student confirmation (optional preference). `Not started`
- [ ] Notify student when module is ready to complete (push notification or dashboard badge). `Not started`

---

## Learning experience improvements (post Learning Experience sprint)

- [ ] Richer attempt history page showing all attempts side by side. `Not started`
  - Route: `/activity/history/:activityId`
  - Show all `ActivityAttempt` rows for a given activity, ordered by date
  - Each row: attempt number, score, date, first few words of submission
- [ ] Side-by-side diff viewer for attempts. `Not started`
  - Compare attempt N with attempt N-1 visually
  - Highlight what changed between submissions
  - Deferred â€” requires richer attempt history page first
- [ ] Inline sentence-level comment annotations. `Not started`
  - AI returns comments anchored to specific sentence positions
  - UI renders inline margin annotations (like Google Docs)
  - Requires new AI prompt output format
- [ ] Teacher / admin review of AI feedback quality. `Not started`
  - Admin can browse recent `ActivityAttempt` feedback JSONs
  - Admin can flag poor feedback for prompt review
  - Requires admin UI extension
- [ ] Skill-based progress analytics from `changes.category` data. `Not started`
  - Aggregate `changes.category` values from recent attempts per student
  - Show: "Grammar is your most common issue this week"
  - Requires data aggregation query on `FeedbackJson` or new field on `ActivityAttempt`
- [ ] Vocabulary extraction from writing attempt mistakes. `Not started`
  - Extract vocabulary from `vocabularyIssues` and `changes` with category=vocabulary
  - Add to student's vocabulary list for spaced repetition
  - Requires vocabulary tracking feature
- [ ] Speaking and listening activity types. `Not started`
  - See future activity types section above
- [ ] Client-side LCS-based visual diff for richer comparison. `Not started`
  - Compute diff between student's draft and improved version in the browser
  - Highlight word-level insertions and deletions
  - Lower priority â€” server-side `changes` list already covers this

---

## Legacy database cleanup

> âš ï¸ These items require explicit confirmation before execution. Do not run without a backup.

- [ ] T19: Confirm no active FK dependency on `SourceWritingScenarioId` in `LearningActivity`. `Planned`
  - Verify all current activities use `aiGeneratedContentJson` not `SourceWritingScenarioId`
  - Query: `SELECT COUNT(*) FROM learning_activities WHERE source_writing_scenario_id IS NOT NULL`
- [ ] Backfill any required legacy scenario content into `aiGeneratedContentJson`. `Blocked`
  - Blocked on T19 confirmation above
- [ ] Export and back up `writing_scenarios` and `writing_submissions` tables. `Not started`
  - Export to CSV or S3 before any schema changes
- [ ] Remove `SourceWritingScenarioId` FK from `LearningActivity`. `Not started`
  - EF Core migration: drop column and FK constraint
  - Remove `WritingScenario` domain entity and all references
- [ ] Drop legacy `writing_scenarios` and `writing_submissions` tables. `Not started`
  - Only after backup confirmed and no active references
  - Requires explicit confirmation from user before execution
