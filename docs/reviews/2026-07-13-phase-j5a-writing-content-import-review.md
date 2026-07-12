# Phase J5a: Writing content-type import expansion

**Date:** 2026-07-13
**Related:** Phase J5 (roadmap 2026-07-10, `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md` §D.6)
**Type:** Implementation review

## Trigger

The user asked to move to the next J-track phase after closing out the J4B follow-up corrections.
J5 was defined by the original architecture audit as "Import content-type expansion (Listening/
Speaking/Writing/Mixed)" — the lowest-priority, largest-scope item on the J list, deliberately left
open after J4 closed J0-J4. Before starting, an audit confirmed the four target types differ
sharply in difficulty:

- **Writing** — easiest. Reuses existing `free_text_entry`/`ai_open_ended` exercise-type
  evaluators and `WritingScenario` activity type; no audio.
- **Mixed** — mostly already works via existing per-row type inference (`DefaultCandidateType =
  null` already triggers field-based inference).
- **Listening** — medium-to-large depending on whether import means a transcript (cheap, reuses
  the existing TTS-at-runtime pattern) or real audio-file upload (needs a brand-new storage/
  upload pipeline).
- **Speaking** — hardest; `SpeakingTurn` scores a *student's* spoken answer, but there's no
  import path for reference speaking-prompt audio.

User decision (AskUserQuestion): sequence J5 as small passes, easiest first — **J5a Writing → J5b
Mixed → J5c Listening → J5d Speaking** — matching the project's established small-pass convention
(J2a/b/c, I4 Pass 1/2/3). For Listening (J5c, not started), the user chose **real audio file
upload** over transcript-only, which will make J5c a materially bigger phase than initially
scoped (new storage/upload pipeline, not just a new `ResourceCandidateType`/content shape).

This document covers **J5a (Writing) only** — J5b/c/d are not started.

## Scope decision: import only, not downstream generation

A key finding during audit: Lesson/Exercise/Module generation (both deterministic and AI-assisted,
J2a/b/c) already consumes the Resource Bank via `LessonResourceLookup`/`LessonResourceSnapshot` —
not a raw-prompt AI flow as initially misread from a narrow grep. `LessonResourceLookup.FindAsync`
already has a `default: return null` case for any `PublishedResourceType` it doesn't recognize, so
adding `Writing` there does not crash existing generation — it correctly returns "not found."

J5a's scope is deliberately **import + publish + Resource Bank browse only** — it does NOT wire
Writing resources into Lesson/Exercise/Module generation. That's a separate future phase. The
frontend's "Generate Learn/Activity/Module" row actions are explicitly **hidden** for Writing rows
in the Resource Bank unified page (see Decisions below) rather than left enabled to fail with a
confusing backend error.

## Files reviewed

- `src/LinguaCoach.Domain/Enums/ResourceCandidateType.cs`, `PublishedResourceType.cs`
- `src/LinguaCoach.Application/ResourceImport/ResourceBankItemContent.cs`,
  `UnifiedResourceBankContracts.cs`, `ResourceCandidatePreviewContracts.cs`
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceImportService.cs`,
  `ResourceCandidatePublishService.cs`, `ResourceCandidatePreviewService.cs`,
  `ResourceCandidateValidationService.cs` (confirmed generic, no changes needed),
  `ResourceCandidateFieldHelper.cs` (confirmed generic, no changes needed),
  `ResourceBankQueryService.cs`
- `src/LinguaCoach.Api/Controllers/AdminContentImportController.cs`
- `src/LinguaCoach.Infrastructure/Lessons/LessonResourceLookup.cs` (confirmed safe default-null
  behavior for an unrecognized `PublishedResourceType`, no change needed)
- `src/LinguaCoach.Web/src/app/core/models/admin-resource-import.models.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/admin-content-import.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-import-run-candidates/admin-import-run-candidates.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-unified/admin-resource-bank-unified.component.ts`

## Changes made

### Backend
- New `ResourceCandidateType.WritingPrompt` (staging-time type) and
  `PublishedResourceType.Writing` (published bank discriminator) enum values. Both stored as
  plain string/int columns already (`HasConversion<string>` for candidate type,
  default int for published type) — no EF migration required.
- New `WritingPromptContent(Title, PromptText, Genre, SuggestedMinWords)` record. Deliberately
  **no rubric/answer-key field** — matches `ResourceCandidatePublishService`'s own existing
  precedent (its `ActivityTemplateCandidate` bullet) of never publishing something that implies a
  scoring capability this codebase doesn't actually have wired up yet.
- `ResourceImportService`: recognizes a `prompt` field for both forced-type extraction and
  per-row type inference (checked after Reading, before Form.io template fields — no field-name
  overlap).
- `ResourceCandidatePublishService`: new `BuildWritingPromptEntity` — same shape as
  `BuildVocabularyEntry`/`BuildGrammarProfileEntry` (requires `CefrLevel`, reads `prompt`/`title`/
  `genre`/`minWords` fields, produces entity type name `"CefrWritingPrompt"` for traceability).
- `ResourceCandidatePreviewService`: new `BuildWritingPreview` — renders title/prompt/genre/
  suggested word count in the "what the student would see" preview panel.
- `ResourceBankQueryService`: `ListUnifiedAsync`'s `ToUnifiedDto`/`ToDomainType`/
  `MatchesUnifiedType` all handle `Writing` — Writing resources now show up in the unified
  Resource Bank browse page like every other type. No dedicated typed `ListWritingAsync`/
  `GetWritingDetailAsync` pair was added (the typed per-type methods exist only to serve
  `TodayBankResourceSelector`'s real-time runtime consumption, which doesn't reference Writing
  yet — see Scope decision above).
- `AdminContentImportController`: `"writing"` added to the resource-type string map.

### Frontend
- `ContentImportResourceType`/`CONTENT_IMPORT_RESOURCE_TYPES` gained `'writing'`;
  `CONTENT_IMPORT_COMING_SOON_TYPES` now reads `Listening, Speaking, Mixed / AI detect` (Writing
  removed).
- `UnifiedResourceBankItemType`/`UNIFIED_RESOURCE_BANK_TYPES` gained `'writing'` — Writing rows
  now filterable/visible on `/admin/resource-bank`.
- `RESOURCE_PUBLISH_SUPPORTED_TYPES`/`RESOURCE_CANDIDATE_TYPES` updated to include
  `WritingPrompt`.
- Preview drawer template (duplicated between `admin-content-import` and the newer
  `admin-import-run-candidates` page from the J4B follow-up) gained a `@case ('WritingPrompt')`
  block rendering title/prompt/genre/suggested-min-words.
- `admin-resource-bank-unified.component.ts`: added `writing: 'Writing'` to the resource-type
  mapping table, and added a `TYPES_SUPPORTING_GENERATION` set that **hides** the Generate Learn/
  Activity/Module (and their AI variants) row actions for Writing rows — only `view` stays
  available. This is the concrete implementation of the "import only, not generation" scope
  decision above.

## Decisions made

1. **Hide, don't disable-with-tooltip, the unsupported generate actions for Writing.** The
   codebase's established pattern (`ExerciseLaunchEligibility`, the J4B "not launchable yet"
   badge) is to surface capability gaps honestly rather than let an admin click something that
   fails with a generic backend error. Hiding the actions entirely follows that same convention.
2. **No rubric/scoring field on `WritingPromptContent`.** Content-only, matching this codebase's
   conservative stance (seen in the `ActivityTemplateCandidate` publish-block precedent) against
   publishing anything that implies an evaluation capability that doesn't exist yet.
3. **No typed `ListWritingAsync`/`GetWritingDetailAsync` pair.** Those methods exist for
   `TodayBankResourceSelector`'s runtime student-facing consumption of the 4 pre-existing types;
   since Writing isn't wired into generation yet, adding unused typed accessors would be
   speculative surface area with no caller.
4. **No AskUserQuestion needed mid-implementation** — the J5a scope (import + publish + browse,
   no generation wiring) followed directly from the pre-implementation audit and the earlier
   sequencing/audio-format decisions; no further ambiguity arose during coding.

## Implementation tasks produced

None outstanding for J5a. Follow-up phases already planned: J5b (Mixed), J5c (Listening, real
audio-file upload — user-selected, bigger scope than transcript-only), J5d (Speaking).

## Risks or unresolved questions

- **AI subskill-classification quality is a pre-existing, cross-type limitation, surfaced during
  live testing.** Running "Analyze" on a live-imported Writing candidate had the AI classifier
  suggest subskill `writing.email`, which does not exactly match the registered taxonomy value
  `writing.email_message` (`CurriculumSubskillConstants.WritingEmailMessage`) — the deterministic
  `ValidateSkillSubskill` gate correctly caught this and blocked publish with a clear error. This
  is **not a J5a defect**: the same AI-guess/taxonomy-mismatch risk exists identically for every
  existing candidate type (Vocabulary/Grammar/Reading), since the AI analysis prompt isn't
  constrained to the exact taxonomy string list. It happened to surface during this session's live
  QA because Writing was the type being tested. Not fixed here (out of scope) — worth a future
  look at either tightening the AI analysis prompt to the exact taxonomy list, or adding fuzzy
  matching in `ValidateSkillSubskill`.
- Live QA did not reach a fully successful "Approve & Publish" click end-to-end (blocked by the
  above AI-classification mismatch, not by any J5a code path). The successful-publish path
  **is** fully covered by two new dedicated unit tests
  (`Publishing_writing_prompt_candidate_creates_exactly_one_row_with_mapped_fields`,
  `Writing_prompt_without_a_title_derives_one_from_the_prompt_text`) that construct a
  publish-ready candidate directly (bypassing AI analysis, matching every other type's test
  convention in this file) and assert a successful publish with exact field mapping.
- J5c (Listening, real audio upload) will need a proper scoping pass of its own before
  implementation — a new storage/upload pipeline is a materially different shape of work than
  J5a/J5b.

## Verification

- `dotnet build` — clean, 0 errors (pre-existing warnings only, unrelated).
- `npx tsc -p tsconfig.app.json --noEmit` — clean.
- `npm run build -- --configuration production` — new/changed chunks compiled clean; the one
  pre-existing bundle-budget `ERROR` (present on `main` before this work, unrelated) is unchanged.
- `dotnet test` (full suite) — **3,464 passed, 0 failed**: 5 architecture tests (no layer-boundary
  violations from the new enum values), 2,147 unit tests (162 pre-existing + 5 new J5a tests —
  2 import-inference tests, 2 publish tests, 1 unified-bank-aggregation test), 1,312 integration
  tests, all unmodified and passing.
- Live browser smoke test (gstack `browse`, admin session, API container rebuilt from the new
  code and confirmed healthy first):
  - `/admin/content/import` → New Import tab: "Writing" appears in the Content type dropdown;
    "Coming soon" hint correctly dropped Writing, now reads "Listening, Speaking, Mixed / AI
    detect".
  - Submitted a real CSV import (`title,prompt,cefrLevel,genre,minWords`) → 1 candidate staged as
    `WritingPrompt`, CEFR B1, title "Email reply" — table renders correctly.
  - Preview drawer's new `WritingPrompt` case correctly rendered Title/Prompt/Genre/Suggested
    minimum words.
  - Publish gate correctly blocked (see AI-subskill risk note above) with a clear, specific error
    message — proving the publish path plugs into the same deterministic validation gates as
    every other type.
  - `/admin/resource-bank`: "Writing" appears in both the type filter and skill filter dropdowns.
  - No console errors on any page throughout.

## Final verdict

J5a (Writing content-type import) is implemented and verified: import, staging, preview, publish
gating, and Resource Bank browse all correctly handle the new type, reusing every existing gate
exactly as Vocabulary/Grammar/Reading do. Downstream Lesson/Exercise/Module generation is
deliberately not wired to Writing yet (explicit scope boundary, UI actions hidden accordingly).
Ready to commit locally.

## Next recommended action

Commit this change locally (no push/deploy). Scope J5b (Mixed) next — expected to be the smallest
remaining J5 pass given `DefaultCandidateType = null` already triggers per-row type inference.
