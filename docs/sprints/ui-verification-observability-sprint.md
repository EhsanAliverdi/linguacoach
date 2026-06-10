---
status: historical
---
# UI Verification & Observability Sprint

**Date**: 2026-06-05
**Status**: In progress
**Branch**: main

---

## Sprint Goal

Two goals:

1. **Verify the actual UI works end-to-end** — not only backend tests pass.
2. **Add proper logging/observability** — correlation IDs, structured logs, configurable log levels, admin diagnostics page.

The student should be able to navigate every critical flow without breaking.
The developer should be able to trace any bug from frontend error → correlation ID → backend log.

---

## Current Concern

Backend tests pass (320 unit/integration + 16 Playwright). However:

- UI may have broken state in flows added across multiple sprints
- No correlation IDs → impossible to match frontend errors to backend logs
- Logs are unstructured strings → hard to search in production
- No admin diagnostics page → debugging requires server access
- Log levels are not configurable from environment → noisy in dev, blind in prod

---

## Pages That Must Be Verified

### Public
- [ ] Landing page — loads, hero, CTA
- [ ] Login page — loads, invalid login error, valid admin login, valid student login
- [ ] Change password — loads, validates, redirects after success
- [ ] Sign out — clears session, redirects to login

### Admin
- [ ] Admin dashboard — KPI cards, quick actions, recent students table
- [ ] Admin sidebar — all links work, active state correct
- [ ] Admin mobile drawer — opens/closes, nav items work
- [ ] Create student — form loads, validation, success + credentials displayed
- [ ] Student list — table loads, real data
- [ ] AI config — provider/model/key status loads
- [ ] Prompts page — list loads, create version works
- [ ] Careers page — list loads
- [ ] Diagnostics page (new) — status, events, filters

### Student
- [ ] Dashboard — stat cards show real data, coach card shows focus area or honest placeholder
- [ ] /my-path — module cards, progress bar, score chip, ready-to-complete notice
- [ ] /activity — learning state, writing state, feedback state
- [ ] Activity feedback — diff/change list renders, Persian block, mini lesson, improved version
- [ ] Retry/improve flow — "Improve my answer" pre-fills textarea, second attempt works, score comparison
- [ ] /progress — loads without broken state
- [ ] /profile — loads, data correct

---

## Critical User Journeys

1. Admin login → create student → copy credentials
2. Student first login → must change password → onboarding → dashboard
3. Student → start activity → submit → feedback → improve → resubmit
4. Student → /my-path → module cards → continue practising
5. Admin → diagnostics page → view system status → search correlation ID

---

## Logging Architecture

### Correlation ID flow

```
Browser request
  → Angular interceptor adds X-Correlation-ID (UUID) to request
  → Backend CorrelationIdMiddleware reads or generates ID
  → ID added to response header X-Correlation-ID
  → ID added to ILogger scope for all request-scoped logs
  → API error response includes correlationId field
  → Angular interceptor reads correlationId from error response
  → Friendly error shown: "Something went wrong. Reference: abc-123"
  → Admin searches abc-123 in /admin/diagnostics → sees matching events
```

### Log levels (configurable from env)

| Environment Variable | Default | Purpose |
|---|---|---|
| `LOG_LEVEL` | `Information` | Root log level |
| `LOG_LEVEL_MICROSOFT` | `Warning` | ASP.NET/Identity/EF noise |
| `LOG_LEVEL_EFCORE` | `Warning` | EF Core SQL query logs |
| `ENABLE_DETAILED_AI_LOGS` | `false` | Full AI request/response bodies |
| `ENABLE_REQUEST_BODY_LOGS` | `false` | Request body logging |

### In-memory diagnostic event buffer

- `DiagnosticEventBuffer` — thread-safe ring buffer, default capacity 500
- `DiagnosticLoggerProvider` — ILoggerProvider that writes filtered events to the buffer
- Enabled by env var `ENABLE_ADMIN_DIAGNOSTIC_EVENTS=true` (default true in Development)
- Events never contain: passwords, API keys, JWT tokens, full submitted text
- Not for audit/compliance — development/staging debugging tool only

### Structured log categories

Each significant action uses a semantic event with named properties:
- `{UserId}`, `{Email}`, `{Role}` — identity fields
- `{ActivityId}`, `{AttemptId}`, `{ModuleId}`, `{PathId}` — entity IDs
- `{CorrelationId}` — request correlation
- `{ElapsedMs}` — duration
- `{Score}`, `{FocusArea}` — domain values

---

## Admin Diagnostics Page (`/admin/diagnostics`)

### System Status section
- Environment (Development/Production)
- App version
- Server time (UTC)
- Log level
- Database reachable (boolean)
- AI provider configured (boolean) + active provider/model
- Uptime

### Recent Events section
- Last N events from in-memory buffer
- Columns: timestamp, level, category, message, correlationId, userId, path
- Level colour coding: Info=blue, Warn=amber, Error=red, Debug=grey
- Filters: level, category, correlation ID text search
- Auto-refresh toggle (every 5s)

### Correlation ID lookup
- Search box → filters events list to matching correlationId
- Used when user reports error with reference number

---

## Test Plan

### Backend
- Correlation ID generated if missing, preserved if present
- Response includes `X-Correlation-ID` header
- Diagnostics `/status` — admin only (401 if no auth, 403 if student)
- Diagnostics `/events` — admin only
- AI key never returned in diagnostics response
- In-memory buffer stores events up to capacity
- Buffer rolls over oldest when at capacity

### Frontend
- Angular interceptor attaches X-Correlation-ID to every request
- API error response shows friendly message with reference ID
- Admin diagnostics page renders status section
- Admin diagnostics page renders events table

### Playwright
- Admin diagnostics page opens, status card visible
- Full student journey still passes (no regression)
- No unexpected console errors on dashboard, activity, my-path pages

---

## In Scope

- [x] Sprint documentation
- [ ] CorrelationIdMiddleware
- [ ] GlobalExceptionMiddleware (safe error response)
- [ ] Safe request logging middleware
- [ ] Structured logs in key handlers
- [ ] Log level env var configuration
- [ ] DiagnosticEventBuffer + DiagnosticLoggerProvider
- [ ] GET /api/admin/diagnostics/status
- [ ] GET /api/admin/diagnostics/events
- [ ] Angular diagnostics page (/admin/diagnostics)
- [ ] Angular correlation ID interceptor (extend existing auth interceptor)
- [ ] Friendly error display with reference ID
- [ ] Backend tests
- [ ] Playwright tests
- [ ] All checks green

---

## Out of Scope

- Database log storage (option C) — deferred
- Real-time log streaming (WebSocket/SSE)
- Frontend error reporting to backend endpoint (deferred)
- New product features
- UI redesign

---

## Known Issues Found

*(filled in during verification)*

---

## Fixed Issues

*(filled in during verification)*

---

## Deferred Issues

*(filled in during verification)*

---

## Implementation Tasks (ordered)

1. Sprint doc ✓
2. CorrelationIdMiddleware + CorrelationIdService
3. GlobalExceptionMiddleware
4. SafeRequestLoggingMiddleware
5. Structured log calls in: LoginHandler, ChangePasswordHandler, CreateStudentHandler, ActivityGetHandler, ActivitySubmitHandler, DashboardQueryHandler, LearningPathQueryHandler, CompleteModuleHandler
6. Log level env var mapping in Program.cs + appsettings
7. DiagnosticEventBuffer + DiagnosticLoggerProvider
8. DiagnosticsController (admin-only)
9. Angular admin diagnostics component + route + sidebar nav
10. Angular correlation interceptor (extend auth.interceptor.ts)
11. Backend tests
12. Playwright tests
13. Final checks

---

## API Reference

### GET /api/admin/diagnostics/status

Requires: Admin JWT

```json
{
  "environment": "Development",
  "version": "1.0.0",
  "serverTimeUtc": "2026-06-05T00:00:00Z",
  "logLevel": "Information",
  "diagnosticEventsEnabled": true,
  "uptimeSeconds": 3600,
  "database": { "reachable": true },
  "ai": { "providerConfigured": true, "activeProvider": "openai", "activeModel": "gpt-4o-mini" }
}
```

### GET /api/admin/diagnostics/events?level=&category=&correlationId=&q=&limit=100

Requires: Admin JWT

```json
{
  "total": 42,
  "items": [
    {
      "timestampUtc": "2026-06-05T00:00:00Z",
      "level": "Information",
      "category": "Activity",
      "message": "AI evaluation succeeded for activity {ActivityId}",
      "correlationId": "abc-123",
      "userId": "...",
      "path": "/api/activity/123/attempt",
      "statusCode": null,
      "elapsedMs": null
    }
  ]
}
```

### Error response format (all endpoints, on unhandled exception)

```json
{
  "message": "Something went wrong. Please try again.",
  "correlationId": "abc-123"
}
```
