# Phase 20I — Full Live Student/Admin QA & Data Consistency Audit

**Date:** 2026-07-03
**Related:** Phase 20H (`2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`), pilot-student fixes commits `9e8008e`, `2592e4f`
**HEAD before work:** `2592e4f` (already pushed and deployed; confirmed via `git log origin/main` and the `Deploy` GitHub Actions workflow, run `28632024873`, success)

## Scope actually covered

Given the size of the requested checklist (Parts A–S), this pass prioritized breadth of the highest-risk areas over exhaustively covering every listed sub-bullet. Covered in depth: deployment verification, admin login and Feature Gates, admin Student Detail for the target pilot student, production DB inspection (identity/placement/readiness), full onboarding → placement → Today lesson → activity → feedback flow on a fresh QA student, and Practice Gym landing state. Not driven end-to-end this pass: Journey, Progress, Profile edit flows, admin operational pages beyond Diagnostics/Feature Gates/Students, and the full runtime-settings value inventory (Part P). These are listed as follow-up in "Deferred / not covered" below.

## Access and credential handling

- Admin credentials and the production DB connection string were provided directly by the product owner in chat. They were used only in-memory for this session (Bash env vars passed to a throwaway `postgres:16-alpine` Docker container for `psql`, and browser session state for the live UI). Neither was written to any file, doc, commit, or log.
- The harness's own auto-mode classifier blocked two actions as a safety check: (1) a manual `storage` dump of the browser session (correctly refused — risk of leaking live tokens into output), and (2) resetting `pilot.student.20e`'s password without explicit per-action authorization. Both were respected; see below for how student-side access was obtained instead.
- Per product-owner decision (`AskUserQuestion`), student-side QA was performed against a newly created QA account (`qa.phase20i.20260703@example.com`), not by resetting `pilot.student.20e`'s password. `pilot.student.20e` was inspected via Admin UI and read-only DB queries only.
- DB access was read-only (`SELECT` / `\d` only) throughout. No `UPDATE`/`DELETE`/`INSERT`/`DDL` was run against production.

## Part A — Deployment and environment verification

- HEAD (`2592e4f`) matches `origin/main` and the latest successful `Deploy` workflow run.
- Admin login (`ehsan.aliverdi@gmail.com`) succeeds; admin dashboard loads with no console errors.
- DB reachable, AI provider `gemini`, 0 errors / 1 warning in the last 24h per Diagnostics.
- Feature Gates page loads with structured data; the two critical AI-signal-safety gates (Speaking, Writing) are **Locked**, matching the "dangerous AI gates cannot be edited" requirement.
- Minor (P2, not fixed): navigating to a URL that doesn't match an Angular route (e.g. a guessed `/admin/feature-gates` instead of the real `/admin/settings/feature-gates`) falls through to the student `/dashboard` route even for an Admin-role user, producing two `400`s (`GET /api/dashboard`, `GET /api/student/dashboard/summary`) and a visible "Student profile not found" message to the admin. Not investigated further — likely a wildcard-route fallback not gated by role.

## Part B/C — Student identity, onboarding, preferences (pilot.student.20e)

DB state for `pilot.student.20e` (student_profile_id `c2a7caff-b46a-4da4-b424-8bd5ca8c0394`):

| Field | Value |
|---|---|
| `translation_help_preference` | NULL |
| `support_language_code` / `support_language_name` | empty |
| `language_pair_id` | `20000000-...-001` (the seeded Persian↔English pair — the *only* row in `language_pairs`) |
| `learning_goal` / `learning_goals` | empty / `[]` |
| `focus_areas` | `[]` |

This confirms the `2592e4f` fix is working as intended in production: despite the legacy Persian `language_pair_id` still being set from onboarding, `translation_help_preference` and `support_language_code/name` are correctly empty, and the admin readiness audit and live dashboard no longer leak Farsi text (per the prior live-verification pass).

**Product question (not a bug):** `language_pairs` has exactly one seeded row (Persian↔English). New students in onboarding step 1 ("Choose your language path") are shown only "Persian to English" as a selectable option, and the onboarding placeholder text for the "why do you want to improve your English" field includes a Persian example. This reads as intentional single-language-pair scoping for the current pilot cohort (Persian-speaking users learning English), not a bug — but it means onboarding will visibly force a "Persian" selection on any non-Persian pilot student if the pilot expands. **Ask:** should more `language_pairs` rows be seeded before inviting non-Persian-speaking students, or is this step meant to be hidden/skipped when only one pair exists?

## Part D — Placement QA

### Bug found and fixed (P1): Admin "Adaptive placement assessment" panel always shows "No placement assessment on record"

**Repro:** Open Admin → Students → any student with a completed placement → Overview tab → "Adaptive placement assessment" card.
**Expected:** Shows the completed assessment (CEFR, skill breakdown, completion date).
**Actual:** Always shows "No placement assessment on record. Start placement" — even for `pilot.student.20e`, who has a genuinely completed assessment (`9945e76e-35dc-4ee3-a0ed-8e1f3b22d8bb`, completed 2026-07-02 07:13:34, CEFR B2).

**Root cause:** `AdminPlacementController.GetLatestPlacement` (`src/LinguaCoach.Api/Controllers/AdminPlacementController.cs`) returns `{ hasPlacement = false }` on the not-found path, but on success just does `return Ok(result)`, passing through `PlacementAssessmentSummaryDto` as-is. That DTO has no `hasPlacement` property. The frontend (`AdminPlacementLatestResponse.hasPlacement: boolean`, checked as `!placementLatest()?.hasPlacement` in `admin-student-detail.component.ts`) therefore reads `hasPlacement` as `undefined` on every successful response, which is falsy — so the panel always renders the "no placement" empty state regardless of actual data.

**Impact:** Every admin viewing any student's placement history via this panel sees a false "no placement" state, and the "Start placement" button it exposes creates a **new** `PlacementAssessment` row for a student who already has a completed one — this is very likely how the stray `InProgress` assessment (`fcc7ca1f-...`, started by `admin` on 2026-07-02 20:33, never completed) ended up in `pilot.student.20e`'s history from the prior audit session.

**Fix:** `AdminPlacementController.GetLatestPlacement` now explicitly returns `hasPlacement = true` alongside the DTO fields on the success path.
**Test added:** `AdminPlacementEndpointTests.GetLatestPlacement_AfterCompletion_ReturnsHasPlacementTrue` — starts and completes a placement, then asserts `hasPlacement == true` on the `/placement/latest` response. (No prior test covered the success path — only the "no placement" and "unknown student" cases existed, which is why this shipped broken.)

**Not addressed:** the stray orphan `InProgress` assessment row for `pilot.student.20e` was left in place — deleting/expiring it is a data mutation outside this session's read-only DB mandate. Recommend: use the admin "Expire" action (`POST .../placement/{id}/expire`) on that specific assessment once the fix above is live and confirmed, so the panel resolves to the completed one going forward without manual SQL.

### Bug found and fixed (P1): Duplicate placement skill-result rows on every completed assessment

**Repro:** Complete a placement assessment (any student) and inspect `placement_skill_results` for that assessment.
**Expected:** One row per skill (6 rows: listening, reading, writing, vocabulary, grammar, speaking).
**Actual:** Exactly two identical rows per skill (12 rows total) — confirmed both in production (`pilot.student.20e`'s and the new QA student's completed assessments) and visually on the student-facing placement result page, where the "Skill breakdown" grid rendered every skill twice (screenshot captured during this session).

**Root cause:** `.NET`'s options config binding **appends** config-bound array items to the class-default array rather than replacing it. `PlacementAssessmentOptions.SkillsToAssess` has a 6-skill default *and* an identical 6-skill list in `appsettings.json`, so the bound `IOptions<PlacementAssessmentOptions>.SkillsToAssess` at runtime contains 12 entries (each skill twice). Several call sites in `PlacementAssessmentService` already defend against this with `.Distinct()` (a pre-existing comment in `ReviewScaffoldDryRunTests.cs` documents the same quirk for a different options class) — but `BuildSkillResults` and `CreateInitialItems` did not, so every completion inserted two `PlacementSkillResult` rows per skill, and initial item generation created twice as many opening items as intended.

**Fix (root cause):** `LinguaCoach.Infrastructure/DependencyInjection.cs` now runs `services.PostConfigure<PlacementAssessmentOptions>(o => o.SkillsToAssess = o.SkillsToAssess.Distinct().ToArray())` once, at the source, instead of requiring every call site to remember `.Distinct()`.
**Fix (defense in depth):** Added a unique index `ux_placement_skill_results_assessment_skill` on `(placement_assessment_id, skill)` via EF migration `20260703020244_T_Phase20I_PlacementSkillResultUniqueIndex`, so a future duplicate-insert bug fails loudly (constraint violation) instead of silently duplicating data. `PlacementAssessmentService.FinalizeCompletionAsync` now catches that specific `DbUpdateException` and returns the winning result set instead of throwing, in case two truly concurrent completion requests ever race past the existing "already has results" check.
**Migration data safety:** the migration deletes pre-existing duplicate rows (keeping the earliest per `(assessment, skill)` pair) *before* creating the unique index, so it will not fail against the already-duplicated production rows for `pilot.student.20e` and other students who completed placement before this fix. This is a schema migration applied through the normal deploy pipeline (`docker compose` / EF `Migrate()` on API startup), not a manual production SQL mutation.
**Tests added:**
- `AdminPlacementEndpointTests.CompletePlacement_SkillResultsHaveNoDuplicateSkill` — completes a placement via the real HTTP flow and asserts no duplicate skill appears in the response.
- `DbContextMappingTests.PlacementSkillResult_DuplicateSkillForSameAssessmentThrows` — asserts the unique index rejects a duplicate `(assessment, skill)` insert.

### Other placement observations (not bugs)

- Full placement flow (20 questions reached via adaptive branching — more than the "8 to 15" stated in the UI copy, likely because this run's answers were deliberately inconsistent/wrong to exercise the adaptive engine) completed cleanly: no console errors, correct completion transition, lifecycle moved to `CourseReady`, Learning Plan generated.
- `Start_WhenAlreadyCompleted_Returns409` and related lifecycle-guard tests continue to pass — the placement-stage gating revert from the prior phase was not touched.

## Part F/G/H/I — Dashboard, Today, Activity player, Feedback (QA student)

Full walkthrough on the fresh QA student (`qa.phase20i.20260703@example.com`, career context "Customer support officer"):

- Onboarding (5 steps) → placement (20 questions) → dashboard → Today lesson → activity player → feedback, all completed with **zero unexpected console errors**.
- Dashboard correctly reflects the chosen career context ("Greeting and Initial Inquiry — Customer support officer"), not a generic "workplace" default — confirms the `LearningGoalContextResolver` fix from the prior phase is working.
- Today lesson generated real, well-formed content (a gap-fill vocabulary exercise, "Workplace Phrases," AI-generated, correctly labeled). Completed it: scored 4/6 (66.67%), corrections rendered clearly per wrong answer, no raw JSON/prompt leakage, no AI disclaimer needed (deterministic gap-fill scoring, not an AI judgment call).
- DB confirms the attempt persisted correctly: `activity_attempts` row with `score=66.67`, `percentage=66.67`, `passed=true`, `completed=true`.
- The dashboard's skill-breakdown grid showed each skill twice — this is the same duplicate-skill-results bug from Part D, now fixed (not yet deployed at time of testing).
- A stray "Dashboard is only available after onboarding is complete." string appeared in the raw text dump of the dashboard page but is **not actually visible** in a screenshot — it's inert/hidden text (likely an a11y or loading-state string), not a rendered bug. No action taken.

## Part J — Practice Gym (spot check)

- Loads cleanly for the fresh QA student with an honest "Your practice is being prepared" message (no readiness pool populated yet for a brand-new account — expected, background jobs run on a schedule) plus a populated "free practice by skill" catalog (listening exercises with clear types, skill labels, durations). No empty/broken cards observed.
- For `pilot.student.20e` (via Admin readiness audit), Practice Gym pool health: 177 ready items (target 10, well over-provisioned), 1614 queued, 35 failed, 7 expired. The "Failed: 35" and unusually large "Queued: 1614" backlog were not root-caused this pass — flagged as a **product/ops question**, not confirmed as a bug: is a queue this large for one student expected behavior of the readiness replenishment job, or a sign of a runaway enqueue loop? Recommend a follow-up DB/job-log investigation before scaling to more pilot students, since 1614 queued items for a single account seems disproportionate.
- Admin readiness audit for `pilot.student.20e` also flagged (informational, not fixed): "58 listening activity/activities have no ready audio asset" and "Could not fully evaluate activity content validity for this student" — both surfaced as `warning`, not `blocking`, per the existing audit design. Not investigated further this pass.

## Part S — DB consistency (partial)

Inspected: `student_profiles`, `placement_assessments`, `placement_skill_results`, `language_pairs`, `student_activity_readiness_items` (grouped by source/status), `activity_attempts`, `AspNetUsers`/role mapping (via admin UI join). Found: the orphan `InProgress` placement assessment (Part D), the duplicated skill-result rows (Part D, fixed), and the single-row `language_pairs` table (Part B/C, product question). No `userId`/`profileId` mismatches found. Not inspected this pass: `StudentLearningPlan`/objectives table directly (verified via UI only), `StudentLearningEvent` ledger, `RuntimeSettingOverride`, `AdminAuditLog`, `GenerationValidationFailure`.

## Deferred / not covered this pass

- Journey, Progress, and Profile pages were not driven end-to-end (only referenced via admin summary data).
- Admin operational pages beyond Students/Diagnostics/Feature Gates (AI Operations, Prompts, Usage Policies, Curriculum, Notifications, Integrations, Security) were not opened.
- Runtime settings/feature-gates value inventory (Part P's full checklist of specific flags) was not itemized beyond confirming the critical AI-safety gates are locked.
- The `GET /api/api/admin/generation-quality/summary` 404 seen in Diagnostics (double `/api/api/` prefix) was noted but not root-caused or fixed — likely a stray leading slash in a frontend URL constant. **P2, recommend a follow-up fix.**
- Practice Gym's large queue backlog (1614 items) for `pilot.student.20e` was flagged as a product/ops question, not resolved.
- TODO-20H-1 (Practice Gym dedup) classification: not re-evaluated this pass — no new duplicate-card evidence was gathered against `pilot.student.20e`'s actual Practice Gym UI (only the admin readiness-pool counts were inspected).

## Product-owner questions

1. Should more `language_pairs` rows be seeded before inviting non-Persian-speaking pilot students, given onboarding step 1 currently only offers "Persian to English"?
2. Is the ~1614-item Practice Gym queue backlog for a single student (`pilot.student.20e`) expected replenishment-job behavior, or does it warrant investigation before scaling the pilot?
3. Should the orphan `InProgress` placement assessment for `pilot.student.20e` (created via the now-fixed admin panel bug) be expired via the admin "Expire" action once this fix is deployed?

## Local validation

```
dotnet build --configuration Release         → 0 errors, 25 warnings (pre-existing)
dotnet test  --configuration Release --no-build
  LinguaCoach.ArchitectureTests   → 5/5 passed
  LinguaCoach.UnitTests           → 1757/1757 passed
  LinguaCoach.IntegrationTests    → 1389/1389 passed (includes 4 new/updated tests)
```

Angular unit/e2e suites and `npm run build -- --configuration production` were **not** run this pass — no Angular source files were changed (both fixes are backend-only: controller, DI config, service, migration). Documented here per the CLAUDE.md documentation-impact rule rather than silently skipped.

## Files changed

- `src/LinguaCoach.Api/Controllers/AdminPlacementController.cs` — `hasPlacement = true` on success.
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — dedupe `SkillsToAssess` once via `PostConfigure`.
- `src/LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs` — defensive catch for the (now much less likely) skill-result unique-constraint race.
- `src/LinguaCoach.Persistence/Configurations/PlacementSkillResultConfiguration.cs` — unique index.
- `src/LinguaCoach.Persistence/Migrations/20260703020244_T_Phase20I_PlacementSkillResultUniqueIndex.cs` (+ Designer, + snapshot) — migration with pre-index dedup cleanup.
- `tests/LinguaCoach.IntegrationTests/Api/AdminPlacementEndpointTests.cs` — 2 new tests.
- `tests/LinguaCoach.IntegrationTests/Persistence/DbContextMappingTests.cs` — 1 new test.

## Explicit confirmations

- No AI scoring, CEFR update, objective-completion, Learning Plan regeneration, or review-scaffold-safety logic was changed.
- No attempts, submissions, or evaluations were deleted. One new `activity_attempts` row and one new `student_profiles`/placement-assessment set were **created** (QA test account), nothing existing was removed.
- No secrets, passwords, tokens, DB connection strings, or personal data were committed or exposed. Credentials were used only via in-memory env vars / browser session state for this session.

## Final verdict

**Ready to invite one real controlled pilot student now:** Yes for a Persian-speaking student matching the existing cohort profile — the two P1 data-integrity bugs found here are real but were both silent/cosmetic for the student (they affected the admin's view of placement history and doubled a display list, not scoring, completion, or CEFR correctness) and are fixed with tests in this change.

**Ready to expand to 3–5 students:** Not yet — recommend first (a) deploying and live-verifying this fix, (b) getting product-owner answers to the `language_pairs` and Practice Gym queue-backlog questions above, and (c) a follow-up pass covering the Parts explicitly deferred this session (Journey/Progress/Profile end-to-end, admin operational pages, runtime-settings inventory).

## Next recommended action

Push this commit, watch the `Deploy` workflow, then live-verify: (1) Admin → `pilot.student.20e` → Overview → "Adaptive placement assessment" now shows the completed B2 assessment instead of "No placement assessment on record", and (2) a freshly completed placement (new test account) shows exactly one row per skill in the result breakdown, both on-screen and in `placement_skill_results`.
