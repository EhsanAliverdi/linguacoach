# Phase 16E — Speaking Submission Review Visibility and Pending Feedback

**Date:** 2026-06-28
**Sprint:** Phase 16E
**Related phase:** Builds on Phase 16D audio submission foundation

---

## Files reviewed / created

### New backend files
- `src/LinguaCoach.Application/Admin/AdminStudentSpeakingQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminStudentSpeakingAttemptsHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminStudentSpeakingController.cs`
- `tests/LinguaCoach.IntegrationTests/Api/AdminStudentSpeakingTests.cs`

### Edited backend files
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registration for new handler

### New/edited frontend files
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — two new interfaces appended
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — `getStudentSpeakingAttempts()` added
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts` — three new signals, `loadSpeakingAttempts()`, `speakingStatusTone()`, `speakingStatusLabel()`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.html` — Speaking Submissions card inserted
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts` — 6 new tests, factory and spy wiring

---

## Scope

Phase 16E makes submitted speaking recordings visible and reviewable before any AI evaluation is added.

**Explicitly excluded from scope:**
- AI scoring, speech-to-text, or pronunciation/fluency evaluation
- Raw blob paths or storage keys returned to any client
- Audio playback in admin UI (deferred; Bearer token cannot be injected into a nav `<a href>` without additional infrastructure)
- New speaking activity formats or schema changes

---

## Findings by priority

### P0 — Security (verified by design)

**Audio storage key isolation.** `AudioStorageKey` is declared as `internal` in the domain entity and is never projected into any DTO. The handler derives MIME type from the file extension only (`MimeTypeFromKey`). The integration test `SpeakingAttempts_AsAdmin_ResponseDoesNotContainStoragePath` asserts that the string `"speaking-recordings/"` never appears in any HTTP response body.

**Ownership enforcement.** The admin audio endpoint verifies two conditions before streaming: (1) the `StudentProfile` with the given `studentProfileId` exists, and (2) `attempt.StudentProfileId == studentProfileId`. A mismatch returns 404. No cross-student contamination is possible.

**Unknown student safety.** Unknown `studentProfileId` returns `{ status: "NotFound", attempts: [] }` (HTTP 200) rather than 500. Test `SpeakingAttempts_AsAdmin_ForUnknownStudent_DoesNotReturn500` verifies this.

**Auth guards.** Both endpoints enforce `[Authorize(Roles = nameof(UserRole.Admin))]`. Unauthenticated requests return 401; Student-role requests return 403. Four integration tests cover these paths.

### P1 — Correctness

**Status model.** Three statuses derived purely from existing entity fields:
- `promptKey == "audio_submission_pending"` → `"PendingEvaluation"`
- `score.HasValue && promptKey != "audio_submission_pending"` → `"Evaluated"`
- Otherwise → `"Submitted"`

No new columns, no schema migration required.

**Soft-deleted activity fallback.** If an `ActivityAttempt` references a `LearningActivity` that has been soft-deleted (or does not join), the handler falls back to a raw attempt query with null `activityTitle` / `activityType`. This prevents the list from silently omitting orphaned attempts.

**studentId convention.** Admin endpoints use `StudentProfile.Id` (not `ApplicationUser.Id`). This matches the established pattern from `AdminStudentProgressHandler` and all other admin student endpoints.

### P2 — UI

**Admin card placement.** Speaking Submissions card is inserted between the Practice Gym card and the Learning Journey card in `admin-student-detail.component.html`. Four UI states are covered: loading, error, empty (no recordings), and the submissions table.

**Playback deferral.** Admin audio playback is deferred: the Playback column shows "Audio submitted — playback not available in admin yet." This is the safe approach because the admin audio endpoint requires a Bearer token that cannot be injected into a plain anchor tag. No broken links, no exposed storage URLs.

**Badge tone.** `speakingStatusTone()` now returns `SpAdminBadgeTone` (imported directly from the badge component) to satisfy the strict Angular template type check. Pre-fix, the build failed with `NG2: Type 'string' is not assignable to type 'SpAdminBadgeTone'`.

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| Return `NotFound` as a 200 with `status:"NotFound"`, not HTTP 404 | Consistent with the established pattern in `AdminStudentProgressHandler`; avoids noise on unknown-but-valid admin lookups |
| Soft-deleted activity fallback query | Prevent attempt rows from disappearing silently if an activity is deleted after submission |
| Playback deferred with informational text | Honest UX; avoids broken links and storage key exposure until proper auth-aware playback infrastructure is built |
| `SpAdminBadgeTone` import from component, not re-exported from design-system index | Only one consumer; no reason to add to public API surface |

---

## AskUserQuestion answers

None required. All decisions resolved from spec constraints and existing codebase patterns.

---

## Implementation tasks produced

All tasks completed in this session.

| Part | Status |
|------|--------|
| A — Audit (baseline confirmed) | Done |
| B — Student pending feedback (no change needed; Phase 16B card shows automatically for `promptKey="audio_submission_pending"`) | Done |
| C — Admin API (query, handler, controller, DI) | Done |
| D — Admin UI card | Done |
| E — Student recent activity (no change needed; `StudentProgressSummaryHandler` already surfaces attempt events) | Done (no-op) |
| F — Audio retrieval security verification | Done (verified by design + integration test) |
| G — Status model | Done |
| H — Integration tests (8 new) + Angular unit tests (6 new) | Done |
| I — Validation (build + all test suites) | Done |
| J — Documentation | Done |

---

## Risks and unresolved questions

**Admin audio playback** — not implemented. When the team is ready to add it, the endpoint `GET /api/admin/students/{profileId}/speaking-attempts/{attemptId}/audio` already exists and is secured. The Angular side will need an auth-aware streaming approach (e.g., `HttpClient` blob download → `URL.createObjectURL`).

**Student recent activity timeline (Part E)** — confirmed no change needed because existing `StudentProgressSummaryHandler` treats all `ActivityAttempt` rows uniformly. Audio-submission attempts appear automatically.

---

## Final verdict

Phase 16E is complete. All security constraints from the specification are satisfied by design and verified by tests. No storage paths are exposed. No AI evaluation is introduced. The implementation is additive — no existing endpoints, components, or schemas were modified.

---

## Next recommended action

Phase 16F — Admin Audio Playback (auth-aware streaming in the Angular admin UI, building on the already-secured `GET .../audio` endpoint).

---

## Build and test totals (Phase 16E)

| Suite | Before | After | New |
|-------|--------|-------|-----|
| Angular unit (Karma/Jasmine) | 1,519 | 1,525 | +6 |
| Backend unit (xUnit) | 1,504 | 1,504 | 0 |
| Backend integration (xUnit) | 1,234 | 1,242 | +8 |
| Backend architecture (NetArchTest) | 3 | 3 | 0 |
| Angular build (production) | clean | clean | — |
