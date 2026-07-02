---
status: current
lastUpdated: 2026-06-10 22:00
owner: architecture
supersedes:
supersededBy:
---

# Architecture Documentation — Source of Truth Map

This file explains which documentation is authoritative and how to resolve conflicts.

---

## Source-of-truth metadata

Major docs should include:

```yaml
---
status: current | draft | historical | superseded
lastUpdated: YYYY-MM-DD HH:mm
owner: product | architecture | engineering | qa | deployment
supersedes:
supersededBy:
---
```

Agents should use this metadata to decide which docs are current.

Conflict resolution order:

1. `AGENTS.md`
2. `docs/architecture/README.md`
3. docs marked `current`
4. newer `lastUpdated`
5. latest sprint docs
6. historical/superseded docs as context only

---

## Conflict Resolution Rule

When any two docs disagree, prefer the source higher in this list:

1. **AGENTS.md** — standing rules for all coding agents
2. **docs/architecture/README.md** — source-of-truth map and metadata rules
3. **Docs marked `current`** in source-of-truth metadata
4. **Newer `lastUpdated` values** when docs have the same status
5. **Latest sprint docs** — what was decided and built most recently
6. **Historical or superseded docs** — context only

---

## Current Product Direction

SpeakPath is an **AI-powered workplace English class platform**.

Not: a random exercise generator, a writing correction app, or a card-based practice tool.

Current implemented activity types: `WritingScenario`, `ListeningComprehension`, `VocabularyPractice`, `SpeakingRolePlay`.

Current recommended next sprint: **Dynamic Pattern Selection** or **Practice Gym Expansion**.

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
| [curriculum-syllabus-model.md](curriculum-syllabus-model.md) | `CurriculumObjective` entity; CEFR level constants; context tag / focus area taxonomy; seeder pattern; `ICurriculumSyllabusQuery` candidate query interface (Phase 10K) |
| [readiness-pool.md](readiness-pool.md) | `StudentActivityReadinessItem` entity; `ReadinessPoolStatus` / `ReadinessPoolSource` enums; lifecycle transitions; routing snapshot; `IStudentActivityReadinessPoolService`; concurrency model (Phase 10M) |
| [curriculum-routing.md](curriculum-routing.md) | `ICurriculumRoutingService`; `CurriculumRoutingRequest/Recommendation`; CEFR normalization; level/context/skill/difficulty routing rules; RoutingReason enum; integration points (Phase 10L) |
| [runtime-settings-and-feature-gates.md](runtime-settings-and-feature-gates.md) | `IFeatureGateRegistry` / `IRuntimeSettingsService`; `FeatureGateGroupDefinition` registry; `RuntimeSettingOverride` table; effective-value resolution order; audit via `AdminAuditLog`; what's runtime-editable vs read-only (Phase 20B) |
| [student-readiness-and-backfill.md](student-readiness-and-backfill.md) | `IStudentReadinessAuditService` / `IStudentPilotReadinessRepairService`; read-only per-student pilot-readiness audit (~20 checks); explicit, idempotent, audited repair actions; implemented vs deferred repair actions (Phase 20D) |

### Planned / Deferred (not implemented yet)

| Doc | What it defines |
|---|---|
| [teacher-role-and-read-access.md](teacher-role-and-read-access.md) | `UserRole.Teacher` (minimal, admin-provisioned, read-only student roster access); `/api/teacher` vs `/api/admin` boundary; deferred, Phase 21A precursor |
| [view-as-user-impersonation.md](view-as-user-impersonation.md) | Admin "view as student" impersonation design (short-lived scoped JWT, audit trail, banner UX); deferred, Phase 21A precursor; interim workaround is separate browser contexts |

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

## Implementation State (as of 2026-06-10)

| Feature | Status |
|---|---|
| WritingScenario activity | ✅ Done |
| ListeningComprehension activity (with TTS audio) | ✅ Done |
| VocabularyPractice activity | ✅ Done |
| SpeakingRolePlay activity (MVP, fake STT) | ✅ Done |
| Student learning memory + adaptive path | ✅ Done |
| Placement Assessment MVP | ✅ Done |
| LearningSession / Today page — end-to-end (Phases 1–5B) | ✅ Done — data layer, session generator, backend endpoints, Today's Lesson card, LessonPage, `/prepare` wiring, activity nav; 90 e2e + 645 dotnet tests pass |
| Exercise Pattern Engine | ✅ Done — seeded pattern definitions, pattern-aware prepare/generation, `InteractionMode` renderer dispatch, 8 MVP renderers, 97 e2e + 762 dotnet tests pass |
| Pattern Evaluation Engine | ✅ Done — deterministic `ExactMatch` / `KeyedSelection` / `NoMarking` evaluators, structured `AiStructured` / `AiOpenEnded` evaluators, pattern router, `StudentSkillProfile` upserts, compact memory signals, pattern-aware result UI, 865 dotnet tests + 111 Playwright tests pass |
| Student UX Alignment / Writing-Assumption Cleanup | ✅ Done — Today/Journey/Practice/Progress/Profile nav model; Practice Gym MVP at `/practice`; mixed-skill copy; 165 Playwright tests pass |
| Curriculum Syllabus Foundation | ✅ Done — Phase 10K: `CurriculumObjective`, `ICurriculumSyllabusQuery`, seeder |
| CEFR-Aware Activity Routing | ✅ Done — Phase 10L: `ICurriculumRoutingService`, routing wired into all 5 generation handlers |
| Session reflection | ⬜ Deferred — needs AI prompt `session_reflection` and stable session completion signal |
| Practice Gym expansion | ⬜ Deferred — Workplace Chat, Email, Gap Fill, Phrase Match unlock; dynamic session templates |
| IFileStorageService / MinIO | ⬜ Deferred — not blocking deployment at current scale |
| Admin lifecycle reset tools | ⬜ Deferred |

---

## Historical Docs

| Doc | Status |
|---|---|
| [docs/engineering-plans/implementation-roadmap.md](../engineering-plans/implementation-roadmap.md) | Historical — original T1–T12 task plan. T10 (CEFR assessment) and T11 (speaking sessions) are superseded. Read AGENTS.md instead. |
| [docs/decisions/activity-flow-migration.md](../decisions/activity-flow-migration.md) | Historical — documents the removal of old `/api/writing/*` flow. Still accurate as archive. |
| Older sprint docs (pre-2026-06-09) | Historical — describe what was true at the time. Do not treat as current direction. |

---

## Sprint Docs — Current

| Sprint doc | What it covers |
|---|---|
| [course-session-placement-redesign-sprint.md](../sprints/course-session-placement-redesign-sprint.md) | Full redesign decisions, competitive gap review, 6 implementation phases |
| [2026-06-10-today-lesson-learning-session-sprint.md](../sprints/2026-06-10-today-lesson-learning-session-sprint.md) | Today's Lesson / Learning Session end-to-end (complete) |
| [2026-06-10-exercise-pattern-engine-sprint.md](../sprints/2026-06-10-exercise-pattern-engine-sprint.md) | Exercise Pattern Engine — InteractionMode, MarkingMode, 8 MVP patterns (complete) |
| [2026-06-10-pattern-evaluation-engine-sprint.md](../sprints/2026-06-10-pattern-evaluation-engine-sprint.md) | Pattern Evaluation Engine — deterministic + AI evaluators, skill/memory updates, result UI (complete) |
| [2026-06-10-student-ux-alignment-writing-assumption-cleanup-sprint.md](../sprints/2026-06-10-student-ux-alignment-writing-assumption-cleanup-sprint.md) | Student UX Alignment — Today/Journey/Practice nav model, Practice Gym MVP, mixed-skill copy (complete) |
| [speaking-role-play-mvp-sprint.md](../sprints/speaking-role-play-mvp-sprint.md) | SpeakingRolePlay MVP (complete) |
| [listening-audio-tts-sprint.md](../sprints/listening-audio-tts-sprint.md) | TTS audio for ListeningComprehension (complete) |
| [vocabulary-practice-activity-sprint.md](../sprints/vocabulary-practice-activity-sprint.md) | VocabularyPractice (complete) |
