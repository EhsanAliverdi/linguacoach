# Phase 16D — Voice Recording and Speaking Submission Foundation

**Date:** 2026-06-28
**Sprint:** Phase 16D
**Review type:** Implementation review
**Reviewer:** Claude Sonnet 4.6

---

## Files changed

### Backend
- `src/LinguaCoach.Api/Controllers/ActivityController.cs` — new `SubmitAudioAttempt` endpoint; removed ActivityType constraint from `GetSpeakingAudio`

### Frontend
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/voice-recorder/voice-recorder.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/voice-recorder/voice-recorder.component.html` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/audio-response/audio-response.component.ts` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/audio-response/audio-response.component.html` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/exercise-renderer/exercise-renderer.component.html`
- `src/LinguaCoach.Web/src/app/core/services/activity.service.ts`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-lesson/activity-lesson.component.ts`

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/AudioAttemptEndpointTests.cs` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/voice-recorder/voice-recorder.component.spec.ts` (new)
- `src/LinguaCoach.Web/src/app/features/student/activity/renderers/audio-response/audio-response.component.spec.ts` (new)

---

## Summary of changes

### Backend — `POST /api/activity/{id}/audio-attempt`

New endpoint matching `SubmitSpeakingAttempt` validation pattern but without STT or AI:

1. Auth check (`GetCurrentUserId()`)
2. File presence and size check (10 MB limit via `SpeakingAudioService.GetMaxAudioBytes()`)
3. MIME type validation (`IsAllowedMimeType`: webm/wav/mp3/mp4/m4a/ogg)
4. Student profile lookup
5. Activity existence check (`IsActive`)
6. Ownership check via LearningModule → LearningPath join (if `LearningModuleId` is set)
7. Per-student storage limit check (50 files via `ExceedsStorageLimitAsync`)
8. Store to temp key → create `ActivityAttempt` → commit to final key → update storage key
9. Return `ActivityFeedbackDto` with all null/empty fields

The `ActivityAttempt` is created with `feedbackJson="{}"`, `submittedContent="[voice recording]"`, `promptKey="audio_submission_pending"`, `score=null`. The frontend's `hasFeedbackContent` getter returns `false` for this DTO, causing the Phase 16B "feedback pending" card to display automatically without any additional frontend logic.

Temp-key cleanup is handled by the same `catch (Exception) when (tempKey is not null)` guard as `SubmitSpeakingAttempt`.

**`GetSpeakingAudio` change:** The `ActivityType.SpeakingRolePlay` constraint was removed so audio recorded via `audio-attempt` (which is not restricted by activity type) can also be retrieved. Ownership is enforced at the attempt level (AttemptId + LearningActivityId + StudentProfileId match). No security regression.

### Frontend — VoiceRecorderComponent

Single-responsibility component owning the full MediaRecorder lifecycle:

- `RecorderState` union: `'idle' | 'requesting-permission' | 'permission-denied' | 'unsupported' | 'recording' | 'recorded'`
- `initialState()` checks `navigator.mediaDevices?.getUserMedia` at construction time; returns `'unsupported'` if unavailable
- `startRecording()`: sets `requesting-permission` synchronously, then awaits `getUserMedia`. On rejection, sets `permission-denied` and returns. On success, creates `MediaRecorder` with `preferredMimeType()` fallback chain (webm+opus → webm → ogg+opus → mp4)
- `stopRecording()`: stops recorder if recording, calls `releaseStream()` (stops all tracks, nulls `_stream`)
- `reRecord()`: revokes preview object URL, resets to `'idle'`
- `ngOnDestroy()`: stops recorder if active, releases stream, revokes preview URL — prevents microphone light staying on after navigation

### Frontend — AudioResponseComponent

Thin orchestration component: wraps `VoiceRecorderComponent`, holds `recordedAudio` signal, shows submit button only after `(recorded)` fires. Delegates all MediaRecorder logic to VoiceRecorderComponent.

### ExerciseRendererComponent wiring

- `ExerciseAnswerPayload` union extended with `{ kind: 'audioResponse'; blob: Blob; mimeType: string; durationSeconds: number }`
- `audioResponseContent` getter maps `prompt` and `situation` from staged exercise data, raw JSON, or activity-level fields (same fallback chain as `freeTextContent`)
- `@case ('audioResponse')` added before `@case ('freeTextEntry')` in the HTML switch
- Both Today Lesson and Practice Gym paths hit `onRendererSubmit` in `ActivityLessonComponent`, which now has an early-return for `audioResponse` calling `submitAudioAttempt`

---

## Scope compliance

The following were explicitly excluded and remain excluded:

| Excluded item | Status |
|---|---|
| AI speaking evaluation | Not implemented |
| Pronunciation scoring | Not implemented |
| Speech-to-text | Not implemented |
| New activity formats | Not implemented |
| Transcript generation | Not implemented |
| `read_aloud`, `repeat_sentence`, `respondToSituation` via audio | Not implemented (text path unchanged) |

The `audioResponse` interactionMode was already present in the TypeScript union. This phase wired up the renderer for it.

---

## Security review

| Check | Result |
|---|---|
| Auth required | `GetCurrentUserId()` returns Unauthorized if not authenticated |
| File size enforced | `SpeakingAudioService.GetMaxAudioBytes()` (10 MB config) |
| MIME type validated | `IsAllowedMimeType()` — only webm/wav/mp3/mp4/m4a/ogg |
| Student ownership verified | LearningModule → LearningPath → StudentProfile join |
| Storage limit enforced | `ExceedsStorageLimitAsync()` — 50 files per student |
| Audio retrieval ownership | Attempt must match StudentProfileId (existing logic, unchanged) |
| No storage key in response | `ActivityFeedbackDto` does not include any storage path |
| No filesystem path leakage | Verified by `AudioAttemptEndpointTests.AudioAttempt_ResponseDoesNotExposeStorageKey` |
| Temp file cleaned on error | `catch (Exception) when (tempKey is not null)` deletes temp key |

---

## Test results

| Suite | Before | After | Delta |
|---|---|---|---|
| Angular unit | 1,496 | 1,519 | +23 |
| Backend integration | 1,225 | 1,234 | +9 |
| Backend unit | 2,732 | 2,732 | 0 |
| Playwright E2E | 262 | 262 | 0 |
| Production build | pass | pass | — |

---

## Decisions made

1. **No ActivityType restriction on `audio-attempt`**: The endpoint accepts any activity type. The `audioResponse` interactionMode on an activity is sufficient context. ActivityType restriction would require a new enum value or a whitelist, both of which are premature.

2. **`GetSpeakingAudio` ActivityType constraint removed**: The constraint was added when `speaking-attempt` was SpeakingRolePlay-only. Now that audio can come from any activity type via `audio-attempt`, the constraint would have blocked audio retrieval. Ownership is enforced at the attempt level, which is the correct security boundary.

3. **Shared exercise-renderer path**: Both Today and Practice Gym use the same `ExerciseRendererComponent`, so wiring `audioResponse` there covers both without any duplication. No presenter or page-level changes required.

4. **Pending feedback via existing mechanism**: Phase 16B's `hasFeedbackContent=false` already shows the pending card. Returning an empty `ActivityFeedbackDto` requires no frontend changes to the feedback page.

5. **`VoiceRecorderComponent` as separate component**: MediaRecorder state could have been inlined into `AudioResponseComponent`, but isolating it enables future reuse (e.g. if lesson-level recording UX is needed) and makes the recorder testable independently.

---

## Risks and unresolved questions

- **Manual test coverage**: No Playwright tests were added for `audioResponse`. Browser microphone access in Playwright requires `--use-fake-ui-for-media-stream` flag and is non-trivial to set up. The component states are covered by unit tests. A future sprint could add Playwright coverage using the Chromium fake media stream flag.
- **Safari compatibility**: Safari supports `audio/mp4` but not `audio/webm`. The `preferredMimeType()` fallback chain includes `audio/mp4`. Untested on Safari.
- **Maximum duration**: No server-side duration enforcement. Client-side duration is passed as `durationSeconds` form field but is not validated or stored. A future sprint could enforce a maximum duration.

---

## Final verdict

Implementation complete and correct. All security checks present. No scope creep. Tests green. Pending card renders automatically via existing Phase 16B infrastructure.

## Next recommended action

Phase 16E or next product priority. The `audioResponse` interactionMode is now fully wired. Future work: AI evaluation of audio submissions (call `SubmitSpeakingAttempt` pattern), or Playwright test coverage for the recorder flow.
