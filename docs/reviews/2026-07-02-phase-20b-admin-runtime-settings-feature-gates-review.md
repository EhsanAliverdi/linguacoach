---
status: current
lastUpdated: 2026-07-02 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 20B — Admin Runtime Settings & Feature Gates — Engineering Review

**Date:** 2026-07-02
**Related sprint:** `docs/sprints/current-sprint.md` (Phase 20B entry)
**Related architecture doc:** `docs/architecture/runtime-settings-and-feature-gates.md`

## Files reviewed / touched

**Domain / Persistence:**
- `src/LinguaCoach.Domain/Entities/RuntimeSettingOverride.cs` (new)
- `src/LinguaCoach.Domain/Entities/LessonGenerationSettings.cs` (`ResetToDefaults()` added)
- `src/LinguaCoach.Persistence/Configurations/RuntimeSettingOverrideConfiguration.cs` (new)
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` (new `DbSet`)
- `src/LinguaCoach.Persistence/Migrations/20260702001652_T_Phase20B_RuntimeSettingOverride.{cs,Designer.cs}` (new, via `dotnet ef migrations add`)

**Application:**
- `src/LinguaCoach.Application/Admin/RuntimeSettings/FeatureGateEnums.cs` (new)
- `src/LinguaCoach.Application/Admin/RuntimeSettings/FeatureGateDefinition.cs` (new)
- `src/LinguaCoach.Application/Admin/RuntimeSettings/FeatureGateDtos.cs` (new)
- `src/LinguaCoach.Application/Admin/RuntimeSettings/IFeatureGateRegistry.cs` (new)
- `src/LinguaCoach.Application/Admin/RuntimeSettings/IRuntimeSettingsService.cs` (new)
- `src/LinguaCoach.Application/Admin/RuntimeSettings/FeatureGateDefinitions.cs` (new — static registry of 8 groups)

**Infrastructure:**
- `src/LinguaCoach.Infrastructure/Admin/FeatureGateRegistryService.cs` (new)
- `src/LinguaCoach.Infrastructure/Admin/RuntimeSettingsService.cs` (new)
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` (DI registration)

**API:**
- `src/LinguaCoach.Api/Controllers/AdminRuntimeSettingsController.cs` (new)

**Angular:**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` (feature-gate types appended)
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` (4 new methods)
- `src/LinguaCoach.Web/src/app/features/admin/admin-feature-gates/*` (new page + spec)
- `src/LinguaCoach.Web/src/app/app.routes.ts` (new route)
- `src/LinguaCoach.Web/src/app/design-system/admin/layouts/admin-app-layout/admin-app-layout.component.html` (new nav item, both mobile + desktop lists)
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/*` (Configure CTAs + spec)
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-operations/*` (Open-settings CTAs + spec)

**Tests:**
- `tests/LinguaCoach.UnitTests/Admin/RuntimeSettingsServiceTests.cs` (new, 13 tests)
- `tests/LinguaCoach.IntegrationTests/Api/AdminRuntimeSettingsEndpointTests.cs` (new, 14 tests)
- `src/LinguaCoach.Web/.../admin-feature-gates.component.spec.ts` (new, 19 tests)
- Extended `admin-lessons.component.spec.ts`, `admin-ai-operations.component.spec.ts`

## Findings by priority

No blocking findings. Two implementation issues were found and fixed during
this same session (not left as follow-up):

1. **High (fixed) — static field initialization order bug.** `FeatureGateDefinitions.All`
   was originally a field-initialized property (`= BuildAll()`), which in C#
   runs in textual declaration order — since `All` was declared *before* the
   individual group fields (`ReviewScaffoldGeneration`, etc.), those fields
   were still `null` when `BuildAll()` ran, causing a `NullReferenceException`
   on every call to `GetGroup()`. Fixed by making `All` a lazily-evaluated
   property (`_all ??= BuildAll()`), which runs after all static field
   initializers regardless of declaration order. Caught immediately by the
   first unit test run.
2. **Medium (fixed) — Angular template/API mismatches.** `sp-admin-select`
   and `sp-admin-checkbox` don't expose a plain `[value]`/`[checked]` input
   (they're `ControlValueAccessor`s without an `@Input` alias for the CVA
   value), and Angular templates can't contain arrow-function expressions
   (`v => ({...})`). Both were caught by `ng build --configuration
   production` and fixed by switching to `[ngModel]`/`(ngModelChange)`
   bindings and moving the array-mapping into a component method
   (`allowedValueOptions()`).

## Decisions made

- **Scope split by backing store, not by category.** Review-scaffold/
  Practice-Gym-pilot settings get a brand-new `RuntimeSettingOverride`
  table (no prior override mechanism existed). Lesson-generation settings
  reuse the existing `LessonGenerationSettings` table/endpoint verbatim
  (already DB-backed) — the registry wraps it rather than duplicating
  storage. This halved the amount of new persistence code needed.
- **AI signal-safety gates stay 100% read-only this phase**, per explicit
  product/safety instruction, even though `ApplyMasterySignals`/
  `AllowReviewSignals`/`AllowPositiveSignals` are technically mutable
  appsettings values. `AllowObjectiveCompletion`/`AllowCefrUpdate` are
  hardcoded `false` in code and reported with `ValueSource.Hardcoded`.
- **"AI can regenerate Learning Plan" has no dedicated flag** anywhere in
  `LearningPlanOptions` or elsewhere in the codebase. Rather than invent
  one, it's registered as an informational, always-locked, no-settings
  entry (`learning-plan-regeneration`) with a description explaining there
  is no runtime toggle today.
- **Dependency enforcement is informational only** (text shown in the
  drawer, e.g. "RequireAdminReview should be true"), not a hard
  cross-field validation engine. This matches the phase's "don't overbuild"
  instruction — the dependency list is short and human-readable today.
- **Confirmation is a single typed `CONFIRM` string**, required whenever
  any touched setting in the update request has `RequiresConfirmation =
  true` (High/Critical risk: `EnableReviewScaffoldGeneration`,
  `AllowTodayLessonInsertion`). Kept intentionally simple rather than
  per-setting confirmation phrases.
- **Reset is a soft deactivation**, not a row delete, for
  `RuntimeSettingOverride` rows (`IsActive = false`). Change history lives
  in `AdminAuditLog`, not in per-row history on the override table itself.

## AskUserQuestion decisions

None asked during implementation — the phase brief and codebase survey
were sufficiently unambiguous once the backing-store split (above) was
identified. The implementation plan was reviewed and approved via
`ExitPlanMode` before any code was written.

## Implementation tasks produced

All tracked to completion in this session (no carry-over tasks):
domain entity + migration, application registry, infrastructure service,
API controller + DI, backend tests, frontend models/API/page/drawer,
route/nav/CTA wiring, frontend tests, docs.

## Risks / unresolved questions

- `ReadinessPoolReplenishmentService` does not yet consult
  `RuntimeSettingOverride` — admin edits to review-scaffold/pilot settings
  are visible and audited but do not yet change live replenishment
  behavior. Tracked as `TODO-20B-1`. This was an explicit, accepted
  in-scope decision per the phase brief, not an oversight.
- The confirmation-text UX (typing the literal word `CONFIRM`) is
  functional but minimal; a future pass could show the specific setting
  name being confirmed rather than a single generic prompt across the
  whole update request.

## Final verdict

Ready to ship. All acceptance criteria from the approved plan are met:
registry-backed typed gates, editable Practice-Gym-pilot/review-scaffold
and lesson-generation settings behind a slide-in drawer, AI signal-safety
gates visible but locked, reason + validation + audit on every change,
reset-to-default for editable groups, existing disabled-state admin cards
now link to the settings page, design-system components reused
exclusively (no new shared components needed), full backend/frontend test
suites green with zero new regressions, production build clean.

## Next recommended action

Consider `TODO-20B-1` (wiring the override table into the live
replenishment read path) as the natural follow-up once the admin UI has
been used in practice — that's the point at which admin edits to
review-scaffold/pilot settings will actually change student-facing
behavior, so it should be scheduled deliberately rather than bundled into
this control-plane-only phase.
