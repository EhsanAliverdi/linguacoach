---
status: current
lastUpdated: 2026-06-27 (12F)
owner: product
supersedes:
supersededBy:
---

# SpeakPath — Current Product State

Last updated: 2026-06-27 (12F)

---

## Learning Plan Completion Lifecycle (Phase 12F, 2026-06-27)

Closes the lifecycle loop: Learning Plan objectives now transition deterministically
through `Active → InProgress → Completed → Mastered` driven by existing mastery evidence.

**Completion signals:** `StudentMasteryReport` gains `CompletedObjectiveKeys` — objectives
where mastery signal is `NeedsReview` (some consecutive successes, avg score 50-79). Full
mastery continues to populate `MasteredObjectiveKeys`.

**Completion service:** `ILearningPlanService` gains `MarkObjectiveCompletedAsync` and
`MarkObjectiveMasteredAsync`. Both are idempotent. Implemented in `LearningPlanService`
with shared `TransitionObjectiveAsync` helper. Logs a warning when all objectives are exhausted.

**Mastery job integration:** `StudentMasteryEvaluationJob` now calls both new methods for
each mastered/completed key before triggering plan regeneration. All calls are warning-only;
generation continues regardless of plan update failure.

**Progress metrics:** `LearningPlanProgressSummary` expanded with `TotalObjectives`,
`ObjectivesMastered`, `ObjectivesInProgress`, `DeferredObjectives`, `CompletionPercentage`,
and `LastCompletedAt`. `MasteryPercentage` now reflects Mastered/Total only.

**No student UI changes. No new migrations. No new API endpoints.**

16 new completion lifecycle unit tests. All 2618 tests pass (3 arch + 1460 unit + 1155 integration).

Review: `docs/reviews/2026-06-27-phase-12f-learning-plan-completion-lifecycle-review.md`.

---

## Learning Plan Guided Routing (Phase 12E, 2026-06-27)

Closes the Phase 12D gap: `PreferredObjectiveKey` is now consumed by `CurriculumRoutingService`.

**Routing change:** When a generation job passes a planned objective key, routing validates it against five safety rules (CEFR exact or one-level-lower-with-scaffold, skill match, runnable, mastery exclusion) and selects it first if all rules pass (`RoutingReason.LearningPlan`). Rejection always falls back to the existing routing pipeline — no generation failure, no silent CEFR downgrade.

**Status lifecycle:** `LearningPlanObjectiveStatus.InProgress` added. When routing returns `LearningPlan`, both `LessonBatchGenerationJob` and `PracticeGymGenerationJob` call `MarkObjectiveInProgressAsync` to advance the plan objective status.

**Admin diagnostics:** `POST /api/admin/curriculum/routing-preview` now accepts an optional `preferredObjectiveKey` field and returns `preferredObjectiveDisposition` ("accepted" / "rejected" / "fallback_used") so admins can test learning-plan routing without running a real generation job.

**No student UI changes. No new migrations.**

15 new routing tests. All 2602 tests pass.

---

## Learning Plan Orchestrator Foundation (Phase 12D, 2026-06-27)

Deterministic per-student learning plan layer that coordinates curriculum routing, mastery evaluation, and readiness pool into a coherent objective sequence. No AI calls. No student UI changes. No ReviewScaffold global enable.

**New domain entities:** `StudentLearningPlan` and `StudentLearningPlanObjective` (tables `student_learning_plans`, `student_learning_plan_objectives`). Migration T61. A student has at most one Active or Regenerating plan at a time. Old plans are Superseded on regeneration.

**Plan generation:** Builds a 10-objective sequence (configurable) from a balanced skill rotation (speaking × 2, writing × 2, listening × 2, reading × 2, vocabulary × 2). Inserts review objectives from weak/mastered mastery keys. Prevents duplicate objective keys within the plan.

**Regeneration triggers:** Automatic plan regeneration fires after mastery sweep (when mastery changes), CEFR level admin change, and student preference update. All triggers are non-blocking (failure is logged as warning only).

**Admin visibility:** `GET /api/admin/students/{id}/learning-plan` and `.../learning-plan/progress` endpoints for admin inspection. Read-only, no side effects.

**Job integration:** `LessonBatchGenerationJob` and `PracticeGymGenerationJob` now consult the learning plan for a preferred objective key and pass it to curriculum routing as a hint. Free routing fallback when no plan exists.

**Config (`LearningPlan` appsettings section):** `PlannedLessonCount` (default 10), `MaxUpcomingObjectives` (default 5), `MaxPracticeGymObjectives` (default 5), `MasteryCompletionThreshold` (default 70%).

38 new tests. All 2587 tests pass. Review: `docs/reviews/2026-06-27-phase-12d-learning-plan-orchestrator-foundation-review.md`.

---

## Prepared Lesson Pipeline and Readiness Lifecycle (Phase 12C, 2026-06-27)

Configurable buffer bounds and per-run observability for the readiness pool replenishment engine.

**New config options (`ReadinessPool` appsettings section):**
- `MinimumReadyThreshold` (default 3) — admin alert threshold; students with fewer Ready items than this are counted in `StudentsBelowMinimumThreshold`.
- `MaxBufferCount` (default 20) — hard cap on active items (Queued + Generating + Ready + Reserved) per student per source. Prevents unbounded over-fill. Must be ≥ `TodayLessonPoolTargetCount`.

**Replenishment summary fields added:**
- `SkippedAtMaxBuffer` — items skipped because student was already at the buffer cap.
- `ElapsedMs` — computed from `CompletedAt - StartedAt`.
- `GenerationSuccessRate` — `ItemsQueued / (ItemsQueued + SkippedDuplicates + SkippedAtMaxBuffer)`.

**Aggregate pool health fields added:**
- `StudentsBelowMinimumThreshold` — students with Ready < `MinimumReadyThreshold` (including zero-ready students).
- `AverageReadyPerStudent` — system-wide average, displayed in admin Lessons pool health stat grid.

**No student UI changes. No ReviewScaffold global enable. No migration.**

17 new tests. All 3933 tests pass. Review: `docs/reviews/2026-06-27-phase-12c-prepared-lesson-pipeline-readiness-lifecycle-review.md`.

---

## Mastery Re-evaluation Engine (Phase 10Z, 2026-06-26)

Deterministic mastery classification engine layered on top of the student learning event ledger. Evaluates skill/objective mastery from `StudentLearningEvent` history without any AI calls.

**Mastery thresholds (configurable via `"Mastery"` appsettings section):**

| Rule | Default |
|------|---------|
| Evidence required for any classification | 3 events |
| Mastered: evidence count | ≥ 5 events |
| Mastered: consecutive successes | last 3 |
| Mastered: average score | ≥ 80 |
| AtRisk: consecutive failures | ≥ 2 |
| AtRisk: average score | < 30 |
| Stale item age threshold | 90 days |

**Readiness pool demotion decisions:**
- `Mastered` + review-eligible item → `ConvertToReviewOnly`
- `Mastered` + not useful for review → `Skip`
- CEFR mismatch > 1 level → `MarkStale`
- Item age > 90 days, never consumed → `Expire`
- `AtRisk` or `NeedsPractice` → `KeepReady`
- Terminal state (Consumed/Expired/Failed/Skipped) → `NoChange`

**Background job:** `StudentMasteryEvaluationJob` runs daily via Quartz, evaluates all students with learning events.

**No admin UI added. No student UI added. No migration needed.**

---

## Admin Visual Fixes — Bounded Tables (Phase 10UI-PARITY-REBUILD-2A, 2026-06-24)

Screenshot-driven visual pass against `e2e/screenshots/prod/`. Three admin tables
rendered all rows unbounded versus the paginated design, producing very long pages.
Added client-side pagination (reusing `SpAdminPaginationComponent`) to: AI Usage
"By feature" (8/page), AI Usage "Calls over time" (8/page), and Curriculum objectives
(12/page). Exercise Types and Diagnostics already matched the design. Diagnostics
Background Jobs section remains absent (needs backend endpoint, deferred to 2B). Build
green, 1361 frontend tests pass. No secrets, no fake data, no student-UI or backend
changes. Review:
`docs/reviews/2026-06-24-phase-10ui-parity-rebuild-2-screenshot-driven-admin-visual-fixes.md`

---

## Admin Design Route Map — VERIFIED (Phase 10UI-PARITY-REBUILD-1, 2026-06-24)

Verified Angular admin shell, sidebar nav, and routes against the new design source
`docs/design/speakpath/admin/`. All 15 design nav entries map to existing Angular
routes and components. Sidebar sections and labels match the design exactly
(desktop sidebar plus mobile drawer). Added `/admin/students/create` redirect alias
for the design-canonical create-student path. Full route map in
`docs/design/admin-reference-alignment.md`. No secrets rendered; charts use
"No data available" placeholders only.

---

## Admin Standalone Visual Parity — CLOSED (Phase 10UI-PARITY-FINAL, 2026-06-24)

All admin shared components and feature pages aligned to `docs/design/SpeakPath Admin (standalone) V1.html`.

**Commits:** `104624a` (1C-A), `c051eb8` (1C-B1), `5a0d921` (docs), `6e9196d` (shared DS), `2075134` (FINAL sweep)

### What was aligned

- **Design tokens** — `admin-tokens.css` matches standalone `:root` exactly (`--ink`, `--muted`, `--border`, `--border-2`, `--surface`, `--canvas`, shadow vars, font)
- **Shared components** — card, kpi-card, button, badge, table, pagination, input, toggle all at exact token parity
- **Student detail hero** — radius 14px, gap 18px, sh-xs shadow, `#211B36` name, `#8B85A0` email, no monospace
- **AI config native select** — 36px height, 1.5px `#E2DEF0` border, focus indigo
- **Usage policies rules expansion** — `#FBFAFE` bg, `#ECE9F5` border, `#8B85A0` muted
- **Notifications/Security tabs** — `#8B85A0` inactive, `#5B4BE8` active + underline
- **Color sweep (16 files)** — all Tailwind gray fallbacks replaced with standalone tokens (`#8B85A0`, `#211B36`, `#E2DEF0`, `#ECE9F5`, `#13B07C`, `#F6F4FB`)

### Accepted gaps (P3)

- Graph/chart areas show placeholder divs — no charting library added (policy)
- No live screenshot comparison (backend not running this session)

### Gates

- ✅ Production build clean
- ✅ 1361/1361 frontend tests passing
- ✅ No fake data, no secrets, no student UI changes, no backend changes

Full audit: `docs/reviews/2026-06-24-phase-10ui-parity-final-standalone-admin-screenshot-closure-audit.md`

---

## UI / Backend Reconciliation Audit — complete (Phase 10UI-AUDIT-0, 2026-06-23)

Audit-only phase. No code changed. All admin and student routes audited against backend capabilities.

### Top P0 gaps found (fix immediately)

| Gap | Location | Fix |
|---|---|---|
| `/admin/usage-policies` has NO nav link | Admin sidebar | Add nav item — single HTML change |
| `/admin/curriculum` has NO nav link | Admin sidebar | Add nav item — single HTML change |

### Top P1 gaps found

| Gap | Location | Fix phase |
|---|---|---|
| Readiness pool health not shown | /admin/students/:id | 10UI-FIX-2 |
| Student activity history not shown | /admin/students/:id | 10UI-FIX-2 |
| Onboarding flow viewer missing | No admin page | 10UI-FIX-2 |
| Orphan AdminUsageComponent (stale placeholder) | admin-usage folder | 10UI-FIX-4 |
| Dashboard "AI provider" stat card always static | /admin dashboard | 10UI-FIX-4 |

### Student UI status

Student-facing routes (/today, /journey, /practice, /progress, /profile) are broadly aligned with backend. No P0 gaps. Minor P2 gaps: CEFR not shown on /progress, no Google login button on /login.

Full report: docs/reviews/2026-06-23-phase-10ui-audit-0-ui-backend-capability-reconciliation.md
New TODOs: TODO-UI-01 through TODO-UI-10 in TODOS.md.

---

## Enterprise Auth/Security — FULLY CLOSED (Phase 10Auth-F-FINAL, 2026-06-23)

All 6 implementation phases complete and verified. 2369/2369 .NET + 1025/1025 Angular tests pass. Production-ready for current single-host SpeakPath stage.

Gap check: docs/reviews/2026-06-23-phase-10auth-f-0-enterprise-auth-security-gap-check.md
Closure audit: docs/reviews/2026-06-23-phase-10auth-f-final-enterprise-auth-security-closure-audit.md

### Auth capability summary

| Capability | Status | Detail |
|---|---|---|
| Password policy | ✅ | 10+ chars, upper+lower+digit+special |
| Account lockout | ✅ | 5 attempts, 15-min duration, generic errors |
| Rate limiting | ✅ | 5 policies: AuthLogin/Reset/ChangePassword/ExternalLogin/Refresh |
| Security headers | ✅ | X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy |
| Auth event audit log | ✅ | 23 event types, migration T58, admin endpoint |
| Audit metadata safety | ✅ | No passwords, tokens, secrets, or Google IDs in audit |
| Security notifications | ✅ | 5 notification groups (password change/reset/lockout/external link) |
| Refresh token sessions | ✅ | Hash-only, rotation, reuse detection, migration T59 |
| Session revocation | ✅ | Logout, revoke-all, password change/reset all revoke sessions |
| Google external login | ✅ | Disabled by default, testable abstraction, no migration |
| Force-password-change middleware | ✅ | HTTP 403 on all endpoints except change-password |
| Admin security settings page | ✅ | /admin/security — read-only overview + auth events tab |

### Auth endpoints

- `POST /api/auth/login` — email/password, issues JWT + refresh token
- `POST /api/auth/refresh` — rotate refresh token, issue new access token
- `POST /api/auth/logout` — revoke single refresh token
- `POST /api/auth/revoke-sessions` — revoke all sessions (authenticated)
- `POST /api/auth/change-password` — change password, revoke all sessions
- `POST /api/auth/reset-password` — public, token-validated, revokes sessions
- `POST /api/auth/external/google` — Google ID token login

### Deferred (documented, not blocking)

- CSP header — requires Angular build nonce strategy
- HSTS — requires production TLS confirmation
- Distributed rate limiting — before horizontal scaling
- Admin-initiated per-user session revocation UI
- SMS security notifications — requires SMS provider + phone verification
- Cloud KMS for Data Protection keys — before horizontal scaling
- CAPTCHA / bot protection
- MFA, enterprise SSO/SAML/OIDC — not in current product scope
- Formal deployment guide (`docs/deployment/`)

---

## Enterprise Notification Platform — FULLY CLOSED (Phase 10W-FINAL-2, 2026-06-23)

All notification sub-phases complete and verified. 2291 .NET / 1004 Angular tests pass. Platform is production-ready for in-app and email on single-host Docker.

### Channels delivered

- **In-App:** live bell dropdown, unread count, mark read/all, archive. User-isolated. Committed component.
- **Email:** SMTP provider, SmtpEmailSender (resolves config at send time via `INotificationChannelConfigResolver`), NotificationDispatchJob (Quartz, every 2 min, batchSize=50). SMTP credentials never returned to frontend. Secrets encrypted at rest with ASP.NET Core Data Protection; keys persisted to `dp_keys` Docker volume; keys optionally encrypted via X.509 certificate (`KeyProtectionMode=Certificate`).
- **SMS:** foundation only — `ISmsSender` / `DisabledSmsSender` / `SmsOptions`. No real provider. Phone number collection deferred.

### Admin notification center

- Notifications list (filter by channel/status/category/severity/search, pagination).
- Delivery queue (filter by channel/status/failed-only, retry/cancel actions).
- Configuration tab (InApp/Email/SMS/dispatch status, SMTP safe fields, SMS safe fields, test-email). DB-backed channel config with editable forms (Phase 10W-5C-2/5C-3). Secrets protected with ASP.NET Core Data Protection. `hasPassword`/`hasApiKey` booleans only in API responses.
- Send notification slide-over (InApp + Email channels, recipient lookup, title/body/category/severity/deep-link).
- Templates tab (CRUD, preview, 4 seeded defaults).

### Templates

4 seeded templates: `account.password_reset`/Email, `account.student_created`/Email, `admin.manual_notification`/InApp, `admin.manual_notification`/Email. Simple `{{VarName}}` substitution. Missing variables logged + left visible in output. Password reset and student-created emails use templates with hard-coded fallback.

### Preferences

`notification_preferences` table (migration T56). Per-user category×channel preferences. Account/System categories required (cannot be disabled). SMS always deferred (returns false). User API GET/PUT. Admin read API. Profile section with required/coming-soon indicators.

### Security invariants

- Password reset token: never logged, never stored in metadata, never returned to admin, generic error on failure.
- SMTP password: `HasPassword` bool only in admin config DTO.
- SMS ApiKey: `HasApiKey` bool only in admin config DTO.
- User isolation: all notification queries filter `RecipientUserId == userId`.
- Email not sent inline during requests — always queued to outbox.

### Deferred

- `TODO-10W-5D-UNIQUE-CONSTRAINT`: DB unique index on `(template_key, channel)` for active templates.
- `TODO-10W-PHONE`: phone number collection and verification.
- `TODO-10W-SMS-PROVIDER`: real Twilio/other SMS sender (requires TODO-10W-PHONE).
- `TODO-10W-DP-CLOUD-KMS`: multi-instance production deployments need `PersistKeysToDbContext` or a cloud KMS (Azure KV / AWS Secrets Manager). Deferred until horizontal scaling is needed.
- `TODO-10W-5D-UNIQUE-CONSTRAINT`: DB unique index on `(template_key, channel)` for active templates.
- `TODO-10W-PHONE`: phone number collection and verification.
- `TODO-10W-SMS-PROVIDER`: real Twilio/other SMS sender (requires TODO-10W-PHONE).

Closure audit: `docs/reviews/2026-06-22-phase-10w-final-notification-platform-closure-audit.md`
DB config + secret encryption review: `docs/reviews/2026-06-23-phase-10w-5c-3-runtime-config-resolver-secret-encryption-review.md`
Key persistence review: `docs/reviews/2026-06-23-phase-10w-5c-4-data-protection-key-persistence-review.md`
Key encryption hardening review: `docs/reviews/2026-06-23-phase-10w-5c-5-data-protection-key-encryption-hardening-review.md`

---

## Enterprise Notification Platform — APIs + dispatch foundation complete (Phase 10W-2, 2026-06-21)

In-app notification APIs are live for authenticated users. Outbox dispatch processes InApp items end-to-end; Email/SMS safely queued.

- **APIs:** `GET /api/notifications` (paged, filtered, expires-excluded, archived-excluded), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`, `POST /api/notifications/{id}/archive`.
- **Filters:** `unreadOnly`, `category`, `severity`. Invalid values return 400. Current-user isolation enforced.
- **Dispatch:** `INotificationDispatchService.DispatchDueAsync` — InApp items delivered, Email/SMS items skipped with error until 10W-4/10W-6.
- **Tests:** 2131/2131 .NET (3 arch + 1278 unit + 850 integration).
- **Bell UI:** live notification dropdown at `src/app/design-system/student/notification-dropdown/` (committed, selector `sp-notification-dropdown`). Wired into `StudentAppLayoutComponent`. Gitignored vendor template no longer depended on (10W-4C).

Foundation review: `docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md`
API review: `docs/reviews/2026-06-21-phase-10w-2-in-app-notification-apis-dispatch-foundation-review.md`

## Enterprise Notification Platform — backend foundation complete (Phase 10W-1, 2026-06-21)

Backend notification foundation is in place. Entities, migration, service abstraction, and DI registration are done. No external delivery, no API, no UI yet.

- **Domain:** `Notification`, `NotificationOutboxItem` entities. 4 enums: `NotificationChannel`, `NotificationStatus`, `NotificationSeverity`, `NotificationCategory`.
- **Application:** `INotificationService` with `QueueInAppAsync`, `QueueEmailAsync`, `QueueSmsAsync`, `QueueAsync`.
- **Persistence:** `notifications` + `notification_outbox_items` tables (migration `T54_NotificationFoundation`). 6 indexes.
- **Behavior:** Queuing any channel creates a `Notification` row + a `NotificationOutboxItem` row. No external dispatch yet.
- **Tests:** 2108/2108 .NET (3 arch + 1278 unit + 827 integration).

Gap check: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`
Foundation review: `docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md`

Next: Phase 10W-2 — in-app notification APIs + dispatch worker.

---

## AI Usage + AI Pricing admin — full closure (Phases 10U + 10V, closed 2026-06-21)

Admin AI Usage page (`/admin/ai-usage`) is fully functional:

- **Summary cards**: total calls, success rate, failed, fallback, cost, input/output/total tokens. All respect active filters.
- **Zero-cost alert**: warning banner appears when any AI call in the filtered range was logged with $0 cost and tokens > 0. Includes call count and token total. Updates on every filter/date reload. Disappears when no zero-cost rows match active filters. (Phase 10V-3B)
- **Filter bar**: period preset (All time, Today, Last 7 days, Last 30 days, This month, Custom range). Custom range shows From/To date inputs + Apply/Clear dates; frontend validates both required and from ≤ to before calling APIs.
- **Column filters**: provider, model, feature key, status (success/failed/fallback), student (GUID). All filters compose with date range. Invalid status → 400. Invalid studentId GUID → 400.
- **Recent calls table**: server-side pagination (25/page, max 100), newest-first, paged envelope with `totalCount`/`totalPages`. Changing any filter resets to page 1. Empty/loading/error states.
- **CSV export**: `GET /api/admin/ai-usage/export.csv` — all active filters, up to 10,000 rows, RFC 4180, `Content-Disposition: attachment`. Columns: `CreatedAt, Provider, Model, FeatureKey, StudentId, WasSuccessful, IsFallback, FailureReason, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, CorrelationId`.
- **Daily trend table**: `GET /api/admin/ai-usage/trends` — grouped by calendar day (client-side), zero-fills missing days within a date range, all filters applied. Columns: Date, Calls, Success, Failed, Fallback, Tokens, Cost.
- **AI Pricing config**: `appsettings.json` holds pricing for 12 models (5 OpenAI, 4 Gemini, 3 Anthropic). Read by `AiPricingOptions.GetProviderPricing`. No hardcoded pricing in production C#. `AiModelPricingOverride` DB table added (migration `T53`). `IAiPricingResolver` resolves DB override first, config fallback second, null/0-cost third. All three providers (OpenAI, Gemini, Anthropic) use resolver for runtime cost. Missing pricing logs $0 and does not throw. Admin override management UI in AI Config page (list/create/edit/deactivate). Read-only pricing visibility panel shows current effective price per model.

Deferred: unique override constraint (TODO-10V-UNIQUE-CONSTRAINT), timezone selector, row cap config, student typeahead, charts/alerts, `AiUsageLog` schema extensions (GAP-1 through GAP-7).

**Tests (at closure):** 2080/2080 .NET, 896/896 Angular. All builds clean.

---

## AI Usage recent calls server-side filters (Phase 10U-5)

`GET /api/admin/ai-usage/recent` now accepts `provider`, `model`, `featureKey`, and `status` query params in addition to `from`/`to`/`page`/`pageSize`. Filters apply before count and pagination so `totalCount`/`totalPages` reflect the filtered universe. Invalid `status` returns 400.

Status semantics: `success` = WasSuccessful and not fallback; `failed` = not WasSuccessful; `fallback` = IsFallback (may also be successful).

Admin AI Usage page: four-filter bar above the recent calls table (provider, model, feature, status). Changing any filter resets to page 1 and reloads. "Clear filters" button appears when any filter is active; clearing resets column filters only and does not reset the date period. Pagination preserves active filters.

Summary stat cards are not affected by column filters.

**Tests:** 823/823 Angular tests pass. Backend: 1988/1988 pass (12 new filter integration tests).

---

## AI Usage recent calls server-side pagination (Phase 10U-4)

`GET /api/admin/ai-usage/recent` now returns a paged envelope `{ items, totalCount, page, pageSize, totalPages }` instead of a flat `{ total, items }`. Breaking change to the recent-calls endpoint shape.

Query params accepted: `page` (default 1), `pageSize` (default 25, max 100 — enforced server-side), `from`/`to` (ISO-8601 UTC, from 10U-3). The `/summary` endpoint is unchanged.

Admin AI Usage page: pagination is now server-driven. Changing the page calls `GET /recent?page=N&pageSize=25` with the active date range. Changing the period preset resets to page 1. `sp-admin-pagination` is shown when `totalPages > 1`. Date filtering (period preset select from 10U-3) and pagination compose correctly — `totalCount`/`totalPages` reflect the filtered universe.

Summary stat cards (total calls, cost, tokens, by-provider, by-feature) are independent from recent-call pagination and always reflect the full date-filtered dataset.

**Tests:** 813/813 Angular tests pass. Backend: 1977/1977 pass (10 new pagination integration tests).

---

## AI Usage date filtering (Phase 10U-3)

`GET /api/admin/ai-usage/summary` and `/recent` both accept `from`/`to` UTC query params. Admin AI Usage page has a period preset select above the stat grid: All time, Today, Last 7 days, Last 30 days, This month.

---

## AI Usage token totals and pricing config seed (Phase 10U-1/10U-2)

`GET /api/admin/ai-usage/summary` now includes `totalInputTokens`, `totalOutputTokens`, `totalTokens`. Admin AI Usage page shows three new stat cards for these. `appsettings.json` now has `OpenAI:Pricing`, `Gemini:Pricing`, `Anthropic:Pricing` sections with per-model pricing — unblocks `AiUsageLog.CostUsd` from always being $0.

---

## Student management final validation complete (Phase 10Students-F-H)

All enterprise student management work (Phases 10Students-F-A through 10X-L) validated end-to-end. Backend: 1944/1944 tests pass. Frontend: 791/791 Angular tests pass. Playwright: 6/6 reset tests pass. One E2E mock defect fixed: `admin-students-reset.spec.ts` mock returned old flat-array shape; updated to `PagedResponse` shape. No product contract changes. No student-facing changes.

---

## Admin shared UI: slide-over and table-action fixes (Phase 10X-L)

`sp-admin-slide-over` now renders above the entire admin shell (z-index 1000+, up from 400). Backdrop click no longer closes panels by default (`closeOnBackdrop` default changed to `false`). Stacked panels are supported via `[stackIndex]` input. Set CEFR and Assign Policy flows on `/admin/students/{id}` now use `sp-admin-slide-over` instead of a centred modal div. The three-dot action menu on admin tables no longer causes vertical scroll when opened near the bottom of the table; menu is now rendered `position:fixed` relative to the viewport.

No product behaviour changed for end users. Admin-only change.

**Tests:** 791/791 Angular tests pass. No backend change. No Playwright run (no pre-existing Playwright coverage for these flows).

---

## Student list filter selects available in admin UI (Phase 10Students-F-G)

Admin student list filter bar now has three filter selects: lifecycle stage (12 options), onboarding status (4 options), and CEFR level (A1–C2). Each filter change resets to page 1 and calls the backend. A "Clear filters" button appears when any of search/lifecycleStage/onboardingStatus/cefrLevel is active; clearing does not touch the "Show archived" toggle. Uses `sp-admin-select` component.

**Tests:** 32/32 Angular tests pass (admin-students). No backend change.

---

## Server-side student pagination, filtering, and sorting available (Phase 10Students-F-F)

`GET /api/admin/students` now returns a paged wrapper `{ items, totalCount, page, pageSize, totalPages }` instead of a flat array. Breaking change to the list endpoint shape.

Query params accepted: `page` (default 1), `pageSize` (default 25, max 100), `search` (email/name substring, case-insensitive), `includeArchived` (default false), `lifecycleStage`, `onboardingStatus`, `cefrLevel`, `sortBy` (student/name/email/onboardingStatus/lifecycleStage/cefrLevel/createdAt), `sortDir` (asc/desc, default desc).

Admin student list page is now server-driven: page, search, include-archived, and column sort all trigger a backend call. Pagination UI reflects `totalPages` from the server. All row actions (edit, reset password, reset data, archive) reload the current page after completion.

**Tests:** 756/756 Angular tests pass. Backend: 1944/1944 pass.

---

## Student audit history tab available (Phase 10Students-F-E)

`GET /api/admin/students/{id}/audit-history` returns up to 50 admin action history entries for a student, newest-first, combining `AdminAuditLog` (governance actions: SetCefr, Archive, lifecycle changes, policy assignments) and `StudentResetLog` (lifecycle reset records).

Admin student detail page now shows an "Audit history" section at the bottom with: action badge, source (Audit / Reset), actor ID prefix, reason, old→new value, details (inline for short; slide-over for long). No edit or delete controls. No password or secret fields exposed.

**Tests:** 751/751 Angular tests pass. Backend: 1932/1932 pass.

---

## Admin CEFR management available (Phase 10Students-F-D)

`PUT /api/admin/students/{id}/cefr` allows admins to set or clear a student's CEFR level from the admin student detail page. Valid values: A1, A2, B1, B2, C1, C2 (case-insensitive on input, stored normalised). Null or empty string clears the level.

Admin student detail profile section now shows current CEFR as a badge (or "Not set"), a "Set CEFR" button opening a modal with the level dropdown and optional reason field, and helper text confirming students cannot edit this field.

Each change writes an `AdminAuditLog` entry: action `SetCefr`, old/new value JSON, reason.

No migration required. No student-facing changes. Placement logic unchanged. Student `UpdateLearningPreferences` continues to explicitly exclude `CefrLevel`.

**Tests:** 743/743 Angular tests pass. Backend: 1925/1925 pass.

---

## Dedicated student detail endpoint + onboarding progress complete (Phase 10Students-F-B)

`GET /api/admin/students/{id}` is now a dedicated endpoint returning full student detail including onboarding progress (`StudentOnboardingProgressInfo`). Previously the component used the student list endpoint; it now calls the dedicated endpoint via `AdminApiService.getStudent(id)`.

Onboarding progress section added to `admin-student-detail.component`: status badge, current step (code pill), percentage complete, empty state when no progress row exists.

SQLite integration test blocker resolved: `OrderByDescending(p => p.StartedAt)` removed (unique index; SQLite does not support `DateTimeOffset` in ORDER BY). `OnboardingFlowSeeder` added to `ApiTestFactory` to satisfy FK constraint in onboarding progress tests.

**Tests:** 719/719 Angular tests pass. Backend: 1911/1911 pass. `git diff --check`: clean.

See: `docs/reviews/2026-06-19-phase-10students-f-b-dedicated-student-detail-endpoint-onboarding-progress-review.md`

---

## Admin read: student learning preferences complete (Phase 10Students-F-A)

Admins can now view all student-set learning preferences from the student detail page. A "Student preferences" summary card shows preference fields inline. A "View preferences" button opens the new `sp-admin-slide-over` panel for full detail. Admin edit of preferences is intentionally not implemented.

`sp-admin-slide-over` is now available as the design-system primitive for all admin secondary detail panels (student detail, policy editor, prompt preview, audit history).

No new migration or endpoint was required. Preference fields were already returned by `GET /api/admin/students` (added in Phase 10R-J).

**Tests:** 708/708 Angular tests pass. Backend: 1885 pass. `git diff --check`: clean.

See: `docs/reviews/2026-06-19-phase-10students-f-a-admin-read-student-preferences-review.md`

---

## Student usage policy assignment admin UI complete (Phase 10R-J)

Admins can now view, assign, and reset a student's usage policy from the student detail page.

What changed:
- New "Usage Policy" section on every student detail page.
- Shows effective policy name, scope, and source badge (Student override vs. Global default).
- "Assign Policy" button opens a modal — admin picks any active policy and optionally enters a reason. Saves immediately via `PUT /api/admin/students/{id}/usage-policy`.
- "Reset to Default" button visible only when a student override is active. Requires confirm dialog. Calls new `DELETE /api/admin/students/{id}/usage-policy`.
- If override is removed, student automatically falls back to the global default policy at next AI call.
- Assignment and removal both written to `AdminAuditLog`.
- `TODO-10R-STUDENT-ASSIGN` closed. 681/681 tests pass.

See: `docs/reviews/2026-06-19-phase-10r-j-student-usage-policy-assignment-admin-ui-review.md`

---

## Usage Policy Rule Editor admin UI complete (Phase 10R-H)

Admins can now create, edit, and delete individual usage policy rules directly in the Usage Policies admin page.

What changed:
- "Add rule" button in each expanded policy row opens a modal rule editor.
- Per-rule Edit and Delete buttons with modal forms and delete confirmation.
- Feature select (from definitions API) falls back to free-text input if definitions unavailable.
- Feature key is intentionally immutable on edit — shown as read-only with guidance to delete/re-add.
- All rule fields editable: enforcement mode, unit type, daily/weekly/monthly/cost limits, warning threshold, tracking enabled, active.
- Local state update on success — no full page reload; expanded row state preserved.
- Build clean; 670/670 tests pass.

See: `docs/reviews/2026-06-19-phase-10r-h-usage-policy-rule-editor-admin-ui-review.md`

---

## Usage Policy Rule CRUD backend complete (Phase 10R-G)

Admins can now manage individual usage policy rules via the API.

What changed:
- `UsagePolicyRule.Update(...)` domain method added — all limit fields are now mutable via domain layer.
- Three new admin API endpoints: `POST/PUT/DELETE /api/admin/usage-policies/{policyId}/rules[/{ruleId}]`.
- Duplicate-feature-key guard at application layer prevents two rules for the same feature in one policy.
- Frontend `UsageGovernanceService` has `addRule`, `updateRule`, `deleteRule` methods ready for a UI.
- No migration needed. No UI rule editor built yet (next phase: TODO-10R-RULE-MGMT-UI).

See: `docs/reviews/2026-06-19-phase-10r-g-usage-policy-rule-crud-backend-foundation-review.md`

---

## Usage Governance Admin UX complete (Phase 10R-F)

Usage Policies admin page is production-usable.

What changed:
- Summary stat cards (total / active / default policy name).
- Expandable rule rows showing feature name, enforcement mode, unit type, and limits.
- Feature display names resolved from feature-definitions API.
- All admin design system wrappers used.

See: `docs/reviews/2026-06-19-phase-10r-f-usage-governance-admin-ux-foundation-review.md`

---

## Frontend test cleanup complete (Phase 10X-J-T)

Frontend tests no longer lock admin and student UI work to Tailwind, TailAdmin, BEM, wrapper
implementation, border/radius/spacing, or inline style details unless a class is explicitly
documented as a public API.

What changed:
- Angular specs now prefer text, roles, ARIA attributes, form/CVA values, emitted events,
  open/close behavior, sorting events, and wrapper presence.
- Playwright tests now prefer page behavior and smoke flows: accessible row-action buttons,
  `aria-pressed` chip state, visible text, main landmarks, roles, and `data-testid` locators.
- Style-only tests were removed. Useful tests were rewritten to keep behavior coverage.
- Product behavior, API behavior, backend code, and UI functionality were unchanged.

See: `docs/reviews/2026-06-18-phase-10x-j-t-frontend-test-cleanup-review.md`

---

## Admin form and modal migration complete (Phase 10X-I)

Completed the three deferred admin UI CVA migration targets:

- **AI Config:** provider/model/voice selects kept native inside `sp-admin-form-field` (incompatible
  with `sp-admin-select` string binding). TTS voice, model name, API key (password), and Qwen
  endpoint inputs migrated to `sp-admin-input`. All action buttons migrated to `sp-admin-button`.
- **Integrations:** storage display fields → `sp-admin-input [disabled]`. Generation settings
  number fields kept native `<input type=”number”>` inside `sp-admin-form-field` (CVA string
  constraint). Background job controls and tables migrated to `sp-admin-button` and Tailwind
  table classes.
- **Student modals (all 3):** edit, reset-password, and reset-data page-local modals replaced with
  `sp-admin-modal`. All inner text/password inputs use `sp-admin-input`, multi-line fields use
  `sp-admin-textarea`, and actions use `sp-admin-button`. Page-local modal CSS removed entirely.

Wrapper enhancements this phase:
- `sp-admin-modal`: `maxWidth` input added (default 520px; student edit uses 720px).
- `sp-admin-input`: `[value]` input added for one-way display binding.
- `sp-admin-layout`: content area changed to `<main>` for `role=”main”` semantics (fixes Playwright).

Gates: .NET 1885 passed, Angular 421 passed (up from 411), build clean.

Closed TODOs: TODO-10X-G-AICONFIG-FORMS, TODO-10X-G-INTEGRATIONS-FORMS, TODO-10X-D-MODAL, TODO-10X-I.

See: `docs/reviews/2026-06-19-phase-10x-i-admin-form-modal-migration-review.md`

---

## Admin form wrapper CVA foundation (Phase 10X-H)

Made the TailAdmin-backed admin form wrappers safe for real Angular forms:

- `sp-admin-input`, `sp-admin-select`, and the new `sp-admin-textarea` now implement
  `ControlValueAccessor`. They two-way bind via `[(ngModel)]` or reactive `[formControl]`/
  `formControlName`, propagate disabled state from a disabled `FormControl`, and mark touched on blur.
- `sp-admin-form-field` renders the red `*` required marker via `[required]`.
- This unblocks per-field migration of the AI Config dense provider-credentials grid and the
  Integrations operational forms, which stay native this phase to avoid silent save regressions.
- Existing student-detail modals and the admin-only dark-mode boundary remain deferred.
- Gates: .NET 1885, Angular 394 (up from 379), Playwright 188.

See: `docs/reviews/2026-06-19-phase-10x-h-admin-form-cva-modal-foundation-review.md`

## Finish remaining admin page refactor (Phase 10X-G-F)

Completed the remaining wrapper consistency work after 10X-G:

- Students: the row table is wrapped in `sp-admin-table` (projected mode); lifecycle, onboarding,
  and CEFR pills now use the `sp-admin-badge` wrapper (was raw `.sp-admin-badge` class). Obsolete
  page-local pagination, row-action link, and badge CSS removed. Filter bar, pagination, sortable
  headers, and `sp-admin-table-actions` row menu were already in place from 10X-F/10X-G.
- Curriculum: create/edit and routing-preview form fields now use `sp-admin-form-field` for labels
  and hints (closing TODO-10X-G-CURRICULUM-FORMS). Native ngModel controls kept inside each field
  because `sp-admin-input`/`sp-admin-select` lack a ControlValueAccessor and cannot two-way bind.
- Verified the remaining priority pages (AI Usage, Prompts, Exercise Types, Diagnostics, Usage
  Policies, Integrations cards) were already wrapper-migrated in 10X-B/10X-G; no raw badge/table
  legacy markup remained except student-detail (out of scope this phase).

Intentionally deferred (unchanged, see TODOs): AI Config dense provider-credentials form fields,
Integrations operational forms, student management modals, the admin-only dark-mode class boundary,
and the full 10R-F/10U/10V redesigns.

Angular: 379 passed. .NET: 1885 passed. Playwright: 188 passed.

See: `docs/reviews/2026-06-18-phase-10x-g-f-finish-admin-page-refactor-review.md`

---

## Full admin page refactor (Phase 10X-G)

The highest-legacy admin pages now consume `sp-admin-*` wrappers consistently:

- Dashboard KPI tiles use `sp-admin-stat-card`; sections use `sp-admin-card` (including dashed
  analytics placeholders); status pills use `sp-admin-badge`. Page-local KPI/status/badge/table CSS removed.
- AI Config category Save/Test actions use `sp-admin-button`; duplicate in-card headings removed.
- Curriculum create/edit and routing-preview panels use `sp-admin-card`; actions use `sp-admin-button`.
- The admin header user/profile menu is wired through `sp-admin-dropdown` (open state, click-outside,
  and Escape handled by the wrapper; no page-local menu signal).

Admin-only dark mode remains scoped to `AdminThemeService` (`adminTheme` localStorage key) and does
not affect student UI. Full admin-only dark-mode class boundary is still future work.

Deferred: remaining page-local form fields (`.sp-ai-select`, `.sp-input`, Integrations operational
forms), student management modals, and the full usage-governance/AI-usage/prompt-playground redesigns.

Angular: 377 passed. .NET: 1885 passed. Playwright: 188 passed.

---

## Admin wrapper capability completion (Phase 10X-F)

New wrappers added (2026-06-18):
- `sp-admin-dropdown`: TailAdmin-backed dropdown with content projection, click-outside + Escape close.
- `sp-admin-table-actions`: row action three-dot dropdown. Generic actions API + content projection.
- `sp-admin-theme-toggle`: admin-only dark/light toggle. Uses `AdminThemeService` (isolated from student UI).
- `AdminThemeService`: admin-scoped theme service. `adminTheme` localStorage key.

Updated wrappers:
- `sp-admin-table`: sortable columns (`sortable`, `sortColumn`, `sortDirection`, `(sortChange)`). `hasActions` slot.
- `sp-admin-header`: named `[left]` and `[actions]` content slots. Theme toggle auto-rendered.
- `sp-admin-filter-bar`: named `[search]`, `[filters]`, `[actions]` slots.

`admin-students` row actions migrated to `sp-admin-table-actions`.
Angular: 373 passed. .NET: 1885 passed. Playwright: 188 passed.

Next: full admin page refactor (TODO-10X-G).

---

## TailAdmin wrapper adaptation (Phase 10X-E)

All 15 `sp-admin-*` wrapper components now use actual TailAdmin free Angular template patterns internally. Custom CSS approximations replaced with real TailAdmin class structures.

- Layout shell: `min-h-screen xl:flex`, `xl:ml-[290px]/xl:ml-[90px]` transition — exact TailAdmin Layout One.
- Sidebar: `fixed left-0 top-0 h-screen w-[290px]/w-[90px] bg-white border-r border-gray-200`.
- Header: `sticky top-0 flex w-full bg-white border-b border-gray-200 z-[99999]`.
- Button: brand-500 primary (`#465fff`), outline secondary. Rounded-lg, inline-flex.
- Badge: `rounded-full font-medium text-xs` — TailAdmin light variant color map (success/warning/info/primary/danger/neutral).
- Card / stat-card: `rounded-2xl border border-gray-200 bg-white`.
- Modal: `rounded-3xl bg-white`, backdrop `bg-gray-400/50 backdrop-blur-sm`.
- Table: `rounded-2xl border border-gray-200 bg-white`, th `text-xs text-gray-500 bg-gray-50`.
- Input / select: `h-11 rounded-lg border border-gray-200 bg-transparent`.
- Drawer: `fixed right-0 h-screen bg-white border-l border-gray-200`.
- Pagination / filter-bar: TailAdmin footer/filter bar structures.

Admin feature pages are unchanged — they use `sp-admin-*` only.
Wrapper public APIs (inputs/outputs) are stable.

See: `docs/architecture/admin-tailadmin-adapter-inventory.md`, `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md`

Remaining: table sorting, dropdown, theme toggle (10X-F).

---

## TailAdmin template import and adapter plan (Phase 10X-D)

The actual free TailAdmin Angular template source is now imported as a vendor reference.

- **Source:** https://github.com/TailAdmin/free-angular-tailwind-dashboard (commit da992cf, MIT)
- **Location:** `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/`
- **Gitignored:** yes — clone separately, not committed to main repo
- **Adapter inventory:** `docs/architecture/admin-tailadmin-adapter-inventory.md`
- The stale `admin-template/tailadmin/` placeholder has been removed.
- The target is no longer "TailAdmin-inspired". It is: use the actual TailAdmin source as vendor reference, exposed to SpeakPath only through `sp-admin-*` wrapper components.
- Feature pages must never import from `templates/`.

## Admin UI foundation, core migration, and gate closure (Phase 10X-A / 10X-B / 10X-C-F)

The admin app has a SpeakPath wrapper component layer aligned with TailAdmin Angular Layout One.

**Visual source of truth:** actual TailAdmin Angular free template — `src/app/templates/tailadmin/`.
Styling currently approximates TailAdmin Layout One via CSS custom properties. Full adapter alignment is 10X-E/10X-F.
See `docs/architecture/admin-ui-design-system.md` for the full architecture.

**Critical fix in 10X-C-F:** `AdminAppLayoutComponent` now uses `ViewEncapsulation.None` so
shell CSS (sidebar, nav items, header, drawer, profile flyout) reaches child component DOM.
Before this fix, the CSS existed but was blocked by Angular's default emulated encapsulation.
All admin pages now render with sidebar left, content right, header sticky — matching TailAdmin Layout One.

- Admin tokens: `src/app/design-system/admin/tokens/admin-tokens.css`.
- Barrel: `src/app/design-system/admin/index.ts`.
- Shell wrappers: `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header`.
- Page wrappers: page header, card, stat card, button, badge, table, state components, form controls, pagination, filter bar, modal, drawer, and toast outlet.
- Service foundations: admin toast, modal confirm state, drawer state.
- All core admin pages migrated to wrapper layer: Dashboard, Students, AI Config, AI Usage, Prompts,
  Exercise Types, Integrations, Diagnostics, Curriculum, and Usage Policies.

Remaining admin polish is scoped to page-local legacy internals, not new product features.
Dashboard inline CSS, AI Config form internals, Integrations internals, Curriculum create/edit/preview
forms, and student management modals remain future cleanup areas.

See: `docs/architecture/admin-ui-design-system.md`

## What is built and verified

The following end-to-end flow is implemented and verified:

```
Admin logs in
â†’ Admin creates student (temp password shown once)
â†’ Student logs in
â†’ Student changes temporary password (enforced server-side)
â†’ Student completes onboarding (language pair, career profile, experience level)
â†’ Student reaches Today page (the student home/dashboard)
â†’ Student starts Today's Lesson or navigates to Journey, Practice, Progress, or Profile
â†’ Student starts an activity (Writing / Listening / Vocabulary / Speaking)
â†’ Student submits draft or recording
â†’ Student sees structured AI feedback
â†’ Student retries or continues to next activity
â†’ Student can revisit learning history
```

## Playwright gate status

Phase 10H-F restored the full Playwright suite after Practice Gym fixture drift.

Failure categories found and fixed:

- Selector drift from old fixed Practice Gym card IDs to catalog-driven
  `practice-format-*` cards.
- Fixture/test data drift around ready runnable exercise types and planned
  non-runnable AI role play rows.
- Copy/label drift for the landing hero and perfect-score result label.
- Shared audio fallback selector drift after listening formats moved to the
  shared audio player.

Final Playwright result: 175 passed. No tests remain failing or skipped from this
stabilisation pass. No product behaviour changed.

## Usage governance (Phase 10R)

All AI feature calls are tracked per student. Admins can define and assign quota policies with per-feature daily limits. Expensive on-demand AI calls are blocked (HTTP 429) before they incur cost when a student exhausts their quota. Prepared/pre-generated content is always allowed.

- Feature registry: 16 features across PreparedLearning, DynamicAi, ExpensiveAi categories.
- 3 seeded policies: Default Pilot Student (TrackOnly all), Low Cost Student (HardLimit 5/day writing, 3/day speaking, 5/day TTS), Test Unlimited (TrackOnly, for testing).
- Admin UI at `/admin/usage-policies` — create, edit, assign policies.
- HTTP 429 response includes `featureKey`, `availableAlternatives`, `resetAt`.
- Audit log written for every policy assignment change.

Deferred: workspace/cohort inheritance, billing, monthly/weekly limits, student-facing widget.

See: `docs/architecture/usage-governance.md`

---

## Learning preferences in AI context

Student profile preferences are used by AI generation and evaluation.
Generated Today lesson activities, Practice Gym activities, background Practice
Gym activities, buffered lesson activities, lesson batch planning summaries, and
AI activity evaluation (WritingScenario, SpeakingRolePlay) all receive compact
learner preference context when fields are present.

The context can include preferred name, learning language, support language,
translation help preference, learning goals, custom goal, focus areas, custom
focus, difficulty preference, and current CEFR level as system-estimated.

Prompt-editing fields, admin-only profile names, roles, quotas, lifecycle state,
account details, raw submitted text, and any student-editable CEFR override are
excluded. Missing preferences create no fake defaults.

`LearningGoalContext` uses custom goal first, then selected goals, then legacy
goal fields, then career context. If none are present, it remains null and does
not default to workplace.

## Preference enforcement rules (Phase 10K-F)

- Vocabulary cadence picks gate on `WorkplaceSpecific` from the resolved goal context.
  Non-workplace students (Day-to-day, Travel, Social, etc.) receive `PhraseMatch`.
  Workplace students receive `GapFillWorkplacePhrase`.
- Lesson batch generation compact summary includes `preferredSessionDurationMinutes`
  as a hint to the AI planner. `SessionDurationTemplates` in `SessionGeneratorService`
  is the authoritative session length gate.
- AI evaluation prompts receive `learnerPreferences` and `learningGoalContext`
  variable slots. Current evaluation prompt templates do not yet reference these
  variables — a prompt-engineering pass is needed to activate them.

## Student navigation model

The student app has five top-level sections:

| Section | Route | Question answered |
|---|---|---|
| **Today** | `/dashboard` | What should I do now? |
| **Journey** | `/journey` (also `/my-path`) | Where am I in my course? |
| **Practice** | `/practice` | What can I practise freely? |
| **Progress** | `/progress` | How am I improving? |
| **Profile** | `/profile` | What are my settings? |

- The student-facing label for the home page is **Today**, not Dashboard. The route `/dashboard` is preserved.
- `/journey` and `/my-path` both load the Learning Journey page. `/my-path` is kept for backwards compatibility.
- **Practice Gym** (`/practice`) is the student-facing landing for classroom-style free practice by skill, vocabulary class, exercise type, and future live practice. It does not auto-start an activity on load.
- Vocabulary is accessible from Practice Gym and Progress â€” it is not a top-level nav item.
- Writing and Email are valid activity types within Practice Gym and lessons. The student product is not writing/email-first.

## Implemented activity types

| Type | Status |
|---|---|
| `WritingScenario` | âœ… implemented |
| `ListeningComprehension` | âœ… implemented (with TTS audio) |
| `VocabularyPractice` | âœ… implemented |
| `SpeakingRolePlay` | âœ… implemented (MVP â€” fake STT) |

All four activity types use the unified `/activity` path.
`/api/writing/*` endpoints have been removed. See `docs/decisions/activity-flow-migration.md`.

## Practice Gym - activated pattern cards

Skill cards call `GET /api/activity/practice-gym/next?skill=<skill>`. Exact
exercise type cards call `GET /api/activity/practice-gym/next?exerciseType=<key>`.
Both serve a ready pre-generated activity from the pool (`source: "pool"`) when
available, or fall back to on-demand generation (`source: "onDemandFallback"`)
and route to `/activity?activityId=<id>&returnTo=/practice`.

`GET /api/activity/next` still accepts canonical `?exerciseType=<key>` plus legacy `?pattern=<key>` and `?type=` query parameters, unchanged, as the underlying fallback/compatibility path.

| Practice Gym card | Selection | Status |
|---|---|---|
| Vocabulary class | `/activity?exerciseType=phrase_match&returnTo=/practice` (module link, unaffected) | functional word-card lesson + matching practice |
| Listening | pool-aware skill selection | functional |
| Reading | pool-aware skill selection (`reading_multiple_choice_single`, `reading_multiple_choice_multi`, `reading_fill_in_blanks`, `reorder_paragraphs`) | functional |
| Writing | pool-aware skill selection | functional |
| Speaking | pool-aware skill selection | functional recorded prompt, no pronunciation claim |
| Matching | `/activity?exerciseType=phrase_match&returnTo=/practice` (module link, unaffected) | functional |
| Fill in the blanks | `/activity?exerciseType=gap_fill_workplace_phrase&returnTo=/practice` (module link, unaffected) | functional |
| Email | `/activity?exerciseType=email_reply&returnTo=/practice` (module link, unaffected) | functional |
| Workplace Chat | `/activity?exerciseType=teams_chat_simulation&returnTo=/practice` (module link, unaffected) | functional |
| Multiple choice | covered by Reading (`reading_multiple_choice_single` single, `reading_multiple_choice_multi` multi) | functional |
| Sentence transformation | - | Coming soon |
| Error correction | - | Coming soon |
| Word formation | - | Coming soon |
| Unscrambling | - | Coming soon |
| AI role play | - | Coming soon, live AI |
| Pronunciation | - | Coming soon, no STT/scoring support |


Practice Gym skill cards now use the ExerciseType registry. A skill card no
longer means one fixed activity type. The selected skill resolves to an enabled,
ready, generation-eligible, Practice Gym-supported exercise type with the same
`primarySkill`, then routes through canonical `exerciseType=<key>`. If no
eligible row exists, the frontend shows a safe unavailable message and does not
start broken generation. Planned future exercise format rows remain blocked. This is not the
final Practice Gym pre-generation pool; the future pool should reuse the same
registry selection rules.

All pattern-keyed activities go through `PatternEvaluationRouter`. Progress updates only after a submitted attempt. Returning from any pattern card goes back to `/practice` via `returnTo`. Ready Practice Gym cache entries are consumed before on-demand AI generation.

## Real TTS via Admin AI Config (complete â€” 2026-06-11)

`ListeningAudioService` and `PlacementAudioService` now resolve TTS provider at request time via `TtsProviderResolver`:

- `AiProviderConfig` rows `tts.listening` and `tts.placement` control which TTS service runs
- Default seed: `provider=fake, model=fake, voice=fake` â†’ silent WAV (tests never need `OPENAI_API_KEY`)
- Admin can switch to real TTS providers in Admin AI Config UI:
  - OpenAI: `provider=openai`, model `tts-1` or `tts-1-hd`, voice such as `onyx`
  - Gemini: `provider=gemini`, model must be a Gemini TTS model such as `gemini-2.5-flash-preview-tts`, voice such as `Kore`
  - Qwen: `provider=qwen`, model `cosyvoice-v2`, voice such as `longxiaochun_v2`
- TTS category saves reject non-TTS models. Existing Gemini TTS configs with a normal text model are defensively routed to the default Gemini TTS model by `GeminiTextToSpeechService`.
- `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; returns `audio/mpeg`; never throws
- `GeminiTextToSpeechService` calls the Gemini `generateContent` TTS path with `responseModalities=["AUDIO"]`, `speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName`, and returns `audio/wav`; never throws
- Activity audio endpoints remain JWT-protected. Angular fetches listening audio through `HttpClient` and converts it to a temporary `blob:` URL before rendering `<audio>`, so browser media requests do not hit `/api/activity/{id}/audio` anonymously.
- `PlacementAudioService` checks both `.wav` and `.mp3` on disk (backward compat with pre-existing files)
- T35 migration adds nullable `voice_name varchar(100)` to `ai_provider_configs`

## Onboarding experience step (complete â€” 2026-06-11)

A new step-5 collects professional context before placement:

- `PATCH /api/onboarding/experience` â€” sets `ProfessionalExperienceLevel`, `RoleFamiliarity`, computes `WorkplaceSeniority`
- Uses `StudentProfile.SetExperienceContext()` â€” bypasses onboarding state machine; can be called at any stage
- Angular: `step5-experience` component inserted between step-4 and `/placement`
- Step-4 now shows "Step 4 of 5"; navigates to `/onboarding/step-5` on finish
- Non-blocking: API failure still navigates to `/placement`; "Skip for now" skips without calling API
- Existing completed students not broken â€” endpoint accepts any auth token regardless of onboarding state

## Onboarding v2 foundation (complete — 2026-06-17, Phase 10I)

A configurable multi-step onboarding system (v2) runs in parallel with the existing v1 state machine. Existing students and v1 code are untouched.

### New API endpoints

- `GET /api/onboarding` — returns `OnboardingV2StatusDto`: current step, completed steps, percentage, preliminary CEFR level. Lazy-creates a `StudentOnboardingProgress` record on first call. Students who completed v1 onboarding are auto-marked complete.
- `POST /api/onboarding/steps/{stepKey}` — submits an answer for one step. Validates answer against step type (max length, valid option keys, max selections). Applies typed `OnboardingAnswerMapping` to `StudentProfile.UpdateLearningPreferences()`. Idempotent — upserts `StudentOnboardingResponse`.
- `POST /api/onboarding/complete` — validates all SystemRequired+enabled steps are done, scores assessment answers against server-side metadata, stores `PreliminaryCefrLevel` on progress, transitions `LifecycleStage` → `PlacementRequired`. Does **not** overwrite a real `CefrLevel` from PlacementAssessment.
- `GET /api/admin/onboarding/flow` (Admin role) — read-only view of the active `OnboardingFlowDefinition` including steps and answer mappings. Never exposes `AssessmentMetadataJson`, correct answers, or scoring weights.

### Architecture decisions

- v2 is parallel — v1 `OnboardingStatus`/`OnboardingStep` fields on `StudentProfile` remain as legacy compatibility.
- Single active flow enforced by PostgreSQL partial unique index (`WHERE is_active = true`).
- Flow versions are immutable once students have progress; admin edits must create a new version.
- `PreliminaryCefrLevel` stored on `StudentOnboardingProgress` only — never overwrites `StudentProfile.CefrLevel` unless it is null.
- `AssessmentMetadataJson` (correct answers, scoring weights) is server-side only — never returned to student or admin APIs.
- Percentage counts SystemRequired+IsEnabled steps only.
- Post-onboarding lifecycle → `PlacementRequired` (no “OnboardingComplete” stage exists).
- Unique `(progress_id, step_key)` constraint on `StudentOnboardingResponse`.

### Angular route

`/onboarding/v2` — standalone shell component with 11 step renderers (Welcome, PreferredName, SupportLanguage, LearningGoals, FocusAreas, DifficultyPreference, SingleChoice, MultipleChoice, FreeText, AssessmentQuestion, Summary).

### Known limitations

- No admin visual flow builder — flow is seeded via `OnboardingFlowSeeder`.
- Preliminary CEFR is a simple weight-band calculation, not a full adaptive placement engine.
- No curriculum routing or readiness pool based on v2 outcome.
- No Playwright E2E spec for v2 flow (no test user seeded with v2 progress).

### Migration

T47_OnboardingV2 — adds `onboarding_flow_definitions`, `onboarding_step_definitions`, `student_onboarding_progress`, `student_onboarding_responses`.

## Test suite baseline (as of tts-placement-today-sprint — 2026-06-11)

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

## Admin capabilities

- Create students with temporary passwords
- Configure AI providers, model assignments, and prompt templates via Admin UI
- AI provider credentials stored securely in DB (never returned to client)
- AI usage logs accessible
- Student list is the admin entry point for create/edit/archive student management
- Create student returns to Students with a toast after success
- Archive uses `StudentLifecycleStage.Archived`, hides archived students by default, and disables sign-in
- AI Config shows category-level provider/model routing: Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path, Listening TTS, and Placement TTS
- Integrations can trigger lesson generation, inspect recent generation batches, view ready lesson buffers, retry failed/partial batches, and cancel queued/running batches stuck from background generation failures
- Background lesson generation now materializes AI lesson plans into ready `LearningSession` rows instead of failing on generated `GenerationJobItem` tracking state
- Practice Gym background caching queues and materializes ready pattern-keyed activities for eligible active students
- Admin shell header is avatar-only; user email, role, profile placeholder, and sign out live in the avatar flyout menu
- Curriculum is hidden from admin navigation while its future purpose is redefined

## Placement Assessment â€” current state

Placement Assessment MVP is implemented:
- 6-section structured assessment (`PlacementAssessment`, `PlacementSection` entities)
- AI evaluation â†’ `PlacementResult` as CEFR source of truth
- Listening section uses **server-side TTS audio** (`PlacementAudioService`), not browser SpeechSynthesis
- `GET /api/placement/audio/{assessmentId}/listening` streams authenticated audio
- Frontend shows native `<audio controls>` when server audio is available; graceful fallback if not
- Transcript hidden by default behind "Show transcript"

## LearningSession data layer (Phase 1 complete â€” 2026-06-10)

- `LearningSession` and `SessionExercise` domain entities implemented
- `SessionStatus` and `ExerciseStatus` enums added
- EF configurations and migration T32 applied (`learning_sessions`, `session_exercises` tables)
- `LinguaCoachDbContext` updated with `LearningSessions` and `SessionExercises` DbSets
- 52 new tests added (284 unit, 247 integration â€” 531 total)

## LearningSession generator (Phase 2 complete â€” 2026-06-10)

- `ExerciseKind` enum added (`VocabularyWarmup`, `ContextInput`, `ListeningInput`, `ReadingInput`, `WritingTask`, `SpeakingTask`, `Review`)
- `ISessionGeneratorService` / `SessionGeneratorService` implemented
- Duration templates: 10 min (3 steps), 15 min (4 steps), 20 min (4 steps), 30 min (5 steps)
- Weak-skill substitution: Speaking weak â†’ SpeakingTask promoted; Listening weak â†’ ListeningInput enforced
- Idempotent: calling twice on the same day returns the same session
- Module progression: advances to next module after 5 completed sessions
- 65 new tests added in Phase 2 (609 total: 328 unit, 281 integration)

## LearningSession backend endpoints (Phase 3 complete â€” 2026-06-10)

- `SessionsController` at `src/LinguaCoach.Api/Controllers/SessionsController.cs`
- Endpoints: `GET /today`, `GET /{id}`, `POST /{id}/start`, `POST /{id}/complete`, `POST /{id}/exercises/{eid}/complete`, `GET /{id}/reflection` (501 stub)
- `SessionQueryHandler` and `SessionLifecycleHandler` in `LinguaCoach.Infrastructure/Sessions/`
- Lifecycle transitions: `CourseReady` â†’ `InLesson` (start), `InLesson` â†’ `ActiveLearning` (complete)
- All operations idempotent; ownership verified on every request
- 27 new integration tests added in Phase 3 (629 total: 328 unit, 301 integration)

## LearningSession frontend (Phase 4 complete â€” 2026-06-10)

- Today's Lesson card on dashboard â€” visible for `CourseReady`, `InLesson`, `ActiveLearning` lifecycle stages
  - Shows title, duration, skill focus, step count, status badge
  - Button label adapts: "Start today's lesson" / "Resume lesson" / "Review today's lesson"
  - Practice Gym remains secondary but visible
- `LessonComponent` at `/lesson/:sessionId` â€” Angular standalone component
  - Session detail loaded from `GET /api/sessions/{id}`
  - Ordered exercise steps, progress bar, per-step panel with instructions
  - Prepared buffered steps open directly; unprepared old-session steps show an explicit load action
  - Start, complete exercise, complete lesson flows fully wired
  - Completion summary shown on lesson complete
- `SessionService` + TypeScript models added to frontend core
- 14 new Playwright e2e tests â€” 81/81 pass total (no regressions)

## Exercise activity wiring (Phase 5A complete â€” 2026-06-10)

- `POST /api/sessions/{sessionId}/exercises/{exerciseId}/prepare` endpoint added
- Idempotent: calling twice returns the same `LearningActivity`
- ExerciseKind â†’ ActivityType deterministic mapping: VocabularyWarmupâ†’VocabularyPractice, ContextInputâ†’WritingScenario, ListeningInputâ†’ListeningComprehension, ReadingInputâ†’ReadingTask, WritingTaskâ†’WritingScenario, SpeakingTaskâ†’SpeakingRolePlay
- Review step returns a lightweight reflection placeholder (`isReview: true`), no AI generation
- VocabularyPractice and `ReadingTask` (not yet in `IAiActivityGenerator`) use `SystemFallback` placeholders
- 16 new integration + unit tests; 645 total (328 unit + 317 integration)

## Exercise activity wiring â€” frontend (Phase 5B complete â€” 2026-06-10)

- `LessonComponent` now calls `POST /api/sessions/{id}/exercises/{eid}/prepare` when student opens an exercise
- "Open activity" button navigates to `/activity?activityId=<id>&returnTo=/lesson/<sessionId>`
- `ActivityLessonComponent` supports `?activityId=<id>` (loads specific prepared activity) and `?returnTo=<path>`
- Review steps show a reflection prompt + "Mark complete" â€” no activity generated
- Server-assigned `learningActivityId` (persists across refresh) skips re-prepare
- `GET /api/activity/{id}` backend endpoint added
- 8 new Playwright tests; 90/90 pass

## Exercise Pattern Engine (complete â€” 2026-06-10)

- `exercise_patterns` table is seeded with the 8 MVP patterns.
- `LearningActivity.ExercisePatternKey` stores the durable pattern link.
- Pattern-aware prepare/generation sets `exercisePatternKey` and returns `interactionMode` on `ActivityDto`.
- Pattern-keyed activity responses include bounded `contentJson` for frontend renderers; legacy listening activities do not expose raw answer-bearing JSON before submission.
- `ActivityLessonComponent` now routes pattern-keyed activities through `ExerciseRendererComponent`.
- MVP renderers are wired: ReadOnly, FreeTextEntry, MatchingPairs, GapFill, AudioAndFreeText, AudioAndGapFill, ChatReply, EmailReply.
- All 7 active renderers (excluding ReadOnly) follow a Lesson â†’ Practice â†’ Evaluate structure: a "Goal" element (`learningGoal`) shown via `ChatReplyComponent`'s own goal display, `EmailReplyComponent`/`FreeTextEntryComponent`'s `coachNote`, or the shared `ExerciseLessonIntroComponent` (GapFill, MatchingPairs, AudioAndFreeText, AudioAndGapFill).
- Frontend renderer coverage added; full Playwright suite passes 97/97.
- Backend baseline: 762 tests pass (380 unit + 382 integration).
- `npm run build` passes; known non-blocking Angular warnings remain for admin CSS budgets and skipped selectors.

## Pattern Evaluation Engine (complete â€” 2026-06-10)

All 7 phases complete. `MarkingMode` is now first-class in the evaluation flow.

- **Evaluators**: `ExactMatchEvaluator` (gap_fill, listen_and_gap_fill), `KeyedSelectionEvaluator` (phrase_match), `NoMarkingEvaluator` (lesson_reflection), `AiStructuredEvaluator` (listen_and_answer, email_reply, teams_chat_simulation), `AiOpenEndedEvaluator` (spoken_response_from_prompt)
- **Router**: `IPatternEvaluationRouter` dispatches by `MarkingMode`; wired into `ActivitySubmitHandler`
- **Persistence**: `ActivityAttempt` stores structured `SubmittedAnswerJson`, `EvaluationResultJson`, `MaxScore`, `Percentage`, `Passed`, `Completed`, `MarkingMode`; EF migration T34 adds nullable columns only
- **Skill update**: `PatternSkillUpdateService` upserts `StudentSkillProfile` from `skillImpacts`; validates key allowlist, clamps delta, synthesises fallback from pattern key when impacts absent
- **Memory update**: compact memory packet (exercisePatternKey, score, coachSummary, top 3 corrections, top 5 impacts, top 3 signals) sent to `StudentMemoryService.UpdateMemoryAsync` â€” never includes raw submitted text; swallowed on failure
- **Frontend result UI**: `PatternEvaluationResultComponent` with 6 branches (MatchingPairs, GapFill, Chat/Email, ListenAndAnswer, SpokenResponse, ReadOnly); legacy non-pattern paths unchanged
- **Test counts**: 865 dotnet (451 unit + 414 integration) + 111 Playwright â€” all pass

## Student UX Alignment / Writing-Assumption Cleanup (complete â€” 2026-06-10)

All 7 phases complete. The student UI no longer implies SpeakPath is a writing/email-only app.

- **Nav**: student sidebar and mobile nav show Today, Journey, Practice, Progress, Profile. Dashboard label removed. Vocabulary removed from top-level nav.
- **Today** (`/dashboard`): motivational home page. Heading: "Today's Lesson". "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Journey** (`/journey`, `/my-path`): page heading "Learning Journey". Memory fallback copy updated from "workplace writing" to "workplace English". "Continue practising" CTA replaced with safe CTAs to `/dashboard` and `/practice`.
- **Practice Gym** (`/practice`): MVP landing page. Functional cards: Vocabulary (â†’`/vocabulary`), Listening, Writing, Speaking (â†’`/activity?type=X`). Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. Does not auto-start on load.
- **Fixture cleanup**: generic writing/email-only fixture copy in Playwright tests updated to mixed-skill workplace English. Valid WritingScenario and email_reply test coverage preserved. No seed data deleted.
- **Test counts**: 865 dotnet (unchanged) + 165 Playwright â€” all pass

## Known gaps / not yet built

- Session reflection (`GET /api/sessions/{id}/reflection` returns 501; needs AI prompt key `session_reflection`)
- `ActivityShellComponent` not yet embedded inline in lesson page (navigates away instead)
- No real STT provider (SpeakingRolePlay uses `FakeSpeechToTextService`)
- No email delivery for temp passwords (admin copies manually)
- No admin CRUD for career profiles / learning tracks (seed data only)
- No audio cleanup job (50-file soft ceiling in place as mitigation)
- Dynamic pattern selection (week skills â†’ pattern choice) not yet implemented

See `docs/backlog/deferred-work.md` for the full deferred work list.

## Next recommended work

1. **Dynamic Pattern Selection** â€” choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
2. **Practice Gym Expansion** â€” deep pattern/skill selection within Practice Gym (Workplace Chat, Email, Gap Fill, Phrase Match unlock; dynamic session template).
3. **Session Reflection AI** â€” evaluation outputs now stable; wire `session_reflection` prompt.

See `docs/sprints/current-sprint.md` for the active sprint scope.

## Exercise Type Catalog foundation (Phase 3A)

The platform now has a durable exercise type catalog for future generation control.
Skills and exercise types are separate: a module can target primary and secondary
skills, while its Practice stage uses a catalog `exerciseType`.

Admins can list and enable or disable exercise types from Admin Exercise Types.
Disable affects future Today and Practice Gym generation only. Existing activities,
attempts, and history remain readable.

Planned future exercise formats are visible in the catalog as planned entries.
They are not generation-eligible until implementation status becomes ready, even
if an admin enables them.

## Phase 3B ExerciseType routing foundation

The backend now has an `IExerciseTypeRegistry` backed by the persisted exercise type catalog. It resolves `exerciseType` keys to renderer, evaluator, generation prompt, legacy `ActivityType`, and `ExercisePatternKey` metadata.

`GET /api/activity/next?exerciseType=<key>` is supported for ready runnable types. Existing `/activity?type=...` and `/activity?pattern=...` links still work. Practice Gym now routes implemented cards with `exerciseType` where safe. Today session generation validates deterministic pattern keys through the registry before creating steps.

Planned future exercise formats remain visible in Admin. They are not generation-eligible or routable to student activity flows until implementation status is `ready`.

## SpeakingRolePlay staged migration (Phase 5 — 2026-06-15)

`SpeakingRolePlay` now generates and serves `module_stage_v1` staged content,
matching the pattern established by `WritingScenario` and `ListeningComprehension`.

**What changed:**

- Generation prompt (`activity_generate_speaking_roleplay`) rewritten to produce
  `module_stage_v1` with `learnContent`, `practiceContent`, and `feedbackPlan`.
  Token budget increased: `maxInputTokens` 900 → 1600, `maxOutputTokens` 800 → 1200.
- `learnContent` explicitly forbids recording controls, microphone instructions,
  `startRecording`, and `stopRecording`.
- `practiceContent.exerciseData` requires: `role`, `partnerRole`, `situation`, `prompt`.
- `AiActivityGeneratorHandler` validates `SpeakingRolePlay` as staged (retry-once-then-fail).
- `ActivityGetHandler` detects legacy flat speaking JSON and adapts it to `legacy_adapted_v1`
  via `AdaptLegacySpeaking`. Old student data and history continue working unchanged.
- `SpeakingRolePlayEvaluator.ExtractExerciseDataJson` feeds only `practiceContent.exerciseData`
  into the evaluation prompt.
- Frontend `LegacySpeakingPresenter` returns `stagedLearning` block when `stageContent.learn`
  exists; falls back to legacy `speakingScenario` block for old rows.

**What was NOT changed:**

- No planned speaking format rows made runnable.
- No Practice Gym pre-generation changes.
- No Today pre-generation changes.
- No MinIO / audio lifecycle changes.
- No new planned future exercise renderer or evaluator.
- `/activity` endpoint and old compatibility params remain.

**Remaining staged migrations:** pattern-backed activities.

## Phase 6 — VocabularyPractice staged migration, completed

`VocabularyPractice` now uses `module_stage_v1` for newly generated deterministic vocabulary activities. The migration keeps the existing seeded vocabulary source. It does not add broad AI vocabulary generation.

The staged vocabulary module has exactly three pages: Learn, Practice, and Feedback. Learn teaches vocabulary meaning, usage, word form, example context, memory strategy, and common mistakes. Practice contains the fill-blank vocabulary task through `practiceContent.exerciseData`. Feedback uses the existing deterministic vocabulary evaluator with staged `practiceContent.exerciseData` support and legacy flat JSON fallback.

Completed staged migrations:

- `ListeningComprehension`
- `WritingScenario`
- `SpeakingRolePlay`
- `VocabularyPractice`

Remaining staged migrations are pattern-backed activities. Planned future exercise formats made runnable so far: `reading_multiple_choice_single` (Phase 8A), `reading_multiple_choice_multi` (Phase 8B), `reading_fill_in_blanks` (Phase 8C), `reorder_paragraphs` (Phase 8D), `reading_writing_fill_in_blanks` (Phase 8E), `summarize_written_text` (Phase 8F), `write_essay` (Phase 8G), `listening_multiple_choice_single` (Phase 8H — first runnable listening-primary format), `listening_multiple_choice_multi` (Phase 8I — second runnable listening-primary format), `listening_fill_in_blanks` (Phase 8J — third runnable listening-primary format, first runnable listening+writing format), `select_missing_word` (Phase 8K — fourth runnable listening-primary format), `highlight_correct_summary` (Phase 8L — fifth runnable listening-primary format, first runnable listening+reading format), `highlight_incorrect_words` (Phase 8M — sixth runnable listening-primary format, second runnable listening+reading format), `write_from_dictation` (Phase 8O — seventh runnable listening-primary format), and `summarize_spoken_text` (Phase 8Q — eighth runnable listening-primary format, first AI-evaluated listening+writing format). All reading-primary, writing, and listening planned future formats are now ready. All remaining planned future exercise formats are the speaking formats (`read_aloud`, `repeat_sentence`, `describe_image`, `respond_to_situation`, `retell_lecture`, `summarize_group_discussion`, `answer_short_question`), which remain planned and non-runnable. Today pre-generation remains a future phase. Phase 8P (2026-06-16) wired the audio lifecycle for all 9 listening pattern keys. `HandlePatternKeyedAsync` now calls `EnsureAudioAsync` after creating pattern-keyed listening activities. `ActivityDto` gains an `AudioStatus` string field (`"ready"` / `"pending"` / `"unavailable"`). A shared `app-audio-player` Angular component was created and all 5 listening renderer HTML templates now use it instead of inline `<audio>` tags. The exercise-renderer getters for `listeningFillInBlanks`, `highlightCorrectSummary`, and `highlightIncorrectWords` now fall back to `activity.audioUrl` from the API when `ed['audioUrl']` is absent from the content JSON. Audio is now generated on first fetch for all listening patterns; `audioUrl` will be non-null when TTS succeeds. Phase 8Q (2026-06-16) added `summarize_spoken_text` to `ListeningAudioService.ListeningPatternKeys` (now 10 keys) so it reuses the same shared audio lifecycle and `app-audio-player`. Its evaluation reuses the existing `AiStructuredEvaluator` AI path (same as `summarize_written_text` / `write_essay`); `learnContent` and the expected-answer `keyPoints` are never sent to the AI before submission.

Phase 8N (2026-06-16) added configurable practice item counts as a foundation (not a new format). Every `ExerciseTypeDefinition` now carries `MinItemsPerPractice`/`DefaultItemsPerPractice`/`MaxItemsPerPractice` and `MinOptionsPerItem`/`DefaultOptionsPerItem`/`MaxOptionsPerItem`, seeded per type, editable in the admin exercise-types page (with inline `min <= default <= max` and non-negative validation) and via admin PATCH. Counts feed generation prompt context and optional validator count enforcement. Counts are configuration only and never change readiness; no format was made runnable. See [practice-item-sets.md](../architecture/practice-item-sets.md).

## Phase 10O — Practice Gym Suggested Practice & Pool Serving, completed (2026-06-18)

Phase 10O connects the readiness pool to the student-facing Practice Gym. The pool built in 10M/10N is now surfaced as personalised suggestion cards via a new student API.

### New student-facing API endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/practice-gym/suggestions` | Returns SuggestedItems, ContinueItems, ReviewItems from the readiness pool |
| POST | `/api/practice-gym/suggestions/{id}/start` | Reserves an item; returns LearningActivityId / LearningSessionId for navigation |
| POST | `/api/practice-gym/suggestions/{id}/complete` | Best-effort marks item consumed |

### Sections returned

- **SuggestedItems** — Ready items ranked by focus-area match → goal context match → pool priority → expiry → FIFO. Max 6.
- **ContinueItems** — Reserved (in-progress) items not past expiry. Max 3.
- **ReviewItems** — ReviewOnly status items or Ready+lower-level content with review/scaffold/remediation routing reason. Max 4.

### Labels / wording

Normal → "Recommended for your current goal" | Review → "Review" | Scaffold → "Step back to strengthen basics" | Remediation → "Targeted fix" | Fallback → "General practice".

### What is NOT done yet (after 10O-F)

- Existing `GET /api/activity/practice-gym/next` (by skill/exercise type) is unchanged.
- Session/SessionExercise completion paths do not yet wire consumption (linked via LearningActivityId only).

### Tests (10O only)
+14 unit tests, +10 integration tests. Total: 1774 passed, 0 failed.

---

## Phase 10O-F — Practice Gym UI Integration & Completion Consumption Wiring, completed (2026-06-18)

Phase 10O-F connects the 10O backend API to the Angular Practice Gym page and wires completed activities back to readiness pool consumption.

### Angular UI changes

- **`PracticeGymSuggestionsService`** — new Angular service: `getSuggestions()`, `startSuggestion(id)`, `completeSuggestion(id)`.
- **`PracticeGymComponent`** — extended: suggestion signals, `loadSuggestions()`, `startSuggestion()` with loading/disabled state, `routingLabel()` helper.
- **Practice Gym template** — new sections: Suggested for you, Continue practice, Review practice. Cards show title, skill, CEFR level, estimated duration, context tags, routing label. Empty/loading/error states present. Existing By skill and By exercise type sections preserved.
- Student-friendly routing labels: Normal → "Recommended for your current goal", Review → "Review", Scaffold → "Step back to strengthen basics", Remediation → "Targeted fix", Fallback → "General practice".
- Lower-level content labelled with muted chip. No silent downgrade.

### Backend consumption wiring (TODO-014 resolved)

- `ActivitySubmitHandler` — injected `IPracticeGymSuggestionService`. `TryConsumeReadinessItemAsync` called best-effort after all completion paths:
  - WritingScenario / AI-evaluated: always called after save.
  - VocabularyPractice: called after deterministic evaluation.
  - ListeningComprehension: called after deterministic evaluation.
  - Pattern evaluation: called only when `evalResult.Completed == true`.
- Lookup scoped to `studentProfileId + activityId + Reserved status`. Exception swallowed — completion response never blocked.
- Idempotent: `TryMarkConsumedAsync` no-ops on already-consumed items.

### Tests added

- 4 integration tests (`ReadinessConsumptionWiringTests`): completion marks consumed, idempotent, no-item path safe, consumed item absent from suggestions.
- 12 Angular unit tests in `practice-gym.component.spec.ts`: load, empty, error, section rendering, start navigation, labels, existing sections preserved.

### Total test counts

- Architecture: 3 passed
- Unit: 1174 passed
- Integration: 601 passed (was 597 before this phase)
- Angular: 272 passed (was 247 before this phase)
- Total backend: 1778 passed, 0 failed

### TODOs closed

- TODO-014 — TryMarkConsumedAsync wired into ActivitySubmitHandler. Done.
- TODO-015 — Angular Practice Gym suggestion UI implemented. Done.

---

## Phase 10N — Background Replenishment Pipeline, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No Angular source changed.

**What was added:**

- `IReadinessPoolReplenishmentService` / `ReadinessPoolReplenishmentService` — background engine that sweeps expired/reserved items, recovers orphaned generating items, retries failed items, and fills pool shortfalls for all active students.
- `ReadinessPoolReplenishmentOptions` — appsettings-bound configuration (target count, expiry days, retry delay, max items per run, review/scaffold flag).
- `PoolHealthSummary` — health snapshot DTO: ready count, in-flight count, shortfall, `needsReplenishment` flag.
- `ReadinessPoolReplenishmentJob` — Quartz job running every 20 minutes.
- `GET /api/admin/students/{studentId}/readiness-pool/health` — new read-only admin endpoint showing health for both TodayLesson and PracticeGym pools.

**Key rules preserved:**
- `general_english` is the fallback context; workplace is not default.
- B2 students cannot silently receive B1 content as Normal.
- `EnableReviewScaffoldGeneration=false` by default; review/scaffold requires explicit flag AND ledger weakness signals.
- Existing on-demand generation paths unchanged (pool serving deferred to Phase 10O).
- No admin write endpoints added.

**Tests:** 16 new unit + 11 new integration = +27. Total: 1750 passed, 0 failed.

See: `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md`

---

## Phase 10L — CEFR-Aware Activity Routing, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No Angular source changed.

**What was added:**

- `ICurriculumRoutingService` / `CurriculumRoutingService` — pure application-layer routing policy.
  Selects suitable CEFR band and curriculum objectives before every AI generation call.
  Does not call AI. Does not modify student data. Always returns a recommendation.
- `CurriculumRoutingRequest` — input: student context, raw CEFR level, primary skill, source label, resolved goal context, learner preferences, `AllowReviewOrScaffold`.
- `CurriculumRoutingRecommendation` — output: `TargetCefrLevel` (normalized), `CurriculumObjectiveKey/Title`, `ContextTags`, `FocusTags`, `DifficultyBand`, `RoutingReason`, `IsLowerLevelContent`, `Explanation`, `RoutingContextSummary` (for AI prompt injection).
- `CurriculumRoutingRequestFactory` — builds request from `StudentProfile` + `ResolvedLearningGoalContext`.
- CEFR normalization: `B2+` → `B2`, `C1-` → `C1`, null/unknown → `A1`. Does not modify `StudentProfile.CefrLevel`.
- `RoutingReason` enum: `Normal`, `Review`, `Scaffold`, `Remediation`, `Fallback`.

**Routing wired into all 5 generation handlers:**
- `ActivityGetHandler.HandlePatternKeyedAsync` (on-demand + Practice Gym)
- `ExercisePrepareHandler` (Today's Lesson)
- `PracticeGymGenerationJob.MaterializeAsync` (background Practice Gym)
- `ActivityMaterializationJob.MaterializeExerciseAsync` (background lesson batch)
- `LessonBatchGenerationJob.BuildCompactSummaryAsync` (AI lesson planning summary)

**AI prompt integration:**
- `ActivityGenerationContext` extended with `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold`.
- `DbPromptAiContextBuilder` appends routing context before "Return ONLY".
- `cefrLevel` passed to AI is now the routing-normalized level, not raw profile value.

**Core rules enforced:**
- Routing never silently lowers CEFR level. Lower-level content requires `AllowReviewOrScaffold=true` and produces `RoutingReason.Review` + `IsLowerLevelContent=true`.
- Routing never defaults to workplace context. Non-workplace profiles always get `general_english` or goal-specific tags.
- DifficultyPreference maps to DifficultyBand: Gentle→1, Balanced→2, Challenging→4.

**What is NOT implemented (deferred to 10M+):**
`AllowReviewOrScaffold=true` in any handler (built but always false — enablement needs adaptive/ledger signals), session length → candidate count, CEFR-aware format matrix, readiness pools, background replenishment, Practice Gym UI redesign, admin write UI, `StudentProfile.CefrLevel` migration.

**Tests:** 16 new unit tests + 7 new integration tests. Total: 1692 passed (was 1656).

See: `docs/reviews/2026-06-17-phase-10l-cefr-aware-activity-routing-review.md`
See: `docs/architecture/curriculum-routing.md`

---

## Phase 10K — Curriculum Boundary / Level Syllabus Foundation, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No CEFR-aware routing implemented.

**What was added:**

- `CurriculumObjective` domain entity — scoped by CEFR level (A1–C2), primary skill, context tags, focus tags, prerequisite keys, recommended order, difficulty band (1-5), active/reviewable/exam-inspired flags.
- `CefrLevelConstants`, `CurriculumSkillConstants`, `CurriculumContextTagConstants` — canonical validated string sets. `workplace` is one context tag among 13; it is not the default for any objective.
- `CurriculumObjectiveSeeder` — 22 starter objectives across A1/A2/B1/B2, all major skills, multiple learner contexts. **Seed-only-missing** (Phase 10Q): skips any key already in the DB so admin-edited objectives are never overwritten on startup.
- `ICurriculumSyllabusQuery` / `CurriculumSyllabusQueryService` — read-only query service: by CEFR, by CEFR+skill, by CEFR+context tag, by CEFR+focus area, prerequisites, and `GetCandidatesForStudent`. Candidates only — no activity selection.
- `IAdminCurriculumSyllabusQuery` — admin extension returning active and inactive objectives with optional filters (Phase 10Q).
- `CurriculumContextMapper` — maps `ResolvedLearningGoalContext` to curriculum context tags. Null-safe; fallback is `general_english`. Non-workplace profiles never default to `workplace`.
- `CurriculumObjective.ExamplePrompts` / `AdminUpdatedAt` — Phase 10Q audit fields. `AdminUpdate()` sets `AdminUpdatedAt`; seeder uses `UpdateDetails()` which does not.
- `ICurriculumObjectiveWriteService` / `CurriculumObjectiveWriteService` — full admin CRUD with validation (Phase 10Q): slug key, CEFR, skill, context tag, difficulty band 1–5, self-prereq, dangling prereq. `PreviewRoutingAsync` is read-only — no student state mutation.
- Admin API endpoints (Phase 10Q): `GET /objectives`, `GET /objectives/{key}`, `GET /taxonomy`, `POST /objectives`, `PUT /objectives/{key}`, `POST /objectives/{key}/activate`, `POST /objectives/{key}/deactivate`, `POST /routing-preview`.
- Angular `AdminCurriculumComponent` — list with filters, create/edit form, non-mutating routing preview panel (Phase 10Q).
- Migrations: `T50_CurriculumSyllabusFoundation`, `T52_CurriculumObjectiveAdminFields`.

**What is NOT implemented (deferred):**

Exercise format locking by level, `StudentProfile.CefrLevel` type migration.

**TODOS added:** See `TODOS.md` — TODO-001 (plus-levels), TODO-002 (StudentProfile.CefrLevel migration).

---

## Phase 10J-F — Student App Design System & Responsive UI Foundation, completed (2026-06-17)

Frontend-only phase. No product behaviour, API contracts, or backend logic changed.

**Design tokens extended (`styles.css`):**
- `--sp-brand` (solid brand colour, `#5B4BE8`), `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, `--sp-sidebar-w-collapsed`, `--sp-content-max`, `--sp-content-max-desktop`, z-index layer tokens added to `:root`.
- `sp-card-hover` utility class added (transition, hover lift, active scale).
- `sp-pref-chip` / `sp-pref-chip--on` added for all preference chip toggles.
- Duplicate `sp-bottomnav` / `sp-navbtn` removed from global CSS (canonical definition in `student-app-layout.component.css`).

**Profile page:**
- All chip buttons (learning goals, focus areas, session length, difficulty) now use `sp-pref-chip--on` CSS class binding instead of inline `chipStyle()` method.
- `aria-pressed` attribute added to all chip buttons. `data-testid` added per chip.
- `focus-visible` keyboard ring included in chip CSS.
- `chipStyle()` method removed.

**Progress component:**
- All hardcoded hex colors replaced with design tokens (`--sp-success`, `--sp-warn`, `--sp-speaking`, `--sp-writing-ink`, `--sp-success-soft`, `--sp-warn-soft`, `--sp-canvas2`, `--sp-muted`).

**Practice Gym CSS:**
- `var(--sp-primary)` references (non-existent token) replaced with `var(--sp-brand)`.

**Shared student UI components (`src/app/design-system/student/`):**
- `StudentChipComponent` (`sp-chip`) — reusable toggle chip.
- `StudentBadgeComponent` (`sp-badge`) — reusable badge with variant input.

**Tests:** Angular 261 passed. Playwright 187 passed (12 new in `e2e/design-system-10jf.spec.ts`). Backend 1565 passed.

## Phase 10J — Learning Goal Context Resolver, completed (2026-06-17)

`ILearningGoalContextResolver` / `LearningGoalContextResolver` now provides a single consistent priority chain for resolving learning goal context from any `StudentProfile`. All 7 generation and ledger call sites use it. `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` is kept but no longer called externally. Generic fallback is `"general English communication"` — never workplace-biased. `WorkplaceSpecific` flag is derived from keyword detection, not assumed. `LegacyFallbackUsed` flag enables future migration tracking.

## Phase 10X-J — Admin Wrapper Variant API, completed (2026-06-19)

All `sp-admin-*` wrapper components now expose typed variant/size/density/layout inputs. Feature pages request common TailAdmin variations through parameters — not inline class strings. TailAdmin class complexity stays inside wrappers.

**APIs added:** `appearance` on button, `dot`/`purple` on badge, `variant`/`hover`/`loading` on card, `size`/`loading` on stat-card, `variant`/`density`/`selectable`/`stickyHeader` on table, `layout`/`density` on filter-bar and form-field, `size`/`state`/`fullWidth` on input/select/textarea, `size`/`variant`/`showCloseButton` on modal, `side`/`size`/`closeOnBackdrop` on drawer.

**Backward compat:** `variant="ghost"` on button still works (alias to `appearance="ghost" variant="neutral"`). Modal `maxWidth` still works. Existing page code unchanged except two proof-usage calls.

**Tests:** 439 Angular unit tests pass. .NET 1885 pass. Angular build clean.

**Known gaps (tracked):** `sp-admin-input-number`, `sp-admin-select-object`, dashboard mini-table, breadcrumb wrapper.

## Phase 10Students-F-C — Targeted Lifecycle Controls, completed (2026-06-19)

Admin can now Pause, Unpause, and Reactivate students from the student detail page. Each action has a guarded server-side transition, audit log entry, and inline confirm modal. Reactivate reverses archive (sets EmailConfirmed=true, stage=OnboardingRequired). Pause blocks active students. Unpause restores paused students to OnboardingRequired. No database migration required.
