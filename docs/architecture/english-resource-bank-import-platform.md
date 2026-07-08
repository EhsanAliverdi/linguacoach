---
status: current
lastUpdated: 2026-07-09 (Plan-Sync-G0)
owner: architecture
supersedes:
supersededBy:
---

# English Resource Bank Import, Review, Preview, and Publishing Platform (Phase E)

**Date planned:** 2026-07-08 (Plan-Sync-After-C1), **finalized:** 2026-07-08 (Phase E0),
**E1 implemented:** 2026-07-08, **E2 implemented:** 2026-07-08, **E3 implemented:** 2026-07-08,
**E4 implemented:** 2026-07-08, **Plan-Sync-After-E4:** 2026-07-08, **E5 implemented:** 2026-07-08,
**Plan-Sync-E6-Decision:** 2026-07-08, **E6 implemented:** 2026-07-08,
**Plan-Sync-After-D2:** 2026-07-09, **E7 implemented:** 2026-07-09
**Status:** E1 (staging), E2 (AI analysis + validation gates 4-6), E3 (admin rendered preview),
E4 (publish to first banks), E5 (published-bank browsing/search/admin management), E6 (first
real English content depth), and **E7 (full internal reading passage bank + resource depth
expansion)** are all implemented. The published banks now hold an original, internally-authored,
English-only seed pack — 32 vocabulary / 12 grammar / 10 short reading excerpts (E6) plus **10
full-length original reading passages (E7)** — that flowed through the full
staging→validation→approval→publish pipeline — **no direct-final-table seeding, no external
dataset, no Persian/bilingual content**. Traceable end to end: every published row resolves back
to its originating `ResourceCandidate`/import run/source. **Plan-Sync-After-D2 (2026-07-09)
chose Phase E7 before Phase D3** — see the Decision Log for the full reasoning. A new Phase D3
decision checkpoint follows E7 (start D3, continue E8, or a docs-only plan sync) — not resolved
by this phase.
**Phase D1 (2026-07-08, separate doc: docs/architecture/learning-activity-engine.md) has now
consumed this bank** — `ActivityMaterializationJob` queries the published vocabulary/grammar/
reading banks (via `IResourceBankQueryService`, unchanged by D1) as supporting material for a
narrow first slice of Today activities. This platform's own read-only query surface required no
changes to support that consumption.
**Some rows have now been published — but only `VocabularyEntry`/`GrammarProfileEntry`/short-
excerpt `ReadingPassage` candidates that passed validation AND were explicitly admin-approved.**
`ActivityTemplateCandidate` publishing is **deferred** (see "E4 — Publish to first banks" below
for why). **AI analysis is advisory only** — backend rule validation
(`ResourceCandidateValidationService`) remains the sole authority on `ValidationStatus`; the AI
never sets it directly. **The admin preview clearly separates what a student would see from
admin-only metadata**, and every publish-time gate is re-checked live rather than trusted from
earlier staging/validation/approval steps — a source's approval or license flags can change
between staging and publish.
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
| **E4** ✅ done 2026-07-08 | Publish to first banks | Approve/reject + publish action — promotes an approved, validation-passed candidate into `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/short-excerpt `CefrReadingReference`. `ActivityTemplateCandidate` publishing deferred. |
| **E5** ✅ done 2026-07-08 | Published bank browsing/search/admin management | Admin search/browse over the first published banks (vocabulary, grammar, short reading references) — filter by CEFR level/source/search text, surfacing source/license/provenance, published status, and candidate traceability (reverse-lookup back to the `ResourceCandidate` it came from). Read-only — no edit/delete actions; all mutation still happens through Resource Candidates. Not a full analytics dashboard; not yet `ActivityTemplate`/AI-generation-time consumption (that's a later integration once Phase D exists). No external dataset import, no embeddings/semantic search. |
| **E6** ✅ done 2026-07-08 | First real English content depth | An original, internally-authored, English-only seed pack (32 vocabulary / 12 grammar / 10 short reading excerpts) flowed through the full staging→validation→approval→publish pipeline — no direct-final-table seeding, no external dataset, no Persian/bilingual content. Proves the E1-E5 platform handles real usable content, not just synthetic tests. **Not** a full external-dataset import or E6's originally-envisioned "reading/listening resource expansion" scope — that broader scope remains available for a future phase if chosen. |
| **E7** ✅ done 2026-07-09 | Full internal reading passage bank + resource depth expansion | New `CefrReadingPassage` published bank entity for full-length original/internal English reading passages (distinct from the short-excerpt-only `CefrReadingReference`); `ResourceCandidatePublishService` now routes a `ReadingPassage` candidate to whichever bank fits its actual length instead of blocking full passages; E5-style browse/search API + admin page; 10 new full-length passages added to the internal seed pack through the same E1-E6 pipeline. **Re-scoped from its original "bigger import support" sketch** — see the E7 detail section below for why. Background/queued import jobs, ZIP archives, and audio-carrying sources remain deferred to a future phase if chosen. |
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

### E2 — AI analysis + validation gates — implemented (2026-07-08)

- **Backend**: `ResourceCandidateAnalysisService` (gate 4, advisory only) reuses
  `ActivityTemplateInstanceGenerator`'s AI-call pattern (`IAiContextBuilder`/`AiExecutionService`,
  new prompt key `resource_candidate_analyze`) to suggest CEFR level/confidence, skill/subskill,
  difficulty band, context/focus/grammar/vocabulary/pronunciation/activity-suitability/safety
  tags, quality score, and suggested activity uses per candidate. Retries once on bad/unparseable
  AI JSON; **on a second failure or an unavailable provider it fails gracefully** (never throws,
  never corrupts the candidate's existing data) rather than the synchronous student-facing
  retry-once-then-throw behavior `ActivityTemplateInstanceGenerator` uses — this is an offline
  admin-triggered enrichment step, so failure degrades to "needs manual review," not an error.
  `ResourceCandidateValidationService` (gates 5+6, fully deterministic) independently decides
  `ValidationStatus` — **the AI's suggestions are never trusted to set validation status
  themselves**; every check (English-only, CEFR validity + confidence threshold, skill/subskill
  taxonomy, candidate-type, text-length bounds, safety tags, live source-approval re-check,
  Form.io schema safety for `ActivityTemplateCandidate` rows, attribution-required heuristic,
  exact-fingerprint dedup within-run/within-source/global) runs over the candidate's stored field
  values regardless of whether those came from a human, an import default, or an AI suggestion.
- **Judgment calls made** (documented in code, restated here): CEFR-confidence review threshold
  **0.6** (below → `NeedsReview`, never an automatic pass); max `CanonicalText` length **5000**
  chars; any AI-reported safety tag is a hard **Failed**, not just a review flag; attribution
  considered "required" when the source's `LicenseType` name contains `"BY"` (covers the
  Creative-Commons Attribution family) — missing `AttributionText` in that case is a warning, not
  a fail; `CandidateType.Unknown` always needs human review (no clear bank-mapping exists for it);
  a source whose approval was revoked after original import fails re-validation immediately.
- **Dedup scope**: exact-`ContentFingerprint` match checked within the same import run, within
  the same source (across runs), and globally across the whole `ResourceCandidate` table — a
  match produces a `NeedsReview` warning, **never an automatic delete**. Cross-checking against
  published `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` rows was
  **deliberately skipped** — those entities predate `IActivityContentFingerprintService` and have
  no fingerprint-shaped column; adding one would be a schema change to published tables, out of
  scope for a staging-phase gate. No embeddings/pgvector/semantic dedup — exact match only.
- **Admin API**: `POST /api/admin/resource-candidates/{id}/analyze` (AI analysis, then
  auto-re-validates so `ValidationStatus` reflects the new suggestion immediately),
  `POST /api/admin/resource-candidates/{id}/validate` (deterministic re-validation only, no AI
  call — needed e.g. after a source's approval is revoked), `POST
  /api/admin/resource-import-runs/{id}/candidates/analyze` (batch-analyzes pending candidates for
  one run, capped at **50 per call** — E2 is synchronous/batched by design; larger background
  processing is Phase E7's job, not E2's).
- **Admin UI**: Resource Candidates gained Analyze/Re-validate actions (drawer + list row), an
  "Analyze pending" batch action on the Import Runs list, and a detail view showing CEFR/skill/
  subskill/difficulty/quality score/safety issues/validation errors-warnings/raw AI JSON. No
  approve/reject/publish action was added — still explicitly out of scope until E4.
- **Tests**: AI-metadata storage, malformed-AI-JSON handling, AI-unavailable handling, valid/
  invalid CEFR, invalid subskill, low-confidence review, Persian-script rejection, revoked-source
  re-validation, Form.io answer-leak rejection, within-run and cross-table duplicate detection,
  admin-only auth on both new endpoints, batch-limit enforcement, and a dedicated
  zero-published-rows assertion (mirroring E1's own).
- **Acceptance — met**: every candidate has a definitive `ValidationStatus`
  (`Pending`/`Passed`/`Failed`/`NeedsReview`) with structured error/warning reasons; AI
  suggestions are stored but never auto-approve or auto-publish; zero rows published.
- **Explicitly deferred (confirmed not built)**: embeddings/semantic dedup (E8), the rendered
  preview UI (E3), any approve/reject/publish action (E4).

### E3 — Admin rendered preview — implemented (2026-07-08)
- **Backend**: `GET /api/admin/resource-candidates/{id}/preview` (`IResourceCandidatePreviewService`/
  `ResourceCandidatePreviewService`) returns a `ResourceCandidatePreviewDto` with a bank-type-
  specific rendered model — `VocabularyEntry` (word/POS/definition/example), `GrammarProfileEntry`
  (grammar title/explanation/examples), `ReadingPassage` (title/passage text/word count/reading
  time), plus source/license/provenance, CEFR/skill/subskill/difficulty, tags, validation status/
  errors/warnings, duplicate indicators, and an AI-analysis summary (admin-only). **Not a Form.io
  schema render for these three bank types** — a dedicated read-only preview model (reusing
  `sp-admin-card`/`sp-admin-drawer`) is the right shape for reference-data bank entries, not the
  Form.io renderer used by Placement/Activity Templates. **The one exception**:
  `ActivityTemplateCandidate` rows DO carry a real Form.io schema, so that candidate type's
  student-visible render target reuses `app-formio-renderer` — but only after the schema is
  **re-validated live** through `IFormIoSchemaValidationService` at preview time (not just trusted
  from E2's earlier validation pass); if it fails, nothing is exposed as student-visible and a
  `previewWarnings` entry explains why. Any scoring/rubric-shaped metadata on an
  `ActivityTemplateCandidate` row goes only into a separate `AdminOnlyActivityMetadataJson` field,
  never merged into the student-visible schema. **Read-only, never mutates a candidate** —
  no `SaveChangesAsync` call, `UpdatedAtUtc` unchanged after a preview call (asserted by test).
  Unsupported/malformed candidate shapes never throw — `CanPreview=false` plus a `previewWarnings`
  explanation, with whatever generic info (canonical text, source/license) is still available.
- **Admin pages**: a second, dedicated Preview drawer on `/admin/resource-candidates` (kept
  separate from the existing detail/notes drawer) with two clearly distinguished panels — a
  green-bordered **"What the student would see"** panel (the rendered model) and a slate-bordered
  **"Admin-only"** panel (source/license, validation results, AI analysis, fingerprint/duplicate
  indicators, raw/normalized JSON) — plus a persistent **"E3 preview only — publish is not
  available until E4"** banner. No approve/reject/publish control exists anywhere in this UI.
- **Acceptance (revised from the original E0 plan)**: the original plan assumed an approve action
  would already exist by E3 and be UI-gated on preview having been viewed. **No approve/publish
  action exists yet at all** (that's E4's own deliverable) — so there is nothing to gate in E3.
  The "admin must not approve invisible JSON only" principle is instead satisfied by E3 existing
  at all: by the time E4 adds an approve action, a rendered preview will already be available for
  every candidate. **E4 must still enforce** "preview viewed before approve enabled" as its own
  UI gate when it builds the approve action — this doc's E4 section below is updated accordingly.
- **Explicitly deferred (confirmed not built)**: the publish action itself (E4), any approve/
  reject action, bulk actions, final-bank browsing/search.

### E4 — Publish to first banks — implemented (2026-07-08)
- **Backend**: `ResourceCandidatePublishService` — every gate is **re-checked live** at publish
  time (never trusting an earlier staging/validation/approval snapshot, since a source's approval
  or license flags can change after the fact): English-only (`LanguageCode` + script heuristic,
  same as the validation gate, run again), `CefrResourceSource.IsImportApproved`,
  `AllowsStudentDisplay`/`AllowsCommercialUse` (**hard-blocked at publish**, unlike E2's
  validation pass which only warns/flags `NeedsReview` for this — by publish time the permission
  gap is no longer a "note for a human," it's a real blocker on moving content to a live,
  paying-student-facing table), `ValidationStatus == Passed`, `ReviewStatus == Approved`
  (`Approve(notes?)`/`Reject(reason)` are new candidate methods, separate from validation).
  **Idempotent** — publishing an already-published candidate returns the existing
  `PublishedEntityType`/`PublishedEntityId` reference, never a second bank row.
  **Candidate-type support decided in E4**:
  - `VocabularyEntry` → `CefrVocabularyEntry` and `GrammarProfileEntry` → `CefrGrammarProfileEntry`
    — **fully supported**, both target entities need only a handful of fields a staged candidate
    reliably carries.
  - `ReadingPassage` → `CefrReadingReference` — **supported only when the staged text is ≤500
    characters** (`ResourceCandidatePublishService.MaxReadingExcerptLength`). `CefrReadingReference`'s
    own doc comment is explicit that it holds "only a short excerpt/citation, not a full
    copyrighted text — reading difficulty guidance, not a content library." A full reading
    passage (the normal shape this candidate type carries) does not fit that documented purpose.
    Rather than silently truncating a full passage into `ReferenceExcerpt` (lossy and dishonest
    about what was actually published), anything over the threshold is blocked with a clear
    error explaining why. Genuinely short passages/excerpts still publish.
  - `ActivityTemplateCandidate` → **deferred entirely, not published in E4**. `ActivityTemplate`
    is a much richer entity — it needs a stable unique `Key`, a curriculum-taxonomy-valid
    Skill/Subskill pair, and real hand-authored `GenerationInstructions` prose (required for
    `ActivityTemplateInstanceGenerator` to function at all). A row staged from a simple CSV/JSON
    import was never designed to carry a curriculum designer's generation instructions —
    inventing placeholder text to force it through would publish something dishonest (a
    "template" that looks complete but was never actually authored). Blocked with a clear error;
    left for a future phase once a real staging shape for these fields exists.
  - `Unknown` — always blocked, no bank table it could map to.
- **Known limitation vs. the original E0 plan**: the original plan envisioned a "preview must be
  opened at least once before approval is enabled" UI gate. E3 did not build any "has this
  candidate been previewed" tracking field, so E4 does not enforce that specific gate — approval
  is a deliberate admin action gated on the candidate's validation/review state, not on a
  preview-viewed flag. The underlying safety property (no approval of invisible JSON) is still
  upheld in practice — the preview is one click away on the same page an admin uses to approve —
  but it is not mechanically enforced. This could be added in a future phase if it becomes a real
  workflow problem.
- **Admin pages**: Approve/Reject/Publish actions on the Resource Candidates page (reject requires
  a reason). Publish is **disabled, not hidden**, with a clear reason shown when ineligible (e.g.
  "Validation must pass first," "Requires approval first," "Candidate type not yet supported for
  publishing"). Published candidates show their target entity type + id as text — no browse link,
  since **full published-bank browsing is E5, not E4**.
- **Tests**: publish blocked before validation passes, before admin approval, after rejection,
  after source approval is revoked, after a student-display/commercial-use permission is missing,
  and for a Persian-script candidate (English-only re-checked live, defense-in-depth); idempotent
  repeated publish; correct field mapping for `VocabularyEntry`/`GrammarProfileEntry`; the
  `ReadingPassage` length gate (both under and over threshold); `ActivityTemplateCandidate` and
  `Unknown` both blocked with zero final rows created; publish metadata correctly recorded;
  `ResourceRawRecord` unchanged after publish; endpoints admin-only.
- **Acceptance — met**: approved, validated `VocabularyEntry`/`GrammarProfileEntry` candidates
  (and short-excerpt `ReadingPassage` candidates) produce real `CefrVocabularyEntry`/
  `CefrGrammarProfileEntry`/`CefrReadingReference` rows; `ActivityTemplateCandidate` and anything
  failing a live gate recheck remain unpublished; listening/speaking banks remain empty (no such
  candidate type or target entity exists yet — that's E6+).
- **Explicitly deferred**: published-bank browsing (E5), reading/listening banks (E6),
  background/queued large imports + ZIP + audio sources (E7), RAG/embeddings/semantic dedup (E8).

### E5 — Published bank browsing, search, and admin management — implemented (2026-07-08)

- **Backend**: `IResourceBankQueryService`/`ResourceBankQueryService` — list + detail queries for
  all three published bank types (`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/
  `CefrReadingReference`). Filters: search text (case-insensitive `Contains` over the type's
  relevant fields — `Word`/`Notes` for vocabulary, `GrammarPoint`/`Description` for grammar,
  `TextType`/`DifficultyNotes`/`ReferenceExcerpt` for reading), CEFR level (exact match), source
  id (exact match). Pagination capped at 200 per page, matching the existing candidate-list
  endpoint's cap. Sort: newest first by `CreatedAt` (none of the three entities has its own
  "published at" timestamp — a documented, deliberate fallback, not a gap needing a new column).
  **No advanced ranking, no embeddings, no semantic search** — plain deterministic filter + sort.
- **Traceability — the key design finding this phase confirmed**: none of the three published
  bank entities carries a *forward* reference back to the `ResourceCandidate` that produced it.
  Traceability is a **reverse lookup**: `ResourceCandidate.PublishedEntityType`/`PublishedEntityId`
  (set by E4's `MarkPublished`) are matched against the bank row being viewed, then joined through
  to the candidate's raw record/import run the same way `ResourceCandidatePublishService`/
  `ResourceCandidateValidationService` already do. **Never throws when no match exists** — returns
  an explicit "traceability unavailable" result instead (covered by a test that seeds a bank row
  directly via the DbContext, bypassing the publish service entirely, to prove the no-match path
  is handled cleanly). **No new columns were added to any published bank entity** — the reverse
  query was sufficient for E5's read-only browsing needs.
- **Invariant confirmed by a dedicated test**: because these three tables are only ever written to
  by `ResourceCandidatePublishService`, nothing unpublished or rejected can ever appear in a bank-
  browse list — this was true by construction since E4, and E5 adds an explicit test proving it
  holds through the new query surface too, not just trusting E4's own tests.
- **Admin API**: `GET /api/admin/resource-banks/{vocabulary|grammar|reading-references}` (list,
  filters above) and `.../{id}` (detail, includes source/license/provenance + attribution +
  traceability). All admin-only, 404 for a missing id.
- **Admin UI**: three new pages under `/admin/resource-banks/{vocabulary,grammar,reading-
  references}` — search/CEFR/source filter bar, paginated table, read-only detail drawer (core
  content, CEFR level, source/license/provenance, traceability or its "unavailable" state), empty/
  loading/error states matching the existing Resource Candidates page's pattern. **No edit or
  delete actions anywhere** — a persistent note clarifies that mutation still happens through
  Resource Candidates (approve/reject/publish). New "Resource Banks" nav entries under the
  existing Content sidebar group (desktop and mobile).
- **Tests**: list/filter/pagination for all three bank types; detail-with-traceability for all
  three; the unpublished-candidate-never-appears invariant; the no-matching-candidate detail case;
  all 6 endpoints admin-only.
- **Acceptance — met**: an admin can browse, filter, and search the published vocabulary/grammar/
  reading-reference banks, see source/license/provenance and (where available) a trace back to
  the originating candidate/import run, without any edit/delete/publish capability on this
  surface — mutation remains exclusively on Resource Candidates.
- **Explicitly deferred (confirmed not built)**: content depth/volume (E6), background/queued
  larger imports (E7), embeddings/semantic search (E8), any `ActivityTemplate`/AI-generation-time
  consumption of these banks (that's a Phase D integration concern, not E5's).

### E6 — First real English resource depth — implemented (2026-07-08)

- **Scope decision**: E6 was deliberately scoped as a **controlled first content-depth slice**,
  not the broader "reading/listening resource expansion" originally sketched for E6 in the E0
  phase table — a genuine product-scoping choice made this phase, not a shortfall. The goal was
  narrower and more load-bearing: prove the E1-E5 pipeline can carry real, useful, original
  English content end to end, not only synthetic test fixtures.
- **Content**: 32 vocabulary entries (A1-B2, general + some workplace terms, including phrasal
  verbs), 12 grammar profile entries (spanning the grammar subskill taxonomy), 10 short reading
  excerpts (150-225 characters, comfortably under `CefrReadingReference`'s 500-character publish
  limit). **100% original, internally authored — no external dataset, no copied textbook/site
  content, no Persian/bilingual content anywhere.**
- **Pipeline discipline — no shortcuts taken**: the content was staged through
  `ResourceImportService.ImportAsync` (three import runs, one per content group) exactly like an
  admin-triggered import, then validated via `ResourceCandidateValidationService.ValidateAsync`
  (real deterministic gates, no bypass), then approved and published via the real
  `ResourceCandidate.Approve(...)`/`ResourceCandidatePublishService.PublishAsync` — **the same
  code path an admin's UI click would exercise**, run by a startup seeder
  (`InternalResourceSeedPackSeeder`) rather than clicked by a human, since this is pre-reviewed,
  codebase-authored content (the same judgment the existing `ActivityTemplateSeeder` already
  makes for its own hand-authored templates — `Approve()`/`Publish()` called directly, not routed
  through a separate human-review UI step for content that's essentially reviewed by virtue of
  being written into source control).
- **Key design fix — deterministic CEFR/skill/subskill mapping at import time**: prior to E6,
  `ResourceImportService` left `CefrLevel`/`PrimarySkill`/`Subskill` null at import time — those
  fields were populated only by `ResourceCandidateAnalysisService`'s AI-advisory analysis (E2).
  For internally-authored content where the author already knows the correct classification
  (it's not a probabilistic guess), asking an AI to "discover" already-known metadata is
  backwards. E6 added `ResourceImportService.ApplyDeterministicRowMetadata` — if a raw row
  carries its own `cefrLevel`/`skill`/`subskill`/`tags` columns, they're copied straight onto the
  candidate via the same `ApplyAnalysis` mutator E2 uses, but with `cefrConfidence=1.0` and an
  explicit `mappingSource: "import-row-deterministic-mapping"` marker distinguishing it from a
  real AI response — **no AI provider is invoked anywhere in this path**. Rows without these
  columns are completely unaffected (existing import behavior for every prior import is
  unchanged) — this is additive, not a breaking change to E1's import shape.
- **No direct-final-table bypass**: `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/
  `CefrReadingReference` are constructed nowhere in this codebase except inside
  `ResourceCandidatePublishService.BuildTargetEntity` — confirmed by a dedicated test asserting
  every seeded bank row resolves back to a `ResourceCandidate` with `IsPublished=true` and a
  matching `PublishedEntityType`/`PublishedEntityId` (a row inserted by any other path would have
  no such candidate and would fail the assertion).
- **Idempotency**: `InternalResourceSeedPackSeeder` checks for an existing `CefrResourceSource`
  named `"SpeakPath Internal English Seed Pack v1"` and skips entirely if found — a full second
  run creates no duplicate source, import runs, raw records, candidates, or published rows.
- **Source registration**: the seed pack registers its own `CefrResourceSource` — `LanguageCode
  ="en"`, `LicenseType="Internal/Original"` (never claims an external license like CC-BY),
  `AllowsStudentDisplay=true`, `AllowsCommercialUse=true`, honest internal `AttributionText`,
  approved via the real `ApproveForImport(...)` method.
- **Tests**: source idempotency, import-run/raw-record/candidate creation, deterministic
  CEFR/skill/subskill population (proven distinct from AI output), successful validation with no
  AI provider call, successful publish to all three target tables with correct field mapping,
  published rows queryable via E5's `ResourceBankQueryService`, traceability back to
  candidate/import run/source, full-seeder-rerun idempotency, and the no-direct-final-table-
  bypass guarantee. +14 backend tests, all deterministic — no test requires a live/real AI
  provider or external network call.
- **Acceptance — met**: the published banks now hold real, original, English-only content
  (32/12/10 rows) discoverable through E5's browse/search surface, fully traceable to its
  originating candidate/import/source, added without bypassing any staging/review/publish gate.
- **Explicitly deferred (confirmed not built)**: any real external dataset (CEFR-J, CMUdict,
  Common Voice, LibriVox, Gutenberg, Wiktionary, or any scraped site) remains future work, gated
  on licensing review per `docs/architecture/cefr-resource-licensing-review.md`; broader reading/
  listening resource-type expansion (the originally-sketched E6 scope) is deferred to a future
  phase if chosen; `ActivityTemplateCandidate` publishing and full-length `ReadingPassage`
  publishing remain deferred per Phase E4's own decisions (**resolved for reading passages by
  E7, below**); background/queued imports (E7's originally-sketched scope, still deferred — see
  below), embeddings/semantic search (E8), Phase D, and PG-v2 are all untouched.

### E7 — Full internal reading passage bank + resource depth expansion — implemented (2026-07-09)

- **Re-scoped from E7's original sketch**: the phase-breakdown table above originally described
  E7 as "bigger import support" (background/queued jobs, ZIP archives, audio-carrying sources).
  Plan-Sync-After-D2 (2026-07-09) chose a different, more urgent E7 scope instead: Phase D2's own
  audit found the actual blocker to further Today bank-first work was resource depth/type
  coverage, not import mechanics — and Phase E4's own deferred-item list (`docs/backlog/
  product-backlog.md`) already flagged "full-length `ReadingPassage` publishing" as the most
  concrete, well-understood gap. E7 closes that specific gap. Background/queued import jobs
  remain available for a future phase under the same name if ever chosen.
- **New published bank entity**: `CefrReadingPassage` (migration
  `Phase_E7_AddCefrReadingPassage`) — `SourceId`, `Title`, `PassageText`, `Summary?`, `CefrLevel`,
  `DifficultyBand?`, `PrimarySkill` (default `"Reading"`), `Subskill?`, `TopicTagsJson?`,
  `ContextTagsJson?`, `FocusTagsJson?`, `WordCount`/`EstimatedReadingMinutes` (computed at
  construction, same 200-wpm convention `ResourceCandidatePreviewService` already used),
  `AttributionText?` (a denormalized per-row snapshot — the one field on this entity that
  deliberately diverges from `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/
  `CefrReadingReference`'s "join to `CefrResourceSource` for attribution" convention, since a
  future batch could plausibly mix passages needing per-passage attribution nuance),
  `ContentFingerprint?`, `QualityScore?`. Never holds copyrighted third-party text — original/
  internal or explicitly license-approved content only, same rule as every other bank in this
  platform.
- **Publish routing (not a new candidate type)**: `ResourceCandidateType.ReadingPassage` already
  existed (used since E4) — no new type was needed. `ResourceCandidatePublishService` now routes
  by staged text length: at or under `MaxReadingExcerptLength` (500 chars) still publishes to
  `CefrReadingReference` exactly as before (E4's short-excerpt behavior is unchanged and still
  tested); over that threshold, a candidate now publishes to `CefrReadingPassage` **instead of
  being blocked** (E4's original behavior for long passages). Every publish gate still applies
  unchanged: English-only, source approval, `AllowsStudentDisplay`/`AllowsCommercialUse`,
  `ValidationStatus == Passed`, `ReviewStatus == Approved`; a full passage additionally requires
  a `title` field (blocked with a clear error otherwise) since `CefrReadingPassage.Title` is
  required. Idempotent exactly like every other publish target — republishing returns the
  existing reference, never a duplicate row.
- **Preview needed no changes**: `ResourceCandidatePreviewService.BuildReadingPreview` already
  rendered full `PassageText`/`WordCount`/`EstimatedReadingMinutes` for any `ReadingPassage`
  candidate regardless of length — E3's original design was already forward-compatible with full
  passages, a finding from this phase's own audit, not new work.
- **Browse/search**: `IResourceBankQueryService.ListReadingPassagesAsync`/
  `GetReadingPassageDetailAsync` (filters: search text against title/passage/summary, CEFR level,
  source id; pagination capped at 200, same convention as the other three bank types); reverse
  candidate traceability identical to the existing pattern (no forward reference on the bank
  entity itself). New admin API (`GET /api/admin/resource-banks/reading-passages`[`/{id}`]) and a
  new read-only admin page (`/admin/resource-banks/reading-passages`) — list with search/CEFR/
  source filters and pagination, detail drawer with passage text/word count/reading time/source/
  license/provenance/traceability. No edit/delete actions, matching every other bank page.
- **Content added**: 10 new full-length, 100% original reading passages (A1-B2, 458-940
  characters each, well over the 500-char excerpt threshold so every one publishes to
  `CefrReadingPassage`) added to `InternalResourceSeedPackSeeder`'s existing internal source and
  routed through the same real staging→validation→approval→publish pipeline as E6's content — no
  direct-final-table seeding. General/everyday and workplace/social contexts, no copied
  third-party text, no Persian/bilingual content.
- **Query readiness without new consumers**: per this phase's own scope discipline, the new
  query methods are exposed and tested now, but `TodayBankResourceSelector` (Phase D1/D2) is
  **not** wired to consume `CefrReadingPassage` this phase — that consumption decision belongs to
  a future Today-composer phase (D3), not E7.
- **Tests**: `CefrReadingPassageTests` (entity construction/validation, word-count/reading-time
  computation); `ResourceCandidatePublishServiceTests` (full passage publishes to
  `CefrReadingPassage`; short passage still publishes to `CefrReadingReference` unchanged; a full
  passage missing a `title` is blocked with a clear error; republishing is idempotent);
  `ResourceBankQueryServiceTests` (list/filter/pagination/detail-traceability for the new bank
  type); `AdminResourceBankEndpointTests` (auth/404 coverage for the two new routes);
  `InternalResourceSeedPackSeederTests` (the 10 new passages stage/validate/publish correctly,
  full-seeder-rerun idempotency still holds with the larger content set). +24 backend tests, all
  deterministic — no live/real AI provider or external network dependency in any of them.
- **Acceptance — met**: the platform now has a real, honest home for full-length original English
  reading content, distinct from the short-excerpt-only `CefrReadingReference`, added through the
  full staging/review/publish pipeline with no bypass, browsable/searchable by an admin exactly
  like the other three bank types.
- **Explicitly deferred (confirmed not built)**: `ActivityTemplateCandidate` publishing (still
  Phase E4's own deferral, unrelated to reading passages); any real external dataset; background/
  queued import jobs, ZIP archives, audio-carrying sources (E7's *original* sketch, not this
  phase's actual scope); embeddings/semantic search (E8); `TodayBankResourceSelector` consumption
  of `CefrReadingPassage` (left for a future Today-composer phase); Phase D3; PG-v2.

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
| Published Resource Banks | `/admin/resource-banks/{vocabulary,grammar,reading-references}` | ✅ E5 done | Browse/search published `Cefr*` rows, read-only, with reverse candidate traceability. |
| Published Reading Passages | `/admin/resource-banks/reading-passages` | ✅ E7 done | Browse/search full-length `CefrReadingPassage` rows, read-only, same pattern as the E5 pages. |
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
give Phase D real bank content to compose from — not before. Phase D1's originally-documented
"E0-E4 before D1" gate was technically met once Phase E reached E4 (2026-07-08) — but
**Plan-Sync-After-E4 (2026-07-08) revised the near-term sequence: Phase E5 came before Phase D1**,
not after it, since a handful of small, synthetic/test-only published rows with no browsing/
search/admin-management surface didn't meet the gate's actual intent ("Phase D has real, usable
bank content to compose from"). **Phase E5 (2026-07-08) closed the visibility gap** — the
published banks can be browsed, filtered, and searched, with traceability back to their source
candidates — but **did not** close the content-depth gap. **Plan-Sync-E6-Decision (2026-07-08)
resolved that follow-on decision checkpoint: continue with Phase E6 before Phase D1.** **Phase E6
(2026-07-08) closed the content-depth gap for a first slice** — the published banks now hold 32
vocabulary / 12 grammar / 10 reading-excerpt rows of real, original, English-only content, added
through the full staging/review/publish pipeline with no bypass. **The third Phase D1 decision
checkpoint was resolved by starting Phase D1 (2026-07-08)** — see
`docs/architecture/learning-activity-engine.md` for the full D1 design (a narrow,
fallback-safe first slice: `ActivityMaterializationJob` + `TodayBankResourceSelector` inject
bank content into the AI prompt for vocabulary/reading-focused patterns only; legacy generation
is untouched and remains the fallback whenever no matching bank content exists). **Phase D2
(2026-07-08) then expanded that first slice**: a balanced vocabulary/grammar/reading bundle
(rather than a single resource type), CEFR-widening for review/scaffold routing only, a
feedback-signal avoidance check, a clearer structured prompt block, and durable full-resource
provenance on `LearningActivity.BankResourceProvenanceJson` (replacing D1's
`StudentActivityReadinessItem.SetBankItemProvenance` call, which D2's audit found was latently
broken — FK-constrained to `PlacementItemDefinition`, not any Phase E Cefr* bank table). Neither
D1 nor D2 changes anything in this platform's own pipeline (E0-E6) — both are pure read-only
consumers of `IResourceBankQueryService`. **Plan-Sync-After-D2 (2026-07-09, docs-only): Phase E7
comes before Phase D3.** D2's own audit found the Today-side integration mechanism (skill-based
gating, balanced selection, structured context, full provenance) is now about as complete as it
can usefully be for a narrow first slice — every further Today improvement (a dedicated
grammar-focused pattern, Speaking/Listening/image/open-ended support, semantic ranking) is gated
on this platform having more/different content and resource types to select from, not on more
selector engineering. **Phase E7 is therefore the next recommended implementation phase**: it
should focus on resource depth/type expansion needed by Today and future Practice Gym v2 — new
resource types and/or a larger, still-original/English-only content volume — while preserving
the same English-only, staged, reviewable, traceable pipeline this platform has followed since
E0 (no direct final-bank seeding is introduced by E7, exactly as no phase before it has). **Phase
E7 (2026-07-09) delivered exactly that**: a new `CefrReadingPassage` bank for full-length
original reading passages (previously the platform's clearest gap — `CefrReadingReference` was
short-excerpt-only, and E4 explicitly deferred full-passage publishing rather than force it
through dishonestly), plus 10 new full-length passages through the same real pipeline. **Today
consumption of this new bank is deliberately NOT wired this phase** —
`TodayBankResourceSelector` still only reads vocabulary/grammar/short-reading-reference rows;
extending it to full reading passages is left for whichever future phase actually builds a
reading-passage-consuming Today pattern. **Phase D3 remains deferred until after Phase E7 (this
phase) and E8 if needed**, at which point a new Phase D3 decision checkpoint follows, not
resolved in advance. See `docs/roadmap/road-map.md` Decision Log (2026-07-08, Plan-Sync-After-C1,
Plan-Sync-After-E4, Phase E5, Plan-Sync-E6-Decision, Phase E6, Phase D1, Bugfix-D1A, Phase D2,
Plan-Sync-After-D2, and Phase E7 entries) for the full reasoning and current preferred phase
order.

**Plan-Sync-G0 (2026-07-09, docs-only)** confirms Resource Banks, Resource Candidates, and
Activity Templates — this platform's own E0-E7 output — as the **primary content model going
forward**, explicitly narrowing AI generation's role to fallback generation, evaluation,
composition, and cost/diagnostics visibility. A future **Phase G0** audit will review how
existing admin surfaces (including any still framed around the pre-bank-first readiness-pool/
AI-generation-as-primary-content model) relate to this platform and classify each accordingly;
this file's own scope remains Phase E, not the G-track — see `docs/roadmap/road-map.md` §1 and
Decision Log (Plan-Sync-G0 entry) for the full decision.

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
- Docs updated (Phase E2, this section): this file (E2 boundaries section marked implemented,
  judgment calls/thresholds documented); `docs/roadmap/road-map.md`,
  `docs/sprints/current-sprint.md`, `docs/architecture/README.md`.
- Docs updated (Phase E3, this section): this file (E3 boundaries section marked implemented,
  E4's "approve action doesn't exist yet" correction documented); `docs/roadmap/road-map.md`,
  `docs/sprints/current-sprint.md`, `docs/architecture/README.md`.
- Docs updated (Phase E4, this section): this file (E4 boundaries section marked implemented,
  candidate-type support decisions and the preview-viewed-gate limitation documented);
  `docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`, `docs/architecture/README.md`.
- Docs updated (Plan-Sync-After-E4, this section, docs-only): this file (E5 phase-breakdown row
  and "Relationship to Phase D" section updated to reflect E5-before-D1 sequencing);
  `docs/roadmap/road-map.md` (§19a phase sequence, Decision Log), `docs/sprints/current-sprint.md`,
  `docs/architecture/README.md`, `docs/backlog/product-backlog.md`.
- Docs updated (Phase E5, this section): this file (E5 boundaries section added, traceability
  design and invariant documented, "Relationship to Phase D" section updated with the E5-closed-
  the-gap status and the live decision checkpoint); `docs/roadmap/road-map.md`,
  `docs/sprints/current-sprint.md`, `docs/architecture/README.md`.
- Docs updated (Plan-Sync-E6-Decision, this section, docs-only): this file (E6 phase-breakdown
  row and "Relationship to Phase D" section updated to reflect the resolved decision checkpoint —
  E6 next, D1 deferred until after E6 or a later decision); `docs/roadmap/road-map.md` (§19a
  phase sequence, Decision Log), `docs/sprints/current-sprint.md`, `docs/architecture/README.md`,
  `docs/backlog/product-backlog.md`.
- Docs updated (Phase E6, this section): this file (E6 detail section added, "Relationship to
  Phase D" section updated to reflect the closed content-depth gap and the third live-but-
  unresolved Phase D1 decision checkpoint); `docs/roadmap/road-map.md` (Current Project Status,
  Test Totals, Decision Log, §19a phase sequence); `docs/sprints/current-sprint.md`;
  `docs/architecture/README.md`; `docs/backlog/product-backlog.md`.
- Docs updated (Phase D1, this section): this file ("Relationship to Phase D" section updated —
  D1 started and resolves the third checkpoint; a status paragraph noting D1 has consumed the
  bank); `docs/architecture/learning-activity-engine.md` (new D1 section — the actual technical
  home for this phase's design); `docs/roadmap/road-map.md` (Current Project Status, Test
  Totals, Decision Log, §19a phase sequence); `docs/sprints/current-sprint.md`;
  `docs/architecture/README.md`; `docs/backlog/product-backlog.md` (new discovered-bug entry —
  see below).
- Docs updated (Phase D2, this section): this file ("Relationship to Phase D" section updated —
  D2 expanded the D1 slice); `docs/architecture/learning-activity-engine.md` (D1 section rewritten
  to cover D2's balanced bundle, CEFR widening, feedback avoidance, structured prompt block, and
  the provenance-mechanism fix); `docs/roadmap/road-map.md` (Current Project Status, Test Totals,
  Decision Log, §19a phase sequence); `docs/sprints/current-sprint.md`; `docs/architecture/README.md`;
  `docs/backlog/product-backlog.md`.
- Docs updated (Plan-Sync-After-D2, this section, docs-only): this file ("Relationship to Phase
  D" section updated — E7 chosen next, D3 deferred until after E7/E8);
  `docs/roadmap/road-map.md` (§19a phase sequence, Decision Log); `docs/sprints/current-sprint.md`;
  `docs/architecture/learning-activity-engine.md`; `docs/architecture/README.md`;
  `docs/backlog/product-backlog.md`.
- Docs updated (Phase E7, this section): this file (phase-breakdown row, new E7 detail section,
  admin pages table, "Relationship to Phase D" section updated); `docs/architecture/
  learning-activity-engine.md`; `docs/roadmap/road-map.md` (Current Project Status, Test Totals,
  Decision Log, §19a phase sequence); `docs/sprints/current-sprint.md`;
  `docs/architecture/README.md`; `docs/backlog/product-backlog.md`.
- Docs updated (Plan-Sync-G0, this section, docs-only): this file ("Relationship to Today lesson
  composer" section updated with the primary-content-model confirmation and forward reference to
  Phase G0); `docs/roadmap/road-map.md` (§1, Decision Log, §19a phase sequence);
  `docs/sprints/current-sprint.md`; `docs/architecture/learning-activity-engine.md`;
  `docs/architecture/README.md`; `docs/backlog/product-backlog.md`;
  `docs/architecture/readiness-pool.md` (forward-reference note only, not rewritten).
- Docs intentionally not updated: `docs/architecture/cefr-resource-licensing-review.md` — its
  licensing findings are unchanged by this phase; no new sources were browsed or licensing
  conclusions revisited in E0. `docs/architecture/practice-gym.md`/`repetition-and-novelty.md` —
  unrelated to this phase's scope (Practice Gym closure was Phase C-Final, already committed).
- Reason: Phase E0 is a planning-only deliverable — no app code, migrations, or config changed;
  the entity/status/gate model above is the actual technical decision this phase produces.
