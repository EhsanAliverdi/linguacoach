---
status: planned
createdAt: 2026-06-11
lastUpdated: 2026-06-11 14:07
owner: product
related:
  - docs/architecture/course-session-learning-model.md
  - docs/architecture/file-storage-minio.md
  - docs/handoffs/current-product-state.md
---

# Sprint: Lesson Buffer / MinIO / Background Generation

**Date planned:** 2026-06-11  
**Motivation:** Today’s Lesson and Practice Gym generation must stop happening on page load. Lessons should be stable, fast to open, and backed by durable generated assets.

---

## Product Goal

SpeakPath should feel like a real English class with prepared lesson material.

When a student opens a lesson:

- the lesson content is already generated
- listening audio is already generated
- audio assets are already stored
- page load reads saved data only
- refreshing the page does not change the activity, transcript, audio, expected answers, or exercise sequence

Evaluation remains live because it depends on the student’s submitted answer. Content generation does not run in the normal page-load path.

---

## Product Owner Decisions

- Guided lessons use a **pre-generated lesson buffer**.
- The system maintains a configurable number of ready lessons ahead of the student.
- Example policy:
  - ready lesson buffer size: `5`
  - refill threshold: `1`
  - refill batch size: `4`
- After the student completes enough lessons that the ready buffer is low, the system sends a compact learning summary to AI and generates the next batch in the background.
- Generation and TTS run through background jobs, not through student page-load requests.
- Admin can configure lesson-buffer and MinIO/integration settings.
- Practice Gym maintains a configurable pre-generated exercise buffer per enabled practice type/pattern.
- MinIO settings are configurable through `.env` / ASP.NET configuration.
- Admin can see integration health, including whether MinIO connection succeeds.
- Live AI conversation is a separate future mode from guided lessons.

---

## Lesson Buffer Model

```text
Ready lesson buffer size = desired count of upcoming ready sessions
Refill threshold         = generate when ready upcoming sessions <= this number
Refill batch size        = number of new sessions to generate in one background batch
```

Example:

```text
Ready lesson buffer size: 5
Refill threshold: 1
Refill batch size: 4

Student begins with lessons 1-5 ready.
Student completes lesson 4.
Only lesson 5 remains ready.
Background generation creates lessons 6-9.
Student now has 5 ready lessons again.
```

A materialized lesson means:

```text
LearningSession
-> SessionExercise
-> LearningActivity
-> AudioAsset where needed
-> MinIO object where needed
```

---

## Storage Rules

### PostgreSQL Stores

PostgreSQL remains the source of truth for structured learning data:

- `LearningSession`
- `SessionExercise`
- `LearningActivity`
- exercise content JSON
- expected answers and rubrics
- generation status
- prompt/model/provider metadata
- lesson ordering
- student attempts
- scores
- memory signals
- asset references

Use PostgreSQL for anything the app must query, join, score, audit, migrate, or use for adaptation.

### MinIO Stores

MinIO stores large generated or uploaded assets:

- TTS listening audio
- multi-speaker listening audio
- student speaking recordings
- future AI live-session audio turns
- future generated media assets

Do not store primary structured exercise content only in MinIO. The app renders lesson structure from PostgreSQL and uses MinIO for asset bytes.

---

## MinIO / Signed URL Decision

Use MinIO as the production object store through the `IFileStorageService` abstraction.

Frontend should not receive permanent public object paths.

Preferred playback flow:

```text
GET /api/activity/{activityId}/audio-url
-> auth + ownership check
-> backend creates short-lived signed MinIO URL
-> frontend binds signed URL to <audio src>
```

Recommended expiry:

| Use case | Expiry |
|---|---:|
| Current lesson listening audio | 5 minutes |
| History playback | 10-15 minutes |
| Upload URL, if added later | 1-5 minutes |

**Signed URL refresh — do not rely on reactive error handling.**
Mid-playback expiry is unreliable to detect from an `<audio>` element. The frontend must proactively re-fetch a fresh signed URL before the known expiry, not react to a playback error. Recommended approach:

```text
Frontend stores { url, expiresAt } for each active audio asset.
Before binding url to <audio src>, check: if expiresAt - now < 30s, request a fresh URL first.
On lesson/page mount, refresh any URL that will expire within the lesson session.
```

The existing authenticated streaming endpoint can remain as a fallback or local-development path.

---

## Listening Realism Model

Listening generation must support real-life workplace English.

Backend chooses the listening profile. AI generates content inside those constraints.

Store listening profile metadata with each listening activity:

```json
{
  "speakerCount": 2,
  "accent": "Australian",
  "speechRate": "natural",
  "tone": "slightly rushed but polite",
  "backgroundNoise": "office_low",
  "audioDifficulty": "moderate",
  "situation": "manager giving a project update",
  "communicationMode": "phone_message"
}
```

Difficulty progression:

| Level | Listening profile |
|---|---|
| A2 / early B1 | one speaker, clear accent, slow-natural speed, no noise |
| B1 / B2 | one or two speakers, Australian/British/international accent exposure, natural speed, light office noise |
| B2 / C1 | multiple speakers, interruptions, faster speech, phone/Teams compression, mild background noise, indirect requests |

### Multi-Speaker Audio

Generated transcript should be structured, even when one final audio file is produced:

```json
{
  "mode": "multiSpeaker",
  "speakers": [
    { "id": "manager", "label": "Manager", "voice": "Kore", "accent": "Australian" },
    { "id": "colleague", "label": "Colleague", "voice": "Puck", "accent": "British" }
  ],
  "turns": [
    { "speakerId": "manager", "text": "Can you check the latest revision before three?" },
    { "speakerId": "colleague", "text": "Yes, I can confirm it after the supplier call." }
  ]
}
```

For MVP, save one combined generated audio file per listening activity. Keep the transcript structured so later work can support per-turn playback or richer live sessions.

---

## Background Job Architecture

Use a background job system such as Quartz.NET.

Quartz schedules and runs work. Product-level job state is stored in SpeakPath tables so Admin can see what happened.

### Jobs

| Job | Purpose |
|---|---|
| `LessonBufferRefillJob` | Finds students whose ready lesson buffer is below threshold |
| `PracticeGymBufferRefillJob` | Maintains ready Practice Gym exercise buffers per student and pattern |
| `LessonBatchGenerationJob` | Builds compact summary and asks AI for the next session batch plan |
| `SessionMaterializationJob` | Creates `LearningSession` and `SessionExercise` rows from validated plans |
| `ActivityMaterializationJob` | Generates and saves `LearningActivity` content for each exercise |
| `TtsAudioGenerationJob` | Generates listening audio and stores it in MinIO |
| `GenerationRetryJob` | Retries failed generation items with backoff |
| `AudioCleanupJob` | Deletes expired/orphaned MinIO objects and updates DB state |

### Trigger Points

Run buffer checks:

- after lesson completion
- on a periodic Quartz schedule, e.g. every 10-15 minutes
- manually from Admin
- after Practice Gym assignment/completion when a practice cache falls below threshold

Student-facing requests must not block on these jobs.

---

## Generation State Model

Add product-level generation tracking tables. Do not rely only on Quartz internals for product visibility.

### `GenerationBatch`

Tracks a batch of generated lessons for one student.

| Field | Notes |
|---|---|
| `Id` | Guid |
| `StudentProfileId` | owner |
| `TriggerReason` | PlacementCompleted, LessonCompleted, ManualAdmin, ScheduledRefill |
| `Status` | Queued, Running, Completed, Failed, Partial |
| `RequestedSessionCount` | e.g. 4 |
| `CompletedSessionCount` | number materialized |
| `SummarySnapshotJson` | compact summary sent to AI |
| `PromptVersion` | prompt version used |
| `ProviderName` / `ModelName` | AI route used |
| `CorrelationId` | observability |
| `StartedAtUtc` / `CompletedAtUtc` | lifecycle |
| `FailureReason` | safe admin-visible error |

### `GenerationJobItem`

Tracks work inside a batch.

| Field | Notes |
|---|---|
| `Id` | Guid |
| `GenerationBatchId` | FK |
| `ItemType` | SessionPlan, Session, Activity, TtsAudio |
| `TargetEntityId` | nullable until created |
| `Status` | Queued, Running, Completed, Failed, Skipped |
| `AttemptCount` | retry tracking |
| `NextRetryAtUtc` | backoff |
| `LastError` | safe admin-visible error |
| `StartedAtUtc` / `CompletedAtUtc` | lifecycle |

### `AudioAsset`

Tracks MinIO-backed assets.

| Field | Notes |
|---|---|
| `Id` | Guid |
| `StudentProfileId` | owner |
| `LearningSessionId` | nullable for Practice Gym |
| `LearningActivityId` | nullable for live session turns |
| `ActivityAttemptId` | nullable |
| `AssetType` | ListeningTts, SpeakingRecording, LiveAiTurn |
| `ObjectKey` | MinIO key |
| `ContentType` | audio/wav, audio/mpeg, audio/webm |
| `DurationSeconds` | nullable until probed |
| `TranscriptHash` | idempotency |
| `SpeakerProfileJson` | voices/accent/noise/speaker count |
| `ProviderName` / `ModelName` | generation provider |
| `GenerationStatus` | Pending, Ready, Failed |
| `CreatedAtUtc` | |

---

## Idempotency Rules

Every generation job must be safe to run more than once.

Use fingerprints and unique constraints:

| Entity | Idempotency key |
|---|---|
| Session | `StudentProfileId + CourseSequenceNumber` |
| SessionExercise | `LearningSessionId + Order` |
| Activity | `SessionExerciseId + ExercisePatternKey + PromptVersion` |
| TTS audio | `LearningActivityId + TranscriptHash + SpeakerProfileHash + Provider + Model` |
| Practice cache | `StudentProfileId + PatternKey + CEFR + DomainComplexity + ContentFingerprint` |

If the same job runs twice, it must return or update the existing row, not create duplicates.

---

## Compact Summary Sent To AI

The system should never send full history, all attempts, all transcripts, or the full curriculum.

Send a compact summary generated from `StudentLearningMemory`, recent attempts, skill profiles, and module fingerprints:

```json
{
  "studentLevel": "B1",
  "domainComplexity": "intermediate_workplace",
  "careerContext": "Document Controller",
  "sourceLanguage": "Persian",
  "targetLanguage": "English",
  "completedSessions": 4,
  "recentSkillScores": {
    "listening": 72,
    "speaking": 65,
    "writing": 81,
    "vocabulary": 78
  },
  "recurringIssues": [
    "direct tone in requests",
    "misses time/date details in listening",
    "short spoken answers lack follow-up detail"
  ],
  "recentWins": [
    "better email structure",
    "uses polite follow-up phrases"
  ],
  "coveredScenarios": [
    "document delay follow-up",
    "manager update voicemail",
    "Teams clarification message"
  ],
  "avoidRepeating": [
    "document approval delay with manager"
  ],
  "nextFocusRecommendation": [
    "listening for deadlines",
    "softening urgent requests",
    "speaking with clarification questions"
  ]
}
```

AI returns a batch lesson plan. Backend validates and materializes it.

---

## Admin Settings

Add admin-configurable learning generation settings.

Suggested config keys:

| Setting | Default | Notes |
|---|---:|---|
| `ReadyLessonBufferSize` | 5 | Desired upcoming ready sessions |
| `RefillThreshold` | 1 | Generate when ready sessions are at or below this number |
| `RefillBatchSize` | 4 | Number of sessions to generate per refill |
| `MaxGenerationAttempts` | 2 | Per job item |
| `GenerationTimeoutSeconds` | 120 | Per AI call or batch phase |
| `TtsTimeoutSeconds` | 60 | Per audio asset |
| `MaxConcurrentGenerationJobs` | 2 | Global throttle |
| `MaxConcurrentTtsJobs` | 2 | Global throttle |
| `EnableBackgroundGeneration` | true | Feature switch |
| `EnableTtsGeneration` | true | Feature switch |
| `PracticeGymReadyExercisesPerType` | 10 | Target ready cache per enabled type/pattern |
| `PracticeGymRefillThresholdPerType` | 3 | Generate more when per-type ready cache reaches this count |
| `PracticeGymRefillCountPerType` | 7 | Number generated per practice refill |
| `MaxSpeakersPerListeningExercise` | 2 | MVP cap |
| `DefaultAccentPolicyJson` | Australian/British/International mix | JSON |
| `DefaultNoisePolicyJson` | no/light office noise by level | JSON |

Admin UI can start with a simple form. Advanced JSON policies can be text areas until a richer editor is needed.

---

## Admin Integrations Menu

Add an Admin navigation item:

```text
Integrations
```

Initial page sections:

### MinIO / Object Storage

Fields:

- provider: Local / MinIO
- endpoint
- bucket name
- access key
- secret key
- use SSL
- signed URL expiry minutes

Actions:

- Save settings
- Test connection
- Verify bucket exists
- Create bucket if missing, if enabled

Display:

- connection status
- last checked time
- last error, sanitized
- bucket name
- storage provider currently active

Secrets must never be returned to the frontend after save. Admin can set or replace them, but only a masked “configured” state is shown.

### Background Jobs

Show:

- queued batches
- running batches
- failed batches
- last successful generation time
- ready lesson buffer count per student
- ready Practice Gym exercise count per student and practice type

Actions:

- generate lessons now for a student
- generate Practice Gym exercises now for a student/type
- retry failed batch
- retry TTS only
- pause/resume background generation
- clear stuck job after confirmation

---

## Environment / `.env` Configuration

MinIO must be configurable via environment variables and standard ASP.NET configuration binding.

Suggested `.env` values:

```text
FILE_STORAGE_PROVIDER=Minio
FILE_STORAGE_LOCAL_BASE_PATH=/app/audio

MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=speakpath
MINIO_SECRET_KEY=<secret>
MINIO_BUCKET_NAME=speakpath-audio
MINIO_USE_SSL=false
MINIO_SIGNED_URL_EXPIRY_MINUTES=10

BACKGROUND_JOBS_ENABLED=true
LESSON_BUFFER_READY_SIZE=5
LESSON_BUFFER_REFILL_THRESHOLD=1
LESSON_BUFFER_REFILL_BATCH_SIZE=4

PRACTICE_GYM_READY_EXERCISES_PER_TYPE=10
PRACTICE_GYM_REFILL_THRESHOLD_PER_TYPE=3
PRACTICE_GYM_REFILL_COUNT_PER_TYPE=7
```

Map to configuration:

```text
FileStorage:Provider
FileStorage:LocalBasePath
FileStorage:Minio:Endpoint
FileStorage:Minio:AccessKey
FileStorage:Minio:SecretKey
FileStorage:Minio:BucketName
FileStorage:Minio:UseSSL
FileStorage:Minio:SignedUrlExpiryMinutes
BackgroundJobs:Enabled
LessonGeneration:ReadyBufferSize
LessonGeneration:RefillThreshold
LessonGeneration:RefillBatchSize
PracticeGymGeneration:ReadyExercisesPerType
PracticeGymGeneration:RefillThresholdPerType
PracticeGymGeneration:RefillCountPerType
```

Production secrets must come from VPS `.env`, Docker secrets, GitHub secrets, or a secrets manager. Do not commit real credentials.

---

## Practice Gym Policy

Practice Gym should also avoid repeated on-load generation.

Like guided lessons, Practice Gym maintains a configurable ready buffer. Instead of generating when the student clicks a card, the background system keeps 5-10 ready exercises per enabled practice type/pattern.

> **Scale note:** At 8 practice types × 10 ready exercises × N students, generation volume grows fast. At 100 students that is potentially 8,000 queued activities. The global `MaxConcurrentGenerationJobs` limit alone is insufficient — add per-student concurrency throttling so one student's generation batch cannot starve others. Consider deferring Practice Gym cache to a follow-up sprint if student count is low at launch.

Example policy:

```text
Practice Gym ready exercises per type: 10
Practice Gym refill threshold per type: 3
Practice Gym refill count per type: 7
```

Meaning:

```text
For each enabled Practice Gym card:
  Vocabulary
  Listening
  Writing
  Speaking
  Phrase Match
  Gap Fill
  Email
  Workplace Chat

Maintain up to 10 ready exercises for the student.
When the ready count for that type drops to 3 or below, generate 7 more in the background.
```

Preferred behavior:

```text
Student opens Practice Gym card
-> assign one ready cached activity for student + practice type/pattern + level + domain complexity
-> if available, load it immediately
-> if missing, enqueue generation and show "Preparing practice..."
```

Add `PracticeActivityCache` or equivalent:

| Field | Notes |
|---|---|
| `StudentProfileId` | owner |
| `PatternKey` | phrase_match, listen_and_answer, etc. |
| `CefrLevel` | |
| `DomainComplexity` | |
| `SkillFocus` | |
| `ContentFingerprint` | duplicate prevention |
| `LearningActivityId` | cached activity |
| `ExpiresAtUtc` | optional |
| `Status` | Ready, Assigned, Completed, Expired |

Practice activities may be reused, refreshed, or cloned depending on pattern. Once assigned to the student, the exercise must not change just because the page is refreshed.

### Practice Gym Admin Settings

Add configurable settings:

| Setting | Default | Notes |
|---|---:|---|
| `PracticeGymReadyExercisesPerType` | 10 | Target ready cache per enabled type/pattern |
| `PracticeGymRefillThresholdPerType` | 3 | Refill when ready cache falls to this count |
| `PracticeGymRefillCountPerType` | 7 | Number generated per refill |
| `PracticeGymCacheExpiryDays` | 30 | Optional expiry for unused generated practice |
| `PracticeGymEnabledPatternKeysJson` | MVP pattern list | Admin can disable future/expensive patterns |

These settings should be visible in Admin alongside lesson-buffer settings.

---

## Live Speaking Mode Boundary

Live AI conversation is separate from guided lessons.

Do not force live conversation into `LearningSession` materialization.

Future model:

```text
LiveSpeakingSession
  scenario
  targetSkill
  difficulty
  roleConfig
  status

LiveSpeakingTurn
  sessionId
  speaker: Student | Ai
  transcript
  audioAssetId
  timestamp
```

The live session can use realtime Gemini/OpenAI voice APIs later. It must save transcripts, student recordings, AI audio turns, and post-call feedback to PostgreSQL + MinIO through the same `AudioAsset` layer.

The scenario, role, difficulty, and safety boundaries should be selected before the call starts. The AI turns themselves are live.

---

## Backend Work

### Phase 1 - Storage Foundation

- Implement or finish `IFileStorageService`
- Add `LocalFileStorageService`
- Add `MinioFileStorageService`
- Add signed URL generation method for MinIO
- Register provider based on config
- Add health/test method for Admin Integrations
- Update Docker Compose with MinIO service and volume
- Add `.env.example` placeholders, if project has one

### Phase 2 - Audio Asset Model

- Add `AudioAsset` entity and EF config
- Migrate listening TTS audio references out of ad hoc content JSON into `AudioAsset`
- Migrate speaking recording references to `AudioAsset`
- Keep backward compatibility for old local audio paths until migration completes
- Add signed URL endpoint for activity listening audio
- Add signed URL endpoint for speaking attempt audio, if appropriate

### Phase 3 - Lesson Generation Settings

- Add `LessonGenerationSettings` or admin-config table rows
- Seed defaults:
  - ready buffer: 5
  - refill threshold: 1
  - refill batch size: 4
- Add Admin API read/update endpoints
- Validate values: positive integers, threshold lower than buffer size, sane max caps

### Phase 4 - Generation Batch State

- Add `GenerationBatch`
- Add `GenerationJobItem`
- Add EF migration and indexes
- Add idempotency constraints
- Add admin query endpoints
- Add retry status updates

### Phase 5 - Quartz.NET Worker Setup

- Add Quartz.NET package and hosted-service registration
- **Persist Quartz state to a dedicated PostgreSQL schema (required, not optional)** — in-memory Quartz drops all queued jobs on VPS restart, which silently loses pending generation work
- Add job scheduling helpers
- Add global concurrency limits (see note in Practice Gym section on per-student throttling)
- Add structured logging with correlation IDs
- Add graceful shutdown behavior

### Phase 6 - Lesson Buffer Refill

- Implement `LessonBufferRefillJob`
- Count ready upcoming sessions per student
- Trigger generation batch when below threshold
- Run after lesson completion
- Run periodically
- Support manual Admin trigger

### Phase 7 - Batch Lesson Planning

- Build compact summary packet
- Add AI prompt for batch lesson plan generation
- Validate returned JSON strictly
- Prevent repetition with module/session fingerprints
- Persist `SummarySnapshotJson`
- Track AI usage and failures

### Phase 8 - Session / Activity Materialization

- Create `LearningSession` rows from validated plans
- Create `SessionExercise` rows
- Generate `LearningActivity` content for each exercise in background
- Save expected answers/rubrics server-side
- Mark sessions `Ready` only when required exercises are generated
- Do not generate activity content during lesson page load

### Phase 9 - TTS Materialization

- Generate listening audio in background for listening exercises
- Support single-speaker and multi-speaker transcript structures
- Store audio in MinIO
- Save `AudioAsset`
- Use transcript + speaker profile + provider/model fingerprint to avoid duplicate audio
- Return signed URLs for playback

### Phase 10 - Admin Integrations UI

- Add Admin navigation item: Integrations
- Add MinIO configuration card
- Add connection test
- Add masked secret handling
- Add background job status cards
- Add per-student ready buffer view
- Add retry / generate-now actions

### Phase 11 - Practice Gym Cache

- Stop generating Practice Gym activities on every load
- Add configurable per-type ready exercise buffer
- Add background refill when a student/type falls below threshold
- Add cached activity lookup and assignment
- Add queued generation state when cache missing
- Add "Preparing practice..." UI
- Add expiration or refresh policy

---

## Frontend Work

- Today page loads only ready sessions
- Lesson page loads saved session/exercise/activity content only
- Lesson page never calls content generation
- Audio player uses signed URLs or authenticated blob fallback
- If signed URL expires, request a new URL
- If no ready lesson exists, show:

```text
Your next lesson is being prepared.
Please check again shortly.
```

- Admin Integrations page:
  - MinIO config
  - test connection
  - storage status
  - generation job queue
  - failed job retry actions

---

## API Changes

Possible new endpoints:

```text
GET  /api/admin/integrations/storage
PATCH /api/admin/integrations/storage
POST /api/admin/integrations/storage/test

GET  /api/admin/generation/settings
PATCH /api/admin/generation/settings

GET  /api/admin/generation/batches
GET  /api/admin/generation/batches/{id}
POST /api/admin/generation/batches/{id}/retry
POST /api/admin/students/{id}/generate-lessons

GET  /api/activity/{activityId}/audio-url
GET  /api/activity/{activityId}/attempts/{attemptId}/audio-url
```

Existing streaming endpoints can remain:

```text
GET /api/activity/{activityId}/audio
GET /api/activity/{activityId}/attempts/{attemptId}/audio
```

They may be retained for local development, tests, and fallback.

---

## Database Changes

Expected new tables:

- `audio_assets`
- `generation_batches`
- `generation_job_items`
- `lesson_generation_settings` or generic admin config rows
- `practice_activity_cache` if Practice Gym cache is implemented in this sprint

Expected updates:

- `learning_sessions` may need generation status fields:
  - `GenerationStatus`
  - `ReadyAtUtc`
  - `GenerationBatchId`
- `session_exercises` may need materialization status:
  - `MaterializationStatus`
  - `GenerationJobItemId`
- `learning_activities` may need:
  - content fingerprint
  - generation prompt version
  - structured listening profile JSON

---

## Out Of Scope

- Real-time live speaking mode implementation
- Real STT provider implementation
- Public MinIO bucket access
- Teacher/cohort management
- Payments or organizations
- Full audio waveform editing
- Per-turn multi-file stitched playback, unless needed by the selected TTS provider

---

## Testing Plan

### Backend

- `IFileStorageService` local implementation tests
- MinIO implementation tests behind test profile or integration flag
- signed URL generation tests
- Admin storage test endpoint tests
- lesson generation settings validation tests
- buffer threshold/refill logic tests
- idempotency tests for duplicated job execution
- generation batch status transition tests
- TTS audio asset creation tests
- ownership checks for signed audio URL endpoints
- no raw MinIO keys returned to student APIs

### Frontend

- Today page loads ready lesson without calling generation endpoint
- lesson refresh keeps same exercise/audio
- listening player receives signed/blob URL, not raw protected API URL
- expired signed URL requests a fresh URL
- no-ready-lesson state shows "being prepared"
- Admin Integrations page shows MinIO status
- Admin can test connection
- Admin can see failed generation batch and retry it

### End-to-End

- New student completes placement -> buffer generation starts
- Ready lesson opens instantly after generation completes
- Refreshing lesson does not change audio/content
- Completing lesson triggers refill when threshold is reached
- TTS generated once and reused
- Practice Gym reuses cached generated activity instead of changing on refresh
- Practice Gym maintains 5-10 ready exercises per configured type/pattern

---

## Operational Requirements

- All jobs preserve correlation ID behavior.
- All AI calls are usage-tracked.
- Background jobs log provider/model, feature key, status, duration, and failure reason.
- Production logs must not include secrets, full prompts, full student submissions, or full audio transcripts unless explicitly safe.
- Admin-visible errors must be sanitized.
- Failed jobs must not block students who still have ready lessons.
- The system should alert or visibly flag when a student has zero ready lessons and generation failed.

---

## Definition Of Done

- [ ] MinIO can be configured through `.env`
- [ ] Admin Integrations page can save/test MinIO settings without exposing secrets
- [ ] MinIO connection status is visible to Admin
- [ ] Audio assets are stored through `IFileStorageService`
- [ ] Signed URLs work for listening audio playback
- [ ] Today’s Lesson loads saved content only
- [ ] Refreshing a lesson does not regenerate content or audio
- [ ] Configurable lesson buffer exists
- [ ] Quartz background jobs generate/refill lessons
- [ ] Admin can view generation jobs and retry failures
- [ ] Listening exercises support structured speaker/audio profile metadata
- [ ] TTS generation is idempotent
- [ ] Practice Gym uses cached/queued generation, not page-load regeneration
- [ ] Practice Gym ready cache size/refill behavior is configurable from Admin
- [ ] Full backend test suite passes
- [ ] `npm run build` passes
- [ ] Playwright validates stable lesson/audio behavior

---

## Risks

| Risk | Mitigation |
|---|---|
| Background jobs create duplicate lessons | idempotency keys and unique constraints |
| Student runs out of ready lessons | maintain buffer and trigger refill before zero |
| MinIO credentials leak | never return secrets; mask admin fields; no logs |
| Signed URL expires mid-playback | proactive pre-fetch refresh 30s before expiry; do not rely on reactive error handling |
| Quartz state lost on VPS restart | persist Quartz state to PostgreSQL — in-memory scheduler must not be used in production |
| Practice Gym generation volume spikes | per-student concurrency throttle; consider deferring Practice Gym cache until student base grows |
| AI batch plan is invalid | strict JSON validation and failed batch state |
| TTS provider cannot produce requested multi-speaker audio | degrade to single-speaker or supported profile; record limitation |
| Generation cost spikes | admin concurrency limits and buffer settings |
| Jobs get stuck after deployment restart | persisted job state and retry/backoff |

---

## Recommended Sequencing

1. Storage foundation and MinIO integration
2. AudioAsset model and signed URLs
3. Admin Integrations page for MinIO health
4. Generation settings and batch/job tables
5. Quartz setup
6. Lesson buffer refill job
7. Batch lesson planning prompt and validator
8. Session/activity materialization
9. TTS materialization with MinIO
10. Admin job dashboard
11. Practice Gym cache

This order makes audio storage durable before generated lessons depend on it, then moves generation out of request-time paths.

---

## Engineering Review Decisions

Recorded from /plan-eng-review on 2026-06-11.

| # | Decision | Chosen |
|---|---|---|
| D1 | Sprint scope | Full sprint, one branch |
| D2 | Quartz host | Inside API process as IHostedService (not Worker project) |
| D3 | AudioAsset migration | New code writes AudioAsset; old activities keep JSON key (permanent dual-read fallback) |
| D4 | Signed URL response shape | `{ url, expiresAt }` — enables proactive pre-fetch |
| D5 | IFileStorageService rename | Add `MoveAsync(fromKey, toKey)` — preserves speaking audio temp→commit safety invariant |
| D6 | Quartz DI scoping | Use `Quartz.Extensions.DependencyInjection` job factory — scoped DbContext per job run |
| D7 | Test storage implementation | `FakeFileStorageService` (in-memory Dictionary) for all unit/integration tests |

### Critical Gaps (must be addressed before ship)

1. **MinIO bucket-missing silent fail** — `MinioFileStorageService` must validate bucket existence at startup health check. A missing bucket will silently fail all TTS/audio generation jobs with no user-visible error.
2. **Quartz schema bootstrap** — Quartz must validate its own PostgreSQL schema tables exist before scheduling jobs. Startup must fail clearly if the schema is missing, not silently drop queued work.

### Implementation Tasks

- [ ] T1 (P1) — `IFileStorageService` interface with `SaveAsync/ReadAsync/DeleteAsync/MoveAsync/ExistsAsync/GenerateSignedUrlAsync`
- [ ] T2 (P1) — `LocalFileStorageService` filesystem implementation
- [ ] T3 (P1) — `MinioFileStorageService` with bucket health check on startup
- [ ] T4 (P1) — `FakeFileStorageService` in-memory for tests
- [ ] T5 (P1) — Refactor `ListeningAudioService` to use `IFileStorageService`
- [ ] T6 (P1) — Refactor `SpeakingAudioService` to use `MoveAsync` for temp→commit pattern
- [ ] T7 (P1) — `AudioAsset` entity + T38 migration + dual-read fallback for legacy JSON keys
- [ ] T8 (P1) — `GET /api/activity/{id}/audio-url` returning `{ url, expiresAt }` with ownership check
- [ ] T9 (P1) — `LessonGenerationSettings` typed table + T39 migration + defaults seed
- [ ] T10 (P1) — `GenerationBatch` + `GenerationJobItem` + T40 migration + idempotency constraints
- [ ] T11 (P1) — Quartz.NET in API process: `Quartz.Extensions.DependencyInjection`, PostgreSQL persistence, per-student throttle
- [ ] T12 (P1) — `LessonBufferRefillJob` with single `GROUP BY` query; inline trigger from session completion handler
- [ ] T13 (P1) — `LessonBatchGenerationJob` + `SessionMaterializationJob` + `ActivityMaterializationJob` with strict AI JSON validation
- [ ] T14 (P1) — `TtsAudioGenerationJob` idempotent by `TranscriptHash+SpeakerProfileHash`
- [ ] T15 (P1) — `AudioCleanupJob` using `IFileStorageService.DeleteAsync`
- [ ] T16 (P1) — Admin Integrations Angular page (MinIO config + masked secrets + job status + retry)
- [ ] T17 (P1) — `PracticeActivityCache` + `PracticeGymBufferRefillJob` + "Preparing practice…" UI state
- [ ] T18 (P1) — Docker Compose: MinIO service + volume; `.env.example` vars
- [ ] T19 (P1) — Frontend proactive signed URL pre-fetch (check `expiresAt`, refresh 30s before expiry)

---

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Codex Review | `/codex review` | Independent 2nd opinion | 0 | — | — |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | ISSUES_OPEN (PLAN) | 10 issues, 2 critical gaps |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | — | — |

**VERDICT:** ENG review run — 2 critical gaps resolved in implementation tasks (T3 MinIO bucket health check, T11 Quartz schema bootstrap). Ready to implement.

NO UNRESOLVED DECISIONS
