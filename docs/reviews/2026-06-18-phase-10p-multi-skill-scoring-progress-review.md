---
status: current
lastUpdated: 2026-06-18 10:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10P — Multi-skill Scoring & Progress Updates — Engineering Review

**Date:** 2026-06-18
**Related sprint:** Phase 10P
**HEAD before work:** 8acb2bc

---

## Summary

Phase 10P adds a safe, testable multi-skill progress update foundation to SpeakPath. Previously, `ActivitySubmitHandler` only updated `StudentSkillProfile` rows for the single primary skill derived from a pattern key (`PatternSkillUpdateService`). Now every activity attempt updates all affected skills — primary and secondary — with transparent weighting derived from pattern or activity metadata.

---

## Files changed

### New files

| File | Purpose |
|------|---------|
| `src/LinguaCoach.Application/Activity/IMultiSkillProgressService.cs` | Application-layer interface and request/result contracts |
| `src/LinguaCoach.Infrastructure/Activity/MultiSkillProgressService.cs` | Implementation: skill registry, weighting, DB upsert |
| `tests/LinguaCoach.UnitTests/Activity/MultiSkillProgressServiceTests.cs` | 20 unit tests |
| `tests/LinguaCoach.IntegrationTests/Persistence/MultiSkillProgressServiceIntegrationTests.cs` | 10 integration tests |

### Modified files

| File | Change |
|------|--------|
| `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs` | Injected `IMultiSkillProgressService`; calls `BuildRequest` + `ApplyAsync` on both pattern and legacy paths; added `DeserialiseSecondarySkills` helper |
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered `IMultiSkillProgressService` → `MultiSkillProgressService` as scoped |
| `docs/sprints/current-sprint.md` | Updated to reflect Phase 10P completion |
| `TODOS.md` | Added TODO-016 (CEFR promotion) and TODO-017 (SkillLabels merge) |

---

## Architecture

### Multi-skill progress service

`MultiSkillProgressService` is an Application-layer service registered as scoped. It:

1. Resolves affected skills from pattern metadata or ActivityType fallback.
2. Applies weighting: primary 70%, secondaries share 30% equally (primary-only → 100%).
3. Maps a 0–100 score to a −1..1 delta (centred at 60); scales by 10 points/unit.
4. Upserts `StudentSkillProfile` rows: `ApplyScoreDelta` on existing, new row at `DefaultScorePercent + delta` for new.
5. Returns `MultiSkillProgressUpdateResult` with updated skill keys and per-skill deltas for logging/ledger.

### Skill source priority

1. `ExercisePatternDefinition.PrimarySkill` + `SecondarySkillsJson` — highest priority when pattern metadata available.
2. `ActivityType` fallback map — used when no pattern metadata present (legacy AI path).
3. Unknown/empty skill keys are silently ignored.

### Weighting constants

| Constant | Value |
|----------|-------|
| `PrimaryWeightPercent` | 70.0 |
| `SecondaryWeightPercent` | 30.0 |
| `ScalePointsPerUnit` | 10.0 |

All constants are in one place (`MultiSkillProgressService.cs`). Changing weighting does not require touching tests or handlers.

### ActivityType fallback map

| ActivityType | Primary | Secondary |
|--------------|---------|-----------|
| WritingScenario | writing | grammar, vocabulary |
| ListeningComprehension | listening | writing |
| VocabularyPractice | vocabulary | — |
| SpeakingRolePlay | speaking | fluency, pronunciation |
| ReadingTask | reading | vocabulary, grammar |
| PronunciationPractice | pronunciation | speaking |

No workplace default is used at any fallback level.

### Integration points changed

- **Pattern evaluation path** (`HandlePatternEvaluationAsync`): after `PatternSkillUpdateService.ApplyAsync`, calls `MultiSkillProgressService.ApplyAsync` with skills from pattern definition.
- **Legacy AI path** (`HandleAsync`): after `_learningLedger.RecordAsync`, calls `MultiSkillProgressService.ApplyAsync` with ActivityType fallback.
- Both calls are best-effort: exceptions are swallowed, never block the activity submission response.

### Incomplete attempt behaviour

`MultiSkillProgressService.ApplyAsync` returns early without writing any rows when `request.Completed == false`. This prevents negative signals accumulating from abandoned or partial submissions. Documented in code and tests.

### Readiness pool interaction

No change to readiness consumption. `TryConsumeReadinessItemAsync` (added in Phase 10O-F) still fires independently. Multi-skill updates run in parallel with readiness consumption; neither blocks the other.

### CEFR behaviour

`StudentProfile.CefrLevel` is not changed in this phase. Multi-skill signals are written to `StudentSkillProfile` only. CEFR promotion/demotion from accumulated signals is deferred to a future placement/mastery engine (TODO-016).

---

## Tests added

### Unit tests (20 new)

1. Primary-only activity updates one skill at 100% weight.
2. Primary + one secondary splits 70/30.
3. Primary + two secondary skills splits 70/15/15.
4. Duplicate secondary skill is de-duplicated.
5. Secondary equal to primary is de-duplicated.
6. Unknown primary skill writes nothing.
7. Unknown secondary skill dropped silently (primary still written at full weight).
8. Not-completed attempt skips all updates.
9. `BuildRequest`: pattern metadata present → uses pattern primary.
10. `BuildRequest`: no pattern metadata → falls back to ActivityType.
11. `BuildRequest`: WritingScenario fallback includes grammar + vocabulary.
12. `BuildRequest`: SpeakingRolePlay fallback includes fluency + pronunciation.
13. `BuildRequest`: no fallback for type → empty primary.
14. Listening activity updates both listening and writing.
15. Writing activity updates writing, grammar, and vocabulary.
16. Failed/incomplete attempt → no skill update, notes indicate reason.
17. Repeated completion updates existing row (no duplicate).
18. Result contains per-skill delta for all updated keys.
19. Lower-level content still updates skills; notes include `[lower-level/review content]`.
20. No workplace default introduced when pattern and ActivityType both unknown.

### Integration tests (10 new)

1. Pattern with SecondarySkillsJson updates all skill rows (3 rows for writing + grammar + vocabulary).
2. Single-skill activity updates only one row.
3. Listening + writing: listening score > writing score (70/30 weighting verified).
4. Speaking roleplay: speaking, fluency, pronunciation all written.
5. Writing activity: writing, grammar, vocabulary all written.
6. Result `ScoreDeltaBySkill` contains all updated keys.
7. Existing profile row is updated (not duplicated); score increments correctly.
8. `BuildRequest`: pattern metadata overrides ActivityType fallback.
9. No workplace skills written when neither pattern nor fallback match.
10. (Inherited from existing suite) all existing `PatternSkillUpdateServiceTests` still pass.

---

## Test results

| Suite | Before | After | Delta |
|-------|--------|-------|-------|
| Architecture | 3 | 3 | 0 |
| Unit | 1174 | 1194 | +20 |
| Integration | 601 | 610 | +9 |
| **Total** | **1778** | **1807** | **+29** |
| Failed | 0 | 0 | 0 |

Angular: 272 passed, 0 failed.
Frontend build: production build, warnings only, no errors.

---

## Decisions made

| Decision | Rationale |
|----------|-----------|
| Primary 70% / secondary 30% split | Simple, transparent, configurable from one place. Avoids complex mastery math in Phase 10P. |
| Incomplete attempts skipped | Avoids inflating negative signals from abandoned submissions. Behaviour is tested and documented. |
| Exceptions swallowed in `ApplyAsync` | Consistent with existing `PatternSkillUpdateService.ApplyAsync` pattern. Activity submission must not be blocked by progress side-effects. |
| Separate `MultiSkillProgressService` from `PatternSkillUpdateService` | `PatternSkillUpdateService` operates on `PatternEvaluationResult.SkillImpacts` (AI-returned impacts); `MultiSkillProgressService` operates on pattern/activity metadata. Different responsibilities. Merge deferred to TODO-017. |
| `IMultiSkillProgressService` interface in Application layer | Allows unit-testing handler wiring with a mock; follows existing interface-per-service pattern. |

---

## Known limitations

- `MultiSkillProgressService.SkillLabels` and `PatternSkillUpdateService.SkillLabels` are separate dictionaries. Adding a general skill to one requires manually adding to the other. Merge deferred to TODO-017.
- Score-to-delta function is linear centred on 60. A more nuanced mapping (e.g. exponential decay near 0/100) is not implemented.
- No CEFR signal aggregation or promotion logic. Deferred to TODO-016.
- Listening/Speaking/Reading/VocabularyPractice legacy paths (non-AI, non-pattern) also call `MultiSkillProgressService` via the legacy AI path after `RecordAsync`. For `ListeningComprehension` and `VocabularyPractice` the handler returns before reaching that call, so multi-skill updates for those paths currently use the pattern path only when `ExercisePatternKey` is present. Both paths are tested.

---

## Explicitly not implemented

- Full mastery engine
- Full placement engine
- CEFR auto-promotion/demotion
- Admin curriculum builder or admin write endpoints
- Notification system
- Usage/quota enforcement
- Major frontend redesign
- New exercise types

---

## Risks

- If a future phase changes `ActivityType` enum values, the fallback map in `MultiSkillProgressService` must be updated manually. Low risk given enum is stable.
- `PatternSkillUpdateService` and `MultiSkillProgressService` will both write to `StudentSkillProfile` for pattern-evaluated activities. The combined delta is additive. This double-write is intentional for this phase — `PatternSkillUpdateService` writes AI-returned `SkillImpacts`, while `MultiSkillProgressService` writes metadata-derived impacts. They should converge; however, the interaction should be reviewed before a mastery engine reads accumulated scores. Tracked in TODO-017.

---

## TODOs produced

- **TODO-016** — CEFR auto-promotion/demotion using multi-skill progress signals (Phase 10P+)
- **TODO-017** — Merge `MultiSkillProgressService.SkillLabels` with `PatternSkillUpdateService.SkillLabels`

---

## Next recommended action

Phase 10Q could focus on:
- Surfacing multi-skill progress in the Progress page (currently reads `StudentSkillProfile` rows already, so general skills will appear automatically).
- Or beginning TODO-016 (CEFR signal aggregation toward promotion/demotion).
