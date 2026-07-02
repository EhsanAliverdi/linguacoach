# "View As User" — Admin Impersonation

**Status:** Deferred — not scheduled. Not fully designed at implementation
detail; captures the agreed approach and constraints for a future session
to pick up.

**Date:** 2026-07-03

**Related:** Phase 21A precursor, `docs/roadmap/road-map.md`

---

## Problem

Today the only login roles are Admin and Student, each requiring its own
session. When testing or supporting the product, there's no clean way for
an Admin to make a change (e.g. edit a student's plan, adjust curriculum)
and immediately see its effect from that student's point of view without
maintaining two separate logins.

## Interim Workaround (in use today, no code required)

JWTs are stored in `localStorage`/`sessionStorage`, scoped per browser
context. Use two isolated browser contexts — a second browser, an
incognito/private window, or a separate browser profile — to hold an Admin
session and a Student session simultaneously. This is standard practice for
manual cross-role QA and requires zero implementation. **This is the
current approach** while the feature below stays deferred.

## Standard Enterprise Pattern

Admin impersonation ("log in as user", "view as") is a common SaaS pattern
(Stripe, Zendesk, Intercom, Auth0 all implement variants). The Admin
initiates a scoped, audited, time-limited session as a target user without
ever needing that user's password, and can return to their own identity at
any time.

## Proposed Approach

- `POST /api/admin/impersonate/{studentId}` (Admin-only) issues a
  short-lived JWT carrying the target student's claims (so downstream
  authorization still treats the session as `Student`), plus an
  `impersonatedBy` claim holding the real admin's user id.
- Frontend stores the impersonation token alongside a `viewingAs` UI state.
- A persistent, app-wide banner — "Viewing as {student} — Return to Admin"
  — is shown for the duration of the impersonated session.
- "Return to Admin" is a one-click action that discards the impersonation
  token and restores the original admin session (no re-login required).

## Security Requirements

- **Admin-only to initiate.** No other role may start an impersonation
  session.
- **Fully audited.** Every impersonation start and end is recorded as an
  `AuthSecurityEventRecord`, with new `AuthEventType` values (e.g.
  `ImpersonationStarted`, `ImpersonationEnded`), including the acting
  admin's id, the target student's id, and timestamps.
- **Short-lived tokens.** Impersonation tokens expire quickly (e.g. 15-30
  minutes), independent of normal session length.
- **No privilege escalation.** The impersonation token is still validated
  and authorized as `Student` downstream — it never grants Admin
  capabilities while impersonating. It only carries the extra
  `impersonatedBy` tag for audit purposes.
- **No nested impersonation.** An admin already impersonating cannot start
  a second impersonation session without first returning to their own
  identity.
- **Student-target-only, at least initially.** Impersonating another Admin
  or (once it exists) a Teacher is out of scope/disallowed. Only Student
  accounts may be impersonation targets.

## Explicitly Not Designed Yet

This doc captures the agreed shape and constraints, not full implementation
detail (no concrete file list, no DTO/endpoint signatures beyond the sketch
above, no frontend component breakdown). A future session should treat this
as a starting point and produce a full implementation plan — following the
same pattern used for `docs/architecture/teacher-role-and-read-access.md`
— before writing code.

## Documentation Impact When Implemented

- `docs/roadmap/road-map.md` — move from "Deferred" to in-progress/done.
- `docs/backlog/product-backlog.md` — update the corresponding backlog
  entry.
- This doc should be expanded in place (or superseded by a more detailed
  doc) with the full implementation plan once scheduled.

## Open Questions (to resolve when scheduled)

1. Should impersonation sessions be visible to the target student (e.g. a
   notice that an admin is viewing their account), or fully silent?
2. Should there be a maximum number of concurrent impersonation sessions
   system-wide, or per-admin?
3. Does the audit trail need to be surfaced in the admin UI (e.g. an
   "Impersonation history" panel), or is the raw `AuthSecurityEventRecord`
   log sufficient for this first version?
