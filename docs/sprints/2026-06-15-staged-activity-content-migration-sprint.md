---
status: in-progress
lastUpdated: 2026-06-15
owner: architecture
relatedArchitecture: docs/architecture/learning-activity-engine.md#staged-activity-content-module_stage_v1
relatedReview: docs/reviews/2026-06-15-learn-practice-feedback-structure-investigation.md
---

# Staged Activity Content Migration Sprint

## Background

`docs/reviews/2026-06-15-learn-practice-feedback-structure-investigation.md`
confirmed the Learn step for Practice Gym / Today Listening activities
rendered the full exercise (audio player, "Answer questions" CTA, transcript-
lock message) because `AiGeneratedContentJson` was one flat exercise-shaped
payload, forwarded unchanged to both Learn and Practice by
`LegacyListeningPresenter`.

The full fix requires every generated activity to carry three sections —
`learnContent`, `practiceContent`, `feedbackPlan` — under a
`schemaVersion: "module_stage_v1"` envelope, with the Learn page reading
**only** `learnContent`. This is a 7-phase, cross-stack migration (10
generation prompts, 12 evaluation prompts, 10 exercise patterns, 5 presenters,
8 renderers, 3 test layers). It cannot land in one PR.

## PR1 — Listening reference implementation (this sprint, completed)

PR1 built the shared schema/validator/adapter infrastructure reusable by all
future activity types, and shipped it end-to-end for `ListeningComprehension`.

Completed work:

1. `ModuleStageContent.cs` — shared DTOs (`LearnContentDto`, `PracticeContentDto`,
   `FeedbackPlanDto`, `StageContentDto`, `ModuleStageWireDto`).
2. `ModuleStageContentValidator` — schema/forbidden-key/required-key validation.
3. `ActivityDto.StageContent` (additive, nullable).
4. Retry-once-then-fail validation in `AiActivityGeneratorHandler` for
   `ListeningComprehension`, replacing `ValidateListeningActivityJson`.
5. `activity_generate_listening` prompt rewritten to produce `module_stage_v1`.
6. `ActivityGetHandler.BuildStageContent` / `AdaptLegacyListening` — staged
   content for new rows, `legacy_adapted_v1` adapter for old rows.
7. `ListeningComprehensionEvaluator.ExtractExerciseDataJson` — unwraps staged
   `exerciseData` before scoring; scoring logic unchanged.
8. Frontend: `StageContentDto` + related TS types, `ActivityDto.stageContent`.
9. `LegacyListeningPresenter.teachContent(activity)` — `stagedLearning` view
   model when `stageContent` present, legacy fallback otherwise.
10. `activity-teach-page` — new `@case ('stagedLearning')`, old
    `listeningLearning` case marked deprecated.
11. `activity-practice-page` — listening case reads
    `stageContent.practice.{scenario,exerciseData}` via `listeningExerciseData` getter.
12. Backend tests: `ModuleStageContentValidatorTests`,
    `ActivityGetHandlerStageContentTests`, `ListeningComprehensionEvaluatorTests`,
    `ActivityStageContentTests` (integration).
13. **Found during testing**: `ActivityController.ToActivityResponse` did not
    map `StageContent` to the API response at all — fixed by adding the full
    `stageContent` mapping (schemaVersion/learn/practice/feedbackPlan).
14. Frontend tests: `legacy-listening.presenter.spec.ts` rewritten for the new
    `teachContent(activity)` signature (staged + fallback cases);
    `e2e/listening-comprehension-activity.spec.ts` updated — Learn page shows
    teaching content only (no audio/questions/transcript-lock), Practice page
    shows the full exercise after "Start practice".
15. Architecture doc updated (`docs/architecture/learning-activity-engine.md`,
    "Staged Activity Content (`module_stage_v1`)" section).

Verification: 501+ backend unit tests pass, 2/2 new integration tests pass,
84/84 Angular unit tests pass, `dotnet build` clean.

## Follow-up backlog — apply the PR1 recipe per type/pattern

Each row follows the same recipe: new/updated generation prompt producing
`module_stage_v1`, `RequiredPracticeKeysByType` entry (if applicable), an
`AdaptLegacy*` branch in `ActivityGetHandler.BuildStageContent`, an evaluator
unwrap step, a `stagedLearning` presenter case, and tests (validator + adapter
+ evaluator + presenter + e2e).

| Activity type / pattern | Generation prompt key | Evaluation path | Presenter |
|---|---|---|---|
| WritingScenario | `activity_generate_writing` | `activity_evaluate_writing` (`AiActivityGeneratorHandler.EvaluateAttemptAsync`) | `LegacyWritingPresenter` |
| SpeakingRolePlay | `activity_generate_speaking_roleplay` | `activity_evaluate_speaking_roleplay` | `LegacySpeakingPresenter` |
| VocabularyPractice | (deterministic/seeded) | `NoMarkingEvaluator` / pattern eval | `LegacyVocabPresenter` |
| phrase_match | `activity_generate_phrase_match` | `KeyedSelectionEvaluator` | `PatternBackedPresenter` |
| gap_fill_workplace_phrase | `activity_generate_gap_fill_workplace_phrase` | `ExactMatchEvaluator` | `PatternBackedPresenter` |
| listen_and_answer | `activity_generate_listen_and_answer` | `AiStructuredEvaluator` (`activity_evaluate_listen_and_answer`) | `PatternBackedPresenter` |
| listen_and_gap_fill | `activity_generate_listen_and_gap_fill` | `ExactMatchEvaluator` | `PatternBackedPresenter` |
| email_reply | `activity_generate_email_reply` | `AiStructuredEvaluator` (`activity_evaluate_email_reply`) | `PatternBackedPresenter` |
| teams_chat_simulation | `activity_generate_teams_chat_simulation` | `AiStructuredEvaluator` (`activity_evaluate_teams_chat_simulation`) | `PatternBackedPresenter` |
| spoken_response_from_prompt | `activity_generate_spoken_response_from_prompt` | `AiOpenEndedEvaluator` | `PatternBackedPresenter` |
| open_writing_task | `activity_generate_open_writing_task` | `AiOpenEndedEvaluator` (`activity_evaluate_open_writing_task`) | `PatternBackedPresenter` |
| speaking_roleplay_turn | `activity_generate_speaking_roleplay_turn` | `AiOpenEndedEvaluator` (`activity_evaluate_speaking_roleplay_turn`) | `PatternBackedPresenter` |
| lesson_reflection | `activity_generate_lesson_reflection` | `NoMarkingEvaluator` | `PatternBackedPresenter` |

## Required follow-up architecture (separate workstreams)

PR1 (staged schema + Listening reference + compatibility adapter) is **not**
the final architecture. These are required future PR/sprints:

- **Stage-aware generation for all remaining types/patterns** — apply the PR1
  recipe (table above) to each remaining `ActivityType`/`ExercisePattern`.
- **Practice Gym pre-generation pool** — replace per-click
  `/activity?type=...` generation with: (1) look up a ready `module_stage_v1`
  record in a pool/cache for the requested skill/type, (2) open it immediately
  if found, (3) if not found, show a "preparing practice" state or fall back
  to on-demand generation, (4) background job replenishes the pool.
  `/activity?type=...` direct generation is **not** the desired long-term Gym
  flow — it remains only as the fallback path.
- **Today lesson/module pre-generation** — Today's session's `LearningActivity`
  rows should be prepared by a background job ahead of the student opening
  "Today's Lesson," each already a `module_stage_v1` record; "Start module"
  should not normally trigger synchronous AI generation, only fallback for
  missing modules.
- **MinIO/object-storage asset lifecycle for audio** — TTS generation happens
  in a background job; resulting audio file is uploaded to object storage;
  `practiceContent.exerciseData.audioAssetUrl` (reserved as an optional field
  in PR1's schema) is populated with the stored URL before the student reaches
  Practice. The Practice page consumes this stored asset rather than
  triggering generation.
- **Background job replenishment** — scheduled/triggered jobs that keep both
  the Today pipeline and the Gym pool stocked with valid `module_stage_v1`
  records (text + audio assets where applicable).

### Acceptance criteria for follow-up architecture

- Staged schema (PR1) is compatible with pre-generated modules — confirmed by
  the optional `audioAssetUrl` pass-through field.
- Practice Gym cache/pool stores staged (`module_stage_v1`) content, not flat
  exercises.
- Today modules are prepared ahead of use by background jobs.
- Audio assets are generated and stored (MinIO) before Practice when possible.
- Student click path consumes ready content first; on-demand generation is
  fallback only.
- Docs clearly state `/activity?type=...` direct generation is not the
  desired long-term Gym/Today flow.
