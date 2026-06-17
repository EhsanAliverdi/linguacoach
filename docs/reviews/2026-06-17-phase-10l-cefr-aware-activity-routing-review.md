---
status: current
lastUpdated: 2026-06-17 22:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10L — CEFR-Aware Activity Routing — Engineering Review

**Date:** 2026-06-17
**Related sprint:** Phase 10L
**HEAD before work:** 09f7f7f

---

## Summary

Phase 10L introduces a pure application-layer routing policy that decides suitable
CEFR bands and curriculum objectives before every AI generation call.

The routing service uses the Phase 10K curriculum syllabus foundation
(`CurriculumObjective`, `ICurriculumSyllabusQuery`) and the Phase 10K-F preference
propagation fixes (`ILearningGoalContextResolver`, `CurriculumContextMapper`) to
select appropriate objectives without calling AI.

---

## Files Changed

### New — Application layer

| File | Description |
|---|---|
| `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRequest.cs` | Input model: student context, CEFR level, skill, source, goal context, preferences |
| `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRecommendation.cs` | Output model: target level, allowed levels, objective key/title, context/focus tags, difficulty band, RoutingReason, IsLowerLevelContent, explanation |
| `src/LinguaCoach.Application/Curriculum/ICurriculumRoutingService.cs` | Interface with `RecommendAsync` and `NormalizeCefrLevel` |

### New — Infrastructure layer

| File | Description |
|---|---|
| `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs` | Implementation: CEFR normalization, candidate selection, difficulty band mapping, fallback rules |
| `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingRequestFactory.cs` | Static factory building `CurriculumRoutingRequest` from `StudentProfile` + resolved goal context |

### Modified — AI generator

| File | Change |
|---|---|
| `src/LinguaCoach.Application/Activity/IAiActivityGenerator.cs` | Added `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold` to `ActivityGenerationContext` |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Injects `routingContext` and `routingReason` into AI prompt variables |
| `src/LinguaCoach.Infrastructure/Ai/DbPromptAiContextBuilder.cs` | Added `InsertRoutingContext` method — appends routing context before "Return ONLY" |

### Modified — Generation handlers (routing wired in)

| File | Change |
|---|---|
| `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` | Injects `ICurriculumRoutingService`; `HandlePatternKeyedAsync` uses `routing.TargetCefrLevel` |
| `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs` | Injects `ICurriculumRoutingService`; uses routing before `ActivityGenerationContext` build |
| `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs` | Injects `ICurriculumRoutingService`; uses `routing.TargetCefrLevel` in generation context |
| `src/LinguaCoach.Infrastructure/Jobs/ActivityMaterializationJob.cs` | Injects `ICurriculumRoutingService`; uses `routing.TargetCefrLevel` in generation context |
| `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs` | Injects `ICurriculumRoutingService`; adds `routingLevel`, `routingContext`, `routingReason`, `curriculumObjective` to batch summary |

### Modified — DI

| File | Change |
|---|---|
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registers `ICurriculumRoutingService → CurriculumRoutingService` (Scoped) |

### New — Tests

| File | Count |
|---|---|
| `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs` | 16 unit tests |
| `tests/LinguaCoach.IntegrationTests/Curriculum/CurriculumRoutingIntegrationTests.cs` | 7 integration tests |

### Fixed — Existing test

| File | Change |
|---|---|
| `tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs` | Added `ICurriculumRoutingService` argument to manual constructor call |

---

## Routing Rules Implemented

1. **CEFR normalization:** `B2+` → `B2`, `C1-` → `C1`, unknown/null → `A1`.
   Does NOT modify `StudentProfile.CefrLevel`. Display level preserved.

2. **Candidate selection:** `ICurriculumSyllabusQuery.GetCandidatesForStudentAsync` filters
   by normalized CEFR level and context tags from `CurriculumContextMapper`.

3. **Skill filter:** if a primary skill is requested and skill-filtered candidates exist,
   prefer them over unfiltered candidates.

4. **Exact-level first:** candidates at the student's normalized level are tried first.

5. **Lower-level content requires `AllowReviewOrScaffold = true`:** if no exact-level
   candidate is found and review is allowed, one level down is attempted.
   Result carries `RoutingReason.Review` and `IsLowerLevelContent = true`.

6. **No silent level lowering:** if `AllowReviewOrScaffold = false` and no exact-level
   candidate exists, routing returns `RoutingReason.Fallback` at the student's own band.

7. **Non-workplace default:** `CurriculumContextMapper` never adds `workplace` unless
   `ResolvedLearningGoalContext.WorkplaceSpecific = true`. Fallback strips workplace
   from context tags to prevent accidental workplace routing.

8. **DifficultyPreference → DifficultyBand:**
   - Gentle → band 1
   - Balanced/null → band 2
   - Challenging → band 4
   Selects the candidate whose DifficultyBand is closest to the preferred band.

9. **Context tag flow:** goal-key mapping (travel, job_interviews, social, academic, etc.)
   is delegated to `CurriculumContextMapper`, keeping routing stateless.

---

## RoutingReason Values

| Value | Meaning |
|---|---|
| Normal | Exact-level match |
| Review | Lower-level content, AllowReviewOrScaffold=true |
| Scaffold | (Reserved — same logic as Review, available for future differentiation) |
| Remediation | (Reserved) |
| Fallback | No objective found at any level; safe fallback at student's band |

---

## AI Prompt Integration

Routing context reaches AI prompts via:

1. `ActivityGenerationContext.RoutingContext` → `variables["routingContext"]`
2. `DbPromptAiContextBuilder.InsertRoutingContext` appends before "Return ONLY"
3. Format: `Curriculum context: <objective title>. Context: <tags>. Mode: <reason>.`

The `cefrLevel` variable passed to the AI prompt is now `routing.TargetCefrLevel`
(normalized) instead of raw `profile.CefrLevel ?? "B1"`.

---

## Integration Points

| Handler | Source label | Notes |
|---|---|---|
| `ActivityGetHandler.HandlePatternKeyedAsync` | `on_demand` | On-demand Practice Gym and legacy type routing |
| `ExercisePrepareHandler` | `today_lesson` | Today's Lesson session exercise preparation |
| `PracticeGymGenerationJob.MaterializeAsync` | `PracticeGymGenerationJob` | Background Practice Gym generation |
| `ActivityMaterializationJob.MaterializeExerciseAsync` | `ActivityMaterializationJob` | Background lesson batch activity materialization |
| `LessonBatchGenerationJob.BuildCompactSummaryAsync` | `lesson_batch` | Lesson batch AI planning summary |

---

## Tests

### Unit (16 new)

- CEFR normalization: core levels, plus/minus suffixes, null/unknown fallback
- Exact-level objective preferred over lower-level
- Lower-level NOT selected silently when AllowReviewOrScaffold=false
- Lower-level selected with Review reason when allowed
- Day-to-day/non-workplace never routes to workplace
- Workplace goal routes to workplace context
- No active objective → fallback is not workplace
- Skill filter prefers matching-skill objective
- Gentle difficulty prefers band 1, Challenging prefers band 4/5
- Travel goal maps to travel context tag
- Null CEFR → A1 safe fallback
- RoutingContextSummary present when objective matched, null on fallback

### Integration (7 new)

- `ICurriculumRoutingService` is registered in DI
- `NormalizeCefrLevel` via DI instance strips plus/minus
- Seeded objectives present → Normal match returned
- B2 student never silently receives B1 content
- Day-to-day goal → context tags exclude workplace
- Workplace goal → context tags include workplace
- Routing is deterministic (idempotent for same input)

### Total test counts

| Suite | Before | After |
|---|---|---|
| Architecture | 3 | 3 |
| Unit | 1098 | 1124 |
| Integration | 555 | 565 |
| **Total** | **1656** | **1692** |

---

## Acceptance Criteria — Status

| Criterion | Status |
|---|---|
| Clear routing service/policy exists | DONE |
| Uses CurriculumObjective candidates, CEFR, profile, goal context | DONE |
| Does not default to workplace | DONE |
| Does not silently lower level | DONE |
| Lower-level content marked review/scaffold/remediation/fallback | DONE |
| Today/session generation consumes routing recommendation | DONE (ExercisePrepareHandler) |
| Practice Gym generation consumes routing recommendation | DONE (PracticeGymGenerationJob) |
| On-demand activity generation consumes routing recommendation | DONE (ActivityGetHandler) |
| AI prompt/generation context includes routing recommendation | DONE |
| Tests cover CEFR, context, focus, lower-level fallback, non-workplace defaults | DONE |
| Backend tests pass | DONE — 1692 passed, 0 failed |
| Frontend gates | Blocked by pre-existing Node 24 + path-with-space environment issue. No Angular source changed. |
| Docs updated | DONE |
| Commit created | In progress |

---

## What Was NOT Implemented (Intentionally Deferred)

- Readiness pools
- Background replenishment lifecycle states
- Practice Gym suggested practice UI redesign
- Admin curriculum write UI
- `StudentProfile.CefrLevel` migration (raw value preserved; routing normalizes at call time only)
- Plus-level persistence (B2+ not persisted; normalized only for routing)
- Full placement engine
- Full mastery engine
- Major UI redesign
- Session length influence on candidate count (belongs to 10M/10N lesson planner)
- CEFR-aware exercise format matrix (deferred to future admin-configurable control)
- `AllowReviewOrScaffold = true` usage in any handler (all wired as false in 10L; enablement belongs to 10M adaptive logic)

---

## Known Limitations

- `AllowReviewOrScaffold` is always false in all current handler call sites.
  The review/scaffold path is built and tested but not yet triggered in production flows.
  A future phase (adaptive routing, weak-skill remediation) should enable it per-handler.

- Session length preference is captured in `CurriculumRoutingRequest.PreferredSessionDurationMinutes`
  but not yet used to influence candidate count or workload. Full lesson planner belongs to 10M/10N.

- CEFR-aware format suitability (simpler formats for A1, longer tasks for B2+) is not yet
  implemented as an explicit format policy matrix. Current routing selects objectives by CEFR
  but does not gate exercise format by level. Deferred per scope.

- `CurriculumContextTagConstants.ExamInspired` and `Custom` are mapped by `CurriculumContextMapper`
  but no seeded objectives carry these tags yet. Routing falls back gracefully.

---

## Risks

- Low: routing service is pure/deterministic — no AI calls, no DB writes.
- Low: fallback path is always available and safe (general_english at student's band).
- Low: if `ICurriculumSyllabusQuery` returns empty (e.g. empty DB), routing returns Fallback,
  which passes the raw profile CEFR level through unchanged. No regression vs pre-10L behaviour.

---

## Next Recommended Action

Phase 10M — Adaptive Routing Enablement:
enable `AllowReviewOrScaffold = true` based on ledger weak signals, wire session length
to candidate count in `SessionGeneratorService`, add admin debug field showing routing reason.
