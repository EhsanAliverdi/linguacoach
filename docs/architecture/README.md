# Architecture Documentation — Source of Truth Map

This file explains which documentation is authoritative and how to resolve conflicts.

---

## Conflict Resolution Rule

When any two docs disagree, prefer the source higher in this list:

1. **AGENTS.md** — standing rules for all coding agents
2. **Current architecture docs** (this folder, listed below)
3. **docs/backlog/product-backlog.md** — current implementation state
4. **Latest sprint docs** — what was decided and built most recently
5. **Older sprint docs** — historical context only
6. **docs/implementation-roadmap.md** — original MVP task breakdown, now historical

---

## Current Product Direction

SpeakPath is an **AI-powered workplace English class platform**.

Not: a random exercise generator, a writing correction app, or a card-based practice tool.

Current implemented activity types: `WritingScenario`, `ListeningComprehension`, `VocabularyPractice`, `SpeakingRolePlay`.

Next sprint: **Placement Assessment MVP**.

---

## Architecture Docs — Current Source of Truth

| Doc | What it defines |
|---|---|
| [course-session-learning-model.md](course-session-learning-model.md) | `LearningSession` / `SessionExercise` layer, teaching sequence, session duration, micro lessons, weekly plan, Call Mode (P2) |
| [exercise-pattern-library.md](exercise-pattern-library.md) | All named `ExercisePattern` keys, input/output/skills/minutes, TeamsChatSimulation spec, pattern priority table |
| [placement-assessment-model.md](placement-assessment-model.md) | `PlacementAssessment` entity (standalone, not a LearningModule), 6 sections, `PlacementResult` JSON, lifecycle flow |
| [professional-experience-domain-complexity.md](professional-experience-domain-complexity.md) | Two-dimension difficulty: `LanguageDifficulty` (CEFR) + `DomainComplexity` (workplace experience); `ProfessionalExperienceLevel` and `RoleFamiliarity` enums; AI prompt rules |
| [practice-gym.md](practice-gym.md) | Practice Gym as secondary on-demand experience; how it relates to guided course; Call Mode future placement |
| [file-storage-minio.md](file-storage-minio.md) | `IFileStorageService` interface; `LocalFileStorageService` and `MinioFileStorageService`; authenticated streaming pattern |
| [student-lifecycle-reset-tools.md](student-lifecycle-reset-tools.md) | 12 lifecycle stages (canonical enum); admin reset endpoint; `StudentResetLog`; soft vs hard delete rules |
| [student-learning-memory.md](student-learning-memory.md) | `UserLearningSummary` / `StudentSkillProfile`; memory write/read paths; best-effort update rules |
| [learning-activity-engine.md](learning-activity-engine.md) | `LearningActivity` / `ActivityAttempt` entity relationships; AI generation flow; how activity types share infrastructure |

---

## Canonical Lifecycle Stages

```
Created
PasswordChangeRequired
OnboardingRequired
OnboardingInProgress
PlacementRequired          ← set when onboarding finishes (not "OnboardingComplete")
PlacementInProgress
PlacementCompleted
CourseReady
InLesson
ActiveLearning
Paused
Archived
```

`OnboardingComplete` is **not** a lifecycle stage enum value. After onboarding finishes, the stage becomes `PlacementRequired`.

---

## Key Architecture Rules (summary)

### Placement
- `PlacementAssessment` is a **standalone entity** — not a `LearningModule`.
- `LearningPath` is generated **after** placement completes, using `PlacementResult` as the seed.
- `PlacementResult` is the source of truth for CEFR level — self-reported level is temporary only.
- Placement tasks use `BasicWorkplace` / `JuniorRole` domain complexity by default — they assess English, not professional expertise.

### Session and Exercise
- `LearningSession` → `SessionExercise` → `LearningActivity` → `ActivityAttempt`
- `ActivityType` values are implementation tools. The product experience is defined by `ExercisePattern` within `SessionExercise`.
- Practice Gym uses `LearningActivity` directly without a `SessionExercise`. It may still be generated from `ExercisePattern` templates.
- `call_mode_*` patterns belong in Practice Gym only — not in standard guided `LearningSession` in the MVP.

### Difficulty
- Every AI content generation call must include both `{{CEFRLevel}}` (LanguageDifficulty) and `{{DomainComplexity}}` (WorkplaceSeniority).
- Do not introduce workplace concepts beyond the student's `WorkplaceSeniority` without a preceding `micro_lesson_*` step.

### File storage
- All audio goes through `IFileStorageService` — never raw filesystem paths.
- Frontend never receives storage keys or bucket paths — backend streams through authenticated endpoints.
- MinIO is P1 before production-scale audio; it does not block Placement Assessment MVP.

---

## Implementation State (as of 2026-06-09)

| Feature | Status |
|---|---|
| WritingScenario activity | ✅ Done |
| ListeningComprehension activity (with TTS audio) | ✅ Done |
| VocabularyPractice activity | ✅ Done |
| SpeakingRolePlay activity (MVP, fake STT) | ✅ Done |
| Student learning memory + adaptive path | ✅ Done |
| Course session & placement architecture | ✅ Designed, not yet implemented |
| Placement Assessment MVP | ⬜ Next sprint |
| LearningSession / Today page | ⬜ Phase 2 |
| Exercise Pattern Engine | ⬜ Phase 3 |
| Practice Gym separation | ⬜ Phase 4 |
| IFileStorageService / MinIO | ⬜ Phase 5 |
| Admin lifecycle reset tools | ⬜ Phase 6 |

---

## Historical Docs

| Doc | Status |
|---|---|
| [docs/implementation-roadmap.md](../implementation-roadmap.md) | Historical — original T1–T12 task plan. T10 (CEFR assessment) and T11 (speaking sessions) are superseded. Read AGENTS.md instead. |
| [docs/activity-flow-migration.md](../activity-flow-migration.md) | Historical — documents the removal of old `/api/writing/*` flow. Still accurate as archive. |
| Older sprint docs (pre-2026-06-09) | Historical — describe what was true at the time. Do not treat as current direction. |

---

## Sprint Docs — Current

| Sprint doc | What it covers |
|---|---|
| [course-session-placement-redesign-sprint.md](../sprints/course-session-placement-redesign-sprint.md) | Full redesign decisions, competitive gap review, 6 implementation phases |
| [speaking-role-play-mvp-sprint.md](../sprints/speaking-role-play-mvp-sprint.md) | SpeakingRolePlay MVP (complete) |
| [listening-audio-tts-sprint.md](../sprints/listening-audio-tts-sprint.md) | TTS audio for ListeningComprehension (complete) |
| [vocabulary-practice-activity-sprint.md](../sprints/vocabulary-practice-activity-sprint.md) | VocabularyPractice (complete) |
