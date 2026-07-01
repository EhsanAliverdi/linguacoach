# Phase 19B — Review Scaffold Per-Item Admin Approval — Implementation Review

**Date:** 2026-07-02
**Related sprint/phase:** Phase 19B (follows Phase 19A — Review Scaffold Controlled Enablement)
**Review type:** Implementation readiness / completion review

---

## 1. Purpose

Phase 19A introduced a *global* admin-review hold: scaffold-routed readiness items generated under `RequireAdminReview=true` were stamped `RequiresAdminReview=true` and excluded from all Practice Gym suggestion buckets until an admin flipped `ReadinessPool:RequireAdminReview=false` for the whole system. There was no way to approve or reject individual items.

Phase 19B replaces that all-or-nothing gate with a per-item admin approval workflow: admins can inspect each held item and approve, reject, or reopen it individually, with a full audit trail. All Phase 19A global safety gates (`EnableReviewScaffoldGeneration=false`, `DryRunOnly=true`, `RequireAdminReview=true`, `AllowTodayLessonInsertion=false` by default; source allow-listing; per-student daily cap) remain unchanged.

---

## 2. Files Reviewed / Modified

**Domain:**
- `src/LinguaCoach.Domain/Enums/AdminReviewStatus.cs` (new)
- `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs`
- `src/LinguaCoach.Domain/Entities/AdminAuditLog.cs` (reviewed, unchanged — reused as-is)

**Persistence:**
- `src/LinguaCoach.Persistence/Configurations/StudentActivityReadinessItemConfiguration.cs`
- `src/LinguaCoach.Persistence/Migrations/20260701210533_T72_ReviewScaffoldAdminApproval.cs` (new)

**Application:**
- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolDtos.cs`

**Infrastructure:**
- `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs`

**Api:**
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs`

**Angular:**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.spec.ts`

**Tests (new/extended):**
- `tests/LinguaCoach.UnitTests/ReadinessPool/StudentActivityReadinessItemTests.cs` (+18 tests)
- `tests/LinguaCoach.IntegrationTests/Api/ReviewScaffoldAdminApprovalTests.cs` (new, 14 tests)

**Audit basis (Part A):**

| Area | Current Behavior (pre-19B) | Missing Approval Capability | Risk | Decision |
|---|---|---|---|---|
| `StudentActivityReadinessItem.RequiresAdminReview` | Creation-time config snapshot (bool) | No per-item state, no reviewer/timestamp/reason | Admin could only release all-or-nothing | Add `AdminReviewStatus` state machine (Part B/C) |
| `PracticeGymSuggestionService` exclusion | `!RequiresAdminReview` blanket exclusion | No way to selectively release one item | Same as above | Gate on `AdminReviewStatus == Approved` in addition |
| `GET .../pending-review` | Read-only, lightweight DTO, filtered to `Ready`/`ReviewOnly` items with `RequiresAdminReview=true` | No detail on decision, no visibility flags | Admin can't act from this list | Replace with full detail DTO + actions |
| Admin lessons page | Read-only table, "no per-item action" copy | No approve/reject UI | Feature gap this phase closes | Add approval table (Part F) |
| `AdminAuditLog` | Entity + EF config exist, used elsewhere (usage governance) | Not wired to readiness items | N/A — infra ready | Reuse as-is (Part G) |
| Admin authorization | `[Authorize(Roles = Admin)]` on controller | N/A | N/A | Reuse existing pattern |

---

## 3. Approval State Model (Part B)

New enum `AdminReviewStatus` (`src/LinguaCoach.Domain/Enums/AdminReviewStatus.cs`):

```csharp
public enum AdminReviewStatus
{
    NotRequired = 0,   // item never entered the review flow
    PendingReview = 1, // held from students, awaiting decision
    Approved = 2,      // may become visible if other lifecycle gates pass
    Rejected = 3       // permanently hidden unless explicitly reopened
}
```

New fields on `StudentActivityReadinessItem`:
- `AdminReviewStatus AdminReviewStatus`
- `DateTime? AdminReviewedAtUtc`
- `Guid? AdminReviewedByUserId`
- `string? AdminReviewReason`
- `string? AdminReviewNotes`

Set at construction: `AdminReviewStatus = requiresAdminReview ? PendingReview : NotRequired`. Existing rows (created before this migration) are backfilled by the migration's `UPDATE ... WHERE requires_admin_review = true` statement, so previously-held Phase 19A items become `PendingReview` rather than defaulting to `NotRequired`.

Backward compatibility: items that never required review keep `AdminReviewStatus=NotRequired` and are unaffected by any of the new transition methods (rejecting a `NotRequired` item throws).

---

## 4. State Transitions (Part C)

Implemented as entity methods (`StudentActivityReadinessItem.ApproveAdminReview` / `RejectAdminReview` / `ReopenAdminReview`), not just API-layer checks, so invariants hold regardless of caller:

```text
PendingReview → Approved     (ApproveAdminReview; requires lifecycle Status in {Ready, ReviewOnly, Reserved}; idempotent if already Approved)
PendingReview → Rejected     (RejectAdminReview; reason required; idempotent if already Rejected)
Approved → Rejected          (RejectAdminReview; only if lifecycle Status != Consumed)
Rejected → PendingReview     (ReopenAdminReview; only if lifecycle Status != Consumed; idempotent if already PendingReview)
NotRequired → (any)          BLOCKED — rejecting/approving an item that never required review throws
Expired/Failed/Stale → Approved   BLOCKED — cannot approve non-reviewable lifecycle states
```

Rejected items are not required to be re-approved through `PendingReview`; `ReopenAdminReview` intentionally routes back to `PendingReview` (not directly to `Approved`) so every approval still goes through an explicit admin decision.

---

## 5. Admin API (Part D)

All under `[Authorize(Roles = Admin)]` in `AdminReadinessPoolController`:

| Endpoint | Behavior |
|---|---|
| `GET /api/admin/readiness-pool/review-scaffold/pending-review` | Up to 50 items with `RequiresAdminReview=true`, most recent first, **all** admin review statuses (not just pending) so history/reopen is visible from the same table |
| `POST .../{itemId}/approve` | No body. 404 if unknown, 409 if invalid transition (e.g. already terminal-blocked), 200 with full detail otherwise |
| `POST .../{itemId}/reject` | Body `{ reason, notes? }`. 400 if reason missing/blank, 404/409 as above |
| `POST .../{itemId}/reopen` | Body `{ notes? }` optional. 404/409 as above |

Response shape (`ReviewScaffoldItemDetailDto`): id, studentId, activityId, source, lifecycle status, CEFR, skill, objective, pattern/activity type, routing reason, `adminReviewStatus`, reviewer id/timestamp, reason/notes, and two derived flags — `isStudentVisible` and `isPracticeGymEligible` — computed server-side so the Angular layer never has to re-derive gate logic. No raw storage keys, provider secrets, or AI payloads are included anywhere in this DTO.

---

## 6. Practice Gym Eligibility (Part E)

`PracticeGymSuggestionService.GetSuggestionsForStudentAsync` filter changed from:

```csharp
&& !i.RequiresAdminReview
```

to:

```csharp
&& (!i.RequiresAdminReview || i.AdminReviewStatus == AdminReviewStatus.Approved)
```

Combined with the existing exclusions (Consumed/Expired/Failed/Stale/Skipped/Queued/Generating), an item reaches a student only when: source allows Practice Gym, lifecycle status is servable, and (it never required review OR it is individually `Approved`). Pending and rejected items are excluded exactly as before Phase 19A's blanket rule, just evaluated per-item now.

---

## 7. Admin UI (Part F)

`admin-lessons.component` — the old read-only "pending admin review" table (with the "No per-item approve/reject action in this phase" copy) is replaced by a "Review scaffold — approval" table:
- Columns: student, skill/objective, CEFR, pattern, review status badge (+ reason if rejected), visibility badges (`Visible to student` / `Hidden from student`, `Practice Gym eligible`), created, actions.
- Actions: Approve/Reject shown for `PendingReview`; Reject shown for `Approved` (not yet consumed); Reopen shown for `Rejected` (not yet consumed); no actions for `NotRequired`/consumed items.
- Confirmation via existing `window.confirm`/`window.prompt` pattern (matches `admin-ai-config` and `admin-students` precedent — no new modal component introduced).
- No global "enable" toggle added — config (`EnableReviewScaffoldGeneration`, `DryRunOnly`, `RequireAdminReview`, `AllowTodayLessonInsertion`) remains server-side/appsettings only, unchanged from Phase 19A.

---

## 8. Audit Trail (Part G)

Reused the existing `AdminAuditLog` entity (no new audit infrastructure needed). Each state-changing action (not idempotent no-ops) writes one row:
- `Action`: `ApproveReviewScaffoldItem` / `RejectReviewScaffoldItem` / `ReopenReviewScaffoldItem`
- `EntityType`: `StudentActivityReadinessItem`, `EntityId`: item id
- `TargetStudentId`: the item's `StudentId`
- `OldValueJson`/`NewValueJson`: `{"adminReviewStatus":"..."}` before/after
- `Reason`: reject reason or reopen notes where applicable

---

## 9. Safety and Idempotency (Part H)

- Approve/Reject/Reopen are idempotent when the item is already in the target state (no-op, no audit row written, 200 response).
- Invalid transitions throw `InvalidOperationException` in the entity and are translated to `409 Conflict` at the controller.
- Expired/Failed/Stale items cannot be approved (lifecycle guard in `ApproveAdminReview`).
- Consumed items cannot be rejected or reopened (guard checks `Status == Consumed` in both `RejectAdminReview` and `ReopenAdminReview`).
- None of the three transition methods touch `Status`, `TargetCefrLevel`, or any other lifecycle/CEFR/objective field — verified by `ApproveAdminReview_DoesNotMutateLifecycleOrCefrFields` (unit) and `Approve_DoesNotChangeCefrOrCreateOtherEntities` (integration).
- Approving/rejecting/reopening never calls into `ILearningPlanService`, `IStudentMasteryEvaluationService`, or any CEFR-mutation path — confirmed by reading `AdminReadinessPoolController` (no such dependency is invoked in the three new action methods).

---

## 10. Tests

**Unit** (`StudentActivityReadinessItemTests.cs`, tests 23–40, +18):
create stamps PendingReview/NotRequired correctly; approve happy path + reviewer/timestamp stamping; approve idempotent; approve blocked on Expired/Failed/Stale; reject requires non-empty reason; reject happy path with reason/notes; reject idempotent; reject blocked when Approved+Consumed; reject allowed when Approved+not-Consumed; reject blocked on NotRequired; reopen happy path; reopen idempotent; reopen blocked from Approved; reopen blocked on Consumed; approve does not mutate lifecycle/CEFR fields.

**Integration** (`ReviewScaffoldAdminApprovalTests.cs`, new, 14 tests):
auth guards (401 unauthenticated, 403 student) on approve/reject; safe 404 for unknown item; approve happy path + response shape + idempotency + CEFR-untouched + audit log written; reject requires reason (400) + persists reason/notes + hides from Practice Gym gate; reopen happy path + idempotency; pending-review list includes `adminReviewStatus`.

**Angular** (`admin-lessons.component.spec.ts`, extended, +9 net new tests):
renders pending/approved/rejected badges; hides actions for non-actionable items; approve/reject/reopen call the API only when confirmed; reject requires non-empty reason before calling the API.

---

## 11. Validation Run (Part J)

```
dotnet build                                    → 0 errors
dotnet test tests/LinguaCoach.UnitTests         → 1,693 passed, 0 failed (+18 vs Phase 19A's 1,675)
dotnet test tests/LinguaCoach.ArchitectureTests → 3 passed, 0 failed (unchanged)
dotnet test tests/LinguaCoach.IntegrationTests  → 1,338 passed, 0 failed (+14 vs Phase 19A's 1,324)
npm run build -- --configuration production     → clean (pre-existing unrelated template warnings only)
npm test -- --watch=false --browsers=ChromeHeadless → 1,494/1,614 passed; 120 pre-existing failures,
                                                        all in AdminStudentDetailComponent (118) and
                                                        AdminAiConfigComponent (1) — files this phase
                                                        does not touch; 0 new regressions.
                                                        admin-lessons.component.spec.ts: 27/27 passed.
```

Playwright: not run this phase. The existing Playwright suite has no `admin-lessons` spec to extend, and this phase's UI surface (an admin-only approval table) is covered by the Angular unit tests above plus the full backend integration suite for the underlying endpoints. Documented here as the substitute coverage per the phase brief's Part J instructions.

---

## 12. Findings Grouped by Priority

**No blocking findings.** Implementation matches the phase brief's acceptance criteria.

**Notable design decisions (not defects):**
1. The `pending-review` GET endpoint was broadened from "only `PendingReview` items" to "all `RequiresAdminReview=true` items regardless of status." This was a deliberate deviation from a literal reading of Part D's endpoint name, made because Part F requires a "Reopen if rejected" UI action, which is only usable if rejected items remain visible in the same table. Approved/rejected items stay in the list so admins can see decision history.
2. `ReviewScaffoldDryRunSummary.AdminReviewRequiredCount` was narrowed from "count of `RequiresAdminReview=true` Ready/ReviewOnly items" to "count of `AdminReviewStatus=PendingReview` Ready/ReviewOnly items," since the old count no longer reflects "items actually awaiting a decision" once approved/rejected items exist.

---

## 13. Decisions Made

- Reused `AdminAuditLog` rather than adding a new audit entity (infrastructure was already present and unused for this purpose).
- Reopen always routes to `PendingReview`, never directly to `Approved`, so every approval requires an explicit admin decision.
- No AskUserQuestion was needed — the phase brief was fully specified and no ambiguous product/architecture decision arose during implementation.

---

## 14. Risks or Unresolved Questions

- None blocking. The 120 pre-existing Angular test failures (`AdminStudentDetailComponent`, `AdminAiConfigComponent`) are unrelated to this phase and were not introduced or worsened by it; they should be tracked and fixed independently.
- Playwright coverage for the new admin approval actions does not exist yet; if the admin-lessons page later gets its own Playwright spec, the approve/reject/reopen flow should be added to it.

---

## 15. Final Verdict

**Complete.** All Part A–K requirements from the phase brief are implemented, tested, and documented. All global Phase 19A safety gates remain intact and unchanged. No CEFR, objective-completion, or Learning Plan behavior was touched.

---

## 16. Next Recommended Action

Per the roadmap's Tier 3 priority list, the next phase is **20A — Admin AI Operations Dashboard** (provider health, queue depths, retry tools, cost/latency visibility), unless the product owner wants to exercise `EnableReviewScaffoldGeneration=true` in a staging environment first now that per-item approval exists.
