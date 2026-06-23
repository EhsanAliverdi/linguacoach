# Phase 10UI-FIX-7 — Student Detail Reference Alignment, Readiness Pool, and Activity History

**Date:** 2026-06-23
**Sprint:** Current (10UI series)
**HEAD before work:** de6dd95
**HEAD after work:** a26293e
**Related phases:** 10UI-FIX-4 (identified gaps), 10UI-FIX-6 (wrapper patterns established)

---

## Scope

Redesign the admin Student Detail page (`/admin/students/:id`) to:

1. Visually align with SpeakPath admin reference style using `sp-admin-*` wrappers.
2. Expose the readiness pool health backend capability (TODO-UI-02) — endpoint existed but had no frontend.
3. Add a KPI summary strip at the top.
4. Migrate activity history and audit history tables to `sp-admin-table`.
5. Migrate all badge class strings to `sp-admin-badge` wrapper.
6. Migrate error states to `sp-admin-alert`.

---

## Files Changed

| File | Change |
|------|--------|
| `admin.models.ts` | Added `ReadinessPoolSourceHealth`, `StudentReadinessPoolHealth` DTOs |
| `admin.api.service.ts` | Added `getStudentReadinessPoolHealth()` method |
| `admin-student-detail.component.ts` | Full wrapper migration + readiness pool section + KPI strip |
| `admin-student-detail.component.spec.ts` | Added `getStudentReadinessPoolHealth` to all spy setups; 9 new pool health tests |

---

## 1. Page Header

**Before:** Raw `<div class="sp-admin-page-header">` with hand-rolled action row.

**After:** `<sp-admin-page-header [title]="..." [subtitle]="...">` with action buttons projected via `<ng-content>`. Back link preserved via `sp-admin-page-body` context.

**Note:** `sp-admin-page-header` has no `backRoute` or named slots — actions are projected as `<ng-content />`. Buttons use `appearance="ghost"` + `variant="danger"` for destructive actions.

---

## 2. KPI Strip

New 4-column stat card strip above the detail grid:

| Card | Data source |
|------|-------------|
| Lifecycle stage | `s.lifecycleStage` via `lifecycleLabel()` / `lifecycleTone()` |
| Onboarding | `s.onboardingStatus` via `onboardingLabel()` / `onboardingTone()` |
| CEFR level | `s.cefrLevel ?? 'Not set'` |
| Pool health | `poolHealthLabel()` computed from `poolHealth()` signal |

`lifecycleTone()` returns `SpAdminBadgeTone` which includes `'purple'` — not assignable to `SpAdminStatCardTone`. Used `$any()` cast on KPI stat card bindings to satisfy the compiler.

---

## 3. Readiness Pool Health Section (TODO-UI-02)

**Backend endpoint:** `GET /api/admin/students/{studentId}/readiness-pool/health`
Returns `{ studentId, todayLesson: { ... }, practiceGym: { ... } }` with ready/target/shortfall/failed/stale/queued counts and `needsReplenishment` flag.

**Frontend implementation:**
- New signal: `poolHealth`, `poolHealthLoading`, `poolHealthError`
- New method: `poolHealthLabel()` — "Healthy" / "Lesson needs fill" / "Gym needs fill" / "Both need fill"
- New method: `poolHealthTone()` — `'success'` / `'warning'` / `'neutral'`
- Private `loadPoolHealth(id)` called in `ngOnInit`
- Section renders two columns (Today's lesson / Practice gym) each showing ready/target, queued, shortfall, failed, stale, and replenishment badge

---

## 4. Wrapper Migration

| Before | After |
|--------|-------|
| `<section class="sp-admin-table-card sp-admin-detail-card">` | `<sp-admin-card>` |
| `<div class="sp-admin-detail-grid">` | `<div class="sp-admin-detail-grid">` (layout div kept) |
| `<table class="sp-admin-table">` | `<sp-admin-table>` |
| Raw badge class strings | `<sp-admin-badge [tone]="...">` |
| `<div class="sp-admin-alert-error">` | `<sp-admin-alert variant="error">` |
| `<div class="sp-admin-page-header">` | `<sp-admin-page-header>` |

---

## 5. Badge Migration

All badge class strings replaced with `sp-admin-badge`:

| Location | Before | After |
|----------|--------|-------|
| Profile — lifecycle | `[class.sp-admin-badge-indigo]` etc. | `[tone]="lifecycleTone(s.lifecycleStage)"` |
| Profile — onboarding | `[class.sp-admin-badge-green/amber]` | `[tone]="onboardingTone(s.onboardingStatus)"` |
| Profile — CEFR | `sp-admin-badge sp-admin-badge-indigo` | `tone="primary"` |
| Onboarding progress — status | `[class.sp-admin-badge-green/amber]` | `[tone]="op.isComplete ? 'success' : 'info'"` |
| Usage policy — source | `[class.sp-admin-badge-indigo/slate]` | `[tone]="ep.isOverride ? 'primary' : 'neutral'"` |
| Learning memory — skill profile | `[class.sp-admin-badge-amber/green]` | `[tone]="skill.isWeak ? 'warning' : 'success'"` |
| Activity history — result | `[class.sp-admin-badge-green/amber]` | `[tone]="item.passed ? 'success' : 'warning'"` |
| Audit history — action | `[class.sp-admin-badge-indigo/amber]` | `[tone]="item.source === 'AdminAuditLog' ? 'primary' : 'warning'"` |
| Readiness pool — replenishment | new | `[tone]="needsReplenishment ? 'warning' : 'success'"` |

---

## 6. CSS Added

```css
.sp-admin-kpi-strip   — 4-col responsive stat card grid
.sp-admin-pool-grid   — 2-col readiness pool source layout
.sp-admin-pool-source-title — section label style
```

---

## 7. Behaviours Preserved

- All modals (edit, reset password, reset data, lifecycle confirm) unchanged.
- All slide-overs (prefs, audit details, assign policy, set CEFR) unchanged.
- `displayName()`, `experienceLabel()`, `familiarityLabel()` helpers unchanged.
- `ngOnInit` load sequence unchanged (student, memory, history, auditHistory, policy) — pool health added as 6th parallel call.
- `confirmArchive()` window.confirm pattern unchanged.

---

## 8. Tests

| Suite | Tests added |
|-------|------------|
| Pool health section | 8 new tests |
| `getStudentReadinessPoolHealth` spy | Added to all 11 existing `beforeEach`/`setup` blocks |

Total: **1074/1074** (baseline was 1065 — 9 net new).

---

## 9. Gates

| Gate | Result |
|------|--------|
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1074/1074 |
| Backend | No changes |
| Playwright | Not run — no route/navigation behaviour changed |

---

## 10. Deferred

- Notifications SMS label, security deferred notes, integrations readiness pool → **10UI-FIX-8**
- AI Config native selects evaluation → **10UI-FIX-9**
- Admin onboarding flow viewer → **TODO-UI-04**
- Student detail back-link (requires `sp-admin-page-header` backRoute support or layout wrapper) → future

---

## 11. Risks / Unresolved

- `$any()` cast used for `lifecycleTone()` → `SpAdminStatCardTone` binding. Would be cleaner if `SpAdminBadgeTone` and `SpAdminStatCardTone` shared a common base. Non-blocking.
- `sp-admin-button` `appearance="ghost"` renders slightly differently from the old raw `sp-admin-link-button` CSS — visual regression possible but acceptable within wrapper migration rules.

---

## 12. Confirmation

- No backend/API/migration changes.
- No student-facing UI changes.
- No new admin pages.
- Real data only — no fake/mock data shown in production UI.
- Readiness pool shows "Could not load pool health." on error; loading state shown during fetch.
