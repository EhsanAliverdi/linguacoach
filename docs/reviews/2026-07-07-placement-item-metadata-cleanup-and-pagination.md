# Placement item metadata cleanup + admin list pagination

**Date**: 2026-07-07
**Related**: `docs/reviews/2026-07-07-placement-formio-migration-engineering-review.md` (prior Form.io migration this builds on), `docs/architecture/formio-onboarding-placement-model.md`

## Context

Following the Form.io-native placement migration, the admin placement-item editor still asked
admins to manually type an "Item Type" and "Prompt" for every item, even though the actual
question shown to students is entirely defined by the Form.io schema. These two fields had become
pure duplication — authored once from the same data used to build the schema, then never kept in
sync with later schema edits — and the admin edit form was a slide-over drawer rather than a
routed page, cramping the Form.io builder.

Separately, the admin list page didn't match the styling of the exercise-types admin page (no
`sp-admin-filter-bar`/`sp-admin-table-actions`/pagination), and had no pagination at all.

## Decisions made (via AskUserQuestion)

1. **ItemType/Prompt**: removed entirely from `PlacementItemDefinition` (DB migration), not just
   hidden in the UI. Seeder/admin-duplicate-check identity moved off `Prompt` text.
   - Follow-up: the initial hash-based seeder-identity design (hash of skill+level+schema) was
     found to have a correctness gap — since the seeder runs on every app startup, an admin's live
     schema edit would change the hash and cause the seeder to silently re-insert the pristine
     original as a duplicate. Fixed by switching seeder idempotency to "skip if this (skill, CEFR
     level) pair already has any row", which is schema-content-independent and can't be
     invalidated by an admin edit.
2. **Admin item edit UI**: converted from an `sp-admin-slide-over` panel to a dedicated routed
   page (`/admin/placement-items/new` and `/admin/placement-items/{itemId}`), mirroring the
   existing onboarding-template-designer pattern.
3. **Pagination**: user explicitly chose **true server-side pagination** (API accepts
   `page`/`pageSize`/`skill`, returns paged + global-stat counts) over the simpler client-side
   pattern used by exercise-types, despite the item bank currently being ~100 rows.

## Files changed

Backend:
- `LinguaCoach.Domain/Entities/PlacementItemDefinition.cs` — removed `ItemType`/`Prompt`.
- `LinguaCoach.Persistence/Configurations/PlacementItemDefinitionConfiguration.cs` — dropped
  columns/unique index.
- `LinguaCoach.Persistence/Migrations/20260706225124_DropPlacementItemPromptItemType.cs` — EF
  migration.
- `LinguaCoach.Persistence/Seed/PlacementItemBankSeeder.cs` — pair-existence idempotency check.
- `LinguaCoach.Application/Placement/PlacementItemBankContracts.cs` — `AdminPlacementItemDto`
  gained `QuestionPreview`; `ListAdminPlacementItemsQuery` gained `Page`/`PageSize`/`Skill`;
  new `AdminPlacementItemListResult`, `GetAdminPlacementItemQuery`/`IAdminPlacementItemGetQuery`.
- `LinguaCoach.Infrastructure/Placement/PlacementItemSchemaLabel.cs` (new) — derives a display
  label/component-type from a Form.io schema; also the identity-hash helper used by the admin
  add/update duplicate-check.
- `LinguaCoach.Infrastructure/Placement/AdminPlacementItemListQueryHandler.cs` — added
  Skip/Take + skill filter + global aggregate counts.
- `LinguaCoach.Infrastructure/Placement/AdminPlacementItemGetQueryHandler.cs` (new).
- `LinguaCoach.Infrastructure/Placement/AdminAddPlacementItemHandler.cs` /
  `AdminUpdatePlacementItemHandler.cs` — duplicate-check by schema-hash instead of `Prompt`.
- `LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs` — derives the issued
  `PlacementAssessmentItem`'s `ItemType`/`Prompt` snapshot from the schema-label helper at
  issuance time instead of copying from the (now-removed) definition fields; `IsUsed` dedup
  simplified to `SourceItemDefinitionId` only.
- `LinguaCoach.Api/Controllers/AdminPlacementItemController.cs` — paged `List`, new `Get`.
- `LinguaCoach.Infrastructure/DependencyInjection.cs` — registered the new get-query handler.

Frontend:
- `core/models/admin-placement-item.models.ts` — dropped `itemType`/`prompt`, added
  `questionPreview`, added `AdminPlacementItemListResult`.
- `core/services/admin-placement-item.service.ts` — `list()` takes page/pageSize/skill; added
  `get(itemId)`.
- `features/admin/admin-placement-item-editor/` (new) — routed add/edit page, replaces the
  slide-over in the list component.
- `features/admin/admin-placement-items/` — restyled to the exercise-types pattern
  (`sp-admin-filter-bar`, `sp-admin-table` `first-column-fluid` raw-table mode,
  `sp-admin-table-actions`, `sp-admin-table-footer` + `sp-admin-pagination`); now list-only,
  server-driven paging.
- `app.routes.ts` — added `placement-items/:itemId` route.

Tests: updated/added across `PlacementItemBankSeederTests`, `AdminPlacementItemEndpointTests`,
`PlacementAssessmentService`-adjacent tests, and new specs for
`AdminPlacementItemEditorComponent`/updated `AdminPlacementItemsComponent` specs.

## Verification

- `dotnet test` — full solution: 3132 tests passed (5 architecture + 1769 unit + 1358
  integration), 0 failed.
- Frontend: `npx tsc --noEmit` clean; Karma suite for the two placement admin components: 30 + 28
  tests passed.
- Rebuilt and restarted the `linguacoach-api-1` Docker container against the new image; confirmed
  healthy and serving the updated `/api/admin/placement-items` contract.

## Risks / unresolved

- Server-side pagination was chosen ahead of actual scale need (~100 rows); acceptable per
  explicit user request, but adds more moving parts (Skip/Take, filter-aware total vs. global
  totals) than the client-side pattern every other admin list page uses. Future admin list pages
  should default to the client-side pattern unless there's a concrete scale reason.
- The `PlacementItemSchemaLabel` label/type derivation reads only the *first* schema component —
  fine for today's one-question-per-item authoring model; would need revisiting if multi-component
  items become common.

## Verdict

Complete. All backend and frontend tests green; API container rebuilt and verified healthy.

## Next recommended action

None outstanding for this change. If multi-component placement items are introduced later,
revisit `PlacementItemSchemaLabel`'s first-component-only assumption.
