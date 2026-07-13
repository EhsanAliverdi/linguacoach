# Phase J5d: Speaking content-type import (text-only reference prompt) — closes Phase J5

**Date:** 2026-07-13
**Related:** Phase J5 (J5a `docs/reviews/2026-07-13-phase-j5a-writing-content-import-review.md`,
J5b `docs/reviews/2026-07-13-phase-j5b-mixed-content-import-review.md`,
J5c `docs/reviews/2026-07-13-phase-j5c-listening-audio-import-review.md`)
**Type:** Implementation review

## Trigger

Fourth and final J5 pass. Before starting, one real scope decision remained open (parallel to
J5c's audio-vs-transcript question): does a "Speaking" import resource need reference audio (e.g.
a native-speaker model answer), or is it text-only?

**User decision (AskUserQuestion):** text-only reference prompt — a role-play/task prompt the
student reads and then speaks a response to, same shape as Writing (title + prompt + optional
duration target), no audio. This matches how `SpeakingRolePlay` activities already work: the
prompt itself is text; only the *student's own spoken answer* is audio, scored separately via
`SpeakingTurn` — entirely unrelated to import. This made J5d the smallest pass since J5a/J5b, and
notably smaller than J5c.

## Scope

Mirrors J5a's Writing pass almost exactly: new `ResourceCandidateType`/`PublishedResourceType`
values, a new content record, field recognition, publish/preview/unified-bank wiring, frontend
type list + preview case. No schema migration needed (text-only, reuses existing `ResourceCandidate`
columns — no new `AudioStorageKey`-style fields required).

## Files reviewed

Same touch points as J5a/J5c: `ResourceCandidateType.cs`, `PublishedResourceType.cs`,
`ResourceBankItemContent.cs`, `UnifiedResourceBankContracts.cs`,
`ResourceCandidatePreviewContracts.cs`, `ResourceImportService.cs`,
`ResourceCandidatePublishService.cs`, `ResourceCandidatePreviewService.cs`,
`ResourceBankQueryService.cs`, `AdminContentImportController.cs`, the frontend models/service/
preview-drawer files (both `admin-content-import` and `admin-import-run-candidates`), and
`admin-resource-bank-unified.component.ts`.

## Changes made

### Backend
- New `ResourceCandidateType.SpeakingPrompt`, `PublishedResourceType.Speaking`.
- New `SpeakingPromptContent(Title, PromptText, SuggestedDurationSeconds)` — deliberately no
  reference-audio field, matching the user's scope decision. `SuggestedDurationSeconds` is the one
  field genuinely new versus `WritingPromptContent` (which has `Genre`/`SuggestedMinWords` instead
  — word count doesn't apply to a spoken response).
- `ResourceImportService`: recognizes a `scenario` field (the row's task/role-play description) for
  both forced-type extraction and per-row inference — inserted after Listening's `transcript`
  check, no field-name overlap with any other type.
- `ResourceCandidatePublishService`: new `BuildSpeakingPromptEntity`, same shape as
  `BuildWritingPromptEntity` (requires `CefrLevel`, no other new gate — unlike J5c, there's no
  audio-required check since there's no audio at all in this type).
- `ResourceCandidatePreviewService`/`ResourceBankQueryService`: same per-type case pattern as every
  prior J5 phase.
- `AdminContentImportController`: `"speaking"` added to the resource-type map. This was the last
  entry in `CONTENT_IMPORT_COMING_SOON_TYPES` — **the list is now empty**, closing the entire J5
  "import content-type expansion" roadmap item.

### Frontend
- "Speaking" added to the Content Import dropdown and the unified Resource Bank type/skill
  filters.
- The "Coming soon: ..." hint under the Content type field is now conditionally hidden
  (`@if (comingSoonTypes.length > 0)`) rather than rendering an empty, broken-looking line — a
  small but real UI correctness fix forced by this phase finally emptying that list.
- Preview drawer gained a `SpeakingPrompt` case (both duplicated files) showing Title, Scenario
  (reusing the existing `promptText` DTO slot), and Suggested response duration.

## Decisions made

1. **Text-only, no reference audio** — the user's explicit pre-implementation decision, made the
   same way as J5c's audio-format question, keeping this pass small and matching how the
   downstream `SpeakingRolePlay` runtime already separates prompt (text) from response (audio).
2. **Reused `PromptText`/`Title` preview DTO slots rather than adding parallel `Scenario`
   fields** — Speaking and Writing are structurally the same shape (a task prompt), so the existing
   generic slots cover both without DTO bloat; only `SuggestedDurationSeconds` was genuinely new.
3. **Not wired into Lesson/Exercise/Module generation** — same precedent as Writing/Listening
   (J5a/J5c); the Resource Bank page's Generate actions stay hidden for Speaking rows.
4. No further AskUserQuestion needed once the text-only decision was made — implementation followed
   the established J5a/c pattern with no new ambiguity.

## Implementation tasks produced

None. **Phase J5 is now closed in full** (J0-J4 closed 2026-07-11; J5a-d closed 2026-07-13).

## Risks or unresolved questions

- None specific to J5d. The recurring cross-phase observations already documented in J5a/c
  (AI-subskill-classification mismatches; Resource Bank content not yet wired into generation for
  any of the 3 new types) apply identically here — not repeated in depth in this review.
- **Broader, deliberately out-of-scope for all of J5**: none of Writing/Listening/Speaking/Mixed
  resources are consumable by Lesson/Exercise/Module generation yet. J5's stated scope was import
  only; wiring generation to the new types is real future work, not started.

## Verification

- `dotnet build` — clean, 0 errors. No EF migration needed.
- `npx tsc --noEmit` / `npm run build -- --configuration production` — clean; only the
  pre-existing unrelated bundle-budget error remains.
- `dotnet test` (full suite) — **3,496 passed, 0 failed**: 5 architecture, 2,171 unit (+5 new:
  import-inference, publish success/title-derivation), 1,320 integration (+1 new
  `Speaking_resource_type_stages_a_text_only_reference_prompt`, plus a fix to
  `Unsupported_resource_type_returns_400`, which had used `"speaking"` as its "still unsupported"
  placeholder — now uses a genuinely made-up type since nothing real remains unsupported).
- Live browser smoke test (gstack `browse`, admin session, API container rebuilt and confirmed
  healthy): "Speaking" appears in the Content Import dropdown with the "Coming soon" hint
  correctly gone entirely; staged a real CSV row (title + scenario + CEFR + duration) as
  `SpeakingPrompt`; preview drawer correctly rendered Title/Scenario/Suggested response duration;
  "Speaking" confirmed present in both Resource Bank filters; zero console errors throughout.

## Final verdict

J5d (Speaking, text-only) is implemented and verified — the smallest and cleanest of the four J5
passes, as predicted. **This closes Phase J5 in full.** All four target content types (Listening,
Speaking, Writing, Mixed) named in the original 2026-07-10 architecture audit are now importable,
staged, previewable, and publishable through the same unified pipeline as the original Vocabulary/
Grammar/Reading types.

## Next recommended action

Commit this change locally (no push/deploy). With J0-J5 all closed, the next natural roadmap item
is wiring the three new J5 content types (Writing/Listening/Speaking) into Lesson/Exercise/Module
generation — currently hidden entirely in the Resource Bank UI — but that is new, unscoped work
requiring its own audit/plan, not a continuation of J5.
