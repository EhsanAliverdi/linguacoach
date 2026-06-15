# Phase 8F Implementation Review — summarize_written_text

**Date:** 2026-06-15
**Sprint:** Staged Activity Content Migration Sprint
**Related sprint doc:** docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md

---

## Purpose

Promote `summarize_written_text` from Planned to Ready as the first runnable writing-primary exercise format. Student reads a passage and writes a concise summary. All other planned future formats must remain non-runnable.

---

## Files Modified

### Domain

- `src/LinguaCoach.Domain/ExercisePatternKey.cs` — added `SummarizeWrittenText = "summarize_written_text"` under a new `// ── Reading / Writing` section

### Application

- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` — added `["summarize_written_text"] = ["sourceText", "prompt"]` to `RequiredPracticeKeysByPatternKey`; added `sourceText`, `prompt`, `expectedSummary`, `modelSummary`, `keyPoints`, `answerKey`, `submittedSummary`, `textarea`, `submit` to `ForbiddenLearnContentKeys`

### Infrastructure

- `src/LinguaCoach.Infrastructure/Activity/Evaluators/AiStructuredEvaluator.cs` — added `ExercisePatternKey.SummarizeWrittenText => "activity_evaluate_summarize_written_text"` to `ResolvePromptKey` switch
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` — added `"summarize_written_text"` to `StagedPatternKeys` HashSet

### Persistence / Seed

- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` — replaced `Planned("summarize_written_text", ...)` with `Ready(...)`. Total row count remains 36.
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` — added pattern row: `InteractionMode.FreeTextEntry`, `MarkingMode.AiStructured`, `ActivityType.WritingScenario`, `compatibleKindsJson: [4]` (WritingTask), `estimatedMinutes: 7`, `workplaceContext: true`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` — seeded `activity_generate_summarize_written_text` (module_stage_v1 format, 100-150 word passage, summaryRequirements, keyPoints, successChecklist) and `activity_evaluate_summarize_written_text` (0-100 rubric, coachSummary, changes, whatYouDidWell, mainMistakes, grammarIssues, vocabularyIssues, miniLesson, improvedVersion, feedbackInSourceLanguage)

### Frontend

- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` — added `stagedExerciseData` private getter (unwraps `practiceContent.exerciseData` from `module_stage_v1`); updated `freeTextContent` getter to check `ed['sourceText']` → `situation` and `ed['prompt']` → `prompt`

---

## Design Decisions

| Decision | Rationale |
|---|---|
| Reuse `InteractionMode.FreeTextEntry` | Same textarea UI as speaking formats; no new enum value needed |
| Reuse `MarkingMode.AiStructured` | Rubric-based evaluation same as `email_reply`; `AiStructuredEvaluator.CompactContent` already strips learnContent |
| Reuse `FreeTextEntryComponent` | Source text mapped to `situation` field, prompt to `prompt` field; component handles both |
| `stagedExerciseData` getter in exercise-renderer | Centralises `module_stage_v1` unwrapping for reuse by other writing formats |
| Primary skill: writing | Student's output is a written summary; reading is secondary |

---

## Tests Added

### Unit

- `ModuleStageContentValidatorTests`: `Validate_SummarizeWrittenText_WithValidPayload_ReturnsValid`, `Validate_SummarizeWrittenText_MissingRequiredKey_Fails` (Theory: sourceText, prompt), `Validate_SummarizeWrittenText_WithForbiddenKeyInLearnContent_Fails` (Theory: 7 keys)

### Integration

- `ExerciseTypeCatalogTests`: `SummarizeWrittenText_IsReadyAndEligible`, `WriteEssay_RemainsPlanned`

### Angular

- `practice-gym.component.spec.ts`: `summarize_written_text is ready and available in Practice Gym`, `clicking Writing can return summarize_written_text and routes correctly`

---

## CI/CD Results

| Suite | Passed | Failed |
|---|---|---|
| Unit | 655 | 0 |
| Integration | 479 | 0 |
| Architecture | 3 | 0 |
| Angular | 114 | 0 |

All green. Baselines entering Phase 8G: **Unit 655 / Integration 479 / Architecture 3 / Angular 114**.

---

## Constraints Verified

- No other planned future format promoted to Ready
- No new `InteractionMode` enum value (no new pin tests required)
- `/activity`, `exerciseType=`, `type=`, `pattern=` query param compatibility preserved
- `write_essay` remains Planned (verified by `WriteEssay_RemainsPlanned` test)
- No audio, no MinIO, no Today pre-generation

---

## Next Recommended Action

Phase 8G candidates: `summarize_spoken_text` (listening-primary, writing secondary) or another writing-primary planned format.
