# Phase 10R-I: Student Usage Policy Assignment Gap Check

**Date:** 2026-06-19
**Sprint:** Phase 10R — Usage Governance
**Related Phase:** 10R-I (follows 10R-G backend CRUD, 10R-H admin rule editor UI)
**Author:** Gap check review — no code changed

---

## Files Inspected

### Backend

| File | Purpose |
|---|---|
| `src/LinguaCoach.Domain/Entities/UsagePolicy.cs` | Policy aggregate root |
| `src/LinguaCoach.Domain/Entities/UsagePolicyRule.cs` | Per-feature rule |
| `src/LinguaCoach.Domain/Entities/StudentPolicyAssignment.cs` | Student-policy join entity |
| `src/LinguaCoach.Domain/Entities/StudentProfile.cs` | Student profile entity |
| `src/LinguaCoach.Application/UsageGovernance/IUsageGovernanceAdminService.cs` | Admin service interface and request DTOs |
| `src/LinguaCoach.Infrastructure/UsageGovernance/UsageGovernanceAdminService.cs` | Service implementation |
| `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs` | Admin API controller |
| `src/LinguaCoach.Persistence/Migrations/20260617220227_Phase10R_UsageGovernance.cs` | DB migration |
| `src/LinguaCoach.Domain/Enums/UsagePolicyScopeType.cs` | Scope enum |
| `src/LinguaCoach.Domain/Enums/EnforcementMode.cs` | Enforcement mode enum |

### Frontend

| File | Purpose |
|---|---|
| `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts` | Angular governance service |
| `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.ts` | Usage policies admin page |
| `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts` | Students admin page |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` | Student detail page |
| `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` | Admin models |
| `src/LinguaCoach.Web/src/app/app.routes.ts` | Angular routes |

---

## Existing Backend Entities and Fields

### UsagePolicy

```
Id, Name, Description, ScopeType (enum), IsDefault, IsActive,
CreatedAt, UpdatedAt, Rules (collection), StudentAssignments (collection)
```

ScopeType enum values: `Global`, `Workspace`, `Cohort`, `Student`
(Workspace and Cohort are defined but not yet implemented.)

### UsagePolicyRule

```
Id, UsagePolicyId (FK), FeatureKey (string, normalized lowercase),
TrackingEnabled, EnforcementMode, UnitType,
DailyLimit, WeeklyLimit, MonthlyLimit (long, nullable),
DailyCostLimit, MonthlyCostLimit (decimal, nullable),
WarningThresholdPercent (0-100), IsActive, CreatedAt
```

### StudentPolicyAssignment

```
Id, StudentProfileId (FK), UsagePolicyId (FK),
AssignedByAdminUserId (Guid), Reason (string, nullable, max 500),
IsActive (bool), UpdatedAt, CreatedAt
```

Methods: `Deactivate()` — marks IsActive=false, updates UpdatedAt.

### StudentProfile

No direct `UsagePolicyId` column. Policy is resolved via `StudentPolicyAssignment` at runtime.

---

## Existing Admin API Endpoints

| Method | Route | Status |
|---|---|---|
| GET | `/api/admin/feature-definitions` | Implemented |
| GET | `/api/admin/usage-policies` | Implemented |
| GET | `/api/admin/usage-policies/{id}` | Implemented |
| POST | `/api/admin/usage-policies` | Implemented |
| PUT | `/api/admin/usage-policies/{id}` | Implemented |
| POST | `/api/admin/usage-policies/{policyId}/rules` | Implemented |
| PUT | `/api/admin/usage-policies/{policyId}/rules/{ruleId}` | Implemented |
| DELETE | `/api/admin/usage-policies/{policyId}/rules/{ruleId}` | Implemented |
| **PUT** | **`/api/admin/students/{studentId}/usage-policy`** | **Implemented** |
| **GET** | **`/api/admin/students/{studentId}/usage-policy`** | **Implemented** |
| GET | `/api/admin/students/{studentId}/usage` | Implemented |

The assign and get-effective-policy endpoints are already implemented and wired in `AdminUsageGovernanceController.cs`.

### AssignStudentPolicyRequest DTO

```csharp
sealed record AssignStudentPolicyRequest(Guid PolicyId, string? Reason);
```

Response: 204 NoContent. On missing student or policy: 404.

Assignment logic:
1. Deactivates any existing active `StudentPolicyAssignment` for the student.
2. Creates a new `StudentPolicyAssignment` record.
3. Writes an `AdminAuditLog` entry.

---

## Enforcement Resolution

`GetStudentEffectivePolicyAsync(Guid studentProfileId)` in `UsageGovernanceAdminService.cs`:

1. Find most recent active `StudentPolicyAssignment` for the student.
2. If found, load and return that policy (with active rules).
3. If not found, load the default Global policy (`IsDefault=true, IsActive=true, ScopeType=Global`).
4. Return null if neither exists.

This means the fallback chain is: **student override → global default → null (unconstrained)**.

---

## Existing Frontend Services and Models

### usage-governance.service.ts — existing methods

| Method | Route | Notes |
|---|---|---|
| `listUsagePolicies()` | GET `/api/admin/usage-policies` | Used by policy page |
| `getUsagePolicy(id)` | GET `/api/admin/usage-policies/{id}` | Used by policy page |
| `createUsagePolicy(req)` | POST `/api/admin/usage-policies` | Used by policy page |
| `updateUsagePolicy(id, req)` | PUT `/api/admin/usage-policies/{id}` | Used by policy page |
| `addRule(policyId, req)` | POST `…/{policyId}/rules` | Used by policy page |
| `updateRule(policyId, ruleId, req)` | PUT `…/{policyId}/rules/{ruleId}` | Used by policy page |
| `deleteRule(policyId, ruleId)` | DELETE `…/{policyId}/rules/{ruleId}` | Used by policy page |
| **`assignStudentPolicy(studentId, policyId, reason)`** | **PUT `/api/admin/students/{studentId}/usage-policy`** | **Exists but unused** |
| `getStudentUsage(studentId, period)` | GET `/api/admin/students/{studentId}/usage` | Usage stats only |

`assignStudentPolicy` is already in the Angular service. No backend call is missing.

**Missing from Angular service:**
- `getStudentEffectivePolicy(studentId)` — GET `/api/admin/students/{studentId}/usage-policy` not yet in the service.

### Angular models

`StudentListItem` has no usage policy fields.  
No `StudentPolicyAssignment`, `EffectivePolicy`, or override-indicator models exist in TypeScript.

---

## Current Capability Matrix

| Capability | Backend | Frontend |
|---|---|---|
| Global default policy supported | **Yes** | **Yes** (display only) |
| Per-student policy assignment — API exists | **Yes** | **Partial** (service method exists, no UI) |
| Remove/reset assignment to default | **Yes** (Deactivate method) | **No** (no UI, no service method) |
| Effective policy resolution | **Yes** | **No** (GET endpoint not in Angular service) |
| Admin UI to assign student policy | n/a | **No** |
| Admin UI shows current student policy | n/a | **No** |
| Audit log on assignment | **Yes** | n/a |
| Workspace / cohort / group scope | **No** (enum defined, not implemented) | **No** |

---

## Missing API / Data Pieces

All backend API is complete. Frontend gaps only:

1. **`getStudentEffectivePolicy(studentId)`** — Angular service method missing. Backend endpoint exists.
2. **`removeStudentPolicy(studentId)`** — No API endpoint exists for removing an override and resetting to default. Backend has `Deactivate()` on the entity but no dedicated DELETE or reset endpoint.
3. **TypeScript models** — `StudentEffectivePolicyResponse`, `StudentPolicyAssignmentInfo`, `EffectivePolicyIndicator` not defined.
4. **`StudentListItem` policy fields** — Student list does not return current policy name or override indicator.
5. **Admin student detail section** — No "Usage Policy" section in the student detail template.
6. **Admin students page column** — No policy column in the student list table.

---

## Enforcement Risks

1. **Null effective policy.** If no default Global policy exists, `GetStudentEffectivePolicyAsync` returns null. The enforcement layer must handle null gracefully (allow all, or block all). Needs verification in `IUsageQuotaService` implementation.
2. **Stale effective policy on assignment.** If the enforcement layer caches the effective policy per student, a new assignment will not take effect until cache expiry. Confirm whether any caching exists.
3. **ScopeType mismatch.** Creating a policy with `ScopeType=Student` and marking it `IsDefault=true` could create unintended system-wide default. Recommend server-side guard: a Student-scoped policy must not be `IsDefault`.
4. **Workspace/Cohort scope declared but unimplemented.** The enum values exist. If admin UI exposes them as options, assignments using those scope types would silently fail resolution. Filter these out in the create-policy form until implemented.

---

## Recommended Next Phase: 10R-J — Student Policy Assignment Admin UI

### Scope (frontend-only, minimal backend addition)

**Backend addition required (small):**

- Add `DELETE /api/admin/students/{studentId}/usage-policy` endpoint (remove override, revert to default). One new controller action calling `Deactivate()` on the active assignment. ~20 lines.
- Optionally extend `GET /api/admin/students/{studentId}/usage-policy` response to include `assignedAt`, `assignedByAdminUserId`, `reason`, and `isOverride` flag for the UI to display.

**Frontend work:**

1. Add `getStudentEffectivePolicy(studentId)` to `usage-governance.service.ts`.
2. Add `removeStudentPolicy(studentId)` to `usage-governance.service.ts`.
3. Add TypeScript models: `StudentEffectivePolicyDto`, `StudentPolicyAssignmentInfo`.
4. Add a "Usage Policy" section to `admin-student-detail.component` showing:
   - Effective policy name and scope type
   - Override indicator (student override vs. global default)
   - Assigned date and reason (if override)
   - "Assign Policy" button (opens policy picker modal)
   - "Reset to Default" button (if override is active)
5. Optionally add a policy column to the students list table (policy name or "Default").

### Implementation order

1. Backend: add DELETE endpoint (~20 lines, low risk).
2. Angular service: add two methods.
3. Angular model: add DTOs.
4. Student detail: add policy section with assign/reset actions.

---

## Whether Backend Work Is Required

**Yes — minimal.** One new endpoint required:

```
DELETE /api/admin/students/{studentId}/usage-policy
```

This removes the active student policy assignment (revert to default). Without it the admin UI has no way to undo an override.

All other backend API is already complete and tested.

---

## Whether Frontend-Only Work Is Enough

**No** — the remove/reset endpoint is missing. All other gaps are frontend-only.

---

## Final Verdict

The backend foundation is nearly complete. All the heavy lifting (entities, migration, assignment logic, enforcement resolution, audit logging) is done. The Angular service already has `assignStudentPolicy`. The only backend gap is the remove endpoint. Frontend has no student policy UI at all. Phase 10R-J can deliver the full student assignment UI with one small backend addition and focused frontend work in the student detail component.

---

## Decisions Made

- No code changed in this review.
- Next phase: 10R-J.
- Backend: add DELETE remove-assignment endpoint only.
- Frontend: add to student detail component, not student list (lower risk, sufficient for MVP).
- Workspace and Cohort scope types to remain hidden until implemented.
- ScopeType guard recommended (Student policy must not be IsDefault).

---

## Risks and Unresolved Questions

- Null policy enforcement behavior unverified (what happens when no default exists).
- Effective policy caching behavior unconfirmed.
- `AssignedByAdminUserId` must resolve from the authenticated admin — confirm controller reads from `HttpContext.User`.

---

## Next Recommended Action

Implement Phase 10R-J: Student Policy Assignment Admin UI.
Start with DELETE endpoint, then Angular service additions, then student detail section.
