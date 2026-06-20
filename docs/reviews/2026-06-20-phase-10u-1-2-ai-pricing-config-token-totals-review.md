# Phase 10U-1/10U-2 — AI Pricing Config Seed + Usage Token Totals

**Date:** 2026-06-20
**Sprint/Phase:** 10U-1 + 10U-2 (combined XS slice)
**Type:** Implementation Review
**Author:** Claude Code
**Status:** Complete — all gates green, no commit/push

---

## Scope

Fix the silent $0 cost bug caused by missing pricing config keys in appsettings. Surface token totals on the AI Usage admin summary page.

---

## Files Changed

### Backend

| File | Change |
|---|---|
| `src/LinguaCoach.Api/appsettings.json` | Added `OpenAI:Pricing`, `Gemini:Pricing`, `Anthropic:Pricing` sections with per-model InputPer1KTokens / OutputPer1KTokens |
| `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` | Added `TotalInputTokens`, `TotalOutputTokens`, `TotalTokens` to `AiUsageSummaryDto` |
| `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` | Aggregates `TotalInputTokens`, `TotalOutputTokens`, `TotalTokens` from log rows |
| `src/LinguaCoach.Api/Controllers/AiUsageController.cs` | Exposes `totalInputTokens`, `totalOutputTokens`, `totalTokens` in summary JSON response |
| `tests/LinguaCoach.UnitTests/Ai/OpenAiProviderTests.cs` | Added 4 new pricing test cases (provider binding, non-zero cost) |
| `tests/LinguaCoach.UnitTests/Admin/AiUsageSummaryTests.cs` | New file — 3 unit tests for token total correctness |

### Frontend

| File | Change |
|---|---|
| `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` | Added `totalInputTokens`, `totalOutputTokens`, `totalTokens` to `AiUsageSummary` interface |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` | Added `formatTokens()` helper; updated CSS grid to 4-col at 900px, 8-col at 1200px |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` | Added 3 stat cards: Input tokens, Output tokens, Total tokens |
| `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` | Updated `makeSummary()` factory; updated card count assertion (5→8); added 3 new token card tests |
| `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` | Added `totalTokens` to the AI Usage mock to match updated interface |

---

## Backend Changes Detail

### Pricing Config (10U-1)

Added to `appsettings.json` — operational defaults that must be overridden in production via environment secrets or admin config for accuracy:

```json
"OpenAI": {
  "Pricing": {
    "gpt-4o":       { "InputPer1KTokens": 0.0025,  "OutputPer1KTokens": 0.01   },
    "gpt-4o-mini":  { "InputPer1KTokens": 0.00015, "OutputPer1KTokens": 0.0006 },
    "gpt-4.1":      { "InputPer1KTokens": 0.002,   "OutputPer1KTokens": 0.008  },
    "gpt-4.1-mini": { "InputPer1KTokens": 0.0004,  "OutputPer1KTokens": 0.0016 },
    "gpt-4.1-nano": { "InputPer1KTokens": 0.0001,  "OutputPer1KTokens": 0.0004 }
  }
},
"Gemini": {
  "Pricing": {
    "gemini-2.0-flash":      { "InputPer1KTokens": 0.0001,   "OutputPer1KTokens": 0.0004 },
    "gemini-2.0-flash-lite": { "InputPer1KTokens": 0.000075, "OutputPer1KTokens": 0.0003 },
    "gemini-1.5-pro":        { "InputPer1KTokens": 0.00125,  "OutputPer1KTokens": 0.005  },
    "gemini-1.5-flash":      { "InputPer1KTokens": 0.000075, "OutputPer1KTokens": 0.0003 }
  }
},
"Anthropic": {
  "Pricing": {
    "claude-sonnet-4-6":         { "InputPer1KTokens": 0.003,  "OutputPer1KTokens": 0.015 },
    "claude-haiku-4-5-20251001": { "InputPer1KTokens": 0.0008, "OutputPer1KTokens": 0.004 },
    "claude-opus-4-8":           { "InputPer1KTokens": 0.015,  "OutputPer1KTokens": 0.075 }
  }
}
```

**Important:** These values are operational defaults as of 2026-06-20. Provider pricing can change. They should be verified against live provider pricing pages and overridden via environment config or a future admin pricing UI (deferred to 10U-8).

No pricing logic was hardcoded in C#. `AiPricingOptions` already reads from configuration; we just populated the missing keys.

### Summary DTO Token Totals (10U-2)

`AiUsageSummaryDto` extended with three new positional record fields:

```csharp
long TotalInputTokens,
long TotalOutputTokens,
long TotalTokens,
```

`AiUsageHandler.GetSummaryAsync` aggregates these from `AiUsageLog.InputTokens` and `AiUsageLog.OutputTokens` (both already int, cast to long for large-dataset safety).

`AiUsageController.GetSummary` exposes all three fields in the JSON response alongside the existing fields. No breaking change — only additive fields.

---

## Frontend Changes Detail

`AiUsageSummary` TypeScript interface updated with three new fields.

`AdminAiUsageComponent` changes:
- `formatTokens(n: number): string` helper uses `toLocaleString()` for locale-aware number formatting (avoids Angular pipe `string | null` type incompatibility with `sp-admin-stat-card [value]`).
- Three new `sp-admin-stat-card` instances: "Input tokens", "Output tokens", "Total tokens".
- Grid breakpoints updated: 2-col mobile → 4-col at 900px → 8-col at 1200px. No visual regression below 900px.
- Existing "By provider", "By feature", and "Recent calls" sections unchanged.

---

## Tests

### Backend — new tests

| Test | File |
|---|---|
| `Pricing_DefaultAppsettingsValues_BindCorrectlyForOpenAi` (3 models, Theory) | `OpenAiProviderTests.cs` |
| `Pricing_DefaultAppsettingsValues_BindCorrectlyForGemini` (2 models, Theory) | `OpenAiProviderTests.cs` |
| `Pricing_DefaultAppsettingsValues_BindCorrectlyForAnthropic` (2 models, Theory) | `OpenAiProviderTests.cs` |
| `Pricing_NonZeroConfig_ProducesNonZeroCost` | `OpenAiProviderTests.cs` |
| `AiUsageSummaryDto_TokenTotals_SumCorrectly` | `AiUsageSummaryTests.cs` |
| `AiUsageSummaryDto_WhenNoLogs_TokenTotalsAreZero` | `AiUsageSummaryTests.cs` |
| `AiUsageSummaryDto_TotalTokens_EqualsInputPlusOutput` | `AiUsageSummaryTests.cs` |

### Frontend — new/updated tests

| Test | Change |
|---|---|
| `renders stat cards after summary loads` | Updated: 5 → 8 cards |
| `renders input token stat card` | New |
| `renders output token stat card` | New |
| `renders total token stat card` | New |
| `makeSummary()` factory | Updated: added token fields |
| `admin-wrapper-migration` AI Usage mock | Updated: added `totalTokens` |

---

## Gate Results

| Gate | Result |
|---|---|
| `git diff --check` | PASS — no whitespace errors |
| `dotnet build --configuration Release` | PASS — 0 errors, 7 pre-existing warnings |
| `dotnet test --configuration Release` | PASS — 1955/1955 (arch 3, unit 1248, integration 704) |
| `npm run build -- --configuration production` | PASS — clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 794/794 |

Backend tests: +11 (1944 → 1955)
Frontend tests: +3 (791 → 794)

---

## Decisions Made

| Decision | Rationale |
|---|---|
| Pricing values in appsettings as operational defaults | `AiPricingOptions` already reads from config; no C# change needed; values can be overridden per-environment |
| No pricing hardcoded in C# | Existing options pattern is correct; just populate keys |
| `formatTokens()` helper instead of pipe in binding | Angular number pipe returns `string | null`; `sp-admin-stat-card [value]` is `string | number`; helper avoids type error |
| No DB pricing table this phase | Deferred to 10U-8; config-based approach is sufficient for now |
| No migration | No schema change |
| `long` for token aggregates | `int` per row; `long` sum for safety at scale |

---

## What Was NOT Changed

- Provider routing behavior — unchanged
- Usage governance behavior — unchanged
- AI call path — unchanged
- Cost calculation logic — unchanged (`AiPricingOptions.EstimateCostUsd` untouched)
- Fallback provider wiring — unchanged
- Call history DTO (`AiUsageRecentItem`) — unchanged (backward-compatible)
- Pagination/filtering — unchanged
- Any other admin page — unchanged

---

## Remaining AI Usage/Config TODOs After This Phase

- **10U-3:** Date-range filtering on usage endpoints
- **10U-4:** Server-side pagination on recent usage
- **10U-5:** Fallback config UI + wire resolver to use fallback fields
- **10U-6:** Student usage filter
- **10U-7:** Backend filtering (provider/model/feature)
- **10U-8:** Pricing admin UI with DB table (replaces appsettings-based pricing)
- **10U-9:** Export endpoint
- **10U-10:** Cost trend chart and alerts

Pricing values should be verified against provider pricing pages before first production deployment. Override via environment variables or secrets in production.

---

## Enterprise AI Usage Tracking Gaps

Audit of `AiUsageLog` entity and all construction sites (`AiExecutionService.cs`, `CefrAssessmentHandler.cs`, `SpeakingSessionHandler.cs`).

| Dimension | Tracked? | Field | Notes |
|---|---|---|---|
| Provider | ✅ Yes | `ProviderName` | String; sourced from `AiResponse.ProviderName` |
| Model | ✅ Yes | `ModelName` | String; sourced from `AiResponse.ModelName` |
| Student ID | ✅ Yes | `StudentProfileId` (nullable Guid) | Null for system/path-generation calls |
| Feature/function area | ✅ Yes | `FeatureKey` | String key e.g. `activity_evaluate_writing` |
| Input tokens | ✅ Yes | `InputTokens` (int) | Per call; summed in summary since 10U-2 |
| Output tokens | ✅ Yes | `OutputTokens` (int) | Per call; summed in summary since 10U-2 |
| Total tokens | ✅ Derived | `InputTokens + OutputTokens` | No stored column; computed at summary time |
| Input cost | ✅ Derived | `CostUsd` (combined) | Single cost field; not split input/output |
| Output cost | ⚠️ Not split | `CostUsd` only | Input and output cost combined into one field; cannot audit input vs output cost separately |
| Total cost | ✅ Yes | `CostUsd` | Stored per call; was always $0 before 10U-1 fixed pricing config |
| Success/failure status | ✅ Yes | `WasSuccessful`, `FailureReason` | Failure reason is exception type name (not user message) |
| Fallback status | ✅ Yes | `IsFallback` | Boolean; set by `AiExecutionService` |
| Fallback provider/model | ❌ Missing | — | When `IsFallback=true`, the original (failed) provider/model is not recorded. Only the fallback provider/model is stored. Cannot audit: "primary was OpenAI, fell back to Anthropic." |
| Retry status | ❌ Missing | — | No retry count or retry indicator. Cannot distinguish first attempt from retry. |
| Request/correlation ID | ✅ Yes | `CorrelationId` (string?, nullable) | Passed from caller; not always set by all sites |
| Duration | ✅ Yes | `DurationMs` (long) | Wall-clock time of provider call in ms |
| Prompt/template key | ❌ Missing | — | `FeatureKey` identifies the feature but not which prompt template or prompt version was rendered |
| Prompt version | ❌ Missing | — | No version tracking on prompts. Cannot audit: "did prompt v2 reduce output tokens vs v1?" |
| Request type | ⚠️ Partial | `FeatureKey` | Feature key implies request type but is not a typed enum. Cannot filter by category (LLM vs TTS vs STT) without string prefix matching. |
| Activity/session/lesson ID | ❌ Missing | — | `AiUsageLog` has no FK to `LearningActivity`, `LearningSession`, or `SessionExercise`. Cannot trace: "which lesson session triggered this call?" |
| Tenant/admin context | ❌ Missing | — | No workspace/org/admin user ID on log rows. All calls appear under a single tenant. Required for future multi-tenant billing. |
| Date/time period | ✅ Yes | `CreatedAt` (inherited from `BaseEntity`) | UTC timestamp; indexing for date-range queries not yet confirmed |

### Summary

**Fully tracked (8):** provider, model, student ID, feature key, input tokens, output tokens, total cost, success/failure/fallback status, correlation ID, duration, created-at timestamp.

**Partially tracked (2):**
- `CostUsd` is total cost combined — input/output cost split is not stored.
- `FeatureKey` implies request type but is untyped string — no LLM vs TTS category flag.

**Not tracked (5) — future TODOs:**
- Fallback provider/model (the original that failed)
- Retry count/status
- Prompt template key and version
- Activity/session/lesson ID linkage
- Tenant/admin context

### Deferred TODOs Created

These are documented here for future phases. None require a migration in this phase.

**TODO-10U-GAP-1 — Record fallback source provider/model**
Add `OriginalProviderName` and `OriginalModelName` (nullable) to `AiUsageLog`. Set when `IsFallback=true` so the failed primary provider is auditable. Requires migration and `AiExecutionService` change.

**TODO-10U-GAP-2 — Record retry count**
Add `AttemptNumber` (int, default 1) to `AiUsageLog`. Increment per retry in `AiExecutionService`. Enables: "what % of calls required 2+ attempts?" Requires migration.

**TODO-10U-GAP-3 — Record prompt template key and version**
Add `PromptKey` (string?) and `PromptVersion` (int?) to `AiUsageLog`. Pass from `AiExecutionService` or prompt-rendering layer. Enables prompt A/B cost/quality analysis. Requires migration.

**TODO-10U-GAP-4 — Record activity/session/lesson ID**
Add `LearningActivityId` (Guid?) and `LearningSessionId` (Guid?) to `AiUsageLog`. Set where available from caller context. Enables: "how much did session X cost?" Requires migration.

**TODO-10U-GAP-5 — Record tenant/admin context**
Add `AdminUserId` (Guid?) to `AiUsageLog` for calls initiated by an admin (e.g., test connection, category test). Required for multi-tenant billing isolation. Requires migration.

**TODO-10U-GAP-6 — Split CostUsd into InputCostUsd / OutputCostUsd**
Replace `CostUsd` with `InputCostUsd` and `OutputCostUsd` for per-component cost auditing. Requires migration and all call-site changes. High effort; defer until pricing admin table (10U-8) is in place.

**TODO-10U-GAP-7 — Add FeatureCategory enum or flag (LLM vs TTS vs STT)**
Add `RequestType` enum column to `AiUsageLog`: `Llm`, `Tts`, `Stt`. Enables filtering and cost breakdown by modality without string prefix matching. Low migration risk; medium effort.

---

## Confirmation

- No provider routing behavior changed.
- No usage governance behavior changed.
- No unrelated admin UI refactored.
- No commit created.
- No push performed.
