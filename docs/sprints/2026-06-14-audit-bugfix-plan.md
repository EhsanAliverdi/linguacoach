# Audit Bug-Fix Plan — 2026-06-14

Related: [2026-06-13 Deployed Admin + Student E2E Audit](../testing/deployed-admin-student-e2e-audit-2026-06-13.md)

## Goal

Work through the bugs found in the 6-phase deployed E2E audit, one at a time,
in priority order. Each item below will be implemented, verified, and
documented as a separate step.

## Order of work

1. **[High] Blank `?pattern=...` / lesson teach-page activity rendering**
   Affects: lesson Listening/Vocabulary steps, and all 4 enabled Practice
   Gym "by exercise type" cards (`phrase_match`, `gap_fill_workplace_phrase`,
   `email_reply`, `teams_chat_simulation`). Backend returns 200 with valid
   content; `<app-activity-teach-page>` renders empty. Highest-value fix —
   unblocks the majority of currently-dead content.

2. **[High] Speaking activity: no text fallback when mic denied**
   Lesson Speaking steps and Practice Gym `?type=SpeakingRolePlay` show a
   dead end ("Try again" / "Back to Today" only). Placement assessment
   already has a working text-fallback pattern to copy.

3. **[High] No admin password-reset capability**
   No password field/reset action in admin edit UI, no "Forgot password" on
   `/login`. Root cause of student #1 lockout. Add admin-initiated reset
   (e.g. set new temporary password + force change on next login).

4. **[Medium] "Require password change on first login" defaults to checked**
   Quick fix alongside #3 — either default unchecked, or ensure the forced
   new password is always recoverable via #3's reset capability.

5. **[Medium] `/admin/ai-usage` and `/admin/students/new` direct-nav redirect to `/login`**
   Route-guard/routing fix — correct paths are `/admin/usage` and
   `/admin/create-student`. Either fix the route definitions/links or add
   redirects from the old paths.

6. **[Medium] Profile "Current level: Not assessed yet" despite completed placement**
   Wire `/profile`'s level display to the same placement-result data source
   used by Journey/Progress/Dashboard.

7. **[Medium] Onboarding step-1 language-path selection lost on refresh/back**
   Persist/restore step-1 selection from backend state like other steps.

8. **[Medium] `/vocabulary` stays empty after completed Writing activities**
   Investigate population trigger — feature may be unimplemented or
   miswired.

9. **[AI quality] Listening free-text "reply" feedback is generic boilerplate**
   Make it submission-aware like the Writing activity's feedback.

10. **[Low] Cleanup batch**
    - Forward-nav transient blank state on activity pages
    - Unknown routes redirect authenticated users to `/login` instead of `/dashboard`
    - No retry/history UI for completed activities (product gap — may be a
      separate feature proposal rather than a quick fix)

## Process per item

- Investigate root cause in code.
- Implement minimal fix.
- Add/update tests where reasonable.
- Update this doc with outcome notes.
- Update docs impacted (per `AGENTS.md` documentation rules).

## Status

### Item 1 — done (2026-06-14)

Root cause: `activity-lesson.component.ts`'s `setReadyActivity()`
unconditionally set `state` to `'learning'` for non-speaking activities.
`activity-teach-page.component.html`'s `@case ('exerciseRenderer')` block is
intentionally empty (per its own comment: "Pattern-engine activities have no
separate Teach page; renderer covers Teach+Practice") and provides no CTA, so
any activity whose `teachContent().block === 'exerciseRenderer'`
(`PatternBackedPresenter` — i.e. any activity with `interactionMode` set, or
`writingScenario` with `contentJson`, excluding `speakingRolePlay`) got stuck
on a permanently blank Teach page. This covers the Listening/Vocabulary lesson
steps and all 4 Practice Gym `?pattern=...` cards from the audit.

Fix: `setReadyActivity()` now checks
`ActivityPresenterFactory.for(activity).teachContent(activity).block ===
'exerciseRenderer'` and, if so, sets `state` directly to `'writing'` (Practice
page), where `<app-exercise-renderer>` is actually rendered. Speaking
role-play and legacy presenters (vocab/listening/writing) never return
`exerciseRenderer`, so their flows are unaffected.

Verification: `npx tsc -p tsconfig.app.json --noEmit` passes clean. Reviewed
`exercise-pattern-renderers.spec.ts` and `lesson-activity-wiring.spec.ts` —
neither depends on the Teach-page state for pattern-backed activities, no
test updates needed.

### Item 2 — done (2026-06-14)

Lesson Speaking activities and Practice Gym `?type=SpeakingRolePlay` showed a
dead end on `mic-denied`/`mic-unsupported` states (only "Try again"/"Back to
Today", no way to complete the activity). Placement assessment already had a
working text-fallback pattern (textarea + "Microphone access was denied. Type
your response instead.").

Fix: `activity-lesson.component.html`'s `mic-unsupported` and `mic-denied`
blocks now include a textarea bound to `draftText` plus a "Get coach
feedback" button. Added `submitTextFallback()` to
`activity-lesson.component.ts`, which calls the same
`activityService.submitAttempt()` used by the writing flow, routes to
`'feedback'` on success, and stays on the current mic state (with an error
message) on failure. `mic-denied` also keeps "Try microphone again".

Verification: `npx tsc --noEmit` and `ng build` both pass clean (pre-existing
unrelated warnings in `pattern-evaluation-result.component.html` only).

### Item 3 — done (2026-06-14)

No admin-initiated password reset existed (root cause of student #1 lockout
in the audit). `ApplicationUser.MustChangePassword` and
`StudentLifecycleStage.PasswordChangeRequired` were already wired end-to-end
(set at creation, returned by `/api/auth/login`, cleared by
`/api/auth/change-password`), just missing an admin entry point.

Backend:
- `ResetStudentPasswordCommand` + `IAdminStudentQuery.ResetStudentPasswordAsync`
  added to `LinguaCoach.Application/Admin/AdminQueries.cs`.
- Implemented in `AdminHandler.ResetStudentPasswordAsync` using
  `UserManager.RemovePasswordAsync` + `AddPasswordAsync` (bypasses current-password
  check, unlike `ChangePasswordAsync`), then sets `MustChangePassword`.
- New endpoint `POST /api/admin/students/{studentId}/reset-password`
  (`ResetStudentPasswordRequest { NewPassword, MustChangePassword = true }`) in
  `AdminController.cs`, returns 204/400/404.

Frontend:
- `AdminApiService.resetStudentPassword()` added.
- `admin-students.component.ts`: new "Reset password" row action (active
  students only) opens a modal with a temp-password field (8+ chars, matches
  backend's `RequiredLength=8`/`RequireDigit=true` policy), a "Generate
  password" button, and a "Require password change on next login" checkbox
  (defaults checked). On success, shows the new password once in a read-only
  field with a "Done" button — not just a toast, so the admin has time to
  copy it (toasts auto-dismiss after 4.5s). New `.sp-admin-alert-success`
  style added to `styles.css`.

Verification: `dotnet build` (API) and `npx tsc --noEmit` / `ng build`
(Angular) both pass clean.

### Item 4 — resolved via item 3 (2026-06-14)

Plan item 4 offered two options: default "require password change on first
login" to unchecked in the create-student form, or ensure a forced new
password is always recoverable via an admin reset capability. Item 3 delivers
the second option — admins can now reset a locked-out student's password at
any time via `/admin/students` → "Reset password". The create-student
checkbox default (`mustChangePassword = true`,
`create-student.component.ts:19`) is a reasonable security default and is
left unchanged; no further action needed.

### Item 5 — done (2026-06-14)

`/admin/ai-usage` and `/admin/students/new` direct-nav redirected to
`/login` because they were not registered routes — the actual routes are
`/admin/usage` and `/admin/create-student`.

Fix: added two `redirectTo` child routes in `app.routes.ts` under the
`admin` route: `students/new` → `create-student`, and `ai-usage` → `usage`.

Verification: `npx tsc --noEmit` passes clean.

### Item 6 — done (2026-06-14)

`profile.component.ts` had a hardcoded "Not assessed yet" string for "Current
level" — it never fetched any data. `GET /api/dashboard` already returns
`cefrLevel: string | null` (set on `StudentProfile` after placement, same
source the admin students list and dashboard use).

Fix: `ProfileComponent` now implements `OnInit`, injects `DashboardService`,
calls `getDashboard()`, and displays `cefrLevel() ?? 'Not assessed yet'`.

Verification: `npx tsc --noEmit` passes clean.

### Item 7 — done (2026-06-14)

`step1-language.component.ts` never restored a prior selection — on refresh
or back-navigation, `selected` was always `null` even if the language step
was already submitted. `GET /api/onboarding/status` already returns
`languagePairId` once the language step is persisted
(`OnboardingController.cs:109`).

Fix: `ngOnInit` now also calls `onboarding.getStatus()` and, if
`languagePairId` is present, pre-selects it via `this.selected.set(...)`.

Verification: `npx tsc --noEmit` passes clean.

### Item 8 — investigated, no code change (2026-06-14)

The `/vocabulary` feature is fully wired end-to-end, not a stub:
- `vocabulary.component.ts` calls `GET /api/vocabulary`
  (`VocabularyController` → `GetVocabularyHandler` → `StudentVocabularyItems`
  table).
- Population happens via `VocabularyExtractionService.ExtractAsync`
  (`LinguaCoach.Infrastructure/Vocabulary/VocabularyExtractionService.cs`),
  called from `ActivitySubmitHandler` after every legacy-writing submission
  (line ~223) and after pattern-evaluation submissions **only if
  `evalResult.Corrections.Count > 0`** (line ~317).
- Extraction makes a separate AI call
  (`vocabulary_extract_from_attempt`); if that call returns 0 items, it's
  logged at `Debug` and silently returns (line ~95); failures are caught and
  logged at `Warning` (line ~106), never surfacing to the user.

Most likely root causes, in order of likelihood:
1. For pattern-backed Practice Gym activities, `evalResult.Corrections` is
   often empty (correct/near-correct answers, or evaluators for some pattern
   types — e.g. `matchingPairs`, `gapFill` — may never populate
   `Corrections`), so extraction is never even attempted.
2. When extraction does run, the separate AI call may be returning 0 items
   and this is only visible at `Debug` log level.

This is not a quick wiring bug — diagnosing which of the two applies requires
either live application logs from a real submission or a debugging session
against a running backend, which is out of scope for a static code-only fix.
No code change made in this pass.

Follow-up recommendation (not yet scheduled): add an `Information`-level log
line in `ActivitySubmitHandler` when vocabulary extraction is *skipped*
because `Corrections.Count == 0`, and bump
`VocabularyExtractionService`'s "0 items returned" log to `Information` too —
this would make root-causing a live report trivial without further code
changes.

### Item 9 — done (2026-06-14)

`ListeningComprehensionEvaluator.Evaluate` (entirely deterministic, no AI
call) returned one of two **static** strings for `ResponseFeedback`
regardless of what the student actually wrote — generic boilerplate as
flagged in the audit.

Full AI-feedback parity with Writing would require a new prompt template +
AI context wiring (large change, out of scope for this pass). Instead, made
the deterministic feedback content-aware using data already computed:
- New `BuildResponseFeedback(responseTask, responseText, responseScore)`
  helper in `ListeningComprehensionEvaluator.cs`.
- Tokenizes `ExpectedFocus` and the student's `responseText` (reusing
  existing `Tokenize`/`ScoreResponse`), and if expected-focus terms are
  missing from the reply, names them specifically (e.g. "Your reply is
  missing some key points from the message: deadline, budget...").
- If all focus terms are present and `responseScore >= 80`, gives positive
  feedback instead of the generic line.
- Falls back to the original generic message only when there's no
  `ExpectedFocus` data to compare against.

Verification: `dotnet build` passes clean. No existing tests reference
`ListeningComprehensionEvaluator` or `ResponseFeedback`.

### Item 10 — partially done (2026-06-14)

- **Unknown routes redirect authenticated users to `/login`** — fixed.
  `app.routes.ts`'s wildcard route used a static `redirectTo: 'login'`. Now
  uses a function-based `redirectTo` (Angular 17.3+ feature, confirmed
  supported by `ng build`):
  `redirectTo: () => inject(AuthService).isAuthenticated() ? '/dashboard' : '/login'`.

- **Forward-nav transient blank state on activity pages** — not changed in
  this pass. The state-machine fix in item 1 (skip dead Teach page for
  pattern-backed activities) removes the worst case of this; the remaining
  transient flash is a minor loading-state cosmetic issue with no clear
  single root cause, and risks regressions if rushed.

- **No retry/history UI for completed activities** — not changed. This is a
  product feature gap (new UI + possibly new endpoints), not a bug fix, and
  needs its own scoping/design rather than a "fix" in this bugfix pass.

Verification: `npx tsc --noEmit` and `ng build` pass clean.

## Summary

Items 1, 2, 3, 5, 6, 7, 9, and part of 10 (unknown-route redirect) are done
and verified via build. Item 4 resolved via item 3. Item 8 investigated with
findings documented but no code change (requires live diagnostics). Item 10's
remaining two sub-items (forward-nav blank flash, retry/history UI) are
deferred — the former is low-impact/cosmetic, the latter is a product feature
proposal for separate scoping.

All changes build clean (`dotnet build`, `npx tsc --noEmit`, `ng build`).
No automated test suite was run end-to-end as part of this pass; existing
Playwright specs reviewed for the items they could affect (1, 2) were
confirmed unaffected by inspection.
