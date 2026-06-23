# Phase 10UI-FIX-8 Review: Notifications, Security, and Integrations Admin Polish

**Date:** 2026-06-23
**Sprint / feature:** Phase 10 UI polish — admin gap closure
**Commit:** ebde2a0
**Baseline tests:** 1074/1074 (after FIX-7)
**Final tests:** 1086/1086

---

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-notifications/admin-notifications.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-security/admin-security.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-integrations/admin-integrations.component.spec.ts`
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` (read-only, confirmed no aggregate endpoint)

---

## Findings grouped by priority

### P0 — None. No blocking issues found.

### P1 — Addressed in this phase

1. **SMS labelled as production-ready by omission.** The notifications page showed an SMS channel card and config section with no indication that SMS is deferred, no provider is connected, and sending is disabled regardless of the toggle. Fixed with a `Foundation only` warning badge on the channel status card and a `sp-admin-alert variant="warning"` on the config card explaining the full situation.

2. **Security page had no record of deferred capabilities.** Admins had no way to know which security features are planned but not yet active. Fixed with a "Deferred security capabilities" `sp-admin-card` listing seven items: MFA/TOTP, enterprise SSO, distributed/Redis rate limiting, captcha, CSP/HSTS, SMS security notifications, and admin session management. Each row uses an appropriate `sp-admin-badge` tone (`warning` = deferred, `neutral` = deployment-dependent or planned).

3. **Integrations page had no readiness pool section.** Per-student pool health exists at the backend (`/api/admin/students/{id}/readiness-pool/health`), but no aggregate endpoint exists. The integrations page now shows a placeholder card with a `neutral` badge "Backend not available yet" and a note directing admins to the student detail page for per-student data. No fake production data is shown.

### P2 — Notes

- The deferred card on the security page is placed outside the `@if (activeTab === 'overview')` block intentionally so it is always visible regardless of active tab.
- `sp-admin-alert` requires `variant` (not `tone`) and has no `dismissible` input — confirmed consistent with FIX-6 and FIX-7 findings.
- One test initially used the string "Distributed rate limiting" which did not match the actual HTML text "Distributed / Redis rate limiting". Fixed to assert on "Distributed" only, which is stable regardless of the sub-label wording.

---

## Decisions made

| Decision | Rationale |
|---|---|
| Show warning alert on SMS config, not just a badge | A badge alone does not explain why sending is disabled or what is deferred. An alert with prose is clearer for an admin who may not know the project status. |
| Deferred card outside tab guard | Deferred capability awareness is relevant regardless of which security tab is active. |
| Placeholder only for integrations pool aggregate | Backend aggregate endpoint does not exist. Showing "Backend not available yet" is honest and defers implementation to the correct phase. |
| No SMS provider, phone collection, backend changes, migrations | Out of scope per the FIX-8 brief. |

---

## Tests produced

### admin-notifications.component.spec.ts (3 new)
- SMS channel status card shows "Foundation only" badge
- "Foundation only" badge does not appear for In-App or Email channel cards
- SMS config card shows warning alert about provider not connected

### admin-security.component.spec.ts (6 new)
- Deferred capabilities card renders
- Card contains MFA text
- Card contains enterprise SSO text
- Card contains distributed rate limiting text
- Card contains CSP or deployment hardening text
- No secrets (API keys, JWT_KEY) are displayed

### admin-integrations.component.spec.ts (3 new)
- Readiness pool aggregate card title renders
- "Backend not available yet" renders in card
- Existing batches and storage tests still pass alongside pool card

---

## Risks / unresolved questions

- When an SMS provider is eventually integrated, the "Foundation only" badge and warning alert must be removed or replaced with production-ready status. A TODO comment in the HTML would help, but the brief prohibits adding TODOs in this phase. Track in backlog.
- The deferred security capabilities card will need updating as features ship. There is no automated link between the card content and actual feature state.

---

## Final verdict

**Complete.** All three admin UI gaps are addressed. 12 new tests pass. 1086/1086 total. No fake data. No backend changes. No scope creep.

## Next recommended action

Continue Phase 10 UI polish with the next identified gap, or move to the next sprint milestone. Update `docs/sprints/current-sprint.md` and `TODOS.md` to mark FIX-8 complete.

---

## Documentation impact

- Docs reviewed: `docs/architecture/README.md` (referenced, not changed)
- Docs updated: this review doc created
- Docs intentionally not updated: `TODOS.md`, `docs/sprints/current-sprint.md` — to be updated as part of sprint close
- Reason: UI polish only; no architecture, API contract, or model changes
