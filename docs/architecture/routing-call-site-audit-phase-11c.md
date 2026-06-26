# Curriculum Routing Call-Site Audit — Phase 11C

**Date:** 2026-06-26
**Author:** Phase 11C closure hardening pass

## Purpose

Documents every call site that invokes `ICurriculumRoutingService.RecommendAsync`, whether mastered objective keys are wired, and the reason for any intentional deferral.

---

## Call Sites

### 1. ReadinessPoolReplenishmentService
**File:** `src/LinguaCoach.Infrastructure/ReadinessPool/ReadinessPoolReplenishmentService.cs`
**Status:** ✅ Wired (Phase 11C)

| Property | Value |
|----------|-------|
| Synchronous | No — background job |
| Passes mastered keys | Yes — fetched via `IStudentMasteryEvaluationService.EvaluateStudentAsync` before the skill-rotation loop |
| Routing mode | `NewLearning` (default); `Review` when `EnableReviewScaffoldGeneration=true` and weak events detected |
| Mutates readiness items | Yes — `EvaluateStudentAsync` also calls `EvaluateAndDemoteReadinessItemsAsync` internally; new items are queued via `CreateQueuedAsync` |
| Uses pre-generated decisions | No — this is the source of pre-generation |
| Future action | None |

**Note:** `EvaluateStudentAsync` triggers readiness demotion as a side-effect during replenishment. This is intentional — mastered items must be demoted before new items are queued for the same student.

---

### 2. LessonBatchGenerationJob
**File:** `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`
**Status:** ✅ Wired (Phase 11C)

| Property | Value |
|----------|-------|
| Synchronous | No — background job (Quartz) |
| Passes mastered keys | Yes — fetched in `BuildCompactSummaryAsync` |
| Routing mode | `NewLearning` |
| Mutates readiness items | No directly — creates `GenerationBatch` and `LearningSession` rows; downstream `ActivityMaterializationJob` links to pool items |
| Uses pre-generated decisions | No — this is upstream generation |
| Future action | None |

---

### 3. PracticeGymGenerationJob
**File:** `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs`
**Status:** ✅ Wired (Phase 11C)

| Property | Value |
|----------|-------|
| Synchronous | No — background job |
| Passes mastered keys | Yes — fetched in `MaterializeAsync` per cache item |
| Routing mode | `NewLearning` |
| Mutates readiness items | Yes — creates pool item via `ReadinessItemRequestBuilder` and links activity |
| Uses pre-generated decisions | No — materializes from `PracticeActivityCache` |
| Future action | Consider batching mastery fetch per student rather than per cache item if pool is large |

---

### 4. ExercisePrepareHandler
**File:** `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs`
**Status:** ⏸ Intentionally deferred

| Property | Value |
|----------|-------|
| Synchronous | Yes — called on student page load when exercise first accessed |
| Passes mastered keys | No |
| Routing mode | `NewLearning` (implicit default) |
| Mutates readiness items | No — generates activity content only |
| Uses pre-generated decisions | Yes — this handler only fires when the background-pre-generated pool item exists but has no linked activity yet |
| Future action | Wire mastered keys if synchronous mastery lookup latency becomes acceptable, or cache mastery result on student profile |

**Reason for deferral:** This is a synchronous page-load path. Adding `IStudentMasteryEvaluationService.EvaluateStudentAsync` here would add a DB read (up to 200 events) on every first exercise access. The pool pre-generation pipeline already applies mastery filtering, so exercises served via pre-generated pool items are already mastery-aware. This handler is a fallback path.

**Guard:** Do not add mastery evaluation here synchronously without first measuring latency impact or introducing a cached/snapshot mastery state on the student profile.

---

### 5. ActivityGetHandler
**File:** `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`
**Status:** ⏸ Intentionally deferred

| Property | Value |
|----------|-------|
| Synchronous | Yes — on-demand page load for practice gym |
| Passes mastered keys | No |
| Routing mode | `NewLearning` (implicit default) |
| Mutates readiness items | No — reads from practice cache, generates on-demand if cache empty |
| Uses pre-generated decisions | Yes — tries practice cache first; routing only used when cache is cold |
| Future action | Same as ExercisePrepareHandler — wire after latency is measured |

**Reason for deferral:** Same as ExercisePrepareHandler. This is a hot user-facing path. Mastery filtering in the background pre-generation pool already ensures the practice cache is mastery-aware. On-demand generation (cache miss) is rare.

**Guard:** Do not add mastery evaluation here synchronously without latency analysis.

---

### 6. ActivityMaterializationJob
**File:** `src/LinguaCoach.Infrastructure/Jobs/ActivityMaterializationJob.cs`
**Status:** ⏸ Intentionally deferred

| Property | Value |
|----------|-------|
| Synchronous | No — background job |
| Passes mastered keys | No |
| Routing mode | `NewLearning` (implicit default) |
| Mutates readiness items | Yes — links materialized activity IDs to existing pool items |
| Uses pre-generated decisions | Yes — works on items already in the pool; routing is used only for AI context, not to re-select objectives |
| Future action | Wire mastered keys if re-selection is ever needed; currently routing here is for AI prompt context only |

**Reason for deferral:** This job materializes exercises for lesson sessions. The objective selection decision was already made upstream by `LessonBatchGenerationJob`. Routing here provides context to the AI generator, not a new objective selection. Wiring mastered keys would have no effect on which objective is used.

---

### 7. CurriculumObjectiveWriteService (Admin Preview)
**File:** `src/LinguaCoach.Infrastructure/Curriculum/CurriculumObjectiveWriteService.cs`
**Status:** ✅ `RoutingMode` added (Phase 11C-FINAL); mastered keys intentionally not applied

| Property | Value |
|----------|-------|
| Synchronous | No — admin API call |
| Passes mastered keys | No — admin generic preview; student mastery not applied |
| Routing mode | Selectable via `AdminRoutingPreviewRequest.Mode` (default: `NewLearning`) |
| Mutates readiness items | No — preview only |
| Uses pre-generated decisions | No |
| Future action | If student-specific preview is needed, resolve mastery for the specific student |

**Behaviour:** A warning is always included in preview results: `"Student-specific mastery filtering is not applied in generic preview."` This prevents admin from mistakenly assuming the preview reflects actual student routing.

---

## Summary Table

| Call site | Wired | Sync | Mastery keys | Mode | Deferred reason |
|-----------|-------|------|--------------|------|-----------------|
| ReadinessPoolReplenishmentService | ✅ | No | Yes | NewLearning / Review | — |
| LessonBatchGenerationJob | ✅ | No | Yes | NewLearning | — |
| PracticeGymGenerationJob | ✅ | No | Yes | NewLearning | — |
| ExercisePrepareHandler | ⏸ | Yes | No | NewLearning | Page-load latency |
| ActivityGetHandler | ⏸ | Yes | No | NewLearning | Page-load latency |
| ActivityMaterializationJob | ⏸ | No | No | NewLearning | Upstream already decided |
| CurriculumObjectiveWriteService (admin) | ✅ | No | No (by design) | Selectable | Preview only |

## Future Action Required

When a cached/snapshot mastery state is available on `StudentProfile` (e.g., `LastMasteredObjectiveKeysJson`), wire `ExercisePrepareHandler` and `ActivityGetHandler` without adding a DB round-trip per request.
