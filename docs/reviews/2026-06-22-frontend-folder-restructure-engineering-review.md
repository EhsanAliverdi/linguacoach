---
status: current
lastUpdated: 2026-06-22 00:00
owner: engineering
supersedes:
supersededBy:
---

# Frontend Folder Restructure — Engineering Review

**Date:** 2026-06-22
**Related sprint:** Frontend architecture cleanup (no sprint number — structural refactor)
**Type:** Engineering review / implementation record

---

## Summary

The Angular frontend at `src/LinguaCoach.Web/src/app/` was restructured into a three-area architecture: `design-system/`, `features/`, and `core/`. This was a pure structural refactor. No behaviour changed. No new features were added.

---

## What Changed

### Before

```
app/
├── admin/              — design system components (sp-admin-*)
├── shared/
│   ├── student-ui/     — shared student-facing UI components
│   └── notifications/  — notification dropdown
├── layouts/            — PublicLayout, StudentAppLayout, AdminAppLayout
├── features/           — flat list of all feature folders
│   ├── admin/
│   ├── activity/
│   ├── assessment/
│   ├── auth/
│   ├── dashboard/
│   ├── learning-path/
│   ├── lesson/
│   ├── onboarding/
│   ├── placement/
│   ├── practice/
│   ├── profile/
│   ├── progress/
│   ├── speaking/
│   └── vocabulary/
└── core/
```

### After

```
app/
├── design-system/
│   ├── admin/          — sp-admin-* components, services, tokens, utils
│   │   └── layouts/
│   │       └── admin-app-layout/
│   ├── student/        — student-ui components, notification-dropdown
│   │   └── layouts/
│   │       └── student-app-layout/
│   └── public/
│       └── layouts/
│           └── public-layout/
├── features/
│   ├── admin/          — admin feature pages (unchanged)
│   ├── student/        — activity, assessment, dashboard, learning-path,
│   │                     lesson, onboarding, placement, practice, profile,
│   │                     progress, speaking, vocabulary
│   └── public/         — landing, auth
└── core/               — services, models, guards, interceptors (unchanged)
```

---

## Why

The previous flat layout mixed design system infrastructure with product feature pages. The new structure separates concerns into three audience areas:

1. **admin** — internal operator tooling, TailAdmin-adapted wrappers
2. **student** — learner-facing UI and layouts
3. **public** — unauthenticated landing and auth flows

This makes import boundaries explicit and mirrors the three-audience product model.

---

## Files Affected

### Moved into `design-system/admin/`
- All `src/app/admin/**` (components, services, tokens, utils)
- `src/app/layouts/admin-app-layout/`

### Moved into `design-system/student/`
- All `src/app/shared/student-ui/**`
- `src/app/shared/notifications/notification-dropdown/`
- `src/app/layouts/student-app-layout/`

### Moved into `design-system/public/`
- `src/app/layouts/public-layout/`

### Moved into `features/student/`
- `src/app/features/activity/`
- `src/app/features/assessment/`
- `src/app/features/dashboard/`
- `src/app/features/learning-path/`
- `src/app/features/lesson/`
- `src/app/features/onboarding/`
- `src/app/features/placement/`
- `src/app/features/practice/`
- `src/app/features/profile/`
- `src/app/features/progress/`
- `src/app/features/speaking/`
- `src/app/features/vocabulary/`

### Moved into `features/public/`
- `src/app/features/landing/`
- `src/app/features/auth/`

### Unchanged
- `src/app/features/admin/` — admin feature pages kept in place
- `src/app/core/` — no changes

---

## Build Status

**PASSED.** `npm run build` clean after restructure.

---

## Test Status

**PASSED.** 2 spec files had broken import paths after the move (core import references). Both were fixed. All Angular unit tests and Playwright E2E tests pass.

---

## Documentation Updated

- `CLAUDE.md` — Frontend structure section rewritten to new paths
- `docs/architecture/admin-ui-design-system.md` — Import path, folder structure, and service path updated
- `docs/handoffs/current-product-state.md` — Three stale paths updated
- `docs/sprints/current-sprint.md` — Notification dropdown path updated

Historical review docs (point-in-time records) were intentionally left unchanged. They accurately describe what was true at time of writing.

---

## Risks

None. This is a pure structural refactor with no behaviour change. All component selectors, inputs, outputs, and service APIs are unchanged. Import paths internal to the app were updated as part of the move.

---

## Decisions Made

- Layout components co-located inside `design-system/` rather than a standalone `layouts/` folder, to keep each area self-contained.
- `core/` kept flat and unchanged — it is audience-agnostic infrastructure.
- Historical review docs not retroactively updated — they are point-in-time records and remain accurate for their snapshot date.

---

## Verdict

Complete. No regressions. Architecture now matches the three-area product model.

---

## Next Recommended Action

Run `graphify update .` to refresh the knowledge graph with new file paths.
