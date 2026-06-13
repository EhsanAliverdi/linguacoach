# Activity 3-Page Restructure — Steps 4 & 5 Implementation Review

Date: 2026-06-13
Related: [2026-06-13-activity-3-page-restructure-eng-plan.md](2026-06-13-activity-3-page-restructure-eng-plan.md),
[current-sprint.md](../sprints/current-sprint.md),
[2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md](../sprints/2026-06-13-activity-3-page-step3-vocab-listening-pattern-sprint.md)

## Context

Steps 1-3 of the 5-step strangler-fig migration (orchestrator split,
presenter factory, VocabularyPractice/ListeningComprehension → pattern
engine) were complete and committed (`e6d5345`). The eng plan flagged
Steps 4-5 as multi-day efforts each needing live AI prompt calibration.
Per explicit instruction ("finish all the steps... do whatever is
required"), and a follow-up `AskUserQuestion` decision ("Full
implementation attempt"), Steps 4 and 5 were implemented in full,
accepting that AI prompt output is unverified pending a live-AI
calibration pass.

## Files reviewed / changed

**Step 4 — WritingScenario → `open_writing_task`:**
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `OpenWritingTask`,
  `SpeakingRoleplayTurn` constants.
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added
  `AudioResponse = 11`.
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` — added
  `'audioResponse'` to the `InteractionMode` union.
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added
  `OpenWritingTask` and `SpeakingRoleplayTurn` pattern definitions.
- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs` — added
  `OpenWritingTaskContent`, `SpeakingRoleplayTurnContent` (documentation
  DTOs matching AI prompt JSON contracts).
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — added 4 new
  prompt constants + bodies (generate/evaluate × open_writing_task /
  speaking_roleplay_turn), registered via `SeedOrUpgradePromptAsync`.
- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiOpenEndedEvaluator.cs`
  — generalized via `ResolvePromptKey(patternKey)` switch so one evaluator
  serves `spoken_response_from_prompt`, `open_writing_task`, and
  `speaking_roleplay_turn`.
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` —
  `WritingScenario` cadence picks (no `?type=` override) now route through
  `HandlePatternKeyedAsync(OpenWritingTask, ...)`.

**Step 5 — SpeakingRolePlay → `speaking_roleplay_turn` + `AudioResponse`:**
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` — added
  `SpeakingRolePlay` case to the same routing block:
  `HandlePatternKeyedAsync(SpeakingRoleplayTurn, ...)`.
- `src/LinguaCoach.Api/Controllers/ActivityController.cs` —
  `SubmitSpeakingAttempt` (`POST /api/activity/{id}/speaking-attempt`) is
  now pattern-aware:
  - if `activity.ExercisePatternKey == speaking_roleplay_turn`, the
    transcript is routed through `IPatternEvaluationRouter` →
    `AiOpenEndedEvaluator` (prompt `activity_evaluate_speaking_roleplay_turn`),
    and the `PatternEvaluationResult` is mapped to `ActivityFeedbackDto`.
  - otherwise (legacy `spoken_response_from_prompt` / no pattern key),
    the original `SpeakingRolePlayEvaluator` path (prompt
    `activity_evaluate_speaking_roleplay`) is unchanged.
  - Added `IPatternEvaluationRouter` to the controller's DI constructor.

## Frontend: no changes required for Step 5

Investigated whether Step 5 needed a new `AudioResponse`/`audioResponse`
renderer component, `ExerciseAnswerPayload` variant, and
`ActivityPresenterFactory` changes (as anticipated in the eng plan).

Found that `ActivityGetHandler.MapToDto` branches the speaking DTO shape
on `activity.ActivityType == SpeakingRolePlay` (not on pattern key), and
deserializes a `SpeakingContent` shape that is identical to the new
`SpeakingRoleplayTurnContent` (`Scenario`, `StudentRole`, `ListenerRole`,
`SpeakingGoal`, `Prompt`, `ExpectedPoints`, `SuggestedPhrases`,
`MaxDurationSeconds`). `LegacySpeakingPresenter` (`speakingScenario` /
`speakingRecord` blocks) and `ActivityLessonComponent`'s recording state
machine (`isSpeakingRolePlay()`, `requestMicPermission`/`startRecording`/
`submitRecording`/etc.) are likewise keyed on `activityType`, not pattern
key. The existing recording UI **is** the `AudioResponse` UI — it already
works end-to-end for `speaking_roleplay_turn` activities once the backend
generates and evaluates them via the pattern engine.

`ActivityPresenterFactory.for()`'s `activity.activityType !==
'speakingRolePlay'` exclusion from `PatternBackedPresenter` was therefore
**left in place** — `speaking_roleplay_turn` activities continue through
`LegacySpeakingPresenter`, which is the correct (and only working) path
since `ExerciseRendererComponent` has no `audioResponse` case and was not
built. Building a new generic audio renderer was assessed as unnecessary
scope: it would duplicate the working recording UI for no functional gain
and adds risk without live AI calibration to validate against.

## Findings grouped by priority

**P0 — none.** Builds and existing tests pass.

**P1 — AI prompt calibration pending.** The 4 new prompts
(`activity_generate_open_writing_task`, `activity_evaluate_open_writing_task`,
`activity_generate_speaking_roleplay_turn`,
`activity_evaluate_speaking_roleplay_turn`) were written following existing
prompt conventions but not exercised against a live AI provider. Per the
user's "Full implementation attempt" decision, this is accepted as a
follow-up.

**P2 — documentation.** Roadmap tables in `exercise-pattern-library.md` and
`learning-activity-engine.md` list pattern keys / activity-type roadmap;
both new pattern keys (`open_writing_task`, `speaking_roleplay_turn`) and
the new `AudioResponse` interaction mode should be reflected (tracked as a
follow-up task below).

## Decisions made

1. Legacy `SpeakingRolePlayEvaluator` / `spoken_response_from_prompt` /
   explicit `?type=SpeakingRolePlay` path is preserved unchanged for
   backward compatibility — only `speaking_roleplay_turn` activities use
   the new pattern-router path.
2. No new frontend renderer/component for `AudioResponse` — the existing
   `speakingScenario`/`speakingRecord` blocks and recording state machine
   already serve this pattern (see "Frontend" section above).
3. `ActivityPresenterFactory`'s `speakingRolePlay` exclusion from
   `PatternBackedPresenter` is intentionally retained, not a leftover bug.

## Implementation tasks produced

- [ ] Live AI calibration pass for the 4 new prompts once AI provider
      access is available; adjust prompt wording/JSON schema if output
      doesn't match `AiOpenEndedPayload`/`OpenWritingTaskContent`/
      `SpeakingRoleplayTurnContent` expectations.
- [ ] Update `docs/architecture/exercise-pattern-library.md` Pattern
      Implementation Priority table and `docs/architecture/
      learning-activity-engine.md` Activity Type Roadmap table to mark
      `open_writing_task`, `speaking_roleplay_turn`, and `AudioResponse`
      as implemented.
- [ ] (Deferred, per Step 3 precedent) retire `LegacyWritingPresenter`/
      `LegacySpeakingPresenter`/`LegacyVocabPresenter`/
      `LegacyListeningPresenter` once production activity rows have been
      regenerated under the pattern engine.

## Risks / unresolved questions

- AI prompt JSON output for the 2 new generate prompts and 2 new evaluate
  prompts is unverified (see P1 above).
- `speaking_roleplay_turn`'s `markingMode: AiOpenEnded` + `AudioResponse`
  interaction mode combination is new; `AudioResponse` is currently unused
  by any frontend `@switch`, but this is fine since the speaking flow
  doesn't dispatch on `interactionMode` for rendering.

## Final verdict

Steps 4 and 5 complete. Backend build (`dotnet build`) and unit tests
(51/51, `tests/LinguaCoach.UnitTests`) pass. `ng build` clean (only
pre-existing CSS warnings). All 5 steps of the strangler-fig migration are
now landed.

## Next recommended action

Schedule a live-AI calibration session for the 4 new prompts, then update
the architecture roadmap docs per the implementation tasks above.
