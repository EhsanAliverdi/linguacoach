# Phase 10UI — Notifications Visual Parity Cleanup

**Date:** 2026-06-26
**Sprint/Feature:** Phase 10UI-NOTIFICATIONS-ONLY-VISUAL-PARITY-CLEANUP
**Review type:** Implementation + build verification

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html`
- `src/LinguaCoach.Web/src/app/design-system/admin/tokens/admin-tokens.css`

**Integrations was not touched.**

---

## Raw utility classes removed

The following raw Tailwind/local classes were removed from the template:

| Class | Replaced with |
|-------|--------------|
| `flex flex-col gap-4 p-1` (slide-over body wrappers) | `sp-admin-form-stack` |
| `flex flex-col gap-5 p-1` (send slide-over body) | `sp-admin-form-stack` |
| `grid grid-cols-2 gap-3` (category/severity rows) | `sp-admin-form-row` |
| `flex items-center gap-3 flex-wrap` (in-app toggle row) | `sp-admin-settings-list` + `sp-admin-settings-row` |
| `flex items-center gap-2 flex-wrap` (config source row) | `sp-admin-flex-row` |
| `flex gap-2` (recipient lookup row) | `sp-admin-flex-row` |
| `flex flex-col gap-2` (channel checkboxes) | `sp-admin-checkbox-stack` |
| `mt-3` (integrations link, channel hint) | `sp-admin-mt-3` |
| `text-xs text-sp-admin-muted mt-2` (inApp note) | `sp-admin-section-sub sp-admin-mt-3` |
| `text-xs text-green-600` (recipient resolved) | `sp-admin-status-label sp-admin-status-label--active` |
| `mt-1` (resolved email hint) | removed (label is inline) |
| `font-mono text-xs` (template key cell) | `sp-admin-cell-code` |
| `border-t border-sp-admin-border pt-4 flex flex-col gap-3` (preview section) | `hr.sp-admin-divider` + `sp-admin-form-stack` |
| `text-sm font-semibold text-sp-admin-text` (preview heading) | `sp-admin-section-title` |
| `text-sm` (preview subject/title) | `sp-admin-result-row` inside `sp-admin-result-panel` |
| `text-xs whitespace-pre-wrap break-words` (preview pre) | `sp-admin-code-block` |
| `mt-2` (integrations link button wrapper) | `sp-admin-form-footer` |

---

## Local classes removed

| Old class | Replacement |
|-----------|-------------|
| `sp-adm-code-preview` (template edit mode key/channel display) | `sp-admin-readonly-field sp-admin-readonly-field--mono` |

---

## Inline richActions moved to TS

| Location | Method |
|----------|--------|
| Outbox table row | `outboxRowActions(item)` + `onOutboxAction($event, item)` |
| Templates table row | `templateRowActions(t)` + `onTemplateAction($event, t)` |

---

## Notifications tab changes

- Filter bar + table structure unchanged (was already clean).
- Pagination moved inside `div.sp-admin-table-footer`.

---

## Delivery Queue tab changes

- Inline richActions in `<td>` replaced by `outboxRowActions(item)` / `onOutboxAction()`.
- Pagination moved inside `div.sp-admin-table-footer`.

---

## Configuration tab changes

- **Removed** the duplicate KPI strip that was repeating the top-of-page channel cards.
- Config source row changed from `<sp-admin-card>` wrapper with raw `flex` to `div.sp-admin-flex-row` inline row.
- In-App channel toggle now uses `sp-admin-settings-list` / `sp-admin-settings-row` / `sp-admin-settings-row-label`.
- Save button moved into `sp-admin-form-footer`.
- Inline note uses `sp-admin-section-sub sp-admin-mt-3` instead of raw `text-xs text-sp-admin-muted mt-2`.
- **Added** Delivery controls card (3 toggle rows: send to inactive students, send to archived students, suppress duplicates 24h) matching `notifications.jsx` reference. State managed in `deliveryForm` object; saved via `saveDeliveryControls()` (optimistic, no API endpoint yet).
- Email/SMS callout link button moved into `sp-admin-form-footer`.
- All cards wrapped in `div.sp-admin-section-stack`.

---

## Templates tab changes

- Template key cell: `font-mono text-xs` → `sp-admin-cell-code`.
- Inline richActions replaced by `templateRowActions(t)` / `onTemplateAction()`.
- Pagination moved inside `div.sp-admin-table-footer`.
- Templates tab label shows live count badge using new `sp-admin-tab-count` token.

---

## Slide-over cleanup

### Template create/edit slide-over

- Body wrapper: `flex flex-col gap-4 p-1` → `sp-admin-form-stack`.
- `sp-adm-code-preview` (edit mode key display) → `sp-admin-readonly-field sp-admin-readonly-field--mono`.
- `grid grid-cols-2 gap-3` (category/severity) → `sp-admin-form-row`.
- Preview section divider: raw `border-t` → `hr.sp-admin-divider`.
- Preview heading: raw `text-sm font-semibold` → `sp-admin-section-title`.
- Preview result: `<p class="text-sm">` → `sp-admin-result-panel` + `sp-admin-result-row`.
- Preview body `<pre>`: `text-xs whitespace-pre-wrap break-words` → `sp-admin-code-block`.

### Send notification slide-over

- Body wrapper: `flex flex-col gap-5 p-1` → `sp-admin-form-stack`.
- Recipient row: `flex gap-2` → `sp-admin-flex-row`.
- Resolved email line: `text-xs text-green-600` → `sp-admin-status-label sp-admin-status-label--active`.
- Channel checkboxes: `flex flex-col gap-2` → `sp-admin-checkbox-stack`.
- Channel hint: `text-xs text-sp-admin-muted mt-1` → `sp-admin-section-sub sp-admin-mt-3`.
- Category/severity grid: `grid grid-cols-2 gap-3` → `sp-admin-form-row`.

---

## Shared tokens/components added

| Token | File | Purpose |
|-------|------|---------|
| `.sp-admin-tab-count` | `admin-tokens.css` | Small count pill inside tab label (e.g. "Templates 20") |

All other tokens used (`sp-admin-form-stack`, `sp-admin-form-row`, `sp-admin-settings-list`, `sp-admin-settings-row`, `sp-admin-settings-row-label`, `sp-admin-section-sub`, `sp-admin-mt-3`, `sp-admin-flex-row`, `sp-admin-checkbox-stack`, `sp-admin-readonly-field`, `sp-admin-readonly-field--mono`, `sp-admin-cell-code`, `sp-admin-result-panel`, `sp-admin-result-row`, `sp-admin-code-block`, `sp-admin-divider`, `sp-admin-table-footer`, `sp-admin-form-footer`, `sp-admin-section-stack`, `sp-admin-section-title`, `sp-admin-status-label`, `sp-admin-status-label--active`) were pre-existing in `admin-tokens.css`.

---

## Conformance checklist

| Rule | Status |
|------|--------|
| No page CSS file | ✅ |
| No inline `style=` | ✅ |
| No inline SVG | ✅ |
| No `sp-admin-modal` | ✅ |
| No slide-over `size=` | ✅ |
| No `sp-adm-*` or `sp-not-*` classes | ✅ |
| All slide-over footers use `sp-admin-button-group` | ✅ |
| All primary content inside `sp-admin-page-body` | ✅ |
| Integrations files not touched | ✅ |
| No backend changes | ✅ |
| No fake data | ✅ |

---

## Build result

Production build passed with zero errors. Pre-existing unrelated warnings only (AdminAiUsageComponent, AdminDiagnosticsComponent, etc.).

---

## Remaining visual gaps

- `saveDeliveryControls()` is optimistic only — no backend API endpoint exists for delivery control flags. Toggles persist in component state per session only.
- `sp-admin-tab-count` on Templates tab shows live `templatesTotal()` count. On first page load before the templates tab is activated, count shows 0 until templates are loaded.

---

## Documentation impact

- Docs reviewed: none required (UI-only change).
- Docs updated: this review document.
- Docs intentionally not updated: architecture docs, API contracts.
- Reason: frontend-only refactor. No product behaviour, API, or data model change.

---

## Final verdict

PASS. All raw Tailwind/local classes removed from Notifications template. Inline richActions moved to TS methods. Configuration tab cleaned and delivery controls added per reference. Slide-overs use shared form tokens throughout. Build is clean.

## Next recommended action

Commit when ready. No backend changes required.
