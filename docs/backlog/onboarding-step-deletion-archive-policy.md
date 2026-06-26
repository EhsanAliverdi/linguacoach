# Onboarding Step Deletion — Archive and Versioning Policy

**Date:** 2026-06-26
**Related sprint:** Phase 11A — Admin Onboarding Builder
**Status:** Deferred — policy decision only, not yet implemented

---

## Context

The Phase 11A admin onboarding builder exposes a `DELETE /api/admin/onboarding/flows/{id}/steps/{key}` endpoint. This performs a hard delete of an `OnboardingStepDefinition` row from the database.

During Phase 11A, a gap was identified: if a student begins onboarding against flow version N, and an admin deletes a step while that student is mid-flow, the student's `OnboardingProgress` record may reference a step key that no longer exists. This can cause:

- `KeyNotFoundException` or null-dereference in the onboarding query handler
- Incorrect `PercentageComplete` calculation
- Broken "current step" resolution

---

## Decision: Deferred

Implementing a full archive/versioning model was deferred from Phase 11A because:

1. In the current deployment, the active flow is created by the seeder and rarely modified after students begin onboarding.
2. No student-facing feature currently depends on consistent step history across a flow version change.
3. The complexity of a proper versioned flow model (e.g. flow snapshots or soft-delete with `ArchivedAt`) is out of scope for Phase 11A.

---

## Recommended Implementation (when prioritised)

### Option A: Soft-delete with `IsArchived` flag (recommended)

Add `bool IsArchived` and `DateTimeOffset? ArchivedAt` to `OnboardingStepDefinition`.

- The `DELETE` endpoint sets `IsArchived = true` and `ArchivedAt = UtcNow` instead of performing a hard delete.
- The admin UI filters out archived steps from the builder view.
- The student-facing onboarding query continues to resolve archived steps so in-flight progress is not broken.
- Once all students have completed or abandoned flows that reference the archived step, a background job can hard-delete.

**Domain change:** `OnboardingStepDefinition.Archive()` method; `RemoveStep` on the aggregate sets `IsArchived` rather than removing from the collection.

**EF migration:** Add `IsArchived` (default `false`) and `ArchivedAt` (nullable) columns.

### Option B: Flow versioning (full solution)

Create a new `OnboardingFlowDefinition` row (bumped version) each time the admin modifies step structure. Students stay locked to the flow version that was active when they started.

- More correct semantically, but requires `OnboardingProgress` to carry a `FlowDefinitionId` FK (it currently does not).
- Higher migration and UI complexity.
- Suitable only if multi-version flows become a product requirement.

---

## Risks if deferred without mitigation

| Risk | Likelihood | Impact |
|------|-----------|--------|
| Admin deletes step while student is mid-flow | Low (admin action required) | Medium (student sees error) |
| Percentage calculation wrong after step deletion | Medium (auto-triggered on reload) | Low (cosmetic) |

---

## Interim safeguard (already in place)

The admin UI shows a confirmation prompt before deleting a step (implemented in Phase 11A). This reduces accidental deletion but does not eliminate in-flight risk.

---

## Implementation tasks (when deferred work is picked up)

1. Add `IsArchived` / `ArchivedAt` to `OnboardingStepDefinition` entity and EF configuration.
2. Write migration.
3. Update `AdminRemoveOnboardingStepHandler` to soft-delete.
4. Update `IAdminOnboardingFlowListQuery` and `IAdminOnboardingFlowQuery` handlers to filter `IsArchived = false` in admin views.
5. Keep student-facing query (`IOnboardingV2Query`) resolving archived steps for in-flight progress.
6. Add unit and integration tests for the soft-delete path.
7. Update this document and remove the "deferred" status.

---

## Related files

- `src/LinguaCoach.Domain/Entities/OnboardingStepDefinition.cs`
- `src/LinguaCoach.Domain/Entities/OnboardingFlowDefinition.cs`
- `src/LinguaCoach.Infrastructure/Onboarding/AdminRemoveOnboardingStepHandler.cs`
- `src/LinguaCoach.Infrastructure/Onboarding/OnboardingV2QueryHandler.cs`
- `docs/architecture/README.md`
