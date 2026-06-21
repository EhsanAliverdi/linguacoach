# Phase 10V-0 — AI Pricing Admin Gap Check

**Date:** 2026-06-21
**Sprint:** Phase 10V
**Type:** Audit / gap check (no code changed)

---

## Files inspected

**Backend**
- `src/LinguaCoach.Infrastructure/Ai/AiPricingOptions.cs`
- `src/LinguaCoach.Infrastructure/Ai/OpenAiProvider.cs`
- `src/LinguaCoach.Infrastructure/Ai/GeminiProvider.cs`
- `src/LinguaCoach.Infrastructure/Ai/AnthropicProvider.cs`
- `src/LinguaCoach.Api/appsettings.json`
- `src/LinguaCoach.Domain/Entities/AiConfigCategory.cs`
- `src/LinguaCoach.Domain/Entities/AiProviderConfig.cs`
- `tests/LinguaCoach.UnitTests/Ai/OpenAiProviderTests.cs`

**Frontend**
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`

---

## 1. Current pricing source

Pricing is stored entirely in `appsettings.json` under provider-namespaced config sections:

```json
"OpenAI": { "Pricing": { "gpt-4o": { "InputPer1KTokens": 0.0025, "OutputPer1KTokens": 0.01 } } }
"Gemini": { "Pricing": { "gemini-2.0-flash": { ... } } }
"Anthropic": { "Pricing": { "claude-sonnet-4-6": { ... } } }
```

`AiPricingOptions` (static class, `Infrastructure.Ai`) reads these via `IConfiguration`. It exposes:

- `GetOpenAiPricing(IConfiguration, modelName)` → `AiModelPricing?`
- `GetGeminiPricing(IConfiguration, modelName)` → `AiModelPricing?`
- `GetProviderPricing(IConfiguration, providerName, modelName)` → `AiModelPricing?`
- `EstimateCostUsd(inputTokens, outputTokens, pricing)` → `decimal`

All three providers call `AiPricingOptions.Get*Pricing(...)` then `EstimateCostUsd(...)` immediately after a successful call. Missing pricing returns `null`; cost is logged as `0m`.

**Pricing is config-only.** No database table. No runtime override. No admin endpoint.

---

## 2. Current AI Config admin page

The admin AI config page (`AdminAiConfigComponent`) has three sections:

| Section | What it manages |
|---------|----------------|
| LLM Categories | `AiConfigCategory` rows — provider + model per category key |
| TTS Categories | Same entity, voice field added |
| Provider credentials | `AiProviderCatalog` — API key, endpoint, model list, test results |

**No pricing fields exist anywhere in the admin UI.** The admin can set provider, model, voice, and API key. Pricing is invisible to the admin. There is no read-only display of pricing, no edit form, no create flow.

---

## 3. Is pricing hardcoded in C#?

No. Pricing is not hardcoded in C# source. It is seeded in `appsettings.json` and read via `IConfiguration`. Changing the config file changes prices immediately on restart. However: the config file is server-managed and not admin-editable at runtime. An admin has no UI or API to view or change prices.

---

## 4. Is pricing admin-editable today?

**No.** There is no API endpoint, no Angular component, and no database record for pricing. Only a deployment config change can update prices.

---

## 5. Existing persistence available?

**No pricing persistence.** `AiConfigCategory` stores provider/model/voice only. `AiProviderConfig` (feature-key overrides) stores provider/model/voice only. Neither entity has pricing fields. No migration exists for pricing data.

The `AiUsageLog` entity stores `CostUsd` as a calculated decimal, but that is a log record, not a price reference.

---

## 6. Tests relying on config pricing

`tests/LinguaCoach.UnitTests/Ai/OpenAiProviderTests.cs` contains eight pricing unit tests. They all inject pricing via in-memory `IConfiguration` dictionaries. No test reads from `appsettings.json` at runtime. Switching to DB-backed pricing would require updating these tests to mock or seed from a repository rather than config.

---

## 7. Missing model pricing behaviour

When `AiPricingOptions.Get*Pricing()` returns `null` (model not found in config), the provider sets `costUsd = 0m` and logs the call with zero cost. There is no warning, alert, or admin notification. Zero-cost logs are silently mixed into the usage report, understating actual spend.

---

## 8. Data model options — recommendation

### Option A: Config-only (current state)
Keep appsettings. Add no persistence. Admin must deploy to change prices.
- Pros: zero migration risk, no complexity.
- Cons: prices are invisible to admin, stale as providers change rates, zero-cost gap undetected.

### Option B: Database-backed pricing table
New `AiModelPricing` table. Replaces config reads entirely.
- Pros: admin-editable, auditable, effective-date support possible.
- Cons: migration required, all provider call sites change, test changes required.

### Option C: Hybrid — config seed + database override (recommended)
Keep appsettings as a fallback seed. Add a `AiModelPricingOverride` table. At call time, check DB first; fall back to config if not found.
- Pros: safe migration path, no risk to existing cost logs, admin can override individual models, config still works as default.
- Cons: two sources of truth (managed by clear resolution order).

---

## 9. Recommended enterprise design

New table: `AiModelPricingOverride`

| Column | Type | Notes |
|--------|------|-------|
| `Id` | Guid | PK |
| `ProviderName` | string | e.g. `openai`, `gemini`, `anthropic` |
| `ModelName` | string | e.g. `gpt-4o` |
| `InputPer1KTokens` | decimal | USD |
| `OutputPer1KTokens` | decimal | USD |
| `Currency` | string | `USD` for now; reserved for AUD |
| `EffectiveFrom` | DateTime | UTC, for audit trail |
| `IsActive` | bool | soft disable without delete |
| `UpdatedByAdminId` | Guid? | FK to admin user |
| `UpdatedAt` | DateTime | UTC |

Resolution order at call time:
1. Active `AiModelPricingOverride` row for (provider, model) → use DB price
2. `appsettings.json` config entry → use config price
3. Neither found → cost = 0, log warning

Admin UI additions (read-only first, then editable):
- Pricing table in AI Config page: list all models with effective price source (DB or config), input/output per 1K tokens, last updated.
- Edit row inline: input/output fields, save button, effective date auto-set to now.
- Add override: new row for provider + model not yet in DB.
- Disable override: sets `IsActive = false`, falls back to config.
- Show last updated + admin who changed it.
- Validation: both input and output required, must be ≥ 0.

Fallback alert (future): surface a warning in AI Usage when models have zero cost (likely missing price config).

---

## 10. Migration impact

- New migration required for `AiModelPricingOverride` table.
- No existing columns change.
- No historical cost recalculation: existing `AiUsageLog.CostUsd` rows were logged at call time using config values. They are already final. DB pricing only affects future calls.
- `AiPricingOptions` static class would need a new overload or replacement interface that accepts a repository lookup first, falling back to `IConfiguration`.
- Unit tests referencing `AiPricingOptions.Get*Pricing` would need updating if the static method is replaced; they can be kept if the static method is wrapped rather than removed.

---

## 11. Admin UI impact

Minimal scope for 10V-1:

1. Read-only pricing panel in AI Config page — list all configured models and their effective price from config.
2. No DB writes, no migration. Pure read from existing `appsettings.json` via a new `/api/admin/ai/pricing` endpoint.

This gives admins visibility without requiring DB work and validates the UI shape before the full edit flow.

10V-2 then adds the `AiModelPricingOverride` table, the API for CRUD, and the edit form.

---

## 12. Risk areas

| Risk | Severity | Notes |
|------|----------|-------|
| Zero-cost logs (missing model in config) | Medium | Silent gap; understates spend. No current alerting. |
| USD vs AUD mixing | Low now | All pricing is USD. Reserve `Currency` column for future. |
| Hardcoded provider name strings | Low | `"OpenAI"`, `"Gemini"`, `"Anthropic"` appear as string literals in `AiPricingOptions`. Consistent but not enum-guarded. |
| Tests tied to config structure | Low | Eight unit tests inject pricing via `IConfiguration`. Would break if `AiPricingOptions` is replaced entirely rather than extended. Safe if wrapped. |
| Historical cost recalculation | High risk to avoid | Do not recalculate existing `AiUsageLog.CostUsd` rows when prices change. They represent what was in config at call time. |

---

## 13. Decisions made

1. Pricing is config-seeded, not admin-editable. Confirmed gap.
2. Recommended design: hybrid (config seed + DB override). Config remains fallback.
3. 10V-1 should be read-only pricing visibility in the admin UI. No migration. No DB writes.
4. 10V-2 should be the `AiModelPricingOverride` table + CRUD + edit UI.
5. `TODO-10U-GAP-6` (split `CostUsd` into `InputCostUsd` + `OutputCostUsd`) remains deferred; do not couple it to 10V-1 or 10V-2.

---

## 14. Implementation tasks produced

| Phase | Task |
|-------|------|
| 10V-1 | New `GET /api/admin/ai/pricing` endpoint — returns all config-defined prices per provider/model. Angular pricing panel in AI Config page (read-only). No migration. |
| 10V-2 | Migration: `AiModelPricingOverride` table. CRUD API. Edit panel in AI Config. `AiPricingOptions` extended to check DB first. |
| 10V-3 | Zero-cost log alert in AI Usage summary when active models have no pricing configured. |

---

## 15. Gate result

- No code changed.
- `git diff --check`: clean.
- Audit only.

---

## 16. Final verdict

Pricing admin is a confirmed gap. The smallest safe next step is 10V-1: a read-only price listing panel fed from existing config, no migration required, no risk to existing cost logs. This gives admins immediate visibility and establishes the UI surface for 10V-2's editable DB-backed pricing.

**Recommended next phase:** 10V-1 — read-only pricing visibility (backend endpoint + Angular panel).
