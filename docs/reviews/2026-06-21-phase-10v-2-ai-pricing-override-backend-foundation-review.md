# Phase 10V-2 — AI Pricing Override Backend Foundation

**Date:** 2026-06-21
**Sprint:** Phase 10V
**Type:** Implementation review

---

## Files changed

**Domain**
- `src/LinguaCoach.Domain/Entities/AiModelPricingOverride.cs` — new entity (provider, model, prices, currency, isActive, effectiveFromUtc, effectiveToUtc, notes, audit fields)

**Persistence**
- `src/LinguaCoach.Persistence/Configurations/AiModelPricingOverrideConfiguration.cs` — EF config, table `ai_model_pricing_overrides`, indexes on (provider, model) and (isActive, effectiveFrom)
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — added `DbSet<AiModelPricingOverride>`
- `src/LinguaCoach.Persistence/Migrations/` — new migration `T53_AiModelPricingOverrides`

**Application**
- `src/LinguaCoach.Application/Ai/IAiPricingResolver.cs` — new interface + `ResolvedModelPricing` record
- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — added `AiModelPricingOverrideItem` DTO, `CreatePricingOverrideCommand`, `UpdatePricingOverrideCommand`, `DeactivatePricingOverrideCommand`; extended `IAdminAiConfigHandler` with 4 new methods

**Infrastructure**
- `src/LinguaCoach.Infrastructure/Ai/AiPricingResolver.cs` — implements `IAiPricingResolver`: DB override first, config fallback second, null third
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs` — implements `ListPricingOverridesAsync`, `CreatePricingOverrideAsync`, `UpdatePricingOverrideAsync`, `DeactivatePricingOverrideAsync`; adds `IConfiguration` injection (already added in 10V-1)
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `IAiPricingResolver → AiPricingResolver`

**API**
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — added endpoints: `GET /api/admin/ai/pricing/overrides`, `POST /api/admin/ai/pricing/overrides`, `PUT /api/admin/ai/pricing/overrides/{id}`, `DELETE /api/admin/ai/pricing/overrides/{id}`; added `CreatePricingOverrideRequest`, `UpdatePricingOverrideRequest` records

**Tests**
- `tests/LinguaCoach.IntegrationTests/Api/AiPricingOverrideEndpointTests.cs` — 13 integration tests
- `tests/LinguaCoach.UnitTests/Domain/AiModelPricingOverrideTests.cs` — 11 domain unit tests

---

## Migration added

Yes. `T53_AiModelPricingOverrides` — table `ai_model_pricing_overrides` with all entity fields plus indexes.

---

## New entity/table

`AiModelPricingOverride` / `ai_model_pricing_overrides`.

Fields: `id`, `provider_name`, `model_name`, `input_price_per_1k_tokens` (numeric 12,8), `output_price_per_1k_tokens` (numeric 12,8), `currency`, `is_active`, `effective_from_utc`, `effective_to_utc`, `notes`, `created_at`, `updated_at_utc`, `created_by_admin_user_id`, `updated_by_admin_user_id`.

---

## Pricing override endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/admin/ai/pricing/overrides` | List all overrides (all states) |
| POST | `/api/admin/ai/pricing/overrides` | Create override |
| PUT | `/api/admin/ai/pricing/overrides/{id}` | Update prices/dates/notes |
| DELETE | `/api/admin/ai/pricing/overrides/{id}` | Soft-deactivate (sets IsActive=false) |

All endpoints are admin-protected. Audit log entries written on create, update, deactivate.

---

## Pricing resolver added

Yes. `IAiPricingResolver` / `AiPricingResolver`. Resolution order:
1. Active DB override: `IsActive=true`, `EffectiveFromUtc <= now`, `EffectiveToUtc null or > now`; most recent wins
2. `appsettings.json` config via `AiPricingOptions.GetProviderPricing`
3. `null` — caller logs cost as 0m (unchanged behavior)

---

## Provider runtime cost calculation changed

No. The three providers (`OpenAiProvider`, `GeminiProvider`, `AnthropicProvider`) still call `AiPricingOptions` directly. The resolver is available via DI and tested, but provider wiring is deferred to 10V-3 to keep this phase focused and safe.

**Rationale:** Changing provider cost calculation requires touching three provider files and their tests simultaneously. Keeping it separate means existing cost logging behavior is unaffected and the resolver can be validated independently first.

---

## Config fallback preserved

Yes. `AiPricingResolver` falls through to `AiPricingOptions` config lookup when no active DB override exists. Config remains authoritative for all existing calls until 10V-3 wires providers.

---

## Historical AiUsageLog recalculation

No. Existing cost log rows are not modified.

---

## Audit logs added

Yes. `AdminAuditLog` entries written for:
- `CreatePricingOverride` — with new value JSON
- `UpdatePricingOverride` — with old and new value JSON
- `DeactivatePricingOverride` — entity reference only

---

## Validation

- Provider required (blank → 400)
- Model required (blank → 400)
- Input/output prices must be >= 0 (negative → 400)
- Currency required, normalized to uppercase
- `EffectiveToUtc` must be null or strictly after `EffectiveFromUtc` (400 if violated)
- Delete of non-existent ID → 404

---

## Backend tests

**Integration tests (13 new):**
- Auth guards: list unauthenticated (401), list as student (403), create unauthenticated (401)
- Create: valid payload returns 200 with all fields; negative price returns 400; effectiveTo before from returns 400
- List: returns created override
- Update: changes prices; non-existent ID returns 404
- Deactivate: returns 204, IsActive=false in list; non-existent ID returns 404
- Resolver: returns DB override when active; falls back to config for gpt-4o; returns null for unknown model; ignores deactivated override

**Unit tests (11 new):**
- Constructor: sets all fields, normalizes provider to lowercase, normalizes currency to uppercase
- Constructor throws: blank provider, blank model, negative input, negative output, effectiveTo before from
- Constructor: zero prices valid
- Update: changes prices, sets UpdatedAtUtc, notes; throws on negative input
- Deactivate: sets IsActive=false, sets UpdatedAtUtc

---

## Frontend changes

No edit UI added in this phase. No Angular components changed. No new TypeScript models added (override DTO is backend-only for now). Frontend build and tests unchanged (880/880).

---

## Remaining AI pricing TODOs

- `TODO-10V-3`: Wire `IAiPricingResolver` into `OpenAiProvider`, `GeminiProvider`, `AnthropicProvider` to replace direct `AiPricingOptions` calls at call time.
- `TODO-10V-3`: Zero-cost alert in AI Usage when active models have no pricing.
- Frontend override management UI (list, create, edit, deactivate) — not yet scoped.

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | Clean (CRLF warning on generated migration snapshot only) |
| `dotnet build --configuration Release` | 0 errors |
| `dotnet test --configuration Release` | 1260 unit + 810 integration + 3 arch = 2073 passed, 0 failed |
| `npm run build -- --configuration production` | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 880/880 passed |

---

## Risks and unresolved questions

- Provider runtime wiring deferred: new calls still use config pricing until 10V-3. This is intentional and documented.
- No uniqueness constraint on (provider, model, effectiveFrom) — two overlapping active overrides resolve to the most recent `EffectiveFromUtc`. A DB unique index could prevent this; deferred as `TODO-10V-UNIQUE-CONSTRAINT`.
- Frontend override management UI not yet added. Overrides created via API only (or future UI in 10V-3/10V-4).

---

## Next recommended action

10V-3: Wire `IAiPricingResolver` into the three AI providers, replacing direct `AiPricingOptions` calls. Also add the zero-cost log alert in AI Usage summary.
