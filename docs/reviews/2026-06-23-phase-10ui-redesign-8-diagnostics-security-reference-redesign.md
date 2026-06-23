# Engineering Review — Phase 10UI-REDESIGN-8: Diagnostics and Security Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-8
**Commit:** 86040cc
**HEAD before work:** 3faa8dd

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-8 entry

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/diagnostics.jsx` — System status card (8-item grid), Recent events section with filter bar (Level/Category/CorrelationID/Search/Limit), dark log area with level-coloured rows. All data from `window.ADMIN_DATA` mock.
- No `security.jsx` reference file exists. Security page follows shared admin reference patterns from dashboard/integrations/ai-config (KPI strip, settings cards, badge-only secret status).
- `docs/design/admin-reference-alignment.md` — prior status and gaps for both routes.

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.spec.ts`

---

## Diagnostics page — changes

### Subtitle

Updated from `"System status, recent log events, and correlation ID lookup."` to `"System status, health indicators, recent events, and correlation ID lookup."`.

### KPI summary strip (new)

Four `sp-admin-kpi-card` tiles rendered before the System status card on every page load. Derived from real `status()` and `events()` signals via `kpiSummary` computed signal.

| Tile | Source | Notes |
|---|---|---|
| Database | `status().database.reachable` | `green` when reachable, `amber` when unreachable |
| AI provider | `status().ai.providerConfigured` + `activeProvider` | `indigo` when configured, `amber` when missing |
| Errors (loaded) | Count of `events()` where `level === 'Error'` | `amber` when > 0, `green` when 0 |
| Warnings (loaded) | Count of `events()` where `level === 'Warning'` | `amber` when > 0, `slate` when 0 |

Labels for Errors and Warnings are explicitly qualified as "(loaded)" — counts reflect the current page/filter result, not a global system total. No fake global metrics presented.

Strip only renders when `kpiSummary()` is non-null (i.e. status loaded successfully). No fake healthy state shown on error.

### (click) → (clicked) on sp-admin-button

All three `sp-admin-button` event bindings updated:
- `(click)="loadStatus()"` → `(clicked)="loadStatus()"`
- `(click)="toggleAutoRefresh()"` → `(clicked)="toggleAutoRefresh()"`
- `(click)="loadEvents()"` → `(clicked)="loadEvents()"`

### CSS token usage

`.sp-diag-count` border and text now use CSS token variables instead of hardcoded Tailwind literals.
`.sp-diag-meta-item` colour uses `var(--sp-admin-muted)`.

### SpAdminKpiCardComponent

Added to named imports, decorator imports array.

---

## Diagnostics — reference gap analysis

| Reference section | Status | Notes |
|---|---|---|
| System status 8-item grid | Existing — preserved | `sp-admin-status-grid` + `sp-admin-status-card` already implemented and correct |
| Recent events filter bar | Existing — preserved | Level/Category/CorrelationID/Search/Limit all present |
| Log area with level rows | Existing — preserved | `sp-admin-table` with row highlighting for Error/Warning |
| Auto-refresh toggle | Existing — preserved | Signal-driven, timer logic unchanged |
| Status KPI strip | **New — added** | 4-tile strip from real data |
| `sp-admin-button (clicked)` correctness | **Fixed** | All 3 buttons corrected |

---

## Security page — changes

### Page header order

The original template had `sp-admin-page-header` **inside** `sp-admin-page-body`. Fixed: `sp-admin-page-header` is now rendered before `sp-admin-page-body`, matching the shared admin layout pattern used by all other admin pages.

### Title and subtitle

- `title="Security Settings"` → `title="Security"`
- Subtitle updated to: `"Authentication, sessions, external login, audit events, and security posture."`

### Tab bar

Converted from raw Tailwind color literals (`border-indigo-600`, `text-indigo-600`, `text-gray-500`) to CSS token classes:
- `.sp-sec-tab-bar` — flex row with token border-bottom
- `.sp-sec-tab` — token-based tab button (`var(--sp-admin-muted)`)
- `.sp-sec-tab--active` — active uses `var(--sp-admin-primary)`

### KPI summary strip (new)

Four `sp-admin-kpi-card` tiles rendered before the tab bar on every page load. Derived from real `settings()` signal via `kpiSummary` computed signal.

| Tile | Source | Notes |
|---|---|---|
| Password min length | `settings().passwordPolicy.requiredLength` | `indigo` variant |
| Lockout threshold | `lockout.maxFailedAccessAttempts` + `lockoutDurationMinutes` | `amber` variant |
| Rate limit policies | `rateLimitPolicies.length` | `violet` variant |
| Google OAuth | `externalLogin.google.enabled` + both keys configured | `green` when configured, `slate` otherwise |

Strip only renders when `kpiSummary()` is non-null. No fake values shown.

### Settings card layout

All six settings cards converted from raw `div.grid.grid-cols-2` with `text-gray-500 dark:text-gray-400` Tailwind literals to CSS token layout:
- `.sp-sec-setting-grid` — 2/3 column responsive grid using CSS tokens
- `.sp-sec-field` — flex column field container
- `.sp-sec-field-label` — `var(--sp-admin-muted)` label
- `.sp-sec-field-value` — `var(--sp-admin-text)` value
- `.sp-sec-config-note` — muted note block using `var(--sp-admin-surface-alt)`

### JWT signing key note

Added note in JWT and session card: "JWT signing key is never displayed. Key presence is verified server-side only."

### Google client secret note

Added note in External login card: "Client secret is never displayed. To update Google provider settings, configure `Authentication:ExternalProviders:Google` in `appsettings.json`."

### Rate limiting table

Converted from `<sp-admin-table>` without `variant`/`density` to `variant="data" density="compact"`. Added empty state row when no policies configured.

### Auth events table

Added `variant="data" density="compact" minWidth="760px"` to the events table. Replaced raw Tailwind literal classes (`font-mono text-xs`, `text-gray-500`, `text-gray-400`) with inline CSS token styles.

### New imports

`SpAdminAlertComponent`, `SpAdminFormFieldComponent`, `SpAdminFormGridComponent`, `SpAdminKpiCardComponent` added to named imports and decorator imports array.

---

## Security — reference gap analysis

No `security.jsx` reference file exists. Gaps assessed against shared admin reference patterns.

| Section | Status | Notes |
|---|---|---|
| Page header correct position | **Fixed** — was inside page-body | |
| Title alignment | **Fixed** — was "Security Settings" | |
| Tab bar CSS tokens | **Fixed** — was Tailwind literals | |
| KPI summary strip | **New — added** | 4-tile strip from real settings data |
| Settings card layout | **Upgraded** — CSS token classes | Replaced Tailwind literal grid/text classes |
| JWT note | **Added** | |
| Google secret note | **Added** | |
| Deferred capabilities card | Preserved — no change needed | Already correct from 10UI-FIX-8 |
| Auth events table | Minor alignment improvement | |

---

## Real data / honest labels

| Section | Source | Notes |
|---|---|---|
| Diagnostics KPI — database | `DiagnosticsStatus.database.reachable` | Real |
| Diagnostics KPI — AI provider | `DiagnosticsStatus.ai.providerConfigured` | Real |
| Diagnostics KPI — errors | Derived from loaded events `level === 'Error'` | Explicitly labelled "(loaded)" |
| Diagnostics KPI — warnings | Derived from loaded events `level === 'Warning'` | Explicitly labelled "(loaded)" |
| Security KPI — password length | `AdminSecuritySettings.passwordPolicy.requiredLength` | Real |
| Security KPI — lockout | `lockout.maxFailedAccessAttempts` + `lockoutDurationMinutes` | Real |
| Security KPI — rate policies | `rateLimitPolicies.length` | Real |
| Security KPI — Google OAuth | Derived from `enabled` + both key flags | Real |

No fake data introduced. No global error/warning counts presented without qualification.

---

## Secret handling confirmed

- JWT signing key: never displayed. Note added in JWT card.
- Google client secret: never displayed. `clientSecretConfigured` badge (Configured/Not set) only. Note added.
- Google client ID: never displayed. `clientIdConfigured` badge only.
- No API keys, SMTP passwords, refresh tokens, reset tokens, or raw auth headers displayed anywhere.
- Test coverage: `does not display JWT signing key value`, `does not display Google client secret value`, `Google client secret shown as Configured/Not set badge only`, `JWT note says key is never displayed`.

---

## Behaviours preserved

**Diagnostics:**
- System status grid (8 cards via `sp-admin-status-grid`) — unchanged
- Events filter bar (Level/Category/CorrelationID/Search/Limit) — unchanged
- Auto-refresh toggle with 5s interval — unchanged
- Event level row highlighting (error/warn background) — unchanged
- Pagination — unchanged
- Error/empty/loading states — unchanged
- `levelTone`, `formatDateTime`, `uptimeLabel` helpers — unchanged

**Security:**
- Overview/Auth Events tab switching — unchanged
- All six settings cards with all fields — unchanged
- Auth events table with filter bar (event type/outcome/email) — unchanged
- Auth events pagination — unchanged
- Deferred security capabilities card (7 items) — unchanged
- All tone helpers (`boolTone`, `configuredTone`, `outcomeTone`) — unchanged
- `formatEventType`, `timeAgo` helpers — unchanged

---

## Tests added

### Diagnostics (10 new)

- `renders kpi summary strip after status loads`
- `kpi strip renders 4 kpi cards`
- `kpiSummary shows database reachable label`
- `kpiSummary shows database unreachable label when not reachable`
- `kpiSummary returns null when status not loaded`
- `kpiSummary counts errors from loaded events`
- `kpiSummary counts warnings from loaded events`
- `kpi strip does not show fake healthy state when status errors`
- `subtitle contains system status and events`
- (one existing test for page header already covered)

### Security (20 new)

- `renders sp-admin-page-header with title Security`
- `page header subtitle mentions authentication`
- `renders kpi summary strip when settings loaded`
- `kpi strip renders 4 sp-admin-kpi-card tiles`
- `kpiSummary passwordMinLength is 12`
- `kpiSummary lockoutAttempts is 5`
- `kpiSummary ratePolicies is 1`
- `kpiSummary googleEnabled is false for mockSettings`
- `kpiSummary returns null when settings not loaded`
- `renders password policy card`
- `renders lockout policy card`
- `renders JWT and session card`
- `renders rate limiting card with policy row`
- `renders security headers card`
- `renders external login Google card`
- `does not display JWT signing key value`
- `does not display Google client secret value`
- `Google client secret shown as Configured/Not set badge only`
- `JWT note says key is never displayed`
- `tab bar renders Overview and Auth Events tabs`
- `overview tab is active by default`

---

## Gates passed

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| Production build | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1251/1251 PASS |
| Backend build | Not run — no backend source changes |
| Playwright | Not run — no existing E2E specs for these pages |

---

## Decisions made

1. Diagnostics error/warning KPI tiles are labelled "(loaded)" to be honest that counts reflect the current filter/page result, not a global system aggregate. A global aggregate endpoint does not exist.
2. Security KPI strip uses `settings()` signal data only — no additional API call needed. All four tiles derive directly from the existing `AdminSecuritySettings` response.
3. `@else if (settings(); as s)` is invalid Angular syntax — resolved by using `settings()!` non-null assertion throughout. The block is guarded by `@else if (settings())` which guarantees non-null.
4. Security page header was previously inside `sp-admin-page-body`. This was corrected to match the shared admin layout contract — `sp-admin-page-header` is always outside `sp-admin-page-body`.
5. Tab bar uses native `(click)` not `(clicked)` — tab buttons are native `<button>` elements, not `sp-admin-button` components. This is correct.

---

## Deferred gaps

- Global diagnostic error/warning aggregate: no backend endpoint. Counts are page-scoped and labelled.
- System health cards beyond database + AI (storage health, notification outbox, background jobs, readiness generation): these cross into Integrations/Notifications page territory and are not surfaced here. The existing `sp-admin-status-grid` already shows database + AI.
- No reference `security.jsx` file exists. Security page follows shared admin design system patterns throughout.

---

## Final verdict

**Complete.** 1251/1251. Build clean. All security constraints satisfied. No fake data. No backend changes. No auth/security behaviour changes. No student-facing UI changes. All existing behaviour preserved.

---

## Next recommended action

**10UI-REDESIGN-FINAL** — Admin UI reference alignment closure audit.

All eight redesign phases are now complete. The final phase performs a route-by-route audit to confirm all pages match the reference and no regressions were introduced.

See: `docs/design/admin-reference-alignment.md` — redesign phase sequence.
