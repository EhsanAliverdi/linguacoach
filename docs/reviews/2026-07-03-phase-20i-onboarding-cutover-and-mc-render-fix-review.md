# Phase 20I (continued) — Onboarding V2 Cutover, MC-Render-as-Text Fix

**Date:** 2026-07-03
**Related:** `docs/reviews/2026-07-03-phase-20i-full-live-student-admin-qa-data-audit-review.md` (initial audit), `docs/reviews/2026-06-17-phase-10i-configurable-onboarding-v2-engineering-review.md` (V2's original design), `docs/reviews/2026-06-26-phase-11a-admin-onboarding-builder-review.md` (V2's admin UI)
**Plan file:** approved via plan mode this session (`graceful-brewing-wind.md`)

## Trigger

Manual QA by the product owner on production surfaced a second, larger batch of findings beyond the initial Phase 20I audit: onboarding step ordering/content problems, a placement-content-quality gap, a suspected MC-rendering bug, and — most significantly — the discovery that a fully-built, admin-configurable onboarding system ("V2") already existed but was never wired up to real student traffic.

## Findings confirmed live (fresh QA account, `qa.phase20i.onboarding2@example.com`)

1. Onboarding step 1 (language) only offered "Persian to English" — the `language_pairs` table has exactly one row.
2. Step 3 (career/job context) was mandatory and appeared *before* the step that reveals whether the student's goal is work-related — wrong order, wrong requiredness.
3. Step 4's free-text placeholder hardcoded a Farsi example regardless of the student's language.
4. "Which area matters most" (step 4) was single-select via a border-color toggle-off-previous pattern — confirmed by CSS state inspection, not just visual impression.
5. Step 5 (work experience) had a skip button but was still shown unconditionally.
6. Placement "Listening" items had zero `<audio>` elements anywhere — confirmed at 4 different questions (Q1, Q7, Q13, Q19) — purely text pretending to be audio.
7. Placement "Reading" items were single sentences, not passages (confirmed at Q14).
8. A claimed "MC renders as free-text input" bug — could not be reproduced in the placement assessment (every MC item across 20 questions rendered correctly as buttons). Root-caused instead via code trace: real bug exists in `ActivityGetHandler`, but only on 4 code paths reachable through `GET /api/activity/next?type=...` (a query param the current live frontend's Practice Gym/Today flows don't use — confirmed via `getById()`/pattern-key routing being what's actually called — though the codebase's own integration tests do exercise `?type=` directly, so it's real, tested API surface, not dead code to ignore).
9. A "Dashboard is only available after onboarding is complete." banner appeared once — investigated and found to be **stale browser cache from this session's own long-running browse daemon**, not a live bug (confirmed absent after hard reload and in a fresh tab; the string doesn't exist anywhere in git history).
10. **The big one**: `translation_help_preference`, `support_language_code/name`, `difficulty_preference`, `focus_areas`, `learning_goals` were confirmed empty in the DB after a fresh student completed the full V1 onboarding + placement flow — fields the AI generation layer (`LearningGoalContextResolver`, `LanguageSupportResolver`) depend on.

## Root cause and decision

Traced (2)+(10) to the existence of two onboarding systems: V1 (live, hardcoded 5-step Angular flow) and V2 (fully built, admin-configurable, orphaned — never routed to since its Phase 10I build). V2 already correctly collects support language (8 options, not just Persian), learning goals, focus areas (already multi-select, closing finding 4), and difficulty preference via `StudentProfile.UpdateLearningPreferences`. The only real gap: V2's `OnboardingAnswerMapping` enum had no cases for career context, session duration, skill-focus text, or work experience — the admin UI could visually create steps for these, but answers would never reach `StudentProfile`.

Decision (confirmed with product owner): **retire V1 outright and cut every student, new and existing, onto V2** — not a phased/new-students-only rollout. Checked live: all 12 existing student accounts (including `pilot.student.20e`) already have `onboarding_status = 2` (Complete), so there was zero in-flight-student risk in an immediate cutover.

Product owner also asked whether placement should become similarly admin-configurable rather than a hardcoded item bank — confirmed yes as a follow-up phase (Phase 4/5 in the plan), not implemented this session; the static `PlacementItemTemplate` array was investigated and confirmed self-contained (no other code references it), making a future DB-backed conversion low-risk.

## Changes implemented this session

### Fix: MC-render-as-text-input (`ActivityGetHandler.cs`)
4 call sites built a `LearningActivity` without resolving `InteractionMode` (passed `null`), which the frontend's `exercise-renderer.component.ts` silently reads as free-text entry. Added `ResolveDefaultInteractionModeAsync` — looks up the `InteractionMode` of the exercise pattern that normally serves each `ActivityType` (for DTO purposes only, doesn't assign a pattern key to the activity). Test: `VocabularyPracticeActivityTests.GetNext_TypedVocabularyPractice_WithEnoughVocabItems_ReturnsVocabularyPractice` extended to assert `interactionMode` is non-null.

### Closed V2's onboarding answer-mapping gaps
- `OnboardingAnswerMapping`: added `CareerContext`, `SessionDuration`, `WorkExperience`, `LearningGoalDescription`.
- `StudentProfile`: added `UpdateOnboardingFreeTextContext(careerContextText, learningGoalDescription)` (partial-update, state-machine-free) and `MarkOnboardingComplete()` (see below). Reused existing state-machine-free `SetExperienceContext` and `UpdateLearningPreferences` (which already had an unused `preferredSessionDurationMinutes` parameter) rather than adding new methods for those.
- `OnboardingV2StepHandler`: added the 4 new mapping cases, plus a conditional-skip mechanism — after the `learning_goals` step, `career_context` and `work_experience` steps are skipped in the "advance to next step" computation unless the student selected the `"work"` goal option. Both steps seeded as `RequirementType.AdminConfigured` so they never block completion regardless of whether they're shown.
- `OnboardingFlowSeeder`: added 3 new steps (`session_duration`, `career_context`, `learning_goal_description`, `work_experience` — 4 actually) at orders 7-10, renumbering the existing assessment/summary steps to 11-15. `learning_goal_description` reuses the existing generic `FreeText` step type/component, which already had no hardcoded non-English placeholder and already has Skip/Continue buttons — closing finding (3) for free.
- New frontend components: `onboarding-v2-session-duration.component.ts` (mirrors the existing `difficulty` component pattern), `onboarding-v2-work-experience.component.ts` (two independent single-choice pickers in one step, with a Skip button that submits an empty object — validation relaxed to accept "both fields or neither").
- **Critical cross-cutting fix found during implementation**: `ActivityGetHandler`, `ActivitySubmitHandler`, `DashboardQueryHandler`, `GetProgressHandler`, `SpeakingSessionHandler`, `CefrAssessmentHandler`, and multiple readiness-pool jobs all gate on the legacy `StudentProfile.OnboardingStatus` field. `OnboardingV2CompleteHandler` never set it — a student who finished V2 onboarding would have been silently blocked from every one of those features. Added `StudentProfile.MarkOnboardingComplete()`, called from `OnboardingV2CompleteHandler` alongside the existing lifecycle-stage transition.
- Tests: new `OnboardingV2StepHandlerTests.cs` (6 tests) covering all 4 new mappings plus both branches of the conditional skip.

### Retired V1 onboarding
- Deleted `step1-language` through `step5-experience` Angular components (`git rm`, reversible via history) and their routes from `app.routes.ts`.
- `onboarding-resume.component.ts` simplified: always routes incomplete students to `/onboarding/v2` (previously mapped V1's 4-state step machine to 5 different routes).
- `login.component.ts` / `change-password.component.ts`: error-fallback routes updated from `/onboarding/step-1` to `/onboarding/v2`.
- Deleted the now-entirely-obsolete `e2e/onboarding-experience-step.spec.ts` (exclusively tested the deleted V1 step-5 route).
- Backend `OnboardingHandler.cs` (V1's step-submission handler) was **not** deleted — its `GET` status endpoint is still used by `getStatus()` calls in login/resume for a fast-path dashboard redirect, and is now correctly informed by `MarkOnboardingComplete()`. Its `POST` step-submission cases become unreachable from the live frontend but are left in place (harmless, still tested) rather than risking a wider blast radius this session.

## Deferred to follow-up (per approved plan, not implemented this session)

- **Phase 4**: admin-configurable placement item bank (`PlacementItemDefinition` entity + CRUD mirroring the onboarding-step admin pattern), replacing the static 72-item `PlacementItemTemplate` array and adding `ReadingPassage`/`ListeningAudioScript` fields.
- **Phase 5**: wiring real TTS audio into placement listening items via the already-built (but currently unused by the live adaptive engine) `PlacementAudioService`.
- **Phase 6**: live diagnosis of why Today-lesson TTS (`tts.listening`) has zero `ai_usage_logs` rows ever despite valid config/credentials — needed before any code fix.

## Validation

```
dotnet build --configuration Release          → 0 errors
dotnet test  --configuration Release --no-build
  LinguaCoach.ArchitectureTests   → 5/5 passed
  LinguaCoach.UnitTests           → 1757/1757 passed
  LinguaCoach.IntegrationTests    → 1395/1395 passed (includes 7 new/updated tests:
    1 in VocabularyPracticeActivityTests, 6 in new OnboardingV2StepHandlerTests)

npm run build -- --configuration production   → succeeded (pre-existing warnings only)
npx ng test --include onboarding*             → 3/3 passed
npm test (full Angular suite)                 → 1548/1668 passed, 120 failed
  — confirmed pre-existing on unmodified main (stashed this session's changes and
    re-ran: 118/134 fail in AdminStudentDetailComponent alone, same root cause
    `getStudentWritingEvaluations is not a function` / HttpClient injector errors
    unrelated to onboarding). No onboarding-related failures introduced.
```

Secret scan (`git diff | grep -iE "password|secret|api_key|connectionstring"`) and `git diff --check` both clean before commit.

## Not yet live-verified

This document was written before pushing/deploying this session's Phase 1-3 changes. Live verification (fresh student through the full V2 flow on `speakpath.app`, confirming preference fields populate and the student isn't blocked from Today/Practice/Dashboard) is required before this phase is considered done — see the follow-up entry in this same doc or a subsequent commit once deployed.
