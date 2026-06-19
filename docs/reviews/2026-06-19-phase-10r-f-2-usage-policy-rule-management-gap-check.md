---
status: current
lastUpdated: 2026-06-19
owner: engineering
supersedes:
supersededBy:
---

# Phase 10R-F-2 — Usage Policy Rule Management Gap Check

**Date:** 2026-06-19
**Related sprint:** 10R-F-2 (Gap audit, no code changes)
**Related TODO:** TODO-10R-RULE-MGMT

---

## Files inspected

| File | Purpose |
|---|---|
| `src/LinguaCoach.Domain/Entities/UsagePolicyRule.cs` | Rule domain entity, mutation surface |
| `src/LinguaCoach.Domain/Entities/UsagePolicy.cs` | Policy aggregate, cascade relationship |
| `src/LinguaCoach.Domain/Enums/EnforcementMode.cs` | EnforcementMode values |
| `src/LinguaCoach.Domain/Enums/UsageUnitType.cs` | UnitType values |
| `src/LinguaCoach.Domain/Enums/UsagePolicyScopeType.cs` | ScopeType values (Global, Workspace, Cohort, Student) |
| `src/LinguaCoach.Domain/Enums/FeatureCategory.cs` | Feature category values |
| `src/LinguaCoach.Application/UsageGovernance/IUsageGovernanceAdminService.cs` | Service interface and DTOs |
| `src/LinguaCoach.Infrastructure/UsageGovernance/UsageGovernanceAdminService.cs` | Service implementation |
| `src/LinguaCoach.Persistence/Configurations/UsagePolicyRuleConfiguration.cs` | EF config, index, precision |
| `src/LinguaCoach.Persistence/Configurations/UsagePolicyConfiguration.cs` | Cascade delete config |
| `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` | DbSet presence check |
| `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs` | Existing API surface |
| `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts` | Frontend service and models |

---

## Existing backend entities / DTOs / endpoints

### Domain entity: `UsagePolicyRule`

Fields (all `private set`, no public mutation methods):

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, inherited from `BaseEntity` |
| `UsagePolicyId` | `Guid` | FK to `UsagePolicy` |
| `FeatureKey` | `string` (max 100) | Normalised to lowercase, required |
| `TrackingEnabled` | `bool` | |
| `EnforcementMode` | `EnforcementMode` enum | None / TrackOnly / SoftWarning / HardLimit / AdminApprovalRequired |
| `UnitType` | `UsageUnitType` enum | Count / Tokens / InputTokens / OutputTokens / Minutes / Seconds / Characters / Cost |
| `DailyLimit` | `long?` | |
| `WeeklyLimit` | `long?` | |
| `MonthlyLimit` | `long?` | |
| `DailyCostLimit` | `decimal?` | precision(18,6) |
| `MonthlyCostLimit` | `decimal?` | precision(18,6) |
| `WarningThresholdPercent` | `int` | 0–100, default 80 |
| `IsActive` | `bool` | |

**Critical observation:** `UsagePolicyRule` has **no public mutation methods** (`Update`, `SetLimits`, `Enable`, `Disable`, etc.). The only way to change a rule is to delete and re-create the EF entity, or to add domain methods first.

There is also **no ordering/sort column** on `UsagePolicyRule`.

### Domain entity: `UsagePolicy`

Has one mutation method: `Update(name, description, isDefault, isActive)`. Rules are a navigation collection with no domain-level add/remove helpers.

EF cascade: `OnDelete(DeleteBehavior.Cascade)` — deleting a policy deletes all its rules automatically.
Index: `(UsagePolicyId, FeatureKey)` — unique enforcement is **not** declared at DB level (only an index, no `IsUnique()`). Duplicate feature keys per policy are theoretically possible.

### Application layer DTOs

| DTO | Coverage |
|---|---|
| `CreateUsagePolicyRuleRequest` | Full field set — used only during policy creation |
| `UpdateUsagePolicyRequest` | Name, description, isDefault, isActive **only** — rules excluded |
| No `UpdateUsagePolicyRuleRequest` | Does not exist |
| No `DeleteUsagePolicyRuleRequest` | Does not exist |

### Admin API endpoints (`AdminUsageGovernanceController`)

| Endpoint | Covers rules? |
|---|---|
| `GET /api/admin/usage-policies` | Returns rules (active only via `ListUsagePoliciesAsync`) |
| `GET /api/admin/usage-policies/{id}` | Returns all rules (including inactive) |
| `POST /api/admin/usage-policies` | Accepts `rules[]` array — creates rules at policy creation time |
| `PUT /api/admin/usage-policies/{id}` | Policy fields only, **no rules** |
| No `POST /api/admin/usage-policies/{id}/rules` | Does not exist |
| No `PUT /api/admin/usage-policies/{id}/rules/{ruleId}` | Does not exist |
| No `DELETE /api/admin/usage-policies/{id}/rules/{ruleId}` | Does not exist |

### Frontend service (`UsageGovernanceService`)

| Method | Notes |
|---|---|
| `listUsagePolicies()` | Returns `UsagePolicy[]` with rules embedded |
| `getUsagePolicy(id)` | Returns single policy with all rules |
| `createUsagePolicy(req)` | Accepts `rules: Partial<UsagePolicyRule>[]` |
| `updateUsagePolicy(id, req)` | Sends name/description/isDefault/isActive only |
| No `createRule`, `updateRule`, `deleteRule` methods | Do not exist |

Frontend `UsagePolicyRule` interface mirrors backend fields correctly including all limit fields.

---

## Current capability matrix

| Operation | Backend API | Domain method | Frontend service | Verdict |
|---|---|---|---|---|
| **Create rule** | Only via `POST /usage-policies` (creation time) | Constructor only, no add-to-policy helper | `createUsagePolicy` only | **Partial** — creation only, not post-creation add |
| **Edit rule** | None | None (all `private set`) | None | **No** |
| **Delete rule** | None (only via cascade policy delete) | None | None | **No** |
| **Enable/disable rule** | None | None | None | **No** |
| **Reorder rule** | N/A — no ordering column | N/A | N/A | **Not applicable** |

---

## Missing pieces (ordered by layer)

### 1. Domain layer — `UsagePolicyRule` mutation methods

`UsagePolicyRule` must get a public `Update(...)` method before any edit endpoint can function safely. Without it, the only options are delete-and-recreate (lossy, no concurrency safety) or directly mutating EF tracked properties via reflection (wrong).

Minimum needed:

```csharp
public void Update(
    bool trackingEnabled,
    EnforcementMode enforcementMode,
    UsageUnitType unitType,
    long? dailyLimit, long? weeklyLimit, long? monthlyLimit,
    decimal? dailyCostLimit, decimal? monthlyCostLimit,
    int warningThresholdPercent,
    bool isActive)
```

### 2. Domain layer — unique constraint on (UsagePolicyId, FeatureKey)

The EF index is non-unique. Two rules for the same feature key in the same policy would produce ambiguous enforcement. Should be made unique to prevent duplicate keys.

### 3. Application layer — new DTOs

- `UpdateUsagePolicyRuleRequest` (all mutable rule fields)
- `AddUsagePolicyRuleRequest` (same as `CreateUsagePolicyRuleRequest`, used for post-creation add)

Extend `IUsageGovernanceAdminService` with:

```csharp
Task<UsagePolicyRule> AddRuleAsync(Guid policyId, AddUsagePolicyRuleRequest req, Guid adminUserId, CancellationToken ct);
Task<UsagePolicyRule> UpdateRuleAsync(Guid policyId, Guid ruleId, UpdateUsagePolicyRuleRequest req, Guid adminUserId, CancellationToken ct);
Task DeleteRuleAsync(Guid policyId, Guid ruleId, Guid adminUserId, CancellationToken ct);
```

### 4. API layer — three new endpoints

```
POST   /api/admin/usage-policies/{id}/rules
PUT    /api/admin/usage-policies/{id}/rules/{ruleId}
DELETE /api/admin/usage-policies/{id}/rules/{ruleId}
```

`PATCH /rules/{ruleId}` for enable/disable is a possible convenience but can be folded into the `PUT`.

### 5. Frontend service — three new methods

```ts
addRule(policyId: string, req: AddUsagePolicyRuleRequest): Observable<UsagePolicyRule>
updateRule(policyId: string, ruleId: string, req: UpdateUsagePolicyRuleRequest): Observable<UsagePolicyRule>
deleteRule(policyId: string, ruleId: string): Observable<void>
```

### 6. Frontend UI — rule editor panel

The rule list in the expanded row needs edit/delete buttons and an "Add rule" form. The form must expose:

- Feature key (select from feature definitions)
- Enforcement mode (select: None / TrackOnly / SoftWarning / HardLimit / AdminApprovalRequired)
- Unit type (select)
- Daily / weekly / monthly limits (number inputs, nullable)
- Daily / monthly cost limits (number inputs, nullable, decimal)
- Warning threshold percent (0–100)
- Tracking enabled (checkbox)
- Active (checkbox)

No migration is needed unless the unique constraint on `(UsagePolicyId, FeatureKey)` is added — that would require a new EF migration.

---

## Validation rules

| Field | Rule |
|---|---|
| `FeatureKey` | Required, max 100 chars, must match a known `FeatureDefinition.Key` |
| `EnforcementMode` | Required, must be a valid enum value |
| `UnitType` | Required, must be a valid enum value |
| `DailyLimit` | Nullable, ≥ 0 when set |
| `WeeklyLimit` | Nullable, ≥ 0 when set |
| `MonthlyLimit` | Nullable, ≥ 0 when set |
| `DailyCostLimit` | Nullable, ≥ 0 when set, decimal precision (18,6) |
| `MonthlyCostLimit` | Nullable, ≥ 0 when set, decimal precision (18,6) |
| `WarningThresholdPercent` | 0–100, default 80 |
| `TrackingEnabled` | Boolean, default true |
| `IsActive` | Boolean, default true |
| Uniqueness | One rule per `(policyId, featureKey)` combination |

Validation already exists in the `UsagePolicyRule` constructor. The `Update` method should repeat the same checks.

---

## Risks

| Risk | Severity | Notes |
|---|---|---|
| Duplicate feature keys per policy | Medium | Non-unique index allows it; enforcement picks an arbitrary rule. Add a `HasIndex(...).IsUnique()` in EF config and a migration. |
| No domain mutation methods | High | Must not bypass domain by directly setting EF properties. |
| `ListUsagePoliciesAsync` only returns active rules | Low | `GetUsagePolicyAsync` returns all. The list endpoint is fine for display but will miss inactive rules in the policy summary. Acceptable for now. |
| `AdminApprovalRequired` enforcement mode | Low | Enum value exists but no approval workflow is implemented. Assigning this mode is safe (it will be stored) but will not enforce anything until a future phase adds the approval gate. Should be hidden or labelled "future" in the UI. |
| No audit log for rule changes | Low | `AssignPolicyToStudentAsync` already writes an audit log. Rule add/edit/delete should do the same for compliance. |
| Migration risk for unique constraint | Low | Only affects new data; existing seeded data has one rule per feature key per policy (confirmed in `UsageGovernanceSeeder`). Safe to add. |

---

## Recommended next small phase: 10R-G — Usage Policy Rule CRUD

### Backend work required: Yes

Three steps, all small and safe:

**Step 1 — Domain (no migration):**
- Add `Update(...)` method to `UsagePolicyRule`
- Optionally add `AddRule(rule)` / `RemoveRule(ruleId)` helpers to `UsagePolicy`

**Step 2 — Application + API (no migration):**
- Add DTOs: `AddUsagePolicyRuleRequest`, `UpdateUsagePolicyRuleRequest`
- Extend `IUsageGovernanceAdminService` with `AddRuleAsync`, `UpdateRuleAsync`, `DeleteRuleAsync`
- Implement in `UsageGovernanceAdminService`
- Add three endpoints to `AdminUsageGovernanceController`

**Step 3 (optional, requires migration):**
- Make `(UsagePolicyId, FeatureKey)` index unique in `UsagePolicyRuleConfiguration`
- Add EF migration

### Frontend work required: Yes (after backend)

- Add three methods to `UsageGovernanceService`
- Expand rule detail row with edit/delete buttons
- Add "Add rule" inline form (feature select, enforcement mode, unit type, limits)
- Use `SpAdminNumberInputComponent`, `SpAdminSelectComponent`, `SpAdminCheckboxComponent`, `SpAdminFormFieldComponent`

### Frontend-only possible: No

The `PUT /usage-policies/{id}` endpoint does not accept rules. There is no way to create, edit, or delete rules from the frontend without new API endpoints.

---

## Final verdict

Rule management requires backend work before any frontend rule editor can be built. The backend changes are small and well-scoped: one domain method, three service methods, three API endpoints. No large redesign is needed. The unique constraint migration is optional but recommended before rule editing ships.

**Backend work required:** Yes
**Frontend-only possible:** No
**Recommended next phase:** 10R-G — Usage Policy Rule CRUD (backend first, then frontend)

---

## Confirmation

- No source code was changed during this audit.
- `git diff --check` not required (no changes).
- No commit or push was made.
