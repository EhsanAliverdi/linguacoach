# Phase 10UI-FIX-3 — Admin Core Component Visual Alignment

**Date:** 2026-06-23
**Sprint:** Current (10UI series)
**Related phase:** 10UI-FIX-2 (shell/sidebar/header), 10UI-FIX-1 (nav)

---

## Scope

Replace all hardcoded TailAdmin-blue CSS color literals in `sp-admin-*` reusable components with SpeakPath indigo CSS custom property tokens. No page redesigns. No backend changes. No student UI changes. No migrations. No new features.

---

## Files reviewed and modified

| Component | File | Changes |
|-----------|------|---------|
| `sp-admin-card` | `card/sp-admin-card.component.ts` | Borders, flat bg, section accent, hover ring, dividers, spinner |
| `sp-admin-button` | `button/sp-admin-button.component.ts` | solid/outline/soft/ghost/link primary; neutral/secondary hover |
| `sp-admin-badge` | `badge/sp-admin-badge.component.ts` | soft/solid/outline primary fallback values |
| `sp-admin-stat-card` | `stat-card/sp-admin-stat-card.component.ts` | Border inline style, icon-indigo/icon-primary |
| `sp-admin-empty-state` | `empty-state/sp-admin-empty-state.component.ts` | Title color, msg color, CTA bg fallback |
| `sp-admin-table` | `table/sp-admin-table.component.ts` | Shell borders, thead bg/style, th/td color, row hover/stripe, bordered variants |
| `sp-admin-input` | `input/sp-admin-input.component.ts` | Border, text, placeholder, focus ring, disabled bg |
| `sp-admin-select` | `select/sp-admin-select.component.ts` | Border, text, focus ring, disabled bg |
| `sp-admin-pagination` | `pagination/sp-admin-pagination.component.ts` | Border-top, label color |
| `sp-admin-slide-over` | `slide-over/sp-admin-slide-over.component.ts` | Panel border, header border, footer border, title/subtitle colors, close btn |
| `sp-admin-modal` | `modal/sp-admin-modal.component.ts` | Title color fallbacks for default/form/confirm variants |
| `sp-admin-textarea` | `textarea/sp-admin-textarea.component.ts` | Border, text, placeholder, focus ring, disabled bg |
| `sp-admin-number-input` | `number-input/sp-admin-number-input.component.ts` | Border, text, placeholder, focus ring, disabled bg |
| `sp-admin-checkbox` | `checkbox/sp-admin-checkbox.component.ts` | Label color |
| `sp-admin-drawer` | `drawer/sp-admin-drawer.component.ts` | Left/right border |
| `sp-admin-copyable-text` | `copyable-text/sp-admin-copyable-text.component.ts` | Hover color |
| `sp-admin-code-pill` | `code-pill/sp-admin-code-pill.component.ts` | Primary variant bg/color |
| `sp-admin-alert` | `alert/sp-admin-alert.component.ts` | Info variant bg/color fallback values |
| `sp-admin-kpi-card` | `kpi-card/sp-admin-kpi-card.component.ts` | Border, icon-indigo, label color, value color fallbacks |
| `sp-admin-section-card` | `section-card/sp-admin-section-card.component.ts` | Border fallback, title color |
| `sp-admin-section-header` | `section-header/sp-admin-section-header.component.ts` | Title color, desc color |
| `sp-admin-table-actions` | `table-actions/sp-admin-table-actions.component.ts` | Menu border, item color, item hover |
| `sp-admin-status-card` | `status-card/sp-admin-status-card.component.ts` | Skeleton bg, border fallback |

---

## Token mapping applied

| Old literal | New token | Fallback |
|-------------|-----------|----------|
| `#465fff` | `var(--sp-admin-primary)` | `#5B4BE8` |
| `#ecf3ff` | `var(--sp-admin-primary-bg)` | `#EDEBFF` |
| `#93c5fd` | `var(--sp-admin-primary)` | `#5B4BE8` (focus border) |
| `rgba(147,197,253,.3)` | `var(--sp-admin-focus-ring)` | `0 0 0 3px rgba(91,75,232,.15)` |
| `#e5e7eb` | `var(--sp-admin-border)` | `#ECE9F5` |
| `#f1f5f9` | `var(--sp-admin-border-subtle)` | `#F4F2FC` |
| `#f3f4f6` | `var(--sp-admin-border-subtle)` | `#F4F2FC` |
| `#f9fafb` | `var(--sp-admin-surface-subtle)` | `#FBFAFE` |
| `#111827` | `var(--sp-admin-text)` | `#0F172A` |
| `#374151` | `var(--sp-admin-text)` | `#0F172A` |
| `#6b7280` | `var(--sp-admin-text-muted)` | `#64748B` |
| `#9ca3af` | `var(--sp-admin-text-faint)` | `#CBD5E1` |

---

## Intentional non-changes

- Dark-mode panel overrides in `sp-admin-slide-over` (`#111827` background, `#1f2937` border) — these are intentional dark-theme panel surfaces, not brand blues.
- Dark-mode item hover in `sp-admin-table-actions` (`#1f2937` bg, `#f9fafb` text) — same reason.
- Non-primary semantic colors (success/warning/danger/info) — these are correct semantic tokens, not brand color mismatches.
- `sp-admin-page-header`, `sp-admin-alert`, `sp-admin-filter-bar` — already fully token-based before this phase; no changes needed.

---

## Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1035/1035 |
| Backend unchanged | n/a |

---

## Decisions made

- Dark-mode backgrounds (`#111827`, `#1f2937`) are preserved. They are correct for dark panel surfaces and unrelated to the TailAdmin brand-blue → SpeakPath-indigo shift.
- Token fallback values updated to SpeakPath palette even when wrapped in `var()`, so the fallback also renders correctly if the custom property is somehow absent.
- `sp-admin-status-card` uses a component-local `--sp-sc-border` variable for tone overrides; its fallback was updated to `#ECE9F5` but the tone-specific values (success/warning/danger/info) were left as-is (correct semantic colors).

---

## Risks and unresolved questions

- No visual regression baseline exists (TODO-10X-J-T-VISUAL-BASELINE). Token substitution is verified correct by inspection and build/test gates, not by screenshot diff.
- Page-level visual alignment (admin feature pages) is deferred to TODO-UI-PAGE-ALIGNMENT.

---

## Final verdict

All brand-color literals in reusable `sp-admin-*` components replaced with tokens. Build and test suite green. Phase complete.

---

## Next recommended action

Commit `ui: align admin core components with reference design` and proceed to page-level spot-check (TODO-UI-PAGE-ALIGNMENT) or next priority TODO.
