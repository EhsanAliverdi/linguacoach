# Deployed Admin + Student E2E Audit — 2026-06-13

## Environment

- URL: https://speakpath.app/
- Phase: 1 (admin-side E2E), Phase 2 (student onboarding/placement/journey/lesson E2E, student #2), Phase 3 (Practice/Progress/Profile/mobile/AI quality/Playwright review, student #2), Phase 4 (gap-coverage cleanup: mobile lesson/audio, retry/history, vocabulary, Persian toggle, admin AI Usage in-app nav, admin error/permission handling, admin cross-check of student #2, Speaking mic-allowed), Phase 5 (exhaustive Practice Gym activity-type sweep, student #2), Phase 6 (edge-case sweep: admin form validation, back/forward nav, session expiry, blank submissions, direct URL manipulation, refresh-during-activity)
- Browser: headless Chromium via gstack browse
- Date/time of test: 2026-06-13 (Phase 1, 2, 3, 4, 5), 2026-06-14 (Phase 6)

## Full audit history / phase log

- **Phase 1** — Admin-side E2E (login, nav, student CRUD, mobile overflow).
  Found: no admin password-reset (High, blocker), 2 direct-URL route-guard
  bugs (Medium), admin mobile overflow (Low). See "Admin summary" and "Bugs
  by severity".
- **Phase 2** — Student #2 onboarding → placement (B2) → journey → first
  Today's Lesson (4/4 steps, 2 via workaround). Found: blank "teach page"
  for Listening/Vocabulary lesson steps (High), Speaking mic-denied no
  fallback (High), onboarding step-1 selection lost on refresh/back
  (Medium), dashboard stats stale after lesson completion (Medium). See
  "Student #2 summary (Phase 2)".
- **Phase 3** — Practice Gym (By skill + By exercise type), Progress,
  Profile, mobile responsiveness, AI quality, Playwright review. Found:
  all 4 enabled "by exercise type" `?pattern=...` cards render blank (High,
  new route family), Profile "Current level: Not assessed yet" (Medium).
  Writing activity confirmed best-working flow. See "Student #2 summary
  (Phase 3)".
- **Phase 4** — Gap coverage: mobile lesson/audio recheck, retry/history UI
  check, vocabulary population check, Persian explanation toggle, admin AI
  Usage in-app nav, admin error/permission handling, admin cross-check of
  student #2, Speaking mic-allowed attempt. Found: vocabulary list empty
  after 3 writing activities (Medium, new), no retry/history UI (Low, new),
  unknown routes redirect authenticated users to `/login` not `/dashboard`
  (Low, new). See "Phase 4 — gap coverage".
- **Phase 5** — Exhaustive Practice Gym activity-type sweep (all 16 visible
  cards). No new bugs; confirmed only 3/8 enabled cards work end-to-end
  (Listening, Writing, Speaking-with-mic-caveat); catalogue gap noted for
  several spec'd activity types with no card. See "Phase 5 — full
  activity-type sweep".
- **Phase 6** — Edge-case sweep: admin create-student form validation
  (invalid email, duplicate email, blank fields), browser back/forward
  through a Practice Gym activity, simulated session expiry mid-submit,
  blank/whitespace-only Writing submission, direct URL manipulation
  (invalid `activityId`/`pattern`), refresh during an in-progress Writing
  activity. One new Low finding (forward-nav state loss); one new
  informational finding (in-memory session survives storage/cookie clear
  until next reload). See "Phase 6 — Edge case testing".

## Accounts used

- Admin account: ehsan.aliverdi@gmail.com (pre-existing admin)
- New persistent audit student: created in this phase (see below)

## Persistent audit students

### Student #1 (Phase 1, locked out — kept for inspection, do not touch further)

- First name: QA
- Last name: FullAudit 20260613-1530
- Email: qa.fullaudit.202606131530@example.com
- Created via: Admin Panel → Students → Create student (optional profile fields expanded)
- Optional profile fields set: career context, learning goal, learning goal description (added during edit test), preferred duration = 30 minutes, experience level = Mid-level (2-5 years), role familiarity = Familiar
- Status after creation: PasswordChangeRequired / NotStarted, CEFR `-`
- Edit test: changed "Learning goal description" field, saved successfully (PUT `/api/admin/students/{id}` → 200)
- Password: set during Phase 1, not stored in this file. Student got locked out via a
  forced first-login password-change flow before the new password was recorded.
  Admin has no password-reset UI (see High bug below). Account is left as-is for
  later inspection; do not log in as this student or modify it further.

### Student #2 (Phase 2, active — current audit account)

- First name: QA
- Last name: FullAudit2 20260613-1530
- Email: qa.fullaudit2.202606131530@example.com
- Password: `QaAudit2#2026!`
- Created via: Admin Panel → Students → Create student (optional profile fields expanded)
- "Require password change on first login" was **unchecked** at creation time
  (deliberately, to avoid repeating student #1's lockout). Onboarding did NOT
  force a password change for this student — login with the original password
  worked throughout the session.
- Optional profile fields set: First name = QA, Last name = "FullAudit2
  20260613-1530" (no other optional fields set at creation)
- Status after creation: OnboardingRequired / NotStarted, CEFR `-`
- Verified: appears correctly in Admin → Students list; Edit/detail view opens
  and shows correct First/Last name fields.
- Final status after this session: onboarding complete, placement complete
  (B2), journey generated, first Today's Lesson ("Understanding and Responding
  to Feedback") completed 4/4 steps (2 steps via real activity flow not
  possible due to bugs below — marked complete via "Mark complete" /
  "Mark step complete" buttons instead). Second Today's Lesson ("Writing
  Concise Professional Emails") generated but not started. Left in this state,
  no cleanup performed.

## Executive summary

**Overall result**: SpeakPath's core learning loop works and, where it
renders, the AI-generated content quality is good to excellent — especially
the Writing activity feedback, which is specific, accurate, and directly
tied to the submitted text. However, **a significant portion of the
activity catalogue is unreachable due to a client-side rendering bug** that
affects multiple route families (lesson teach-pages and Practice Gym
"by exercise type" activities), and **Speaking exercises have no usable path
for any student without microphone permission** — both High-severity,
both span Phases 2 and 3.

**Critical blockers**:
- No admin password-reset / "Forgot password" capability (Phase 1) — any
  student locked out of their account (as happened to student #1) has no
  recovery path. This is the only true blocker found across all 3 phases.

**Highest-risk areas**:
- Blank-activity rendering bug — affects Listening/Vocabulary lesson steps
  (Phase 2) AND all 4 enabled Practice Gym "by exercise type" cards (Phase
  3: Workplace Chat, Fill in the blanks, Email, Matching). Backend
  generation succeeds (200 responses with valid content) in every case;
  the failure is purely in the frontend activity renderer for these content
  shapes/routes.
- Speaking mic-denied dead end — no text fallback in any Speaking activity
  outside the placement assessment (which does have one). Affects lesson
  Speaking steps and Practice Gym Speaking.

**Best-working areas**:
- Writing activities (`?type=WritingScenario`): accurate, submission-aware
  feedback; working retry/next-activity flow; correct blank-submission
  validation; Persian (L1) localization of instructions.
- Listening comprehension (`?type=ListeningComprehension`): working audio
  + transcript + accurate per-question scoring.
- Progress page: real-time, non-stale, accurately aggregates per-activity
  feedback into skill-level trends and recurring-mistake tracking.
- Student-facing mobile responsiveness (375px): no overflow on any of
  Dashboard/Journey/Practice/Progress/Profile/Writing-activity — a clear
  contrast with the admin-side mobile overflow issues.
- Onboarding → placement → journey → dashboard pipeline (Phase 2): works
  end-to-end, generates relevant, non-duplicated content tied to placement
  results.

**Main concerns**:
- The two High bugs above mean a student following the "intended" path
  (lesson steps, or Practice Gym exercise-type cards) will frequently hit
  dead ends, while the "By skill" Practice Gym cards and the placement
  assessment work fine — an inconsistent experience that's hard for a real
  student to work around.
- Minor data-consistency issues: Profile's "Current level" not reflecting
  placement (Medium, Phase 3); dashboard stats lag behind actual progress by
  some short, uncharacterized delay (downgraded from the original Phase 2
  assessment after Phase 3 follow-up showed it self-resolves).
- AI feedback quality is inconsistent across activity types — Writing
  feedback is excellent and submission-aware; Listening's free-text "reply"
  feedback is generic boilerplate.

**Recommended next sprint focus**: fix the blank-activity rendering bug
(both route families) and the Speaking mic-denied fallback first — these are
the two changes that would unblock the largest amount of currently-dead
content for students. Add the admin password-reset capability in parallel
(orthogonal, but unblocks future audit/support work). See "Recommended next
sprint priorities" for the full prioritized list.

**Phase 4 update (gap coverage)**: no new High/Critical bugs found. Phase 4
confirmed the High blank-activity bug also affects lesson Vocabulary and
Writing-task steps at mobile width (375px) — same bug, no new symptom. New
findings: vocabulary list still empty after 3 completed writing activities
(Medium, new), no discoverable retry/history UI for past activities (Low,
new), unknown routes redirect authenticated students to `/login` instead of
`/dashboard` though the session stays valid (Low, new). Admin AI Usage loads
correctly via in-app sidebar click (the Phase 1 bug is specific to the
incorrect direct-URL path `/admin/ai-usage` vs the real route
`/admin/usage`). Admin-side cross-check of student #2 shows correct CEFR
(B2), Lifecycle (ActiveLearning), and Onboarding (Complete) — no mismatch
with student-side state, though admin has no visibility into per-activity
scores/journey progress (gap, not a bug). Speaking with mic permission
granted was not feasible in this headless session (`NotSupportedError` — no
fake-device Chromium flags without a daemon restart); the mic-denied
dead-end UI was re-confirmed to fire for this failure mode too.

**Updated grand totals across all 4 phases**: High: 3 (unchanged — both
blank-activity and Speaking-mic bugs now confirmed at both desktop and
mobile widths). Medium: 5 (was 4, +1 vocabulary-empty). Low: 3 (was 1, +2:
no retry/history UI, unknown-route redirect inconsistency). Process/blocker:
1 (unchanged).

**Phase 5 update (full activity-type sweep)**: no new bugs (totals unchanged
at High: 3, Medium: 5, Low: 3, Process: 1). Exhaustively checked all 8
enabled + 8 "Coming soon" Practice Gym cards. Confirmed only **3 of 8 enabled
cards work end-to-end** (Listening, Writing, and Speaking-with-mic-caveat) —
the other 5 enabled cards (Word cards, Fill in the blanks, Matching, Email,
Workplace chat) are all instances of the existing High "`?pattern=...`
activities render blank" bug. All 8 "Coming soon" cards correctly disabled.
Several activity types from the original spec (Pronunciation, MCQ,
True/False/Not Given, Paragraph Reordering, Sentence Unscrambling, Odd One
Out, Reading Q&A, Grammar) do not exist as Practice Gym cards at all
(catalogue gap, not a bug). See "Phase 5 — full activity-type sweep" section.

**Phase 6 update (edge-case sweep)**: 2 new Low/informational findings, no
new High/Medium bugs. Admin create-student form validation (invalid email,
duplicate email, blank fields) all handled correctly with no
backend-creation side effects. Browser back/forward, blank-form submission,
direct URL manipulation with invalid IDs/patterns, and refresh-during-
in-progress-activity all degrade gracefully (no crashes, no raw errors, no
duplicate submissions). New findings: Forward-navigation to an activity page
shows a transient ~5s blank state visually similar to the existing High
"blank activity" bug before self-resolving (Low, new); an already-loaded SPA
tab can complete one in-flight activity submission after
localStorage/cookies are cleared, though the next reload correctly
re-authenticates (Low/informational, new). See "Phase 6 — Edge case testing".

**Updated grand totals across all 6 phases**: High: 3 (unchanged). Medium: 5
(unchanged). Low: 5 (was 3, +2 from Phase 6: forward-nav transient blank
state, in-memory session outlives storage clear). Process/blocker: 1
(unchanged).

## Admin summary intro (Phase 1)

Admin login and core navigation work correctly. Student CRUD (create + edit)
via the in-app UI works and returns 200/201 from the API. Two confirmed
admin-side bugs found in Phase 1:

1. Deep-linking to `/admin/ai-usage` and `/admin/students/new` (direct URL
   navigation / page refresh) incorrectly redirects to `/login` even though
   the user is authenticated and in-app navigation to the same feature works
   fine via UI links/buttons. This looks like a client-side route-guard issue
   tied to direct navigation/refresh rather than a real auth/session problem
   (API calls succeed, no console/network errors).
2. Mobile (375px) admin Dashboard and Students list have horizontal overflow
   — content/cards are wider than the viewport and get cut off
   (e.g., "Mar...", "Pro...", "ONBOARD..." column headers/cards truncated on
   the right edge). On the Students list, the row "Edit" action could not be
   reliably clicked at mobile width within timeout, suggesting it may be
   off-screen/clipped.

No console errors were observed on any successfully-loaded admin page.

## Admin summary

- Login: works. POST `/api/auth/login` → 200, redirects to `/admin`.
- Admin nav visible: Dashboard, Students, AI Config, Prompts, AI Usage,
  Integrations, Diagnostics. No student-only nav items visible.
- Dashboard (`/admin`): loads, shows Total Students (7 after creation),
  Onboarded count, AI Provider status, Quick actions, Recent students table,
  AI System status panel (Writing/Feedback/Speaking/Listening all Active).
- Students (`/admin/students`): loads, lists students with Lifecycle,
  Onboarding, CEFR, Profile, Joined, Actions (Edit/Archive) columns. "Show
  archived students" checkbox present. No visible search box, column sort
  controls, or pagination — list is small (7 rows) so this may be by design
  for current scale.
- AI Config (`/admin/ai-config`): loads, shows LLM Categories config,
  Default LLM resolution order, TTS voice/provider config section.
- Prompts (`/admin/prompts`): loads, shows versioned prompt templates table
  (key, version, status, token counts, View/Activate/Deactivate actions).
- AI Usage (`/admin/ai-usage`): **BUG** — direct navigation redirects to
  `/login` (see Bugs section).
- Integrations (`/admin/integrations`): loads, shows MinIO/Object Storage
  config fields and "Test connection" + Lesson Buffer section.
- Diagnostics (`/admin/diagnostics`): loads, shows System status
  (Environment: Production, Version 1.0.0, Uptime, Log level, Database
  reachability, AI provider status) with Refresh action.
- Create student flow: works end-to-end via UI (Students → Create student →
  fill required + optional fields → Create student). POST
  `/api/admin/students` → 201.
- Create student form (`/admin/create-student`, Phase 2 observation): default
  fields are Student email, Temporary password, and a checkbox "Require
  password change on first login" (checked by default). Optional profile
  fields (First name, Last name, Display name, Career context, Learning goal,
  Preferred session duration, Experience level, Role familiarity) are hidden
  behind "Add optional profile fields". The "Require password change on first
  login" checkbox being checked by default is the likely root cause of student
  #1's lockout (see High bug below) — unchecking it for student #2 avoided a
  repeat.
- Edit student flow: works via inline edit form opened from the Students
  table Edit button. PUT `/api/admin/students/{id}` → 200.

## Student summary (Phase 2 — BLOCKED, no admin password-reset capability)

Admin logged in successfully (`POST /api/auth/login` → 200, role=Admin). Opened
Admin → Students → Edit for `qa.fullaudit.202606131530@example.com`. The inline
edit form contains only: First name, Last name, Display name, Career/work
context, Learning goal, Learning goal description, Difficult situations,
Preferred duration, Experience level, Role familiarity, Cancel/Save changes.

**There is no password field, "Reset password", or "Send password reset" action
anywhere in the admin UI** — confirmed via full DOM inspection
(`document.querySelectorAll('input')` returns only 6 fields, none of type
`password`, across the entire `/admin/students` page including the open edit
form). The only password-related code path in the client bundle is
`/change-password` (`ChangePasswordComponent`, `mustChangePassword` flag) — a
self-service flow triggered on first login, not an admin-initiated reset.

No swagger/OpenAPI endpoint is exposed (`/api/swagger/v1/swagger.json` → 404,
confirmed in Diagnostics event log) to check for an undocumented admin
reset-password API, and per the audit's safety scope, calling unlisted/raw API
endpoints from outside the documented admin UI was not attempted.

**Phase 2 remains blocked** — see new High-severity bug below
("Admin has no way to reset a student's password"). Original blocker text below
retained for history.

### Original Phase 1 attempt history (retained)

Phase 2 could not start. The audit student account
(`qa.fullaudit.202606131530@example.com`) no longer accepts the password
recorded in Phase 1 (`QaAudit#2026_P1`):

- `POST /api/auth/login` → 401 "Invalid credentials" with that password.

Evidence that a *prior* Phase 2 attempt (the one that hit the spend-limit
error before saving results) got much further than this file shows: the
artifacts directory `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/`
already contains screenshots numbered 06-13, covering:

- `06-placement-listening-audio-unavailable.png` — placement listening
  section, audio appears unavailable
- `07-placement-result-b1.png` — placement result screen, estimated level B1
- `08-dashboard-onboarding-message.png` — Today dashboard after onboarding,
  showing placement summary (B1 estimate, strengths grammar/vocab/reading B1,
  areas to develop writing/speaking/workplaceTone A2+), course "Daily Office
  Interactions" 0/3 modules done, "Start today's lesson" CTA for "Speaking
  with Clarity and Conciseness" (3 steps, verbal professional communication)
- `09-journey-page.png` — Journey page
- `10-today-lesson-overview.png` — Today's lesson overview
- `11-activity-mic-denied-no-fallback.png` — activity with mic permission
  denied and apparently no fallback
- `12-lesson-progress-1of3.png` — lesson progress 1/3 steps
- `13-lesson-complete.png` — lesson marked Completed, 3/3 steps done,
  "Lesson complete!" screen shown

So onboarding, placement (result: B1), journey generation, and at least one
Today's Lesson ("Speaking with Clarity and Conciseness") were already
completed by the prior attempt before it hit the spend-limit error. Most
likely the onboarding/first-login flow forced a password change for this
student, and the new password was never recorded before the spend-limit
error cut off that session — so the original temp password
(`QaAudit#2026_P1`) is now stale and the actual current password is unknown.

This Phase 2 attempt tried the original password and several plausible
variants (`QaAudit#2026_P2`, `QaAudit#2026_New1`, `QaAudit#2026_Student1`,
`QaStudent#2026_P2`) — all returned 401. No admin credentials were available
to this session to reset the student's password via the admin panel.

**Phase 2 is blocked until the orchestrator either:**
1. Supplies the current/changed password for
   `qa.fullaudit.202606131530@example.com`, or
2. Supplies admin credentials so this session can reset the student's
   password via Admin → Students → Edit, or
3. Confirms it's OK to treat the prior attempt's progress (onboarding,
   placement B1, journey, lesson 13 completed) as Phase 2's de facto result
   based on the existing screenshots, with Phase 2 limited to a
   login-blocked note + review of those screenshots only.

No new testing, no new bugs, no new timings were captured in this attempt
beyond the login failure itself.

## Student #2 summary (Phase 2 — completed)

Login as `qa.fullaudit2.202606131530@example.com` / `QaAudit2#2026!` succeeded
(`POST /api/auth/login` → redirect to `/onboarding/step-1`, ~1.4s). Student-only
nav confirmed: Today, Journey, Practice, Progress, Profile — no Admin links.
Direct navigation to `/admin` while logged in as this student redirects cleanly
to `/onboarding/step-1` (no crash, no console error related to the redirect
itself) — admin route guard works correctly for students.

### Onboarding (5 steps)

- Step 1 (Choose language path — Persian → English): only option, select and
  Continue. **Bug**: selecting the path and then refreshing the page, or
  navigating Back from step 2 to step 1, resets the selection and disables
  Continue again (see Bugs).
- Step 2 (Preferred session duration): 4 options (10/15/20/30 min). Selected
  20 minutes. Back/Continue navigation works (modulo the reset bug above).
- Step 3 (Career/workplace context): free-text + quick-pick chips (Junior
  software engineer, Project planner, Nurse, Customer support officer,
  Document controller). Clicking a chip fills the field and enables Continue.
  "Continue" with empty field is disabled (validation present).
- Step 4 (Skill goals + difficulty description): multi-select chips (Writing,
  Speaking, Vocabulary, Listening) + free-text "what do you struggle with"
  field. Selected Writing + Speaking, entered a description. "Next" was
  enabled even with nothing selected (no enforced minimum — minor, not logged
  as a bug since multi-select-optional is a defensible product choice).
- Step 5 (Experience level + role familiarity + "Continue to assessment" /
  "Skip for now"): selected Mid-level (2-5 years) + "Currently working in this
  role", clicked "Continue to assessment".

Onboarding → placement transition completed in ~730ms. No forced
password-change step occurred for this student (confirmed by completing the
whole session, including a later page-context switch, without re-prompting for
credentials).

### Placement assessment (6 sections)

1. **Quick self-check** — 4 confidence sliders (1-5) on writing
   emails/explaining/reading/understanding meetings, optional free-text
   "main workplace challenge", optional self-estimated CEFR level (A1-C2 /
   Not sure). Answered with mixed ratings (2,2,3,3) + free text + "B1" guess.
2. **Vocabulary and grammar** — 6 multiple-choice questions (politeness,
   collocations, verb forms, register). Answered 5 correctly, 1 deliberately
   wrong ("He don't have time today." instead of "He doesn't have time
   today.").
3. **Reading comprehension** — realistic workplace email from "Sara (Team
   Lead)" with 3 comprehension questions. Answered all correctly.
4. **Listening comprehension** — voice message from "Daniel" about moving a
   meeting, with transcript fallback shown alongside the audio player and 3
   comprehension questions. **TTS audio loaded successfully this time**
   (blob URL, `duration: 15.21s`) — see TTS findings below. Answered all 3
   questions correctly.
5. **Writing task** — "write a short professional reply" (80-120 words) to a
   colleague asking for a report. Submitted a deliberately imperfect reply
   (grammar errors: "report is not ready yet, I need one more day for finish
   it. I sending tomorrow morning to you...").
6. **Speaking task** — "record a 30-60s spoken response" describing a typical
   work task. Clicking "Tap to record" in this headless environment denies mic
   access, but **this section correctly showed a text fallback** ("Microphone
   access was denied. Type your response instead.") with a textbox. Submitted
   a short imperfect typed response.

After section 6, "Finish and see my result" → "Working out your level…" →
result screen after ~8-9s:

- **Estimated level: B2**
- Skill breakdown: Grammar accuracy A2+, Workplace vocabulary A2+, Reading
  comprehension B2+, Listening comprehension B2+, Writing B1+, Speaking B1+,
  Formal workplace tone B1+
- Strengths: reading (B2+), listening (B2+), writing (B1+), speaking (B1+)
- Areas to improve: grammar (A2+), vocabulary (A2+)
- Recommended focus: Workplace English B2, recommended session length 15
  minutes

Result is plausible given the deliberately mixed answers (intentionally wrong
grammar answer + imperfect writing/speaking responses correlate with the lower
grammar/vocabulary sub-scores; correct reading/listening answers correlate with
the higher sub-scores). "Continue" → redirected to `/dashboard` in ~760ms.

### Journey

`/journey` loads a "Learning Journey" page with:

- Learning focus summary (same strengths/areas-to-improve text as placement
  result)
- "Practise next": grammar (A2+), vocabulary (A2+)
- "Session history" list of 5 generated sessions, all "Not started":
  1. Understanding and Responding to Feedback — Listening, 4 steps
  2. Writing Concise Professional Emails — Writing, 4 steps
  3. Participating in Team Meetings Effectively — Speaking, 4 steps
  4. Grammar for Clear Workplace Communication — Grammar, 4 steps
  5. Essential Workplace Vocabulary for Professionals — Vocabulary, 4 steps

Content is relevant to the placement result (grammar/vocabulary sessions
present to address the identified weak areas), titles are sensible and
distinct, no duplicates, no placeholder/malformed text observed.

### Today's Lesson — "Understanding and Responding to Feedback"

Dashboard showed "Today's lesson: Understanding and Responding to Feedback,
25 min, listening focus, 4 steps, Not started". Opened lesson detail
(`/lesson/669caf31-...`), goal text shown, "Start lesson" → status becomes "In
progress", progress 0/4.

- **Step 1 (Listening, 7 min)**: "Load activity" → ~16-17s generation →
  "Open activity" appears. Opening the activity navigated to
  `/activity?activityId=...&returnTo=...`, but **the activity content area
  rendered completely blank** (only the "1 Lesson / 2 Practice / 3 Feedback"
  stepper, no lesson text/audio player/questions), despite
  `GET /api/activity/{id}` → 200 (2049B) and
  `GET /api/activity/{id}/audio` → 200 (1.39MB) both succeeding. Reload did not
  fix it. No console errors. **High bug** — see Bugs. Worked around by going
  back to the lesson page and clicking "Mark complete" directly, which
  advanced progress to 1/4.
- **Step 2 (Speaking task, 10 min)**: "Load activity" (~10s) → "Open activity"
  → this time the activity rendered correctly with full scenario text
  ("Feedback on Presentation Style", role-play prompt, suggested phrases) and
  a "Start recording" button. Clicking it (mic denied in this environment)
  showed "🔇 Microphone access denied ... Please allow microphone access ...
  Try again / Back to Today" — **no text-input fallback was offered**, unlike
  the placement speaking section which did offer one. **High bug** — see Bugs.
  This is a dead end for any student without a working microphone/permission.
  Worked around via "Mark complete" on the lesson page, advancing to 2/4.
- **Step 3 (Vocabulary warm-up, 5 min)**: "Load activity" (~10s) → "Open
  activity" → **same blank-activity-page bug as step 1** (vocabulary
  matching/teach-page content did not render; only the stepper showed).
  Worked around via "Mark complete", advancing to 3/4.
- **Step 4 (Lesson review, 3 min)**: no separate "Load activity" — instead a
  reflection prompt with a "Mark step complete" button directly on the lesson
  page. Clicked it → lesson status became "✓ Completed", progress 4/4, with a
  "🎉 Lesson complete!" message and "Back to dashboard" link.

**Refresh persistence**: reloaded `/lesson/669caf31-...` after completion —
status remained "Completed", progress remained "4/4 steps". Persistence works
correctly for lesson/step completion state.

**Dashboard after completion**: returning to `/dashboard` showed a **new**
Today's Lesson ("Writing Concise Professional Emails", 30 min, writing focus,
4 steps, Not started) — journey/today-lesson rotation works. However, "Streak"
still showed "0 day streak", "Practice 0 activities done", and "Score --
average" — these summary stats did not update despite a full lesson being
completed. **Medium bug** — see Bugs.

## Student #2 summary (Phase 3 — Practice, Progress, Profile, mobile)

Login as `qa.fullaudit2.202606131530@example.com` / `QaAudit2#2026!` succeeded
again at the start of Phase 3 (a prior session had expired — `GET` calls
returned 401 until re-login via `/login` form, then redirected straight to
`/dashboard`).

### Practice Gym — "By skill" cards

- **Listening**: card links to `/activity?type=ListeningComprehension`.
  Renders correctly — full scenario text, audio player (`blob:` URL,
  `duration: 16.93s`), "Answer questions" button. **Important timing note**:
  on first click this page can appear blank for up to ~15-16s while the
  activity generates (same visual symptom as the High blank-activity bug),
  but **resolves on its own** once generation finishes — no reload needed.
  After "Answer questions", 3 comprehension questions + 1 free-text "reply"
  question appear. Submitted a mix: 1 correct ("Alex"), 1 wrong ("I don't
  know" for Alex's role), 1 partial ("name and role" vs expected "Current
  role and one key project"), plus a short imperfect free-text reply ("Hi
  Sarah, I recieve your message, I will prepare my introduction for tomorrow
  meeting."). Result: **score 48/100**, per-question grading was accurate
  (correct/wrong/partial all graded as expected), transcript revealed after
  submit, a "Lesson" tip and "Next step to try" shown. **AI quality note**:
  the "Response feedback" for the free-text reply was generic boilerplate
  ("Your reply should confirm the task, mention the timeline if relevant,
  and keep a polite workplace tone.") and did not reference the actual
  submitted text or its typo ("recieve") — see AI quality findings.
- **Reading**: card shows "Coming soon", rendered as a non-interactive
  `[text]` element (not a link) — correctly disabled, no broken navigation.
- **Speaking**: card links to `/activity?type=SpeakingRolePlay`. Renders
  correctly — full scenario ("Introducing a New Team Member", role,
  audience, goal, task, suggested phrases), "Start recording" button.
  Clicking "Start recording" with mic denied shows the same
  "🔇 Microphone access denied ... Try again / Back to Today" dead end as
  the lesson Speaking step in Phase 2 — **no text fallback in Practice Gym
  either**, confirming this is the same High bug, not lesson-specific.
- **Writing**: card links to `/activity?type=WritingScenario`. Renders
  correctly — scenario in English + Persian (L1) translation, target
  phrases, "Watch out" common-mistake note, "See an example" sample reply,
  "Start writing →" button. This was the **best-working flow in the whole
  audit** (see below).
- **Vocabulary class → Word cards**: links to `/vocabulary`. Page itself
  loads correctly (no console errors) but shows an empty state — "No
  vocabulary yet... Your vocabulary list will grow as you complete writing
  activities." After completing 2 writing activities in this session, the
  vocabulary list was not re-checked for population (would require revisit —
  noted as a gap).
- **Vocabulary queue**: "Coming soon", correctly disabled (`[text]`, not a
  link).

### Practice Gym — "By exercise type" cards

- **Fill in the blanks** (`?pattern=gap_fill_workplace_phrase`), **Matching**
  (`?pattern=phrase_match`), **Email** (`?pattern=email_reply`), **Workplace
  chat** (`?pattern=teams_chat_simulation`): all four enabled
  "by exercise type" cards link to `/activity?pattern=<name>&returnTo=/practice`
  routes. **All four render permanently blank** — only the "1 Lesson / 2
  Practice / 3 Feedback" stepper shows, no exercise content, even after 15s+
  wait and a full page reload. `GET /api/activity/next?pattern=<name>` returns
  200 with a non-empty JSON body (e.g. 1834B for `teams_chat_simulation`), so
  the backend is generating/serving content correctly — this is purely a
  client-side rendering failure. **New High bug** — see Bugs (this is the
  same underlying symptom as the existing "teach page renders blank" bug, but
  affects the `?pattern=` route family used by ALL FOUR currently-enabled
  "by exercise type" Practice Gym cards, i.e. the entire exercise-type
  section of Practice Gym is unusable).
- **Multiple choice, Sentence transformation, Error correction, Word
  formation, Unscrambling**: all "Coming soon", correctly rendered as
  non-interactive `[text]`, not links.
- **AI role play** (Live practice section): "Coming soon", correctly
  disabled.

### Writing activity deep dive (best-working flow)

Submitted a deliberately imperfect 47-word email reply (errors: "writing for
introduce", "she will joining", "Sarah have many experience", lowercase
"monday", "when she arrive"). Result:

- Score: **75/100**, label "Good effort", "What you did well" (3 specific
  positive points referencing the actual submission: subject line, naming
  Sarah's role, tone).
- "Suggested changes" — 5 before/after grammar corrections, **all exactly
  matching the 5 errors actually present** in the submitted text, each with
  a correct, specific grammar explanation (e.g. "to + infinitive after verbs
  like 'write'", "future continuous for planned actions", "experience is
  uncountable — use 'a lot of' not 'many'", "days of the week are
  capitalized", "'join me in welcoming' idiom + 3rd person 's'").
- "Show Persian explanation" toggle present (not expanded/verified).
- "Suggested improved version" — full corrected email shown, all 5 issues
  fixed correctly.
- "Rewrite challenge" — a targeted follow-up prompt referencing the
  submission's content (Sarah's experience sentence).
- Buttons: "Improve my answer →", "Try again from scratch", "Next activity",
  "Back to Today" — all present and enabled.
- "Next activity" generated a **new, distinct** writing scenario ("Ms. Sara
  Ali" instead of "Sarah Chen") — different name/details, same task type, no
  duplication, content remained relevant to the B2/workplace-introductions
  focus.
- On the new activity, "Get coach feedback" was correctly **disabled** with
  an empty textbox (blank-submission validation works).

This is the single highest-quality, most accurate, most useful flow observed
across all 3 phases — feedback is specific, correct, and directly tied to the
submitted text.

### Progress page

`/progress` loads with **no console errors** and shows:

- Summary cards: "2 activities completed", "62 average score", "48 latest
  score", "2 this week", "0 modules completed", "0 retry attempts".
- "Recent scores": both activities from this session listed correctly —
  48 ("Introducing New Colleagues: A Quick Update", Attempt 1, Effective
  Workplace Introductions, 13 Jun) and 75 ("Introducing a New Team Member
  Email", Attempt 1, same module, 13 Jun). No duplicates from retries (the
  "Next activity" writing flow generated a *new* activity rather than
  retrying the same one, so no retry-duplication was directly exercised, but
  the "0 retry attempts" counter is consistent with that).
- "Skill progress": Strengths (Clarifying questions, Concise writing, Formal
  workplace tone) and Areas to improve (Grammar accuracy, Workplace
  vocabulary) — consistent with the placement result and the grammar-heavy
  feedback received in this session.
- "Module progress": "Effective Workplace Introductions — In progress, 2/3
  activities, Avg 62, Latest 48", plus 5 other modules listed as "Upcoming,
  0/3 activities" (Participating in Team Meetings, Presenting Information
  Clearly, Handling Workplace Conflicts, Networking & Professional
  Relationships, Generated Lessons) — relevant, no placeholders/duplicates.
- "Your learning focus": placement summary + a new sentence reflecting this
  session's activity ("Practiced workplace introductions via email.
  Continued to focus on grammar accuracy with good progress.") and "Next
  focus: grammar_accuracy (A2+)".
- "Recurring mistakes to watch": 3 items, all **directly traceable** to the
  actual grammar corrections from the Writing activity feedback above
  ("incorrect verb after 'write' for purpose", "incorrect tense for future
  planned actions", "countable/uncountable noun error ('experience')") —
  strong evidence the system aggregates real per-activity feedback into
  longer-term skill tracking.

**Cross-check against Bug #4 (stale dashboard stats)**: Progress page is
**NOT stale** — it reflects both activities completed in this session
immediately. Re-checking `/dashboard` after the Progress page visit also now
showed updated stats: "Practice 2 activities done", "Score 62 average",
"Module progress 2/3 done" (previously 0/3 in Phase 2 right after lesson
completion). This suggests the Phase 2 dashboard-staleness issue may be a
**propagation delay / cache** rather than a permanent failure to record
stats — the dashboard did catch up after additional activity + page
navigations. "Streak" still shows "0 day streak" even with 2 activities
completed today; this may be correct product behavior (streak likely requires
multiple distinct days) rather than a bug — downgrading confidence on the
streak portion of Bug #4 (see updated bug entry).

### Profile page

`/profile` loads with no console errors and shows:

- Identity: avatar "Q", email `qa.fullaudit2.202606131530@example.com`,
  "Student · Persian to English".
- Learning: "Learning goal: Workplace English", "**Current level: Not
  assessed yet**", "Practising: Writing · Listening · Vocabulary".
- Settings: "Daily reminder: Not configured", "Notifications: Not
  configured".
- "Sign out" button present.

**New Medium bug**: "Current level: Not assessed yet" is incorrect — the
placement assessment was completed in Phase 2 with an estimated level of
**B2**, and both Journey and Progress pages correctly show B2-based skill
breakdowns and module recommendations. The Profile page's "Current level"
field appears to not be wired to the placement result. See Bugs.

No editable profile fields were found (read-only view) — "Test editable
fields only if safe" from the spec did not apply; nothing to test.

**Sign out**: clicking "Sign out" via Playwright's normal click timed out
("Element not found or not interactable") — likely a minor
visibility/overlap quirk with the profile dropdown, not necessarily a
product bug. Triggering the same button's `click()` via JS worked: redirected
cleanly to `/login`, session cleared. Logout itself works correctly once
triggered. Re-logged in afterward, leaving the account in a normal
logged-in state (see final account state below).

### Mobile/responsive (375x812)

Checked: `/dashboard`, `/journey`, `/practice`, `/progress`, `/profile`, and
`/activity?type=WritingScenario` (including filling the response textarea and
confirming "Get coach feedback" enables).

- All 5 student pages plus the Writing activity page: `scrollWidth ===
  clientWidth === 375` — **no horizontal overflow** on any student-facing
  page at 375px (contrast with the admin Dashboard/Students-list overflow
  bug from Phase 1).
- Bottom mobile nav bar present (`data-testid="mobile-nav-progress"` etc.)
  and functional.
- Writing activity at 375px: scenario text, target phrases, "Watch out" note,
  example reply, and the response textarea + "Get coach feedback" button all
  render correctly with no clipping; filling the textarea correctly enables
  the button.
- No mobile-specific console/network errors observed (other than the
  pre-existing 401s from the expired-session re-login at the start of Phase
  3, which were resolved by re-login and unrelated to viewport).

Overall, **student-facing pages are responsive and mobile-friendly** — this
is a clear contrast with the admin-side mobile overflow issues found in
Phase 1.

## Phase 4 — gap coverage

Final cleanup phase, covering items not exercised in Phases 1-3. Student #2
(`qa.fullaudit2.202606131530@example.com` / `QaAudit2#2026!`) and admin
(`ehsan.aliverdi@gmail.com`) both used, both left in a normal logged-in state
afterwards. Student #1 was not touched.

### 1. Mobile/responsive (375x812) — see "Responsive/mobile findings" below
for the full writeup. Summary: no overflow on any student page tested,
including a full lesson page and a Practice Gym Listening activity with audio.
The existing blank-activity rendering bug reproduces identically at 375px
(no new mobile-specific symptom).

### 2. Retry/history duplication check

- Completed a third Writing activity (`?type=WritingScenario`) this session
  (score 65, "Attempt 1"). `/progress` immediately reflected it correctly:
  "3 activities completed", "63 average score", "65 latest score", "3 this
  week", "1 modules completed", **"0 retry attempts"**. "Effective Workplace
  Introductions" module is now **Completed, 3/3 activities, Avg 63, Latest
  65** — up from 2/3 in Phase 3, no duplicates, all 3 "Recent scores" entries
  distinct (65, 48, 75, all "Attempt 1", all dated 13 Jun).
- **No "Retry" action was found** on `/progress` — the "Recent scores" list
  items are plain text/non-interactive (no @e/@c refs beyond page nav).
- On `/journey`, the completed session ("Understanding and Responding to
  Feedback — Done, Sat 13 Jun 2026") is rendered as a `cursor:pointer` div but
  clicking it does not navigate or open any detail/retry view — it's a dead
  click. No activity-history/attempt-detail view exists for completed journey
  sessions.
- The only retry-like control found anywhere is the **"Try again from
  scratch"** button shown on a Writing activity's feedback screen (seen in
  Phase 3 and again in Phase 4) — this was not clicked in Phase 4 to avoid
  disturbing the now-cleanly-completed module state. The "0 retry attempts"
  counter on `/progress` is consistent with this control never having been
  exercised across all 4 phases.
- **Conclusion**: no retry-duplication bug found, but also **no
  discoverable retry/history UI** for a student who wants to review or redo a
  past completed activity from `/progress` or `/journey` — only the
  in-the-moment "Try again from scratch" button on a fresh activity's feedback
  screen. Logged as a Low/UX gap, not a correctness bug (see Bugs).

### 3. Vocabulary list population

- `/vocabulary` re-checked after this session's 3rd writing activity (total 3
  Writing-type activities completed across Phases 3-4, scores 48/75/65 — note
  48 was actually the Listening activity's free-text reply, so 2 true Writing
  activities + this session's 3rd).
- Page still shows **"No vocabulary yet... Your vocabulary list will grow as
  you complete writing activities."** — empty state, no console errors,
  `scrollWidth === clientWidth === 375` (also checked at mobile).
- **Confirmed as a real gap**: after 2-3 completed Writing activities with
  rich AI feedback (including target phrases, corrections, suggested
  improved versions), no vocabulary items have been extracted/populated.
  Either the feature is not yet implemented end-to-end, or the
  population trigger/criteria differ from "complete a writing activity" as
  the empty-state text implies. See Bugs (Medium).

### 4. Persian (native-language) explanation toggle

- Completed a new Writing activity (`?type=WritingScenario`, score 65,
  "Introducing a New Team Member Email" theme — John from marketing).
  Feedback screen showed "Show Persian explanation" button.
- Clicked it → button label flips to "Hide Persian explanation" and a
  "توضیح به فارسی" (Persian explanation) section appears with correctly
  rendered, grammatically coherent Farsi text summarizing the feedback
  ("ایمیل شما در انتقال پیام اصلی بسیار واضح است..." — "Your email is very
  clear in conveying the main message...").
- **Works correctly** — proper RTL Farsi text, not broken/empty/mojibake. No
  console errors.

### 5. Admin AI Usage page via in-app navigation

- Logged in as admin (lands on `/admin`). Sidebar shows "AI Usage" link under
  "AI System" section. Clicked it (in-app, not direct URL).
- **Loads correctly**: navigated to `https://speakpath.app/admin/usage`
  (note: different path from the `/admin/ai-usage` URL tested via direct nav
  in Phase 1), heading "AI Usage", "Token usage, cost, provider performance,
  and fallback rates", summary cards (Total calls 404, Success rate 92.3%,
  Failed calls 31, Fallback calls 0, Total cost $0.0000), "By provider" table
  (gemini 377 calls/373 OK, openai 27 calls/0 OK), "By feature" table. No
  console errors.
- **Conclusion**: in-app sidebar navigation to AI Usage works fine. The
  Phase 1 bug is specifically about direct/refresh navigation to the URL
  `/admin/ai-usage` (which doesn't match the real route `/admin/usage`) —
  likely a stale/incorrect link somewhere, or simply a non-existent route
  that 1:1 maps to "redirect to login" under the catch-all. The in-app nav
  itself is healthy.

### 6. Admin error/permission handling

- **Logged out, direct nav to `/admin/students`**: redirects cleanly to
  `/login`, with an extra contextual message: "Pilot access for SpeakPath
  students and admins. Please sign in with an admin account to continue." No
  stack trace, no console errors.
- **Logged out, direct nav to `/this-does-not-exist-404`**: also redirects
  cleanly to `/login` (plain "Sign in" form, no extra message). No stack
  trace, no 404 page exists — unauthenticated unknown routes fall back to
  `/login`.
- **Logged in as student #2, direct nav to `/admin/students`**: redirects
  cleanly to `/dashboard`, with an inline message **"You do not have
  permission to use the admin area."** shown above the normal dashboard
  content. No crash, no console errors — clean, friendly block.
- **Logged in as student #2, direct nav to `/this-does-not-exist-404`**:
  redirects to `/login` (plain sign-in form) rather than `/dashboard` or a
  404 page. However, the student's session remained valid afterward —
  navigating back to `/dashboard` immediately worked without re-login. So
  this is a **route-guard quirk** (unknown routes always bounce to `/login`
  regardless of auth state, even though the session itself is untouched) —
  not a real logout and not a stack trace, but inconsistent with the
  `/admin/students` case (which correctly recognizes the authenticated
  student and shows a permission message instead of `/login`). Logged as Low
  (see Bugs).
- **Conclusion**: no raw stack traces or unhandled errors anywhere. The
  known admin routes have proper permission/redirect handling; the unknown
  catch-all route's behavior is inconsistent (sometimes `/login`, sometimes
  `/dashboard` with a message) but never unsafe.

### 7. Admin cross-check of student #2

- Admin → Students list shows "QA FullAudit2 20260613-1530"
  (`qa.fullaudit2.202606131530@example.com`), Lifecycle **"ActiveLearning"**,
  Onboarding **"Complete"**, CEFR **"B2"**, Profile "Junior software
  engineer", Joined "Jun 13, 2026" — **CEFR B2 matches the student-side
  placement result** (B2) from Phase 2.
- Opened Edit panel for this student: shows First name "QA", Last name
  "FullAudit2 20260613-1530", Career/work context "Junior software engineer",
  Learning goal description "I struggle writing professional emails and
  speaking up in meetings.", Preferred duration "20 minutes" (selected) —
  all consistent with onboarding answers from Phase 2.
- **Gap (not a mismatch)**: the admin Edit panel only exposes profile fields
  (name, career context, learning goal, duration, experience level, role
  familiarity) — it does **not** show journey/module progress, completed
  activities, or scores (the 48/65/75 scores, "Effective Workplace
  Introductions 3/3" module completion, etc. are not visible anywhere in the
  admin UI). The Students list's "CEFR" and "Lifecycle"/"Onboarding" columns
  are the only progress-related signals available to admins, and both are
  **correct** (B2, ActiveLearning, Complete). No mismatch found in what IS
  exposed; the level of admin visibility into per-student activity history
  is a product gap, not a bug, and is consistent with there being no
  activity-history view on the student side either (see item 2 above).

### 8. Speaking with mic permission granted

- Attempted to grant mic access via CDP (`Browser.grantPermissions`) — denied
  by the browse tool's CDP allowlist (method not audited/allowed).
- Attempted `navigator.mediaDevices.getUserMedia({audio:true})` directly in
  the page context (no permission grant) — returned
  **`NotSupportedError`** (not `NotAllowedError`), because this headless
  Chromium instance was launched without `--use-fake-device-for-media-stream`
  / `--use-fake-ui-for-media-stream`, and those flags can't be added to an
  already-running browse daemon without a restart that would drop the
  logged-in session/cookies for both student and admin accounts built up over
  4 phases.
- Given the cost/risk of restarting the daemon for one item, and per the
  task's own guidance ("if not feasible, report why and move on"), this was
  **not attempted via daemon restart**.
- For completeness, re-confirmed the existing mic-denied/unsupported dead end
  on `?type=SpeakingRolePlay`: clicking "Start recording" shows "🔇
  Microphone access denied — SpeakPath needs microphone access..." with only
  "Try again" / "Back to Today", no text fallback — same High bug as Phases
  2-3, now also confirmed to fire on `NotSupportedError` (no MediaRecorder
  API / no real device) in addition to `NotAllowedError` (permission
  denied) — i.e. the same dead-end UI is shown for two different underlying
  getUserMedia failure modes, neither of which has a fallback.
- **Conclusion**: a true "mic granted, can it record/submit/get evaluated"
  test was not feasible in this session. Recommend a dedicated follow-up
  using `browse --headed` with `--use-fake-device-for-media-stream
  --use-fake-ui-for-media-stream` (or a fresh daemon) specifically for this
  check, since it requires a different Chromium launch config than the rest
  of this audit.

## Summary table of pages tested

| Page / Route | Method | Result | Console errors | Network errors | Notes |
|---|---|---|---|---|---|
| `/` (landing) | UI nav | OK | none | none | Sign in links work |
| `/login` | UI nav | OK | none | none | Email/password form |
| `/admin` (Dashboard) | post-login redirect, refresh | OK | none | none | Mobile overflow (see Responsive) |
| `/admin/students` (Students list) | UI nav, direct goto | OK | none | none | Mobile overflow (see Responsive) |
| `/admin/create-student` (via Create student link) | UI nav | OK | none | none | Direct goto to `/admin/students/new` fails (see Bugs) |
| `/admin/ai-config` | UI nav, direct goto | OK | none | none | |
| `/admin/prompts` | UI nav, direct goto | OK | none | none | |
| `/admin/ai-usage` | direct goto | **FAIL** | none | none (200 then redirect) | Redirects to `/login` even when authenticated |
| `/admin/integrations` | UI nav, direct goto | OK | none | none | |
| `/admin/diagnostics` | UI nav, direct goto | OK | none | none | |
| Student create (form submit) | UI action | OK | none | none | POST `/api/admin/students` → 201 |
| Student edit (inline form submit) | UI action | OK | none | none | PUT `/api/admin/students/{id}` → 200 |
| `/login` (student) | UI action | **FAIL** | 401 error logged | `POST /api/auth/login` → 401 | Phase 2 blocked (student #1): stale/unknown password (see Student summary) |
| `/login` (student #2) | UI action | OK | none | `POST /api/auth/login` → 200 (redirect to onboarding) | ~1.4s |
| `/admin` (direct nav as student) | direct goto, while logged in as student #2 | OK | none | `GET /api/dashboard` → 403 (expected, pre-onboarding) | Cleanly redirects to `/onboarding/step-1`, no crash |
| `/onboarding/step-1`..`/onboarding/step-5` | UI flow | OK (with bug) | none | none | Path-selection reset on refresh/back (see Bugs) |
| `/placement` | UI flow | OK | none | none | All 6 sections completed, result B2 |
| `/dashboard` | post-placement redirect, refresh | OK (with bug) | none | none | Streak/Practice/Score stats stale after lesson completion (see Bugs) |
| `/journey` | UI nav | OK | none | none | 5 sessions, relevant, no duplicates |
| `/lesson/{id}` (Today's Lesson) | UI nav | OK | none | none | 4/4 steps completed (2 via workaround) |
| `/activity?activityId=...` (Listening step) | UI nav (Open activity) | **FAIL** | none | API 200s but UI blank | Blank activity content (see Bugs) |
| `/activity?activityId=...` (Speaking step) | UI nav (Open activity) | OK (with bug) | none | none | Renders correctly; mic-denied has no text fallback (see Bugs) |
| `/activity?activityId=...` (Vocabulary step) | UI nav (Open activity) | **FAIL** | none | API 200s but UI blank | Same blank-activity bug as Listening step |
| `/login` (student #2, Phase 3 re-login) | UI action | OK | 401s before re-login (expired session) | `POST /api/auth/login` → 200 | Re-login after expired session worked cleanly |
| `/practice` (Practice Gym) | UI nav | OK | none | none | All cards listed, Coming soon cards correctly disabled |
| `/activity?type=ListeningComprehension` (Practice Gym Listening) | UI nav (card click) | OK (slow) | none | none | ~16s to render; audio + questions work, scoring accurate |
| `/activity?type=SpeakingRolePlay` (Practice Gym Speaking) | UI nav (card click) | OK (with bug) | none | none | Renders correctly; mic-denied has no text fallback (same High bug) |
| `/activity?type=WritingScenario` (Practice Gym Writing) | UI nav (card click) | OK | none | none | Best-working flow; accurate feedback, "Next activity" works |
| `/activity?pattern=teams_chat_simulation` (Workplace Chat) | UI nav (card click) | **FAIL** | none | API 200 but UI blank | Permanently blank, same as teach-page bug |
| `/activity?pattern=gap_fill_workplace_phrase` (Fill in the blanks) | UI nav (card click) | **FAIL** | none | API 200 but UI blank | Permanently blank |
| `/activity?pattern=email_reply` (Email) | UI nav (card click) | **FAIL** | none | API 200 but UI blank | Permanently blank |
| `/activity?pattern=phrase_match` (Matching) | UI nav (card click) | **FAIL** | none | API 200 but UI blank | Permanently blank, even after reload + 15s wait |
| `/vocabulary` | UI nav (card click) | OK | none | none | Loads, empty state, no errors |
| `/progress` | UI nav | OK | none | none | Real data, not stale, matches session activity |
| `/profile` | UI nav | OK (with bug) | none | none | "Current level: Not assessed yet" despite B2 placement |
| Mobile (375px): `/dashboard`, `/journey`, `/practice`, `/progress`, `/profile`, `/activity?type=WritingScenario` | viewport 375x812 | OK | none | none | No horizontal overflow on any student page |
| Sign out | UI action | OK | none | none | Clears session, redirects to `/login` |
| `/activity?type=WritingScenario` (3rd Writing activity, Phase 4) | UI nav (card click) | OK | none | none | Score 65, Persian explanation toggle works correctly |
| `/progress` (Phase 4, 3 activities) | UI nav | OK | none | none | Module "Effective Workplace Introductions" now Completed 3/3, no duplicates |
| `/journey` (click completed session, Phase 4) | UI action (cursor-interactive div) | OK (no-op) | none | none | Click on "Done" session is a dead click, no detail/retry view |
| `/vocabulary` (Phase 4 recheck) | UI nav | OK (still empty) | none | none | Still "No vocabulary yet" after 3 writing-type activities |
| Mobile (375px): `/dashboard`, `/journey`, `/progress`, `/profile` (Phase 4) | viewport 375x812 | OK | none | none | No horizontal overflow |
| Mobile (375px): `/lesson/{id}` (Today's Lesson page) | viewport 375x812, UI nav | OK | none | none | No overflow, stepper cards render correctly |
| Mobile (375px): `/activity?activityId=...` (Vocabulary step) | viewport 375x812, UI nav | **FAIL** | none | none | Same blank-activity bug as desktop, reproduces at 375px |
| Mobile (375px): `/activity?activityId=...` (Writing task step) | viewport 375x812, UI nav | **FAIL** | none | none | Same blank-activity bug, also reproduces at 375px |
| Mobile (375px): `/activity?type=ListeningComprehension` | viewport 375x812, UI nav | OK | none | none | No overflow, audio (`blob:`, 18.5s) loads and is present |
| `/activity?type=SpeakingRolePlay` (mic-allowed attempt, Phase 4) | UI action, `getUserMedia` direct call | **FAIL** (infra) | none | none | `NotSupportedError` — no fake-device flags on this daemon; same mic-denied dead-end UI shown |
| `/admin/students` (logged out, direct goto) | direct goto | OK | none | none | Clean redirect to `/login` with admin-context message |
| `/this-does-not-exist-404` (logged out) | direct goto | OK | none | none | Redirects to `/login`, no stack trace, no real 404 page |
| `/admin/students` (logged in as student #2, direct goto) | direct goto | OK | none | none | Clean redirect to `/dashboard` with "no permission" message |
| `/this-does-not-exist-404` (logged in as student #2) | direct goto | OK (with quirk) | none | none | Redirects to `/login` but session remains valid (see Bugs: Low) |
| `/admin/usage` (AI Usage, in-app sidebar click) | UI nav (sidebar click) | OK | none | none | Loads correctly with usage stats; confirms Phase 1 bug is direct-URL-specific |
| `/admin/students` → Edit (student #2 cross-check) | UI nav + click | OK | none | none | CEFR B2, Lifecycle ActiveLearning, Onboarding Complete — matches student-side B2 |

## Bugs by severity

### High

**Bug: Admin has no way to reset a student's password**
- Severity: High
- Steps to reproduce:
  1. Log in as admin.
  2. Go to Admin → Students → Edit on any student row.
- Expected: Some admin-initiated password reset/recovery mechanism exists
  (reset button, "send reset link", temp password regenerate, etc.) so admins
  can recover access for students who are locked out (e.g. forgot their
  post-onboarding changed password).
- Actual: The edit form only exposes profile fields (name, career context,
  learning goal, preferred duration, experience level, role familiarity). No
  password field, reset button, or reset-link action exists anywhere in the
  admin UI. No `/api/swagger/v1/swagger.json` is exposed to check for an
  unlisted API.
- Impact: A student who is locked out after a forced first-login password
  change (as happened to `qa.fullaudit.202606131530@example.com` in this
  audit) has no recovery path — admin cannot help, and there's no visible
  "forgot password" link on `/login` either (TODO: confirm in Phase 3).
  This blocked Phase 2 of this audit entirely.
  Confirmed: `/login` also has no "Forgot password" link (only Email,
  Password, Sign in).

**Bug: "Teach page" activities (Listening, Vocabulary) render completely blank**
- Severity: High
- Steps to reproduce:
  1. Log in as a student with an active Today's Lesson.
  2. Open the lesson, click "Load activity" on a Listening or Vocabulary step,
     wait for generation to finish, click "Open activity".
- Expected: The activity page (`/activity?activityId=...`) shows the lesson
  content (e.g., listening transcript/audio player + comprehension questions
  for Listening; matching exercise for Vocabulary), inside the
  `<app-activity-teach-page>` component.
- Actual: The page shows only the "1 Lesson / 2 Practice / 3 Feedback" step
  indicator. The `<app-activity-teach-page>` element is present in the DOM but
  renders empty (`<!---->` Angular comment placeholders only, ~0 visible
  content). This happens even though
  `GET /api/activity/{activityId}` → 200 (2049B JSON) and
  `GET /api/activity/{activityId}/audio` → 200 (1.39MB audio) both succeed.
  Reloading the page does not fix it. No console errors of any kind are
  logged.
- Console/network: no errors; underlying API calls succeed.
- Reproduced twice in this session: once for the Listening step (activity
  `ee77c806-9354-4b37-8cf7-d2b88f09dc18`) and once for the Vocabulary step
  (activity `a2c7353b-92ed-48a6-aef3-cc01039d8f43`). The Speaking step
  (`7c39c663-aef8-4237-82c5-71787d25fcd2`) rendered correctly, suggesting the
  bug is specific to the "teach page" content type used by
  Listening/Vocabulary, not the activity page in general.
- Impact: Students cannot complete Listening or Vocabulary lesson steps at all
  — the activity is silently empty with no error message, no retry option, and
  no way to proceed except backing out to the lesson page. In this audit, both
  steps were only marked complete via a "Mark complete" button on the lesson
  page itself (which does not require viewing the activity), bypassing the
  actual learning content entirely.
- Screenshots:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/19-activity-page-blank.png`,
  `20-activity-blank-after-reload.png` (Listening),
  `22-vocab-activity-blank.png` (Vocabulary).

**Bug: Speaking activity mic-denied has no text fallback (lesson activities)**
- Severity: High
- Steps to reproduce:
  1. Open a Today's Lesson Speaking step, "Load activity" → "Open activity".
  2. Click "Start recording" with microphone permission denied/unavailable.
- Expected: Same as the placement assessment's Speaking section — a text
  fallback ("Microphone access was denied. Type your response instead.") with
  a textbox to type the response and proceed.
- Actual: Shows "🔇 Microphone access denied — SpeakPath needs microphone
  access to record your response. Please allow microphone access in your
  browser settings and try again." with only two buttons: "Try again" and
  "Back to Today". No text-input fallback is offered.
- Console/network: no errors.
- Impact: Any student without a working/permitted microphone hits a dead end
  on every Speaking lesson step (cannot complete the step via the activity
  UI at all — must be marked complete via the lesson page's "Mark complete"
  button, bypassing the exercise). This matches the unresolved finding from
  student #1's screenshot `11-activity-mic-denied-no-fallback.png` in the
  prior session — confirmed as a real, reproducible bug (not a one-off).
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/21-speaking-activity-mic-denied-no-fallback.png`

**Bug: Practice Gym "by exercise type" activities (`?pattern=...` routes) render permanently blank**
- Severity: High
- Steps to reproduce:
  1. Log in as a student, go to `/practice`.
  2. Click any "By exercise type" card: Fill in the blanks, Matching, Email,
     or Workplace chat.
  3. Wait 15+ seconds, then try reloading the page.
- Expected: The activity content renders (gap-fill sentences, matching pairs,
  email scenario, chat simulation respectively), as it does for the "By
  skill" cards (`?type=...` routes).
- Actual: The page shows only the "1 Lesson / 2 Practice / 3 Feedback"
  stepper — no exercise content at all, even after a full reload. Confirmed
  for all 4 currently-enabled exercise-type cards:
  `?pattern=teams_chat_simulation`, `?pattern=gap_fill_workplace_phrase`,
  `?pattern=email_reply`, `?pattern=phrase_match`.
- Console/network: no console errors. `GET /api/activity/next?pattern=<name>`
  returns 200 with a non-empty JSON body (e.g. 1834B for
  `teams_chat_simulation`) — backend generation/serving works; this is a
  client-side rendering failure.
- Impact: **The entire "By exercise type" section of Practice Gym is
  unusable** — all 4 enabled cards lead to a dead end. This is the same
  underlying symptom as the existing High "teach page renders blank" bug
  (likely the same root cause — a renderer/component not handling this
  content shape — but affects the `?pattern=` route family specifically,
  distinct from `?type=` and lesson `activityId=` routes which mostly work).
- Screenshots:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/26-practice-workplacechat-blank.png`,
  `38-practice-matching-blank.png`.

### Medium

**Bug: Vocabulary list remains empty after multiple completed Writing activities**
- Severity: Medium
- Steps to reproduce:
  1. Complete 2-3 Writing activities (`?type=WritingScenario`) with "Get
     coach feedback" submitted and scored (Phase 3: scores 75 and 48-related;
     Phase 4: a 3rd activity scored 65).
  2. Go to `/vocabulary`.
- Expected: Per the page's own empty-state copy ("Your vocabulary list will
  grow as you complete writing activities"), the vocabulary list should now
  show words/phrases extracted from the completed Writing activities (e.g.
  target phrases like "I am pleased to introduce", "joining our team as",
  shown on the Writing activity itself).
- Actual: `/vocabulary` still shows the empty state — "No vocabulary yet...
  Your vocabulary list will grow as you complete writing activities." — after
  3 completed Writing activities across Phases 3-4.
- Console/network: no errors, page loads cleanly, no overflow at 375px.
- Impact: A documented/advertised feature (vocabulary tracking from writing
  practice) appears non-functional, or its population trigger doesn't match
  the stated criteria. Students following the "complete writing activities to
  build vocabulary" guidance see no payoff.
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/40-vocabulary-still-empty.png`

**Bug: Profile page shows "Current level: Not assessed yet" despite completed B2 placement**
- Severity: Medium
- Steps to reproduce:
  1. Complete placement assessment (student #2 got estimated level B2 in
     Phase 2).
  2. Go to `/profile`.
- Expected: "Current level" reflects the placement result (B2), consistent
  with Journey and Progress pages which both correctly show B2-based
  breakdowns and recommendations.
- Actual: Profile shows "Current level: Not assessed yet".
- Console/network: no errors.
- Impact: Confusing/inconsistent — a student could think their placement
  didn't complete or was lost, even though Journey/Progress/Dashboard all
  correctly reflect it. Likely the Profile page reads from a different/stale
  field than Journey/Progress.
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/30-profile-page.png`

**Bug: Onboarding step-1 language-path selection is lost on refresh or Back navigation**
- Severity: Medium
- Steps to reproduce:
  1. On `/onboarding/step-1`, click the "Persian to English" path card
     (enables "Continue").
  2. Either reload the page, or click "Continue" to step 2 and then click
     "Back" to return to step 1.
- Expected: The previously-selected path remains selected (card shows
  selected state, "Continue" stays enabled), since it's the only option and
  was already chosen.
- Actual: The selection is lost — "Continue" becomes disabled again and the
  path card shows as unselected. The student must re-click the path card
  before they can proceed again.
- Console/network: no errors.
- Impact: Minor friction (one extra click) for this step since there's only
  one path option, but indicates onboarding step state is not persisted/
  restored from the backend on reload/back — could be worse for steps with
  more complex selections (steps 2-5 were not individually re-tested for the
  same issue beyond step 1, but the same client-side pattern likely applies).
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/14-onboarding-step1-refresh-resets.png`

**Bug: Dashboard streak/practice/score stats don't update after completing a lesson**
- Severity: Medium
- Steps to reproduce:
  1. Complete a full Today's Lesson (4/4 steps, status becomes "Completed").
  2. Navigate to `/dashboard`.
- Expected: "Streak" reflects at least 1 day, "Practice X activities done"
  reflects the completed steps/activities, "Score" shows an average based on
  the completed lesson.
- Actual: Dashboard still shows "0 day streak", "Practice 0 activities done",
  "Score -- average" immediately after completing the first lesson (a new
  Today's Lesson, "Writing Concise Professional Emails", was correctly
  generated/rotated in, so journey progression works — only the stats
  widgets appear stale).
- Console/network: no errors observed.
- Impact: Students get no positive reinforcement/visible progress signal on
  the dashboard after completing work, which could affect motivation/retention.
  Possibly a caching issue (stats computed from a cached dashboard payload) or
  the stats genuinely aren't updated by the lesson-completion endpoint.
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/24-dashboard-after-completion-stale-progress.png`
- **Phase 3 follow-up**: after completing 2 more activities via Practice Gym
  and visiting `/progress`, `/dashboard` was re-checked and now correctly
  showed "Practice 2 activities done", "Score 62 average", "Module progress
  2/3 done". So the stats **did eventually update** — this looks like a
  propagation delay/cache rather than a permanent failure (severity may be
  lower than originally assessed; downgrade to Low/cosmetic pending
  confirmation of the exact delay). "Streak: 0 day streak" still showed even
  with 2 activities completed same-day — this may be correct (streak likely
  requires multiple distinct days), not necessarily a bug.

**Bug: "Require password change on first login" defaults to checked in admin create-student form**
- Severity: Medium (contributing cause of the High "no admin password reset"
  impact on student #1)
- Steps to reproduce:
  1. Admin → Students → Create student.
  2. Observe the "Require password change on first login" checkbox state.
- Expected: Either default to unchecked (admin opts in explicitly), or — if
  checked by default is intentional — the resulting forced-change flow must
  reliably surface/communicate the new password so the admin/student doesn't
  get locked out (tying into the existing High "no password reset" bug).
- Actual: Checkbox is checked by default. Combined with the existing lack of
  any admin password-reset capability, this is a likely root cause of student
  #1's lockout (forced first-login password change with the new password never
  surfaced/recorded).
- Impact: Every new student created with default settings is one
  forgotten-password-during-first-login away from permanent lockout, with no
  recovery path. Mitigated in this session by unchecking the box for student
  #2.

**Bug: Direct navigation to `/admin/ai-usage` redirects to `/login`**
- Severity: Medium
- Steps to reproduce:
  1. Log in as admin (lands on `/admin`).
  2. Navigate directly to `https://speakpath.app/admin/ai-usage` via URL bar
     (or browser refresh on that route).
- Expected: AI Usage admin page loads (as it would if a nav link existed for
  it / as the sidebar implies it should).
- Actual: Page returns HTTP 200 for the route, but the client redirects to
  `/login`, showing the sign-in form. This happens even immediately after a
  fresh login (session is valid — other admin routes work fine
  side-by-side).
- Console/network: no errors logged; `GET /admin/ai-usage` → 200, but app
  ends up rendering `/login`.
- Likely cause: Angular route guard / lazy-loaded module config issue for
  this specific route on direct navigation (vs. SPA-internal navigation).
- Impact: Admins cannot deep-link or bookmark the AI Usage page, and a
  refresh on that page logs them out of that view (though the sidebar nav
  item itself was not located/clicked in Phase 1 — TODO Phase 2/3 to confirm
  whether an in-app sidebar click to "AI Usage" works correctly).

**Bug: Direct navigation to `/admin/students/new` redirects to `/login`**
- Severity: Medium
- Steps to reproduce:
  1. Log in as admin.
  2. Navigate directly to `https://speakpath.app/admin/students/new`.
- Expected: Create-student form loads (the in-app "Create student" link
  goes to `/admin/create-student` and works fine).
- Actual: Redirects to `/login`.
- Likely cause: stale/incorrect route path (`/admin/students/new` vs actual
  `/admin/create-student`) combined with route guard fallback to login
  instead of a 404/redirect-to-students-list.
- Impact: Low-frequency (bookmarks/deep links only), but confusing — looks
  like a session/auth failure when it's actually a routing mismatch.

### Process / blocker (not a product bug, but blocks Phase 2)

**Bug title: Audit student password unknown after onboarding password-change flow**
- Severity: N/A (test-process blocker, logged for traceability)
- Area: Auth / student onboarding
- Page route: `/login`
- Steps:
  1. Phase 1 created student `qa.fullaudit.202606131530@example.com` with
     temp password `QaAudit#2026_P1`.
  2. A prior Phase 2 attempt logged in with that password and proceeded
     through onboarding, placement, journey, and a Today's Lesson
     (evidenced by screenshots 06-13 in the artifacts folder), then hit a
     spend-limit error before recording results.
  3. This Phase 2 attempt tries `POST /api/auth/login` with
     `QaAudit#2026_P1`.
- Expected: Login succeeds (or fails clearly indicating a forced password
  change is pending).
- Actual: `POST /api/auth/login` → 401 "Invalid credentials". Console shows
  a 401 resource-load error. No indication of *why* (no "password was
  changed" messaging).
- Console errors: `Failed to load resource: the server responded with a
  status of 401 ()`.
- Network errors: `POST /api/auth/login` → 401.
- Screenshot path: none captured (login form only, no new artifact).
- Likely cause: the prior attempt's onboarding flow included a
  forced/first-login password change, and the new password was set but
  never recorded before the session was cut off by the spend-limit error.
- Suggested fix (process, not product): orchestrator to supply the
  updated password or admin credentials so Phase 2 can reset it via
  Admin → Students → Edit.

### Low

**No discoverable retry/history UI for completed activities**
- Severity: Low
- Steps to reproduce:
  1. Complete 1+ activities so `/progress` shows "Recent scores" entries.
  2. Try to click/interact with a "Recent scores" entry on `/progress`, or a
     "Done" session on `/journey`.
- Expected: Some way to review a past attempt's feedback/transcript, or
  explicitly retry it (the "0 retry attempts" counter on `/progress` implies
  a retry feature exists somewhere).
- Actual: "Recent scores" entries on `/progress` are plain text, not
  interactive. "Done" sessions on `/journey` are rendered as
  `cursor:pointer` divs but clicking does nothing (dead click, no
  navigation). The only retry-like control found across all 4 phases is
  "Try again from scratch" on a freshly-completed activity's own feedback
  screen (in-the-moment only, not from history).
- Console/network: no errors.
- Impact: Students cannot revisit feedback for past activities or
  intentionally retry one from their history — only "Next activity" (new
  content) or the in-the-moment retry button are available.

**Unknown routes redirect authenticated users to `/login` instead of `/dashboard`**
- Severity: Low
- Steps to reproduce:
  1. Log in as student #2.
  2. Direct-navigate to `https://speakpath.app/this-does-not-exist-404`.
- Expected: Either a friendly 404 page, or (for consistency with the
  `/admin/students` permission-block case) a redirect to `/dashboard`
  recognizing the authenticated session.
- Actual: Redirects to `/login` (plain sign-in form) as if logged out.
  However, the session itself remains valid — navigating to `/dashboard`
  immediately afterward works without re-authentication. No data loss, no
  stack trace.
- Console/network: no errors.
- Impact: Minor/cosmetic — a student hitting a stale/mistyped link briefly
  sees a sign-in form despite being logged in, which could cause confusion
  ("did I get logged out?"). Inconsistent with the `/admin/students` case
  (Phase 4 item 6), which correctly shows a "no permission" message on
  `/dashboard` for the same authenticated session.

**Browser Forward navigation to an activity page shows a transient blank state**
- Severity: Low
- Steps to reproduce:
  1. As a student, open a Practice Gym activity (e.g.
     `?type=ListeningComprehension`), let it finish generating, click
     "Answer questions".
  2. Click browser Back (returns to `/practice`).
  3. Click browser Forward (returns to the activity URL).
- Expected: Either the in-progress question/answer state is restored, or the
  activity reloads with a visible loading indicator.
- Actual: The page shows only the sidebar/nav (no activity content, no
  loading indicator) for ~5 seconds while the activity silently regenerates,
  then resolves to a fresh "Answer questions" intro screen — any previously
  typed answers are lost. No console errors; the page does recover on its
  own (does not require a manual reload).
- Console/network: no errors.
- Impact: Low — no crash, no data corruption, and it self-resolves. However,
  the transient blank state is visually identical to the existing High
  "blank activity" rendering bug and could confuse a student or future QA
  into thinking the page is broken. Also loses in-progress (unsubmitted)
  answers on Forward navigation, which is mildly annoying but matches the
  general "drafts aren't persisted" pattern (Phase 6 item 6).
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/60-forward-nav-blank-activity.png`

**In-memory session can complete one in-flight submission after storage/cookies are cleared**
- Severity: Low (informational)
- Steps to reproduce:
  1. As a student, open an activity and fill in answers (e.g. Listening
     comprehension questions).
  2. In the same page context, clear `localStorage`, `sessionStorage`, and
     all cookies (simulating "logged out in another tab" / session expiry).
  3. Click the submit button (e.g. "Check understanding").
  4. Separately, reload the page.
- Expected: Either the submit in step 3 is rejected/redirects to login
  (session already invalidated), or — if the in-memory token is considered
  valid until next navigation — this is documented as intentional.
- Actual: Step 3's `POST /api/activity/{id}/attempt` returned **200** and the
  feedback screen rendered normally, even with `cookies` returning `[]` and
  both storage areas cleared. Step 4 (reload) correctly redirected to
  `/login`, confirming the session really was invalidated client-side and
  the in-memory app state was the only thing keeping the submission working.
- Console/network: no errors; `POST /api/activity/{id}/attempt` → 200.
- Impact: Low — this is standard SPA behavior (auth token cached in memory,
  not re-read from storage per request) and the next navigation/refresh
  correctly re-authenticates. Not flagged as a security issue, but noted for
  awareness: a student who is administratively logged out (e.g. account
  disabled by admin) mid-activity could still complete and submit that one
  in-flight activity before their next page load.
- Screenshot:
  `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/61-submit-after-storage-cleared-succeeded.png`

**Mobile (375px) layout overflow on Dashboard and Students list**
- Severity: Low
- Steps to reproduce:
  1. Set viewport to 375x812.
  2. Visit `/admin` and `/admin/students` while logged in.
- Expected: Cards/table content reflow to fit mobile width.
- Actual: Dashboard "Quick actions" and "Recent students" sections are wider
  than viewport — labels are cut off ("Mar...", "Pro..."), and the Recent
  students table's "ONBOARD..." column is clipped. On the Students list
  page, attempting to click a row's "Edit" button at mobile width timed out
  (element likely off-screen due to table overflow).
- Impact: Admin usability on mobile/tablet is degraded; core data still
  visible via horizontal scroll (not verified) but UI is not fully
  responsive.

## AI generation quality findings

Based on all AI-generated content observed across Phases 2 and 3 (placement,
journey, lesson activities, Practice Gym Writing/Listening/Speaking):

- **Strengths**:
  - Writing feedback (Practice Gym `?type=WritingScenario`) was the
    standout: corrections precisely matched the actual errors in the
    submission, each with a correct and specific grammar explanation, plus
    a fully corrected "suggested improved version" and a content-aware
    "rewrite challenge". Score (75) felt proportionate to a 47-word reply
    with 5 minor grammar issues.
  - Listening comprehension scoring (Practice Gym `?type=ListeningComprehension`)
    correctly distinguished correct/wrong/partial answers per-question (48
    score for 1/3 correct + 1 partial), and the transcript + "Lesson"/"Next
    step to try" tips were relevant to the actual mistakes.
  - Content is consistently **level-appropriate (B2)** and **relevant to the
    student's profile** — career context wasn't set for student #2 beyond
    name, but content stayed in the "workplace introductions / team
    communication" theme matching the generated learning path, with no
    generic/placeholder filler.
  - Persian (L1) translations appear for Writing scenario instructions —
    good localization relevant to the "Persian to English" path selected at
    onboarding.
  - "Next activity" in Writing generated a genuinely new scenario (different
    name, same task type/level) — no repetition observed across 2 writing
    activities.
  - Progress page's "Recurring mistakes to watch" correctly aggregated the
    specific grammar categories from the Writing feedback into longer-term
    skill tracking — good evidence of cross-activity AI memory/synthesis.
- **Weaknesses / gaps**:
  - **Free-text "reply" feedback is generic** — for the Listening
    comprehension activity's 4th question (a free-text reply to "Sarah"),
    the feedback ("Your reply should confirm the task, mention the timeline
    if relevant, and keep a polite workplace tone.") was boilerplate and did
    not reference the actual submitted text or its grammar error
    ("recieve"). This contrasts sharply with the Writing activity's
    feedback, which was fully submission-aware. Likely this question type
    uses a different/simpler evaluator than the main Writing pattern.
  - **Writing/email bias persists in "By skill" content**: 2 of 2 "By skill"
    scenarios generated in this session were email-writing tasks
    ("Introducing a New Team Member Email" x2). Listening and Speaking
    activities were also both "introducing a colleague" themed — content
    variety across skills could be broader, though this may simply reflect
    the current module's theme ("Effective Workplace Introductions") rather
    than a systemic bias.
  - **No JSON/prompt leakage** observed anywhere in this session — all
    rendered content (where it rendered) was clean, formatted text.
  - **Broken renderers**: the High "blank teach-page" bug (Phase 2) and the
    new High "`?pattern=` activities blank" bug (Phase 3) together mean a
    significant fraction of generated content (Listening/Vocabulary lesson
    steps, all 4 Practice Gym exercise-type activities) is generated
    successfully server-side but never reaches the student — the AI
    generation pipeline itself appears healthy; the gap is entirely in
    activity-page rendering for specific content shapes/routes.

## TTS/audio findings

- **Placement listening section (Section 4 of 6)**: audio loaded successfully
  this time (contrast with student #1's prior attempt, which showed
  "audio unavailable" — see `06-placement-listening-audio-unavailable.png`
  from that session). The `<audio>` element's `currentSrc` was a `blob:`
  URL with `duration: 15.21 seconds`. A transcript was also shown alongside
  the player ("Show transcript" — already expanded), so a text fallback
  exists regardless of audio status. Section took roughly the same ~1-2s as
  other sections to appear after the prior section's "Save and continue" —
  no unusual latency observed for the audio-bearing section itself.
- **Today's Lesson Listening step audio**: `GET
  /api/activity/{activityId}/audio` returned 200 with 1.39MB of audio data in
  ~2.4s — so server-side TTS generation and delivery worked correctly. However
  the activity UI never rendered the audio player (or any content) due to the
  blank "teach page" bug (see Bugs: High), so the audio could not be
  played/verified end-to-end from the student's perspective for this step.
- **Speaking sections** (placement + lesson step 2): no TTS playback involved
  (these are recording/typing tasks), but both rely on mic permission. The
  placement speaking section has a working text fallback when mic access is
  denied; the lesson Speaking activity does not (see Bugs: High).
- No TTS/audio-related console or network errors (4xx/5xx on audio endpoints)
  were observed in this session.

### Phase 3 follow-up

- Practice Gym Listening (`?type=ListeningComprehension`) audio loaded
  successfully — `blob:` URL, `duration: 16.93s`, no errors. TTS pipeline
  continues to work correctly for this route.
- The ~15-16s blank period before this page renders (which then resolves on
  its own) appears to correlate with audio-generation time — this is a
  loading-state/UX gap (no spinner/progress indicator shown during
  generation), not an audio failure. See Performance findings and
  Recommended next sprint priorities (loading state).

## Performance findings

- All successfully-loaded admin pages returned page-load network completion
  with no failed (4xx/5xx) asset requests observed.
- Login POST `/api/auth/login` completed in ~160ms.
- Student create POST `/api/admin/students` completed in ~268ms (201).
- Student edit PUT `/api/admin/students/{id}` completed in ~141ms (200).
- No slow-loading pages observed during Phase 1.

### Phase 2 (student #2) timings

- Student #2 login (`POST /api/auth/login` → redirect to onboarding): ~1.4s.
- Onboarding completion (step 5 "Continue to assessment" → `/placement`):
  ~730ms.
- Placement section transitions (each "Save and continue"): ~750-830ms per
  section.
- Placement final evaluation ("Working out your level…" → result screen):
  ~8-9s.
- Placement "Continue" → `/dashboard`: ~760ms.
- Journey page load: ~620ms.
- Today's Lesson open (dashboard → `/lesson/{id}`): ~720ms.
- Lesson "Start lesson" (Not started → In progress): ~870ms.
- **Activity generation ("Load activity" → "Open activity" becomes
  available)**: ~16-17s for the Listening step (first activity of the lesson,
  likely includes audio TTS generation given the 1.39MB audio payload);
  ~10s for the Speaking and Vocabulary steps.
- Activity page network (Listening step):
  `GET /api/activity/{id}` → 200 in 5.5s, `GET /api/activity/{id}/audio` → 200
  in 2.4s (1.39MB) — these ran as part of the activity page load itself.
- "Mark complete" / "Mark step complete" actions: near-instant (<1s, no
  visible loading state).
- No 4xx/5xx errors on any timed request in this session (other than the
  expected pre-onboarding `GET /api/dashboard` → 403 calls noted above).

### Phase 3 (student #2) timings

- Re-login after expired session: normal login flow, no unusual delay.
- Practice Gym Listening activity (`?type=ListeningComprehension`) first
  render: ~15-16s blank before content appears (no loading indicator) —
  likely audio-generation time (`GET .../audio` style call inferred from the
  16.93s audio duration).
- Practice Gym Speaking/Writing activities (`?type=...`): rendered
  immediately, no perceptible delay.
- Practice Gym "by exercise type" activities (`?pattern=...`): `GET
  /api/activity/next?pattern=teams_chat_simulation` → 200 in ~3.4s (1834B) —
  fast and successful at the API level, but UI never renders (see Bugs:
  High).
- Writing "Get coach feedback" → full feedback render: ~10-18s total
  (observed as "Getting coach feedback…" loading state, then result).
- "Next activity" (Writing): new scenario generated and rendered in a few
  seconds, no visible delay issue.
- `/progress` and `/profile` page loads: both near-instant (<1s), no loading
  spinners needed.

## Responsive/mobile findings

- Login page (375x812): renders correctly, form centered, no overflow
  observed.
- Admin Dashboard (375x812): horizontal overflow — "Quick actions" grid and
  "Recent students" table extend past viewport width, several
  labels/columns clipped on the right edge (see Bugs: Mobile layout
  overflow).
- Students list (375x812): horizontal overflow on the student table; list
  content (names/emails/lifecycle badges) visible but table likely needs
  horizontal scroll; row "Edit" button click timed out at this viewport.
- Create-student form (375x812): renders correctly, single-column layout,
  no overflow observed, all fields accessible.
- Student edit form: not captured cleanly at mobile width due to the Edit
  button click timeout on the Students list (see above); revisit in a later
  phase once the table overflow issue context is understood.

### Phase 4 (student #2) mobile findings (375x812)

- `/dashboard`, `/journey`, `/progress`, `/profile`: all `scrollWidth ===
  clientWidth === 375`, no overflow, no console errors — consistent with
  Phase 3.
- `/lesson/{id}` (Today's Lesson detail page, "Writing Concise Professional
  Emails"): no overflow. "Start lesson" and "Load activity" buttons work at
  375px. Step cards (1-4, with type/duration/description) render correctly,
  no clipping.
- Lesson step activity pages (`/activity?activityId=...`): no horizontal
  overflow on either the Vocabulary warm-up step or the Writing task step —
  but both render **completely blank** (same High "teach page" bug as
  desktop, reproduced identically at 375px). No new mobile-specific symptom;
  the existing bug is viewport-independent.
- Practice Gym Listening (`/activity?type=ListeningComprehension`) at 375px:
  no overflow, "Answer questions" button visible, and a `blob:` audio element
  (18.53s duration) is present in the DOM — audio playback infrastructure
  works at mobile width.
- Practice Gym Speaking (`/activity?type=SpeakingRolePlay`) at desktop width
  (1280px, mic test required full Chromium): mic-denied dead-end UI
  unchanged, no text fallback (same High bug, re-confirmed).
- **Overall**: Phase 4 found zero new mobile-specific layout issues. Every
  student page/flow checked across Phases 3-4 is overflow-free at 375px; the
  only mobile "failures" are the pre-existing High rendering/mic bugs, which
  affect mobile and desktop identically.

## Screenshots/artifacts

Location: `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/`

- `mobile-login.png` — login page at 375x812
- `mobile-admin-dashboard.png` — admin dashboard at 375x812 (overflow visible)
- `mobile-students-list.png` — students list at 375x812 (overflow visible)
- `mobile-create-student.png` — create-student form at 375x812 (renders OK)
- `mobile-student-edit.png` — captured after a timed-out click on the
  Students list Edit button at mobile width; shows create-student form
  state (click did not open the edit panel as expected)

### Phase 2 (student #2) screenshots

- `14-onboarding-step1-refresh-resets.png` — onboarding step 1 after refresh,
  showing the language-path selection lost and "Continue" disabled again
- `15-placement-speaking-no-fallback.png` — placement speaking section before
  the text fallback appeared (mic-denied state mid-transition)
- `16-placement-result-b2.png` — placement result screen, estimated level B2,
  full skill breakdown and strengths/areas-to-improve
- `17-dashboard-onboarding-message.png` — Today dashboard immediately after
  placement, showing the placement summary and first Today's Lesson card
  ("Understanding and Responding to Feedback")
- `18-journey-page.png` — Journey page with 5 generated sessions
- `19-activity-page-blank.png` — Listening step activity page rendering blank
  (only the Lesson/Practice/Feedback stepper visible)
- `20-activity-blank-after-reload.png` — same Listening activity page after a
  reload, still blank
- `21-speaking-activity-mic-denied-no-fallback.png` — lesson Speaking activity
  with mic permission denied and no text-input fallback ("Try again" / "Back
  to Today" only)
- `22-vocab-activity-blank.png` — Vocabulary step activity page rendering
  blank (same bug as Listening step)
- `23-lesson-complete.png` — Today's Lesson marked "✓ Completed", 4/4 steps
  done, "🎉 Lesson complete!" message
- `24-dashboard-after-completion-stale-progress.png` — dashboard after lesson
  completion, showing a new Today's Lesson generated but Streak/Practice/Score
  widgets still at zero/blank

### Phase 3 (student #2) screenshots

- `25-practice-page.png` — Practice Gym page, all "By skill" and "By exercise
  type" cards visible, Coming soon cards correctly disabled
- `26-practice-workplacechat-blank.png` — Workplace Chat
  (`?pattern=teams_chat_simulation`) rendering blank after reload
- `27-practice-writing-feedback.png` — Writing activity feedback, score 75,
  detailed before/after grammar corrections matching the submission
- `28-practice-listening-feedback.png` — Listening comprehension feedback,
  score 48, per-question correct/wrong/partial grading + transcript
- `29-progress-page.png` — Progress page showing real, non-stale data
  (2 activities, scores 48/75, module 2/3, recurring mistakes)
- `30-profile-page.png` — Profile page showing "Current level: Not assessed
  yet" despite completed B2 placement
- `31-mobile-dashboard.png` — student dashboard at 375x812, no overflow
- `32-mobile-journey.png` — Journey page at 375x812, no overflow
- `33-mobile-practice.png` — Practice Gym at 375x812, no overflow
- `34-mobile-progress.png` — Progress page at 375x812, no overflow
- `35-mobile-profile.png` — Profile page at 375x812, no overflow
- `36-mobile-writing-activity.png` — Writing activity at 375x812, scenario +
  form render correctly
- `37-mobile-writing-filled.png` — Writing activity at 375x812 with response
  filled in, "Get coach feedback" enabled
- `38-practice-matching-blank.png` — Matching (`?pattern=phrase_match`)
  rendering blank after 15s wait

### Phase 4 (student #2 + admin) screenshots

- `39-persian-explanation-toggle.png` — Writing activity feedback (score 65)
  with "Show Persian explanation" expanded, correct Farsi text rendered
- `40-vocabulary-still-empty.png` — `/vocabulary` page still showing "No
  vocabulary yet" after 3 completed writing-type activities
- `41-progress-page-3-activities.png` — `/progress` after 3rd activity,
  module "Effective Workplace Introductions" now Completed 3/3, no
  duplicates
- `42-mobile-dashboard-p4.png` — student dashboard at 375x812 (Phase 4
  recheck), no overflow
- `43-mobile-journey-p4.png`, `43-mobile-progress-p4.png`,
  `43-mobile-profile-p4.png` — Journey/Progress/Profile at 375x812, no
  overflow
- `44-mobile-lesson-page.png` — Today's Lesson detail page at 375x812, no
  overflow, all 4 step cards visible
- `45-mobile-activity-blank.png` — Vocabulary warm-up lesson step at 375x812,
  rendering blank (same High bug as desktop)
- `46-mobile-lesson-writing-step-blank.png` — Writing task lesson step at
  375x812, also rendering blank (same High bug)
- `47-mobile-listening-activity.png` — Practice Gym Listening at 375x812, no
  overflow, audio element present (18.53s)
- `48-speaking-mic-notsupported.png` — Speaking activity mic dead-end,
  re-confirmed with `NotSupportedError` (no fake-device flags available)
- `49-admin-ai-usage-inapp-nav.png` — Admin AI Usage page (`/admin/usage`)
  loaded via in-app sidebar click, full usage stats visible
- `50-admin-student2-edit-detail.png` — Admin → Students → Edit for student
  #2, showing profile fields consistent with onboarding answers

## Phase 5 — full activity-type sweep

Login as student #2 succeeded directly (no forced password change). All
cards on `/practice` enumerated and clicked. The Practice page currently
shows only **8 enabled cards** (4 "By skill": Listening, Speaking, Writing,
Vocabulary/Word cards; 4 "By exercise type": Fill in the blanks, Matching,
Email, Workplace chat) plus **8 "Coming soon" cards** rendered as
non-interactive `[text]` elements (Reading, Vocabulary queue, Multiple
choice, Sentence transformation, Error correction, Word formation,
Unscrambling, AI role play). Many activity types named in the original audit
spec (Phrase Match as a distinct type, Pronunciation, MCQ, True/False/Not
Given, Sentence Unscrambling, Paragraph Reordering, Sentence Transformation,
Odd One Out, Word Formation, Reading Q&A, Grammar) do **not exist as
separate Practice Gym cards** at all — either folded into the 8 enabled
cards, listed under "Coming soon", or not present in the catalogue.

| Activity type (card) | Route | Status | Notes/bug ref |
|---|---|---|---|
| Listening (By skill) | `?type=ListeningComprehension` | **Works** | ~26s initial generation (no spinner). 4 questions answered (mix correct/wrong/partial), scored 36/100, per-question expected-vs-actual feedback shown, transcript revealed, "Next step to try" tip relevant. |
| Speaking (By skill) | `?type=SpeakingRolePlay` | **Works with known bug (High: mic-denied no fallback)** | Scenario renders correctly. "Start recording" → mic denied → dead end, "Try again"/"Back to Today" only, no text fallback. Same High bug re-confirmed. |
| Writing (By skill) | `?type=WritingScenario` | **Works** | Scenario (EN+Persian) rendered. Submitted 27-word reply with deliberate "ment"/"i"/lowercase errors. "Get coach feedback" → score, "Good effort" label, multiple "Suggested" corrections shown — feedback relates to submission. |
| Vocabulary / Word cards (By skill) | `?pattern=phrase_match&returnTo=/practice` | **Broken — blank render (High, existing)** | Permanently blank after 16s wait, only stepper visible. Same root cause as existing Practice Gym pattern-route bug. |
| Reading (By skill) | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`, not a link. |
| Vocabulary queue (By skill) | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`, not a link. |
| Fill in the blanks (By exercise type) | `?pattern=gap_fill_workplace_phrase&returnTo=/practice` | **Broken — blank render (High, existing)** | Permanently blank after 16s wait. Same pattern-route bug. |
| Matching (By exercise type) | `?pattern=phrase_match&returnTo=/practice` | **Broken — blank render (High, existing)** | Permanently blank after 16s wait. Same `phrase_match` route as Word cards card above (two different "By skill"/"By exercise type" cards map to the same broken route). |
| Email (By exercise type) | `?pattern=email_reply&returnTo=/practice` | **Broken — blank render (High, existing)** | Permanently blank after 16s wait. Same pattern-route bug. |
| Workplace chat (By exercise type) | `?pattern=teams_chat_simulation&returnTo=/practice` | **Broken — blank render (High, existing)** | Permanently blank after 16s wait. Same pattern-route bug, re-confirms Phase 3 finding. |
| Multiple choice | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`. |
| Sentence transformation | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`. |
| Error correction | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`. |
| Word formation | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`. |
| Unscrambling | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`. |
| AI role play (Live practice) | n/a | **Coming soon — correctly disabled** | Non-interactive `[text]`. |
| Pronunciation, MCQ, True/False/Not Given, Paragraph Reordering, Odd One Out, Reading Q&A, Grammar (By skill/exercise type) | n/a | **Not present in catalogue** | These types from the original spec do not appear as cards anywhere on `/practice` (enabled or "Coming soon"). Either renamed/merged into existing cards, not yet built, or never added to the Practice Gym catalogue — not a runtime bug, but a catalogue gap worth flagging to product. |

### Progress verification after Phase 5 activities

`/progress` after this session shows **5 activities completed** (up from 3
at end of Phase 4), average score 60, latest score 78, "5 this week", 1
module completed. The 2 new entries match this session's submissions:
"Clarifying a Meeting Point: Marketing Strategy" (78, Writing) and "Meeting
Follow-up: Project Alpha Brainstorming" (36, Listening) — both correctly
attributed, dated, and module-linked ("Participating in Team Meetings", now
2/3 activities, avg 57, latest 78). No duplicates, no stale data — confirms
Phase 3/4 finding that Progress page reflects real-time activity completion.

### Phase 5 conclusions

- **No new distinct bugs found.** All 4 broken cards (Word cards, Fill in
  the blanks, Matching, Email, Workplace chat — 5 cards, 4 unique routes:
  `phrase_match`, `gap_fill_workplace_phrase`, `email_reply`,
  `teams_chat_simulation`) are instances of the existing High "Practice Gym
  `?pattern=...` activities render permanently blank" bug (already
  documented). The Speaking mic-denied dead end is the existing High bug.
  No new severity categories or symptoms observed.
- **New observation (not a bug, catalogue/product gap)**: of the 8 enabled
  Practice Gym cards, only **3 are usable end-to-end** (Listening, Speaking
  with the mic caveat, Writing) — i.e. **37.5% of enabled cards work fully**,
  62.5% (5 of 8) are blank dead ends. Including the 8 correctly-disabled
  "Coming soon" cards, **3 of 16 total visible cards (18.75%) deliver a
  complete working activity+feedback loop**; the remaining cards are either
  blank (5, 31.25%) or correctly gated as not-yet-available (8, 50%).
- Many activity types named in the original audit spec do not exist as
  Practice Gym cards at all (see table) — flagged for product/catalogue
  review, not logged as a bug since absence of a card is not a runtime
  failure.

### Phase 5 screenshots

- `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/50-wordcards-blank.png`
  — Vocabulary "Word cards" card (`?pattern=phrase_match`) rendering blank
- `docs/testing/artifacts/deployed-admin-student-e2e-2026-06-13/51-practice-page-overview.png`
  — `/practice` page overview showing all 8 enabled + 8 "Coming soon" cards

## Phase 6 — Edge case testing

Login as admin (`ehsan.aliverdi@gmail.com`) and student #2
(`qa.fullaudit2.202606131530@example.com` / `QaAudit2#2026!`). Student #1 not
touched. No new students created (validation correctly prevented all invalid
submissions). Both accounts left in a normal logged-in state afterward.

### 1. Admin student-creation form validation

- **Invalid email format ("notanemail")**: filled Student email = `notanemail`,
  Temporary password = `TestPass#2026!`, clicked "Create student". **Result:
  PASS.** Inline validation message "Enter a valid email address." appeared
  under the email field; no `POST /api/admin/students` was sent (client-side
  validation blocked submission); no console errors.
  Screenshot: `52-create-student-invalid-email.png`.
- **Duplicate email (student #2's email)**: filled Student email =
  `qa.fullaudit2.202606131530@example.com` (existing student), Temporary
  password = `TestPass#2026!`, clicked "Create student". **Result: PASS.**
  `POST /api/admin/students` → **409**, and the form displayed "A student with
  this email already exists." No duplicate created, no silent overwrite, no
  console errors. Screenshot: `53-create-student-duplicate-email.png`.
- **Blank required fields**: reloaded the form (fresh state), clicked "Create
  student" with both Student email and Temporary password empty. **Result:
  PASS.** Two inline validation messages appeared ("Enter the student email
  address." and "Enter a temporary password."); no network request was sent;
  no console errors. Screenshot: `54-create-student-blank-fields.png`.
- **Conclusion**: admin create-student form validation is solid across all 3
  cases — clear messages, no network call for client-side-invalid input, clean
  409 + friendly message for server-side duplicate detection. No bugs found.

### 2. Browser back/forward through a multi-step flow

- As student #2, navigated `/practice` → clicked "Listening" card →
  `/activity?type=ListeningComprehension` (waited ~14s for generation) →
  clicked "Answer questions" (SPA state change, same URL) → filled in one
  question's answer.
- **Back**: browser Back navigated to `/practice` cleanly (the "Answer
  questions" SPA sub-state was not a separate history entry — Back skipped
  straight to the pre-activity-click page). No console errors, no broken
  state, no duplicate network calls.
- **Forward**: browser Forward re-navigated to
  `/activity?type=ListeningComprehension`. The page initially rendered with
  only the sidebar/nav (no "Answer questions" button or content) for ~5
  seconds while the activity regenerated, then resolved to a fresh "Answer
  questions" intro screen — **the previously-typed answer and the
  in-progress questions view were lost** (reset to the pre-"Answer questions"
  state, not a crash or error). No console errors during either transition.
  Screenshot: `60-forward-nav-blank-activity.png`.
- **Result: PASS with a Low note.** No crashes, no duplicate submissions, no
  broken/raw-error state. The Forward-navigation re-render delay (~5s blank
  before content appears) is visually identical to the existing "blank
  activity" High bug's symptom and could confuse a real user into thinking
  the page is broken — logged as a new Low finding below (distinct from the
  High bug: this one *does* eventually resolve).
- Screenshot of filled question state before navigation:
  `59-listening-questions-filled.png`.

### 3. Logout / session expiry mid-activity

- As student #2, on the Listening activity's questions screen, filled all 4
  answers (3 short-answer + 1 free-text reply), confirming "Check
  understanding" became enabled.
- Simulated session expiry by running `localStorage.clear()`,
  `sessionStorage.clear()`, and expiring all cookies via JS in the same page
  context (`document.cookie` → empty after, confirmed via `cookies` → `[]`).
- Clicked "Check understanding" (submit). **Result:** `POST
  /api/activity/{id}/attempt` → **200**, and the feedback screen rendered
  normally (score, per-question expected/actual, "Improve my answer" /
  "Try again from scratch" / "Next activity" buttons all present). No error,
  no redirect, no console errors.
  Screenshot: `61-submit-after-storage-cleared-succeeded.png`.
- **Follow-up**: reloaded the same page immediately afterward (still with
  storage/cookies cleared). **Result:** correctly redirected to `/login`.
- **Conclusion (informational, not a bug)**: the already-loaded SPA tab
  retains its in-memory auth state and can complete an in-flight
  submission even after `localStorage`/cookies are cleared (simulating
  "logged out in another tab"); a fresh page load/reload correctly enforces
  re-authentication. This matches expected SPA behavior (token held in
  memory, not re-read from storage per-request) and is not a security issue
  on its own — the next navigation/refresh re-authenticates correctly — but
  is worth noting as a minor inconsistency window. Logged as a new
  informational/Low item below.

### 4. Empty/blank form submissions (Writing activity)

- As student #2, opened a fresh Writing activity
  (`?type=WritingScenario&returnTo=/practice`), clicked "Start writing →".
- With the response textbox empty, "Get coach feedback" was **disabled**
  (pre-existing, re-confirmed).
- Filled the textbox with whitespace only (`"   "`) — "Get coach feedback"
  **remained disabled** (`is enabled` → false, `is disabled` → true).
  Screenshot: `57-writing-blank-whitespace-disabled.png`.
- Filled with real text — button became enabled (sanity check).
- **Result: PASS.** No way to trigger an "evaluation" of empty or
  whitespace-only content; validation is robust (covers both empty and
  whitespace-only cases). No console errors.

### 5. Direct URL manipulation (invalid params)

- **Invalid `activityId`**: navigated directly to
  `/activity?activityId=00000000-0000-0000-0000-000000000000&returnTo=/practice`.
  **Result: PASS.** Clean in-app error card: "⚠️ Service not available —
  Activity 00000000-0000-0000-0000-000000000000 not found. Reference:
  42ed35ca16f1" with "Try again" / "Back to Today" buttons. No blank/crash
  page, no raw stack trace. Console showed only pre-existing 409/404 errors
  from earlier in the session (unrelated). Screenshot:
  `55-invalid-activityid-graceful-error.png`.
- **Invalid `pattern`**: navigated directly to
  `/activity?pattern=nonexistent_pattern_xyz&returnTo=/practice`. **Result:
  PASS.** Same error-card pattern: "⚠️ Service not available — Exercise
  pattern 'nonexistent_pattern_xyz' is not recognised. Check the pattern key
  and try again. Reference: 4e4ac085141f" with "Try again" / "Back to Today".
  A new `400` was logged to console (expected, for the invalid pattern
  lookup) but no crash/blank page. Screenshot:
  `56-invalid-pattern-graceful-error.png`.
- **Conclusion**: both invalid-ID and invalid-pattern direct navigation are
  handled gracefully with a friendly, branded error state (including a
  support-style "Reference" code) and recovery actions — no blank pages, no
  raw exceptions. No bugs found.

### 6. Refresh during in-progress (unsaved) activity

- As student #2, opened a Writing activity, clicked "Start writing →", and
  typed a partial response ("I am writing for introduce a new team member to
  the project, John joining marketing department next monday.") into the
  response textbox (button became enabled, confirming the text registered).
- Reloaded the page (full browser refresh) without submitting.
- **Result: PASS.** Page reloaded to the activity's initial "Start writing →"
  state — the in-progress draft was lost (not persisted), but no error, no
  crash, no stale/broken UI. This is acceptable per the audit spec ("draft
  lost gracefully = acceptable"). No console errors beyond the pre-existing
  409/404/400 entries already logged earlier in the session.
  Screenshot: `58-writing-refresh-draft-lost.png`.

### Phase 6 summary table

| # | Test | Result |
|---|---|---|
| 1a | Admin create-student: invalid email format | PASS — inline validation, no POST |
| 1b | Admin create-student: duplicate email | PASS — 409 + friendly message, no duplicate |
| 1c | Admin create-student: blank required fields | PASS — inline validation, no POST |
| 2 | Back/forward through Practice Gym Listening activity | PASS (Low note: forward-nav re-render delay/state loss) |
| 3 | Session expiry mid-activity (storage/cookies cleared) | PASS (informational: in-memory session outlives storage clear until next reload) |
| 4 | Blank/whitespace Writing submission | PASS — button stays disabled for both empty and whitespace-only |
| 5 | Direct URL with invalid `activityId`/`pattern` | PASS — graceful branded error card, both cases |
| 6 | Refresh during in-progress Writing activity | PASS — draft lost gracefully, no crash |

**No High or Medium bugs found in Phase 6.** Two new Low/informational items
added (see Bugs by severity → Low).

## Existing Playwright coverage reviewed

Reviewed `src/LinguaCoach.Web/e2e/*.spec.ts`. Summary of what's covered:

- **practice-gym.spec.ts**: covers `/practice` loading, heading, no
  auto-redirect, presence of Vocabulary/Listening/Writing/Speaking/Workplace
  Chat/Email/Gap Fill/Phrase Match cards, card links (`/vocabulary`,
  `?type=ListeningComprehension`, `?type=WritingScenario`,
  `?type=SpeakingRolePlay`), and that Workplace Chat/Email/Gap Fill/Phrase
  Match cards are "functional and link to pattern activity". Pronunciation
  "Coming soon" is covered.
  **Gap**: "functional and links to pattern activity" tests appear to check
  the *link/navigation* only, not that the resulting `?pattern=...` page
  actually renders content — this is exactly the High bug found in Phase 3
  (all 4 pattern routes render blank). The existing tests likely pass
  (correct href, page loads 200) while missing the rendering failure.
- **exercise-pattern-renderers.spec.ts**: covers renderer components
  (MatchingPairs, GapFill, ChatReply, EmailReply, AudioAndFreeText,
  AudioAndGapFill, ReadOnly, legacy-FreeTextEntry fallback) presumably via
  mocked/seeded activity data — these test the renderer components in
  isolation, not the live `?pattern=` route's end-to-end data flow against
  the real API, which is where the Phase 3 bug manifests.
- **lesson-activity-qa.spec.ts** / **lesson-activity-wiring.spec.ts**: cover
  `/prepare`, "Open activity" button appearance/href, review-step handling,
  refresh-preserves-activityId, mark-complete flow, `phrase_match` and
  `listen_and_answer` rendering (with/without audio), "Back to Today" label.
  These give good coverage of the lesson-step wiring and the
  `listen_and_answer`/`phrase_match` *renderers* — but the live Phase 3
  finding is that the `?pattern=` *route* (not the lesson-embedded
  `activityId=` route) fails to render, which is a different
  route/component path.
- **listening-comprehension-activity.spec.ts**: covers transcript
  hide/reveal and audio-unavailable fallback — matches Phase 3 observations
  (`?type=ListeningComprehension` rendered correctly with transcript-after-
  submit behavior).
- **vocabulary-practice-activity.spec.ts**: covers render+submit, hints, no
  raw JSON, no console errors — but for the standalone vocabulary practice
  activity, not the `/vocabulary` page (which Phase 3 found loads an empty
  state correctly) nor the lesson Vocabulary teach-page step (which Phase 2
  found blank).
- **speaking-role-play-activity.spec.ts**: covers Practice Gym
  Speaking/Pronunciation card states, `?type=SpeakingRolePlay` rendering,
  "microphone unsupported" message, and a **mocked MediaRecorder** record →
  stop → preview → feedback flow. **Gap**: the mocked-mic test presumably
  exercises a *working* mic path; the real-world "mic permission denied, no
  text fallback" dead end (confirmed in both Phase 2 lesson activities and
  Phase 3 Practice Gym) does not appear to be covered — only "unsupported"
  (no MediaRecorder API), not "denied" (API present, permission refused).
- **progress-page.spec.ts**: covers empty state, real-data summary
  cards/scores/skills/modules, no raw JSON, friendly error on API failure,
  no console errors, mobile no-overflow. Matches Phase 3 findings closely —
  Progress page is well-covered already.
- **today-lesson.spec.ts**, **today-page-identity.spec.ts**,
  **onboarding-experience-step.spec.ts**,
  **onboarding-post-placement-dashboard.spec.ts**,
  **placement-assessment.spec.ts**, **journey-page-identity.spec.ts**,
  **student-nav-structure.spec.ts**, **core-flow-smoke.spec.ts**,
  **disabled-actions-cleanup.spec.ts**, **pattern-evaluation-result.spec.ts**,
  **admin-screenshots.spec.ts**: not reviewed in full detail for this audit;
  names suggest coverage of onboarding flow, dashboard identity, placement,
  journey, nav structure, and admin screenshots — broadly aligned with
  Phase 1/2 areas.
- **No Profile page spec found** (`profile-page.spec.ts` or similar does not
  exist) — the "Current level: Not assessed yet" bug (Phase 3) has no
  existing test coverage to have caught it.
- **No admin password-reset spec** — consistent with the Phase 1 finding
  that no such feature exists.

## Proposed new Playwright tests

Targeted at bugs found across all 3 phases:

1. **`/activity?pattern=<name>` content-renders test** (targets new High
   bug: Practice Gym exercise-type blank pages). For each of
   `teams_chat_simulation`, `gap_fill_workplace_phrase`, `email_reply`,
   `phrase_match`: navigate via the Practice Gym card, wait for
   `GET /api/activity/next?pattern=...` to return 200, then assert the
   activity content area (`<app-activity-teach-page>` or equivalent) has
   non-empty rendered text/elements — not just that the route loads. This is
   the missing assertion in `practice-gym.spec.ts`'s "functional and links
   to pattern activity" tests.
2. **Lesson teach-page content-renders test** (targets existing High bug:
   blank Listening/Vocabulary teach pages). For a lesson Listening/Vocabulary
   step, after "Load activity" → "Open activity", assert
   `<app-activity-teach-page>` renders visible text/audio-player/questions,
   not just that `GET /api/activity/{id}` returns 200.
3. **Speaking mic-denied fallback test** (targets existing High bug: no text
   fallback). Mock `getUserMedia` to reject with `NotAllowedError` (mic
   *denied*, distinct from "unsupported" which is already covered), click
   "Start recording" on both a lesson Speaking step and Practice Gym
   `?type=SpeakingRolePlay`, and assert a text-input fallback appears (as it
   does in the placement assessment's Speaking section) rather than a dead
   end with only "Try again / Back to Today".
4. **Profile "Current level" reflects placement result** (targets new Medium
   bug). After completing placement assessment, navigate to `/profile` and
   assert "Current level" shows the placement's estimated CEFR level (e.g.
   B2), not "Not assessed yet".
5. **Dashboard stats update after Practice Gym activity** (targets Bug #4
   follow-up). Complete a Practice Gym activity, then poll/reload
   `/dashboard` and assert "Practice X activities done" and "Score" update
   within a reasonable time bound (to characterize/regress the propagation
   delay found in Phase 3).
6. **Onboarding step-1 selection persists across refresh/back** (targets
   existing Medium bug). Select the language path on step 1, reload, and
   assert the selection and "Continue" enabled state persist.
7. **Listening free-text "reply" feedback references submission** (targets
   AI-quality gap). Submit a free-text reply with an intentional error
   (e.g. a misspelling) to a Listening comprehension activity, and assert the
   returned feedback text differs based on submission content (e.g. not
   identical across two different submissions) — guards against the generic
   boilerplate feedback observed in Phase 3.
8. **Vocabulary list populates after Writing activity completion** (new
   coverage gap, not a confirmed bug). After completing 1+ Writing
   activities, assert `/vocabulary` no longer shows the empty state.

## Recommended next sprint priorities

Prioritized per the original audit spec's 8 categories:

1. **Critical blockers**
   - Add an admin-initiated student password-reset capability and/or a
     "Forgot password" flow on `/login`. This blocked Phase 2 entirely for
     student #1 and leaves any locked-out student with no recovery path.
     (Existing High bug, Phase 1.)

2. **High-impact student learning flow bugs**
   - Fix the blank-activity rendering bug for **both** affected route
     families: lesson `?activityId=...` teach-pages (Listening/Vocabulary
     steps, Phase 2) and Practice Gym `?pattern=...` activities (all 4
     enabled "by exercise type" cards, Phase 3). Given the backend returns
     200 with valid content in both cases, this is likely the single highest
     -value fix — it currently makes a large fraction of generated content
     unreachable by students.
   - Add a text-input fallback for Speaking activities when microphone
     access is denied — matching the existing fallback already implemented
     in the placement assessment's Speaking section. Affects both lesson
     Speaking steps (Phase 2) and Practice Gym `?type=SpeakingRolePlay`
     (Phase 3) — i.e. every Speaking exercise in the product.

3. **Admin audit/debuggability gaps**
   - Fix `/admin/ai-usage` and `/admin/students/new` direct-navigation
     redirect-to-login (route guard bugs, Phase 1) so admins can
     deep-link/refresh/bookmark these pages.
   - Fix the default-checked "Require password change on first login"
     checkbox in the create-student form, or ensure the resulting forced
     password is reliably surfaced — root cause of student #1's lockout
     (Phase 1).

4. **AI quality**
   - Improve the Listening comprehension activity's free-text "reply"
     feedback to be submission-aware (like the Writing activity's feedback
     already is), instead of generic boilerplate (Phase 3).
   - Investigate `/profile`'s "Current level: Not assessed yet" — wire it to
     the same placement-result data source used by Journey/Progress/Dashboard
     (Phase 3, new Medium bug).
   - Lower priority: monitor for writing/email-task repetition across "By
     skill" cards as more modules are exercised — only 2 data points
     observed in this audit, both email-writing tasks, but plausibly themed
     by the current module rather than systemic.

5. **TTS/audio**
   - No TTS/audio bugs found — audio generation and playback worked
     correctly in both Phase 2 (placement) and Phase 3 (Practice Gym
     Listening). No action needed beyond the loading-state item below.

6. **Performance/loading**
   - Add a loading indicator/spinner for Practice Gym Listening's ~15-16s
     initial generation delay (`?type=ListeningComprehension`) — currently
     appears as a blank page indistinguishable from the blank-activity bug,
     which could confuse both users and future QA/debugging (Phase 3).
   - Characterize and, if needed, fix the dashboard-stats propagation delay
     (Bug #4) — stats did update by the time `/progress` was visited and
     `/dashboard` was reloaded, but the exact trigger/delay is unconfirmed.

7. **Mobile**
   - No student-facing mobile bugs found (Phase 3) — `/dashboard`,
     `/journey`, `/practice`, `/progress`, `/profile`, and the Writing
     activity all render without overflow at 375px and forms are usable.
   - Admin-side mobile overflow on Dashboard and Students list remains open
     (Phase 1, Low severity) — lower priority than student-facing issues.

8. **Playwright coverage**
   - Add the 8 tests proposed above, prioritizing #1-#3 (directly tied to
     the two High bugs and the Speaking fallback gap) since existing specs
     give a false sense of coverage for exactly these flows (link/route
     tests pass while content rendering silently fails).
