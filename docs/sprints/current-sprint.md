---
status: current
lastUpdated: 2026-06-27 (12G)
owner: engineering
supersedes:
supersededBy:
---

# Current Sprint — SpeakPath

Last updated: 2026-06-27

---

## Active sprint

**Phase 12G — Real-Time Learning Plan Progress Integration** — complete (2026-06-27)

Wires learning plan objective updates directly into the activity submission path. Plan progress now updates immediately after each pattern-keyed activity attempt; no wait for nightly mastery sweep. Background jobs become reconciliation-only.

**Real-time pipeline (Parts B/C):** `ILearningPlanService` gains `TryUpdateObjectiveProgressAsync` — evaluates mastery for a single objective and transitions the plan if evidence is sufficient. Never throws; returns `LearningPlanObjectiveProgressUpdate` with `StatusChanged`, `PreviousStatus`, `NewStatus`, and `Reason`.

**ActivitySubmitHandler wiring (Parts D/E):** `ILearningPlanService` added as a dependency. Two injection points added: after `_learningLedger.RecordAsync` in the pattern evaluation path and the legacy writing path. VocabularyPractice and ListeningComprehension excluded (no learning event recorded in those paths). Helper `TryUpdateLearningPlanProgressAsync` guards on null/empty key and logs when StatusChanged=true.

**Progress summary (Part F):** `LearningPlanProgressSummary` gains three new fields: `CurrentObjectiveKey` (InProgress, or first Active), `NextObjectiveKey` (first Active when InProgress exists; second Active otherwise), and `ObjectivesCompletedToday` (Completed/Mastered since midnight UTC). `GetProgressAsync` populates all three.

**Tests (Part J):** 15 new unit tests in `LearningPlanRealtimeProgressTests`. Updated `LearningPlanDomainTests` test 23 and `LearningPlanCompletionTests` test 8 for expanded record signature.

**Build/test totals:** 0 errors, 0 failures. 1475 unit + 1155 integration + 3 architecture = 2633 total.

Review: `docs/reviews/2026-06-27-phase-12g-realtime-learning-plan-progress-review.md`.

---

## Previous sprint

**Phase 12F — Learning Plan Completion Lifecycle** — complete (2026-06-27)

Closes the Learning Plan lifecycle loop. Objectives now transition deterministically through `Active → InProgress → Completed → Mastered` driven by existing mastery evaluation evidence.

**Completion service (Parts B/C):** `ILearningPlanService` gains `MarkObjectiveCompletedAsync` and `MarkObjectiveMasteredAsync`. Both are idempotent: already-Completed → no-op for a second Completed call; already-Mastered → no-op regardless of incoming signal. Implemented in `LearningPlanService` via shared `TransitionObjectiveAsync` helper. Logs plan exhaustion when no Active/InProgress objectives remain.

**Mastery report (Part B):** `StudentMasteryReport` gains `CompletedObjectiveKeys` — objectives with `NeedsReview` mastery signal (consecutive successes >= 1, avg score 50-79). These also remain in `WeakObjectiveKeys`. `StudentMasteryEvaluationService.EvaluateStudentAsync` populates the new field.

**Event integration (Part D):** `StudentMasteryEvaluationJob` now calls `MarkObjectiveMasteredAsync` for each mastered key and `MarkObjectiveCompletedAsync` for each completed key before triggering `RegeneratePlanAsync`. All completion calls are warning-only; generation continues regardless.

**Progress calculation (Part F):** `LearningPlanProgressSummary` expanded with `TotalObjectives`, `ObjectivesMastered`, `ObjectivesInProgress`, `DeferredObjectives`, `CompletionPercentage` (Completed+Mastered / Total), and `LastCompletedAt`. `MasteryPercentage` now reflects Mastered / Total specifically. `DeterminePhase` uses completion percentage.

**Background jobs (Part I):** No changes required. `GetNextPlannedObjectiveAsync` already filters to `Active` only — completed and mastered objectives are excluded automatically.

**Admin API (Part G):** `GET /api/admin/students/{id}/learning-plan/progress` now returns expanded summary with all new fields. No new endpoints.

**Tests (Part K):** 16 new unit tests in `LearningPlanCompletionTests`. `LearningPlanDomainTests` test 23 updated for new record signature. All 2618 tests pass.

**Build/test totals:** 0 errors, 0 failures. 1460 unit + 1155 integration + 3 architecture = 2618 total.

Review: `docs/reviews/2026-06-27-phase-12f-learning-plan-completion-lifecycle-review.md`.

---

## Previous sprint

**Phase 12E — Learning Plan Guided Routing** — complete (2026-06-27)

Closes the Phase 12D gap where `PreferredObjectiveKey` was passed to routing but never consumed. Curriculum routing now selects the planned objective first when all safety checks pass, and falls back silently to existing routing when they do not.

**Routing (Part B):** `CurriculumRoutingService.RecommendAsync` gains a new pre-step (Step 2c) after `FilterByMastered`. When `PreferredObjectiveKey` is non-null, `TrySelectPreferredObjectiveAsync` validates it against five safety rules (exists in syllabus, CEFR match or one-level-lower with AllowReviewOrScaffold, skill compatibility, runnable, mastery exclusion). Accepted → `RoutingReason.LearningPlan`. Rejected → log only, fall through to existing pipeline unchanged. New enum value `RoutingReason.LearningPlan = 5`.

**Status lifecycle (Part C):** `LearningPlanObjectiveStatus.InProgress = 6` added. `StudentLearningPlanObjective.MarkInProgress()` transitions `Active → InProgress`. `ILearningPlanService.MarkObjectiveInProgressAsync` added and implemented in `LearningPlanService`.

**Background jobs (Part D):** `LessonBatchGenerationJob` and `PracticeGymGenerationJob` both call `MarkObjectiveInProgressAsync` after routing returns `LearningPlan` reason. Failures are warning-only; generation continues regardless.

**Admin routing preview (Part E):** `AdminRoutingPreviewRequest` gains optional `PreferredObjectiveKey`. `PreviewRoutingAsync` passes it through and returns `PreferredObjectiveDisposition` ("accepted" / "rejected" / "fallback_used" / null) in `AdminRoutingPreviewResult`. Rejected hint adds a warning to the response.

**Tests (Part F):** 15 new unit tests in `CurriculumRoutingServicePreferredObjectiveTests` covering all acceptance/rejection rules, fallback, CEFR safety, mastery exclusion, and override-of-score-based-selection. `LearningPlanDomainTests` updated for new enum count (6 → 7).

**Build/test totals:** 0 errors, 0 failures. 1444 unit + 1155 integration + 3 architecture = 2602 total.

Review: `docs/reviews/2026-06-27-phase-12e-learning-plan-guided-routing-review.md`.

---

## Previous sprint

**Phase 12D — Learning Plan Orchestrator Foundation** — complete (2026-06-27)

Introduces a deterministic Learning Plan Orchestrator that coordinates curriculum routing, mastery evaluation, and readiness pool replenishment into a coherent per-student objective sequence. No AI calls in the plan layer. No student UI changes. No ReviewScaffold global enable. Reuses and wraps existing routing/mastery/readiness infrastructure.

**Domain (new):** `StudentLearningPlan` + `StudentLearningPlanObjective` entities with lifecycle methods (`MarkReady`, `Supersede`, `StartRegeneration`, `MarkCompleted`, `MarkMastered`, `Unblock`, etc.). Enums: `LearningPlanStatus` (Active/Regenerating/Superseded), `LearningPlanObjectiveStatus` (Active/Completed/Mastered/Blocked/Deferred/Review).

**Application (new):** `ILearningPlanService` interface with `GetOrCreatePlanAsync`, `RegeneratePlanAsync`, `GetProgressAsync`, `GetNextPlannedObjectiveAsync`, `GetPracticeGymObjectivesAsync`. DTOs: `LearningPlanSummary`, `LearningPlanProgressSummary`, `PlannedObjectiveContext`. `LearningPlanOptions` config class (`PlannedLessonCount=10`, `MasteryCompletionThreshold=70`). Added `PlanGeneration=5` to `MasteryEvaluationReason` enum. Added `PreferredObjectiveKey` to `CurriculumRoutingRequest` + factory.

**Infrastructure (new):** `LearningPlanService` — deterministic implementation. Builds 10-objective sequence from skill rotation, inserts review objectives for weak/mastered keys, prevents duplicates, persists plan + objectives.

**Persistence:** EF Core configurations for both new entities (`student_learning_plans`, `student_learning_plan_objectives`). Migration T61 (`T61_LearningPlanOrchestrator`). Two new `DbSet<>` properties on `LinguaCoachDbContext`.

**Regeneration triggers (F/G/H):** `StudentMasteryEvaluationJob` calls `RegeneratePlanAsync("mastery_sweep")` when mastery changed (demoted or newly mastered). `ProfileCommandHandler` calls `RegeneratePlanAsync("preference_change")` after student preference update. `AdminHandler.SetStudentCefrAsync` calls `RegeneratePlanAsync("cefr_change")` when CEFR level changes. All triggers are fire-and-forget with warning-only failure logging.

**Admin visibility (I):** `GET /api/admin/students/{id}/learning-plan` and `GET /api/admin/students/{id}/learning-plan/progress` endpoints on `AdminReadinessPoolController`. Read-only, admin-only, no side effects.

**Background job integration (J):** `LessonBatchGenerationJob` calls `GetNextPlannedObjectiveAsync` and passes `preferredObjectiveKey` to routing. `PracticeGymGenerationJob` calls `GetPracticeGymObjectivesAsync(maxCount:1)` and passes `preferredObjectiveKey`. Both fall back to free routing on plan failure.

**Tests:** 30 unit tests (`LearningPlanDomainTests`) + 8 integration tests (`LearningPlanIntegrationTests`). All 2587 tests pass (3 arch + 1429 unit + 1155 integration). No Angular changes.

Review: `docs/reviews/2026-06-27-phase-12d-learning-plan-orchestrator-foundation-review.md`.

---

**Phase 12C — Prepared Lesson Pipeline and Readiness Lifecycle** — complete (2026-06-27)

Configurable buffer bounds and enhanced pool observability. Added `MinimumReadyThreshold` (default 3) and `MaxBufferCount` (default 20) to `ReadinessPoolReplenishmentOptions`. `MaxBufferCount` enforces a hard cap on active items (Queued + Generating + Ready + Reserved) per student per source — `FillShortfallAsync` returns `(0, 0, toCreate)` immediately when already at cap, and caps `toCreate = min(toCreate, MaxBufferCount - activeCount)` otherwise. `ReplenishmentRunSummary` extended with `SkippedAtMaxBuffer`, `ElapsedMs` (computed), and `GenerationSuccessRate` (computed). Replenishment completion log includes `elapsedMs` and `successRate` for per-run observability. `AggregatePoolHealthSummary` extended with `StudentsBelowMinimumThreshold` (students with Ready < `MinimumReadyThreshold`, including zero-ready) and `AverageReadyPerStudent`. Admin Lessons UI displays both new metrics in the aggregate pool health stat grid. 17 new tests (12 unit + 5 integration) — 1399 unit + 1147 integration + 3 arch + 1384 Angular = 3933 all passing. No migration. No student UI changes. No ReviewScaffold global enable. Review: `docs/reviews/2026-06-27-phase-12c-prepared-lesson-pipeline-readiness-lifecycle-review.md`.

---

**Phase 12A — Production Gap Closure: Pool Health and Welcome Email** — complete (2026-06-27)

Closed two production gaps found in Phase 11E live admin QA. F-04: Added `GET /api/admin/readiness-pool/health` endpoint returning `AggregatePoolHealthSummary` (all status counts + per-status student counts, computed in one DB round-trip). Admin Lessons page replaced placeholder card with real stat grid — loading/error states, 8 status metrics, failure alert, refresh button. F-03: Audited `CreateStudentHandler` — welcome email already wired via `QueueEmailAsync` using `account.student_created` template; no backend changes. Part 0 audit: `RoutingEmailSender`, `ResendEmailSender`, `SendGridEmailSender`, `NotificationChannelConfigResolver`, DI registrations, and Angular admin integrations UI — all clean, no raw passwords logged or emailed. Added 6 email routing unit tests (`TrackingServiceProvider` pattern), 5 aggregate pool health integration tests, 3 Angular pool health component tests. Fixed 3 pre-existing test gaps: missing `provider: 'Smtp'` in notifications spec, stale `'SMTP / Email'` assertion in integrations spec, missing `getAggregatePoolHealth` spy in lessons spec. 31 new tests total — 1362 unit + 1113 integration + 3 arch + 1384 Angular = 3862 all passing. No migration. No student UI. Review: `docs/reviews/2026-06-27-phase-12a-pool-health-and-welcome-email-review.md`.

---

**Phase 11B — Curriculum Objective Coverage and Mapping Hardening** — complete (2026-06-26)

Curriculum hardening across Parts A–K. Seed expanded from 22 to 33 objectives, closing all A1–B2 × {speaking, listening, reading, writing, grammar, vocabulary, pronunciation} coverage gaps. New: `ICurriculumValidationService` (Application) + `CurriculumValidationService` (Infrastructure) with 10 validation checks (duplicate keys, invalid CEFR/skill, missing fields, dangling/circular/disabled prerequisites, invalid context/focus tags, coverage gaps, non-runnable skill warnings). `ActivityCompatibilityConstants` documents runnable vs planned skill/exercise format mapping. Routing hardened: `CurriculumRoutingRequest` adds `MasteredObjectiveKeys` and `AllowReviewOfMastered` fields so mastered objectives can be excluded from new-learning routes. Two new admin endpoints: `GET /api/admin/curriculum/validation` and `GET /api/admin/curriculum/coverage`. Admin curriculum UI at `/admin/curriculum` shows live validation summary card, error/warning alerts, and coverage gap list — all from real backend data. 12 new unit tests + 3 routing tests + 2 integration tests + 10 Angular tests = 27 new tests. All 1344 unit / 1103 integration / 3 arch / 1381 Angular tests pass. No migration. No student UI. No admin visual redesign. Review: `docs/reviews/2026-06-26-phase-11b-curriculum-objective-coverage-mapping-hardening-review.md`.

---

**Phase 11A — Admin Onboarding Builder** — complete (2026-06-26)

Admin-configurable onboarding system. Admins can create onboarding flow configurations, define/manage/reorder step definitions, and activate a flow from `/admin/onboarding`. No student UI changes. Backend: 7 new handler interfaces + implementations (list flows, create flow, activate flow, add/update/remove/reorder steps), 9 new API endpoints on `AdminOnboardingController`, `Navigation().HasField("_steps")` EF config fix, domain methods `Deactivate`/`RemoveStep`/`ReorderSteps`/`Update`/`SetOrder` added. Frontend: `admin-onboarding.models.ts`, `admin-onboarding.service.ts`, `admin-onboarding` feature page with KPI strip + step table + add/edit slide-over, `/admin/onboarding` route, sidebar nav item. 8 new integration tests. All 2418 tests pass. No migration required (schema pre-existing). Review: `docs/reviews/2026-06-26-phase-11a-admin-onboarding-builder-review.md`.

---

**Phase 10Z — Mastery Re-evaluation Engine** — complete (2026-06-26)

Deterministic mastery engine evaluating student skill/objective mastery from learning event history. No AI calls. Adds `MasteryStatus` and `ReadinessDemotionDecision` enums (Domain), `ObjectiveMasterySignal`/`StudentMasteryReport`/`MasteryOptions` types (Application), `IStudentMasteryEvaluationService` interface (Application), `StudentMasteryEvaluationService` Infrastructure implementation, `StudentMasteryEvaluationJob` Quartz job (daily sweep), `MasteryOptions` config section in appsettings.json, `MasteredCount`/`NeedsReviewCount`/`LastEvaluatedAtUtc` added to `ReadinessPoolSummary` DTO, DI registration in `DependencyInjection.cs`, and Quartz schedule in `QuartzConfiguration.cs`. 11 new unit tests — all 1329 unit tests pass. Build clean. No migration needed (no new DB columns). No student UI changes. No admin UI changes. No AI calls. Review: `docs/reviews/2026-06-26-phase-10z-mastery-engine-review.md`.

---

**Phase 10Y — Learning Activity Lifecycle Completion** — complete (2026-06-26)

Backend-only lifecycle hardening for the readiness pool. Gaps filled: `Skipped` terminal status (mastered/irrelevant items), CEFR mismatch stale demotion sweep in replenishment service, `ReservedCount` and `SkippedCount` surfaced in pool health and admin diagnostics, `LastEvaluatedAtUtc` timestamp for incremental evaluation tracking, EF migration T60. 13 new tests (8 unit + 5 integration) — 1318 unit + 64 targeted integration all pass. No student UI changes. No admin visual redesign. No fake data. Review: `docs/reviews/2026-06-26-phase-10y-learning-activity-lifecycle-completion-review.md`.

---

**Phase 10UI-STYLE-1 — Admin Reusable Component Visual Alignment** — complete (2026-06-23)

Admin design tokens updated to match SpeakPath brand: text/muted/dim colours → warm purple ink palette; green → brand green (`#13B07C`); shadows → purple-tinted. KPI card: value 28px/800, label 800 weight, icon 40px. Card: title CSS token style, default radius 14px. Badge: font-weight 700. Data table header: muted colour, 800 weight. Page header: tighter letter-spacing. Global styles: 11 new `.sp-adm-*` slide-over/form utility classes. Notifications: 29 Tailwind literals in slide-overs replaced. Integrations: 3 Tailwind literals replaced. All 14 admin routes benefit automatically. 1253/1253 PASS. Build clean. No backend changes. No data logic changes.

---

**Phase 10UI-REDESIGN-FINAL — Admin UI Reference Alignment Closure Audit** — complete (2026-06-23)

Route-by-route closure audit of the full admin UI redesign epic (REDESIGN-1 through REDESIGN-8). One structural bug found and fixed: notifications `sp-admin-page-header` was incorrectly nested inside `sp-admin-page-body` (same fix as Security in REDESIGN-8). 2 new tests added. Full audit confirmed: no fake production data anywhere, no secrets displayed, all 14 admin routes use correct `sp-admin-*` components, all KPI strips derive from real backend data or show explicit "Backend not available yet" labels. 1253/1253 PASS. Production build clean. Admin UI redesign epic closed. Review: `docs/reviews/2026-06-23-phase-10ui-redesign-final-admin-ui-reference-closure-audit.md`.

---

**Phase 10UI-REDESIGN-3 — Student Detail Reference Redesign** — complete (2026-06-23)

Hero section added: coloured initials avatar (hash-based colour, 8-palette), student display name with email/name fallbacks, lifecycle/onboarding/CEFR/support-language badges, action group (Edit, Reset password, Send reset link, Pause/Unpause/Reactivate). KPI strip upgraded from sp-admin-stat-card to sp-admin-kpi-card with icon tiles (Lifecycle, Onboarding, CEFR, Pool health). Danger zone card added as full-width last section (Reset data, Archive, Reactivate — all wired to existing modal flows). Back-to-students link in page header. 30 new tests. 1168/1168 pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-redesign-3-student-detail-reference-redesign.md

---

**Phase 10UI-REDESIGN-2 — Students List and Create Student Reference Redesign** — complete (2026-06-23)

Students list redesigned: 4-tile KPI summary strip (Total students, Onboarded, Activities tracked, Showing this page) using real `getStats()` data, rows-per-page selector (10/25/50/100), filter bar aligned to reference. Create Student redesigned: two-column layout (multi-section form cards left, sticky "What happens next" aside right), back-to-students link, security note about one-time password, Welcome email not-available-yet note. All original fields, validation, submit payload, and modal actions preserved. 21 new tests + 8 migration spec spy fixes. 1138/1138 pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-redesign-2-students-create-reference-redesign.md

---

**Phase 10UI-REDESIGN-1 — Admin Dashboard Reference Redesign** — complete (2026-06-23)

Dashboard fully redesigned to match the SpeakPath admin reference layout. Dark hero weekly-snapshot banner (2 real slots, 2 placeholders). 5-tile KPI icon row (3 real, 1 live AI config, 1 placeholder). Onboarding funnel, at-risk students, and CEFR distribution derived from students list. All unavailable sections (activity chart, score distribution, AI spend, streak, avg session, live events) show explicit "Not implemented" / "Backend not available yet" placeholders. No fake data. 31 new tests + 1 migration spec updated. 1117/1117 pass.

Review: docs/reviews/2026-06-23-phase-10ui-redesign-1-dashboard-reference-redesign.md

---

**Phase 10UI-REDESIGN-0 — Admin Reference Redesign Rollout Plan** — complete (2026-06-23)

Full route-by-route redesign plan for all 14 admin routes. Reference design files read and compared against current Angular components. Page redesign matrix, component gap matrix, and phase sequence produced. No Angular source changes. Two P1 gaps confirmed: dashboard (Old layout — 4 stat cards only) and integrations (domain mismatch — reference SMTP/Webhook/Slack/Analytics/Admin API not surfaced). Next phase: **10UI-REDESIGN-1** (dashboard reference redesign).

Review: docs/reviews/2026-06-23-phase-10ui-redesign-0-admin-reference-redesign-rollout-plan.md

---

**Phase 10UI-FIX-8 — Notifications, Security, and Integrations Admin Polish** — complete (2026-06-23)

SMS channel and config card: Foundation-only badge and warning alert. Security: deferred capabilities card listing MFA, enterprise SSO, distributed rate limiting, captcha, CSP/HSTS, SMS notifications, admin session management. Integrations: readiness pool aggregate placeholder card. 12 new tests. 1086/1086 pass.

Review: docs/reviews/2026-06-23-phase-10ui-fix-8-notifications-security-integrations-polish.md

---

**Phase 10UI-FIX-7 — Student Detail Reference Alignment, Readiness Pool, Activity History** — complete (2026-06-23)

Full wrapper migration for `/admin/students/:id`. Page header migrated to `sp-admin-page-header` with projected action buttons. All `<section class="sp-admin-table-card">` cards replaced with `sp-admin-card`. All raw badge class strings replaced with `sp-admin-badge` wrappers using `lifecycleTone`/`onboardingTone` utils. Activity and audit history tables migrated to `sp-admin-table`. Error states migrated to `sp-admin-alert`. New KPI strip (lifecycle, onboarding, CEFR, pool health stat cards). New readiness pool health section (TODO-UI-02) wiring `GET /api/admin/students/{id}/readiness-pool/health` with today/gym breakdown and replenishment badges. `StudentReadinessPoolHealth` DTO added to `admin.models.ts`; `getStudentReadinessPoolHealth()` added to `AdminApiService`. 9 new pool health tests. 1074/1074 Angular tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-7-student-detail-reference-alignment.md

---

**Phase 10UI-FIX-6 — Students Header, Create Student Wrapper Migration, Curriculum Filter Bar** — complete (2026-06-23)

Students list "Create student" header action migrated from raw `<a class="sp-admin-btn-primary">` to `sp-admin-button`. Create Student page fully migrated to `sp-admin-*` wrappers (page-header, page-body, card, form-field, input, alert, button); native selects retained for `[ngValue]` number/null bindings. Curriculum filter bar migrated from raw `<select class="sp-input">` to `sp-admin-select` with computed option arrays; page body wrapped in `sp-admin-page-body`. 20 new tests. 1065/1065 Angular tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-6-students-create-curriculum-alignment.md

---

**Phase 10UI-FIX-5 — Careers Route Redirect and Dashboard AI Provider Accuracy** — complete (2026-06-23)

Deleted stale `AdminUsageComponent` (emoji placeholder, never routed). Redirected `/admin/careers` → `/admin/curriculum`. Replaced static dashboard "AI provider: Configured" stat card with live status derived from `listAiCategories()` — shows "Configured" / "N/M configured" / "Not configured" / "Unknown" based on real backend state. Replaced hardcoded "Active" AI System card rows with live per-category loop showing real provider names. 10 new dashboard tests + 2 wrapper-migration mock fixes. 1045/1045 Angular tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-5-careers-dashboard-provider-accuracy.md

---

**Phase 10UI-FIX-4 — Admin Page-Level Spot-Check and Alignment Plan** — complete (2026-06-23)

All 14 admin routes inspected against reference design, backend capability, wrapper usage, and stale/misleading UI. Tiny safe fixes applied: tab bar `blue-600`→`indigo-600` on notifications page; `#4338CA`/`#465fff` CSS literals replaced with `var(--sp-admin-primary,#5B4BE8)` token in dashboard, careers, student-detail (x2), students, and usage-policies components. Four planning matrices produced: page matrix (A), wrapper misuse matrix (B), stale UI matrix (C), and recommended phases (D). Next phases: 10UI-FIX-5 (dashboard static cards + careers orphan), 10UI-FIX-6 (student list + create-student + curriculum wrapper migration), 10UI-FIX-7 (student detail full wrapper migration). 1035/1035 tests pass. Build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-4-admin-page-level-spot-check.md

---

**Phase 10UI-FIX-3 — Admin Core Component Visual Alignment** — complete (2026-06-23)

All reusable `sp-admin-*` component CSS aligned to SpeakPath indigo token palette. Replaced TailAdmin-blue hardcoded literals (`#465fff`, `#ecf3ff`, `#93c5fd`, `#e5e7eb`, `#f9fafb`, `#111827`, `#6b7280`, `#374151`) with `var(--sp-admin-*)` tokens throughout: card, button, badge, stat-card, empty-state, table, input, select, pagination, slide-over, modal, textarea, number-input, checkbox, drawer, copyable-text, code-pill, alert, kpi-card, section-card, section-header, table-actions, status-card. Dark-mode panel backgrounds (`#111827`, `#1f2937`) preserved as intentional. 1035/1035 tests pass. Production build clean.

Review: docs/reviews/2026-06-23-phase-10ui-fix-3-admin-core-component-visual-alignment.md

---

**Phase 10UI-FIX-2 — Admin Shell, Sidebar, and Header Visual Alignment** — complete (2026-06-23)

Shell/sidebar/header aligned to SpeakPath reference design. Brand palette shifted to `#5B4BE8` family. Sidebar narrowed 290→240px / 90→64px collapsed. Header height fixed to 60px. Border color to lavender `#ECE9F5`. Background to warm `#F6F4FB`. Active nav indicator bar (left stripe) added. Nav item replaced TailAdmin `menu-item` utilities with `sp-nav-*` SpeakPath classes. Section labels tightened. All 12 required nav routes confirmed present in both desktop sidebar and mobile drawer. Logout confirmed absent from sidebar. 1035/1035 Angular tests pass.

Review: docs/reviews/2026-06-23-phase-10ui-fix-2-admin-shell-sidebar-header-alignment.md

---

**Phase 10UI-FIX-1 — Admin Navigation P0 Fix and Design Reference Registration** — complete (2026-06-23)

Added sidebar nav links for `/admin/usage-policies` (Usage Policies) and `/admin/curriculum` (Curriculum) to both desktop sidebar and mobile drawer in `admin-app-layout.component.html`. Registered SpeakPath reference design pack as source-of-truth in `docs/design/admin-reference-alignment.md`. Production build passes. TODOS updated (TODO-UI-01 closed, TODO-UI-01B + design reference TODOs added).

---

**Phase 10UI-AUDIT-0 — Full UI / Backend Capability Reconciliation** — complete (2026-06-23)

Audit-only phase. No code changed. Route-by-route and capability-by-capability reconciliation across all admin and student routes against backend capabilities.

Key findings:
- P0: `/admin/usage-policies` and `/admin/curriculum` are complete production pages with NO sidebar nav links. Admins cannot find them without typing the URL directly.
- P1: Student detail page missing readiness pool health, activity history section, and onboarding flow viewer.
- P1: Orphan `AdminUsageComponent` (admin-usage folder) renders stale placeholder — dead code contradicting the real AI usage page.
- P1: Admin dashboard "AI provider: Configured" stat card is always static.
- P2: SMS shown in notification config without "foundation only" label. CEFR not shown on student progress page. No Google login button on /login.

Next recommended phase: **10UI-FIX-1** — add three missing nav links (single HTML file, 1-2 hours).

Review: docs/reviews/2026-06-23-phase-10ui-audit-0-ui-backend-capability-reconciliation.md
TODOs added: TODO-UI-01 through TODO-UI-10 in TODOS.md.
Gates: no code changed.

---

**Phase 10Auth-F-FINAL — Enterprise Auth/Security Closure Audit** — complete (2026-06-23)

Audit-only phase. All 6 implementation phases (F-1 through F-6) verified closed. 23 auth event types confirmed. All security invariants confirmed: no secrets/tokens in audit metadata, logs, or API responses. Refresh tokens hash-only with rotation and reuse detection. Session revocation wired into password change, reset, and logout. Deferred items documented (CSP, HSTS, distributed rate limiting, MFA, SMS, cloud KMS). Next recommended phase: 10UI-AUDIT-0.

Gates: 2369/2369 .NET | 1025/1025 Angular | build clean.
Review: docs/reviews/2026-06-23-phase-10auth-f-final-enterprise-auth-security-closure-audit.md

---

**Phase 10Auth-F-6 — Admin Security Settings UI and Read-Only Auth Visibility** — complete (2026-06-23)

`GET /api/admin/security/settings` read-only endpoint (Admin-role only) returning password policy, lockout, rate limit summary, JWT/refresh token config, security headers status, Google external login config — no secrets returned. `GET /api/admin/security/auth-events` aliasing the existing F-2 auth event handler. Angular admin page at `/admin/security` with Overview and Auth Events tabs, using sp-admin-* wrapper components. Security nav item added to admin sidebar (System section). No new migrations. 16 new frontend unit tests.

Review: docs/reviews/2026-06-23-phase-10auth-f-6-admin-security-settings-ui.md

---

**Phase 10Auth-F-5 — Google OAuth / External Login Foundation** — complete (2026-06-23)

`IGoogleTokenValidator` abstraction (testable without real Google API calls), `IExternalLoginService`, `ExternalLoginService` (link/provision/issue JWT + refresh tokens). `GoogleExternalLoginOptions` bound from `Authentication:ExternalProviders:Google` (disabled by default, no public auto-provisioning). Endpoint: `POST /api/auth/external/google`, rate-limited `AuthExternalLogin` (20/5min/IP). Account linking via Identity `AspNetUserLogins` — no new migration. Auto-link by email when `AllowAutoLinkByEmail=true`. Domain restriction via `AllowedDomains`. 7 new audit event types. `account.external_login_linked` notification templates (InApp+Email). 20 new integration tests with `FakeGoogleTokenValidator`. 2369/2369 pass.

Review: docs/reviews/2026-06-23-phase-10auth-f-5-google-oauth-external-login-foundation.md

---

**Phase 10Auth-F-4 — Refresh Tokens and Session Management** — complete (2026-06-23)

`UserRefreshToken` entity, migration T59, `IRefreshTokenService` (hash-only storage, rotation, reuse detection, revoke-all). Login issues refresh token. Password change/reset revokes all sessions. Endpoints: `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/revoke-sessions`. `AuthRefresh` rate limiter (30/5min/IP). 6 new audit event types. 20 new integration tests. 2349/2349 pass. No frontend changes.

Review: docs/reviews/2026-06-23-phase-10auth-f-4-refresh-tokens-session-management.md

---

**Phase 10Auth-F-3 — Security Notifications** — complete (2026-06-23)

Security notifications wired into all auth flows. Templates added: `account.password_changed` (InApp+Email), `account.password_reset_requested` (InApp only — reset-link email serves the email role), `account.password_reset_succeeded` (InApp+Email), `account.locked_out` (InApp+Email). All use `NotificationCategory.Account` (mandatory, bypasses opt-out). Lockout notification fires only on transition — not on every locked-out login attempt. Notification failures are non-fatal. No new migration. 11 new integration tests. 2330/2330 pass.

Review: docs/reviews/2026-06-23-phase-10auth-f-3-security-notifications.md

---

**Phase 10Auth-F-2 — Auth Event Audit Log** — complete (2026-06-23)

`AuthSecurityEvent` entity, migration T58, `IAuthSecurityAuditService` (non-fatal, never logs secrets), audit integration in LoginHandler/ChangePasswordHandler/PasswordResetHandler/CreateStudentHandler. `GET /api/admin/auth-events` (paginated, filterable by userId, email, eventType, outcome, date range). Rate limiter split: `AuthReset` (3/15min, unauthenticated reset-link) and `AuthChangePassword` (10/5min, authenticated, keyed on userId). 16 new integration tests. 2319/2319 pass.

Review: docs/reviews/2026-06-23-phase-10auth-f-2-auth-event-audit-log.md

---

**Phase 10Auth-F-1 — Immediate Auth/Security Hardening** — complete (2026-06-23)

Lockout (5 attempts / 15 min), IP rate limiting on login (10 req/5 min) and reset/change-password (3 req/15 min), password policy hardened (10 chars, upper+lower+digit+special), `SecurityHeadersMiddleware` (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy). `LoginHandler` updated to use `AccessFailedAsync`/`IsLockedOutAsync`/`ResetAccessFailedCountAsync`. 13 new auth security integration tests. 2304/2304 tests pass. No migrations, no UI changes.

Review: docs/reviews/2026-06-23-phase-10auth-f-1-immediate-auth-security-hardening.md

---

**Phase 10Auth-F-0 — Enterprise Auth/Security Gap Check** — complete (2026-06-23)

Audit of authentication, authorization, password, session, token, reset-password, and account-security implementation. No code changes. Roadmap defined for 10Auth-F-1 through 10Auth-F-FINAL. Critical gap: no brute-force/lockout protection. Next phase: 10Auth-F-1 (lockout + rate limiting + password policy hardening, ~1 day, no migration).

Review: docs/reviews/2026-06-23-phase-10auth-f-0-enterprise-auth-security-gap-check.md

---

**Phase 10W-FINAL-2 — Notification Platform Re-closure Audit** — complete (2026-06-23)

Re-audit after 10W-5C-2 through 10W-5C-5 (DB config, secret encryption, key persistence, key-at-rest cert protection). One stale comment corrected in `AdminNotificationHandler.cs`. All 24 audit checks pass. 2291 .NET / 1004 Angular tests pass. Platform is production-ready for in-app and email on single-host Docker.

Review: docs/reviews/2026-06-23-phase-10w-final-2-notification-platform-reclosure-audit.md

---

**Phase 10W-5C-5 — Data Protection Key-at-Rest Certificate Protection** — complete (2026-06-23)

`DataProtectionKeyMode` enum (None/Certificate). `ProtectKeysWithCertificate` when mode=Certificate. PFX file via `X509CertificateLoader` or Windows store thumbprint. Fail-fast on misconfiguration. `.gitignore` updated for `*.pfx`/`*.p12`. 10 new unit tests. 2291 .NET / 1004 Angular pass.

Review: docs/reviews/2026-06-23-phase-10w-5c-5-data-protection-key-encryption-hardening-review.md

---

**Phase 10W-5C-4 — Data Protection Key Persistence** — complete (2026-06-23)

`NotificationKeyProtectionOptions` bound from `DataProtection` appsettings section. `PersistKeysToFileSystem` at DI registration time. Directory auto-created; degrades gracefully on error. Docker `dp_keys` named volume. `.gitignore` updated. 5 new unit tests. 2281 .NET / 1004 Angular pass.

Review: docs/reviews/2026-06-23-phase-10w-5c-4-data-protection-key-persistence-review.md

---

**Phase 10W-5C-3 — Runtime Notification Config Resolver + Secret Encryption** — complete (2026-06-23)

`ISecretProtector`/`DataProtectionSecretProtector` (ASP.NET Core Data Protection). `INotificationChannelConfigResolver`/`NotificationChannelConfigResolver` (DB wins over appsettings). `SmtpEmailSender` decoupled from `IOptions<EmailOptions>` — resolves config at send time. `TestEmailAsync` uses resolver. Base64 fallback on unprotect for backward compat. 7 new unit tests. 2276 .NET / 1004 Angular pass.

Review: docs/reviews/2026-06-23-phase-10w-5c-3-runtime-config-resolver-secret-encryption-review.md

---

**Phase 10W-5C-2 — DB-Backed Notification Channel Configuration** — complete (2026-06-23)

`NotificationChannelConfig` entity + migration `T57`. Hybrid config resolution (DB wins, appsettings fallback). `GET notifications/config` returns V2 with `source` field. `PUT /email`, `/sms`, `/in-app` endpoints. Secrets write-only (hasPassword/hasApiKey booleans only in API). Admin UI editable forms, source badge, secret replace-only UX. 25 backend integration + 9 frontend tests. 978 .NET integration / 1004 Angular pass.

Review: docs/reviews/2026-06-22-phase-10w-5c-2-db-backed-notification-channel-configuration-review.md

---

**Phase 10W-FINAL — Notification Platform Closure Audit** — complete (2026-06-22)

Full audit of the 10W notification platform. All 14 sub-phases verified closed. 2246 .NET + 1011 Angular tests pass. No bugs found. Docs updated. Deferred TODOs: TODO-10W-5D-UNIQUE-CONSTRAINT, TODO-10W-PHONE, TODO-10W-SMS-PROVIDER.

Review: docs/reviews/2026-06-22-phase-10w-final-notification-platform-closure-audit.md

---

**Phase 10W-6 — SMS Provider Foundation** — complete (2026-06-22)

`ISmsSender` / `SmsMessage` / `SmsSendResult` abstraction. `DisabledSmsSender` always skips safely. `SmsOptions` config class (ApiKey never returned to frontend, `HasApiKey` bool only). `AdminSmsConfigStatus` DTO. `NotificationDispatchService` wired with `ISmsSender`. Admin config tab shows SMS detail card.

Review: docs/reviews/2026-06-22-phase-10w-6-sms-provider-foundation-review.md

---

**Phase 10W-PREFS — Notification Preferences Foundation** — complete (2026-06-22)

`NotificationPreference` entity + migration T56. `INotificationPreferenceService`. Account/System categories required (cannot be disabled). SMS deferred (always false). User GET/PUT API. Admin read API. Profile section with category×channel table, SMS "Coming soon", required badge, save. 12 backend + 11 frontend tests.

Review: docs/reviews/2026-06-22-phase-10w-prefs-notification-preferences-foundation-review.md

---

**Phase 10W-5D-RESET-INTEGRATION — System emails use templates** — complete (2026-06-22)

`PasswordResetHandler` and `CreateStudentHandler` use `account.password_reset` / `account.student_created` Email templates. Safe fallback to hard-coded content when template missing/inactive. Token never in metadata/audit/log/API response.

Review: docs/reviews/2026-06-22-phase-10w-5d-reset-template-integration-review.md

---

**Phase 10W-5D — Notification Templates Foundation** — complete (2026-06-22)

`NotificationTemplate` entity + migration T55. `INotificationTemplateRenderer` (`{{VarName}}` replacement). `IAdminTemplateHandler` CRUD + preview. 4 default templates seeded. Admin Templates tab: list, create/edit slide-over, preview panel.

Review: docs/reviews/2026-06-22-phase-10w-5d-notification-templates-foundation-review.md

---

**Phase 10W-4C — Notification Dropdown Source Control Fix** - complete (2026-06-21)

Goal: move live notification bell/dropdown from gitignored vendor template path into committed app source so it survives a fresh clone.

### Delivered

- `NotificationDropdownComponent` at `src/app/design-system/student/notification-dropdown/` (committed, selector `sp-notification-dropdown`).
- Full 10W-3 behavior: live list, unread count, mark read, mark all read, archive/dismiss, loading/empty/error/retry states, deep-link navigation.
- Click-outside via `@HostListener` — no dependency on gitignored `DropdownComponent`.
- `StudentAppLayoutComponent` updated: imports + uses `<sp-notification-dropdown>` replacing static bell button.
- 17 new frontend unit tests. Gates: `npm run build` clean, 942/942 Angular tests pass.
- Gitignored `src/app/templates/` no longer depended on by any committed code.

---

**Phase 10W-4B — Token-Based Password Reset Link** - complete (2026-06-21)

Goal: admin triggers a password reset link email to a student. Student clicks link and sets new password via a public Angular page. Token never returned to admin, never stored in logs or metadata.

### Delivered

- `IPasswordResetService` + `SendPasswordResetLinkCommand` + `CompletePasswordResetCommand` + `CompletePasswordResetResult` in `LinguaCoach.Application/Auth/`.
- `PasswordResetHandler` in `LinguaCoach.Infrastructure/Auth/`: generates ASP.NET Identity token, Base64Url-encodes it, builds reset link with `PublicApp:BaseUrl`, queues email outbox item. Token never logged. Generic error returned on failure (no info leak).
- `POST /api/admin/students/{id}/send-reset-link` (admin auth required). Returns 204.
- `POST /api/auth/reset-password` (public, no auth). Validates passwords match, calls `CompleteResetAsync`, clears `MustChangePassword` on success. Generic errors on all failure paths.
- `PublicApp:BaseUrl` added to `appsettings.json` (default `http://localhost:4200`).
- DI: `services.AddScoped<IPasswordResetService, PasswordResetHandler>()`.
- Frontend: `ResetPasswordComponent` at `/reset-password` — reads `userId`+`token` from query params, validates, calls `auth.resetPassword()`. Success/error states with signals.
- Admin: `sendResetLink(student)` method + `sendingResetLink`/`resetLinkSent` signals in `AdminStudentDetailComponent`. "Send reset link" button in student detail header actions.
- `AdminApiService.sendStudentResetLink()` added.
- `AuthService.resetPassword()` + `ResetPasswordRequest` model added.
- Frontend specs: `reset-password.component.spec.ts` (7 tests), `admin-student-detail` describe block (2 tests).
- Backend integration tests: `PasswordResetEndpointTests.cs` (8 tests — auth gates, outbox queued, body contains link, metadata safety, generic errors).
- Gates: `dotnet build Release` clean, `dotnet test Release` 2159/2159 pass, `npm run build --configuration production` clean, `npm test` 925/925 pass.

### Security invariants

- Token not logged, not in notification metadata, not returned to admin.
- Public endpoint returns identical generic error for user-not-found, bad token, and expired token.
- Raw password never emailed or stored.
- Admin endpoint requires admin role JWT.

---

**Phase 10W-4 — Email Provider + Reset Password Wiring + Dispatch Job** - complete (2026-06-21)

Goal: add email delivery foundation and wire password reset / student creation emails through the notification/outbox pipeline. Add Quartz dispatch job. Keep SMS deferred.

### Delivered

- `IEmailSender` + `EmailMessage` + `EmailSendResult` in Application layer.
- `EmailOptions` (bound from `"Email"` config section; `Enabled=false` by default — no startup crash if unconfigured).
- `DisabledEmailSender` (safe no-op; used when email not configured).
- `SmtpEmailSender` (via `System.Net.Mail`; returns Skipped if disabled/no-host; catches all SMTP exceptions — never throws).
- `IEmailSender` registered in DI: SmtpEmailSender if `Enabled && Host` set, else DisabledEmailSender.
- `NotificationDispatchService` extended: Email outbox items resolved via `UserManager`, sent via `IEmailSender`. Success → Delivered. Skipped → counted as skipped. Failed → counted as failed. SMS still skipped.
- `NotificationDispatchJob` (Quartz, `[DisallowConcurrentExecution]`, every 2 minutes, persisted).
- `AdminHandler.ResetStudentPasswordAsync` queues email notification after reset — body does NOT include raw password.
- `CreateStudentHandler.HandleAsync` queues welcome email after student creation — body does NOT include temp password.
- Both operations tolerate notification queue failure (log warning, continue).
- Appsettings `"Email"` section with safe disabled defaults.
- 11 unit tests + 11 integration/dispatch/job tests (+20 new); existing dispatch tests updated for new constructor.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2150/2150 — 3 arch + 1287 unit + 860 integration; +20)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (916/916)

No migration added. No frontend changes.

See: `docs/reviews/2026-06-21-phase-10w-4-email-provider-reset-password-dispatch-job-review.md`

---

## Previous sprint

**Phase 10W-3 — Live Notification Bell UI Wiring** - complete (2026-06-21)

Goal: replace hard-coded demo notification dropdown with live Angular service + signals-based component wired to the Phase 10W-2 notification APIs.

### Delivered

- `NotificationService` (`core/services/notification.service.ts`): wraps all 5 backend endpoints (`list`, `getUnreadCount`, `markRead`, `markAllRead`, `archive`). Exports `NotificationItem`, `NotificationListResponse`, `UnreadCountResponse`.
- `NotificationDropdownComponent` rewritten: signals-based state (`notifications`, `unreadCount`, `loading`, `error`, `hasUnread`). Loading/error/empty/list states via `@if`/`@else if`/`@for`. Unread badge on bell. Mark-all-read. Per-item archive. Deep-link navigation.
- Demo data (Terry Franci, Nganter App etc.) fully removed from template.
- 6 service spec tests + 16 component spec tests.
- Two bugs fixed: Angular template parse error from `[class.dark:bg-blue-900\/10]` binding; ChunkLoadError from dynamic `import('rxjs')` in spec.

### Gates

- `git diff --check`: PASS
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (916/916)

See: `docs/reviews/2026-06-21-phase-10w-3-live-notification-bell-ui-wiring-review.md`

---

## Previous sprint

**Phase 10W-2 — In-App Notification APIs + Dispatch Foundation** - complete (2026-06-21)

Goal: authenticated current-user notification APIs, paged listing with filters, mark-read/read-all/archive, and outbox dispatch service (InApp delivered; Email/SMS safely skipped).

### Delivered

- `NotificationDto`, `PagedNotificationResult`, `NotificationListQuery` in Application layer.
- `INotificationQueryService` / `NotificationQueryService`: list (paged, filtered, expires-excluded, archived-excluded), unread-count, mark-read, mark-all-read, archive. Current-user isolation enforced.
- `INotificationDispatchService` / `NotificationDispatchService`: processes due outbox items in batches. InApp → delivered. Email/SMS → skipped with error (no provider yet).
- `NotificationsController`: 5 endpoints — `GET /api/notifications`, `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`, `POST /api/notifications/{id}/archive`.
- 16 API integration tests + 9 dispatch tests (+25 total).
- No migration. No frontend changes. No external email/SMS delivery.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2131/2131 — 3 arch + 1278 unit + 850 integration; +23)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (896/896)

See: `docs/reviews/2026-06-21-phase-10w-2-in-app-notification-apis-dispatch-foundation-review.md`

---

## Previous sprint

**Phase 10W-1 — Backend Notification Foundation** - complete (2026-06-21)

Goal: domain entities, EF persistence, Application service abstraction, Infrastructure implementation, migration, DI registration, and full test coverage for the notification platform foundation.

### Delivered

- 4 new domain enums: `NotificationChannel`, `NotificationStatus`, `NotificationSeverity`, `NotificationCategory`.
- 2 new domain entities: `Notification` (factory method, state transitions), `NotificationOutboxItem` (attempt tracking, retry backoff).
- `INotificationService` interface in `LinguaCoach.Application.Notifications` with `QueueAsync`, `QueueInAppAsync`, `QueueEmailAsync`, `QueueSmsAsync`.
- `NotificationService` implementation in `LinguaCoach.Infrastructure.Notifications`.
- EF configurations: `NotificationConfiguration`, `NotificationOutboxItemConfiguration`. Enums stored as strings.
- `LinguaCoachDbContext` extended with `Notifications` and `NotificationOutboxItems` DbSets.
- Migration `T54_NotificationFoundation`: tables `notifications`, `notification_outbox_items`, 6 indexes.
- `INotificationService` registered as scoped in `DependencyInjection`.
- 20 unit tests (`NotificationEntityTests`) + 13 integration tests (`NotificationServiceTests`).
- No external email/SMS delivery. No API endpoints. No frontend changes. No dispatch worker.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2108/2108 — 3 arch + 1278 unit + 827 integration; +28)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (896/896)

See: `docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md`

---

## Previous sprint

**Phase 10W-0 — Enterprise Notification Platform Gap Check** - complete (2026-06-21)

Goal: audit existing notification/email/SMS/in-app capability and define the 10W roadmap.

### Delivered

- Full gap check across all notification-related areas: email, in-app, SMS, event producers, data model, enterprise requirements.
- Review saved: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`
- TODOS.md updated with `TODO-10W-1` through `TODO-10W-FINAL` entries.
- No code changes. Audit only.

### Findings summary

- Email: not implemented. No IEmailSender, no SMTP config, no reset-password email flow.
- In-app: UI placeholder only. `NotificationDropdownComponent` has hard-coded demo items, no backend.
- SMS: not implemented.
- Only existing delivery: transient `ToastService` (auto-dismiss, no persistence).
- All job failures and AI alerts are logged via `ILogger` only — no user delivery.
- Zero entity/table for persistent notifications.

### Gates

- No code changed. `git diff --check`: not required.
- Audit-only phase.

See: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`

---

## Previous sprint

**Phase 10V-3B — AI Pricing Zero-Cost Alert UI** - complete (2026-06-21)

Goal: admin visibility for missing-pricing / zero-cost AI calls in the filtered AI Usage summary.

### Delivered

- `AiUsageSummaryDto` extended with `ZeroCostCallCount` and `ZeroCostTotalTokens`.
- `AiUsageHandler.GetSummaryAsync` computes zero-cost rows: `CostUsd == 0 AND (InputTokens + OutputTokens) > 0`. Respects all active filters (date, provider, model, featureKey, status, studentId).
- `AiUsageController` summary endpoint exposes `zeroCostCallCount` and `zeroCostTotalTokens` in JSON.
- `AiUsageSummary` TypeScript interface extended with both fields.
- AI Usage page template: `sp-admin-alert variant="warning"` shown when `zeroCostCallCount > 0`. Alert includes call count, token total, and explanation. Disappears when count is 0. Updates on every filter/date reload.
- No migration. No pricing calculation change. No provider routing change. No usage governance change. No historical recalculation.
- Backend: +5 integration tests (`AiUsageSummaryFilterTests`), +2 unit tests (`AiUsageSummaryTests`).
- Frontend: +5 Karma tests (`admin-ai-usage.component.spec.ts`). `makeSummary` factory updated with zero-cost defaults.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2080/2080 — 3 arch + 1262 unit + 815 integration)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (896/896)

See: `docs/reviews/2026-06-21-phase-10v-3b-ai-pricing-zero-cost-alert-ui-review.md`

---

## Previous sprint

**Phase 10U-FINAL — AI Usage / AI Config Closure Audit** - complete (2026-06-21)

Goal: final validation and documentation pass for the full 10U work. No new features. Small fixes and doc updates only.

### Audit findings

- Backend: no bugs found. All four endpoints (summary, recent, trends, export.csv) correctly apply date filter + column filters. `ApplyDateFilter` (from ≥, to <, UTC) and `ApplyColumnFilter` (provider, model, featureKey, status, studentId) are shared and consistent across all handlers.
- Frontend: no bugs found. `buildRange('custom')` correctly converts `yyyy-MM-dd` to UTC exclusive upper bound. `clearRecentFilters` correctly preserves custom date range. `applyCustomRange` guards both required + from ≤ to before calling API.
- Pricing: `AiPricingOptions` reads `{Provider}:Pricing:{ModelName}:InputPer1KTokens/OutputPer1KTokens` from config — matches `appsettings.json` exactly. No hardcoded pricing in production C#.
- Playwright: no AI Usage Playwright tests exist — no Playwright run.
- TODOS.md: `TODO-10U` marked done; `TODO-022` marked done with note; `TODO-10U-GAP-6` stale reference to "10U-8 pricing admin" corrected.
- `docs/handoffs/current-product-state.md`: updated to 10U-FINAL with full feature summary.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2041/2041 — 3 arch + 1248 unit + 790 integration)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (872/872)

See: `docs/reviews/2026-06-21-phase-10u-final-ai-usage-config-closure-audit.md`

---

## Previous sprint

**Phase 10U-10 — AI Usage Custom Date Range Picker** - complete (2026-06-21)

Goal: add Custom range period option to AI Usage admin page so admins can inspect any chosen date window.

### Delivered

- `PeriodPreset` extended with `'custom'`.
- `SpAdminInputComponent` added to component imports.
- `customFrom`, `customTo`, `customRangeError` signals; `customFromValue`/`customToValue` two-way-bind mirrors.
- `periodOptions` extended with `{ value: 'custom', label: 'Custom range' }`.
- `onPeriodChange('custom')` defers reload until Apply; switching away clears error and reloads with preset.
- `applyCustomRange()`: validates both required, from <= to; calls `load()` on valid input; resets page to 1.
- `clearCustomRange()`: resets all custom date state.
- `buildRange('custom')`: from = `T00:00:00Z`, to = start of day+1 (exclusive, consistent with 10U-3).
- Template: From/To `sp-admin-input type="date"` + Apply/Clear dates buttons shown when `periodPreset() === 'custom'`; `sp-admin-alert` for validation error.
- No backend changes. No migration. No provider routing change. No usage governance change.
- Frontend: +13 new tests. 872/872 pass. Production build clean.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: not run (no backend changes)
- `dotnet test --configuration Release`: not run (no backend changes)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (872/872)

See: `docs/reviews/2026-06-21-phase-10u-10-ai-usage-custom-date-range-picker-review.md`

---

## Previous sprint

**Phase 10U-9 — AI Usage Trend Summary** - complete (2026-06-21)

Goal: lightweight daily trend table on the AI Usage admin page, reusing all existing filters and date period presets.

### Delivered

- `AiUsageTrendBucket` DTO record in `LinguaCoach.Application/Admin/AiUsageQueries.cs`.
- `IAdminAiUsageHandler.GetTrendsAsync(dateFilter, columnFilter)` — new interface method.
- `AiUsageHandler.GetTrendsAsync`: client-side grouping by `DateOnly.FromDateTime(l.CreatedAt)`; reuses `ApplyDateFilter` + `ApplyColumnFilter`; zero-fills missing dates within `From→To` range when >= 2 real buckets exist.
- `GET /api/admin/ai-usage/trends`: same filter params and 400 validation as `/summary`, `/recent`, `/export.csv`. Returns `date (yyyy-MM-dd)`, `callCount`, `successCount`, `failureCount`, `fallbackCount`, `inputTokens`, `outputTokens`, `totalTokens`, `costUsd`.
- `AiUsageTrendBucket` interface + `getTrends(range?, filters?)` in `AiUsageService`.
- Component: `trendBuckets`, `loadingTrends`, `trendError` signals; `loadTrends()` called from `load()` on every filter/period change and clear.
- Template: "Usage trend" `sp-admin-card` with loading/error/empty/data states; zero-count rows muted via `.sp-au-trend-zero`.
- Backend: +15 integration tests (`AiUsageTrendTests`). 790/790 integration.
- Frontend: +11 component tests (loading/error/empty/data/filter-reload/clear/period). 858/858 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2041/2041)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (858/858)

See: `docs/reviews/2026-06-21-phase-10u-9-ai-usage-trend-summary-review.md`

---

## Previous sprint

**Phase 10U-8 — AI Usage CSV Export** - complete (2026-06-21)

Goal: CSV export of AI usage recent calls using the currently active filters, for admin reporting and reconciliation.

### Delivered

- `IAdminAiUsageHandler.GetExportAsync(dateFilter, columnFilter, maxRows)` — new method.
- `AiUsageHandler.GetExportAsync`: reuses `ApplyDateFilter` + `ApplyColumnFilter`; `Take(10_000)` cap; newest-first sort.
- `GET /api/admin/ai-usage/export.csv`: same filter params as `/summary` and `/recent`; same 400 validation. `BuildCsv` + `CsvEscape` helpers (RFC 4180). `text/csv` + `Content-Disposition: attachment; filename=ai-usage-{timestamp}.csv`.
- CSV columns: `CreatedAt, Provider, Model, FeatureKey, StudentId, WasSuccessful, IsFallback, FailureReason, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, CorrelationId`. No PII beyond opaque StudentId GUID.
- `AiUsageService.exportUsageCsv(range?, filters?)` returns `Observable<Blob>`. No page/pageSize.
- Component: `exporting` + `exportError` signals; `exportCsv()` triggers browser download via `URL.createObjectURL` + anchor click (no `file-saver` dep); `SpAdminAlertComponent` shows error on failure.
- Template: "Export CSV" / "Exporting…" button in filter bar; error alert above recent calls card.
- Backend: +16 integration tests (`AiUsageExportTests`). 775/775 integration.
- Frontend: +6 component tests. 847/847 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2026/2026)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (847/847)

See: `docs/reviews/2026-06-20-phase-10u-8-ai-usage-csv-export-review.md`

---

## Previous sprint

**Phase 10U-7 — AI Usage Summary Filter Alignment** - complete (2026-06-20)

Goal: make summary cards and breakdowns respect the same column filters as recent calls (provider, model, featureKey, status, studentId, date range).

### Delivered

- `IAdminAiUsageHandler.GetSummaryAsync` accepts `AiUsageRecentFilter? columnFilter` (new second param).
- `ApplyRecentFilter` renamed `ApplyColumnFilter` — shared by both `GetSummaryAsync` and `GetRecentAsync`.
- `/summary` endpoint: added `provider`, `model`, `featureKey`, `status`, `studentId` query params; same validation (400 on invalid status, 400 on non-GUID studentId) as `/recent`.
- `AiUsageService.getSummary(range?, filters?)` — forwards column filters to backend.
- `buildColumnFilters()` private helper on component — reads filter signals once, used by both `load()` and `loadRecent()`.
- All filter change handlers now call `load()` — reloads summary + recent together on every column filter change.
- `clearRecentFilters()` calls `load()` — keeps date period unchanged.
- Template: helper text "Summary totals reflect active filters." shown when any column filter is active.
- Backend: +14 integration tests (`AiUsageSummaryFilterTests`). 759/759 integration.
- Frontend: +10 new component tests; 3 existing updated for new `getSummary` 2-arg signature. 841/841 pass.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (2010/2010)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (841/841)

See: `docs/reviews/2026-06-20-phase-10u-7-ai-usage-summary-filter-alignment-review.md`

---

## Previous sprint

**Phase 10U-6 — AI Usage Student Filter** - complete (2026-06-20)

Goal: server-side `studentId` filter on the AI Usage recent calls endpoint; student select in the filter bar loaded from `adminApi.listStudents`.

### Delivered

- `AiUsageRecentFilter` record: added `StudentId` (nullable Guid).
- `AiUsageHandler.ApplyRecentFilter`: LINQ `Where(l => l.StudentProfileId == filter.StudentId.Value)` clause.
- `/recent` endpoint: `studentId` query param; 400 on non-GUID; unknown GUID returns empty paged result.
- `AiUsageRecentCallFilter` Angular interface: added `studentId?: string`.
- `AdminAiUsageComponent`: `studentOptions` signal loaded from `adminApi.listStudents({ pageSize: 50 })` on init; `onRecentStudentChange`; `clearRecentFilters` includes student; `hasActiveRecentFilters` includes student.
- Template: student select rendered conditionally on `studentOptions().length > 0`.
- Backend: +8 integration tests (`AiUsageStudentFilterTests`). Real student profile rows created via `CreateStudentAndGetTokenAsync` + DB join to resolve `StudentProfile.Id`. 745/745 integration + 1248/1248 unit + 3/3 arch.
- Frontend: +8 component tests. `admin-wrapper-migration.spec.ts` AI Usage test updated with `AdminApiService` mock. 831/831 pass.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1996/1996)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (831/831)

See: `docs/reviews/2026-06-20-phase-10u-6-ai-usage-student-filter-review.md`

---

## Previous sprint

**Phase 10U-5 — AI Usage Recent Calls Filters** - complete (2026-06-20)

Goal: server-side provider/model/featureKey/status filters for the AI Usage recent calls table.

### Delivered

- `AiUsageRecentFilter` record in Application layer with `HasInvalidStatus` guard.
- `IAdminAiUsageHandler.GetRecentAsync` updated to accept `AiUsageRecentFilter`.
- `AiUsageHandler.ApplyRecentFilter`: filters applied after date filter, before count/skip/take.
- `/recent` endpoint: `provider`, `model`, `featureKey`, `status` query params; 400 on invalid status.
- Status semantics: `success` = WasSuccessful && !IsFallback; `failed` = !WasSuccessful; `fallback` = IsFallback.
- `AiUsageRecentCallFilter` interface in Angular service; `getRecent` 4th param.
- Four-filter bar above recent calls table: provider, model, feature, status selects. "Clear filters" button when any filter active.
- Filter option sources: provider from summary.byProvider + items; model from items; feature from summary.byFeature + items.
- `clearRecentFilters` clears column filters only; does not reset date period.
- Pagination preserves active filters on page change.
- Summary totals unchanged — not affected by column filters.
- Backend: +12 integration tests (`AiUsageRecentFilterTests`). 1988/1988 pass.
- Frontend: +10 component tests (12 new, 3 replaced). 823/823 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1988/1988)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (823/823)

See: `docs/reviews/2026-06-20-phase-10u-5-ai-usage-recent-calls-filters-review.md`

---

## Previous sprint

**Phase 10U-4 — AI Usage Recent Calls Pagination** - complete (2026-06-20)

Goal: server-side pagination for the AI Usage recent calls/history list to handle enterprise-scale logs.

### Delivered

- `AiUsagePagedResult` record in Application layer: `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`.
- `IAdminAiUsageHandler.GetRecentAsync(page, pageSize, filter)`: replaced old `limit` param.
- `AiUsageHandler`: `CountAsync` + `Skip`/`Take` with page clamp (never exceeds totalPages) and pageSize clamp (1–100).
- `AiUsageController /recent`: replaced `limit` with `page`/`pageSize`; response envelope is `{ items, totalCount, page, pageSize, totalPages }`.
- Angular `AiUsageRecentResponse` interface updated to paged shape.
- `AiUsageService.getRecent(page, pageSize, range?)` signature.
- Component: `loadRecent()` extracted; `onRecentPageChange()` added; `recentTotalCount`/`recentTotalPages` signals driven by server response; client-side slice removed.
- Template: iterates `filteredRecentItems()` (current page); pagination wired to `onRecentPageChange`.
- Summary totals unaffected — `GetSummaryAsync` unchanged.
- Date filter + pagination compose correctly — filter applied before count/skip/take.
- Backend: +10 integration tests (`AiUsagePaginationTests`). 1977/1977 pass.
- Frontend: +8 component tests. 813/813 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1977/1977)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (813/813)

See: `docs/reviews/2026-06-20-phase-10u-4-ai-usage-recent-calls-pagination-review.md`

---

## Previous sprint

**Phase 10U-3 — AI Usage Date Filtering** - complete (2026-06-20)

Goal: add `from`/`to` UTC date-range filtering to the AI usage summary and recent endpoints; add period preset select (All time, Today, Last 7 days, Last 30 days, This month) to the Angular admin usage page.

### Delivered

- `AiUsageDateFilter` record in Application layer: `From` (inclusive `>=`), `To` (exclusive `<`), `IsInverted` guard.
- `IAdminAiUsageHandler` interface: both methods accept optional `AiUsageDateFilter`.
- `AiUsageHandler`: `ApplyDateFilter` helper wires filter into EF Core LINQ query.
- `AiUsageController`: `from`/`to` query params on both `/summary` and `/recent`; returns 400 when range is invalid (From >= To).
- `AiUsageDateRange` interface in Angular service; both `getSummary` and `getRecent` forward range as HTTP query params.
- `PeriodPreset` type exported from component; `periodOptions`, `buildRange()`, `onPeriodChange()`, `load()` added to component.
- Period preset `sp-admin-select` in `sp-admin-filter-bar` above stat grid.
- Backend: +12 integration tests (`AiUsageDateFilterTests`). 1967/1967 pass.
- Frontend: +11 component tests (period preset coverage). 805/805 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1967/1967)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (805/805)

See: `docs/reviews/2026-06-20-phase-10u-3-ai-usage-date-filtering-review.md`

---

## Previous sprint

**Phase 10U-1/10U-2 — AI Pricing Config Seed + Usage Token Totals** - complete (2026-06-20)

Goal: fix silent $0 AI cost bug (missing pricing config keys) and surface token totals on /admin/usage summary cards.

### Delivered

- `appsettings.json`: Added `OpenAI:Pricing`, `Gemini:Pricing`, `Anthropic:Pricing` sections with per-model pricing for 12 models. Unblocks `AiUsageLog.CostUsd` from always being $0. Values are operational defaults — override in production via env secrets.
- `AiUsageSummaryDto`: Extended with `TotalInputTokens`, `TotalOutputTokens`, `TotalTokens` (long).
- `AiUsageHandler`: Aggregates token totals from `AiUsageLog` rows.
- `AiUsageController`: Exposes token fields in summary JSON (additive, no breaking change).
- Angular `AiUsageSummary` interface: Updated with three token fields.
- `/admin/usage` summary grid: Three new stat cards (Input tokens / Output tokens / Total tokens). Grid updated to 4-col at 900px, 8-col at 1200px.
- Backend tests: +11 (pricing Theory tests + summary DTO tests). 1955/1955 pass.
- Frontend tests: +3 (token card assertions). 794/794 pass. Both builds clean.
- No migration. No provider routing change. No usage governance change.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1955/1955)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (794/794)

See: `docs/reviews/2026-06-20-phase-10u-1-2-ai-pricing-config-token-totals-review.md`

---

## Previous sprint

**Phase 10Students-F-H — Student Management Final Validation** - complete (2026-06-20)

Goal: validate all enterprise student management work (10Students-F-A through 10X-L) end-to-end.

### Delivered

- All backend gates passed: 1944/1944 tests (3 arch + 1237 unit + 704 integration).
- All frontend gates passed: 791/791 Angular tests, production build clean.
- Playwright: 6/6 tests pass after mock fix.
- One issue found and fixed: E2E reset spec used old flat-array mock shape for the student list endpoint. Updated to `PagedResponse` shape (`{ items, totalCount, page, totalPages }`) matching Phase 10F breaking change. Also added detail/audit-history stubs within the same wildcard intercept.
- No backend or product contract gaps found.
- No new features added. No student-facing changes. No unrelated refactors. No commit/push.

See: `docs/reviews/2026-06-20-phase-10students-f-h-student-management-final-validation-review.md`

---

## Previous sprint

**Phase 10X-L — Shared Admin Overlay, Slide-Over, and Table Action Fixes** - complete (2026-06-20)

Goal: fix shared admin UI issues on /admin/students and /admin/students/{id} at the shared component level.

### Delivered

- `sp-admin-slide-over`: `closeOnBackdrop` default changed `true` → `false`. z-index raised from 400/401 to 1000/1001. New `stackIndex` input for stacked panels (each level adds 50 to z-index). Bindings applied via `[style.z-index]`. Escape key and accessibility already correct.
- `admin-student-detail`: Set CEFR flow converted from `.sp-admin-modal` centered div to `sp-admin-slide-over` (size=sm, stackIndex=1). Assign policy flow converted the same way. View preferences already used slide-over; confirmed intact. Edit student / Reset password / Reset data / Lifecycle modals left as-is (destructive/high-stakes; out of scope).
- `sp-admin-table-actions`: dropdown now uses `position:fixed` + `getBoundingClientRect()`. Escapes overflow parents in table containers. Flip-up logic when near viewport bottom. Closes on window scroll. Removes the vertical scroll-on-open bug.
- Specs: slide-over spec updated (closeOnBackdrop=false default, backdrop/escape tests, stacking tests). Student detail spec: new 10X-L describe block with 6 targeted tests. Table-actions spec created from scratch (18 tests).
- 791/791 tests pass. Build green. No backend change. No product behaviour changed.

See: `docs/reviews/2026-06-20-phase-10x-l-shared-admin-overlay-slide-over-table-actions-review.md`

---

**Phase 10Students-F-G — Student List Filter Select UI** - complete (2026-06-20)

Goal: add lifecycle stage, onboarding status, and CEFR level filter selects to the admin student list filter bar. Backend params were already wired in Phase 10Students-F-F.

### Delivered

- `filterLifecycleStage`, `filterOnboardingStatus`, `filterCefrLevel` signals added to `AdminStudentsComponent`.
- Three `sp-admin-select` instances inserted in the filter bar for lifecycle stage (12 options), onboarding status (4 options), and CEFR level (A1–C2).
- Each filter change resets page to 1 and calls `load()`. `load()` passes filters as `undefined` when empty.
- Clear filters button: visible when any of searchTerm/lifecycleStage/onboardingStatus/cefrLevel is set. Clears all four, resets page to 1, does not touch `includeArchived`.
- `SpAdminSelectComponent` added to component imports array.
- 7 new tests + all 25 existing tests pass (32 total). Build green.
- No backend change. No student-facing change. No bulk operations.

See: `docs/reviews/2026-06-20-phase-10students-f-g-student-list-filter-select-ui-review.md`

---

**Phase 10Students-F-F — Server-Side Student Pagination, Filtering, Sorting** - complete (2026-06-19)

Goal: make the admin student list enterprise-ready by moving all pagination, filtering, searching, and sorting to the backend. Breaking change to the list endpoint response shape.

### Delivered

- `StudentListQuery` and `PagedResponse<T>` records added to `AdminQueries.cs`.
- `ListStudentsPagedAsync(StudentListQuery)` added to `IAdminStudentQuery` interface and `AdminHandler` implementation. Applies archived filter, lifecycle/onboarding/CEFR exact-match filters, case-insensitive search across email/displayName/firstName/lastName, sorting on name/email/onboardingStatus/lifecycleStage/cefrLevel/createdAt, Skip/Take pagination. `pageSize` capped at 100.
- `GET /api/admin/students` updated to accept `page`, `pageSize`, `search`, `includeArchived`, `lifecycleStage`, `onboardingStatus`, `cefrLevel`, `sortBy`, `sortDir` query params. Returns `PagedResponse<StudentListItem>` (breaking change from `StudentListItem[]`).
- `GET /api/admin/students/{id}` (detail endpoint) unchanged.
- `StudentListQuery` and `PagedResponse<T>` TypeScript interfaces added to `admin.models.ts`.
- `AdminApiService.listStudents(query)` updated to accept `StudentListQuery` and return `Observable<PagedResponse<StudentListItem>>`.
- `admin-students.component.ts` refactored: client-side computed signals removed, server-driven `load()` on every param change (page, search, includeArchived, sort). Row actions reload current page after success.
- `admin-dashboard.component.ts` updated to read `r.items` from paged response.
- All spec files updated: `admin-students.component.spec.ts` (full rewrite for server-driven pattern), `admin-dashboard.component.spec.ts`, `admin-wrapper-migration.spec.ts`, `AdminManagementEndpointTests.cs`.
- 12 new integration tests in `AdminEndpointTests.cs`. 2 pre-existing tests in `AdminManagementEndpointTests.cs` fixed for new response shape.
- Backend: 1944 tests pass. Frontend: 756 tests pass.
- No migration. No student-facing changes. No bulk operations.

See: `docs/reviews/2026-06-19-phase-10students-f-f-server-side-student-pagination-filtering-sorting-review.md`

---

**Phase 10Students-F-E — Student Audit / History Tab** - complete (2026-06-19)

Goal: surface student-specific admin action history in the admin student detail page. Combines AdminAuditLog and StudentResetLog entries, newest-first, capped at 50.

### Delivered

- `StudentAuditHistoryItemDto` record in `AdminQueries.cs`.
- `GetStudentAuditHistoryAsync(Guid studentProfileId)` added to `IAdminStudentQuery` interface and `AdminHandler` implementation. Queries both `AdminAuditLogs` (by `TargetStudentId`) and `StudentResetLogs` (by `StudentProfileId`). Combined in memory, sorted newest-first, capped at 50. Returns null when student not found (→ 404).
- `GET /api/admin/students/{id}/audit-history` in `AdminController` — admin-only, 200 / 404.
- No migration required — both tables already existed.
- `StudentAuditHistoryItem` TypeScript interface added to `admin.models.ts`.
- `AdminApiService.getStudentAuditHistory(id)` in Angular service.
- Audit History section added at bottom of `admin-student-detail.component`: loading / error / empty states, table rows with action badge, source, actor ID prefix, reason, old→new value, details. Long details (>80 chars) open `sp-admin-slide-over`. No edit/delete controls on rows. No password fields.
- 7 backend integration tests + 8 frontend unit tests. All gates green.
- No student-facing changes. No global audit search. No server-side student list pagination.

See: `docs/reviews/2026-06-19-phase-10students-f-e-student-audit-history-tab-review.md`

---

**Phase 10Students-F-D — Admin CEFR Management** - complete (2026-06-19)

Goal: allow admins to set or clear a student's CEFR level from the admin student detail page. Students cannot edit their own CEFR.

### Delivered

- `AdminSetCefrLevel(string? level)` domain method on `StudentProfile`: normalises, validates, or clears CEFR.
- `SetStudentCefrCommand` record and `SetStudentCefrAsync` in `IAdminStudentQuery` and `AdminHandler`.
- `AdminHandler.SetStudentCefrAsync`: writes `AdminAuditLog` with action `SetCefr`, old/new value JSON, and reason.
- `PUT /api/admin/students/{id}/cefr` in `AdminController` — admin-only, 200/400/404.
- `SetStudentCefrRequest` DTO: `CefrLevel` (string|null) + optional `Reason`.
- `AdminApiService.updateStudentCefr(id, cefrLevel, reason?)` in Angular service.
- CEFR badge display with "Set CEFR" button in admin student detail profile section.
- Modal with A1–C2 dropdown + "Clear / Not set" option + optional reason field.
- On success: student detail reloaded, toast shown. On error: inline error in modal.
- Helper text: "CEFR is controlled by assessment and admin. Students cannot edit this."
- 5 backend integration tests + 9 frontend unit tests. All gates green.
- No migration added. No student-facing changes. No placement logic touched.

See: `docs/reviews/2026-06-19-phase-10students-f-d-admin-cefr-management-review.md`

---

**Phase 10Students-F-B — Dedicated Student Detail Endpoint + Onboarding Progress** - complete (2026-06-19)

Goal: dedicated `GET /api/admin/students/{id}` endpoint returning full student detail with onboarding progress. Fix SQLite integration test blocker. Wire Angular component to dedicated endpoint.

### Delivered

- `GetStudentDetailAsync` in `AdminHandler` queries `StudentOnboardingProgress` by `UserId` (no ORDER BY — unique index, at most one row; avoids SQLite DateTimeOffset incompatibility).
- `AdminStudentDetailDto` and `StudentOnboardingProgressInfo` records in `AdminQueries.cs`.
- `GET /api/admin/students/{studentId:guid}` in `AdminController`.
- `OnboardingFlowSeeder.SeedAsync` added to `ApiTestFactory.EnsureCreatedAsync` — fixes FK constraint in integration tests.
- 6 integration tests: expected fields, preference fields, null onboarding, onboarding row exists, 404, 403.
- Angular: `getStudent(id)` in `AdminApiService`; `AdminStudentDetail` and `StudentOnboardingProgressInfo` models; component loads from dedicated endpoint; onboarding progress section with status badge, step, percentage, empty state.
- Frontend spec: `Subject<AdminStudentDetail>` replaces `require('rxjs')` hack; `displayName: null` override fix.
- Review doc: `docs/reviews/2026-06-19-phase-10students-f-b-dedicated-student-detail-endpoint-onboarding-progress-review.md`

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1911/1911)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (719/719)

---

**Phase 10Students-F-A — Admin Read: Student Learning Preferences** - complete (2026-06-19)

Goal: surface student learning preferences to admins in a read-only view on the student detail page. Deliver `sp-admin-slide-over` as the design-system foundation for admin secondary detail panels.

### Delivered

- `sp-admin-slide-over` component added to admin design system (`src/app/admin/components/slide-over/`). Inputs: `open`, `title`, `subtitle`, `size` (sm/md/lg/xl), `loading`, `loadingMessage`, `error`, `errorTitle`, `closeOnBackdrop`. Output: `closed`. Slots: `[slot=header-actions]`, body, `[slot=footer]`. Escape and backdrop close. ARIA: `role=dialog`, `aria-modal`. Responsive.
- Exported from `src/app/admin/index.ts` barrel.
- "Student preferences" section added to `admin-student-detail.component`: summary card shows all preference fields; "View preferences" button opens slide-over with full detail.
- `hasAnyPreference()` helper guards empty state.
- No new backend endpoint or migration required — preference fields already returned by `GET /api/admin/students` from Phase 10R-J.
- Admin edit of preferences intentionally not implemented (read-only scope).
- 16 unit tests for `sp-admin-slide-over`. 6+ unit tests for preferences section in `admin-student-detail.component.spec.ts`.
- Gap check review: `docs/reviews/2026-06-19-phase-10students-f-0-enterprise-student-management-gap-check.md`
- Slide-over panel review: `docs/reviews/2026-06-19-phase-10students-f-foundation-admin-slide-over-panel-review.md`
- Phase review: `docs/reviews/2026-06-19-phase-10students-f-a-admin-read-student-preferences-review.md`

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (708/708)

### Remaining TODOs

- `TODO-10X-DRAWER`: typed drawer payloads for student detail, usage policy editor, prompt preview.
- `TODO-10X-DARKMODE`, `TODO-10X-MODAL`, `TODO-10X-TOAST`: admin shell polish items.
- `TODO-10U`: full AI usage/config redesign.
- `TODO-10V`: prompt playground.
- Admin edit of student preferences: not scoped, requires product decision.

---

**Phase 10R-J — Student Usage Policy Assignment Admin UI** - complete (2026-06-19)

Goal: allow admins to view, assign, and reset a student's usage policy from the student detail page.

### Delivered

- `RemoveStudentPolicyAssignmentAsync` on `IUsageGovernanceAdminService` and implementation — deactivates active assignment, writes audit log, safe no-op if none exists.
- `StudentEffectivePolicyResult` record — wraps `UsagePolicy` with `IsOverride`, `AssignedAt`, `AssignedByAdminUserId`, `Reason`.
- `GetStudentEffectivePolicyAsync` updated to return `StudentEffectivePolicyResult?` instead of bare `UsagePolicy?`.
- `GET /api/admin/students/{id}/usage-policy` response extended with `isOverride`, `assignedAt`, `assignedByAdminUserId`, `reason` fields.
- `DELETE /api/admin/students/{id}/usage-policy` endpoint added — 204 on success, safe no-op if no active assignment.
- `StudentEffectivePolicy` TypeScript interface added to `usage-governance.service.ts`.
- `getStudentEffectivePolicy(studentId)` and `removeStudentPolicy(studentId)` added to `UsageGovernanceService`.
- Usage Policy section added to `admin-student-detail.component`: policy name, scope, override/default badge, assigned date, reason (when override), active rule count.
- "Assign Policy" action: opens modal, loads active policies, lets admin pick policy and enter reason, calls `assignStudentPolicy`, refreshes.
- "Reset to Default" action: visible only when override is active, confirm dialog, calls `removeStudentPolicy`, refreshes.
- `admin-student-detail.component.spec.ts` created: 9 tests covering render, badge, modal open, assign call, assign error, remove confirm, remove cancel, remove error.
- `TODO-10R-STUDENT-ASSIGN` closed.
- `StudentListItem` test fixtures in `admin-dashboard.component.spec.ts` and `admin-students.component.spec.ts` updated with new learning preferences fields (`preferredName`, `supportLanguageCode`, `supportLanguageName`, `difficultyPreference`, `translationHelpPreference`, `focusAreas`, `customFocusArea`, `learningGoals`, `customLearningGoal`, `learningPreferencesUpdatedAt`) — all 708 frontend tests passing.

See: `docs/reviews/2026-06-19-phase-10r-j-student-usage-policy-assignment-admin-ui-review.md`

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors, 7 pre-existing warnings)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (708/708)

### Remaining TODOs

- `TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT`: Optional DB unique index on `(UsagePolicyId, FeatureKey)`.
- `TODO-10U`: full AI usage/config redesign.
- `TODO-10V`: prompt playground.

---

## Previous sprint

**Phase 10R-H — Usage Policy Rule Editor Admin UI** - complete (2026-06-19)

Goal: add admin UI for creating, editing, and deleting individual usage policy rules using the 10R-G backend.

### Delivered

- Rule editor modal (`sp-admin-modal variant="form"`) with full form: feature select/input, enforcement mode, unit type, all limit fields, warning threshold, tracking enabled, active toggles.
- Delete confirmation modal (`sp-admin-modal variant="danger"`) with rule key display.
- Add rule button in each expanded policy row. Edit/Delete buttons per rule row.
- Signal-per-field form state; client-side validation with inline error display.
- `openAddRule`, `openEditRule`, `closeRuleModal`, `saveRule`, `openDeleteRule`, `closeDeleteModal`, `confirmDelete` methods.
- Local state helpers `addRuleInPlace`, `updateRuleInPlace`, `removeRuleInPlace` — no full reload after rule CRUD.
- Feature key shown as read-only in edit mode with explanatory note.
- Build: clean. Tests: 670/670.

See: `docs/reviews/2026-06-19-phase-10r-h-usage-policy-rule-editor-admin-ui-review.md`

---

## Previous sprint

**Phase 10R-G — Usage Policy Rule CRUD Backend Foundation** - complete (2026-06-19)

Goal: add backend/domain/API support for individual usage policy rule create, update, and delete.

### Delivered

- `UsagePolicyRule.Update(...)` domain method with full validation (mirroring constructor invariants).
- `AddUsagePolicyRuleRequest` and `UpdateUsagePolicyRuleRequest` application DTOs.
- `AddRuleAsync`, `UpdateRuleAsync`, `DeleteRuleAsync` on `IUsageGovernanceAdminService` + implementation.
- Duplicate-key guard in `AddRuleAsync`: one rule per `(policyId, featureKey)` enforced at application layer.
- Three new admin API endpoints: `POST/PUT/DELETE /api/admin/usage-policies/{policyId}/rules[/{ruleId}]`.
- `MapRule` helper on controller; all three endpoints admin-auth protected.
- Frontend `UsageGovernanceService`: `addRule`, `updateRule`, `deleteRule` methods + two new request interfaces.
- 4 unit tests, 16 integration tests (8 service + 8 endpoint), 3 Angular service tests — all pass.
- No migration needed; no UI rule editor built yet.

### Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS (1905/1905)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (670/670)

### Remaining TODOs

- `TODO-10R-RULE-MGMT-UI`: Inline rule editor UI in admin-usage-policies page (next phase).
- `TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT`: Optional DB unique index on `(UsagePolicyId, FeatureKey)`.

---

## Previous sprint

**Phase 10R-F — Usage Governance Admin UX Foundation** - complete (2026-06-19)

Goal: make the Usage Policies admin page production-usable using existing API and admin design system wrappers.

### Delivered

- Summary stat cards: Total Policies, Active count, Default policy name.
- Expandable rule detail rows: feature key (code pill), feature display name, enforcement mode badge (typed), unit type, active state, limit summary (daily/weekly/monthly count and cost).
- Feature name lookup via computed `featureNameMap` from the feature definitions API.
- Fixed `enforcementBadgeTone` return type to `SpAdminBadgeTone` (was unconstrained `string`).
- Added `SpAdminStatCardComponent`, `SpAdminSectionCardComponent`, `SpAdminCodePillComponent`.
- Scope type now shown as neutral badge. Description shown as muted subtitle in table row.
- 9 new behavioral tests added; full suite 667/667 pass.

### Gates

- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (667/667)
- Backend unchanged; .NET build/test not required.

### Remaining TODOs

- `TODO-10R-RULE-MGMT`: Rule create/edit/delete UI blocked until a per-rule API endpoint is added.
- ~~`TODO-10R-STUDENT-ASSIGN`~~: Done in Phase 10R-J.

---

## Previous sprint

**Phase 10X-J-T — Frontend Test Cleanup: Remove Brittle CSS/Class Assertions** - complete (2026-06-19)

Goal: clean up frontend tests that asserted Tailwind, TailAdmin, BEM, wrapper implementation,
border/radius/spacing classes, inline styles, and other visual implementation details while the
admin UI remains under active iteration.

### Delivered

- Angular specs now avoid brittle CSS/class assertions for admin wrappers, admin layout, student
  chips/badges, profile chips, and wrapper-migration coverage.
- Playwright tests now use accessible buttons, roles, `aria-pressed`, `data-testid`, visible text,
  and smoke-flow assertions instead of Tailwind/TailAdmin/internal class selectors.
- Removed style-only test cases that only checked CSS implementation details.
- Kept behavior coverage for rendering, projected content, form/CVA binding, sorting events,
  dropdown/modal open and close, row actions, navigation, and disabled/loading state.
- Documented the frontend testing rule in `docs/architecture/admin-ui-design-system.md`.
- Added TODO-10X-J-T-VISUAL-BASELINE for a future proper visual regression baseline.

### Gates

- Frontend gates required for this phase:
  - `cd src/LinguaCoach.Web && npm ci`
  - `cd src/LinguaCoach.Web && npm run build -- --configuration production`
  - `cd src/LinguaCoach.Web && npm test -- --watch=false --browsers=ChromeHeadless`
  - `npx playwright test --workers=1 --reporter=dot`
  - `git diff --check`
- Backend gates intentionally not run; this phase is frontend-only.

### Review doc

`docs/reviews/2026-06-18-phase-10x-j-t-frontend-test-cleanup-review.md`

---

**Phase 10X-I — Migrate Remaining Admin Forms and Modals to CVA Wrappers** - complete (2026-06-19)

Goal: migrate the three deferred admin form/modal targets to `sp-admin-*` CVA wrappers after the
CVA foundation (10X-H) and layout blocker fix (10X-LAYOUT-BLOCKER) unblocked this work.

### Delivered

- **AI Config:** provider/model/voice category selects kept as native `<select>` inside
  `<sp-admin-form-field>` (incompatible with `sp-admin-select` due to `[ngValue]="null"`).
  TTS voice text input, model name input, API key (password) input, Qwen endpoint input all
  migrated to `<sp-admin-input>`. Add/Test/Save/Clear buttons migrated to `<sp-admin-button>`.
  Removed page-local `.sp-ai-select`/`.sp-ai-model-select` CSS; added `.sp-adm-native-select`.
- **Integrations:** storage display fields migrated to `<sp-admin-input [disabled]="true">` inside
  `<sp-admin-form-field>`. Generation settings number inputs kept native `<input type="number">`
  (CVA writes strings — numeric domain integrity preserved); wrapped in `<sp-admin-form-field>`.
  Test/Save/Cancel/Retry/Generate buttons migrated to `<sp-admin-button>`. Tables rewrote to
  TailAdmin Tailwind classes directly.
- **Student modals (all 3):** edit, reset-password, and reset-data page-local modals replaced with
  `<sp-admin-modal>`. All text/password inputs inside use `<sp-admin-input>`. Textareas use
  `<sp-admin-textarea>`. Submit/cancel actions use `<sp-admin-button>`. Removed all page-local
  `.sp-admin-modal-backdrop`/`.sp-admin-modal`/`.sp-admin-modal-header`/`.sp-admin-edit-grid` CSS.
- **`sp-admin-modal`:** added `maxWidth` `@Input()` (default `520px`); student edit modal uses `720px`.
- **`sp-admin-input`:** added `@Input() value` getter/setter for one-way display binding.
- **`sp-admin-layout`:** content area changed from `<div>` to `<main>` for `role="main"` semantics,
  fixing Playwright `getByRole('main')` locator failures.
- 11 new Angular unit tests for the migrated components.

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build (production): clean; Angular tests: 421 passed (up from 411)
- Playwright: `getByRole('main')` locator issue resolved by `<main>` layout fix

### Closed TODOs

- TODO-10X-G-AICONFIG-FORMS: done
- TODO-10X-G-INTEGRATIONS-FORMS: done
- TODO-10X-D-MODAL: done
- TODO-10X-I: done

### Review doc

`docs/reviews/2026-06-19-phase-10x-i-admin-form-modal-migration-review.md`

---

## Previous sprint

**Phase 10X-LAYOUT-BLOCKER — TailAdmin Layout One Shell Parity** - complete (2026-06-19)

Goal: make admin shell/sidebar/header/content visually match TailAdmin Layout One.
Blocked 10X-I until resolved.

### Delivered

- Imported TailAdmin `@utility` classes (`menu-item`, `menu-item-active`, `menu-item-inactive`,
  `menu-item-icon-*`, `menu-dropdown-*`, `no-scrollbar`) and `@theme` tokens into global `styles.css`.
- Rewrote `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header` wrappers to use exact
  TailAdmin Tailwind classes with no competing custom CSS class names.
- Rewrote `admin-app-layout.component.html` to use TailAdmin grouped nav section structure
  (`menu-item`/`menu-item-active`/`menu-item-inactive` classes, `h2` section headings).
- Replaced old mobile drawer (custom CSS) with TailAdmin `fixed inset-0 z-40` backdrop
  and `xl:hidden -translate-x-full / translate-x-0` aside pattern.
- Stripped all competing layout CSS from `admin-app-layout.component.css`.
- Added `body.admin-layout` class (OnInit/OnDestroy) for TailAdmin gray-50 background
  without affecting student layout.
- 20 new Angular tests for Layout One shell structure.
- Updated 13 existing tests to validate TailAdmin classes instead of old CSS class names.
- All CI gates pass: 1885 .NET tests, 411 Angular tests, 183+ Playwright tests.

### Root causes fixed

1. TailAdmin `@utility` menu classes missing from SpeakPath Tailwind build.
2. `admin-app-layout.component.css` margin-left override fighting Tailwind `xl:ml-*`.
3. `sp-admin-layout/sidebar/header` CSS classes conflicting with Tailwind classes.
4. Body background not overridden for admin context.
5. Nav items using old `sp-admin-nav-item` instead of `menu-item` utility.

### Review doc

`docs/reviews/2026-06-19-phase-10x-layout-blocker-tailadmin-shell-parity-review.md`

---

## Previous sprint

**Phase 10X-H — Admin Form Wrapper CVA + Remaining Form/Modal Migration Foundation** - complete (2026-06-19)

Goal: make TailAdmin-backed admin form wrappers safe for real Angular forms by adding
`ControlValueAccessor`, preparing the foundation for future enterprise admin screens.

### Delivered

- `sp-admin-input`: `ControlValueAccessor` via `NG_VALUE_ACCESSOR` + `forwardRef`. Supports
  `[(ngModel)]`, reactive `[formControl]`/`formControlName`, `setDisabledState`, touched-on-blur.
  Pass-through `type`, `placeholder`, `autocomplete`, `readonly`, `required`, `invalid`.
- `sp-admin-select`: `ControlValueAccessor`. Options via `[options]` or projected `<option>`,
  `placeholder` disabled default option, `required`, `invalid`, disabled propagation, touched-on-blur.
- `sp-admin-textarea`: new wrapper with `ControlValueAccessor`. `rows`, `placeholder`, `readonly`,
  `required`, `invalid`, disabled propagation, touched-on-blur. Exported from `admin/index.ts`.
- `sp-admin-form-field`: red `*` required marker via `[required]`; label/hint/error structure.
- Tests: 15 new wrapper specs (ngModel write/propagate, reactive FormControl bind, disabled
  propagation, touched-on-blur for input/select/textarea; form-field label/hint/error/required).

### Deferred (CVA foundation now unblocks these)

- AI Config dense provider-credentials grid (TODO-10X-G-AICONFIG-FORMS) — kept native this phase to
  avoid silent save regressions across the high-field-count ngModel-driven grid.
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS) — per-field migration pass deferred.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build (production): clean; Angular tests: 394 passed (up from 379)
- Playwright: 188 passed

See: `docs/reviews/2026-06-19-phase-10x-h-admin-form-cva-modal-foundation-review.md`

---

## Previous sprint

**Phase 10X-G-F — Finish Remaining Admin Page Refactor to TailAdmin-backed Wrappers** - complete (2026-06-18)

Goal: finish wrapper consistency on the remaining admin pages after 10X-G.

### Delivered

- Students: row table wrapped in `sp-admin-table` (projection mode); lifecycle/onboarding/CEFR pills
  migrated from raw `.sp-admin-badge` class to the `sp-admin-badge` wrapper. Removed obsolete
  page-local pagination, row-action link, and `.sp-admin-row-actions` CSS.
- Curriculum: create/edit and routing-preview form fields migrated to `sp-admin-form-field`
  (closes TODO-10X-G-CURRICULUM-FORMS). Native ngModel controls retained inside each field because
  `sp-admin-input`/`sp-admin-select` have no ControlValueAccessor.
- Verified AI Usage, Prompts, Exercise Types, Diagnostics, Usage Policies, and Integrations cards
  were already wrapper-migrated in 10X-B/10X-G. No further raw badge/table legacy in those pages.
- Tests: 2 new wrapper-migration specs (Students table/badge/actions; Curriculum form fields).

### Gates

- git diff --check: clean
- .NET build: 0 errors; .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration)
- Angular build: clean; Angular tests: 379 passed (up from 377)
- Playwright: 188 passed

### Not implemented in 10X-G-F (deferred)

- AI Config dense provider-credentials form fields (TODO-10X-G-AICONFIG-FORMS).
- Integrations operational forms (TODO-10X-G-INTEGRATIONS-FORMS).
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).
- Admin-only dark-mode class boundary (TODO-10X-G-DARKMODE).
- Usage governance UX (10R-F), AI Usage redesign (10U), prompt playground (10V), notification
  platform, enterprise auth/security, observability, billing, StudentProfile.CefrLevel migration,
  full placement engine, full mastery engine.

See: `docs/reviews/2026-06-18-phase-10x-g-f-finish-admin-page-refactor-review.md`

---

## Earlier sprint

**Phase 10X-G — Full Admin Page Refactor to TailAdmin-backed Wrappers** - complete (2026-06-18)

Goal: refactor the highest-legacy admin pages onto `sp-admin-*` wrappers, wire the admin
header user menu through `sp-admin-dropdown`, and reduce duplicated page-local CSS.

### Delivered

- Dashboard: KPI tiles → `sp-admin-stat-card`; sections → `sp-admin-card` (incl. dashed
  placeholders); status pills → `sp-admin-badge`. Removed ~50 lines of page-local CSS
  (KPI card, status card, badge, table-card) now owned by wrappers.
- AI Config: removed duplicate in-card `<h2>` headings (card title is canonical); LLM and
  TTS category Save/Test actions → `sp-admin-button`.
- Curriculum: create/edit and routing-preview panels → `sp-admin-card`; form and preview
  actions → `sp-admin-button` (replaced student-design `.sp-card`/`.sp-btn`).
- Admin header user/profile menu → `sp-admin-dropdown` (open state, click-outside, Escape
  owned by the wrapper). Removed `profileMenuOpen`, `toggleProfileMenu`, and the document
  click handler from `AdminAppLayoutComponent`.
- Tests: new `admin-app-layout.component.spec.ts` (4 header-dropdown tests); updated dashboard
  KPI assertion to target `sp-admin-stat-card`; curriculum create test asserts `sp-admin-card`.
- Docs: design-system page-refactor rules + header-dropdown section; adapter inventory 10X-G
  phase; product-state; TODOs.

### Gates

- git diff --check: ✅ clean
- Angular build: ✅ passed
- Angular tests: ✅ 377 passed (up from 373)
- .NET build: ✅ 0 errors
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ 188 passed

### Not implemented in 10X-G (deferred)

- Migrating remaining page-local form fields (`.sp-ai-select`, `.sp-input`, Integrations
  operational forms) to `sp-admin-form-field`/`sp-admin-select` — see TODOs.
- Student edit/reset/archive modal internals (TODO-10X-D-MODAL).
- Usage governance UX (TODO-10R-F), AI Usage redesign (TODO-10U), prompt playground (TODO-10V).
- Notification platform, enterprise auth/security, observability stack, billing,
  StudentProfile.CefrLevel migration, full placement engine, full mastery engine.

See: `docs/reviews/2026-06-18-phase-10x-g-full-admin-page-refactor-review.md`

---

## Previous sprint

**Phase 10X-F — Admin Wrapper Capability Completion** - complete (2026-06-18)

Goal: add missing reusable admin wrapper capabilities: sortable tables, row action dropdowns, admin dropdown wrapper, theme toggle, improved filter-bar and header slots.

### Delivered

- `sp-admin-dropdown` — TailAdmin-backed dropdown with trigger/menu content projection, click-outside + Escape close, align and width inputs.
- `sp-admin-table-actions` — row action dropdown (three-dot trigger). Generic `[actions]` array API + content projection for conditional per-row actions. Danger item red styling.
- `sp-admin-theme-toggle` — admin-only dark/light toggle based on TailAdmin ThemeToggleButtonComponent. Uses `AdminThemeService` (isolated `adminTheme` localStorage key).
- `AdminThemeService` — admin-scoped theme service mirroring TailAdmin pattern.
- `sp-admin-table` updated: sortable column flag, `sortColumn`/`sortDirection` inputs, `(sortChange)` output, `↕/▲/▼` icons, `aria-sort`, keyboard-accessible headers.
- `sp-admin-header` updated: named `[left]` and `[actions]` content slots. Theme toggle auto-rendered in right action zone.
- `sp-admin-filter-bar` updated: named `[search]`, `[filters]`, `[actions]` slots. Left/right zone split. Backward-compat general projection retained.
- Admin barrel (`admin/index.ts`) updated with 4 new exports.
- `admin-students` page row actions migrated to `sp-admin-table-actions` (projected content with conditional actions).
- 24 new Angular tests added in `admin-components.spec.ts` (Phase 10X-F block).
- Playwright tests updated to open row action dropdown before interacting with action items (reset, archive, view, edit).

### Gates

- Angular build: ✅ passed
- Angular tests: ✅ 373 passed (up from 349)
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ 188 passed

### Not implemented in 10X-F

- Full admin page refactor/redesign (10X-G)
- Usage governance UX (TODO-10R-F), AI Usage redesign (TODO-10U), prompt playground (TODO-10V)
- Notification platform, enterprise auth, billing, placement/mastery engine

---

## Previous sprint

**Phase 10X-E — Adapt Real TailAdmin Patterns into sp-admin-* Wrappers** - complete (2026-06-18)

Goal: replace custom CSS approximations in all 15 `sp-admin-*` wrappers with actual TailAdmin free Angular template class structures. Admin feature pages unchanged — they continue using `sp-admin-*` only.

### Delivered

- All 15 wrapper components adapted to TailAdmin source patterns:
  layout (`min-h-screen xl:flex`), sidebar (`fixed w-[290px]/w-[90px]`), header (`sticky top-0 z-[99999]`),
  button (brand-500 primary / outline), badge (light variant color map), card (`rounded-2xl border border-gray-200`),
  stat-card, input (`h-11 rounded-lg border border-gray-200`), select, form-field, modal (`rounded-3xl`, `bg-gray-400/50 backdrop-blur-sm`),
  table (`rounded-2xl`, th `text-xs bg-gray-50`), pagination, filter-bar, drawer.
- Fixed TypeScript parse error: HTML comment inside `@Component({})` decorator in `sp-admin-badge.component.ts`
  (converted to `//` TS comments above decorator).
- Added 15 new TailAdmin-backed pattern tests in `admin-components.spec.ts` (Angular: 334 → 349).
- Updated `docs/architecture/admin-tailadmin-adapter-inventory.md`: all adapted wrappers marked `✅ Done`.
- Updated `docs/architecture/admin-ui-design-system.md`: Phase 10X-E note, wrapper API stability.
- Engineering review saved to `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md`.

### Gates

- Angular build: ✅ passed
- Angular tests: ✅ 349 passed (0 failed)
- .NET tests: ✅ 1885 passed (3 arch + 1233 unit + 649 integration)
- Playwright: ✅ (see Playwright gate status in product-state)

### Not implemented in 10X-E

- Table sorting, dropdown, theme toggle wiring (10X-F)
- Usage governance UX, AI Usage redesign, prompt playground, notification platform, enterprise auth, billing, placement engine

---

## Previous sprint

**Phase 10X-D — TailAdmin Template Import & Adapter Plan** - complete (2026-06-18)

Goal: import the free TailAdmin Angular template as a vendor reference, document the adapter boundary, and create the mapping inventory that drives 10X-E/10X-F.

### Delivered

- Cloned TailAdmin free Angular template into `templates/tailadmin/free-angular-tailwind-dashboard/` (commit da992cf, MIT license).
- Removed nested `.git` directory; added `.gitignore` to exclude `node_modules/dist/.angular/coverage`.
- Added `templates/README.md` and `templates/tailadmin/README.md` with source URL, commit, license, allowed/disallowed adapter rules, and update process.
- Updated `docs/architecture/admin-ui-design-system.md`: closed TODO-10X-ASSETS, updated folder structure, vendor source location, and adapter reference.
- Created `docs/architecture/admin-tailadmin-adapter-inventory.md`: full mapping table (TailAdmin pattern → sp-admin-* wrapper, phase, status, notes).
- Updated `docs/sprints/current-sprint.md` and `docs/handoffs/current-product-state.md`.
- Created engineering review at `docs/reviews/2026-06-18-phase-10x-d-tailadmin-template-import-adapter-plan-review.md`.
- Angular build, Angular tests, Playwright, and .NET tests all passed (gates below).

### Not implemented in 10X-D

- Full wrapper replacement using TailAdmin source (10X-E)
- Full admin page refactor (10X-F)
- Usage governance UX, AI Usage redesign, prompt playground, notification platform, enterprise auth/security, observability stack, billing, StudentProfile.CefrLevel migration, full placement engine, full mastery engine

---

## Previous sprint

**Phase 10X-C-F — TailAdmin Layout One Gate Closure** - complete (2026-06-18)

Goal: close Phase 10X-C, fix the critical ViewEncapsulation bug, verify all gates,
and confirm the admin shell now matches TailAdmin Angular Layout One structurally.

### Delivered

- Fixed `AdminAppLayoutComponent` to use `ViewEncapsulation.None` so shell CSS
  (sidebar, nav, header, drawer, profile flyout) reaches child component DOM.
- Raised `anyComponentStyle` budget in `angular.json` from 8kB → 12kB warning / 20kB error
  to accommodate the legitimate admin shell CSS.
- Angular production build: passing.
- Angular unit tests: 334 passed.
- Playwright: 188 passed.
- .NET tests: 1885 passed (3 arch + 1233 unit + 649 integration).
- Visual screenshots confirmed: sidebar left, content right, header sticky, collapsed state,
  mobile drawer — all match TailAdmin Layout One structure.
- Updated `docs/architecture/admin-ui-design-system.md` with TailAdmin as visual source of truth,
  ViewEncapsulation note, asset status, legacy style rules, and migration checklist.
- Updated `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`, TODOS.md.
- Engineering review saved to `docs/reviews/`.

### Remaining legacy areas (unchanged from 10X-B)

- Dashboard still has large component-local CSS for action cards, KPI layout, and placeholders.
- Student edit/reset modals still use page-local modal markup.
- AI Config and Integrations still retain page-local form internals inside wrapper cards.
- Curriculum create/edit/preview subviews still have page-local form markup.

---

## Previous sprint

**Phase 10X-B - Admin Core Page Migration to Design System** - complete (2026-06-18)

Goal: migrate existing admin pages to the reusable SpeakPath admin wrapper system without adding new business features.

### In scope

- Migrate core admin pages to `sp-admin-*` wrappers where feasible.
- Reduce direct feature-page use of common TailAdmin-style utility lists.
- Improve wrapper capability only where required by migration.
- Preserve existing admin functionality.
- Add Angular wrapper-presence tests.
- Add a stable Playwright mobile overflow check for migrated admin pages.
- Update architecture, handoff, review, and TODO docs.

### Out of scope

Full usage-governance UX, full AI Usage redesign, notification platform, enterprise auth, prompt playground, observability stack, billing, StudentProfile CEFR migration, full placement engine, full mastery engine, and new backend business logic.

### Architecture decisions

- Feature pages import wrappers from `src/app/admin`.
- TailAdmin-specific style decisions stay behind wrappers and tokens.
- Student UI remains separate.
- Tables that need custom projected rows use `sp-admin-table` projection.
- Page-local CSS remains allowed for unique workflows, but common cards, headers, badges,
  filters, buttons, tables, empty, loading, and error states should use wrappers.

### Delivered

- Wrapper improvements: projected table mode, `primary` badge tone, empty-state title,
  and improved filter-bar alignment.
- Migrated pages: Dashboard, Students, AI Config, AI Usage, Prompts, Exercise Types,
  Integrations, Diagnostics, Curriculum, and Usage Policies.
- Added `admin-wrapper-migration.spec.ts` plus wrapper assertions in existing admin specs.
- Added Playwright mobile overflow coverage for `/admin/students` and `/admin/ai-config`.

### Remaining legacy internals

- Dashboard still carries large inline component CSS.
- Student edit/reset/archive modals still use page-local modal markup.
- AI Config and Integrations still retain page-local form internals inside wrapper cards.
- Curriculum create/edit/preview subviews still retain page-local form internals.

See: `docs/architecture/admin-ui-design-system.md`
See: `docs/reviews/2026-06-18-phase-10x-b-admin-core-page-migration-review.md`

---

## Most recently completed sprint

**Phase 10R — Usage Governance, Token Tracking & Quota Enforcement** — complete (2026-06-18)

Phase 10R introduces enterprise-grade usage governance. Every AI feature call is tracked per student. Admins can define quota policies with daily limits per feature. Expensive AI calls are blocked before they incur cost when a student's quota is exhausted. The system distinguishes prepared learning (always allowed) from expensive on-demand AI (gated).

### What was built

**Domain**
- 4 new enums: `FeatureCategory`, `EnforcementMode`, `UsageUnitType`, `UsagePolicyScopeType`.
- 7 new entities: `FeatureDefinition`, `UsagePolicy`, `UsagePolicyRule`, `StudentPolicyAssignment`, `UsageEvent` (append-only ledger), `StudentUsageDaily` (upserted aggregate), `AdminAuditLog`.

**Persistence**
- 7 new EF Core configurations.
- EF migration `Phase10R_UsageGovernance`.
- `UsageGovernanceSeeder` — seeds 16 feature definitions and 3 policies idempotently.

**Application layer**
- `IUsageQuotaService` — `CheckAsync`, `RecordAsync`, `GetUsageSummaryAsync`, `GetEffectivePolicyAsync`.
- `QuotaDecision` — result with Allowed flag, limits, AvailableAlternatives.
- `QuotaExceededException` — wraps QuotaDecision for middleware mapping.
- `IUsageGovernanceAdminService` — feature definitions, policy CRUD, student assignment.

**Infrastructure**
- `UsageQuotaService` — policy resolution (student → global default), HardLimit enforcement against `StudentUsageDaily`, event recording.
- `UsageGovernanceAdminService` — policy CRUD with audit log writes.
- `AiExecutionService` — pre-call `CheckAsync` for 8 expensive features; post-call `RecordAsync`; failed calls not recorded.

**API**
- `AdminUsageGovernanceController` — 8 endpoints under `/api/admin/` (feature definitions, policy CRUD, student assignment, usage summary).
- `GlobalExceptionMiddleware` — `QuotaExceededException` → HTTP 429 with structured body.

**Angular**
- `UsageGovernanceService` — HTTP client for all 8 admin endpoints.
- `AdminUsagePoliciesComponent` — create/edit policy form with signals; policies table.
- Route: `/admin/usage-policies`.

**Tests**
- 16 DB-layer integration tests (`UsageGovernanceDbTests`): seeding idempotency, record+aggregate, HardLimit blocking, TrackOnly pass-through, policy resolution, audit log, daily rollup, usage summary, admin CRUD.
- 5 HTTP endpoint integration tests (`UsageGovernanceEndpointTests`): list features, 403, create policy, student usage, non-admin 403.
- 8 Angular component unit tests + 6 service HTTP tests.

### Gates at completion
- Architecture: 3 passed
- Unit: 1233 passed
- Integration: 649 passed
- Total .NET: 1885 passed, 0 failed
- Angular: 302 passed, 0 failed
- Angular prod build: clean

### What is intentionally NOT in Phase 10R
Workspace/cohort policy inheritance, billing integration, provider pricing tables, monthly/weekly limit enforcement, CSV export, student-facing usage widget, notification platform, enterprise auth overhaul.

See: `docs/reviews/2026-06-18-phase-10r-usage-governance-token-quota-review.md`
See: `docs/architecture/usage-governance.md`

---

## Previously most recently completed sprint

**Phase 10Q — Admin Curriculum, Format & Context Controls** — complete (2026-06-18)

Phase 10Q gives admins full CRUD control over the curriculum syllabus used for CEFR-aware activity routing. Includes a non-mutating routing preview, seeder protection for admin-edited objectives, and a full Angular admin UI.

### What was built

**Domain**
- `CurriculumObjective` — added `ExamplePrompts` (string?) and `AdminUpdatedAt` (DateTimeOffset?) properties.
- Added `AdminUpdate()` method: validates CEFR, skill, difficulty band, self-prerequisites; sets `AdminUpdatedAt = UtcNow`.
- Existing `UpdateDetails()` preserved as seeder-safe (never sets `AdminUpdatedAt`).

**EF Migration**
- `T52_CurriculumObjectiveAdminFields` — adds nullable `example_prompts` and `admin_updated_at` columns to `curriculum_objectives`.

**Application layer**
- `AdminCurriculumContracts.cs` — `AdminCurriculumObjectiveDto`, `AdminCurriculumObjectiveUpsertRequest`, `CurriculumTaxonomyDto`, `AdminRoutingPreviewRequest`, `AdminRoutingPreviewResult`, `ICurriculumObjectiveWriteService`, `IAdminCurriculumSyllabusQuery`.

**Infrastructure**
- `CurriculumObjectiveWriteService` — Create, Update, Activate, Deactivate, PreviewRouting. Full validation: slug key format, CEFR, skill, context tag, difficulty band 1–5, self-prerequisite, dangling prerequisite. `PreviewRoutingAsync` is read-only: calls routing service but never calls `SaveChangesAsync` or mutates student data.
- `CurriculumSyllabusQueryService` — now implements both `ICurriculumSyllabusQuery` and `IAdminCurriculumSyllabusQuery`. `GetAllObjectivesForAdminAsync` returns active and inactive objectives with optional filters.
- `DependencyInjection.cs` — dual-interface registration for `CurriculumSyllabusQueryService`; registered `ICurriculumObjectiveWriteService`.

**Seeder**
- `CurriculumObjectiveSeeder` — changed from upsert to seed-only-missing: `if (existing.ContainsKey(def.Key)) continue;`. Admin-edited objectives are never overwritten on startup.

**API**
- `AdminCurriculumController` — 8 endpoints:
  - `GET /api/admin/curriculum/objectives` (cefrLevel, skill, isActive filters)
  - `GET /api/admin/curriculum/objectives/{key}`
  - `GET /api/admin/curriculum/taxonomy`
  - `POST /api/admin/curriculum/objectives`
  - `PUT /api/admin/curriculum/objectives/{key}`
  - `POST /api/admin/curriculum/objectives/{key}/activate`
  - `POST /api/admin/curriculum/objectives/{key}/deactivate`
  - `POST /api/admin/curriculum/routing-preview`

**Angular**
- `CurriculumService` — Angular service with all TypeScript interfaces and 8 methods matching the backend API.
- `AdminCurriculumComponent` — single standalone component with `view` signal (`list` | `create` | `edit` | `preview`). List with CEFR/skill/active filters; create/edit form with all fields; non-mutating routing preview panel. No TailAdmin migration.
- `app.routes.ts` — added `/admin/curriculum` route.
- `AdminShellComponent` — added Curriculum nav link.

**Tests**
- 20 unit tests in `AdminCurriculumObjectiveUnitTests`: AdminUpdate, AdminUpdatedAt, ExamplePrompts, DifficultyBand, self-prerequisite, seeder-safe UpdateDetails, general_english/workplace constants.
- 20 integration tests in `AdminCurriculumObjectivesIntegrationTests`: list/filter, taxonomy, create (valid/invalid CEFR/skill/self-prereq/dangling-prereq), update, deactivate/reactivate, non-admin 403, routing preview non-mutation, general_english default, seeder idempotent.
- 16 Angular unit tests in `admin-curriculum.component.spec.ts`: list renders, filter calls API, activate/deactivate, create/edit navigation, form population, routing preview, error state, taxonomy loading.

### Product rules enforced
- `general_english` remains default/fallback — `workplace` is never silently added as default.
- Deactivating an objective does NOT delete historical records (soft lifecycle only).
- Seeder never overwrites admin-edited objectives.
- Routing preview is non-mutating: no AI calls, no student state changes.

---

## Previous sprint

**Phase 10P — Multi-skill Scoring & Progress Updates** — complete (2026-06-18)

Phase 10P adds a safe, testable multi-skill progress update foundation. Activity attempts now update `StudentSkillProfile` rows for all skills trained by an exercise, not just the primary skill.

### What was built

**Application layer**
- `IMultiSkillProgressService` — new interface in `LinguaCoach.Application.Activity` with `ApplyAsync` and `BuildRequest` methods.
- `MultiSkillProgressUpdateRequest` / `MultiSkillProgressUpdateResult` — input/output contracts.

**Infrastructure layer**
- `MultiSkillProgressService` — implementation in `LinguaCoach.Infrastructure.Activity`.
  - Skill registry: 19 known keys (9 general + 10 workplace-specific). No workplace default.
  - Weighting: primary 70%, secondaries share 30% equally. Primary-only → 100%.
  - Score-to-delta: 0-100 score mapped to −1..1 delta centred on 60; scaled by 10 points/unit.
  - ActivityType fallback map for 6 activity types when no pattern metadata available.
  - Incomplete attempts skipped (no skill writes on failed/abandoned submissions).
  - Best-effort: exceptions swallowed, never blocks activity submission.
- `ActivitySubmitHandler` — injected `IMultiSkillProgressService`. Called after both the pattern evaluation path and the legacy AI path.
  - Pattern path: uses `ExercisePatternDefinition.PrimarySkill` + `SecondarySkillsJson`.
  - Legacy path: falls back to `ActivityType` fallback map.

**Tests**
- 20 unit tests in `MultiSkillProgressServiceTests`: weighting splits, deduplication, unknown keys, incomplete attempts, ActivityType fallback, no-workplace-default, lower-level content.
- 10 integration tests in `MultiSkillProgressServiceIntegrationTests`: DB writes, multi-skill rows, listening+writing, speaking roleplay, writing+grammar+vocab, pattern override, idempotent update.

### Previous sprint

**Phase 10O-F — Practice Gym UI Integration & Completion Consumption Wiring** — complete (2026-06-18)

Phase 10O-F connects the 10O backend suggestion API to the Angular Practice Gym UI and wires completed activities back to readiness pool consumption.

### What was built

**Angular UI**
- `PracticeGymSuggestionsService` — new Angular service with `getSuggestions()`, `startSuggestion()`, `completeSuggestion()` methods targeting `/api/practice-gym/suggestions`.
- `PracticeGymComponent` — extended with suggestion state signals, `loadSuggestions()`, `startSuggestion()` flow, and `routingLabel()` for student-friendly reason display.
- Practice Gym template — added Suggested for you, Continue practice, and Review practice sections with cards showing title, skill, CEFR level, estimated duration, context tags, and routing label. Empty/loading/error states. Existing By skill and By exercise type sections preserved.

**Backend wiring (TODO-014)**
- `ActivitySubmitHandler` — injected `IPracticeGymSuggestionService`. Added `TryConsumeReadinessItemAsync` helper called best-effort after all activity completion paths (WritingScenario/AI, VocabularyPractice, ListeningComprehension, pattern evaluation). Consumption only fires when `evalResult.Completed` is true for pattern path.
- Idempotent: `TryMarkConsumedAsync` no-ops if item is already consumed. Exceptions swallowed so completion response is never blocked.

**Tests**
- 4 new integration tests in `ReadinessConsumptionWiringTests`: completion marks item consumed, idempotent, no-item path succeeds, consumed item absent from suggestions.
- 12 new Angular unit tests in `practice-gym.component.spec.ts`: suggestions load, empty/error states, section rendering, start navigation, routing labels, existing sections preserved under error.

### Previous sprint

**Phase 10O — Practice Gym Suggested Practice & Pool Serving** — complete (2026-06-18)

Phase 10O connects the readiness pool to the student-facing Practice Gym. Students now receive personalised suggestion cards from their pre-filled pool, organised into Suggested, Continue, and Review sections, instead of only the static skill/exercise-type launcher.

### What was built

**Application layer**
- `IPracticeGymSuggestionService` — interface: `GetSuggestionsForStudentAsync`, `StartSuggestionAsync`, `TryMarkConsumedAsync`.
- `PracticeGymSuggestionsDto` / `PracticeGymSuggestionItemDto` / `StartSuggestionResult` — student-facing DTOs with full routing metadata, call-to-action labels, and navigation targets.

**Infrastructure layer**
- `PracticeGymSuggestionService` — pool query, in-memory ranking (focus match → context match → priority → expiry → FIFO), section partitioning, reservation with concurrency retry, best-effort consumption.

**API layer**
- `PracticeGymSuggestionsController` — three new student-facing endpoints:
  - `GET /api/practice-gym/suggestions` — personalised suggestion cards.
  - `POST /api/practice-gym/suggestions/{id}/start` — reserve item, return navigation target.
  - `POST /api/practice-gym/suggestions/{id}/complete` — best-effort mark consumed.
- Existing `GET /api/activity/practice-gym/next` unchanged.

**DI**
- `IPracticeGymSuggestionService` registered as Scoped in `DependencyInjection.cs`.

### Tests
- 14 new unit tests in `PracticeGymSuggestionServiceTests.cs`: exclusion rules, section assignment, reservation idempotency, consumed/failed status handling, TryMarkConsumed.
- 10 new integration tests in `PracticeGymSuggestionIntegrationTests.cs`: DI registration, GET suggestions sections, POST start/complete lifecycle, smoke tests for existing paths, admin write-endpoint absence.

### Gates at completion
- Architecture: 3 passed
- Unit: 1174 passed (was 1160, +14)
- Integration: 597 passed (was 587, +10)
- Total: 1774 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in Phase 10O.

### What is intentionally NOT in Phase 10O
Angular/frontend integration (API is ready; deferred to a follow-on frontend phase), `TryMarkConsumedAsync` wiring in `ActivitySubmitHandler` (deferred — see TODOS.md TODO-010), admin write endpoints, full mastery/placement engine, `StudentProfile.CefrLevel` migration, plus-level persistence, notification system, usage/quota enforcement.

See: `docs/reviews/2026-06-18-phase-10o-practice-gym-suggested-practice-review.md`

---

## Previously most recently completed sprint

**Phase 10N — Background Replenishment Pipeline** — complete (2026-06-17)

Phase 10N builds the background engine that keeps student readiness pools healthy. Pool items are now swept for expiry, orphaned generating items are recovered, failed items are retried within attempt limits, and shortfalls are filled for Today lesson and Practice Gym pools.

### What was built

**Application layer**
- `ReadinessPoolReplenishmentOptions` — configuration bound from `appsettings.json` under `"ReadinessPool"`. Defaults: target 10 items per pool, 14-day expiry, 2-hour reserved timeout, 30-min generating timeout, 60-min retry delay, 50 max items per run. `EnableReviewScaffoldGeneration=false` (conservative default).
- `PoolHealthSummary` — lightweight health DTO: ready count, in-flight count, shortfall, target, needsReplenishment flag.
- `IReadinessPoolReplenishmentService` + `ReplenishmentRunSummary` — service contract for running the full maintenance cycle and querying health per student/source.

**Infrastructure layer**
- `ReadinessPoolReplenishmentService` — full engine:
  - Sweeps expired ready items (past `ReadyItemExpiryDays`).
  - Sweeps expired reserved items (past `ReservedItemExpiryHours`).
  - Recovers orphaned generating items (past `GeneratingTimeoutMinutes` → Failed).
  - Retries failed items (`AttemptCount < MaxGenerationAttempts` and past delay → new Queued item).
  - Fills shortfalls for each active student × source using routing recommendations.
  - Prevents duplicates: skips items already Queued/Generating/Ready/Reserved for same objective/pattern/CEFR.
  - Review/scaffold only enabled when `EnableReviewScaffoldGeneration=true` AND ledger weak events exist.
  - B2 students never silently receive B1 Normal content.
  - `general_english` remains fallback; workplace is not default.
- `ReadinessPoolReplenishmentJob` — Quartz job, every 20 minutes, `[DisallowConcurrentExecution]`.

**DI + Quartz**
- `AddInfrastructure(IConfiguration?)` — now accepts optional config to bind `ReadinessPoolReplenishmentOptions`.
- `QuartzConfiguration` — `ReadinessPoolReplenishmentJob` trigger added (every 20 min).
- `DependencyInjection.cs` — `IReadinessPoolReplenishmentService` registered as Scoped.

**Admin API**
- `GET /api/admin/students/{studentId}/readiness-pool/health` — new read-only endpoint. Returns health for `TodayLesson` and `PracticeGym` pools: target, ready, in-flight, failed, stale, expired, shortfall, needsReplenishment.

### Tests
- 16 new unit tests in `ReplenishmentOptionsTests.cs`: options defaults, pool health math, status exclusion rules, lower-level content guard, routing snapshot preservation, general_english fallback, retry attempt gating.
- 11 new integration tests in `ReplenishmentIntegrationTests.cs`: service DI, health counts by status, ReviewOnly exclusion, expired exclusion, queued/generating in-flight counting, retry path, admin health endpoint, admin pool endpoint (smoke), 10M lifecycle smoke, unknown student zero counts, two-student isolation.

### Gates at completion
- Architecture: 3 passed
- Unit: 1160 passed (was 1144, +16)
- Integration: 587 passed (was 576, +11)
- Total: 1750 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in Phase 10N.

### What is intentionally NOT in Phase 10N
Practice Gym suggested UI redesign, admin write endpoints, `StudentProfile.CefrLevel` migration, plus-level persistence, full mastery engine, full placement engine, serving from pool on user-facing paths (deferred to 10O), `AllowReviewOrScaffold=true` enabled by default, per-student review/scaffold signal, ledger-weighted skill rotation.

See: `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md`

---

## Previously most recently completed sprint

**Phase 10M — Student Activity Readiness Pool Foundation** — complete (2026-06-17)

Phase 10M introduces the persisted lifecycle model for pre-generated Today lessons and Practice Gym activities. Activities are no longer treated as simple one-off outputs; every generated item is tracked in a student-specific readiness pool with lifecycle status, routing snapshot, and personalisation metadata.

### What was built

**Domain layer**
- `ReadinessPoolStatus` enum: Queued, Generating, Ready, Reserved, Consumed, Expired, Failed, Stale, ReviewOnly.
- `ReadinessPoolSource` enum: TodayLesson, PracticeGym, LessonBatch, Review, Remediation, OnDemand.
- `RoutingReason` enum — moved from `Application.Curriculum` namespace to `Domain.Enums` so it can be used by the domain entity without a circular dependency.
- `StudentActivityReadinessItem` entity with full lifecycle transition methods (MarkGenerating, MarkReady, MarkFailed, Reserve, MarkConsumed, Expire, MarkStale, MarkReviewOnly, LinkMaterializedIds).
- Guard: `IsLowerLevelContent=true` requires a non-Normal RoutingReason. B2 students cannot silently receive B1 content as Normal.
- `IsServableAsNormalContent` and `IsServableAsReview` helper properties.

**Persistence layer**
- `StudentActivityReadinessItemConfiguration` — EF table `student_activity_readiness_items`, snake_case columns, 4 indexes (student/status/source, student/status/priority, activity id, session id).
- Optimistic concurrency token (PostgreSQL xmin) registered in `OnModelCreating`.
- `DbSet<StudentActivityReadinessItem>` added to `LinguaCoachDbContext`.
- EF migration `T51_StudentActivityReadinessPool`.

**Application layer**
- `IStudentActivityReadinessPoolService` — interface with full lifecycle + query methods.
- `CreateReadinessItemRequest` DTO.
- `ReadinessPoolSummary` / `ReadinessItemDto` — for admin inspection.
- `ReadinessItemRequestBuilder.FromRoutingRecommendation` — helper to build pool item request from a `CurriculumRoutingRecommendation` + student context snapshot.

**Infrastructure layer**
- `StudentActivityReadinessPoolService` — implementation with optimistic concurrency retry loop on reservation.
- Registered as `AddScoped<IStudentActivityReadinessPoolService, StudentActivityReadinessPoolService>()`.

**Integration points**
- `PracticeGymGenerationJob` — creates Queued→Generating→Ready pool item with routing snapshot per materialized cache row.
- `LessonBatchGenerationJob` — creates Queued→Generating→Ready pool item per materialized session. `BuildCompactSummaryAsync` now returns `(summaryJson, routing)` tuple.
- `ActivityMaterializationJob` — links generated `LearningActivityId` and `SessionExerciseId` to any matching pool item by `LearningSessionId`.

**Admin endpoint**
- `GET /api/admin/students/{studentId}/readiness-pool` — read-only pool inspection (Admin role only). No write endpoints.

### Tests
- 20 new unit tests in `ReadinessPool/StudentActivityReadinessItemTests.cs`.
- 11 new integration tests in `ReadinessPool/ReadinessPoolIntegrationTests.cs`.
- 1 existing integration test updated (`LessonBatchGenerationJobTests`) to pass pool service.
- 1 existing unit test file updated (`CurriculumRoutingServiceTests`) and 1 integration test file (`CurriculumRoutingIntegrationTests`) to use `RoutingReason` from `Domain.Enums`.

### Gates at completion
- Architecture: 3 passed
- Unit: 1144 passed (was 1124, +20)
- Integration: 576 passed (was 565, +11)
- Total: 1723 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed in this phase.

### What is intentionally NOT in this phase
Full background replenishment engine, Practice Gym suggested UI redesign, admin write endpoints for pool, `StudentProfile.CefrLevel` migration, plus-level persistence, full placement engine, full mastery engine, `AllowReviewOrScaffold=true` enabling in handlers. See TODOS.md.

See: `docs/reviews/2026-06-17-phase-10m-student-activity-readiness-pool-review.md`

---

## Previously most recently completed sprint

**Phase 10L — CEFR-Aware Activity Routing** - complete (2026-06-17)

Phase 10L introduces a pure application-layer routing policy that selects suitable
CEFR bands and curriculum objectives before every AI activity generation call.

### What was built

**Routing models (Application layer)**
- `CurriculumRoutingRequest` — input: student context, CEFR, skill, source, goal context, preferences.
- `CurriculumRoutingRecommendation` — output: target CEFR, allowed levels, objective key/title, context/focus tags, difficulty band, `RoutingReason`, `IsLowerLevelContent`, explanation.
- `ICurriculumRoutingService` — interface with `RecommendAsync` and `NormalizeCefrLevel`.

**Routing service (Infrastructure layer)**
- `CurriculumRoutingService` — CEFR normalization (B2+ → B2), candidate selection from `ICurriculumSyllabusQuery`, skill filter, difficulty band mapping, lower-level guard (never silently lowers level), non-workplace fallback.
- `CurriculumRoutingRequestFactory` — static helper building requests from `StudentProfile` + resolved goal context.

**AI prompt integration**
- `ActivityGenerationContext` extended: `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold`.
- `AiActivityGeneratorHandler` injects `routingContext` and `routingReason` into AI variables.
- `DbPromptAiContextBuilder` appends routing context before "Return ONLY" in rendered prompt.

**Integration points wired**
- `ActivityGetHandler.HandlePatternKeyedAsync` — on-demand and Practice Gym pattern routing.
- `ExercisePrepareHandler` — Today's Lesson exercise preparation.
- `PracticeGymGenerationJob.MaterializeAsync` — background Practice Gym generation.
- `ActivityMaterializationJob.MaterializeExerciseAsync` — background lesson batch materialization.
- `LessonBatchGenerationJob.BuildCompactSummaryAsync` — AI lesson planning summary now includes routing metadata.

**DI:** `ICurriculumRoutingService` registered as Scoped.

### Tests

- 16 new unit tests in `CurriculumRoutingServiceTests.cs`.
- 7 new integration tests in `CurriculumRoutingIntegrationTests.cs`.
- 1 existing integration test updated (`LessonBatchGenerationJobTests`) to pass routing service.

### Gates at completion
- Architecture: 3 passed
- Unit: 1124 passed (was 1098, +26)
- Integration: 565 passed (was 555, +10)
- Total: 1692 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed.

### What is intentionally NOT in this phase
Readiness pools, background replenishment lifecycle, Practice Gym suggested UI redesign, admin curriculum write UI, `StudentProfile.CefrLevel` migration, plus-level routing persistence, full placement engine, `AllowReviewOrScaffold=true` in handlers (built but always false — enablement belongs to 10M adaptive routing), session length influence on candidate count, CEFR-aware format matrix.

See: `docs/reviews/2026-06-17-phase-10l-cefr-aware-activity-routing-review.md`

---

## Previously most recently completed sprint

**Phase 10K-F — Profile Preference Enforcement Audit & Routing Fix** - complete (2026-06-17)

Phase 10K-F audits and fixes preference propagation across all AI generation surfaces before Phase 10L CEFR-aware routing.

### What was built

**Audit findings**
- Full preference propagation audit across 5 AI generation surfaces, evaluation path, and background jobs.
- `LearnerPreferenceContextFormatter`, `LearningGoalContextResolver`, `CurriculumContextMapper` all confirmed correct for generation paths.
- Three gaps found and fixed.

**Fix P1 — evaluation context carries zero preference data**
- Added `LearnerPreferenceContext` and `LearningGoalContext` optional fields to `ActivityEvaluationContext` (`IAiActivityGenerator.cs`).
- `ActivitySubmitHandler` now passes both fields (formatter + resolver output) into evaluation context.
- `AiActivityGeneratorHandler.EvaluateAttemptAsync` now includes `learnerPreferences` and `learningGoalContext` in prompt variable dict.
- AI evaluation for `WritingScenario` and `SpeakingRolePlay` can now reflect student difficulty preference, support language, and learning goals.

**Fix P2 — vocabulary cadence unconditionally routed to `GapFillWorkplacePhrase`**
- `ActivityGetHandler` vocabulary cadence pick now gates on `WorkplaceSpecific` from `ILearningGoalContextResolver`.
- Non-workplace students receive `PhraseMatch`; workplace students receive `GapFillWorkplacePhrase`.
- A student with Day-to-day English / Travel / Social goals will no longer receive workplace-labelled vocabulary by default.

**Fix P3 — `PreferredSessionDurationMinutes` absent from batch generation summary**
- Added `preferredSessionDurationMinutes` to `learnerPreferences` object in `LessonBatchGenerationJob.BuildCompactSummaryAsync`.
- AI lesson planner now receives session length preference as a hint.

**Tests**
- 20 new unit tests in `PreferenceEnforcementTests.cs`.
- Covers: general English fallback, workplace/non-workplace goal gates, formatter output for goals/focus/support language/difficulty, `CurriculumContextMapper` null guard, vocabulary cadence pattern key selection.

### Gates at completion
- Backend: 1098 unit + 555 integration + 3 architecture = 1656 passed, 0 failed
- Angular/Playwright: blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed.

### What is intentionally NOT in this phase
Full 10L CEFR-aware routing, exercise format locking, readiness pools, background replenishment lifecycle, Practice Gym suggested UI redesign, admin curriculum write UI, StudentProfile.CefrLevel migration, plus-level routing, full placement engine.

See: `docs/reviews/2026-06-17-phase-10k-f-profile-preference-enforcement-review.md`

---

## Previously most recently completed sprint

**Phase 10K — Curriculum Boundary / Level Syllabus Foundation** - complete (2026-06-17)

Phase 10K introduces the curriculum syllabus data model and query foundation. It defines what the system is recommended to teach at each CEFR level, skill, learner goal/context, and focus area. No CEFR-aware routing, readiness pools, or background generation were implemented — this phase is foundation only.

### What was built

**Domain constants**
- `CefrLevelConstants` — canonical A1/A2/B1/B2/C1/C2 string constants with `IsValid()` helper.
- `CurriculumSkillConstants` — canonical skill identifiers (writing, reading, listening, speaking, vocabulary, grammar, pronunciation, fluency, confidence).
- `CurriculumContextTagConstants` — canonical learner context tags (general_english, day_to_day, travel, study_academic, migration_settlement, job_interviews, social_conversation, workplace, pronunciation, listening_confidence, writing_confidence, exam_inspired, custom). `workplace` is one tag among many, not the default.

**Domain entity**
- `CurriculumObjective` — single flat entity with: Key (stable), Title, Description, CefrLevel, PrimarySkill, SecondarySkillsJson, ContextTagsJson, FocusTagsJson, PrerequisiteKeysJson, RecommendedOrder, DifficultyBand (1-5), IsActive, IsReviewable, IsExamInspired, TeachingNotes.
- Full constructor validation: invalid CEFR rejected, invalid skill rejected, self-prerequisite rejected, DifficultyBand 1-5 enforced.
- `Activate()` / `Deactivate()` / `UpdateDetails()` methods.

**Persistence**
- `CurriculumObjectiveConfiguration` — EF Core config, snake_case columns, unique index on Key, composite index on (cefr_level, primary_skill, is_active).
- Migration `T50_CurriculumSyllabusFoundation` — creates `curriculum_objectives` table.
- `CurriculumObjectiveSeeder` — 22 starter objectives across A1/A2/B1/B2, all major skills, multiple learner contexts. Upserts on Key (idempotent). Post-seed prerequisite integrity validation.

**Application layer**
- `ICurriculumSyllabusQuery` — query interface: GetActiveObjectives, GetByCefr, GetByCefrAndSkill, GetByCefrAndContext, GetByCefrAndFocusArea, GetPrerequisites, GetCandidatesForStudent, GetByKey.
- `CurriculumContextMapper` — static null-safe mapper from `ResolvedLearningGoalContext` to curriculum context tags. Null input returns `[general_english]`. Non-workplace context never defaults to workplace.

**Infrastructure**
- `CurriculumSyllabusQueryService` — implements `ICurriculumSyllabusQuery` using EF Core with `AsNoTracking()`. `GetCandidatesForStudent` returns ordered candidates only — does NOT select activities or formats.

**API**
- `AdminCurriculumController` — read-only: `GET /api/admin/curriculum/objectives` (with optional `cefrLevel` and `skill` filters), `GET /api/admin/curriculum/objectives/{key}`. Admin-only. No write endpoints.

**Tests**
- Unit: 22 tests in `CurriculumObjectiveTests` and `CurriculumContextMapperTests`.
- Integration: 14 tests in `CurriculumSyllabusIntegrationTests` — seeder, query service, admin endpoint, regression smoke.

**Docs**
- `docs/reviews/2026-06-17-phase-10k-curriculum-syllabus-eng-review.md`
- `TODOS.md` — 3 deferred items: CEFR plus-levels, StudentProfile.CefrLevel migration, admin curriculum builder.

### Gates at completion
- Backend: all tests passed (architecture + unit + integration)
- Angular unit: 261 passed
- Angular build: clean
- Playwright: 187 passed
- No breaking changes to existing learner-facing behavior

### What is intentionally NOT in this phase
CEFR-aware activity routing, exercise format locking, readiness pools, background generation, Practice Gym suggested practice, admin write UI, StudentProfile CEFR migration, plus-level routing.

---

## Previously completed sprint

**Phase 10J-F — Student App Design System & Responsive UI Foundation** - complete (2026-06-17)

Phase 10J-F refactors the student-facing Angular UI into a more maintainable design-system foundation. No product behaviour changed. No backend changes.

### What was built

**Design tokens (styles.css)**
- Added `--sp-brand`, `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, `--sp-sidebar-w-collapsed`, `--sp-content-max`, `--sp-content-max-desktop`, z-index layer tokens.
- Added `sp-card-hover` utility class (was used but missing).
- Added `sp-pref-chip` / `sp-pref-chip--on` — centralised preference chip class replacing scattered inline chipStyle() strings.
- Removed duplicate `sp-bottomnav` / `sp-navbtn` definition (kept in component CSS only).

**Profile page**
- Replaced `chipStyle()` inline style method with `sp-pref-chip` CSS class bindings.
- Added `aria-pressed` to all chip buttons (learning goals, focus areas, session length, difficulty).
- Added `data-testid` per chip for test targeting.
- Added `focus-visible` outline for keyboard accessibility.

**Progress component**
- Replaced all hardcoded hex color values with design tokens (`--sp-success`, `--sp-warn`, `--sp-writing-ink`, `--sp-success-soft`, `--sp-warn-soft`, `--sp-speaking`, `--sp-canvas2`, `--sp-muted`).
- Fixed `var(--sp-writing-bg)` and `var(--sp-warn-bg)` references (those tokens don't exist).

**Practice Gym CSS**
- Replaced all `var(--sp-primary)` references with `var(--sp-brand)`.

**Shared student UI components**
- `StudentChipComponent` (`sp-chip` selector) — reusable toggle chip with `selected`, `disabled`, `toggle`.
- `StudentBadgeComponent` (`sp-badge` selector) — reusable badge with variant input.
- `src/app/shared/student-ui/index.ts` barrel export.

**Tests**
- Angular: 261 tests, 261 passed.
- Playwright: 187 tests, 187 passed (12 new in `e2e/design-system-10jf.spec.ts`).
- Backend: 1565 passed across all suites.

---

## Previously completed sprint

**Phase 10I - Configurable Multi-step Onboarding / Assessment v2 Foundation** - complete (2026-06-17)

Phase 10I introduces the v2 onboarding system running in parallel with the existing v1 state machine. Existing students are unaffected. New students receive a configurable, flow-driven onboarding experience with preliminary CEFR scoring.

### What was built

**Domain layer**
- `OnboardingFlowDefinition` entity — versioned, immutable once students have progress; single active flow enforced via PostgreSQL partial unique index.
- `OnboardingStepDefinition` entity — system-required vs admin-configured steps, typed `OnboardingAnswerMapping` enum (serialised as string in DB).
- `StudentOnboardingProgress` entity — per-student progress record with `PreliminaryCefrLevel`; xmin concurrency token; unique `(progress_id, step_key)` constraint on responses.
- `StudentOnboardingResponse` entity — idempotent answer store (upsert pattern).
- `PreliminaryCefrCalculator` — static weighted scoring (A1–C2); never overwrites a real PlacementAssessment CEFR level.

**Persistence layer**
- EF Core configurations for all 4 new entities.
- Migration T47_OnboardingV2.
- `OnboardingFlowSeeder` — idempotent; seeds 10 SystemRequired steps + 1 AdminConfigured disabled step.

**Application layer**
- Contracts: `OnboardingV2Contracts.cs` — student DTOs (no `AssessmentMetadataJson`), admin DTOs, interfaces, commands/queries/results.

**Infrastructure layer**
- `OnboardingV2QueryHandler` — lazy-creates progress; detects v1-complete students and auto-initialises as complete.
- `OnboardingV2StepHandler` — validates and applies answers; percentage counts only SystemRequired+enabled steps.
- `OnboardingV2CompleteHandler` — scores assessment responses; sets `PreliminaryCefrLevel`; calls `SetCefrLevel()` only if CefrLevel is null; transitions lifecycle to `PlacementRequired`.
- `AdminOnboardingFlowQueryHandler` — read-only GET, excludes `AssessmentMetadataJson`.

**API layer**
- `OnboardingController` extended: `GET /api/onboarding`, `POST /api/onboarding/steps/{stepKey}`, `POST /api/onboarding/complete`.
- `AdminOnboardingController` added: `GET /api/admin/onboarding/flow` (Admin role only).
- `OnboardingFlowSeeder` wired into app startup after `ExerciseTypeDefinitionSeeder`.

**Angular layer**
- `onboarding-v2.models.ts` — TypeScript interfaces for all v2 DTOs.
- `OnboardingV2Service` — `getStatus()`, `submitStep()`, `complete()`.
- `OnboardingV2Component` — shell with progress bar, dynamic step dispatch by `stepType`.
- 11 step renderer components: Welcome, PreferredName, SupportLanguage, LearningGoals, FocusAreas, DifficultyPreference, SingleChoice, MultipleChoice, FreeText, AssessmentQuestion, Summary.
- Route: `/onboarding/v2` added to `app.routes.ts`.

### Tightening rules applied (all 10)

1. `PreliminaryCefrLevel` stored separately — real `CefrLevel` only updated when null.
2. `OnboardingAnswerMapping` typed enum, not raw string keys.
3. Flow versions immutable once students have progress; admin edits create new versions.
4. Single active flow enforced by DB partial unique index.
5. Student API never exposes `AssessmentMetadataJson`, correct answers, or scoring weights.
6. Percentage counts SystemRequired+enabled steps only.
7. Post-onboarding lifecycle → `PlacementRequired`.
8. Unique `(progress_id, step_key)` constraint on `StudentOnboardingResponse`.
9. v1 `OnboardingStatus`/`OnboardingStep` fields preserved as legacy compatibility.
10. Documented limitations: no full CEFR engine, no admin visual builder, no curriculum routing.

### Final test counts (CI green)

- Backend: 863 unit + 511 integration passed (net gain from v2 handler tests).
- Angular: production build clean (CSS warnings pre-existing, no errors).
- Architecture tests: not rebuilt (build artefact absent; unit/integration cover coverage).

Known limitations: no Playwright E2E spec for v2 onboarding flow (no test user seeded with v2 progress); no admin visual flow builder; preliminary CEFR is a simple weight band, not a full adaptive engine; no curriculum routing post-onboarding.

---

## Previously most recently completed sprint

**Phase 10H-F - Practice Gym Playwright Fixture Stabilisation** - complete (2026-06-17)

Phase 10H-F restored the full Playwright suite as a reliable gate after the
catalog-driven Practice Gym UI and related renderer/result copy had moved ahead
of older E2E fixtures.

### Failure categories found

- Selector drift: tests still looked for old fixed Practice Gym card IDs such
  as `practice-card-listening` and `speaking-card`.
- Fixture/test data drift: Practice Gym tests did not mock the current exercise
  type catalog shape, including planned non-runnable AI role play rows.
- Copy/label drift: the landing hero and perfect-score feedback labels had
  intentionally changed.
- Shared component selector drift: listening fallback text moved into the shared
  `app-audio-player` unavailable state.

No backend/API failures, timing issues, real UI regressions, or environment-only
failures remained after the fixes.

### What was fixed

- Practice Gym E2E tests now use current catalog-driven `practice-format-*`
  selectors.
- Practice Gym mocked exercise-type data now includes ready runnable formats and
  planned locked formats.
- Practice Gym routing assertions now verify activity startup through
  `activityId` navigation instead of obsolete module-link assumptions.
- Listening renderer tests now assert the shared `audio-unavailable` fallback.
- Landing and score-band assertions now match current user-facing copy.

### Final test counts

- Targeted affected Playwright specs: 62 passed.
- Full Playwright: 175 passed.

Known limitations: none.

---

## Previously most recently completed sprint

**Phase 10H - AI Context Personalisation from Learning Preferences** - complete (2026-06-17)

Phase 10H wires Phase 10G learning preferences into AI generation context for
Today lesson activities, Practice Gym activities, background Practice Gym
materialization, buffered lesson activity materialization, and lesson batch
planning summaries.

### What was added

- `LearnerPreferenceContextFormatter` for compact, bounded preference context.
- AI prompt rendering inserts learner preferences before JSON return instructions.
- Generation context now carries `LearnerPreferenceContext` and `LearningGoalContext`.
- Ledger events now store `LearningGoalContext` when a clear value exists.
- Dynamic pattern selection uses preference-backed goal context before legacy fallback.
- Practice Gym generic background topic fallback changed to "English class practice".

### Tests added

- Formatter tests for included fields, missing preferences, excluded admin fields,
  CEFR system-estimated wording, goal fallback order, and no workplace default.
- Prompt builder tests for learner preference insertion and token-budget failure.

See: `docs/reviews/2026-06-17-phase-10h-ai-context-personalisation-from-learning-preferences.md`

---

## Previously most recently completed sprint

**Phase 10G — Student Profile & Learning Preferences v2** — complete (2026-06-17)

Extended student profile with editable learning preferences: preferred name, support language, translation help, learning goals (multi-select + custom), focus areas (multi-select + custom), difficulty preference. New API endpoints `GET /api/profile` and `PUT /api/profile/preferences`. Angular profile page redesigned with 6 sections. CEFR remains read-only.

### What was added

- `TranslationHelpPreference` and `DifficultyPreference` enums (Domain)
- `UpdateLearningPreferences` method on `StudentProfile` — student-editable only, validates constraints
- EF migration T46 — 10 new columns on `student_profiles`, JSON columns for lists
- `GetStudentProfileQuery` + handler (Application + Infrastructure)
- `UpdateLearningPreferencesCommand` + handler (Application + Infrastructure)
- `ProfileController` with `GET /api/profile` and `PUT /api/profile/preferences`
- `ProfileService` in Angular with `getProfile()` and `updatePreferences()`
- Profile page rewritten: 6 sections (Account, Level, Goals, Focus, Support language, Preferences)
- CEFR shown read-only with per-level explanation text

### Final test counts

- Backend unit: 996 (was 985)
- Backend integration: 539 (was 534)
- Architecture: 3
- Angular unit: 243 (was 229)

See: `docs/reviews/2026-06-17-phase-10g-student-profile-learning-preferences-v2.md`

---

## Previously most recently completed sprint

**Phase 10D — Activity Quality / Workload Validation** — complete (2026-06-17)

Added quality and workload validation to ensure generated activities are meaningful and feedback wording matches the student's score.

### What was added

- `WorkloadModeRegistry` in `ModuleStageContentValidator` — classifies pattern keys as `SingleSubstantialTask` (one item is the full exercise) or `MultiItem` (multiple items expected).
- `EnforceWorkloadSanity` method — fires when `countSettings` is provided; fails multi-item formats with item count below `MinItemsPerPractice`.
- Extended `ItemCountArrayByPattern` — added `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`, `phrase_match` entries.
- `ExerciseTypeDefinitionSeeder.CountOverrides` — added `MinItemsPerPractice >= 2` for `phrase_match`, `gap_fill_workplace_phrase`, `listen_and_gap_fill`, `listen_and_answer`.
- Score-aware feedback in `PatternEvaluationResultComponent`: four-tier `scoreBandLabel`, `scoreRingColour`, new `scoreBandInstruction()`, `showImprovementPrompt` getter.
- 100% score no longer shows "Improve your answer" or "Review the corrections".

### Tests added

- 23 backend unit tests: workload validation, single-substantial-task exemptions, registry classification, item-count config enforcement, no-workplace-default check.
- 25 Angular unit tests: score-aware labels at all four tiers, 100% wording contract, `showImprovementPrompt` logic, ring colour.

### Final test counts

- Backend unit: 974 (was 951)
- Backend integration: 534
- Architecture: 3
- Angular unit: 229 (was 204)

See: `docs/reviews/2026-06-17-phase-10d-activity-quality-workload-validation.md`

---

## Previously most recently completed sprint

**Phase 10C — Ledger-aware Dynamic Pattern Selection** — complete (2026-06-17)

See commit `e01680a`.

---

## Previously completed sprint

**Phase 10B — Student Learning Memory / Taught-Content Ledger** — complete (2026-06-17)

New structured `StudentLearningEvent` ledger written after every activity submission from Today lessons and Practice Gym. Foundation for ledger-aware dynamic pattern selection.

### What was added

- `StudentLearningEvent` domain entity + `LearningEventSource` / `LearningEventOutcome` enums
- EF migration `T45_StudentLearningEvents` (new `student_learning_events` table with 3 indexes)
- `IStudentLearningLedger` application interface + `StudentLearningLedgerService` infrastructure implementation
- Hooked into `ActivitySubmitHandler` at both pattern evaluation path and legacy AI path
- Query helpers: `GetRecentAsync`, `GetRecentPatternKeysAsync`, `GetWeakEventsAsync`, `GetRecentByPatternKeysAsync`
- Best-effort write — never blocks or fails student's activity submission
- No workplace default forced when context is null

### Tests added

- 10 unit tests: `StudentLearningEventTests` (domain entity validation, field storage, no-workplace-default)
- 9 integration tests: `StudentLearningLedgerServiceTests` (write/read/query/isolation)
- 4 API integration tests: ledger event written from Practice Gym and Today lesson, exercise type/pattern key captured, skill profile update still works

### Final test counts

- Backend unit: 941 (was 931)
- Backend integration: 531 (was 517)
- Architecture: 3

See: `docs/reviews/2026-06-17-phase-10b-student-learning-memory-ledger.md`

---

## Previously most recently completed sprint

**Phase 9J — Speaking/Listening Family QA & Hardening** — complete (2026-06-16)

Performed end-to-end QA and hardening across all speaking/listening formats from Phases 9A–9I.

### What was fixed
- **P0 bug:** `summarize_group_discussion` submission serialization was missing from `activity-lesson.component.ts`. The `else` fallback was serializing the entire payload object instead of `{ items: [...] }`, causing malformed `SubmittedAnswerJson` to reach the evaluator.
- **Stale comment** in `ExerciseTypeDefinitionSeeder.CountOverrides` incorrectly labelled Phase 9 speaking formats as "planned, non-runnable". Updated to "Speaking (Ready)".

### Tests added
- 3 backend unit tests: `summarize_group_discussion` AiOpenEnded evaluator coverage (high score, low score, compactContent exclusion).
- 23 Angular unit tests: new `exercise-renderer.component.spec.ts` covering renderer dispatch, content extraction, submission payload shape, and audioScript fallback for all 7 Phase 9 formats.

### Final test counts
- Backend unit: 907 (was 904)
- Backend integration: 517
- Architecture: 3
- Angular: 204 (was 181)

See: `docs/reviews/2026-06-16-phase-9j-speaking-listening-family-hardening.md`

---

## Previously most recently completed sprint

**CI/CD stabilization (post Phase 7A-7D)** — complete (2026-06-15)

Fixed all 27 failing integration tests (token budget regressions from
module_stage_v1 prompt growth + FakeAiProvider fixture missing
pattern-specific exerciseData keys) and the empty
`LinguaCoach.ArchitectureTests` project (added real NetArchTest layer-boundary
tests). Backend: 472/472 integration+unit tests passing, 3/3 architecture
tests passing. Angular: 103/103 unit tests passing, dev/prod builds succeed.
See `docs/reviews/2026-06-15-ci-cd-stabilization-review.md`.

## Previously completed sprint

**Activity 3-page restructure (Teach / Practice / Feedback), full-stack** — complete (2026-06-13)

5-step strangler-fig migration, all steps landed:

- **Step 1** — split `ActivityLessonComponent` (876-line monolith) into a thin
  orchestrator shell + 3 composed page components (`ActivityTeachPageComponent`,
  `ActivityPracticePageComponent`, `ActivityFeedbackPageComponent`). Zero
  behavior change.
- **Step 2** — added `ActivityPagePresenter` interface + `PatternBackedPresenter`
  / 4 `Legacy*Presenter` bridges, selected by `ActivityPresenterFactory.for(activity)`.
  `TeachViewModel`/`PracticeViewModel` replace boolean-flag `@Input()`s.
- **Step 3** — `gap_fill_workplace_phrase` hint parity; `VocabularyPractice`/
  `ListeningComprehension` cadence picks now route through
  `HandlePatternKeyedAsync`. See
  [2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md](2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md).
- **Steps 4 & 5** — `WritingScenario`/`SpeakingRolePlay` cadence picks now
  route through `open_writing_task`/`speaking_roleplay_turn`; new
  `AudioResponse` interaction mode; `/speaking-attempt` is pattern-aware
  (new pattern → `IPatternEvaluationRouter`/`AiOpenEndedEvaluator`, legacy →
  unchanged `SpeakingRolePlayEvaluator`). No frontend changes needed — existing
  speaking recording UI is activity-type-keyed and already serves
  `AudioResponse`. See
  [2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md](../reviews/2026-06-13-activity-3-page-step4-5-writing-speaking-pattern-review.md).

Eng plan: [2026-06-13-activity-3-page-restructure-eng-plan.md](../reviews/2026-06-13-activity-3-page-restructure-eng-plan.md).

`dotnet build`/`ng build` clean, 51/51 unit tests pass.

**Follow-ups (not blocking, tracked separately):**
- Live-AI calibration pass for the 4 new prompts (`open_writing_task`,
  `speaking_roleplay_turn` generate/evaluate) — output unverified against a
  live AI provider.
- Retire `LegacyVocabPresenter`/`LegacyListeningPresenter`/
  `LegacyWritingPresenter`/`LegacySpeakingPresenter` + their template branches
  once production activity rows are regenerated under the pattern engine
  (gated on production data review).

---

## Previously completed sprint

**Practice Gym cache race condition fix** - complete (2026-06-12)

A full code/docs audit found `ActivityGetHandler.TryAssignReadyPracticeCacheAsync` could
let two concurrent `GET /api/activity/next?pattern=...` requests claim the same `Ready`
`PracticeActivityCache` row, returning the same `LearningActivity` to both. Added `xmin`
concurrency token to `PracticeActivityCache` (mirrors existing `LearningPath` config) and
retry-with-exclusion on `DbUpdateConcurrencyException`, falling back to the next ready
row or on-demand generation. 483 unit + 434 integration tests pass. See
`docs/reviews/2026-06-12-practice-gym-cache-race-condition-fix-engineering-review.md`.

**Lesson and Practice classroom alignment** - complete (2026-06-12)

Today's Lesson now consumes ready buffered `LearningSession` rows before falling
back to lazy session generation. The lesson page no longer auto-prepares later
steps just because the student opened the page, selected a step, or completed the
previous step. Existing prepared steps still open directly.

Practice Gym now uses the same classroom framing as Today's Lesson: teach first,
practice with a supported exercise pattern, then show feedback after submit. The
Practice Gym page separates skill practice (Listening, Reading, Speaking,
Writing), vocabulary class, exercise type practice, and future live role play.
Only implemented patterns are active; Reading, vocabulary queue, live AI role
play, and future exercise types remain disabled.

Background Practice Gym caching now has both parts: `PracticeGymBufferRefillJob`
queues pending cache rows for eligible active students, and
`PracticeGymGenerationJob` materializes those rows into ready `LearningActivity`
records. `GET /api/activity/next?pattern=...` consumes ready cache entries before
generating on demand. Phrase-match and gap-fill generation prompts now require a
lesson component before practice, and lesson batch planning is constrained to the
active pattern library instead of defaulting every skill to writing.

**Lesson batch materialization fix** - complete (2026-06-12)

Production logs showed AI lesson planning succeeding, then
`LessonBatchGenerationJob` failing while saving the first generated session with
`DbUpdateConcurrencyException`. The failed save also left the same `DbContext`
dirty, so marking the batch failed could throw again and Quartz reported an
unhandled job exception. Root cause: session/activity `GenerationJobItem` rows
created after the original batch save were attached only through the aggregate's
private item collection, so EF could treat them as updates to missing rows
instead of inserts. The job now explicitly inserts those items, avoids tracked
path/module state during generated module lookup, and clears failed tracked
state before marking a materialization failure. Added integration coverage that
executes `LessonBatchGenerationJob` with a fake lesson-plan provider and verifies
a completed batch plus ready sessions. See
`docs/reviews/2026-06-12-lesson-batch-materialization-fix-engineering-review.md`.

**Admin responsive header polish** — complete (2026-06-12)

`/admin` now keeps dashboard grids tablet-safe between 900px and 930px, where the
desktop sidebar first appears. The admin header now shows only the avatar button;
email and role moved into the avatar flyout menu. Related layout rules are
documented in `docs/architecture/frontend-layout-system.md`.

**Admin stuck batch cancellation** — complete (2026-06-12)

Admins can now cancel queued/running lesson-generation batches from
`/admin/integrations` instead of running one-off SQL. The action marks the batch
`Failed` with the safe reason `Cancelled by admin.` and the background job checks
for that state before and during session materialization so it does not overwrite
an admin cancellation. Recent batches now show a Cancel button for active rows and
the existing Failure column shows the cancellation reason.

**Lesson batch generation concurrency fix** — complete (2026-06-12)

Duplicate concurrent batch triggers for the same student caused
`DbUpdateConcurrencyException` and left `GenerationBatch` rows stuck in
"Running" forever. Admin endpoint now returns 409 if a batch is already
running for the student; job marks itself `Failed` on any unhandled error
during materialization instead of getting stuck. Existing stuck rows can now be
cancelled from Admin Integrations rather than fixed by direct SQL. See
`docs/reviews/2026-06-12-admin-stuck-batch-cancel-engineering-review.md`.

**Quartz JobDataMap string-only fix (background lesson generation)** — complete (2026-06-12)

`LessonBatchGenerationJob.TriggerAsync` stored non-string values in `JobDataMap`,
which throws `JobPersistenceException` under Quartz's `UseProperties = true`
Postgres job store. This broke both the admin "Generate lessons now" button AND
the background buffer-refill pipeline — explaining why all lessons were
generated on-the-fly ("Preparing your lesson...") instead of pre-generated. See
`docs/reviews/2026-06-12-quartz-jobdatamap-string-fix-engineering-review.md`.

**Admin "Generate lessons now" button fix** — complete (2026-06-12)

Button gave no feedback on click (success or error). Frontend now shows a
confirmation with queued session count, or the server error message. See
`docs/reviews/2026-06-12-admin-generate-lessons-button-fix-engineering-review.md`.

**AI Config Overhaul / No-Fallback Rule / Journey Fix** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-11-ai-config-no-fallback-journey-fix-sprint.md`

Triggered by: post-QA audit corrections from product owner (2026-06-11).
Audit report: `docs/testing/deployed-student-e2e-audit-2026-06-11.md`

An audit on 2026-06-12 found that Tracks 1-4 and most of Track 5 had already been
delivered under other sprint names (T36 AiConfigCategories migration, Exercise UX /
Admin Polish, Real TTS). The one genuinely outstanding item — BUG-005 (dashboard streak
showing "--") — was fixed in this pass: `DashboardResult.StreakDays` computed server-side
from consecutive days with an `ActivityAttempt`, wired into the dashboard stat grid and
the header streak pill. See the sprint doc's "Status update" and "Streak implementation"
sections for full detail.

### What this sprint addressed

1. **No-fallback rule** — All AI failures return 503 + "Service not available" UI. No SystemFallback content ever shown to students.
2. **Admin AI Config overhaul** — Replace 12+ individual feature-key rows with 4 LLM category cards (Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path) + 2 independent TTS cards (Listening TTS, Placement TTS).
3. **Journey page fix** — Replace old LearningPath module cards with LearningSession history (date-grouped, per-step scores).
4. **Audio / TTS 503 handling** — Audio endpoint returns clear 404 when TTS not configured; frontend shows graceful failure. Activity audio playback fetches protected audio with Angular `HttpClient` and renders a `blob:` URL.
5. **Lower-severity QA bugs** — Mobile activity blank page, phrase-match 400, streak "--" display, sidebar layout clipping.

---

## Most recently completed sprint

**Exercise Submission Scoring Bug (CRITICAL)** — complete (2026-06-12)

See full review: `docs/reviews/2026-06-12-exercise-submission-scoring-bug-engineering-review.md`

Triggered by a production report: `gap_fill_workplace_phrase` submissions scored 0 with
every `itemResults[].studentAnswer: null`, despite correct `acceptedAnswers` being
present. Escalated by product owner to "every exercise" (not just gap fill).

Root cause: frontend item-id fallbacks (`String(index + 1)`, 1-indexed, unprefixed) did
not match backend deterministic-evaluator key conventions (`gap_{i+1}`, `phrase_{i}`/
`meaning_{i}` 0-indexed).

Fixed:
- `gap_fill_workplace_phrase` — `mapGapItems` fallback id now `gap_${index + 1}`.
- `phrase_match` — two-id-scheme redesign (`phrase_${index}` / `meaning_${index}`) across
  `exercise-renderer.component.ts`, `MatchingPair` interface, `MatchingPairsComponent`,
  and `matching-pairs.component.html`.

Not changed (verified lower risk / separate issue, see review):
- `listen_and_gap_fill` — `ListenAndGapFillItemDto.Id` exists in contract, fallback only
  exercised if AI omits `id`.
- `listen_and_answer` — `QuestionId` mismatch affects per-question feedback labels only,
  not overall AI-judged score.

AI-evaluated patterns (email_reply, teams_chat, spoken_response) were unaffected —
`SubmittedAnswerJson` is forwarded raw to the AI prompt.

Tests: `dotnet test` 480 unit + 430 integration passed; `npm run build` passed.

The separate "lesson structure" complaint (What we learn / Practice / Feedback / Redo →
next, for both Lessons and Practice) raised alongside this bug report is a
product/architecture item — not addressed here, needs its own planning pass with the
product owner.

---

## Most recently completed sprint

**Today's Lesson button / lazy LearningPath generation fix (CRITICAL)** — complete
(2026-06-12)

See full review: `docs/reviews/2026-06-12-todays-lesson-button-fix-engineering-review.md`

Root cause: `SessionGeneratorService` threw when a `CourseReady` student had no active
`LearningPath` (the legacy `/activity` flow was the only place a path got lazily
created). `SessionsController.Today` returned 400, and the dashboard silently
swallowed the error, leaving "Start today's lesson" with a null link — button did
nothing, no session/lesson ever generated for these students.

Fixed: `SessionGeneratorService` now lazily generates a `LearningPath` via
`ILearningPathGenerator` (same handler `ActivityGetHandler` already uses) when none
exists, mirroring the existing fallback there. Dashboard now surfaces session-load
errors instead of swallowing them.

Quartz/background-job config investigated as a possible cause — found correct,
no change needed.

Follow-up implemented same day: `PlacementService` now generates the student's
`LearningPath` proactively right after `CourseReady`, via `ILearningPathGenerator`
(best-effort, never blocks placement). The lazy fallback remains as a safety net
for pre-existing affected students.

Tests: 482 unit + 430 integration passing. `npm run build` passed.

---

## Current priority

**Adaptive Learning Foundation** Phase 2 (numeric `StudentSkillProfile` scores) is done
(2026-06-12) — see `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.
`StudentSkillProfile.ScorePercent` (0-100) replaces the persisted `IsWeak` boolean;
`IsWeak` is now derived (`ScorePercent < 50`). Migration `T42_StudentSkillScorePercent`
backfills existing rows. 482 unit + 430 integration tests pass.

Phase 3 planning pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-plan.md`. Most of Phase 3
(Practice rendering, redo/next loop, per-attempt AI feedback) already exists.

Phase 3 P1 done (2026-06-12): AI evaluation prompts (email_reply, teams_chat,
spoken_response, listen_and_answer) now receive the student's current
`StudentSkillProfile.ScorePercent` for the relevant skill, so `coachSummary` can be
grounded in student progress instead of generic. 482 unit + 430 integration tests pass.

Phase 3 P2 scoping pass done (2026-06-12) — see
`docs/reviews/2026-06-12-lesson-practice-structure-phase3-p2-scoping.md`. Full
"What we learn" grammar/vocab/phrases card remains deferred (cross-cutting AI prompt
work). Task 1 (surface existing `teachingNote` as `[goal]` for GapFill/MatchingPairs) and
P2b task 1 (surface learningGoal/skillFocus/targetVocabulary for email_reply/teams_chat)
both implemented same day, frontend-only. See sprint doc's "Phase 3 P2"/"P2b" sections.
suggestedPhrases for spoken_response was already implemented (confirmed
2026-06-12, no change needed). Remaining: targetGrammarPoint (large,
cross-cutting, no schedule).

---

## Adaptive Learning Foundation — vocabulary extraction widened to all activity patterns

**Adaptive Learning Foundation** — planning complete (2026-06-12); first implementation
item (vocabulary extraction) done (2026-06-12)

See `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.

Reviewed and sequenced the remaining tracks (10-14) from the 2026-06-12 product owner
brainstorm: Adaptive Onboarding & Staged Assessment, Configurable Onboarding/Placement,
Multi-Course/Enrolment Model, Estimated Known Words. All confirmed already recorded in
`docs/backlog/product-backlog.md`. Recommended sequencing: vocabulary extraction first
(already speced, independent), then numeric `StudentSkillProfile` scores, then staged
assessment architecture review, then configurable onboarding, then multi-course
(dedicated `/plan-eng-review` required). Three open product questions recorded — see
sprint doc.

Per product owner correction, vocabulary extraction was implemented as a cross-cutting
engine: `VocabularyExtractionService` now extracts from any pattern-evaluated activity
that produces AI `Corrections` (email reply, workplace chat, listen-and-answer, spoken
response), not only legacy writing attempts. Deterministic patterns (gap fill, phrase
match) are unaffected — see implementation note in the sprint doc above. Does not change
current implementation priority below.

---

## Most recently completed sprint

**Exercise UX / Admin Polish** — complete (2026-06-12)

See full sprint plan: `docs/sprints/2026-06-12-exercise-ux-admin-polish-sprint.md`

### What was done

All 7 phases shipped on 2026-06-12:

- **Phase 1** — Verified attempt/retry integrity; fixed a pre-existing gap-fill submission shape bug (frontend was sending the wrong JSON shape, causing all answers to score as incorrect).
- **Phase 2** — Workplace Chat: `ChatReplyContent` gained a distinct `learningGoal` field (separate from tone guidance), shown via a `chat-reply-goal` UI element. `activity_evaluate_teams_chat_simulation` prompt updated to evaluate goal-reaching, tone, clarity, and clarification-seeking.
- **Phase 3** — Email Reply: new `InteractionMode.EmailReply`, self-healing `ExercisePatternSeeder`, new `EmailReplyComponent` renderer with subject + body fields, `SubmittedAnswerJson` shape `{ subject, body }`, evaluator prompt updated.
- **Phase 4** — Shared Lesson → Practice → Evaluate framing: new `ExerciseLessonIntroComponent` ("Goal" display) applied to `GapFillComponent`, `MatchingPairsComponent`, `AudioAndFreeTextComponent`, `AudioAndGapFillComponent`. Chat Reply, Email Reply, and Free Text Entry already had equivalent goal displays.
- **Phase 5** — Admin nav: "AI Usage" moved from the (now-removed) "Analytics" group into "AI System", alongside AI Config and Prompts.
- **Phase 6** — Design-token consistency audit of all sprint-touched components — already aligned with `.sp-*` tokens, no changes needed.
- **Phase 7** — Docs close-out (this entry).

### Key constraints preserved

- Lesson → Practice → Evaluate framing applied only to the 6 currently-active exercise renderers, not retrofitted across the 40+ unimplemented patterns in the library.
- No backend changes beyond the `EmailReply` interaction mode (additive, append-only enum).

### Final test results

```
dotnet test tests/LinguaCoach.UnitTests:  477 passed
npm run build:                            passed (0 new errors/warnings)
```

---

## Previously completed sprint

**Real TTS / Placement Onboarding Gap / Today Session Card** — complete (2026-06-11)

See full sprint plan: `docs/sprints/2026-06-10-tts-placement-today-sprint.md`

### What was done

All tracks shipped on 2026-06-11:

- **Track 1 (Real TTS)** — `VoiceName` added to `AiProviderConfig` (T35 migration). `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; never throws. `TtsProviderResolver` reads `tts.listening` / `tts.placement` feature keys from DB, returns `FakeTextToSpeechService` (provider=`fake`) or `OpenAiTextToSpeechService` (provider=`openai`). `ListeningAudioService` and `PlacementAudioService` now resolve TTS at runtime. `DefaultAiSeeder` seeds both keys as `fake/fake/fake` (idempotent). Admin UI updated with voice name field and fake provider support.
- **Track 2 (Onboarding experience step)** — `PATCH /api/onboarding/experience` endpoint added. `StudentProfile.SetExperienceContext()` bypasses state machine. New `step5-experience` Angular component inserted between step-4 and placement. Step-4 now shows "Step 4 of 5" and navigates to step-5. Existing completed students can call the endpoint without error. Non-blocking — API failure still navigates to placement.
- **Track 3 (Today session card)** — previously completed in Practice Gym Activation sprint; confirmed and skipped.

### Key constraints preserved

- `FakeTextToSpeechService` remains default; `dotnet test` does not require `OPENAI_API_KEY`
- OpenAI TTS only activates when admin sets `tts.*` feature key provider to `openai`
- Existing completed students not broken by new experience step
- Practice Gym behaviour unchanged; Pronunciation remains Coming soon

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed (0 errors)
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

---

## Previously completed sprint

**Practice Gym Activation / Pattern-Based Free Practice** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-practice-gym-activation-sprint.md`

### What was done

All phases shipped on 2026-06-10:

- **Phase 2 (backend)** — `GET /api/activity/next` extended with `?pattern=<key>`. `GetNextActivityQuery` has `PreferredPatternKey`. `ActivityGetHandler.HandlePatternKeyedAsync` validates pattern key, loads definition, calls AI with `OverridePromptKey`, sets `ExercisePatternKey` on the created `LearningActivity`. `AiActivityGeneratorHandler` now supports `VocabularyPractice` when pattern-driven. Invalid pattern key returns 400.
- **Phase 3 & 4 (frontend)** — `ActivityService.getNext` accepts `patternKey`. `ActivityLessonComponent` reads `?pattern=` and passes it to the service. Practice Gym activates Phrase Match, Gap Fill, Email, and Workplace Chat as `<a routerLink>` with `pattern=` and `returnTo=/practice`.
- **Phase 5 (return flow)** — `returnTo=/practice` embedded in all four new card links. Existing `nextActivity()` / `backToDashboard()` logic handles it unchanged.
- **Phase 6 (progress verification)** — confirmed: `ActivitySubmitHandler` records `ActivityAttempt` for all pattern types; `PatternSkillUpdateService` runs after each submission; no progress on card open.
- **Phase 7 (tests + docs)** — 8 new backend integration tests; 6 new Playwright tests (4 card activation, Pronunciation still coming soon, Speaking no pronunciation claim). All existing tests still pass.

### Key constraints preserved

- Pronunciation card remains Coming soon
- No fake pronunciation claims
- PatternEvaluationRouter not bypassed
- No new endpoints or routes added
- No seed data deleted, no real user data deleted

### Final test results

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      167 passed
```

---

## Previously completed sprint

**Student UX Alignment / Writing-Assumption Cleanup** — complete (2026-06-10)

See full sprint plan: `docs/sprints/2026-06-10-student-ux-alignment-writing-assumption-cleanup-sprint.md`

### What was done

All 7 phases shipped on 2026-06-10:

- **Phase 2** — Navigation labels/routes: sidebar and mobile nav now show **Today, Journey, Practice, Progress, Profile**. Dashboard label removed. Vocabulary removed from top-level nav. `/journey` route added. `/practice` route added.
- **Phase 3** — Today page alignment: heading "Today's Lesson" added. "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Phase 4** — Journey mixed-skill cleanup: page heading "Learning Journey" added. Memory fallback "workplace writing" → "workplace English". "Continue practising" CTA replaced with safe CTAs.
- **Phase 5** — Practice Gym MVP at `/practice`: functional cards for Vocabulary, Listening, Writing, Speaking. Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. No auto-start on load.
- **Phase 6** — Playwright fixture copy cleanup: generic writing/email-only fixture language updated to mixed-skill workplace English across `core-flow-smoke.spec.ts`, `disabled-actions-cleanup.spec.ts`, `lesson-activity-wiring.spec.ts`, `admin-screenshots.spec.ts`. Valid WritingScenario and email_reply test coverage preserved.
- **Phase 7** — Documentation cleanup: `current-product-state.md`, `current-sprint.md`, `docs/architecture/README.md` updated. Older sprint docs marked historical. Sprint doc closed.

### Key constraints preserved

- No real user data deleted
- No seed rows deleted (`WritingScenarioSeeder`, `LearningActivitySeeder` unchanged)
- Writing and Email remain valid activity types
- `/my-path` still works (backwards compatible with `/journey`)
- No backend files changed in this sprint

### Final test results

```
dotnet test:     865 passed (451 unit + 414 integration) — unchanged
npm run build:   passed
Playwright:      165 passed (21 new Practice Gym tests + 9 new Journey tests)
```

---

## Completed sprints

- Admin UX / Student Management / AI Config Cleanup — complete
- Today's Lesson / Learning Session (Phases 1–5B) — complete
- Exercise Pattern Engine — complete
- Pattern Evaluation Engine (Phases 1–7) — complete
- Student UX Alignment / Writing-Assumption Cleanup (Phases 1–7) — complete
- Real TTS / Placement Onboarding Gap / Today Session Card — complete
- **Exercise UX / Admin Polish (Phases 1–7) — complete**

---

## Current state

All four activity types are implemented. Placement Assessment is complete. The full evaluation stack is live end-to-end. Student nav model is aligned:

- Today (`/dashboard`) is the student home page — Today's Lesson is the primary CTA
- Journey (`/journey`, `/my-path`) shows the learning path with mixed-skill framing
- Practice (`/practice`) is the Practice Gym MVP — free practice by skill or exercise type
- Progress and Profile unchanged
- Pattern-aware evaluators route by `MarkingMode`: `ExactMatch`, `KeyedSelection`, `AiStructured`, `AiOpenEnded`, `NoMarking`
- `StudentSkillProfile` updated from evaluation skill impacts after every pattern attempt
- Compact memory signals from evaluation fed into `StudentLearningMemory`
- Pattern-aware result UI with 6 branches

Session reflection (`GET /api/sessions/{id}/reflection`) is a 501 stub — deferred.

---

## Deferred

- **Dynamic pattern selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history
- **Dynamic Practice Gym session templates** — configurable session templates within Practice Gym (e.g. "30-min vocab session")
- Session reflection AI prompt (`session_reflection`) — requires stable session completion signal
- IFileStorageService / MinIO — not blocking deployment at current scale
- Admin lifecycle reset tools
- Call Mode / Pronunciation scoring
- Real STT provider
- OpenAI TTS (advanced voices)
- Email delivery, payments, organisations

---

## Next recommended work

1. **Dynamic Pattern Selection** — choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
   Scoping pass done (2026-06-12): see
   `docs/reviews/2026-06-12-dynamic-pattern-selection-scoping.md`. Recommended first
   slice: per-slot pattern pools + last-N-session repetition avoidance in
   `SessionGeneratorService`/`SessionDurationTemplates`.
2. **Dynamic Practice Gym session templates** — configurable multi-exercise sessions within Practice Gym.
3. **Session Reflection AI** — now that evaluation outputs are stable, wire `session_reflection` AI prompt.

---

## Planned future sprint

**Lesson Buffer / MinIO / Background Generation** - planned.

See: `docs/sprints/2026-06-11-lesson-buffer-minio-background-generation-sprint.md`

This sprint covers pre-generating the next 5-10 lessons, pre-generating a configurable 5-10 Practice Gym exercises per type/pattern, storing audio assets in MinIO, signed URL playback, Quartz.NET background generation jobs, Admin Integrations for MinIO health/configuration, and cached Practice Gym generation.

---

## Key rule

Do not add more isolated activity types. Build the course structure and pattern engine that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.

---

## Phase 10J — Learning Goal Context Resolver (2026-06-17)

**Goal:** Normalize `LearningGoalContext` across all generation and ledger paths via a consistent, testable resolver.

**Delivered:**

- `ResolvedLearningGoalContext` value object (`src/LinguaCoach.Application/Learning/`)
- `ILearningGoalContextResolver` interface with `LearningGoalResolutionContext` call context
- `LearningGoalContextResolver` implementation (`src/LinguaCoach.Infrastructure/Learning/`)
- DI registration: `AddSingleton<ILearningGoalContextResolver, LearningGoalContextResolver>()`
- 7 call sites migrated from `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` to resolver:
  - `ActivityGetHandler` (3 sites)
  - `ActivitySubmitHandler` (2 ledger record sites)
  - `ExercisePrepareHandler` (1 site)
  - `PracticeGymGenerationJob` (1 site)
  - `SessionGeneratorService` (1 site)
  - `ActivityMaterializationJob` (1 site)
  - `LessonBatchGenerationJob` (1 site)
- 18 unit tests, 2 integration tests — all pass

**Priority chain (strict order):**
1. `ExplicitGoalOverride` from call context
2. `LearningGoals` + `FocusAreas` (Phase 10G/10I structured fields)
3. `CustomLearningGoal` / `CustomFocusArea`
4. `LearningGoalDescription` → `LearningGoal` → `CareerContext` (legacy)
5. `"general English communication"` — never workplace-only

**Not implemented:** curriculum routing, readiness pools, CEFR routing, background generation.

**Backward compatible:** `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` kept intact. Old ledger records without goal context do not throw.

---

## Phase 10X-J — Admin Wrapper Variant API (2026-06-19)

**Goal:** Make `sp-admin-*` wrapper components robust and flexible so admin pages become consumers of design-system components, not owners of styling/layout logic.

**Delivered:**

- `sp-admin-button`: `appearance` (solid/outline/soft/ghost/link), `size` (xs/sm/md/lg), `fullWidth`, `iconOnly`. Legacy `variant="ghost"` compat alias preserved.
- `sp-admin-badge`: `appearance` (soft/solid/outline), `size` (sm/md), `dot`, `purple` tone added.
- `sp-admin-card`: `variant` (default/bordered/elevated/flat/metric/section), `padding` (none/sm/md/lg), `radius` (md/lg/xl/2xl), `headerDivider`, `hover`, `loading`.
- `sp-admin-stat-card`: `size` (sm/md/lg), unified tone names (primary/success/warning/danger/info/neutral alias the legacy names), `loading` skeleton, `[slot=trend]`.
- `sp-admin-table`: `variant` (basic/data/bordered/striped/simple/card), `density` (compact/comfortable/spacious), `selectable`, `stickyHeader`, column `width`/`align`.
- `sp-admin-filter-bar`: `layout` (inline/stacked/responsive), `density` (compact/comfortable).
- `sp-admin-form-field`: `layout` (vertical/horizontal/inline), `size` (sm/md/lg).
- `sp-admin-input`: `size` (sm/md/lg), `state` (default/error/success/disabled), `fullWidth`. CVA preserved.
- `sp-admin-select`: `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved.
- `sp-admin-textarea`: `size` (sm/md/lg), `state`, `fullWidth`. CVA preserved.
- `sp-admin-modal`: `size` (sm/md/lg/xl/full), `variant` (default/danger/form/confirm), `showCloseButton`. `maxWidth` still works (overrides size).
- `sp-admin-drawer`: `side` (left/right), `size` (sm/md/lg/xl), `closeOnBackdrop`.
- 18 new Phase 10X-J unit tests. 439 Angular tests pass (0 failures).
- Proof usage applied: Students page `variant="data" density="compact"`, modal buttons `size="sm"`. Dashboard stat-cards `size="md" [loading]`, AI System card `variant="metric"`.
- .NET: 1885 tests pass. Angular build clean. Playwright: pending (run from `src/LinguaCoach.Web`).

**Not implemented:** 10R-F usage governance UX, 10U AI Usage redesign, 10V prompt playground, notification platform, enterprise auth/security, observability stack, billing, StudentProfile.CefrLevel migration, full placement engine, full mastery engine.

---

## Phase 10Students-F-C — Targeted Lifecycle Controls (2026-06-19)

**Goal:** Add Pause, Unpause, and Reactivate admin lifecycle controls for students.

**Delivered:**

- `ReactivateStudentCommand`, `PauseStudentCommand`, `UnpauseStudentCommand` records (with `AdminUserId`)
- `IAdminStudentQuery` extended with `ReactivateStudentAsync`, `PauseStudentAsync`, `UnpauseStudentAsync`
- `AdminHandler` implements all three — each writes `AdminAuditLog` entry
- Guard rules: Reactivate requires Archived; Pause rejects Archived/already Paused; Unpause requires Paused
- Reactivate sets `user.EmailConfirmed = true`; Unpause/Reactivate both land on `OnboardingRequired`
- `POST /api/admin/students/{id}/reactivate`, `/pause`, `/unpause` endpoints
- Frontend service: `reactivateStudent`, `pauseStudent`, `unpauseStudent`
- Detail component: context-sensitive buttons (Reactivate when Archived, Unpause when Paused, Pause otherwise)
- Inline confirm modal with cancel/confirm/error/saving state; refreshes student on success
- 9 new integration tests, 16 new frontend tests — all pass
- No migration added (Paused enum value 10 already existed)

**Not implemented:** audit log for Archive (pre-existing gap), window.confirm replacement for Archive/RemovePolicy.

**Test counts:** .NET 680 integration + 1237 unit + 3 architecture = 1920 total. Angular 734.

---

## Phase 10V-0 — AI Pricing Admin Gap Check (2026-06-21)

**Goal:** Audit current AI pricing configuration to define the smallest safe path to admin-manageable pricing.

**Delivered:**

- Full gap audit: pricing source, admin UI gap, persistence gap, risk areas, migration impact.
- Review saved: `docs/reviews/2026-06-21-phase-10v-0-ai-pricing-admin-gap-check.md`
- Confirmed: pricing is config-only (`appsettings.json` via `AiPricingOptions`), not admin-editable.
- Confirmed: `AiConfigCategory` and `AiProviderConfig` entities have no pricing fields.
- Confirmed: zero-cost logs occur silently when model not found in config.
- Recommended design: hybrid config seed + `AiModelPricingOverride` DB table.
- Recommended next: 10V-1 = read-only pricing visibility (no migration), 10V-2 = DB-backed editable pricing.
- No code changed. `git diff --check`: clean.

**Not implemented:** 10V-1 read-only panel, 10V-2 pricing CRUD, 10V-3 zero-cost alert, prompt playground.

---

## Phase 10V-1 — Read-Only AI Pricing Panel (2026-06-21)

**Goal:** Expose config-based AI model pricing in the admin UI for visibility. No migration, no DB writes.

**Delivered:**

- `GET /api/admin/ai/pricing` endpoint — reads all provider pricing from `IConfiguration`, returns `AiModelPricingItem[]`
- `AiModelPricingItem` DTO: `providerName`, `modelName`, `inputPer1KTokens`, `outputPer1KTokens`, `currency`, `source`, `isConfigured`
- `IAdminAiConfigHandler.ListPricing()` added; implemented in `AdminHandler` with `IConfiguration` injection
- Admin AI Config page: Section 4 "Model Pricing" — read-only table grouped by provider, info alert, empty state
- `listAiPricing()` added to `AdminApiService`, `AiModelPricingItem` added to `admin.models.ts`
- 5 new backend integration tests (401/403/200/fields/spot-check price value)
- 8 new frontend unit tests; 2 existing mocks fixed
- No migration. No pricing calculation change. No provider routing change.
- All gates pass: 2046 .NET tests, 880 Angular tests, prod build clean.

**Test counts:** .NET 795 integration + 1248 unit + 3 arch = 2046 total. Angular 880.

**Not implemented:** missing-model detection (10V-3), DB-backed editable pricing (10V-2), prompt playground (10V).

---

## Phase 10V-2 — AI Pricing Override Backend Foundation (2026-06-21)

**Goal:** Add DB-backed AI model pricing overrides with full CRUD API and a pricing resolver service.

**Delivered:**

- `AiModelPricingOverride` domain entity with validation, Update/Deactivate methods, and audit fields
- EF configuration + migration `T53_AiModelPricingOverrides` (table `ai_model_pricing_overrides`)
- `IAiPricingResolver` / `AiPricingResolver`: DB override first, config fallback, null third
- `IAdminAiConfigHandler` extended with 4 pricing override methods
- `AdminHandler` implements: `ListPricingOverridesAsync`, `CreatePricingOverrideAsync`, `UpdatePricingOverrideAsync`, `DeactivatePricingOverrideAsync`
- Endpoints: `GET/POST /api/admin/ai/pricing/overrides`, `PUT/DELETE /api/admin/ai/pricing/overrides/{id}`
- Audit log entries on create/update/deactivate
- Soft-delete (deactivate) pattern consistent with project style
- `IAiPricingResolver` registered in DI
- 13 integration tests + 11 domain unit tests; all pass
- Provider runtime cost calculation NOT changed (deferred to 10V-3 for safety)
- Config fallback preserved; no historical cost recalculation
- No frontend edit UI added

**Test counts:** .NET 810 integration + 1260 unit + 3 arch = 2073 total. Angular 880.

**Not implemented:** provider runtime wiring (10V-3), frontend override UI, zero-cost alert, unique override constraint.

---

## Phase 10V-3 — AI Pricing Runtime Resolver Wiring + Override Management UI (2026-06-21)

**Goal:** Wire `IAiPricingResolver` into all three AI providers for runtime cost calculation, and add override management UI to AI Config page.

**Delivered:**

**Part A — Runtime resolver wiring:**
- `IAiPricingResolver` injected into `OpenAiProvider`, `GeminiProvider`, `AnthropicProvider`
- Direct `AiPricingOptions.GetOpenAiPricing` / `GetGeminiPricing` / `GetProviderPricing` calls replaced with `await _pricingResolver.ResolveAsync(ProviderName, modelToUse, ct)`
- Cost formula: `(inputTokens / 1000m) * resolved.InputPer1KTokens + (outputTokens / 1000m) * resolved.OutputPer1KTokens`
- Missing pricing still logs 0m cost — no throw, unchanged behavior
- No DI changes needed — `IAiPricingResolver` already registered as scoped in 10V-2
- Unit test helpers `NullPricingResolver` / `NullPricingResolverForResolver` added to fix two existing test fixtures

**Part B — Zero-cost visibility:**
- Deferred. Existing null-cost log messages sufficient. Tracked as `TODO-10V-3B`.

**Part C — Frontend override management UI:**
- `AiModelPricingOverrideItem`, `CreatePricingOverrideRequest`, `UpdatePricingOverrideRequest` added to `admin.models.ts`
- `listAiPricingOverrides`, `createAiPricingOverride`, `updateAiPricingOverride`, `deactivateAiPricingOverride` added to `AdminApiService`
- AI Config Section 4 extended: config pricing table (read-only, unchanged) + DB overrides table + inline create/edit form + deactivate with confirm
- Validation: provider required, model required, prices >= 0, effectiveTo after effectiveFrom when provided
- After create/edit/deactivate: signal updated in place (no full reload needed)
- 11 new frontend tests; existing mocks in `admin-wrapper-migration.spec.ts` and `admin-ai-config.component.spec.ts` updated

**Constraints respected:**
- No provider routing change
- No usage governance change
- No historical AiUsageLog recalculation
- No new migration
- No AI Config page redesign

**Test counts:** .NET 810 integration + 1260 unit + 3 arch = 2073 total. Angular 891.

**Not implemented:** zero-cost alert UI (TODO-10V-3B), unique override constraint (TODO-10V-UNIQUE-CONSTRAINT).

---

## Phase 10V-FINAL — AI Pricing Admin Closure Audit (2026-06-21)

**Goal:** Close 10V cleanly — verify 10V-3 commit, fix missing docs, run final audit.

**Delivered:**

- TODOS.md: `TODO-10V-3` marked done; `TODO-10V-3B` (zero-cost alert) added as new deferred item
- `docs/sprints/current-sprint.md`: 10V-3 and 10V-FINAL sections added
- `docs/reviews/2026-06-21-phase-10v-3-ai-pricing-runtime-and-override-ui-review.md`: created in 10V-3
- `docs/reviews/2026-06-21-phase-10v-final-ai-pricing-admin-closure-audit.md`: created in this phase
- All gates verified green before commit

**Remaining AI pricing TODOs:**
- `TODO-10V-3B`: zero-cost alert UI in AI Usage or AI Config
- `TODO-10V-UNIQUE-CONSTRAINT`: optional unique index on `(ProviderName, ModelName, EffectiveFromUtc)`
