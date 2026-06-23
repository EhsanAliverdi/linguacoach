# Phase 10Auth-F-6 — Admin Security Settings UI and Read-Only Auth Visibility

**Date:** 2026-06-23
**Related sprint:** 10Auth
**Phase:** 10Auth-F-6

---

## Files Added or Modified

### Backend

- `src/LinguaCoach.Application/Admin/SecuritySettingsQueries.cs` — read model records + `IAdminSecurityHandler` interface
- `src/LinguaCoach.Infrastructure/Admin/AdminSecurityHandler.cs` — reads live config (IdentityOptions, IConfiguration, GoogleExternalLoginOptions); builds safe settings snapshot; no secrets returned
- `src/LinguaCoach.Api/Controllers/AdminSecurityController.cs` — `GET /api/admin/security/settings`, `GET /api/admin/security/auth-events`; Admin-role only
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `IAdminSecurityHandler`

### Frontend

- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — appended security settings interfaces + `AdminAuthEventItem` + `AdminAuthEventListQuery`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — `getSecuritySettings()`, `listSecurityAuthEvents()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.ts` — new standalone component with Signals
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.html` — tabbed page: overview (policy cards) + auth events (table + pagination)
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.spec.ts` — 16 unit tests
- `src/LinguaCoach.Web/src/app/app.routes.ts` — lazy-loaded `/admin/security` route
- `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.html` — Security nav item (mobile drawer + desktop sidebar, System section)

---

## Findings

### Security constraints verified

- `AdminSecurityHandler` never reads or returns the JWT signing key.
- Google `ClientSecret` only returned as `ClientSecretConfigured: bool`.
- Google `ClientId` only returned as `ClientIdConfigured: bool`.
- Rate limit summary is hardcoded to match `Program.cs` registrations — ASP.NET Core rate limiter has no queryable policy registry at runtime.
- No write surface added in this phase; endpoint is read-only.
- Endpoint is `[Authorize(Roles = "Admin")]` — students cannot access.

### Auth events endpoint

- Reuses existing `IAdminAuthEventHandler` from F-2; no code duplication.
- Page size clamped to 100 in controller.
- Co-located under `/api/admin/security/` namespace for discoverability.

### Frontend

- `sp-admin-table` uses `ng-content` pattern (not `[columns]/[rows]` inputs) — template written accordingly.
- Filter bindings use `[(ngModel)]` matching existing admin pages.
- All field names verified against `AdminAuthEventItem` model (`emailOrUserName`, `failureReasonCode`, `occurredAtUtc`).

---

## Decisions Made

| Decision | Rationale |
|---|---|
| No PUT endpoint for Google config | Out of scope; config editing deferred to later phase |
| Rate limit summary hardcoded | ASP.NET Core exposes no runtime policy registry |
| Auth events alias under `/security/` | Groups related audit surface with security namespace |
| Security nav item in System section | Consistent with Integrations + Diagnostics placement |

---

## Implementation Tasks Produced

None — all tasks in this phase are complete.

---

## Risks / Unresolved

- If rate limit policies in `Program.cs` change, `AdminSecurityHandler` hardcoded summary will drift. A future phase should either query a registered summary service or test the values against the live registrations.

---

## Final Verdict

Phase 10Auth-F-6 complete. Backend and frontend fully implemented. No new migrations. No secrets exposed.

## Next Recommended Action

Run backend + frontend gates, then commit with message `security: add admin security settings page`.
