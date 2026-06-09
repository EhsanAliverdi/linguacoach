---
status: current
lastUpdated: 2026-06-09 14:00
owner: engineering
supersedes:
supersededBy:
---

# Sprint: Server-Side TTS for Placement Listening

## Goal

Replace browser-only `SpeechSynthesis` in the placement listening section with server-side generated, human-like TTS audio. The student hears a natural workplace audio clip instead of a robotic browser voice.

**Product goal:** Make SpeakPath feel like a serious AI language-learning product. The listening section must sound like a real workplace voice message, not a browser accessibility reader.

---

## Status

In progress — 2026-06-09

---

## Scope

### In scope
- `PlacementAudioService` — generates TTS audio for placement listening using the existing `ITextToSpeechService` abstraction and `ListeningAudioService` patterns
- Audio stored via local filesystem (same pattern as `ListeningAudioService` — scoped to `placement/{assessmentId}/`)
- `GET /api/placement/audio/{assessmentId}/listening` — authenticated, ownership-checked streaming endpoint
- `PlacementCurrentSectionDto` extended with `audioUrl` and `audioAvailable` for the listening section
- `PlacementService.GetCurrentSection` generates/retrieves audio when the listening section is served
- Frontend: replace `SpeechSynthesis` play button with native `<audio controls>` when `audioUrl` is present
- Frontend: graceful fallback message + optional `SpeechSynthesis` when `audioUrl` absent
- Sprint doc, architecture doc updates, backlog update

### Out of scope
- Full `IFileStorageService` / MinIO migration (Phase 5 — future sprint)
- Real OpenAI / Azure / ElevenLabs TTS provider (env-configured swap-in, but Fake is production path for now)
- Call Mode, Pronunciation, avatar/video
- Changing the `ITextToSpeechService` interface (it is already correct)
- ListeningComprehension activity audio (already working — do not break)

---

## Architecture Decisions

### Reuse existing TTS + storage patterns
`ListeningAudioService` (Activity layer) already: generates TTS via `ITextToSpeechService`, stores bytes on local filesystem, and returns bytes for streaming. The placement audio service follows the same patterns rather than duplicating storage logic or introducing a new abstraction prematurely.

**File storage key pattern:**
```
placement/{assessmentId:N}/listening.wav
```

Stored under the same `Tts:AudioStoragePath` root that `ListeningAudioService` uses. Frontend never sees this path — it receives an API URL.

### Audio generation placement in flow
Audio is generated (if not already present) when `GetPlacementCurrentSection` is called for the `listening` section. This means:
- Audio is generated once, on first load
- Repeated refreshes do not regenerate audio (existence check before calling TTS)
- If TTS fails, placement still loads; `audioAvailable: false` is returned and frontend shows fallback

### Endpoint pattern
```
GET /api/placement/audio/{assessmentId}/listening
```
- Requires authentication
- Verifies `assessmentId` belongs to the requesting student (ownership check)
- Streams bytes with correct `Content-Type`
- Returns 404 if audio not found or not generated yet
- Returns 403 if wrong owner

### DTO extension
`PlacementCurrentSectionDto` gains two optional fields:
```csharp
string? AudioUrl        // null for non-listening sections; set when audio is available
bool    AudioAvailable  // true = server audio exists; false = show fallback
```

`AudioUrl` is a relative API path: `/api/placement/audio/{assessmentId}/listening`

These fields are only populated for the `listening` section. Frontend checks `audioUrl` presence.

### Frontend replacement
- If `audioUrl` is set and `audioAvailable` is true: show native `<audio controls>` pointing to the API URL. No `SpeechSynthesis`.
- If `audioAvailable` is false (server fallback): show message "Audio is temporarily unavailable. You can read the transcript instead." Optionally keep `SpeechSynthesis` as last resort.
- Transcript remains hidden in `<details>` / Show transcript in both paths.

---

## API changes

| Method | Path | Notes |
|---|---|---|
| GET | `/api/placement/audio/{assessmentId}/listening` | New. Streams WAV audio for placement listening section. |
| GET | `/api/placement/current` | Extended response: `audioUrl`, `audioAvailable` fields added |

---

## DB changes

None. Audio path is derived from `assessmentId`. No new columns.

---

## Frontend changes

| File | Change |
|---|---|
| `placement.models.ts` | Add `audioUrl`, `audioAvailable` to `PlacementCurrentSection` |
| `placement.service.ts` | Add `getListeningAudio(assessmentId)` helper (or use `audioUrl` directly) |
| `placement.component.ts` | Remove `speakAudio()`/`stopAudio()` as primary path; add audio element state |
| `placement.component.html` | Replace SpeechSynthesis block with `<audio>` or fallback message |

---

## Backend changes

| File | Change |
|---|---|
| `PlacementAudioService.cs` (new) | Generate + retrieve placement listening audio |
| `PlacementDtos.cs` | Add `AudioUrl`, `AudioAvailable` to `PlacementCurrentSectionDto` |
| `PlacementHandlers.cs` | Add `IGetPlacementAudioHandler` interface |
| `PlacementService.cs` | Inject + call `PlacementAudioService` in `GetCurrentSection` for listening |
| `PlacementController.cs` | Add `GET audio/{assessmentId}/listening` endpoint |
| `DependencyInjection.cs` | Register `PlacementAudioService` |

---

## Test plan

### Backend
- `PlacementAudioService` generates audio for listening section using fake TTS
- `PlacementAudioService` does not regenerate if audio already exists
- `GetCurrentSection` for listening returns `audioAvailable: true` and an `audioUrl`
- `GetCurrentSection` for non-listening sections returns `audioUrl: null`
- Audio endpoint returns 200 + correct bytes for the owning student
- Audio endpoint returns 404 if audio not yet generated
- Audio endpoint returns 403/404 for wrong student
- TTS failure does not fail `GetCurrentSection` — returns `audioAvailable: false`

### Frontend / Playwright
- Listening section shows `<audio>` element when `audioAvailable` is true
- Transcript hidden by default; Show transcript reveals it
- Fallback message shown when `audioAvailable` is false
- SpeechSynthesis is NOT triggered when server audio is available

---

## Risks

- Fake TTS produces silent WAV — audio player shows but is silent; this is acceptable for dev/test; real audio requires a real TTS provider (future swap-in via `TTS_PROVIDER` env var)
- Local filesystem audio storage is not portable across container restarts — acceptable for now, documented as Phase 5 (MinIO migration)

---

## Tasks

- [x] Sprint doc created
- [ ] `PlacementAudioService` — backend service
- [ ] `PlacementDtos.cs` — extend with `AudioUrl`, `AudioAvailable`
- [ ] `PlacementService` — inject audio service, populate listening section DTO
- [ ] `PlacementController` — audio streaming endpoint
- [ ] `DependencyInjection.cs` — register new service
- [ ] Frontend models — add fields
- [ ] Frontend component — replace SpeechSynthesis
- [ ] Tests pass
- [ ] Docs updated

---

## Implementation notes

Server-side TTS for placement listening is implemented and committed. Key facts for future agents:

- `PlacementAudioService` generates audio on first `GET /api/placement/current` for the listening section
- Audio key: `placement/{assessmentId:N}/listening.wav` under `Tts:AudioStoragePath`
- Endpoint: `GET /api/placement/audio/{assessmentId}/listening` (authenticated, ownership-checked)
- Frontend: `<audio controls>` when `audioAvailable` is true; fallback message + optional SpeechSynthesis otherwise
- `FakeTextToSpeechService` produces silent WAV — audio player shows but is silent in dev/test
- Real TTS provider (OpenAI/Azure/ElevenLabs) is a P2 backlog item — swap in via `TTS_PROVIDER` env var once provider is implemented
- Configurable onboarding and placement assessment is deferred to backlog (P1 after stabilisation) — see `docs/backlog/product-backlog.md`

## Documentation impact

- Docs reviewed: `AGENTS.md`, `docs/architecture/placement-assessment-model.md`, `docs/architecture/file-storage-minio.md`, `docs/handoffs/current-product-state.md`
- Docs updated: `docs/architecture/placement-assessment-model.md` (listening audio section added), `docs/handoffs/current-product-state.md` (placement state updated), `docs/backlog/product-backlog.md` (configurable onboarding/placement backlog item added), this sprint doc
- Docs intentionally not updated: `docs/architecture/file-storage-minio.md` — no change to `IFileStorageService` interface or MinIO migration plan; local filesystem reuse only
