# Engineering Review — Phase 10UI-REDESIGN-7: Notifications and Integrations Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-7
**Commit:** 93d6e50

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-7 entry

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/notifications.jsx` — Email + Webhook channel cards with toggle, notification triggers table, recently sent table. All data from `window.ADMIN_DATA` (mock).
- `docs/design/speakpath/admin/pages/integrations.jsx` — 4 integration cards (SMTP/Webhook/Slack/Analytics), Admin API card showing masked key + Base URL. All mock data.

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts`

---

## Notifications page — changes

### Header

- `title="Notification Center"` → `title="Notifications"`
- Subtitle expanded to describe all four tabs (notifications, outbox, config, templates).

### KPI channel summary strip (new)

Four `sp-admin-kpi-card` tiles rendered before the tab bar on every page load. Data from `channelSummary` computed signal derived from `config()`.

| Tile | Source | Notes |
|---|---|---|
| In-App | `cfg.inApp.statusLabel` | Tone from `configTone()` |
| Email | `cfg.email.statusLabel` | Tone from `configTone()` |
| SMS | Always "Foundation only" | Always `variant="amber"`, never implies production-ready |
| Dispatch job | `cfg.dispatchJob.enabled` → Enabled/Disabled | Tone from `configTone()` |

### Config loaded on ngOnInit

Previously `loadConfig()` was only called on config tab activate (`onConfigTabActivated()`). Updated `ngOnInit()` to call `this.loadConfig()` eagerly so the KPI strip renders on every page load. The guard `if (!this.config())` in `onConfigTabActivated()` prevents double-loading.

### Tab bar

`div.flex.gap-2.mb-4.border-b.border-gray-200` → `div.sp-notif-tab-bar`
Each `<button class="px-4...border-indigo-600...">` → `<button class="sp-notif-tab" [class.sp-notif-tab--active]="...">`

All classes use CSS token variables (`--sp-admin-primary`, `--sp-admin-border`).

### Config channel status section

The raw `div.grid.grid-cols-1.sm:grid-cols-2.lg:grid-cols-4` with nested `sp-admin-card > div.p-4` per channel was replaced with `div.sp-notif-kpi-strip` containing 4 `sp-admin-kpi-card` tiles. SMS tile always shows "Foundation only" regardless of backend config label.

### Config channel cards

All three channel config cards (`sp-admin-card > div.p-5` pattern) replaced with `sp-admin-card title="..."` using named title attributes and token-based layout classes (`.sp-notif-field-row`, `.sp-notif-checkbox-row`, `.sp-notif-save-row`).

### Outbox and template action buttons

Raw `<button class="text-xs px-2 py-1 rounded border...">` → `<sp-admin-button size="sm" variant="ghost" (clicked)="...">` throughout.

### Test email card

`<h3>` heading inside raw div replaced with `sp-admin-card title="Send test email"`. Result message uses CSS variable colour bindings instead of Tailwind literal classes.

---

## Integrations page — changes

### Header

Subtitle updated: `"Object storage health and background lesson generation."` → `"Connected services, storage, background jobs, and API/webhook readiness."`

### Integration card grid (new)

Six cards in a responsive 1/2/3 column grid rendered above all existing functional cards.

| Card | Status | Data source |
|---|---|---|
| Object storage | Real — connected/credentials missing badge derived from `storage()` | Real, `getStorage()` |
| SMTP / Email | Link card → `/admin/notifications` | No backend call |
| Webhook | Not implemented — "Backend not available yet" | None |
| Slack | Not implemented — "Backend not available yet" | None |
| Analytics | Not implemented — "Backend not available yet" | None |
| Admin API | Not implemented — "API keys never displayed" | None |

No fake data. No secrets shown. Not-implemented cards are explicit.

### Background job metrics

`sp-admin-stat-card` (4 tiles) → `sp-admin-kpi-card` (4 tiles: Queued, Running, Failed, Last success). Values are content-projected text (no `[value]` binding). `sp-int-kpi-strip` 2/4 column responsive grid.

### (click) → (clicked)

All `(click)` event bindings on `sp-admin-button` elements updated to `(clicked)` throughout the template (test connection, save settings, generate lessons, retry, cancel).

### RouterLink added

`RouterLink` imported and registered in the decorator to support the SMTP link card `routerLink="/admin/notifications"`.

---

## Spec fixes

### Notifications spec

- `getNotificationConfig` spy now returns `of(mockConfig)` in `beforeEach` (required because `ngOnInit` now calls `loadConfig()` eagerly).
- `config signal starts null` renamed to `config signal is populated after ngOnInit` with inverted assertion.
- `loadConfig sets configError on failure` — `component.config.set(null)` added before setting error spy so the test starts from a clean state.
- `onConfigTabActivated calls loadConfig only when config is null` — resets `apiSpy.getNotificationConfig.calls` and `component.config` to null before testing tab-activate deduplication.

### Integrations spec

- `provideRouter([])` added to all three `TestBed.configureTestingModule` setups (required by `RouterLink`).
- `provideRouter` import added.

### Wrapper migration spec

- `provideRouter([])` added to all four integrations `TestBed.configureTestingModule` setups.
- "Integrations save button calls saveSettings" test updated: `saveBtn?.click()` (native DOM click, does not fire `(clicked)`) replaced with `fixture.componentInstance.saveSettings()`.

---

## Real data / honest labels

| Section | Source | Notes |
|---|---|---|
| KPI strip — In-App | `cfg.inApp.statusLabel` | Real |
| KPI strip — Email | `cfg.email.statusLabel` | Real |
| KPI strip — SMS | Always "Foundation only" | Hardcoded — never implies production SMS |
| KPI strip — Dispatch | Derived from `cfg.dispatchJob.enabled` | Real |
| Integration card — Object storage badge | `storage().accessKey && storage().secretKey` | Real |
| Integration card — SMTP | No data — link to Notifications page | No fake values |
| Webhook / Slack / Analytics / Admin API | None | "Backend not available yet" |
| Background jobs KPIs | `b.summary.queued/running/failed/lastSuccessfulGenerationUtc` | Real |

No fake data introduced anywhere.

---

## Security constraints satisfied

- SMS never labelled as production-ready. Always "Foundation only — provider not connected" or equivalent amber tile.
- SMTP password not displayed. `hasPassword` badge only.
- No API keys, secrets, or bearer tokens rendered anywhere.
- Admin API card explicitly states keys are never displayed and no endpoint is implemented.
- Webhook/Slack/Analytics integrations explicitly labelled not implemented.

---

## Behaviours preserved

- All four tabs (notifications, outbox, config, templates) — unchanged
- Outbox retry/cancel actions — unchanged (button variant updated, logic unchanged)
- Config save (in-app, email, SMS) — unchanged
- Test email send — unchanged
- Template CRUD (create, edit, preview, deactivate) — unchanged
- Pagination and filter on all tabs — unchanged
- Object storage display and test connection — unchanged
- Generation settings form (all 11 fields) — unchanged
- Background generation toggle and TTS toggle — unchanged
- Generate lessons manually — unchanged
- Retry/cancel batch actions — unchanged
- Ready buffer per student table — unchanged
- Readiness pool aggregate placeholder card — unchanged

---

## Tests

**Total: 1221/1221 PASS.**
Zero new test count — only spec fixes (stale assertions updated to reflect eager init, RouterLink injection requirement resolved).

---

## Gates passed

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| Production build | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1221/1221 PASS |
| Backend build | Not run (no backend changes) |
| Playwright | Not run (no E2E specs for these pages) |

---

## Decisions made

1. KPI strip is rendered on every page load (not just config tab). Config is fetched in `ngOnInit`. The `onConfigTabActivated()` guard prevents double-fetching when the tab is clicked after init.
2. SMS KPI tile always shows `variant="amber"` and "Foundation only" regardless of the backend `statusLabel` value. This prevents any future backend change accidentally implying SMS is production-ready.
3. SMTP link card in the integration grid navigates to `/admin/notifications` rather than duplicating config UI. Single source of truth.
4. Webhook/Slack/Analytics/Admin API cards render with "Backend not available yet" text. No fake connection status, no fake API keys.
5. Integration card grid uses raw `div.sp-int-card` layout rather than `sp-admin-card` tiles to match the reference design's card-within-section structure. The functional cards below (storage, settings, jobs) continue to use `sp-admin-card`.
6. `sp-admin-button (clicked)` is the correct event binding. All `(click)` bindings on `sp-admin-button` were incorrect and have been corrected throughout both pages.

---

## Deferred gaps

- Webhook backend endpoint: out of scope.
- Slack integration endpoint: out of scope.
- Analytics provider connection: out of scope.
- Admin API key management endpoint: out of scope.
- SMS provider and phone verification flow: out of scope. Foundation only.

---

## Final verdict

**Complete.** 1221/1221. Build clean. All security constraints satisfied. No fake data. No backend changes. All existing behaviour preserved.

---

## Next recommended action

**10UI-REDESIGN-8** — Diagnostics and Security pages.

See: `docs/design/admin-reference-alignment.md` — `/admin/diagnostics` and `/admin/security` rows.
