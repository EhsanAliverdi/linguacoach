# Delivery Health — Bank-First Rehaul

**Date:** 2026-07-17
**Related sprint / feature:** Admin diagnostics. Follows the I-track (`docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md`, `-i2c-readiness-pool-removal-review.md`), which deleted the legacy per-exercise generation pipeline and the readiness pool but left the admin "Today Delivery Health" page (`/admin/lessons`) displaying dead generation-buffer/settings/batches UI backed by honest-no-op endpoints.

## Context / decision

The user asked whether the "Today Delivery Health" admin page was still functional and reflected the current bank-first architecture. Investigation confirmed: the page rendered without error, but most of its content described a pipeline deleted in Phase I2B/I2C — `AdminGenerationController`'s `RetryBatch`/`GenerateLessons` actions were honest `409` no-ops, and the ready-lesson-buffer table / generation-settings form displayed historical or permanently-inert data. Only the Mastery Validation section was live.

Decision: rehaul the page to (1) delete the dead admin-facing generation surface, and (2) replace it with a real fleet-wide health view over the *current* content-delivery path — the bank-first module-selection pipeline (`ITodayPlanModuleSelectionService` / `IPracticeGymModuleSelectionService`), which records per-student-per-day `Selected`/`FallbackOnly` bookkeeping rows but previously had no aggregate/fleet-wide admin view, only per-student diagnostics. Renamed the page from "Today Delivery Health" to **"Delivery Health"** (user-requested) since it now covers both Today and Practice Gym, not just Today.

## Files reviewed

Backend: `AdminGenerationController.cs` (deleted), `AdminTodayPlanModuleController.cs`, `AdminPracticeGymModuleController.cs`, `TodayPlanModuleSelectionContracts.cs`, `PracticeGymModuleSelectionContracts.cs`, `StudentTodayPlanModuleAssignment.cs`/`TodayPlanModuleAssignmentStatus.cs`, `StudentPracticeGymModuleAssignment.cs`/`PracticeGymModuleAssignmentStatus.cs`, `Module.cs`, `StudentProfile.cs`, `LessonGenerationSettings.cs`, `GenerationBatch.cs`/`GenerationJobItem.cs`, `StudentReadinessAuditService.cs`, `RuntimeSettingsService.cs`, `FeatureGateDefinitions.cs`, `AdminHandler.cs`, `AdminEndpointTests.cs`, `TodayPlanModulePipelineEndpointTests.cs`, `PracticeGymModulePipelineEndpointTests.cs`.
Frontend: `admin-today-delivery-health.component.ts`/`.html`/`.spec.ts` (deleted), `admin.api.service.ts`, `admin.models.ts`, `admin-integrations.service.ts`, `admin-integrations.component.ts`, `app.routes.ts`, `admin-app-layout.component.html`.
Docs: `docs/architecture/bank-first-admin-backend-surface-audit.md`, `docs/architecture/readiness-pool.md`.

## Findings and decisions, by priority

### P0 — New fleet-wide delivery-health aggregate endpoints

Added `GET /api/admin/today-plan/delivery-health?days={n}` on `AdminTodayPlanModuleController` and `GET /api/admin/practice-gym/delivery-health?days={n}` on `AdminPracticeGymModuleController` (default 7-day lookback, max 90). Both are read-only, admin-only, no mutation — same pattern as the controllers' existing `preview`/`assignments` actions. Each returns:

- `today` — eligible students (CEFR-placed), selected/suggested count, fallback-only count, no-assignment-yet count.
- `byCefrLevel` — the same breakdown per CEFR level.
- `trend` — one bucket per day over the lookback window.
- `topFallbackReasons` — top 5 non-null `FallbackReason` values by count.
- `bankCoverage` — approved-Module count per CEFR level with ≥1 eligible student, flagging levels with 0 approved Modules (the leading cause of fallback).

"Eligible" is defined as `StudentProfile.CefrLevel != null` (placement completed) rather than replicating `StudentDashboardSummaryHandler`'s `LifecycleStage`-based "course active" computation, which would have added unwanted coupling to dashboard-internal logic for a diagnostics endpoint.

New `Application`-layer DTOs added to `TodayPlanModuleSelectionContracts.cs`/`PracticeGymModuleSelectionContracts.cs` alongside the existing selection contracts, following the file's established pattern. The Practice Gym DTO field is named `SelectedCount` (not `SuggestedCount`) even though it counts "suggested" assignments, so both pipelines share one frontend shape (`AdminDeliveryHealth`) — documented in the record's doc comment.

**SQLite EF-provider constraint (test-only, matches an existing documented pattern):** the initial Practice Gym query filtered `StudentPracticeGymModuleAssignment.SuggestedAt` (a `DateTimeOffset` column) server-side with `.Where(a => a.SuggestedAt >= sinceOffset)`, which threw `InvalidOperationException: could not be translated` under the SQLite EF provider used by the test suite. `PracticeGymModuleAssignmentRecorder.cs` already documents this exact SQLite limitation and works around it by fetching rows first and filtering client-side; the new endpoint now does the same. Today Plan's assignment table uses a plain `DateTime` (`AssignedForDate`), which translates fine, so it needed no such workaround.

### P0 — Deleted the dead generation admin surface, judgment call on scope

Original plan (approved by the user) was to delete `AdminGenerationController` **and** the `LessonGenerationSettings`/`GenerationBatch`/`GenerationJobItem` entities plus a migration dropping their tables. Investigation before executing found materially wider coupling than assumed:

- `LessonGenerationSettings` backs **3 live feature-gate groups** (`lesson-generation-buffer`, `tts-generation`, `practice-gym-generation-per-type`) read/written through the generic `/admin/settings/feature-gates` page via `RuntimeSettingsService`/`FeatureGateDefinitions`.
- `StudentReadinessAuditService.AddAudioTtsChecksCoreAsync` — a genuinely live per-student readiness-audit check (`GET /api/admin/students/{id}/readiness`) — reads `LessonGenerationSettings.EnableTtsGeneration` to report a "TTS generation setting" diagnostic line.
- `AdminHandler`'s student "clear courses and sessions" reset action deletes `GenerationJobItem`/`GenerationBatch` rows for that student as part of cleanup.
- `LearningSession.GenerationBatchId` is a live, mapped FK column pointing at `GenerationBatch`.

Deleting the entities would have required rewiring the feature-gate registry, stripping a check from the student-readiness audit, editing the student-reset flow, and a migration touching a live entity's FK — a much larger and riskier change than "remove the dead admin page," and outside what the user asked to rehaul. **Judgment call:** scaled back to deleting only the dead HTTP surface (`AdminGenerationController` in full) and keeping the entities/tables in place — same "harmless but inert" disposition Phase I2B already gave these settings. Flagged here per the "flag anything ambiguous" convention the I2B/I2C reviews use.

Storage-integration actions (`GetStorage`/`UpdateStorage`/`TestStorage`) were relocated verbatim into a new `AdminIntegrationsController` (same routes, same behavior) since they're unrelated to generation and are genuinely called by the live `/admin/integrations` page (`AdminIntegrationsService.getStorage()`/`.testStorage()`). `AdminGenerationController.cs` deleted entirely.

`CancelBatch` (`POST /admin/generation/batches/{id}/cancel`) was deleted along with the rest of the controller — its only test (`CancelGenerationBatch_AsAdmin_MarksRunningBatchFailed` in `AdminEndpointTests.cs`) exercised nothing but that HTTP action and was removed. No frontend caller existed for it (confirmed via grep) beyond an unused duplicate method on `AdminIntegrationsService`, also removed.

### P1 — Frontend: dead API surface and duplicate methods removed

`admin.api.service.ts`: removed `generateLessonsForStudent`, `getGenerationSettings`, `updateGenerationSettings`, `getGenerationBatches`; added `getTodayPlanDeliveryHealth(days?)`/`getPracticeGymDeliveryHealth(days?)`. `admin.models.ts`: removed `AdminGenerationSettings`/`AdminUpdateGenerationSettingsRequest`/`AdminGenerationBatchesResponse`/`AdminGenerateLessonsResponse`/`AdminGenerationBatchSummary`/`AdminReadyBufferEntry`/`AdminGenerationBatchItem` (confirmed via grep: only referenced by the deleted component); added `AdminDeliveryHealth` and its section types, shared by both pipelines.

`admin-integrations.service.ts` had a second, **unused** copy of the generation settings/batches methods (`getGenerationSettings`, `updateGenerationSettings`, `getBatches`, `retryBatch`, `cancelBatch`, `generateLessons`) alongside its real, live storage methods — confirmed via grep these were never called from `admin-integrations.component.ts` or anywhere else. Removed along with their now-orphaned `GenerationSettings`/`GenerationBatch`/`BatchesResponse` interfaces.

### P1 — Component rehaul and rename

`admin-today-delivery-health/` deleted entirely; replaced with `admin-delivery-health/` (`AdminDeliveryHealthComponent`, selector `app-admin-delivery-health`). `app.routes.ts`'s `/admin/lessons` route repointed to the new component (route path kept, per the established "reframe in place, no deep links broken" convention from Phase G1). Nav label in `admin-app-layout.component.html` (desktop + mobile) changed from "Today Delivery Health" to **"Delivery Health"** — user-requested mid-task, since the page now covers Practice Gym too, not just Today.

New page structure: an info banner reframing the page around the bank-first module pipeline; a "Today" section and a "Practice Gym" section (each with a KPI row, a CEFR-level breakdown via `sp-admin-breakdown-bars`, a 7-day trend via `sp-admin-area-chart`, a top-fallback-reasons list, and a bank-coverage-gap warning); the Mastery Validation section carried over unchanged (still served by `AdminMasteryController`, unaffected by this rehaul).

Two icon-name bugs caught by `ng build` type-checking: `sp-admin-kpi-card`'s `icon` input uses its own narrower `KpiIcon` union (not the general `SpAdminIconName` type used elsewhere in the design system) — `alert-triangle`/`alert-circle` aren't in it. Fixed to `alert`/`clock`, which are.

### P2 — New integration tests; one pre-existing test removed

Added `DeliveryHealth_returns_today_byCefrLevel_trend_and_bankCoverage_sections` and `NonAdmin_rejected_for_delivery_health_endpoint` to both `TodayPlanModulePipelineEndpointTests.cs` and `PracticeGymModulePipelineEndpointTests.cs`, following the files' existing shared-fixture conventions (defensive assertions since other tests in the same fixture class seed cross-test data). `CancelGenerationBatch_AsAdmin_MarksRunningBatchFailed` removed from `AdminEndpointTests.cs` (tested only the deleted `CancelBatch` action).

New `admin-delivery-health.component.spec.ts` replaces the deleted spec: covers both delivery-health calls, the mastery-validation flow (ported near-verbatim from the deleted spec), the selected-rate/CEFR-breakdown/bank-gap computed signals, and error states for all three API calls.

## Migration

None. No entity shapes changed (see the P0 judgment-call section above — `LessonGenerationSettings`/`GenerationBatch`/`GenerationJobItem` were deliberately kept, not deleted).

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,818 / 3,818 passing, 0 failing** (`ArchitectureTests` 30/30, `UnitTests` 2,454/2,454, `IntegrationTests` 1,334/1,334).
- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries. The only pre-existing issue is the initial-bundle-size budget overage, predating this change.
- `npx ng test --include='**/admin-delivery-health.component.spec.ts'`: **could not run to completion** — karma compiles the whole spec bundle together, and 5 pre-existing, unrelated spec files (`activity-feedback-page.component.spec.ts`, `activity-lesson-submission.component.spec.ts`, `activity-lesson-vocab.component.spec.ts`, `presenters/test-helpers.ts`, `practice-gym.component.spec.ts`) fail to type-check against `activity.models.ts`/`practice-gym-suggestions.service.ts`'s `feedbackPolicy`/`moduleSuggestions` required fields. This is the exact same residual gap Phase I2B/I2C flagged on 2026-07-10, still unresolved seven days later. Mitigated the same way those reviews did: manual review of the new spec plus a clean `ng build` type-check (which does type-check all non-spec app source) — not a substitute for an actual karma run.

## Decisions made

1. Scaled back the backend entity deletion (see P0 above) — kept `LessonGenerationSettings`/`GenerationBatch`/`GenerationJobItem` due to live coupling into feature gates, the student-readiness audit, and the student-reset flow; deleted only the dead `AdminGenerationController` HTTP surface.
2. Storage-integration endpoints relocated to a new `AdminIntegrationsController` rather than deleted, since they're live and unrelated to generation.
3. Practice Gym's delivery-health DTO field named `SelectedCount` (not `SuggestedCount`) to share one frontend shape across both pipelines; documented the naming choice in the record's doc comment.
4. Page renamed "Today Delivery Health" → "Delivery Health" (user-requested) to reflect that it now covers both Today and Practice Gym.
5. "Eligible students" defined as CEFR-placed (`CefrLevel != null`) rather than replicating dashboard lifecycle-stage logic, to keep the diagnostics endpoint decoupled from dashboard-internal state machinery.

## Risks / unresolved questions

- The whole-suite frontend karma run remains blocked by the same 5 pre-existing broken spec files Phase I2B/I2C flagged on 2026-07-10 — still unresolved, still out of this task's scope. A future pass should fix those independently so karma becomes usable again.
- `LessonGenerationSettings`/`GenerationBatch`/`GenerationJobItem` remain in the schema, now with **zero remaining HTTP-facing readers/writers** beyond the feature-gate/readiness-audit/student-reset touchpoints documented above — a good candidate for a scoped future cleanup (rewire the 3 feature-gate groups, strip the TTS audit check, drop the FK) if someone wants to finish what this task deliberately did not.
- "Eligible students" (CEFR-placed) is a looser definition than "actually course-active" — a placed-but-not-yet-course-ready student would count as eligible even though they wouldn't yet see Today content. Acceptable for a diagnostics page; worth revisiting if it produces confusing numbers in practice.

## Final verdict

Rehaul complete and verified: 0 backend build errors, 3,818/3,818 tests passing, 0 new frontend build errors. `/admin/lessons` ("Delivery Health") now surfaces real bank-first module-selection health for both Today and Practice Gym instead of dead legacy-generation UI, while Mastery Validation continues unaffected. The one gap is the pre-existing, unrelated whole-suite karma blocker — flagged clearly rather than worked around by touching out-of-scope files.

## Next recommended action

None required to ship this change. Optional follow-ups, in priority order: (1) fix the 5 pre-existing broken frontend spec files so `ng test` is usable fleet-wide again; (2) decide whether to finish the entity cleanup flagged above (feature gates / readiness audit / student reset / FK) now that its full blast radius is mapped; (3) revisit the "eligible students" definition if it proves confusing against real data.
