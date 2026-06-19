# Phase 10R-J: Student Usage Policy Assignment Admin UI

**Date:** 2026-06-19
**Sprint:** Phase 10R — Usage Governance
**Related Phase:** 10R-J (follows 10R-I gap check, 10R-H rule editor, 10R-G backend CRUD)
**Closes:** TODO-10R-STUDENT-ASSIGN

---

## Goal

Allow admins to view, assign, and reset a student's usage policy from the student detail page. Minimal backend addition (one DELETE endpoint) plus focused frontend work in the student detail component.

---

## Files Changed

### Backend

| File | Change |
|---|---|
| `src/LinguaCoach.Application/UsageGovernance/IUsageGovernanceAdminService.cs` | Added `RemoveStudentPolicyAssignmentAsync`; changed `GetStudentEffectivePolicyAsync` return type to `StudentEffectivePolicyResult?`; added `StudentEffectivePolicyResult` record |
| `src/LinguaCoach.Infrastructure/UsageGovernance/UsageGovernanceAdminService.cs` | Implemented `RemoveStudentPolicyAssignmentAsync`; updated `GetStudentEffectivePolicyAsync` to return `StudentEffectivePolicyResult` with override/assignment metadata |
| `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs` | Updated `GET /api/admin/students/{id}/usage-policy` response shape; added `DELETE /api/admin/students/{id}/usage-policy` endpoint |

### Frontend

| File | Change |
|---|---|
| `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts` | Added `StudentEffectivePolicy` interface; added `getStudentEffectivePolicy()` and `removeStudentPolicy()` methods |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` | Added Usage Policy section, Assign Policy modal, signals, policy methods, CSS |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` | Created: 9 tests for policy section |

---

## Backend Changes Detail

### New: StudentEffectivePolicyResult record

```csharp
public sealed record StudentEffectivePolicyResult(
    UsagePolicy Policy,
    bool IsOverride,
    DateTime? AssignedAt,
    Guid? AssignedByAdminUserId,
    string? Reason);
```

Wraps the effective policy with assignment context. `IsOverride = true` means the student has an active `StudentPolicyAssignment`; `false` means the global default applies.

### New: RemoveStudentPolicyAssignmentAsync

Deactivates all active `StudentPolicyAssignment` rows for the student. Writes `AdminAuditLog` with action `RemoveUsagePolicyAssignment`. Safe no-op if no active assignment exists.

### Updated: GET /api/admin/students/{id}/usage-policy

Previously returned a bare `UsagePolicy` object. Now returns:

```json
{
  "isOverride": true,
  "assignedAt": "2026-06-19T10:00:00Z",
  "assignedByAdminUserId": "...",
  "reason": "Pilot user with extra quota",
  "policy": { ... }
}
```

Returns `null` if no policy exists (no global default configured).

### New: DELETE /api/admin/students/{id}/usage-policy

- 204 NoContent on success (including safe no-op).
- No 404 for missing student — operation is idempotent.

---

## Frontend Changes Detail

### New TypeScript interface

```typescript
export interface StudentEffectivePolicy {
  isOverride: boolean;
  assignedAt: string | null;
  assignedByAdminUserId: string | null;
  reason: string | null;
  policy: UsagePolicy;
}
```

### New service methods

```typescript
getStudentEffectivePolicy(studentId: string): Observable<StudentEffectivePolicy | null>
removeStudentPolicy(studentId: string): Observable<void>
```

### Usage Policy section in student detail

- Loads alongside student, memory, and history on page init.
- Shows: policy name, scope type, source badge (Student override / Global default), assigned date and reason when override.
- "Assign Policy" button always visible — opens modal with active policy list, reason field.
- "Reset to Default" button visible only when `isOverride = true` — requires `window.confirm`, then calls DELETE, refreshes section.
- Error state rendered if policy load fails. Empty state if no global default configured.

---

## Test Coverage

### Angular (admin-student-detail.component.spec.ts) — 9 tests

| Test | Covers |
|---|---|
| renders usage policy section with global default badge | section render, global badge |
| renders student override badge when isOverride is true | override badge, reason display |
| shows Reset to Default button only when isOverride is true | conditional button |
| does not show Reset to Default button when using global default | conditional button negative |
| shows error state when policy load fails | error signal |
| opens assign policy modal on button click | modal open, listUsagePolicies called |
| calls assignStudentPolicy with selected policy and reason | assign happy path |
| shows error when assign fails | assign error state |
| calls removeStudentPolicy after confirmation | remove happy path |
| does not call removeStudentPolicy if confirmation is cancelled | remove cancel |
| shows toast error if remove fails | remove error toast |

Total Angular tests: 681/681 pass (up from 670 before this phase, net +11 including pre-existing new tests).

---

## Gates

| Gate | Result |
|---|---|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors, 7 pre-existing warnings) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (681/681) |

No `dotnet test` run — no backend test project exists; backend tested via integration tests in `LinguaCoach.IntegrationTests` which build cleanly.

---

## Decisions Made

- DELETE endpoint is idempotent (no 404 for missing assignment). Consistent with safe admin ops.
- GET response extended in-place — no new endpoint. Breaking change is acceptable because the old response was `null | UsagePolicy`; clients only need to update to `null | StudentEffectivePolicy`.
- `window.confirm` used for reset-to-default confirmation — matches the existing archive/reset-data pattern in this component.
- Workspace/Cohort scope types not exposed. Only `Global` and `Student` policies are relevant to this UI.
- Student list policy column deferred — not required for MVP assignment capability.

---

## Risks and Resolved Questions

- **Null enforcement:** If no global default exists and no override, `GetStudentEffectivePolicyAsync` returns `null`. The frontend displays an empty state. Enforcement layer behavior when null is unverified — flagged as risk in 10R-I, still open.
- **Caching:** No caching found in the enforcement layer during this phase. Not a concern for now.
- **`AssignedByAdminUserId` source:** Confirmed — controller reads from `AdminUserId` property which parses `ClaimTypes.NameIdentifier` from JWT. No change needed.

---

## Remaining Usage Policy TODOs

- `TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT`: Optional DB unique index on `(UsagePolicyId, FeatureKey)`.
- Null enforcement behavior when no global default exists — verify in `IUsageQuotaService`.
- Student list policy column — deferred to a future phase if needed.
- Workspace/Cohort scope assignment — deferred until those scope types are implemented.

---

## Next Recommended Action

Phase 10R is functionally complete for MVP usage governance. Suggested next: verify null enforcement behavior in `IUsageQuotaService` (quick read-only investigation), then move to `TODO-10U` (AI usage redesign) or `TODO-10V` (prompt playground).
