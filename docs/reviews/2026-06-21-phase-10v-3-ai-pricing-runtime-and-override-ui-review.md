# Phase 10V-3 — AI Pricing Runtime Resolver Wiring + Override Management UI

**Date:** 2026-06-21
**Sprint:** Phase 10V
**Type:** Implementation review

---

## Summary

Phase 10V-3 completed three parts:

- **Part A:** Wired `IAiPricingResolver` into all three AI providers (OpenAI, Gemini, Anthropic), replacing direct `AiPricingOptions` static config calls with resolver-based runtime cost calculation.
- **Part B:** Zero-cost visibility deferred — no new endpoint fields added. Existing null-cost logging already covers this; a TODO remains for a future usage alert UI.
- **Part C:** Added override management UI to the AI Config pricing panel — list, create, edit, and deactivate controls.

---

## Files changed

### Backend

**Infrastructure — AI providers**
- `src/LinguaCoach.Infrastructure/Ai/OpenAiProvider.cs` — added `IAiPricingResolver` constructor parameter; replaced `AiPricingOptions.GetOpenAiPricing` + `EstimateCostUsd` with `_pricingResolver.ResolveAsync` + inline arithmetic
- `src/LinguaCoach.Infrastructure/Ai/GeminiProvider.cs` — same pattern; replaced `AiPricingOptions.GetGeminiPricing`
- `src/LinguaCoach.Infrastructure/Ai/AnthropicProvider.cs` — same pattern; replaced `AiPricingOptions.GetProviderPricing(_configuration, "Anthropic", ...)`

No DI registration changes needed — `IAiPricingResolver` was already registered as scoped in 10V-2; constructor injection resolves it automatically for all three providers.

**No migration added.** No new domain entities. No new API endpoints.

### Tests — backend

- `tests/LinguaCoach.UnitTests/Ai/OpenAiProviderTests.cs` — added `NullPricingResolver` file-scoped stub; passed to `OpenAiProvider` constructor in the one test that instantiates it directly
- `tests/LinguaCoach.UnitTests/Ai/AiProviderResolverTests.cs` — added `NullPricingResolverForResolver` file-scoped stub; registered as `IAiPricingResolver` singleton in the `BuildResolver` DI service collection

### Frontend

**Models**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — added `AiModelPricingOverrideItem`, `CreatePricingOverrideRequest`, `UpdatePricingOverrideRequest` interfaces

**Service**
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — added `listAiPricingOverrides()`, `createAiPricingOverride()`, `updateAiPricingOverride()`, `deactivateAiPricingOverride()`

**Component**
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`:
  - Added `OverrideFormState` interface
  - Added signals: `overrides`, `overrideForm`, `deactivateBusy`
  - Added `listAiPricingOverrides` to `forkJoin` in `ngOnInit`
  - Added methods: `openCreateOverride`, `openEditOverride`, `cancelOverrideForm`, `saveOverride`, `deactivateOverride`, `toDatetimeLocal`, `fromDatetimeLocal`
  - Section 4 template extended with: config pricing (read-only, unchanged), DB overrides table with Edit/Deactivate per active row, inline create/edit form panel

**Tests — frontend**
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.spec.ts`:
  - Added `listAiPricingOverrides` spy to `makeAdminApi` factory
  - Added `AiModelPricingOverrideItem` import
  - Added 11 new override panel tests
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` — added `listAiPricingOverrides` spy to both AI Config mock sites (lines 160 and 392)

---

## Runtime cost calculation pattern (all three providers)

**Before (static config):**
```csharp
var pricing = AiPricingOptions.GetOpenAiPricing(_configuration, modelToUse);
var cost = pricing is null ? 0m : AiPricingOptions.EstimateCostUsd(inputTokens, outputTokens, pricing);
```

**After (resolver):**
```csharp
var resolved = await _pricingResolver.ResolveAsync(ProviderName, modelToUse, ct);
var cost = resolved is null
    ? 0m
    : (inputTokens / 1000m) * resolved.InputPer1KTokens + (outputTokens / 1000m) * resolved.OutputPer1KTokens;
```

Resolution order (unchanged from 10V-2 `AiPricingResolver`):
1. Active DB override: `IsActive=true`, `EffectiveFromUtc <= now`, `EffectiveToUtc null or > now`
2. `appsettings.json` config via `AiPricingOptions.GetProviderPricing`
3. `null` — cost logged as 0m (same behavior as before)

---

## Provider routing and governance

Not changed. `AiProviderResolver`, `AiUsageLogger`, token counting, and fallback logic are all untouched.

---

## Historical AiUsageLog recalculation

Not performed. Existing cost log rows are unchanged.

---

## Part B — Zero-cost visibility

Deferred. No new DTO fields added to `GET /api/admin/ai/pricing`. The existing null-cost log messages (`pricing is not configured for model {Model}`) are sufficient until a dedicated alert panel is scoped.

**Remaining TODO:** `TODO-10V-3B`: Add a zero-cost alert in AI Usage summary or AI Config panel when active models have no pricing configured.

---

## Gate results

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | 0 errors |
| `dotnet test --configuration Release` | 1260 unit + 810 integration + 3 arch = 2073 passed, 0 failed |
| `npm run build -- --configuration production` | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 891/891 passed (11 new) |

---

## Risks and unresolved questions

- Zero-cost alert UI not added (Part B deferred).
- No uniqueness constraint on (provider, model, effectiveFrom) — deferred from 10V-2 as `TODO-10V-UNIQUE-CONSTRAINT`.
- Override form uses `datetime-local` input with manual UTC conversion via `new Date().toISOString()`. Browser timezone handling is implicit; a future improvement could show explicit UTC labels.

---

## Next recommended action

Mark Phase 10V complete. Next phase scope TBD — candidates include: zero-cost alert UI (10V-3B), uniqueness constraint, or Phase 10W.
