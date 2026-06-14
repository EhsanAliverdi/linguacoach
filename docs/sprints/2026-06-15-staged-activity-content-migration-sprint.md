---
status: in-progress
lastUpdated: 2026-06-15 10:05
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

## PR2 — WritingScenario staged migration, complete

PR2 applies the PR1 staged-content recipe to the legacy `WritingScenario`
activity type only. New `activity_generate_writing` content now produces
`module_stage_v1` with `primarySkill`, `secondarySkills`, and
`exerciseType: "writing_scenario"`.

Completed work:

1. `activity_generate_writing` now returns staged JSON only.
2. The prompt separates Learn, Practice, and FeedbackPlan.
3. Learn is teaching-only and excludes the final writing task, textarea,
   submitted answer, answer keys, and submit/check controls.
4. `ModuleStageContentValidator` validates WritingScenario practice data.
5. Required staged writing practice keys are `prompt`, `situation`,
   `audience`, and `tone` under `practiceContent.exerciseData`.
6. `ActivityGetHandler.BuildStageContent` maps staged WritingScenario content
   normally and adapts old flat WritingScenario JSON to `legacy_adapted_v1`.
7. The legacy adapter preserves writing task fields in
   `practiceContent.exerciseData`, including prompt, situation, audience, tone,
   required phrases, and target vocabulary when present.
8. API responses expose `stageContent` for WritingScenario through the existing
   generic response mapping.
9. `activity_evaluate_writing` receives staged evaluation context based on
   `practiceContent`, `feedbackPlan`, and teaching context from `learnContent`.
10. The Angular Writing presenter returns `stagedLearning` when `stageContent`
    exists and keeps the old fallback path.
11. The Writing practice page reads staged `practiceContent.exerciseData` for
    situation, audience, tone, expected length, prompt, required phrases, and
    target vocabulary.

Out of scope remains unchanged:

* Practice Gym pre-generation pool.
* Today background generation.
* MinIO/audio lifecycle.
* New listening exercise types.
* ModuleRun persistence.
* Speaking, Vocabulary, and pattern-backed staged migrations.
* Removing legacy compatibility paths.
* Removing `/activity`.

Remaining staged migrations:

* SpeakingRolePlay.
* VocabularyPractice.
* Pattern-backed writing exercises.
* Pattern-backed listening exercises.
* Pattern-backed chat, email, gap-fill, matching, and reflection flows.

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

## Phase 3A — Exercise Type Catalog and Admin Enable Disable Foundation

Status: implemented in this PR.

Phase 3A adds the durable exercise type catalog before migrating additional
activity types to `module_stage_v1`.

Key decisions:

* Skills are not exercise types.
* `primarySkill` and `secondarySkills` describe learning intent.
* `exerciseType` selects the Practice renderer and evaluator.
* The catalog is the source of truth for future generation eligibility.
* Admin enable disable affects future generated modules only.
* Existing activities and attempts remain readable after disable.
* `implementationStatus` prevents planned PTE-style types from becoming runnable.

Backend changes:

* Added `exercise_type_definitions` table.
* Added seeded catalog rows for existing implemented patterns and future PTE-style types.
* Added admin list and patch APIs under `/api/admin/exercise-types`.
* Added authenticated student catalog read endpoint at `/api/activity/exercise-types`.
* Practice Gym generation now checks enabled, ready, Practice Gym-supported rows.
* Today deterministic session generation filters disabled or unavailable patterns.
* Background Practice Gym cache queues only catalog-eligible pattern keys.

Admin UI changes:

* Added Admin Exercise Types page.
* Displays key, skill metadata, category, enabled state, implementation status,
  surface support, audio and image requirements, and generation availability.
* Admins can enable or disable an exercise type.
* Planned rows remain marked Not implemented and blocked from generation.

Out of scope for Phase 3A:

* New PTE renderers.
* New PTE evaluators.
* Practice Gym pre-generation pool redesign.
* Today background pre-generation redesign.
* MinIO audio lifecycle.
* Speaking, Vocabulary, or pattern prompt migration beyond compatibility gates.

## Phase 3B update: ExerciseType registry foundation

Phase 3B adds a central exercise-type registry. The registry reads the durable `ExerciseTypeDefinition` catalog and resolves each `exerciseType` key to renderer, evaluator, generation prompt, legacy `ActivityType`, and `ExercisePatternKey` compatibility metadata.

Generation now accepts `exerciseType` as the canonical future selector through `GET /api/activity/next?exerciseType=<key>`. Existing `type=` and `pattern=` routes remain supported. Planned PTE-style rows remain catalog-visible, but they are excluded from generation until `implementationStatus` is `ready`.

Today deterministic step selection now validates configured pattern keys through the registry's Today-ready view. Disabled or unready rows are removed before session creation. Practice Gym routes now prefer `exerciseType` query parameters for implemented cards. Skill-card routing still uses temporary safe defaults until dynamic skill-to-type selection is implemented.
