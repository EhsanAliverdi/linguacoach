# Engineering Review — Phase 10UI-REDESIGN-5: AI Config and Prompts Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-5
**Commit:** 136cb84

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-5 entry

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/ai-config.jsx` — provider list strip, LLM categories table with X/N configured badge, TTS section, Rate limits with progress bars
- `docs/design/speakpath/admin/pages/prompts.jsx` — tab+search layout, prompt row card pattern, category/status badges, modal editor
- `docs/design/speakpath/admin/Admin.html` — CSS context for adm-card, adm-badge patterns

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-prompts/admin-prompts.component.spec.ts`

---

## Reference gap analysis

### AI Config

| Reference section | Status | Notes |
|---|---|---|
| Provider list strip (Connected/Not set per provider) | Already present | Section 3 (Provider credentials) covers this |
| LLM categories table with X/N badge | Added | `configSummary().llmConfigured/llmTotal` badge on card header |
| TTS section | Already present | Section 2 unchanged |
| Rate limits with progress bars | Not implemented — explicit card | No backend endpoint; card added with "Backend not available yet" label |
| KPI summary strip | Added | 4 tiles: LLM configured, TTS configured, providers with key, pricing models |

### Prompts

| Reference section | Status | Notes |
|---|---|---|
| Tab-style category filter | Partially aligned | Added `categoryFilter` signal + select in filter bar — full tab component not available in the sp-admin design system |
| Subtitle with template count | Added | `'Manage and version AI prompt templates · N templates'` |
| Stat card strip | Upgraded | `sp-admin-stat-card` → `sp-admin-kpi-card` (consistent with other pages) |
| Category/type badge per row | Added | Derived from first key segment (e.g. `writing.exercise.*` → "Writing") |
| Modal editor | Already present | Existing `detail()` card + `showForm()` slide-in retained unchanged |

---

## Changes made

### AI Config component

**New: `configSummary` computed signal**
```typescript
readonly configSummary = computed(() => {
  // llmConfigured/llmTotal — LLM categories with non-null, non-fake provider
  // ttsConfigured/ttsTotal — TTS categories with non-null, non-fake provider
  // providersWithKey      — providers where catalog.hasApiKey === true
  // pricingModels         — total pricing rows loaded
});
```

**New: 4-tile KPI summary strip**
Placed above `sp-admin-page-body`, gated on `!loading() && !loadError()`.
- LLM configured (green when all set, amber when some missing)
- TTS configured (teal when ≥1 set, slate otherwise)
- Providers with key (indigo)
- Pricing models (violet)

`SpAdminKpiCardComponent` added to imports.

**New: X/N configured badge on LLM Categories card**
`<div slot="actions">` containing `<sp-admin-badge>` with `configSummary().llmConfigured/llmTotal` — matches reference pattern.

**New: Rate limits and quotas card**
Section 5 at the bottom. Shows `aria-label="Rate limits not implemented"` div with:
> Backend not available yet — Real-time rate limit usage requires a backend endpoint that is not yet implemented. Cost and token usage totals are visible on the AI Usage page.

Link to `/admin/usage` included. No fake progress bars, no invented usage numbers.

**Security: API keys never displayed**
No template change introduced secret exposure. Key fields remain `type="password"`. The "Key stored ✓" label from Section 3 is the only acknowledgement that a key exists.

### Prompts component

**Removed: `SpAdminStatCardComponent`** — replaced by `SpAdminKpiCardComponent`.

**Updated: page header**
- Title changed from `"Prompt Templates"` to `"Prompts"` (matches reference)
- Subtitle updated to `'Manage and version AI prompt templates · N templates'` where N is `prompts().length` (real count)

**New: KPI strip** (`.sp-pt-kpi-strip`)
Four `sp-admin-kpi-card` tiles replacing `sp-admin-stat-card` grid:
- Templates (unique key count, indigo)
- Active versions (green or slate)
- Total versions (violet)
- Avg token budget (amber)

**New: `categoryFilter` signal + `setCategoryFilter()`**
Derives category from first key segment. Resets page to 1 on change.

**New: `categoryFilterOptions` computed**
Builds `{value, label}` array from distinct categories in loaded prompts. Drives the new category select in the filter bar.

**Updated: `filteredPrompts` computed**
Now includes `matchesCategory` check in addition to existing search and status filters.

**New: `promptCategory(key)` method**
Extracts and capitalises the first `.`-delimited segment. Returns `'Other'` for single-segment keys.

**New: `categoryTone(cat)` method**
Maps known categories to badge tones: Writing→info, Speaking→warning, Feedback→success, Assessment→danger. Default: neutral.

**New: Category column in table**
Added between Key and Version. Shows `<sp-admin-badge [tone]="categoryTone(promptCategory(p.key))">` with the derived category label.

---

## Real data / honest labels

| Section | Source | Notes |
|---|---|---|
| AI Config KPI strip | `categories()` and `providers()` signals — loaded from API | No estimates |
| X/N configured badge | Same | No estimates |
| Rate limits card | None | "Backend not available yet" — no fake usage |
| Prompts KPI strip | `prompts()` signal — loaded from API | All derived from real list |
| Category badge | Derived from `key` field on `PromptTemplateItem` | Honest derivation, labelled as such |
| Subtitle count | `prompts().length` | Live count |

No fake data introduced anywhere.

---

## Behaviours preserved

### AI Config
- LLM category save/test flows unchanged
- TTS category save/test flows unchanged
- Provider credentials: Set key, Clear key, Configure Qwen (key + endpoint), Test connection
- Model add + Test model
- Model Pricing: read-only config table, DB overrides (create/edit/deactivate)
- Loading and error states

### Prompts
- Create new prompt version form
- View prompt content detail card
- Activate/Deactivate per version
- Search by key
- Status filter (all/active/inactive)
- Pagination
- Refresh button

---

## Student safety

No prompt editing route or UI is exposed to students. Prompts remain admin-only. No changes to authentication guards or routing. Confirmed via code review — all prompt edit surfaces are inside the admin feature module.

---

## Tests

### New tests (20 total)

**AI Config (10):**
- `sp-admin-kpi-card` elements render for summary strip
- Summary strip has `aria-label="AI configuration summary"`
- `configSummary.llmConfigured` counts configured LLM categories
- `configSummary.ttsConfigured` counts configured TTS categories
- `configSummary.providersWithKey` counts providers with stored key
- `configSummary.pricingModels` counts pricing rows
- Configured badge on LLM categories card header shows `X/N configured`
- Rate limits card renders with "Backend not available yet"
- Rate limits card has `aria-label="Rate limits not implemented"`
- API keys are not displayed in any rendered text

**Prompts (10):**
- `sp-admin-kpi-card` elements render for summary strip
- Summary strip has `aria-label="Prompt template summary"`
- Page header subtitle includes template count
- `promptCategory()` extracts first key segment capitalised
- `promptCategory()` returns `Other` for single-segment key
- Category badge renders in table row
- `categoryFilter` filters rows by derived category
- `setCategoryFilter` resets page to 1
- `categoryFilterOptions` computed from loaded prompts
- `categoryTone` returns known tone for Writing; neutral for unknown

**Total: 1206/1206 pass.**

---

## Gates passed

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| Production build | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1206/1206 PASS |
| Backend build | Not run (no backend changes) |
| Playwright | Not run (no E2E specs for these pages) |

---

## Decisions made

1. Rate limits card shows "Backend not available yet" — no fake progress bars or invented RPM/token numbers. Link to AI Usage page for cost visibility.
2. Category column in prompts table is derived from the key's first segment — not a separate DB field. Honest derivation, clearly readable from the key pattern. If a proper `featureArea` field is added to `PromptTemplateItem` in future, the column can be upgraded.
3. Tab-style category navigation (reference pattern) not implemented as tabs — `SpAdminFilterBarComponent` with a select achieves the same filtering without introducing a new tab component.
4. `sp-admin-stat-card` replaced by `sp-admin-kpi-card` — consistent with REDESIGN-1 through REDESIGN-4 upgrades across all admin pages.
5. Page title changed from "Prompt Templates" to "Prompts" — matches reference. Existing spec updated accordingly.

---

## Deferred gaps

- Rate limits backend endpoint: requires new API surface. Not in scope.
- TTS card configuration (Provider/Voice/Speed dropdowns from reference): backend already supports TTS category config; the existing TTS section covers this. A dedicated "Text-to-Speech" styled card matching the reference layout more closely is deferred.
- Full tab bar for prompt categories: requires a tab component or inline tab pattern. Current select achieves the same function.

---

## Final verdict

**Complete.** 1206/1206. Build clean. All security constraints satisfied. No fake data. No backend changes. All existing behaviour on both pages preserved.

---

## Next recommended action

**10UI-REDESIGN-6** — AI Usage redesign.

See: `docs/design/admin-reference-alignment.md` — `/admin/usage` row.
