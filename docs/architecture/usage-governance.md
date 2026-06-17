---
status: current
lastUpdated: 2026-06-18
phase: 10R
---

# Usage Governance — Token Tracking & Quota Enforcement

## Overview

Phase 10R introduces the usage governance system. It tracks token consumption and AI call costs per student, enforces configurable per-feature daily/weekly/monthly quotas, and blocks expensive AI calls before they incur cost.

The system separates **prepared learning** (pre-generated, zero marginal AI cost) from **expensive AI** (on-demand generation, real cost per call). Prepared learning is always allowed. Expensive AI is gated.

---

## Core concepts

### Feature registry

All controllable features are registered in `FeatureDefinition`. Each entry has:

- `Key` — unique lowercase dot-notation string (e.g. `writing.evaluate`)
- `Category` — `PreparedLearning`, `DynamicAi`, `ExpensiveAi`, `AdminAction`, `System`
- `IsExpensive` — true for features that incur meaningful per-call AI cost
- `DefaultEnforcementMode` — default enforcement when no policy rule overrides it

### Feature categories

| Category | Description | Default enforcement |
|---|---|---|
| PreparedLearning | Pre-generated content, zero per-call cost | None (unlimited) |
| DynamicAi | On-demand but low-cost AI | TrackOnly |
| ExpensiveAi | Expensive on-demand AI (GPT-4o, Claude Sonnet) | TrackOnly (upgradable to HardLimit) |
| AdminAction | Admin-initiated actions | TrackOnly |
| System | Internal system events | None |

### Enforcement modes

| Mode | Behaviour |
|---|---|
| None | No tracking, always allowed |
| TrackOnly | Track usage, never block |
| SoftWarning | Allow but surface warning to UI when near limit |
| HardLimit | Block when limit exceeded, return 429 |
| AdminApprovalRequired | Block, require manual admin override |

### Usage policies

A `UsagePolicy` is a named set of `UsagePolicyRule` entries, each mapping a feature key to enforcement mode and limits. Policies have a `ScopeType`:

- `Global` — applies to all students unless overridden
- `Student` — assigned to a specific student (workspace/cohort deferred as TODO)

The active policy for a student is resolved as:

1. Student-specific assignment (if active)
2. Global default policy

### Usage event ledger

`UsageEvent` is append-only. Each AI call produces one event recording provider, model, input/output tokens, estimated cost, and correlation ID.

### Daily aggregate

`StudentUsageDaily` is an upserted aggregate per (student, date). It tracks:

- Total tokens, cost, AI call count
- Per-feature counters: `WritingEvaluations`, `SpeakingEvaluations`, `LessonGenerations`, etc.

This is the primary read model for quota enforcement (avoids full ledger scans for daily limit checks).

---

## Quota enforcement flow

```
Client → feature call → AiExecutionService
                           ↓
                        QuotaService.CheckAsync(studentId, featureKey)
                           ↓
                   resolve effective policy
                           ↓
                   find HardLimit rule for feature
                           ↓
               count today's events in UsageEvents
                           ↓
               compare to DailyLimit
                           ↓
             Allowed=true / Allowed=false + AvailableAlternatives
                           ↓
              [if blocked] → QuotaExceededException → HTTP 429
              [if allowed] → execute AI call
                           ↓
                    QuotaService.RecordAsync(event)
```

Failed AI calls are NOT recorded (no cost incurred).

---

## HTTP 429 response shape

```json
{
  "message": "Daily limit reached for writing.evaluate.",
  "featureKey": "writing.evaluate",
  "availableAlternatives": ["practice.prepared.complete"],
  "resetAt": "2026-06-19T00:00:00Z",
  "correlationId": "..."
}
```

---

## Seeded data

### Feature definitions (16)

PreparedLearning (5): `practice.prepared.complete`, `lesson.prepared.view`, `vocabulary.review`, `listening.prepared.play`, `progress.view`

DynamicAi (3): `speaking.evaluate`, `tts.generate`, `stt.transcribe`

ExpensiveAi (8): `writing.evaluate`, `lesson.generate`, `lesson.regenerate`, `learning_path.generate`, `learning_path.regenerate`, `practice.dynamic.generate`, `placement.evaluate`, `mastery.evaluate`

### Policies (3)

| Name | Scope | Purpose |
|---|---|---|
| Default Pilot Student | Global/default | TrackOnly all features, no blocking |
| Low Cost Student | Student | HardLimit expensive features (5/day writing, 3/day speaking, 5/day TTS) |
| Test Unlimited | Student | TrackOnly all, for automated testing |

---

## Admin APIs

All under `/api/admin/` — require admin role.

| Method | Path | Description |
|---|---|---|
| GET | `/feature-definitions` | List all feature definitions |
| GET | `/usage-policies` | List all policies |
| POST | `/usage-policies` | Create policy |
| GET | `/usage-policies/{id}` | Get policy with rules |
| PUT | `/usage-policies/{id}` | Update policy |
| PUT | `/students/{id}/usage-policy` | Assign policy to student |
| GET | `/students/{id}/usage-policy` | Get effective policy |
| GET | `/students/{id}/usage?period=today\|month` | Get usage summary |

---

## Audit log

Every admin action that changes a student's policy assignment writes an `AdminAuditLog` record with actor, target, action name, old/new value JSON, reason, and correlation ID.

---

## Deferred / out of scope for 10R

- Workspace/cohort policy inheritance (ScopeType.Workspace, ScopeType.Cohort)
- Billing and payment integration
- Provider-level pricing tables (cost is estimated by caller, not computed centrally)
- Monthly/weekly limit enforcement (daily only for now; monthly cost limit is stored but not checked)
- CSV export of usage data
- Student-facing usage widget
- Notification platform (no email/push when quota is near)
- Enterprise auth overhaul

These are tracked in `TODOS.md`.

---

## Files

### Domain

- `src/LinguaCoach.Domain/Enums/FeatureCategory.cs`
- `src/LinguaCoach.Domain/Enums/EnforcementMode.cs`
- `src/LinguaCoach.Domain/Enums/UsageUnitType.cs`
- `src/LinguaCoach.Domain/Enums/UsagePolicyScopeType.cs`
- `src/LinguaCoach.Domain/Entities/FeatureDefinition.cs`
- `src/LinguaCoach.Domain/Entities/UsagePolicy.cs`
- `src/LinguaCoach.Domain/Entities/UsagePolicyRule.cs`
- `src/LinguaCoach.Domain/Entities/StudentPolicyAssignment.cs`
- `src/LinguaCoach.Domain/Entities/UsageEvent.cs`
- `src/LinguaCoach.Domain/Entities/StudentUsageDaily.cs`
- `src/LinguaCoach.Domain/Entities/AdminAuditLog.cs`

### Application

- `src/LinguaCoach.Application/UsageGovernance/IUsageQuotaService.cs`
- `src/LinguaCoach.Application/UsageGovernance/QuotaDecision.cs`
- `src/LinguaCoach.Application/UsageGovernance/QuotaExceededException.cs`
- `src/LinguaCoach.Application/UsageGovernance/IUsageGovernanceAdminService.cs`

### Infrastructure

- `src/LinguaCoach.Infrastructure/UsageGovernance/UsageQuotaService.cs`
- `src/LinguaCoach.Infrastructure/UsageGovernance/UsageGovernanceAdminService.cs`
- `src/LinguaCoach.Infrastructure/Ai/AiExecutionService.cs` (modified — quota pre-check + post-record)

### Persistence

- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` (7 new DbSets)
- `src/LinguaCoach.Persistence/Configurations/` (7 new EF config files)
- `src/LinguaCoach.Persistence/Migrations/Phase10R_UsageGovernance/`
- `src/LinguaCoach.Persistence/Seed/UsageGovernanceSeeder.cs`

### API

- `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs`
- `src/LinguaCoach.Api/Middleware/GlobalExceptionMiddleware.cs` (QuotaExceededException → 429)

### Frontend

- `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.ts`

### Tests

- `tests/LinguaCoach.IntegrationTests/UsageGovernance/UsageGovernanceTestFactory.cs`
- `tests/LinguaCoach.IntegrationTests/UsageGovernance/UsageGovernanceIntegrationTests.cs`
