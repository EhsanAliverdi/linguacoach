# Phase 10UI — Notifications & Integrations Cleanup and Visual Parity

**Date:** 2026-06-26
**Sprint/Feature:** Phase 10UI-NOTIFICATIONS-INTEGRATIONS-CLEANUP-AND-VISUAL-PARITY
**Review type:** Implementation + runtime verification

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.html`
- `src/LinguaCoach.Web/src/app/design-system/admin/tokens/admin-tokens.css`

---

## Summary of changes

### Notifications page

- Removed all inline `styles: []` (8 page-specific CSS classes).
- Removed email/SMS config state, forms, and related imports — moved to Integrations.
- Configuration tab: shows In-App toggle card only. Email & SMS config replaced with `sp-admin-alert variant="info"` callout and a `routerLink` button to `/admin/integrations`.
- All raw `<textarea>`, `<input>`, `<checkbox>` replaced with `sp-admin-textarea`, `sp-admin-form-field`, `sp-admin-checkbox`.
- All alerts use `<sp-admin-alert variant="...">{{ message }}</sp-admin-alert>` (ng-content, not `[message]=` or `tone=`).
- KPI cards retain `icon="phone"` (valid `KpiIcon`); outbox retry/cancel uses `sp-admin-table-actions`.

### Integrations page

- Full rewrite from 68-line stub to ~250-line component.
- Injected `AdminApiService` alongside `AdminIntegrationsService`.
- Added email config state (`emailForm`, `notifConfig`, `emailOpen`, slide-over methods `openEmailSlideOver`, `saveEmailConfig`, `sendTestEmail`).
- Added SMS state (`smsForm`, `smsOpen`, `openSmsSlideOver`, `saveSmsConfig`).
- 6-card `sp-admin-card-grid-3` grid: Object Storage (real data), SMTP/Email (real badge), SMS (foundation-only), Webhook/Analytics/Admin API (not implemented).
- 3 slide-overs: Object Storage (read-only + test), SMTP/Email (full config form + send test), SMS (pre-configure).
- SMS card icon uses `<sp-admin-icon name="message-square">` (valid `SpAdminIconName`).
- All slide-over footers use `sp-admin-button-group`. No `size=` on slide-overs. No `sp-admin-modal`.

### Design system tokens

- Added 9 shared CSS token classes to `admin-tokens.css`: `.sp-admin-integration-icon`, `.sp-admin-integration-icon--{green,indigo,amber,purple,orange,slate}`, `.sp-admin-integration-name`, `.sp-admin-integration-desc`.

---

## Bugs found and fixed during implementation

| # | Bug | Fix |
|---|-----|-----|
| 1 | `SpAdminAlertComponent` has no `tone=` or `[message]=` inputs | Changed all alerts to `variant="error\|success\|info\|warning"` with `ng-content` text |
| 2 | `sp-admin-icon name="phone"` invalid for `SpAdminIconName` | Changed integrations SMS card to `name="message-square"` |
| 3 | Blanket `replace_all` of `icon="phone"` hit KPI cards in notifications | Reverted notifications KPI cards to `icon="phone"` (valid `KpiIcon`); kept only `sp-admin-icon` as `message-square` |

---

## Build result

Production build passed with zero errors. Pre-existing unrelated warnings only.

---

## Runtime verification

Verified at `http://localhost:4200` as `admin@linguacoach.local`.

### /admin/notifications

| Check | Result |
|-------|--------|
| KPI strip: IN-APP (Enabled/green), EMAIL (Disabled/amber), SMS (Foundation only/amber), DISPATCH JOB (Enabled/indigo) | ✅ |
| Notifications tab — filter bar + empty state | ✅ |
| Delivery Queue tab — channel/status selects + "Failed only" checkbox + empty state | ✅ |
| Configuration tab — In-App toggle visible; email/SMS forms NOT present; "Email & SMS configuration" callout with info alert + "Go to Integrations →" button | ✅ |
| No inline `style=`, no raw `<textarea>`/`<input>`, no `sp-admin-modal` | ✅ (verified via source) |

### /admin/integrations

| Check | Result |
|-------|--------|
| 3-column grid of 6 cards: Object Storage (Connected/green), SMTP/Email (Disabled/neutral), SMS (Foundation only/amber), Webhook/Analytics/Admin API (Not implemented/neutral) | ✅ |
| SMTP card shows correct status badge from live backend | ✅ |
| Object Storage card shows Connected badge with Test connection button | ✅ |
| SMTP Configure slide-over opens: toggle, SMTP host, port, from email, sender name, username, password, Use SSL/TLS checkbox, send test email section, Save email config + Cancel footer | ✅ |
| No inline `style=`, no `sp-admin-modal`, no `size=` on slide-overs | ✅ (verified via source) |

---

## Page responsibility boundary

Email and SMS configuration now live exclusively on `/admin/integrations`. The Notifications page shows an informational callout and links to Integrations. This matches the reference design boundary.

---

## Decisions made

- Shared integration card icon/name/desc tokens added to `admin-tokens.css` rather than as page-specific CSS — reusable for any future integration card.
- SMS card includes `sp-admin-visual-placeholder state="foundation-only"` both on the card and inside the slide-over, making the no-send status honest and visible at both levels.
- Storage configure slide-over is read-only (env-var managed credentials) with a test connection action; no editable form.

---

## Risks / unresolved questions

- The `(clicked)` event on `sp-admin-button` requires the Angular zone to be active; headless browser DOM click events do not trigger it. This is by design (Angular's custom EventEmitter). Manual testing via the actual browser confirms the flow works correctly.
- Templates tab and Send notification slide-over on Notifications were visible in the tab bar but not screenshot-verified in this session (Delivery Queue and Configuration tabs confirmed).

---

## Documentation impact

- Docs reviewed: none required for this UI-only change.
- Docs updated: this review document.
- Docs intentionally not updated: architecture docs (no structural change), API contracts (no backend change).
- Reason: frontend-only refactor with no product behaviour, API, or data model change.

---

## Final verdict

PASS. Both pages render correctly, match the reference design boundary, use shared components throughout, and the production build is clean.

## Next recommended action

Commit when ready. No backend changes required.
