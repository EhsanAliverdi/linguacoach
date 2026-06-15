# Phase 8J — listening_fill_in_blanks — Implementation Review

**Date:** 2026-06-16
**Related sprint:** [2026-06-15-staged-activity-content-migration-sprint.md](../sprints/2026-06-15-staged-activity-content-migration-sprint.md)
**Related feature:** Staged activity content migration, listening-primary exercise formats (Phase 8H/8I/8J)

## Summary

Implemented `listening_fill_in_blanks` end-to-end as the third runnable
listening-primary exercise format and the first runnable format with
secondary skill `writing`. Student listens to a short spoken script and
fills in 4 missing words in a transcript passage, each via a per-gap
dropdown of options. Evaluation is fully deterministic (no AI call),
keyed by gap `id`, comparing submitted answers against `answer` /
`acceptedAnswers` with case/whitespace-insensitive normalization.

## Files reviewed / changed

### Backend — Domain
- `src/LinguaCoach.Domain/Enums/InteractionMode.cs` — added
  `ListeningFillInBlanks = 16` (append-only, enum-pinned).
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added
  `ListeningFillInBlanks = "listening_fill_in_blanks"`.

### Backend — Persistence/Seed
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — new pattern
  definition: primary skill listening, secondary `["Writing"]`,
  `compatibleKindsJson: [2]` (ListeningInput), `ActivityType.ListeningComprehension`,
  `InteractionMode.ListeningFillInBlanks`, `MarkingMode.ExactMatch`,
  5 minutes, `requiresAudio: false`.
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` —
  promoted `listening_fill_in_blanks` from `Planned(...)` to `Ready(...)`:
  `ImplementationStatus=ready`, `SupportsPracticeGym=true`,
  `SupportsTodayLesson=false`, `PrimarySkill=listening`,
  `SecondarySkills=["writing"]`.
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — new prompt key
  `activity_generate_listening_fill_in_blanks` (maxInput:900, maxOutput:1300),
  full `module_stage_v1` template generating `audioScript` (40-90 words),
  `audioUrl: null`, `passageWithBlanks` with `{{gap1}}`-`{{gap4}}` tokens,
  exactly 4 `gaps` (id/answer/acceptedAnswers/4 options/explanation),
  `successChecklist`, plus learn/practice separation rules.

### Backend — Application
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` —
  `RequiredPracticeKeysByPatternKey["listening_fill_in_blanks"] =
  ["audioScript", "passageWithBlanks", "gaps"]`; `ForbiddenLearnContentKeys`
  extended with `answer`, `answers`, `acceptedAnswers`, `submit` (other
  forbidden keys — audioScript, transcript, gaps, options, passageWithBlanks,
  selectedAnswer(s), checkAnswer, answerKey, correctAnswer, checkButton,
  submitButton — already covered the rest of the spec's list).
- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs` — new
  `ListeningFillInBlanksContent` / `ListeningFillInBlanksGapDto` DTOs,
  kept separate from `ReadingFillInBlanksContent`/`ReadingFillInBlanksGapDto`
  to avoid affecting `reading_fill_in_blanks` / `reading_writing_fill_in_blanks`.
- `src/LinguaCoach.Application/Activity/Evaluators/ExactMatchEvaluator.cs` —
  new `listening_fill_in_blanks` branch in `ParseExpectedItems`: deserializes
  `ListeningFillInBlanksContent`, iterates `Gaps`, keyed by `gap.Id`, accepted
  answers from `AcceptedAnswers` (fallback `BuildAcceptedList(Answer)`).
  Reuses existing `Normalize()` for case/whitespace-insensitive comparison.

### Backend — Infrastructure
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` —
  added `"listening_fill_in_blanks"` to `StagedPatternKeys`.

### Frontend — new renderer
- `src/LinguaCoach.Web/src/app/features/activity/renderers/listening-fill-in-blanks/listening-fill-in-blanks.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/listening-fill-in-blanks/listening-fill-in-blanks.component.html` (new)
  - Reuses the Phase 8H/8I audio-fallback section (`audioUrl: null` always;
    text fallback of `audioScript` with "Audio is temporarily unavailable").
  - Reuses the `{{gapN}}`-token passage rendering + per-gap `<select>`
    dropdown pattern from `reading_fill_in_blanks` (Phase 8C/8E).
  - `data-testid`s: `audio-player-section`, `audio-player`,
    `audio-unavailable`, `audio-script-fallback`, `passage-with-blanks`,
    `gap-select-{gapId}`, `listening-fill-in-blanks-submit-btn`.

### Frontend — wiring
- `exercise-renderer.component.ts` — new `listeningFillInBlanksContent`
  getter (`stagedExerciseData` with `raw` fallback per established
  `ed[...] ?? raw[...]` pattern), `onListeningFillInBlanksSubmitted` handler,
  `ExerciseAnswerPayload` union extended with
  `{ kind: 'listeningFillInBlanks'; answers: Record<string, string> }`.
- `exercise-renderer.component.html` — new `@case ('listeningFillInBlanks')`.
- `activity-lesson.component.ts` — new branch serializing submission as
  `{ answers: {...} }`.
- `core/models/activity.models.ts` — `InteractionMode` type union extended
  with `'listeningFillInBlanks'`.

## Tests added/updated

- `tests/LinguaCoach.UnitTests/Domain/InteractionModeMarkingModeTests.cs` —
  `InteractionMode_ListeningFillInBlanks_IsSixteen`, total values 16→17.
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` —
  new `listening_fill_in_blanks` section: valid-payload test, missing-required-key
  theory (`audioScript`, `passageWithBlanks`, `gaps`), forbidden-learn-key
  theory (`audioScript`, `transcript`, `passageWithBlanks`, `gaps`, `options`,
  `answer`, `answers`, `acceptedAnswers`).
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs` — new
  `listening_fill_in_blanks` section: all-correct full score, one-wrong
  partial score, accepted-alternative-answer accepted, case/whitespace
  normalization, missing-answer handled safely.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` —
  pattern counts 19→20 (seeded), 18→19 (active after deactivation).
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` —
  new `ListeningFillInBlanks_IsReadyAndEligible` test; removed
  `listening_fill_in_blanks` from `OtherPlannedFormats_RemainNonRunnable`'s
  still-planned list; `Registry_SelectPracticeGymSkill_ExcludesDisabledPlannedAndUnsupportedRows`
  now also disables `listening_fill_in_blanks`.
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` —
  new `readyListeningFillInBlanks` fixture, ready/eligible test, and
  routing test mirroring the Phase 8I pattern.

## CI/CD results

- `git diff --check`: clean, no whitespace errors.
- `dotnet restore`: up to date.
- `dotnet build --configuration Release`: succeeded (0 errors, pre-existing
  warnings only).
- `dotnet test --configuration Release`: **717 unit / 485 integration / 3
  architecture — all green** (baseline was 699/484/3).
- Angular unit tests (`ng test --watch=false --browsers=ChromeHeadless`):
  **122/122 — all green** (baseline was 120).
- Angular dev build (`ng build --configuration development`): succeeded.
- Angular production build (`ng build --configuration production`): succeeded.
- Playwright e2e: not run — no live stack available in this environment,
  consistent with Phase 8H/8I. No new Playwright spec added; no existing
  8H/8I-specific Playwright spec exists to mirror.
- Deployment/startup validation: not run — no live deployment environment
  available in this session, consistent with Phase 8H/8I.

## Scope confirmations

- Only `listening_fill_in_blanks` was made runnable.
- `summarize_spoken_text`, `highlight_correct_summary`, `select_missing_word`,
  `highlight_incorrect_words`, `write_from_dictation`, all speaking formats,
  and all other planned formats remain non-runnable (Planned).
- No MinIO or audio lifecycle changes — `audioUrl` always `null`, text
  fallback only.
- No Today pre-generation changes — `SupportsTodayLesson=false`.
- `/activity`, `exerciseType=`/`type=`/`pattern=` compatibility preserved.
- No student data deleted; activity history / Practice Gym pool behavior
  unaffected for existing formats.

## Findings by priority

No defects found. Implementation followed the established Phase 8H/8I
patterns (audio fallback, staged content getters, append-only enum,
deterministic evaluator dispatch).

## Decisions made

- New `InteractionMode.ListeningFillInBlanks = 16` rather than reusing
  `ReadingFillInBlanks` (13) or `AudioAndGapFill` (9), since neither matches
  the required per-gap dropdown + `{{gapN}}` passage + audio-fallback shape
  without risking changes to `reading_fill_in_blanks` or `listen_and_gap_fill`.
- New `ListeningFillInBlanksContent`/`ListeningFillInBlanksGapDto` DTOs kept
  separate from the reading fill-in-blanks DTOs for the same isolation reason.
- `ExactMatchEvaluator` reused (not `KeyedSelectionEvaluator`) per
  `MarkingMode.ExactMatch` spec requirement.

## Risks or unresolved questions

- None identified. Playwright/deployment validation remain environment-limited
  as in prior phases.

## Final verdict

Phase 8J complete. All required backend and frontend changes implemented,
all CI/CD checks (build, unit, integration, Angular unit/dev/prod builds)
pass green. `listening_fill_in_blanks` is Ready, Practice-Gym-eligible,
listening-primary with writing secondary skill.

## Next recommended action

Proceed to the next planned listening or writing format per sprint backlog
(e.g. `summarize_spoken_text` or another planned listening format), following
the same staged-content + deterministic-evaluator + audio-fallback pattern
established across Phases 8H-8J.
