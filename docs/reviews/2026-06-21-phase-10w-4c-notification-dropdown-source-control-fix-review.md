# Phase 10W-4C — Notification Dropdown Source Control Fix: Implementation Review

**Date:** 2026-06-21
**Sprint:** 10W-4C
**Author:** Engineering (Claude Code)

---

## Problem

Phase 10W-3 implemented the live notification bell/dropdown by rewriting the `NotificationDropdownComponent` inside the gitignored vendor/template directory:

```
src/LinguaCoach.Web/src/app/templates/   ← gitignored
```

This meant a fresh clone would not include the live notification implementation — the student app header would revert to a static bell with no dropdown and no live data.

---

## Solution

Created an app-owned committed `NotificationDropdownComponent` at:

```
src/LinguaCoach.Web/src/app/shared/notifications/notification-dropdown/
  notification-dropdown.component.ts
  notification-dropdown.component.html
  notification-dropdown.component.spec.ts
```

Selector: `sp-notification-dropdown`

The component:
- Uses `@HostListener('document:mousedown')` for click-outside detection — no dependency on the gitignored `DropdownComponent`.
- Injects `NotificationService` and `Router` from committed core services.
- Implements all 10W-3 behaviors: live list, unread count, mark read, mark all read, archive/dismiss, loading state, empty state, error/retry state, deep-link navigation.
- Contains no hard-coded demo data.

---

## Files changed

### New files

- `src/LinguaCoach.Web/src/app/shared/notifications/notification-dropdown/notification-dropdown.component.ts`
- `src/LinguaCoach.Web/src/app/shared/notifications/notification-dropdown/notification-dropdown.component.html`
- `src/LinguaCoach.Web/src/app/shared/notifications/notification-dropdown/notification-dropdown.component.spec.ts`

### Modified files

- `src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.ts` — import + add to `imports[]`
- `src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.html` — replaced static bell `<button>` with `<sp-notification-dropdown>`

---

## Gitignored templates

The gitignored `src/app/templates/` directory still contains the prior customized `NotificationDropdownComponent`. It is not imported or referenced anywhere in committed code. Committed source no longer depends on anything under `src/app/templates/`.

---

## Findings

| Item | Result |
|---|---|
| Gitignored template dependency removed | Yes |
| Shell/header uses committed component | Yes — `StudentAppLayoutComponent` imports `NotificationDropdownComponent` |
| Live API list | Yes |
| Unread count | Yes |
| Mark read | Yes |
| Mark all read | Yes |
| Archive/dismiss | Yes |
| Loading state | Yes |
| Empty state | Yes |
| Error/retry state | Yes |
| Deep-link navigation | Yes |
| Hard-coded demo data removed from committed path | Yes — never existed in committed path; component reads only from `NotificationService` |
| No dependency on gitignored `DropdownComponent` | Yes — click-outside uses `@HostListener` on host element ref |

---

## Tests

17 new tests in `notification-dropdown.component.spec.ts` covering:

- Unread count loaded on init
- Bell button renders
- Panel hidden before open
- Opens and loads notifications on toggle
- Loading state rendered during fetch
- Error state + retry button on failure
- Empty state rendered
- Notification list items rendered
- Mark read on click + unread count decrement
- Already-read notification does not call markRead
- Mark all read + unread count zeroed
- Archive removes item + decrements unread if was unread
- Retry calls loadNotifications
- closeDropdown sets isOpen false
- severityIcon returns correct emoji per severity
- timeAgo returns seconds-ago string
- No hard-coded demo data assertion

---

## Gates

| Gate | Result |
|---|---|
| `npm run build -- --configuration production` | Pass |
| `npm test -- --watch=false --browsers=ChromeHeadless` | Pass 942 / 942 (up from 925) |
| Backend gates | Not run — no backend changes |

---

## Decisions made

1. **`src/app/shared/notifications/`** chosen as location — consistent with existing `src/app/shared/student-ui/` pattern for shared components not tied to a feature.
2. **`@HostListener` for click-outside** — avoids any dependency on the gitignored `DropdownComponent`. The behavior is equivalent.
3. **Static bell replaced in `StudentAppLayoutComponent`** — the admin layout did not have a notification bell; no changes needed there.
4. **Gitignored templates left unchanged** — per spec; no vendored files modified.

---

## Risks and unresolved questions

- **Admin layout bell** — AdminAppLayout has no notification bell. Not in scope for this phase; deferred.
- **Polling** — No polling implemented. Unread count is loaded once on init. Live refresh deferred to a future phase.

---

## Final verdict

Gitignored template dependency removed. Committed component fully replicates 10W-3 behavior. All gates green.

## Next recommended action

Proceed to TODO-10W-5 (notification templates/preferences) or TODO-10W-6 (SMS) per backlog priority.
