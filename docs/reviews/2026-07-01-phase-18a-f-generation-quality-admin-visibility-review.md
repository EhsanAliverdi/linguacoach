# Phase 18A-F — Generation Quality Admin Visibility

**Date:** 2026-07-01
**Sprint:** Phase 18A-F
**Author:** Engineering

---

## 1. Overview

Phase 18A-F adds lightweight admin visibility for prompt versions, generation validation failures, and content quality diagnostics. It does not change the generation engine, add new activity formats, change activity player behaviour, or change speaking/writing mastery signal behaviour.

---

## 2. Files Changed

**Domain:**
- `src/LinguaCoach.Domain/Entities/GenerationValidationFailure.cs` — new entity

**Persistence:**
- `src/LinguaCoach.Persistence/Configurations/GenerationValidationFailureConfiguration.cs` — EF config
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — added `DbSet<GenerationValidationFailure>`
- `src/LinguaCoach.Persistence/Migrations/` — new T69_GenerationValidationFailures migration

**Application:**
- `src/LinguaCoach.Application/Admin/AdminQueries.cs` — extended `PromptTemplateItem` with `SeededAtUtc`; added `ValidationFailureItem`, `PatternFailureBreakdownItem`, `CefrFailureBreakdownItem`, `GenerationQualitySummary` DTOs; added `IAdminGenerationQualityHandler` interface

**Infrastructure:**
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added `LogValidationFailureAsync` helper; instruments first-attempt and retry validation failures to persist records
- `src/LinguaCoach.Infrastructure/Admin/AdminGenerationQualityHandler.cs` — new handler implementing `IAdminGenerationQualityHandler`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs` — updated `ListPromptsAsync` to include `CreatedAt` in DTO
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `AdminGenerationQualityHandler`

**API:**
- `src/LinguaCoach.Api/Controllers/AdminGenerationQualityController.cs` — new controller, `GET /api/admin/generation-quality/summary`

**Angular:**
- `src/LinguaCoach.Web/src/app/core/services/generation-quality.service.ts` — new `GenerationQualityService` with typed interfaces
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts` — added `GenerationQualityService` dependency, computed quality signals, `loadGenerationQuality()` method
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html` — added Generation Quality card section with KPI strip, validation failure table, pattern/CEFR breakdown, prompt version summary
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.spec.ts` — updated to mock `GenerationQualityService`; added 6 new tests

**Tests:**
- `tests/LinguaCoach.UnitTests/Admin/GenerationQualityHandlerTests.cs` — 7 new unit tests
- `tests/LinguaCoach.IntegrationTests/Api/AdminGenerationQualityEndpointTests.cs` — 8 new integration tests

---

## 3. Audit Findings (Part A)

| Area | Current Visibility | Missing Visibility | Risk | Decision |
| ---- | ------------------ | ------------------ | ---- | -------- |
| AiPrompt | Key, Version, IsActive, MaxInputTokens, MaxOutputTokens via GET /api/admin/prompts | SeededAtUtc not exposed | Low | Added SeededAtUtc (CreatedAt) to PromptTemplateItem |
| Prompt hash | Version int available | No SHA256 content hash stored | Low | Document: content hash is future enhancement; Version+SeededAt sufficient for now |
| Generation batch | Status, ProviderName, ModelName, FailureReason via GET /api/admin/generation/batches | No per-pattern failure detail | Medium | Addressed via new GenerationValidationFailure entity |
| Content validation failures | Thrown as AiResponseValidationException, logged via ILogger | Not persisted to DB, no admin endpoint | High | New entity + endpoint added |
| Pattern quality metrics | None | No generated/failed/rate counts | High | Added via GenerationValidationFailures aggregation |
| CEFR breakdown | None | Missing | Medium | Added via GenerationValidationFailures aggregation |
| Provider/model breakdown | AiUsageLogs has provider/model by feature key | Not cross-referenced with validation failures | Low | Document: future enhancement — join with AiUsageLogs by correlationId |
| AiUsageLogs | featureKey, provider, model, wasSuccessful, failureReason | featureKey does not include patternKey | Low | Out of scope — AiUsageLogs FailureReason captures exception type, not validator errors |
| Admin UI prompt metadata | GET /api/admin/prompts exists, no hash/date shown | SeededAt not shown | Low | Added seededAtUtc to prompt summary in new quality endpoint |
| Diagnostics page | System health, AI config, event buffer | No generation quality section | High | Added Generation Quality card to Diagnostics page |

---

## 4. Implementation Details

### 4.1 GenerationValidationFailure Entity

New append-only entity. Fields:
- `PatternKey` (nullable) — exercise pattern key at generation time
- `ActivityTypeName` — ActivityType enum name
- `CefrLevel` (nullable) — CEFR level from generation context
- `ObjectiveKey` (nullable) — reserved for future routing instrumentation
- `ProviderName`, `ModelName` (nullable) — not yet available at handler level; future enhancement via AiUsageLogs join
- `ValidationErrors` — semicolon-delimited error messages (safe; never contains raw AI output)
- `AttemptNumber` — 1 = first attempt failed; 2 = retry also failed (generation abandoned)
- `CorrelationId`, `StudentProfileId` (nullable)

EF migration: T69_GenerationValidationFailures.

### 4.2 Instrumentation

`AiActivityGeneratorHandler.LogValidationFailureAsync` persists a `GenerationValidationFailure` record:
- On first-attempt validation failure (before retry), with `AttemptNumber = 1`
- On retry validation failure, with `AttemptNumber = 2`, before throwing `AiResponseValidationException`

Failure logging is non-blocking (exceptions are caught and logged via `ILogger`).

### 4.3 GET /api/admin/generation-quality/summary

Admin-only endpoint. `recentDays` parameter (1–90, default 30).

Response sections:
- `validationFailureSummary` — totalFailures, abandonedGenerations, failuresLast24Hours
- `latestFailures` — up to 20 recent failures with safe fields only
- `patternFailureBreakdown` — grouped by patternKey with failure count and abandoned count
- `cefrFailureBreakdown` — grouped by cefrLevel
- `promptSummary` — active prompts with key, version, maxTokens, seededAtUtc

Content field excluded from promptSummary — use `GET /api/admin/prompts/{id}` for full content.

### 4.4 Admin UI

Added "Generation quality — last 30 days" card to the Diagnostics page (`/admin/diagnostics`).

Shows:
1. KPI strip: total failures, abandoned count, failures last 24h, active prompt count
2. Latest validation failures table (up to 20)
3. Pattern failure breakdown and CEFR breakdown (grid when data exists)
4. Active prompt version summary table

Empty state is shown when no failures exist in the window.

### 4.5 PromptTemplateItem Extension

Added `SeededAtUtc` (= `AiPrompt.CreatedAt`) to `PromptTemplateItem`. No migration required.

---

## 5. Data Not Yet Available

| Data | Reason | Recommendation |
| ---- | ------ | -------------- |
| Provider/model per validation failure | AiExecutionService records provider/model in AiUsageLogs; not passed back to caller | Future: correlate via correlationId or pass provider info from AiExecutionService |
| Prompt content SHA256 hash | Not stored on AiPrompt entity; computing on read is expensive for large prompts | Future: add ContentHash column, compute on seeder write |
| Prompt history / diff | Only current version stored; no version history retained per key | Future: retain all versions; prompt edit history |
| Prompt UpdatedAt | BaseEntity only has CreatedAt; SeededAt proxy is creation time | Future: add UpdatedAt to AiPrompt |
| Objective key per validation failure | RoutingContext has objective title; not easily extractable to a key at this level | Future: pass objectiveKey from LessonBatchGenerationJob through context |
| Per-student failure attribution | Generation context has no studentProfileId at the handler level for pool/batch generation | Future: pass studentProfileId through ActivityGenerationContext |

---

## 6. Privacy and Safety

- No provider API keys returned
- No storage keys or blob paths returned
- No raw AI output in validation errors (only parsed error messages from validator)
- `promptSummary` at generation-quality endpoint omits `content` field; full content via dedicated prompts endpoint only
- Failure messages are safe structured strings from `ModuleStageContentValidator`

---

## 7. Test Results

| Suite | Passed | Failed | Notes |
| ----- | ------ | ------ | ----- |
| Backend unit | 1640 | 0 | +7 new (GenerationQualityHandlerTests) |
| Backend architecture | 3 | 0 | No regressions |
| Backend integration | 1310 | 9 | 9 pre-existing AI-provider failures (same as Phase 18A baseline of 8; +1 pre-existing in SpeakingRolePlayActivityTests) |
| Angular production build | pass | — | 0 errors |
| Angular unit | 1414 | 119 | 119 pre-existing failures (down 1 from baseline); 33 diagnostics spec tests pass (+6 new) |
| Playwright | — | — | Not run; backend/unit/Angular coverage documented |

### Known pre-existing integration failures

Same 8–9 live-AI-provider failures as documented in Phase 18A. Tests call real AI provider (fake provider returns ServiceUnavailable). Not regressions.

---

## 8. Decisions Made

| Decision | Rationale |
| -------- | --------- |
| Separate `AdminGenerationQualityHandler` (not added to existing `AdminHandler`) | `AdminHandler` already implements 4 interfaces; separation keeps handler focused |
| `recentDays` window (max 90) | Prevents unbounded queries on the failures table; 30-day default matches typical monitoring horizon |
| No prompt content hash | Not stored; computing on read is expensive; Version+SeededAt sufficient for this phase |
| No per-student attribution for pool/batch generation | `ActivityGenerationContext` does not carry studentProfileId; added to future enhancements |
| Angular computed signals for template data | Avoids `@if (expr; as alias)` alias propagation issues in nested `@for` blocks |

---

## 9. Risks and Unresolved Questions

1. Over time the `generation_validation_failures` table grows indefinitely. No retention policy added. Future: add a pruning job or archive strategy.
2. Provider/model info is not yet associated with failures. Future: join with AiUsageLogs.
3. Objective key not captured. Future: thread from generation context.

---

## 10. Recommendation for Phase 18B

- Add provider/model to `GenerationValidationFailure` by passing info from `AiExecutionService` through a return value or event
- Add content hash to `AiPrompt` entity for deterministic version tracking
- Consider a data retention policy for the failures table (prune rows > 90 days)
- Add prompt diff view using version history (requires storing all versions)
- Consider auto-alerting when abandoned generation count exceeds threshold
