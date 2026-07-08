---
status: current
lastUpdated: 2026-07-08 (Phase E1)
owner: architecture
supersedes:
supersededBy:
---

# English Resource Bank Import, Review, Preview, and Publishing Platform (Phase E)

**Date planned:** 2026-07-08 (Plan-Sync-After-C1), **finalized:** 2026-07-08 (Phase E0),
**E1 implemented:** 2026-07-08
**Status:** E1 (staging foundation) implemented. E2 (AI analysis + validation gates) not started.
**No rows have ever been written to any published `Cefr*` bank table** — E1 is staging-only, by
design and by test (`No_rows_are_ever_written_to_any_published_cefr_bank_table`).
**Supersedes the informal "seed CEFR-J/UniversalCEFR data" framing** used in earlier planning
docs (`docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md` §4.6/§9,
`docs/architecture/cefr-resource-licensing-review.md`). Those docs' licensing findings still
apply — this doc replaces the *shape* of the work (a full pipeline, not a one-shot seed script).

**Phase E0 (2026-07-08) finalized the entity model, status/gate model, and E1-E4 scope below**,
after auditing the existing bank-first architecture (`ActivityTemplate`, `PlacementItemDefinition`,
`AdminReviewStatus`, `IFormIoSchemaValidationService`, `IActivityContentFingerprintService`,
`IFileStorageService`, the feature-gate system, and the `Cefr*` entities themselves). **One
decision in this section supersedes the Plan-Sync-After-C1 placeholder below**: the source
registry reuses the existing `CefrResourceSource` entity directly rather than adding a new
`ResourceImportSource` entity — see "Final entity model" below for why. No app code, migrations,
or config were changed in E0 — this is a planning-only phase.

---

## Product rule (non-negotiable)

**SpeakPath teaches English with English.**

- Every resource bank entry (vocabulary, grammar, reading, listening, template seed content) is
  **English-only**.
- **Supported languages (Persian, etc.) are runtime-only support** — used for UI chrome,
  onboarding language-pair selection, "explain in your language" support-language hints, and
  translation-help preferences. They are **never** seeded as learning content, and never part of
  an import source.
- **No Persian seed corpus.** No bilingual phrase bank. No English–Persian (or any
  English–X) parallel-text import of any kind, at any phase.
- **No direct upload to final tables.** Every imported record passes through the full pipeline
  below — there is no shortcut that writes straight into a published bank table.

This rule governs every phase below and every future resource-bank source. If a future source
under consideration only exists as a bilingual corpus, the correct action is to reject it or
strip it to its English-only subset before it enters the pipeline — never to import the
non-English side as "seed" content.

---

## Why a platform, not a seed script

The original Phase E framing (informally: "import CEFR-J/UniversalCEFR data once, licensing
permitting") undersold the actual shape of this work. Resource banks will need multiple sources
over time (vocabulary, grammar, reading, listening, and later audio/speech corpora), each with
different licenses, formats, and quality levels, and each needing human review before student
content banks trust it. A one-shot script does not give admins visibility into what was
imported, why a record was accepted or rejected, or a way to preview exactly what a student
would see before it's published. Phase E is therefore planned as a **repeatable, auditable
pipeline with an admin-visible staging area**, not a migration-time data dump.

---

## Pipeline

```
Source registry
    → Import run
    → Raw records (as-imported, untouched)
    → Candidate analysis (AI + deterministic checks, CEFR/skill/subskill classification)
    → Validation (schema, license, forbidden content, English-only enforcement)
    → Admin rendered preview (exactly what a student would see)
    → Review (approve / reject, with reason)
    → Publish to English resource banks (CefrVocabularyEntry / CefrGrammarProfileEntry /
      CefrReadingReference / CefrDescriptor / future reading-passage & listening-script banks)
```

Every stage is a distinct, queryable state — a record can be inspected at "raw," "candidate,"
"validated," or "published" without ambiguity, and a rejected record's reason is preserved for
audit rather than silently discarded.

---

## Final entity model (decided in Phase E0)

### Source registry: reuse `CefrResourceSource`, do not add `ResourceImportSource`

The Plan-Sync-After-C1 placeholder above proposed a new `ResourceImportSource` entity "reusing
`CefrResourceSource` where it already fits." The E0 audit found this backwards: **`CefrResourceSource`
already IS a complete source registry** — `Name`, `LicenseType`, `SourceUrl`, `UsageRestrictionNotes`,
`IsImportApproved` (bool), `ImportedAtUtc`, plus `ApproveForImport(notes?)` / `RevokeApproval(reason)`
/ `RecordImport(importedAtUtc)` (the last throws if not approved). Adding a second, near-identical
`ResourceImportSource` entity alongside it would duplicate exactly the fields this project's own
"do not duplicate existing good entities without reason" convention exists to prevent.

**Decision: `CefrResourceSource` is the one source registry for all Phase E resource types** —
vocabulary, grammar, reading, and (later) pronunciation/audio sources all register here, not just
CEFR-labeled content. Its name predates this broader use but is not worth a rename/migration for a
cosmetic reason. No new fields are needed for E1; `ResourceImportRun` (below) carries the
per-import file format, since one source can offer files in more than one format over time.

### Staging entities: new, as originally named

**`ResourceImportRun`**, **`ResourceRawRecord`**, **`ResourceCandidate`** are new entities — the
Plan-Sync-After-C1 naming holds (not `SeedImportRun`/`SeedRawRecord`/`SeedCandidate`), for two
reasons: (1) "Resource" matches the platform's own name and the target `Cefr*` "resource bank"
entities it publishes into; (2) "Seed" is already a loaded term in this codebase for the existing
idempotent code-authored `*Seeder` classes (`ActivityTemplateSeeder`, `PlacementItemBankSeeder`) —
reusing it for external-data staging would blur two unrelated mechanisms.

- **`ResourceImportRun`** — one row per admin-triggered import execution. `SourceId` (FK →
  `CefrResourceSource`), `FileFormat` (`Csv`/`Json`/`Jsonl`), `OriginalFileName`, `FileStorageKey`
  (via `IFileStorageService`, category `"resource-import"`), `Status` (`ResourceImportRunStatus`),
  `StartedAtUtc`, `CompletedAtUtc?`, `RawRecordCount`, `ErrorSummary?`, `TriggeredByAdminUserId`.
- **`ResourceRawRecord`** — one row per as-imported source row, untouched. `ImportRunId` (FK),
  `RowNumber`, `RawContentJson` (verbatim), `ExtractionStatus` (`ResourceRawRecordStatus`),
  `ParseErrorMessage?`.
- **`ResourceCandidate`** — one row per raw record mapped into a normalized shape, the main
  admin-review unit. `RawRecordId` (FK), `SourceId` (denormalized for query convenience),
  `BankType` (`ResourceBankType` — `Vocabulary`/`Grammar`/`Reading`/`Pronunciation`/...),
  `CandidateDataJson` (normalized structured guess — placeholder mapping in E1, AI-assisted in
  E2), `CefrLevel?`, `Skill?`, `Subskill?`, `ValidationStatus` (`ResourceCandidateValidationStatus`),
  `ValidationErrorsJson?`, `ContentFingerprint?` (populated in E2, reusing
  `IActivityContentFingerprintService`'s SHA-256/normalized-JSON approach — no new fingerprinting
  algorithm), `ReviewStatus` (reuses the existing `AdminReviewStatus` enum — see "Status/gate
  model" below), `IsPublished` (bool, mirrors `ActivityTemplate.IsPublished`), `PublishedAtUtc?`,
  `PublishedEntityType?`/`PublishedEntityId?` (which `Cefr*` row it became, for traceability).

### Published resources: hybrid — reuse typed `Cefr*` entities, no polymorphic table

Rejected the single polymorphic `ResourceBankItem` + type-specific-JSON option. The existing
`CefrVocabularyEntry` (`Word`/`PartOfSpeech`/`Notes`), `CefrGrammarProfileEntry`
(`GrammarPoint`/`Description`), and `CefrReadingReference` (`TextType`/`ReferenceExcerpt`) already
have clean, purpose-built typed schemas — cramming them into one JSON-blob table would be a step
backward from the established `ActivityTemplate`/`PlacementItemDefinition` convention of explicit
typed fields, and would need a translation layer everywhere they're read anyway.

**Decision: hybrid, incrementally-typed.** E4 publishes into the existing, already-defined
`CefrVocabularyEntry` and `CefrGrammarProfileEntry` (vocabulary + grammar — the two banks with the
most resolved E1/E2 source candidates). E6 publishes into the existing `CefrReadingReference`. Any
future pronunciation/listening/speaking bank (E7+) gets its own new typed entity **when that phase
actually starts** — not built speculatively now. `CefrDescriptor` (can-do statements) is reference
data the pipeline may populate later but is not a named target for E1-E6.

---

## Status/gate model (decided in Phase E0)

| Concept | Model | Notes |
|---|---|---|
| Source approval | `CefrResourceSource.IsImportApproved` (existing bool) + `ApproveForImport`/`RevokeApproval` | Reused as-is — no new enum. A source with `IsImportApproved=false` cannot have any `ResourceImportRun` created against it (enforced at run-creation time, not just at publish time). |
| Import run status | New `ResourceImportRunStatus`: `Pending`, `Running`, `Completed`, `CompletedWithErrors`, `Failed` | Per-run; a run can complete with some raw records `ParseFailed` without the whole run failing. |
| Raw record extraction status | New `ResourceRawRecordStatus`: `Extracted`, `ParseFailed` | Per-row, continue-on-error — one bad CSV row never aborts the run (mirrors the per-item try/catch convention already used by `PracticeGymGenerationJob`). |
| Candidate validation status | New `ResourceCandidateValidationStatus`: `PendingValidation`, `Passed`, `Failed` | Mechanical/automatic outcome only (gates 1-6 below); `ValidationErrorsJson` records structured reasons. Deliberately separate from review status — validation is a machine judgment, review is a human one. |
| Candidate review status | Reuses existing `AdminReviewStatus` (`NotRequired`/`PendingReview`/`Approved`/`Rejected`) | Same enum as `ActivityTemplate`/`PlacementItemDefinition` — per the explicit instruction not to invent a parallel review-status mechanism. |
| Publish status | New `ResourceCandidate.IsPublished` bool + `PublishedAtUtc`/`PublishedEntityType`/`PublishedEntityId` | Mirrors `ActivityTemplate.IsPublished`. A candidate can only be published once `ReviewStatus == Approved` and `ValidationStatus == Passed`. |

### The 7 gates, and which are automatic vs. admin-reviewed

1. **English-only gate** — **automatic**, no override. Deterministic language check on every
   candidate's text fields (script/character-set check at minimum; a lightweight language-id
   check if a suitable library is already available — no new heavy dependency for this alone).
   This is the one gate an admin cannot approve past — it enforces the non-negotiable product
   rule, not a quality preference.
2. **License gate** — **automatic**. Derived from `CefrResourceSource.IsImportApproved` at
   **import-run creation time** (not just at publish time) — an unapproved source cannot even
   produce raw records, so a licensing mistake can't accumulate staged data before anyone notices.
3. **Parser gate** — **automatic**, per-row. A row that doesn't parse into the expected
   CSV/JSON/JSONL shape is marked `ParseFailed` on its `ResourceRawRecord` and simply excluded
   from candidate creation — it does not abort the run.
4. **AI analysis gate** — **automatic, advisory only**. Suggests CEFR level/skill/subskill/tags
   using the same retry-once-then-fail pattern as `ActivityTemplateInstanceGenerator`. A failed AI
   suggestion never blocks a candidate — it just leaves those fields for manual admin
   classification. AI output here is a suggestion, never an authority.
5. **Rule validation gate** — **automatic**. Deterministic: required fields present, CEFR
   level/skill/subskill valid against `Domain.Constants`, no forbidden-content patterns
   (script/injection-style content, obviously non-English content beyond gate 1's basic check,
   length bounds).
6. **Dedup/fingerprint gate** — **automatic**. Reuses `IActivityContentFingerprintService`'s exact
   SHA-256-of-normalized-JSON approach (sorted keys, lowercased/whitespace-collapsed leaves) against
   a per-source, per-bank-type namespace — flags (does not silently drop) exact-duplicate
   candidates for admin attention.
7. **Admin review/publish gate** — **the only manual gate**. Admin views the rendered preview
   (E3), then approves or rejects with a reason (`AdminReviewStatus`, gate 7a), then a separate
   explicit publish action (E4, gate 7b) maps `CandidateDataJson` into the target `Cefr*` row.
   Approval and publish are two distinct actions, not one click — mirroring
   `ActivityTemplate.Approve()`/`.Publish()` being separate methods.

Gates 1-3 run in E1. Gates 4-6 run in E2. Gate 7 is split across E3 (preview, prerequisite to
enabling approval) and E4 (approve/reject + publish action).

---

## Phase breakdown (E0–E8)

| Phase | Goal | Scope |
|---|---|---|
| **E0** ✅ done 2026-07-08 | Final model/planning for the resource import platform | Finalized entity model (above), status/gate model (above), E1-E4 exact boundaries (below), admin route/page list (below). No code, migrations, or config changed. |
| **E1** | Source registry integration + CSV/JSON/JSONL import + raw/candidate staging | **First implementation slice** — see "E1 exact scope" below. Gates 1-3 only (English-only, license, parser). **Does not publish anything.** |
| **E2** | AI analysis + validation gates | Gates 4-6 (AI analysis/advisory, rule validation, dedup/fingerprint). See "E2-E4 boundaries" below. |
| **E3** | Admin rendered preview | Gate 7a prerequisite — dedicated read-only rendered view per candidate, required before approval is enabled. |
| **E4** | Publish to first banks | Gates 7a (approve/reject) + 7b (publish action) — promotes an approved, validation-passed candidate into `CefrVocabularyEntry`/`CefrGrammarProfileEntry`. |
| **E5** | Published bank browsing/search | Admin (and eventually `ActivityTemplate`/AI-generation-time) search/browse over published bank content — filter by CEFR/skill/subskill/context, not just an admin CRUD list. |
| **E6** | Reading/listening resources | Extend the pipeline to `CefrReadingReference`-shaped content (passages) — same pipeline, new candidate/validation rules for longer-form text. A first listening-script bank (new typed entity) may also land here once audio-adjacent metadata handling is designed. |
| **E7** | Bigger import support | Background/queued import jobs (Quartz) for larger sources, ZIP archive support, audio-file-carrying sources (Common Voice, LibriVox) — deferred until E1-E6 prove the pipeline on simpler text sources first. |
| **E8** | RAG/search enrichment | Embedding/vector-search-based candidate deduplication and semantic bank search — explicitly deferred past all of E0-E7; no pgvector, no embeddings before this phase, consistent with Phase B's repetition/novelty foundation scope discipline. |

---

## E1 exact scope — implemented (2026-07-08)

**Entities**: `ResourceImportRun`, `ResourceRawRecord`, `ResourceCandidate` (new, migration
`AddResourceImportStaging`). **`CefrResourceSource` WAS extended** — the E0 plan assumed no new
fields were needed, but implementation found E1's own admin-page requirements (language/source-kind
metadata, license/commercial-use/student-display flags, attribution text, version/download URL)
needed real fields: `LanguageCode` (must be `"en"` — enforced in the constructor/`Update`, this
entity can never represent a non-English source), `AllowsStudentDisplay` (bool),
`AllowsCommercialUse` (bool), `AttributionText` (string?), `SourceVersion` (string?),
`DownloadUrl` (string?), `UpdatedAtUtc`, plus a new `Update(...)` method (metadata edit, separate
from `ApproveForImport`/`RevokeApproval` — an edit never silently changes approval status).

**Backend** (`ResourceImportService`, `src/LinguaCoach.Infrastructure/ResourceImport/`): admin
uploads a CSV/JSON/JSONL file directly via multipart form (processed in-memory from the request
stream — **not** persisted through `IFileStorageService`, since the uploaded file is ephemeral
import input, not a long-lived asset like audio; this is a deliberate deviation from the E0 plan's
assumption). Gate 2 (license + English-only source) blocks **before any `ResourceImportRun` row is
created** — an import against an unapproved or non-English source is a caller/process error, not a
data-quality issue worth a run record. A conservative 5MB file-size cap applies (documented in
code — no existing upload convention in this codebase targets structured-data files at this
size). Per row: duplicate-hash check → gate 1 (English-only: explicit `languageCode`/`language`/
`lang` field if present, else a conservative Arabic/Persian-script + non-Latin-proportion
heuristic — documented limitation: this does not catch non-English text in Latin script, e.g.
French/Turkish) → gate 3 (must have at least one recognizable content field: `word`/`lemma`/
`text`/`passage`/`title`/`grammarKey`/`formIo`/`schema`/`template`) → stage `ResourceRawRecord`
(`Parsed`) + exactly one `ResourceCandidate` (`ValidationStatus = NeedsReview`, `ReviewStatus =
NotRequired` — no review workflow exists yet in E1, matching `AdminReviewStatus`'s own documented
semantics for "not yet applicable"). Rejected rows get a `ResourceRawRecord` (`Rejected`) with a
warning reason but **no `ResourceCandidate`**. One malformed row never aborts the run
(continue-on-error, matching `PracticeGymGenerationJob`'s per-item try/catch convention); only a
fundamentally unreadable file (not valid CSV/JSON/JSONL at all) fails the whole run.
`ContentFingerprint` reuses `IActivityContentFingerprintService` (no new fingerprint logic).

**Admin pages** (all under the existing "Content" sidebar group), routes as planned:
- **Resource Sources** (`/admin/resource-sources`) — `CefrResourceSource` CRUD + approve/revoke.
- **Resource Import Runs** (`/admin/resource-import-runs`) — list per source, trigger new run
  (file upload), status, record counts; **Raw Records shown as a nested tab/section on the run's
  detail view**, not a separate top-level nav item.
- **Resource Candidates** (`/admin/resource-candidates`) — list with source/import-run/candidate-
  type/validation-status/review-status/language/CEFR/search-text filters; detail view showing raw
  + candidate JSON side by side; explicit "staging only — publishing arrives in Phase E4" banner.
  **No rendered preview (E3), no approve/reject/publish action (E4)** — read-only + `AdminNotes`-
  only edit, per the explicit E1 scope boundary.

**Explicitly NOT in E1** (confirmed, none of these were built): full AI analysis (E2), full
rendered preview (E3), publishing to any bank (E4) — **no tiny "safe" early publish target
either**, confirmed by a dedicated test (`No_rows_are_ever_written_to_any_published_cefr_bank_table`);
embeddings/pgvector (E8); audio ZIP import (E7); generic web scraping; Persian/bilingual data (a
Persian-script fixture exists only as a negative test proving gate 1 rejects it, never as staged/
approved content); Today composer migration (Phase D, separate).

**E1 acceptance criteria — met**: an admin can approve a `CefrResourceSource` (English-only,
language-code-enforced), upload a CSV/JSON/JSONL file against it, see the resulting
`ResourceImportRun` with an accurate record count and per-row parse status, and see the resulting
`ResourceCandidate` rows — with zero rows ever written to any published `Cefr*` bank table.
+17 backend tests covering both gates, all 3 formats, duplicates, malformed rows, fingerprint
stability, and the bank-table-untouched guarantee.

---

## E2–E4 boundaries

### E2 — AI analysis + validation gates
- **Backend**: a new AI-analysis service reusing `ActivityTemplateInstanceGenerator`'s
  retry-once-then-fail pattern to suggest CEFR level/skill/subskill/tags per candidate (advisory
  only, gate 4); a deterministic rule-validation pass (gate 5: required fields, valid CEFR/skill/
  subskill against `Domain.Constants`, forbidden-content patterns); a dedup pass reusing
  `IActivityContentFingerprintService` (gate 6), flagging (not silently dropping) duplicates.
- **Admin pages**: extend Resource Candidates with AI-suggested (editable) metadata, a
  validation-status filter/badge, and a "run validation" action.
- **Tests**: AI-enrichment retry/failure handling with a fake AI provider (matching the existing
  `ActivityTemplateInstanceGenerator` test convention); rule-validation unit tests per gate 5
  check; fingerprint/dedup unit tests with fixed input → known hash, mirroring
  `ActivityContentFingerprintService`'s own test style.
- **Acceptance**: every candidate has a definitive `ValidationStatus` (`Passed`/`Failed`) with
  structured `ValidationErrorsJson` reasons when failed; AI suggestions are always editable and
  never auto-approve or auto-publish; still zero rows published.
- **Explicitly deferred**: embeddings/semantic dedup (E8), the rendered preview UI (E3).

### E3 — Admin rendered preview
- **Backend**: a read-only "preview projection" endpoint per candidate, rendering the bank-type-
  specific student-facing shape (e.g. a vocabulary-entry card: word/CEFR level/part of speech; a
  grammar-entry card: grammar point/description) — **not** a Form.io schema render. This is a
  deliberate departure from `app-formio-renderer`: these are reference-data bank entries, not
  interactive Form.io activities, so a dedicated read-only preview card (reusing
  `sp-admin-card`/`sp-admin-drawer` from the shared admin component library) is the right shape,
  not the Form.io renderer used by Placement/Activity Templates.
- **Admin pages**: a Candidate Preview drawer/page — rendered card + a raw/candidate JSON toggle
  for debugging, plus the gate 1-6 validation results.
- **Acceptance**: the approve action (gate 7a) is **UI-disabled until the admin has opened the
  rendered preview at least once for that candidate** — "admin must not approve invisible JSON
  only" is enforced as a UI gate, not just a written convention.
- **Explicitly deferred**: the publish action itself (E4), bulk approve/reject actions.

### E4 — Publish to first banks
- **Backend**: a `PublishCandidateHandler` — requires `ReviewStatus == Approved` AND
  `ValidationStatus == Passed` AND the preview-viewed UI gate from E3; maps `CandidateDataJson`
  into a new `CefrVocabularyEntry` or `CefrGrammarProfileEntry` row (matched by `BankType`); sets
  `ResourceCandidate.IsPublished=true`/`PublishedAtUtc`/`PublishedEntityType`/`PublishedEntityId`;
  links the published row's provenance back to `SourceId`.
- **Admin pages**: a Publish action on the Candidate detail/review page (enabled only once the E3
  gate and E4 preconditions are met). Full published-bank browsing is **E5**, not E4 — E4 adds
  only the publish action and a minimal confirmation, not a browse/search UI.
- **Tests**: publish handler correctly maps fields per bank type; idempotency (a candidate cannot
  be published twice); rejected/pending/validation-failed candidates cannot be published (guard
  tests for each precondition).
- **Acceptance**: approved, validated, preview-viewed candidates targeting vocabulary or grammar
  produce real `CefrVocabularyEntry`/`CefrGrammarProfileEntry` rows; reading/listening/speaking
  banks remain empty until E6+.
- **Explicitly deferred**: published-bank browsing (E5), reading/listening banks (E6),
  background/queued large imports + ZIP + audio sources (E7), RAG/embeddings/semantic dedup (E8).

---

## Initial source priorities

In rough intended order, **license permitting** (nothing here is import-approved yet — see
`docs/architecture/cefr-resource-licensing-review.md`, which this doc does not supersede on the
licensing question):

1. **CEFR-J / Open Language Profiles** — vocabulary and grammar, English-only subset.
2. **CMUdict** — pronunciation/phoneme reference data (public domain, CMU).
3. **Internal AI-generated ActivityTemplate seed pack** — original content already produced by
   this project (e.g. the Phase C1 `ActivityTemplateSeeder` templates), not a third-party import,
   but tracked through the same registry/provenance discipline for consistency.
4. **Common Voice English** (later, Phase E7+) — audio corpus, English-only subset.
5. **LibriVox English** (later, Phase E7+) — public-domain audiobook/text corpus, English-only.
6. **Project Gutenberg English / public-domain texts** (later, Phase E7+) — reading passages.

### Explicit exclusion

**No British Council, Cambridge, Oxford, or BBC Learning English lesson scraping or copied
content, at any phase.** These are commercially licensed educational materials; scraping or
reproducing them would be a licensing violation regardless of technical feasibility. This is a
hard exclusion, not a "license permitting" conditional like the sources above.

---

## Admin UX / navigation plan (finalized in Phase E0)

All new pages live under the existing **Content** sidebar group (alongside Lessons, Curriculum,
Exercise Types, Onboarding, Placement items, Activity templates, Review queue) — not a new
top-level nav section. The "Content" vs "AI System" split is separately flagged for cleanup in
Phase G once enough bank-first admin pages exist (`docs/roadmap/road-map.md` §19a); Phase E adds
to that eventual cleanup's scope but does not attempt it now.

| Page | Route | Phase | Notes |
|---|---|---|---|
| Resource Sources | `/admin/resource-sources` | ✅ E1 done | `CefrResourceSource` CRUD + approve/revoke (extended with new fields, not a new entity). |
| Resource Import Runs | `/admin/resource-import-runs` | ✅ E1 done | List + trigger new run (file upload). Raw Records are a nested tab on the run detail view, not a separate top-level page. |
| Resource Candidates | `/admin/resource-candidates` | ✅ E1 done (read-only + staging banner) → E2 (validation) → E3 (preview) → E4 (approve/publish) | The main working surface, built up incrementally across E1-E4 rather than all at once. |
| Published Resource Banks | `/admin/resource-banks` (tentative) | E5 | Browse/search published `Cefr*` rows — not part of E4. |
| Tags/Taxonomy | TBD | Later, post-E4 | Deferred until enough banks exist to warrant a shared taxonomy admin surface. |

Shared components to reuse (no new UI primitives to invent): the `design-system/admin` component
library (`sp-admin-card`, `sp-admin-table`/`data-table`, `sp-admin-drawer`, `filter-bar`,
`pagination`, `page-header`, `badge`/`status-card`, `empty-state`/`error-state`/`loading-state`)
and, for source/run/candidate list-and-detail pages, the same list/editor page pattern already
proven by `admin-activity-templates`/`admin-activity-template-editor` and
`admin-placement-items`/`admin-placement-item-editor`.

**Non-negotiable UX rule carried into E3/E4**: admin must view the rendered preview before
approving a candidate — approval is a UI-disabled action until the preview has been opened at
least once. Admin must never approve based on raw JSON/CSV alone.

---

## Relationship to Today lesson composer (Phase D)

Phase D (bank-first Today lesson composer) is intentionally sequenced **after** Practice Gym
migration (Phase C2/C3/C4/C-Final) and after enough of this resource-bank platform exists to
give Phase D real bank content to compose from — not before. See
`docs/roadmap/road-map.md` Decision Log (2026-07-08, Plan-Sync-After-C1 entry) for the reasoning
and preferred phase order.

---

## Documentation impact

- Docs reviewed (Phase E0): `ActivityTemplate`/`PlacementItemDefinition`/`AdminReviewStatus`/
  `IFormIoSchemaValidationService`/`IActivityContentFingerprintService`/`IFileStorageService`/the
  feature-gate system (all via code audit, not docs), plus this file's own prior
  Plan-Sync-After-C1 content, `docs/architecture/cefr-resource-licensing-review.md`,
  `docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`,
  `docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md`.
- Docs updated (Phase E0): this file (entity model finalized, status/gate model added, E1-E4
  boundaries defined, admin UX plan finalized); `docs/roadmap/road-map.md`,
  `docs/sprints/current-sprint.md`, `docs/architecture/README.md`,
  `docs/backlog/product-backlog.md` (see accompanying commit).
- Docs updated (Phase E1, this section): this file (E1 exact-scope section marked implemented,
  actual entity/field deviations documented); `docs/roadmap/road-map.md`,
  `docs/sprints/current-sprint.md`, `docs/architecture/README.md`.
- Docs intentionally not updated: `docs/architecture/cefr-resource-licensing-review.md` — its
  licensing findings are unchanged by this phase; no new sources were browsed or licensing
  conclusions revisited in E0. `docs/architecture/practice-gym.md`/`repetition-and-novelty.md` —
  unrelated to this phase's scope (Practice Gym closure was Phase C-Final, already committed).
- Reason: Phase E0 is a planning-only deliverable — no app code, migrations, or config changed;
  the entity/status/gate model above is the actual technical decision this phase produces.
