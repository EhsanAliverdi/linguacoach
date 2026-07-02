# Phase 20G — Live Student Pilot Golden Path Completion — Review

- **Date:** 2026-07-02
- **Sprint/feature:** Phase 20G, follows Phase 20F (`docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`)
- **HEAD at start:** `6414f39`
- **Deployed URL tested:** `https://speakpath.app` (live production)
- **Pilot student used:** `pilot.student.20e@speakpath.app` (`c2a7caff-b46a-4da4-b424-8bd5ca8c0394`), the existing Phase 20E pilot student. Password was already known from Phase 20E (no reset needed). No new student created.

## Core question

*"Can one real student now use the deployed app end-to-end without developer hand-holding?"*

**Answer: mostly yes.** Placement, dashboard, one full activity (with a real bug fixed live), feedback, Practice Gym, Journey, Progress, and Profile all work end-to-end in production. One admin-only regression remains open (readiness audit 500s for this specific student's data) — it does not block the student experience, but it does block the admin's ability to re-verify via the Phase 20D tool for this one student.

## Admin login and readiness-before

Logged into `/admin` with the production admin account. Admin Student Detail for the pilot student showed:

- Lifecycle: **Placement in progress**
- Pilot readiness: **Needs attention** — 0 blocking, 2 warning, 4 info
- Warnings were both expected/correct for this stage: "Placement is required but not yet completed", "No CEFR level set yet"
- No repair action was recommended or needed at this point (`recommendedActions: []`)

No dry-run or real repair was necessary before the walkthrough.

## Placement result

**Completed in full**, live, in one continuous run (resumed from the in-progress assessment created during Phase 20F's live validation):

- Logged in as the pilot student in a separate browser tab (clean session).
- Dashboard correctly showed "Continue your placement assessment" → clicked through → adaptive engine correctly **resumed the same assessment** (`assessmentId=9945e76e-...`), did not start a duplicate.
- Answered all 19 adaptive items (mix of multiple-choice and gap-fill, across listening/reading/writing/vocabulary/grammar/speaking).
- **Assessment completed**: "Your level has been set" → **CEFR B2**, full 6-skill breakdown shown, "Your personalised course is ready."
- No 500s during placement. One pre-existing, unrelated 404 observed (`/api/api/admin/generation-quality/summary` — a known double-`/api/` typo in an admin dashboard widget, not part of the student flow).

## CourseReady / Learning Plan result

- Lifecycle correctly advanced through the sequence and reached **CourseReady** after placement completed.
- A real Learning Plan was generated automatically: 5 objectives, current objective "Speaking — B2."
- Admin Student Detail (checked later) confirmed: Learning Ready: Yes, Learning Plan: Exists, Plan Status: Active, 4 objectives, Current objective "speaking — B2."

## Dashboard result

**Passed.** After a brief initial render gap (loading), the dashboard showed:

- Today's Lesson: a real, AI-generated lesson ("Crafting Clear Workplace Emails," 15 min, vocabulary focus, 4 steps)
- Level B2, current CEFR
- Practice/Review stat cards
- Practice Gym summary with a real suggestion

One P1 found: `GET /api/placement/result` returned 400 ("Placement is not completed yet") even though placement had just completed — see "P0/P1 findings" below. **Fixed and confirmed live.**

## Today result

**Passed.** The generated lesson loaded with 4 real steps (Vocabulary warm-up, Listening, Writing task, Lesson review). Clicking into step 1 triggered real-time AI content generation (`~9s`, visible "Preparing activity…" state, no error) producing a genuine teaching module on "Phrases for Structured Explanations" including a Persian-language support translation (matching the pilot student's onboarding-selected support language).

## Activity completed: **yes**

- Activity type: `vocabularyPractice` / pattern `gap_fill_workplace_phrase` / interactionMode `gapFill`.
- **P0 found and fixed live**: the practice step rendered with zero fillable blanks — see "P0/P1 findings" below.
- After the fix deployed, reloaded the same activity: word-bank chip UI rendered correctly with 6 real sentences and a 24-word bank.
- Filled all 6 blanks (via word-bank taps) and submitted.

## Feedback loaded: **yes**

- "Checking answers…" loading state, then a scored result: **4/6 correct (67%)**, with per-item "You wrote: X / Correct: Y" breakdown for the 2 misses, and "Try again" / "Next activity" / "Back to dashboard" actions.
- No error, no crash. Confirms activity submission → AI evaluation → feedback rendering works end-to-end live.
- Confirmed downstream effects: dashboard streak advanced 0→1, "1 activities done", learning plan showed "Step progress 1/3 done."

## Practice Gym result

**Passed**, with one P1 documented (not fixed):

- Page loads, "Suggested for you," "Review queue" (correctly showing the graceful "You're all caught up" empty state), and a full catalogue of ready-to-start exercises across Listening/Reading/Writing/Speaking/Vocabulary.
- **P1 found, documented only**: the "Suggested for you" section showed the identical suggestion ("Giving Structured Explanations," speaking, B2) 6 times as separate cards. Confirmed via the raw API response that these are 6 genuinely distinct `StudentActivityReadinessItem` rows (distinct `readinessItemId`/`linkedLearningActivityId`), all generated for the same curriculum objective with no diversification across the plan's other objectives. This is real backend readiness-pool/suggestion data, not a frontend rendering bug — it reads as a bug to a student even though every card is individually functional. Root cause and fix are out of scope for this focused pilot-smoke phase (would require understanding/adjusting the Practice Gym suggestion-selection or replenishment diversification logic); filed as `TODO-20G-1`.
- `PracticeGymPilotEnabled` and review-scaffold visibility were not touched — confirmed no pending/rejected scaffold item appeared anywhere, and Today lesson insertion remains disabled (checked via Admin Lessons page).

## Journey result

**P0 found and fixed live.** Before the fix, `/journey` showed "Your learning plan is being prepared — Complete your placement assessment to unlock your personalised journey" for a student who had already completed placement and had a real, populated Learning Plan (confirmed simultaneously visible and correct on the Dashboard). See "P0/P1 findings" below. After the fix and redeploy: Journey correctly showed CEFR B2, plan progress, current objective (Speaking, B2), upcoming objectives (Writing/Reading/Vocabulary), and milestones ("Placement complete," "Learning plan created").

## Progress result

**Passed.** Real data throughout: CEFR progress (B2 → B2), skill breakdown (Grammar 51%, Vocabulary 51%, Workplace vocabulary 55%), mastery/review counts, and a recent-activity timeline (placement completion shown; the just-completed vocabulary activity was not yet visible in this timeline — minor, not investigated further, filed as `TODO-20G-2`).

## Profile result

**Passed.** Account info, CEFR (read-only, correctly shows B2 with the standard "your level is updated through placement..." note), full placement summary with per-skill breakdown, learning goals/focus-area selectors, support language (correctly pre-set to Persian), practice preferences, and notification preferences all rendered correctly with no errors.

## Readiness-after and admin re-verification

**P0 found, NOT fixed — documented.** Re-running the readiness audit on Admin Student Detail after the full walkthrough (placement + activity completion) returned **"Pilot readiness unavailable — Could not load pilot readiness."** Investigation:

- Confirmed via network log: `GET /api/admin/students/{id}/readiness` → **500**, `ExceptionType=PostgresException` (via diagnostics), consistently reproducible (5/5 sequential and 5/5 parallel direct fetch calls all 500).
- **Confirmed isolated to this one student**, not a systemic regression: the same endpoint returns **200** for a different, even-more-advanced student (`cfcca014-5950-4392-945b-dc668ceb72e1`, lifecycle "Active learning").
- **Confirmed the individual pieces work**: `progress-summary`, `placement/latest`, `writing-evaluations`, `readiness-pool`, `readiness-pool/health` all independently return 200 for the pilot student. Only the combined readiness audit fails.
- Attempted local reproduction against the Docker sandbox (same technique that successfully diagnosed the Phase 20F migration bug): drove a local test student through placement completion via direct API calls; readiness stayed **200** at that state. Did not have time within this phase to also reproduce activity completion + the Practice Gym's specific duplicate-suggestion pattern locally to fully match the pilot student's exact data shape.
- **Root cause not identified.** Production diagnostics deliberately does not expose the underlying SQL error text (by design, for security) and no production DB/server-log access was available in this session — the same constraint noted in Phase 20E and 20F.
- **Working hypothesis, not confirmed**: something in the newly-populated data — most likely the 49 duplicate Practice Gym readiness items generated for one objective (an unusual `speaking` objective mapped to a `ListeningComprehension`-typed activity via pattern `listening_multiple_choice_single`) — trips one of the readiness audit's unwrapped queries (`AddActivityContentChecksAsync`, `AddAudioTtsChecksAsync`, or `AddFeedbackAndReviewScaffoldChecksAsync`, none of which are wrapped in try/catch, unlike several other checks in the same service). This is a lead for the next debugging session, not a verified cause.

Filed as `TODO-20G-3` (P0, urgent, needs prod DB/log access — same escalation pattern as `TODO-20E-1`).

## Console/network errors found

- `POST /api/api/admin/generation-quality/summary?recentDays=30` → 404 — pre-existing, unrelated admin dashboard widget bug (double `/api/` in the URL), not touched this phase, not part of the student flow.
- `GET /api/placement/result` → 400 — **found and fixed** (see below).
- `GET /api/student/learning-plan/journey` → returned 200 but with an always-empty payload — **found and fixed** (see below).
- Gap-fill activity rendering with zero items — **found and fixed** (see below).
- `GET /api/admin/students/{id}/readiness` → 500 for the pilot student only — **found, documented, not fixed** (see above).

No 401/403 were observed anywhere in the student or admin flow. No broken media/audio requests were observed (the completed activity did not include audio; audio/TTS was not otherwise exercised in this smoke — documented per phase instructions rather than fabricated).

## P0/P1 findings — fixed and deployed live

### P0-1 (fixed): Gap-fill activity rendered with zero fillable blanks

**Root cause:** `ExerciseRendererComponent.gapFillContent` only read `contentJson`'s top-level `items` field. Pattern-engine-generated content (`module_stage_v1` schema, used for all AI-generated pattern-backed activities) nests items under `practiceContent.exerciseData.items` instead — exactly the unwrap already established and used by `freeTextContent` via the `stagedExerciseData` getter, but never applied to `gapFillContent`.

**Fix:** `gapFillContent` now falls back to `stagedExerciseData['items']` when the top-level `items` array is empty, matching the established pattern. `mapGapItems` already expected the exact field shape (`sentence`, `answer`, `hint`) the AI content provides — no other change needed.

**Files:** `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.ts`

**Tests:** 3 new Karma specs in `exercise-renderer.component.spec.ts` (extraction, dispatch/rendering, legacy-content precedence). One of the 3 caught a mistake in the fix's first draft (`instructions` field path) before deployment.

**Live confirmation:** reloaded the same activity after deploy — real sentences + 24-word word bank rendered; completed and submitted successfully; scored feedback (4/6, 67%) rendered correctly.

### P0-2 (fixed): Dashboard/Profile `placement/result` 400 after completing placement

**Root cause:** `GetPlacementResultAsync` (`PlacementService`) required the legacy `ResultJson` field, which only the older, non-adaptive completion path populates. The adaptive completion path (`CompleteAdaptive()`) — the only placement flow reachable from the current student UI — never sets it, so the check `assessment.ResultJson is null` always failed for every adaptively-placed student, live, on every dashboard/profile load. A secondary, related bug: the assessment lookup used an unordered `FirstOrDefaultAsync`, which could non-deterministically pick a stale row for a student with more than one `PlacementAssessment`.

**Fix:** Added an adaptive-aware branch that builds the result DTO from `PlacementSkillResults` when `ResultJson` is null but the assessment is adaptive and completed. Added `.OrderByDescending(a => a.CreatedAt)` to the assessment lookup.

**Files:** `src/LinguaCoach.Infrastructure/Placement/PlacementService.cs`

**Tests:** `StudentPlacementControllerTests.GetResult_AfterAdaptiveCompletion_ReturnsOkNotBadRequest` (new integration test).

**Live confirmation:** re-navigated to `/dashboard` post-deploy — `GET /api/placement/result` returned 200; the per-skill breakdown grid (previously silently hidden) rendered correctly (Listening/Writing/Vocabulary/Speaking/Reading/Grammar, all B2).

### P0-3 (fixed): Journey page always showed "complete your placement" regardless of actual state

**Root cause:** `StudentLearningPlanController.GetJourney` passed the JWT's **user ID** directly to `ILearningPlanService.GetJourneyAsync(studentProfileId, ...)`, whose parameter is a **StudentProfile ID** — two different GUIDs for the same person. The internal profile lookup (`WHERE Id == studentProfileId`) never matched, so the endpoint always silently returned its graceful-empty fallback (`planStatus: "None"`, `totalObjectives: 0`, `currentCefrLevel: "A1"`), for every student, regardless of real plan state. The admin and readiness-audit call sites of the same method were already correct (they pass `profile.Id` directly) — only the student-facing controller had the bug.

**Fix:** Added `ILearningPlanService.GetJourneyForUserAsync(userId, ...)`, which resolves the `StudentProfile` by `UserId` first, then delegates to the existing `GetJourneyAsync(profile.Id, ...)` (zero duplication of the objective-mapping logic). Controller now calls the new method.

**Files:** `src/LinguaCoach.Application/LearningPlan/ILearningPlanService.cs`, `src/LinguaCoach.Infrastructure/LearningPlan/LearningPlanService.cs`, `src/LinguaCoach.Api/Controllers/StudentLearningPlanController.cs`, plus 3 test-fake implementers updated to satisfy the interface change.

**Tests:** `StudentLearningPlanJourneyTests.GetJourney_ResolvesActivePlan_ByUserIdNotProfileId` (new integration test) — deterministically creates a real plan keyed by profile ID, then asserts the user-ID-keyed student endpoint sees it (would have failed against the pre-fix code, matching the exact live symptom).

**Live confirmation:** `/journey` post-deploy showed CEFR B2, plan progress, current objective, upcoming objectives, and milestones.

## P1 findings — documented, not fixed

- **Practice Gym "Suggested for you" shows 6 identical cards** (same objective, 6 distinct readiness items, no diversification). Confirmed real backend data via raw API inspection, not a frontend bug. `TODO-20G-1`.
- **Recent Activity timeline on Progress page didn't show the just-completed vocabulary activity** (only showed placement completion). Not investigated further within this phase's time budget. `TODO-20G-2`.

## P0 findings — documented, not fixed

- **Readiness audit 500s for the pilot student specifically** after placement + activity completion (see "Readiness-after" above). `TODO-20G-3`, escalated the same way as `TODO-20E-1`/`TODO-20F-1` — needs production DB/log access to pin down the exact query.

## Runtime settings

No runtime setting was changed. Confirmed via Admin Feature Gates and Admin Lessons pages: review-scaffold generation remains gated as before, `PracticeGymPilotEnabled`/labels unchanged, "Today lesson insertion disabled," "0 review scaffold items exist," no pending/rejected scaffold item was ever visible to the student at any point in the walkthrough.

## Tests

- **Backend:** `dotnet build` — 0 errors. `dotnet test tests/LinguaCoach.UnitTests` — **1,750/1,750 pass**. `dotnet test tests/LinguaCoach.IntegrationTests` — **1,380/1,380 pass** (+2 new: placement-result regression, journey-userId regression). `dotnet test tests/LinguaCoach.ArchitectureTests` — **5/5 pass** (unchanged).
- **Frontend:** `npm run build -- --configuration production` — clean. `npm test -- --watch=false --browsers=ChromeHeadless` — **1,551 pass / 120 fail** (was 1,548/120 at the start of this phase; +3 new gap-fill tests; same 120 pre-existing, unrelated failures — **0 new regressions**).
- **Playwright:** not added this phase. The golden path was validated by hand, live, against production (the strongest possible validation for this phase's purpose); a scripted Playwright smoke against a stable local fixture is tracked separately and wasn't practical to build and stabilize within this session's time budget alongside the three live bug fixes. Manual validation notes are captured throughout this review instead.

## Docs updated

- `docs/sprints/current-sprint.md`, `docs/roadmap/road-map.md`, `docs/handoffs/current-product-state.md`, `docs/pilot/student-pilot-runbook.md`, `TODOS.md`, this review.

## Final verdict

**Ready for one controlled student pilot: conditionally yes, with one open admin-tool caveat.**

Every student-facing acceptance criterion in this phase passed live in production: login, placement (start through completion), CourseReady + Learning Plan, Dashboard, Today, one full activity (start → submit → feedback), Practice Gym, Journey, Progress, and Profile. Three real, live P0 bugs were found and fixed in this session, each confirmed working after deploy.

The one open item is the admin-only readiness-audit regression (`TODO-20G-3`) for this specific student's data — it does not block a real student from using the app, but it does mean an admin cannot currently re-verify this particular student's state through the Phase 20D tool. Recommend: proceed with treating this student as pilot-ready for the student-facing flow, while tracking `TODO-20G-3` for resolution with production DB/log access before relying on the readiness tool for this student going forward.

## Explicit confirmations

- No AI scoring, CEFR update, objective completion, Learning Plan regeneration, or unsafe review scaffold behavior was changed. The CEFR B2 result and Learning Plan/objective progression seen throughout this review are the product's existing, pre-built deterministic placement/plan/mastery logic operating normally — nothing in this phase altered any of those algorithms.
- No attempts, submissions, or evaluations were deleted anywhere. The one placement assessment and one activity attempt created during this live walkthrough remain in production as real (not synthetic) pilot data.
- No secrets, passwords, or tokens were committed or exposed. All three commits this phase were scanned for the pilot student's password, admin password/email, and JWT bearer tokens before commit — no matches.
- Committed and pushed: three commits (`76f28e2`, `0891943`, plus this review/docs commit), all deployed via the normal CI/CD pipeline and confirmed live.
