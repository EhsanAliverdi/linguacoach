---
status: current
lastUpdated: 2026-07-08 (Plan-Sync-After-C1)
owner: architecture
supersedes:
supersededBy:
---

# English Resource Bank Import, Review, Preview, and Publishing Platform (Phase E)

**Date planned:** 2026-07-08
**Status:** Planning only — no implementation started (E0 not begun).
**Supersedes the informal "seed CEFR-J/UniversalCEFR data" framing** used in earlier planning
docs (`docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md` §4.6/§9,
`docs/architecture/cefr-resource-licensing-review.md`). Those docs' licensing findings still
apply — this doc replaces the *shape* of the work (a full pipeline, not a one-shot seed script).

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

## Phase breakdown (E0–E8)

| Phase | Goal | Scope |
|---|---|---|
| **E0** | Final model/planning for the resource import platform | Finalize entity model (`ResourceImportSource`, `ResourceImportRun`, `ResourceRawRecord`, `ResourceCandidate`, published bank entities — reusing `CefrResourceSource`/`CefrVocabularyEntry`/etc. where they already fit), finalize state-machine transitions, finalize admin route/page list below. No code. |
| **E1** | Source registry + CSV/JSON/JSONL import + raw/candidate staging | **First implementation slice.** `ResourceImportSource` CRUD (admin-authored: name, license type, url, import date, usage restriction notes — mirrors `CefrResourceSource`'s existing shape), a manual/admin-triggered CSV/JSON/JSONL file import into `ResourceRawRecord` rows, and a first-pass mapping into `ResourceCandidate` rows (untouched raw fields alongside a normalized candidate shape). **Does not publish anything** — staging only. |
| **E2** | AI analysis + validation gates | AI-assisted CEFR level/skill/subskill classification of candidates; deterministic validation (schema shape, English-only language check, forbidden-word/content check, license-approval gate reusing `CefrResourceSource.IsImportApproved`). Produces a validation-passed/failed state per candidate with recorded reasons. |
| **E3** | Admin rendered preview | A dedicated admin page rendering exactly what a student-facing consumer (e.g. a future `ActivityTemplate` or resource lookup) would show for a candidate — not just raw JSON/CSV fields. |
| **E4** | Publish to first banks | Human review action (approve/reject with reason, mirroring the existing `AdminReviewStatus` pattern used by `ActivityTemplate`/`PlacementItemDefinition`) that promotes an approved, validated candidate into a published `Cefr*` bank row. First banks: vocabulary and grammar (the two with resolved-enough source candidates from E1/E2). |
| **E5** | Published bank browsing/search | Admin (and eventually `ActivityTemplate`/AI-generation-time) search/browse over published bank content — filter by CEFR/skill/subskill/context, not just an admin CRUD list. |
| **E6** | Reading/listening resources | Extend the pipeline to `CefrReadingReference`-shaped content (passages) and a first listening-script bank — same pipeline, new candidate/validation rules for longer-form text and (later) audio-adjacent metadata. |
| **E7** | Bigger import support | Background/queued import jobs (Quartz) for larger sources, ZIP archive support, audio-file-carrying sources (Common Voice, LibriVox) — deferred until E1-E6 prove the pipeline on simpler text sources first. |
| **E8** | RAG/search enrichment | Embedding/vector-search-based candidate deduplication and semantic bank search — explicitly deferred past all of E0-E7; no pgvector, no embeddings before this phase, consistent with Phase B's repetition/novelty foundation scope discipline. |

**E1 is the first implementation slice** — source registry + CSV/JSON/JSONL import + raw/candidate
staging only. It deliberately does **not** implement E2 (AI analysis), E3 (preview), or E4
(publish) — those are separate phases with their own review gates.

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

## Admin UI requirements

New admin pages (small, focused — no large redesign, consistent with the project's
"reuse the `AdminReviewStatus` pattern rather than reinvent" convention established for
`ActivityTemplate`/`PlacementItemDefinition`):

- **Sources page** — `ResourceImportSource` CRUD (mirrors `CefrResourceSource`'s existing
  license/provenance fields).
- **Import runs page** — list of import executions per source, status, record counts.
- **Raw records page** — read-only view of as-imported records per run.
- **Candidate review page** — the main working surface: candidate list, CEFR/skill/subskill
  filters, validation status, approve/reject actions.
- **Rendered preview page** — student-facing render of a single candidate (E3).
- **Published bank pages** — browse/search published `Cefr*` bank rows (E5).
- **Tags/taxonomy page** (later) — managing context/focus tag vocabulary used across candidates
  and published banks, once enough banks exist to warrant a shared taxonomy admin surface.

---

## Relationship to Today lesson composer (Phase D)

Phase D (bank-first Today lesson composer) is intentionally sequenced **after** Practice Gym
migration (Phase C2/C3/C4/C-Final) and after enough of this resource-bank platform exists to
give Phase D real bank content to compose from — not before. See
`docs/roadmap/road-map.md` Decision Log (2026-07-08, Plan-Sync-After-C1 entry) for the reasoning
and preferred phase order.

---

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`,
  `docs/architecture/cefr-resource-licensing-review.md`,
  `docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md`.
- Docs updated: this new file; `docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`,
  `docs/architecture/README.md`, `docs/architecture/practice-gym.md`,
  `docs/architecture/repetition-and-novelty.md` (see accompanying commit).
- Docs intentionally not updated: `docs/architecture/cefr-resource-licensing-review.md` — its
  licensing findings are unchanged by this re-plan; this doc only changes the shape of the
  *pipeline*, not the licensing conclusions.
- Reason: this is a planning-only deliverable (Plan-Sync-After-C1) — no code, schema, or config
  changed.
