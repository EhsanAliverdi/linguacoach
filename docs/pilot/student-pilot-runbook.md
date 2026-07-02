---
status: current
lastUpdated: 2026-07-02 (Phase 20F)
owner: product
---

# Student Pilot Runbook

How to bring one real student onto SpeakPath safely, using the Phase 20D
readiness/repair tooling and the Phase 20B/20C runtime settings admin
pages. Written after running this process end-to-end in production during
Phase 20E — see
`docs/reviews/2026-07-02-phase-20e-controlled-student-pilot-smoke-qa-review.md`
for the full findings from that run.

**Current status: NOT ready to invite a real student.** See "Known
limitations" below before doing anything else with this runbook.

## 1. Create or select a pilot student

**Fresh student (recommended for a first pilot):**

1. Log in to `/admin` as an Admin-role account.
2. Go to **Students → Create student** (`/admin/create-student`).
3. Enter the student's real email and a temporary password (10+ chars,
   upper/lower/digit/special). Leave **"Require password change on first
   login"** checked — this is the safe, realistic default; only uncheck it
   for a disposable test account you control end-to-end yourself.
4. Click **Create student**. The temporary password is shown once — save
   it before sharing with the student (share it out-of-band, not in this
   tool).
5. Find the student in **Students** (search by email) → **Row actions
   (•••) → View profile** to get to their Admin Student Detail page and
   note their `StudentId` (from the URL) for the next step.

**Existing student:** search for them directly in **Students**, click
**View profile**.

## 2. Run the readiness audit

On Admin Student Detail (`/admin/students/{id}`), scroll to the **Pilot
readiness** card. It loads automatically and shows:

- A verdict badge: **Ready** / **Needs attention** / **Blocked**.
- Blocking / warning / info counts.
- An expandable list of ~20 individual checks across 10 categories
  (account, placement/CEFR, Learning Plan, course readiness, Today lesson,
  Practice Gym, activity content, audio/TTS, feedback/review scaffold,
  progress).
- A list of recommended repair actions, if any check found something
  repairable.

If the card instead shows **"Pilot readiness unavailable — Could not load
pilot readiness"**, the audit endpoint itself is failing — **do not
proceed with this runbook**. This was the exact failure found in Phase
20E (see "Known limitations"). Escalate to an engineer with production
log/DB access before continuing.

## 3. Dry-run repairs first

For each recommended repair action shown on the card, click it — this
opens the repair slide-over. Leave **Dry run** selected (the default) and
run it first. Dry run:

- Never writes to the database.
- Shows a before/after preview of what the repair would change.
- Is safe to run as many times as you like.

Read the preview carefully. If the "after" state looks correct, proceed
to step 4. If it looks wrong, stop and escalate — do not run the real
repair on an unexpected preview.

## 4. Run real repairs only where safe and necessary

Switch the slide-over to **Real run**, type a short reason (required),
and confirm. Real repairs:

- Write exactly one `AdminAuditLog` entry each.
- Are idempotent — running the same repair again when nothing is left to
  fix is a safe no-op.
- **Never** delete or modify any `ActivityAttempt`, submission, or
  evaluation. Never touch AI scoring, CEFR, objective-completion, or
  Learning Plan regeneration-from-AI logic.

Five repair actions are implemented today: generate a missing Learning
Plan, refill an empty Today lesson, expire CEFR-mismatched readiness
items, expire stale reserved items, and "run all four in sequence." Five
more (`refill_practice_gym_if_empty`, `backfill_missing_activity_metadata`,
`regenerate_missing_tts_for_listening_if_supported`,
`normalize_student_lifecycle_if_safe`, `refresh_progress_projection_if_supported`)
are **not implemented yet** — the card will say so; do not expect a button
for these. See `TODOS.md` (Phase 20D section) for why each is deferred.

After running real repairs, the readiness card auto-refreshes. Confirm the
verdict improved (or note in your pilot log why it didn't).

## 5. Walk the student routes yourself

Even with a **Ready** verdict, walk the actual student experience once
before inviting a real student — the readiness audit checks *can the
system serve this student*, not *does the UI look right*. Log in as the
student (or use their temporary password once, then have them change it)
and check, in order:

| Route | What "pass" looks like |
|---|---|
| `/login` → forced `/change-password` (first login only) | Student sets their own password, redirected onward |
| `/onboarding/step-1` … `/onboarding/step-5` | All steps completable, no crash, ends at `/placement` (or skips straight to dashboard if profile fields were pre-filled at creation) |
| `/placement` | "Start placement" begins a real adaptive assessment; completes in 5–10 minutes; ends with a CEFR result screen |
| `/dashboard` | Shows Today's lesson (or an honest "being prepared" message, never a red error) |
| Today lesson / activity player (reached from the dashboard, route is `/lesson/:sessionId` then `/activity`) | At least one activity type renders and can be completed |
| Feedback (a step inside the activity flow, not a standalone URL) | Shows either real feedback or an honest "Feedback pending" card — never a blank page |
| `/practice` | Practice Gym shows ready suggestions, or a clear non-error "being prepared" state |
| `/journey` | Shows the learning plan roadmap, or an honest "being prepared" message |
| `/progress` | Shows CEFR/skill/mastery summary, or an honest "not available yet" message — **never** a raw exception message (this was a real bug, fixed in Phase 20E) |
| `/profile` | Account, placement summary, and preferences all load |

## 6. Check relevant runtime settings

Before inviting a student, confirm on **Feature Gates**
(`/admin/settings/feature-gates`) that these are set the way you intend
for this pilot (do not change production defaults unless the pilot
specifically requires it, and document any change you do make):

- `PracticeGymPilotEnabled` — student-visible Practice Gym pilot label.
- `EnableReviewScaffoldGeneration` / `DryRunOnly` / `RequireAdminReview` —
  keep `DryRunOnly=true` unless you specifically intend live
  review-scaffold generation for this pilot.
- `AllowTodayLessonInsertion` — keep disabled unless intentionally testing
  scaffold insertion into Today lessons.
- Lesson/readiness refill settings (buffer sizes, thresholds) — only
  change if the readiness audit flagged a shortfall repair wasn't enough.

Any setting you change for a pilot: write down the old value, the new
value, and when you'll revert it (or confirm it's meant to be permanent).

## 7. Pass/fail criteria for "ready to invite one student"

All of the following must be true:

- [ ] Readiness audit loads (no 500) and shows **Ready** or **Needs
      attention with only non-blocking warnings you've reviewed**.
- [ ] Student can log in and (if applicable) complete forced password
      change.
- [ ] Student can complete onboarding.
- [ ] Student can start **and complete** a placement assessment.
- [ ] Dashboard loads without a broken/red-error card.
- [ ] Today lesson loads or gives an honest non-error "preparing" message.
- [ ] Student can complete at least one activity end-to-end.
- [ ] Feedback appears (or an honest "pending" state — never blank/error).
- [ ] Practice Gym loads or gives an honest non-error reason.
- [ ] Journey, Progress, and Profile all load without a raw error message.
- [ ] No pending/rejected review-scaffold item is visible to the student.
- [ ] Admin can see the readiness verdict and repair history for this
      student.

## 8. Known limitations (as of Phase 20F, 2026-07-02)

- **Fixed in Phase 20F, pending live confirmation (`TODO-20F-1`):**
  `POST /api/student/placement/start` and the readiness audit both 500'd
  in production for every student. Root cause: 6 EF Core migrations had no
  `.Designer.cs` file and were silently invisible to
  `dotnet ef database update`/`Database.Migrate()` — not a failure, just
  never applied, on every environment, forever. Fixed by restoring the
  missing Designer.cs files and making the affected migrations'
  idempotent. Validated locally against a fresh database and a database
  independently drifted to match production's exact symptom; **not yet
  directly confirmed against `https://speakpath.app`** (no production
  DB/SSH/log access was available in that session — the fix ships via the
  normal `main` → CI/CD deploy pipeline). See
  `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.
  **Until `TODO-20F-1` is confirmed, treat this runbook the same as before:
  do not invite a real student.**
- The **Pilot readiness card and Speaking Submissions / Writing
  Evaluations sub-widgets on Admin Student Detail** were casualties of the
  same root cause and should now also work once the fix is live — verify
  this alongside `TODO-20F-1`.
- `refill_practice_gym_if_empty`, `backfill_missing_activity_metadata`,
  `regenerate_missing_tts_for_listening_if_supported`,
  `normalize_student_lifecycle_if_safe` are not implemented yet — a
  readiness check that recommends one of these cannot be repaired from the
  UI today.
- `repairAllSafeStudentReadiness` (the "run all" API) exists but is not
  currently wired to a button in the Admin Student Detail UI — run the
  four individual safe repairs one at a time instead, or call the API
  directly (`POST /api/admin/students/{id}/readiness/repair-safe-all`).

## 9. Reset / cleanup notes

- Repair actions never delete `ActivityAttempt`, submission, or evaluation
  history — there is nothing to "undo" from a repair beyond re-running the
  audit.
- If you created a disposable pilot/test student and want it gone,
  **Archive** them from **Students → Row actions → Archive** — this
  repo's policy is to never hard-delete student data. Archived students
  are excluded from the default Students list (toggle "Show archived" to
  find them again).
- Any runtime setting you changed for testing (step 6): revert it via
  Feature Gates once the pilot session is done, unless you've decided to
  keep it.

## 10. Final checklist: ready to invite one student?

As of 2026-07-02 (Phase 20F): **Not yet — pending live confirmation.** The
production placement/readiness fix (see "Known limitations") has been
pushed but not directly confirmed against `https://speakpath.app`. Once
`TODO-20F-1` passes, re-run this runbook's steps 2–7 against
`pilot.student.20e@speakpath.app`
(`c2a7caff-b46a-4da4-b424-8bd5ca8c0394`, already created and sitting at
"onboarding complete, placement not started" in production), and only
mark this checklist complete when every box in section 7 is checked.
