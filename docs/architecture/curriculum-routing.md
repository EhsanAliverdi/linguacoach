---
status: current
lastUpdated: 2026-06-17 23:00
owner: architecture
supersedes:
supersededBy:
---

# Curriculum Routing — Architecture

Introduced in Phase 10L.

> **Forward reference (2026-07-17, docs-only):** this flat objective/tag model is targeted for
> replacement by a skill-graph + goal-vector + AI-composer architecture — see
> `docs/architecture/adaptive-curriculum-skill-graph.md`. Nothing below is deleted or changed yet;
> this doc still describes the current, live implementation. The replacement is phased and has not
> started.

---

## Purpose

Select suitable curriculum objectives and CEFR bands before every AI generation call.

The routing service is a pure, deterministic application-layer component:
- Does not call AI.
- Does not modify student data.
- Does not gate or block generation — always returns a recommendation.

---

## Components

### Application layer

| Type | Location | Role |
|---|---|---|
| `CurriculumRoutingRequest` | `Application/Curriculum/` | Input: student context, CEFR, skill, preferences, source |
| `CurriculumRoutingRecommendation` | `Application/Curriculum/` | Output: target CEFR, objective key/title, context/focus tags, reason |
| `ICurriculumRoutingService` | `Application/Curriculum/` | Interface: `RecommendAsync`, `NormalizeCefrLevel` |

### Infrastructure layer

| Type | Location | Role |
|---|---|---|
| `CurriculumRoutingService` | `Infrastructure/Curriculum/` | Implementation: normalization, candidate selection, fallback |
| `CurriculumRoutingRequestFactory` | `Infrastructure/Curriculum/` | Builds request from `StudentProfile` + `ResolvedLearningGoalContext` |

---

## CEFR Normalization

Input `StudentProfile.CefrLevel` is never modified.

For routing only, raw values are normalized:

| Input | Normalized |
|---|---|
| `B2` | `B2` |
| `B2+`, `B2-` | `B2` |
| `C1+` | `C1` |
| `null`, `unknown`, `` | `A1` (safe conservative fallback) |

---

## Routing Decision Flow

```
Request
  → NormalizeCefrLevel(profile.CefrLevel)
  → CurriculumContextMapper.MapFromResolvedContext(goalContext)
  → ICurriculumSyllabusQuery.GetCandidatesForStudentAsync(level, tags, focusAreas)
  → [skill filter if PrimarySkill set]
  → SelectBestCandidate (closest DifficultyBand to preference)
  → if none found AND AllowReviewOrScaffold=true → try one level down (Review reason)
  → if still none → Fallback (strip workplace from tags, return at student level)
  → CurriculumRoutingRecommendation
```

---

## RoutingReason

| Value | Meaning |
|---|---|
| `Normal` | Exact-level match found |
| `Review` | Lower-level content selected; `AllowReviewOrScaffold=true` |
| `Scaffold` | Reserved (same trigger as Review) |
| `Remediation` | Reserved |
| `Fallback` | No objective found; safe fallback |

When `RoutingReason != Normal`, `IsLowerLevelContent` may be true.
A handler must not silently use lower-level content — the reason and flag must be
passed into generation context and are visible in logs and debug UI.

---

## Workplace Context Rule

Routing never defaults to workplace context.

`CurriculumContextMapper` adds `workplace` only when `ResolvedLearningGoalContext.WorkplaceSpecific = true`.

The Fallback path strips `workplace` from context tags even if somehow present.

---

## DifficultyPreference → DifficultyBand

| Student preference | Target band |
|---|---|
| Gentle | 1 |
| Balanced / null | 2 |
| Challenging | 4 |

The candidate with the smallest `|DifficultyBand - preferredBand|` is selected.

---

## AI Prompt Integration

`CurriculumRoutingRecommendation` flows into `ActivityGenerationContext`:

```
ActivityGenerationContext.CefrLevel       ← routing.TargetCefrLevel
ActivityGenerationContext.RoutingContext  ← routing.RoutingContextSummary
ActivityGenerationContext.RoutingReason  ← routing.RoutingReason.ToString()
ActivityGenerationContext.IsReviewOrScaffold ← routing.IsLowerLevelContent
```

`DbPromptAiContextBuilder.InsertRoutingContext` appends routing context
before the "Return ONLY" instruction in the rendered prompt.

---

## Integration Points (Phase 10L)

| Handler | Source label | Description |
|---|---|---|
| `ActivityGetHandler.HandlePatternKeyedAsync` | `on_demand` | On-demand and Practice Gym pattern routing |
| `ExercisePrepareHandler` | `today_lesson` | Today's Lesson exercise preparation |
| `PracticeGymGenerationJob.MaterializeAsync` | `PracticeGymGenerationJob` | Background Practice Gym generation |
| `ActivityMaterializationJob.MaterializeExerciseAsync` | `ActivityMaterializationJob` | Background lesson batch activity materialization |
| `LessonBatchGenerationJob.BuildCompactSummaryAsync` | `lesson_batch` | AI lesson planning summary |

---

## AllowReviewOrScaffold

In Phase 10L all call sites pass `AllowReviewOrScaffold = false`.
The review/scaffold path is built and tested but not triggered in production flows.

Phase 10M (Adaptive Routing Enablement) should enable it per-handler based on
ledger weak signals and student performance patterns.

---

## Dependency Registration

```csharp
services.AddScoped<ICurriculumRoutingService, CurriculumRoutingService>();
```

`ICurriculumSyllabusQuery` (registered in Phase 10K) is a dependency of `CurriculumRoutingService`.

---

## Deferred

- `AllowReviewOrScaffold=true` in handlers → Phase 10M
- Session length → candidate count influence → Phase 10M/10N lesson planner
- CEFR-aware exercise format matrix → future admin-configurable control
- `StudentProfile.CefrLevel` migration to typed enum → TODOS.md
