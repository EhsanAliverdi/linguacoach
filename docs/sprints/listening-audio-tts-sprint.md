# Listening Audio/TTS Sprint

## Goal

Make `ListeningComprehension` feel like a real listening activity by adding an audio playback layer while preserving the existing safe transcript reveal behaviour.

Students should be able to open a listening activity, play a short workplace audio message, answer comprehension questions, submit, then see the transcript, expected answer summaries, and feedback.

## Current State

The previous text MVP added `ListeningComprehension` as the third supported activity type. It generates a hidden `audioScript`, shows questions, evaluates answers deterministically, and reveals the transcript only after submission.

## Audio Generation And Storage

This sprint uses the existing `ITextToSpeechService` abstraction and upgrades it for listening audio generation.

Current implementation:

- `FakeTextToSpeechService` generates a deterministic local WAV placeholder.
- `ListeningAudioService` writes generated files to a configured local audio folder.
- Listening activity content stores audio metadata only: availability, storage key, content type, provider, voice, and duration metadata.
- The frontend receives only the authenticated audio endpoint URL, never the filesystem path.

This keeps the UI and backend access pattern ready for a real provider without making tests or local development depend on paid TTS.

## Provider And Config

Default provider: `Fake`

Configuration:

- `Tts:Provider` / `TTS_PROVIDER`
- `Tts:Enabled` / `TTS_ENABLED`
- `Tts:AudioStoragePath` / `TTS_AUDIO_STORAGE_PATH`
- `Tts:DefaultVoice` / `TTS_DEFAULT_VOICE`

The fake provider is suitable for development and automated tests. A real OpenAI, Azure, Google, or other TTS provider is deferred until provider choice and cost tracking are explicitly prioritised.

## Authenticated Audio Endpoint

Audio is served through:

```text
GET /api/activity/{activityId}/audio
```

Rules:

- Authentication is required.
- Module-owned activities are only accessible by the owning student.
- The endpoint returns only audio bytes with the content type.
- It does not return transcript text, expected answers, provider keys, or filesystem paths.

## Transcript Reveal Rules

Before submit:

- audio URL may be returned
- transcript/audioScript is hidden
- expected answers are hidden

After submit/history:

- transcript is visible
- question feedback and expected answer summaries are visible
- audio can remain playable

## Fallback Behaviour

If TTS is disabled or generation fails:

- the listening activity is still returned
- `audioAvailable` is false
- the UI shows a friendly text-based fallback note
- transcript remains hidden until submit
- the activity can still be completed using the text MVP flow

## Tests

Coverage added or updated:

- listening DTO includes safe audio metadata
- transcript and expected answers remain hidden before submit
- audio endpoint requires authentication
- owner can access module-owned listening audio
- another student cannot access module-owned listening audio
- audio endpoint returns `audio/wav`
- Playwright audio player rendering
- Playwright fallback note when audio is unavailable
- submission still reveals transcript
- no raw JSON or storage paths are exposed in the UI

## Limitations

- Audio is deterministic fake WAV audio, not real speech.
- No replay limits.
- No speed controls.
- No timed captions.
- No speech-to-text.
- No speaking role-play.
- No pronunciation feedback.
- No audio cleanup job yet.
- No admin audio usage reporting yet.

## Future Work

- Real provider-backed TTS.
- TTS cost and usage reporting.
- Audio file cleanup/caching policy.
- Replay limits.
- Audio speed controls.
- Timed captions.
- Speech-to-text response.
- Speaking role-play.
- Pronunciation feedback.
