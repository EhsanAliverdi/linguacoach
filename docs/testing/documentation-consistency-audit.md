# Documentation Consistency Audit

**Date:** 2026-06-09  
**Type:** Documentation-only cleanup — no application code changed  
**Performed by:** Automated documentation sprint

---

## Files Reviewed

| File | Notes |
|---|---|
| AGENTS.md | Updated — see changes below |
| CLAUDE.md | Updated — product requirements section aligned |
| docs/implementation-roadmap.md | Updated — historical notice added at top |
| docs/activity-flow-migration.md | No changes — accurate historical archive |
| docs/backlog/product-backlog.md | Updated — stale statuses fixed |
| docs/architecture/course-session-learning-model.md | Previously updated in redesign sprint; no new changes needed |
| docs/architecture/exercise-pattern-library.md | Previously updated in redesign sprint; no new changes needed |
| docs/architecture/placement-assessment-model.md | Updated — contradiction resolved (see below) |
| docs/architecture/professional-experience-domain-complexity.md | No changes needed |
| docs/architecture/practice-gym.md | No changes needed |
| docs/architecture/file-storage-minio.md | No changes needed |
| docs/architecture/student-lifecycle-reset-tools.md | No changes needed — lifecycle stages are canonical here |
| docs/architecture/learning-activity-engine.md | No changes — accurate, no contradictions found |
| docs/architecture/student-learning-memory.md | No changes — accurate |
| docs/sprints/course-session-placement-redesign-sprint.md | Updated — Phase 1 tasks corrected, DB table row fixed |
| docs/sprints/speaking-role-play-mvp-sprint.md | No changes — historical sprint, accurate |

---

## Contradictions Found and Resolved

### 1. Placement as LearningModule vs standalone entity

**Location:** `docs/architecture/placement-assessment-model.md`, `docs/sprints/course-session-placement-redesign-sprint.md`, `docs/backlog/product-backlog.md`

**Contradiction:**
- `placement-assessment-model.md` said: "Placement is implemented as a special `LearningModule` of type `Placement`"
- The sprint redesign established: placement generates the first LearningPath after it completes — it cannot itself be a module within a path that doesn't exist yet

**Fix:**
- `placement-assessment-model.md` — rewrote "Placement as First Module" section to "Placement is Separate from the Guided LearningPath"
- `course-session-placement-redesign-sprint.md` — Phase 1 list updated; removed "Placement as special first module (ModuleType = Placement)"; removed `learning_modules` migration row from DB changes table
- `product-backlog.md` — `Add ModuleType column to LearningModule` task marked `Superseded`

**Canonical rule:** `PlacementAssessment` is a standalone entity. `LearningPath` is generated after placement completes, using `PlacementResult` as the seed.

---

### 2. OnboardingComplete as lifecycle stage enum

**Location:** `docs/architecture/placement-assessment-model.md`, `docs/sprints/course-session-placement-redesign-sprint.md`

**Contradiction:**
- `placement-assessment-model.md` lifecycle diagram used `OnboardingComplete / PlacementRequired` as if `OnboardingComplete` is an enum stage
- `student-lifecycle-reset-tools.md` (canonical) defines only `OnboardingRequired`, `OnboardingInProgress`, `PlacementRequired` — no `OnboardingComplete`

**Fix:**
- `placement-assessment-model.md` lifecycle diagram updated: `OnboardingComplete / PlacementRequired` → `PlacementRequired`
- `course-session-placement-redesign-sprint.md` lifecycle transition line updated: `OnboardingComplete →` removed
- `docs/architecture/README.md` states explicitly: `OnboardingComplete` is not a valid enum stage; when onboarding finishes the stage becomes `PlacementRequired`

**Canonical rule:** After onboarding completes, `StudentLifecycleStage = PlacementRequired`. `OnboardingComplete` is plain English only, never an enum value.

---

### 3. SpeakingRolePlay backlog status stale

**Location:** `docs/backlog/product-backlog.md`

**Contradiction:**
- "SpeakingRolePlay activity MVP" section had all items marked `Planned` or `Not started`
- SpeakingRolePlay MVP was fully delivered in the speaking-role-play-mvp-sprint (commit b87bb96)
- "Future activity types" section still listed SpeakingRolePlay as `Not started` with "Keep Speaking card as Coming soon"

**Fix:**
- All SpeakingRolePlay sprint items marked `Done`
- Sprint section header updated with "COMPLETE" notice
- "Future activity types" SpeakingRolePlay item updated to `Done` with actual delivery description
- "Keep all unimplemented skill cards" item updated: Writing/Listening/Vocabulary/Speaking are implemented; only Pronunciation and Reading remain "Coming soon"

---

### 4. "Writing is the only active skill currently"

**Location:** `docs/backlog/product-backlog.md` — Progress and activity tracking section

**Contradiction:** Four activity types are now implemented.

**Fix:** Updated to: "Implemented activity types: WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay. Pronunciation is not yet implemented."

---

### 5. CEFR level source of truth

**Location:** `docs/backlog/product-backlog.md` — Profile page section

**Contradiction:** "Current level: read from CEFR assessment result or `StudentProfile.CefrLevel`" — this treats self-reported level as equivalent to PlacementResult

**Fix:** Updated to: "read from `PlacementResult.estimatedOverallLevel` (source of truth); fall back to `StudentProfile.CefrLevel` only if placement not yet completed"

---

### 6. Implementation roadmap T10/T11 misleading future agents

**Location:** `docs/implementation-roadmap.md`

**Contradiction:** T10 "CEFR assessment" marked Done suggests the old simple CEFR assessment is the current design. T11 "Speaking sessions" marked Done uses an old entity model (`SpeakingSession`/`SpeakingTurn`) that no longer exists.

**Fix:** Historical notice added at the top of the file clearly marking it as superseded, listing what each old task maps to in the current design.

---

### 7. CLAUDE.md product requirements outdated

**Location:** `CLAUDE.md`

**Contradiction:** CLAUDE.md said "AI tests CEFR level" (simple AI call) and did not mention placement assessment, session model, or domain complexity.

**Fix:** Product requirements section updated to reflect: placement assessment as a 6-section structured flow; PlacementResult as source of truth; DomainComplexity prompt rule; current implemented activity types.

---

### 8. AGENTS.md scope rules listed "speaking/listening/vocabulary/pronunciation" as not-to-add

**Location:** `AGENTS.md` — Section 21 Scope Rules

**Contradiction:** These are all now implemented (except pronunciation). The scope rule was out of date.

**Fix:** Scope rules updated to list: Call Mode, Pronunciation, avatar/video lessons as the actual scope boundaries. Added two new standing rules about ActivityType vs ExercisePattern and DomainComplexity.

---

### 9. AGENTS.md Section 24 (Current Product Priority) outdated

**Location:** `AGENTS.md`

**Contradiction:** Said "Next major direction: Student Learning Memory + Adaptive Curriculum" — that work is now complete. The section also said "do not add speaking/listening/vocabulary/pronunciation" — all four are now implemented.

**Fix:** Section 24 updated: next sprint is Placement Assessment MVP; all four activity types are implemented; priority is now course structure, not more activity types. Section 25 (Critical Flow) updated to reflect all four activity types and to show the "next flow to build" (placement → Today's Lesson).

---

## Remaining Unresolved Questions

These were noted in the sprint doc and are intentionally left open until implementation:

1. Should students be able to access Practice Gym before completing placement?  
   *(Proposed: yes, with a banner. Not decided.)*

2. Should `TeamsChatSimulation` be promoted to a new `ActivityType` or remain only an `ExercisePattern`?  
   *(Proposed: pattern first. Not decided.)*

3. What is the expiry/resumption policy for incomplete placement sessions?  
   *(Proposed: resumable indefinitely, no expiry. Not decided.)*

4. Weekly plan storage: `LearningPath` metadata or a separate `WeeklyPlan` table?  
   *(Deferred to Phase 2 implementation sprint.)*

---

## Source of Truth Precedence

1. AGENTS.md
2. docs/architecture/*.md (current)
3. docs/backlog/product-backlog.md
4. docs/sprints/ (most recent first)
5. docs/implementation-roadmap.md (historical only)

See: [docs/architecture/README.md](../architecture/README.md)

---

## Current Product State

| Dimension | Current value |
|---|---|
| Implemented activity types | WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay |
| Pending activity types | PronunciationPractice (P2), ReadingTask (P1 via pattern engine) |
| Lifecycle model | 12 stages, canonical in student-lifecycle-reset-tools.md |
| CEFR source of truth | PlacementResult (not yet built); self-reported CefrLevel is temporary fallback |
| Placement model | Standalone PlacementAssessment entity — not a LearningModule |
| Session model | Designed — LearningSession / SessionExercise; not yet implemented |
| File storage | Local filesystem (dev); IFileStorageService / MinIO designed for Phase 5 |
| dotnet test | 437 passed (at redesign sprint start) |
| npm run build | Passed |
| Playwright tests | 56 passed |

---

## Next Recommended Sprint

**Placement Assessment MVP (Phase 1)**

Scope:
- `StudentLifecycleStage` on `StudentProfile`
- Onboarding additions: session duration, professional experience level, role familiarity
- `PlacementAssessment` and `PlacementSection` entities (standalone, new tables)
- 6-section placement flow (backend + Angular)
- `placement_assessment_evaluate` AI prompt
- `PlacementResult` feeds `StudentSkillProfile` and `UserLearningSummary`
- Generates first `LearningPath` after placement completes
- Lifecycle-aware routing guard in Angular

Reference: [docs/architecture/placement-assessment-model.md](../architecture/placement-assessment-model.md)
