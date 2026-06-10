---
status: historical
---
# ListeningComprehension Activity Sprint - Text-Based MVP

## Current State

SpeakPath supports `WritingScenario` and `VocabularyPractice` through the shared `LearningPath -> LearningModule -> LearningActivity -> ActivityAttempt` model. `ListeningComprehension` exists in the domain enum but was not wired into generation, submission, UI rendering, history, or progress.

## Product Goal

Introduce a first listening comprehension activity without real audio. The learner practises understanding a realistic workplace voice message, answering key-detail questions, and optionally writing a short professional response.

This is text-based first because it validates the activity model, transcript reveal rules, question scoring, and user flow before adding TTS costs, audio storage, playback controls, or speech tooling.

## Architecture Decisions

- Use the existing `LearningActivity.AiGeneratedContentJson` activity content pattern.
- Keep `ListeningComprehension` inside the existing activity endpoint and attempt flow.
- Generate listening content through `activity_generate_listening` using existing AI fallback and usage tracking.
- Use a deterministic fallback listening activity when AI generation fails.
- Evaluate the MVP deterministically: short-answer token matching plus a simple response-task check.
- Do not update student memory or vocabulary extraction for listening attempts in this sprint.

## Activity Content Model

Listening content includes title, scenario, instructions, speaker/listener roles, difficulty, hidden `audioScript`, transcript reveal flag, 2-4 short-answer questions, expected answers, and an optional response task.

Before submission, API responses must not expose:

- `audioScript`
- transcript text
- `expectedAnswer`

After submission, feedback may include:

- score
- coach summary
- per-question feedback
- expected answer summaries
- transcript
- response feedback
- mini lesson
- next improvement step

## UI Behaviour

The `/activity` page renders a dedicated Listening badge and asks the learner to imagine they listened to the workplace message. It shows the scenario, speaker/listener context, questions, and optional response task. The transcript is hidden with the message "Transcript unlocks after you answer."

After submit, the feedback view shows score, question feedback, expected answer summaries, response feedback, and the transcript.

## Selection Rule

The conservative MVP rule is:

- every 4th attempt may be `VocabularyPractice` when enough vocabulary exists
- otherwise every 5th attempt is `ListeningComprehension`
- otherwise return `WritingScenario`

Vocabulary practice keeps priority over listening when both conditions match.

## API Changes

- `GET /api/activity/next` can return `activityType = listeningComprehension`.
- `ActivityDto` includes listening fields without transcript or expected answers.
- `POST /api/activity/{activityId}/attempt` accepts listening answers by `questionId` plus optional `responseText`.

## DB Changes

No migration is required. `ActivityType.ListeningComprehension` already exists and content is stored in `LearningActivity.AiGeneratedContentJson`.

## Tests

Planned and implemented focused backend coverage:

- selection can return `ListeningComprehension`
- transcript and expected answers are not exposed before submit
- submitting listening answers creates an `ActivityAttempt`
- deterministic scoring returns a score
- transcript appears in feedback after submit
- module-owned activity submission is protected from another student

Frontend coverage should cover rendering, hidden transcript, submission payload, and feedback display.

## Limitations

- No real audio playback.
- No TTS provider integration.
- No replay limits.
- No listening speed controls.
- No transcript timing or captions.
- No speech-to-text response.
- No pronunciation feedback.
- Listening-specific memory and skill profile updates are deferred.

## Future Work

- Generated audio files.
- TTS provider integration.
- Audio player UI.
- Replay controls.
- Transcript reveal after audio playback.
- Timed captions.
- Listening-specific memory updates.
- Listening skill profile.
- Spoken response to listening tasks.
