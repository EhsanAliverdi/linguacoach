# Engineering Review — Phase 10UI-REDESIGN-FINAL: Admin UI Reference Alignment Closure Audit

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-FINAL
**Related phases:** REDESIGN-1 through REDESIGN-8

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-FINAL entry

---

## Scope

Route-by-route closure audit of the full admin UI redesign epic (REDESIGN-1 through REDESIGN-8). Covers all admin routes for:

- Reference design alignment
- `sp-admin-*` component usage correctness
- Real data vs. placeholder honesty
- Fake/static production data
- Secrets/sensitive data
- Backend capability coverage

One structural bug (notifications header position) was found and corrected as a tiny safe fix.

---

## Route Alignment Matrix

| Route | Component | Header outside body | KPI strip | CSS tokens | (clicked) on sp-admin-button | Ref alignment | Status |
|---|---|---|---|---|---|---|---|
| `/admin` | AdminDashboardComponent | n/a (inline) | Yes — 5 tiles | Yes | n/a | dashboard.jsx | Complete |
| `/admin/students` | AdminStudentsComponent | n/a (inline) | Yes — 4 tiles | Yes | Yes | students.jsx | Complete |
| `/admin/students/:id` | AdminStudentDetailComponent | n/a (inline) | Yes — 4 tiles | Yes | n/a | (no reference) | Complete |
| `/admin/create-student` | CreateStudentComponent | Yes (HTML template) | n/a | Yes | n/a | create-student.jsx | Complete |
| `/admin/ai-config` | AdminAiConfigComponent | n/a (inline) | Yes — 4 tiles | Partial* | Partial* | ai-config.jsx | Complete |
| `/admin/prompts` | AdminPromptsComponent | n/a (inline) | Yes — 4 tiles | Yes | n/a | prompts.jsx | Complete |
| `/admin/usage` | AdminAiUsageComponent | n/a (inline) | Yes — 4 tiles | Yes | n/a | ai-usage.jsx | Complete |
| `/admin/usage-policies` | AdminUsagePoliciesComponent | n/a (inline) | n/a | Yes | n/a | (no reference) | Complete |
| `/admin/exercise-types` | AdminExerciseTypesComponent | n/a (inline) | Yes — 4 tiles | Yes | n/a | exercise-types.jsx | Complete |
| `/admin/curriculum` | AdminCurriculumComponent | n/a (inline) | Yes — 4 tiles | Yes | n/a | curriculum.jsx | Complete |
| `/admin/notifications` | AdminNotificationsComponent | **Fixed** (was inside body) | Yes — 4 tiles | Yes (main); Tailwind in slide-overs† | Yes | notifications.jsx | Complete |
| `/admin/integrations` | AdminIntegrationsComponent | Yes (HTML template) | Yes — 4 tiles | Yes (main); 3 minor literals‡ | Yes | integrations.jsx | Complete |
| `/admin/diagnostics` | AdminDiagnosticsComponent | Yes (HTML template) | Yes — 4 tiles | Yes | Yes | diagnostics.jsx | Complete |
| `/admin/security` | AdminSecurityComponent | Yes (HTML template) | Yes — 4 tiles | Yes | n/a | (no reference) | Complete |
| `/admin/careers` | — | — | — | — | — | — | Redirects to /admin/curriculum |

*AI Config slide-over for provider detail card uses some raw Tailwind class composition for dynamic border/background — functional, not a blocking gap.

†Notifications slide-over form fields (template create/edit, send notification) retain 29 Tailwind color literals. These are in non-visible slide-over UI, not main page content. Deferred — not a blocking gap.

‡Integrations: 3 minor Tailwind literals (`text-gray-400`, `text-gray-500`) in static text paragraphs. Non-blocking.

---

## Component / Design-System Usage Audit

| Component | Used correctly across all pages |
|---|---|
| `sp-admin-page-header` | Yes — all pages have header outside page-body after FINAL fix |
| `sp-admin-page-body` | Yes |
| `sp-admin-kpi-card` | Yes — all redesigned pages use `sp-admin-kpi-card`, not deprecated `sp-admin-stat-card` |
| `sp-admin-card` | Yes |
| `sp-admin-badge` | Yes |
| `sp-admin-button (clicked)` | Yes — all `sp-admin-button` elements use `(clicked)` not `(click)` |
| `sp-admin-table` | Yes |
| `sp-admin-filter-bar` | Yes |
| `sp-admin-alert` | Yes |
| `sp-admin-status-card` | Yes (diagnostics only) |
| `sp-admin-status-grid` | Yes (diagnostics only) |
| `sp-admin-stat-card` (deprecated) | **None remaining** — all migrated to `sp-admin-kpi-card` |

---

## Fake / Static Data Audit

| Section | Finding |
|---|---|
| All KPI strips | Real data from backend signals. No invented values. |
| Dashboard hero KPI | Real data from `getDashboardStats()`. |
| Dashboard unavailable sections | Explicit "Backend not available yet" placeholder cards — not fake filled values. |
| AI Usage unavailable sections | Explicit "Backend not available yet" labels. |
| AI Config rate limits card | "Backend not available yet" label. No fake usage numbers. |
| Integrations readiness pool | "Readiness pool generation is a planned background job" placeholder. No fake job counts. |
| Integrations webhook/Slack/analytics cards | "Backend not available yet". No fake webhook URLs. |
| `admin-data.jsx` reference mock data | Not imported into Angular runtime. Reference design only. |
| `'fake'` AI provider option | Legitimate product feature — disables AI for a category. Not fake test data. |

No fake production data found anywhere in admin templates.

---

## Secrets / Sensitive Data Audit

| Secret | Handling |
|---|---|
| JWT signing key (`JWT_KEY`) | Never displayed. Note in Security JWT card: "JWT signing key is never displayed." |
| Google client secret | Never displayed. Only `clientSecretConfigured` boolean badge shown. Note added. |
| Google client ID | Never displayed. Only `clientIdConfigured` boolean badge shown. |
| OpenAI / Anthropic / Gemini API keys | Never displayed. Only `keyConfigured` boolean badge shown in AI Config. |
| SMTP password | Not displayed in Integrations. Notifications slide-over has password input (type=password) for entering new value — this is a form field, not a display. |
| SMS API key | Same as SMTP: input (type=password) for entering new key — form field, not display. |
| Database connection string | Never displayed. Only reachable/unreachable status shown in Diagnostics. |
| JWT tokens / refresh tokens | Never displayed. |
| Reset tokens / auth codes | Never displayed. |

All secrets either never displayed or replaced with boolean configured/not-configured badges.

---

## Backend Capability Coverage Audit

| Admin route | Backend endpoint(s) | Status |
|---|---|---|
| Dashboard | `GET /api/admin/dashboard/stats`, `GET /api/admin/students?page=1&pageSize=5`, AI provider status | Real |
| Students | `GET /api/admin/students`, `GET /api/admin/students/stats`, `PUT/POST/DELETE` student actions | Real |
| Student detail | `GET /api/admin/students/:id`, `GET /api/admin/students/:id/activities` | Real |
| Create student | `POST /api/admin/students` | Real |
| AI Config | `GET/PUT /api/admin/ai-config` per category | Real |
| Prompts | `GET/POST/PUT/DELETE /api/admin/prompts` | Real |
| AI Usage | `GET /api/admin/ai-usage` with filters | Real — activities/engagement trend endpoint deferred |
| Usage Policies | `GET/PUT /api/admin/usage-policies` | Real |
| Exercise Types | `GET/PUT /api/admin/exercise-types` | Real |
| Curriculum | `GET/POST/PUT/DELETE /api/admin/curriculum` | Real |
| Notifications | `GET/PUT /api/admin/notifications/config`, `GET /api/admin/notifications/outbox` | Real — SMS provider, Slack/Webhook integrations deferred |
| Integrations | `GET /api/admin/integrations/status`, `GET /api/admin/background-jobs` | Real — webhook/Slack/analytics/admin-api backend deferred |
| Diagnostics | `GET /api/admin/diagnostics/status`, `GET /api/admin/diagnostics/events` | Real |
| Security | `GET /api/admin/security/settings`, `GET /api/admin/security/auth-events` | Real |

All deferred capabilities are clearly labelled in the UI with "Backend not available yet" or "Foundation only" labels.

---

## Fix Applied in This Phase

### Notifications `sp-admin-page-header` position

**Before:** `sp-admin-page-header` was the first element *inside* `<sp-admin-page-body>`, violating the shared admin layout contract.

**After:** `sp-admin-page-header` is rendered *before* `<sp-admin-page-body>`, matching every other admin page.

This is the same structural fix applied to Security in REDESIGN-8. The notifications page was redesigned in REDESIGN-7 before the structural bug was identified.

**Files changed:**
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html` — header moved outside page-body
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts` — 2 new tests added: `renders sp-admin-page-header with title Notifications`, `page header is not nested inside sp-admin-page-body`

---

## Deferred Items (Not Blocking)

| Item | Location | Decision |
|---|---|---|
| 29 Tailwind color literals in slide-over forms | Notifications component | Deferred — slide-over forms, not main page content |
| 3 minor Tailwind literals in static text | Integrations component | Deferred — non-interactive text only |
| AI Config provider card dynamic Tailwind composition | AI Config component | Deferred — functional, complex to CSS-tokenise |
| Global error/warning aggregate for Diagnostics KPI | Backend | No endpoint exists — labels say "(loaded)" |
| Activities/engagement trend for AI Usage | Backend | Deferred — explicit "Backend not available yet" card |
| SMS provider integration | Notifications | Deferred — explicit "Foundation only" label |
| Webhook / Slack / Analytics backend | Integrations | Deferred — explicit "Backend not available yet" |

---

## Gates

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| Production build (`npm run build -- --configuration production`) | Clean (CSS `&` empty selector warnings only — pre-existing, non-blocking) |
| Full test suite (`npm test -- --watch=false --browsers=ChromeHeadless`) | 1253/1253 PASS |
| Backend build | No backend changes |
| Playwright | No E2E specs for notifications/integrations/diagnostics/security |

---

## Decisions Made

1. `sp-admin-page-header` outside `sp-admin-page-body` is the canonical admin layout contract. Any future page must follow this pattern.
2. Slide-over form Tailwind literals are not blocking — they are in non-main-content areas and represent acceptable remaining debt.
3. The `'fake'` AI provider option is a legitimate feature, not fake data.
4. All KPI strips across all 14 admin routes derive from real backend data or show explicit not-implemented labels. No invented values anywhere.
5. Secrets audit is clean — no API keys, JWT keys, client secrets, or raw credentials are displayed anywhere in the admin UI.

---

## Final Verdict

**Complete.** All admin routes are aligned with the SpeakPath reference design (where a reference exists). All `sp-admin-*` components are used correctly. No fake production data. No secrets displayed. 1253/1253 tests pass. Production build clean.

The admin UI redesign epic (REDESIGN-1 through REDESIGN-8 + FINAL) is closed.

---

## Next Recommended Action

Admin UI redesign epic is complete. No further admin redesign phases needed.

Remaining admin UI debt (slide-over form Tailwind literals, minor Integrations text literals) can be addressed as P3 cleanup items if desired — they are not blocking release.
