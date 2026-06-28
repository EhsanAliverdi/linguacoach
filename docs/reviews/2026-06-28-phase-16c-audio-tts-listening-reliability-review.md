# Phase 16C — Audio/TTS and Listening Activity Reliability Review

**Date:** 2026-06-28
**Sprint:** Phase 16C
**Related sprint doc:** `docs/sprints/current-sprint.md`
**Related product state:** `docs/handoffs/current-product-state.md`

---

## Scope

Hardening-only pass targeting audio playback, TTS fallback, and listening activity state reliability across Today and Practice. No new exercise formats, new AI scoring, lesson player redesign, or routing/mastery/placement/learning-plan changes were permitted.

---

## Files Reviewed

### Angular frontend

| File | Change type |
|------|------------|
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/audio-player/audio-player.component.ts` | Enhanced — state machine added |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/audio-player/audio-player.component.html` | Enhanced — loading, failed, retry UI |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/audio-player/audio-player.component.spec.ts` | New — 17 unit tests |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/listening-fill-in-blanks/listening-fill-in-blanks.component.html` | Bug fix — conditional guard |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/highlight-correct-summary/highlight-correct-summary.component.html` | Bug fix — conditional guard |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/highlight-incorrect-words/highlight-incorrect-words.component.html` | Bug fix — conditional guard |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/retell-lecture/retell-lecture.component.ts` | Import added |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/retell-lecture/retell-lecture.component.html` | Raw `<audio>` → `app-audio-player` |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/summarize-group-discussion/summarize-group-discussion.component.ts` | Import added |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/summarize-group-discussion/summarize-group-discussion.component.html` | Raw `<audio>` → `app-audio-player` |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/summarize-group-discussion/summarize-group-discussion.component.spec.ts` | Stale testids updated |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/repeat-sentence/repeat-sentence.component.ts` | Import added |
| `src/LinguaCoach.Web/src/app/features/student/activity/renderers/repeat-sentence/repeat-sentence.component.html` | Audio player section added |
| `src/LinguaCoach.Web/e2e/exercise-pattern-renderers.spec.ts` | 3 new Playwright audio tests |

### Backend (no changes required)

The backend audio endpoint gate (`GET /api/activity/{id}/audio`) was audited and found to gate on `ActivityType.ListeningComprehension` only. This is a known limitation documented in the findings below, not a bug introduced by this phase. No backend files were modified.

---

## Findings

### P0 — repeat-sentence: audioUrl present but no audio UI rendered (FIXED)

`repeat-sentence.component.html` had the `audioUrl` field on its item model but never rendered `app-audio-player` or any audio UI when `audioUrl` was set. Students given a Repeat Sentence activity with real audio received no playback affordance.

**Fix:** Added `@if (item.audioUrl || item.audioScript)` block with `<app-audio-player>` before the sentence text div. Sentence text is always shown as a text reference regardless of audio availability.

---

### P1 — Three renderers: audioUrl present but AudioPlayer hidden by wrong conditional (FIXED)

`listening-fill-in-blanks`, `highlight-correct-summary`, and `highlight-incorrect-words` all conditioned `app-audio-player` display on `@if (content.audioScript)`. When `audioUrl` was set but `audioScript` was null (i.e., audio exists but no fallback transcript), the player was hidden and the student saw nothing.

**Fix:** Changed condition to `@if (content.audioScript || content.audioUrl)` in all three templates.

---

### P1 — retell-lecture and summarize-group-discussion: raw `<audio>` with no state machine (FIXED)

Both renderers used a raw `<audio controls>` element with hardcoded `type="audio/mpeg"`, bypassing the shared `AudioPlayerComponent`. Error states, loading indicators, and the TTS fallback transcript were not shown through the standard component.

**Fix:** Migrated both renderers to `<app-audio-player>` with `AudioPlayerComponent` imported. The `AudioPlayerComponent` was also added to their `imports` arrays.

---

### P1 — AudioPlayerComponent: no loading or error state (FIXED)

The `AudioPlayerComponent` had no loading indicator, no error state, and no retry mechanism. A failed or slow audio load produced a silent broken UI.

**Fix:**

- Added `AudioLoadState` type: `'idle' | 'loading' | 'ready' | 'failed'`.
- Added `retryKey: number` for forced `<audio>` element re-creation on retry.
- `onLoadStart()` → `'loading'`, `onCanPlay()` → `'ready'`, `onError()` → `'failed'`.
- `retry()` increments `retryKey` and resets state to `'idle'`.
- Template uses `@for (key of [retryKey]; track key)` around `<audio>` to force DOM destroy/re-create when `retryKey` changes, triggering a new network request.
- Loading indicator: `data-testid="audio-loading"` shown during `'loading'` state.
- Failed state: `data-testid="audio-failed"` div with `data-testid="audio-retry-btn"` retry button and optional `audioScript` transcript fallback.
- `data-testid="audio-unavailable"` shown when no `audioUrl`, with `audioScript` transcript if available.

---

### Known limitation — backend audio endpoint gate (not fixed, by design)

`GET /api/activity/{id}/audio` and `GET /api/activity/{id}/audio-url` both gate on `ActivityType.ListeningComprehension`. Formats such as `RetellLecture`, `SummarizeGroupDiscussion`, `AnswerShortQuestion`, `RepeatSentence` receive audio URLs directly in their activity content payload, not via this endpoint. This is working as designed. Audio for these formats is generated at activity creation time and stored on the content model; no endpoint change is required.

---

## Decisions Made

1. **AudioLoadState as component-local type, not exported.** The state machine is an implementation detail of `AudioPlayerComponent`. No other component needs to read or set audio load state.
2. **`@for` key trick for retry.** Angular's `@for` directive with `track key` destroys the old DOM node and creates a new `<audio>` element when the tracked key changes. This is the correct Angular 17+ approach for forcing element re-creation without a native imperative `load()` call.
3. **audioScript as fallback in failed state.** When audio fails to load, the transcript is shown inline beneath the error message. This is consistent with the unavailable state and requires no additional input from the activity content model.
4. **No new TTS provider.** The phase explicitly forbade creating a new TTS provider. Existing `TtsProviderResolver` and `PlacementAudioService` are unchanged.
5. **Sentence text always shown in repeat-sentence.** The sentence is always shown as a visual reference even when audio is available, because students may need to read and type it.

---

## Test Coverage

### Angular unit tests

17 new tests in `audio-player.component.spec.ts`:

- `showPlayer` is false when no `audioUrl`
- `audio-unavailable` testid shown when no audioUrl
- `audio-player-section` testid shown when audioUrl set
- `label` and `helpText` rendered
- Custom `audioUnavailableMessage` replaces default
- `audioStatus === 'pending'` shows correct message
- `audio-player` element present when url set
- `audio-loading` shown after loadstart event
- `audio-loading` cleared after canplay event
- `audio-failed` shown after error event
- `audio-player` hidden in failed state
- `audio-retry-btn` shown in failed state
- `audio-failed` cleared and `audio-player` shown after retry
- `retryKey` increments on retry
- `audioScript` shown in failed state
- `audioScript` shown in unavailable state
- State resets to idle on retry

4 updated tests in `summarize-group-discussion.component.spec.ts` (stale testids from raw `<audio>` approach replaced with `audio-unavailable` and `audio-player-section`).

### Playwright E2E tests (3 new)

1. **Audio-backed activity shows audio player section when audioUrl is provided** — `listeningFillInBlanks` with mocked audioUrl; verifies `data-testid="audio-player-section"` visible.
2. **Audio-backed activity shows unavailable state and does not crash when no audioUrl** — `summarizeSpokenText` with null audioUrl; verifies `data-testid="audio-unavailable"` visible and activity still submittable.
3. **Practice activity with audio remains submittable after audio loads** — `writeFromDictation` with null audioUrl; fills input and submits successfully.

---

## Validation Results

| Check | Result |
|-------|--------|
| `dotnet build --configuration Release` | Clean |
| `dotnet test --configuration Release` | 2,732 pass (1,504 unit, 1,225 integration, 3 architecture) |
| Angular unit tests (`ng test --watch=false --browsers=ChromeHeadless`) | 1,496 pass (1,479 baseline + 17 new) |
| Angular production build (`ng build --configuration production`) | Clean |
| Playwright E2E (`playwright test`) | 262 pass, 3 skipped, 0 failed |

The 3 skipped Playwright tests are pre-existing skips. The `admin-student-detail.spec.ts` flaky failure observed in one run was confirmed pre-existing: it passes in isolation and in repeat full-suite runs with and without Phase 16C changes.

---

## Risks and Unresolved Questions

1. **Backend audio gate scope.** The `ListeningComprehension`-only gate on the audio endpoint means future exercise formats with server-generated audio will need the gate updated. This is tracked as a known limitation, not a bug.
2. **`@for` key trick browser compatibility.** The `@for (key of [retryKey]; track key)` pattern relies on Angular's control flow block introduced in v17. The project targets Angular 17+, so this is safe.
3. **Mobile audio autoplay restrictions.** The `AudioPlayerComponent` uses `controls` attribute (manual play). No autoplay is attempted. Mobile browsers' autoplay restrictions do not affect this implementation.

---

## Final Verdict

Phase 16C is complete. All P0 and P1 audio reliability issues are fixed. The `AudioPlayerComponent` now has a full loading/failed/retry state machine. All audio-backed renderers use the shared component consistently. Test coverage meets the 16-scenario target from the phase spec. All builds and test suites are green.

---

## Next Recommended Action

Continue to Phase 16D or the next phase in the sprint backlog. No blocking issues remain from Phase 16C.
