# Deployed Student E2E Audit — 2026-06-11

**Test account:** aliverdi.ehsan@hutchisonports.com.au (password NOT recorded)
**Environment URL:** https://speakpath.app/
**Date/Time:** 2026-06-11, approx. 07:20–08:00 AEST (UTC+10)
**Tester:** Claude Code QA agent (gstack /qa skill, v1.57.9.0)
**Branch tested:** production (deployed main)

---

## Summary Table

| Page / Area | Tested | Status | Notes |
|---|---|---|---|
| Landing page | ✅ | Pass | Clean, correct marketing copy |
| Login | ✅ | Pass | Redirects correctly to dashboard |
| Today / Dashboard | ✅ | Partial | Streak shows "--" (not 0) |
| Journey | ✅ | Pass | Module list, progress, placement summary all render |
| Practice Gym | ✅ | Partial | Several activity types fall back to empty fallback |
| Progress | ✅ | Pass | Scores, modules, skill profile all visible |
| Profile | ✅ | Bug | Content clipped by open sidebar; level shows "Not assessed yet" despite placement |
| Lesson view | ✅ | Bug | Content clipped by sidebar; steps 2–4 locked until step 1 complete |
| Writing activity | ✅ | Pass | AI generation, submission, feedback all work |
| Listening activity | ✅ | Critical bug | Audio 401 on every load; TTS inaccessible |
| Vocabulary (Phrase Match) | ✅ | Bug | Loads with content but submit returns 400; no feedback rendered |
| Vocabulary (Fallback) | ✅ | Critical bug | Empty — no phrases or meanings rendered |
| Gap Fill | ✅ | Critical bug | Renders as empty vocabulary fallback — no blanks |
| Email (Writing pattern) | ✅ | Pass | Loads, submits, feedback works (fallback level A2) |
| Workplace Chat | ✅ | Pass | Loads, submits, feedback works (fallback level B1) |
| Activity history / review | ✅ | Pass | Past attempts, scores, and feedback all visible |
| Mobile – Login | ✅ | Pass | Renders correctly at 375px |
| Mobile – Dashboard | ✅ | Partial | Header username text wraps over streak badge |
| Mobile – Journey | ✅ | Pass | Scrollable, all modules visible |
| Mobile – Activity page | ✅ | Critical bug | Completely blank — skeleton placeholders only, no content |

---

## Bugs by Severity

---

### 🔴 CRITICAL

---

#### BUG-001: Audio / TTS endpoint returns 401 on every listening activity load

**Severity:** Critical
**Area:** Backend / TTS
**Page/route:** `/activity?type=ListeningComprehension` (and any activity with audio)
**Steps to reproduce:**
1. Log in as student.
2. Go to Practice Gym → Listening.
3. Activity loads; audio player renders with duration 0:00.
4. Check browser console.

**Expected:** Audio plays from TTS-generated MP3.
**Actual:** Multiple 401 errors on `GET /api/activity/{id}/audio`. Audio player shows 0:00. No sound plays. Student cannot do the listening component.
**Console errors:**
```
Failed to load resource: the server responded with a status of 401 ()   (×5+)
```
**Network/API errors:** `GET /api/activity/333d3bd8-64a2-4b89-962a-4077c74ec18c/audio → 401`
**Screenshot:** `screenshots/11-listening-activity.png`
**Likely cause:** The audio endpoint requires a different auth check (e.g. Bearer token in Authorization header) but the frontend sends only the session cookie. Cookie-based auth works for all other endpoints; media streaming may use a different middleware or may be checking a signed URL / separate token that isn't being generated correctly.
**Suggested fix:** Verify `/api/activity/{id}/audio` handler uses the same cookie-based auth middleware as all other student API routes. If it uses a signed URL scheme, ensure the frontend requests and attaches the signed token before rendering the audio element.

---

#### BUG-002: Vocabulary phrase-match and Gap Fill activities render empty ("Fallback activity" badge, no content)

**Severity:** Critical
**Area:** Backend / AI generation
**Page/route:** `/activity?activityId=...` (lesson vocabulary step), `/activity?pattern=gap_fill_workplace_phrase`
**Steps to reproduce:**
1. Log in → Today → Resume lesson → Open Activity (vocabulary warm-up).
2. Also: Practice Gym → Gap Fill.

**Expected:** A phrase-match grid (phrases on left, meanings on right) or gap-fill blanks.
**Actual:** Page shows "Fallback activity" badge, "Match these workplace phrases to their meanings." instruction, empty PHRASE and MEANING columns, "Check matches" button disabled. Cannot be completed.
**Console errors:** None.
**Network/API errors:** `GET /api/activity/{id}` responds but returns an activity record with no `pairs` / `items` payload. The fallback renderer activates.
**Screenshot:** `screenshots/09-vocab-activity.png`, `screenshots/10-vocab-empty-fallback-ISSUE.png`, `screenshots/17-gapfill-empty-ISSUE.png`
**Likely cause:** The AI generation pipeline for `PhraseMatch` / `GapFill` activity types is failing silently. The activity record is created but the AI-generated content (phrase pairs, blanks) is missing or stored under an unexpected field name. The "Fallback activity" badge indicates the frontend detected an absent `activityType`-specific payload and rendered a generic shell.
**Suggested fix:** Check the activity generation worker for `PhraseMatch` and `GapFill` types. Inspect the stored record for activity `ce8918e4-05b8-43cf-bd1d-cf99bcf385e5` in the database — if `pairs` is null/empty, the generation call failed. Add generation failure logging and a retry path. Ensure the fallback renders a user-friendly error rather than an unusable empty form.

---

#### BUG-003: Activity page completely blank on mobile (375px viewport)

**Severity:** Critical
**Area:** Frontend / Responsive
**Page/route:** `/activity?type=WritingScenario` (and likely all activity routes) at ≤375px
**Steps to reproduce:**
1. Set viewport to 375×812.
2. Navigate to any `/activity` URL while logged in.
3. Page renders only three grey skeleton/placeholder boxes and bottom nav. No text, no inputs, no content.

**Expected:** Activity content renders in a mobile-friendly layout.
**Actual:** Three empty grey placeholder boxes fill the screen. No heading, no scenario text, no textarea, no buttons (except the bottom nav). The accessibility tree shows only the navigation items — the main content element is empty.
**Console errors:** None (stale 401s from earlier session).
**Screenshot:** `screenshots/25-mobile-writing-activity.png`
**Likely cause:** The activity page component likely has a CSS breakpoint or a conditional render that hides the main content wrapper below a minimum width, or the content relies on a JS-driven layout that fails to initialize at narrow viewport. Could also be a hydration failure specific to narrow viewports. The skeleton loaders render but the actual content never replaces them.
**Suggested fix:** Test the activity route at 375px in a local dev environment. Check for `min-width` guards on the activity container. Verify skeleton-to-content transitions fire correctly at narrow viewports.

---

### 🟠 HIGH

---

#### BUG-004: Phrase Match submit returns 400 — no feedback shown after matching

**Severity:** High
**Area:** Backend / API
**Page/route:** `/activity?pattern=phrase_match`
**Steps to reproduce:**
1. Practice Gym → Phrase Match.
2. Match both phrase pairs (they turn green).
3. Click "Check matches".

**Expected:** Step advances to Feedback (step 3); score and coaching shown.
**Actual:** Step indicator moves to "Practice" (step 2) but the view stays on the match grid. The pairs remain green/selected. No score, no feedback. Console shows `400` then `405` on the attempt endpoint.
**Console errors:** `Failed to load resource: the server responded with a status of 400 ()` then `405`
**Network/API errors:** `POST /api/activity/{id}/attempt → 400`, then `GET /api/activity/{id}/attempt → 405`
**Screenshot:** `screenshots/19-phrasematch-result.png`
**Likely cause:** The attempt submission payload for phrase-match is malformed (400), or the backend rejects the `matches` format. The subsequent 405 is the frontend retrying with the wrong HTTP method. The activity title also changed to "VocabularyPractice: Professional email writing" after the check — a different activity was loaded instead of advancing to feedback.
**Suggested fix:** Inspect what the frontend sends to `POST /api/activity/{id}/attempt` for phrase-match and compare against the API contract. Likely the `matches` array field name or shape has drifted. Add an integration test for the phrase-match submission path.

---

#### BUG-005: "Next activity" after listening feedback returns the same activity

**Severity:** High
**Area:** Frontend / Backend
**Page/route:** `/activity?type=ListeningComprehension`
**Steps to reproduce:**
1. Complete a listening activity (answer all questions, click "Check understanding").
2. Feedback screen shows. Click "Next activity".

**Expected:** A new, different activity loads.
**Actual:** The same listening activity scenario re-loads from step 1 ("Listen to the message, then answer the questions.").
**Console errors:** None new.
**Screenshot:** N/A (same view as `screenshots/11-listening-activity.png`)
**Likely cause:** The "next activity" API call (`GET /api/activity/next?type=ListeningComprehension`) returns the same activity ID, likely because the current activity ID is not being excluded from the query, or the student's recently-completed list isn't being updated before the next-activity fetch.
**Suggested fix:** Ensure the attempt is saved to the student's history before the next-activity fetch, and that the next-activity query filters out recently completed activity IDs.

---

#### BUG-006: Profile page shows "Current level: Not assessed yet" despite completed placement

**Severity:** High
**Area:** Frontend / Data
**Page/route:** `/profile`
**Steps to reproduce:**
1. Log in. Navigate to Profile.

**Expected:** Current level shows "B2+" (matching placement result visible on Journey and Progress pages).
**Actual:** Shows "Current level: Not assessed yet".
**Console errors:** None.
**Screenshot:** `screenshots/07-profile.png`
**Likely cause:** The profile page reads from a different data field than Journey/Progress. The placement result is stored as a raw string in the student's placement record but the profile page may be reading a separate `currentLevel` field that was never populated after the placement assessment was completed.
**Suggested fix:** The profile `currentLevel` display should read from the same source as the Journey/Progress placement summary — likely the student's most recent completed placement attempt result. Check the profile API response and align the field mapping.

---

#### BUG-007: Streak counter shows "--" instead of 0 or a number

**Severity:** High
**Area:** Frontend / Data
**Page/route:** `/dashboard`, header streak badge
**Steps to reproduce:**
1. Log in. View the dashboard.

**Expected:** Streak shows a number (0 if no streak, or actual day count).
**Actual:** Shows "-- day streak" on both the sidebar badge and the dashboard stats card.
**Console errors:** None.
**Screenshot:** `screenshots/03-dashboard-today.png`
**Likely cause:** The streak API endpoint is returning `null` or an unexpected shape, and the frontend renders `--` as a null-guard fallback. The streak calculation may require a daily session record that doesn't exist yet for this account.
**Suggested fix:** Treat null streak as 0 in the frontend. Verify the streak calculation endpoint handles accounts with no streak history gracefully.

---

### 🟡 MEDIUM

---

#### BUG-008: Content clipped by open sidebar on Profile, Lesson, and Listening Feedback pages

**Severity:** Medium
**Area:** Frontend / Layout
**Page/route:** `/profile`, `/lesson/{id}`, `/activity` (feedback step)
**Steps to reproduce:**
1. Log in on a 1280px desktop viewport with the sidebar open (default).
2. Navigate to Profile, a lesson, or listening feedback.

**Expected:** Main content area renders to the right of the sidebar without overlap.
**Actual:** The main content area starts at `left: 0` or a fixed position that places it under the open sidebar. Labels and left-side text are visually cut off behind the sidebar panel.
**Console errors:** None.
**Screenshot:** `screenshots/07-profile.png`, `screenshots/08-lesson-overview.png`, `screenshots/13-listening-feedback.png`
**Likely cause:** The `main` content area on these pages does not apply the sidebar-offset margin/padding that the dashboard, journey, practice, and progress pages apply. Likely a missing CSS class or a layout component that isn't wrapping these routes.
**Suggested fix:** Apply the same sidebar-aware layout wrapper used on the dashboard to all authenticated pages. Check whether Profile, Lesson, and Activity routes are wrapped in the standard shell layout component.

---

#### BUG-009: Fallback activities served at wrong CEFR level (A2 for B2+ student)

**Severity:** Medium
**Area:** AI generation
**Page/route:** `/activity?pattern=email_reply`, `/activity?pattern=phrase_match`
**Steps to reproduce:**
1. Log in (student with B2+ placement).
2. Practice Gym → Email or Phrase Match.

**Expected:** AI-generated activity at approximately B2 level.
**Actual:** Both activities show "Fallback activity A2" badge. The content (phrases like "I would like to follow up", "Please let me know") is basic A2 material, significantly below the student's assessed level.
**Console errors:** None.
**Screenshot:** `screenshots/16-email-activity.png`, `screenshots/18-phrasematch-activity.png`
**Likely cause:** Fallback activities are hard-coded at A2 and are served when AI generation fails. The generation is failing silently and the fallback doesn't inherit the student's target level.
**Suggested fix:** Pass the student's CEFR level to the fallback activity selector so the fallback content is level-appropriate. Separately, investigate why AI generation is failing for these activity types.

---

#### BUG-010: Mobile header — username text wraps over streak badge

**Severity:** Medium
**Area:** Frontend / Responsive
**Page/route:** `/dashboard` at 375px
**Steps to reproduce:**
1. Set viewport to 375px.
2. Navigate to dashboard.

**Expected:** Header shows username cleanly truncated with streak badge.
**Actual:** "Hi, aliverdi.ehsan" text wraps onto a second line, visually overlapping the streak badge in the top-right area.
**Console errors:** None.
**Screenshot:** `screenshots/23-mobile-dashboard.png`
**Likely cause:** The greeting text uses the full username without truncation and the header flex container doesn't constrain the text width at narrow viewports.
**Suggested fix:** Add `text-overflow: ellipsis; overflow: hidden; max-width: ...` to the greeting/username span on mobile, or shorten the greeting to first name only below a breakpoint.

---

#### BUG-011: AI evaluation awarded score 0 with minimal feedback ("The submission was received")

**Severity:** Medium
**Area:** AI generation / Evaluation
**Page/route:** `/activity/ab38fd37-4b38-44fe-aaef-e73cbde657eb/history` (Attempt 1, 4 Jun)
**Steps to reproduce:** Visible in activity history for "Updating Project Document Statuses".

**Expected:** Meaningful coaching feedback with a proportional score.
**Actual:** Score is 0, feedback reads "The submission was received." No "What you did well" content, no suggested changes.
**Console errors:** None.
**Screenshot:** Visible in history at `screenshots/22-activity-review-history.png` (scroll to the 0-score card)
**Likely cause:** The AI evaluator prompt either received an empty submission or timed out and returned a degenerate result. The "submission was received" text looks like an error-path default inserted when the LLM call failed or returned an empty response.
**Suggested fix:** Add a minimum-quality guard on the evaluator output: if score is 0 and feedback is fewer than N words, retry or flag for manual review. Ensure the evaluator returns a useful default when the LLM times out.

---

### 🔵 LOW

---

#### BUG-012: "Continue my learning path" button is always disabled on Journey page

**Severity:** Low
**Area:** Frontend / Feature
**Page/route:** `/journey`
**Steps to reproduce:** Scroll to bottom of Journey page.

**Expected:** Button enabled when the student has completed enough modules to warrant extending the path.
**Actual:** Button is permanently disabled with text "Add a small set of recommended modules based on your learning memory and recent progress."
**Console errors:** None.
**Likely cause:** The generate-next-path feature is not yet fully implemented, or the precondition check always returns false. The button exists in the UI but is gated.
**Suggested fix:** Either remove the button until the feature is ready, or implement the path generation trigger so it activates when the student has completed the current module set.

---

#### BUG-013: Progress page shows "0 this week" despite recent activity

**Severity:** Low
**Area:** Frontend / Data
**Page/route:** `/progress`
**Steps to reproduce:** Log in and view Progress page.

**Expected:** "This week" count reflects activities completed in the current calendar week.
**Actual:** Shows "0 this week" even though the student completed 23 total activities, with several done in the past 7 days (visible in the "Recent scores" list including entries from 7 Jun).
**Console errors:** None.
**Likely cause:** The "this week" counter uses a strict ISO week boundary (Mon–Sun UTC) and activities on the boundary may not be counted, or the timezone offset is causing all recent activities to fall into the previous week.
**Suggested fix:** Log the timezone used in the weekly aggregate query and confirm it matches the user's local timezone or at least AEST for this pilot.

---

## Health Score

| Category | Score | Notes |
|---|---|---|
| Console errors | 40 | Persistent 401s on audio endpoint across all activity pages |
| Functional | 45 | 3 critical functional failures: audio, vocabulary/gap-fill content, mobile activity blank |
| UX | 65 | Sidebar overlap on 3 pages; header wrap on mobile |
| Content/AI | 60 | Fallback at wrong level; 0-score edge case; "Next activity" loops |
| Performance | 85 | Pages load fast; API responses under 200ms |
| Accessibility | 70 | Snapshot tree is clean on working pages; broken on mobile activity |
| Visual | 80 | Design is consistent and polished on pages that render correctly |
| Links | 100 | No broken links found |

**Overall health score: ~63 / 100**

---

## Screenshots Index

| File | Description |
|---|---|
| `01-landing.png` | Public landing page |
| `02-login-page.png` | Login form |
| `03-dashboard-today.png` | Dashboard / Today page (streak "--" visible) |
| `04-journey.png` | Journey / learning path |
| `05-practice.png` | Practice Gym index |
| `06-progress.png` | Progress page |
| `07-profile.png` | Profile page (sidebar clipping visible) |
| `08-lesson-overview.png` | Lesson overview (sidebar clipping visible) |
| `09-vocab-activity.png` | Vocabulary activity — empty fallback (ISSUE) |
| `10-vocab-empty-fallback-ISSUE.png` | Same — closer view |
| `11-listening-activity.png` | Listening activity — audio 0:00 (ISSUE) |
| `12-listening-questions.png` | Listening questions step |
| `13-listening-feedback.png` | Listening feedback (works; sidebar clip visible) |
| `14-writing-activity-intro.png` | Writing activity intro — works |
| `15-writing-feedback.png` | Writing AI feedback — works well |
| `16-email-activity.png` | Email activity — fallback A2 (ISSUE) |
| `17-gapfill-empty-ISSUE.png` | Gap Fill — empty fallback (ISSUE) |
| `18-phrasematch-activity.png` | Phrase Match — loads with content |
| `19-phrasematch-result.png` | Phrase Match — submit fails, no feedback (ISSUE) |
| `20-workplace-chat.png` | Workplace Chat — works |
| `21-vocabulary-page.png` | Vocabulary saved phrases page (empty — no saved phrases yet) |
| `22-activity-review-history.png` | Activity history / review — works |
| `23-mobile-dashboard.png` | Mobile 375px — dashboard (minor header wrap) |
| `24-mobile-journey.png` | Mobile 375px — journey (works) |
| `25-mobile-writing-activity.png` | Mobile 375px — activity blank (CRITICAL ISSUE) |
| `26-mobile-login.png` | Mobile 375px — login (works) |

---

## Recommended Next Sprint Priorities

### P0 — Fix before next user-facing release

1. **BUG-001 — TTS audio 401:** Listening activities are core product. Audio silently failing with 401 means every listening session is broken. Fix the audio endpoint auth middleware.
2. **BUG-002 — Empty vocabulary/gap-fill activities:** Three activity types (Vocabulary Phrase Match in lesson, Gap Fill, and the lesson vocabulary step) render an empty fallback. Students can't complete them. Fix AI generation for `PhraseMatch` and `GapFill` or ensure the fallback at minimum shows a usable activity.
3. **BUG-003 — Mobile activity page blank:** The entire activity experience is inaccessible on mobile. Given the product is aimed at immigrant professionals likely using phones, this is a critical reach failure.

### P1 — Fix in current sprint

4. **BUG-004 — Phrase Match 400 on submit:** Vocabulary matching works visually but can't save or evaluate. Fix the attempt submission payload shape.
5. **BUG-005 — Next activity loops:** The "Next activity" button returning the same activity makes practice feel broken. Fix the next-activity exclusion logic.
6. **BUG-006 — Profile shows "Not assessed yet":** Misaligned data source between profile and journey/progress pages.
7. **BUG-008 — Sidebar overlap on Profile/Lesson/Feedback:** Content is cut off on three pages. Apply the standard layout wrapper.

### P2 — Address in following sprint

8. **BUG-007 — Streak "--":** Minor data display issue but affects motivation. Treat null as 0.
9. **BUG-009 — Fallback activities at wrong level:** A2 content for a B2+ student is demotivating. Pass student level to fallback selector.
10. **BUG-010 — Mobile header overlap:** Minor cosmetic issue; truncate username.
11. **BUG-011 — Zero-score evaluation:** Add quality guard on evaluator output.
12. **BUG-013 — "0 this week" timezone mismatch:** Investigate weekly aggregate timezone.

### P3 — Backlog

13. **BUG-012 — "Continue my learning path" always disabled:** Either implement or remove the button.

---

## Proposed New Playwright Tests

The following tests would catch the most impactful failures found in this audit. These should be added to the existing E2E suite rather than replacing it.

```typescript
// 1. Audio endpoint auth — assert TTS audio loads with a 200 (not 401)
test('listening activity audio endpoint returns 200 for authenticated student', async ({ page, context }) => {
  await loginAsStudent(page);
  const audioResponse = page.waitForResponse(r => r.url().includes('/audio'));
  await page.goto('/activity?type=ListeningComprehension');
  const resp = await audioResponse;
  expect(resp.status()).toBe(200);
});

// 2. Vocabulary/Gap-Fill activity has content
test('vocabulary phrase-match activity renders at least 2 phrase pairs', async ({ page }) => {
  await loginAsStudent(page);
  await page.goto('/activity?pattern=gap_fill_workplace_phrase');
  const phrases = page.locator('[data-testid="phrase-item"], .phrase-button');
  await expect(phrases).toHaveCount(2); // at minimum
});

// 3. Phrase Match submit succeeds (no 400/405)
test('phrase match submit returns 200 and shows feedback', async ({ page }) => {
  await loginAsStudent(page);
  await page.goto('/activity?pattern=phrase_match');
  // match all pairs ... (helper to click phrase then meaning)
  const [response] = await Promise.all([
    page.waitForResponse(r => r.url().includes('/attempt') && r.request().method() === 'POST'),
    page.click('text=Check matches'),
  ]);
  expect(response.status()).toBe(200);
  await expect(page.locator('text=Feedback')).toBeVisible();
});

// 4. Activity page renders on mobile
test('writing activity renders content at 375px viewport', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await loginAsStudent(page);
  await page.goto('/activity?type=WritingScenario');
  await expect(page.locator('h2, [role="heading"]')).toBeVisible();
  await expect(page.locator('textarea, [role="textbox"]')).toBeVisible();
});

// 5. Next activity navigates to a different activity
test('next activity button loads a different scenario', async ({ page }) => {
  await loginAsStudent(page);
  // complete a listening activity...
  const firstTitle = await page.locator('h2').textContent();
  await page.click('text=Next activity');
  await page.waitForURL('**/activity**');
  const secondTitle = await page.locator('h2').textContent();
  expect(secondTitle).not.toBe(firstTitle);
});

// 6. Profile shows assessed level after placement
test('profile page shows assessed level when placement is complete', async ({ page }) => {
  await loginAsStudent(page);
  await page.goto('/profile');
  await expect(page.locator('text=Not assessed yet')).not.toBeVisible();
  await expect(page.locator('text=B2')).toBeVisible(); // or whatever the level is
});
```

---

*Report generated by Claude Code QA agent — gstack /qa skill v1.57.9.0*
*Test account: aliverdi.ehsan@hutchisonports.com.au*
*Screenshots: `.gstack/qa-reports/screenshots/`*
