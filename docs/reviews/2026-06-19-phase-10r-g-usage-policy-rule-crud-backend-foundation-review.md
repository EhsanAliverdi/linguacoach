---
status: current
lastUpdated: 2026-06-19
owner: engineering
supersedes:
supersededBy:
---

# Phase 10R-G — Usage Policy Rule CRUD Backend Foundation

**Date:** 2026-06-19
**Related sprint:** 10R-G
**Closes:** TODO-10R-RULE-MGMT (backend portion)
**Remaining:** TODO-10R-RULE-MGMT-UI (frontend rule editor UI, next phase)

---

## Files changed

| File | Change |
|---|---|
| `src/LinguaCoach.Domain/Entities/UsagePolicyRule.cs` | Added `Update(...)` domain mutation method with full validation |
| `src/LinguaCoach.Application/UsageGovernance/IUsageGovernanceAdminService.cs` | Added `AddRuleAsync`, `UpdateRuleAsync`, `DeleteRuleAsync` methods; added `AddUsagePolicyRuleRequest` and `UpdateUsagePolicyRuleRequest` DTOs |
| `src/LinguaCoach.Infrastructure/UsageGovernance/UsageGovernanceAdminService.cs` | Implemented all three new service methods with duplicate-key guard |
| `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs` | Added `POST/PUT/DELETE /usage-policies/{policyId}/rules[/{ruleId}]` endpoints; added `MapRule` private helper |
| `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts` | Added `AddUsagePolicyRuleRequest`, `UpdateUsagePolicyRuleRequest` interfaces; added `addRule`, `updateRule`, `deleteRule` methods |
| `tests/LinguaCoach.UnitTests/UsageGovernance/UsageGovernanceUnitTests.cs` | Added 4 unit tests for `UsagePolicyRule.Update` |
| `tests/LinguaCoach.IntegrationTests/UsageGovernance/UsageGovernanceIntegrationTests.cs` | Added 8 service-level + 8 endpoint-level integration tests for rule CRUD |
| `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.spec.ts` | Added 3 HTTP tests for `addRule`, `updateRule`, `deleteRule` |

---

## Domain methods added

### `UsagePolicyRule.Update(...)`

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

Validates the same invariants as the constructor:
- All limit values non-negative when set
- `warningThresholdPercent` 0–100

`FeatureKey` is intentionally immutable after creation — changing the feature a rule targets is semantically equivalent to deleting the old rule and creating a new one.

---

## Service methods added

| Method | Behaviour |
|---|---|
| `AddRuleAsync(policyId, request, adminUserId)` | Validates policy exists; guards against duplicate `(policyId, featureKey)` via application-layer check; creates and persists the rule |
| `UpdateRuleAsync(policyId, ruleId, request, adminUserId)` | Verifies rule belongs to the given policy (tenant-safe join); calls `rule.Update(...)` |
| `DeleteRuleAsync(policyId, ruleId, adminUserId)` | Verifies rule belongs to policy; removes from DB |

---

## API endpoints added

| Method | Route | Success | Error cases |
|---|---|---|---|
| `POST` | `/api/admin/usage-policies/{policyId}/rules` | 201 Created + rule DTO | 404 policy not found, 400 bad field, 409 duplicate feature key |
| `PUT` | `/api/admin/usage-policies/{policyId}/rules/{ruleId}` | 200 OK + rule DTO | 404 not found / wrong policy, 400 bad field |
| `DELETE` | `/api/admin/usage-policies/{policyId}/rules/{ruleId}` | 204 No Content | 404 not found / wrong policy |

All three endpoints require `[Authorize(Roles = "Admin")]` (inherited from controller).

---

## Frontend service updated

Three new methods added to `UsageGovernanceService`:
- `addRule(policyId, req): Observable<UsagePolicyRule>`
- `updateRule(policyId, ruleId, req): Observable<UsagePolicyRule>`
- `deleteRule(policyId, ruleId): Observable<void>`

Two new request interfaces added:
- `AddUsagePolicyRuleRequest`
- `UpdateUsagePolicyRuleRequest`

No UI forms were built. The service layer is ready for a future rule editor component.

---

## Database migration added

No. The existing schema already supports full rule CRUD:
- `UsagePolicyRules` table with all required columns
- Cascade delete from policy to rules (`OnDelete(DeleteBehavior.Cascade)`)
- `UsagePolicyRules` DbSet on `LinguaCoachDbContext`

The optional unique constraint on `(UsagePolicyId, FeatureKey)` was not added as a migration in this phase. Duplicate-key prevention is enforced at the application layer in `AddRuleAsync`. A future phase can promote this to a DB-level constraint with a migration if needed.

---

## Test results

### Backend

- `git diff --check`: PASS
- `dotnet restore`: PASS
- `dotnet build --configuration Release`: PASS (0 errors, 5 pre-existing warnings)
- `dotnet test --configuration Release`: **PASS — 1905 tests (3 arch + 1237 unit + 665 integration), 0 failures**

New tests added:
- 4 unit: `UsagePolicyRule.Update` validation (negative limit, invalid threshold, valid update, clear to null)
- 8 service integration: add rule, duplicate key throws, policy not found, update fields, update wrong policy throws, delete removes from DB, delete wrong policy throws, enable/disable via update
- 8 endpoint integration: add rule 201, duplicate 409, policy not found 404, update 200, update wrong policy 404, delete 204, delete not found 404, non-admin 403 on all three methods

### Frontend

- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: **PASS — 670/670** (was 667, +3 new service tests)

---

## Remaining rule-management TODOs

- `TODO-10R-RULE-MGMT-UI`: Build the inline rule editor in the admin-usage-policies page. The backend is now ready. The frontend service is now ready. Next phase adds the create/edit/delete form UI in the expanded rule detail row.
- `TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT`: Optionally promote the duplicate-key guard from application layer to a DB unique index: `HasIndex(r => new { r.UsagePolicyId, r.FeatureKey }).IsUnique()` + migration.

---

## Confirmation

- Full admin rule editor UI was NOT implemented. Only service methods and API endpoints were added.
- No commit or push was made.
- No existing quota enforcement behavior was changed.
- No policy creation behavior was changed.
- No student policy assignment UI was added.
- No analytics, billing, or notifications were added.

---

## Documentation impact

- Docs reviewed: `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`, `TODOS.md`
- Docs updated: this review doc, `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`, `TODOS.md`
- Reason: backend capability added; TODO-10R-RULE-MGMT partially closed (backend done, UI deferred)
