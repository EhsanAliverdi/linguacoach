# Phase 20A — Admin AI Operations Dashboard — Implementation Review

**Date:** 2026-07-02
**Related sprint/phase:** Phase 20A (follows Phase 19C — Review Scaffold Practice Gym Pilot Rollout)
**Review type:** Implementation readiness / completion review

---

## 1. Purpose

The platform has accumulated several independent AI subsystems — speaking evaluation (16H–16J), writing evaluation (17B–17C), generation quality diagnostics (18A-F), AI usage/cost tracking, and readiness-pool/review-scaffold generation (19A–19C) — each with its own admin endpoint and page. There was no single place for an admin to answer "is AI healthy right now?" without visiting five separate pages.

Phase 20A adds a read-only, additive aggregation layer: one new endpoint and one new admin page that pulls from all of the above existing services and presents a combined operational summary. It adds no new AI behaviour, no new scoring, and no new mutation path — it only reads state that already exists.

---

## 2. Files Reviewed / Modified

**Audit (read-only, via subagent research — Part A equivalent):**
- AI usage tracking (`AiUsageHandler`, `IAdminAiUsageHandler`), speaking/writing evaluation quality + signal-safety services, `GenerationValidationFailure`/`AdminGenerationQualityHandler`, `ReadinessPoolReplenishmentOptions`, review-scaffold pilot summary, all existing admin AI/diagnostics controllers and Angular pages, design-system admin component inventory, admin routing/nav convention.

**Application:**
- `src/LinguaCoach.Application/Admin/AdminAiOperationsDtos.cs` (new) — `AdminAiOperationsSummaryDto` and 8 supporting record types

**Api:**
- `src/LinguaCoach.Api/Controllers/AdminAiOperationsController.cs` (new) — `GET /api/admin/ai-operations/summary`

**Angular:**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — new `AdminAiOperationsSummary` and 9 supporting interfaces
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — `getAiOperationsSummary()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-operations/admin-ai-operations.component.ts` / `.html` (new)
- `src/LinguaCoach.Web/src/app/app.routes.ts` — new `admin/ai-operations` route
- `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.html` — new "AI Operations" nav item (both mobile and desktop sidebar blocks) in the existing "AI System" section

**Tests (new):**
- `tests/LinguaCoach.IntegrationTests/Api/AdminAiOperationsSummaryTests.cs` — 9 tests
- `src/LinguaCoach.Web/.../admin-ai-operations.component.spec.ts` — 14 tests

---

## 3. Design Decision: Aggregate, Don't Re-Implement

Every field in the summary is sourced by calling the **existing** service interface for that subsystem, not by re-querying the database with new logic:

| Section | Source service (already DI-registered) |
|---|---|
| Provider/model usage | `IAdminAiUsageHandler.GetSummaryAsync()` |
| Speaking evaluation counts | `ISpeakingEvaluationQualityQuery.GetQualitySummaryAsync()` |
| Speaking signal gates | `ISpeakingEvaluationSignalApplicationService.GetSignalSafetySummaryAsync()` |
| Writing evaluation counts | `IAdminWritingEvaluationQuery.GetQualitySummaryAsync()` |
| Writing signal gates | `IWritingEvaluationSignalApplicationService.GetSignalSafetySummaryAsync()` |
| Generation quality | `IAdminGenerationQualityHandler.GetSummaryAsync(recentDays: 30)` |
| Review scaffold/pilot config | `IOptions<ReadinessPoolReplenishmentOptions>` (same options class as 19A–19C) |

Two small pieces of net-new logic were added, both explicitly called out as thin aggregation, not new business rules:
1. **Pending/approved review-scaffold counts** — two `COUNT()` queries against `StudentActivityReadinessItems`, matching the same predicate as the existing Phase 19C pilot-summary endpoint's `pendingReviewCount`/`approvedCount`, kept deliberately small per the brief's "do not duplicate large readiness pool UI" instruction.
2. **Recent failures table + oldest-pending age** — five small queries (2× oldest-pending `MinAsync`, 2× top-5 failed evaluations, merged with generation quality's already-existing `LatestFailures`) to build the combined "Recent failures" table the brief asked for. No existing service exposed this combined view.

`OverallStatus` (`Healthy`/`Degraded`/`AttentionNeeded`) and the `Warnings` list are computed in the controller from the aggregated data (invariant violations, abandoned-generation warning, elevated failure rates) — this is presentation logic over existing signals, not a new AI/scoring rule.

---

## 4. Backend Endpoint

`GET /api/admin/ai-operations/summary`, `[Authorize(Roles = Admin)]`, read-only, no request body.

Response shape (`AdminAiOperationsSummaryDto`):
- `generatedAtUtc`, `overallStatus`, `warnings[]`, `unavailableSections[]`
- `providerUsage` — totals + per-provider + per-feature breakdown (from existing AI usage log)
- `speakingEvaluationSummary` / `writingEvaluationSummary` — pending/completed/failed/not-supported counts, oldest-pending age, provider info, latest failure reasons
- `generationQualitySummary` — validation failure totals, pattern/CEFR/provider breakdowns, latest failures (capped at 5)
- `readinessPoolAiSummary` — all six review-scaffold/pilot config flags (19A–19C) + pending/approved counts
- `signalGateSummary` — 10 explicit per-pipeline (speaking vs. writing) safety flags + one combined invariant-violation flag
- `recentFailures[]` — up to 15 rows merged from speaking/writing/generation failures, newest first

---

## 5. Safety and Non-Leakage

- Every failure `Reason` string is a **curated failure-reason string already produced by our own evaluation/generation services** (e.g. `SpeakingEvaluation.FailureReason`, `GenerationValidationFailure.ValidationErrors`) — never a raw provider payload, prompt, or transcript. `Truncate()` additionally caps every reason string at 200 characters as defence-in-depth.
- No field anywhere in `AdminAiOperationsSummaryDto` carries a prompt, transcript, raw AI output, storage key, or bearer token — confirmed both by design (no such field was added) and by test (`Summary_DoesNotExposeRawPromptOrProviderSecrets` asserts the raw JSON response never contains `apikey`, `secret`, `bearer `, `sk-`, `"transcript"`, `"prompt"`, etc.).
- Student/evaluation identifiers (`StudentProfileId`, `EvaluationId`) are included in `recentFailures` per the brief's "student/activity/evaluation id where safe" requirement — these are opaque GUIDs, not PII, and the endpoint is admin-only.
- Admin-only via the existing `[Authorize(Roles = Admin)]` convention, identical to every other admin controller in this codebase — verified by test (401 unauthenticated, 403 student token).

---

## 6. Signal Safety Gate Modelling

The brief listed 6 gate bullets, but speaking and writing configure `AllowPositiveSignals`/`AllowReviewSignals` **independently** (confirmed in the Phase 20A audit — `SpeakingEvaluationOptions` and `WritingEvaluationOptions` are separate config sections). Collapsing them into one combined flag per bullet would have hidden real divergence (e.g. speaking positive signals on, writing off). Instead, `AiOperationsSignalGateSummary` exposes 10 explicit per-pipeline booleans (`Speaking*`/`Writing*` pairs for CEFR update, objective completion, Learning Plan auto-regen, positive signals, review signals) plus one combined `AnyInvariantViolationsDetected`. This is more transparent than the brief's literal 6-field suggestion, not a scope deviation — it surfaces strictly more information, never less.

All CEFR-update and objective-completion and Learning-Plan-auto-regen flags are sourced from `CefrUpdatesDisabled`/`ObjectiveCompletionsDisabled`/`LearningPlanAutoRegenDisabled` on the existing `SpeakingSignalSafetySummaryDto`/`WritingSignalSafetySummaryDto` — those are **hardcoded `true`** in `SpeakingEvaluationOptions.AllowCefrUpdate`/`AllowObjectiveCompletion` and the writing equivalents (verified in the audit), so this dashboard cannot report them as enabled even if someone tried — the invariant is structural, not a runtime config value.

---

## 7. Unavailable / Deferred Metrics

Per the brief's "Do not invent cost values" / "do not overbuild" instructions, two sections are explicitly marked unavailable via `unavailableSections`:
1. **`RealTimeJobQueueDepth`** — there is no dedicated job/queue table in this codebase (speaking/writing evaluation run inline or via Quartz batch jobs, not a persisted queue). The pending-status counts already shown are the closest available signal and are surfaced instead.
2. **`CostEstimationForZeroCostOrNoOpProviders`** — cost is only ever shown when already persisted on the AI usage log (`TotalCostUsd`); this dashboard never estimates cost for zero-cost/NoOp provider calls.

Per the brief's explicit "Do not implement" list, the following original roadmap-placeholder scope items were **not** built in this phase (the roadmap's Phase 20A entry has been rewritten to reflect only what shipped — see §9):
- Provider health check / ping endpoint
- Retry tools (re-queue failed evaluations)
- New queue processor
- Fine-grained impression tracking
- Any cost estimation not already backed by persisted data

---

## 8. Admin UI

New page at `/admin/ai-operations` (`AdminAiOperationsComponent`), added to the existing "AI System" sidebar section (next to AI Config, Prompts, AI Usage). Built entirely from existing `sp-admin-*` design-system components — no page-specific button or card styling was introduced:
- `sp-admin-page-header`/`sp-admin-page-body` — page shell
- `sp-admin-loading-state`/`sp-admin-error-state` — load states
- An inline empty-state card (all counters zero) — matches the existing `sp-admin-card` + plain-text pattern used elsewhere for empty states without a dedicated variant
- `sp-admin-kpi-card`/`sp-admin-status-card`/`sp-admin-status-grid`/`sp-admin-badge` — all metric display
- `sp-admin-table` — generation-quality latest-failures table and the combined recent-failures table
- `sp-admin-not-implemented-state` — used both for genuinely-empty sub-sections (e.g. "no provider usage yet") and for the `unavailableSections` list, satisfying the brief's "Not implemented yet" placeholder requirement with the one existing component built for exactly that purpose
- `sp-admin-alert` — warnings banner and the invariant-violation banner

No raw JSON dump anywhere. No hand-rolled KPI/card CSS.

---

## 9. Roadmap Scope Correction

The pre-existing roadmap placeholder for "Phase 20A" (written before this phase started) listed "provider health check endpoint," "retry tools," and "per-feature cost and latency summary" as in-scope. The actual Phase 20A brief received explicitly excluded provider ping and retry tooling ("Do not implement: New retry execution tools," "New queue processor"). The roadmap entry has been rewritten in this update to describe what was actually delivered, and the excluded items are now listed under "known limitations" instead of "scope" so a future phase doesn't assume they already exist.

---

## 10. Tests

**Integration — `AdminAiOperationsSummaryTests.cs`** (new, 9 tests): 401 unauthenticated, 403 student; admin gets 200 with all expected top-level sections present; empty database returns safe zero counts (not an error); seeded speaking failure/completion counts show up correctly and appear in the combined recent-failures table; seeded writing failure shows up correctly; review-scaffold/pilot config flags report the conservative defaults (`false`/`false`/`false`/`true`); all 10 signal-gate flags report the safe default state with no invariant violation; raw response body never contains provider secrets, API keys, bearer tokens, prompts, or transcripts.

**Angular — `admin-ai-operations.component.spec.ts`** (new, 14 tests): loads summary on init; loading state before resolution; error state on failure; empty state when all counters are zero; overall status badge renders; provider usage table renders; speaking/writing operational counts + failure reasons render; safety gate card renders enabled/disabled labels; invariant-violation banner only renders when the flag is true; recent-failures table renders safe fields; "Not implemented yet" renders for unavailable sections; no prompt/secret/API-key text anywhere in the rendered page; review-scaffold/pilot state renders; refresh button reloads.

---

## 11. Validation Run

```
git status                                       → clean tree at start; new/modified files as listed in §2
git diff --check                                 → no whitespace errors
dotnet build                                     → 0 errors
dotnet test tests/LinguaCoach.UnitTests          → 1,704 passed, 0 failed (unchanged — no new unit tests this phase)
dotnet test tests/LinguaCoach.IntegrationTests   → 1,351 passed, 0 failed (+9 — the new AdminAiOperationsSummaryTests)
dotnet test tests/LinguaCoach.ArchitectureTests  → 3 passed, 0 failed (unchanged)
npm run build -- --configuration production      → clean (pre-existing unrelated template/budget warnings only)
npm test -- --watch=false --browsers=ChromeHeadless → 1,520/1,640 passed; 120 pre-existing failures, all in
                                                        AdminStudentDetailComponent (getStudentWritingEvaluations
                                                        mock gap) — a file this phase does not touch, same class
                                                        of failure documented in the Phase 19B/19C reviews;
                                                        0 new regressions. admin-ai-operations.component.spec.ts
                                                        (the only new spec file): 14/14 passing.
```

Playwright: not run this phase. No existing Playwright spec covers admin-diagnostics-style pages cheaply, and this phase's UI (a single read-only aggregation page) is fully covered by the Angular unit tests plus the backend integration suite for the underlying endpoint — same rationale documented in the Phase 19B/19C reviews.

---

## 12. Findings Grouped by Priority

**No blocking findings.**

**Notable design decisions (not defects):**
1. Signal gates modelled as 10 per-pipeline booleans instead of the brief's literal 6-bullet list — more transparent, not a scope reduction (§6).
2. Two genuinely-unavailable metrics explicitly listed rather than approximated (§7).
3. Roadmap Phase 20A placeholder scope (provider ping, retry tools) corrected to match what the actual brief asked for (§9).

---

## 13. Decisions Made

- Reused every existing quality/signal-safety/usage service rather than adding parallel query logic — the only new database queries are the two lightweight readiness-pool counts and the recent-failures/oldest-pending lookups, both explicitly scoped as thin aggregation.
- No AskUserQuestion was needed — the phase brief was fully specified, including an explicit "do not implement" list that resolved the only real scope ambiguity (provider health/retry tooling).

---

## 14. Risks or Unresolved Questions

- None blocking. The 120 pre-existing Angular test failures (`AdminStudentDetailComponent`) are unrelated to this phase and were not introduced or worsened by it.
- If a future phase wants real job-queue depth or retry tooling, `RealTimeJobQueueDepth` is already flagged as a known gap in `unavailableSections` and in this review's §7/§9.

---

## 15. Final Verdict

**Complete.** All acceptance criteria are implemented and tested. No AI scoring, CEFR update, objective completion, Learning Plan regeneration, review-scaffold behaviour, or student-facing flow was changed — this phase only adds a read-only aggregation endpoint and admin page over existing data.

---

## 16. Next Recommended Action

Per the roadmap's Tier 3 priority list, the next phase is **21A — Enterprise SaaS Organisation Model** (requires a dedicated `/plan-eng-review` first per its own scope note) or **22A — Production Operations Hardening**, unless the product owner wants a follow-up phase specifically for provider health-check/retry tooling now that this dashboard's `unavailableSections` has made that gap visible.
