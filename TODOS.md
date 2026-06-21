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
- TODO-10W-4c: Notification dropdown component customization is local-only — `src/app/templates/` is gitignored in LinguaCoach.Web. If repo is cloned fresh, `NotificationDropdownComponent` reverts to TailAdmin demo data. Document onboarding step to re-apply 10W-3 changes, or move the component outside the gitignored vendor folder.
- TODO-10W-5: Templates and preferences — `NotificationTemplate` entity + migration. `INotificationTemplateRenderer` (Liquid or Handlebars). Admin CRUD for templates. `NotificationPreference` per-user per-channel. Opt-out respected in dispatch worker.
- TODO-10W-6: SMS provider — `ISmsProvider` interface. `TwilioSmsProvider` implementation. Config: `Sms__Provider`, `Sms__Twilio__*`. Wire one SMS producer: quota warning. Rate limiting per user per day. Opt-in required (STOP compliance).
- TODO-10W-FINAL: Notification platform audit — delivery audit log review, security/PII audit, unsubscribe compliance, load test dispatch worker.
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
