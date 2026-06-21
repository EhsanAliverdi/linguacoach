# Phase 10V-1 — Read-Only AI Pricing Panel

**Date:** 2026-06-21
**Sprint:** Phase 10V
**Type:** Implementation review

---

## Files changed

**Backend**
- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — added `AiModelPricingItem` DTO and `ListPricing()` to `IAdminAiConfigHandler`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs` — added `IConfiguration` injection, implemented `ListPricing()`
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — added `GET /api/admin/ai/pricing` endpoint
- `tests/LinguaCoach.IntegrationTests/Api/AiConfigEndpointTests.cs` — 4 new pricing endpoint integration tests

**Frontend**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — added `AiModelPricingItem` interface
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — added `listAiPricing()` method
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts` — added `pricing` signal, `pricingByProvider()` helper, Section 4 pricing panel, `SpAdminEmptyStateComponent` import
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.spec.ts` — added `listAiPricing` to mock, `PRICING_ROWS` fixture, 8 new pricing tests
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` — fixed 2 existing AI Config mocks to include `listAiPricing`

**Docs (from 10V-0, included in this commit)**
- `docs/reviews/2026-06-21-phase-10v-0-ai-pricing-admin-gap-check.md`
- `docs/sprints/current-sprint.md`
- `TODOS.md`

---

## Pricing endpoint

- Route: `GET /api/admin/ai/pricing`
- Admin-protected (inherits controller `[Authorize(Roles = "Admin")]`)
- Returns `IReadOnlyList<AiModelPricingItem>` serialized as JSON array
- Source: reads `{Provider}:Pricing:{Model}:InputPer1KTokens` and `OutputPer1KTokens` from `IConfiguration`
- Providers covered: OpenAI, Gemini, Anthropic, Qwen
- Sorted by provider name then model name
- Fields: `providerName`, `modelName`, `inputPer1KTokens`, `outputPer1KTokens`, `currency` (USD), `source` (Configuration), `isConfigured` (bool)

---

## Pricing source used

`IConfiguration` via `AiPricingOptions`-compatible key path — same config sections used by cost calculation. No DB reads. No new tables.

---

## Providers/models returned

All models configured under `OpenAI:Pricing`, `Gemini:Pricing`, `Anthropic:Pricing`, `Qwen:Pricing` in `appsettings.json`. Currently 12 rows across 3 providers (OpenAI: 5, Gemini: 4, Anthropic: 3).

---

## Missing pricing detection

Not implemented in this phase. `isConfigured` field is present and set correctly for rows that parse successfully. Active-model-vs-pricing comparison deferred to 10V-3.

---

## Frontend read-only pricing panel

Added as Section 4 in the existing `AdminAiConfigComponent`:
- Grouped by provider
- Table: model, input price, output price, currency, configured badge
- Info alert: "Pricing is read-only and loaded from configuration. Admin overrides will be added in a later phase."
- Empty state rendered when no pricing rows
- No edit controls, no save/update forms

---

## Edit controls added

No.

---

## Backend tests

4 new integration tests in `AiConfigEndpointTests`:
1. `ListAiPricing_Unauthenticated_Returns401`
2. `ListAiPricing_AsStudent_Returns403`
3. `ListAiPricing_AsAdmin_ReturnsConfiguredRows` — checks OpenAI, Gemini, Anthropic present
4. `ListAiPricing_AsAdmin_RowsHaveRequiredFields` — validates all fields and USD/Configuration values
5. `ListAiPricing_AsAdmin_OpenAiGpt4oPriceMatchesConfig` — spot-checks gpt-4o prices against appsettings values

---

## Frontend tests

8 new unit tests in `admin-ai-config.component.spec.ts`:
1. `calls listAiPricing on init`
2. `populates pricing signal after load`
3. `pricingByProvider groups rows by provider`
4. `renders pricing section heading`
5. `renders read-only configuration note`
6. `renders model names in pricing table`
7. `renders empty state when no pricing rows`
8. `does not render edit buttons in pricing section`

2 existing mocks in `admin-wrapper-migration.spec.ts` updated to include `listAiPricing`.

---

## Gate results

| Gate | Result |
|------|--------|
| `git diff --check` | Clean |
| `dotnet build --configuration Release` | 0 errors, 7 pre-existing warnings |
| `dotnet test --configuration Release` | 1248 unit + 795 integration + 3 arch = 2046 passed, 0 failed |
| `npm run build -- --configuration production` | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 880/880 passed |

---

## Migration added

No.

---

## Pricing calculation behavior changed

No. `AiPricingOptions.EstimateCostUsd` is unchanged. Cost logging in providers is unchanged.

---

## Provider routing behavior changed

No.

---

## Usage governance behavior changed

No.

---

## Unrelated admin UI refactor

No.

---

## 10V-0 docs included in commit

Yes. `docs/reviews/2026-06-21-phase-10v-0-ai-pricing-admin-gap-check.md`, updated `docs/sprints/current-sprint.md`, and updated `TODOS.md` are all included.

---

## Risks and unresolved questions

- Missing-model detection (active category model has no pricing row) deferred to 10V-3.
- `isConfigured` is always `true` for rows returned (only configured rows are returned). A future endpoint variant could also emit unconfigured active models. Deferred.
- Qwen pricing: no Qwen pricing rows in appsettings yet. Endpoint returns empty for Qwen. No error.

---

## Next recommended action

10V-2: `AiModelPricingOverride` DB table, migration, CRUD API, edit panel in AI Config. See `docs/reviews/2026-06-21-phase-10v-0-ai-pricing-admin-gap-check.md` for recommended schema.
