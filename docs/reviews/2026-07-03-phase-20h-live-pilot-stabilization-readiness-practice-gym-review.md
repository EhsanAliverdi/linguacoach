---
status: current
lastUpdated: 2026-07-03 (20H)
owner: engineering
supersedes:
supersededBy:
---

# Phase 20H — Live Pilot Stabilization: Readiness Edge Case + Practice Gym Deduplication

**Date:** 2026-07-03
**Related sprint/feature:** Phase 20H (follows Phase 20G — Live Student Pilot Golden Path Completion)
**Related TODOs:** `TODO-20G-1` (Practice Gym duplicate suggestions), `TODO-20G-3` (readiness audit 500 for pilot student)

## Goal

Remove the last two known blockers/risks from Phase 20G before inviting a real controlled pilot
student: the admin readiness audit 500 for `pilot.student.20e@speakpath.app`, and duplicate
Practice Gym "Suggested for you" cards. Stabilization only — no new AI scoring, CEFR update,
objective completion, Learning Plan regeneration, activity types, or UI redesign.

## Files reviewed

- `src/LinguaCoach.Infrastructure/Admin/StudentReadinessAuditService.cs`
- `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs`
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs`
- `tests/LinguaCoach.UnitTests/Admin/StudentReadinessAuditServiceTests.cs`
- `tests/LinguaCoach.UnitTests/PracticeGym/PracticeGymSuggestionServiceTests.cs`
- `tests/LinguaCoach.UnitTests/ReadinessPool/ReadinessPoolReplenishmentServiceEffectiveSettingsTests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/AdminStudentReadinessEndpointTests.cs`
- `TODOS.md` (`TODO-20G-1`, `TODO-20G-3`)
- `docs/architecture/student-readiness-and-backfill.md`

## Root cause — readiness audit 500 (`TODO-20G-3`)

`StudentReadinessAuditService.GetReadinessAsync` runs ten check-category methods sequentially.
Two of them (`AddLearningPlanChecksAsync`, `AddProgressChecksAsync`) already wrapped their risky
calls in try/catch, converting failures into a structured `Warning` check. **Four others —
`AddPracticeGymChecksAsync`, `AddActivityContentChecksAsync`, `AddAudioTtsChecksAsync`, and
`AddFeedbackAndReviewScaffoldChecksAsync` — had no exception handling at all.** Any unexpected
data shape, transient dependency failure, or query translation issue inside those four methods
propagated straight out of `GetReadinessAsync` and became a raw 500 for that one request, with no
opportunity for the audit to report a partial, structured result.

This matches the isolated-to-one-student symptom recorded in `TODO-20G-3`: a systemic bug would
fail for every student, but an unguarded exception triggered only by a specific student's data
shape (the reported 49 duplicate Practice Gym readiness items for one objective, plus a `speaking`
objective mapped to a `ListeningComprehension`-typed activity via pattern
`listening_multiple_choice_single`) explains why one student 500s while others return 200.
Production log/DB access to confirm the exact exception type was not available in this session
either — the fix instead removes the entire class of failure rather than chasing the one instance,
per the audit's own design principle ("must never 500 for valid student data").

One additional smaller finding while reading `AddAudioTtsChecksAsync`: the `AudioAssets` query
used `a.LearningActivityId!.Value` inside a `.Where(...).Contains(...)` without first filtering out
null `LearningActivityId` rows — hardened by adding an explicit `a.LearningActivityId != null`
filter ahead of it, defense-in-depth alongside the try/catch.

## Root cause — Practice Gym duplicate suggestions (`TODO-20G-1`)

`ReadinessPoolReplenishmentService.FillShortfallAsync` prevents duplicate readiness-pool items
using a `DuplicateKey(ObjectiveKey, PatternKey, CefrLevel)` computed two ways:

- **Existing items** (loaded from the DB): real `PatternKey` once an item has been materialized
  into an actual exercise by `PracticeGymGenerationJob.MaterializeAsync`.
- **New candidates** (about to be queued): `PatternKey` hardcoded to `null`, because pattern
  selection happens later, during materialization — not at queue time.

Because `DuplicateKey` equality included `PatternKey`, a queue-time candidate's key
`(objective, null, cefr)` could **never** equal a materialized item's key
`(objective, "listening_multiple_choice_single", cefr)`. Every replenishment run therefore failed
to recognize that an objective/level combination already had a ready item, and kept queuing more —
explaining the reported accumulation (49 rows for one objective in production, 6 duplicate cards
visible in Practice Gym for the pilot student). `PracticeGymSuggestionService` itself has no
dedupe logic of its own, so it faithfully surfaced every one of those rows as a separate card.

## Fixes applied

**Readiness audit (`StudentReadinessAuditService.cs`):**
Wrapped `AddPracticeGymChecksAsync`, `AddActivityContentChecksAsync`, `AddAudioTtsChecksAsync`, and
`AddFeedbackAndReviewScaffoldChecksAsync` bodies in try/catch, matching the existing pattern from
`AddLearningPlanChecksAsync`/`AddProgressChecksAsync`. On exception, each now logs a warning and
appends one structured `Warning`/`Warning`-severity check (`practicegym.check_failed`,
`activities.check_failed`, `audio.check_failed`, `feedback.check_failed`) instead of throwing. No
raw exception message or stack trace is ever included in the response — only `ex.GetType().Name`
in `TechnicalDetail`. Also hardened the `AudioAssets` null-FK query.

**Practice Gym replenishment (`ReadinessPoolReplenishmentService.cs`):**
`DuplicateKey` dropped `PatternKey` entirely — it is now `(ObjectiveKey, CefrLevel)` only, since
pattern is never known at queue time and comparing on it made the dedup check permanently
ineffective. Both the existing-keys projection and the new-candidate key were updated to match.

**Practice Gym suggestions (`PracticeGymSuggestionService.cs`), defense-in-depth:**
Added `ItemIdentityKey` (materialized `LearningActivityId` when present, else the readiness item's
own `Id`) and a `DedupeByIdentity` helper. `GetSuggestionsForStudentAsync` now:
- Dedupes within each bucket (Continue/Review/Suggested) independently.
- Builds Continue first, then excludes anything already claimed by Continue from Review; then
  excludes anything claimed by Continue+Review from Suggested — so **a single item can never
  appear in more than one bucket**, and **Continue always wins** ties.
- Caps (`MaxContinue`=3, `MaxReview`=4, `MaxSuggested`=6) are applied *after* dedupe, so they still
  bound the final visible lists correctly.
- Review-scaffold visibility gates (pilot enable flag, admin-review approval, pending/rejected
  exclusion) are unchanged — dedupe runs strictly after those existing filters.

## Tests added

**Unit — `StudentReadinessAuditServiceTests.cs`:**
- `SettingsProviderThrows_FeedbackChecksReturnWarning_NotException` — forces a collaborator
  exception and asserts the audit returns a structured `Warning` check, not an unhandled exception.

**Unit — `PracticeGymSuggestionServiceTests.cs`:**
- `GetSuggestions_DuplicateReadyItemsSameActivity_CollapseToOneSuggestedCard`
- `GetSuggestions_SameActivityInContinueAndReady_ContinueWinsAndNotDuplicatedInSuggested`
- `GetSuggestions_DistinctActivitiesBeyondCap_StillCapped`

**Unit — `ReadinessPoolReplenishmentServiceEffectiveSettingsTests.cs`:**
- `MaterializedItemWithRealPatternKey_PreventsReQueueingSameObjectiveAndLevel` — seeds a
  materialized Ready item with a real `PatternKey`, runs `RunAsync()`, and asserts no further item
  is queued for the same objective/level (this test failed with 2 items before the dedup-key fix,
  reproducing the bug, and passes with 1 after).

**Integration — `AdminStudentReadinessEndpointTests.cs`:**
- `GetReadiness_ProductionLikeDuplicateAndMismatchedActivityShape_Returns200WithStructuredChecks` —
  reproduces the reported production data shape (49 duplicate `PracticeGym` readiness items for one
  objective, a `speaking`-primary-skill objective linked to a `ListeningComprehension` activity via
  an unusual pattern key) and asserts 200 with structured checks, no leaked exception text.

## Local validation

- `git status` — clean before starting; diff limited to the 3 source files + 4 test files listed above.
- `git diff --check` — no whitespace errors.
- `dotnet build --configuration Debug` — succeeds, 0 errors (pre-existing warnings only).
- `dotnet test tests/LinguaCoach.UnitTests` — **1755/1755 passed**.
- `dotnet test tests/LinguaCoach.IntegrationTests` — **1381/1381 passed** (includes the new production-shape reproduction test).
- `dotnet test tests/LinguaCoach.ArchitectureTests` — **5/5 passed**.
- `cd src/LinguaCoach.Web && npm run build -- --configuration production` — succeeds (no Angular files touched this phase; pre-existing Sass selector warnings only, no errors).

Commit: `4dc49cc` — "Phase 20H — Fix readiness audit 500 edge case and Practice Gym duplicate suggestions".

## Deployment / live validation status

**Not yet pushed or deployed as of this writing.** Per this repo's operating guardrails, pushing to
`main` triggers the CI/CD deploy pipeline to production (`speakpath.app`) — a shared-state, hard-to-reverse
action — so it requires explicit user confirmation before proceeding, which had not yet been given
when this doc was written. Live validation against `https://speakpath.app` (readiness audit 200 for
`pilot.student.20e@speakpath.app`, deduped Practice Gym, dashboard/today/journey/progress/profile
still loading, no new 500s) is deferred until the push/deploy is authorized, and this doc/TODOS.md
should be updated with the live result once it runs.

## TODO status

- **`TODO-20G-1`** (Practice Gym duplicate suggestions): root cause identified and fixed at the
  data layer (replenishment dedup key), plus defense-in-depth at the suggestion-service layer.
  **Fix implemented and locally verified; marked RESOLVED pending live confirmation.**
- **`TODO-20G-3`** (readiness audit 500 for pilot student): exact production exception was not
  directly observed (no prod log/DB access in this session, same constraint as the original TODO),
  but the entire class of failure (unhandled exception from 4 of 10 check categories) is fixed, and
  a production-shape reproduction integration test passes. **Fix implemented and locally verified;
  marked RESOLVED pending live confirmation** — if the live 500 persists after deploy, the
  structured-Warning fallback still guarantees a 200 response, so the P0 severity is resolved even
  if a follow-up investigates the exact original exception.

## Risks / unresolved questions

- The exact Postgres exception that caused the original 500 was never directly observed; the fix
  is a structural guarantee (no check category can crash the whole audit) rather than a targeted
  fix for one confirmed root cause. This is intentional and matches the audit's own design
  contract, but means the *specific* underlying data anomaly (if any beyond volume/shape) is still
  undiagnosed.
- Live validation is outstanding — this review will need a follow-up append (or a new dated review)
  once deploy is authorized and live checks run.

## Final verdict (pending live validation)

**Locally: ready.** Both TODO-20G-1 and TODO-20G-3 have implemented, tested fixes with 0 test
regressions across 3141 backend tests. **Live: not yet confirmed** — do not invite a real pilot
student until the push/deploy/live-validation step below is completed and this doc is updated with
a live "yes."

## Next recommended action

Get explicit go-ahead to push `4dc49cc` to `main`, let the deploy pipeline ship it, then run the
Part C live validation checklist against `https://speakpath.app` with
`pilot.student.20e@speakpath.app` and update this doc + `TODOS.md` with the live result.
