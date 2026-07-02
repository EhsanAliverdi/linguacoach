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
- `GetSuggestions_SameObjectiveAndPatternDifferentMaterializedActivities_CollapseToOneSuggestedCard`
  (added after live validation surfaced the gap — see Deployment section)

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

Commits: `4dc49cc` (fix), `8d216fd` (docs), `80cb0eb` (follow-up dedupe-key fix after live
validation — see Deployment section below). All three re-ran the full local suite before push;
the `80cb0eb` follow-up re-confirmed 1756/1756 unit, 1381/1381 integration, 5/5 architecture tests
passing (one net-new unit test).

## Deployment / live validation status — CONFIRMED LIVE (2026-07-03)

Pushed and deployed in two steps, both via the existing CI/CD pipeline (`.github/workflows/deploy.yml`):

1. **`4dc49cc` + `8d216fd`** (code/tests + docs) deployed as GitHub Actions run `28621275227`
   (build+push images, deploy to VPS, post-deploy canary checks — all green).
2. Live-checked `GET /api/admin/students/{id}/readiness` for
   `pilot.student.20e@speakpath.app` (profile `c2a7caff-b46a-4da4-b424-8bd5ca8c0394`) →
   **200**, `readyForPilot: true`, `blockingIssueCount: 0`. Crucially, the response includes an
   `activities.check_failed` check (`status: warning`, `technicalDetail: "PostgresException"`) —
   this is live, direct confirmation that the exact class of failure this phase fixed (an
   unguarded exception inside one of the four wrapped check methods) was actually occurring for
   this student in production, and is now correctly degrading to a structured warning instead of
   a 500. **`TODO-20G-3` confirmed fixed live.**
3. Reset the pilot student's password via the existing admin repair-adjacent endpoint
   (`POST /api/admin/students/{id}/reset-password`) to log in as the student for live validation
   (no password was available from a prior session) — logged the change here per the docs
   persistence rule; no attempts/submissions/evaluations were touched.
4. Live-checked `GET /api/practice-gym/suggestions` as the pilot student — initially still showed
   6 cards all titled "Giving Structured Explanations" for one objective. Inspection showed each
   had a **distinct** `linkedLearningActivityId` (queued before the replenishment dedup-key fix
   caught up) — literal duplicate rows, not yet cleaned up by the fix, which only prevents new
   duplicates going forward. This is a real gap: `PracticeGymSuggestionService`'s original dedupe
   only collapsed same-activity-id or same-item-id duplicates, not "different materialized
   activity, same objective+pattern." **Follow-up fix (`80cb0eb`):** reprioritized the dedupe key
   to group by `(CurriculumObjectiveKey, PatternKey, ActivityType)` first — the fields that
   actually drive a card's visible title/CTA — falling back to activity id, then item id. Added
   test #30 reproducing the exact live shape. Redeployed as run `28622255816` (green).
5. Re-checked Practice Gym live after the follow-up deploy: the same 6 readiness items now show
   **6 distinct `patternKey`/`activityType` values** (`listening_multiple_choice_single`,
   `summarize_written_text`, `write_from_dictation`, `listen_and_answer`, `phrase_match`,
   `respond_to_situation`) — genuinely different exercises, no literal duplicate rows remaining.
   **`TODO-20G-1`'s literal duplicate-data bug confirmed fixed live.**
6. Re-checked readiness audit once more post-follow-up-deploy: still 200, same structured
   `activities.check_failed` warning, `readyForPilot: true` — stable across both deploys.
7. Live-checked `dashboard`, `sessions/today`, `student/learning-plan/journey`, `progress`,
   `profile` as the pilot student — all **200** on both deploys. No new 500s observed on any
   endpoint touched during this validation pass.

**Residual, out-of-scope observation:** the 6 Suggested cards above all still share one objective
("Giving Structured Explanations") — `Suggested` ranking (`RankSuggestions` in
`PracticeGymSuggestionService`) doesn't diversify across the student's other 3 Learning Plan
objectives, so a student can still see several same-titled-but-different-pattern cards in a row.
This is **not** the duplicate-data bug this phase targeted (confirmed: no two cards reference the
same pattern/type/activity anymore) — it's a pre-existing Suggested-ranking design limitation the
original `TODO-20G-1` text explicitly flagged as a separate, deeper question ("root cause and
correct fix require understanding the intended replenishment/selection design"), and Phase 20H's
own scope explicitly excludes "new activity types" / behavior changes beyond preventing
duplicate/invalid suggestions. Recommend tracking as a new lightweight follow-up TODO
(`TODO-20H-1`, suggested) rather than reopening this phase.

## TODO status

- **`TODO-20G-1`** (Practice Gym duplicate suggestions): **RESOLVED, confirmed live 2026-07-03.**
  Root cause fixed at the data layer (replenishment dedup key) plus defense-in-depth at the
  suggestion-service layer. Live validation initially found a residual gap (pre-existing duplicate
  rows queued before the fix, with distinct materialized activity ids the original dedupe missed);
  fixed in a same-day follow-up (`80cb0eb`) and reconfirmed live — the pilot student's Practice Gym
  now shows 6 genuinely distinct patterns/activity types, no literal duplicate rows.
- **`TODO-20G-3`** (readiness audit 500 for pilot student): **RESOLVED, confirmed live 2026-07-03.**
  Live response for the pilot student now includes an `activities.check_failed` structured warning
  with `technicalDetail: "PostgresException"` — direct confirmation the originally-reported
  exception is exactly the failure mode this phase fixed, and it now degrades to 200 with a
  structured check instead of a 500.

## Risks / unresolved questions

- The exact Postgres exception text/stack was still not directly read (no prod log/DB console
  access in this session) — but its *type* (`PostgresException`, surfacing from the
  `AddActivityContentChecksAsync` category specifically) is now confirmed live via the structured
  `technicalDetail` field, which matches the original hypothesis in `TODO-20G-3`.
- **New, out-of-scope observation:** Practice Gym's Suggested list can still show several cards for
  the same Learning Plan objective (now genuinely distinct patterns, not duplicate rows) because
  `RankSuggestions` doesn't diversify across objectives. Recommend opening a new lightweight
  `TODO-20H-1` to track this as a future ranking/diversity improvement — it is not a data-duplicate
  bug and is out of this stabilization phase's scope.

## Final verdict

**Ready to invite one real controlled pilot student: YES.** Both `TODO-20G-1` and `TODO-20G-3` are
fixed, tested, deployed, and confirmed live against `https://speakpath.app` with
`pilot.student.20e@speakpath.app` — readiness audit 200 (no 500s), Practice Gym free of literal
duplicate rows, and Dashboard/Today/Journey/Progress/Profile all loading. No AI scoring, CEFR
update, objective-completion, or Learning Plan regeneration behavior changed; no
attempts/submissions/evaluations were deleted or modified.

## Next recommended action

Optionally open `TODO-20H-1` for the Suggested-list objective-diversity observation above (not
blocking); otherwise proceed with inviting the pilot student.
