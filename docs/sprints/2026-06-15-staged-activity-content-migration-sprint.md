---
status: in-progress
lastUpdated: 2026-06-15 13:10
owner: architecture
relatedArchitecture: docs/architecture/learning-activity-engine.md#staged-activity-content-module_stage_v1
relatedReview: docs/reviews/2026-06-15-learn-practice-feedback-structure-investigation.md
---
---

status: in-progress
lastUpdated: 2026-06-15
owner: architecture
relatedArchitecture: docs/architecture/learning-activity-engine.md#staged-activity-content-module_stage_v1
relatedReview: docs/reviews/2026-06-15-learn-practice-feedback-structure-investigation.md
-----------------------------------------------------------------------------------------

# Staged Activity Content Migration Sprint

## Background

`docs/reviews/2026-06-15-learn-practice-feedback-structure-investigation.md`
confirmed the Learn step for Practice Gym / Today Listening activities
rendered the full exercise: audio player, "Answer questions" CTA, transcript-lock
message. This happened because `AiGeneratedContentJson` was one flat
exercise-shaped payload, forwarded unchanged to both Learn and Practice by
`LegacyListeningPresenter`.

The full fix requires every generated activity/module to carry three sections:

* `learnContent`
* `practiceContent`
* `feedbackPlan`

These sections live under a `schemaVersion: "module_stage_v1"` envelope, with the
Learn page reading **only** `learnContent`.

This is a 7-phase, cross-stack migration:

* generation prompts
* evaluation prompts
* exercise patterns
* presenters
* renderers
* backend DTO/API mapping
* frontend tests
* Playwright tests

It cannot land in one PR.

## Product model clarification — skills and exercise types are different

The staged model must not assume that one skill equals one fixed activity type.

Wrong assumption:

```text
Listening = ListeningComprehension
Writing = WritingScenario
Speaking = SpeakingRolePlay
```

Correct model:

```text
A Learning Module targets one or more skills.
The Practice stage uses one supported exercise type.
```

Examples:

| Exercise type                            | Skills tested       |
| ---------------------------------------- | ------------------- |
| Summarize Spoken Text                    | Listening + Writing |
| Multiple Choice, Choose Multiple Answers | Listening           |
| Fill in the Blanks from Audio            | Listening           |
| Highlight Correct Summary                | Listening + Reading |
| Multiple Choice, Choose Single Answer    | Listening           |
| Select Missing Word                      | Listening           |
| Highlight Incorrect Words                | Listening + Reading |
| Write from Dictation                     | Listening + Writing |

Therefore every staged learning module should support:

* `primarySkill`
* `secondarySkills`
* `exerciseType`
* `moduleGoal`
* `learnContent`
* `practiceContent`
* `feedbackPlan`

Example staged metadata:

```json
{
  "schemaVersion": "module_stage_v1",
  "title": "Summarize a spoken workplace update",
  "moduleGoal": "Listen for the main message and write a concise summary.",
  "primarySkill": "listening",
  "secondarySkills": ["writing"],
  "exerciseType": "summarize_spoken_text",
  "learnContent": {},
  "practiceContent": {},
  "feedbackPlan": {}
}
```

Important rules:

* `primarySkill` describes the main skill being developed.
* `secondarySkills` describes additional skills involved in the task.
* `exerciseType` selects the Practice renderer and evaluator.
* The renderer must be chosen by `exerciseType`, not only by skill.
* The evaluator must understand `primarySkill`, `secondarySkills`, `exerciseType`,
  and `feedbackPlan`.

When a student selects a skill in Practice Gym, such as Listening, the system may
serve any ready module where `primarySkill = listening`.

Examples:

* `summarize_spoken_text`
* `listening_multiple_choice_multi`
* `listening_fill_in_blanks`
* `highlight_correct_summary`
* `listening_multiple_choice_single`
* `select_missing_word`
* `highlight_incorrect_words`
* `write_from_dictation`

When a student selects a specific exercise type, the system should serve that exact
exercise type.

## Current staged schema direction

The staged schema should evolve toward this shape:

```json
{
  "schemaVersion": "module_stage_v1",
  "title": "...",
  "moduleGoal": "...",
  "primarySkill": "listening",
  "secondarySkills": ["writing"],
  "exerciseType": "summarize_spoken_text",
  "learnContent": {
    "teachingTitle": "...",
    "explanation": "...",
    "keyPoints": ["..."],
    "examples": [
      {
        "phrase": "...",
        "meaning": "...",
        "note": "..."
      }
    ],
    "strategy": "...",
    "commonMistakes": ["..."],
    "sourceLanguageSupport": "..."
  },
  "practiceContent": {
    "instructions": "...",
    "scenario": "...",
    "task": "...",
    "exerciseData": {}
  },
  "feedbackPlan": {
    "evaluationCriteria": ["..."],
    "rubric": [],
    "feedbackFocus": "...",
    "successCriteria": ["..."]
  }
}
```

`skillFocus` may remain temporarily for backward compatibility, but new staged
content should prefer `primarySkill` and `secondarySkills`.

## PR1 — Listening reference implementation, completed

PR1 built the shared schema/validator/adapter infrastructure reusable by all
future activity types, and shipped it end-to-end for `ListeningComprehension`.

Completed work:

1. `ModuleStageContent.cs` — shared DTOs:

   * `LearnContentDto`
   * `PracticeContentDto`
   * `FeedbackPlanDto`
   * `StageContentDto`
   * `ModuleStageWireDto`
2. `ModuleStageContentValidator` — schema, forbidden-key, and required-key validation.
3. `ActivityDto.StageContent` — additive, nullable.
4. Retry-once-then-fail validation in `AiActivityGeneratorHandler` for
   `ListeningComprehension`, replacing `ValidateListeningActivityJson`.
5. `activity_generate_listening` prompt rewritten to produce `module_stage_v1`.
6. `ActivityGetHandler.BuildStageContent` / `AdaptLegacyListening` — staged content
   for new rows, `legacy_adapted_v1` adapter for old rows.
7. `ListeningComprehensionEvaluator.ExtractExerciseDataJson` — unwraps staged
   `exerciseData` before scoring; scoring logic unchanged.
8. Frontend:

   * `StageContentDto`
   * related TypeScript types
   * `ActivityDto.stageContent`
9. `LegacyListeningPresenter.teachContent(activity)` — `stagedLearning` view model
   when `stageContent` is present, legacy fallback otherwise.
10. `activity-teach-page` — new `@case ('stagedLearning')`; old
    `listeningLearning` case marked deprecated.
11. `activity-practice-page` — listening case reads
    `stageContent.practice.{scenario,exerciseData}` through
    `listeningExerciseData` getter.
12. Backend tests:

    * `ModuleStageContentValidatorTests`
    * `ActivityGetHandlerStageContentTests`
    * `ListeningComprehensionEvaluatorTests`
    * `ActivityStageContentTests` integration coverage
13. Found during testing: `ActivityController.ToActivityResponse` did not map
    `StageContent` to the API response. Fixed by adding full `stageContent` mapping:

    * `schemaVersion`
    * `learn`
    * `practice`
    * `feedbackPlan`
14. Frontend tests:

    * `legacy-listening.presenter.spec.ts` rewritten for new `teachContent(activity)`
      signature
    * staged and fallback cases covered
    * `e2e/listening-comprehension-activity.spec.ts` updated
    * Learn page shows teaching content only
    * Learn page does not show audio/questions/transcript-lock
    * Practice page shows full exercise after "Start practice"
15. Architecture doc updated:

    * `docs/architecture/learning-activity-engine.md`
    * new "Staged Activity Content (`module_stage_v1`)" section

Verification:

* 501+ backend unit tests pass
* 2/2 new integration tests pass
* 84/84 Angular unit tests pass
* `dotnet build` clean
* Angular build clean

## Follow-up backlog — apply the PR1 recipe per type/pattern

Each row follows the same recipe:

* new/updated generation prompt producing `module_stage_v1`
* `RequiredPracticeKeysByType` entry where applicable
* `AdaptLegacy*` branch in `ActivityGetHandler.BuildStageContent`
* evaluator unwrap step
* `stagedLearning` presenter case
* tests:

  * validator
  * adapter
  * evaluator
  * presenter
  * e2e

| Activity type / pattern     |        Primary skill |                       Secondary skills | Generation prompt key                           | Evaluation path                                                                 | Presenter                 |
| --------------------------- | -------------------: | -------------------------------------: | ----------------------------------------------- | ------------------------------------------------------------------------------- | ------------------------- |
| WritingScenario             |              Writing |                   Grammar / Vocabulary | `activity_generate_writing`                     | `activity_evaluate_writing` (`AiActivityGeneratorHandler.EvaluateAttemptAsync`) | `LegacyWritingPresenter`  |
| SpeakingRolePlay            |             Speaking |                 Listening / Vocabulary | `activity_generate_speaking_roleplay`           | `activity_evaluate_speaking_roleplay`                                           | `LegacySpeakingPresenter` |
| VocabularyPractice          |           Vocabulary |                      Reading / Writing | deterministic/seeded                            | `NoMarkingEvaluator` / pattern eval                                             | `LegacyVocabPresenter`    |
| phrase_match                |           Vocabulary |                                Reading | `activity_generate_phrase_match`                | `KeyedSelectionEvaluator`                                                       | `PatternBackedPresenter`  |
| gap_fill_workplace_phrase   | Grammar / Vocabulary |                                Reading | `activity_generate_gap_fill_workplace_phrase`   | `ExactMatchEvaluator`                                                           | `PatternBackedPresenter`  |
| listen_and_answer           |            Listening |                                Writing | `activity_generate_listen_and_answer`           | `AiStructuredEvaluator` (`activity_evaluate_listen_and_answer`)                 | `PatternBackedPresenter`  |
| listen_and_gap_fill         |            Listening |                                Reading | `activity_generate_listen_and_gap_fill`         | `ExactMatchEvaluator`                                                           | `PatternBackedPresenter`  |
| email_reply                 |              Writing |                   Reading / Vocabulary | `activity_generate_email_reply`                 | `AiStructuredEvaluator` (`activity_evaluate_email_reply`)                       | `PatternBackedPresenter`  |
| teams_chat_simulation       |              Writing | Reading / Speaking-style communication | `activity_generate_teams_chat_simulation`       | `AiStructuredEvaluator` (`activity_evaluate_teams_chat_simulation`)             | `PatternBackedPresenter`  |
| spoken_response_from_prompt |             Speaking |                              Listening | `activity_generate_spoken_response_from_prompt` | `AiOpenEndedEvaluator`                                                          | `PatternBackedPresenter`  |
| open_writing_task           |              Writing |                   Grammar / Vocabulary | `activity_generate_open_writing_task`           | `AiOpenEndedEvaluator` (`activity_evaluate_open_writing_task`)                  | `PatternBackedPresenter`  |
| speaking_roleplay_turn      |             Speaking |                              Listening | `activity_generate_speaking_roleplay_turn`      | `AiOpenEndedEvaluator` (`activity_evaluate_speaking_roleplay_turn`)             | `PatternBackedPresenter`  |
| lesson_reflection           |  Reflection / Review |                                Writing | `activity_generate_lesson_reflection`           | `NoMarkingEvaluator`                                                            | `PatternBackedPresenter`  |

## New listening-related exercise backlog

These exercise types should be added as first-class `exerciseType` values over
future PRs. They should not be forced into one generic `ListeningComprehension`
shape.

| New exercise type                  | Primary skill |  Secondary skills | Practice behaviour                                                       | Evaluator requirement                                                             |
| ---------------------------------- | ------------: | ----------------: | ------------------------------------------------------------------------ | --------------------------------------------------------------------------------- |
| `summarize_spoken_text`            |     Listening |           Writing | Listen to 60–90 second audio and write 50–70 word summary in 10 minutes  | Evaluate main idea, key details, summary accuracy, structure, grammar, word count |
| `listening_multiple_choice_multi`  |     Listening |           Reading | Listen to 40–90 second audio and select multiple correct options         | Evaluate all correct selections, missed correct answers, false positives          |
| `listening_fill_in_blanks`         |     Listening | Reading / Writing | Read transcript with missing words and type words while listening        | Exact/near-exact word matching, spelling tolerance if configured                  |
| `highlight_correct_summary`        |     Listening |           Reading | Listen to audio and select paragraph that best summarizes it             | Evaluate selected summary and distractor reasoning                                |
| `listening_multiple_choice_single` |     Listening |           Reading | Listen to short recording and choose one correct answer                  | Evaluate single selected answer                                                   |
| `select_missing_word`              |     Listening |           Reading | Listen to audio where final word/phrase is beeped and select best ending | Evaluate selected missing phrase                                                  |
| `highlight_incorrect_words`        |     Listening |           Reading | Listen while reading transcript and click words that differ from audio   | Evaluate correct clicks, missed differences, false positives                      |
| `write_from_dictation`             |     Listening |           Writing | Listen to a short sentence and type it exactly                           | Evaluate exact wording, spelling, punctuation/capitalisation rules                |

Implementation note:

These should be implemented as exercise patterns or equivalent renderer/evaluator
pairs. They should use the same `module_stage_v1` contract:

* Learn teaches the strategy.
* Practice renders the task.
* Feedback evaluates the task using exercise-specific rules.

## Practice Gym behaviour target

Practice Gym should support two selection modes.

### By skill

Examples:

* Listening
* Speaking
* Writing
* Reading
* Vocabulary
* Grammar

When a student selects a skill, the app should serve a ready staged module where:

```text
primarySkill = selected skill
```

The exercise type may vary.

Example:

```text
Student selects Listening
→ app may serve summarize_spoken_text
→ app may serve highlight_incorrect_words
→ app may serve write_from_dictation
→ app may serve listening_multiple_choice_single
```

### By exercise type

Examples:

* Summarize Spoken Text
* Fill in the Blanks
* Highlight Incorrect Words
* Email Reply
* Workplace Chat
* Sentence Transformation

When a student selects a specific exercise type, the app should serve that exact
exercise type.

## Today lesson behaviour target

Today’s Lesson contains several Learning Modules.

Each module has:

* title
* module goal
* primary skill
* secondary skills
* exercise type
* estimated duration
* Learn
* Practice
* Feedback

The Today generator should not only create a list of raw exercises. It should create
a balanced set of staged modules based on:

* student level
* weak skills
* recent attempts
* desired study time
* variety across exercise types
* skill balance
* spaced repetition needs

Example Today lesson:

```text
Today’s Lesson — 30 minutes

Module 1
- Primary skill: Vocabulary
- Exercise type: phrase_match
- Learn → Practice → Feedback

Module 2
- Primary skill: Listening
- Secondary skill: Writing
- Exercise type: summarize_spoken_text
- Learn → Practice → Feedback

Module 3
- Primary skill: Writing
- Secondary skill: Grammar
- Exercise type: email_reply
- Learn → Practice → Feedback
```

## Required follow-up architecture — separate workstreams

PR1, staged schema + Listening reference + compatibility adapter, is **not** the
final architecture. These are required future PR/sprints.

### 1. Stage-aware generation for all remaining types/patterns

Apply the PR1 recipe to each remaining `ActivityType` and `ExercisePattern`.

Each migrated type must produce:

* `primarySkill`
* `secondarySkills`
* `exerciseType`
* `learnContent`
* `practiceContent`
* `feedbackPlan`

### 2. Skill-to-exercise selection model

Add a selection layer that maps a requested skill to eligible exercise types.

Example:

```text
Listening →
- summarize_spoken_text
- listen_and_answer
- listen_and_gap_fill
- listening_multiple_choice_multi
- listening_multiple_choice_single
- select_missing_word
- highlight_correct_summary
- highlight_incorrect_words
- write_from_dictation
```

This should be used by:

* Practice Gym
* Today lesson generation
* background pre-generation jobs
* adaptive curriculum planning

### 3. Practice Gym pre-generation pool

Replace per-click `/activity?type=...` generation with:

1. Look up a ready `module_stage_v1` record in a pool/cache for the requested
   skill or exercise type.
2. Open it immediately if found.
3. If not found, show a "preparing practice" state or fall back to on-demand generation.
4. Background job replenishes the pool.

`/activity?type=...` direct generation is **not** the desired long-term Gym flow.
It remains only as the fallback path.

The Practice Gym cache/pool must store staged modules, not flat exercises.

### 4. Today lesson/module pre-generation

Today’s session `LearningActivity` rows should be prepared by a background job ahead
of the student opening "Today’s Lesson."

Each prepared row should already be a `module_stage_v1` record.

"Start module" should not normally trigger synchronous AI generation. It should only
fallback to generation for missing modules.

### 5. MinIO/object-storage asset lifecycle for audio

TTS generation should happen in a background job.

The resulting audio file should be uploaded to MinIO or the configured object storage.

For audio-based exercise types, `practiceContent.exerciseData` may include:

```json
{
  "audioAssetUrl": "...",
  "audioContentType": "audio/mpeg",
  "audioDurationSeconds": 73
}
```

The Practice page should consume the stored asset rather than triggering generation.

Audio asset lifecycle must cover:

* generation
* upload
* retrieval
* expiry/signing if needed
* retry on failed generation
* fallback if asset is missing
* cleanup policy

### 6. Background job replenishment

Scheduled or triggered jobs must keep both the Today pipeline and the Practice Gym
pool stocked with valid `module_stage_v1` records.

Jobs should generate:

* text content
* practice data
* feedback plan
* audio assets where applicable
* metadata:

  * primary skill
  * secondary skills
  * exercise type
  * level
  * estimated duration
  * source: Today or Practice Gym
  * readiness status

### 7. Renderer and evaluator registry

Renderer and evaluator lookup should be based on `exerciseType`.

Do not rely only on old `ActivityType`.

Example:

```text
exerciseType = summarize_spoken_text
→ renderer = SummarizeSpokenTextRenderer
→ evaluator = SummarizeSpokenTextEvaluator
```

This registry should support multi-skill exercises.

### 8. Analytics and progress model

Progress should track more than one skill per module.

A single attempt may improve several skills.

Example:

```text
summarize_spoken_text
→ Listening score
→ Writing score
→ Overall module score
```

Future progress records should support:

* primary skill score
* secondary skill score
* exercise type
* module goal
* attempt score
* feedback viewed
* retry count

## PR — Practice Gym pre-generation pool foundation (Phase 4), completed

Implements item 3 of "Required follow-up architecture" above, scoped as a
foundation only (the acceptance criteria below remain the long-term target).

Completed work:

1. `PracticeCacheStatus.Failed` added; `PracticeActivityCache.MarkFailed()`.
2. `IPracticeGymPoolService` (`FindReadyForExerciseTypeAsync`,
   `FindReadyForSkillAsync`, `MarkConsumedAsync`, `MarkFailedAsync`) and
   `PracticeGymPoolService` implementation, reusing `PracticeActivityCache` —
   see `docs/architecture/learning-activity-engine.md#practice-gym-pre-generation-pool-foundation`.
3. `GET /api/activity/practice-gym/next?skill=<skill>|exerciseType=<key>` —
   pool-first, on-demand-fallback endpoint returning `source: pool|onDemandFallback`.
4. Frontend `practice-gym.component.ts` skill-card click flow rewritten to call
   `getPracticeGymNext` and route to `/activity?activityId=<id>&returnTo=/practice`.
5. Tests:
   * `practice-gym.component.spec.ts` — pool source, on-demand fallback source,
     no-eligible-result, and request-failure cases.
   * `PracticeGymNextEndpointTests` (integration) — pool hit by exact type and
     by skill, on-demand fallback, disabled type, planned type, empty
     request, unauthenticated, plus regression checks for
     `/api/activity/next?exerciseType=`, `?pattern=`, `?type=`.
   * `e2e/practice-gym.spec.ts` — updated mocks for `practice-gym/next`,
     skill cards now assert `/activity?activityId=...` routing.

Out of scope for this PR (unchanged): Today pre-generation, MinIO/audio asset
lifecycle, new planned future exercise renderers/evaluators, background pool-fill job changes.

## Additional fix landed alongside this PR — T44 migration Designer.cs

Unrelated production bug fixed in the same change per explicit instruction:
see `docs/reviews/2026-06-15-t44-exercise-type-catalog-migration-fix.md`. The
`T44_ExerciseTypeCatalog` migration was missing its `*.Designer.cs`, so
`exercise_type_definitions` was never created by `Database.Migrate()` and
`ExerciseTypeDefinitionSeeder.SeedAsync` crashed the API container on startup.
Regenerated the migration pair; verified via integration tests that run
`Database.Migrate()`.

## Phase 5 — SpeakingRolePlay staged migration, completed

Migrates `SpeakingRolePlay` to `module_stage_v1`. Follows the same PR1/PR2 recipe.

Completed work:

1. `DefaultAiSeeder.ActivityGenerateSpeakingRolePlayContent` rewritten to produce
   `module_stage_v1` JSON with `primarySkill`, `secondarySkills`, `exerciseType`,
   `learnContent`, `practiceContent`, `feedbackPlan`. Prompt key unchanged.
   Token limit raised from 900 to 1600 input / 1200 output for the larger staged prompt.
2. `DefaultAiSeeder.ActivityEvaluateSpeakingRolePlayContent` updated to evaluate
   against `practiceContent.exerciseData` (role, partnerRole, situation, prompt,
   successChecklist) and `feedbackPlan.rubric` when staged; legacy flat fallback retained.
3. `ModuleStageContentValidator.RequiredPracticeKeysByType` — added
   `SpeakingRolePlay` entry: `["prompt", "role", "partnerRole", "situation"]`.
4. `ModuleStageContentValidator.ForbiddenLearnContentKeys` — extended with
   speaking-specific forbidden keys: `recordingControls`, `microphoneInstructions`,
   `startRecording`, `stopRecording`.
5. `AiActivityGeneratorHandler.GenerateActivityContentAsync` — `SpeakingRolePlay`
   now routes through the same `ValidateStagedContent` / retry-once path used by
   Listening and Writing. Removed the old `ValidateSpeakingRolePlayJson` flat-check.
6. `AiActivityGeneratorHandler.EvaluateAttemptAsync` — added
   `BuildSpeakingEvaluationContent` helper; passes staged `practiceContent`,
   `feedbackPlan`, `learnContent` to the evaluator (same pattern as Writing).
7. `SpeakingRolePlayEvaluator.ExtractExerciseDataJson` — new public static method
   that unwraps `practiceContent.exerciseData` from staged JSON; falls back to
   full JSON for legacy flat content.
8. `ActivityGetHandler.MapToDto` — SpeakingRolePlay branch now calls
   `BuildStageContent` and includes `StageContent` in the DTO. Legacy flat fields
   (`SpeakingScenario`, `StudentRole`, etc.) still populated from exerciseData or
   root-level JSON for backward compatibility.
9. `ActivityGetHandler.AdaptLegacySpeaking` — new legacy adapter for old flat
   SpeakingRolePlay JSON; maps to `legacy_adapted_v1` with generic learnContent
   and preserves all roleplay fields in `practiceContent.exerciseData`.
10. `ActivityGetHandler.LooksLikeLegacySpeaking` — detection predicate for old
    speaking JSON shape.
11. `ActivityGetHandler.SpeakingContent` — extended with staged exerciseData fields
    (`Role`, `PartnerRole`, `Situation`, `Tone`, `RequiredPhrases`, `SuccessChecklist`).
12. `LegacySpeakingPresenter.teachContent(activity)` — returns `stagedLearning`
    view model when `stageContent.learn` is present; falls back to `speakingScenario`
    for legacy activities.
13. `ActivityTestFactory.FakeAiProvider` — added `role` and `partnerRole` to the
    shared fake `exerciseData` so the staged validator passes for SpeakingRolePlay.
14. Backend tests added:
    * `ModuleStageContentValidatorTests` — valid staged speaking payload, missing
      top-level sections, missing required practice keys (prompt/role/partnerRole/
      situation), forbidden speaking keys in learnContent.
    * `ActivityGetHandlerStageContentTests` — legacy flat speaking maps to
      `legacy_adapted_v1`, learnContent clean, staged speaking maps fields correctly.
15. Frontend tests updated:
    * `legacy-speaking.presenter.spec.ts` — staged path returns `stagedLearning`,
      learn VM comes from `stageContent.learn`, no recording controls in staged
      learn block, legacy path still returns `speakingScenario`.

Verification:
* `dotnet build` clean (0 errors, 6 pre-existing warnings)
* 520/520 backend unit tests pass (+15 new)
* 472/472 integration tests pass
* 96/96 Angular unit tests pass (+4 new)
* Angular production build clean

Out of scope for this PR (unchanged): Practice Gym pre-generation changes,
Today pre-generation, MinIO/audio lifecycle, new planned future exercise renderers/evaluators
(`read_aloud`, `repeat_sentence`, `describe_image`, etc. remain planned/non-runnable),
Pattern-backed exercise migration.

## Acceptance criteria for follow-up architecture

* Staged schema is compatible with pre-generated modules.
* Staged schema supports `primarySkill`, `secondarySkills`, and `exerciseType`.
* Practice Gym cache/pool stores staged `module_stage_v1` modules, not flat exercises.
* Practice Gym skill selection can serve varied exercise types.
* Today modules are prepared ahead of use by background jobs.
* Today lesson generation can include multi-skill modules.
* Audio assets are generated and stored in MinIO/object storage before Practice when possible.
* Student click path consumes ready content first.
* On-demand generation is fallback only.
* Renderer selection is based on `exerciseType`.
* Evaluators understand multi-skill exercises.
* Docs clearly state `/activity?type=...` direct generation is not the desired long-term Gym/Today flow.

## Phase 6 — VocabularyPractice staged migration, completed

`VocabularyPractice` now uses `module_stage_v1` for newly generated deterministic vocabulary activities. The migration keeps the existing seeded vocabulary source. It does not add broad AI vocabulary generation.

The staged vocabulary module has exactly three pages: Learn, Practice, and Feedback. Learn teaches vocabulary meaning, usage, word form, example context, memory strategy, and common mistakes. Practice contains the fill-blank vocabulary task through `practiceContent.exerciseData`. Feedback uses the existing deterministic vocabulary evaluator with staged `practiceContent.exerciseData` support and legacy flat JSON fallback.

Completed staged migrations:

- `ListeningComprehension`
- `WritingScenario`
- `SpeakingRolePlay`
- `VocabularyPractice`

Remaining staged migrations are pattern-backed activities. Planned future exercise formats remain planned and non-runnable unless implemented end-to-end. Today pre-generation remains a future phase. MinIO/audio lifecycle remains a future phase. No new planned future exercise renderer or evaluator was implemented in Phase 6.

## Phase 7A — Pattern-backed staged migration foundation, completed

Migrates the first two deterministic/local pattern-backed activities to `module_stage_v1`:
`phrase_match` and `gap_fill_workplace_phrase`.

Both patterns are AI-generated but deterministically evaluated (no AI call at evaluation time).

### Completed work

1. **`DefaultAiSeeder.ActivityGeneratePhraseMatchContent`** rewritten to produce `module_stage_v1` JSON with `primarySkill: vocabulary`, `secondarySkills: ["reading"]`, `exerciseType: phrase_match`. Learn stage teaches phrase meanings without listing the matching pairs as a task. Practice stage contains `exerciseData.pairs` in the existing `phrase`/`meaning` shape. Prompt key unchanged: `activity_generate_phrase_match`.

2. **`DefaultAiSeeder.ActivityGenerateGapFillContent`** rewritten to produce `module_stage_v1` JSON with `primarySkill: vocabulary`, `secondarySkills: ["reading"]`, `exerciseType: gap_fill_workplace_phrase`. Learn stage teaches the vocabulary/grammar concept without including the gap-fill sentences. Practice stage contains `exerciseData.items` in the existing `sentence`/`answer`/`distractors`/`hint` shape. Prompt key unchanged: `activity_generate_gap_fill_workplace_phrase`.

3. **`ModuleStageContentValidator`** — added `RequiredPracticeKeysByPatternKey` dict (keyed by exercise pattern key, takes precedence over `RequiredPracticeKeysByType`). Entries: `phrase_match` requires `pairs`; `gap_fill_workplace_phrase` requires `items`. `Validate()` now accepts optional `exercisePatternKey` parameter.

4. **`AiActivityGeneratorHandler`** — added `StagedPatternKeys` set (`phrase_match`, `gap_fill_workplace_phrase`). Pattern-driven `VocabularyPractice` activities whose `ExercisePatternKey` is in `StagedPatternKeys` now route through the same `ValidateStagedContent` / retry-once path used by Listening, Writing, and Speaking. `ValidateStagedContent` updated to accept optional `exercisePatternKey` and passes it to the validator.

5. **`KeyedSelectionEvaluator`** — added `UnwrapStagedContent` helper: detects `schemaVersion: "module_stage_v1"`, extracts `practiceContent.exerciseData`, returns raw JSON for evaluation. `ParseExpectedPairs` uses the unwrapped JSON. Legacy flat `phrase_match` content still evaluates unchanged.

6. **`ExactMatchEvaluator`** — added `UnwrapStagedContent` helper (same pattern). `ParseExpectedItems` uses the unwrapped JSON for both `listen_and_gap_fill` and `gap_fill_workplace_phrase`. Legacy flat content still evaluates unchanged.

7. **`PatternBackedPresenter`** — `teachContent` returns `stagedLearning` view model when `activity.stageContent?.learn` is present. Falls back to `patternLearning` block for legacy flat activities. `practiceContent` unchanged (returns `exerciseRenderer` block for both staged and legacy).

8. **Tests added:**
   - `ModuleStageContentValidatorTests`: valid staged `phrase_match` passes; valid staged `gap_fill_workplace_phrase` passes; missing `pairs` fails; missing `items` fails; forbidden keys in learnContent (`pairs`, `gaps`, `answerKey`, `selectedAnswers`) fail.
   - `KeyedSelectionEvaluatorTests`: staged `phrase_match` all-correct returns full score; staged all-wrong still completes; legacy flat format still evaluates correctly.
   - `ExactMatchEvaluatorTests`: staged `gap_fill_workplace_phrase` all-correct returns full score; staged partial returns partial score; legacy flat format still evaluates correctly.
   - `pattern-backed.presenter.spec.ts`: staged path returns `stagedLearning` block; `learn` VM comes from `stageContent.learn`; no answer controls in staged learn block; legacy fallback still returns `patternLearning`; practice block is `exerciseRenderer` for both patterns.

### Verification

- `dotnet build` clean (0 errors)
- 546/546 backend unit tests pass
- 16/16 presenter unit tests pass (7 new)
- Angular dev build clean

### Still out of scope

- Remaining pattern-backed activities: `listen_and_answer`, `listen_and_gap_fill`, `email_reply`, `teams_chat_simulation`, `spoken_response_from_prompt`, `open_writing_task`, `speaking_roleplay_turn`, `lesson_reflection`
- Today pre-generation
- MinIO/audio lifecycle
- Planned future exercise format renderers/evaluators remain planned and non-runnable

---

## Phase 7B — migrate `listen_and_answer` and `listen_and_gap_fill` to `module_stage_v1`

**Date:** 2026-06-15
**Commit:** pending
**Status:** complete

### Goals

Migrate the two listening pattern-backed activities to `module_stage_v1`. Same recipe as Phase 7A. No MinIO/audio lifecycle changes, no Today pre-generation, no other patterns.

### Files changed

1. **`src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`**
   - Added `listen_and_answer` → `["audioScript", "questions"]` to `RequiredPracticeKeysByPatternKey`.
   - Added `listen_and_gap_fill` → `["audioScript", "gaps"]` to `RequiredPracticeKeysByPatternKey`.
   - Pattern-key lookup takes precedence over `ActivityType.ListeningComprehension` (which requires `questions`) — this prevents `listen_and_gap_fill` being incorrectly validated against the `questions` requirement.

2. **`src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`**
   - Added `listen_and_answer` and `listen_and_gap_fill` to `StagedPatternKeys`.
   - `ListeningComprehension/WritingScenario/SpeakingRolePlay` case now passes `context.ExercisePatternKey` to `ValidateStagedContent` (and retry) so the correct pattern-key-based required keys are used on validation and retry.

3. **`src/LinguaCoach.Infrastructure/Activity/Evaluators/AiStructuredEvaluator.cs`**
   - `CompactContent` now detects `module_stage_v1` content and returns only `practiceContent.exerciseData` + `feedbackPlan` — excludes `learnContent` to protect token budget and prevent teaching text being used as an answer source by the evaluator AI.
   - `CompactContent` marked `internal static` for unit testing.

4. **`src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`**
   - `ActivityGenerateListenAndAnswerContent`: Rewritten to produce `module_stage_v1`. `learnContent` teaches general listening strategy (no audioScript/questions/transcript). `practiceContent.exerciseData` carries `speakerRole`, `listenerRole`, `audioScript`, `transcriptAvailableAfterSubmit`, `questions`, and `responseTask`.
   - `ActivityGenerateListenAndGapFillContent`: Rewritten to produce `module_stage_v1`. `learnContent` teaches key-word catching strategy. `practiceContent.exerciseData` carries `speakerRole`, `audioScript`, `transcriptAvailableAfterSubmit`, and `gaps` (4–5 items with `id`, `sentenceWithBlank`, `answer`, `hint`).
   - Both prompts include strict Learn-stage rules: no audioScript/questions/gaps/expectedAnswer/transcript in `learnContent`.

5. **`tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`**
   - `Validate_ListenAndAnswer_WithValidPayload_ReturnsValid` — valid staged `listen_and_answer` passes.
   - `Validate_ListenAndAnswer_MissingRequiredKey_Fails` (Theory: `audioScript`, `questions`) — missing either required key fails.
   - `Validate_ListenAndGapFill_WithValidPayload_ReturnsValid` — valid staged `listen_and_gap_fill` passes.
   - `Validate_ListenAndGapFill_MissingRequiredKey_Fails` (Theory: `audioScript`, `gaps`) — missing either required key fails.
   - `Validate_ListenAndGapFill_WithQuestionsInsteadOfGaps_Fails` — wrong required key explicitly fails.
   - `Validate_ListenAndAnswer_PatternKeyOverrides_ActivityTypeCheck` — confirms pattern-key lookup overrides ActivityType-level `questions` requirement.
   - `RemoveExerciseDataKey` helper added (reusable within the file).

6. **`tests/LinguaCoach.UnitTests/Activity/AiStructuredEvaluatorTests.cs`**
   - `CompactContent_StagedContent_ExcludesLearnContentAndReturnsExerciseData` — staged JSON: result contains `audioScript`, `questions`, `feedbackFocus`; does not contain `learnContent` or the teaching title.
   - `CompactContent_LegacyFlatContent_ReturnedAsIs` — legacy flat JSON passes through.
   - `CompactContent_EmptyJson_ReturnsEmptyObject` — `{}` returns `{}`.

### What the `ExactMatchEvaluator` change covers for free

`ExactMatchEvaluator` already has `UnwrapStagedContent` from Phase 7A. Its `ParseExpectedItems` already branches on `patternKey == "listen_and_gap_fill"` and deserialises as `ListenAndGapFillContent { Gaps }`. With staged content now wrapping exerciseData, the existing unwrap + deserialise path handles it without further changes.

### No presenter changes needed

`PatternBackedPresenter.teachContent` already returns `stagedLearning` for any activity where `activity.stageContent?.learn` is present. Once the generator produces `module_stage_v1` for these patterns, `ActivityGetHandler.BuildStageContent` parses it and populates `StageContentDto` automatically. Presenter tests already cover this path for all `stageContent`-bearing activities.

### Verification

- `dotnet build` clean (0 errors, 6 pre-existing warnings)
- 557/557 backend unit tests pass (+11 from Phase 7B)
- Angular build unchanged (no frontend changes in this phase)

### Still out of scope

- `email_reply`, `teams_chat_simulation`, `spoken_response_from_prompt`, `open_writing_task`, `speaking_roleplay_turn`, `lesson_reflection`
- Today pre-generation
- MinIO/audio lifecycle
- Planned future exercise format renderers/evaluators remain planned and non-runnable

---

## Phase 7C — migrate `email_reply`, `teams_chat_simulation`, and `open_writing_task` to `module_stage_v1`

**Date:** 2026-06-15
**Commit:** pending
**Status:** complete

### Goals

Migrate the three writing/chat pattern-backed activities to `module_stage_v1`. Same recipe as Phase 7A/7B. No MinIO/audio lifecycle, no Today pre-generation, no speaking/reflection patterns.

### Root cause fixed

All three patterns have `ActivityType.WritingScenario`. The `AiActivityGeneratorHandler.GenerateActivityContentAsync` `WritingScenario` branch always calls `ValidateStagedContent`. Their flat prompts would fail that validation, causing on-demand generation to throw `AiResponseValidationException`. Pre-generated pool activities were unaffected (seeded before staged validation was enforced). Phase 7C fixes generation for all three patterns by producing `module_stage_v1`.

### Files changed

1. **`src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`**
   - Added `email_reply` → `["prompt", "incomingMessage"]` to `RequiredPracticeKeysByPatternKey`.
   - Added `teams_chat_simulation` → `["prompt", "chatHistory"]` to `RequiredPracticeKeysByPatternKey`.
   - Added `open_writing_task` → `["prompt"]` to `RequiredPracticeKeysByPatternKey`.
   - Pattern-key lookup takes precedence over `ActivityType.WritingScenario` (which requires `["prompt", "situation", "audience", "tone"]`).

2. **`src/LinguaCoach.Infrastructure/Activity/Evaluators/AiOpenEndedEvaluator.cs`**
   - `CompactContent` now detects `module_stage_v1` and returns only `practiceContent.exerciseData` + `feedbackPlan` — excludes `learnContent`. Same pattern as `AiStructuredEvaluator.CompactContent` (Phase 7B). Marked `internal static` for unit testing.
   - `AiStructuredEvaluator.CompactContent` (Phase 7B) already handles `email_reply` and `teams_chat_simulation` staged content correctly — no further changes needed.

3. **`src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`**
   - `ActivityGenerateEmailReplyContent`: Rewritten to produce `module_stage_v1`. `learnContent` teaches general email reply strategy (structure, tone, opener phrases). `practiceContent.exerciseData` carries `incomingMessage` (the email to reply to), `recipient`, `relationship`, `tone`, `prompt`, `requiredInformation`, `requiredPhrases`, `targetVocabulary`, `expectedLength`, `suggestedSubject`, `successChecklist`.
   - `ActivityGenerateTeamsChatContent`: Rewritten to produce `module_stage_v1`. `learnContent` teaches general workplace chat communication (conciseness, tone, clarity). `practiceContent.exerciseData` carries `chatHistory` (array of message objects), `speakerRole`, `recipientRole`, `tone`, `prompt`, `requiredInformation`, `requiredPhrases`, `targetVocabulary`, `successChecklist`.
   - `ActivityGenerateOpenWritingTaskContent`: Rewritten to produce `module_stage_v1`. `learnContent` teaches general workplace writing strategy (planning, structure, clarity). `practiceContent.exerciseData` carries `prompt`, `tone`, `expectedLength`, `requiredInformation`, `requiredPhrases`, `targetVocabulary`, `successChecklist`.
   - All three prompts include strict Learn-stage rules: `learnContent` must not contain the actual writing task, incoming message/chat, expected answer, or any practice controls.

4. **`tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`**
   - `Validate_EmailReply_WithValidPayload_ReturnsValid` — valid staged `email_reply` passes.
   - `Validate_EmailReply_MissingRequiredKey_Fails` (Theory: `prompt`, `incomingMessage`) — missing either required key fails.
   - `Validate_EmailReply_WithPracticeControlInLearnContent_Fails` (Theory: `answerKey`, `submitLabel`, `textarea`) — practice controls forbidden in learnContent.
   - `Validate_TeamsChat_WithValidPayload_ReturnsValid` — valid staged `teams_chat_simulation` passes.
   - `Validate_TeamsChat_MissingRequiredKey_Fails` (Theory: `prompt`, `chatHistory`) — missing either required key fails.
   - `Validate_OpenWritingTask_WithValidPayload_ReturnsValid` — valid staged `open_writing_task` passes.
   - `Validate_OpenWritingTask_MissingPrompt_Fails` — missing `prompt` fails.
   - `Validate_OpenWritingTask_WithControlKeyInLearnContent_Fails` (Theory: `submitLabel`, `checkLabel`, `textarea`) — control keys forbidden in learnContent.
   - `RemoveExerciseDataKey` helper (added in Phase 7B) reused.

5. **`tests/LinguaCoach.UnitTests/Activity/AiOpenEndedEvaluatorTests.cs`**
   - `CompactContent_StagedContent_ExcludesLearnContentAndReturnsExerciseData` — staged JSON: result contains `prompt`, `feedbackFocus`; does not contain `learnContent` or teaching title.
   - `CompactContent_LegacyFlatContent_ReturnedAsIs` — legacy flat JSON passes through.
   - `CompactContent_EmptyJson_ReturnsEmptyObject` — `{}` returns `{}`.

### No generator handler changes needed

`AiActivityGeneratorHandler` `WritingScenario` branch already passes `context.ExercisePatternKey` to `ValidateStagedContent` (Phase 7B). No `StagedPatternKeys` additions needed — all WritingScenario activities go through staged validation unconditionally.

### No `ActivityGetHandler` changes needed

`BuildStageContent` detects `module_stage_v1` generically and passes through `primarySkill`, `secondarySkills`, and `exerciseType` from the JSON. Old flat activities in the pool are still handled by `AdaptLegacyWriting`. No changes needed.

### No frontend changes needed

`PatternBackedPresenter.teachContent` already returns `stagedLearning` for any pattern with `stageContent`. The email/chat/writing renderers in Practice are driven by `InteractionMode` (`EmailReply`, `ChatReply`, `FreeTextEntry`) — they already read `practiceContent.exerciseData` via `stageContent.practice.exerciseData`. No presenter or page changes required.

### Evaluator routing unchanged

- `email_reply` → `AiStructuredEvaluator` (MarkingMode.AiStructured) — `CompactContent` from Phase 7B already strips learnContent.
- `teams_chat_simulation` → `AiStructuredEvaluator` (MarkingMode.AiStructured) — same.
- `open_writing_task` → `AiOpenEndedEvaluator` (MarkingMode.AiOpenEnded) — `CompactContent` updated in this phase.

### Verification

- `dotnet build` clean (0 errors, 6 pre-existing warnings)
- 574/574 backend unit tests pass (+17 from Phase 7C)
- Angular build unchanged (no frontend changes in this phase)

### Still out of scope

- `spoken_response_from_prompt`, `speaking_roleplay_turn`, `lesson_reflection` — speaking/reflection patterns, deferred to Phase 7D
- Today pre-generation
- MinIO/audio lifecycle
- Planned future exercise format renderers/evaluators remain planned and non-runnable
- Practice Gym pool changes (existing compatibility unchanged)

---

## Phase 7D — Speaking/reflection pattern-backed staged migration

**Status:** complete
**Date:** 2026-06-15

### Patterns migrated

- `spoken_response_from_prompt`
- `speaking_roleplay_turn`
- `lesson_reflection`

This completes the pattern-backed staged migration. All pattern-backed activities now produce `module_stage_v1`.

### ActivityType mapping

| Pattern | ActivityType |
|---|---|
| `spoken_response_from_prompt` | `SpeakingRolePlay` |
| `speaking_roleplay_turn` | `SpeakingRolePlay` |
| `lesson_reflection` | `WritingScenario` |

### Files changed

| File | Change |
|---|---|
| `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` | Added `spoken_response_from_prompt` (`prompt`), `speaking_roleplay_turn` (`prompt`, `partnerTurn`), `lesson_reflection` (`prompt`) to `RequiredPracticeKeysByPatternKey` |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Added three patterns to `StagedPatternKeys` (belt-and-suspenders; these ActivityTypes already validate staged unconditionally) |
| `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` | Rewrote all three generation prompts to produce `module_stage_v1` |
| `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs` | +14 tests for all three patterns |

### Generation — all AI-generated

All three patterns are AI-generated on-demand via their registered `aiGeneratePromptKey`. No deterministic seed path.

### Validation routing

`spoken_response_from_prompt` and `speaking_roleplay_turn` use `ActivityType.SpeakingRolePlay` — the generator's top `case` block already calls `ValidateStagedContent` unconditionally for `SpeakingRolePlay`. The `StagedPatternKeys` additions are harmless redundancy.

`lesson_reflection` uses `ActivityType.WritingScenario` — same: `WritingScenario` always validates staged.

### Evaluator routing — unchanged

- `spoken_response_from_prompt` → `AiOpenEndedEvaluator` (MarkingMode.AiOpenEnded) — `CompactContent` from Phase 7C already strips `learnContent` for staged activities.
- `speaking_roleplay_turn` → `AiOpenEndedEvaluator` (MarkingMode.AiOpenEnded) — same.
- `lesson_reflection` → `AiStructuredEvaluator` (MarkingMode.AiStructured) — `CompactContent` from Phase 7B already strips `learnContent`.

### No frontend changes needed

`PatternBackedPresenter` already returns `stagedLearning` for any pattern with `stageContent.learn`. Practice renderers read `stageContent.practice.exerciseData`. Old flat pool activities still handled by legacy adapters.

### Learn-stage rules enforced

`learnContent` for all three patterns must not contain recording controls, the final speaking/reflection prompt, expected answer, or any practice/submission controls. These are already in `ForbiddenLearnContentKeys`. Validator tests confirm enforcement.

### Verification

- `dotnet build` clean
- All backend unit tests pass (+14 from Phase 7D)
- Angular build unchanged (no frontend changes)

### Constraints confirmed

- Today pre-generation: not implemented
- MinIO/audio lifecycle: not implemented
- Planned future exercise format renderers/evaluators: remain planned and non-runnable
- `/activity`, `exerciseType=`, `type=`, `pattern=` compatibility: preserved
- Old student data: not deleted or broken

### Pattern-backed staged migration complete

All pattern-backed activities now produce `module_stage_v1`:

| Phase | Patterns |
|---|---|
| 7A | `phrase_match`, `gap_fill_workplace_phrase` |
| 7B | `listen_and_answer`, `listen_and_gap_fill` |
| 7C | `email_reply`, `teams_chat_simulation`, `open_writing_task` |
| 7D | `spoken_response_from_prompt`, `speaking_roleplay_turn`, `lesson_reflection` |

### Remaining non-pattern architecture items

- Today pre-generation (future phase)
- MinIO/audio lifecycle (future phase)
- Planned future exercise format renderers/evaluators (future phase, non-runnable)
- Practice Gym dynamic skill selection — existing, unchanged
- Practice Gym pool health/monitoring — future phase

---

## Phase 8A — `reading_multiple_choice_single` becomes first runnable planned future exercise format

**Status:** complete
**Date:** 2026-06-15

### Goal

Make `reading_multiple_choice_single` the first "planned future exercise format"
to become fully runnable, following the Phase 7B recipe (pattern key,
catalog row, deterministic non-AI evaluator). Only this one format changes
status. All other planned reading/writing/listening/speaking formats remain
planned and non-runnable.

### New pattern

`reading_multiple_choice_single` — primarySkill `reading`, secondarySkills `[]`,
`ActivityType.ReadingTask`, `InteractionMode.MultipleChoice`,
`MarkingMode.KeyedSelection`. Both `ActivityType.ReadingTask` and
`InteractionMode.MultipleChoice` existed but were unused before this phase.

### Files changed

| File | Change |
|---|---|
| `src/LinguaCoach.Domain/ExercisePatternKey.cs` | Added `ReadingMultipleChoiceSingle` constant |
| `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` | Added new pattern row (11th pattern) |
| `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` | Converted `reading_multiple_choice_single` catalog row from `Planned` to `Ready`; total catalog rows unchanged at 36 |
| `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` | Added `passage`, `question`, `correctOptionId`, `distractorExplanations` to `ForbiddenLearnContentKeys`; added `reading_multiple_choice_single` → `["passage", "question", "options", "correctOptionId"]` to `RequiredPracticeKeysByPatternKey` |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Added pattern to `StagedPatternKeys`; allowed `ActivityType.ReadingTask` past the unsupported-type guard; added `ReadingTask` to the staged-validation switch |
| `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` | New deterministic, non-AI evaluation path for `reading_multiple_choice_single` — compares submitted `selectedOptionId` to `correctOptionId`, returns explanation/distractor feedback |
| `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` | New `activity_generate_reading_multiple_choice_single` prompt producing `module_stage_v1` with `passage`, `question`, 4 options A–D, `correctOptionId`, `explanation`, `distractorExplanations`, `successChecklist` |
| `src/LinguaCoach.Web/.../renderers/reading-multiple-choice/reading-multiple-choice.component.{ts,html}` | New standalone Angular renderer (Learn + Practice) |
| `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.{ts,html}` | Wired new renderer, new `ExerciseAnswerPayload` variant `multipleChoiceSingle` |
| `src/LinguaCoach.Web/.../activity-lesson/activity-lesson.component.ts` | Maps `multipleChoiceSingle` payload to `{ selectedOptionId }` |
| `src/LinguaCoach.Web/.../presenters/pattern-backed.presenter.ts` | Added `multipleChoice` skill badge case (Reading) |
| `src/LinguaCoach.Web/.../practice-gym.component.spec.ts` | Replaced planned-reading fixture with ready `reading_multiple_choice_single`; added pool-flow tests |

### Validation

`RequiredPracticeKeysByPatternKey` entry takes precedence over
`RequiredPracticeKeysByType` for `ActivityType.ReadingTask`, so no change
to the type-level map was needed.

### Evaluator

`KeyedSelectionEvaluator` now branches on `ExercisePatternKey ==
"reading_multiple_choice_single"` before its existing phrase-match path.
Score is 1.0 for a correct `selectedOptionId`, 0.0 otherwise. Feedback uses
`explanation` on correct answers and `distractorExplanations[selectedOptionId]`
plus `explanation` on incorrect answers. No-selection is scored 0 but still
`Completed: true`.

### Pool infrastructure

`reading_multiple_choice_single` has a non-empty `ExercisePatternKey`, so
`PracticeGymPoolService` pool reservation works for it like any other
pattern-backed activity (this is why a full `ExercisePatternDefinition` row
was added, not just a catalog-only entry).

### Tests added

- `tests/LinguaCoach.IntegrationTests/Sessions/ExerciseTypeCatalogTests.cs`:
  `ReadingMultipleChoiceSingle_IsReadyAndEligible`,
  `OtherPlannedReadingTypes_RemainUnchanged`
- `tests/LinguaCoach.IntegrationTests/Sessions/ExercisePatternPhase1Tests.cs`:
  pattern-count assertions updated 10 → 11 (active count 9 → 10 after one
  deactivation in the existing test)
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`:
  valid payload, missing-required-key theory, forbidden-learn-key theory
- `tests/LinguaCoach.UnitTests/Activity/KeyedSelectionEvaluatorTests.cs`:
  correct selection, incorrect selection with distractor explanation,
  no-selection

### Verification

- `dotnet build --configuration Release`: 0 errors, 6 pre-existing unrelated
  CS0618 warnings
- `dotnet test --configuration Release`:
  - Architecture: 3/3 (unchanged)
  - Unit: 603/603 (baseline 589 + 14 new)
  - Integration: 474/474 (baseline 472 + 2 new)
- Angular unit tests / dev build / production build / Playwright e2e: not
  run — `node_modules` not installed in this worktree (pre-existing
  environment gap, not introduced by this phase); per instructions, no
  package install was performed

### Constraints confirmed

- Only `reading_multiple_choice_single` became runnable; all other planned
  rows untouched (`OtherPlannedReadingTypes_RemainUnchanged` test)
- No audio formats, Today pre-generation, MinIO, or new
  speaking/listening formats touched
- `/activity` `exerciseType=`/`type=`/`pattern=` query compatibility
  preserved (no changes to those code paths)
- No student data, activity history, or pool behavior changes beyond
  adding one new pattern-backed activity type
- "PTE" terminology not used anywhere in code, comments, tests, or docs

### Phase 8B candidate

Pick the next planned reading or writing format with a similarly simple,
deterministic, keyed-selection or exact-match shape (e.g. another
single-answer reading format) and repeat this recipe: pattern definition →
catalog row → validator keys → evaluator branch → prompt → renderer.

---

## Phase 8B — `reading_multiple_choice_multi` — COMPLETE 2026-06-15

### Goal

Make `reading_multiple_choice_multi` the second runnable planned future reading exercise format.

The student reads a workplace passage and selects **all** answers supported by the text.
Evaluated deterministically — no AI evaluation call required.

### What was implemented

| File | Change |
|------|--------|
| `src/LinguaCoach.Domain/ExercisePatternKey.cs` | Added `ReadingMultipleChoiceMulti = "reading_multiple_choice_multi"` |
| `src/LinguaCoach.Domain/Enums/InteractionMode.cs` | Added `MultipleChoiceMulti = 12` (append-only) |
| `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` | Added `ReadingMultipleChoiceMulti` pattern with `InteractionMode.MultipleChoiceMulti`, `MarkingMode.KeyedSelection`, `ReadingInput` compat kind |
| `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs` | Promoted `reading_multiple_choice_multi` catalog row from `Planned` to `Ready`; total catalog rows unchanged at 36 |
| `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs` | Added `correctOptionIds` and `optionExplanations` to `ForbiddenLearnContentKeys`; added `reading_multiple_choice_multi` → `["passage", "question", "options", "correctOptionIds"]` to `RequiredPracticeKeysByPatternKey` |
| `src/LinguaCoach.Application/Activity/Evaluators/KeyedSelectionEvaluator.cs` | New deterministic evaluation path for `reading_multiple_choice_multi` — compares submitted `selectedOptionIds` set to `correctOptionIds` set; identifies missed correct options and false positives; includes option-level explanations in feedback |
| `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` | New `activity_generate_reading_multiple_choice_multi` prompt producing `module_stage_v1` with `passage`, `question`, 4 options A–D, `correctOptionIds` (at least two), `explanation`, `optionExplanations`, `successChecklist` |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Added `reading_multiple_choice_multi` to `StagedPatternKeys` for staged content validation on generation |
| `src/LinguaCoach.Web/.../renderers/reading-multiple-choice-multi/` | New `ReadingMultipleChoiceMultiComponent` — checkbox-style multi-select renderer; shows passage, question, togglable options, submit button |
| `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.ts` | Imported new component; added `readingMultipleChoiceMultiContent` getter; added `onReadingMultipleChoiceMultiSubmitted` handler; added `multipleChoiceMulti` to `ExerciseAnswerPayload` union |
| `src/LinguaCoach.Web/.../exercise-renderer/exercise-renderer.component.html` | Added `@case ('multipleChoiceMulti')` branch |
| `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` | Added `'multipleChoiceMulti'` to `InteractionMode` union type |
| `tests/.../Sessions/ExerciseTypeCatalogTests.cs` | Added `ReadingMultipleChoiceMulti_IsReadyAndEligible`; updated `OtherPlannedReadingTypes_RemainUnchanged` to exclude both ready reading keys |
| `tests/.../Sessions/ExercisePatternPhase1Tests.cs` | Updated pattern counts from 11→12 (all active) and 10→11 (after deactivation) |
| `tests/.../Domain/InteractionModeMarkingModeTests.cs` | Added pin test for `MultipleChoiceMulti = 12`; updated count to 13 |
| `tests/.../Activity/KeyedSelectionEvaluatorTests.cs` | Added 5 evaluator tests: exact correct set, missing one correct, false positive, no selection, invalid JSON |
| `tests/.../Activity/ModuleStageContentValidatorTests.cs` | Added valid payload test, 4 missing-required-key tests, 7 forbidden-learn-key tests |
| `tests/.../practice/practice-gym.component.spec.ts` | Added `readyReadingMulti` fixture; added 2 new tests for multi availability and routing |

### CI/CD results

- `git diff --check`: PASS
- `dotnet restore`: PASS
- `dotnet build --configuration Release`: PASS (0 errors, 6 pre-existing warnings)
- `dotnet test --configuration Release`: **621 unit / 475 integration / 3 architecture = 1099/1099 PASS**
- Angular unit tests: **106/106 PASS** (up from 104 — 2 new tests added)
- Angular production build: PASS

### Scope boundaries respected

- Only `reading_multiple_choice_multi` became runnable; all other planned rows untouched
- No audio formats, Today pre-generation, MinIO, or speaking/listening formats touched
- `/activity` `exerciseType=`/`type=`/`pattern=` query compatibility preserved
- No student data, activity history, or pool behavior changes beyond adding one new pattern
- "PTE" terminology not used anywhere in code, comments, tests, or docs

### Phase 8C — COMPLETE 2026-06-15

`reading_fill_in_blanks` — passage with `{{gapN}}` tokens, per-gap dropdown options, `ExactMatchEvaluator` gap-keyed branch, `InteractionMode.ReadingFillInBlanks = 13`. Tests: 631 unit / 476 integration / 3 arch / 108 Angular — all green.

### Phase 8D — COMPLETE 2026-06-15

`reorder_paragraphs` — shuffled paragraph blocks, move-up/move-down UI, `ExactMatchEvaluator` position-keyed branch, `InteractionMode.ReorderParagraphs = 14`. Submitted shape: `{ orderedIds: string[] }`. Per-position scoring with partial credit. Tests: 644 unit / 477 integration / 3 arch / 110 Angular — all green.

### Phase 8E — COMPLETE 2026-06-15

`reading_writing_fill_in_blanks` — same `{{gapN}}`/dropdown UI as `reading_fill_in_blanks`, secondary skill `["writing"]`, `InteractionMode.ReadingWritingFillInBlanks = 15`, `ExactMatchEvaluator` reuses the `reading_fill_in_blanks` branch (patternKey OR condition). All reading-primary exercise types are now Ready. Tests: 655 unit / 479 integration / 3 arch / 112 Angular — all green.

### Phase 8F — COMPLETE 2026-06-15

`summarize_written_text` — student reads a 100-150 word passage and writes a concise summary. Primary skill: writing; secondary: reading. `InteractionMode.FreeTextEntry` reused (no new enum value). `MarkingMode.AiStructured` — rubric-based AI evaluation via `AiStructuredEvaluator`. Frontend: `FreeTextEntryComponent` reused; `stagedExerciseData` getter added to `exercise-renderer` unwraps `practiceContent.exerciseData` and maps `sourceText`→`situation`, `prompt`→`prompt`. AI prompts seeded: `activity_generate_summarize_written_text` (maxInput:1000, maxOutput:1400) and `activity_evaluate_summarize_written_text` (maxInput:2000, maxOutput:1200). All other planned formats remain non-runnable. Tests: 655 unit / 479 integration / 3 arch / 114 Angular — all green.

### Phase 8G — `write_essay` — COMPLETE 2026-06-15

`write_essay` — student reads an essay prompt and writes a structured response (intro/body/conclusion). Primary skill: writing; secondary: none. `InteractionMode.FreeTextEntry` reused (no new enum value), `MarkingMode.AiStructured`. New `ExercisePatternKey.WriteEssay = "write_essay"`. Pattern metadata: `ActivityType.WritingScenario`, `compatibleKindsJson: [4]` (WritingTask), estimated 10 minutes. `ExerciseTypeDefinitionSeeder` entry promoted from `Planned` to `Ready`. `ModuleStageContentValidator`: required practice keys `["prompt","topic"]`; forbidden learnContent keys extended with `modelEssay`, `expectedEssay`, `submittedEssay`. `AiActivityGeneratorHandler.StagedPatternKeys` includes `write_essay` (module_stage_v1 generation/validation). `AiStructuredEvaluator.ResolvePromptKey` routes to `activity_evaluate_write_essay`. Frontend: `FreeTextEntryComponent` reused; `exercise-renderer` `freeTextContent` getter falls back to `exerciseData.topic` for `situation`, maps `requirements.targetWordCount` to new `wordCountTarget: string` field (changed from `number`), displayed in `free-text-entry.component.html` next to the live word count. AI prompts seeded: `activity_generate_write_essay` (maxInput:1000, maxOutput:1600) and `activity_evaluate_write_essay` (maxInput:2000, maxOutput:1400). All other planned formats remain non-runnable.

Fixed pre-existing Phase 8F validator bug: `ForbiddenLearnContentKeys` incorrectly included `keyPoints` (a standard learnContent field present in nearly all formats) plus duplicate/overly-broad `answerKey`, `textarea`, `submit` entries — these were silently failing ~17 unit tests before this phase. Removed.

Tests: 671 unit / 482 integration / 116 Angular — all green. Production Angular build succeeds.

### Phase 8H — `listening_multiple_choice_single` — COMPLETE 2026-06-15

`listening_multiple_choice_single` — first runnable listening-primary format. Student listens to a short audio script and chooses one correct answer from 4 options. Primary skill: listening; secondary: none. `InteractionMode.MultipleChoice` reused (no new enum value), `MarkingMode.KeyedSelection`. New `ExercisePatternKey.ListeningMultipleChoiceSingle = "listening_multiple_choice_single"`. Pattern metadata: `ActivityType.ListeningComprehension`, `compatibleKindsJson: [2]` (ListeningInput), `requiresAudio: false`, estimated 5 minutes. `ExerciseTypeDefinitionSeeder` entry promoted from `Planned` to `Ready` (SupportsPracticeGym=true, SupportsTodayLesson=false).

`ModuleStageContentValidator`: required practice keys `["audioScript","question","options","correctOptionId"]`. No new forbidden learnContent keys needed — all required keys (audioScript, transcript, question, options, correctOptionId, distractorExplanations, answerKey, correctAnswer, selectedAnswer, checkAnswer) were already forbidden from prior phases.

`AiActivityGeneratorHandler.StagedPatternKeys` includes `listening_multiple_choice_single`. Deterministic evaluation only — no AI call. `KeyedSelectionEvaluator` (previously single-purpose for `reading_multiple_choice_single`) now dispatches both `reading_multiple_choice_single` and `listening_multiple_choice_single` to a shared pattern-agnostic evaluator: compares `selectedOptionId` to `correctOptionId`, returns explanation + distractor explanation, with a "passage" vs "audio" wording switch based on pattern key.

AI prompt seeded: `activity_generate_listening_multiple_choice_single` (maxInput:900, maxOutput:900) — generates `audioScript` (30-70 words natural spoken English), `question`, 4 `options`, `correctOptionId`, `explanation`, `distractorExplanations`, `audioUrl: null`. No evaluate-prompt seeded (deterministic evaluation).

**Audio/TTS reuse note**: `audioUrl` is always `null` from generation since no TTS pipeline produces pre-generated audio for staged content. Frontend renders an explicit fallback ("Audio is temporarily unavailable") with the `audioScript` shown as text below it, so the exercise is usable without MinIO/audio asset infrastructure. This is a deliberate, documented tradeoff: the script is technically visible before submission (it is the only way to consume the "audio" content), but `question`/`options`/`correctOptionId` remain hidden until after submission, preserving the assessment integrity of the exercise. No MinIO, new storage, or background TTS jobs were introduced.

Frontend: reused `ReadingMultipleChoiceComponent`/`reading-multiple-choice.component.html` (renamed conceptually to a generic single-choice renderer) — added optional `audioScript`/`audioUrl`/`scenario` fields to `ReadingMultipleChoiceContent`, made `passage` optional, added an "Audio" section (audio player when `audioUrl` set, text fallback otherwise) and a "Context" scenario block. `exercise-renderer.component.ts` `readingMultipleChoiceContent` getter fixed to read from `stagedExerciseData` (practiceContent.exerciseData) with fallback to `raw`, fixing a latent bug affecting both `reading_multiple_choice_single` and this new format under module_stage_v1 staged content.

Practice Gym: `listening_multiple_choice_single` appears under the Listening skill, pool-first with on-demand fallback, same as other ready formats. All other listening formats (`listen_and_answer`, `listen_and_gap_fill` excluded — already ready from MVP; remaining planned listening formats `listening_multiple_choice_multi`, `listening_fill_in_blanks`, `highlight_correct_summary`, `select_missing_word`, `highlight_incorrect_words`, `write_from_dictation`, `summarize_spoken_text`) remain planned/non-runnable.

Tests: 684 unit / 483 integration / 118 Angular — all green. Angular dev and production builds succeed.

See [Phase 8H review](../reviews/2026-06-15-phase-8h-listening-multiple-choice-single-implementation.md).

### Phase 8I — `listening_multiple_choice_multi` — COMPLETE 2026-06-16

`listening_multiple_choice_multi` — second runnable listening-primary format. Student listens to a short audio script and selects ALL correct answers (at least two) from 4 options. Primary skill: listening; secondary: none. `InteractionMode.MultipleChoiceMulti` reused (already used by `reading_multiple_choice_multi`, no new enum value), `MarkingMode.KeyedSelection`. New `ExercisePatternKey.ListeningMultipleChoiceMulti = "listening_multiple_choice_multi"`. Pattern metadata: `ActivityType.ListeningComprehension`, `compatibleKindsJson: [2]` (ListeningInput), `requiresAudio: false`, estimated 5 minutes. `ExerciseTypeDefinitionSeeder` entry promoted from `Planned` to `Ready` (SupportsPracticeGym=true, SupportsTodayLesson=false).

`ModuleStageContentValidator`: required practice keys `["audioScript","question","options","correctOptionIds"]`. No new forbidden learnContent keys needed — all required keys (audioScript, transcript, question, options, correctOptionIds, optionExplanations, answerKey, correctAnswer, selectedAnswer/selectedAnswers, checkAnswer/submitButton/checkButton) were already forbidden from prior phases.

`AiActivityGeneratorHandler.StagedPatternKeys` includes `listening_multiple_choice_multi`. Deterministic evaluation only — no AI call. `KeyedSelectionEvaluator` dispatch for `reading_multiple_choice_multi` extended to also handle `listening_multiple_choice_multi` via the same pattern-agnostic evaluator: compares the submitted set of `selectedOptionIds` against `correctOptionIds` as a set (exact match required), reports missed correct options and false positives, with a "passage" vs "audio" wording switch based on pattern key. `ReadingMultipleChoiceMultiExerciseData`/`ReadingMultipleChoiceMultiSubmittedAnswer` DTOs reused as-is — JSON deserialization tolerates the extra `audioScript`/`audioUrl` fields in the listening exerciseData shape.

AI prompt seeded: `activity_generate_listening_multiple_choice_multi` (maxInput:900, maxOutput:1000) — generates `audioScript` (30-80 words natural spoken English), `question`, 4 `options`, `correctOptionIds` (at least 2), `explanation`, `optionExplanations` for all 4 options, `audioUrl: null`. No evaluate-prompt seeded (deterministic evaluation).

**Audio/TTS reuse note**: identical pattern to Phase 8H — `audioUrl` is always `null`; frontend falls back to showing `audioScript` as text with an "Audio is temporarily unavailable" notice. No MinIO, new storage, or background TTS jobs introduced.

Frontend: reused `ReadingMultipleChoiceMultiComponent`/`reading-multiple-choice-multi.component.html` — added optional `audioScript`/`audioUrl`/`scenario` fields to `ReadingMultipleChoiceMultiContent`, made `passage` optional, added the same "Context" scenario block and "Audio" section (audio player when `audioUrl` set, text fallback otherwise) as 8H. `exercise-renderer.component.ts` `readingMultipleChoiceMultiContent` getter fixed to read from `stagedExerciseData` (practiceContent.exerciseData) with fallback to `raw`, fixing the same latent bug class as 8H, now affecting both `reading_multiple_choice_multi` and this new format under module_stage_v1 staged content.

Practice Gym: `listening_multiple_choice_multi` appears under the Listening skill, pool-first with on-demand fallback, same as other ready formats. All other listening formats (`listen_and_answer`, `listen_and_gap_fill` already ready from MVP; remaining planned listening formats `listening_fill_in_blanks`, `highlight_correct_summary`, `select_missing_word`, `highlight_incorrect_words`, `write_from_dictation`, `summarize_spoken_text`) remain planned/non-runnable.

Tests: 699 unit / 484 integration / 120 Angular — all green. Angular dev and production builds succeed.

See [Phase 8I review](../reviews/2026-06-16-phase-8i-listening-multiple-choice-multi-implementation.md).
