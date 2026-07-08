---
status: current
lastUpdated: 2026-07-08 (Phase C1)
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

SpeakPath is a **bank-first AI teaching app** for workplace English.

Not: a random exercise generator, a writing correction app, or a card-based practice tool.

AI still teaches, composes Today lessons, personalizes activities, evaluates student answers,
explains mistakes, and generates feedback. But AI is not an uncontrolled generator invoked fresh
for every activity: **banks** (Placement Item Bank, Activity Template Bank, CEFR Resource Bank)
define what is correct, reusable, and level-appropriate. A selector/composer looks for a
suitable bank item before generating anything new; AI generates only when the bank cannot
satisfy the need; and the backend validates every AI output (schema, CEFR/skill/subskill
consistency, no answer/scoring leakage) before a student ever sees it.

Full rationale, current-state audit, and phased roadmap:
`docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md` and
`docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md`.

Key facts about where this stands today (2026-07-08):

- **Placement** is Form.io-native, backend-scored, and the strongest current example of the
  bank-first pattern — see `formio-onboarding-placement-model.md`.
- **Onboarding** is also Form.io-native (`StudentFlowTemplate`/`Version`/`Submission`).
- The **Activity Template Bank** (`ActivityTemplate` entity + admin CRUD + review/publish
  workflow) exists and is proven end-to-end by exactly **one** feature-flagged Practice Gym
  pilot pattern (`formio_practice_gym_pilot`).
- The **CEFR Resource Bank** schema exists (`CefrResourceSource`, `CefrDescriptor`,
  `CefrVocabularyEntry`, `CefrGrammarProfileEntry`, `CefrReadingReference`) but holds **no
  imported data** — import is gated on licensing review
  (`cefr-resource-licensing-review.md`).
- **Today lessons and most Practice Gym exercise patterns still use the legacy, per-student,
  always-fresh `IAiActivityGenerator` generation path with zero bank involvement.** This is
  intentional and not yet migrated — see Phases C/D of the 2026-07-08 plan doc. Do not delete
  this path; it is the active fallback for everything not yet migrated to the bank.
- **Real content-level repetition/novelty avoidance exists as of 2026-07-08 (Phase B)** —
  `StudentActivityUsageLog` + `IActivityContentFingerprintService` + `IActivityNoveltyPolicy`,
  deterministic/exact-match only (no embeddings/semantic near-duplicate detection). See
  docs/architecture/repetition-and-novelty.md. `PracticeActivityCache.ContentFingerprint`
  (fixed in Clean-A) remains a separate queue-slot uniqueness key, not this content-dedup signal.

Current state: **Phase C1 done (2026-07-08)** — generalized the Form.io template path from 1
pilot pattern to a small first batch of 3 (`phrase_match`, `gap_fill_workplace_phrase`,
`reading_multiple_choice_single`); see docs/architecture/practice-gym.md. Recommended next phase:
either **Phase C2+** (migrate more Practice Gym patterns using the same proven approach) or
**Phase D** (bank-first Today lesson composer) per
`docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md` §17 — neither started.

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
| [learning-activity-engine.md](learning-activity-engine.md) | `LearningActivity` / `ActivityAttempt` entity relationships; legacy always-fresh AI generation flow (still the active path for Today lessons and most Practice Gym patterns); how activity types share infrastructure |
| [readiness-pool.md](readiness-pool.md) | `StudentActivityReadinessItem` entity; `ReadinessPoolStatus` / `ReadinessPoolSource` enums; lifecycle transitions; routing snapshot; `IStudentActivityReadinessPoolService`; concurrency model (Phase 10M); template-provenance fields added 2026-07-07 |
| [curriculum-routing.md](curriculum-routing.md) | `ICurriculumRoutingService`; `CurriculumRoutingRequest/Recommendation`; CEFR normalization; level/context/skill/difficulty routing rules; RoutingReason enum; integration points (Phase 10L). `CurriculumObjective` entity, CEFR level constants, and subskill taxonomy (`CurriculumSubskillConstants`, added 2026-07-07) are defined in Domain but do not yet have a dedicated architecture doc — the `curriculum-syllabus-model.md` doc referenced here previously no longer exists in the repo; see `docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md` §4.4 for the subskill taxonomy design instead |
| [runtime-settings-and-feature-gates.md](runtime-settings-and-feature-gates.md) | `IFeatureGateRegistry` / `IRuntimeSettingsService`; `FeatureGateGroupDefinition` registry; `RuntimeSettingOverride` table; effective-value resolution order; audit via `AdminAuditLog`; what's runtime-editable vs read-only (Phase 20B). Backs the `PracticeGymFormIoPilot.Enabled` gate added 2026-07-07 |
| [student-readiness-and-backfill.md](student-readiness-and-backfill.md) | `IStudentReadinessAuditService` / `IStudentPilotReadinessRepairService`; read-only per-student pilot-readiness audit (~20 checks); explicit, idempotent, audited repair actions; implemented vs deferred repair actions (Phase 20D) |
| [cefr-resource-licensing-review.md](cefr-resource-licensing-review.md) | CEFR Resource Bank schema (`CefrResourceSource`/`CefrDescriptor`/`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`, added 2026-07-07, no data imported yet); licensing gate for CEFR-J/UniversalCEFR import |
| [formio-onboarding-placement-model.md](formio-onboarding-placement-model.md) | Form.io-native onboarding (`StudentFlowTemplate`/`Version`/`Submission`) and placement (`PlacementItemDefinition` with `FormIoSchemaJson`/`ScoringRulesJson`, backend-only scoring); the strongest current bank-first example |
| [repetition-and-novelty.md](repetition-and-novelty.md) | `StudentActivityUsageLog`; `IActivityContentFingerprintService`/`IActivityNoveltyPolicy`; deterministic/exact-match cooldown foundation (Phase B, 2026-07-08) — not embeddings/semantic near-duplicate detection |

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

## Implementation State (as of 2026-07-08)

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
| Curriculum Syllabus Foundation | ✅ Done — Phase 10K: `CurriculumObjective`, seeder |
| CEFR-Aware Activity Routing | ✅ Done — Phase 10L: `ICurriculumRoutingService`, routing wired into all 5 generation handlers |
| Form.io onboarding | ✅ Done (2026-07-06) — `StudentFlowTemplate`/`Version`/`Submission`, custom question designer/renderer removed, V1 hardcoded 5-step handler intentionally kept as a documented dead-but-harmless backward-compat branch |
| Form.io placement | ✅ Done (2026-07-06/07) — `PlacementItemDefinition.FormIoSchemaJson`/`ScoringRulesJson`, legacy `ItemType`/`Prompt`/`CorrectAnswer`/`ReadingPassage`/`ListeningAudioScript`/`ContentJson` columns dropped cleanly; backend-only scoring confirmed by tests; adaptive engine unchanged |
| Subskill taxonomy | ✅ Done (2026-07-07) — `CurriculumSubskillConstants` (36 subskills), nullable `Subskill` on `CurriculumObjective`/`PlacementItemDefinition`/`StudentLearningEvent`/`StudentActivityReadinessItem` |
| CEFR Resource Bank schema | ✅ Schema done (2026-07-07) — `CefrResourceSource`/`CefrDescriptor`/`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`; ⬜ **no data imported** — gated on licensing review |
| ActivityTemplate bank | ✅ Done (2026-07-07) — entity + full admin CRUD + review/publish workflow, reuses `IFormIoSchemaValidationService` |
| Template-bound AI generation + validation | ✅ Done (2026-07-07) — `IActivityTemplateInstanceGenerator`, schema/CEFR/required-key/forbidden-word validation, `/generate-preview` endpoint |
| Readiness-pool template provenance | ✅ Done (2026-07-07) — `StudentActivityReadinessItem.SourceTemplateId`/`SourceBankItemId`/Form.io snapshots/`ValidationStatus`/`PersonalizationReason` |
| Placement calibration/review fields | ✅ Done (2026-07-07) — `DifficultyBand`, `ReviewStatus`, `DiscriminationIndex`/`CalibrationSampleSize`; ⬜ `EvidenceWeight` recorded but **not yet consumed** by `PlacementAssessmentService`'s confidence calc |
| `StudentLearningEvent.CurriculumObjectiveKey` | ✅ Done (2026-07-07) — closes the mastery-grouping proxy gap (`PatternKey` fallback retained for historical events) |
| Admin review queue (cross-entity) | ✅ Done (2026-07-07) — `ActivityTemplate` + `PlacementItemDefinition`, read-only triage list |
| Form.io Practice Gym pilot | ✅ Done (2026-07-07), **one pattern only** (`formio_practice_gym_pilot`), triple safety-gated (feature flag off by default + `ImplementationStatus="planned"` + requires an approved template) |
| Generalize Form.io template path — first batch (Phase C1) | ✅ Done (2026-07-08) — generalized from 1 to 4 pattern keys (`phrase_match`, `gap_fill_workplace_phrase`, `reading_multiple_choice_single` added). Each requires: code-level allow-list membership + the existing master feature flag + an approved/published `ActivityTemplate`. Evaluation dispatch (`ActivitySubmitHandler`) fixed to be content-driven (checks `LearningActivity.FormIoSchemaJson` presence) rather than pattern-driven, so legacy fallback for the SAME pattern key is unaffected. ~24 of ~28 patterns and all Today lessons still use the legacy freeform `IAiActivityGenerator` path |
| Content-level repetition/novelty avoidance | ✅ Done (2026-07-08, Phase B) — `StudentActivityUsageLog`, `IActivityContentFingerprintService` (deterministic, exact-match only, no embeddings), `IActivityNoveltyPolicy` (fingerprint/template/topic/scenario cooldowns). Wired into `ActivitySubmitHandler`, `PracticeGymGenerationJob`'s Form.io pilot, and `ActivityMaterializationJob`. `TopicKey`/`ScenarioKey` extraction from content not yet built. See docs/architecture/repetition-and-novelty.md |
| Clean-A / Clean-A2 dead-code cleanup | ✅ Done (2026-07-08) — removed dead onboarding enums, an orphaned onboarding component, dead route aliases, and a fully-orphaned admin career/word authoring API/UI chain; see `docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md` |
| Session reflection | ⬜ Deferred — needs AI prompt `session_reflection` and stable session completion signal |
| Bank-first Today lesson composer | ⬜ Deferred — planned Phase D of the 2026-07-08 plan, not started |
| Generalize Form.io template path across the rest of Practice Gym | ⬜ Deferred — Phase C1 (first batch of 3) done 2026-07-08; broader migration (Phase C2+) not started |
| IFileStorageService / MinIO | ✅ Done — audio (TTS + speaking uploads) fully on object storage; not blocking deployment at current scale |
| Admin lifecycle reset tools | ✅ Done |

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
