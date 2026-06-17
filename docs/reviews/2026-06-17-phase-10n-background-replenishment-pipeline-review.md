---
status: current
lastUpdated: 2026-06-17 23:30
owner: engineering
---

# Phase 10N — Background Replenishment Pipeline — Engineering Review

**Date:** 2026-06-17
**Sprint:** Phase 10N
**Related phases:** 10K (curriculum syllabus), 10L (CEFR-aware routing), 10M (readiness pool entity)

---

## Summary

Phase 10N builds the background replenishment engine that keeps student readiness pools healthy. The engine handles pool health checks, expired/stale/orphaned item cleanup, failed item retry, and shortfall fill for Today lesson and Practice Gym pools.

---

## Files changed

### New — Application layer

- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolReplenishmentOptions.cs` — configuration options, section `"ReadinessPool"`.
- `src/LinguaCoach.Application/ReadinessPool/PoolHealthSummary.cs` — lightweight health snapshot DTO.
- `src/LinguaCoach.Application/ReadinessPool/IReadinessPoolReplenishmentService.cs` — service interface + `ReplenishmentRunSummary` DTO.

### New — Infrastructure layer

- `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs` — full replenishment engine implementation.
- `src/LinguaCoach.Infrastructure/Jobs/ReadinessPoolReplenishmentJob.cs` — Quartz job wrapper (every 20 min, `[DisallowConcurrentExecution]`).

### Modified — Infrastructure DI

- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registers `IReadinessPoolReplenishmentService`, `ReadinessPoolReplenishmentJob`, and `ReadinessPoolReplenishmentOptions` from configuration.
- Signature updated: `AddInfrastructure(IConfiguration? configuration = null)` — backward-compatible optional parameter.

### Modified — API

- `src/LinguaCoach.Api/Program.cs` — passes `builder.Configuration` to `AddInfrastructure`.
- `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs` — adds `ReadinessPoolReplenishmentJob` trigger (every 20 minutes).
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` — adds `GET /api/admin/students/{studentId}/readiness-pool/health` endpoint.

### New — Tests

- `tests/LinguaCoach.UnitTests/ReadinessPool/ReplenishmentOptionsTests.cs` — 16 unit tests covering options defaults, pool health math, lower-level content rules, routing snapshot preservation, and review/scaffold gating.
- `tests/LinguaCoach.IntegrationTests/ReadinessPool/ReplenishmentIntegrationTests.cs` — 11 integration tests covering service DI, health counts, status isolation, retry path, admin endpoints (both), and 10M smoke tests.

### Modified — Docs

- `docs/architecture/readiness-pool.md` — Phase 10N section: replenishment engine, job, config, health model, sweep/retry rules, review/scaffold rule, admin endpoints.
- `docs/sprints/current-sprint.md` — Phase 10N entry added.
- `docs/handoffs/current-product-state.md` — Phase 10N section added.
- `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md` — this file.

---

## Architecture decisions

### 1. Replenishment service in Application + Infrastructure (not a domain service)

The replenishment engine orchestrates DB queries, routing recommendations, and pool service calls. It belongs in Infrastructure (depends on EF Core and external services) with an Application-layer interface. This matches the existing job pattern.

### 2. Configuration via Options pattern, not DB-backed

Options are bound from `appsettings.json` via `IOptions<ReadinessPoolReplenishmentOptions>`. A TODO is recorded for moving these to DB-backed admin config in Phase 10O+.

### 3. Duplicate prevention via in-memory key set

Before queuing new items, the service loads existing `Queued/Generating/Ready/Reserved` item keys `(objectiveKey, patternKey, cefrLevel)` into a `HashSet` and skips duplicates. This avoids N+1 queries during fill.

### 4. Review/scaffold gating via ledger weak events

`AllowReviewOrScaffold=true` is only passed to routing when `EnableReviewScaffoldGeneration=true` (default false) AND `IStudentLearningLedger.GetWeakEventsAsync` returns at least one event. This prevents B2 students from silently receiving B1 Normal content. The conservative default means no lower-level content is generated unless the feature flag is explicitly enabled and ledger signals support it.

### 5. Serving from pool deferred to Phase 10O

Phase 10N creates pool items and maintains their health but does not change user-facing serving paths. Today and Practice Gym still fall back to on-demand generation. `ReserveNextReadyAsync` integration into session retrieval belongs to Phase 10O.

---

## Findings by priority

### P0 — None

No blocking issues found.

### P1 — conservative review/scaffold default confirmed correct

`EnableReviewScaffoldGeneration=false` by default. B2 students cannot silently receive B1 content. This aligns with the 10L/10M rules that required `IsLowerLevelContent=true` to have `RoutingReason != Normal`.

### P2 — skill rotation is round-robin, not ledger-weighted

The fill loop rotates through a fixed skill list. A future improvement (10O+) would weight skill selection from `StudentSkillProfile` weak signals. Recorded as TODO.

### P3 — `EnableReviewScaffoldGeneration` is a global feature flag

It is not yet per-student. A per-student signal (e.g. "has weak events for specific skill") would be more precise. Deferred to Phase 10O.

---

## Tests

| Suite | Before | After | Delta |
|---|---|---|---|
| Architecture | 3 | 3 | 0 |
| Unit | 1144 | 1160 | +16 |
| Integration | 576 | 587 | +11 |
| Total | 1723 | 1750 | +27 |
| Failed | 0 | 0 | — |

---

## CI gate results

- `dotnet restore` — clean
- `dotnet build --configuration Release` — clean, 0 errors
- `dotnet test --configuration Release` — 1750 passed, 0 failed
- Angular/Playwright — blocked by pre-existing Node 24 + path-with-space esbuild postinstall issue. No Angular source changed in Phase 10N. Pre-existing environment-only blocker.

---

## What was NOT implemented

Per phase specification, the following are explicitly not in Phase 10N:

- Practice Gym suggested UI redesign
- Full "Suggested for you" frontend experience
- Admin write UI for pool
- Full mastery engine
- Full placement engine
- `StudentProfile.CefrLevel` migration
- Plus-level persistence
- Serving from pool on user-facing paths (deferred to 10O)
- Per-student `EnableReviewScaffoldGeneration` signal
- Ledger-weighted skill rotation
- `AllowReviewOrScaffold=true` enabled by default

---

## TODOs added

- Move `ReadinessPoolReplenishmentOptions` to DB-backed admin config (10O+).
- Enable `EnableReviewScaffoldGeneration` after mastery/weakness engine validated.
- Wire `ReserveNextReadyAsync` into Today session retrieval (10O).
- Wire `ReserveNextReadyAsync` into Practice Gym retrieval (10O).
- Weight skill rotation by `StudentSkillProfile` weak signals (10O+).
- Per-student review/scaffold signal instead of global feature flag (10O+).

---

## Risks

- Replenishment creates `Queued` items only. The existing `PracticeGymGenerationJob` and `LessonBatchGenerationJob` must pick them up and materialize them. If those jobs are disabled or slow, queued items accumulate without becoming ready.
- `MaxItemsGeneratedPerRun=50` is a safety cap but may be insufficient if many students are below target simultaneously. Monitor in production and tune.

---

## Final verdict

Phase 10N is complete. The replenishment engine is implemented, tested, and documented. No existing behaviour was broken. The conservative defaults (review/scaffold off, workplace not default, general_english fallback) are preserved throughout.

## Next recommended action

Phase 10O: wire `ReserveNextReadyAsync` into Today session and Practice Gym retrieval so the pool is actually consumed by students, and implement the suggested Practice Gym UI.
