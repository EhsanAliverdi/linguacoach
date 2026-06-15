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
- Practice Gym pool changes (existing compatibility unchanged)
