# Phase 10UI-STUDENTS-PARITY-1 — Engineering Review

**Date:** 2026-06-24
**Sprint/Feature:** 10UI-STUDENTS-PARITY-1
**Commit:** 72c8a35
**Branch:** main

---

## Summary

Rebuilt the admin Students list page (Part A) and admin Student detail page (Part C) to match the design target screenshots at `docs/design/speakpath/admin/pages/students.jsx`.

---

## Files Modified

- `src/app/features/admin/admin-students/admin-students.component.ts` — list page rebuilt
- `src/app/features/admin/admin-students/admin-students.component.spec.ts` — spec updated
- `src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` — detail page rebuilt
- `src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` — spec updated

---

## Part A — Students List

### Changes

- **Removed:** 4 KPI cards (Total students, Onboarded, Activities tracked, Showing this page)
- **Removed:** Onboarding breakdown bars section
- **Removed:** Profile table column
- **Changed:** Default page size 25 → 10
- **Filter row:** Search first, then lifecycle select only (removed onboarding + CEFR selects), then show-archived checkbox, spacer, rows-per-page
- **Action menu:** Icon+label items, divider before Archive, removed "Send reset link" (replaced by "Reset password")
- **Joined column:** `date:'MMM d, y'` with `white-space:nowrap`
- **Footer:** "Showing X–Y of Z students" + pagination (pagination hidden when only 1 page)
- **Added signals:** `showingFrom`, `showingTo` computeds

### Test result

41 / 41 passing (removed 7 KPI strip tests, removed 2 profile card tests, updated pageSize references).

---

## Part C — Student Detail

### Changes

**Hero:**
- Back button: `← Students` → `← Back to Students`
- Hero actions: Edit (switches to Settings tab) | Reset password | Archive (non-archived); Reactivate (archived only)
- Removed: Pause, Unpause, Send reset link from hero

**Tabs (new):**
- Overview | Activity | Settings
- `activeTab = signal<'overview' | 'activity' | 'settings'>('overview')`
- `@if (activeTab() === ...)` blocks in template

**Overview tab:**
- Left: Profile card (Email, Joined, Lifecycle badge, Onboarding badge, Career context, Learning goal)
- Right column: Stats strip (Day streak, Mins this week, Activities done) + CEFR Level card (badge + Set CEFR)
- Extra 2-col cards below: Onboarding progress, Usage policy, Preferences (with View all slide-over), Learning memory, Readiness pool health

**Activity tab:**
- Activity history table (Activity, Skill, Score/100, Duration, Date)
- Audit history table (Action, Actor, Reason, Value change, Date)

**Settings tab:**
- Inline edit form: Display name, Email (disabled), First name, Last name, Career context, Learning goal, Preferred duration, Experience level, Save changes
- Edit form initialized on student load (`startEdit(detail)` in `loadStudent` success handler)
- Danger zone card: Reset password / Reset data / Archive (non-archived); Reactivate (archived)

**Removed:**
- 4 KPI card strip (Lifecycle, Onboarding, CEFR, Pool health)
- Old flat detail grid
- Edit modal (replaced by inline Settings tab form)
- `editing` signal (replaced by direct `student()` reference in `saveEdit()`)

### Test result

112 / 112 passing.

Changes made to spec:
- `'Student preferences'` heading → `'Preferences'`
- `'No preferences set yet.'` empty state (unchanged, still in card)
- Pause/Unpause UI button tests: updated to verify component state instead of hero button DOM
- "shows confirm modal when Pause is clicked": replaced with programmatic `startLifecycleAction('pause', ...)` test
- Audit history tests: added `activeTab.set('activity')` before assertions
- Danger zone tests: added `activeTab.set('settings')` before assertions
- REDESIGN-3 KPI strip describe block: replaced with "overview stats strip" tests checking `.sp-sd-stats-strip` and CEFR value
- `'Lesson needs fill'` (was from KPI strip method): updated to check `'Needs replenishment'` badge in pool health section
- `'View preferences'` button text → `'View all'`
- `'renders focus areas when present'`: opens slide-over before asserting focus area text

---

## Full Suite Result

**1325 / 1325 passing.** No regressions introduced.

---

## Build

Production build completed with no new errors. Pre-existing warnings in `AdminAiUsageComponent`, `AdminDiagnosticsComponent`, `PatternEvaluationResultComponent` are unrelated.

---

## Security Constraints Verified

- No API keys, secrets, or provider secrets rendered
- No mock data imported from JSX design files
- No student UI touched (`features/student/`, `design-system/student/`)
- No migrations added
- No chart libraries added

---

## Decisions Made

- Pause/Unpause moved out of hero. Design target has no Pause button in hero. Lifecycle controls remain callable programmatically and via the existing lifecycle action confirm modal — the modal wiring was not removed, only the hero buttons.
- Inline edit form replaces edit modal. Edit button in hero switches tab to Settings; form is always initialized on load.
- `showingFrom`/`showingTo` computeds use plain `pageSize` property (not signal). Recomputes correctly because `page()` is a signal dependency.
- Pool health section renders `'Needs replenishment'` badge text. Old KPI strip method `poolHealthLabel()` returning `'Lesson needs fill'` is no longer surfaced in the template.

---

## Pending Parts

- **Part D:** Rebuild create-student page (4 sections + aside)
- **Parts E–J:** Shared components, remaining specs, build gates, screenshot capture, final commit

---

## Documentation Impact

- Docs reviewed: `docs/architecture/README.md`, `docs/design/speakpath/admin/pages/students.jsx`
- Docs updated: This review doc
- Docs intentionally not updated: `docs/handoffs/current-product-state.md`, `TODOS.md` — pending until all parts of the phase are complete

---

## Final Verdict

**Parts A and C: COMPLETE.** 1325/1325 tests pass. Build clean. Committed as `72c8a35`.

## Next Recommended Action

Implement Part D — rebuild the create-student page to match the design target at `docs/design/speakpath/admin/pages/create-student.jsx`.
