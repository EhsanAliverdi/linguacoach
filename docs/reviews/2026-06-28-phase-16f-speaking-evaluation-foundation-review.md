# Phase 16F — AI Speaking Evaluation Foundation: Implementation Review

**Date:** 2026-06-28
**Sprint:** Phase 16F — AI Speaking Evaluation Foundation
**Review type:** Engineering / implementation review

---

## Files reviewed / created

### New files

| File | Layer | Purpose |
|------|-------|---------|
| `src/LinguaCoach.Domain/Enums/SpeakingEvaluationStatus.cs` | Domain | Enum: Pending/Evaluating/Completed/Failed/Skipped/NotSupported |
| `src/LinguaCoach.Domain/Entities/SpeakingEvaluation.cs` | Domain | Entity with lifecycle factory and state transition methods |
| `src/LinguaCoach.Application/Speaking/ISpeakingEvaluationProvider.cs` | Application | Narrow provider interface for audio (separate from IAiProvider) |
| `src/LinguaCoach.Application/Speaking/ISpeakingEvaluationService.cs` | Application | Service interface (RequestEvaluation, GetEvaluation, ProcessPending) |
| `src/LinguaCoach.Application/Speaking/SpeakingEvaluationDto.cs` | Application | DTO for student-facing evaluation result |
| `src/LinguaCoach.Application/Speaking/SpeakingEvaluationOptions.cs` | Application | Config (Enabled=false, Provider=NoOp, MaxBatchSize=10, MaxRetries=3) |
| `src/LinguaCoach.Infrastructure/Speaking/NoOpSpeakingEvaluationProvider.cs` | Infrastructure | IsSupported=false, resolves immediately as NotSupported |
| `src/LinguaCoach.Infrastructure/Speaking/SpeakingEvaluationService.cs` | Infrastructure | Batch processor; RequestEvaluationAsync non-fatal; MaxRetries enforced |
| `src/LinguaCoach.Infrastructure/Jobs/SpeakingEvaluationJob.cs` | Infrastructure | Quartz job (every 5 min, DisallowConcurrentExecution) |
| `src/LinguaCoach.Persistence/Configurations/SpeakingEvaluationConfiguration.cs` | Persistence | EF Core config for speaking_evaluations table |
| `src/LinguaCoach.Persistence/Migrations/20260628120000_T65_SpeakingEvaluationFoundation.cs` | Persistence | Hand-authored migration; status stored as integer |
| `tests/LinguaCoach.UnitTests/Domain/SpeakingEvaluationTests.cs` | Tests | 7 unit tests: entity lifecycle, validation, retry accumulation |
| `tests/LinguaCoach.IntegrationTests/Api/SpeakingEvaluationEndpointTests.cs` | Tests | 6 integration tests: auth, 404, ownership, happy path, security |

### Modified files

| File | Change summary |
|------|---------------|
| `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` | Added `DbSet<SpeakingEvaluation>` |
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered options, NoOp provider, service, job |
| `src/LinguaCoach.Api/Quartz/QuartzConfiguration.cs` | Registered SpeakingEvaluationJob with 5-minute trigger |
| `src/LinguaCoach.Api/Controllers/ActivityController.cs` | RequestEvaluationAsync after audio upload (non-fatal); new GET evaluation endpoint |
| `src/LinguaCoach.Application/Admin/AdminStudentSpeakingQueries.cs` | Extended DTO with evaluation fields |
| `src/LinguaCoach.Infrastructure/Admin/AdminStudentSpeakingAttemptsHandler.cs` | Left-join SpeakingEvaluations; failure reason gated |
| `src/LinguaCoach.Api/appsettings.json` | Added SpeakingEvaluation config section |
| `src/LinguaCoach.Web/src/app/core/models/activity.models.ts` | Added SpeakingEvaluationDto interface |
| `src/LinguaCoach.Web/src/app/core/services/activity.service.ts` | Added getAttemptEvaluation method |
| `src/LinguaCoach.Web/src/app/features/student/activity/activity-feedback-page/activity-feedback-page.component.ts` | Evaluation load + polling (10s interval, max 12 polls) |
| `src/LinguaCoach.Web/src/app/features/student/activity/activity-feedback-page/activity-feedback-page.component.html` | States: Completed/Pending/Failed/NotSupported |
| `src/LinguaCoach.Web/src/app/features/student/activity/activity-lesson/activity-lesson.component.html` | Pass activityId and attemptId to feedback page |
| `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` | Extended AdminStudentSpeakingAttempt with evaluation fields |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` | Updated speakingStatusTone/Label for new statuses |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html` | Show score, provider/model, feedback, improvement, failure reason |
| `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` | Fixed 2 literal speaking attempt objects to include new fields |

---

## Findings grouped by priority

### P0 — Correctness and safety (all verified)

- **Non-fatal evaluation creation:** `RequestEvaluationAsync` wraps all work in try/catch. Audio submission endpoint returns 200 regardless of evaluation service state. Verified by integration test `AudioAttemptSubmission_DoesNotBlockOnEvaluationError`.
- **No raw storage keys in API:** `FailureReason` in admin DTO is gated — only exposed when `EvalStatus == Failed`. `AudioStorageKey` never projected. Integration test `GetEvaluation_ResponseDoesNotExposeStorageKey` verifies.
- **Ownership enforcement:** `GetEvaluationAsync` queries by `attemptId AND studentProfileId`. Controller double-verifies profile ownership before calling service. Integration test `GetEvaluation_WrongOwner_Returns404` verifies.
- **Status enum stored as int:** Migration uses `integer` for status column, matching EF Core convention. No runtime string parsing risk.
- **MaxRetries guard:** `ProcessSingleAsync` checks `evaluation.RetryCount >= MaxRetries` before calling provider, preventing infinite retry loops.

### P1 — Behaviour correctness

- **NoOp provider resolves correctly:** `IsSupported=false` causes `ProcessSingleAsync` to call `MarkNotSupported()` immediately. Students see "Recording saved / AI evaluation not available" rather than stuck "Evaluating" state.
- **Polling termination:** Angular component polls at 10s intervals, stops after Completed/Failed/NotSupported or after 12 polls (2 minutes). `takeWhile` with `inclusive=true` ensures the terminal state update reaches the signal before the subscription ends.
- **Left-join correctness:** `GroupJoin + SelectMany + DefaultIfEmpty` pattern used for EF Core left outer join between `ActivityAttempts` and `SpeakingEvaluations`. Handles deleted activities gracefully via separate dictionary lookup.
- **DetermineStatus fallback:** `evalStatus switch` handles all 6 enum values; falls back to `promptKey`/`score` logic for legacy attempts without an evaluation record.

### P2 — Design decisions

- **`ISpeakingEvaluationProvider` separate from `IAiProvider`:** Text prompt interface cannot carry audio. Correct decision — the two interfaces have incompatible call shapes.
- **Job registration in Api (not Worker):** Consistent with existing jobs (LessonBufferRefill, etc.). Quartz runs inside Api process.
- **Enabled=false by default:** Safe — no AI calls possible without explicit opt-in. Provider=NoOp ensures graceful no-op even if Enabled is accidentally set to true without a real provider configured.
- **Student UI polling approach:** Fire-and-forget polling via RxJS `interval + switchMap + takeWhile` is appropriate for this asynchronous use case. No SSE/WebSocket complexity needed.

---

## Decisions made

1. `ISpeakingEvaluationProvider` is a separate interface from `IAiProvider` — audio evaluation is not text completion.
2. `RequestEvaluationAsync` is always non-fatal — audio submission must never be blocked by evaluation setup.
3. `FailureReason` is only exposed to admin when evaluation status is `Failed` — not to students, not for other statuses.
4. NoOp provider is the only implementation — real providers (Whisper + GPT-4o Audio, Gemini Audio) will be added in Phase 16G or later.
5. Angular feedback page polls every 10 seconds for a maximum of 2 minutes (12 polls) for Pending/Evaluating states.
6. Admin speaking table replaced the "Playback" column with "Score" and inlines feedback/improvement/failure text under the status cell.

---

## AskUserQuestion answers

None required for this phase — implementation followed spec constraints directly.

---

## Implementation tasks produced

All completed in this session. No outstanding tasks.

---

## Risks and unresolved questions

- **Real provider not yet implemented:** No actual audio evaluation occurs. Phase 16G will add a real `ISpeakingEvaluationProvider` (e.g., OpenAI Whisper + GPT-4o Audio or Gemini Audio API).
- **No AiUsageLog tracking yet:** Real provider implementation will need to wire up usage logging (featureKey, provider, model, tokens, cost) matching the existing IAiProvider tracking pattern.
- **Polling stops after 2 minutes:** If evaluation takes longer (e.g., large audio, slow provider), student will see "Pending" state permanently until they reload. Acceptable for NoOp — revisit with real provider.
- **SQLitePCLRaw vulnerability warning:** Pre-existing, unrelated to this phase.
- **Migration timestamp collision risk:** Hand-authored migration uses `20260628120000`. Confirm no collisions if parallel branches add migrations.

---

## Final verdict

**Complete.** All 13 sub-parts of Phase 16F implemented and verified. Backend builds clean (0 errors), all 2764 backend tests pass. Angular production build clean, 1525 unit tests pass.

## Next recommended action

Phase 16G: implement a real `ISpeakingEvaluationProvider` using OpenAI Whisper (STT) + GPT-4o for feedback generation, or Gemini Audio API. Wire up `AiUsageLog` tracking. Add `SpeakingEvaluation:Enabled=true` and `Provider=OpenAI` to production environment config only.
