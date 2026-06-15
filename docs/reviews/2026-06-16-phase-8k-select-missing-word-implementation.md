# Phase 8K — select_missing_word — Implementation Review

**Date:** 2026-06-16
**Related sprint:** [2026-06-15-staged-activity-content-migration-sprint.md](../sprints/2026-06-15-staged-activity-content-migration-sprint.md)
**Related feature:** Staged activity content migration, listening-primary exercise formats (Phase 8H/8I/8J/8K)

## Summary

Implemented `select_missing_word` end-to-end as the fourth runnable
listening-primary exercise format. Student listens to a short spoken
script where the displayed text ends with a missing word/phrase
(shown as `{{missing}}` -> blank), then chooses the word/phrase that
correctly completes it from 4 options. Evaluation is fully
deterministic (no AI call), reusing the existing single-choice
`KeyedSelectionEvaluator` branch — compare selected option id to
`correctOptionId`.

## Files reviewed / changed

### Backend — Domain
- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added
  `SelectMissingWord = "select_missing_word"`.
- No new `InteractionMode` value — reuses `MultipleChoice = 3`
  (same shape as `listening_multiple_choice_single`, Phase 8H).

### Backend — Persistence/Seed
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — new
  pattern definition: primary skill listening, secondary `[]`,
  `compatibleKindsJson: [2]` (ListeningInput),
  `ActivityType.ListeningComprehension`, `InteractionMode.MultipleChoice`,
  `MarkingMode.KeyedSelection`, 5 minutes, `requiresAudio: false`.
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` —
  promoted `select_missing_word` from `Planned(...)` to `Ready(...)`:
  `ImplementationStatus=ready`, `SupportsPracticeGym=true`,
  `SupportsTodayLesson=false`, `PrimarySkill=listening`,
  `SecondarySkills=[]`.
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — new prompt key
  `activity_generate_select_missing_word` (maxInput:900, maxOutput:900),
  full `module_stage_v1` template generating `audioScript` (30-70 words,
  includes the correct missing word/phrase naturally), `audioUrl: null`,
  `incompleteText` (same script with the missing word/phrase replaced by
  `{{missing}}`), `question`, exactly 4 `options` with `correctOptionId`,
  `explanation`, `distractorExplanations`, `successChecklist`, plus
  learn/practice separation rules.

### Backend — Application
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` —
  `RequiredPracticeKeysByPatternKey["select_missing_word"] =
  ["audioScript", "incompleteText", "options", "correctOptionId"]`;
  `ForbiddenLearnContentKeys` extended with `incompleteText`,
  `missingWord`, `missingPhrase` (audioScript, transcript, question,
  options, correctOptionId, distractorExplanations already covered by
  existing forbidden keys).
- `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` —
  dispatch in `EvaluateAsync` extended: `select_missing_word` routes to
  the existing `EvaluateReadingMultipleChoiceSingleAsync` (same as
  `reading_multiple_choice_single` / `listening_multiple_choice_single`).
  No new DTOs — reuses `ReadingMultipleChoiceExerciseData` /
  `ReadingMultipleChoiceSubmittedAnswer`. The "audio" vs "passage" wording
  in feedback/coach-summary (`sourceNoun`) now also covers
  `select_missing_word`.

### Backend — Infrastructure
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` —
  added `"select_missing_word"` to `StagedPatternKeys`.

### Frontend — renderer reuse (no new component)
- `src/LinguaCoach.Web/src/app/features/activity/renderers/reading-multiple-choice/reading-multiple-choice.component.ts` —
  `ReadingMultipleChoiceContent` gained a new optional `incompleteText`
  field; new `incompleteTextDisplay` getter replaces the literal
  `{{missing}}` token with `_____` for display.
- `.../reading-multiple-choice.component.html` — new card section
  (`data-testid="incomplete-text"`) rendering `incompleteTextDisplay`
  when `content.incompleteText` is present, placed above the question.
- `select_missing_word` reuses the existing `app-reading-multiple-choice`
  component and `@case ('multipleChoice')` — same component already used
  by `reading_multiple_choice_single` and `listening_multiple_choice_single`.

### Frontend — wiring
- `exercise-renderer.component.ts` — `readingMultipleChoiceContent` getter
  extended to read `incompleteText` from `stagedExerciseData`/`raw`
  (`ed['incompleteText'] ?? raw['incompleteText']`).
- No new `ExerciseAnswerPayload` kind — reuses `'multipleChoiceSingle'`
  and the existing `onReadingMultipleChoiceSubmitted` handler.
- No changes to `exercise-renderer.component.html` (`@case`),
  `activity-lesson.component.ts`, or `core/models/activity.models.ts` —
  all reused unchanged from the existing `multipleChoice` interaction mode.

## Tests added/updated

- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` —
  new `select_missing_word` section: valid-payload test, missing-required-key
  theory (`audioScript`, `incompleteText`, `options`, `correctOptionId`),
  forbidden-learn-key theory (`audioScript`, `transcript`, `incompleteText`,
  `question`, `options`, `correctOptionId`).
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs` —
  new `select_missing_word` section: correct selection full score,
  incorrect selection zero score with distractor explanation, no
  selection handled safely.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs` —
  new `SelectMissingWord_IsReadyAndEligible` test; removed
  `select_missing_word` from `OtherPlannedFormats_RemainNonRunnable`'s
  still-planned list; `Registry_SelectPracticeGymSkill_ExcludesDisabledPlannedAndUnsupportedRows`
  now also disables `select_missing_word`.
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs` —
  pattern counts 20→21 (seeded/idempotent/active), 19→20 (active after
  one pattern deactivated).
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts` —
  new `readySelectMissingWord` fixture, ready/eligible test, and routing
  test mirroring the Phase 8J pattern.

## CI/CD results

- `git diff --check`: clean, no whitespace errors.
- `dotnet restore`: up to date.
- `dotnet build --configuration Release`: succeeded (0 errors, pre-existing
  warnings only).
- `dotnet test --configuration Release`: **731 unit / 486 integration / 3
  architecture — all green** (baseline was 717/485/3).
- Angular unit tests (`ng test --watch=false --browsers=ChromeHeadless`):
  **124/124 — all green** (baseline was 122).
- Angular dev build (`ng build --configuration development`): succeeded
  (pre-existing `PatternEvaluationResultComponent` template warning only,
  unrelated to this change).
- Angular production build (`ng build --configuration production`): succeeded.
- Playwright e2e: not run — no live stack available in this environment,
  consistent with Phase 8H/8I/8J. No new Playwright spec added; no existing
  8H/8I/8J-specific Playwright spec exists to mirror.
- Deployment/startup validation: not run — no live deployment environment
  available in this session, consistent with Phase 8H/8I/8J.

## Scope confirmations

- Only `select_missing_word` was made runnable.
- `summarize_spoken_text`, `highlight_correct_summary`,
  `highlight_incorrect_words`, `write_from_dictation`, all speaking formats,
  and all other planned formats remain non-runnable (Planned).
- No MinIO or audio lifecycle changes — `audioUrl` always `null`, text
  fallback only.
- No Today pre-generation changes — `SupportsTodayLesson=false`.
- `/activity`, `exerciseType=`/`type=`/`pattern=` compatibility preserved.
- No student data deleted; activity history / Practice Gym pool behavior
  unaffected for existing formats.

## Findings by priority

No defects found. Implementation followed the established Phase 8H/8I/8J
patterns and intentionally maximized reuse (no new enum value, no new
DTOs, no new renderer component, no new evaluator branch logic — only
dispatch routing).

## Decisions made

- Reuse `InteractionMode.MultipleChoice = 3` and `MarkingMode.KeyedSelection`
  rather than adding a new enum value, since `select_missing_word` is
  structurally identical to `listening_multiple_choice_single` (audio +
  question + 4 options + `correctOptionId` + `explanation` +
  `distractorExplanations`) — only the displayed practice text differs
  (`incompleteText` vs no passage/question-only).
- Reuse the existing `app-reading-multiple-choice` component and
  `'multipleChoiceSingle'` payload kind rather than building a new
  renderer, adding only the `incompleteText` field/display to avoid
  duplicating the audio-fallback + options + submit UI.
- `KeyedSelectionEvaluator` reused via dispatch extension (not a new
  evaluator class) per `MarkingMode.KeyedSelection` spec requirement.

## Risks or unresolved questions

- None identified. Playwright/deployment validation remain environment-limited
  as in prior phases.

## Final verdict

Phase 8K complete. All required backend and frontend changes implemented,
all CI/CD checks (build, unit, integration, Angular unit/dev/prod builds)
pass green. `select_missing_word` is Ready, Practice-Gym-eligible,
listening-primary with no secondary skills.

## Next recommended action

Proceed to the next planned listening format per sprint backlog
(e.g. `summarize_spoken_text`, `highlight_correct_summary`,
`highlight_incorrect_words`, or `write_from_dictation`), following the
same staged-content + deterministic-evaluator + audio-fallback pattern
established across Phases 8H-8K.
