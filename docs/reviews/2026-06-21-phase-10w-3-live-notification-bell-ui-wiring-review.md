# Phase 10W-3 — Live Notification Bell UI Wiring — Engineering Review

**Date:** 2026-06-21
**Sprint:** Phase 10W-3
**Status:** Complete

---

## Files reviewed / modified

- `src/LinguaCoach.Web/src/app/core/services/notification.service.ts` — new Angular service
- `src/LinguaCoach.Web/src/app/core/services/notification.service.spec.ts` — 6 service tests
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../notification-dropdown/notification-dropdown.component.ts` — rewritten with signals
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../notification-dropdown/notification-dropdown.component.html` — rewritten, demo data removed
- `src/LinguaCoach.Web/src/app/templates/tailadmin/.../notification-dropdown/notification-dropdown.component.spec.ts` — 16 component tests

---

## Delivered

### NotificationService (`core/services/notification.service.ts`)

Angular `@Injectable({ providedIn: 'root' })` service wrapping all 5 backend endpoints:

- `list(page, pageSize, unreadOnly)` — `GET /api/notifications` with HttpParams
- `getUnreadCount()` — `GET /api/notifications/unread-count`
- `markRead(id)` — `POST /api/notifications/:id/read`
- `markAllRead()` — `POST /api/notifications/read-all`
- `archive(id)` — `POST /api/notifications/:id/archive`

Exports: `NotificationItem`, `NotificationListResponse`, `UnreadCountResponse` interfaces.

No `environment.apiUrl` prefix — uses relative paths, consistent with all other services in the codebase.

### NotificationDropdownComponent rewrite

Replaced 340-line hard-coded demo template (Terry Franci, Nganter App etc.) with signals-based live component.

**State:**

```typescript
readonly notifications = signal<NotificationItem[]>([]);
readonly unreadCount  = signal<number>(0);
readonly loading      = signal(false);
readonly error        = signal<string | null>(null);
readonly hasUnread    = computed(() => this.unreadCount() > 0);
```

**Lifecycle:** `ngOnInit` calls `loadUnreadCount()` (silent on error — bell still renders). `toggleDropdown()` triggers `loadNotifications()` on open.

**Template states:**

- `@if (loading())` — spinner with `role="status"`
- `@else if (error())` — error message + Retry button
- `@else if (notifications().length === 0)` — empty state ("No notifications")
- `@else` — `@for (item of notifications(); track item.id)` live list

**Interactions:**

- Click notification → `markRead(id)` if unread, navigate if `deepLinkUrl` present
- Mark all read → `markAllRead()`, resets count and updates all items locally
- Archive button → `archive(id)`, removes from list, decrements count if was unread

**Unread badge:** pulsing orange dot on bell when `hasUnread()`.

**Unread count chip:** displayed in dropdown header when `unreadCount() > 0`.

---

## Bugs fixed during implementation

### 1. Angular template parse error — `[class.dark:bg-blue-900\/10]`

Angular's template parser rejected the colon in `dark:bg-blue-900` as an invalid property binding name. The parser treated the `<li>` tag as unterminated, causing JIT compilation failure for all 13 component tests.

**Fix:** replaced the two `[class.*]` bindings with a single interpolated `[class]` binding:

```html
[class]="'...base classes...' + (!item.readAtUtc ? ' bg-blue-50 dark:bg-blue-900/10' : '')"
```

### 2. ChunkLoadError — dynamic `import('rxjs')` in spec

One test used `const { Subject } = await import('rxjs')` inside `fakeAsync` which triggered a webpack chunk load timeout in Karma.

**Fix:** moved `Subject` to the static top-level import.

---

## Gates

- `git diff --check`: PASS
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (916/916)

---

## Security / constraints respected

- No cross-user notification exposure (all requests use current user's JWT — enforced by backend)
- No sensitive content added to metadata
- No external email/SMS sending
- No changes to auth or reset-password flows
- No changes to usage governance

---

## Constraints NOT implemented (deferred)

- Polling (30 s interval) — deferred to 10W-4 or a later phase
- Admin-specific bell endpoint — deferred
- Notification preferences / opt-out — deferred to 10W-5

---

## Next recommended action

Phase 10W-4: email provider + reset-password wiring + Quartz dispatch job.

See `TODOS.md` → TODO-10W-4.
