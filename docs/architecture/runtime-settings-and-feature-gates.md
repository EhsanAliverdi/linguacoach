---
status: current
lastUpdated: 2026-07-02 (20C)
owner: architecture
supersedes:
supersededBy:
---

# Runtime Settings & Feature Gates (Phase 20B, wired in 20C)

## Problem this solves

Before this phase, review-scaffold, Practice Gym pilot, and lesson-generation
flags were either appsettings-only (requiring a redeploy to change) or, in
the case of `LessonGenerationSettings`, already DB-backed but with no
central admin UI, no validation catalogue, and no audit trail tied to a
typed registry. Admin pages showed raw text like *"Enable
`ReadinessPool:PracticeGymPilotEnabled` in config"* instead of a real
control.

This phase adds a single admin control plane: `/admin/settings/feature-gates`.

## Registry model

`LinguaCoach.Application.Admin.RuntimeSettings`:

- `FeatureGateSettingDefinition` — one settable field: key, display name,
  description, data type, default, editable/risk/confirmation flags,
  validation rule (min/max/maxLength/allowedValues).
- `FeatureGateGroupDefinition` — a gate plus its related settings edited
  together in one drawer: group key, category, backing store, read-only/
  requires-restart flags, dependency text, warning text, settings list.
- `FeatureGateDefinitions` (static class) — the source of truth: 8 groups,
  built from real options classes / DB entities only (see table below). No
  invented flags.
- `IFeatureGateRegistry` — returns the static definitions (no live values).
- `IRuntimeSettingsService` — resolves effective values, applies validated/
  audited updates and resets.

Implementations live in `LinguaCoach.Infrastructure.Admin`
(`FeatureGateRegistryService`, `RuntimeSettingsService`).

## Groups and their backing store

| Group key | Backing store | Notes |
|---|---|---|
| `review-scaffold-generation` | `ReadinessPoolReplenishmentOptions` (appsettings) + new `RuntimeSettingOverride` table | Editable. `EnableReviewScaffoldGeneration` and `AllowTodayLessonInsertion` require typed `CONFIRM` confirmation (High/Critical risk). |
| `practice-gym-review-scaffold-pilot` | same | Editable. Phase 19C pilot gate + label/reason/max-visible. |
| `lesson-generation-buffer` | existing `LessonGenerationSettings` DB table | Editable. Reuses the existing `GET/PATCH /api/admin/generation/settings` table and its `Update()` validation. |
| `tts-generation` | same table | Editable. |
| `practice-gym-generation-per-type` | same table | Editable. |
| `ai-signal-safety-speaking` | `SpeakingEvaluationOptions` (appsettings) | Read-only this phase. `AllowObjectiveCompletion`/`AllowCefrUpdate` are hardcoded `false` in code (not configurable at all). |
| `ai-signal-safety-writing` | `WritingEvaluationOptions` (appsettings) | Read-only this phase, same shape. |
| `learning-plan-regeneration` | none (informational) | No dedicated runtime flag exists in code for Learning Plan regeneration; shown for visibility only, not editable. |

## Effective value resolution

For `ReadinessPoolOverride`-backed groups: an active `RuntimeSettingOverride`
row (matched by `Key`) wins over the appsettings value. Resetting a group
deactivates (`IsActive = false`) its override rows rather than deleting
them.

For `LessonGenerationSettingsTable`-backed groups: the existing single-row
`LessonGenerationSettings` table is the source; if no row exists yet, the
class defaults are shown as `Default`. This is the **same table** the
existing `/api/admin/generation/settings` endpoint reads/writes — the
registry does not duplicate storage for this category.

For `AppSettingsReadOnly` groups: values always come from `IOptions<T>`;
`isEditableAtRuntime` is forced `false` regardless of the definition, and
`AllowObjectiveCompletion`/`AllowCefrUpdate` report `Hardcoded` as their
source.

## Runtime-effective wiring (Phase 20C)

Phase 20B's scope boundary said `ReadinessPoolReplenishmentService` read
only appsettings and did not consult `RuntimeSettingOverride`. Phase 20C
closes that gap for the safe, reviewed groups:

- **New `IEffectiveReadinessPoolSettingsProvider`**
  (`Application/ReadinessPool/IEffectiveReadinessPoolSettingsProvider.cs`,
  implemented by `Infrastructure/ReadinessPool/EffectiveReadinessPoolSettingsProvider.cs`)
  — clones the `IOptions<ReadinessPoolReplenishmentOptions>` snapshot, then
  applies any active `RuntimeSettingOverride` rows with key prefix
  `"ReadinessPool."` on top, one field at a time. Fails safe at two levels:
  a DB failure returns the unmodified appsettings clone (logged as a
  warning); a single corrupt override value is skipped (that field keeps
  its appsettings value) without affecting any other override.
- **`ReadinessPoolReplenishmentService` and `PracticeGymSuggestionService`**
  now depend on this provider instead of `IOptions<ReadinessPoolReplenishmentOptions>`
  directly. Each public entry point (`RunAsync`, `GetHealthAsync`,
  `GetSuggestionsForStudentAsync`) resolves the effective snapshot once at
  the top of the call; every private helper method keeps reading the same
  `_opts` field exactly as before — a deliberately minimal, low-risk diff.
  Since both services are `Scoped` and Quartz creates a fresh DI scope per
  job execution (and ASP.NET Core creates one per HTTP request), this is
  sufficient to make admin overrides take effect on the *next* run/request
  with no caching layer, no app restart, and no distributed cache needed.
- **`DryRunOnly` fix**: this flag existed since Phase 19A but was never
  actually read by the generation path — it was only ever displayed on the
  dry-run summary endpoint. `FillShortfallAsync` now skips persisting a
  scaffold/review item (computing routing/dedup/caps as normal, just not
  calling `CreateQueuedAsync`) whenever `EnableReviewScaffoldGeneration &&
  DryRunOnly` for that item. Normal new-learning generation is completely
  unaffected by this flag.
- **`review-scaffold-generation`** and **`practice-gym-review-scaffold-pilot`**
  groups are now fully runtime-effective: `EnableReviewScaffoldGeneration`,
  `DryRunOnly`, `RequireAdminReview`, `MaxScaffoldItemsPerStudentPerDay`,
  `ScaffoldAllowedSources`, `AllowTodayLessonInsertion`,
  `MinimumConfidenceForReviewNeed`, `PracticeGymPilotEnabled`,
  `PracticeGymPilotLabel`, `PracticeGymPilotReason`,
  `MaxStudentVisibleScaffoldSuggestions`.
- **`lesson-generation-buffer`** (`ReadyLessonBufferSize`, `RefillThreshold`,
  `RefillBatchSize`, `EnableBackgroundGeneration`) and
  **`practice-gym-generation-per-type`**
  (`PracticeGymRefillThresholdPerType`, `PracticeGymRefillCountPerType`)
  were **already runtime-effective before Phase 20C** — the consuming jobs
  (`LessonBufferRefillJob`, `LessonBatchGenerationJob`,
  `PracticeGymBufferRefillJob`) read `LessonGenerationSettings` directly
  from the database, the same row the admin PUT endpoint writes to. Phase
  20C only added tests/labeling confirming this, no code change needed.
- **Display-only (not wired, no code change)**: `MaxGenerationAttempts`,
  `GenerationTimeoutSeconds`, `MaxConcurrentGenerationJobs` (in
  `lesson-generation-buffer`), `EnableTtsGeneration`, `TtsTimeoutSeconds`,
  `MaxConcurrentTtsJobs` (`tts-generation`), and
  `PracticeGymReadyExercisesPerType` (`practice-gym-generation-per-type`)
  are editable and audited, but **no job in this codebase actually reads
  them today** (confirmed by code search — `ActivityMaterializationJob`
  and `PracticeGymGenerationJob` have no timeout/concurrency/attempt-count
  enforcement at all). Wiring these would mean *building new enforcement
  behavior* (a concurrency limiter, a timeout wrapper, a retry counter),
  not just redirecting a read — out of this phase's safe/limited scope.
  Each such setting's registry `Description` says so explicitly, and the
  admin drawer shows a "Display only — requires deployment" badge for
  them. See `TODO-20C-1`.
- **AI signal-safety gates and `learning-plan-regeneration`** remain
  exactly as Phase 20B left them: read-only, no new provider, no behavior
  change. No dangerous AI learning gate was made editable or wired.

Each `FeatureGateSettingDefinition` now carries an `IsRuntimeEffective`
flag (`Application/Admin/RuntimeSettings/FeatureGateDefinition.cs`) so the
admin drawer can show "Runtime effective" vs. "Display only" per editable
setting, surfaced through `FeatureGateSettingValueDto.IsRuntimeEffective`.

No AI scoring, CEFR update, objective completion, or Learning Plan
regeneration behavior changed as part of this phase.

## API

`Api/Controllers/AdminRuntimeSettingsController.cs`, admin-only:

- `GET /api/admin/runtime-settings/feature-gates`
- `GET /api/admin/runtime-settings/feature-gates/{key}`
- `PUT /api/admin/runtime-settings/feature-gates/{key}/settings` — body
  `{ values, reason, confirmationText? }`
- `DELETE /api/admin/runtime-settings/feature-gates/{key}/override` — body
  `{ reason }`

Validation: unknown group/key → 404, read-only/locked setting or group →
400, out-of-range/invalid value → 400, missing reason → 400, missing
`CONFIRM` on a High/Critical-risk change → 400.

## Audit

Every successful update or reset writes one `AdminAuditLog` row per
changed key (`Action` = `UpdateFeatureGate` / `ResetFeatureGateOverride`,
`EntityType` = `RuntimeSettingOverride` or `LessonGenerationSettings`,
`EntityId` = the setting key, old/new value JSON, reason). This is the
same `AdminAuditLog` entity used by the rest of the admin surface — no new
audit mechanism was introduced.

## Frontend

- `features/admin/admin-feature-gates/` — category/search/risk/status
  filters, one `sp-admin-card` per group, `sp-admin-drawer` for
  view/edit/reset. Uses only existing design-system components (drawer,
  card, badge, alert, form-field, input, number-input, select, toggle,
  checkbox, code-pill).
- Route: `/admin/settings/feature-gates`, nav item under **System**.
- Deep link: `?gate=<groupKey>` opens that group's drawer on load.
- Admin Lessons and Admin AI Operations pages now show a **Configure** /
  **Open settings** link next to the previously-static "enable in config"
  text, pointing at the relevant `?gate=` deep link.

## Explicitly deferred (see `TODOS.md`)

- Wiring `RuntimeSettingOverride` into `ReadinessPoolReplenishmentService`'s
  live read path.
- Making `ReadinessPoolReplenishmentOptions`' own buffer/threshold fields
  (`TodayLessonPoolTargetCount`, `MinimumReadyThreshold`, `MaxBufferCount`,
  etc.) editable — not in the required editable list for this phase.
- Any `Infrastructure / Provider Config` or `Student Experience` gate
  categories — no concrete candidates existed in code, so none were added.
