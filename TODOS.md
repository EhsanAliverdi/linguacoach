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

### ~~TODO-UI-02 — Admin student detail: readiness pool health section~~ DONE
**Completed:** Phase 10UI-FIX-7, 2026-06-23. Commit: a26293e.
Pool health section added with today/gym breakdown, KPI strip integration, and `sp-admin-badge` replenishment indicators.

---

### ~~TODO-UI-03 — Admin student detail: activity history section~~ DONE
**Completed:** Phase 10UI-FIX-7, 2026-06-23. Commit: a26293e.
Activity history section existed; migrated to `sp-admin-table` wrapper and `sp-admin-badge` result badges.

---

### TODO-UI-04 — Admin onboarding flow viewer page/modal (P1)
**What:** Add a read-only view for the active onboarding flow definition, wiring `GET /api/admin/onboarding/flow`. Can be a new `/admin/onboarding` route or a modal from the dashboard.
**Why:** Admins cannot inspect step definitions, answer mappings, or enabled steps without a database query. Endpoint has existed since Phase 10I.
**Deferred from:** Phase 10UI-AUDIT-0, 2026-06-23.

---

### ~~TODO-UI-05~~ — Remove orphan AdminUsageComponent — **DONE in Phase 10UI-FIX-5** (2026-06-23)

`src/LinguaCoach.Web/src/app/features/admin/admin-usage/` folder deleted. Component was unreachable (no route pointed to it). Real AI usage page is `AdminAiUsageComponent` at `/admin/usage`.

---

### ~~TODO-UI-06~~ — Admin dashboard: replace static "AI provider: Configured" card — **DONE in Phase 10UI-FIX-5** (2026-06-23)

Dashboard stat card now calls `listAiCategories()` and shows "Configured" / "N/M configured" / "Not configured" / "Unknown" based on real `AiConfigCategoryItem.providerName` values. AI System card replaced with live category loop. 1045/1045 Angular tests pass.

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

### ~~TODO-UI-11~~ — Admin careers orphan: redirect route — **DONE in Phase 10UI-FIX-5** (2026-06-23)

`/admin/careers` route changed to `redirectTo: 'curriculum'`. Stale `AdminUsageComponent` (emoji placeholder) deleted. `AdminCareersComponent` file retained pending final removal decision — no sidebar link, no active route.
**What:** `/admin/careers` (`AdminCareersComponent`) is an orphan: no sidebar link, title says "Curriculum", no `sp-admin-*` wrappers, duplicates vocabulary management that now lives in `/admin/curriculum`. Decision needed: redirect `/admin/careers` to `/admin/curriculum`, or add a tombstone page, or suppress the route.
**Why:** An admin who knows the URL can access a broken, unwrapped, stale page. The backend capabilities (career profiles, curriculum words) are real and served by `/admin/curriculum`. Having two conflicting pages is misleading.
**Context:** `AdminCareersComponent` should NOT be wrapper-migrated — it is an orphan scheduled for removal. Preferred outcome: redirect route in `app.routes.ts`.
**Deferred from:** Phase 10UI-FIX-4, 2026-06-23.

---

### ~~TODO-UI-12~~ — Create Student wrapper migration — **DONE in Phase 10UI-FIX-6** (2026-06-23)

`CreateStudentComponent` migrated to `sp-admin-page-header`, `sp-admin-page-body`, `sp-admin-card`, `sp-admin-form-field`, `sp-admin-input`, `sp-admin-alert`, `sp-admin-button`. Native `<select>` retained for `[ngValue]` number/null bindings (known incompatibility). 17 new tests. 1065/1065 pass.

### ~~TODO-UI-13~~ — Curriculum filter bar + page body wrapper — **DONE in Phase 10UI-FIX-6** (2026-06-23)

`AdminCurriculumComponent` filter bar selects migrated from raw `<select class="sp-input">` to `sp-admin-select` with computed `cefrOptions`/`skillOptions`/`activeOptions`. Page wrapped in `sp-admin-page-body`. 2 new tests.

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

---

## Admin UI — Reference Redesign Phases (from 10UI-REDESIGN-0, 2026-06-23)

Rollout plan: docs/reviews/2026-06-23-phase-10ui-redesign-0-admin-reference-redesign-rollout-plan.md

- [x] TODO-REDESIGN-1 (P1): **10UI-REDESIGN-1** — Dashboard reference redesign. DONE 2026-06-23. Review: docs/reviews/2026-06-23-phase-10ui-redesign-1-dashboard-reference-redesign.md
- ~~TODO-REDESIGN-2~~ — **DONE in 10UI-REDESIGN-2** (2026-06-23). Students list: KPI strip, rows-per-page selector, filter bar aligned. Create Student: two-column layout, sticky aside panel, security note, back link. 1138/1138.
- ~~TODO-REDESIGN-3~~ — **DONE in 10UI-REDESIGN-3** (2026-06-23). Hero section: coloured initials avatar, name/email, lifecycle/onboarding/CEFR badges, action group. KPI strip upgraded to sp-admin-kpi-card. Danger zone card: Reset data, Archive, Reactivate. 30 new tests. 1168/1168.
- TODO-REDESIGN-4 (P2): **10UI-REDESIGN-4** — Curriculum + Exercise Types redesign. Curriculum: track-level icon cards alongside objectives table (backend has objectives, not tracks — show placeholder for track management). Exercise types: card-per-type layout with icon tile, expandable stat grid.
- TODO-REDESIGN-5 (P2): **10UI-REDESIGN-5** — AI Config + Prompts redesign. AI Config: rate limits/quotas section (placeholder — no endpoint), dedicated TTS settings card. Prompts: tab+search same row, inline row action buttons.
- TODO-REDESIGN-6 (P2): **10UI-REDESIGN-6** — AI Usage + Usage Policies redesign. AI Usage: date range pills, SVG area chart, SVG bar chart, SVG heatmap (all placeholders or real data per endpoint availability). Usage Policies: no reference counterpart — keep as-is.
- TODO-REDESIGN-7 (P1): **10UI-REDESIGN-7** — Notifications + Integrations redesign. Notifications: webhook channel card (placeholder). Integrations: add platform integrations section (SMTP=real, Webhook/Slack/Analytics=placeholder, Admin API=placeholder) alongside existing MinIO/jobs infrastructure section.
- TODO-REDESIGN-8 (P3): **10UI-REDESIGN-8** — Diagnostics + Security reference redesign. Diagnostics: minor border card style. Security: no reference counterpart — keep current.
- ~~TODO-REDESIGN-FINAL~~: **10UI-REDESIGN-FINAL** — Admin UI reference alignment closure audit across all routes. **DONE 2026-06-23.** Notifications header fix, 2 new tests, 1253/1253 PASS. Closure review: `docs/reviews/2026-06-23-phase-10ui-redesign-final-admin-ui-reference-closure-audit.md`.
- ~~TODO-STYLE-1~~: **10UI-STYLE-1** — Admin reusable component visual alignment. **DONE 2026-06-23.** Token colour palette → warm purple ink; green → brand green; shadows → purple-tinted; radius scale updated; KPI card typography upgraded; card title uses tokens; badge weight 700; table header muted/800; page header tighter tracking; 11 new sp-adm-* form classes; notifications+integrations Tailwind debt closed. 1253/1253 PASS. Review: `docs/reviews/2026-06-23-phase-10ui-style-1-admin-reusable-component-visual-alignment.md`.

---

## Admin UI — Visual Fidelity Gaps (from 10UI-VISUAL-FINAL, 2026-06-24)

Audit: docs/reviews/2026-06-24-phase-10ui-visual-final-admin-visual-fidelity-audit.md

### TODO-VISUAL-01 — Dashboard activity trends chart (P1) ✅ DONE 2026-06-24
**What:** Replace `skeleton="chart"` placeholder on dashboard "Activity trends" card with a real SVG bar or area chart.
**Done:** `GET /api/admin/dashboard/activity-trends?period=7d|30d|90d` returns daily `activityCount` buckets. Dashboard wires the data via `activityTrends` signal; `sp-admin-breakdown-bars` renders bars. Hero KPI "Activity attempts (7d)" summed from buckets.
**Phase:** 10UI-BACKEND-AGG-1 / AGG-2

### TODO-VISUAL-02 — Dashboard score distribution chart (P1) ✅ DONE 2026-06-24
**What:** Replace `skeleton="chart"` placeholder with real 5-bin horizontal bar chart.
**Done:** `GET /api/admin/dashboard/score-distribution?period=7d|30d|90d` returns 5 fixed buckets plus `averageScore`. Dashboard wires data via `scoreDistribution` signal; breakdown bars render. Hero KPI "Avg score (7d)" shows real `averageScore`.
**Phase:** 10UI-BACKEND-AGG-1 / AGG-2

### TODO-VISUAL-03 — Dashboard AI spend donut/breakdown (P1)
**What:** Replace `skeleton="grid"` placeholder with per-category AI cost breakdown bars.
**Status:** Partially done. `GET /api/admin/ai-usage/by-category` returns per-feature-key breakdown. Hero KPI "AI cost (7d)" wired. Full donut/spend-by-category-type chart still needs a grouped-category backend field.
**Phase:** 10UI-BACKEND-AGG-1 / AGG-2 (partial)

### TODO-VISUAL-04 — Dashboard streak leaderboard (P1)
**What:** Replace `skeleton="timeline"` placeholder with named student streak list (top 5).
**Needs:** No streak entity in domain. Deferred pending streak model addition.
**Phase:** Future

### TODO-VISUAL-05 — AI Usage activities bar chart (P1) ✅ DONE 2026-06-24
**What:** Replace `skeleton="chart"` placeholder with 14-bar activities-per-day chart.
**Done:** `GET /api/admin/ai-usage/aggregate-trends?period=7d|30d|90d` returns daily AI cost/call buckets. AI Usage page wires via `aiUsageTrends` signal.
**Phase:** 10UI-BACKEND-AGG-1

### TODO-VISUAL-06 — AI Usage student engagement heatmap (P1)
**What:** Replace `skeleton="ring"` placeholder with 7×12 GitHub-style activity heatmap.
**Needs:** New backend endpoint `GET /admin/stats/activity-heatmap?weeks=12`.
**Phase:** 10UI-BACKEND-AGG-2 (complex)

### TODO-VISUAL-07 — Dashboard system health: per-service latency (P2)
**What:** Add per-service response-time bars to system health card (Writing AI, Feedback AI, DB, Auth).
**Needs:** New backend endpoint `GET /admin/health/detailed` returning per-service latency.
**Phase:** 10UI-BACKEND-AGG-1

### TODO-VISUAL-08 — Dashboard pending actions: computed contextual list (P2) ✅ DONE 2026-06-24
**What:** Replace hardcoded "Admin quick actions" 4-card grid with a computed contextual pending-action list derived from real student + AI config state.
**Done:** `pendingActions` computed signal on dashboard derives from `aiCategories()` (unconfigured count) and `students()` (no-CEFR, not-onboarded). Shows polished empty state if none. Links to `/admin/ai-config` and `/admin/students`.
**Phase:** 10UI-POLISH-1

### TODO-VISUAL-09 — AI Config: category edit should use slide-over not centered modal (P2)
**What:** The AI Config category edit form uses `sp-admin-modal` (centered). Reference uses a right-side slide-in drawer.
**Can be built now** using existing `sp-admin-slide-over` component.
**Phase:** 10UI-POLISH-1

### TODO-VISUAL-10 — Toggle switch component (P2) ✅ DONE 2026-06-24
**What:** Exercise Types and Notifications use button variants for enable/disable. Reference uses a dedicated CSS toggle-switch pill component.
**Done:** `sp-admin-toggle` created (CVA, accessible role=switch, 17 specs). Applied to Notifications channel isEnabled fields (in-app, email, SMS). Exercise Types left on context-menu — inline toggle too risky without table redesign (deferred to TODO-VISUAL-10B).
**Phase:** 10UI-POLISH-1

### TODO-VISUAL-11 — Students list and dashboard: avatar tiles in rows (P3) ✅ DONE 2026-06-24
**What:** Avatar tiles (coloured initial circle) added to dashboard at-risk list and recent students table in VISUAL-FINAL. Students list page itself still has no per-row avatars.
**Done:** `avatarInitial()` and `avatarColor()` added to `admin-students.component.ts`. Name cell updated to show coloured initial circle. 8 specs added for avatar helpers and DOM rendering.
**Phase:** 10UI-POLISH-1

### TODO-VISUAL-12 — Dashboard live events feed (P2)
**What:** Real-time scrollable events feed card. Currently skeleton placeholder.
**Needs:** SignalR or polling endpoint for live diagnostic events.
**Phase:** 10UI-BACKEND-AGG-2 (complex)

---

## Admin UI parity

### TODO-REBUILD-1 — Admin design route map verified (DONE 2026-06-24)
**What:** Verified Angular admin shell, sidebar nav, and routes against new design source `docs/design/speakpath/admin/`.
**Outcome:** All 15 design nav entries map to existing routes and components. Added `/admin/students/create` redirect alias. Sidebar sections/labels match design exactly.
**Review:** `docs/reviews/2026-06-24-phase-10ui-parity-rebuild-1-admin-page-by-page-exact-design-match.md`
**Follow-up:** Run frontend build/test in shared checkout before merge (worktree lacks node_modules).

### TODO-REBUILD-2A — Bound unbounded admin tables (DONE 2026-06-24)
**What:** Screenshot-driven visual fix. AI Usage "By feature" and "Calls over time" tables, and Curriculum objectives table, rendered all rows unbounded, making pages extremely long versus paginated design.
**Done:** Added client-side pagination (reuse `SpAdminPaginationComponent`) to those three tables. Build green, 1361 tests pass.
**Review:** `docs/reviews/2026-06-24-phase-10ui-parity-rebuild-2-screenshot-driven-admin-visual-fixes.md`
**Phase:** 10UI-PARITY-REBUILD-2A

### TODO-REBUILD-2B — Diagnostics Background Jobs section (P2)
**What:** Design shows Background Jobs (4 KPI tiles + recent batches table). No background-jobs API exists.
**Needs:** Backend endpoint for job batch summary. Out of scope for visual-only phase.
**Phase:** 10UI-PARITY-REBUILD-2B / backend-agg

---

## Admin AI Operations (Phase 20A, 2026-07-02)

### TODO-20A-1 — AI provider health check / ping endpoint
**What:** Add `GET /api/admin/ai-operations/provider-health` (or similar) that pings each configured AI provider (OpenAI/Gemini/Anthropic) with a lightweight request and reports reachability/latency.
**Why:** Phase 20A's dashboard shows historical usage and failure counts, but has no live "is the provider reachable right now" signal. The phase brief explicitly excluded this ("Do not implement: New retry execution tools" and no provider-ping requirement was in the "do implement" list).
**Context:** `ISpeakingEvaluationProvider`/writing provider equivalents already expose `ProviderName`/`IsSupported`; a ping would need a new lightweight capability check method on each provider implementation.
**Deferred from:** Phase 20A engineering review, 2026-07-02.

### TODO-20A-2 — Retry tooling for failed speaking/writing evaluations
**What:** Add an admin action to re-queue a failed `SpeakingEvaluation`/`WritingEvaluation` back to `Pending` for retry, with a re-run cap and audit log entry.
**Why:** The AI Operations dashboard surfaces failed evaluations but admins currently have no way to act on them from the UI — they'd need direct DB access.
**Context:** `SpeakingEvaluation`/`WritingEvaluation` domain entities have `RetryCount` already tracked but no public "retry" state transition exists on either entity.
**Deferred from:** Phase 20A engineering review, 2026-07-02 (explicitly out of scope: "Do not implement: New retry execution tools").

### TODO-20A-3 — Real-time job/queue depth
**What:** Expose actual queue depth for speaking/writing evaluation and generation jobs, distinct from the current "count of rows with Status=Pending" approximation.
**Why:** `AdminAiOperationsSummaryDto` lists `RealTimeJobQueueDepth` under `unavailableSections` because there is no dedicated job/queue table in this codebase — evaluations run inline or via Quartz batch jobs against entity status, not a persisted queue with depth/position semantics.
**Context:** Would likely require either a dedicated queue table or Quartz job-store introspection (`IJobExecutionContext`/scheduler metadata) surfaced through a new admin endpoint.
**Deferred from:** Phase 20A engineering review, 2026-07-02.

### TODO-20A-4 — Cost estimation for zero-cost / NoOp provider calls
**What:** Optionally estimate a "would-be cost" for AI calls made through zero-cost or `NoOp` providers, clearly labelled as an estimate.
**Why:** `AdminAiOperationsSummaryDto` lists this under `unavailableSections` — the phase brief explicitly said "Do not invent cost values," so cost is only ever shown when already persisted on the AI usage log.
**Context:** Would require a pricing table lookup even for providers that don't charge (e.g. to answer "what would this have cost on a real provider"), which is a product decision, not just an engineering one.
**Deferred from:** Phase 20A engineering review, 2026-07-02.

---

## Admin Runtime Settings / Feature Gates (Phase 20B)

### TODO-20B-1 — Wire `RuntimeSettingOverride` into `ReadinessPoolReplenishmentService`'s live read path
**Status: RESOLVED in Phase 20C (2026-07-02).** `IEffectiveReadinessPoolSettingsProvider` (`Application/ReadinessPool/`, implemented in `Infrastructure/ReadinessPool/EffectiveReadinessPoolSettingsProvider.cs`) now resolves appsettings + active `RuntimeSettingOverride` rows, and both `ReadinessPoolReplenishmentService` and `PracticeGymSuggestionService` depend on it instead of `IOptions<ReadinessPoolReplenishmentOptions>` directly. Admin edits to review-scaffold/Practice-Gym-pilot settings now take effect on the next job run/request with no redeploy. See `docs/architecture/runtime-settings-and-feature-gates.md` ("Runtime-effective wiring") and the Phase 20C review doc.

### TODO-20B-2 — Make `ReadinessPoolReplenishmentOptions` buffer/threshold fields admin-editable
**What:** Extend the feature-gate registry so `TodayLessonPoolTargetCount`, `PracticeGymPoolTargetCount`, `MinimumReadyThreshold`, `MaxBufferCount`, `ReadyItemExpiryDays`, `ReservedItemExpiryHours`, `GeneratingTimeoutMinutes`, `FailedRetryDelayMinutes`, and `MaxItemsGeneratedPerRun` are runtime-editable (currently read-only/observational in the registry).
**Why:** These weren't in the phase's required editable list (that list mapped to `LessonGenerationSettings` fields instead), so they were left read-only to keep scope tight.
**Context:** Would reuse the same `RuntimeSettingOverride` table and `review-scaffold-generation`-style group pattern already built in this phase.
**Deferred from:** Phase 20B engineering review, 2026-07-02.

### TODO-20B-3 — Runtime-editable AI signal-safety gates
**What:** Allow `ApplyMasterySignals`, `MinimumConfidenceForMasterySignal`, `AllowReviewSignals`, and `AllowPositiveSignals` (speaking and writing) to be changed at runtime through the registry, with appropriate confirmation/audit requirements.
**Why:** Phase 20B's brief required these to "default conservative and must not be changed by this phase" — they are shown in the registry for visibility but forced read-only regardless of the underlying appsettings mutability.
**Context:** `AllowObjectiveCompletion`/`AllowCefrUpdate` are hardcoded `false` in code and must stay that way; only the four signal-application flags above are realistic candidates for future runtime editing.
**Deferred from:** Phase 20B engineering review, 2026-07-02 (explicit product/safety decision, not a technical limitation).

---

## Runtime Settings Effective Wiring (Phase 20C)

### TODO-20C-1 — Build real enforcement for the 7 lesson-generation fields with no consuming job
**What:** Add actual timeout/concurrency/retry-attempt enforcement so `MaxGenerationAttempts`, `GenerationTimeoutSeconds`, `MaxConcurrentGenerationJobs`, `EnableTtsGeneration`, `TtsTimeoutSeconds`, `MaxConcurrentTtsJobs`, and `PracticeGymReadyExercisesPerType` are runtime-effective (currently editable/audited but display-only).
**Why:** Code search during Phase 20C confirmed no job in this codebase reads these fields at all — `ActivityMaterializationJob` and `PracticeGymGenerationJob` have no timeout wrapper, concurrency limiter, or attempt-count retry loop today. Wiring them would mean building new behavior (not redirecting a read), which was judged out of Phase 20C's "careful and limited" mandate.
**Context:** Would likely require: a `CancellationTokenSource` timeout wrapper around AI generation calls (`GenerationTimeoutSeconds`, `TtsTimeoutSeconds`), a semaphore or Quartz `[DisallowConcurrentExecution]`-style guard per student/job type (`MaxConcurrentGenerationJobs`, `MaxConcurrentTtsJobs`), an attempt-count check before giving up on a queued item (`MaxGenerationAttempts` — note a similarly-named field already exists and is read on `ReadinessPoolReplenishmentOptions`, a different class from `LessonGenerationSettings`; do not conflate them), and a cap on the Practice Gym per-type cache size (`PracticeGymReadyExercisesPerType`).
**Deferred from:** Phase 20C engineering review, 2026-07-02.

---

## Student Data Readiness, Backfill & Pilot Cleanup (Phase 20D)

### TODO-20D-1 — Single-student Practice Gym replenishment repair action
**What:** Implement `refill_practice_gym_if_empty` — currently registered with `IsImplemented=false`.
**Why:** No single-student-scoped entry point exists today. `IReadinessPoolReplenishmentService.RunAsync()` processes all active students in one call; running it as a side effect of one admin repair button would be a wasteful global sweep, not a targeted fix.
**Context:** Needs a new, narrowly-scoped overload (or a new method) on `IReadinessPoolReplenishmentService` that runs the existing shortfall-fill logic for exactly one student. `GetHealthAsync` (already per-student, already read-only) is sufficient for the readiness *check*; only the *repair* is missing.
**Deferred from:** Phase 20D engineering review, 2026-07-02.

### TODO-20D-2 — Backfill missing activity metadata
**What:** Implement `backfill_missing_activity_metadata` — currently registered with `IsImplemented=false`.
**Why:** No concrete, safe backfill target was identified during the Phase 20D survey — "which metadata" was never scoped by the phase brief to a specific field.
**Context:** Needs a follow-up survey of `LearningActivity`/`StudentActivityReadinessItem` fields that can legitimately go missing on older records, and a decision on what a safe, non-inventive backfill value would be for each.
**Deferred from:** Phase 20D engineering review, 2026-07-02.

### TODO-20D-3 — Single-activity TTS regeneration repair action
**What:** Implement `regenerate_missing_tts_for_listening_if_supported` — currently registered with `IsImplemented=false`.
**Why:** No single-activity/single-student TTS generation entry point exists; `TtsAudioGenerationJob` only operates batch-wide on a schedule.
**Context:** Needs a new method that generates a TTS `AudioAsset` for one `LearningActivity`, reusing the same provider/config path `TtsAudioGenerationJob` already uses, gated by the existing `EnableTtsGeneration` effective setting (Phase 20C).
**Deferred from:** Phase 20D engineering review, 2026-07-02.

### TODO-20D-4 — Safe lifecycle-stage normalization repair action
**What:** Implement `normalize_student_lifecycle_if_safe` — currently registered with `IsImplemented=false`.
**Why:** Lifecycle transitions are normally driven by dedicated flows (placement completion, onboarding). Forcing a stage jump from an admin repair risks bypassing invariants that were not fully covered by the Phase 20D survey.
**Context:** Would need an explicit, reviewed rule set for which stage transitions are safe to force administratively (e.g. `CourseReady` → `ActiveLearning` once a plan and a session exist) versus which require re-running an actual flow (e.g. anything before `PlacementCompleted`).
**Deferred from:** Phase 20D engineering review, 2026-07-02.

Note: `refresh_progress_projection_if_supported` (the 5th suggested repair
action) was registered as `NotApplicable` rather than deferred — there is
no stored progress/mastery projection in this codebase to refresh;
progress is always computed live from the ledger.

---

## Controlled Student Pilot Smoke QA (Phase 20E)

### TODO-20E-1 — P0: production `PostgresException` blocking placement start and the readiness audit
**Status: FIXED in Phase 20F (2026-07-02), pending one live confirmation after deploy.** Root cause: 6 EF Core migration classes (`T62_AdaptivePlacementEngine`, `T63_PlacementResponseSubmission`, `T65_SpeakingEvaluationFoundation`, `T66_SpeakingEvaluationAppliedSignal`, `T67_WritingEvaluationTables`, `T68_WritingEvaluationAppliedSignal`) had no `.Designer.cs` file, so EF Core's migration discovery (which reads the `[Migration("id")]` attribute the code generator normally places there) never saw them — not a failure, just silent invisibility, on every environment, always. Compounded by 3 pairs of migrations independently creating the same table, latent because the "invisible" side of each pair had never run anywhere. Fixed by adding the 6 missing Designer.cs files and making all 5 affected migrations' `Up()` idempotent (`ADD COLUMN`/`CREATE TABLE`/`CREATE INDEX ... IF NOT EXISTS`). Verified against a from-scratch fresh database (all 64 migrations apply, 0 errors) and against a local sandbox independently drifted to match production's exact symptom (all previously-invisible migrations now apply; `POST /api/student/placement/start` → 201; `GET /api/admin/students/{id}/readiness` → 200; background jobs stop throwing). See `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.
**Remaining:** production itself was not directly accessed in Phase 20F (no DB/SSH/log access available); the fix was pushed via the normal `main` → CI/CD deploy pipeline, which runs `Database.Migrate()` on the real production database automatically on next API startup. **A live check against `https://speakpath.app` after deployment completes is required** to close this out — see `TODO-20F-1`.

### TODO-20E-2 — Wire `repairAllSafeStudentReadiness` ("run all") to a button in Admin Student Detail
**What:** `AdminApiService.repairAllSafeStudentReadiness` (→ `POST /api/admin/students/{id}/readiness/repair-safe-all`) exists but is not called from any button in `admin-student-detail.component.ts`. `run_all_safe_repairs` is also never returned as a `RecommendedActionKey` by any individual check, so today an admin must run the four safe repairs one at a time.
**Why:** Out of scope for Phase 20D (found during Phase 20E's use of the tooling); low risk, small UI addition.
**Context:** `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`, `src/LinguaCoach.Infrastructure/Admin/StudentPilotReadinessRepairService.cs` (`RunAllSafeRepairsAsync` already implemented server-side).
**Deferred from:** Phase 20E, 2026-07-02.

### TODO-20E-3 — Remaining mojibake (UTF-8 double-encoding) instances in code comments/test titles
**What:** `grep -rn 'â€' src/LinguaCoach.Web/src` still finds the Phase 15H-class encoding bug in code comments and Jasmine `describe`/`it` titles (never rendered to a user). Four user-visible instances were fixed in Phase 20E; these remaining ones are cosmetic-only and non-functional.
**Why deferred:** Pure churn with no user-facing benefit; not worth the diff noise outside a dedicated cleanup pass.
**Deferred from:** Phase 20E, 2026-07-02.

---

## Production Placement/Readiness P0 Unblocker (Phase 20F)

### TODO-20F-1 — Confirm the migration fix live against production
**Status: RESOLVED, confirmed live 2026-07-02.** After the fix deployed via CI/CD (`gh run 28570470545`, ~2.5 min build+deploy), live-checked against `https://speakpath.app`: `GET /api/admin/students/{id}/readiness` for `pilot.student.20e@speakpath.app` (`c2a7caff-b46a-4da4-b424-8bd5ca8c0394`) → **200**, `readyForPilot: true`, `readinessStatus: needsAttention` (0 blocking, 2 warnings, 4 info — a real, non-500, structured result). `POST /api/student/placement/start` (as the pilot student) → **201**, followed by `GET /api/student/placement/next` → **200** with a real Question 1 (Listening) rendering correctly in the UI with answer options. `/api/placement/status` and `/api/student/placement/current` also returned 200 (previously 500). Admin diagnostics showed **zero errors logged** in the 15 minutes spanning the deploy and this check.
**Context:** `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.
**Deferred from:** Phase 20F, 2026-07-02.

### TODO-20F-2 — Add a real-Postgres migration-application smoke test
**What:** Add a CI-run test (e.g. via Testcontainers) that applies every migration in `src/LinguaCoach.Persistence/Migrations` from an empty PostgreSQL database and asserts success, distinct from the existing SQLite `EnsureCreated()`-based integration tests (which never execute migration files at all).
**Why:** The Phase 20F root cause (6 migrations silently invisible to EF Core) went undetected by the full existing test suite because integration tests bypass real migrations entirely. The new `MigrationDiscoveryTests` (Phase 20F) catches the "missing Designer.cs" class of bug via reflection, but does not verify the migration SQL actually executes cleanly against real Postgres, including the three duplicate-table collisions this phase also found and fixed.
**Context:** `tests/LinguaCoach.ArchitectureTests/MigrationDiscoveryTests.cs`, `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.
**Deferred from:** Phase 20F engineering review, 2026-07-02.

### TODO-20F-3 — Retro: why did 6 migrations ship without Designer.cs and 3 pairs duplicate the same table?
**What:** Understand and prevent the process gap that allowed six migration files to be committed without their required Designer.cs, and three separate migrations to independently reimplement the same table under different names, without any of it being caught for what looks like several days to weeks of wall-clock phase history.
**Why:** This phase fixed the symptom safely and idempotently; it did not change how migrations get authored/reviewed. `docs/roadmap/road-map.md` §20 states "Migrations are hand-authored, named T1–T66 in sequence" — that invariant had already broken down before this incident.
**Deferred from:** Phase 20F, 2026-07-02 (explicitly out of scope for a P0 unblocker phase).

---

## Live Student Pilot Golden Path Completion (Phase 20G)

### TODO-20G-1 — Practice Gym "Suggested for you" shows 6 identical duplicate cards
**Status: RESOLVED, confirmed live 2026-07-03.** Root cause: `ReadinessPoolReplenishmentService.FillShortfallAsync`'s duplicate-prevention key included `PatternKey`, but `PatternKey` is only assigned during materialization (well after an item is queued) — so the queue-time key was always `(objective, null, cefr)` and could never match a materialized item's `(objective, "real pattern", cefr)`, letting replenishment re-queue duplicates for the same objective/level forever. Fixed by dropping `PatternKey` from the dedup key. Added defense-in-depth dedupe in `PracticeGymSuggestionService`. **Live validation found a residual gap:** pre-existing duplicate rows queued before the fix each had a distinct materialized `LearningActivityId`, which the initial same-activity/same-item dedupe didn't catch. Fixed same-day in a follow-up commit (`80cb0eb`) that reprioritizes the dedupe key to group by `(CurriculumObjectiveKey, PatternKey, ActivityType)` first. Reconfirmed live: the pilot student's Practice Gym now shows 6 genuinely distinct patterns/activity types for that objective, zero literal duplicate rows. See `TODO-20H-1` below for a related, separately-scoped observation (Suggested list doesn't diversify across objectives).
**What (original):** The pilot student's Practice Gym "Suggested for you" section shows the same suggestion ("Giving Structured Explanations," speaking, B2) 6 times as separate cards. Confirmed via raw API inspection (`GET /api/practice-gym/suggestions`) that these are 6 genuinely distinct `StudentActivityReadinessItem` rows (distinct `readinessItemId`/`linkedLearningActivityId`), all generated for the same curriculum objective (`b2.speaking.structured_explanations`) with no diversification across the plan's other 4 objectives.
**Why:** Confusing/broken-looking student experience even though each card is individually functional — reads as a rendering bug but is real backend readiness-pool data.
**Context doc:** `docs/reviews/2026-07-02-phase-20g-live-student-pilot-golden-path-review.md`, `docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`.
**Deferred from:** Phase 20G, 2026-07-02. **Fixed:** Phase 20H, 2026-07-03.

### TODO-20G-2 — Progress page "Recent activity" timeline didn't show a just-completed activity
**What:** After completing a vocabulary gap-fill activity live, the Progress page's Recent Activity timeline still only showed "Placement assessment completed" — the activity completion event wasn't visible.
**Why:** Minor discoverability gap; not investigated (time-boxed out of this phase). Could be a query window, event-type filter, or timing/caching issue in `StudentProgressSummaryHandler.BuildRecentActivityAsync`.
**Context:** `docs/reviews/2026-07-02-phase-20g-live-student-pilot-golden-path-review.md`.
**Deferred from:** Phase 20G, 2026-07-02.

### TODO-20G-3 — P0: readiness audit 500s again for the pilot student specifically (URGENT, needs prod DB/log access)
**Status: RESOLVED, confirmed live 2026-07-03.** 4 of the audit's 10 check-category methods (`AddPracticeGymChecksAsync`, `AddActivityContentChecksAsync`, `AddAudioTtsChecksAsync`, `AddFeedbackAndReviewScaffoldChecksAsync`) had no try/catch at all, so any unexpected data shape in those queries propagated straight into a 500. All four now match the existing pattern from `AddLearningPlanChecksAsync`/`AddProgressChecksAsync` — failures become a structured `Warning` check, never a raw exception. Live-checked against `https://speakpath.app` for `pilot.student.20e@speakpath.app`: **200**, `readyForPilot: true`, and the response includes an `activities.check_failed` check with `technicalDetail: "PostgresException"` — direct live confirmation that the exact originally-hypothesized failure (an unguarded `PostgresException` in the activity-content-validity category) was occurring, and now degrades safely instead of 500ing.
**What (original):** After the pilot student completed placement and one activity, `GET /api/admin/students/{id}/readiness` started returning 500 (`ExceptionType=PostgresException`) consistently (5/5 sequential and 5/5 parallel calls all failed). **Confirmed isolated to this one student** — the same endpoint returns 200 for a different, even-more-advanced student (`cfcca014-5950-4392-945b-dc668ceb72e1`). Confirmed the individual pieces work: `progress-summary`, `placement/latest`, `writing-evaluations`, `readiness-pool`, `readiness-pool/health` all independently return 200 for the pilot student — only the combined readiness audit fails.
**Why this is different from `TODO-20E-1`/Phase 20F:** that was a systemic migration-discovery bug affecting every student; this is isolated to one student's specific data combination (most likely correlated with the `TODO-20G-1` duplicate-suggestion data — 49 Practice Gym readiness items for one objective, an unusual `speaking` objective mapped to a `ListeningComprehension`-typed activity via pattern `listening_multiple_choice_single`).
**Context:** `docs/reviews/2026-07-02-phase-20g-live-student-pilot-golden-path-review.md`, `docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`.
**Deferred from:** Phase 20G, 2026-07-02, same escalation pattern as `TODO-20E-1` — production DB/log access was not available in this session. **Fixed:** Phase 20H, 2026-07-03.

---

## Live Pilot Stabilization (Phase 20H)

### TODO-20H-1 — Practice Gym Suggested list doesn't diversify across Learning Plan objectives
**What:** After fixing the literal duplicate-row bug (`TODO-20G-1`), live validation showed the pilot student's Suggested list still surfaces several cards for the same single objective (6 genuinely distinct patterns/activity types, e.g. listening comprehension, writing scenario, vocabulary practice — all for "Giving Structured Explanations") rather than spreading across the Learning Plan's other 3 objectives.
**Why:** Not a data-duplicate bug (confirmed: no two cards share the same pattern/activity type/materialized activity) — `PracticeGymSuggestionService.RankSuggestions` simply doesn't have an objective-diversity term in its ranking, so if one objective currently has the most/best-scoring ready items, it can dominate all `MaxSuggested` (6) slots.
**Context:** `docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`.
**Deferred from:** Phase 20H, 2026-07-03 (explicitly out of scope for a stabilization-only phase — requires a ranking/selection design decision, not a bug fix).
**Status as of Phase 20I:** Not re-evaluated — still valid, not re-confirmed against `pilot.student.20e`'s live Practice Gym UI this pass. See `docs/reviews/2026-07-03-phase-20i-full-live-student-admin-qa-data-audit-review.md`.

---

## Full Live Student/Admin QA (Phase 20I)

### TODO-20I-1 — `language_pairs` table has only one seeded row (Persian↔English)
**Status: Resolved, 2026-07-03.** Onboarding was cut over to the V2 flow, whose `support_language` step already offers 8 languages (not tied to `language_pairs`) and writes directly to `SupportLanguageCode`/`SupportLanguageName` — the fields the resolvers actually read. `language_pairs`'s single row no longer gates the student's native-language choice. See `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md`.
**Original what/why:** Onboarding step 1 ("Choose your language path") only ever offered "Persian to English" because that's the only row in `language_pairs`.
**Context:** `docs/reviews/2026-07-03-phase-20i-full-live-student-admin-qa-data-audit-review.md`, Part B/C.
**Deferred from:** Phase 20I, 2026-07-03 (product decision needed, not a code bug).

### TODO-20I-2 — Practice Gym readiness-pool queue backlog (1614 queued items) for `pilot.student.20e`
**What:** Admin readiness audit for `pilot.student.20e` shows Practice Gym pool health: 177 ready (target 10), **1614 queued**, 35 failed, 7 expired. The queued count is disproportionate for a single student and wasn't root-caused.
**Why:** Could indicate a runaway enqueue loop in the readiness replenishment job, or could be expected steady-state behavior — not distinguished this pass.
**Context:** `docs/reviews/2026-07-03-phase-20i-full-live-student-admin-qa-data-audit-review.md`, Part J.
**Deferred from:** Phase 20I, 2026-07-03 (needs a DB/job-log investigation before scaling pilot beyond one student).

### TODO-20I-3 — `GET /api/api/admin/generation-quality/summary` 404s (double `/api/api/` prefix)
**What:** Admin Diagnostics page shows a recurring 404 for a URL with a doubled `/api/api/` prefix, and the AI Operations "Generation quality — last 30 days" widget shows "Could not load generation quality data."
**Why:** Likely a stray leading slash in a frontend URL constant. Low severity (informational widget only), not fixed this pass.
**Context:** `docs/reviews/2026-07-03-phase-20i-full-live-student-admin-qa-data-audit-review.md`, Part A / Deferred section.
**Deferred from:** Phase 20I, 2026-07-03.

### TODO-20I-4 — RESOLVED, 2026-07-03: Admin-configurable placement item bank shipped
**What:** `PlacementAssessmentService`'s static 72-item `ItemBank` array is replaced by a DB-backed `PlacementItemDefinition` entity (`Skill`, `CefrLevel`, `ItemType`, `Prompt`, `CorrectAnswer`, plus new `ReadingPassage`/`ListeningAudioScript` fields), administered through a new admin page at `/admin/placement-items` mirroring the onboarding-step CRUD pattern exactly (`AdminPlacementItemController` → `IAdminAdd/Update/Remove/ListPlacementItemHandler` → `PlacementItemDefinitionConfiguration`). `PlacementItemBankSeeder` backfills the original 72 items verbatim on startup, idempotent and admin-edit-safe (keyed on `Prompt`, which is also a unique DB index — the same string the adaptive selection logic already used as de-facto item identity for its "used prompts" dedup). `PlacementAssessmentService` now loads the enabled item bank from the DB once per outer call instead of referencing a static field; the adaptive selection algorithm itself is unchanged.
**Why:** Product owner explicitly asked for placement to be as flexible/admin-editable as onboarding, and for the "reading is one sentence, listening is plain text" content-quality gap to be closeable without a code deploy.
**Context:** `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md`.
**Shipped in:** commit `c8a8c6c`, 2026-07-03. Full backend suite (3170 tests) + new Karma specs (26 tests) green before push. `ReadingPassage`/`ListeningAudioScript` fields exist but nothing yet reads them at assessment time — that's TODO-20I-5.

### TODO-20I-5 — RESOLVED, 2026-07-03: real TTS audio + reading passages wired into live placement
**What:** `PlacementAssessmentItem` now carries `ReadingPassage`/`ListeningAudioScript` (copied from `PlacementItemDefinition` at issuance) and `AudioStorageKey`/`AudioContentType` (populated lazily on first request via the new `AdaptivePlacementAudioService`, which uses `IFileStorageService` — Minio-backed in prod, unlike the legacy `PlacementAudioService`'s hand-rolled local-filesystem storage that never worked in production). New endpoint `GET /api/student/placement/audio/{assessmentId}/items/{itemId}/listening` on `StudentPlacementController` (the live adaptive controller). `PlacementNextItemDto` gained `ReadingPassage`/`HasAudio`; the frontend `PlacementComponent` now renders an `<audio>` player for listening items and a passage box for reading items. `PlacementItemBankSeeder` derives each listening item's audio script from its prompt's quoted "You hear: '...'" text (substituting gap-fill blanks with the correct answer), and retroactively backfilled the script onto the 72 rows the Phase 20I-4 deploy had already seeded without it.
**Why:** Confirmed the audio infrastructure wasn't missing, just unwired to the currently-live engine — closes finding (6) from the onboarding-cutover review (listening items were text pretending to be audio).
**Context:** `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md`.
**Shipped in:** commit `6774c10`, 2026-07-03. Full backend suite (3180 tests) + Karma specs (49 across the two touched components) green before push. Remaining gap: reading passages are only as rich as whatever an admin authors into `/admin/placement-items` — the seeded default items still have `ReadingPassage = null` (deriving a "passage" from the existing single-sentence prompts wouldn't have improved content depth), so real multi-sentence passages need genuine content authoring, not further wiring.

### TODO-20I-6 — RESOLVED, 2026-07-03: TTS usage was invisible, not broken
**What:** Original claim ("MinIO has no audio files", TTS never invoked) was based on stale/incomplete evidence. Live reproduction proved TTS generation genuinely works — a real ~970KB Gemini-generated WAV file was confirmed playable, with correct duration/voice metadata, generated the same day. The actual bug: `ListeningAudioService.EnsureAudioAsync` and `PlacementAudioService.EnsureListeningAudioAsync` both call `ITextToSpeechService.GenerateSpeechAsync` directly, completely bypassing `AiExecutionService`'s shared usage-logging wrapper — so `ai_usage_logs` never got a row for `tts.listening`/`tts.placement` even though the calls succeeded. Fixed by logging usage directly in both services, mirroring `AiExecutionService.LogUsageAsync`'s `AiUsageLog` shape (0 tokens/cost since TTS isn't token-priced in this codebase).
**Why:** Cost/usage tracking for TTS was silently invisible to AI Operations, even though audio generation itself was fine. No user-facing behavior changed.
**Context:** `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md`.
**Fixed in:** commit `94dfd96`, 2026-07-03. Full backend suite (3161 tests) green before push.

### TODO-20I-7 — Onboarding V2 admin builder has no OptionsJson editor (known limitation of the seeder reconciliation fix)
**What:** `/admin/onboarding`'s "Add step"/"Edit step" forms have no field for a step's `OptionsJson` — an admin can create a new step's key/title/type/mapping/order, but not its selectable options. `OnboardingFlowSeeder`'s reconciliation logic (added this phase to fix two seeded option-key bugs) compares each step's `(key, OptionsJson)` to decide whether to publish a new flow version — if the admin UI ever gains OptionsJson editing, that comparison would need to skip admin-customized steps, or every deploy would silently overwrite an admin's option edits with the code-seeded defaults.
**Why:** Not urgent today (the gap already existed; this phase's fix just made the seeder's behavior around it more precise), but worth closing before relying on admins customizing `Default Flow` step options.
**Context:** `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md`.
**Deferred from:** Phase 20I, 2026-07-03.

### TODO-20I-1 through -6 status update, 2026-07-03: onboarding V2 cutover fully verified live
**What:** After the initial cutover (commit `4972548`), live end-to-end verification found and fixed 4 additional real bugs (seeder never reconciling against the already-seeded production flow; two onboarding step components leaking answers into the next step of the same type; `CompletedStepKeys` never persisting due to a missing EF `ValueComparer` — making V2 onboarding completely uncompletable for every student since it was built; the summary step never recording its own completion; and two enum-string mismatches plus an unconditional-overwrite bug in `UpdateLearningPreferences` silently nulling out `support_language_code`/`difficulty_preference`). All fixed, deployed, and reconfirmed via a full live run: a fresh student's `StudentProfile` now correctly shows every onboarding field populated (support language, translation preference, career context, learning goals, focus areas, difficulty, session duration, work experience) and lands cleanly on the dashboard.
**Context:** `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md` has the full bug-by-bug writeup.

### TODO-D4-1 — RESOLVED (Phase E9, 2026-07-09): lean bank tables now carry published selection metadata
**What (original):** Phase D4's "general English by default, workplace only when routed" context filter in `TodayBankResourceSelector` could only be applied to full `CefrReadingPassage` resources, because that was the only published bank entity storing context tags. `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` had no context/focus/subskill/difficulty columns on the final tables (the metadata existed on the staging `ResourceCandidate` but was dropped at publish), so supporting vocabulary/grammar/short-reference selection could not be context-filtered.
**Resolution (Phase E9):** added `subskill`/`difficulty_band`/`context_tags_json`/`focus_tags_json` (nullable) to all three lean tables (migration `Phase_E9_AddLeanBankSelectionMetadata`; tag columns text, aligned in shape with `CefrReadingPassage`, filterable via the portable `.Contains("\"tag\"")` LIKE pattern `CurriculumObjective` uses). `ResourceCandidatePublishService` now maps candidate context/focus/subskill/difficulty onto the lean rows at publish; an idempotent `PublishedBankMetadataBackfillSeeder` repairs pre-E9 rows only where traceable to exactly one published candidate; `ResourceBankQueryService` + the three admin list endpoints expose the metadata read-only and support optional context/focus/subskill/difficulty filters. All bank types are now filterable from the published rows.
**Residual (narrowed, tracked below as TODO-E9-1):** the *selector* does not yet consume the new lean-table filtering — wiring `TodayBankResourceSelector` to filter supporting vocabulary/grammar/references by context/focus/subskill/difficulty is Phase D5. Also, E6/E7/E8-authored lean rows carry only the metadata their authors supplied (e.g. context tags + subskill but no difficulty band), which is faithful provenance, not a gap.
**Context:** `docs/architecture/english-resource-bank-import-platform.md` (E9 detail section); `ResourceCandidatePublishService`; `PublishedBankMetadataBackfillSeeder`.
**Resolved in:** Phase E9, 2026-07-09.

### TODO-E9-1 — RESOLVED (Phase D5, 2026-07-09): Today selector now consumes the lean-table metadata filters
**What (original):** Phase E9 made `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` filterable by context/focus/subskill/difficulty on the published rows, but `TodayBankResourceSelector` still only applied the general-English/workplace context filter to full passages (its D4 behavior).
**Resolution (Phase D5):** the three lean per-type selectors were unified into a shared `SelectLeanAsync` that applies the E9 `ContextTag`/`FocusTag`/`Subskill`/`DifficultyBand` filters through a deterministic strict→loose relaxation ladder (drop difficulty → focus → subskill → context → general), each combined with exact-CEFR-first / review-only-widen-down. The general-English/workplace exclusion now applies to vocabulary/grammar/reading-reference rows as well as passages; `PreferredFocusTags` is fed from `ResolvedLearningGoalContext.FocusAreaKeys`; provenance records `appliedFilters`/`matchedContextTags`. Topic matching is deterministic metadata matching only (no embeddings/vector search). See `docs/architecture/learning-activity-engine.md` (Phase D5 section).
**Residual (tracked as TODO-D5-1):** the internal lean packs carry no difficulty band and focus tags only on some rows, so D5's difficulty/focus filtering is opportunistic on those types.
**Resolved in:** Phase D5, 2026-07-09.

### TODO-D5-1 — RESOLVED (Phase E10, 2026-07-09): internal lean packs now carry difficulty + focus metadata
**What:** Phase D5's selector supports context/focus/subskill/difficulty filtering on all bank types, but the internal E6/E7/E8 lean packs (`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`) were authored with context tags + subskill only — no difficulty band, and focus tags on just some vocabulary rows. So a difficulty-band or focus-tag preference on those types usually relaxes away (which the ladder handles gracefully); only full passages carry difficulty/focus densely. Context and subskill filtering is dense across all types.
**Why:** Closing this is a **content** task (author difficulty/focus metadata into the lean packs through the existing staging → validation → approval → publish pipeline), not a schema or selector change. Deferred because D5 was a selector-quality phase, and the selector already relaxes safely when the metadata is thin. `ActivityMaterializationJob` also currently null-feeds `PreferredSubskill`/`PreferredDifficultyBand` (no reliable per-request source yet).
**Context:** `docs/architecture/english-resource-bank-import-platform.md` (E9 residual + D5 note); `TodayBankResourceSelector`; `InternalResourceSeedPackE8Seeder`.
**Deferred from:** Phase D5, 2026-07-09.
**Status update (Plan-Sync-After-D5, 2026-07-09):** promoted to a dedicated implementation phase — **Phase E10 — Internal Bank Metadata Depth Expansion for Focus and Difficulty**, sequenced next (before Phase D6 and PG-v2). See `docs/roadmap/road-map.md` §1, §19 Decision Log (Plan-Sync-After-D5), and §19a item 20e.
**Resolution (Phase E10, 2026-07-09):** `InternalBankMetadataDepthSeeder` (idempotent startup step after the E9 backfill) derives a difficulty band from CEFR (A1→1…B2→4, C1/C2→5) and a focus tag from each row's subskill (e.g. `vocabulary.collocation` → `["collocation"]`) onto every internal lean row (`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`). It touches only `Internal/Original` rows traceable to exactly one published candidate, fills only empty fields (never overwrites authored values), preserves subskill + context, never inserts a row, and is a no-op on rerun. All internal lean rows now carry context + subskill + difficulty + focus, filterable via the existing E9 query/admin filters. See `docs/architecture/english-resource-bank-import-platform.md` (E10 detail section).
**Resolved in:** Phase E10, 2026-07-09.

### TODO-E10-1 — Runtime job null-feeds subskill/difficulty selection preferences (Phase D6)
**What:** After E10 the metadata *exists* on every internal lean row (difficulty band + focus tag + subskill + context), and the D5 selector supports `PreferredSubskill`/`PreferredDifficultyBand` filters. But `ActivityMaterializationJob` still passes `null` for `PreferredSubskill`/`PreferredDifficultyBand` because there is no reliable per-request source yet (only `PreferredFocusTags` is fed, from `ResolvedLearningGoalContext.FocusAreaKeys`, and `PrefersWorkplaceContext`). So subskill/difficulty filtering only activates when a preference is explicitly supplied (e.g. in tests), not from live routing.
**Why:** Closing this is a **selector/routing** task (derive a per-request subskill and/or difficulty-band preference from routing reason, pattern, mastery/weakness signals, or difficulty preference, then feed it into the request) — it belongs to **Phase D6 — Today Topic Matching and Subskill-Aware Resource Selection**, not to E10 (which was a data-depth phase). The data substrate is now in place.
**Context:** `docs/architecture/learning-activity-engine.md` (Phase D5/E10 notes); `TodayBankResourceSelector`; `ActivityMaterializationJob.ParseFocusTags`.
**Deferred from:** Phase E10, 2026-07-09.
**Resolution (Phase D6, 2026-07-09):** closed. `CurriculumRoutingRecommendation` now surfaces the matched objective's `Subskill`, and `ActivityMaterializationJob` feeds reliable per-request signals into `TodayBankSelectionRequest`: `PreferredSubskill = routing.Subskill`; `PreferredFocusTags` prefers `routing.FocusTags` (falling back to the learner's focus areas); `PreferredDifficultyBand` is derived conservatively from `StudentProfile.DifficultyPreference` relative to the routed CEFR's normal band via the shared `CefrDifficultyBand` helper (Gentle → one band lower, Balanced → CEFR-normal band, Challenging → one band higher, unknown/unmappable → null). Subskill/difficulty filtering now activates from live routing, not only in tests. **Residual limitation:** E10's derived difficulty bands are CEFR-uniform (all B1 internal rows = band 3), so difficulty narrowing is effectively a no-op for Balanced and a relaxation for Gentle/Challenging on today's internal data; the mechanism is correct and covered by mixed-band tests, and becomes materially selective once genuinely mixed-difficulty content lands. See `docs/architecture/learning-activity-engine.md` (Phase D6 notes).
**Resolved in:** Phase D6, 2026-07-09.

### TODO-H1-1 — Unified Resource Bank pagination happens in application memory, not the database
**What:** `ResourceBankQueryService.ListUnifiedAsync` (Phase H1) queries each of the four typed bank tables with its filters applied at the DB level, but then merges/sorts/paginates the combined result **in application memory**, not via a real cross-table SQL query. At current content volume (dozens of rows per type, internal seed packs only) this is simple, correct, and safe.
**Why:** A genuinely large multi-table paged query needs a real unified projection — either a DB view over the four typed tables, or physical consolidation into one table (Option A from `docs/architecture/product-model-realignment-h0.md` §4, explicitly deferred in H0/H1). Building that now would be solving a scale problem this content doesn't have yet, and Option A's right physical shape is better decided once Learn Item/Activity/Module (H3-H5) exist and it's clear what they actually need to query.
**Context:** `ResourceBankQueryService.ListUnifiedAsync` in `src/LinguaCoach.Infrastructure/ResourceImport/ResourceBankQueryService.cs`; `docs/architecture/product-model-realignment-h0.md` §4 (Option A vs Option B).
**Deferred from:** Phase H1, 2026-07-09.

### TODO-H2-1 — Import Content UX v1 has no AI structure analysis and no Listening/Speaking/Writing/Mixed type
**What:** `POST /api/admin/content-imports` (Phase H2) only supports deterministic mapping — a row's own columns or the admin's explicit defaults, nothing guessed — and only three resource types (`vocabulary`/`grammar`/`reading`). Listening/Speaking/Writing and "Mixed / AI detect" are shown as "coming soon" in the Import Content UI and rejected server-side, because `ResourceCandidateType` has no shape for them yet.
**Why:** `docs/architecture/product-model-realignment-h0.md` §3's intended import flow describes AI analyzing input structure and detecting CEFR/mapping columns automatically; that is explicitly out of scope for H2 v1 per the phase brief ("no AI-heavy automatic structuring... expose a clear 'AI structure analysis pending/future' placeholder"). Adding Listening/Speaking/Writing/Mixed candidate types is a larger `ResourceCandidateType`/downstream-service ripple (analysis/validation/publish services all switch on this enum) that deserves its own scoped phase, not a silent addition inside an import-UX phase.
**Context:** `AdminContentImportController.SupportedResourceTypes`/`SupportedInputModes`; `ContentImportContracts.cs`; `admin-resource-import.models.ts` (`CONTENT_IMPORT_RESOURCE_TYPES`/`CONTENT_IMPORT_COMING_SOON_TYPES`).
**Deferred from:** Phase H2, 2026-07-09.

### TODO-H3-1 — Generate Learn is deterministic-only and single-resource-per-call
**What:** `LearnItemGenerationService` (Phase H3) composes a Learn Item draft directly from the selected resources' own fields — no AI provider call, so the body/examples/common-mistakes are only as good as the source resource's own text (a bare vocabulary word with no `Notes` produces a thin draft). The H1 unified Resource Bank page's "Generate Learn" row action also only supports one resource per call (`role: Primary`) — there is no multi-select UI yet to combine several resources (e.g. one vocabulary word + one grammar point) into a single richer Learn Item in one step; an admin can still do this via `POST /api/admin/learn-items/generate-from-resources` directly with multiple `resources` entries, or via manual create.
**Why:** Both are explicit H3 foundation-phase scope decisions, not gaps: real AI-generated teaching prose needs a new AI feature key (`docs/architecture/product-model-realignment-h0.md` explains why that's deferred), and multi-select-from-a-table is a UI investment better justified once the deterministic single-resource path is proven in daily admin use.
**Context:** `LearnItemGenerationService` in `src/LinguaCoach.Infrastructure/LearnItems/`; `AdminResourceBankUnifiedComponent.generateLearn` in `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-unified/`.
**Deferred from:** Phase H3, 2026-07-09.

### TODO-H4-1 — Generate Activity supports only 3 deterministic activity types, single-resource-per-call, no runtime wiring
**What:** `ActivityGenerationService` (Phase H4) only produces `gap_fill`/`multiple_choice_single` (Vocabulary/Grammar) and `short_answer` (ReadingReference/ReadingPassage) — no listening/speaking/writing activity types, no AI-generated exercises. The Resource Bank page's "Generate Activity" row action supports one resource per call (`role: Primary`), same limitation as H3's Generate Learn (multi-resource generation is available via the API directly, not the row action). `ActivityDefinition` is also **not wired into any runtime path** — Today materialization, Practice Gym generation, and `ActivityTemplate`'s own live Form.io pilot are completely unaware of it; an approved `ActivityDefinition` cannot yet be delivered to a student. `short_answer` activities are permanently ungraded by design (`RequiresManualOrAiEvaluation=true`) since H4 has no manual-grading queue or AI-evaluation wiring for this new entity.
**Why:** All explicit H4 foundation-phase scope decisions: real AI-generated exercises and additional activity types need dedicated scoped work; runtime delivery is H6/H7's job (Daily Lesson/Practice Gym module pipelines), not this entity/API/admin-review foundation phase; a manual-grading or AI-evaluation queue for open-ended Activities is a separate, larger feature.
**Context:** `ActivityGenerationService` in `src/LinguaCoach.Infrastructure/ActivityDefinitions/`; `AdminResourceBankUnifiedComponent.generateActivity` and `AdminLearnItemsComponent.generateActivity` in `src/LinguaCoach.Web/src/app/features/admin/`.
**Deferred from:** Phase H4, 2026-07-09.

### TODO-H5-1 — Generate Module requires typing ids in the UI, no multi-item selection, no runtime wiring
**What:** `ModuleGenerationService` (Phase H5) requires every Learn Item/Activity Definition it composes a Module from to already be `Approved` — a deliberate quality gate, but it means an admin who only has draft content must approve items first, in a separate page, before Generate Module will succeed anywhere. The Modules page's own "Generate Module" modal is a simple two-text-field form (admin types a Learn Item id and an Activity Definition id copied from the Learn Items/Activities pages) — there is no picker/search UI, and it only supports exactly one Learn Item + one Activity per call (the richer `generate-from-items` API endpoint accepts multiple of each, but no UI drives that yet). Compatibility matching for the from-Learn-Item/from-Activity entry points is deliberately simple (CEFR level + skill string equality, capped at 5 matches) — it does not consider context/focus tag overlap or linked-source-resource overlap, both of which the phase brief listed as compatibility signals worth considering later. `ModuleDefinition` is also **not wired into any runtime path** — H6 (Daily Lesson) and H7 (Practice Gym) are the planned future consumers, not built yet.
**Why:** All explicit H5 foundation-phase scope decisions: requiring `Approved` sources keeps a Module's content-studio quality bar consistent with the H3/H4 review pattern rather than looser; a full drag/drop module builder or searchable item picker is real UI investment better justified once the simple id-based flow is proven in daily admin use; richer compatibility scoring (tag overlap, resource overlap) is a refinement that can be added without a schema change once there's real usage data to tune it against; runtime delivery is explicitly H6/H7's job, not this phase's.
**Context:** `ModuleGenerationService` in `src/LinguaCoach.Infrastructure/ModuleDefinitions/`; `AdminModulesComponent.submitGenerateFromItems` in `src/LinguaCoach.Web/src/app/features/admin/admin-modules/`.
**Deferred from:** Phase H5, 2026-07-09.

### TODO-H6-1 — Daily Lesson module selection does not yet use real learning-plan/weak-skill signals
**What:** `DailyLessonModuleSelectionRequest` accepts `RequestedSkill`/`FocusAreas`/`ContextTags` (soft-preference signals meant to represent a student's learning plan objectives and weak skills), but the only call site, `SessionQueryHandler.HandleAsync(GetTodaysSessionQuery)`, currently populates only `CefrLevel` from `StudentProfile.CefrLevel` and leaves the rest null — so today the selector only ever gates on CEFR level plus the module's own metadata, with no actual learning-plan awareness. Full wiring would mean loading the student's active `StudentLearningPlan`'s highest-priority non-completed `StudentLearningPlanObjective` and mapping its `Skill`/`Context` into the request.
**Why:** Explicit H6 scope-reduction decision (see `docs/reviews/2026-07-09-phase-h6-daily-lesson-module-pipeline-review.md`, Decisions §4): fully wiring `ICurriculumRoutingService.RecommendAsync` requires a `CurriculumRoutingRequestFactory`-built request that pulls in mastery/readiness-pool state not otherwise needed for a first, additive Today integration; a simpler direct-from-`StudentLearningPlan` read is enough to close this TODO without the full routing-service dependency, once there's time to add and test it.
**Context:** `SessionQueryHandler.HandleAsync(GetTodaysSessionQuery)` in `src/LinguaCoach.Infrastructure/Sessions/SessionQueryHandler.cs`; `DailyLessonModuleSelectionRequest` in `src/LinguaCoach.Application/DailyLessonModules/DailyLessonModuleSelectionContracts.cs`.
**Deferred from:** Phase H6, 2026-07-09.
