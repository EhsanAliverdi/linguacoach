---
status: current
lastUpdated: 2026-07-03
owner: engineering
supersedes:
supersededBy:
---

# Pilot-Student Bug Fixes — Implementation Summary

**Date:** 2026-07-03
**Related sprint/feature:** Follow-up implementation for
`docs/reviews/2026-07-03-pilot-student-onboarding-placement-practice-live-audit.md` and
`docs/reviews/2026-07-03-workplace-default-content-and-placement-gating-review.md`
**Plan file:** `abundant-munching-babbage.md` (approved by user before implementation)

## Files reviewed / modified

Backend: `PracticeGymSuggestionService.cs`, `ModuleStageContentValidator.cs` (+tests),
`PlacementAssessmentService.cs`, `ActivityGetHandler.cs`, `ActivitySubmitHandler.cs`,
`ActivityController.cs`, `SessionGeneratorService.cs`, `AiLearningPathGeneratorHandler.cs`,
`LearningPlannerService.cs`, `CefrAssessmentHandler.cs`, `LessonBatchGenerationJob.cs`,
`PracticeGymGenerationJob.cs`, `ActivityMaterializationJob.cs`, `SpeakingSessionHandler.cs`,
`PlacementService.cs`, `AiStructuredEvaluator.cs`, `AiOpenEndedEvaluator.cs`,
`PatternEvaluationContracts.cs`, `OnboardingV2QueryHandler.cs`, new
`LanguageSupportResolver.cs`. Frontend: none shipped (see Fix 8 below). Tests:
`SessionGeneratorServiceTests.cs`, `ModuleStageContentValidatorTests.cs`, new
`OnboardingV2QueryHandlerTests.cs`. New: `scripts/2026-07-03-audit-broken-activities-and-stray-placement-rows.sql`.

## What was fixed (shipped, all tests green)

**Fix 3 — Farsi/Persian hardcoded fallback (systemic, not just one activity).**
Found and removed `?? "Persian"` in 13+ call sites across the AI-generation pipeline, plus two
evaluators (`AiStructuredEvaluator`, `AiOpenEndedEvaluator`) that hardcoded `"Persian"`
unconditionally with no profile lookup at all. New `LanguageSupportResolver` centralizes the
correct logic: prefer the student's actual `SupportLanguageName` (when
`TranslationHelpPreference != Never`), fall back to the course `LanguagePair.SourceLanguage`,
and only as a last resort fall back to the target language name (i.e., no translation content
at all) — never a guessed foreign language. Threaded `SourceLanguageName`/`TargetLanguageName`
through `PatternEvaluationRequest` so the two evaluators that previously had zero access to
the student's profile now receive it from their callers (`ActivitySubmitHandler`,
`ActivityController`).

**Fix 4/5 — Placement status reconciliation.**
`PlacementAssessmentService.StartAssessmentAsync` now checks `profile.LifecycleStage` before
creating a new adaptive assessment row — if the student already passed placement (via any
flow), it returns the existing completed assessment (or a synthesized completed summary)
instead of silently creating an orphaned `InProgress` row. `GetLatestAssessmentAsync` now
prefers a `Completed` row over a later stray `InProgress` one. The Angular `/profile` page
required no changes — it already branches correctly on `status`; it was just receiving wrong
data.

**Fix 1 — Practice Gym card metadata mismatch.**
`PracticeGymSuggestionService.ToDto` now batch-loads the linked `LearningActivity` for
materialized items and prefers its real `Title`/`ActivityType`/`Difficulty` over the
pre-generation routing snapshot, so a card's displayed title/type/level always matches what
actually opens.

**Fix 2 — Broken empty-array activity content.**
Added `ValidateArrayFieldNotEmpty` to `ModuleStageContentValidator` — a required array field
(e.g. `pairs`, `items`) now fails validation even when `countSettings` is null, closing the gap
that let the original broken activity slip through. Data cleanup for already-existing broken
rows is a manual DB step — see the audit script.

**Fix 7 — Workplace-default content theming.**
`SessionGeneratorService.BuildSessionMetadata`, `AiLearningPathGeneratorHandler.GenerateAsync`,
and `LearningPlannerService.BuildLessonPlanAsync` all previously hardcoded
`"workplace professional"` / `"General workplace"` / `"Document Controller"` fallbacks instead
of consulting the existing (and already-correct) `LearningGoalContextResolver`, whose priority
chain already puts explicit Learning Goals ahead of the legacy career field. All three now use
the resolver. Also fixed the same class of bug in `CefrAssessmentHandler` (found during
this work). Regression test added asserting an explicit non-workplace goal wins over a
populated career field.

**Fix 6 — Onboarding preference fields (mandatory for new completions).**
The active v2 onboarding flow (`OnboardingFlowSeeder`) already marks `learning_goals`,
`focus_areas`, and `support_language` as `SystemRequired` steps — new students already can't
skip them. The actual gap was `OnboardingV2QueryHandler`'s lazy-backfill path, which blindly
marked any v1-legacy-complete student (like `pilot.student.20e`) as v2-complete without
checking whether these newer fields were ever answered. Fixed: the backfill now checks
`LearningGoals`/`FocusAreas`/`SupportLanguageCode`/`TranslationHelpPreference` and, if any are
missing, routes the student to the first missing step instead of marking them falsely
complete.

## What was attempted and reverted — Fix 8 (placement-stage content gating)

The plan called for gating `DashboardQueryHandler`/`ActivityGetHandler`/`ActivitySubmitHandler`
on `LifecycleStage >= PlacementCompleted`, in addition to the existing `OnboardingStatus`
check, per the user's stated preference ("before full placement, we should only have
activities that help identify level, not everything open").

This was implemented, then **reverted** after running the full test suite surfaced strong,
explicit evidence that the current system already has a different, deliberate design:

- `OnboardingEndpointTests.Dashboard_AfterOnboardingComplete_Returns200WithMessage` asserts the
  dashboard returns **200** with the message *"Complete your placement assessment to unlock
  your personalised course"* for a `PlacementRequired` student — a designed graceful
  degradation, not a hard block.
- `StudentDashboardSummaryTests` has multiple tests explicitly named
  `..._ForPlacementRequiredStudent` asserting 200 responses with degraded
  (`NotAvailable`/`PlacementRequired`) sub-states.
- The shared test factory `ActivityTestFactory.CreateOnboardedStudentAsync` — used by dozens of
  activity/vocabulary/pattern-evaluation tests — has always defaulted new students to
  `PlacementRequired`, and those tests assert successful (200) activity generation/submission,
  meaning full activity access working pre-placement is today's actual, tested, intentional
  behavior, not an overlooked gap.

Blocking on this signal would have silently broken a real, designed UX (a friendly
"finish placement" nudge on the dashboard) and a large swath of currently-correct behavior,
for a feature that the original plan itself flagged as needing a product design decision
("what does 'placement-only content' mean — reuse the placement flow, or a new content
subset?") before implementation, not just a policy toggle.

**Status: reverted, not shipped.** All backend gate code, the `DashboardController`/
`ActivityController` catch-clause additions, and the Angular `placementRequiredRedirectGuard`
addition on `/dashboard` were removed. Test factories were left as originally found. Full test
suite (backend 3149/3149, frontend 1551/1551 excluding a pre-existing unrelated 120-failure
baseline in `AdminStudentDetailComponent`) is green with zero regressions.

## Decisions made

- Fixes 1–7 and 6's backfill logic: implemented as planned, verified via automated tests.
- Fix 8: **not implemented this session.** Recommend treating it as a separate, explicitly
  product-scoped feature — the question of what "placement-only content" should look like
  (reuse the existing placement-assessment flow's own item bank vs. a new
  `LearningActivity` subset; whether the dashboard's existing graceful-degradation message
  should be replaced or kept alongside a stricter gate; how to migrate students already
  mid-flight) needs a real decision, not an assumption, given how much designed behavior
  depends on the current permissive state.

## Risks / unresolved questions

- The empty-array activity-content bug (Fix 2) and stray-placement-row bug (Fix 4) both still
  need the manual DB audit/cleanup in `scripts/2026-07-03-audit-broken-activities-and-stray-placement-rows.sql`
  run against production — this was not executed in this session (no direct prod DB access).
- Fix 6's mandatory-field enforcement only covers the lazy-backfill path for v1-legacy
  students; it does not retroactively fix any `StudentOnboardingProgress` row that was already
  created (incorrectly) as complete before this fix shipped. If `pilot.student.20e` already has
  such a row, it will need the same manual audit/correction.
- Fix 3's `LanguageSupportResolver` fallback chain still uses `LanguagePair.SourceLanguage` as a
  secondary signal before falling back to "no translation" — this preserves existing behavior
  for any student who does have a `LanguagePair` set, but that field's exact product meaning
  (vs. `SupportLanguageCode`) was not fully re-investigated; worth confirming with whoever owns
  onboarding whether `LanguagePair` should be deprecated in favor of `SupportLanguageCode`
  entirely.

## Final verdict

7 of 8 planned fixes shipped and verified (backend + frontend test suites green, zero
regressions). Fix 8 was correctly identified in the original plan as needing a product
decision — attempting it anyway surfaced that assumption was wrong, and it was reverted rather
than force a behavior change against the grain of the existing, tested product design.

## Next recommended action

1. Run the audit script against production, review results, and apply the commented-out
   `UPDATE`s manually (or hand off to whoever has prod DB access).
2. Decide Fix 8's actual design (see "Decisions made" above) as its own scoped follow-up, with
   the dashboard's existing graceful-degradation UX taken as a hard constraint, not something to
   silently replace.
3. Deploy fixes 1, 2, 3, 4/5, 6, 7 and live-validate against `pilot.student.20e@speakpath.app`
   using the same gstack `browse` repro steps from the original audit.
