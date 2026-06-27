# Phase 13B — Adaptive Placement Response Submission and Real Scoring: Engineering Review

**Date:** 2026-06-27
**Sprint/Feature:** Phase 13B — Adaptive Placement Engine
**Status:** Complete — all gates passed

---

## Files Reviewed / Changed

### Domain
- `src/LinguaCoach.Domain/Entities/PlacementAssessmentItem.cs` — Added `EvaluationNotes` (string?), `DurationSeconds` (int?); `RecordResponse` now guards against duplicate submission with `InvalidOperationException`.

### Application
- `src/LinguaCoach.Application/Placement/IPlacementScoringService.cs` (new) — `PlacementScoreResult` record + `IPlacementScoringService` interface.
- `src/LinguaCoach.Application/Placement/PlacementAssessmentDtos.cs` — Added `SubmitResponseResult`, `PlacementNextItemDto`, `PlacementSkillProgressDto`, `PlacementItemHistoryDto`, `PlacementAssessmentProgressDto`.
- `src/LinguaCoach.Application/Placement/IPlacementAssessmentService.cs` — Added `SubmitResponseAsync`, `GetNextItemAsync`, `GetProgressAsync`.

### Infrastructure
- `src/LinguaCoach.Infrastructure/Placement/PlacementScoringService.cs` (new) — Deterministic case-insensitive trim comparison. No AI, no LLM. Notes are human-readable.
- `src/LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs` — Full rewrite of simulation logic. Real adaptive scoring, per-skill confidence model, adaptive item selection, idempotent submission, completion triggers.
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — `IPlacementScoringService` registered before `IPlacementAssessmentService`.

### Persistence
- `src/LinguaCoach.Persistence/Migrations/20260627130000_T63_PlacementResponseSubmission.cs` (new) — Adds `evaluation_notes varchar(1000)` and `duration_seconds integer` to `placement_assessment_items`.
- `src/LinguaCoach.Persistence/Configurations/PlacementAssessmentItemConfiguration.cs` — Mapped two new columns.
- `src/LinguaCoach.Persistence/Migrations/LinguaCoachDbContextModelSnapshot.cs` — Updated with T62 and T63 fields; added `PlacementAssessmentItem` and `PlacementSkillResult` entity blocks; added FK relationships and navigation for `EnsureCreated` (SQLite test path).

### API
- `src/LinguaCoach.Api/Controllers/AdminPlacementController.cs` — Added `GET .../progress`, `GET .../items`, `POST .../items/{itemId}/submit`. `AssessmentBelongsAsync` helper extracted. `SubmitPlacementResponseRequest` record added.

### Angular
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — Added 5 interfaces: `AdminPlacementItemHistory`, `AdminPlacementSkillProgress`, `AdminPlacementProgress`, `AdminPlacementNextItem`, `AdminPlacementSubmitResult`.
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — Added `getPlacementProgress`, `submitPlacementResponse`.
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` — Added signals and methods for placement loading, start, complete.
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html` — Full placement card: empty state, status/confidence/adaptive progress, per-item history table, per-skill results table, action buttons.

### Tests
- `tests/LinguaCoach.UnitTests/Placement/PlacementScoringServiceTests.cs` (new) — 9 unit tests.
- `tests/LinguaCoach.UnitTests/Domain/PlacementAssessmentItemTests.cs` — 2 new tests: `RecordResponse_WithNotesAndDuration_SetsFields`, `RecordResponse_DuplicateCall_Throws`.
- `tests/LinguaCoach.IntegrationTests/Api/AdminPlacement13BEndpointTests.cs` (new) — 12 integration tests covering progress, items, submit, duplicate idempotency, 400/404/409/401 error cases, completion with real responses, skill progress update.
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` — Added `getLatestPlacement`, `getPlacementProgress`, `startPlacement`, `completePlacement` to all 14 `createSpyObj` mock setups.

---

## Findings by Priority

### Critical (resolved)

**Bug: `ArgumentException` — duplicate key "listening" in `_opts.SkillsToAssess.ToDictionary(...)`**

All five `ToDictionary` calls on `_opts.SkillsToAssess` were throwing because ASP.NET Core's Options array-binding merges config-file values with class-level defaults when the section is bound. This produces 12 entries (6 defaults + 6 from `appsettings.json`) for the same skills.

Fix applied: `.Distinct()` added before every `.ToDictionary(...)` call on `SkillsToAssess` in `PlacementAssessmentService.cs` (lines ~533, ~556, ~703, ~717, ~779). Applies to `SubmitResponseAsync`, `GetNextItemAsync`, `GetProgressAsync`, and `CompleteAssessmentAsync`.

**Angular spec: `TypeError: this.adminApi.getLatestPlacement is not a function`**

All 14 `jasmine.createSpyObj('AdminApiService', [...])` calls in the admin-student-detail spec were missing the four new methods: `getLatestPlacement`, `getPlacementProgress`, `startPlacement`, `completePlacement`. Added to all spy lists; default `of(null as unknown as AdminPlacementLatestResponse)` return value added to every `beforeEach` setup.

### Minor (noted)

- `PlacementAssessmentOptions.SkillsToAssess` defaults and config section both define the same 6 skills. Root cause is the ASP.NET Core Options array-binding merge behaviour. No code change to options registration — `.Distinct()` is the correct guard at the use site since the Options pattern does not guarantee unique values.

---

## Architecture and Logic

### Scoring model (deterministic)
- `PlacementScoringService.Score`: trims and compares case-insensitively; empty/null response → false.
- Notes format: `"Correct. Expected: 'X'."` or `"Incorrect. Expected: 'X'. Received: 'Y'."`.
- No AI, no LLM, no async.

### Confidence model
```
evidenceWeight = Min(count / 6, 1.0)
confidence = (evidenceWeight × 0.6) + (avgScore × 0.4)
+0.10 for 3+ consecutive successes at highest passed level
−0.15 for 3+ consecutive failures at target level
```
Conservative CEFR: minimum per-skill estimate. `StudentProfile.CefrLevel` updated only when confidence ≥ 0.6.

### Adaptive item selection
- Last answer for skill: score ≥ 0.8 → try one level harder; < 0.4 → try one level easier; else same.
- Falls back to adjacent levels if target level is exhausted.
- `SelectNextSkill`: least-evidenced non-confident skill with available items.

### Completion triggers (first wins)
1. `max_items_reached` — answered count ≥ MaxItems.
2. `confidence_threshold_reached` — all skills ≥ ConfidenceThreshold.
3. `items_exhausted` — no unanswered items remain.

### Idempotency
- `SubmitResponseAsync` re-returns existing result if item already answered (no re-score).
- `FinalizeCompletionAsync` is idempotent (checks existing `PlacementSkillResult` rows).

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| `.Distinct()` guard at use site | Options array merge is a framework behaviour; deduplication at the use site is simpler than changing the options registration or removing class-level defaults. |
| `of(null as unknown as AdminPlacementLatestResponse)` in tests | Keeps test type safety without requiring a full mock object for tests that don't exercise placement. |
| `startPlacement`, `completePlacement` added to spec spies | These are called from the component on button clicks; tests must not throw on construction even if placement buttons are not the focus of those test suites. |

---

## Implementation Tasks Produced

None. All tasks completed in this session.

---

## Risks / Unresolved Questions

- The `SkillsToAssess` array-merge root cause is an ASP.NET Core Options behaviour that could silently affect any `string[]` option with class-level defaults when a matching config section exists. Other options in this codebase use the same if/else pattern and may be at risk. No impact detected currently.
- `GetNextItemAsync` is implemented but not exposed via any API endpoint or wired to the student-facing UI (out of scope for 13B).
- No expiry background job (out of scope for 13B).
- No student-facing adaptive placement flow (out of scope for 13B).

---

## Final Verdict

All builds and tests pass:

- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `dotnet test --configuration Release` — 2684 passed (3 arch + 1504 unit + 1177 integration), 0 failed
- `npm run build -- --configuration production` — success
- `npm test -- --watch=false --browsers=ChromeHeadless` — 1384 passed, 0 failed

---

## Next Recommended Action

Phase 13C options:
- Expiry background job for timed-out assessments
- Student-facing placement UI (begin assessment, answer items, view results)
- AI-generated item bank to expand beyond 72 deterministic items
- `GetNextItemAsync` endpoint for progressive question delivery in student UI
