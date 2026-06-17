# Phase 10J — Learning Goal Context Resolver

**Date:** 2026-06-17
**Related sprint:** Phase 10J (context consistency phase)
**Type:** Implementation review

---

## Files reviewed / created

### New files
- `src/LinguaCoach.Application/Learning/ResolvedLearningGoalContext.cs`
- `src/LinguaCoach.Application/Learning/ILearningGoalContextResolver.cs`
- `src/LinguaCoach.Infrastructure/Learning/LearningGoalContextResolver.cs`
- `tests/LinguaCoach.UnitTests/Application/LearningGoalContextResolverTests.cs`
- `tests/LinguaCoach.IntegrationTests/Learning/LearningGoalContextResolverIntegrationTests.cs`

### Modified files
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — DI registration
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` — 3 call sites replaced
- `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs` — 2 ledger call sites replaced
- `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs` — 1 call site replaced
- `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs` — 1 call site replaced
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs` — 1 call site replaced
- `src/LinguaCoach.Infrastructure/Jobs/ActivityMaterializationJob.cs` — 1 call site replaced
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs` — 1 call site replaced
- `tests/LinguaCoach.IntegrationTests/Sessions/SessionGeneratorServiceTests.cs` — updated constructor
- `tests/LinguaCoach.IntegrationTests/Sessions/LessonBatchGenerationJobTests.cs` — updated constructor

---

## What was built

### Problem
`LearnerPreferenceContextFormatter.BuildLearningGoalContext()` was called in 7 places across generation and submission paths. It used a priority chain of `CustomLearningGoal → LearningGoals → LearningGoalDescription → LearningGoal → CareerContext`, but:
- Had no structured output — returned a plain nullable string
- Carried no metadata (source, workplace flag, support language, difficulty)
- Could not be tested in isolation without calling the static formatter
- Produced inconsistent results because callers could not inspect or enrich the context

### Solution
Created `ILearningGoalContextResolver` / `LearningGoalContextResolver` with:
- A strict 5-level priority chain (see below)
- Structured `ResolvedLearningGoalContext` return type with source tracking, workplace detection, legacy fallback flag, support language, difficulty
- Bounded `ContextSummary` (max 200 chars) suitable for ledger metadata and AI prompts
- `LearningGoalResolutionContext` call context for explicit overrides and source tagging

### Priority chain (implemented strictly)

| Priority | Source | Condition |
|----------|--------|-----------|
| 1 | `ExplicitGoalOverride` | from `LearningGoalResolutionContext` |
| 2 | `LearningGoals` + `FocusAreas` | Phase 10G / 10I structured list fields |
| 3 | `CustomLearningGoal` / `CustomFocusArea` | free-text structured fields |
| 4a | `LearningGoalDescription` | legacy v1 onboarding field |
| 4b | `LearningGoal` | legacy v1 onboarding field |
| 4c | `CareerContext` | legacy v1 onboarding field |
| 5 | `"general English communication"` | fallback — never workplace-only |

### Workplace detection
`WorkplaceSpecific = true` only when resolved summary contains any of: `workplace`, `professional`, `business`, `office`, `career`, `work`, `corporate`, `industry`, `job`, `meeting`, `presentation`, `colleague`, `client`, `email` — checked case-insensitively. Generic fallback always produces `WorkplaceSpecific = false`.

---

## Decisions made

1. **Resolver is additive** — `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` is kept intact but no longer called externally. Existing test coverage for the formatter is preserved.

2. **Singleton registration** — resolver is stateless; `AddSingleton` is appropriate and avoids per-request allocation.

3. **ContextSummary replaces the old string** in all ledger records (`StudentLearningEvent.LearningGoalContext`). Old records without this field already handled by nullable column — no migration needed.

4. **`LearningGoalResolutionContext.Source`** is set at each call site for traceability in future ledger analytics.

5. **No curriculum routing / readiness pools / background generation** — explicitly deferred.

---

## Generation paths wired

| Call site | Source tag |
|-----------|-----------|
| `ActivityGetHandler` (3 sites) | `"ActivityGetHandler"` |
| `ActivitySubmitHandler.Legacy` | `"ActivitySubmitHandler.Legacy"` |
| `ActivitySubmitHandler.Pattern` | `"ActivitySubmitHandler.Pattern"` |
| `ExercisePrepareHandler` | `"ExercisePrepareHandler"` |
| `SessionGeneratorService` | `"SessionGeneratorService"` |
| `PracticeGymGenerationJob` | `"PracticeGymGenerationJob"` |
| `ActivityMaterializationJob` | `"ActivityMaterializationJob"` |
| `LessonBatchGenerationJob` | `"LessonBatchGenerationJob"` |

---

## Test counts

- Unit tests: 18 (all pass)
- Integration tests: 2 (all pass)

### Unit test coverage
1. Explicit override used first
2. LearningGoals from profile included in summary
3. CustomLearningGoal included
4. LegacyFallbackUsed = true for LearningGoalDescription source
5. LegacyFallbackUsed = true for LearningGoal source
6. LegacyFallbackUsed = true for CareerContext source
7. Generic fallback when all fields empty
8. WorkplaceSpecific = true for workplace goal
9. WorkplaceSpecific = true for career context
10. WorkplaceSpecific = false for travel goal
11. WorkplaceSpecific = false for generic fallback
12. SupportLanguage included in result
13. DifficultyPreference included in result
14. Null context does not throw
15. Null profile throws ArgumentNullException
16. Structured source sets LegacyFallbackUsed = false
17. FocusAreas only — source Structured, LegacyFallbackUsed false
18. Long goal — ContextSummary bounded at 200 chars

### Integration test coverage
1. Profile with structured goals returns them with full metadata
2. Empty profile generic fallback is not workplace-biased

---

## Risks / unresolved questions

- `SetSkillAndGoal()` has onboarding step guards so `LearningGoalDescription` cannot be set directly in unit tests without reflection. Tests use reflection for that field only. This is acceptable for the legacy coverage scenario.
- `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` has a slightly different priority order (it puts `CustomLearningGoal` first, then `LearningGoals`, then legacy fields). The resolver intentionally reverses this to put structured list fields first. This is a deliberate correction — custom free-text is secondary to structured selections. Existing formatter tests still pass and the formatter is no longer called externally.

---

## Backward compatibility

- Old `StudentLearningEvent` rows with `LearningGoalContext = null` are unaffected — field is nullable.
- Old rows where `LearningGoalContext` was set by the formatter still read correctly — the column stores a string.
- No migration needed.

---

## Final verdict

Phase 10J delivered. Resolver is consistent, testable, and backward-compatible. All 7 call sites migrated. No curriculum routing or background generation implemented.

## Next recommended action

Phase 10K (if planned): wire `ResolvedLearningGoalContext.WorkplaceSpecific` into domain complexity cap logic in `SessionGeneratorService`. Or proceed to Dynamic Pattern Selection using ledger signals now that context is consistent.
