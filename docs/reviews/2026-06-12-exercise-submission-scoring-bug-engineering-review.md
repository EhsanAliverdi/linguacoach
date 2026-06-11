# Exercise Submission Scoring Bug — Engineering Review

Date: 2026-06-12
Related sprint: `docs/sprints/current-sprint.md` (Critical bug fix, ahead of Adaptive Learning Foundation item 2)

## Trigger

Production report from product owner: a `gap_fill_workplace_phrase` submission returned
`score: 0`, every `itemResults[].studentAnswer: null`, despite the activity having real
gaps with correct `acceptedAnswers`. Follow-up: "its not just gap fill it is every
excersize."

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs`
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/matching-pairs/matching-pairs.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/matching-pairs/matching-pairs.component.html`
- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs`
- `src/LinguaCoach.Infrastructure/Activity/ListeningComprehensionEvaluator.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`

## Findings, by priority

### Priority 1 — gap_fill_workplace_phrase (FIXED)

`exercise-renderer.component.ts` `mapGapItems` generated fallback item ids as
`String(index + 1)` (`"1"`, `"2"`, ...) whenever the AI-generated content lacked an `id`
field — which `GapFillItemDto` never has. `ExactMatchEvaluator.ParseExpectedItems`
generates expected keys as `$"gap_{i + 1}"` (`"gap_1"`, `"gap_2"`, ...). The frontend
submitted `{ answers: { "1": "...", "2": "..." } }`; the backend looked up `"gap_1"`,
found nothing, and scored every item 0 with `studentAnswer: null`.

**Fix**: `mapGapItems` fallback id changed to `` `gap_${index + 1}` ``.

### Priority 1 — phrase_match (FIXED)

`matchingPairsContent` assigned a single `id = String(index + 1)` per pair, used
identically for both the phrase tile and its (shuffled) meaning tile. `selections` ended
up as `{ "<phraseIndex>": "<meaningIndex>" }` using the same 1-indexed, unprefixed id
space on both sides.

`KeyedSelectionEvaluator.ParseExpectedPairs` expects `Pairs: { "phrase_0": "meaning_0",
... }` — 0-indexed, with distinct `phrase_`/`meaning_` prefixes, and the submitted
*value* must equal the matched pair's `meaning_{i}` key string.

This was not a simple key-rename: the two sides of the UI need two different id
namespaces. `MatchingPair` previously had one shared `id`.

**Fix**:
- `MatchingPair` gained a `meaningId` field alongside `id`.
- `matchingPairsContent` now assigns `id: phrase_${index}` and `meaningId:
  meaning_${index}` (0-indexed) per pair.
- `MatchingPairsComponent.isMatchedMeaning` and `selectMeaning` now operate on
  `meaningId`.
- `matching-pairs.component.html` meaning column now binds to `pair.meaningId` for
  click/disabled/testid/track/style, instead of `pair.id`.
- Result: `selections` is now `{ "phrase_0": "meaning_2", ... }`, matching
  `PhraseMatchSubmittedAnswer.Pairs` exactly.

### Priority 2 — listen_and_gap_fill (NOT CHANGED — verified lower risk)

`ListenAndGapFillItemDto.Id` exists in the DTO contract (unlike `GapFillItemDto` and
`PhraseMatchPairDto`), so `mapAudioGaps`'s `obj['id']` should normally be populated by
the AI generator. The evaluator's fallback chain (`gap.Id ?? gap.SentenceWithBlank ??
Guid.NewGuid()`) combined with the frontend's `String(index + 1)` fallback remains a
latent mismatch only if the AI ever omits `id` — not confirmed as occurring in practice.
No code change made; flagged as a watch item if this pattern is reported broken too.

### Priority 3 — listen_and_answer per-question id (NOT CHANGED — separate, lower severity)

`ListeningComprehensionEvaluator.cs:27` uses `QuestionId = q.Id ?? string.Empty` against
`ListenAndAnswerQuestionDto.Id` (exists in contract). This pattern is AI-judged overall
(`MarkingMode.AiOpenEnded`/structured), so a `QuestionId` mismatch affects only
per-question feedback labeling, not the overall score. Not addressed in this pass.

### AI-evaluated patterns — unaffected

`email_reply`, `teams_chat_simulation`, `spoken_response_from_prompt` use
`AiStructuredEvaluator`/`AiOpenEndedEvaluator`, which forward `SubmittedAnswerJson` raw to
the AI prompt. Item-key naming does not matter for these.

## Decisions made

- Fix only the two confirmed score-zeroing classes (gap_fill_workplace_phrase,
  phrase_match) in this pass.
- Leave listen_and_gap_fill and listen_and_answer as documented watch items rather than
  speculative fixes, per CLAUDE.md "do not guess" guidance — no evidence either is
  currently broken.
- The broader "lesson structure" complaint (What we learn / Practice / Feedback / Redo →
  next, for both Lessons and Practice) is a separate product/architecture item, not
  addressed in this review. To be raised with the product owner as a follow-up planning
  item.

## Implementation tasks produced

1. `exercise-renderer.component.ts` `mapGapItems`: fallback id `gap_${index + 1}`. Done.
2. `exercise-renderer.component.ts` `matchingPairsContent`: two-id-scheme
   (`phrase_${index}` / `meaning_${index}`). Done.
3. `MatchingPair` interface + `MatchingPairsComponent` + template updated for
   `meaningId`. Done.

## Risks / unresolved questions

- listen_and_gap_fill and listen_and_answer id-mismatch risk remains unconfirmed; revisit
  if reported.
- Lesson-structure redesign not scoped — needs a dedicated planning pass with the product
  owner.

## Verification

```
dotnet test tests/LinguaCoach.UnitTests:        480 passed
dotnet test tests/LinguaCoach.IntegrationTests: 430 passed
npm run build:                                  passed (pre-existing budget/SCSS warnings only)
```

## Final verdict

Both confirmed score-zeroing bug classes (gap_fill_workplace_phrase, phrase_match) fixed.
All existing tests pass. listen_and_gap_fill and listen_and_answer flagged as
lower-priority watch items, not fixed in this pass.

## Next recommended action

Surface the "lesson structure" (What we learn / Practice / Feedback / Redo → next)
complaint to the product owner as a separate planning item, then resume Adaptive Learning
Foundation item 2 (numeric `StudentSkillProfile` scores).
