# Form.io Quiz Tab + Builder Consolidation

**Date**: 2026-07-07
**Related sprint/feature**: Follow-up to the placement Form.io migration (`docs/reviews/2026-07-07-placement-formio-migration-engineering-review.md`) and the placement item metadata/pagination cleanup (`docs/reviews/2026-07-07-placement-item-metadata-cleanup-and-pagination.md`). Architecture doc: `docs/architecture/formio-onboarding-placement-model.md` ("Quiz tab authoring" section).

## Context

Both admin-authored flows (onboarding's `StudentFlowTemplate` and placement's
`PlacementItemDefinition`) required the admin to hand-type a separate "Scoring rules (JSON)"
textarea alongside the Form.io builder to record each question's correct answer — clunky,
error-prone, and disconnected from the question it scores. The product owner asked for this to be
replaced by a per-component "Quiz" tab inside the Form.io builder's own component-settings modal,
plus consolidation of the shared `FormioBuilderComponent` so both flows use one component with
individually-toggleable features.

Given the scope (Form.io internals, a security-sensitive schema/scoring separation this app
already enforces strictly, two backend flows, a new DB column + migration), this was planned in
Plan Mode before implementation — see the approved plan for full architecture rationale. Two
decisions were confirmed with the user before building:
1. **Server-side authoritative split** (not client-side): the admin submits ONE annotated schema;
   the backend is the sole authority splitting it into student-safe schema + backend-only scoring
   rules, never trusting a client-submitted "already clean" pair.
2. **Both flows in one phase**: onboarding's scoring parser was upgraded onto placement's richer
   `ComponentScoringRule` shape in the same change, rather than staggered.

## Files reviewed

Read before implementing: `FormIoSchemaValidationService.cs` (confirmed its `ForbiddenAnswerLeakKeys`
check only scans direct component properties + a `properties` bag, NOT arbitrary nested objects —
meaning a naive `component.quiz.rule.correctAnswer` shape would NOT have been caught by the
existing validator, informing the splitter's design), `PlacementScoringRules.cs`
(`ComponentScoringRule` shape, reused as-is), `StudentOnboardingFlowService.cs` (bespoke
`{correctAnswerKey}` scoring parser, upgraded), `PlacementScoringService.cs` (extracted into shared
`ComponentAnswerScorer`), `formio-builder.component.ts` (confirmed already-shared, no drift between
consumers), `@formio/js` v5.4.1 source (`WebformBuilder.js`, `Radio.form.js`) for the `editForm`
override mechanism and `builder: { premium: false }` palette-restriction pattern.

## Findings, grouped by priority

**High — security-critical, addressed by design**: embedding scoring data in the same schema the
builder edits (`component.quiz`) creates a leak risk if the client were trusted to strip it.
Resolved by making the server (`IFormIoQuizSchemaSplitter`) the sole authority for the split, with
an unconditional deletion-by-key of the `quiz` property (not a typed-DTO round-trip, which could
silently pass through unknown properties), plus a second independent gate: the existing validator
now also rejects a raw `quiz` key on any schema being validated as student-facing.

**Medium — data model reconciliation**: onboarding's `{correctAnswerKey}` scoring shape was a
strict subset of placement's `ComponentScoringRule`. Resolved by upgrading onboarding's parser to
read the same shape (with permanent backward-compatible reading of the old flat shape — no
backfill/re-migration of existing onboarding scoring data), and extracting the shared
`ComponentAnswerScorer` so the `single_choice`/`multiple_choice`/`text_exact`/`text_normalized`
comparison logic isn't duplicated.

**Low — doc/code drift found during research, fixed opportunistically**: the architecture doc and
`FormioBuilderComponent`'s own comment both claimed a "best-effort component-type restriction"
that was never actually implemented (`BUILDER_OPTIONS` had no palette-limiting key at all). Fixed
by adding a real, narrower, verified-safe restriction (`builder: { premium: false }`) and
correcting the doc language to describe it accurately rather than overclaiming.

## Decisions made

- **No backfill** of `AuthoringSchemaJson` for existing placement items or onboarding versions —
  they keep scoring via their existing `ScoringRulesJson` until an admin re-saves through the new
  Quiz tab UI. The editor shows a "predates the Quiz tab" banner in that state. Reconstructing quiz
  annotations from existing `(schema, scoring rules)` pairs would be a second "reverse splitter"
  code path for a one-time, bounded, self-service re-tick — not worth the extra surface.
- **Only the display-mode toggle** (single-page vs. multi-step wizard) moved into the shared
  builder as an opt-in input (`showDisplayModeToggle`). The Custom-vs-FormIo renderer-kind toggle
  stayed in `AdminOnboardingEditorComponent` — it's consumer-specific (swaps in a bespoke
  `OnboardingWizardComponent` with no placement equivalent), not a generic builder feature.
- **Palette restriction scoped to hiding the Premium group only** — a full per-component-key
  restriction across Basic/Advanced/Layout/Data matching the backend's exact allow-list would need
  per-key verification against Form.io's own default group membership, which wasn't done; the
  backend validator remains the actual, sufficient security boundary either way.

## Implementation

**Backend** (5 phases, each independently tested):
- `LinguaCoach.Application.FormIo.IFormIoQuizSchemaSplitter` / `FormIoQuizSplitResult` (new) +
  `LinguaCoach.Infrastructure.FormIo.FormIoQuizSchemaSplitter` (new) — the security-critical split,
  walking the same container structure (`components`/`columns`/`rows`) as the existing validator.
- `LinguaCoach.Application.FormIo.ComponentAnswerScorer` (new) — extracted shared per-component
  scoring logic, used by both `PlacementScoringService` and `StudentOnboardingFlowService`.
- `PlacementItemDefinition`/`StudentFlowTemplateVersion` — new nullable `AuthoringSchemaJson`
  column + `SetAuthoringSchema` setter each; EF migration `T_FormIoQuizAuthoringSchema`.
- `AddPlacementItemCommand`/`UpdatePlacementItemCommand`/`SaveOnboardingTemplateDraftCommand` — new
  optional `AuthoringSchemaJson` field; when present, the three save handlers
  (`AdminAddPlacementItemHandler`, `AdminUpdatePlacementItemHandler`,
  `AdminOnboardingTemplateService`) call the splitter instead of using the command's
  `FormIoSchemaJson`/`ScoringRulesJson` directly.
- `FormIoSchemaValidationService.ForbiddenAnswerLeakKeys` — added `"quiz"` as a defense-in-depth
  entry.
- `StudentOnboardingFlowService.ParseScoringRules` — upgraded to the unified
  `ScoringRulesDocument`/`ComponentScoringRule` shape, with permanent backward-compatible reading
  of the pre-upgrade flat shape.

**Frontend**:
- `shared/formio/quiz-scoring-rule.model.ts` (new) — `QuizScoringRule`/`QuizAnnotation` TS types,
  `finalizeQuizAnnotations` (derives `quiz.rule.kind` from component type before save) and
  `countScoredComponents` (for the editors' summary line).
- `shared/formio/quiz-edit-tab.ts` (new) — the Form.io `editForm` tab definitions for the six
  target component types, wired into `FormioBuilderComponent`'s shared `BUILDER_OPTIONS`.
- `FormioBuilderComponent` — gained `showDisplayModeToggle` input, `setDisplayMode()`,
  `displayModeChange` output (the moved-in wizard-toggle logic), and the `builder: { premium:
  false }` palette restriction.
- `AdminOnboardingEditorComponent`/`AdminPlacementItemEditorComponent` — removed the scoring
  textarea + its validation helpers; `saveDraft()`/`saveItem()` now send `authoringSchemaJson`;
  added the "X of Y questions scored" summary line and the legacy re-authoring banner.

## Testing

- `FormIoQuizSchemaSplitterTests` (new, unit) — the leak-invariant centerpiece:
  `Split_NeverLeavesQuizKeyAnywhereInOutputSchema_ForArbitrarilyNestedInput` walks the entire
  output JSON tree (not just known keys) for `quiz` at every legal container depth, plus malformed
  input theory tests confirming the splitter never throws and always still strips.
- `QuizAnnotationLeakRegressionTests` (new, integration) — real HTTP admin-save + student-read
  endpoints for both placement and onboarding, asserting the raw response body never contains
  `quiz`/`correctAnswer`/`correctAnswers`.
- `OnboardingQuickCheckScoringTests` (new, integration) — onboarding had zero prior test coverage
  of its CEFR quick-check scoring; added 4 tests covering both the legacy flat shape and the new
  unified shape, correct and incorrect answers, asserting the resulting preliminary CEFR level.
- Updated specs for `AdminPlacementItemEditorComponent`/`AdminOnboardingEditorComponent` (removed
  scoring-textarea tests, added `needsReauthoring`/`scoredSummary` coverage).
- Full suite run after every phase: backend `dotnet test` reached 1779 unit + 1365 integration + 5
  architecture tests, all passing. Frontend: 1576 relevant tests passing (120 pre-existing,
  unrelated `AdminStudentDetailComponent`/`AdminAiConfigComponent` failures confirmed identical
  before and after this work — not caused by it).

## Risks / unresolved

- The Quiz tab's per-component-type UI (radio/select/selectboxes/checkbox/textfield/textarea) was
  implemented against Form.io's documented `editForm`/`dataSrc: 'custom'` mechanisms but has not
  been manually smoke-tested in a live browser for all six types — Form.io builder UI behavior
  isn't meaningfully unit-testable end-to-end. Recommend a manual QA pass per component type before
  considering this fully done.
- Palette restriction is narrower than a full backend-allow-list mirror (see decisions above) —
  acceptable since the backend validator is the real boundary, but flagged for anyone expecting a
  1:1 restriction.

## Verdict

Complete for all 5 planned phases. Backend fully tested and green; frontend typechecks clean and
existing/new specs pass. Manual browser QA of the Quiz tab UI is the one recommended follow-up
before calling this fully shipped.

## Next recommended action

Manual QA pass in a live browser: open the placement item editor and onboarding template editor,
exercise the Quiz tab for each of the six component types, confirm the correct-answer picker
populates from the component's own authored values, and confirm a full save → re-open round-trip
preserves the quiz annotations.
