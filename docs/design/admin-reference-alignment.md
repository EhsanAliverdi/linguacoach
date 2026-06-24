# Admin Design Reference Alignment

**Reference file:** `docs/design/SpeakPath Admin (standalone) V1.html`
**Last updated:** 2026-06-24 (10UI-PARITY-FINAL)

This document tracks the alignment status of the Angular admin against the standalone reference.

---

## Status: CLOSED — parity accepted with minor known gaps

All phases complete as of commit `2075134`.

---

## Token Alignment

| Standalone token | Value | Angular token | Status |
|---|---|---|---|
| `--ink` | `#211B36` | `--sp-admin-text` | ✅ exact |
| `--text` | `#4B4462` | `--sp-admin-text-secondary` | ✅ exact |
| `--muted` | `#8B85A0` | `--sp-admin-text-muted` | ✅ exact |
| `--faint` | `#C5BFD8` | `--sp-admin-text-faint` | ✅ exact |
| `--border` | `#ECE9F5` | `--sp-admin-border` | ✅ exact |
| `--border-2` | `#E2DEF0` | `--sp-admin-border-2` | ✅ exact |
| `--surface` | `#FFFFFF` | `--sp-admin-surface` | ✅ exact |
| `--surface-2` | `#F6F4FB` | `--sp-admin-surface-subtle` | ✅ exact |
| `--canvas` | `#F6F4FB` | `--sp-admin-bg` | ✅ exact |
| `--indigo` | `#5B4BE8` | `--sp-admin-primary` | ✅ exact |
| `--indigo-soft` | `#EDEBFF` | `--sp-admin-primary-bg` | ✅ exact |
| `--indigo-ink` | `#3A2EA8` | `--sp-admin-primary-hover` | ✅ exact |
| `--coral` | `#FF7A59` | `--sp-admin-coral` | ✅ exact |
| `--magenta` | `#B45CF0` | `--sp-admin-magenta` | ✅ exact |
| `--success` | `#13B07C` | `--sp-admin-green` | ✅ exact |
| `--warn` | `#F0982C` | `--sp-admin-warn` | ✅ exact |
| `--danger` | `#EF4444` | `--sp-admin-danger` | ✅ exact |
| `--sh-xs` | `0 1px 2px rgba(33,27,54,.06)` | `--sp-admin-shadow-xs` | ✅ exact |
| `--sh-sm` | `0 2px 8px rgba(60,48,140,.07)` | `--sp-admin-shadow-sm` | ✅ exact |
| `--sh-md` | `0 8px 24px rgba(60,48,140,.10)` | `--sp-admin-shadow-md` | ✅ exact |

---

## Shared Component Alignment

| Component | Key standalone class | Angular component | Status |
|---|---|---|---|
| Card | `.adm-card` | `sp-admin-card` | ✅ |
| KPI card | `.adm-kpi` | `sp-admin-kpi-card` | ✅ |
| Button | `.adm-btn` | `sp-admin-button` | ✅ |
| Badge | `.adm-badge` | `sp-admin-badge` | ✅ |
| Table | `.adm-table` | `sp-admin-table` | ✅ |
| Pagination | `.adm-pagination` | `sp-admin-pagination` | ✅ |
| Input | `.adm-input` | `sp-admin-input` | ✅ |
| Toggle | `.adm-toggle` | `sp-admin-toggle` | ✅ |
| Nav item | `.adm-nav-item` | `styles.css .sp-nav-item` | ✅ |
| Detail hero | `.adm-detail-hero` | inline in `admin-student-detail` | ✅ |

---

## Phase History

| Phase | Date | Commit | What changed |
|---|---|---|---|
| 10UI-PARITY-1A | 2026-06-24 | pre-context | initial token pass |
| 10UI-PARITY-1B | 2026-06-24 | `a3dff57` | parity fixes pass 1 |
| 10UI-PARITY-1C-A | 2026-06-24 | `104624a` + `6e9196d` | card, kpi-card, button, badge, table, pagination, input, toggle, tokens |
| 10UI-PARITY-1C-B1 | 2026-06-24 | `c051eb8` | student detail hero, ai-config select, usage-policies expansion |
| 10UI-PARITY-FINAL | 2026-06-24 | `2075134` | full Tailwind gray sweep across all 16 admin feature files |

---

## Accepted Gaps

| Gap | Reason | Severity |
|---|---|---|
| Graph/chart areas use placeholder divs | No charting library allowed | P3 — acceptable |
| No live screenshot for all routes | Backend not running in audit session | P3 — documented |
