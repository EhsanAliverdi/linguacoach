---
title: Phase H7 — Practice Gym Module Pipeline — Implementation Review
date: 2026-07-09
related: Phase H7 (Practice Gym Module Pipeline), H-track (docs/architecture/product-model-realignment-h0.md)
status: complete
---

# Phase H7 — Practice Gym Module Pipeline — Implementation Review

**Date:** 2026-07-09
**Related sprint/feature:** Phase H7 — Practice Gym Module Pipeline, part of the H-track
(`Resource Bank Item → Learn Item/Activity Definition → Module Definition → Daily Lesson Module
Pipeline (H6) → Practice Gym Module Pipeline (H7)`). Second runtime consumer of
`ModuleDefinition`, after H6's Daily Lesson pipeline.

## Mandatory Step -1 — H6 ancestry verification

Before any implementation, verified that H6 (`3dbcaf19`) actually includes H5 (`c0f20708`) in its
history — the phase brief flagged a discrepancy between the reported "HEAD before work" and H5's
commit that needed checking first:

```
git merge-base --is-ancestor c0f20708 3dbcaf19 && echo "H6 includes H5"
→ H6 includes H5
```

`c0f20708` (H5) is `3dbcaf19`'s (H6) direct parent in `git log --oneline` — H6 was built on top of
H5 correctly. `git status --short` showed a clean working tree before any H7 changes began. No
branch/history correction was needed; H7 proceeded as planned.

## Step 0 audit — existing Practice Gym flow

- **Two parallel Practice Gym entry points exist today**, both of which had to keep working
  unchanged: (1) `PracticeGymSuggestionsController`/`IPracticeGymSuggestionService` — student-
  facing `GET api/practice-gym/suggestions` reading `StudentActivityReadinessItem` rows with
  `Source == ReadinessPoolSource.PracticeGym`; (2) the older
  `ActivityController.GetPracticeGymNext`/`IPracticeGymPoolService` — `GET
  api/activity/practice-gym/next`, reading `PracticeActivityCache` directly with a legacy
  on-demand-AI fallback via `IGetNextActivityHandler`.
- **Generation/refill path**: `PracticeGymBufferRefillJob` (queues `PracticeActivityCache` rows,
  deterministic, no AI) → `PracticeGymGenerationJob` (materializes each row into a
  `LearningActivity`, either via the `ActivityTemplate` Form.io pilot path — for the 8
  `TemplateMigratedPatternKeys` patterns — or the legacy free-form `IAiActivityGenerator` path;
  both call AI). Neither of these was touched — H7 does not generate or materialize anything.
- **`ModuleDefinition`/`ActivityDefinition`/`LearnItem` had zero Practice Gym wiring before H7** —
  exactly the same starting point H6 had for Today, confirming H6's shape could be reused nearly
  directly.
- **`IPracticeGymSuggestionService`'s existing DTOs (`PracticeGymSuggestionsDto`/
  `PracticeGymSuggestionItemDto`) already omit all answer/scoring data** — confirming the
  student-safe-projection convention H7's new DTOs needed to follow was already established here,
  not just in H6.
- **`ActivityTemplate` (Form.io pilot) and H4's `ActivityDefinition` remain two separate,
  unconnected entities** — selecting a `ModuleDefinition`'s linked `ActivityDefinition` does not
  give Form.io rendering "for free" the way a template-sourced `LearningActivity` does. H7
  deliberately does not attempt to bridge this (see Decisions below) — it projects
  `ActivityDefinition.FormSchemaJson` as display-only content, same as H6 did for Daily Lesson.
- **Additive table needed**: yes — `StudentActivityReadinessItem`/`PracticeActivityCache` have no
  concept of "which admin-curated Module was suggested," so a new bookkeeping table
  (`StudentPracticeGymModuleAssignment`) was added, mirroring H6's
  `StudentDailyModuleAssignment`.
- **No optional `ModuleDefinitionId` link was added to any existing Practice Gym runtime table** —
  `StudentActivityReadinessItem`/`PracticeActivityCache`/`LearningActivity` are all untouched;
  the new bookkeeping table stands alone, same non-invasive posture as H6.

## What was built

1. **Domain:** `StudentPracticeGymModuleAssignment` (additive bookkeeping entity — `StudentId`,
   nullable `ModuleDefinitionId`, `SuggestedAt` (`DateTimeOffset`, not date-only — Practice Gym
   suggestions aren't bound to a calendar day the way Today is), `Status`, `SelectionReason`,
   `FallbackReason`, `SelectedAt`/`DismissedAt`/`ConsumedAt` — all reserved for a future phase,
   never set by H7) and `PracticeGymModuleAssignmentStatus` enum (`Suggested`/`Presented`/
   `Selected`/`Dismissed`/`Consumed`/`Expired`/`FallbackOnly`). Not a student attempt/score
   record.
2. **Persistence:** `StudentPracticeGymModuleAssignmentConfiguration` (snake_case table
   `student_practice_gym_module_assignments`, `DeleteBehavior.Restrict` FK to `ModuleDefinition`,
   indexes on `(StudentId, SuggestedAt)`, `(StudentId, ModuleDefinitionId)`, `Status`). Migration
   `Phase_H7_AddPracticeGymModulePipeline` — **additive-only**, one new table, no change to any
   existing table.
3. **Application:** `IPracticeGymModuleSelectionService.SelectAsync` (pure/read-only — no writes)
   with `PracticeGymModuleSelectionRequest`/`Result`/`PracticeGymModuleSuggestion`, extended
   beyond H6's shape with Practice Gym-specific self-directed signals
   (`RequestedSkill`/`RequestedSubskill`/`RequestedObjectiveKey`/`RequestedDifficulty`/
   `WeaknessSignals`) and per-suggestion `IsReview`/`IsScaffold`/`IsRemediation` flags.
   `IPracticeGymModuleAssignmentRecorder` is the single write path. `PracticeGymSuggestionsDto`
   extended with an additive, optional `ModuleSuggestions` field (a nested
   `PracticeGymModuleSelectionResult`) — a plain class with `init` properties, so this is a
   non-breaking addition (existing object-initializer construction sites are unaffected).
4. **Infrastructure:** `PracticeGymModuleSelectionService` — deterministic, no AI call, adapted
   from H6's `DailyLessonModuleSelectionService`. Eligibility rule is identical to H6: Approved
   Module + at least one Approved linked `LearnItem` + at least one Approved linked
   `ActivityDefinition`. Self-directed narrowing: `RequestedObjectiveKey` → `RequestedSkill` →
   `RequestedSubskill` each narrow the eligible pool only when doing so still leaves at least one
   candidate (an over-narrow request degrades gracefully rather than forcing a fallback — Practice
   Gym selection is a soft preference, unlike Today's fully automatic pick). CEFR: same "exact
   match preferred, broaden only as an explicit review/scaffold/remediation/fallback reason"
   behavior as H6. Scoring adds subskill/objective/weakness-signal/difficulty-band terms on top of
   H6's skill/tag/estimated-time terms. `IsReview` is set when the broadened selection is a lower
   CEFR level than requested; `IsScaffold` when the broadened module has no CEFR level at all
   (a generic building block); `IsRemediation` when the module's skill matches an entry in
   `WeaknessSignals`. 14-day reuse guard via `StudentPracticeGymModuleAssignment` history (plus
   any explicitly-passed `RecentSuggestedModuleDefinitionIds`) — implemented as a fetch-then-
   filter-client-side query, not a server-side `DateTimeOffset` comparison, because SQLite's EF
   Core provider cannot translate `DateTimeOffset >= parameter` (caught by the new unit tests
   failing during development; PostgreSQL does not have this limitation, but the client-side
   filter works identically on both providers). Never throws — wrapped in a top-level try/catch
   that degrades to `FallbackRequired = true`. `PracticeGymModuleAssignmentRecorder` is idempotent
   per student per **calendar day** (not per full request) — Practice Gym suggestions are
   recomputed on every page load, so without day-level idempotency the table would grow unbounded
   and a module suggested seconds ago would immediately look "recently suggested" to the reuse
   guard. Wired into `PracticeGymSuggestionService.GetSuggestionsForStudentAsync` additively: the
   existing readiness-pool-backed suggestion logic (ranking, dedup, pilot gating, etc.) is
   completely unchanged; module selection runs in a **separate** try/catch (logged via
   `ILogger`, never rethrown) and attaches the result via the new `ModuleSuggestions` property.
5. **API:** `AdminPracticeGymModuleController` (admin-only) —
   `GET /api/admin/practice-gym/modules/preview?studentId=&maxSuggestions=&skill=&subskill=`
   (calls the selector directly, bypassing the recorder — no side effects) and
   `GET /api/admin/practice-gym/students/{studentId}/assignments?days=` (assignment history from
   `StudentPracticeGymModuleAssignment`).
6. **No new student "start" endpoint.** Per the phase brief's own permission ("if start cannot be
   done safely in H7, return suggestions only"): `ActivityDefinition` has no attempt/scoring
   runtime wire-in yet (confirmed in the Step 0 audit), so launching a module-derived practice
   session safely was out of scope. Module suggestions are display-only in H7; the existing
   suggestedItems/continueItems/reviewItems `start` flow remains the only way to actually launch
   practice.
7. **Angular:** `practice-gym-suggestions.service.ts` extended with
   `PracticeGymModuleSuggestion`/`PracticeGymModuleSuggestionsSection`/
   `PracticeGymModuleLearnItemSummary`/`PracticeGymModuleActivitySummary` and
   `PracticeGymSuggestionsResponse.moduleSuggestions`. Student Practice Gym page gets a
   read-only "Recommended module practice" section (title, skill/CEFR badges, estimated minutes,
   reason, a "Coming soon" label instead of a Start button) shown only when the backend returned
   module suggestions — no new network call, since `moduleSuggestions` rides along on the
   existing `GET api/practice-gym/suggestions` response. Admin `admin-student-detail` page gets a
   new read-only "Practice Gym module selection" card, mirroring H6's Daily Lesson card exactly.
8. **Tests:** 26 new unit tests (`PracticeGymModuleSelectionServiceTests`, SQLite in-memory; one
   more than the phase brief's 25-item list — an extra assignment-history-based reuse-guard test
   was added alongside the explicit-recent-ids one) + 14 new integration tests
   (`PracticeGymModulePipelineEndpointTests`, plain `ApiTestFactory`). Full backend suite: 3,895
   tests (2,334 unit + 1,556 integration + 5 architecture), 0 failures.
9. **Docs:** this review, plus road-map.md, current-sprint.md, current-product-state.md,
   product-backlog.md, product-model-realignment-h0.md, learning-activity-engine.md,
   architecture/README.md, TODOS.md — including the requested roadmap/TODO note that legacy
   bank/admin structures should be **removed** (not hidden) after a Plan-Sync-After-H7 checkpoint,
   via new H8/H9 placeholders.

## Decisions made (P1 — design decisions requiring an explicit call)

1. **No student "start" flow for module suggestions.** `ActivityDefinition` (H4) has no attempt,
   scoring, or feedback runtime wired to it anywhere in the codebase — building a safe "start"
   endpoint would mean either (a) inventing a new attempt/scoring path in H7 (explicitly
   out-of-scope: "no final module scoring... no complete feedback/rating loop") or (b)
   materializing a `ModuleDefinition`'s `ActivityDefinition` into a `LearningActivity` the way
   `ActivityTemplate` already does (a much larger, riskier change not scoped for H7). Module
   suggestions are display-only this phase; the phase brief explicitly permitted this fallback.
2. **`SuggestedAt` is `DateTimeOffset`, not a date-only field like H6's `AssignedForDate`.**
   Practice Gym suggestions are recomputed on every page load rather than once per calendar day
   the way Today is — a precise timestamp is the more natural unit here. This required switching
   the 14-day reuse-guard query and the recorder's idempotency check to a fetch-then-filter
   pattern instead of a server-side `Where` clause, because SQLite's EF Core provider cannot
   translate `DateTimeOffset` comparisons (a test-only limitation — PostgreSQL has no such
   restriction, and the client-side filter is correct on both).
3. **Self-directed request narrowing degrades gracefully instead of hard-filtering.** When a
   student requests a specific skill/subskill/objective that has no eligible Module, H7 falls
   back to the broader eligible pool rather than declaring `FallbackRequired`. Rationale: Practice
   Gym is inherently self-directed and exploratory — a request-mismatch should surface *something*
   useful rather than nothing, unlike Today's fully automatic, higher-stakes selection.
4. **`IsReview`/`IsScaffold`/`IsRemediation` are derived flags, not independent selector inputs.**
   `IsReview`/`IsScaffold` are set only during CEFR broadening (mirroring H6's single "review/
   scaffold... fallback" reason string, but split into two named booleans for a lower-vs-
   scaffold-level distinction); `IsRemediation` is set independently whenever a suggestion's skill
   matches a `WeaknessSignals` entry, whether or not CEFR was broadened. This is a reasonably
   simple heuristic, not a "true" review/scaffold/remediation curriculum classification (which
   `RoutingReason` — the existing readiness-pool concept — already models more richly for legacy
   suggestions); a future phase could align these more closely if useful.
5. **No `ModuleDefinitionId` link added to `PracticeActivityCache`/`StudentActivityReadinessItem`/
   `LearningActivity`.** All three remain completely untouched, matching the "no
   PracticeActivityCache replacement unless proven safe and explicitly additive" constraint — the
   new bookkeeping table is fully standalone.

## AskUserQuestion decisions

None — the phase brief was fully self-contained; no ambiguity required a user clarification
during implementation.

## Backend tests added (40 total: 26 unit + 14 integration)

**Unit (`PracticeGymModuleSelectionServiceTests`):** approved module selected for matching
skill/CEFR; pending module not selected; rejected module not selected; approved module with
pending Learn Item not selected; approved module with pending Activity Definition not selected;
wrong-CEFR module not silently selected when an exact match exists; lower-level module selected
only with an explicit review/scaffold/remediation reason (and `IsReview` set); requested skill
influences selection; requested subskill influences selection; weakness signals influence
selection and mark `IsRemediation`; context/focus tags influence selection; estimated
minutes/difficulty are used where feasible; recently-suggested module not suggested again too
soon (explicit recent-ids list); recently-suggested module excluded via assignment history;
no suitable module returns fallback-required, not an exception; malformed module JSON handled
safely; no-CEFR student uses safe broad matching; selection result does not expose answer keys;
selection result does not expose scoring rules; selection creates no Module attempts; selection
does not update mastery; selection does not mutate `ModuleDefinition`; selection does not mutate
`LearnItem`; selection does not mutate `ActivityDefinition`; selection does not delete or replace
Practice Gym cache records; Today's module pipeline (`StudentDailyModuleAssignment`) remains
unaffected.

**Integration (`PracticeGymModulePipelineEndpointTests`):** suggestions include module
suggestions when a compatible approved Module exists; suggestions fall back when none exists;
module suggestions do not expose answer keys; module suggestions do not expose scoring rules;
existing Practice Gym suggestions endpoint still works; existing Practice Gym fallback path still
works (repeat calls both 200); admin preview shows suggested Modules; admin preview shows a
fallback reason when no Module is available; non-admin rejected (403) for the admin preview
endpoint; H3 Learn Items endpoint still works; H4 Activities endpoint still works; H5 Modules
endpoint still works; H6 Today endpoint still works (route/DI regression check — no 500); existing
readiness-pool health endpoint not broken.

## Risks or unresolved questions

- Module suggestions have no launch path yet — students can see them but not start them. A future
  phase (H7 follow-up or part of H8) will need to decide whether to build a real attempt/scoring
  runtime for `ActivityDefinition` or bridge it into the existing `LearningActivity`/
  `ActivityTemplate` materialization path.
- No automated frontend test exercises the new Practice Gym module section or the admin preview
  card directly (consistent with H6/H3/H4/H5 precedent) — the production build was used to
  confirm no new TS/Angular compile errors instead.
- The `IsReview`/`IsScaffold`/`IsRemediation` heuristic is intentionally simple (see Decision 4)
  and may need refinement once there's real admin/student usage to tune it against.

## Final verdict

**Complete and accepted.** All 27 acceptance criteria from the phase brief are met: the H6
ancestry check confirmed H6 includes H5 before any code changed; the working tree was clean
before H7 began; a Practice Gym module selection service exists and only ever selects approved
Modules with approved linked content; selection considers requested skill, subskill/objective,
CEFR, context/focus tags, and weakness signals where available; a 14-day reuse guard and clear
fallback reasons exist; Practice Gym surfaces module-based suggestions additively when suitable
Modules exist and falls back safely (readiness-pool suggestions are always present) when they
don't; no answer keys or scoring rules are ever exposed to students; Today (H6) is completely
unaffected; PG-v2 was not started; no Module attempts, mastery updates, or readiness/delivery-
queue deletion occurred; no legacy bank removal was performed; existing H3/H4/H5/H6 flows all
still work (regression-tested); the full backend suite (3,895 tests) and the Angular production
build both pass (only the pre-existing bundle-size budget warning); docs are updated, including
the requested removal-oriented cleanup note for after H7; committed locally, not pushed, not
deployed.

**Confirmed:** PG-v2 not started. No full Practice Gym redesign. No student self-authored/custom
module creation. No Module attempts. No final module scoring. No learner mastery updates from
Modules. No `ActivityTemplate` replacement. No `LearningActivity` replacement. No
`LearningSession` replacement. No `PracticeActivityCache` deletion/replacement (fully untouched).
No readiness pool deletion. No delivery queue deletion. Today fallback remains intact. Practice
Gym fallback remains intact (both entry points). No legacy bank removal in H7 — that work is
explicitly deferred to a future Plan-Sync-After-H7 checkpoint and H8/H9 (see roadmap/TODO
updates). No physical `ResourceBankItem` consolidation. No external datasets. No Persian/bilingual
content. No direct final-table seeding.

## Next recommended action

**Plan-Sync-After-H7** (docs-only) to decide the sequencing and scope of the removal-oriented
cleanup track — **H8 (Content Studio/Admin IA cleanup and removal planning)** and **H9 (Legacy
Bank Structure Removal and Consolidation)** — now that both H6 and H7 have proven the module
pipeline pattern end-to-end. A module-suggestion "start" flow (see Risks above) is a separate,
smaller follow-up that could land before or alongside H8/H9.
