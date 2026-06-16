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

### Root causes (actual, post-implementation)

**Bug 1 — `EnsureAudioAsync` type guard too narrow.**
The guard was `if (activity.ActivityType != ActivityType.ListeningComprehension) return;`. This blocked all pattern-keyed activities because they have no `ActivityType` difference — but the guard fired before checking the pattern key. The fix replaces the guard with:
- `isLegacyListening` = `ActivityType == ListeningComprehension && no patternKey`
- `isListeningPatternKeyed` = `patternKey` is one of the 9 known listening keys
If neither is true, return early. This is now an explicit allowlist.

**Bug 2 — `HandleAsync(GetActivityByIdQuery)` missed pattern-keyed listening.**
The by-ID path only called `EnsureAudioAsync` for `ActivityType.ListeningComprehension` without checking the pattern key. Fixed to also trigger for the 9 listening pattern keys.

**Bug 3 — `audio` block leaked into `ContentJson`.**
After `EnsureAudioAsync` writes `{"audio": {"storageKey": "..."}}` at the root of `AiGeneratedContentJson`, the `rendererContentJson` returned to the client via `ContentJson` included `storageKey`. Added `StripAudioFromContentJson()` helper that removes the `audio` property before setting `rendererContentJson`. Audio is already surfaced via top-level `audioUrl`/`audioStatus` fields on `ActivityDto`.

**Bug 4 — Angular test helpers missing `audioStatus`.**
`ActivityDto` interface requires `audioStatus: string | null`. Two test files omitted it, causing a TypeScript compile error. Added `audioStatus: null` to both.

### `MapToDto` coverage
The `ListeningComprehension` branch already extracts `audio` metadata and populates `AudioUrl`, `AudioAvailable`, `AudioStatus` etc. Since all listening patterns use `ActivityType.ListeningComprehension`, they hit this branch — no changes needed to `MapToDto` itself.

---

## Changes made

### Backend

1. **`ListeningAudioService.cs`** — replaced the `ActivityType != ListeningComprehension` guard with an explicit allowlist: legacy listening (type = LC, no pattern key) OR pattern-keyed listening (pattern key in `ListeningPatternKeys`). Added `ListeningPatternKeys` static set with all 9 keys.

2. **`ActivityGetHandler.cs`** — fixed `HandleAsync(GetActivityByIdQuery)` to also call `EnsureAudioAsync` for pattern-keyed listening activities. Added `StripAudioFromContentJson()` helper and used it in `MapToDto` to prevent `storageKey` leaking through `ContentJson`.

### Angular

3. **`presenters/test-helpers.ts`** — added `audioStatus: null` to `makeActivity()` factory.

4. **`activity-lesson-vocab.component.spec.ts`** — added `audioStatus: null` to inline `vocabActivity` fixture.

---

## Tests

All tests passed after fixes.

### Test counts (Phase 8P final)
| Suite | Count | Result |
|---|---|---|
| Unit | 803 | All pass |
| Integration | 504 | All pass |
| Architecture | 3 | All pass |
| Angular | 132 | All pass |

Unit tests already included in `ListeningAudioServiceTests.cs` (written as part of Phase 8P pre-analysis):
- Guard: `WritingScenario` and `VocabularyPractice` are no-ops
- Guard: all 9 listening pattern keys cause audio metadata to be written
- Missing/empty `audioScript` sets unavailable metadata
- Already-available audio is skipped (idempotent)
- `ActivityDto.AudioStatus` field existence and accepted values

---

## Decisions made

- **`StripAudioFromContentJson`** strips the internal `audio` block from `ContentJson` before it reaches the client. Storage keys must not leak to the frontend.
- **`ListeningPatternKeys` duplicated in `ListeningAudioService`** — `ActivityGetHandler` already had this set; `ListeningAudioService` now has its own copy so the guard is self-contained and testable without `ActivityGetHandler`.
- **`write_from_dictation` not changed** — per-sentence audio generation is out of scope. Passage-level `audioScript` will get audio if present in the JSON.

---

## Risks and unresolved questions

- `write_from_dictation` has per-sentence `audioUrl` items in its content JSON. Phase 8P does not address per-sentence TTS generation.
- `summarize_spoken_text` and speaking formats remain non-runnable and are not affected.

---

## Documentation impact

- `docs/reviews/2026-06-16-phase-8p-audio-lifecycle-review.md` — this file (updated with actual implementation).

---

## Next recommended action

Verify audio plays correctly in the browser for `listening_multiple_choice_single`, `listening_fill_in_blanks`, and `highlight_correct_summary` once a TTS provider is configured.
