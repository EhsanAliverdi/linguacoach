# TODOS

Deferred items captured from engineering reviews and sprint planning.
Each item includes context, motivation, and the phase where it was deferred.

---

## Curriculum / CEFR

### TODO-001 — CEFR plus/sublevel handling (B2+)
**What:** Extend the curriculum and placement model to handle CEFR plus-levels such as B2+.
**Why:** `StudentProfile.CefrLevel` and some placement results may return "B2+" or similar. The Phase 10K model uses `CefrLevelConstants` (A1–C2 only) and will not match plus-levels correctly.
**Context:** Phase 10K seeds A1–C2 objectives and validates against `CefrLevelConstants.All`. A student assessed at B2+ will fall through to the B2 bucket silently. Future phases should define a sublevel mapping (B2+ → B2, C1-) or extend `CefrLevelConstants` with a plus tier.
**Depends on:** Phase 10K complete (done). Requires alignment with placement assessment output format.
**Deferred from:** Phase 10K engineering review, 2026-06-17.

---

### TODO-002 — Migrate `StudentProfile.CefrLevel` from free-text string to validated enum/constant
**What:** Replace `StudentProfile.CefrLevel` (currently `string?`, no validation) with a validated type using `CefrLevelConstants`.
**Why:** `CefrLevel` can currently hold any arbitrary string. Phase 10K introduces `CefrLevelConstants` for curriculum validation, but `StudentProfile` still accepts free-text. This creates a divergence: curriculum queries normalise CEFR, but profile data may not match.
**Context:** A migration to change the column type is low risk (values are short strings), but requires auditing all callers of `StudentProfile.CefrLevel` and updating placement assessment output. Phase 10K deferred this to avoid migration risk in a foundation phase.
**Depends on:** TODO-001 (plus-level handling) should be resolved first so the migration captures the full value set.
**Deferred from:** Phase 10K engineering review, 2026-06-17.

---

### TODO-003 — Admin curriculum objective builder / write UI
**What:** Add admin CRUD endpoints and a basic UI for creating, editing, and deactivating curriculum objectives without a code deployment.
**Why:** Phase 10K seeds objectives via `CurriculumObjectiveSeeder`. Non-developer staff (curriculum designers, coaches) cannot currently modify the syllabus without a code change and redeploy.
**Context:** Phase 10K adds read-only admin endpoints (`GET /api/admin/curriculum/objectives`). Write endpoints are deferred. The domain model (`CurriculumObjective.UpdateDetails`, `Activate`, `Deactivate`) is already designed to support CRUD. The admin UI (TailAdmin migration) is a separate workstream.
**Depends on:** Phase 10K complete (done). Admin UI migration (separate backlog item).
**Deferred from:** Phase 10K engineering review, 2026-06-17.

---

## Preference Enforcement / AI Context

### TODO-004 — Wire `learnerPreferences` and `learningGoalContext` into evaluation prompt templates
**What:** Update `activity_evaluate_writing` and `activity_evaluate_speaking_role_play` prompt templates in the database to reference `{{learnerPreferences}}` and `{{learningGoalContext}}` variables.
**Why:** Phase 10K-F adds these variables to `ActivityEvaluationContext` and passes them to the prompt variable dict, but the prompt templates do not yet reference them. Variables are available but unused.
**Context:** Adding unused variables to the dict is safe. A prompt-engineering pass should add preference-aware coaching instructions (e.g. use support language for explanations when `translationHelpPreference` allows it; adjust challenge level based on `difficultyPreference`).
**Depends on:** Phase 10K-F complete (done).
**Deferred from:** Phase 10K-F engineering review, 2026-06-17.

### TODO-005 — Add `SupportLanguageCode` to `ActivityGenerationContext`
**What:** Add the BCP-47 support language code (e.g. `"fa"`, `"zh"`) to `ActivityGenerationContext` alongside the existing `SupportLanguageName`.
**Why:** `SupportLanguageCode` is stored on `ResolvedLearningGoalContext` but not propagated to `ActivityGenerationContext`. If a prompt template needs the code for structured language-switching, it is unavailable.
**Context:** Current prompts use `SupportLanguageName` in prose. Add when a prompt template requires the code.
**Depends on:** Phase 10K-F complete (done).
**Deferred from:** Phase 10K-F engineering review, 2026-06-17.

### TODO-006 — Add `TranslationHelpPreference` to `ResolvedLearningGoalContext`
**What:** Expose `TranslationHelpPreference` as a field on `ResolvedLearningGoalContext` so downstream consumers (curriculum mappers, ledger, future routing) can read it without going through the formatter.
**Why:** Only `LearnerPreferenceContextFormatter` currently knows the translation preference. Ledger events and curriculum selection cannot use it directly.
**Depends on:** Phase 10K-F complete (done).
**Deferred from:** Phase 10K-F engineering review, 2026-06-17.

---

## Readiness Pool (Phase 10M/10N foundation — serving deferred to 10O)

### ~~TODO-007~~ — Pool replenishment background engine — **DONE in Phase 10N**

`ReadinessPoolReplenishmentService` + `ReadinessPoolReplenishmentJob` implemented. Sweeps, recovers, retries, and fills shortfalls for all active students every 20 minutes.

### TODO-008 — Serve from pool on Today and Practice Gym page load (Phase 10O)
**What:** Update `ActivityGetHandler` and `ExercisePrepareHandler` to check the readiness pool for a suitable ready item before falling back to on-demand generation.
**Why:** Phase 10N creates and maintains pool items but does not change page-load serving. The pool is populated and healthy but never consumed by students.
**Context:** `IStudentActivityReadinessPoolService.ReserveNextReadyAsync` is safe and ready. Integration points: `ActivityGetHandler.HandlePatternKeyedAsync` (Practice Gym), `ExercisePrepareHandler.HandleAsync` (Today lessons). Existing on-demand fallback must remain intact.
**Deferred from:** Phase 10M/10N, 2026-06-17.

### TODO-009 — Enable `AllowReviewOrScaffold=true` based on mastery signals (Phase 10O+)
**What:** Wire mastery/ledger signals so that `CurriculumRoutingRequestFactory.Build` passes `allowReviewOrScaffold=true` when a student has demonstrated mastery of the target objective.
**Why:** `EnableReviewScaffoldGeneration=false` by default in Phase 10N. The routing service supports review/scaffold routing but it is never activated in production.
**Context:** Requires mastery engine or reliable ledger query. `GetWeakEventsAsync` exists but is a conservative signal only. Phase 10N keeps the flag false until the signal is validated.
**Deferred from:** Phase 10L/10M/10N, 2026-06-17.

### ~~TODO-010~~ — Sweep orphaned Generating pool items — **DONE in Phase 10N**

`ReadinessPoolReplenishmentService.RecoverOrphanedGeneratingAsync` marks generating items past `GeneratingTimeoutMinutes` as Failed. Retry picks them up if under `MaxGenerationAttempts`.

### TODO-011 — Move ReadinessPoolReplenishmentOptions to DB-backed admin config (Phase 10O+)
**What:** Expose `TodayLessonPoolTargetCount`, `PracticeGymPoolTargetCount`, `MaxItemsGeneratedPerRun`, `ReadyItemExpiryDays` etc. in the admin UI so they can be tuned without a redeploy.
**Why:** Currently bound from `appsettings.json`. Target counts and expiry windows are operational decisions that should be tunable by admin without code changes.
**Deferred from:** Phase 10N, 2026-06-17.

### TODO-012 — Ledger-weighted skill rotation in replenishment (Phase 10O+)
**What:** Weight skill selection in `FillShortfallAsync` toward skills with low `StudentSkillProfile.ScorePercent` instead of round-robin rotation.
**Why:** Round-robin creates equal distribution. Weighted selection would prioritise weak skills and make the pool more useful for adaptive learning.
**Deferred from:** Phase 10N, 2026-06-17.

### TODO-013 — Per-student review/scaffold signal instead of global flag (Phase 10O+)
**What:** Replace `EnableReviewScaffoldGeneration` global flag with a per-student check: allow review/scaffold for a specific student × skill when `StudentSkillProfile.ScorePercent < threshold` for a prerequisite objective.
**Why:** Global flag is too coarse. A B1-weak B2 student should get review content for B1 prerequisites, not all students globally.
**Deferred from:** Phase 10N, 2026-06-17.

### TODO-016 — CEFR auto-promotion/demotion using multi-skill progress signals (Phase 10P+)
**What:** Use `StudentSkillProfile.ScorePercent` signals accumulated by `MultiSkillProgressService` to trigger placement/mastery evaluation that may promote or demote a student's CEFR level.
**Why:** Phase 10P writes skill progress signals but deliberately does not change `StudentProfile.CefrLevel`. A future mastery/placement engine should consume the accumulated skill profile and decide level changes.
**Context:** Multi-skill signals are now written per activity attempt. A threshold policy (e.g. 80% of tracked skills at ≥70 for 3+ weeks) could trigger placement re-evaluation. Full placement engine is out of scope for Phase 10P.
**Deferred from:** Phase 10P, 2026-06-18.

### TODO-017 — Merge MultiSkillProgressService.SkillLabels with PatternSkillUpdateService.SkillLabels
**What:** Both services maintain their own `SkillLabels` dictionary. Extract to a shared `SkillRegistry` class in the Domain or Application layer.
**Why:** Divergence risk: adding a skill key to one service but not the other creates silent drops. A single registry (possibly seeded from DB) would eliminate this.
**Deferred from:** Phase 10P, 2026-06-18.

---

## Usage Governance (Phase 10R foundation — deferred items)

### TODO-018 — Workspace and cohort policy inheritance
**What:** Implement `UsagePolicyScopeType.Workspace` and `UsagePolicyScopeType.Cohort` policy resolution layers between Global default and Student-specific assignment.
**Why:** Phase 10R resolves policies as Global → Student only. Workspace/cohort inheritance is required for multi-tenant enterprise deployments where an org-level policy overrides the global default.
**Context:** `UsagePolicyScopeType` enum and `ScopeType` DB column already exist. `GetEffectivePolicyAsync` has a TODO comment for the workspace/cohort check.
**Deferred from:** Phase 10R, 2026-06-18.

### TODO-019 — Provider pricing tables for accurate cost estimation
**What:** Introduce a `ProviderPricingTable` that maps (provider, model) to (input cost per 1k tokens, output cost per 1k tokens). Auto-compute `EstimatedCost` in `RecordAsync` instead of relying on callers.
**Why:** `EstimatedCost` is currently set by callers who may use stale or placeholder values. Cost estimates may diverge from real provider invoices. Required before any billing feature goes live.
**Context:** All `UsageEvent` records have `Provider` and `Model` fields. A pricing table lookup at record time would give consistent cost tracking.
**Deferred from:** Phase 10R, 2026-06-18.
**Partial fix:** Phase 10U-1 (2026-06-20) added operational pricing defaults to `appsettings.json` for 12 models across OpenAI, Gemini, and Anthropic. `AiUsageLog.CostUsd` is now non-zero for those models. A DB pricing table (admin-editable, deploy-free updates) is deferred to 10U-8.

### TODO-020 — Monthly and weekly limit enforcement
**What:** Implement `WeeklyLimit` and `MonthlyLimit` checks in `UsageQuotaService.CheckAsync`.
**Why:** `UsagePolicyRule` stores `WeeklyLimit`, `MonthlyLimit`, and `MonthlyCostLimit` but only `DailyLimit` is enforced. Weekly/monthly enforcement is needed for production quota management.
**Context:** The DB columns and rule properties are in place. `CheckAsync` needs additional aggregate queries for the current week/month window.
**Deferred from:** Phase 10R, 2026-06-18.

### TODO-021 — Student-facing usage widget
**What:** Add a student-facing page or dashboard card showing remaining quota, usage this week/month, and which features are near limit.
**Why:** Students have no visibility into their quota state until a 429 is returned. A proactive usage widget improves UX and reduces support load.
**Context:** `GET /api/admin/students/{id}/usage` exists for admins. A student-facing equivalent under `/api/usage` would power the widget.
**Deferred from:** Phase 10R, 2026-06-18.

### ~~TODO-022~~ — CSV export of usage data — **DONE in Phase 10U-8** (2026-06-21)

`GET /api/admin/ai-usage/export.csv` delivers filtered `AiUsageLog` rows as RFC 4180 CSV. Columns: `CreatedAt, Provider, Model, FeatureKey, StudentId, WasSuccessful, IsFallback, FailureReason, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, CorrelationId`. Accepts all column filters (provider, model, featureKey, status, studentId) and date range. No pagination — returns up to 10,000 rows newest-first. Note: this is `AiUsageLog`-based, not `StudentUsageDaily`. A `StudentUsageDaily` export remains a future option if needed for billing reconciliation at the aggregate level.

### TODO-023 — Near-quota notification
**What:** Send email/push notification when a student reaches 80% of a HardLimit quota. Use the `WarningThresholdPercent` field on `UsagePolicyRule`.
**Why:** Students currently receive no warning before hitting a hard limit. The `WarningThresholdPercent` field is stored but never used.
**Context:** Requires notification platform (email or in-app). `SoftWarning` enforcement mode in `QuotaDecision` is wired for the API response but no async notification is triggered.
**Deferred from:** Phase 10R, 2026-06-18.

### TODO-024 — Explicit transaction scope for UsageEvent + StudentUsageDaily upsert
**What:** Wrap `UsageQuotaService.RecordAsync` in an explicit `IDbContextTransaction` so UsageEvent and StudentUsageDaily are written atomically.
**Why:** Currently both writes use a single `SaveChangesAsync` call which is implicit. An explicit transaction scope would make the atomicity guarantee clearer and allow future use of `IsolationLevel.Serializable` for the upsert if write contention becomes an issue.
**Deferred from:** Phase 10R engineering review, 2026-06-18.

---

### ~~TODO-014~~ — Wire TryMarkConsumedAsync into ActivitySubmitHandler — **DONE in Phase 10O-F**

`ActivitySubmitHandler` now calls `TryConsumeReadinessItemAsync` best-effort after all activity completion paths. Looks up Reserved readiness item by `studentProfileId + LearningActivityId`, calls `TryMarkConsumedAsync`. Idempotent and exception-safe.

### ~~TODO-015~~ — Angular Practice Gym "Suggested for you" section — **DONE in Phase 10O-F**

`PracticeGymSuggestionsService` added. Practice Gym component updated with Suggested for you, Continue practice, and Review practice sections. Cards show skill, CEFR level, routing label, duration. Start flow calls `POST .../start` and navigates to returned activity. Empty/error states implemented.
# Phase 10X follow-ups

- ~~TODO-10X-B~~: migrate core admin pages to the `sp-admin-*` wrapper layer — **DONE in Phase 10X-B**
- ~~TODO-10X-C~~: Angular build gate, ViewEncapsulation fix, TailAdmin Layout One structural alignment — **DONE in Phase 10X-C-F**
- ~~TODO-10X-ASSETS~~: **DONE in Phase 10X-D** — TailAdmin free Angular template imported at `src/app/templates/tailadmin/free-angular-tailwind-dashboard/` (commit da992cf, MIT). Adapter inventory created. `admin-template/` folder removed. Templates gitignored from main repo.
- ~~TODO-10X-D-MODAL~~: migrate student edit/reset/archive modal internals to `sp-admin-modal` — **DONE in Phase 10X-I** (2026-06-19). All 3 student modals (edit, reset-password, reset-data) replaced with `sp-admin-modal`. Page-local modal CSS removed.
- ~~TODO-10X-E~~: wrapper alignment phase — adapt real TailAdmin layout/sidebar/header/button/badge/card/input/modal patterns from `src/app/templates/tailadmin/` into `sp-admin-*` wrappers. Remove approximation CSS replaced by real patterns. — **DONE in Phase 10X-E** (2026-06-18). All 15 wrappers adapted. Angular 349 passed, .NET 1885 passed.
- ~~TODO-10X-F~~: wrapper completion — table sorting, dropdown, theme toggle, filter bar, pagination alignment with TailAdmin source. — **DONE in Phase 10X-F** (2026-06-18). `sp-admin-dropdown`, `sp-admin-table-actions`, `sp-admin-theme-toggle`, `AdminThemeService` added. `sp-admin-table` sortable columns, `sp-admin-header` named slots, `sp-admin-filter-bar` named slots. Admin-students row actions migrated to dropdown. Angular 373 passed, .NET 1885 passed, Playwright 188 passed.
- ~~TODO-10X-G~~: full admin page refactor using new wrappers — **DONE in Phase 10X-G** (2026-06-18). Dashboard KPI tiles → `sp-admin-stat-card`, sections → `sp-admin-card`, badges → `sp-admin-badge` (page-local KPI/status/badge/table CSS removed). AI Config duplicate in-card headings removed; category Save/Test → `sp-admin-button`. Curriculum create/edit/preview panels → `sp-admin-card`, actions → `sp-admin-button`. Admin header user menu → `sp-admin-dropdown`. Angular 377, .NET 1885, Playwright 188. Remaining page-local form-field migration split into the three TODOs below.
- ~~TODO-10X-G-F~~: finish remaining admin page refactor — **DONE in Phase 10X-G-F** (2026-06-18). Students row table → `sp-admin-table` projection; lifecycle/onboarding/CEFR pills → `sp-admin-badge` wrapper; obsolete page-local pagination/row-action/badge CSS removed. Curriculum create/edit/preview form fields → `sp-admin-form-field`. Verified AI Usage/Prompts/Exercise Types/Diagnostics/Usage Policies/Integrations cards already wrapper-migrated. Angular 379, .NET 1885, Playwright 188.
- ~~TODO-10X-G-AICONFIG-FORMS~~: migrate AI Config provider/model/voice inputs and dense credentials grid — **DONE in Phase 10X-I** (2026-06-19). Category selects kept native (incompatible with `[ngValue]="null"`); wrapped in `sp-admin-form-field`. Text/password/endpoint inputs migrated to `sp-admin-input`. API key/endpoint buttons migrated to `sp-admin-button`.
- ~~TODO-10X-G-CURRICULUM-FORMS~~: migrate Curriculum create/edit and routing-preview fields to `sp-admin-form-field` — **DONE in Phase 10X-G-F** (2026-06-18). Used `sp-admin-form-field` for labels/hints; native ngModel controls retained inside (wrappers lack ControlValueAccessor).
- ~~TODO-10X-G-INTEGRATIONS-FORMS~~: migrate Integrations operational forms to admin wrappers — **DONE in Phase 10X-I** (2026-06-19). Storage display fields → `sp-admin-input [disabled]`. Generation settings number inputs kept native (CVA writes strings); wrapped in `sp-admin-form-field`. Tables → TailAdmin Tailwind classes directly. Action buttons → `sp-admin-button`.
- ~~TODO-10X-FORMS-CVA~~: add a `ControlValueAccessor` to `sp-admin-input` and `sp-admin-select` so they can two-way bind to parent ngModel/reactive forms — **DONE in Phase 10X-H** (2026-06-19). CVA via `NG_VALUE_ACCESSOR`+`forwardRef` added to `sp-admin-input`, `sp-admin-select`, and the new `sp-admin-textarea`. Supports ngModel, reactive `formControl`/`formControlName`, `setDisabledState`, touched-on-blur. 15 new wrapper specs. Angular 394, .NET 1885, Playwright 188.
- TODO-10X-G-DARKMODE: define the admin-only dark-mode class boundary so `AdminThemeService` dark mode is fully scoped to the admin shell and cannot leak into student UI. Currently isolated via `adminTheme` localStorage key only.
- TODO-10X-MODAL: add richer modal confirm orchestration, focus trap, and keyboard return focus.
- TODO-10X-DRAWER: add typed drawer payloads for student detail, usage policy editor, and prompt preview.
- TODO-10X-TOAST: add toast action buttons and queue limits if admin workflows need them.
- ~~TODO-10X-LAYOUT-BLOCKER~~: admin shell/sidebar/header/content must visually match TailAdmin Layout One — **DONE in Phase 10X-LAYOUT-BLOCKER** (2026-06-19). Imported TailAdmin `@utility` + `@theme` tokens into global styles.css. Rewrote `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header` to use pure TailAdmin Tailwind classes. Rewrote `admin-app-layout.html` with `menu-item`/grouped-sections nav structure. Stripped all competing custom CSS from layout component. Added `body.admin-layout` body-class for gray-50 background. 20 new Angular tests + 13 updated. Angular 411, .NET 1885, Playwright 183+ passed. 10X-I form migration unblocked.
- ~~TODO-10X-I~~: migrate remaining admin forms and modals to CVA wrappers — **DONE in Phase 10X-I** (2026-06-19). AI Config, Integrations, and all 3 student modals migrated. `sp-admin-modal maxWidth`, `sp-admin-input [value]`, and `sp-admin-layout <main>` added. Angular 421, .NET 1885 passed.
- ~~TODO-10R-F~~: Usage Governance Admin UX Foundation — **DONE in Phase 10R-F** (stat cards, expandable rule detail, feature name lookup, admin wrappers).
- ~~TODO-10R-RULE-MGMT backend~~: Rule CRUD backend (domain method, service methods, API endpoints) — **DONE in Phase 10R-G**.
- ~~TODO-10R-RULE-MGMT-UI~~: Rule editor admin UI — **DONE in Phase 10R-H**. Modal add/edit/delete with full form, delete confirm, local state update.
- TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT: Optional — promote duplicate-key guard from application layer to DB unique index on `(UsagePolicyId, FeatureKey)` via EF migration.
- ~~TODO-10R-STUDENT-ASSIGN~~: Student policy assignment UI — **DONE in Phase 10R-J**. Usage Policy section in student detail: view effective policy, assign override, reset to default. DELETE endpoint added. 681 Angular tests pass.
- ~~TODO-10U~~: full AI usage/config redesign on the admin design system — **DONE in Phases 10U-1 through 10U-10** (2026-06-20/21). Summary cards, date filtering, recent calls pagination + filters, student filter, summary filter alignment, CSV export, daily trend table, custom date range picker. Deferred: pricing admin UI, timezone selector, row cap config, student typeahead, charts, alerts, TODO-10U-GAP-1 through GAP-7.
- TODO-10V: prompt playground on the admin design system. Wrapper variant API (10X-J) is now available.
- ~~TODO-10V-1~~: Read-only AI pricing visibility panel in AI Config page — **DONE in Phase 10V-1** (2026-06-21). `GET /api/admin/ai/pricing` endpoint, `AiModelPricingItem` DTO, Section 4 pricing table in AI Config page. No migration. 2046 .NET + 880 Angular tests pass.
- ~~TODO-10V-2~~: Database-backed pricing overrides — **DONE in Phase 10V-2** (2026-06-21). `AiModelPricingOverride` entity + migration `T53_AiModelPricingOverrides`. `IAiPricingResolver` resolves DB-first then config. CRUD endpoints. Audit log. Soft-deactivate. 2073 .NET tests pass. Provider runtime wiring deferred to 10V-3.
- ~~TODO-10V-3~~: Wire `IAiPricingResolver` into `OpenAiProvider`, `GeminiProvider`, `AnthropicProvider` + frontend override management UI — **DONE in Phase 10V-3** (2026-06-21). All three providers now use resolver (DB override → config → 0-cost). Override list/create/edit/deactivate UI added to AI Config page. 2073 .NET + 891 Angular tests pass. Zero-cost alert UI deferred to TODO-10V-3B.
- ~~TODO-10V-3B~~: Zero-cost alert in AI Usage summary — **DONE in Phase 10V-3B** (2026-06-21). `zeroCostCallCount` + `zeroCostTotalTokens` fields added to `AiUsageSummaryDto` and summary API response. Handler computes them from filtered logs (cost=0 AND tokens>0). Warning alert in AI Usage page updates with filters. +5 backend integration tests, +2 unit tests, +5 frontend tests. 2080 .NET + 896 Angular tests pass.
- TODO-10V-UNIQUE-CONSTRAINT: Optional — add DB unique index on `(ProviderName, ModelName, EffectiveFromUtc)` for `ai_model_pricing_overrides` to prevent duplicate active overrides with same effective date. Currently resolves by most recent `EffectiveFromUtc`.
- TODO-10X-J-INPUT-NUMBER: implement `sp-admin-input-number` — numeric CVA wrapper for number|null inputs (currently remain native inside sp-admin-form-field).
- TODO-10X-J-SELECT-OBJECT: implement `sp-admin-select-object` — select wrapper for non-string option values (number|null object selects remain native).
- TODO-10X-J-DASHBOARD-MINITABLE: migrate dashboard recent-students mini-table from page-local CSS to sp-admin-table (projected mode).
- TODO-10X-J-T-VISUAL-BASELINE: add a proper visual regression baseline for stable admin and student screens once the admin UI has settled. Do not replace this with unit or Playwright class assertions; use screenshot or visual tooling with approved baselines.

---

## Enterprise Notification Platform (Phase 10W — roadmap defined 2026-06-21)

Gap check: docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md

- ~~TODO-10W-1~~: Backend notification foundation — **DONE in Phase 10W-1** (2026-06-21). `Notification` + `NotificationOutboxItem` entities, `INotificationService`, `NotificationService`, migration `T54_NotificationFoundation`, DI registration. 20 unit tests + 13 integration tests. 2108/2108 .NET pass.
- ~~TODO-10W-2~~: In-app notification APIs — **DONE in Phase 10W-2** (2026-06-21). `GET /api/notifications` (paged, filtered, expires-excluded), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`, `POST /api/notifications/{id}/archive`. `INotificationDispatchService` with InApp delivery and Email/SMS safe-skip. 16 API tests + 9 dispatch tests. 2131/2131 .NET pass.
- ~~TODO-10W-3~~: Bell UI — **DONE in Phase 10W-3** (2026-06-21). `NotificationService` (5 endpoints), `NotificationDropdownComponent` rewritten with signals. Loading/error/empty/live-list states. Unread badge, mark-all-read, archive. Demo data removed. 916/916 Angular tests pass. (Polling and admin-bell deferred to later phase.)
- ~~TODO-10W-4~~: Email provider + reset password wiring — **DONE in Phase 10W-4** (2026-06-21). `IEmailSender` + `EmailMessage` + `EmailSendResult` (Application). `DisabledEmailSender` (safe no-op fallback). `SmtpEmailSender` (`System.Net.Mail`, catches all exceptions). `NotificationDispatchJob` (Quartz, every 2 min). Email dispatch path in `NotificationDispatchService`. `AdminHandler.ResetStudentPasswordAsync` and `CreateStudentHandler` queue emails (no raw password in body/metadata). 2150/2150 .NET tests pass.
- ~~TODO-10W-4b~~: Token-based self-service password reset link — **DONE in Phase 10W-4B** (2026-06-21). `IPasswordResetService` + `PasswordResetHandler` (Base64Url-encoded Identity token, queues email outbox, token never logged/returned). `POST /api/admin/students/{id}/send-reset-link` (admin auth). `POST /api/auth/reset-password` (public, generic errors). `/reset-password` Angular page with signals. 8 backend integration tests + 9 frontend unit tests. 2159/2159 .NET pass, 925/925 Angular pass.
- ~~TODO-10W-4c~~: Notification dropdown source control fix — **DONE in Phase 10W-4C** (2026-06-21). `NotificationDropdownComponent` moved to committed `src/app/shared/notifications/notification-dropdown/` (selector `sp-notification-dropdown`). `StudentAppLayoutComponent` updated to use committed component. Gitignored `src/app/templates/` no longer depended on. 17 new frontend unit tests. 942/942 Angular pass.
- ~~TODO-10W-5A~~: Admin Notification Center (read-only + outbox management) — **DONE in Phase 10W-5A** (2026-06-22). `GET /api/admin/notifications` (paged, filtered by channel/status/category/severity/date/search). `GET /api/admin/notifications/outbox` (paged, filtered by channel/status/date/failedOnly). `POST /api/admin/notifications/outbox/{id}/retry` (Failed/Queued only, resets NextAttemptAtUtc). `POST /api/admin/notifications/outbox/{id}/cancel` (marks Archived). `IAdminNotificationHandler` + `AdminNotificationHandler`. `MarkCancelled()` on domain entity. 18 integration tests. Admin Notifications page (`/admin/notifications`) with tabs, filters, pagination, retry/cancel actions. 956/956 Angular + 2176/2176 .NET pass. Review: docs/reviews/2026-06-22-phase-10w-5a-admin-notification-center-review.md
- ~~TODO-10W-5B~~: Admin Send Notification — **DONE in Phase 10W-5B** (2026-06-22). `POST /api/admin/notifications/send`. Channels: InApp (immediate notification+outbox), Email (queued outbox). SMS rejected with 400. Single recipient via email lookup. Result: requestedRecipientCount/queuedCount/skippedCount/channelsQueued/errors. Slide-over form in admin notification center. 14 integration + 16 frontend tests. 2190 .NET / 972 Angular pass. Review: docs/reviews/2026-06-22-phase-10w-5b-admin-send-notification-review.md. Multi-recipient broadcast deferred.
- ~~TODO-10W-5C~~: Notification configuration — **DONE in Phase 10W-5C** (2026-06-22). `GET /api/admin/notifications/config` (safe read-only: InApp/Email/SMS/DispatchJob status; `hasPassword` bool only — raw SMTP secret never exposed). `POST /api/admin/notifications/config/email/test` (routes through `IEmailSender`; returns skipped when disabled). Angular Config tab: channel status cards, email config detail (host/port/from/ssl/hasUsername/hasPassword), test-email form. 13 backend integration tests + 10 frontend unit tests. 2203 .NET / 982 Angular pass. Review: docs/reviews/2026-06-22-phase-10w-5c-notification-configuration-review.md
- ~~TODO-10W-5C-DEFERRED~~: DB-backed notification channel configuration — **DONE in Phase 10W-5C-2** (2026-06-23). `NotificationChannelConfig` entity + migration `T57_NotificationChannelConfig`. Hybrid resolution: DB override wins, appsettings fallback. GET config returns V2 with `source` field. PUT `/email`, `/sms`, `/in-app` endpoints. Secrets stored encoded (never returned to frontend — `hasPassword`/`hasApiKey` only). Admin UI: editable email/SMS/InApp forms, secret replace-only UX, source badge. 25 backend integration tests + 9 new frontend tests. 978 .NET integration / 1004 Angular pass. Review: docs/reviews/2026-06-22-phase-10w-5c-2-db-backed-notification-channel-configuration-review.md
- ~~TODO-10W-5C-2-ENCRYPTION~~: Upgrade `SecretEncrypted` from Base64 to real ASP.NET Core Data Protection — **DONE in Phase 10W-5C-3** (2026-06-23). `ISecretProtector` / `DataProtectionSecretProtector` using `IDataProtectionProvider`. Base64 fallback on unprotect for backward compat. 7 new unit tests. Review: docs/reviews/2026-06-23-phase-10w-5c-3-runtime-config-resolver-secret-encryption-review.md
- ~~TODO-10W-EMAIL-SENDER-RUNTIME~~: Wire `SmtpEmailSender` to read DB config at send time via the config resolver — **DONE in Phase 10W-5C-3** (2026-06-23). `INotificationChannelConfigResolver` / `NotificationChannelConfigResolver` introduced. `SmtpEmailSender` constructor changed to `INotificationChannelConfigResolver`. DB row wins over appsettings at runtime. `TestEmailAsync` also uses resolver. Review: docs/reviews/2026-06-23-phase-10w-5c-3-runtime-config-resolver-secret-encryption-review.md
- ~~TODO-10W-5C-3-KEY-PERSISTENCE~~: Configure Data Protection key persistence — **DONE in Phase 10W-5C-4** (2026-06-23). `NotificationKeyProtectionOptions` bound from `DataProtection` appsettings section. `PersistKeysToFileSystem` called at DI registration time. Directory auto-created if missing; degrades gracefully on error. Docker: `dp_keys` named volume + `DataProtection__KeysPath=/app/data-protection-keys` env var. `.gitignore` updated. 5 new unit tests. Review: docs/reviews/2026-06-23-phase-10w-5c-4-data-protection-key-persistence-review.md
- ~~TODO-10W-DP-KEY-ENCRYPT~~: Encrypt the Data Protection key ring at rest — **DONE in Phase 10W-5C-5** (2026-06-23). `DataProtectionKeyMode` enum (`None`/`Certificate`). `ProtectKeysWithCertificate` called when `KeyProtectionMode=Certificate`. PFX file path + password, or Windows store thumbprint. Startup throws clearly if cert misconfigured. `.gitignore` updated for `*.pfx`/`*.p12`. 10 new unit tests. Review: docs/reviews/2026-06-23-phase-10w-5c-5-data-protection-key-encryption-hardening-review.md
- TODO-10W-DP-CLOUD-KMS: Multi-instance production deployments should use `PersistKeysToDbContext` or a cloud KMS (Azure Key Vault, AWS Secrets Manager) for key ring sharing. Deferred until horizontal scaling is required.
- ~~TODO-10W-5D~~: Notification templates foundation — **DONE in Phase 10W-5D** (2026-06-22). `NotificationTemplate` entity + migration `T55_NotificationTemplates`. `INotificationTemplateRenderer` (simple `{{VarName}}` replacement, missing vars left visible). `IAdminTemplateHandler` CRUD + preview. 4 default templates seeded (password_reset email, student_created email, manual InApp, manual Email). Admin Templates tab: list, create/edit slide-over, preview panel. 23 backend integration tests + 17 frontend unit tests. 2225 .NET / 999 Angular pass. Review: docs/reviews/2026-06-22-phase-10w-5d-notification-templates-foundation-review.md
- ~~TODO-10W-5D-RESET-INTEGRATION~~: Wire `PasswordResetHandler` and `CreateStudentHandler` to use `account.password_reset`/Email and `account.student_created`/Email templates via `INotificationTemplateRenderer`. Safe fallback to hard-coded content when template missing/inactive. Token never in metadata/audit/log/API response. 8 new integration tests. 2233 .NET / 999 Angular pass. **DONE in Phase 10W-5D-RESET-INTEGRATION** (2026-06-22). Review: docs/reviews/2026-06-22-phase-10w-5d-reset-template-integration-review.md
- TODO-10W-5D-UNIQUE-CONSTRAINT: Optional — add DB unique index on `(template_key, channel)` for active templates to promote the application-layer duplicate guard to the DB layer.
- ~~TODO-10W-PREFS~~: Notification preferences — `NotificationPreference` entity + migration `T56_NotificationPreferences`. `INotificationPreferenceService` with `IsChannelEnabledAsync` / `GetPreferencesAsync` / `UpdatePreferencesAsync`. Integrated into `NotificationService.QueueAsync`. Account/System categories required (cannot be disabled). SMS always blocked (deferred). User API GET/PUT `/api/notifications/preferences`. Admin read API GET `/api/admin/notifications/preferences/{userId}`. Angular profile section with category×channel table, SMS "Coming soon", required badge, save button. 12 backend integration tests + 11 frontend unit tests. 2246 .NET / 1011 Angular pass. **DONE in Phase 10W-PREFS** (2026-06-22). Review: docs/reviews/2026-06-22-phase-10w-prefs-notification-preferences-foundation-review.md
- ~~TODO-10W-6~~: SMS provider foundation — `ISmsSender` / `SmsMessage` / `SmsSendResult` abstraction. `DisabledSmsSender` (always skipped, never throws). `SmsOptions` config class bound from `Sms` appsettings section. `AdminSmsConfigStatus` DTO (safe fields only — no ApiKey value returned to frontend). `NotificationDispatchService` wired with `ISmsSender`. Admin config tab shows SMS status/provider/senderId/hasApiKey. Admin send UI retains "SMS not yet available" note. 2246 .NET / 1011 Angular tests pass. **DONE in Phase 10W-6** (2026-06-22). Review: docs/reviews/2026-06-22-phase-10w-6-sms-provider-foundation-review.md
- TODO-10W-PHONE: Phone number collection and verification — add `PhoneNumber` field to `StudentProfile` (or a dedicated table), phone verification flow (OTP/confirmation), opt-in/STOP compliance, before any real SMS provider is activated.
- TODO-10W-SMS-PROVIDER: Real SMS provider — `TwilioSmsSender` (or similar). Config: `Sms__Provider=Twilio`, `Sms__Twilio__AccountSid`, `Sms__Twilio__AuthToken`, `Sms__Twilio__FromNumber`. Rate limiting per user per day. Requires TODO-10W-PHONE.
- ~~TODO-10W-FINAL~~: Notification platform closure audit — all 14 10W sub-phases verified closed, security/PII audit passed (token/password/SMS-secret never exposed), user isolation confirmed, all 4 seeded templates verified, docs updated. 2246 .NET / 1011 Angular pass. **DONE in Phase 10W-FINAL** (2026-06-22). Review: docs/reviews/2026-06-22-phase-10w-final-notification-platform-closure-audit.md
- ~~TODO-10W-FINAL-2~~: Notification platform re-closure audit after 5C-2/5C-3/5C-4/5C-5 — all 24 checks pass, one stale comment corrected, 2291 .NET / 1004 Angular pass. Platform production-ready for in-app/email on single-host Docker. **DONE in Phase 10W-FINAL-2** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10w-final-2-notification-platform-reclosure-audit.md
- ~~TODO-10Students-F-F~~: server-side student pagination, filtering, sorting — **DONE in Phase 10Students-F-F** (2026-06-19). `GET /api/admin/students` now returns `PagedResponse<StudentListItem>`. Query params: page, pageSize, search, includeArchived, lifecycleStage, onboardingStatus, cefrLevel, sortBy, sortDir. Admin students component is server-driven. 756 Angular + 1944 .NET tests pass.
- ~~TODO-10Students-F-G~~: add lifecycle/onboardingStatus/cefrLevel filter selects to the admin students filter bar UI — **DONE in Phase 10Students-F-G** (2026-06-20). Three `sp-admin-select` instances wired into filter bar. Clear filters button. 32 Angular tests pass.
- ~~TODO-10X-L~~: fix shared admin overlay/slide-over/table-action bugs — **DONE in Phase 10X-L** (2026-06-20). `sp-admin-slide-over` z-index raised to 1000+, `closeOnBackdrop` default changed to false, `stackIndex` input added for stacked panels. Set CEFR and Assign Policy flows converted from centred modal to `sp-admin-slide-over`. Table-actions dropdown fixed with `position:fixed` + `getBoundingClientRect()` to escape overflow parents. 791 Angular tests pass.
- TODO-10X-L-MODAL-MIGRATE: convert remaining admin-student-detail modals (Edit student, Reset password, Reset data, Lifecycle confirm) from `.sp-admin-modal` pattern to `sp-admin-slide-over` or a dedicated confirm component. Deferred — these are high-stakes destructive flows; convert in a focused modal-cleanup sprint.
- TODO-10X-TABLE-ACTIONS-CDK: if Angular CDK is adopted, migrate `sp-admin-table-actions` dropdown to `CdkOverlay` with `FlexibleConnectedPositionStrategy` for more robust portal-based positioning.
- TODO-10U-GAP-1: Add `OriginalProviderName` and `OriginalModelName` (nullable string) to `AiUsageLog`. Set when `IsFallback=true` so the primary that failed is auditable. Requires migration + `AiExecutionService` change. Identified in Phase 10U-1/10U-2 enterprise gap audit.
- TODO-10U-GAP-2: Add `AttemptNumber` (int, default 1) to `AiUsageLog`. Increment per retry in `AiExecutionService`. Enables: "what % of calls required 2+ attempts?" Requires migration. Identified in Phase 10U-1/10U-2 enterprise gap audit.
- TODO-10U-GAP-3: Add `PromptKey` (string?) and `PromptVersion` (int?) to `AiUsageLog`. Pass from prompt-rendering layer. Enables prompt A/B cost/quality analysis. Requires migration. Identified in Phase 10U-1/10U-2 enterprise gap audit.
- TODO-10U-GAP-4: Add `LearningActivityId` (Guid?) and `LearningSessionId` (Guid?) to `AiUsageLog`. Set where available from caller context. Enables per-session cost tracing. Requires migration. Identified in Phase 10U-1/10U-2 enterprise gap audit.
- TODO-10U-GAP-5: Add `AdminUserId` (Guid?) to `AiUsageLog` for admin-initiated calls (test connection, category test). Required for multi-tenant billing isolation. Requires migration. Identified in Phase 10U-1/10U-2 enterprise gap audit.
- TODO-10U-GAP-6: Split `AiUsageLog.CostUsd` into `InputCostUsd` + `OutputCostUsd` for per-component cost auditing. High effort (migration + all call sites). Defer until a pricing admin UI is implemented. Note: 10U-8 became CSV export, not pricing admin; pricing admin UI remains deferred. Identified in Phase 10U-1/10U-2 enterprise gap audit.
- TODO-10U-GAP-7: Add `RequestType` enum column to `AiUsageLog` (`Llm`, `Tts`, `Stt`). Enables filtering and cost breakdown by modality without string prefix matching on `FeatureKey`. Requires migration. Identified in Phase 10U-1/10U-2 enterprise gap audit.

---

## Enterprise Auth / Security (Phase 10Auth-F — roadmap defined 2026-06-23)

Gap check: docs/reviews/2026-06-23-phase-10auth-f-0-enterprise-auth-security-gap-check.md

- ~~TODO-10Auth-F-1~~: Auth security baseline — lockout (5 attempts, 15 min), AuthLogin rate limiter (10 req/5 min), AuthReset rate limiter (3 req/15 min), password policy hardened (10 chars, upper+lower+digit+special), SecurityHeadersMiddleware (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy), 13 new integration tests. 2304/2304 pass. **DONE in Phase 10Auth-F-1** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-1-immediate-auth-security-hardening.md
- ~~TODO-10Auth-F-2~~: Auth event audit log — `AuthSecurityEvent` entity + migration `T58_AuthEventAuditLog`, `IAuthSecurityAuditService`, audit in Login/ChangePassword/PasswordReset/CreateStudent handlers, `GET /api/admin/auth-events` (paginated, filtered). 16 new integration tests. 2319/2319 pass. Rate limiter split into `AuthReset` + `AuthChangePassword`. **DONE in Phase 10Auth-F-2** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-2-auth-event-audit-log.md
- ~~TODO-10Auth-F-3~~: Security notifications — `account.password_changed` (InApp+Email), `account.password_reset_requested` (InApp only), `account.password_reset_succeeded` (InApp+Email), `account.locked_out` (InApp+Email). 7 new templates seeded. Lockout notification fires on transition only (anti-spam). 11 new integration tests. 2330/2330 pass. **DONE in Phase 10Auth-F-3** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-3-security-notifications.md
- ~~TODO-10Auth-F-4~~: Refresh token / session management — `UserRefreshToken` entity + migration `T59_RefreshTokensAndSessions`, `IRefreshTokenService` (hash-only, rotation, reuse detection, revoke-all). Login issues refresh token. Password change/reset revokes all sessions. `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/revoke-sessions`. `AuthRefresh` rate limiter. 6 new audit event types. 20 new integration tests. 2349/2349 pass. **DONE in Phase 10Auth-F-4** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-4-refresh-tokens-session-management.md
- ~~TODO-10Auth-F-5~~: Google OAuth / external login foundation — `IGoogleTokenValidator` abstraction, `IExternalLoginService`, `ExternalLoginService` (link/provision/issue tokens), `GoogleExternalLoginOptions` config, `POST /api/auth/external/google`, `AuthExternalLogin` rate limiter (20/5min/IP), `account.external_login_linked` notification templates (InApp+Email), 7 new audit event types, 20 new integration tests. No migration (uses existing `AspNetUserLogins`). 2369/2369 pass. **DONE in Phase 10Auth-F-5** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-5-google-oauth-external-login-foundation.md
- ~~TODO-10Auth-F-6~~: Admin security settings UI — `GET /api/admin/security/settings` (read-only, Admin-role), `GET /api/admin/security/auth-events` alias, Angular `/admin/security` page (Overview + Auth Events tabs, sp-admin-* wrappers), Security nav item in System section. 16 frontend unit tests. 2369 .NET / 1025 Angular pass. **DONE in Phase 10Auth-F-6** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-6-admin-security-settings-ui.md
- ~~TODO-10Auth-F-FINAL~~: Auth/security closure audit — all 10Auth-F sub-phases verified (F-1 through F-6), 23 auth event types confirmed, security invariants confirmed (no secrets/tokens in audit/logs), all gates passed: 2369 .NET / 1025 Angular. **DONE in Phase 10Auth-F-FINAL** (2026-06-23). Review: docs/reviews/2026-06-23-phase-10auth-f-final-enterprise-auth-security-closure-audit.md

---

## UI / Backend Capability Reconciliation (Phase 10UI-AUDIT-0 — next recommended phase)

### TODO-10UI-AUDIT-0 — Full UI / Backend Capability Reconciliation
**What:** Systematic audit of every backend API capability against the current Angular UI (admin + student). Produce a route-by-route matrix: route | backend capabilities | UI currently exposes | missing UI | priority | recommended next phase.
**Why:** Multiple backend feature epics (10Auth-F, 10W, 10V, 10R, 10U, 10P, 10O, 10N) have closed without always updating or completing the corresponding UI. The admin panel and student shell may have stale, missing, or misleading UI for capabilities that already exist in the backend.
**Admin routes to cover:** `/admin`, `/admin/students`, `/admin/ai-config`, `/admin/prompts`, `/admin/usage`, `/admin/exercise-types`, `/admin/integrations`, `/admin/diagnostics`, `/admin/notifications`, `/admin/curriculum`, `/admin/security`.
**Student routes to cover:** `/student/today`, `/student/journey`, `/student/practice`, `/student/progress`, `/student/profile`.
**Output:** Route-by-route matrix + prioritised list of next UI phases.
**Deferred from:** Phase 10Auth-F-FINAL closure audit, 2026-06-23.

---

## UI Gap Tracking (from Phase 10UI-AUDIT-0, 2026-06-23)

### ~~TODO-UI-01~~ — Add missing nav links: usage-policies and curriculum — **DONE in Phase 10UI-FIX-1** (2026-06-23)

`/admin/usage-policies` ("Usage Policies") and `/admin/curriculum` ("Curriculum") nav items added to both desktop sidebar and mobile drawer in `admin-app-layout.component.html`. `/admin/careers` has no admin UI page and is deferred (backend endpoint exists under `AdminController`).

### TODO-UI-01B — Add Careers nav link when admin page exists (deferred)
**What:** Add `/admin/careers` nav item to the admin sidebar.
**Why:** `GET /api/admin/careers` returns career data used in onboarding. No admin CRUD page exists yet.
**Context:** `AdminCareersComponent` exists as a minimal list page — evaluate whether it warrants a nav link before adding.
**Deferred from:** Phase 10UI-FIX-1, 2026-06-23.

---

### TODO-UI-02 — Admin student detail: readiness pool health section (P1)
**What:** Add a "Readiness pool" section to `/admin/students/:id` wiring `GET /api/admin/students/{id}/readiness-pool/health` and `/readiness-pool`.
**Why:** Admins cannot diagnose why a student has no ready activities. The pool health endpoint has existed since Phase 10N.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-03 — Admin student detail: activity history section (P1)
**What:** Add an "Activity history" section to `/admin/students/:id` calling `GET /api/admin/students/{id}/activity-history`.
**Why:** Audit history (admin actions) is shown but not the student's actual activity attempts. These are different datasets. The endpoint has existed since the early admin phases.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-04 — Admin onboarding flow viewer page/modal (P1)
**What:** Add a read-only view for the active onboarding flow definition, wiring `GET /api/admin/onboarding/flow`. Can be a new `/admin/onboarding` route or a modal from the dashboard.
**Why:** Admins cannot inspect step definitions, answer mappings, or enabled steps without a database query. Endpoint has existed since Phase 10I.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-05 — Remove or repurpose orphan AdminUsageComponent (P1)
**What:** Delete `src/LinguaCoach.Web/src/app/features/admin/admin-usage/` folder (the old placeholder with static emoji cards). It is never rendered (the `/admin/usage` route redirects to the real AI usage page) but is confusing dead code.
**Why:** The stale placeholder says "Analytics not yet tracked" with emoji — contradicting the fully implemented AI usage page. Confusing for future agents and developers.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-06 — Admin dashboard: replace static "AI provider: Configured" card (P1)
**What:** Replace the always-static "AI provider: Configured" stat card on `/admin` with a live status derived from `GET /api/admin/ai/categories`.
**Why:** Card always shows "Configured" regardless of whether credentials are actually set. Misleads admins into thinking AI is ready when it may not be.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-07 — Notification config: add SMS "foundation only" label (P2)
**What:** Add a visible "Foundation only — not active" or "Coming soon" label to the SMS section in `/admin/notifications` Configuration tab.
**Why:** `DisabledSmsSender` always skips. No UI indication that SMS is not functional. Admins could waste time configuring a non-working channel.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-08 — Student progress page: show CEFR level badge (P2)
**What:** Display the student's current CEFR level badge on the `/progress` page.
**Why:** CEFR is the student's primary achievement signal. It is tracked in the backend and shown in admin but not on the student's own progress page.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-09 — Admin integrations: add readiness pool replenishment status (P2)
**What:** Add a readiness pool health summary to `/admin/integrations` page. May require a new aggregate endpoint.
**Why:** Integrations page shows lesson generation batch status but not practice gym pool health. Admins cannot tell if the background replenishment job is working.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### TODO-UI-10 — Admin security page: add deferred feature notes (P2)
**What:** Add visible "Deferred" or "Not yet implemented" notes for per-user session revocation, MFA, CAPTCHA, HSTS on `/admin/security`.
**Why:** Admins may assume these are missing bugs rather than intentionally deferred features.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

## Design Reference Alignment (Phase 10UI-FIX-1, 2026-06-23)

### TODO-UI-11 — Admin careers orphan: tombstone or suppress route (P0)
**What:** `/admin/careers` (`AdminCareersComponent`) is an orphan: no sidebar link, title says "Curriculum", no `sp-admin-*` wrappers, duplicates vocabulary management that now lives in `/admin/curriculum`. Decision needed: redirect `/admin/careers` to `/admin/curriculum`, or add a tombstone page, or suppress the route.
**Why:** An admin who knows the URL can access a broken, unwrapped, stale page. The backend capabilities (career profiles, curriculum words) are real and served by `/admin/curriculum`. Having two conflicting pages is misleading.
**Context:** `AdminCareersComponent` should NOT be wrapper-migrated — it is an orphan scheduled for removal. Preferred outcome: redirect route in `app.routes.ts`.
**Deferred from:** Phase 10UI-FIX-4, 2026-06-23.

---

### TODO-UI-DESIGN-REFERENCE — Keep docs/design/admin-reference-alignment.md current
**What:** Update `docs/design/admin-reference-alignment.md` whenever new admin pages are added or nav structure changes.
**Why:** This doc is the authoritative mapping between the reference React design and the Angular `sp-admin-*` implementation. Stale mapping causes drift.
**Deferred from:** Phase 10UI-FIX-1, 2026-06-23.

### ~~TODO-UI-SHELL-ALIGNMENT~~ — Admin shell visual alignment — **DONE in Phase 10UI-FIX-2** (2026-06-23)

Brand palette, sidebar widths, header height, border colors, background, nav item active state with indicator bar — all aligned to reference design. 1035 Angular tests pass.

### TODO-UI-COMPONENT-ALIGNMENT — Align admin page visual patterns to reference design
**What:** Per-page visual review: compare each `docs/design/speakpath/admin/pages/*.jsx` against its Angular counterpart. Identify layout/spacing/card/table differences.
**Why:** Pages were built before the reference design existed. Some may deviate from the visual intent.
**Context:** Dashboard, Students, and AI Usage are highest-traffic and highest-risk for divergence. Start there.
**Deferred from:** Phase 10UI-FIX-1, 2026-06-23.

### TODO-UI-PAGE-ALIGNMENT — Reference dashboard stat cards: live data audit
**What:** Audit which dashboard stat cards show live data vs. static values. Static values: "AI provider: Configured" always shows as configured. Align with reference design's computed KPI approach.
**Why:** Admin trust in dashboard depends on data being real.
**Context:** Reference `dashboard.jsx` computes all stats from mock data. Angular counterpart has at least one static card.
**Deferred from:** Phase 10UI-FIX-1, 2026-06-23. Related: TODO-UI-06.
