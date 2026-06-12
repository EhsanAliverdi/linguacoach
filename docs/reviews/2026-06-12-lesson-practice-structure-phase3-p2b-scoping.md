# Phase 3 P2b — Remaining "What we learn" Framing: Scoping Pass

Date: 2026-06-12
Related sprint: `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md` (Phase 3)
Status: Scoping only — no code changes in this pass

## Context

Phase 3 P2 task 1 (`teachingNote` → `[goal]` for GapFill/MatchingPairs) is done. This
pass scopes the remaining P2 work: a richer "What we learn" breakdown (grammar / vocab
/ phrases), and the `targetGrammarPoint` field, for the pattern-driven generators
(`email_reply`, `teams_chat_simulation`, `gap_fill_workplace_phrase`, `phrase_match`,
`spoken_response_from_prompt`, `listen_and_answer`, `listen_and_gap_fill`).

## Files reviewed

- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` (all 7 generation prompts,
  lines ~435-650)
- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs` (all content DTOs)
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` (lines 380-540,
  `ActivityDto` mapping per activity type)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/email-reply/*`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/chat-reply/*`

## Findings

### email_reply and teams_chat_simulation: data already generated, partially wired

The `ActivityGenerateEmailReplyContent` and `ActivityGenerateTeamsChatContent` prompts
already ask the AI for `learningGoal`, `skillFocus` (email only), `targetPhrases`, and
`targetVocabulary`. `EmailReplyContent`/`TeamsChatSimulationContent` DTOs
(`PatternContentDtos.cs:102-138`) already have all these fields.

On the frontend:

- `ChatReplyContent` (chat-reply.component.ts:12-20) reads `learningGoal` and
  `targetPhrases` directly from raw content JSON, both rendered in the template
  (chat-reply.component.html:10-13, 47-53). **`targetVocabulary` is not in the
  interface and not rendered.**
- `EmailReplyContent` (email-reply.component.ts:5-13) reads `targetPhrases` only
  (rendered html:24-28). **`learningGoal`, `skillFocus`, and `targetVocabulary` are
  not in the interface and not rendered.**

So for these two patterns, the AI is already producing most of the "What we learn"
data — it's a frontend interface + template gap only, same shape as the P2 task 1 fix
(no backend or prompt changes needed for `learningGoal`/`targetPhrases`/
`targetVocabulary`).

### gap_fill_workplace_phrase / phrase_match: no vocab/phrases/grammar fields

These prompts (`ActivityGeneratePhraseMatchContent`, `ActivityGenerateGapFillContent`)
only have `teachingNote` (already surfaced in P2 task 1). Adding `targetVocabulary`/
`targetGrammarPoint` here is a genuine prompt change — the pairs/items themselves
already are the vocabulary, so a separate field is lower value here than for
email_reply/teams_chat.

### listen_and_answer / listen_and_gap_fill / spoken_response_from_prompt

None of these prompts have `learningGoal`, `targetPhrases`, `targetVocabulary`, or a
grammar field. `spoken_response_from_prompt` has `suggestedPhrases` (DTO:
`SpokenResponseContent.SuggestedPhrases`) which is conceptually close to
`targetPhrases` but not currently surfaced as a "What we learn" item either — check
before adding a new field.

### targetGrammarPoint: still doesn't exist anywhere

No prompt or DTO has a real grammar-focus output field. `LearningPathGenerateAdaptiveContent`
has `grammarFocus` in its module-fingerprint JSON, but that's path-generation metadata,
unrelated to per-activity content. Adding `targetGrammarPoint` to any pattern is new
prompt-engineering work — not a "surface existing field" task like the others found
so far.

## Recommended approach (incremental)

1. **email_reply / teams_chat_simulation — surface already-generated fields**
   (frontend-only, same pattern as P2 task 1):
   - `EmailReplyContent` (email-reply.component.ts): add `learningGoal?: string | null`,
     `skillFocus?: string | null`, `targetVocabulary?: string[]`; render in
     email-reply.component.html alongside existing `targetPhrases` block.
   - `ChatReplyContent` (chat-reply.component.ts): add `targetVocabulary?: string[]`;
     render alongside existing `targetPhrases` block.
   - Zero backend/prompt changes — these fields are already generated and discarded
     today, same situation as `teachingNote` was for gap_fill/phrase_match.

2. **(Deferred, separate task)** Decide whether `spoken_response_from_prompt`'s
   `suggestedPhrases` should be surfaced as a "What we learn" phrases list in the
   speaking renderer — small frontend addition if yes, but needs a product call on
   whether "suggested" framing fits "What we learn".

3. **(Deferred, larger, own pass)** `targetGrammarPoint` — new field across all
   7 pattern-driven prompts + DTOs + `ActivityDto` + frontend rendering. This is the
   only item that's genuinely new cross-cutting AI-prompt work. Should be scoped and
   implemented pattern-by-pattern, starting with email_reply/teams_chat/
   gap_fill_workplace_phrase (per the original P2 plan's prioritization).

4. **(Out of scope)** `gap_fill_workplace_phrase` / `phrase_match` `targetVocabulary` —
   the exercise items themselves already are the vocabulary; a separate field adds
   little. Not recommended.

## Sizing

- Task 1 (email_reply/teams_chat field surfacing): small, frontend-only, 4 files
  (2 `.ts` + 2 `.html`), same shape as P2 task 1.
- Task 2 (suggestedPhrases for spoken_response): small, but needs a product decision
  first — not blocked on anything else.
- Task 3 (`targetGrammarPoint`): large, cross-cutting, own scoping/implementation pass.
- Task 4: not recommended, no further sizing needed.

## Decisions made

- Task 1 is the next concrete P2 win — same low-risk shape as the already-shipped P2
  task 1 (gap_fill/matching_pairs `teachingNote`).
- Task 3 (`targetGrammarPoint`) remains deferred with no scheduled phase, consistent
  with the original P2 scoping verdict.
- Task 4 rejected — not worth the prompt/DTO churn.

## Implementation tasks produced

1. Surface `learningGoal`/`skillFocus`/`targetVocabulary` in `EmailReplyContent` and
   `targetVocabulary` in `ChatReplyContent`, rendered in their templates — frontend-only,
   4 files.
2. (Needs product decision) Surface `suggestedPhrases` for spoken_response as a
   "What we learn" phrases list.
3. (Own pass, deferred) `targetGrammarPoint` across all pattern-driven prompts/DTOs/
   frontend.

## Risks / unresolved questions

- Task 2 needs a product call on whether "suggested" phrasing fits the "What we learn"
  framing before implementing.
- Task 3 has no scheduled phase — flag again if product wants a date.

## Final verdict

One more small, safe, frontend-only win exists (task 1: email_reply/teams_chat
learningGoal/skillFocus/targetVocabulary), same shape as the already-shipped gap_fill/
matching_pairs `teachingNote` fix. Everything else either needs a product decision
(task 2) or is genuinely new cross-cutting prompt work with no schedule (task 3).

## Next recommended action

Implement task 1 now (4-file frontend change). Park tasks 2-3 — task 2 needs a product
decision, task 3 needs its own scoping/implementation pass when prioritized.
