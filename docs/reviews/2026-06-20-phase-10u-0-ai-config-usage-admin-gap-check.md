# Phase 10U-0 — AI Config and AI Usage Admin Gap Check

**Date:** 2026-06-20
**Sprint/Phase:** 10U-0
**Type:** Gap Check / Investigation
**Author:** Claude Code (audit)
**Status:** Complete — no code changed

---

## Files Inspected

### Backend

**Domain**
- `src/LinguaCoach.Domain/Entities/AiUsageLog.cs`
- `src/LinguaCoach.Domain/Entities/AiProviderConfig.cs`
- `src/LinguaCoach.Domain/Entities/AiConfigCategory.cs`
- `src/LinguaCoach.Domain/Entities/AiProviderCredential.cs`

**Infrastructure**
- `src/LinguaCoach.Infrastructure/Ai/AiPricingOptions.cs`
- `src/LinguaCoach.Infrastructure/Ai/OpenAiProvider.cs`
- `src/LinguaCoach.Infrastructure/Ai/AnthropicProvider.cs`
- `src/LinguaCoach.Infrastructure/Ai/GeminiProvider.cs`
- `src/LinguaCoach.Infrastructure/Ai/QwenProvider.cs`
- `src/LinguaCoach.Infrastructure/Ai/AiProviderResolver.cs`
- `src/LinguaCoach.Infrastructure/Ai/AiProviderTester.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs`

**Application**
- `src/LinguaCoach.Application/Ai/IAiProvider.cs`
- `src/LinguaCoach.Application/Ai/IAiProviderResolver.cs`
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs`

**Persistence**
- `src/LinguaCoach.Persistence/Configurations/AiUsageLogConfiguration.cs`
- `src/LinguaCoach.Persistence/Configurations/AiProviderConfigConfiguration.cs`
- `src/LinguaCoach.Persistence/Configurations/AiConfigCategoryConfiguration.cs`
- `src/LinguaCoach.Persistence/Configurations/AiProviderCredentialConfiguration.cs`

**API**
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs`
- `src/LinguaCoach.Api/Controllers/AdminController.cs`
- `src/LinguaCoach.Api/appsettings.json`
- `src/LinguaCoach.Api/appsettings.Development.json`

### Frontend

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.spec.ts`
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`

---

## 1. Current Backend Capability Matrix

| Capability | Status | Notes |
|---|---|---|
| Provider config (entity) | ✅ Complete | `AiProviderConfig`, `AiProviderCredential`, `AiConfigCategory` entities in DB |
| Provider config (endpoint) | ✅ Complete | `GET /api/admin/ai-providers`, `PUT /api/admin/ai-providers/{provider}/api-key` |
| Model config | ⚠️ Partial | Allowlist hardcoded in `AiProviderConfig.cs:65-94`; admins can add custom models via POST but cannot remove from hardcoded list |
| Feature-to-model routing | ✅ Complete | `AiConfigCategory` + `AiProviderConfig` + `AiProviderResolver.ResolveLlm()` / `ResolveTts()` |
| Fallback config (entity) | ✅ Entity exists | `AiProviderConfig.FallbackProviderName`, `FallbackModelName`, `FallbackEnabled` stored in DB |
| Fallback config (in use) | ❌ Not wired | Resolver does not use fallback fields; behavior is dead |
| Pricing config | ❌ Missing data | `AiPricingOptions` reads `OpenAI:Pricing:gpt-4o:InputPer1KTokens` from appsettings — **these keys do not exist in any appsettings file**; all costs compute as $0 |
| Token tracking — input | ✅ Complete | `AiUsageLog.InputTokens` (int, non-nullable), recorded per call |
| Token tracking — output | ✅ Complete | `AiUsageLog.OutputTokens` (int, non-nullable), recorded per call |
| Cost tracking | ⚠️ Broken | `AiUsageLog.CostUsd` stored but always $0 due to missing pricing data |
| Period filtering | ❌ Missing | No date-range params on any usage endpoint |
| Pagination (limit) | ⚠️ Partial | `GET /api/admin/ai-usage/recent?limit=100`, clamped to [1, 500] |
| Pagination (offset/page) | ❌ Missing | No `offset`, `page`, or `pageSize` param |
| Provider filtering (backend) | ❌ Missing | All filtering is client-side |
| Model filtering (backend) | ❌ Missing | All filtering is client-side |
| Feature filtering (backend) | ❌ Missing | All filtering is client-side |
| Student filtering | ❌ Missing | `StudentProfileId` stored but not exposed as filter |
| Export / reporting | ❌ Missing | No CSV, JSON, or report endpoint |
| Usage summary endpoint | ✅ Complete | `GET /api/admin/ai-usage/summary` — totalCalls, cost, byProvider, byFeature |
| Recent usage endpoint | ✅ Complete | `GET /api/admin/ai-usage/recent` — all fields returned |

---

## 2. Current Frontend Capability Matrix

### /admin/ai-usage

| Capability | Status | Notes |
|---|---|---|
| Summary stat cards | ✅ Complete | totalCalls, successfulCalls, failedCalls, fallbackCalls, totalCostUsd, successRate |
| By-provider summary | ✅ Complete | Calls, successful, fallback, cost per provider |
| By-feature summary | ✅ Complete | Calls, successful, cost per feature |
| Recent calls table | ✅ Complete | Columns: timestamp, studentId, featureKey, provider, model, status, tokens, cost, duration, correlationId |
| Provider filter | ✅ Complete | Client-side dropdown, auto-populated from data |
| Status filter | ✅ Complete | OK / Failed / Fallback |
| Client-side pagination | ✅ Complete | 25 items/page |
| Loading state | ✅ Complete | Independent summary + recent spinners |
| Empty state | ✅ Complete | Rendered when no calls |
| Error state | ✅ Complete | Per-section error messages |
| Date-range filter | ❌ Missing | No date picker; no API param to back it |
| Student filter | ❌ Missing | No student lookup or filter |
| Export | ❌ Missing | No download button |
| Cost trend chart | ❌ Missing | No time-series visualization |
| Cost alerts | ❌ Missing | No high-cost-call highlighting |

### /admin/ai-config

| Capability | Status | Notes |
|---|---|---|
| LLM category grid | ✅ Complete | Per category: provider dropdown, model dropdown, Save + Test |
| TTS category grid | ✅ Complete | Per category: provider, model, voice input, Save + Test |
| Provider catalog list | ✅ Complete | Badges: key stored, env var, endpoint set |
| Model test chips | ✅ Complete | Color-coded by test state; shows latency on hover |
| API key management | ✅ Complete | Set / Update / Clear per provider |
| Qwen endpoint config | ✅ Complete | Endpoint + key dual form |
| Provider test (all models) | ✅ Complete | Updates test chips |
| Single model test | ✅ Complete | Updates one chip |
| Add custom model | ✅ Complete | Per provider; validates non-blank |
| Category save | ✅ Complete | PATCH with 2s toast confirmation |
| Category test | ✅ Complete | LLM and TTS; returns ok/latencyMs/error |
| Voice input (TTS) | ✅ Complete | Optional text field (e.g., "onyx") |
| Fallback config UI | ❌ Missing | No UI for `FallbackProviderName` / `FallbackModelName` / `FallbackEnabled` |
| Pricing display | ❌ Missing | No per-model pricing visible |
| Cost calculator | ❌ Missing | No "estimate cost for N tokens" tool |

---

## 3. Current Data Model Summary

### AiUsageLog
Append-only log of every AI provider call.

```
Id                  Guid
StudentProfileId    Guid? (nullable — system calls have no student)
FeatureKey          string (e.g., "lesson_generation")
ProviderName        string (e.g., "openai")
ModelName           string (e.g., "gpt-4o-mini")
IsFallback          bool
WasSuccessful       bool
FailureReason       string?
InputTokens         int
OutputTokens        int
CostUsd             decimal
DurationMs          int
CorrelationId       Guid
CreatedAt           DateTime (UTC)
```

### AiProviderConfig
Per-feature provider/model selection with optional fallback.

```
Id                  Guid
FeatureKey          string
ProviderName        string
ModelName           string
VoiceName           string?
FallbackProviderName string?
FallbackModelName   string?
FallbackEnabled     bool
UpdatedAt           DateTime
```
Includes hardcoded static allowlist `KnownModelsByProvider` (dictionary, ~30 lines).

### AiConfigCategory
High-level category routing (e.g., "llm.default", "tts.listening").

```
Id          Guid
CategoryKey string
DisplayName string
ProviderName string
ModelName   string
VoiceName   string?
UpdatedAt   DateTime
IsConfigured (computed: provider/model are not "fake")
```

### AiProviderCredential
API keys, endpoints, and test results per provider.

```
Id              Guid
ProviderName    string
ApiKey          string? (encrypted at rest)
ApiEndpoint     string? (Qwen workspace URL)
ModelTests      Dictionary<string, ModelTestResult>
UpdatedAt       DateTime
```
`ModelTestResult` record: `(bool Ok, int LatencyMs, string? Error, DateTime TestedAt)`

---

## 4. Current API Endpoint Summary

### Usage

```
GET  /api/admin/ai-usage/summary
     → { totalCalls, successfulCalls, failedCalls, fallbackCalls, totalCostUsd, successRate,
         byProvider[{ provider, calls, successful, fallback, costUsd }],
         byFeature[{ feature, calls, successful, costUsd }] }

GET  /api/admin/ai-usage/recent?limit=100
     → { total, items[{ id, createdAt, studentProfileId, featureKey, provider, model,
                        isFallback, wasSuccessful, failureReason, inputTokens, outputTokens,
                        costUsd, durationMs, correlationId }] }
```

### Configuration

```
GET  /api/admin/ai-providers
     → AiProviderCatalogItem[]

PUT  /api/admin/ai-providers/{provider}/api-key        body: { apiKey }
PUT  /api/admin/ai-providers/{provider}/endpoint       body: { apiEndpoint }
POST /api/admin/ai-providers/{provider}/test
POST /api/admin/ai-providers/{provider}/models         body: { modelName }
POST /api/admin/ai-providers/{provider}/models/test    body: { modelName }

GET   /api/admin/ai/categories
PATCH /api/admin/ai/categories/{categoryKey}           body: { providerName?, modelName?, voiceName? }
POST  /api/admin/ai/categories/{categoryKey}/test
      → { categoryKey, providerName, modelName, voiceName, ok, latencyMs, error }
```

---

## 5. Missing API/Data Pieces

1. **Date-range filtering on usage** — no `startDate`/`endDate` params on summary or recent endpoints.
2. **True server-side pagination** — `recent` accepts `limit` only; no `offset`/`page`/`pageSize`.
3. **Student-specific usage filter** — `StudentProfileId` stored but not filterable via API.
4. **Backend provider/model/feature filters** — all filtering is client-side; breaks at scale.
5. **Usage export endpoint** — no `GET /api/admin/ai-usage/export?format=csv`.
6. **Per-student cost breakdown** — no aggregate "student X spent $Y" endpoint.
7. **Correlation ID search** — can't query all log rows for a given correlationId.
8. **Pricing lookup endpoint** — frontend has no way to query what pricing is configured; must assume or hardcode.
9. **Fallback config endpoints** — no PATCH to set `FallbackProviderName` / `FallbackEnabled` per feature, even though entity supports it.
10. **Period-bucketed summary** — no "daily/weekly/monthly" summary; only all-time aggregates.

---

## 6. Missing UI Pieces

1. **Date-range picker on /admin/usage** — cannot filter by hour/day/week/month.
2. **Student lookup on /admin/usage** — no way to view usage for a specific student.
3. **Advanced call filters** — no failure-reason search, duration-range filter, or cost-threshold filter.
4. **Export button** — no CSV/JSON download for usage data.
5. **Pricing display on /admin/ai-config** — no "$/1K tokens" display per model.
6. **Fallback configuration UI** — `FallbackProviderName` / `FallbackModelName` / `FallbackEnabled` fields exist in entity but are not exposed in UI.
7. **Cost trend chart** — no time-series graph.
8. **High-cost call alert** — no visual flag for calls exceeding a threshold (e.g., > $0.05).
9. **Fallback rate warning** — no indicator when fallback rate exceeds acceptable threshold.
10. **Token totals on summary cards** — stat cards show cost but not total input/output tokens.

---

## 7. Hardcoded Logic That Should Become Configurable

| File | Location | Hardcoded Value | Should Become |
|---|---|---|---|
| `AiProviderConfig.cs` | L65–94 | Model allowlist per provider | DB table or admin-managed list |
| `admin-ai-config.component.ts` | L383–387 | TTS model detection patterns | Provider metadata from API |
| `AiPricingOptions.cs` | L15–27 | Pricing lookup path in appsettings | Admin-configurable pricing table in DB |
| `OpenAiProvider.cs` | L30 | Default model `gpt-4o-mini` | Category default fallback from DB |
| `AnthropicProvider.cs` | L11 | Default model `claude-sonnet-4-6` | Category default fallback from DB |
| `GeminiProvider.cs` | L12 | Default model `gemini-2.0-flash` | Category default fallback from DB |
| `AiProviderTester.cs` | L10–12 | Test prompt string | Admin-editable test prompt |
| `admin-ai-config.component.ts` | L49–56 | Category display descriptions | Admin API endpoint |
| `admin-ai-usage.component.ts` | L71 | Page size = 25 | User preference or admin setting |

---

## 8. Cost/Pricing Risks

### Critical: All Costs Are $0

`AiPricingOptions` reads pricing from appsettings keys of the form:

```
OpenAI:Pricing:gpt-4o:InputPer1KTokens
OpenAI:Pricing:gpt-4o:OutputPer1KTokens
```

**Neither `appsettings.json` nor `appsettings.Development.json` contains these keys.** The estimation method (`EstimateCostUsd()`) receives zero rates and returns `0`. Every `AiUsageLog.CostUsd` is $0 in any environment without manual secrets injection.

Admins currently have no visibility into real AI spend.

### Secondary Risks

- **Pricing changes require deploy.** Provider price updates cannot be made from the admin UI. Rate hikes go unnoticed until a developer redeploys.
- **No cost alerts.** No alerting when daily/monthly spend exceeds a threshold.
- **No model cost comparison.** Admins cannot see that `gpt-4o` costs significantly more than `gpt-4o-mini` inside the UI.
- **Fallback cost impact invisible.** `IsFallback` flag is logged but there is no UI to calculate the cost difference between primary and fallback providers.
- **No cost breakdown by student.** Cannot determine whether specific students are disproportionately expensive.

---

## 9. Token Tracking Risks

- **No max-token budget enforcement.** Features can request unlimited output tokens. No per-feature or per-student quota exists.
- **Timed-out calls lose token records.** Tokens are logged after API response. If a call times out or is aborted, no token row is written; that spend is invisible.
- **No token aggregation by period.** Cannot answer "how many tokens did we consume last week?"
- **No per-student token quota.** `StudentProfileId` is logged but never compared to a budget.
- **Summary cards show only cost, not tokens.** Admins see cost (currently $0) but never see raw input/output token counts in the summary view.

---

## 10. Recommended Implementation Phases

Ordered small to large, each independently shippable.

### Phase 10U-1 — Fix Pricing Data (Backend only, no migration)
**Effort: XS**

Add real pricing values to `appsettings.json` (and secrets for production) for all currently-configured provider/model combinations. No code change required — `AiPricingOptions` already reads these keys. Immediately unblocks cost visibility. This is the highest-impact fix per line of change.

### Phase 10U-2 — Token Totals on Usage Summary Cards (Frontend only)
**Effort: XS**

Extend `GET /api/admin/ai-usage/summary` response to include `totalInputTokens` and `totalOutputTokens`. Add two stat cards to the Angular usage page. Zero schema migration required.

### Phase 10U-3 — Date-Range Filtering on Usage (Backend + Frontend)
**Effort: S**

Add optional `startDate` and `endDate` query params to `GET /api/admin/ai-usage/recent` and `GET /api/admin/ai-usage/summary`. Add a date-range picker to the Angular usage page. Period-bucketed summary (daily/weekly) can be deferred.

### Phase 10U-4 — Server-Side Pagination on Recent Usage (Backend + Frontend)
**Effort: S**

Replace `limit`-only param with `page`/`pageSize`. Update `AiUsageHandler.GetRecentAsync()`. Update Angular component to call server for each page rather than slicing a 500-row local array.

### Phase 10U-5 — Fallback Config UI (Frontend + minimal backend)
**Effort: S**

Expose `FallbackProviderName`, `FallbackModelName`, `FallbackEnabled` from entity via a new PATCH endpoint (or extend existing category PATCH). Add UI section to /admin/ai-config. Requires wiring the resolver to actually use fallback fields (currently dead code).

### Phase 10U-6 — Student Usage Filter (Backend + Frontend)
**Effort: M**

Add `studentId` query param to `GET /api/admin/ai-usage/recent`. Add student lookup/autocomplete to the Angular usage page.

### Phase 10U-7 — Backend Filtering (Provider/Model/Feature) (Backend)
**Effort: M**

Move provider, model, and feature filtering from client-side to backend query params. Required before dataset grows large enough to make 500-row client-side filtering a UX problem.

### Phase 10U-8 — Pricing Admin UI (Backend + Frontend)
**Effort: M**

Add `AiProviderPricing` DB table (providerName, modelName, inputPer1KTokens, outputPer1KTokens, effectiveFrom). Admin CRUD endpoint. Display in /admin/ai-config per model. Removes deploy dependency for price changes.

### Phase 10U-9 — Export (Backend)
**Effort: S**

Add `GET /api/admin/ai-usage/export?format=csv&startDate=...&endDate=...`. Returns streamed CSV. Deferred until filtering and pagination are solid.

### Phase 10U-10 — Cost Trend Chart and Alerts (Frontend + minor backend)
**Effort: M**

Period-bucketed summary endpoint (daily/weekly). Angular chart component. High-cost-call flag. Fallback rate alert.

---

## 11. Recommended Next Phase

**10U-1: Fix Pricing Data** (appsettings only, no migration, no code change)

Rationale: All costs are currently $0. This is a silent, invisible data quality failure that makes the entire usage admin section misleading. Every subsequent phase (cost alerts, model comparison, per-student cost) depends on pricing data being correct. XS effort, immediate impact.

**10U-2: Token Totals on Summary Cards** (backend DTO + frontend only)

Should follow immediately after 10U-1 since it is also XS and the backend already has the data.

---

## 12. What Should Be Deferred

- **Cost trend chart** — useful but requires period-bucketed backend endpoint; do after date-range filtering is stable.
- **Per-student token quotas** — requires enforcement layer in AI call path; scope risk; defer to a dedicated governance phase.
- **Admin-editable test prompts** — low value vs. complexity; defer.
- **Model capability legend** — editorial content; defer.
- **Usage export** — useful but not blocking; do after server-side pagination is stable.
- **Cost calculator tool** — nice-to-have; defer.
- **Correlation ID search** — debugging tool; defer until demand is demonstrated.
- **Max-token budget enforcement** — significant scope; requires call-path change; defer to governance phase.

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Fix pricing data before any UI work | All cost fields are $0; downstream features depend on correct data |
| Keep fallback wiring in scope for 10U-5 | Entity exists, resolver ignores it; dead code is a correctness risk |
| Keep export deferred | Blocked by pagination/filtering; do not parallelize |
| Backend filtering deferred until scale demands it | Client-side filtering adequate below ~10K rows/day |

---

## Risks and Unresolved Questions

1. **How is pricing injected in production today?** If via secrets/environment variables (not appsettings), the pricing section may already exist in production but not in the checked-in config files. Needs verification before 10U-1.
2. **Is the fallback resolver intentionally disabled?** The entity has `FallbackEnabled` but the resolver does not use it. This may be intentional (incomplete feature) or an oversight. Needs clarification before 10U-5.
3. **Is `AiUsageLog` indexed for date-range queries?** Index on `CreatedAt` should exist before adding date-range filtering at scale.
4. **What is the expected daily call volume?** Determines urgency of server-side pagination and backend filtering.

---

## Final Verdict

The AI admin area is structurally solid. The provider abstraction, category routing, credential management, token logging, and test-connection flows are all production-quality. The admin UI for /admin/ai-config is comprehensive.

The usage admin (/admin/usage) has significant gaps that make it misleading rather than informative: costs are invisibly $0, there is no date filtering, and all filtering is client-side. These gaps make it impossible for an admin to understand real AI spend or audit provider performance.

**The next recommended action is Phase 10U-1 (fix pricing data) followed immediately by 10U-2 (token totals on summary cards).** Both can ship as a single small slice with no schema migration.

---

## Confirmation

- No backend source files changed.
- No frontend source files changed.
- No migrations added.
- No provider behavior changed.
- No cost calculation changed.
- No commit created.
- No push performed.
