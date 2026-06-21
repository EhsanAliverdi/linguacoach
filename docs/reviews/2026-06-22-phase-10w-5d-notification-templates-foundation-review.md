# Phase 10W-5D — Notification Templates Foundation Review

**Date:** 2026-06-22
**Sprint/Feature:** Phase 10W-5D — Notification Templates Foundation
**Related sprint:** Enterprise Notification Platform (Phase 10W)
**Prior phase:** 10W-5C (commit 51d0f78)

---

## Files Changed

### Backend — New
- `src/LinguaCoach.Domain/Entities/NotificationTemplate.cs` — entity with `Create`, `Update`, `Deactivate`, `Activate` domain methods; validates Email→Subject, InApp→Title
- `src/LinguaCoach.Application/Notifications/INotificationTemplateRenderer.cs` — renderer interface + `TemplateRenderResult` record
- `src/LinguaCoach.Application/Admin/AdminTemplateQueries.cs` — DTOs, commands, interface `IAdminTemplateHandler`
- `src/LinguaCoach.Infrastructure/Notifications/SimpleNotificationTemplateRenderer.cs` — regex-based `{{VarName}}` replacement; missing vars left visible and reported
- `src/LinguaCoach.Infrastructure/Admin/AdminTemplateHandler.cs` — full CRUD + preview implementation
- `src/LinguaCoach.Persistence/Configurations/NotificationTemplateConfiguration.cs` — EF table config
- `src/LinguaCoach.Persistence/Migrations/*T55_NotificationTemplates*` — migration
- `src/LinguaCoach.Persistence/Seed/NotificationTemplateSeeder.cs` — 4 default templates (idempotent, admin-safe)
- `tests/LinguaCoach.IntegrationTests/Api/AdminNotificationTemplateEndpointTests.cs` — 23 integration tests

### Backend — Modified
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — added `DbSet<NotificationTemplate>`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `IAdminTemplateHandler` + `INotificationTemplateRenderer`
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — injected `IAdminTemplateHandler`; 5 new endpoints + 3 request records
- `src/LinguaCoach.Api/Program.cs` — wired `NotificationTemplateSeeder.SeedAsync`

### Frontend — Modified
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — 5 new TS interfaces
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — 6 new service methods
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts` — `templates` tab state + 10 methods
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html` — Templates tab button + list table + create/edit slide-over + preview panel
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` — 17 new template tab tests

### Docs — New/Modified
- `TODOS.md` — 10W-5D marked complete; notification preferences deferred as `TODO-10W-PREFS`
- `docs/reviews/2026-06-22-phase-10w-5d-notification-templates-foundation-review.md` — this file

---

## Findings

### Architecture — Clean

- Domain entity enforces: Email→Subject required, InApp→Title required at `Create` and `Update`. No validation bypasses.
- Renderer is pure (`INotificationTemplateRenderer` → `SimpleNotificationTemplateRenderer`): no external calls, no DB access, registered as `Singleton` (stateless regex).
- Handler (`AdminTemplateHandler`) is `Scoped` (owns DbContext). Preview method reads from DB, renders in memory — does not write any outbox row.
- Duplicate active key+channel guard is enforced in `CreateTemplateAsync` before insert, returns 409 Conflict.
- `PagedResponse<T>` signature matched correctly (`TotalPages` included).

### Security — No issues

- No template content is executed as code. Renderer uses compiled `Regex.Replace` with a 1-second timeout.
- Missing variables are left as visible `{{VarName}}` placeholder — not silently dropped, not errored. Reported in `MissingVariables` list.
- Preview endpoint returns rendered strings only. No `Notification` or `NotificationOutboxItem` is created. Verified by `PreviewTemplate_DoesNotQueueNotification` integration test.
- No secrets exposed through template content (templates contain only static text and placeholders).

### Seeder — Admin-safe

`NotificationTemplateSeeder` checks for existing active records by `TemplateKey + Channel` before inserting. Never overwrites admin-edited templates. Seeds 4 defaults:
1. `account.password_reset` / Email — password reset link email
2. `account.student_created` / Email — welcome email
3. `admin.manual_notification` / InApp
4. `admin.manual_notification` / Email

Seeder only runs in non-Testing environments (standard pattern).

### Reset-password integration — Deferred (safe)

Reset-link email currently uses hard-coded subject/body in `PasswordResetHandler`. Swapping to `account.password_reset` template would require `INotificationTemplateRenderer` injection into `PasswordResetHandler` and a seeded template lookup. Deferred as `TODO-10W-5D-RESET-INTEGRATION` — the existing flow is unchanged and correct.

### Scope respected

- No SMS provider added. SMS channel can have templates but SMS delivery remains disabled.
- No user notification preferences UI added (deferred as `TODO-10W-PREFS`).
- Existing Notifications / Outbox / Config / Send tabs unchanged.
- No AI pricing, usage governance, or unrelated admin UI touched.

---

## Test Results

| Gate | Before | After |
|------|--------|-------|
| .NET tests | 2203 | 2225 (+22 integration) |
| Angular unit tests | 982 | 999 (+17 template) |
| Angular build (prod) | PASS | PASS |

---

## Decisions Made

1. **Missing variables → leave placeholder visible, report in list.** Admins can see what's unresolved; no silent data loss.
2. **Duplicate active guard at application layer.** Returns 409 Conflict. DB unique index deferred (noted as `TODO-10W-5D-UNIQUE-CONSTRAINT`).
3. **Preview does not call send.** Verified by integration test comparing outbox count before/after.
4. **SMS templates allowed in DB.** SMS delivery still blocked in `NotificationDispatchService`; having a template doesn't enable delivery.
5. **Reset-password email integration deferred.** Low-risk to existing flow; template foundation is ready.

---

## Risks / Unresolved Questions

- `TODO-10W-5D-RESET-INTEGRATION`: Wire `PasswordResetHandler` to use `account.password_reset` Email template.
- `TODO-10W-5D-UNIQUE-CONSTRAINT`: Optional DB unique index on `(template_key, channel)` for active templates.
- `TODO-10W-PREFS`: Notification preferences (per-user per-channel opt-out) — full separate phase.
- Renderer timeout is 1 second per field (Regex compiled). Malformed regex-like input in template body is safe (no user-supplied regex patterns).

---

## Final Verdict

**PASS.** Phase 10W-5D complete. Template entity, renderer, CRUD + preview endpoints, seeder, admin Templates tab, and tests all in place. All scope constraints respected. 2225 .NET / 999 Angular tests pass.

---

## Next Recommended Action

Phase 10W-5D-RESET-INTEGRATION (wire reset-link email to template) or Phase 10W-PREFS (notification preferences) or Phase 10W-6 (SMS provider).
