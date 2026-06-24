# Phase 10UI-AI-CONFIG-PARITY-1 — Engineering Review

**Date:** 2026-06-24
**Sprint/Feature:** 10UI-AI-CONFIG-PARITY-1
**Branch:** main

---

## Summary

Rebuilt the `/admin/ai-config` page to match the design target at `docs/design/speakpath/admin/pages/ai-config.jsx`. Replaced inline forms with display-only cards and slide-over drawers. Extracted template and styles to separate files to avoid Angular template-literal parsing failures caused by nested backticks.

---

## Parts Completed

### Part A — KPI Strip
- Replaced `sp-admin-kpi-card` slot-based components with native `.sp-aic-kpi-card` tiles.
- Each tile: 52px colored icon column (`background` set per tile), right body with uppercase 10.5px/800 label and 24px/800 value.
- Four tiles: LLM Configured, TTS Configured, Providers With Key, Pricing Models.
- All values derived from `configSummary()` computed signal — real backend data only.

### Part B — Tab Bar
- Five tabs: LLM Categories, Text-to-Speech, Provider Credentials, Model Pricing, Rate Limits.
- Native `button` elements with `.sp-aic-tab` and `.sp-aic-tab--active` classes.
- Active tab controlled by `activeTab = signal<AiConfigTab>('llm')`.

### Part C — LLM Categories Tab
- Category cards: `.sp-aic-cat-card` with configured border highlight.
- Each card: icon (38×38 rounded), name, `code.sp-aic-code-pill` key, Configured/Not set badge.
- Read-only Provider and Model display fields: `.sp-aic-field-value` (height:32px, border, canvas bg) — look like inputs, are read-only.
- Configure button opens the Configure drawer. Test button preserved.

### Part D — Configure LLM Slide-Over Drawer
- Uses `sp-admin-slide-over` (size='md' = 480px).
- `configuringCategory = signal<CategoryState | null>(null)` drives open/close.
- Body: description callout (canvas bg), Provider `<select>`, Model `<select>` (disabled when no provider), optional Voice `sp-admin-input` (TTS only), resolution order callout.
- Footer: Save changes / Cancel (sticky via `[slot=footer]`).
- Save delegates to `saveCategory(cs)` → `updateAiCategory` API.

### Part E — Model Pricing Tab
- Per-provider section cards: `.sp-aic-pricing-provider-card` with uppercase provider header + model count badge + pricing table.
- One card per provider, sorted alphabetically.
- **Add override** button is at the top of the pricing section (not inside a card).
- Pricing overrides are in a separate `.sp-aic-overrides-card` below.
- Single empty state when no overrides — no duplicate empty states.

### Part F — Add / Edit Pricing Override Slide-Over Drawer
- Orange warning callout (`background:#FFFBEB; border: 1px solid #FDE68A; color:#92400E`).
- Model `<select>` with `<optgroup>` per provider (from `pricingByProvider()`).
- Auto-fills providerName when model is selected (`onOverrideModelChange`).
- Config price hint shown when model is selected.
- `$` prefix price inputs (relative positioned prefix span).
- Effective date range section with From/To date inputs.
- Note text input.
- Footer: Save override / Save changes (edit mode) / Cancel.

### Part G — Provider Credentials
- No broad redesign. Restructured into `.sp-aic-provider-card` with header row, model chips, add-model row, optional key/Qwen config form.
- Secret safety preserved: API keys use `type="password"` inputs, never rendered as text.

### Part H — Template/Style Extraction
- Template moved to `admin-ai-config.component.html`.
- Styles moved to `admin-ai-config.component.css`.
- Root cause of original build failure: inline `styles: [\`...\`]` inside `template: \`...\`` terminated the outer template literal when it encountered a nested backtick.

---

## Files Modified

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts` — class rewritten; `templateUrl`/`styleUrl` added
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.html` — new external template
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.css` — new external styles
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.spec.ts` — updated 3 tests:
  - `renders model test chips for each model` — added `'credentials'` tab
  - `renders kpi tile cards for the summary strip` — changed selector from `sp-admin-kpi-card` to `.sp-aic-kpi-card`
  - `saveOverride with empty modelName sets error` — renamed; now tests empty `modelName` not empty `providerName`
  - Added `createAiPricingOverride`, `updateAiPricingOverride`, `deactivateAiPricingOverride` spies to `makeAdminApi`
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` — updated 4 tests:
  - `AI Config page renders with wrapper cards` — changed `sp-admin-page-header` → `.sp-aic-page-header`, `sp-admin-card` → `.sp-aic-cat-card`
  - `AI Config page renders sp-admin-form-field wrappers...` — switched to credentials tab (form fields in credentials, not LLM tab)
  - `AI Config TTS voice field uses sp-admin-input wrapper` — switched to credentials tab
  - `AI Config LLM category native selects are wrapped...` — opens Configure drawer before checking for selects

---

## Security Constraints Verified

- No API keys, secrets, or provider secrets rendered in any tab or drawer.
- API key inputs use `type="password"`.
- No mock data imported from JSX files.
- No student UI touched.
- No migrations added.
- No chart libraries added.

---

## Test Result

**1325 / 1325 passing.** No regressions.

---

## Build

Production build clean. Pre-existing warnings in `AdminAiUsageComponent`, `AdminDiagnosticsComponent`, `PatternEvaluationResultComponent` are unrelated and pre-date this phase.

---

## Decisions Made

- Template and styles extracted to `.html` / `.css` files (not inline). Inline backtick styles inside a backtick template string caused `TS-991010: template must be a string` build errors.
- LLM and TTS category cards are read-only display tiles; editing is done in the Configure slide-over drawer. This matches the design target exactly.
- `providerName` is not required as a separate validation field in the override form; it is auto-derived from the selected model via `onOverrideModelChange`.
- Rate Limits tab renders a "Backend not available yet" notice with `aria-label="Rate limits not implemented"` — no backend endpoint exists for real-time rate data.

---

## Documentation Impact

- Docs reviewed: `docs/design/speakpath/admin/pages/ai-config.jsx`, `docs/reviews/2026-06-20-phase-10u-0-ai-config-usage-admin-gap-check.md`
- Docs updated: This review doc
- Docs intentionally not updated: `docs/handoffs/current-product-state.md`, `TODOS.md` — pending until full phase is complete and pushed

---

## Final Verdict

**Parts A–H: COMPLETE.** 1325/1325 tests pass. Build clean.

## Next Recommended Action

Commit `ui: match ai config content and drawers to design` and push. Then update `docs/handoffs/current-product-state.md` and `TODOS.md`.
