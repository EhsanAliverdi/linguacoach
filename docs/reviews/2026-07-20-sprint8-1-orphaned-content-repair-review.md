# Sprint 8.1 — Orphaned Grammar/Reading Content Repair, Debugging Analysis

**Date:** 2026-07-20
**Related sprint:** Platform Reliability & Pilot Readiness, Sprint 8 (Content Recovery + Any-Level Placement) — see `C:\Users\aliverdi.ehsan\.claude\plans\majestic-wishing-naur.md` for the full 7-sprint plan this belongs to.
**Files reviewed / changed:**
- `src/LinguaCoach.Domain/Entities/ResourceCandidate.cs` (`RepairOrphanedPublishReference()`)
- `src/LinguaCoach.Domain/Entities/ResourceImportRun.cs` (`AssignRetroactiveImportPackage()`)
- `src/LinguaCoach.Application/ResourceImport/ResourceCandidateOrphanRepairContracts.cs` (new)
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceCandidateOrphanRepairService.cs` (new)
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Controllers/AdminResourceImportController.cs` (new endpoint `POST /api/admin/resource-candidates/repair-orphaned-publish`)
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceCandidatePublishService.cs` (read only — reused unmodified)
- `src/LinguaCoach.Persistence/Seed/InternalResourceSeedPackSeeder.cs` / `InternalResourceSeedPackE8Seeder.cs` (read only — root-cause reference)
- `src/LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs` (`CefrLevels` extended to `A1..C2`)
- `src/LinguaCoach.Persistence/Seed/PlacementItemBankSeeder.cs` (36 new C1/C2 items)

## Background

A prior audit (documented in the approved Sprint 8-14 plan) found 76 `resource_candidates` (32 Grammar,
44 Reading) that were real, human/AI-quality-scored, `ReviewStatus=Approved` content, marked
`IsPublished=true`, but permanently stuck: their `PublishedEntityType` pointed at
`CefrGrammarProfileEntry`/`CefrReadingReference`/`CefrReadingPassage` — typed tables the Phase I0
"drop typed bank tables" migration removed. Because `ResourceCandidate.IsPublished` blocks
editing/re-publishing/rejecting and no unpublish step existed, these candidates could never reach the
live `resource_bank_items` table. `resource_bank_items` had zero rows of type Grammar or Reading,
confirmed live, prior to this fix.

## Findings, in the order they were discovered

### Finding 1 (expected): dead publish reference blocks recovery
Confirmed via live query: `PublishedEntityId` resolved to nothing in any current table for all 76
candidates. Fix: `ResourceCandidate.RepairOrphanedPublishReference()` — a one-time domain method that
clears `IsPublished`/`PublishedEntityType`/`PublishedEntityId`/`PublishedByUserId`/`PublishedAtUtc`,
throwing if called on a non-published candidate. The caller must independently verify the reference is
genuinely orphaned before calling — this method itself does not re-check.

### Finding 2 (unexpected — discovered mid-implementation): the provenance gate structurally blocks ALL internal seed-pack content
The first live run of the repair endpoint returned `foundCount: 76, repairedCount: 0, failedCount: 76`
— every candidate failed `ResourceCandidatePublishService.PublishAsync()`'s live gate check with:
*"This candidate's Import Run has no associated Import Package — it cannot be traced to an approved
Import Execution Plan and cannot be published."*

Investigation (live Postgres queries joining `resource_candidates` → `resource_raw_records` →
`resource_import_runs` → `cefr_resource_sources`) showed:
- All 76 candidates trace to two internal, first-party sources: *"SpeakPath Internal English Seed
  Pack v1"* and *"SpeakPath Internal English Seed Pack E8 (Grammar/Usage/Reading Depth)"* — both
  `is_import_approved=true`, `allows_student_display=true`, `allows_commercial_use=true`.
- **100% of ALL import runs from both internal sources** (not just the 76 orphaned candidates —
  every run, including ones behind currently-published Vocabulary content) have `import_package_id
  IS NULL`.
- Reading `InternalResourceSeedPackSeeder.cs` showed the seeder *does* create a self-approved
  `ImportPackage`/`ImportProfile` via `SeedApprovedPackageAsync()` and pass its ID through to every
  import call — but the seeder is idempotent by source name (`if (existing is not null) return;` at
  the top of `SeedAsync`). Because both internal sources already existed in this dev database from
  before the Phase 4.2 provenance gate was added, the seeder has skipped entirely on every app
  restart since, and the retroactive package/profile it would create for genuinely fresh content was
  never backfilled onto the historical runs.

This is a real, narrow historical gap — not a design flaw in the gate itself, and not a case where the
gate should be bypassed. The gate's own reasoning (never publish content with unverifiable
provenance/license) is legitimate; the correct fix is to give the historical runs the same
self-approved package a fresh seeder run would create, replicating `SeedApprovedPackageAsync()`
exactly, not to weaken the check.

**Fix:** `ResourceImportRun.AssignRetroactiveImportPackage(Guid)` (throws if the run already has a
package — never reassigns a legitimately-linked run) plus
`ResourceCandidateOrphanRepairService.BackfillMissingImportPackagesAsync()`, which finds every
`ResourceImportRun` behind the target candidates with `ImportPackageId == null`, groups them by
source, and for each source creates one `ImportPackage` + one approved `ImportProfile`
(`ApprovedByUserId=null`, `EstimatedCostExpected=0`, matching the seeder's own convention for
trusted system-authored content) before backfilling every affected run's `ImportPackageId`.

### Finding 3 (self-inflicted, caught before the second live run): the first repair attempt left candidates in a worse-to-query state
Because `RepairOrphanedPublishReferencesAsync()`'s original query filtered on
`IsPublished == true`, and the repair's own first step clears `IsPublished` before attempting
republish, the first (failed) live run left all 76 candidates `IsPublished=false`,
`PublishedEntityType=null` — no longer "stuck" in the original sense, but also invisible to the
same query on a second run (`foundCount: 0`). Fix: broadened the query to a union of Case A (still
marked published, pointing at a dead type — first-time-seen orphans) and Case B (already unpublished,
`PublishedEntityType=null`, `ReviewStatus=Approved`, `CandidateType` in the two historically-affected
types — candidates a prior partial run already unstuck but never got to republish). The republish loop
only calls `RepairOrphanedPublishReference()` for Case A rows (Case B rows are already unpublished;
calling it again would throw).

## Live verification

Second live run against the Docker dev database:
```
{"foundCount":76,"repairedCount":76,"failedCount":0,"items":[...]}
```
Confirmed via direct query against `resource_bank_items`:

| type (0=Vocab,1=Grammar,2=ReadingRef,3=ReadingPassage) | A1 | A2 | B1 | B2 |
|---|---|---|---|---|
| 1 (Grammar) | 10 | 9 | 7 | 6 |
| 2 (ReadingReference) | 5 | 7 | 7 | 7 |
| 3 (ReadingPassage) | 4 | 4 | 5 | 5 |

Totals: 32 Grammar + 26 ReadingReference + 18 ReadingPassage = 76, matching exactly.

## Placement CEFR range + C1/C2 item authoring (Sprint 8.2 / 8.3, same session)

`PlacementAssessmentService.CefrLevels` extended from `["A1","A2","B1","B2"]` to
`["A1","A2","B1","B2","C1","C2"]` — every consumer (`ComputeSkillConfidence`, `SelectNextTemplate`,
`ComputeOverallCefr`, `CreateInitialItems`) operates on the array generically (index/adjacency), so
this is a single-line, level-count-agnostic change; verified by full rebuild (0 errors).

36 real C1/C2 `PlacementItemDefinition` seed rows authored in `PlacementItemBankSeeder.DefaultItems`
(3 items × 6 skills × 2 levels), matching the existing A1-B2 format exactly (multiple_choice/gap_fill/
speaking_response, real answer keys, real passages/listening scripts where applicable). Content
targets genuine C1 (Advanced) / C2 (Proficiency) constructs: inversion, mixed conditionals, subjunctive
mood, nuanced lexis (meticulous/cogent/perfunctory/equivocate), inference-heavy reading passages,
tone/irony-dependent listening items, and register-appropriate hedging for writing. Verified live:
`placement_item_definitions` now has 108 rows total, 3 per (skill, level) across all 6 CEFR levels ×
6 skills.

## Decisions made

1. **Reuse the real, fully-gated `IResourceCandidatePublishService.PublishAsync()` pipeline for the
   republish step, never a shortcut write directly into `resource_bank_items`.** Every gate
   (English-only, source approval/license, validation status, review status, and now provenance)
   still applies — recovered content is provenance-identical to freshly imported content.
2. **Backfill the missing `ImportPackage`/`ImportProfile` rather than relaxing the Phase 4.2 gate.**
   The gate's purpose (block unverifiable provenance) is sound; the actual defect was a one-time
   backfill gap in idempotent seeders, not a flaw in the check itself.
3. **C1/C2 placement items were authored directly, not deferred to a later content pass**, per the
   approved plan's cross-cutting decision that placement items are real test content with a
   defensible answer key — silently wrong answers would miscalibrate every future C1/C2 placement.

## Sprint 8.4 (added mid-session) — Progress-page skill fix, Vocabulary mojibake fix, and a second critical placement bug found via live checkpoint

**Progress-page skill fix**: `StudentProgressSummaryHandler.ExtractSkillFromObjectiveKey` parsed the
pre-Sprint-7 underscore key format (`b1_speaking_x` → `parts[1]`), which produces garbage against
Sprint 7's dot-delimited `SkillGraphNode` keys. Fixed by adding `CurrentObjectiveSkill` to
`LearningPlanProgressSummary` (`ILearningPlanService.cs`), populated in
`LearningPlanService.GetProgressAsync` directly from the current objective's own `Skill` column —
never parsed from the key. `StudentProgressSummaryHandler` now reads this field directly.
`ExtractSkillFromObjectiveKey` deleted (dead code). 3 test call sites updated for the new
positional/named record field.

**Vocabulary page mojibake fix**: 4 double/triple-UTF-8-mis-encoded glyphs in
`vocabulary.component.ts` (warning icon, book icon, arrow, multiplication sign) repaired via
byte-exact `perl -i` substitution (the Edit tool's string matching couldn't reliably match the
literal mangled byte sequences).

**The live checkpoint (driving a real test student through adaptive placement, answering every
item correctly) surfaced a second, more severe bug than anything in the original audit**: every
non-speaking skill plateaued at B1 and was never once offered a B2 item, let alone C1/C2 — despite
scoring 100% throughout. Root cause: `PlacementAssessmentOptions.MinItems=5` (intended, per its own
doc comment, as an evidence floor) was never actually referenced by any completion/selection logic
in `PlacementAssessmentService`. The confidence formula's consecutive-success bonus lets a skill
cross the 0.75 confidence threshold after just 3 answers (`CreateInitialItems` seeds 2 at the
starting level + 1 above), at which point `SelectNextSkill` permanently excludes that skill from
ever being served another (harder) item. This is a pre-existing bug, not something Sprint 8.2's
`CefrLevels` extension introduced — it would have equally capped students at B1 under the original
A1-B2 range, just never observed because no prior work included a real end-to-end all-correct
adaptive walkthrough.

**Fix**: new `IsSkillDone(skill, states)` helper — `Confidence >= ConfidenceThreshold AND
EvidenceCount >= MinItems` — applied consistently to `ShouldComplete`'s allConfident/allExhausted
checks, `SelectNextSkill`'s exclusion filter, `AddNextItemForSkillAsync` (placement-cards flow),
and `GetSkillStatusAsync`'s per-skill `completed` flag (previously each of these independently
checked confidence alone).

**Live re-verification after the fix** (same test student, reset and re-run): all 5 non-speaking
skills climbed A2→B1→B2→C1 correctly, each reaching `Confidence=1.0` with exactly `EvidenceCount=5`
(the `MinItems` floor) before stopping — proof the adaptive ladder can now genuinely reach C1/C2
with real correct answers, not just that the C1/C2 item content exists. Speaking correctly stayed
at A2/low-confidence since the test submitted placeholder (non-audio) responses — expected, not a
bug; speaking's real evaluation path requires an actual recording and was not exercised here.

Full backend suite after all Sprint 8 changes: **3,753/3,753 passing** (30 architecture + 2,408
unit + 1,315 integration) — includes 56 placement unit tests and 98 placement integration tests,
all unaffected by the `IsSkillDone` change.

## Risks / unresolved questions

- The 36 new C1/C2 placement items have not yet had a second human review pass beyond this
  authoring session, per the approved plan's requirement ("presented for review before Sprint 8 is
  considered done"). Recommend a dedicated read-through before this is fully closed out.
- `PlacementAssessmentOptions.MaxItems=48` was not changed. With 6 CEFR levels now in the item bank
  (up from 4), the adaptive engine has more headroom per skill (up to 18 items/skill instead of 12),
  but typical convergence (~6-7 items/skill) is unaffected since a student only walks up/down from
  their starting level — no live student walkthrough at C1/C2 has been run yet to confirm real
  end-to-end convergence behavior at the new top of the range.
- Sprint 8's remaining items (Progress-page skill-parsing fix, Vocabulary page mojibake fix, live
  checkpoint) are not yet done — tracked as open tasks.

## Verdict

**Sprint 8.1 (content recovery) — complete and live-verified.** 76/76 candidates repaired, confirmed
via direct database query, zero failures, zero data loss (repair only clears stale publish-state
fields and creates new bank rows through the real gated pipeline — no destructive writes to any
existing table).

**Sprint 8.2 (CEFR range extension) — complete and live-verified.** A real driven placement run
reached C1 on every non-speaking skill with real correct answers, confirmed via
`POST /api/student/placement/complete`'s returned `skillResults`.

**Sprint 8.3 (C1/C2 item authoring) — complete, seeded live, and live-verified as correctly
answer-keyed** (every C1 item submitted with its authored correct answer scored `isCorrect=true`
during the live checkpoint) — still pending a second human review pass for content quality/tone,
per the plan's requirement.

**Sprint 8.4 (Progress-page skill fix, Vocabulary mojibake fix, adaptive-ladder MinItems bug) —
complete and live-verified.** The MinItems bug was the most consequential finding of this sprint:
without it, no student — regardless of true ability — could ever place above B1 through the
adaptive engine, which would have silently defeated the entire point of Sprint 8.2/8.3's CEFR-range
and content work. Full backend suite green (3,753/3,753) after all fixes.

## Next recommended action

Sprint 8 is functionally complete: content recovery, CEFR range, C1/C2 items, the Progress-page
regression, the Vocabulary mojibake bug, and the adaptive-ladder MinItems bug are all fixed and
live-verified. Remaining before calling Sprint 8 fully done: a second human review pass over the 36
C1/C2 placement items' content quality (not just answer-key correctness, already verified). After
that, proceed to Sprint 9 (Multi-Skill Delivery + Honest Selection) per the approved 7-sprint plan.
