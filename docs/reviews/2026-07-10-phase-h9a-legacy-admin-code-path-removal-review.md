# Phase H9A — Legacy Admin/API/Code Path Removal Safety Pass

**Date:** 2026-07-10
**Related sprint/feature:** H9 cleanup track (split H9A/H9B/H9C/H9D), following H7/H8/H10
**HEAD before work:** `6968322a` (feat: add activitydefinition runtime launch bridge, Phase H10)

## Goal

Begin the removal-oriented cleanup promised after H7/H8/H10, safely and incrementally: remove
old/invalid admin-facing and frontend/code surfaces now proven unnecessary, while leaving
backend/data/runtime removal for later, better-audited phases (H9B/H9C/H9D).

## Mandatory Step -1 result

- `git log --oneline --decorate -n 15` confirmed HEAD = `6968322a`, the exact commit H10 produced.
- `git status --short` was clean.
- Proceeded per the ticket's instructions.

## Step 0 — removal safety audit

| Candidate | Classification | Notes |
|---|---|---|
| 4 typed admin bank pages (`admin-resource-bank-{vocabulary,grammar,reading-references,reading-passages}`) | **Remove now in H9A** | Not linked from nav/dashboard since H8; unified Resource Bank (H1) is the replacement browsing entry point with full filter parity. |
| 4 typed route entries in `app.routes.ts` | **Remove now in H9A** | Replaced with `RedirectFunction` redirects to `/admin/resource-bank?type=<value>`. |
| `AdminResourceBankService` (frontend, 8 typed methods) | **Remove now in H9A** | Confirmed via grep: used only by the 4 deleted components. |
| 12 orphaned frontend model interfaces (`ResourceBankSourceInfoDto`, `ResourceBankTraceabilityDto`, and the 4 typed List/Detail/ListResult triples) | **Remove now in H9A** | Confirmed via grep: referenced only by the deleted service and models file itself. |
| `UnifiedResourceBankItemDto.DetailRoute` literal values (backend, 4 construction sites in `ResourceBankQueryService.cs`) | **Remove now in H9A** (small, non-destructive) | Previously hardcoded to the just-removed typed routes; would have become dead 404 links in the unified page's detail drawer. Nulled out instead. Not part of the "no backend deletion" default because this is a value fix, not a deletion of behavior/API surface. |
| Unified Resource Bank detail-drawer "Typed bank page" link block (template) | **Remove now in H9A** | Dead once `DetailRoute` is always `null`. |
| `AdminResourceBankController` typed action methods (8: `ListVocabulary`/`GetVocabularyDetail`/etc.) | **Keep route only for compatibility** | Deliberately conservative — avoids touching `AdminResourceBankEndpointTests.cs`; dead from the frontend's perspective post-H9A but still a valid HTTP surface if anything external depends on it. |
| `IResourceBankQueryService` 8 typed methods | **Keep long-term — valid runtime infrastructure** | `TodayBankResourceSelector.cs` calls these directly for student-facing "Today" bank-resource selection — unrelated to the admin UI, must never be removed without a migration. |
| Typed bank DB tables (`CefrVocabularyEntry`, `CefrGrammarProfileEntry`, `CefrReadingReference`, `CefrReadingPassage`) | **Keep long-term** | Forbidden in H9A; subject of H9B/H9C/H9D. |
| `ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate` admin flows | **Keep long-term** | Active import/publish pipeline, untouched. |
| `ActivityTemplate`, `PracticeActivityCache`, `StudentActivityReadinessItem`, `PracticeGymSuggestionService`, `PracticeGymPoolService` | **Keep long-term** | Live runtime infrastructure, untouched. |
| `LearningActivity`/`LearningSession`/`SessionExercise`/`LearningModule` | **Keep long-term** | Core learning model, untouched. |
| Today fallback path, Practice Gym fallback path, H10 ActivityDefinition launch bridge | **Keep long-term** | Untouched; no code in these paths was modified. |
| Admin nav/sidebar/dashboard links | **Already resolved in H8** | H8 already removed nav entries; re-verified still absent (Karma regression test at `admin-app-layout.component.spec.ts` still passes unchanged). |

No candidate was classified "Unknown" — nothing ambiguous was removed.

## Items removed

- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-vocabulary/` (component + template)
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-grammar/` (component + template)
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-reading-references/` (component + template)
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-reading-passages/` (component + template)
- The 4 corresponding route entries in `app.routes.ts` (replaced with redirects, see below)
- `AdminResourceBankService` class in `admin-resource-import.service.ts` (8 typed list/detail methods)
- 12 orphaned model interfaces in `admin-resource-import.models.ts`: `ResourceBankSourceInfoDto`, `ResourceBankTraceabilityDto`, `ResourceBankVocabularyListItemDto`/`Detail`/`ListResult`, `ResourceBankGrammarListItemDto`/`Detail`/`ListResult`, `ResourceBankReadingReferenceListItemDto`/`Detail`/`ListResult`, `ResourceBankReadingPassageListItemDto`/`Detail`/`ListResult`
- Now-unused imports in `admin-resource-import.service.ts` pointing at the deleted model interfaces
- The dead "Typed bank page" link block in `admin-resource-bank-unified.component.html`

## Items intentionally retained and why

- `AdminResourceBankController`'s 8 typed HTTP actions — kept as a compatibility-only surface; not proven safe to delete without touching `AdminResourceBankEndpointTests.cs`, out of H9A's conservative scope.
- `IResourceBankQueryService`'s 8 typed methods — load-bearing for `TodayBankResourceSelector` (student-facing Today feature), forbidden to touch in H9A.
- All typed bank DB tables, import pipeline, `ActivityTemplate`, `PracticeActivityCache`, readiness queue, `LearningActivity`/`LearningSession`/`SessionExercise`/`LearningModule`, Today/Practice Gym fallbacks, ActivityDefinition launch bridge — all explicitly forbidden by the ticket, none touched.

## Routes redirected

Old bookmarks now redirect via Angular's `RedirectFunction` (confirmed supported in this project's
Angular 19.2.0 via `node_modules/@angular/router/router_module.d-Bx9ArA6K.d.ts`):

```text
/admin/resource-banks/vocabulary          → /admin/resource-bank?type=vocabulary
/admin/resource-banks/grammar             → /admin/resource-bank?type=grammar
/admin/resource-banks/reading-references  → /admin/resource-bank?type=readingReference
/admin/resource-banks/reading-passages    → /admin/resource-bank?type=readingPassage
```

The `type` query param values match `UnifiedResourceBankItemType` exactly (camelCase). The unified
Resource Bank page (`AdminResourceBankUnifiedComponent`) now injects `ActivatedRoute` and reads
`?type=` on `ngOnInit`, pre-seeding its `typeFilter` signal (validated against the known type
list — an unrecognized value falls back to "all", never a crash or blank page) before calling
`loadAll()`.

## Backend changes

Very small, non-destructive: the 4 `UnifiedResourceBankItemDto` construction sites in
`ResourceBankQueryService.cs` (`ListVocabularyAsync`, `ListGrammarAsync`,
`ListReadingReferencesAsync`, `ListReadingPassagesAsync`) had their `DetailRoute` positional
argument changed from a hardcoded string literal (pointing at the now-removed typed routes) to
`null`. No method signatures, no controller actions, no service interfaces changed.

## Frontend build result

`npm run build -- --configuration production` succeeded. No new TypeScript/Angular compile
errors. The build fails only on the known pre-existing bundle-size budget error (initial bundle
2.56 MB vs 1.00 MB error threshold / 500 kB warning threshold) — unrelated to this phase, present
before H9A started.

## Backend validation result

- `dotnet build --configuration Release` — succeeded, 0 errors (20 pre-existing warnings, none new).
- `dotnet test --configuration Release` — all 3,925 tests passed (5 ArchitectureTests, 2,352
  UnitTests, 1,568 IntegrationTests). No regressions.

## Karma status

Not run. Karma's shared spec bundle remains blocked by `TODO-H8-2` (5 pre-existing spec files with
stale required-field fixtures unrelated to H9A, discovered in Phase H8, still open). A new spec
file (`admin-resource-bank-unified.component.spec.ts`, 3 tests covering `?type=` query-param
pre-seeding, an unrecognized value falling back to "all", and the no-param default) was added
following the existing `ActivatedRoute` + `convertToParamMap` mocking pattern used elsewhere in
the codebase (e.g. `admin-feature-gates.component.spec.ts`), but the whole-bundle compile failure
means it cannot currently be exercised. Not expanded to fix the 5 unrelated fixture files — outside
H9A's scope per the ticket's own instruction not to expand scope for unrelated Karma failures.

## Docs updated

- `TODOS.md` — `TODO-H9-1` marked partially resolved (H9A done); added `TODO-H9B-1`,
  `TODO-H9C-1`, `TODO-H9D-1`.
- `docs/roadmap/road-map.md`
- `docs/sprints/current-sprint.md`
- `docs/handoffs/current-product-state.md`
- `docs/backlog/product-backlog.md`
- `docs/architecture/product-model-realignment-h0.md`
- `docs/architecture/learning-activity-engine.md`
- `docs/architecture/english-resource-bank-import-platform.md`
- `docs/architecture/README.md`
- This review doc.

## Known limitations / retained legacy surfaces

- `AdminResourceBankController`'s 8 typed HTTP actions still exist as a compatibility-only
  surface with no frontend caller.
- `IResourceBankQueryService`'s 8 typed methods remain, serving `TodayBankResourceSelector`.
- Typed Cefr* bank tables and physical `ResourceBankItem` consolidation are entirely deferred to
  H9B/H9C/H9D.
- Karma unit tests remain blocked by pre-existing `TODO-H8-2`.

## Final verdict

H9A complete. Only safe, proven-unreachable frontend/admin surface was removed. No typed bank
tables, data, backend service methods, or runtime dependencies were touched. No destructive EF
migration was added — none was needed.

## Explicit confirmations

- No typed bank tables removed.
- No typed bank data removed.
- No destructive EF migration added.
- No physical ResourceBankItem consolidation performed.
- No `ActivityTemplate` removal.
- No `PracticeActivityCache` removal.
- No `StudentActivityReadinessItem`/readiness queue removal.
- No `LearningActivity`/`LearningSession`/`SessionExercise` removal.
- No Today fallback removal.
- No Practice Gym fallback removal.
- No ActivityDefinition launch bridge removal.
- No PG-v2 started.
- No Practice Gym redesign.
- No learner mastery updates from Modules.
- No external datasets introduced.
- No Persian/bilingual content introduced.
- No direct final-table seeding.
- Committed locally only; not pushed; not deployed; branch unchanged (`main`).

## Next recommended action

Kick off H9B: decide whether physical `ResourceBankItem` consolidation (H0 §4 Option A) is worth
pursuing, or whether the current read-model approach (Option B) should be the permanent design.
See `TODO-H9B-1`.
