---
status: current
lastUpdated: 2026-07-03
owner: engineering
supersedes:
supersededBy:
---

> **Implementation status:** the workplace-default content fix (Finding 1) shipped the same
> day. The placement-stage gating fix (Finding 2) was implemented then reverted after test
> evidence showed it conflicts with existing, deliberate dashboard/activity behavior — see
> `docs/reviews/2026-07-03-pilot-student-fixes-implementation-summary.md` for details and next
> steps.

# Workplace-Default Content Theming and Missing Placement-Stage Content Gating

**Date:** 2026-07-03
**Related sprint/feature:** Follow-up to `docs/reviews/2026-07-03-pilot-student-onboarding-placement-practice-live-audit.md`
**Trigger:** User observation, discussing the live audit above: (1) full Practice Gym/lesson catalog should not be open before placement is fully calibrated — only level-identifying activities should be; (2) all generated content defaults to "workplace" theming even though the student never selected that goal.

## Files reviewed

- `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs`
- `src/LinguaCoach.Infrastructure/LearningPath/AiLearningPathGeneratorHandler.cs`
- `src/LinguaCoach.Infrastructure/LearningPlan/LearningPlannerService.cs`
- `src/LinguaCoach.Infrastructure/Learning/LearningGoalContextResolver.cs`
- `src/LinguaCoach.Web/src/app/features/student/onboarding/**/step3-career.component.ts`
- `src/LinguaCoach.Domain/Entities/LearningActivity.cs`, `ActivityAttempt.cs`
- `src/LinguaCoach.Domain/Enums/PlacementStatus.cs`
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`, `ActivitySubmitHandler.cs`
- `src/LinguaCoach.Infrastructure/Speaking/SpeakingSessionHandler.cs`
- `src/LinguaCoach.Infrastructure/Progress/GetProgressHandler.cs`
- `src/LinguaCoach.Infrastructure/Dashboard/DashboardQueryHandler.cs`
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymBufferRefillJob.cs`
- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs`
- `src/LinguaCoach.Infrastructure/Placement/PlacementService.cs`, `PlacementAssessmentService.cs`

## Findings

### Finding 1 — "Workplace" theming is a scattered hardcoded fallback, not driven by student goals

A correctly-designed component already exists to avoid this: `LearningGoalContextResolver.cs` (comment at line 17: "never workplace-only"), with a generic `"general English communication"` fallback (lines 110-120). It's wired into most exercise-pattern-selection call sites (`SessionGeneratorService`, `ActivityGetHandler`, `ActivitySubmitHandler`, `ExercisePrepareHandler`, `ReadinessPoolReplenishmentService`, `CurriculumObjectiveWriteService`, `PracticeGymGenerationJob`, `ActivityMaterializationJob`, `LessonBatchGenerationJob`).

But the **session/lesson title, topic, and goal text shown to the student** — the part that says "Crafting Clear Workplace Emails," "Junior software engineer," "workplace professional context" — is built by separate code that never calls the resolver and instead hardcodes workplace-flavored fallbacks whenever the optional `CareerProfile` FK is null:

- `SessionGeneratorService.cs:375` — `focusSkill ?? "workplace communication"`
- `SessionGeneratorService.cs:377` — `moduleTopic ?? "Professional workplace communication"`
- `SessionGeneratorService.cs:378` — `careerContext = profile.CareerProfile?.Name ?? profile.CareerContext ?? "workplace professional"`
- `AiLearningPathGeneratorHandler.cs:63` — `careerContext = profile.CareerProfile?.Name ?? "General workplace"` (fed straight into the AI prompt at line 110, and into `DefaultPathFactory.Create` at line 82) — **never consults `LearningGoals`/`FocusAreas` at all**
- `LearningPlannerService.cs:151` (legacy planner) — `CareerContext: profile.CareerProfile?.Name ?? "Document Controller"`

Additionally, even where the resolver *is* wired in correctly, its own priority chain (lines 89-108) checks the free-text `CareerContext` field — populated by the onboarding "Step 3: Career" screen, whose suggestion chips include `"Junior software engineer"` — as a legacy signal *before* falling through to the generic fallback. Since the career/job field is filled in during onboarding for essentially every student, the goals-empty fallback path (the one designed to avoid workplace bias) rarely triggers even in the code paths that have it.

**Net effect:** a student who selects no Learning Goal (or explicitly picks "Day-to-day English") still gets "workplace professional" themed lessons, because (a) two separate content-generation code paths hardcode workplace-flavored literals independent of goals, and (b) the one correctly-designed anti-workplace-bias fallback is preempted by the onboarding career field on the paths where it does apply.

### Finding 2 — No placement-stage content restriction exists; full catalog opens on `OnboardingStatus.Complete` alone

There is no concept in the domain model of a "placement-calibration activity" distinct from a "full curriculum activity." `LearningActivity` (`src/LinguaCoach.Domain/Entities/LearningActivity.cs`) carries only `ActivityType`, `Source` (`AiGenerated`/`SystemFallback`), `ExercisePatternKey`, `IsActive` — nothing marking an activity as placement-only. `ActivityAttempt` carries no equivalent flag either.

Every access-gating check found across the codebase is a flat `OnboardingStatus == Complete` test, with no reference to placement completion or level-confidence at all:

- `ActivityGetHandler.cs:90`, `ActivitySubmitHandler.cs:99`
- `SpeakingSessionHandler.cs:44`
- `GetProgressHandler.cs:29`
- `DashboardQueryHandler.cs:36`
- `PracticeGymBufferRefillJob.cs:54`
- `ReadinessPoolReplenishmentService.cs:223,279`

`PlacementStatus` and `PlacementAssessment` (`src/LinguaCoach.Infrastructure/Placement/PlacementService.cs`, `PlacementAssessmentService.cs`) govern only the placement flow itself — determining `CefrLevel` — and are never cross-checked by any of the activity/Practice Gym access-control code above.

**Net effect:** once `OnboardingStatus.Complete` is true, the entire Practice Gym catalog (Listening, Reading, Writing, Speaking, Vocabulary — all types, all patterns) becomes available immediately, regardless of whether placement has produced a reliable level yet. This matches what the live audit observed: a student mid-placement (per `/profile`'s adaptive-assessment status) already sees a full "Suggested for you" Practice Gym list and a generated Today's Lesson.

## Decisions made

None yet. Findings only, presented for the user to decide direction on both:

1. Whether/how to restrict students to level-identifying activities only until placement is confirmed reliable, and what "placement-identifying activity" should mean in the domain model (a new flag on `LearningActivity`/`ExercisePatternKey` category, a separate placement-only activity pool, or gating by `PlacementStatus` in the existing handlers).
2. Whether to consolidate all workplace-fallback literals to route through `LearningGoalContextResolver`'s existing non-workplace-biased fallback, and whether the onboarding career field should still preempt the goals-based resolution, or only apply when the student has an actual `CareerProfile`/explicit workplace-related goal selected.

## Risks / unresolved questions

- Changing placement gating to restrict full-catalog access is a behavior change affecting every student, not just this pilot account — needs scoping for existing students already past placement.
- The onboarding career-field precedence in `LearningGoalContextResolver` may be intentional (e.g., "students usually want workplace English because they filled in a job") — worth confirming intent with product before changing precedence order, since removing it could regress relevance for the (likely majority) of students who do want workplace content.
- No test coverage currently asserts "no workplace theming when goals are empty and career field absent" — any fix here should add regression tests at `SessionGeneratorService` and `AiLearningPathGeneratorHandler` fallback branches specifically.

## Final verdict

Both are real gaps, not working-as-intended:
- Workplace theming is a fallback-literal problem in exactly two files (`SessionGeneratorService.cs`, `AiLearningPathGeneratorHandler.cs`) plus one legacy planner, not a systemic AI-prompt issue — a fix is scoped and findable.
- Placement-stage content gating does not exist at all today; adding it is a new gating feature, not a bug fix, and needs a design decision on what "placement-only" activities look like before implementation.

## Next recommended action

Discuss with the user:
1. Decide what a "placement-only" activity subset means (existing pattern types marked as placement-calibration vs. a dedicated new pool) before scoping the gating change.
2. Decide whether the onboarding career-field fallback precedence should change, and confirm this won't regress relevance for students who *do* want workplace content.
3. Once directions are set, scope as separate implementation tasks (content-domain-default fix vs. placement-gating feature) rather than one combined change, since they touch different subsystems and have different regression-test needs.
