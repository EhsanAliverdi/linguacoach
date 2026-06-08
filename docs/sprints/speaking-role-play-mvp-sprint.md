# Speaking Role-Play MVP Sprint

**Status:** Planned
**Date:** 2026-06-08
**Depends on:** Full app verification and disabled actions cleanup sprint (complete)

---

## Sprint goal

Introduce `SpeakingRolePlay` as the fourth supported `ActivityType`.

Students can open a workplace speaking scenario, record a short spoken response using the browser microphone, submit the recording, receive a transcript (via fake/dev STT), and get AI feedback on clarity, tone, structure, vocabulary, and workplace appropriateness.

---

## MVP scope

- SpeakingRolePlay activity generation (AI + deterministic fallback)
- Audio recording via browser MediaRecorder API
- Audio upload via separate multipart endpoint `POST /api/activity/{id}/speaking-attempt`
- Fake STT transcription (deterministic placeholder transcript)
- AI evaluation of transcript as workplace speaking (not pronunciation)
- ActivityAttempt saved with transcript, feedback JSON, score, and audio storage reference
- Audio playback in activity history (`GET /api/activity/{id}/attempts/{attemptId}/audio`)
- Dashboard Speaking card active, routes to `/activity?type=SpeakingRolePlay`
- Progress and history pages handle SpeakingRolePlay attempts
- Per-student audio file limit: 50 stored audio attempts (checked via DB count, not filesystem scan)

---

## What is NOT in scope

- Pronunciation scoring
- Word-level pronunciation feedback or fluency metrics
- Live conversation / turn-by-turn AI voice
- Real-time streaming
- Complex audio waveform UI
- Real STT provider (OpenAI Whisper, Azure, Google) — fake provider only
- Audio cleanup job (DB count limit provides short-term protection)
- Admin audio review
- Discriminated union ActivityDto refactor (deferred to post-sprint)
- Duration enforcement via audio decoding (MediaRecorder duration metadata is unreliable)

---

## Activity content model

Stored in `LearningActivity.AiGeneratedContentJson` as:

```json
{
  "activityType": "SpeakingRolePlay",
  "title": "Explain a delay to your manager",
  "scenario": "Your manager asks why a project task is delayed. Record a short professional response.",
  "studentRole": "Project Planner",
  "listenerRole": "Manager",
  "difficulty": "B1",
  "speakingGoal": "Explain the delay clearly and politely.",
  "prompt": "Record a 30–60 second response explaining the delay, the reason, and the next action.",
  "expectedPoints": [
    "mention the delay",
    "give a brief reason",
    "explain the next action",
    "use polite professional tone"
  ],
  "suggestedPhrases": [
    "I wanted to update you on...",
    "The delay is due to...",
    "I will follow up with..."
  ],
  "maxDurationSeconds": 60
}
```

---

## Audio upload and storage flow

```
Student records → stops → previews (<audio controls>) → clicks Submit
  ↓
POST /api/activity/{id}/speaking-attempt (multipart/form-data)
  fields: audioFile (IFormFile), durationSeconds? (optional)
  ↓
SpeakingAudioService.StoreTemporaryAsync(stream, tempKey=UUID)
  ↓
ISpeechToTextService.TranscribeAsync(stream, contentType)
  ↓
  STT failure? → delete temp file → return friendly error (no DB row created)
  ↓
SpeakingRolePlayEvaluator.EvaluateAsync(transcript, activityContent)
  ↓
  Eval failure? → delete temp file → return friendly error (no DB row created)
  ↓
SpeakingAudioService.CommitAsync(tempKey → attemptId key)
  ↓
ActivityAttempt saved with:
  - SubmittedContent = transcript
  - FeedbackJson = evaluation JSON
  - Score = numeric
  - AudioStorageKey = {attemptId:N}.webm (or .mp4 on Safari)
```

### Safari / cross-browser note

The Angular submit handler must read `mediaRecorder.mimeType` after recording and pass it in the multipart Content-Type. Server allowed types: `audio/webm`, `audio/wav`, `audio/mpeg`, `audio/mp4`. Do NOT hardcode `audio/webm`.

### Per-student audio limit

Before storing: count `ActivityAttempts` for this student where `AudioStorageKey IS NOT NULL`. If >= 50, return `400 Bad Request` with message:
> "Speaking history is full (50 recordings). Contact your teacher to clear old recordings."

Using DB count (not filesystem scan) avoids race conditions.

---

## STT provider

Interface:

```csharp
public interface ISpeechToTextService
{
    Task<SttResult> TranscribeAsync(
        Stream audio, string contentType, CancellationToken ct = default);
}

public sealed record SttResult(
    bool Success,
    string? Transcript,
    string Provider,
    long DurationMs,
    string? FailureReason);
```

MVP implementation: `FakeSpeechToTextService` returns a deterministic placeholder transcript:
> "I wanted to update you about the delay. The supplier is late, and I will send the revised timeline today."

Tests must use `FakeSpeechToTextService`. No test depends on a live STT provider.

Config:
- `Stt:Provider` / `STT_PROVIDER` (default: `Fake`)
- `Stt:Enabled` / `STT_ENABLED` (default: `true`)
- `Stt:MaxAudioMb` / `STT_MAX_AUDIO_MB` (default: `10`)
- `Speaking:AudioStoragePath` / `SPEAKING_AUDIO_STORAGE_PATH` (default: `./app-data/speaking-audio`)

---

## AI prompts

Two new prompt seeds:

### `activity_generate_speaking_roleplay`

Variables: `cefrLevel`, `careerContext`, `sourceLanguageName`, `targetLanguageName`, `recentMistakes`, `topicHint`

Output: JSON only (the activity content model shape above).

Rules: keep speaking tasks short; avoid sensitive/private content; avoid real company/person names; match career context; B1 = simple and practical; max duration 30–60 seconds.

### `activity_evaluate_speaking_roleplay`

Input: speaking scenario, expected points, student transcript, career context, CEFR level.

Output JSON:

```json
{
  "score": 72,
  "transcript": "...",
  "coachSummary": "Your response was clear and polite...",
  "strengths": ["clear opening", "polite tone"],
  "improvements": ["mention the next action earlier"],
  "missingExpectedPoints": ["specific next action"],
  "suggestedImprovedResponse": "I wanted to update you on...",
  "miniLesson": "When explaining a delay: situation + reason + next action.",
  "nextImprovementStep": "Try again and include when you will send the revised timeline."
}
```

Does NOT score pronunciation.

---

## Typed routing contract

`GET /api/activity/next?type=SpeakingRolePlay`

- Returns `SpeakingRolePlay` or deterministic fallback if AI fails
- Never silently returns WritingScenario, VocabularyPractice, or ListeningComprehension
- Not included in the automatic activity rotation (only served when explicitly requested)

**Prior learning applied:** [dotnet-typed-activity-silent-fallback] (confidence 10/10) — catch block must not silently return another type.

---

## Backend components

### New files
- `LinguaCoach.Application/Speaking/ISpeechToTextService.cs` (interface + SttResult)
- `LinguaCoach.Infrastructure/Activity/FakeSpeechToTextService.cs`
- `LinguaCoach.Infrastructure/Activity/SpeakingAudioService.cs`
- `LinguaCoach.Infrastructure/Activity/SpeakingRolePlayEvaluator.cs`

### Modified files
- `AiActivityGeneratorHandler.cs` — add SpeakingRolePlay to generation + evaluation guards
- `ActivityGetHandler.cs` — add SpeakingRolePlay branch (AI + fallback JSON + typed guard)
- `ActivitySubmitHandler.cs` — inject `ISpeechToTextService` + `SpeakingRolePlayEvaluator`; add SpeakingRolePlay dispatch
- `ActivityCommands.cs` (ActivityDto) — add 8 speaking fields: `SpeakingScenario`, `StudentRole`, `ListenerRole`, `SpeakingGoal`, `SpeakingPrompt`, `ExpectedPoints`, `SuggestedPhrases`, `MaxDurationSeconds`
- `ActivityCommands.cs` (ActivityFeedbackDto) — add 4 speaking fields: `SpeakingStrengths`, `SpeakingImprovements`, `MissingExpectedPoints`, `SuggestedImprovedResponse`
- `ActivityController.cs` — add `POST /api/activity/{id}/speaking-attempt` (multipart) + `GET /api/activity/{id}/attempts/{attemptId}/audio`
- `ActivityAttemptsHandler.cs` — add SpeakingRolePlay branch for history
- `ActivityAttempt` entity — add `AudioStorageKey` nullable column + migration

---

## Angular components

### New states in PageState union
```typescript
type PageState =
  | 'loading' | 'learning' | 'writing' | 'submitting' | 'feedback' | 'error'
  | 'permission-request' | 'ready' | 'recording' | 'recorded' | 'submitting-audio';
```

### UI states for speaking
1. **permission-request** — "Allow microphone access to record your response"
2. **ready** — scenario + prompt displayed, Record button
3. **recording** — Stop button, duration counter
4. **recorded** — `<audio controls>` preview + Submit button + Re-record button
5. **submitting-audio** — spinner
6. **feedback** — transcript + coach summary + strengths + improvements + missing points + suggested response + mini lesson

### MediaRecorder notes
- Read `mediaRecorder.mimeType` after recording starts; store it for use in submit Content-Type header
- `navigator.mediaDevices.getUserMedia` failure → show permission-denied state
- `MediaRecorder` not available → show unsupported-browser state with friendly message

---

## ActivityAttempt storage

SpeakingRolePlay attempt:
- `SubmittedContent` = transcript text
- `FeedbackJson` = speaking evaluation JSON
- `Score` = numeric 0–100
- `AudioStorageKey` = `{tempUUID}.ext` committed to `{attemptId:N}.ext` after full success

Audio file naming: `{attemptId:N}.webm` (or `.mp4` on Safari based on mimeType).

---

## Activity history

`/activity/{activityId}/history` for SpeakingRolePlay:
- Attempt number, date, score, transcript
- Audio playback: `<audio controls src="/api/activity/{id}/attempts/{attemptId}/audio">`
- Coach summary, strengths, improvements, missing expected points, suggested response, mini lesson

---

## Dashboard update

- Speaking card: remove "Coming soon", enable click, route to `/activity?type=SpeakingRolePlay`
- Pronunciation card: remains "Coming soon"

---

## Data flow diagram

```
Dashboard
  └── Speaking card → /activity?type=SpeakingRolePlay
        ↓
  ActivityController.GetNext(type=SpeakingRolePlay)
        ↓
  ActivityGetHandler.ResolveActivityTypeAsync → SpeakingRolePlay (explicit)
        ↓
  AiActivityGeneratorHandler.GenerateActivityContentAsync(SpeakingRolePlay)
        ├── success → LearningActivity saved → ActivityDto (speaking fields)
        └── failure → BuildSpeakingFallbackJson → LearningActivity saved → ActivityDto
        ↓
  Angular: render scenario + prompt + record button
        ↓
  Student records (MediaRecorder) → stops → previews
        ↓
  POST /api/activity/{id}/speaking-attempt (multipart)
        ↓
  SpeakingAudioService.StoreTemporaryAsync (tempKey)
        ↓
  ISpeechToTextService.TranscribeAsync
        ├── failure → delete file → 400 error
        └── success → transcript
              ↓
        SpeakingRolePlayEvaluator.EvaluateAsync (AI)
              ├── failure → delete file → 400 error
              └── success → feedback JSON
                    ↓
              SpeakingAudioService.CommitAsync (tempKey → attemptId)
                    ↓
              ActivityAttempt saved
                    ↓
              ActivityFeedbackDto returned
                    ↓
        Angular: render transcript + feedback
```

---

## Tests

### Backend integration tests
New file: `SpeakingRolePlayActivityTests.cs`

- `GetNext_SpeakingRolePlay_ReturnsSpeakingRolePlayType`
- `GetNext_SpeakingRolePlayExplicit_DoesNotReturnAnotherType`
- `GetNext_SpeakingRolePlay_FallbackJson_HasExpectedFields`
- `SpeakingAttempt_Unauthenticated_Returns401`
- `SpeakingAttempt_WrongStudent_ReturnsBadRequest`
- `SpeakingAttempt_InvalidFileType_ReturnsBadRequest`
- `SpeakingAttempt_OversizedFile_ReturnsBadRequest`
- `SpeakingAttempt_ExceedsPerStudentLimit_ReturnsBadRequest`
- `SpeakingAttempt_Valid_FakeSTT_ReturnsFeedbackWithTranscript`
- `SpeakingAttempt_Valid_SavesToActivityAttempt`
- `SpeakingAudio_Unauthenticated_Returns401`
- `SpeakingAudio_WrongOwner_Returns404`
- `SpeakingAudio_ValidOwner_ReturnsBytesWithCorrectContentType`
- `Progress_AfterSpeakingAttempt_CountsCorrectly`
- `History_SpeakingAttempt_ReturnsTranscriptAndFeedback`

### Angular unit tests
New file: `activity-lesson-speaking.component.spec.ts` (or extend existing)

- SpeakingRolePlay activity renders scenario and prompt
- Microphone unsupported state renders friendly message
- Permission denied state renders friendly message
- Recording controls (record/stop) render in correct states
- Audio preview appears after stop (mocked MediaRecorder)
- Submit calls speaking-attempt endpoint
- Feedback renders transcript, coach summary, strengths, improvements
- History page renders speaking attempt with audio player

### Playwright E2E tests
New file: `e2e/speaking-role-play-activity.spec.ts`

- Dashboard Speaking card is active and opens SpeakingRolePlay
- Activity renders scenario and speaking prompt
- Mocked MediaRecorder allows record → stop → preview flow
- Submit sends audio and returns feedback
- Transcript appears in feedback view
- History page renders speaking attempt
- Pronunciation card remains "Coming soon"
- No unexpected console errors

---

## Configuration

`appsettings.json` / `appsettings.Development.json`:

```json
{
  "Stt": {
    "Provider": "Fake",
    "Enabled": true,
    "MaxAudioMb": 10
  },
  "Speaking": {
    "AudioStoragePath": "./app-data/speaking-audio",
    "MaxAudioFilesPerStudent": 50
  }
}
```

`.env.example` additions:
```
STT_PROVIDER=Fake
STT_ENABLED=true
STT_MAX_AUDIO_MB=10
SPEAKING_AUDIO_STORAGE_PATH=./app-data/speaking-audio
SPEAKING_MAX_AUDIO_FILES_PER_STUDENT=50
```

---

## Failure mode inventory

| Failure | Has test | Has error handling | User sees |
|---|---|---|---|
| AI generation fails | Yes | Inline fallback JSON | Activity loads anyway |
| STT fails | Yes | Delete temp file, 400 | "Could not transcribe. Please try again." |
| AI evaluation fails after STT | Yes | Delete temp file, 400 | "Could not evaluate. Please try again." |
| File too large | Yes | 400 before storage | "Recording too large." |
| Invalid file type | Yes | 400 before storage | "Invalid audio format." |
| Per-student limit hit | Yes | 400 before storage | "Speaking history full. Contact teacher." |
| Wrong student submits | Yes | 400 ownership check | "Activity not found." |
| Audio playback wrong owner | Yes | 404 ownership check | 404 |
| Microphone permission denied | Yes (Angular) | Permission-denied UI state | Friendly message |
| Browser unsupported | Yes (Angular) | Unsupported UI state | "Use Chrome, Firefox, or Safari." |
| Safari audio/mp4 | N/A (dynamic mimeType) | MediaRecorder.mimeType read | Works correctly |

---

## Deferred future work

- Real STT provider (OpenAI Whisper, Azure Speech-to-Text)
- Pronunciation scoring
- Word-level pronunciation feedback
- Fluency metrics (speech speed, pauses)
- Real-time role-play conversation
- Turn-by-turn AI voice conversation
- Teacher/admin speaking review
- Audio cleanup job (delete files older than 90 days)
- Audio waveform visualisation
- Discriminated union ActivityDto refactor

---

## Final checks

```bash
dotnet test LinguaCoach.slnx   # must pass all tests
npm run build                  # must pass
npx playwright test            # must pass all 49 existing + new speaking tests
```

Manual verification:
- Dashboard → Speaking card → SpeakingRolePlay renders
- Microphone permission flow
- Record → stop → preview
- Submit → transcript → feedback
- History page → speaking attempt visible
- Progress page → attempt counted
