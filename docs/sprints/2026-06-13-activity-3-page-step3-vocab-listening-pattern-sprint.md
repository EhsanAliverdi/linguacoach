---
status: done
lastUpdated: 2026-06-13 00:00
owner: engineering
supersedes:
supersededBy:
---

# Step 3 — VocabularyPractice + ListeningComprehension → Pattern Engine

Related: [2026-06-13-activity-3-page-restructure-eng-plan.md](../reviews/2026-06-13-activity-3-page-restructure-eng-plan.md) (Step 3 of the 5-step strangler-fig migration). Continues `current-sprint.md`'s "Activity 3-page restructure" entry (Steps 1-2 done).

## Goal

Eliminate the legacy (non-pattern, `interactionMode == null`) `VocabularyPractice`
and `ListeningComprehension` generation path, so every new activity of these
types carries `interactionMode` + `contentJson` and renders through
`ExerciseRendererComponent` via `PatternBackedPresenter`. Once no new legacy
rows are created and old ones age out, `LegacyVocabPresenter` /
`LegacyListeningPresenter` and the corresponding `activity-teach-page`/
`activity-practice-page` template branches (`vocabLearning`/`vocabPractice`,
`listeningLearning`/`listeningPractice`) can be deleted.

## Findings — current state is further along than the eng plan assumed

A research pass (2026-06-13) found the pattern engine for vocab/listening is
**already built and in active use** for Today's Lesson generation:

- `ExercisePatternSeeder` (`src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`)
  already defines `phrase_match`, `gap_fill_workplace_phrase`,
  `listen_and_answer`, `listen_and_gap_fill` — all P0 patterns from
  `exercise-pattern-library.md`.
- `DefaultAiSeeder` already has calibrated generate/evaluate prompts for all
  four (`activity_generate_phrase_match`, `activity_generate_gap_fill_workplace_phrase`,
  `activity_generate_listen_and_answer`, `activity_generate_listen_and_gap_fill`).
- `SessionGeneratorService` / `SessionDurationTemplates` already select these
  pattern keys exclusively for Today's Lesson — `interactionMode` is always
  set for session-generated activities.
- `ActivityGetHandler.HandlePatternKeyedAsync` (the `?pattern=` path used by
  Practice Gym) is fully pattern-based.

**The remaining gap is narrow**: `GET /api/activity/next` with **no**
`?pattern=` and **no** `?type=` query param still calls
`ResolveActivityTypeAsync` (`ActivityGetHandler.cs:349-379`), which on every
4th attempt picks `ActivityType.VocabularyPractice` (via
`VocabPracticeIntervalAttempts = 4`) and on every 5th picks
`ActivityType.ListeningComprehension` (`ListeningIntervalAttempts = 5`), then
generates a **legacy, non-pattern activity** (`MapToDto` with null
`interactionMode`, `vocabItems`/`listeningQuestions` populated directly).
This is the "every-Nth-attempt" deterministic/AI-generated path — it is live
and reachable from the legacy `/activity` route (and any caller of
`getNext()` without a pattern/type override).

Naming note: the eng plan's Step 3 description says "new prompt keys, new
`ExercisePatternDefinition` rows" — these already exist under the names
`gap_fill_workplace_phrase` (vocab) and `listen_and_answer` /
`listen_and_gap_fill` (listening), not new names to be invented.

## Content parity gap

Legacy `VocabularyPractice.vocabItems[]` carries per-item `hint` and
`explanation` fields (shown via "Show hint" toggle and post-feedback
explanation). The pattern-based `gap_fill_workplace_phrase` schema's gap
items do not currently carry an equivalent per-item hint/explanation field —
confirm against `GapFillContent`/`GapFillItem` (Domain) and the
`gap-fill.component.ts` renderer before switching the default path, or the
"Show hint" UX disappears for vocab practice generated this way.

## Proposed implementation slices

1. **Confirm parity or extend `gap_fill_workplace_phrase` schema** with
   per-item hint/explanation (Domain entity + AI prompt + renderer). Skip if
   product accepts the UX simplification.
2. **Switch `ResolveActivityTypeAsync`'s Vocab/Listening branches** in
   `ActivityGetHandler` to call `HandlePatternKeyedAsync("gap_fill_workplace_phrase", ...)`
   / `HandlePatternKeyedAsync("listen_and_answer", ...)` (or
   `listen_and_gap_fill`, TBD which is the better Today's-Lesson-equivalent)
   instead of the legacy `MapToDto` branches. Remove
   `_vocabGenerator`/legacy listening generation code paths once unused.
3. **Verify no remaining production paths** create `interactionMode == null`
   rows of these two activity types (grep `MapToDto` callers, Practice Gym
   pools, admin "Generate lessons now").
4. **Frontend**: once step 3 ships and existing legacy rows have aged out of
   active use (`LearningActivity` rows are read-once via Today's Lesson /
   Practice Gym, not long-lived — low retention risk), delete
   `LegacyVocabPresenter`, `LegacyListeningPresenter`, and the
   `vocabLearning`/`vocabPractice`/`listeningLearning`/`listeningPractice`
   branches from the Teach/Practice page templates and
   `ActivityPagePresenter` union types. This is a follow-up PR, not part of
   slice 1-3.
5. AI prompt calibration pass for any prompt changes from slice 1, per
   `live-ai-quality-review-prompt-calibration-sprint.md` precedent.
6. Update `exercise-pattern-library.md` (Pattern Implementation Priority —
   already lists these as P0, just confirm), `learning-activity-engine.md`
   (Activity Type Roadmap), and `course-session-learning-model.md` if the
   `/activity` legacy route's behavior changes.

## Risks / open questions

- Does removing the legacy `VocabularyPractice`/`ListeningComprehension`
  generation change the "every 4th/5th attempt" cadence semantics in any
  user-visible way? (Should not — same `ActivityType` is returned, only the
  internal representation changes to pattern-keyed.)
- Confirm the legacy `/activity` route (no query params) is still reachable
  in the current student nav — if it's dead UI, slice 2 may be unnecessary
  and Step 3 reduces to documentation + presenter cleanup only.
- Slice 1 (hint/explanation parity) needs a product decision: extend schema
  vs. accept UX change. Recommend a quick product check before slice 2.

## Decisions made (this planning pass)

- Step 3 is **not** "build new patterns from scratch" — it's a routing
  switchover + content-parity check + eventual presenter cleanup.
- Slices 1-3 are one sprint; slice 4 (presenter/template deletion) is a
  separate follow-up sprint gated on production data review.

## Implementation (2026-06-13)

Slices 1-3 implemented per user direction ("finish all the steps").

- **Slice 1**: `gap_fill_workplace_phrase` gap items now carry `hint`
  end-to-end. `GapFillItem` (frontend) gained `hint?: string | null`;
  `exercise-renderer.component.ts`'s `mapGapItems` primary branch maps
  `obj['hint']`; `gap-fill.component.ts`/`.html` add a "Show hint" toggle
  matching the legacy vocab UX. Backend `GapFillItemDto.Hint` and the AI
  prompt schema already supported this — no backend change needed.
- **Slice 2**: in `ActivityGetHandler.HandleAsync`, when
  `ResolveActivityTypeAsync` picks `VocabularyPractice` or
  `ListeningComprehension` via the every-4th/5th-attempt cadence (and no
  `?type=`/`?pattern=` override is present), the handler now calls
  `HandlePatternKeyedAsync(ExercisePatternKey.GapFillWorkplacePhrase, ...)`
  / `HandlePatternKeyedAsync(ExercisePatternKey.ListenAndAnswer, ...)`
  instead of the legacy null-`interactionMode` generation. `_vocabGenerator`
  and the legacy `ListeningComprehension` AI-generation branch are retained
  only for the explicit `?type=` override path (`ActivityController`'s
  legacy `/activity?type=` query), which is still wired up.
- **Slice 3**: confirmed `PracticeGymGenerationJob` and
  `ExercisePrepareHandler` (Today's Lesson session generation) were already
  pattern-keyed per the original findings — no other code path creates
  `interactionMode == null` rows of these two types via the cadence-driven
  `/activity/next` flow.
- **Slice 4** (delete `LegacyVocabPresenter`/`LegacyListeningPresenter` and
  the `vocabLearning`/`vocabPractice`/`listeningLearning`/`listeningPractice`
  template branches): **not done** — explicitly deferred as a follow-up
  sprint gated on production data review (existing rows created via
  `?type=` overrides still need the legacy presenters). Revisit once
  `?type=` override usage is confirmed negligible or removed.
- **Slice 5** (AI prompt calibration pass): not applicable — no prompt
  changes were made in slice 2 (reusing existing calibrated
  `activity_generate_gap_fill_workplace_phrase` /
  `activity_generate_listen_and_answer` prompts).
- **Slice 6**: `exercise-pattern-library.md` already listed these as P0;
  no roadmap doc changes needed since `ActivityType` returned by
  `/activity/next` is unchanged, only the internal representation
  (`interactionMode`/`contentJson`) now populated.

`dotnet build` and `ng build` clean. Verdict: Step 3 complete except Slice 4
(intentionally deferred, tracked above).

## Next recommended action

Open a follow-up sprint for Slice 4 (legacy presenter/template removal)
once production data confirms `?type=`-override-generated legacy rows have
aged out. Otherwise proceed to Step 4 (WritingScenario →
`open_writing_task`).
