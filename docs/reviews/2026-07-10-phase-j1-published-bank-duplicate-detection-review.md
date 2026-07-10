---
status: current
lastUpdated: 2026-07-10 23:40
owner: engineering
supersedes:
supersededBy:
---

# Phase J1 — Duplicate Detection Against the Published Resource Bank

**Date:** 2026-07-10
**Related sprint/feature:** Second phase from `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md` (§D), following Phase J0 (docs sync).
**Files reviewed/changed:**
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceCandidateValidationService.cs`
- `tests/LinguaCoach.UnitTests/ResourceImport/ResourceCandidateValidationServiceTests.cs`

## Problem

The audit found that `ResourceCandidateValidationService.ValidateDedupAsync` checked a staged
candidate's exact content fingerprint against other `ResourceCandidate` rows (within-run,
within-source, global) but never against already-published `ResourceBankItem` rows. A stale code
comment claimed the published tables had "no fingerprint-shaped column" — no longer true since
Phase I0 gave `ResourceBankItem` a real `ContentFingerprint` column for every content type. A
resource could be re-imported and re-published indefinitely without ever being compared to content
already live in the bank.

## Decision

Asked the user directly: should an exact fingerprint match against the published bank hard-block
publish, or stay advisory (matching every other dedup gate in this service)? **Decided: advisory
warning**, consistent with the within-run/within-source/global candidate checks — forces
`NeedsReview`, never blocks publish outright. Rationale: re-importing content identical to
something already live is a legitimate scenario (e.g. a corrected re-upload that happens to
normalize to the same fingerprint), so a human reviews it rather than the system rejecting it
automatically.

## What changed

Added a new check at the end of `ValidateDedupAsync`: query `_db.ResourceBankItems` for a matching
`ContentFingerprint`. If found, add the warning `"Duplicate: an already-published Resource Bank
item has the same content fingerprint."`, which (like every other `"Duplicate"`-prefixed warning in
this service) forces `needsHumanReview = true` and the candidate's status to `NeedsReview`.

**Self-match guard:** the check is skipped when `candidate.IsPublished` is already true. A
candidate's own published counterpart always shares its fingerprint (`PublishAsync` copies it
verbatim), so checking an already-published candidate against the bank would only ever "find"
itself — this was caught by a real test regression during implementation
(`InternalResourceSeedPackE8SeederTests.Every_e8_candidate_validates_as_Passed`, which re-validates
candidates after the seeder has already published them) and fixed by gating the new check on
`!candidate.IsPublished`. In the normal admin workflow (validate → review → approve → publish),
`IsPublished` is false at validation time, so this guard doesn't weaken the check for the case it
exists to catch — a not-yet-published candidate whose content already exists in the bank.

Removed the stale comment block explaining why the published-bank check was skipped (it was
factually wrong post-I0) and replaced it with the guard's own rationale.

No schema/migration change — `ResourceBankItem.ContentFingerprint` already existed as a column;
only the query was missing.

## Tests

Added two unit tests to `ResourceCandidateValidationServiceTests`:
- `Duplicate_of_already_published_resource_bank_item_is_flagged_needs_review` — seeds a published
  `ResourceBankItem` with a known fingerprint, then a not-yet-published candidate with the same
  fingerprint; asserts `NeedsReview` + the new warning text.
- `No_published_duplicate_warning_when_fingerprint_does_not_match_any_bank_item` — seeds an
  unrelated published item with a different fingerprint; asserts `Passed` and no false positive.

## Validation

- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline).
- `dotnet test --configuration Release` — 3,426/3,426 passing (5 architecture, 2,109 unit [+2 new],
  1,312 integration). One regression found and fixed during implementation (see self-match guard
  above) before the final green run.
- Frontend not touched this phase.

## What was NOT changed

- Publish is still never hard-blocked by a duplicate finding at any level (candidate-vs-candidate
  or candidate-vs-published) — this was an explicit product decision this session, not an oversight.
- No fuzzy/near-duplicate detection — exact fingerprint match only, unchanged.
- `ResourceCandidatePublishService` itself was not touched; the new gate lives entirely in
  validation, matching where every other dedup check already lives.

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`,
  `ResourceCandidateValidationService.cs`'s existing doc comments.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry).
- Docs intentionally not updated: none — this phase's scope was small enough that its own review
  file plus the two standing logs cover it fully.
- Reason: n/a.

## Risks or unresolved questions

None new. The hard-block-vs-advisory question is now resolved (advisory); if content volume grows
and duplicates slip through review in practice, revisiting that decision is a future product call,
not a code gap.

## Final verdict

Closes the "no dedup against the published bank" gap identified in the architecture audit.
Duplicate detection is now advisory-consistent across all three levels (within-run, within-source/
global candidates, and now published bank), with no false-positive self-matching for
already-published candidates.

## Next recommended action

Proceed to Phase J2 (AI-assisted Lesson/Exercise/Module generation) per the audit's phase ordering,
or Phase J3 (admin preview-as-learner for Modules) if the user prefers to close the UX gap first.
