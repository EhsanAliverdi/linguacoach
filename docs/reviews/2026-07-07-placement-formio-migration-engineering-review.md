---
title: Adaptive Placement — Full Form.io-Native Migration
date: 2026-07-07
related: docs/architecture/formio-onboarding-placement-model.md, docs/architecture/placement-assessment-model.md
---

# Adaptive Placement — Full Form.io-Native Migration

## Title
Complete the adaptive placement migration to native Form.io authoring/rendering (backend + frontend)

## Date
2026-07-07

## Related sprint/feature
Continuation of the Form.io onboarding/placement migration started in commit `246daead`
("Replace onboarding/placement with Form.io-authored flows + custom renderer"). This phase
finishes placement specifically — onboarding was already fully migrated in the prior commit.

## Files reviewed
- `src/LinguaCoach.Domain/Entities/PlacementItemDefinition.cs`, `PlacementAssessmentItem.cs`,
  `PlacementAssessment.cs`, `PlacementAnswer.cs` (deleted)
- `src/LinguaCoach.Domain/Questions/*` (deleted)
- `src/LinguaCoach.Infrastructure/Placement/*` (PlacementAssessmentService, PlacementScoringService,
  QuestionContentToFormIoMapper (deleted), AiPlacementEvaluator/FakePlacementEvaluator (deleted),
  legacy PlacementService/PlacementAudioService (deleted))
- `src/LinguaCoach.Infrastructure/Onboarding/FormIoSchemaValidationService.cs` (reused, unchanged)
- `src/LinguaCoach.Application/Placement/*` (PlacementAssessmentDtos, PlacementItemBankContracts,
  new PlacementScoringRules.cs)
- `src/LinguaCoach.Api/Controllers/StudentPlacementController.cs`, `AdminPlacementItemController.cs`,
  `AdminPlacementController.cs`, `PlacementController.cs` (deleted)
- `src/LinguaCoach.Persistence/Seed/PlacementItemBankSeeder.cs`,
  `PlacementAssessmentItemContentBackfiller.cs` (deleted)
- `src/LinguaCoach.Web/src/app/features/student/placement/*`,
  `src/LinguaCoach.Web/src/app/features/admin/admin-placement-items/*`
- `src/LinguaCoach.Web/src/app/core/services/placement.service.ts`,
  `src/LinguaCoach.Web/src/app/core/models/placement.models.ts`
- `src/LinguaCoach.Web/src/app/core/guards/placement.guard.ts`
- `src/LinguaCoach.Web/src/app/features/student/dashboard/dashboard/dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/shared/question/*` (deleted)
- Backend/Angular test suites (see Testing section)

## Scope decisions (AskUserQuestion)
Two scope decisions were confirmed with the user before implementation, via AskUserQuestion:

1. **Submission wire contract**: the respond endpoint now carries the full Form.io
   `submission.data` dictionary (`{ assessmentId, itemId, submission: { data: {...} }, ... }`),
   replacing the old single-string `Response` field with no backward-compat shim. Chosen because
   nothing else called the old shape, and the task explicitly required multi-component group
   items to score every component, not just the first.
2. **Legacy dead-code removal**: the separate, already-dead `/api/placement/*` controller stack
   (`PlacementController`, `PlacementHandlers`, `PlacementAnswer`, `AiPlacementEvaluator`/
   `FakePlacementEvaluator`, legacy `PlacementService`/`PlacementAudioService`) was confirmed
   in-scope for removal in this phase, after verifying zero remaining frontend callers.

## Findings, grouped by priority

### High — found and fixed during this session
- **Route guard / dashboard silently broken by the legacy-controller removal.** The backend
  agent correctly deleted the dead `/api/placement/*` stack, but two frontend call sites were
  still wired to it: `placement.guard.ts` (`placementRequiredRedirectGuard`,
  `placementAccessGuard`) called `PlacementService.getStatus()` → `GET /api/placement/status`
  (now 404), and `dashboard.component.ts` called `PlacementService.getResult()` →
  `GET /api/placement/result` (now 404). Both guards swallow errors via `catchError(() => of(true))`,
  so the failure mode was silent — every request would 404, the guard would allow navigation
  through regardless of placement state, and the dashboard's estimated-level card would silently
  show nothing. This was caught because the frontend implementation agent flagged it explicitly
  in its completion report as "out of scope, not fixed" rather than missing it.
  - **Fix**: `placement.guard.ts` now calls `DashboardSummaryService.getSummary()` (a live,
    non-deleted endpoint that already exposes `courseReadiness.lifecycleStatus`) instead of the
    deleted status endpoint. `dashboard.component.ts` now calls the existing
    `PlacementService.getAdaptiveCurrent()` (`GET /api/student/placement/current`) and maps the
    `AdaptivePlacementSummary` onto the dashboard's `PlacementResult` display shape (a private
    `toPlacementResult()` mapper — `recommendedStartingCourse`/`recommendedSessionDuration` have
    no adaptive equivalent and are set to `null`; the template already guards on these being
    falsy, so that section simply doesn't render, which is correct — adaptive placement doesn't
    produce those fields).
  - Verified via targeted Karma run (73/73 passing across `dashboard`, `placement`,
    `placement-cards`, `admin-placement-items` specs) and a clean production build.

### Medium — none outstanding
No medium-severity findings remained after the fix above; the two implementation agents' own
build/test loops caught and resolved compile-time issues before reporting completion.

### Low — informational
- `admin-onboarding-editor.component.spec.ts` needed a one-line unrelated fixture fix
  (`rendererKind: 'FormIo'`) to unblock the Karma test bundle from a pre-existing TS compile
  error that predates this session (confirmed via git log). Not part of this migration's scope;
  left as the minimal change needed to get a green test run.
- Pre-existing, unrelated Angular test failures (121 total, e.g. `AdminStudentDetailComponent`
  missing a mock method, `AdminAiConfigComponent`, an admin-wrapper migration diagnostics test)
  were left untouched per the task's instruction to group and not fix unrelated failures.

## Decisions made
- Full conversion of all 72 seeded placement items to native Form.io schemas + scoring rules,
  rather than keeping the `QuestionContentToFormIoMapper` fallback indefinitely — consistent with
  "prefer complete conversion" guidance and the fact that this is not production data.
- `PlacementAssessmentItem` snapshots `FormIoSchemaJson`/`ScoringRulesJson`/`ScoringRulesVersion`
  at issuance time so that editing an item definition later cannot retroactively change how an
  already-answered item was scored/audited.
- Kept the adaptive engine (`PlacementAssessmentService`) algorithmically untouched — confirmed
  by the full backend test suite passing unchanged (selection, confidence, completion, CEFR
  finalization tests all green).

## Implementation summary

**Backend** (domain model, EF migration `T_NativeFormIoPlacementItems`, scoring rules POCOs +
rewritten `PlacementScoringService.ScoreSubmission` scoring every component independently, admin
contracts/handlers, adaptive engine seam, student API contract, 72-item seed conversion, legacy
dead-code removal, backend tests): see prior implementation session. 3129/3129 backend tests
passing (5 architecture + 1769 unit + 1355 integration), 0 failures, confirmed by re-running the
full suite after the frontend fix above (no backend files touched in this fix).

**Frontend**: student placement submission-dict flow, admin placement items now
Form.io-primary (backend-only scoring-rules panel with client-side orphaned-key validation
mirroring the backend's `PlacementFormIoScoringValidator`), `QuestionEditorComponent` +
`question-content.models.ts` deleted (confirmed zero remaining references repo-wide). Plus the
guard/dashboard fix documented above. Angular: 1575 passed / 121 failed overall, all failures
pre-existing and unrelated; zero failures in placement-related specs. Production build succeeds.

## Risks or unresolved questions
- Speaking/writing placement items needing AI evaluation remain a placeholder
  (`requiresManualOrAiEvaluation`) excluded from adaptive selection — same functional gap as
  before this migration, not introduced by it, and out of scope per the task's own instructions.
- No other legacy `PlacementService` call sites were found beyond the guard/dashboard ones fixed
  here, but this was verified by repo-wide grep rather than an exhaustive runtime trace — worth a
  final manual browser pass (see Validation Gates below) before considering this fully closed.

## Final verdict
**Complete.** All 10 required backend work items and all 3 required frontend work items from the
task are implemented, tested, and verified. The one cross-cutting regression (route guard /
dashboard hitting deleted legacy endpoints) introduced by the two agents' scope split was caught
and fixed in this same session before landing.

## Next recommended action
Run the manual browser validation checklist (admin authoring → scoring rule entry → preview →
student adaptive flow → completion → history, checking browser console is clean) before this is
considered ready to commit. No commit/push/deploy has been performed as part of this work.
