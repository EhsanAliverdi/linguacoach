# Phase 18A-G — Generation Diagnostics Hardening — Engineering Review

**Date:** 2026-07-01
**Sprint:** Phase 18A-G (extends Phase 18A-F)
**Author:** Engineering (Claude Code)

---

## Summary

Phase 18A-G hardens the generation diagnostics system introduced in Phase 18A-F. It adds provider/model traceability, SHA-256 content hashing for prompt versioning, configurable data retention with an automated prune job, objective/student context threading, and an abandoned-generation rate warning. The admin diagnostics UI is updated to surface all new fields.

---

## Files Reviewed / Changed

### Backend

| File | Change |
|------|--------|
| `src/LinguaCoach.Domain/Entities/AiPrompt.cs` | Added `ContentHash` property computed from SHA-256 on construction |
| `src/LinguaCoach.Persistence/Configurations/AiPromptConfiguration.cs` | Maps `content_hash` column (varchar 64, nullable) |
| `src/LinguaCoach.Persistence/Migrations/20260701120000_T70_AiPromptContentHash.cs` | Hand-authored migration adding `content_hash` to `ai_prompts` |
| `src/LinguaCoach.Persistence/Migrations/LinguaCoachDbContextModelSnapshot.cs` | Snapshot updated for T70 |
| `src/LinguaCoach.Application/Activity/IAiActivityGenerator.cs` | Added `ObjectiveKey`, `StudentProfileId`, `GenerationSource` optional fields to `ActivityGenerationContext` |
| `src/LinguaCoach.Application/Admin/AdminQueries.cs` | Extended `ValidationFailureItem`, `PromptTemplateItem`; new `ProviderModelBreakdownItem`, `AbandonedGenerationWarning`; extended `GenerationQualitySummary` |
| `src/LinguaCoach.Infrastructure/Ai/AiExecutionService.cs` | New `AiExecutionResult` record and `ExecuteWithMetaAsync` method |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Uses `ExecuteWithMetaAsync`; threads correlation ID, provider, model, objective key, student profile ID into failure log |
| `src/LinguaCoach.Infrastructure/Admin/AdminGenerationQualityHandler.cs` | Full rewrite: reads `IConfiguration`, computes provider breakdown, abandoned warning, content hash short form |
| `src/LinguaCoach.Infrastructure/Jobs/GenerationValidationFailurePruneJob.cs` | **New** — Quartz daily prune job; retention [7,365] days; non-blocking; uses `ToListAsync + RemoveRange + SaveChanges` |
| `src/LinguaCoach.Api/Controllers/AdminGenerationQualityController.cs` | Exposes all new fields; `recentDays` max clamped to `min(retentionDays, 90)` |
| `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs` | Registers `GenerationValidationFailurePruneJob` with daily trigger |
| `src/LinguaCoach.Api/appsettings.json` | `GenerationQuality` config section added |

### Frontend

| File | Change |
|------|--------|
| `src/LinguaCoach.Web/src/app/core/services/generation-quality.service.ts` | Updated all interfaces for new fields |
| `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts` | Three new computed signals: `qProviderBreakdown`, `qAbandonedWarning`, `qRetentionDays` |
| `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html` | Abandoned warning banner, provider/model column, provider breakdown section, hash column, retention days in heading |
| `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.spec.ts` | Updated `makeQualitySummary` helper; fixed `PromptMetaItem` test objects |

### Tests

| File | Change |
|------|--------|
| `tests/LinguaCoach.UnitTests/Admin/GenerationQualityHandlerTests.cs` | +15 tests for Parts B, C, D, E, F |
| `tests/LinguaCoach.UnitTests/Admin/GenerationValidationFailurePruneJobTests.cs` | **New** — 4 tests for prune job |

---

## Findings by Priority

### P0 — No issues

No blocking issues found. All hard constraints were respected:
- Activity player behaviour: unchanged
- New activity formats: none added
- Speaking/writing mastery signals: unchanged
- CEFR from AI: no change
- Objective completion from AI: no change
- LP regeneration from AI: no change
- Live AI calls in tests: none

### P1 — Resolved during implementation

**SQLite EF Core Guid format mismatch (prune job tests)**
- Symptom: `DbUpdateConcurrencyException` when deleting EF-loaded entities that were inserted via raw SQL `INSERT`.
- Root cause: EF Core SQLite stores Guid as a different binary/text format than `Guid.ToString()` string literals in raw SQL.
- Fix: Use EF Core's `.Add()` + `.SaveChangesAsync()` to insert (correct format), then `ExecuteSqlAsync($"UPDATE ... SET created_at = {timestamp} WHERE id = {id}")` with proper parameterization to backdate. EF Core serializes both parameters using its internal type mapping, ensuring they match in comparisons.

**SQLite DateTime format mismatch (prune job backdating)**
- Symptom: `WHERE created_at < @cutoff` not matching rows backdated via raw SQL with `timestamp.ToString("o")`.
- Root cause: EF Core's SQLite DateTimeTypeMapping uses `yyyy-MM-dd HH:mm:ss.fffffff` (space separator, no Z), not ISO 8601 roundtrip format.
- Fix: Switched to `ExecuteSqlAsync` with interpolated `{timestamp}` so EF Core's parameter serialization is used for both stored value and comparison, ensuring format consistency.

**Angular badge tone compile error**
- `tone="amber"` is not in `SpAdminBadgeTone` union (`'success' | 'warning' | 'info' | 'danger' | 'neutral' | 'primary' | 'purple'`).
- Fix: Changed to `tone="warning"`.

---

## Decisions Made

1. **`ExecuteWithMetaAsync` instead of modifying `ExecuteAsync`**: Existing callers of `ExecuteAsync` are not broken. The new method is a parallel alternative used only by `AiActivityGeneratorHandler`.

2. **`ToListAsync + RemoveRange + SaveChanges` for prune job**: `ExecuteDeleteAsync` was tried first but creates EF tracking issues with the same DbContext instance. The materialized approach works correctly with SQLite in-memory tests.

3. **SHA-256 computed in entity constructor**: Keeps hashing logic in the domain layer, ensures all `AiPrompt` instances created via normal paths have a hash. Migration makes the column nullable so pre-T70 rows are safe.

4. **Content hash short form (8 chars) computed in handler, not stored**: Avoids a separate column. The full 64-char hash is in the domain; the short form is a presentation concern derived at query time.

5. **`ActivityGenerationContext` optional fields (backward compatible)**: Three new optional parameters with defaults means no call sites need updating.

---

## AskUserQuestion Answers

None required. All decisions were within engineering scope.

---

## Test Coverage

| Area | Tests | Result |
|------|-------|--------|
| Provider/model breakdown (Part B) | 4 unit | Pass |
| Content hash (Part C) | 4 unit | Pass |
| Retention config (Part D) | 1 unit + 4 prune job | Pass |
| Objective/student context (Part E) | 2 unit | Pass |
| Abandoned warning (Part F) | 4 unit | Pass |
| Prune job execution | 4 unit | Pass |
| Full backend unit suite | 1,660 | Pass |
| Backend integration suite | 1,319 (8 pre-existing failures) | No regressions |
| Architecture tests | 3 | Pass |
| Angular production build | n/a | Clean |
| Angular unit suite | 1,414/1,533 (119 pre-existing) | No regressions |

---

## Security Verification (Part I)

- No provider API keys, storage keys, or raw AI output are exposed in any endpoint or DTO.
- `ProviderName` and `ModelName` are metadata strings recorded at failure time, not derived from config at query time.
- `CorrelationId` is a short internal ID (16-char hex), not user-facing data.
- `ContentHashShort` is a one-way hash of the prompt template, not the prompt content itself.
- `ValidationErrors` contains only the structured validation error messages from schema/field checks, not AI response content.

---

## Risks and Unresolved Questions

| Risk | Severity | Status |
|------|----------|--------|
| Pre-existing 8 integration test failures (AI provider tests) | Low | Pre-existing; not introduced by this phase |
| Pre-existing 119 Angular unit test failures (`getStudentWritingEvaluations`) | Low | Pre-existing; not introduced by this phase |
| Prune job backdating in PostgreSQL integration tests | Low | Not applicable — prune job is tested in unit tests with SQLite; PostgreSQL integration test coverage of prune logic is deferred to a future integration test sprint |

---

## Implementation Tasks Produced

None. All Phase 18A-G scope is complete.

---

## Final Verdict

**PASS.** All Parts (B through L) implemented and verified. Build is clean. No new regressions introduced. All hard constraints honoured.

---

## Next Recommended Action

- Monitor abandoned generation rate warning in production once deployed.
- Consider adding a PostgreSQL integration test for the prune job in a future sprint.
- Phase 19 scope to be determined by product.
