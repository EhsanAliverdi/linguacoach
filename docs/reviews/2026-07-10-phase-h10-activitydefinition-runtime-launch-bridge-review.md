---
title: Phase H10 — ActivityDefinition Runtime Launch Path / Attempt Bridge — Implementation Review
date: 2026-07-10
related: H-track (docs/architecture/product-model-realignment-h0.md), Phase H7 (docs/reviews/2026-07-09-phase-h7-practice-gym-module-pipeline-review.md), Plan-Sync-After-H7 (docs/reviews/2026-07-09-plan-sync-after-h7-legacy-bank-removal-strategy.md)
status: complete
---

# Phase H10 — ActivityDefinition Runtime Launch Path / Attempt Bridge — Implementation Review

**Date:** 2026-07-10
**Related sprint/feature:** Phase H10, part of the H-track. Gives an approved `ActivityDefinition`
(H4) its first real launch/attempt/scoring path — reached only through an approved
`ModuleDefinition` (H5) suggestion in Practice Gym (H7) — without removing or replacing
`ActivityTemplate`, `PracticeActivityCache`, or any existing runtime. H9 (destructive legacy
cleanup) is intentionally still not started; this phase exists specifically so H9 will one day
have less to remove, not to remove anything itself.

## Mandatory Step -1 — clean starting point

```
git log --oneline --decorate -n 12   → HEAD is 4c89c499 (Phase H8), as expected
git status --short                   → clean, no output
```

## Step 0 — runtime launch/scoring audit

Full research is summarized here; see the H10 planning research for the complete trace.

1. **The only currently-working scored path is `ActivityTemplate`'s Form.io pilot.**
   `PracticeGymGenerationJob.TryMaterializeFromTemplateAsync` personalizes an approved
   `ActivityTemplate`, then calls `LearningActivity.SetFormIoContent(schema, scoringRules)` — this
   is the **entire** mechanism that makes an activity scoreable; there is no separate "Form.io
   activity" entity. `PracticeGymSuggestionService.StartSuggestionAsync` reserves a readiness item
   and returns a `LearningActivityId`; the client navigates to `/activity?activityId=...`;
   `ExerciseRendererComponent` renders `FormioRendererComponent` when `FormIoSchemaJson` is set;
   submission posts to the existing `POST api/activity/{activityId}/attempt` →
   `ActivitySubmitHandler.HandlePatternEvaluationAsync` (routed there because the activity has a
   non-empty `ExercisePatternKey`) → content-driven `isFormIoScored` check (true whenever
   `FormIoSchemaJson` is set, regardless of the pattern's own default marking mode) →
   `FormIoPatternEvaluator` → `IPlacementScoringService.ScoreSubmission` →
   `ComponentAnswerScorer.Score` per Form.io component → `ActivityAttempt` persisted →
   `IStudentLearningLedger.RecordAsync` called unconditionally.
2. **Student-safe DTOs:** `PracticeGymModuleActivitySummary` (H7) already sends
   `ActivityDefinitionId`/`FormSchemaJson`/`Instructions` to the client for display — and already
   deliberately omits `AnswerKeyJson`/`ScoringRulesJson` per its own doc comment. The activity page
   itself fetches `LearningActivity`'s own student-safe DTO separately once launched — no new
   client-facing DTO shape was needed for rendering.
3. **Answer keys/scoring rules stay server-side** in `ActivityDefinition.AnswerKeyJson`/
   `ScoringRulesJson` (backend-only per H4's own doc comments) and, once materialized, in
   `LearningActivity.ScoringRulesJson` (same "backend-only, never returned to students" contract,
   confirmed by reading the entity's own doc comment).
4. **`ActivityDefinition` can be rendered directly with the existing Form.io renderer**:
   `FormioRendererComponent` requires only a parsed schema object — it has zero references to
   `LearningActivity`/`ActivityTemplate`/`ActivityDefinition` and is already shared across
   onboarding, placement, and the Practice Gym pilot.
5. **`ActivityDefinition.ScoringRulesJson` can be scored directly by the existing scoring types —
   confirmed by reading the actual serialization code, not just doc comments.**
   `ActivityGenerationService` (H4) populates it via
   `JsonSerializer.Serialize(new ScoringRulesDocument(new Dictionary<string, ComponentScoringRule>
   { ["answer"] = new(ScoringRuleKinds.TextNormalized, CorrectAnswer: term, Points: 1.0) }))` for
   `gap_fill`, and the equivalent `SingleChoice` shape for `multiple_choice_single` — byte-for-byte
   the same shape `ComponentAnswerScorer`/`PlacementScoringService` already consume in production
   (placement, onboarding, and the `ActivityTemplate` Form.io pilot itself). No adapter needed.
6. **Bridging into the existing runtime (`LearningActivity`) is unambiguously safer than a new
   parallel attempt table.** `ActivityAttempt.LearningActivityId` is a required, non-nullable,
   constructor-enforced `Guid` — there is no path to reuse `ActivityAttempt` without a real
   `LearningActivity` row, and inventing a second attempt entity would mean reimplementing the
   ledger write, multi-skill progress update, memory update, readiness/usage-log bookkeeping, and
   `ActivitySubmitHandler`'s entire dispatch logic a second time — all of which today live
   entirely inside that one handler and know nothing about any non-`LearningActivity`-rooted
   attempt.
7. **Chosen option: a hybrid bridge (Option C, executed as Option B for the launch itself).** See
   Decision 1 below.

## Chosen option: Option C (hybrid bridge), landing squarely on Option B's mechanism

The phase brief offered Option A (native `ActivityDefinition` attempt runtime), Option B (bridge
into the existing runtime), and Option C (hybrid — bridge now, native runtime later, kept open).
**H10 implements Option B's actual bridge mechanism, framed as Option C's sequencing**: the launch
path materializes an eligible `ActivityDefinition` into a real `LearningActivity` via
`SetFormIoContent` — exactly what `ActivityTemplate` already does — so submission, scoring, the
ledger, multi-skill progress, and admin history all work through the **existing, unmodified**
`ActivitySubmitHandler` pipeline. Nothing about this choice forecloses a future native
`ActivityDefinition` attempt runtime (Option A) if that's ever justified — the bridge is
additive and the door stays open, per Option C's own framing.

**Why not Option A:** the research found no safety or product reason to duplicate an entire
scoring/ledger/progress pipeline that already exists and is already proven — only risk. **Why not
"pure" Option B with no bridge table:** a bare materialize-and-forget approach would lose the
answer to "which `ModuleDefinition`/`ActivityDefinition` did this `LearningActivity` come from,"
which the phase brief explicitly required ("Traceability from runtime launch/attempt/bridge back
to `ModuleDefinition` and `ActivityDefinition`") — hence the new, minimal
`StudentActivityDefinitionLaunch` bridge/traceability table.

## What was built

1. **Domain:** `StudentActivityDefinitionLaunch` (traceability bookkeeping — `StudentId`,
   `ModuleDefinitionId`, `ActivityDefinitionId`, nullable `LearnItemId`, `LearningActivityId`,
   `Source` (`ActivityDefinitionLaunchSource`: `PracticeGym`/`DailyLesson`/`AdminPreview`),
   `LaunchedAt`). Not a student attempt/score record itself — scoring happens entirely through the
   existing `LearningActivity`/`ActivityAttempt` pipeline; this table only remembers provenance.
2. **Persistence:** `StudentActivityDefinitionLaunchConfiguration` (snake_case table
   `student_activity_definition_launches`, `DeleteBehavior.Restrict` FKs to `ModuleDefinition`,
   `ActivityDefinition`, `LearnItem`, and — new for this phase — `LearningActivity` itself, plus a
   unique index on `LearningActivityId` since each materialized `LearningActivity` has exactly one
   launch record). Migration `Phase_H10_AddActivityDefinitionLaunchBridge` — **additive-only**,
   one new table, no change to any existing table (including `LearningActivity`/`ActivityAttempt`,
   whose schemas are completely untouched).
3. **Application:** `ActivityDefinitionLaunchEligibility` — a pure, static, exception-safe
   eligibility check shared by both the selector (to precompute `CanLaunch` on suggestions) and
   the launch service (to re-validate fresh at click time, since approval/content can change
   between suggestion and click). Checks: `ModuleDefinition`/`ActivityDefinition` both `Approved`;
   `RendererType == Formio`; `FormSchemaJson` present and valid JSON; `ActivityType` in the
   supported set (`gap_fill`, `multiple_choice_single`); `ScoringRulesJson` present, valid, and no
   component flagged `RequiresManualOrAiEvaluation`. Fails closed (not eligible) on any malformed
   or missing data — never throws. `IActivityDefinitionLaunchService.LaunchAsync` and its
   request/result records; `PracticeGymModuleSuggestion` (H7) extended with additive, optional
   `CanLaunch`/`UnsupportedReason` fields (trailing positional parameters with defaults — every
   existing named-argument construction site is unaffected).
4. **Infrastructure:** `ActivityDefinitionLaunchService.LaunchAsync` — loads the `ModuleDefinition`
   (must be `Approved`), walks its `ModuleDefinitionActivityLink`s in `SortOrder`, picks the first
   `Approved` + eligible `ActivityDefinition`; if found, materializes a new `LearningActivity`
   (`ActivitySource.SystemGenerated`, `ExercisePatternKey = ExercisePatternKey.
   FormIoPracticeGymPilot` — the same shared, already-seeded generic marker pattern
   `ActivityTemplate`'s pilot uses, chosen specifically because the evaluation dispatch is
   content-driven, not pattern-driven, so any pattern key that exists in the DB works identically)
   and calls `SetFormIoContent(FormSchemaJson, ScoringRulesJson)`; saves both the `LearningActivity`
   and a `StudentActivityDefinitionLaunch` bridge row. Never throws — wrapped in a top-level
   try/catch returning a safe `Success = false` result, matching every H6/H7 selector's
   convention. `PracticeGymModuleSelectionService` (H7) extended to compute `CanLaunch`/
   `UnsupportedReason` per suggestion using the same shared eligibility helper — no extra network
   round trip needed for the student UI to know whether Start is available.
5. **API:** `POST api/practice-gym/module-suggestions/{moduleDefinitionId}/start` on the existing
   `PracticeGymSuggestionsController` (matches its existing `[Authorize]`/`GetCurrentUserId()`
   convention exactly). Resolves the caller's own `StudentProfile` server-side from the JWT — a
   student can never launch on another student's behalf, and a caller with no student profile
   (e.g. an admin token) gets 401. Always returns 200 with `Success`/`UnsupportedReason` for every
   non-launchable case, matching `StartSuggestionResult`'s existing non-throw convention — the
   existing suggestions above remain the fallback either way.
6. **Angular:** `PracticeGymModuleSuggestion`/`ModuleSuggestionStartResult` extended/added in
   `practice-gym-suggestions.service.ts`; `startModuleSuggestion()` posts to the new endpoint.
   The student Practice Gym page's H7 "Recommended module practice" section now shows a real
   **Start** button (styled identically to the existing readiness-pool suggestion cards' start
   button) when `canLaunch` is true, and a clear, student-safe "not launchable yet" label
   (`unsupportedReason`) otherwise — no more blanket "Coming soon." On success it navigates to
   `/activity?activityId=...`, reusing the **entire existing, unmodified** activity page,
   Form.io renderer, and submission flow. Admin: `AdminPracticeGymSuggestedModule` extended with
   `canLaunch`/`unsupportedReason` (the backend already emits these — no controller change was
   needed, since the admin preview endpoint returns the raw selection result), surfaced as a small
   "Launchable"/reason badge per suggestion on the existing admin student-detail diagnostic card.
7. **Tests:** 16 new unit tests (`ActivityDefinitionLaunchServiceTests`, SQLite in-memory) + 2 new
   regression tests added to H7's own `PracticeGymModuleSelectionServiceTests` (confirming
   `CanLaunch` is correctly `false`/`true` for unsupported/eligible fixtures) + 12 new integration
   tests (`ActivityDefinitionLaunchEndpointTests`, plain `ApiTestFactory`). Full backend suite:
   3,925 tests (2,352 unit + 1,568 integration + 5 architecture), 0 failures.
8. **Docs:** this review, plus road-map.md, current-sprint.md, current-product-state.md,
   product-backlog.md, product-model-realignment-h0.md, learning-activity-engine.md,
   english-resource-bank-import-platform.md, architecture/README.md, TODOS.md.

## Decisions made

1. **Option C framing, Option B mechanism** — covered above.
2. **Supported activity types: `gap_fill` and `multiple_choice_single` only, exactly as the phase
   brief specified.** `short_answer` is explicitly generated by H4 with
   `RequiresManualOrAiEvaluation = true` — `ActivityDefinitionLaunchEligibility` checks this flag
   directly (not just the type name), so even a future `short_answer` variant that somehow got
   deterministic scoring rules would still correctly launch, and conversely a `gap_fill`/
   `multiple_choice_single` activity with malformed or missing scoring rules correctly fails
   closed rather than launching something unscoreable.
3. **Reused the existing `FormIoPracticeGymPilot` exercise pattern key rather than inventing a new
   one.** This pattern is already seeded (`ExercisePatternSeeder`), already exists purely as a
   generic "this activity is Form.io-scored" marker (its own teaching-purpose doc comment says as
   much), and reusing it means zero new seed data, zero migration risk, and — critically — it
   guarantees `ActivitySubmitHandler.HandlePatternEvaluationAsync`'s content-driven `isFormIoScored`
   dispatch fires correctly, since that check only requires `ExercisePatternKey` to be non-empty
   and `FormIoSchemaJson` to be set; the pattern's own nominal `MarkingMode`/`PrimarySkill` are
   overridden or only used for best-effort multi-skill-progress labeling, exactly as they already
   are for `ActivityTemplate`-sourced activities of any real skill/topic.
4. **`ActivityType` (the coarse Domain enum, not `ActivityDefinition.ActivityType` the free-text
   string) is mapped with a simple skill-based heuristic** (`VocabularyPractice` by default,
   `ReadingTask` when the Activity Definition's skill mentions "reading") — this enum predates H4
   and has no `gap_fill`/`multiple_choice_single` member; it is a coarse reporting category only
   (Form.io rendering is driven entirely by `FormIoSchemaJson`, never by this enum), and the
   existing `ActivityTemplate` pilot already accepts exactly this level of imprecision for its own
   shared marker-pattern activities.
5. **Today (H6) launch integration deferred — `TODO-H10-2`.** The phase brief explicitly allowed
   this ("Daily Lesson module launch is optional in H10... If not, leave Today display-only").
   `IActivityDefinitionLaunchService`/`ActivityDefinitionLaunchSource.DailyLesson` are already
   source-agnostic and ready for this — wiring a Start action into the Today module card is a
   small, isolated follow-up, not attempted this phase to keep scope focused on Practice Gym.
6. **Native `ActivityDefinition` attempt runtime (Option A) deferred — `TODO-H10-3`.** Tracked
   explicitly so a future phase can revisit whether the bridge remains sufficient once real usage
   data exists, or whether decoupling from `LearningActivity` becomes worth the cost the Step 0
   audit identified.

## Tests added (30 total: 16 + 2 regression + 12)

**Unit (`ActivityDefinitionLaunchServiceTests`):** launch allowed for approved module + approved
supported activity; rejected for pending module; rejected for rejected module; rejected for
pending Activity Definition; rejected for unsupported renderer type; rejected for invalid Form.io
schema; rejected for unsupported activity type; rejected for manual/AI-evaluated activity; result
does not expose answer keys; result does not expose scoring rules; preserves `ModuleDefinitionId`
traceability; preserves `ActivityDefinitionId` traceability; scopes the bridge row to the
launching student (two students launching the same module each get their own row); launch failure
does not delete Practice Gym cache records; launch failure does not mutate `ModuleDefinition`;
launch failure does not mutate `ActivityDefinition`.

**Regression (added to H7's `PracticeGymModuleSelectionServiceTests`):** a suggestion built from
this file's own (deliberately non-production-shaped) scoring fixture correctly reports
`CanLaunch = false`; a suggestion built from a properly-shaped, launch-eligible Activity
Definition correctly reports `CanLaunch = true`.

**Integration (`ActivityDefinitionLaunchEndpointTests`):** start succeeds for a launchable module
suggestion; start returns an unsupported state (200, `Success = false`) for a non-launchable one;
start requires authentication (401 with no token); start rejects a caller with no student profile
(admin token → 401); start response does not expose answer keys; start response does not expose
scoring rules; existing Practice Gym suggestions endpoint still works; existing
`activity/practice-gym/next` fallback endpoint still works; H5 Modules admin endpoint still works;
H6 Today endpoint still works (route/DI regression, no 500); H7 Practice Gym module suggestions
still work (including the new `moduleSuggestions` section); admin endpoints remain protected
(403 for a non-admin token).

## Risks or unresolved questions

- Today's module card remains display-only (`TODO-H10-2`) — a student who sees a Daily Lesson
  module suggestion still cannot start it from Today, only from Practice Gym.
- The `ActivityType` skill-based mapping (Decision 4) is a deliberate simplification, not a
  precise classification — acceptable because it only affects reporting/multi-skill-progress
  labeling, never rendering or scoring, and mirrors the existing pilot's own imprecision.
- No native `ActivityDefinition` attempt runtime exists yet (`TODO-H10-3`) — every launched
  activity's attempt history lives in `ActivityAttempt` keyed by `LearningActivityId`, discoverable
  back to its `ModuleDefinition`/`ActivityDefinition` only via the new bridge table, not via a
  direct FK on `ActivityAttempt` itself.
- The pre-existing Karma test-bundle compile failure (`TODO-H8-2`) still blocks the full frontend
  unit-test suite — re-confirmed this phase via `npm test -- --watch=false --browsers=ChromeHeadless`;
  the exact same six spec files fail with the exact same errors as documented in H8's review, and
  `git status --short` confirms none of them were touched by H10. The production build (which
  doesn't compile specs) was used as the primary frontend validation gate instead.

## Final verdict

**Complete.** A first, real runtime launch path now exists for approved `ActivityDefinition`
records reached through approved `ModuleDefinition` suggestions in Practice Gym: eligible
suggestions show a real Start button, launch materializes a genuine, traceable, scoreable
`LearningActivity` via the exact mechanism `ActivityTemplate`'s pilot already uses, and submission/
scoring/feedback flow through the completely unmodified existing pipeline — no new scoring code,
no new attempt entity, no new ledger-writing code. Unsupported activity types
(`short_answer`, manual/AI-evaluated, unsupported renderer/schema) fail gracefully with a clear,
student-safe, non-500 response and the existing Practice Gym experience is otherwise unaffected.
Traceability to both `ModuleDefinition` and `ActivityDefinition` (and, where available,
`LearnItem`) is preserved via the new bridge table. `ActivityTemplate`, `PracticeActivityCache`,
`StudentActivityReadinessItem`, and the full `LearningActivity`/`LearningSession`/`SessionExercise`
runtime are all completely untouched. No H9 destructive cleanup, no PG-v2, no full Practice Gym
redesign, no learner mastery updates from Modules, no physical `ResourceBankItem` consolidation.
Backend suite (3,925 tests) and Angular production build both pass (only the pre-existing
bundle-size warning); committed locally, not pushed, not deployed.

## Next recommended action

**`TODO-H10-2`** (wire a Today module-card Start action, reusing the now-source-agnostic launch
service) and **`TODO-H10-3`** (revisit whether a native `ActivityDefinition` attempt runtime is
worth building once real usage data exists) are both small, independent follow-ups. Separately,
**H9 — Legacy Bank Structure Removal and Consolidation** can now begin its own fresh safety audit
with one fewer open question: `ActivityDefinition` finally has a real (if still Practice-Gym-only)
launch path, so `ActivityTemplate` is one step closer to being provably safe to eventually retire
— though H9 must still re-verify this directly rather than assuming it from this document.
