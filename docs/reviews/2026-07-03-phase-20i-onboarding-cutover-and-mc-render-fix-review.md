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

## Live verification (2026-07-03) — 4 more real bugs found and fixed

Live verification with a fresh student was not a formality — it caught 4 additional real, previously-undiscovered bugs, each only reachable because this was **the first time V2 onboarding was ever driven end-to-end by a real student** since it was built in Phase 10I/11A. Each was fixed, redeployed, and re-verified before moving to the next:

1. **The seeder never re-ran against the already-seeded production flow.** `OnboardingFlowSeeder.SeedAsync` bailed out entirely (`if (existingActive) return`) once any active flow existed, so the 4 new steps from Phase 2 above were completely absent in production — students skipped straight from `difficulty_preference` to the assessment intro. Fixed by making the seeder reconcile: if its own "Default Flow" is missing steps `BuildDefaultSteps()` now defines, publish a new version (deactivate old, activate new) rather than mutate the active flow in place, matching the class's own documented immutability contract. Also added the 4 new step types/mappings to the admin builder's frontend dropdowns (`STEP_TYPES`/`ANSWER_MAPPINGS`), which hadn't been updated either — the admin UI couldn't have created these steps manually even as a workaround.

2. **Two onboarding step components leaked state into each other.** `career_context` then `learning_goal_description` (both `FreeText`) — Angular reused the same `OnboardingV2FreeTextComponent` instance since `*ngIf` only re-renders on a truthiness change, not a step-key change, so the second step's textarea inherited the first step's answer verbatim. `assessment_q1`/`assessment_q2` (both `AssessmentQuestion`) had the identical bug — Q1's selected answer key could carry over as a pre-selected, submittable answer to Q2. Fixed with `ngOnChanges` resets in `FreeTextComponent`, `AssessmentComponent`, and (defensively) `SingleChoiceComponent`.

3. **`CompletedStepKeys` never persisted, ever, for any student.** `StudentOnboardingProgress.RecordStepCompleted()` mutates a `List<string>` in place (`.Add(stepKey)`); the EF mapping used `HasConversion` with no `ValueComparer`, so the default reference-equality change tracker never saw a change on the same List instance — `completed_step_keys` stayed `[]` in the database regardless of how many steps a student completed. `/api/onboarding/complete` therefore always failed with "Required steps not completed" listing *every* step. This is the single most severe bug found this session: it made V2 onboarding **completely uncompletable, for every student, since the feature was built** — nobody had ever tried to finish it for real before this session. Fixed with an explicit `ValueComparer<List<string>>`; also hardened `StudentProfile.LearningGoals`/`FocusAreas` with the same comparer defensively (currently safe only because their setter reassigns via `.ToList()` rather than mutating in place).

4. **The summary step's completion button never recorded itself as completed.** After fixing #3, `/complete` failed with only one step missing: `summary`. `OnboardingV2SummaryComponent`'s "Start learning" button emitted `completed` directly to the parent, which called `triggerComplete()` without ever submitting the `summary` step itself (a `SystemRequired` step) through the normal step-submission flow. Fixed by submitting the current step before completing, mirroring every other step.

5. **Two enum-string mismatches meant two more fields were silently null even after #3 was fixed**, discovered via a DB check on a fully-completed profile: `support_language_code` and `difficulty_preference` were still empty. Root cause (two separate, unrelated bugs found together): (a) `StudentProfile.UpdateLearningPreferences` unconditionally overwrites `SupportLanguageCode`/`SupportLanguageName`/`TranslationHelpPreference`/`DifficultyPreference`/etc. on *every* call — only `LearningGoals`/`FocusAreas`/`PreferredSessionDurationMinutes` skip the update when null — so each subsequent onboarding step (which calls this method with the other fields left null) silently wiped out what an earlier step had just set; (b) the support-language step sent `translationHelp: 'WhenAsked'` and the difficulty step's seeded option key was `'Moderate'`, neither of which matches the actual enum member names (`TranslationHelpPreference.WhenDifficult`, `DifficultyPreference.Balanced`), so `Enum.TryParse` silently failed regardless of bug (a). Fixed (a) by adding `UpdatePreferencesPreservingOthers()` to `OnboardingV2StepHandler`, which reads the profile's current value for every field the calling step doesn't own, rather than changing `UpdateLearningPreferences` itself (which `/profile`'s full-form submission relies on overwriting everything). Fixed (b) by correcting the frontend string and the seeded option key (user-facing label unchanged). Broadened the seeder's reconciliation check from step-*keys* to step-*content* (key + options) so fix (b) actually reached the already-created production flow.

**Final confirmed-live result** (`qa.phase20i.finalcheck@example.com`, full flow including Spanish support language, "work" goal, career context, work experience, both assessment questions):

```
onboarding_status: 2 (Complete)
support_language_code: es          support_language_name: Spanish
translation_help_preference: 1 (WhenDifficult)
career_context: "Junior software engineer"
learning_goals: ["work"]           focus_areas: ["speaking"]
difficulty_preference: 1 (Balanced)
preferred_session_duration_minutes: 20
professional_experience_level: 3 (MidLevel_2_5Years)
role_familiarity: 3 (ExperiencedInRole)
lifecycle_stage: 4 (PlacementRequired)
```

Every field the AI generation layer depends on is now correctly captured, and the student lands cleanly on the dashboard with "Start placement" — no errors, no raw exceptions, no blocked features. Total commits this phase: `4972548`, `0419c4a`, `6b26a16`, `268c631`, `6e737f8`, `d6e6d75`.

## Local validation (final)

All 6 deploys re-ran the full backend suite before shipping; final count: `dotnet test --configuration Release` → 3161/3161 passed (ArchitectureTests 5, UnitTests 1757, IntegrationTests 1399, including 12 new/updated tests across this phase's 6 commits). `npm run build -- --configuration production` green on every deploy.
