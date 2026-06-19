# Phase 10Students-F-0: Enterprise Student Management Gap Check

**Date:** 2026-06-19
**Sprint/Feature:** Phase 10Students — Enterprise Student Management
**Auditor:** Claude Code (automated audit + human review)
**Related Sprint:** docs/sprints/ (TBD — pre-sprint audit)
**Status:** Investigation complete. No code changed.

---

## Files Inspected

### Backend
- `src/LinguaCoach.Domain/Entities/StudentProfile.cs`
- `src/LinguaCoach.Domain/Entities/StudentResetLog.cs`
- `src/LinguaCoach.Domain/Entities/StudentSkillProfile.cs`
- `src/LinguaCoach.Domain/Entities/StudentPolicyAssignment.cs`
- `src/LinguaCoach.Domain/Entities/StudentOnboardingProgress.cs`
- `src/LinguaCoach.Domain/Entities/UserLearningSummary.cs`
- `src/LinguaCoach.Domain/Entities/AdminAuditLog.cs`
- `src/LinguaCoach.Persistence/Identity/ApplicationUser.cs`
- `src/LinguaCoach.Application/Admin/AdminQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminController.cs`
- `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs`
- `src/LinguaCoach.Api/Controllers/ProfileController.cs`

### Frontend
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/create-student/create-student.component.ts`
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`
- `src/LinguaCoach.Web/e2e/admin-students-reset.spec.ts`

---

## 1. Backend Capability Matrix

| Capability | Status | Endpoint / Location | Notes |
|---|---|---|---|
| List students | YES | `GET /api/admin/students?includeArchived=bool` | No pagination, no sort params; ordered by CreatedAt asc |
| Search students | NO | — | No name/email/stage search param |
| Filter students | PARTIAL | `includeArchived` bool only | No lifecycle stage, CEFR, onboarding status filter |
| Pagination | NO | — | Returns all records; no skip/take |
| Sorting | NO | — | Hardcoded `OrderBy(CreatedAt)` in handler |
| Student detail endpoint | NO | — | No `GET /api/admin/students/{id}`; clients parse list |
| Create student | YES | `POST /api/admin/students` | Email, temp password, optional profile fields; creates Identity user + StudentProfile |
| Edit student profile | YES | `PUT /api/admin/students/{id}` | 10 editable fields (admin-authored only); student-editable prefs not exposed here |
| Activate student | NO | — | No re-activate from archived state |
| Deactivate student | NO | — | No soft deactivate without archiving |
| Suspend student | NO | — | No suspended/paused lifecycle transition endpoint |
| Archive student | YES | `POST /api/admin/students/{id}/archive` | Sets LifecycleStage=Archived, disables email confirmation |
| Reset password | YES | `POST /api/admin/students/{id}/reset-password` | Supports mustChangePassword flag |
| Invite / re-invite | NO | — | No email invite flow; admin sets temporary password manually |
| Usage policy assignment | YES | `PUT /api/admin/students/{id}/usage-policy` | Reason required |
| Get effective usage policy | YES | `GET /api/admin/students/{id}/usage-policy` | Returns assignment metadata |
| Remove student policy override | YES | `DELETE /api/admin/students/{id}/usage-policy` | Reverts to global default |
| Onboarding status (read) | YES | In `StudentListItem` DTO | OnboardingStatus enum + LastCompletedStep |
| Lifecycle stage (read) | YES | In `StudentListItem` DTO | 12-stage enum available |
| CEFR visibility (admin) | YES | In `StudentListItem` DTO | CefrLevel string; set by assessment only |
| Admin CEFR control | PARTIAL | Via reset endpoint clearing placement | No direct admin CEFR set endpoint; only clear via reset |
| Learning goals (student-set) | YES (read) | In `StudentListItem` DTO (LearningGoalDescription) | Admin can read; cannot write student-editable prefs via admin endpoint |
| Learning goals (admin-set) | YES | `PUT /api/admin/students/{id}` | LearningGoal field (admin-authored) |
| Focus areas (student-set) | NO (admin read) | Not in `StudentListItem` or admin DTOs | FocusAreas/CustomFocusArea fields exist on entity but not exposed in admin DTO |
| Support language (read) | NO (admin read) | Not in `StudentListItem` or admin DTOs | SupportLanguageCode/Name exist on entity; not exposed to admin |
| Preferred name (read) | NO (admin read) | Not in `StudentListItem` or admin DTOs | PreferredName exists on entity; not in admin DTO |
| Learning preferences read (admin) | NO | — | No admin endpoint to read student-authored prefs (FocusAreas, SupportLanguage, DifficultyPreference, etc.) |
| Progress summary (admin) | YES | `GET /api/admin/students/{id}/learning-memory` | UserLearningSummary with journey, skills, weaknesses, next focus |
| Skill profile (admin) | YES | In learning-memory response | StudentSkillProfile rows (key, label, score, isWeak) |
| Activity history (admin) | YES | `GET /api/admin/students/{id}/activity-history` | attemptId, type, score, passed, date |
| Usage summary (admin) | YES | `GET /api/admin/students/{id}/usage` | Period-based usage metrics |
| Student reset (lifecycle) | YES | `POST /api/admin/students/{id}/reset` | 8 selective clear flags, 6 UI presets; rate-limited 10/admin/hour |
| Student reset audit trail | YES | StudentResetLog entity (append-only) | CorrelationId, previousStage, newStage, clearedItemsJson |
| Admin general audit log | YES | AdminAuditLog entity (append-only) | Covers all admin governance actions |
| StudentOnboardingProgress | YES | Entity exists | V2 onboarding flow progress; not surfaced in admin DTOs yet |
| Bulk operations | NO | — | No bulk activate/deactivate/assign |

---

## 2. Frontend Capability Matrix

| Capability | Status | File:Location | Notes |
|---|---|---|---|
| Student list page | YES | admin-students.component.ts | Signals-based, standalone component |
| Student list columns | YES | lines 55–110 | Student, Lifecycle, Onboarding, CEFR, Profile, Joined, Actions (7 cols) |
| Search (name/email) | YES | lines 34–45, 362–368 | Case-insensitive, reactive signal; email OR displayName/firstName/lastName |
| Show archived toggle | YES | line 34 | includeArchived checkbox; reloads from API |
| Sorting (click header) | YES | lines 58–65, 395–408 | Student, Onboarding, Joined columns sortable; direction indicator |
| Client-side pagination | YES | lines 387–393 | 25/page; hidden if ≤1 page; resets on search/sort |
| Student detail page | YES | admin-student-detail.component.ts | Multi-section layout |
| Detail: profile section | YES | lines 60–120 | 11 fields: lifecycle, onboarding, CEFR, career, goals, experience, joined |
| Detail: usage policy section | YES | lines 122–175 | Assign/remove override; shows effective policy with source badge |
| Detail: learning memory section | YES | lines 177–232 | Journey, skills, weaknesses, next focus, skill profile tags |
| Detail: activity history section | YES | lines 234–278 | Table: activity, type, score, result, date |
| Create student form | YES | create-student.component.ts | Email + password required; optional profile fields collapsible |
| Edit student modal | YES | admin-students.component.ts:120–183 | 10 fields, 2-col grid; opens from list row action |
| Reset password modal | YES | lines 185–229 | Auto-generate; mustChangePassword checkbox; shows password on success |
| Reset data modal | YES | lines 231–327 | 6 presets, 8 flags, reason + email confirm; result view with log ID |
| Archive action | YES | line 96 | window.confirm(); hides from list unless archived toggle on |
| Assign policy modal | YES | detail:282–321 | Policy dropdown + optional reason |
| Remove policy action | YES | detail (remove button) | Confirmation prompt; reverts to global default |
| Student-authored prefs display | NO | — | FocusAreas, SupportLanguage, DifficultyPreference, PreferredName not shown anywhere in admin UI |
| StudentOnboardingProgress display | NO | — | V2 onboarding flow progress not surfaced |
| CEFR admin set/clear control | PARTIAL | Via reset data modal | Admin can clear CEFR via reset; no direct set |
| Lifecycle transition controls | PARTIAL | Archive only | No activate, suspend, or manual stage transition controls |
| Bulk operations | NO | — | No multi-select or bulk actions |
| Unit tests: list component | PARTIAL | admin-students.component.spec.ts | 15 tests; gaps in form submission, reset flows, sort toggle |
| Unit tests: detail component | PARTIAL | admin-student-detail.component.spec.ts | 15 tests; policy section only; profile/memory/activity untested |
| E2E tests | PARTIAL | e2e/admin-students-reset.spec.ts | 6 tests covering reset data modal; list/search/sort/create/edit/archive untested |

---

## 3. Missing API / Data Pieces

### High Priority

1. **No pagination/filter/sort params on `GET /api/admin/students`.**
   At small tenant scale this is acceptable. At enterprise scale (100+ students) this becomes a performance and UX problem. Server-side pagination is the correct fix; client-side pagination already exists in the frontend but loads all records.

2. **Admin cannot read student-authored learning preferences.**
   `FocusAreas`, `CustomFocusArea`, `SupportLanguageCode`, `SupportLanguageName`, `DifficultyPreference`, `TranslationHelpPreference`, `PreferredName` all exist on the entity and are student-editable, but none appear in `StudentListItem` or any admin DTO. An admin support view cannot surface what the student has configured.

3. **No dedicated `GET /api/admin/students/{id}` endpoint.**
   Admin detail views must parse the full list response or call the list and filter. This blocks efficient deep-link navigation and future server-side pagination.

4. **`StudentOnboardingProgress` not exposed to admin.**
   The V2 onboarding flow progress (current step key, percentage, started/completed dates, preliminary CEFR) exists on the entity but is not returned in any admin query or DTO.

### Medium Priority

5. **No activate / re-activate endpoint.**
   Once archived, there is no API path to restore a student. The only recovery is via the reset endpoint (which changes lifecycle stage) but the `EmailConfirmed=false` set during archive is not re-set by any current endpoint.

6. **No suspend endpoint.**
   `LifecycleStage.Paused` exists in the enum but no admin API can set it directly. Only student self-pause or reset can reach Paused.

7. **No admin endpoint to read or write student-authored focus areas / learning goals (student version).**
   Admins might need to view or assist editing these; currently invisible.

8. **No bulk operations.**
   For larger tenants, bulk policy assignment, bulk deactivation, or bulk export are expected SaaS features.

### Low Priority

9. **No invite/re-invite email flow.**
   Admin manually sets a temporary password. For enterprise onboarding, a "send invite email" action is standard. Not currently in scope but expected by enterprise buyers.

10. **Audit log not queryable via admin API.**
    `AdminAuditLog` and `StudentResetLog` exist as entities but there is no API to surface them in the admin UI.

---

## 4. Missing UI Pieces

### High Priority

1. **Student-authored preferences not visible in admin detail.**
   `FocusAreas`, `SupportLanguage`, `PreferredName`, `DifficultyPreference`, `TranslationHelpPreference` need a "Student Preferences" section in the detail page. The backend entity has all fields; the admin DTO and endpoint are the gap.

2. **Onboarding flow progress (V2) not visible.**
   `StudentOnboardingProgress` (current step, percentage complete, preliminary CEFR) is not surfaced in the admin detail page.

3. **No lifecycle stage transition controls beyond archive.**
   Admins cannot manually set Paused, re-activate from Archived, or trigger OnboardingRequired without using the blunt reset tool.

### Medium Priority

4. **CEFR is display-only; no explicit admin level management UI.**
   Per product rules, admin/system/evaluation can update CEFR; students cannot. Currently admin cannot set it directly — only clear it via reset. An "Admin set CEFR" action or at minimum a "clear CEFR" targeted action (without a full reset) would be cleaner.

5. **No audit/history tab in student detail.**
   `AdminAuditLog` and `StudentResetLog` records exist but are invisible in the UI. Enterprise admins expect a log of who did what to a student.

6. **Unit test coverage is shallow.**
   Edit form submission, reset password, and all detail page sections beyond policy are untested. E2E coverage is limited to the reset-data modal.

### Low Priority

7. **No bulk actions in student list.**
   Multi-select + bulk archive, bulk policy assign, or CSV export are missing.

8. **No column customization or advanced filter panel in list.**
   Current filters are minimal (archived toggle + text search). Enterprise admins expect filterable lifecycle stage, CEFR range, onboarding status, and date range.

---

## 5. Risk Areas

| Risk | Severity | Notes |
|---|---|---|
| Full student list load at scale | HIGH | No server-side pagination. 500+ students will cause slow loads and large API payloads. |
| Admin blind to student-set preferences | HIGH | Support flows fail if admin cannot see SupportLanguage, FocusAreas, DifficultyPreference. |
| No re-activate path | MEDIUM | Archived student cannot be restored without manual DB intervention or reset workaround. |
| Audit log invisible in UI | MEDIUM | Enterprise compliance review requires surfacing admin action history. |
| Reset tool is the only lifecycle lever | MEDIUM | The reset tool is powerful but blunt. Targeted transitions (pause, re-activate) are safer. |
| StudentOnboardingProgress not in admin view | MEDIUM | Admin cannot diagnose onboarding stalls without backend inspection. |
| Shallow test coverage on edit/reset flows | LOW-MEDIUM | Edit, archive, reset password paths in the list component are not unit tested. |
| No CEFR write path for admin | LOW | Policy says admin can set CEFR; no endpoint exists to do so directly. |
| window.confirm() for archive action | LOW | Non-accessible; not dismissible by keyboard; expected to be replaced with a proper modal. |

---

## 6. Recommended Implementation Phases (small to large)

### Phase A — Admin Read of Student Preferences (Backend + Frontend, no migration)
**Effort:** Small

- Extend `StudentListItem` DTO with: `PreferredName`, `SupportLanguageCode`, `SupportLanguageName`, `DifficultyPreference`, `TranslationHelpPreference`, `FocusAreas`, `CustomFocusArea`, `LearningGoals` (student-set), `CustomLearningGoal`.
- Add a "Student Preferences" section to admin-student-detail, showing these fields read-only.
- No migration needed (fields exist on entity).
- No student-facing change.

### Phase B — Admin Student Detail Endpoint + Onboarding Progress
**Effort:** Small-Medium

- Add `GET /api/admin/students/{id}` returning full detail (StudentListItem + StudentOnboardingProgress fields).
- Add `StudentOnboardingProgress` fields to the DTO: currentStepKey, completedStepKeys, percentageComplete, startedAt, completedAt, isComplete, preliminaryCefrLevel.
- Add an "Onboarding Progress" section to admin-student-detail.
- Update frontend service to call detail endpoint when navigating to detail page (currently re-uses list item passed via router state).

### Phase C — Targeted Lifecycle Actions
**Effort:** Small-Medium

- Add `POST /api/admin/students/{id}/reactivate` endpoint: sets `EmailConfirmed=true`, sets lifecycle to `OnboardingRequired` (if previously OnboardingComplete) or preserves last active stage.
- Add `POST /api/admin/students/{id}/pause` endpoint: sets `LifecycleStage=Paused`.
- Add `POST /api/admin/students/{id}/unpause` endpoint: restores to prior active stage.
- Add these actions to the frontend detail and list action menus (conditional on current stage).
- Replace `window.confirm()` for archive with a proper confirmation modal.

### Phase D — Admin CEFR Management
**Effort:** Small

- Add `PUT /api/admin/students/{id}/cefr` endpoint: accepts `cefrLevel` (A1-C2 or null), records in AdminAuditLog.
- Add "Set CEFR level" action to admin-student-detail (dropdown, confirmation).
- Enforce: student-facing profile endpoint must remain read-only for CEFR.

### Phase E — Audit / History Tab
**Effort:** Medium

- Add `GET /api/admin/students/{id}/audit-log` returning paginated AdminAuditLog + StudentResetLog entries for the student.
- Add an "Audit History" tab/section to admin-student-detail.
- Display: actor, action, entity, old/new values, timestamp, correlationId.

### Phase F — Server-Side Pagination, Filtering, Sorting
**Effort:** Medium-Large

- Refactor `GET /api/admin/students` to accept: `page`, `pageSize`, `search` (email/name), `lifecycleStage`, `onboardingStatus`, `cefrLevel`, `sortBy`, `sortDir`.
- Return `{ items: StudentListItem[], totalCount, page, pageSize }`.
- Update `IAdminStudentQuery.ListStudentsAsync` signature.
- Update frontend service and list component to drive pagination from server response.
- This is a breaking change to the list contract; coordinate with any other consumers.

### Phase G — Test Coverage Expansion
**Effort:** Medium

- Unit tests for edit form submission, reset password, archive in list component.
- Unit tests for profile, memory, activity sections in detail component.
- E2E tests for search, sort, pagination, create student, edit, archive.
- This can proceed in parallel with any phase above.

### Phase H — Bulk Operations (Deferred)
**Effort:** Large

- Multi-select in student list.
- Bulk archive, bulk policy assignment, CSV export.
- Deferred until single-student workflows are fully solid.

---

## 7. Recommended Next Phase

**Phase A — Admin Read of Student Preferences**

This is the highest-value, lowest-risk next step:

- No migration required.
- No new endpoints required (extend existing DTO + handler).
- Unblocks admin support workflows that currently have no visibility into student-configured preferences.
- Frontend change is additive (new section on detail page).
- Zero risk to student-facing flows.

After Phase A, move to **Phase B** (student detail endpoint + onboarding progress) to eliminate the list-as-detail antipattern before adding more fields.

---

## 8. What Should Be Deferred

| Item | Reason |
|---|---|
| Invite / email flow | Requires email infrastructure integration; not blocking core admin workflows |
| Bulk operations | Premature until single-student flows are complete and stable |
| Server-side pagination | Not urgent at current tenant scale; Phase F is correct ordering |
| Billing / workspace / cohort assignment | Out of scope per product rules; do not conflate with student management |
| Student-editable CEFR | Blocked by product rule; must remain admin/assessment only |
| Admin editing of student-authored prefs directly | Preserve student ownership; admin read-only is sufficient for Phase A |
| Notification / communication flows | Out of scope for current sprint |

---

## Decisions Made

- **Student-authored preferences (FocusAreas, SupportLanguage, etc.) should be visible to admins but not editable by admins.** Students own these fields; admin visibility is a support/diagnostic need.
- **CEFR must remain write-blocked for students.** Admin set-CEFR endpoint is needed (Phase D) but student endpoint stays read-only.
- **Phase A (DTO extension + UI read section) is the correct immediate next step.** Zero migration risk, highest support value.
- **Server-side pagination is Phase F, not Phase A.** Client-side pagination is sufficient at current scale; fixing data visibility gaps is higher priority.

---

## Risks / Unresolved Questions

1. **Re-activate flow and EmailConfirmed:** When re-activating an archived student, the handler must restore `EmailConfirmed=true` on the Identity user. Confirm this is safe given Identity's concurrency token model.
2. **StudentOnboardingProgress for V2 vs legacy:** Determine if all active students have V2 onboarding progress records or if there are legacy-only students with no `StudentOnboardingProgress` row. Detail view must handle null gracefully.
3. **CEFR admin-write endpoint:** Confirm whether admin-set CEFR should also log a StudentResetLog entry or only AdminAuditLog.
4. **Filter panel scope:** Decide which filters to expose in Phase F before starting (lifecycle stage, onboarding status, CEFR range — any or all).

---

## Final Verdict

The student management foundation is solid and architecturally clean. The entity model is rich and covers all required enterprise fields. The main gaps are:

1. **Admin blind spots** — student-authored preferences are invisible to admins.
2. **Missing targeted lifecycle controls** — archive is the only direct state transition.
3. **No student detail endpoint** — the list-as-detail pattern is fragile.
4. **Scale readiness** — no server-side pagination (acceptable now, not at scale).
5. **Test coverage** — shallow on edit/reset/archive flows.

None of these require architectural changes. All are additive. The recommended sequence is A → B → C → D → E → F → G, with G running in parallel.

---

## Next Recommended Action

Implement **Phase A — Admin Read of Student Preferences**:

- Extend `StudentListItem` DTO to include student-authored preference fields.
- Add "Student Preferences" read-only section to `admin-student-detail.component.ts`.
- No migration. No new endpoint. No student-facing change.
