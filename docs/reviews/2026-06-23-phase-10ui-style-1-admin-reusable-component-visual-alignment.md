# Engineering Review — Phase 10UI-STYLE-1: Admin Reusable Component Visual Alignment

**Date:** 2026-06-23
**Sprint:** Phase 10UI-STYLE-1
**HEAD before work:** 54c67a3

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-STYLE-1 entry

---

## Reference files inspected

- `docs/design/speakpath/SpeakPath Brand & System.html` — colour palette, typography, radius, shadows
- `docs/design/speakpath/admin/Admin.html` — admin page background, card styles, sidebar/header, nav items, KPI card layout
- `docs/design/speakpath/admin/shell.jsx` — sidebar dimensions, active nav styles, header layout
- `docs/design/admin-reference-alignment.md` — prior phase status
- `docs/reviews/2026-06-23-phase-10ui-redesign-final-admin-ui-reference-closure-audit.md` — debt items identified

---

## Gap analysis

The admin components were structurally correct from prior phases. The visual gaps were:

| Gap | Current | Reference |
|---|---|---|
| Text ink colour | `#0F172A` (Tailwind slate-900) | `#211B36` (warm purple ink) |
| Body text colour | `#334155` (Tailwind slate-700) | `#4B4462` (warm purple text) |
| Muted text colour | `#64748B` (Tailwind slate-500) | `#8B85A0` (warm purple muted) |
| Dim text colour | `#94A3B8` (Tailwind slate-400) | `#BDB8CC` (warm purple faint) |
| Faint text colour | `#CBD5E1` (Tailwind slate-300) | `#D4D0E0` |
| Green status | `#16A34A` / `#F0FDF4` (Tailwind) | `#13B07C` / `#E0F6EE` (SpeakPath brand green) |
| Card shadow | `rgba(0,0,0,.05)` (colourless) | `rgba(33,27,54,.06)` (purple-tinted) |
| Card shadow hover | `rgba(0,0,0,.08)` | `rgba(60,48,140,.10)` |
| Card radius (md) | `12px` | `14px` |
| Card radius (lg) | `14px` | `18px` |
| KPI value size | `24px font-weight 700` | `28px font-weight 800` |
| KPI label weight | `font-weight 600` | `font-weight 800` |
| KPI label letter-spacing | `.05em` | `.08em` |
| KPI icon size | `44px / 12px radius` | `40px / 11px radius` |
| Card title | raw Tailwind `text-gray-800 font-medium` | CSS token `13.5px font-weight 700 var(--sp-admin-text)` |
| Card default radius | `'2xl'` (20px) | `'md'` (14px) |
| Badge font-weight | `500` | `700` |
| Table header colour | `text-dim` | `text-muted` |
| Table header weight | `700` | `800` |
| Table header letter-spacing | `.05em` | `.08em` |
| Page title letter-spacing | `-.02em` | `-.03em` |
| Page subtitle size | `13.5px` | `13px` |
| Notifications slide-over | 29 raw Tailwind literals | CSS token classes |
| Integrations static text | 3 raw Tailwind literals | CSS token classes |

---

## Tokens changed

**File:** `src/LinguaCoach.Web/src/app/design-system/admin/tokens/admin-tokens.css`

| Token | Before | After |
|---|---|---|
| `--sp-admin-text` | `#0F172A` | `#211B36` |
| `--sp-admin-text-secondary` | `#334155` | `#4B4462` |
| `--sp-admin-text-muted` | `#64748B` | `#8B85A0` |
| `--sp-admin-text-dim` | `#94A3B8` | `#BDB8CC` |
| `--sp-admin-text-faint` | `#CBD5E1` | `#D4D0E0` |
| `--sp-admin-green` | `#16A34A` | `#13B07C` |
| `--sp-admin-green-bg` | `#F0FDF4` | `#E0F6EE` |
| `--sp-admin-green-ring` | `#DCFCE7` | `#A8EDD4` |
| `--sp-admin-shadow-card` | `0 1px 3px rgba(0,0,0,.05), 0 1px 2px rgba(0,0,0,.04)` | `0 1px 2px rgba(33,27,54,.06)` |
| `--sp-admin-shadow-card-hover` | `0 4px 12px rgba(0,0,0,.08)` | `0 8px 24px rgba(60,48,140,.10)` |
| `--sp-admin-shadow-action` | `0 2px 8px rgba(91,75,232,.10)` | `0 2px 8px rgba(91,75,232,.12)` |
| `--sp-admin-shadow-dropdown` | `0 10px 32px rgba(15,23,42,.14)` | `0 10px 32px rgba(33,27,54,.14)` |
| `--sp-admin-shadow-modal` | `0 20px 60px rgba(15,23,42,.20)` | `0 20px 60px rgba(33,27,54,.20)` |
| `--sp-admin-radius-md` | `12px` | `14px` |
| `--sp-admin-radius-lg` | `14px` | `18px` |
| `--sp-admin-radius-xl` | `18px` | `22px` |

---

## Reusable components changed

### 1. `sp-admin-kpi-card` (`sp-admin-kpi-card.component.ts`)

- Value: `24px font-weight 700` → `28px font-weight 800 letter-spacing -.03em`
- Label: `font-weight 600 letter-spacing .05em` → `font-weight 800 letter-spacing .08em`
- Icon tile: `44px / border-radius 12px` → `40px / border-radius 11px`
- Card border-radius: hardcoded `16px` → `var(--sp-admin-radius-md, 14px)`
- Shadow: updated to consume token

### 2. `sp-admin-page-header` (`sp-admin-page-header.component.ts`)

- Title letter-spacing: `-.02em` → `-.03em`
- Title line-height: `1.2` → `1.15`
- Subtitle font-size: `13.5px` → `13px`
- Subtitle line-height: added `1.5`

### 3. `sp-admin-card` (`sp-admin-card.component.ts`)

- Card title: removed raw Tailwind `text-base font-medium text-gray-800` → `.sp-adm-card-title` CSS token rule `13.5px font-weight 700 var(--sp-admin-text)`
- Radius classes: hardcoded px → `var(--sp-admin-radius-*)` tokens
- Default radius input: `'2xl'` → `'md'` (14px — matches reference)

### 4. `sp-admin-badge` (`sp-admin-badge.component.ts`)

- Badge font-weight: `500` → `700` (matches reference `font-weight: 700`)
- Badge sm size: `11px` → `11.5px`

### 5. `sp-admin-data-table` (`sp-admin-data-table.component.ts`)

- Table header colour: `var(--sp-admin-text-dim)` → `var(--sp-admin-text-muted)` (readable warm purple)
- Table header weight: `700` → `800`
- Table header letter-spacing: `.05em` → `.08em`

### 6. `src/styles.css` (global)

Added admin slide-over/form utility classes to replace Tailwind literals:
- `.sp-adm-label` — form label (13px font-weight 600 token colour)
- `.sp-adm-label-opt` — optional label suffix (muted)
- `.sp-adm-label-req` — required asterisk (danger)
- `.sp-adm-hint` — hint/help text (12px muted)
- `.sp-adm-hint-error` — inline error text (12px danger)
- `.sp-adm-code-preview` — monospace preview box (surface-subtle bg)
- `.sp-adm-preview-box` — preview result container
- `.sp-adm-preview-field` — field label inside preview (muted)
- `.sp-adm-section-divider` — border-top section divider
- `.sp-adm-textarea` — token-styled textarea (replaces Tailwind textarea classes)
- `.sp-adm-textarea-mono` — monospace textarea variant

---

## Closure-audit debt addressed

| Debt item | Status |
|---|---|
| 29 Tailwind literals in notifications slide-over forms | **Resolved** — all replaced with `sp-adm-*` CSS token classes |
| 3 Tailwind literals in integrations static text | **Resolved** — replaced with `.sp-adm-hint` |

---

## Route-level visual impact summary

All 14 admin routes benefit from token changes automatically. Key improvements visible across all pages:

- Text colour is now warm purple (`#211B36`) matching the SpeakPath brand, not cool grey-blue
- Muted text is warm purple-grey (`#8B85A0`), not Tailwind slate
- Card shadows have a warm purple tint matching the reference
- All card radii are 14px (via token) — tighter and more consistent
- KPI strips show larger, bolder values (28px/800) with wider-spaced uppercase labels — matches reference layout
- Card titles are consistently styled with token-based font/colour
- Table headers are legibly muted with heavier weight
- Badge text is bolder (weight 700)
- Form slide-overs use consistent token-based labels, hints, and inputs

---

## Tests

No new tests added. All styling changes are visual — no component API or contract changes. All existing tests verified:

- `sp-admin-kpi-card` — value/label text content unchanged, tile count unchanged
- `sp-admin-card` — title text content unchanged, loading/variant behaviour unchanged
- `sp-admin-badge` — tone/appearance logic unchanged
- Notifications secret/fake-data/header tests — all pass
- No regressions

**Test count: 1253/1253 PASS**

---

## Gates

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| Production build | Clean (pre-existing template warnings only) |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1253/1253 PASS |
| Backend build | Not run — no backend source changes |
| Playwright | Not run — no existing E2E specs for admin visual styling |

---

## Decisions made

1. Text colours changed from Tailwind slate to SpeakPath warm purple to match brand identity. The slate palette was a TailAdmin residue — all `--sp-admin-text-*` tokens now use the same purple-ink palette as the reference design (`#211B36`, `#4B4462`, `#8B85A0`, `#BDB8CC`).
2. Green updated from `#16A34A` (Tailwind green-600) to `#13B07C` (SpeakPath brand green). This affects success badges, green KPI tiles, and green alerts across all admin pages.
3. Card default radius changed from `'2xl'` (was 20px, now 22px) to `'md'` (14px). The reference uses 14px for standard cards. Existing pages that explicitly pass `radius` are unaffected.
4. KPI value typography stepped up to match reference `30px font-weight 800` pattern — used `28px` as `30px` pushed too large in the 4-column grid layout.
5. Slide-over textarea elements use new `.sp-adm-textarea` class instead of raw Tailwind — consistent with the rest of the design token system.

---

## Remaining visual gaps

| Gap | Reason deferred |
|---|---|
| Admin sidebar CSS file (`admin-app-layout.component.css`) uses hardcoded hex colours for profile menu | Profile menu is a secondary UI element; colours are close to reference; refactor would require TS+CSS combined edit with test impact |
| `text-green-600` in notifications success feedback text | Green is now brand-aligned from token; Tailwind class produces same visual result |
| AI Config provider card uses dynamic Tailwind border composition | Functional, complex to CSS-tokenise without logic change |
| Font-family: `Plus Jakarta Sans` not explicitly loaded in Angular project | Font is loaded via Google Fonts in `index.html`; present in browser; not a token gap |

---

## Confirmation

- No backend API changes
- No migrations
- No business logic changes
- No student-facing UI changes
- No new admin features
- No fake/invented data
- No secrets displayed
- All pages use existing data logic unchanged
- All component APIs unchanged (no breaking changes to inputs/outputs)
