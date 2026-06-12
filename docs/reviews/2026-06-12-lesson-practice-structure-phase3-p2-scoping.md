# Phase 3 P2 — Structured "What we learn" Framing: Scoping Pass

Date: 2026-06-12
Related sprint: `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md` (Phase 3)
Status: Scoping only — no code changes in this pass

## Context

Per `docs/reviews/2026-06-12-lesson-practice-structure-phase3-plan.md`, P2 is the
remaining Phase 3 item: replace the single `learningGoal` line in
`ExerciseLessonIntroComponent` with a structured "What we learn" breakdown
(grammar / vocabulary / phrases), per the original product brief.

## Files reviewed

- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/exercise-lesson-intro/`
- All 9 renderer components referencing `learningGoal` / `targetPhrases` /
  `targetVocabulary` (grep across `renderers/`)
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` (generation prompts)
- `src/LinguaCoach.Domain/ExercisePatternKey.cs`

## Findings

### Good news: most of the data already exists

`ActivityDto` (`activity.models.ts:32-43`) already has, at top level (shared across all
activity types):

- `learningGoal: string | null`
- `targetPhrases: string[]`
- `targetVocabulary: string[]`

These are populated for `WritingScenario` today. `targetPhrases` is also rendered by
ChatReply, EmailReply, and FreeTextEntry renderers already (via their own inline blocks,
not `ExerciseLessonIntroComponent`).

### Gap 1 (revised after verification) — pattern-driven generators have NO goal/phrase/vocab fields, but DO have an unused `teachingNote`

Checked the actual generation prompts (`DefaultAiSeeder.cs`): `phrase_match`,
`gap_fill_workplace_phrase`, and `listen_and_answer` generation prompts have **none** of
`learningGoal`, `targetPhrases`, `targetVocabulary`. So Step 2 ("wire existing fields
into 4 renderers") would render nothing for these patterns — not a safe no-op so much as
a dead end as originally framed.

However, every one of these prompts already asks the AI for a `"teachingNote"` field
("one sentence about the language pattern practised" / "common thread in these
phrases") — and **this field is generated but never parsed, mapped, or displayed
anywhere** (confirmed: zero references to `teachingNote` outside `DefaultAiSeeder.cs`).

This is the actual quick win for "What we learn" on these patterns: surface the
existing `teachingNote` as the `[goal]`-equivalent via `ExerciseLessonIntroComponent`,
no new AI prompt fields needed.

### Gap 2 — no structured grammar field

No DTO field exists for "the grammar point this exercise teaches". One generation
prompt (line ~415 of `DefaultAiSeeder.cs`) has a `grammarFocus` value in its *example*
JSON for content generation context, but this is not surfaced to `ActivityDto` or the
frontend. A new optional field `targetGrammarPoint: string | null` would be needed.

### Gap 3 — `ExerciseLessonIntroComponent` renders one line only

Current template (`exercise-lesson-intro.component.html`) renders:
```
Goal: {{ goal }}
```
To become "What we learn", it needs to additionally accept and render
`targetGrammarPoint`, `targetVocabulary[]`, `targetPhrases[]` — each optional, only
shown if present (graceful degradation for patterns that don't populate them yet).

## Recommended approach (incremental, no big-bang)

1. **Extend `ExerciseLessonIntroComponent`** to accept optional
   `grammarPoint?: string | null`, `vocabulary?: string[]`, `phrases?: string[]` inputs
   alongside the existing `goal`. Render as a small "What we learn" card with up to
   three labeled rows (Grammar / Vocabulary / Phrases), each only shown if non-empty.
   This is purely additive — existing `[goal]`-only usages keep working unchanged.

2. **Surface the existing `teachingNote` field as `[goal]`** for GapFill and
   MatchingPairs (confirmed `PhraseMatchContent`/gap-fill content DTOs in
   `PatternContentDtos.cs` already have `TeachingNote`; `ListenAndGapFillContent`
   needs the same check — verify before extending to AudioAndGapFill/AudioAndFreeText):

   Important: for these two patterns, `content` in the renderer (`GapFillContent`,
   `MatchingPairsContent` on the frontend) is parsed directly from
   `ActivityDto.ContentJson` (the raw `rendererContentJson` — see
   `ActivityGetHandler.cs:386`), **not** from `ActivityDto.LearningGoal`. The AI JSON
   has `teachingNote`, but the frontend interfaces read `learningGoal`. Two equally
   small options:
   - **(a) Frontend-only**: in `GapFillContent`/`MatchingPairsContent` (and their
     `.html` templates' `[goal]` binding), read
     `content.learningGoal ?? content.teachingNote` — zero backend changes, but the
     interfaces gain a `teachingNote?: string | null` field reflecting the raw AI JSON
     shape.
   - **(b) Backend mapping**: when building `rendererContentJson` for these patterns
     (`ActivityGetHandler.cs`), copy `teachingNote` into a `learningGoal` key in the
     JSON sent to the frontend, so the existing `learningGoal`-reading interfaces work
     unchanged.

   Recommendation: (a) — smaller diff, keeps `ContentJson` a closer mirror of the raw
   AI output, and the frontend interfaces already optionally type `learningGoal` as
   nullable so adding a sibling optional field is consistent.

   This gets "What we learn" framing onto gap_fill and matching_pairs (the two
   `VocabularyPractice`-typed exercise patterns, currently `LearningGoal: null` at
   `ActivityGetHandler.cs:414`) with **zero new AI prompt changes** — the AI already
   generates `teachingNote`, it's just discarded today.

3. **Add `targetGrammarPoint` (new field)** — smallest possible addition:
   - `ActivityDto.targetGrammarPoint: string | null` (frontend model)
   - Corresponding backend DTO field (wherever `LearningGoal`/`TargetPhrases` are
     currently mapped — likely `ActivityGetHandler` / activity content DTOs)
   - Update generation prompts to optionally emit `"targetGrammarPoint"` — start with
     the patterns that already have a natural grammar focus (email reply, teams chat,
     gap fill) and treat it as optional/nullable everywhere else.

4. **ChatReply / EmailReply / FreeTextEntry** — these already render `targetPhrases`
   inline with their own markup (Phase 4 of Exercise UX). Decide whether to migrate
   them onto the shared `ExerciseLessonIntroComponent` "What we learn" card (consistency)
   or leave their existing inline blocks (less churn). Recommendation: leave as-is for
   now — migrating them is cosmetic consistency work, not part of the P2 brief, and
   risks regressions in already-shipped UI.

## Sizing

- Step 1 (component extension): small, isolated, no backend changes.
- Step 2 (wire existing fields into 4 renderers): small, template-only, but needs
  verification that `targetPhrases`/`targetVocabulary` are actually populated by the
  AI generators for gap_fill/matching_pairs/audio patterns — if not populated, the new
  UI sections simply won't show (`@if` guards), so this is safe either way.
- Step 3 (new `targetGrammarPoint` field): touches backend DTO + AI prompts for
  multiple patterns — this is the larger-effort item and the main reason P2 was
  deferred as its own phase. Could be done pattern-by-pattern incrementally rather than
  all at once.
- Step 4: out of scope / optional polish, not required by the brief.

## Decisions made

- P2 will be implemented incrementally: Steps 1-2 first (low risk, mostly additive UI
  using existing data), Step 3 (`targetGrammarPoint`) as a separate follow-up once
  Steps 1-2 are verified, prioritizing the patterns where it adds the most value
  (email_reply, teams_chat_simulation, gap_fill_workplace_phrase).
- Step 4 (migrating ChatReply/EmailReply/FreeTextEntry onto the shared component) is
  out of scope for P2 — left as-is.

## Implementation tasks produced

1. **GapFill/MatchingPairs `teachingNote` → goal display** (frontend-only,
   Option (a) above):
   - `gap-fill.component.ts` `GapFillContent`: add `teachingNote?: string | null`;
     update `.html` to `[goal]="content.learningGoal ?? content.teachingNote"`.
   - `matching-pairs.component.ts` `MatchingPairsContent` (or equivalent): same change.
   - Verify `ListenAndGapFillContent` (audio-and-gap-fill) AI prompt/DTO for an
     equivalent field before deciding whether to extend to AudioAndGapFill/
     AudioAndFreeText in the same pass or a follow-up.
2. (Deferred follow-up) Extend `ExerciseLessonIntroComponent` with optional
   `grammarPoint`/`vocabulary`/`phrases` inputs for a richer "What we learn" card —
   only worth doing once a pattern actually has that data (see task 3).
3. (Deferred follow-up) Add `targetGrammarPoint: string | null` + populate
   `targetVocabulary`/`targetPhrases` for pattern-driven generators (email_reply,
   teams_chat, gap_fill_workplace_phrase) — cross-cutting AI prompt work, done
   pattern-by-pattern.

## Risks / unresolved questions

- `ListenAndGapFillContent` (audio-and-gap-fill pattern) needs the same check as
  `PhraseMatchContent`/gap-fill — does its AI prompt already emit `teachingNote` or
  equivalent? If yes, include in task 1; if no, defer with tasks 2-3.
- Tasks 2-3 are additive/optional and need no further product input — pick up
  incrementally as time allows.

## Final verdict

P2's only concrete near-term win is task 1: surfacing the already-generated but
discarded `teachingNote` field as the "Goal" line for GapFill/MatchingPairs — a small,
frontend-only, two-file change. Everything else (richer structured "What we learn" card,
new grammar/vocab AI fields for pattern-driven generators) is genuinely cross-cutting
AI-prompt work and should remain deferred, picked up pattern-by-pattern as separate
follow-ups, not as a single P2 implementation push.

## Next recommended action

Implement task 1 (GapFill/MatchingPairs `teachingNote` → `[goal]`) as a small follow-up
change. Treat tasks 2-3 as future, separately-scoped work — not part of this Phase 3
closeout.
