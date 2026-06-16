# Phase 8P — Audio Lifecycle for All Listening Pattern Keys — Implementation Review

**Date:** 2026-06-16
**Related sprint:** Pattern Engine / Listening audio lifecycle
**Status:** Complete

---

## Summary

Phase 8P wires the TTS audio generation pipeline for all 9 listening exercise pattern keys. Before this phase, `EnsureAudioAsync` was only called for legacy `ListeningComprehension` activities generated via `HandleLegacyActivityTypeAsync`. Pattern-keyed activities created via `HandlePatternKeyedAsync` (all 7 new listening formats plus the 2 original legacy listening patterns) never had audio generated, so every activity returned `audioUrl: null`.

---

## Files reviewed

- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`
- `src/LinguaCoach.Infrastructure/Activity/ListeningAudioService.cs`
- `src/LinguaCoach.Application/Activity/ActivityCommands.cs`
- `src/LinguaCoach.Web/src/app/core/models/activity.models.ts`
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/audio-and-free-text/*`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/audio-and-gap-fill/*`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/listening-fill-in-blanks/*`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/highlight-correct-summary/*`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/highlight-incorrect-words/*`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs` (read-only, confirmed ActivityType)

---

## Key findings

### Pre-condition confirmed
All 9 listening pattern keys are seeded with `ActivityType.ListeningComprehension`. The existing guard in `ListeningAudioService.EnsureAudioAsync` (`if (activity.ActivityType != ActivityType.ListeningComprehension) return;`) therefore does NOT block any of these patterns. No change to the guard was required.

### Root cause
`HandlePatternKeyedAsync` saved the activity and returned `MapToDto` without ever calling `EnsureAudioAsync`. Audio was generated for legacy listening activities via `HandleLegacyActivityTypeAsync` (line 388) but not for pattern-keyed ones.

### `MapToDto` coverage
The `ListeningComprehension` branch in `MapToDto` (lines 653–718) already extracts `audio` metadata from `AiGeneratedContentJson` and populates `AudioUrl`, `AudioAvailable`, etc. Since all listening patterns use `ActivityType.ListeningComprehension`, they were already covered — the only missing piece was `EnsureAudioAsync` never being called.

---

## Changes made

### Backend

1. **`ActivityGetHandler.cs`** — added `ListeningPatternKeys` static `HashSet<string>` (9 keys) and wired `EnsureAudioAsync` + `SaveChangesAsync` in `HandlePatternKeyedAsync` after activity creation.

2. **`ActivityCommands.cs`** — added `string? AudioStatus = null` to `ActivityDto` record.

3. **`ActivityGetHandler.MapToDto`** — sets `AudioStatus` in the `ListeningComprehension` branch: `"ready"` when audio is available, `"unavailable"` when audio failed, `"pending"` when audio metadata is absent.

### Angular

4. **`activity.models.ts`** — added `audioStatus: string | null` to `ActivityDto` interface.

5. **`audio-player` component** — created new shared component (`audio-player.component.ts` + `.html`) with inputs: `audioUrl`, `audioStatus`, `audioUnavailableMessage`, `audioScript`, `label`, `helpText`. Shows `<audio>` player when `audioUrl` is set; shows fallback with optional script text otherwise.

6. **`exercise-renderer.component.ts`** — the three getters (`listeningFillInBlanksContent`, `highlightCorrectSummaryContent`, `highlightIncorrectWordsContent`) now fall back to `this.activity.audioUrl` when `ed['audioUrl']` is absent.

7. **Renderer HTML templates** — all 5 listening renderers (`audio-and-free-text`, `audio-and-gap-fill`, `listening-fill-in-blanks`, `highlight-correct-summary`, `highlight-incorrect-words`) now use `<app-audio-player>` instead of inline `<audio>` tags.

8. **Renderer TS files** — `AudioPlayerComponent` imported and added to `imports[]` array in all 5 renderer components.

---

## Tests

- New file: `tests/LinguaCoach.UnitTests/Activity/ListeningAudioServiceTests.cs`
- 15 new unit tests covering:
  - Guard: non-listening types are no-ops
  - Missing/empty `audioScript` sets unavailable metadata
  - Already-available audio is skipped (idempotent)
  - `ActivityDto.AudioStatus` field existence and accepted values
  - All 9 listening pattern keys cause audio metadata to be written

### Test counts
| Suite | Before | After |
|---|---|---|
| Unit | 655 | 803 |
| Integration | 479 | 479 |
| Architecture | 3 | 3 |

---

## Decisions made

- **No guard change in `ListeningAudioService`** — all 9 patterns already use `ActivityType.ListeningComprehension`, so the existing guard was sufficient.
- **Shared `audio-player` component** — eliminates duplicated audio player HTML across 5 renderer templates.
- **`audioUrl` fallback in exercise-renderer getters** — `ed['audioUrl'] ?? raw['audioUrl'] ?? this.activity.audioUrl` so that audio works whether it comes from the content JSON or the top-level API field.
- **`write_from_dictation` not changed** — per-sentence audio is a different pattern; this phase only affects the shared passage/clip audio field.

---

## Risks and unresolved questions

- `write_from_dictation` has per-sentence `audioUrl` items in its content JSON. Phase 8P does not address per-sentence TTS generation. The passage-level `audioScript` will get audio, but the per-sentence clips still return null.
- `summarize_spoken_text` and speaking formats remain non-runnable and are not affected.

---

## Documentation impact

- `docs/handoffs/current-product-state.md` — updated with Phase 8P entry.
- `docs/reviews/2026-06-16-phase-8p-audio-lifecycle-review.md` — this file.

---

## Next recommended action

Verify audio plays correctly in the browser for `listening_multiple_choice_single`, `listening_fill_in_blanks`, and `highlight_correct_summary` once a TTS provider is configured. No further backend work is required for the audio lifecycle of the 7 new listening formats.
