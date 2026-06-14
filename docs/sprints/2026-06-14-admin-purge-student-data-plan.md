# Admin: Student Lifecycle Reset — Implementation Summary

Date: 2026-06-14
Related: `docs/architecture/student-lifecycle-reset-tools.md`

## Context

App is in development mode. Admin needs a way to wipe a test student's data
to re-test flows from a clean state, without recreating accounts each time.

## Decision history

An earlier draft of this plan proposed a simpler 3-level nested reset model
(Progress / Progress+Onboarding / Account) with no audit trail and full
hard-deletes including `StudentProfile`/Identity user removal. That draft is
superseded.

Via AskUserQuestion, the user reviewed the existing
`docs/architecture/student-lifecycle-reset-tools.md` spec (targetStage +
clear-flags shape, `StudentResetLog` audit table, soft-delete for sessions/
exercises/attempts, audio cleanup, presets, rate limiting) and chose to
**follow that doc's design as written, including the admin UI** (originally
deferred to "Phase 6" by the doc — built now instead).

## What was implemented

### Domain
- `LearningSession`, `SessionExercise`, `ActivityAttempt`: added
  `DateTime? DeletedAtUtc` + `MarkDeleted()`. EF global query filters
  (`DeletedAtUtc == null`) added in their configurations — soft-deleted rows
  are invisible to all existing queries without further changes.
- `StudentProfile`: added `ResetToOnboarding()` (clears onboarding selections,
  language pair, learning track, career profile, skill focus, learning goal
  text; sets lifecycle stage to `OnboardingRequired`) and
  `ClearPlacementResult()` (nulls `CefrLevel`).
- New `StudentResetLog` entity (append-only audit row): StudentProfileId,
  AdminUserId, PreviousStage, NewStage, ClearedItemsJson, Reason,
  CorrelationId, PerformedAtUtc.

### Persistence
- Migration `T43_StudentResetTools`: adds `deleted_at_utc` columns to
  `session_exercises`, `learning_sessions`, `activity_attempts`; creates
  `student_reset_logs` table with FK to `student_profiles` (cascade).

### Application layer (`LinguaCoach.Application.Admin`)
- `ResetStudentCommand` (StudentProfileId, AdminUserId, TargetStage, 8 clear
  flags, Reason, CorrelationId).
- `ClearedItemsResult` / `ResetStudentResponse` matching the architecture
  doc's response shape exactly.
- `IAdminStudentQuery.ResetStudentAsync` and `CountRecentResetsAsync`
  (for rate limiting).

### Infrastructure (`AdminHandler.ResetStudentAsync`)
Single DB transaction. Per the doc's "Soft-Delete vs Hard-Delete" table:
- **Audio files** (`clearAudioFiles`): collects `ActivityAttempt.AudioStorageKey`
  + `AudioAsset.ObjectKey` for the student, calls `IFileStorageService.DeleteAsync`
  per key (failures logged, non-fatal), deletes `AudioAsset` rows, nulls
  `AudioStorageKey` on attempts, returns count of successful deletes.
- **Activity attempts** (`clearActivityAttempts`): soft-deleted via
  `MarkDeleted()`.
- **Courses/sessions/exercises** (`clearCoursesAndSessions`): soft-deleted
  via `MarkDeleted()`; also hard-deletes `PracticeActivityCache`,
  `GenerationJobItem`/`GenerationBatch` rows for the student.
- **Placement results** (`clearPlacementResults`): hard-deletes
  `PlacementAnswer`/`PlacementAssessment` rows, nulls `StudentProfile.CefrLevel`.
- **Vocabulary** (`clearVocabulary`): hard-deletes `VocabularyEntry` and
  `StudentVocabularyItem` rows.
- **Learning memory** (`clearLearningMemory`): removes `UserLearningSummary`,
  `SpeakingSession`/`SpeakingTurn`, `WritingSubmission`, `AiUsageLog` rows for
  the student.
- **Onboarding answers** (`clearOnboardingAnswers`): `StudentProfile.ResetToOnboarding()`.
- **Progress data** (`clearProgressData`): hard-deletes `StudentSkillProfile`
  rows (recalculated from remaining attempts on next read).
- Always sets `StudentProfile.LifecycleStage = targetStage` and writes a
  `StudentResetLog` row, even if no clear flags are set (e.g. "Fix password"
  preset).

### API
- `POST /api/admin/students/{studentId}/reset` — `[Authorize(Roles = Admin)]`.
  Validates `reason` is non-empty, enforces rate limit (max 10 resets/admin/hour
  via `CountRecentResetsAsync` against `StudentResetLogs`), returns 429 if
  exceeded. Generates `CorrelationId` server-side.

### Frontend
- `AdminApiService.resetStudent(studentProfileId, request)` +
  `ResetStudentRequest`/`ResetStudentResponse`/`ClearedItemsResult` models.
- Admin Students page: new "Reset data" danger action per row, opening a modal
  with:
  - Preset dropdown (Fix password, Restart onboarding, Restart placement,
    Reset course only, Full clean reset, Custom) — applies the doc's preset
    table flag combinations.
  - All 8 clear-flag checkboxes (editable after preset selection, for custom
    tuning).
  - Required reason textarea (submit disabled until non-empty).
  - Typed-email confirmation (submit disabled until it matches the student's
    email).
  - Result view: new lifecycle stage, cleared-items summary, reset log ID.

## Out of scope (per architecture doc, unchanged)

- Bulk reset of multiple students.
- Admin notification on reset.
- Self-service student reset.
- Automated lifecycle transitions.
- Account-level hard delete (deleting `StudentProfile`/Identity user) was
  part of an earlier superseded draft and was **not** implemented — the
  architecture doc's reset model does not include account deletion, only
  lifecycle-stage resets with data clearing.

## Verification

- `dotnet build` (full solution): 0 errors.
- `dotnet test tests/LinguaCoach.UnitTests`: 484/484 passing.
- `npx tsc -p tsconfig.app.json --noEmit`: clean.
- `npx ng build --configuration development`: succeeds (pre-existing NG8102
  warnings in `pattern-evaluation-result.component.html`, unrelated).
- No Playwright coverage added for the new admin reset flow (follow-up).

## Status

Implemented and verified (build + unit tests green). Playwright e2e coverage
for the admin reset modal is a recommended follow-up.
