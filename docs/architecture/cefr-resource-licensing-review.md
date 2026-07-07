# CEFR External Resource Licensing Review

**Date:** 2026-07-07
**Related:** Phase 3 of `docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md` (CEFR Resource Bank foundation)
**Status:** No data imported. This document tracks open license questions that must be resolved before any import happens.

## Purpose

The CEFR Resource Bank (`CefrResourceSource`, `CefrDescriptor`, `CefrVocabularyEntry`,
`CefrGrammarProfileEntry`, `CefrReadingReference`) is schema-only as of this phase. Every
content-bearing row carries a `SourceId` FK to a `CefrResourceSource` row, and
`CefrResourceSource.IsImportApproved` defaults to `false` and must be explicitly set via
`ApproveForImport(...)` before any content is imported against that source. This is an
application-level gate, not a database constraint — reviewers and future import tooling must
still check `IsImportApproved` before writing content rows.

## Candidate sources under evaluation

### CEFR-J / Open Language Profiles (`openlanguageprofiles/olp-en-cefrj`)

- **What it is:** A CEFR-referenced word list and grammar profile for English, developed for
  the CEFR-J project (Tokiwa Kaisha) and republished under the Open Language Profiles banner.
- **License status:** Believed to carry a more permissive academic/open license, but **this
  has not been independently verified against the current LICENSE file in the repository**.
  Do not assume permissive terms — read the actual license text before import.
- **Open questions:**
  1. What is the exact license (CC-BY, CC-BY-NC, CC-BY-SA, other)?
  2. Does the license permit commercial use? LinguaCoach is a commercial product or may become
     one — this is the single most important question to answer before import.
  3. Does it require attribution in-product, and if so, where should that attribution surface
     (footer, about page, admin-only)?
  4. Are there redistribution restrictions that would affect exporting vocabulary lists to
     students (e.g. via `CefrVocabularyEntry` → activity content)?
- **Recommendation:** Higher priority candidate than UniversalCEFR *if* the license check
  clears it for commercial use. Do not import until confirmed.

### UniversalCEFR (`UniversalCEFR` GitHub organization)

- **What it is:** A collection of CEFR-labelled datasets aggregated from multiple research
  sources.
- **License status:** **Flagged as likely non-commercial/research-only** by the architecture
  review that preceded this phase. Several datasets aggregated under this umbrella originate
  from academic research releases that commonly carry NC (non-commercial) or research-only
  terms.
- **Open questions:**
  1. Does *every* dataset folder in the org carry the same license, or does license vary
     per-dataset? (Aggregator repos frequently mix licenses — a blanket assumption is unsafe.)
  2. For any dataset that is NC-only: is there a compatible commercial-use alternative, or
     does this dataset need to be excluded entirely from LinguaCoach's bank?
  3. Is attribution/citation required per-dataset, and does UniversalCEFR itself impose
     additional terms on top of the underlying dataset licenses?
- **Recommendation:** Treat as **lower priority / higher risk** than CEFR-J until per-dataset
  licensing is confirmed. If LinguaCoach is or becomes commercial, assume UniversalCEFR content
  is unusable unless a specific dataset's license is individually verified to permit it.

## Process for clearing a source

1. Read the actual license file/text at the source (not a summary or README claim).
2. Record the finding directly on the corresponding `CefrResourceSource` row:
   `LicenseType` (exact license identifier), `SourceUrl` (canonical location),
   `UsageRestrictionNotes` (plain-language restrictions, e.g. "non-commercial only",
   "attribution required in student-facing UI").
3. Only call `CefrResourceSource.ApproveForImport(...)` once the above is confirmed and, for
   any source with ambiguous or NC-leaning terms, only after legal/product sign-off.
4. Record the actual import via `CefrResourceSource.RecordImport(...)` — this is intentionally
   blocked by the entity if `IsImportApproved` is false.

## Non-goals of this document

This is not a legal opinion. It is a working checklist so that the eventual import decision is
made with the right questions already surfaced, not skipped under delivery pressure. Final
import approval should involve whoever owns licensing/legal risk for LinguaCoach, not just
engineering judgment.
