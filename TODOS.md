# TODOS

Deferred items captured from engineering reviews and sprint planning.
Each item includes context, motivation, and the phase where it was deferred.

---

## Curriculum / CEFR

### TODO-001 — CEFR plus/sublevel handling (B2+)
**What:** Extend the curriculum and placement model to handle CEFR plus-levels such as B2+.
**Why:** `StudentProfile.CefrLevel` and some placement results may return "B2+" or similar. The Phase 10K model uses `CefrLevelConstants` (A1–C2 only) and will not match plus-levels correctly.
**Context:** Phase 10K seeds A1–C2 objectives and validates against `CefrLevelConstants.All`. A student assessed at B2+ will fall through to the B2 bucket silently. Future phases should define a sublevel mapping (B2+ → B2, C1-) or extend `CefrLevelConstants` with a plus tier.
**Depends on:** Phase 10K complete (done). Requires alignment with placement assessment output format.
**Deferred from:** Phase 10K engineering review, 2026-06-17.

---

### TODO-002 — Migrate `StudentProfile.CefrLevel` from free-text string to validated enum/constant
**What:** Replace `StudentProfile.CefrLevel` (currently `string?`, no validation) with a validated type using `CefrLevelConstants`.
**Why:** `CefrLevel` can currently hold any arbitrary string. Phase 10K introduces `CefrLevelConstants` for curriculum validation, but `StudentProfile` still accepts free-text. This creates a divergence: curriculum queries normalise CEFR, but profile data may not match.
**Context:** A migration to change the column type is low risk (values are short strings), but requires auditing all callers of `StudentProfile.CefrLevel` and updating placement assessment output. Phase 10K deferred this to avoid migration risk in a foundation phase.
**Depends on:** TODO-001 (plus-level handling) should be resolved first so the migration captures the full value set.
**Deferred from:** Phase 10K engineering review, 2026-06-17.

---

### TODO-003 — Admin curriculum objective builder / write UI
**What:** Add admin CRUD endpoints and a basic UI for creating, editing, and deactivating curriculum objectives without a code deployment.
**Why:** Phase 10K seeds objectives via `CurriculumObjectiveSeeder`. Non-developer staff (curriculum designers, coaches) cannot currently modify the syllabus without a code change and redeploy.
**Context:** Phase 10K adds read-only admin endpoints (`GET /api/admin/curriculum/objectives`). Write endpoints are deferred. The domain model (`CurriculumObjective.UpdateDetails`, `Activate`, `Deactivate`) is already designed to support CRUD. The admin UI (TailAdmin migration) is a separate workstream.
**Depends on:** Phase 10K complete (done). Admin UI migration (separate backlog item).
**Deferred from:** Phase 10K engineering review, 2026-06-17.

---

## Preference Enforcement / AI Context

### TODO-004 — Wire `learnerPreferences` and `learningGoalContext` into evaluation prompt templates
**What:** Update `activity_evaluate_writing` and `activity_evaluate_speaking_role_play` prompt templates in the database to reference `{{learnerPreferences}}` and `{{learningGoalContext}}` variables.
**Why:** Phase 10K-F adds these variables to `ActivityEvaluationContext` and passes them to the prompt variable dict, but the prompt templates do not yet reference them. Variables are available but unused.
**Context:** Adding unused variables to the dict is safe. A prompt-engineering pass should add preference-aware coaching instructions (e.g. use support language for explanations when `translationHelpPreference` allows it; adjust challenge level based on `difficultyPreference`).
**Depends on:** Phase 10K-F complete (done).
**Deferred from:** Phase 10K-F engineering review, 2026-06-17.

### TODO-005 — Add `SupportLanguageCode` to `ActivityGenerationContext`
**What:** Add the BCP-47 support language code (e.g. `"fa"`, `"zh"`) to `ActivityGenerationContext` alongside the existing `SupportLanguageName`.
**Why:** `SupportLanguageCode` is stored on `ResolvedLearningGoalContext` but not propagated to `ActivityGenerationContext`. If a prompt template needs the code for structured language-switching, it is unavailable.
**Context:** Current prompts use `SupportLanguageName` in prose. Add when a prompt template requires the code.
**Depends on:** Phase 10K-F complete (done).
**Deferred from:** Phase 10K-F engineering review, 2026-06-17.

### TODO-006 — Add `TranslationHelpPreference` to `ResolvedLearningGoalContext`
**What:** Expose `TranslationHelpPreference` as a field on `ResolvedLearningGoalContext` so downstream consumers (curriculum mappers, ledger, future routing) can read it without going through the formatter.
**Why:** Only `LearnerPreferenceContextFormatter` currently knows the translation preference. Ledger events and curriculum selection cannot use it directly.
**Depends on:** Phase 10K-F complete (done).
**Deferred from:** Phase 10K-F engineering review, 2026-06-17.

---

## Readiness Pool (Phase 10M foundation — serving/replenishment deferred)

### TODO-007 — Pool replenishment background engine (Phase 10N)
**What:** Implement a background job that monitors pool health per student and triggers generation when the ready count drops below a threshold.
**Why:** Phase 10M records pool items during existing generation jobs. It does not proactively refill the pool. Without replenishment, the pool will only be populated when `PracticeGymGenerationJob` or `LessonBatchGenerationJob` happen to run.
**Context:** The `IStudentActivityReadinessPoolService` and `StudentActivityReadinessItem` entity are ready. The replenishment job needs to: query ready counts by student/source, trigger generation for low-count students, and handle stale/failed item cleanup.
**Deferred from:** Phase 10M engineering review, 2026-06-17.

### TODO-008 — Serve from pool on Today and Practice Gym page load (Phase 10N/10O)
**What:** Update `ActivityGetHandler` and `ExercisePrepareHandler` to check the readiness pool for a suitable ready item before falling back to on-demand generation.
**Why:** Phase 10M records items in the pool but does not change page-load serving. The pool is currently write-only from the user-facing perspective.
**Context:** `IStudentActivityReadinessPoolService.ReserveNextReadyAsync` is safe and ready. The serving integration point is `ActivityGetHandler.HandlePatternKeyedAsync` for Practice Gym and `ExercisePrepareHandler.HandleAsync` for Today lessons.
**Deferred from:** Phase 10M engineering review, 2026-06-17.

### TODO-009 — Enable `AllowReviewOrScaffold=true` based on mastery signals (Phase 10N)
**What:** Wire mastery/ledger signals so that `CurriculumRoutingRequestFactory.Build` passes `allowReviewOrScaffold=true` when a student has demonstrated mastery of the target objective.
**Why:** All current production call sites pass `allowReviewOrScaffold=false`. The routing service supports review/scaffold routing but it is never activated.
**Context:** Requires mastery engine or ledger query to determine when a student has passed an objective. The routing service and pool lifecycle already support `RoutingReason.Review / Scaffold / Remediation`.
**Deferred from:** Phase 10L/10M engineering review, 2026-06-17.

### TODO-010 — Sweep orphaned Generating pool items (Phase 10N)
**What:** Add a background sweep that expires `StudentActivityReadinessItem` rows stuck in `Generating` status beyond a configurable timeout.
**Why:** If `PracticeGymGenerationJob` or `LessonBatchGenerationJob` throw an unexpected exception after creating a pool item but before calling `MarkReadyAsync` or `MarkFailedAsync`, the item remains in `Generating` indefinitely.
**Context:** The outer `Execute` catch block in generation jobs marks the cache/batch as failed but does not have access to `poolItemId` in the current design. A sweep job is the correct fix.
**Deferred from:** Phase 10M engineering review, 2026-06-17.
