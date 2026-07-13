# Phase J5b: Mixed content-type import expansion

**Date:** 2026-07-13
**Related:** Phase J5 (roadmap 2026-07-10; J5a `docs/reviews/2026-07-13-phase-j5a-writing-content-import-review.md`)
**Type:** Implementation review

## Trigger

Second of the four small J5 passes the user sequenced (AskUserQuestion, 2026-07-13, recorded in
the J5a review): **J5a Writing (done) → J5b Mixed → J5c Listening → J5d Speaking**. J5b was
expected to be the smallest remaining pass, per the pre-J5a audit: `ResourceImportService`
already has full per-row field-name inference (`InferCandidateType`) that runs automatically
whenever no admin-forced type is set (`DefaultCandidateType == null`) — the file-upload import
flow (`AdminResourceImportController`) already exercises this path implicitly. Content Import's
newer H2 admin UX (`AdminContentImportController`), however, always forced a single type onto
every row since it launched — "Mixed" was listed as a "Coming soon" placeholder with no wiring at
all.

## Scope

Expose the existing per-row inference capability through the Content Import admin UX by adding a
"Mixed (auto-detect per row)" option — internally, this simply means "don't force a type; let
each row's own fields decide." No new candidate/published type, no new content shape, no new
validation/publish logic — those already handle heterogeneous candidate types per row (visible in
every existing "review candidates" table, which already renders each row's own `candidateType`
independently).

## Files reviewed

- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceImportService.cs` — confirmed
  `InferCandidateType` and the `AnyContentFields` gate already handle arbitrary row shapes
  (including J5a's `prompt` field) with zero changes needed.
- `src/LinguaCoach.Application/ResourceImport/ContentImportContracts.cs` — `ContentImportRequest`
- `src/LinguaCoach.Infrastructure/ResourceImport/ContentImportService.cs` — the H2 wrapper
- `src/LinguaCoach.Api/Controllers/AdminContentImportController.cs`
- `src/LinguaCoach.Web/src/app/core/models/admin-resource-import.models.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/admin-content-import.component.ts`/`.html`

## Changes made

### Backend
- `ContentImportRequest.ResourceType` is now `ResourceCandidateType?` (was non-nullable) — a
  `null` value means "Mixed," passed straight through as
  `ResourceImportRequest.DefaultCandidateType`, which the underlying pipeline already treats as
  "run per-row inference" (`ProcessRow`'s existing `request.DefaultCandidateType is { } forcedType
  ? ... : InferCandidateType(row)` ternary — untouched).
- `AdminContentImportController.SupportedResourceTypes` changed from
  `Dictionary<string, ResourceCandidateType>` to `Dictionary<string, ResourceCandidateType?>` with
  a new `["mixed"] = null` entry — `TryGetValue` still correctly distinguishes "key not found"
  (still rejected: Listening/Speaking) from "key found, value is null" (Mixed).

### Frontend
- `ContentImportResourceType`/`CONTENT_IMPORT_RESOURCE_TYPES` gained `'mixed'` →
  "Mixed (auto-detect per row)"; `CONTENT_IMPORT_COMING_SOON_TYPES` now reads just
  `Listening, Speaking`.
- New `resourceTypeHint` getter on `AdminContentImportComponent`: the Content type field's hint
  swaps from the default "Applied to every item in this import" (actively wrong for Mixed) to an
  explanation of the field-name → type mapping when Mixed is selected. Template changed from a
  static `hint="..."` attribute to `[hint]="resourceTypeHint"`.

## Decisions made

- No AskUserQuestion needed — J5b's scope followed directly from the pre-implementation audit
  (Mixed already "mostly works" via existing inference) and the user's own sequencing decision.
  No ambiguity arose during implementation.
- Deliberately did not attempt real AI-based content-type detection despite the frontend's old
  "Mixed / AI detect" label wording. The existing deterministic field-name inference already
  provides the practical value (auto-classify heterogeneous CSV/JSON rows); building an actual AI
  classification step is a materially different, riskier scope not requested by the roadmap's
  "content-type expansion" framing and not needed to make Mixed useful. The UI option label was
  changed to "Mixed (auto-detect per row)" to describe what it actually does rather than promise
  AI.

## Implementation tasks produced

None outstanding for J5b. J5c (Listening, real audio upload) and J5d (Speaking) remain.

## Risks or unresolved questions

- None specific to J5b. The one cross-type AI-subskill-classification observation from the J5a
  review (AI-guessed subskills not always matching the exact curriculum taxonomy string) applies
  equally here but isn't new.
- Pasted-line input mode ("one item per line") combined with Mixed will classify every line as
  `ReadingPassage` (a bare `text` field always infers to Reading) — Mixed's real value is for
  CSV/JSON imports with genuinely heterogeneous per-row columns, not line-based paste. This is
  existing, expected behavior of `InferCandidateType`, not a new gap.

## Verification

- `dotnet build` — clean, 0 errors.
- `npx tsc -p tsconfig.app.json --noEmit` — clean.
- `npm run build -- --configuration production` — `admin-content-import-component` chunk compiled
  clean; only the pre-existing unrelated bundle-budget error remains.
- `dotnet test` (full suite) — **3,466 passed, 0 failed**: 5 architecture, 2,148 unit (+2 new:
  one `ContentImportServiceTests` proving `ResourceType: null` classifies 3 rows into 3 different
  types through the exact H2 wrapper, plus the existing `ResourceImportServiceTests` coverage of
  `InferCandidateType` unchanged), 1,313 integration (+1 new
  `Mixed_resource_type_classifies_each_row_independently`, and the old
  `Unsupported_resource_type_returns_400` test repointed from `"mixed"` — now genuinely
  supported — to `"listening"`, still correctly rejected).
- Live browser smoke test (gstack `browse`, admin session, API container rebuilt and confirmed
  healthy first):
  - "Mixed (auto-detect per row)" appears in the Content type dropdown; selecting it correctly
    swaps the field hint to the per-row classification explanation.
  - Submitted a real JSON import with 4 heterogeneous rows (a vocabulary word, a grammar point, a
    reading passage, a writing prompt) with **no forced type** → all 4 staged as 4 different
    candidate types (`VocabularyEntry`, `GrammarProfileEntry`, `ReadingPassage`, `WritingPrompt`)
    in a single run, visible correctly in the candidates table.
  - No console errors.

## Final verdict

J5b (Mixed) is implemented and verified — the smallest of the four J5 passes as predicted, since
it activated an already-built capability (per-row inference) through a previously type-forcing
admin UX rather than building new classification logic. Ready to commit locally.

## Next recommended action

Commit this change locally (no push/deploy). J5c (Listening, real audio file upload — user's
explicit choice over transcript-only) needs its own scoping pass before implementation: it
requires a new storage/upload pipeline, materially different shape of work than J5a/J5b.
