---
status: current
lastUpdated: 2026-06-17 00:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10B — Student Learning Memory / Taught-Content Ledger

**Date:** 2026-06-17
**Related sprint:** Phase 10B
**Feature area:** Student learning memory, taught-content ledger, adaptive foundation

---

## Existing memory/profile pieces found

Before writing any code, the following existing infrastructure was inspected:

| Component | Location | Purpose |
|---|---|---|
| `UserLearningSummary` | `LinguaCoach.Domain/Entities/UserLearningSummary.cs` | Rolling AI-friendly student memory summary (JSON lists, capped lengths) |
| `StudentSkillProfile` | `LinguaCoach.Domain/Entities/StudentSkillProfile.cs` | Per-skill score (0-100) updated after each pattern evaluation |
| `ActivityAttempt` | `LinguaCoach.Domain/Entities/ActivityAttempt.cs` | Append-only attempt record; the authoritative audit trail |
| `StudentMemoryService` | `LinguaCoach.Infrastructure/Memory/StudentMemoryService.cs` | Calls AI to update `UserLearningSummary` after each attempt (best-effort) |
| `PatternSkillUpdateService` | `LinguaCoach.Infrastructure/Activity/PatternSkillUpdateService.cs` | Upserts `StudentSkillProfile` rows from `PatternEvaluationResult.SkillImpacts` |
| `ActivitySubmitHandler` | `LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs` | Orchestrates pattern evaluation, skill update, memory update, vocabulary extraction |
| `LearningSession` / `SessionExercise` | Domain entities | Today lesson session and per-step exercise tracking |

Key invariants preserved:
- `ActivityAttempt` is append-only. Not modified.
- `StudentSkillProfile` updates continue to run after every evaluation.
- `StudentMemoryService.UpdateMemoryAsync` continues to run (best-effort, swallowed on failure).
- Memory update never blocks or fails the student's activity submission.

---

## New ledger/entity design

### Two new enums

**`LearningEventSource`** — where the event originated:
- `TodayLesson` — linked to a `SessionExercise` in a `LearningSession`
- `PracticeGym` — standalone Practice Gym activity, no session link
- `Placement` — reserved for future placement-derived events
- `Manual` — reserved for admin/manual events

**`LearningEventOutcome`** — result of the event:
- `Introduced`, `Practised`, `Reviewed`, `Mastered`, `NeedsReview`, `Failed`, `Skipped`

### New entity: `StudentLearningEvent`

A structured, queryable, append-only record of one learning event per student.

**Table:** `student_learning_events`

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | BaseEntity |
| `created_at` | timestamptz | DB default now() |
| `student_profile_id` | uuid FK | → StudentProfile (cascade delete) |
| `source` | varchar(50) | LearningEventSource as string |
| `outcome` | varchar(50) | LearningEventOutcome as string |
| `activity_id` | uuid? | FK-style reference to LearningActivity |
| `session_id` | uuid? | FK-style reference to LearningSession |
| `session_exercise_id` | uuid? | FK-style reference to SessionExercise |
| `activity_attempt_id` | uuid? | FK-style reference to ActivityAttempt |
| `exercise_type` | varchar(100)? | ActivityType string |
| `pattern_key` | varchar(100)? | ExercisePatternKey |
| `primary_skill` | varchar(100)? | Primary skill key for the pattern |
| `secondary_skills_json` | text? | Reserved — not populated in phase 10B |
| `learning_goal_context` | varchar(200)? | Learner goal/topic — null if not collected |
| `cefr_level_at_event` | varchar(10)? | CEFR level at time of event |
| `concepts_taught_json` | text? | Reserved for future curriculum engine |
| `concepts_practised_json` | text? | Reserved for future curriculum engine |
| `mistake_tags_json` | text? | Top correction categories from AI evaluators |
| `score` | float8? | Percentage 0-100 where available |
| `normalized_score` | float8? | Score / 100 (0-1 range) |
| `occurred_at_utc` | timestamptz | Set at construction time |
| `metadata_json` | text? | Format-specific overflow; not the primary query surface |

**Indexes:**
- `ix_student_learning_events_student` — by `student_profile_id`
- `ix_student_learning_events_student_time` — by `(student_profile_id, occurred_at_utc)`
- `ix_student_learning_events_student_pattern` — by `(student_profile_id, pattern_key)`

**EF migration:** `T45_StudentLearningEvents`

---

## New application interface: `IStudentLearningLedger`

Located in `LinguaCoach.Application/Memory/IStudentLearningLedger.cs`.

Methods:
- `RecordAsync(event, ct)` — best-effort write; logs failures, never throws
- `GetRecentAsync(studentProfileId, limit, ct)` — newest first
- `GetRecentByPatternKeysAsync(studentProfileId, keys, limit, ct)` — filter by pattern keys
- `GetRecentPatternKeysAsync(studentProfileId, limit, ct)` — distinct recent pattern keys
- `GetWeakEventsAsync(studentProfileId, limit, ct)` — NeedsReview and Failed outcomes

Implementation: `StudentLearningLedgerService` in `LinguaCoach.Infrastructure/Memory/`.

---

## What writes learning events

Events are written from **`ActivitySubmitHandler`** at two points:

### Pattern evaluation path (`HandlePatternEvaluationAsync`)

After `SaveChangesAsync` and `PatternSkillUpdateService.ApplyAsync`, before memory update:

1. Queries `SessionExercises` to detect Today lesson vs Practice Gym.
2. Derives `LearningEventSource` — `TodayLesson` if a `SessionExercise` links to the activity; `PracticeGym` otherwise.
3. Derives `LearningEventOutcome` from `evalResult.Passed` and `evalResult.Percentage`.
4. Extracts `primarySkillKey` via existing `PatternSkillUpdateService.GetPrimarySkillKey`.
5. Extracts `mistakeTagsJson` from top 5 correction categories (AI evaluators only).
6. Calls `_learningLedger.RecordAsync` — best-effort, never blocks feedback.

### Legacy AI path (WritingScenario, legacy writing)

After `SaveChangesAsync` and before `StudentMemoryService.UpdateMemoryAsync`:

1. Same Today/PracticeGym source detection via `SessionExercises` query.
2. Outcome derived from score >= 70 → Practised, score < 70 → NeedsReview, no score → Practised.
3. Session and exercise IDs captured where available.
4. Calls `_learningLedger.RecordAsync` — best-effort.

---

## What data is captured now

| Field | Populated now | Notes |
|---|---|---|
| StudentProfileId | Yes | Always |
| Source | Yes | TodayLesson / PracticeGym |
| Outcome | Yes | Derived from score/passed |
| ActivityId | Yes | Always |
| SessionId | Yes, when Today lesson | Null for Practice Gym |
| SessionExerciseId | Yes, when Today lesson | Null for Practice Gym |
| ActivityAttemptId | Yes | Always |
| ExerciseType | Yes | ActivityType.ToString() |
| PatternKey | Yes, when pattern-keyed | Null for legacy flat activities |
| PrimarySkill | Yes, for pattern path | Via GetPrimarySkillKey() |
| CefrLevelAtEvent | Yes, when set on profile | StudentProfile.CefrLevel |
| Score | Yes, when available | 0-100 |
| NormalizedScore | Yes, when available | 0-1 |
| MistakeTagsJson | Yes, for AI evaluators | Top 5 correction categories |
| LearningGoalContext | No | Reserved — no context passed yet |
| SecondarySkillsJson | No | Reserved — not populated |
| ConceptsTaughtJson | No | Reserved for curriculum engine |
| ConceptsPractisedJson | No | Reserved for curriculum engine |
| MetadataJson | No | Reserved for format-specific details |

---

## Fields reserved for future curriculum/CEFR phases

These fields are on the entity and in the DB table but not yet populated:

- `learning_goal_context` — for learner goal context (day-to-day, travel, academic, etc.)
- `concepts_taught_json` — for curriculum engine to record what was taught in a micro-lesson
- `concepts_practised_json` — for curriculum engine to record what was practised
- `secondary_skills_json` — for multi-skill pattern coverage
- `metadata_json` — overflow for format-specific evaluation data

These are null-safe by design. Null values in these fields are expected and do not cause failures.

---

## How this avoids a giant unbounded memory blob

1. `ActivityAttempt` remains the authoritative append-only audit trail. The ledger is NOT a copy of attempt data.
2. `StudentLearningEvent` rows are bounded, structured, and queryable — not a text blob.
3. `UserLearningSummary` (existing) handles the rolling AI-friendly compact summary. Its list caps (10 strong/weak skills, 20 covered scenarios, etc.) remain enforced.
4. The ledger query helpers (`GetRecentAsync`, `GetRecentPatternKeysAsync`, `GetWeakEventsAsync`) always apply a `limit` parameter, enforcing bounded result sets.
5. No new AI-generated text blobs are introduced. Mistake tags are compact category strings extracted from existing evaluator output.

---

## How workplace-only assumptions are avoided

1. `LearningGoalContext` is `null` by default — no workplace default is forced.
2. The `LearningEventSource` enum has no workplace-specific values.
3. `LearningEventOutcome` is skill-agnostic.
4. No prompt variable, seeder, or service defaults to "workplace" context.
5. The test `LearningGoalContext_NoWorkplaceDefault_WhenContextNull` explicitly asserts this.

---

## Known limitations

- `LearningGoalContext` is not yet populated. It requires a signal from the session/activity context not yet threaded through to the submit handler.
- `SecondarySkillsJson`, `ConceptsTaughtJson`, `ConceptsPractisedJson`, and `MetadataJson` are reserved but empty. They require the curriculum engine (Phase 11+) or richer AI evaluator outputs.
- The ledger is not yet read by `DynamicPatternSelector`. The query helpers (`GetRecentPatternKeysAsync`) are ready for it, but wiring them in is deferred to Phase 10C or equivalent.
- VocabularyPractice and ListeningComprehension legacy paths do NOT write ledger events in this phase (these paths return before reaching either hook point). Future phases can add hooks there.
- `LearningSession` foreign key references are stored as plain `uuid?` columns, not enforced FK constraints, to avoid circular cascades and migration complexity.

---

## Recommendation for next phase

**Phase 10C — Ledger-Aware Dynamic Pattern Selection:**

1. Wire `IStudentLearningLedger.GetRecentPatternKeysAsync` into `DynamicPatternSelector` to avoid repeating recently-practised patterns.
2. Wire `GetWeakEventsAsync` to boost patterns for skills with recent `NeedsReview`/`Failed` outcomes.
3. Begin populating `LearningGoalContext` from `LearningSession.Topic` or a new learner-goal field on `StudentProfile`.
4. Extend `SecondarySkillsJson` once pattern definitions carry multi-skill metadata.

---

## Implementation tasks produced

All tasks completed in this phase:

- [x] `StudentLearningEvent` domain entity
- [x] `LearningEventSource` and `LearningEventOutcome` enums
- [x] `StudentLearningEventConfiguration` EF config
- [x] `T45_StudentLearningEvents` EF migration
- [x] `IStudentLearningLedger` application interface
- [x] `StudentLearningLedgerService` infrastructure implementation
- [x] DI registration in `DependencyInjection.cs`
- [x] Hook into `ActivitySubmitHandler` pattern path
- [x] Hook into `ActivitySubmitHandler` legacy AI path
- [x] 10 unit tests for `StudentLearningEvent` domain entity
- [x] 9 integration tests for `StudentLearningLedgerService`
- [x] 4 API integration tests in `PatternEvaluationSubmitTests`

---

## Risks

- Low. All ledger writes are best-effort (swallowed on failure). The existing submit flow cannot be broken by ledger failures.
- Medium (future): If `LearningSession` rows are deleted, `session_id` references in ledger rows will be orphaned (not FK-enforced). This is intentional — audit rows should survive session deletion.
- Low: VocabularyPractice and legacy ListeningComprehension paths do not write ledger events. These are deterministic paths that return before the memory/ledger hooks. Can be added in a future phase.

---

## Final verdict

Implementation complete. All 1475 tests pass (941 unit, 531 integration, 3 architecture).

The ledger foundation is in place and ready for Phase 10C pattern selection integration.
