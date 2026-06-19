# Phase 10X-K-15 — Dashboard Admin Page Polish Review

**Date:** 2026-06-19
**Sprint/Phase:** 10X-K-15
**Author:** Claude (Sonnet 4.6)

---

## Files Reviewed

- `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` (AdminStats, StudentListItem)
- `src/LinguaCoach.Web/src/app/admin/utils/admin-badge.utils.ts` (onboardingLabel, onboardingTone)

## Files Changed

- **Modified:** `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`
- **Created:** `src/LinguaCoach.Web/src/app/features/admin/admin-dashboard/admin-dashboard.component.spec.ts`

---

## Page Improvements Made

### KPI cards — use AdminStats instead of derived counts

Before: "Total students" and "Onboarded" KPI cards derived counts from `students()` signal, loading was tied to `loadingStudents()`, causing inconsistency when stats loaded at a different speed than the student list.

After: Both cards use `stats()` signal (`stats()?.totalStudents`, `stats()?.onboardedStudents`) with `[loading]="loadingStats()"`. This is correct — `AdminStats` is the authoritative source for these counts, not a client-side filter of the list.

### Onboarding badge — fixed incorrect status value

Before: `s.onboardingStatus === 'Complete'` — the status value `'Complete'` does not exist. Real values are `'Completed'`, `'NotStarted'`, `'InProgress'`.

After: Uses shared `onboardingLabel(s.onboardingStatus)` and `onboardingTone(s.onboardingStatus)` from `admin-badge.utils.ts`, matching the Students page exactly.

### AI System card — raw dot span replaced

Before: `<span slot="actions" class="sp-admin-status-dot sp-admin-status-dot-green">` — custom one-off dot classes with manual green styling.

After: `<sp-admin-badge slot="actions" tone="success" [dot]="true">Online</sp-admin-badge>` — uses the shared badge dot pattern. Each status row also gets `[dot]="true"`.

### Placeholder analytics cards removed

The three `[dashed]="true"` cards ("Usage analytics", "Learning progress", "Feedback quality") with emoji icons and "Coming soon" / "Not tracked yet" text were removed. These were explicitly placeholder-looking with no useful data. The bottom section now renders only the AI System card, eliminating the awkward two-column layout that left a large empty analytics column.

### Bottom layout simplified

Before: `sp-admin-dash-bottom` two-column grid (280px AI System + 1fr analytics grid).

After: `sp-admin-card` for AI System at full width without a wrapping grid. Cleaner, no awkward empty space.

### Manage AI config link

Before: `<a class="sp-admin-link" style="font-size:13px;margin-top:12px;display:inline-block">` — inline styles.

After: `<a routerLink="/admin/ai-config" class="sp-admin-link">` inside a `<div class="mt-3">` — same link style, no inline style.

### Removed dead code / styles

- Removed `onboardedCount()` helper (replaced by `stats()?.onboardedStudents`).
- Removed from styles: `.sp-admin-dash-bottom`, `.sp-admin-status-dot`, `.sp-admin-status-dot-green`, `.sp-admin-analytics-grid`, `.sp-admin-placeholder-icon/title/desc`, `.sp-admin-btn-sm`, `.sp-admin-table-scroll`.
- Removed unused `SpAdminButtonComponent` import (added, then removed when `routerLink` incompatibility was found).

---

## Wrappers / Helpers Used

- `sp-admin-page-header` — unchanged
- `sp-admin-page-body` — unchanged
- `sp-admin-stat-card` — fixed to use AdminStats; `[loading]` now consistent
- `sp-admin-card` — unchanged (Quick actions, Recent students, AI System)
- `sp-admin-action-card` — unchanged
- `sp-admin-table` — unchanged
- `sp-admin-badge` — extended: `[dot]="true"` on AI System online badge and all status rows; onboarding badge now uses shared utils
- `sp-admin-loading-state` — unchanged
- `sp-admin-empty-state` — unchanged
- `onboardingLabel` / `onboardingTone` from `admin-badge.utils` — added to fix incorrect status comparison

---

## Build Result

```
✔ Production build succeeded
Output: dist/lingua-coach.web
```

No errors. Pre-existing empty sub-selector warnings only.

---

## Angular Test Result

```
TOTAL: 606 SUCCESS
Chrome Headless 149.0.0.0 (Windows 10)
```

606 tests pass (14 new dashboard tests added).

---

## Playwright Result

Not run. No existing Playwright tests target the Dashboard admin page. No Playwright tests were affected.

---

## Remaining Dashboard Issues

None blocking. Deferred by scope:

- Charts / usage analytics — explicitly out of scope; placeholder cards removed intentionally.
- Real-time AI system health from API — current "Active" badges are static. A future phase could wire these to the `/admin/ai-categories` endpoint.

---

## Confirmation

- **No backend/API/product behavior changed.** `listStudents()` and `getStats()` calls unchanged. KPI values now sourced from `AdminStats` (same API response, different field). The `onboardedCount()` client-side filter removal is not a behavior change — it was wrong (comparing against `'Complete'` which never matched).
- **No commit or push made.**

---

## Next Recommended Action

Phase 10X-K-15 complete. All admin pages in the batch-2 cleanup scope are done. Ready to commit/push batch 3 when ready.
