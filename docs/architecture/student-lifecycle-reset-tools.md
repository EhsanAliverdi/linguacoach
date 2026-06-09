---
status: current
lastUpdated: 2026-06-09 13:11
owner: architecture
supersedes:
supersededBy:
---

# Student Lifecycle and Reset Tools

## Purpose

During development, QA, pilot support, and testing, admins must be able to move a student to a specific lifecycle stage and clear selected data.

This is essential for:
- QA reruns (reset and test a flow again)
- Pilot support (student hit a bug mid-onboarding, needs clean restart)
- Onboarding validation (test each stage in isolation)
- Placement retesting

---

## Student Lifecycle Stages

```
Created
  → PasswordChangeRequired       (student has never logged in)
    → OnboardingRequired         (password changed, onboarding not started)
      → OnboardingInProgress     (started onboarding, not completed)
        → PlacementRequired      (onboarding complete, placement not started)
          → PlacementInProgress  (placement started, not completed)
            → PlacementCompleted (placement done, waiting for course generation)
              → CourseReady      (first course generated, student can begin)
                → InLesson       (student is actively in a lesson session)
                  → ActiveLearning  (normal ongoing learning state)
Paused                           (student voluntarily paused or admin paused)
Archived                         (student deactivated, no login allowed)
```

The `StudentLifecycleStage` field is stored on `StudentProfile`.

Stage transitions are enforced by the backend. The frontend reads stage to determine which screen to show.

---

## Lifecycle Stage to UI Mapping

| Stage | Student sees |
|---|---|
| Created | Login page (no access until password changed) |
| PasswordChangeRequired | Force password change screen |
| OnboardingRequired | Onboarding start screen |
| OnboardingInProgress | Continue onboarding |
| PlacementRequired | Placement start prompt |
| PlacementInProgress | Continue placement |
| PlacementCompleted | Generating course... (brief transition) |
| CourseReady | Today page (first lesson ready) |
| InLesson | Today page / active lesson |
| ActiveLearning | Today page / full navigation |
| Paused | Paused state screen (resume option) |
| Archived | Redirect to login, show deactivated message |

---

## Admin Reset Endpoint

```
POST /api/admin/students/{studentId}/reset
```

### Request Body

```json
{
  "targetStage": "OnboardingRequired",
  "clearOnboardingAnswers": true,
  "clearPlacementResults": false,
  "clearCoursesAndSessions": false,
  "clearActivityAttempts": false,
  "clearVocabulary": false,
  "clearLearningMemory": false,
  "clearAudioFiles": false,
  "clearProgressData": false,
  "reason": "Student reported stuck on onboarding screen after browser crash"
}
```

### Response

```json
{
  "studentId": "...",
  "previousStage": "PlacementInProgress",
  "newStage": "OnboardingRequired",
  "clearedItems": {
    "onboardingAnswers": true,
    "placementResults": false,
    "coursesAndSessions": false,
    "activityAttempts": false,
    "vocabulary": false,
    "learningMemory": false,
    "audioFilesDeleted": 0,
    "progressData": false
  },
  "resetLogId": "...",
  "performedByAdminId": "...",
  "performedAtUtc": "2026-06-09T10:00:00Z",
  "correlationId": "..."
}
```

### Validation Rules

- Admin role required
- `targetStage` must be a valid `StudentLifecycleStage` value
- Some `targetStage` values require matching clear flags (e.g. targeting `OnboardingRequired` while `clearCoursesAndSessions = false` when the student has sessions is allowed but logged as a warning)
- `reason` is required (non-empty string)
- The reset is atomic: all clears happen in a single transaction

---

## Reset Preset Shortcuts (Admin UI)

The admin UI should offer preset reset options to reduce mistakes:

| Preset | targetStage | What is cleared |
|---|---|---|
| Fix password | PasswordChangeRequired | nothing |
| Restart onboarding | OnboardingRequired | onboarding answers |
| Restart placement | PlacementRequired | placement results |
| Reset course only | CourseReady | courses, sessions |
| Full clean reset | OnboardingRequired | everything |

Presets map to the API request structure. The admin still confirms before submitting.

---

## Reset Log (`StudentResetLog`)

Every reset is logged permanently.

| Field | Notes |
|---|---|
| Id | Guid |
| StudentProfileId | Who was reset |
| AdminUserId | Who performed the reset |
| PreviousStage | Stage before reset |
| NewStage | Stage after reset |
| ClearedItemsJson | What was cleared |
| Reason | Free text from admin |
| CorrelationId | Request correlation ID |
| PerformedAtUtc | Timestamp |

This log is append-only. Resets are never deleted.

---

## Audio File Cleanup on Reset

When `clearAudioFiles = true`:

1. Backend queries all `ActivityAttempt.AudioStorageKey` values for the student.
2. Backend queries all listening activity `AiGeneratedContentJson` audio keys for the student.
3. For each key, calls `IFileStorageService.DeleteAsync(key)`.
4. Failures are logged but do not fail the reset transaction.
5. The count of successfully deleted files is returned in the response.

Audio keys in the database are cleared regardless of whether the file deletion succeeded.

---

## Soft-Delete vs Hard-Delete

| Data type | Approach |
|---|---|
| Onboarding answers | Hard clear (nullify fields on StudentProfile) |
| Placement results | Hard clear (delete PlacementSection rows, nullify PlacementAssessment result) |
| Courses / sessions / exercises | Soft-delete (set DeletedAtUtc, not visible to student) |
| Activity attempts | Soft-delete (preserve for audit) |
| Vocabulary items | Hard clear |
| Learning memory | Hard clear (UserLearningSummary reset to defaults) |
| Audio files | Hard delete via IFileStorageService |
| Progress data | Recalculated from remaining attempts |
| Reset log | Never deleted |

---

## Admin UI Design (future)

The admin student detail page should include a Reset section:

- Lifecycle stage display (current stage badge)
- Reset button → opens confirmation modal
- Modal shows: what will be cleared, reason input, confirm button
- Success: show new stage, cleared items summary
- Failure: show error with correlation ID

Do not implement admin reset UI until Phase 6. The API endpoint is designed now so it can be implemented consistently.

---

## Security Rules

- Admin role required (401 if unauthenticated, 403 if not admin)
- Admin cannot reset other admins
- `reason` is stored and visible in the reset log
- No reset silently modifies data — everything is logged
- Production resets should require extra confirmation (e.g. admin must type the student's email)
- Rate limit: max 10 resets per admin per hour to prevent accidental bulk resets

---

## Out of Scope (this sprint)

- Bulk reset of multiple students
- Admin notification on reset (email or push)
- Self-service student reset (student cannot reset themselves)
- Automated lifecycle transitions (future: e.g. auto-archive after 90 days inactivity)
