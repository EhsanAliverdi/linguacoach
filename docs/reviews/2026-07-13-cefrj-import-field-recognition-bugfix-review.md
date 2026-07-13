# Bug fix: real-world CSV import rejected 100% of rows (CEFR-J Vocabulary Profile)

**Date:** 2026-07-13
**Related:** Phase J5 (import pipeline, all sub-phases just closed)
**Type:** Major debugging analysis / bug fix

## Trigger

User attempted to import a real published dataset —
`https://github.com/openlanguageprofiles/olp-en-cefrj/blob/master/cefrj-vocabulary-profile-1.5.csv`
(the CEFR-J Vocabulary Profile, ~7,800 words) — via the admin file-upload import flow and got:

> 0 of 7799 row(s) staged, 7799 warning(s), 7799 rejected. All 7799 row(s) were rejected — see raw
> record warnings for details.

## Root cause

The file's header row is `headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold`. Neither
`ResourceImportService`'s recognized content-field names (`word`/`lemma` for vocabulary) nor its
CEFR-level metadata column name (`cefrlevel`) matched this file's actual column names
(`headword`, `CEFR`). Every row failed Gate 3 ("must have at least one recognizable content
field") before candidate-type inference or CEFR mapping ever ran.

This is not a parsing bug — the deterministic field-name-matching pipeline worked exactly as
designed. The gap is that the recognized field-name set was narrower than a real, commonly-used
published dataset's own header convention. `headword` and `CEFR` (capitalized, no "Level" suffix)
are the standard column names in academic/published CEFR word lists (CEFR-J, and similar profiles
following the same convention) — not an obscure or malformed format.

## Fix

- `VocabularyFields`/`AnyContentFields`: added `headword` as a recognized alias for `word`/`lemma`,
  in both per-row inference (`InferCandidateType`) and forced-type extraction
  (`ExtractCanonicalTextForType`).
- CEFR-level metadata column: added `cefr` as a recognized alias for `cefrlevel` (new
  `GetFieldAny` helper trying multiple column names, since the existing single-name `GetField`
  didn't support this). This means CEFR-J's `CEFR` column is now read into
  `ResourceCandidate.CefrLevel` at import time via the existing Phase E6 deterministic-mapping
  path — the same mechanism that already handles `cefrLevel`/`skill`/`subskill`/`tags` columns
  when an internally-authored pack already knows its own classification.
- Publish-time and preview-time vocabulary field extraction (`BuildVocabularyEntry`,
  `BuildVocabularyPreview`, `DeriveTitle`) also updated to recognize `headword`, for consistency —
  these already had a `CanonicalText` fallback so they weren't broken, but would have shown a
  misleading "no word/lemma field" warning for every row otherwise.
- **Gate 3's rejection message rewritten to be actionable**: it now lists the row's *actual* column
  names alongside the full recognized set and a concrete suggestion ("rename a column, or use a
  different content type"), instead of a generic fixed list that gave no clue what was actually
  wrong with *this* file.

## A real regression caught before commit

Fixing the CEFR column recognition had a side effect: `ResourceCandidateBatchAnalysisServiceTests`'
shared test fixture (`ImportOneApprovedCandidateAsync`, used by 5 tests in
`AdminResourceImportEndpointTests`) used a CSV with a `cefr` column and no `DefaultCefrLevel`. Once
`cefr` became recognized, the resulting candidate got its `CefrLevel` set deterministically at
import time (Phase E6), which also sets `AiAnalysisJson` non-null — meaning the candidate no
longer counted as "pending" for `AnalyzePendingCandidates` (defined as `AiAnalysisJson == null`).
One test (`AnalyzePendingCandidates_Batch_Reports_Considered_And_Analyzed_Counts`) failed as a
result. This is not a bug in the fix — it's the Phase E6 mechanism working exactly as designed
(a row that already carries its own valid CEFR value doesn't need AI classification) — the test
fixture's incidental column choice just happened to collide with the newly-recognized name. Fixed
by dropping the `cefr` column from that shared fixture (it never needed to test CEFR mapping,
just "one importable candidate").

## Decisions made

- Scope kept narrow: fixed exactly the two column-name mismatches this specific file hit
  (`headword`, `CEFR`), rather than speculatively adding a large synonym list for every
  conceivable header variant. Broader coverage (AI-assisted structure/column detection) was
  raised by the user as a follow-up and is tracked separately — see below.
- Did not touch Gate 1 (English-only) or Gate 2 (source approval) — only Gate 3's rejection
  message and the recognized-field lists.

## Follow-up raised by the user (not implemented in this fix)

The user asked two related questions after seeing this failure:
1. Should file import be processed by AI to identify structure, rather than fixed field-name
   matching? (I.e., real AI-assisted column-mapping detection — this is distinct from J5b's "Mixed
   (auto-detect per row)," which is deterministic per-row *type* inference over a fixed field-name
   set, not AI-based *column-name* mapping. The "AI structure detection is not yet implemented"
   hint text has existed in this UI since Phase H2 and was preserved, not built, through all of
   J5a-d.)
2. Error messages need to be much better across different source-file structures generally (this
   fix's Gate 3 message rewrite is a first step, not a complete answer).

Both are legitimate, larger scope than this bug fix — flagged for a separate scoping
conversation/phase, not bundled into this hotfix.

## Verification

- `dotnet build` — clean, 0 errors.
- `dotnet test` (full suite) — **3,498 passed, 0 failed** (5 architecture, 2,173 unit incl. 2 new
  tests for `headword`/`CEFR` recognition and the improved rejection message, 1,320 integration
  incl. the regression fix above).
- Live re-import of the actual reported file (`cefrj-vocabulary-profile-1.5.csv`, 7,799 rows)
  against the rebuilt API: **7,798 of 7,799 staged** (1 rejected, not investigated further — a
  single-row edge case is expected background noise, not evidence of a remaining systemic gap),
  all correctly classified as `VocabularyEntry` with real CEFR levels read from the file's own
  `CEFR` column. Zero console errors.

## Final verdict

Fixed and verified against the exact file that surfaced the bug. Ready to commit locally.

## Next recommended action

Commit this fix locally. Separately, scope the user's AI-structure-detection request as its own
phase/decision before any implementation — it's a materially different, larger feature (calling an
AI provider to propose a column mapping, an admin review/confirm step, usage tracking per
AGENTS.md's AI rules) than anything built across J5a-d, not an extension of this bug fix.
