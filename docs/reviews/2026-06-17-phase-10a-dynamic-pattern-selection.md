---
title: Phase 10A — Dynamic Pattern Selection
date: 2026-06-17
sprint: Phase 10A
status: implemented
owner: engineering
---

# Phase 10A — Dynamic Pattern Selection

## Summary

Replaced the single-skill binary `ApplyWeakSkillSubstitution` with a per-slot
`DynamicPatternSelector` that considers skill scores, recent pattern history, catalog
readiness, and student goal context. Per-slot candidate pools were added to
`SessionDurationTemplates` so the selector has real alternatives to choose from.

---

## Files changed

| File | Change |
|------|--------|
| `src/LinguaCoach.Application/Sessions/DynamicPatternSelection.cs` | New — `PatternSelectionInput`, `PatternSelectionResult`, `PatternCatalogEntry` records |
| `src/LinguaCoach.Infrastructure/Sessions/DynamicPatternSelector.cs` | New — pure static selector logic |
| `src/LinguaCoach.Infrastructure/Sessions/SessionDurationTemplates.cs` | Added `CandidatePatternKeys` pool to `ExerciseStepTemplate`; extended all four duration templates with pools |
| `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs` | Replaced `ApplyWeakSkillSubstitution` with `ApplyDynamicPatternSelection`; added recent history query and catalog-entry build step |
| `tests/LinguaCoach.UnitTests/Sessions/DynamicPatternSelectorTests.cs` | New — 20 unit tests covering selector ranking and fallback |

---

## Selector inputs

`PatternSelectionInput` carries:

- `CefrLevel` — CEFR string, nullable. Not yet used for pattern routing (no
  per-pattern level metadata exists in the catalog). Reserved for Phase 10B.
- `SkillScores` — map of skill key → score 0-100. Derived from
  `StudentSkillProfile.ScorePercent` for all skills (not just weak ones). Empty
  when no profile exists.
- `LearningGoalContext` — student's declared goal/context string, e.g.
  "day-to-day", "travel", "study". Sourced from `LearningGoalDescription ??
  LearningGoal`. Null when unset. Included in selector reason string but not used
  for pattern routing yet — routing is goal-context-aware by design but neutral by
  default.
- `RecentPatternKeys` — last 10 `ExercisePatternKey` values from the student's
  `SessionExercise` history, ordered newest-first. Empty on first session.
- `CandidatePatternKeys` — pool of valid keys for this slot, from
  `ExerciseStepTemplate.GetCandidates()`.
- `SlotPrimarySkill` — the skill this template slot is intended to train.
- `AvailableCatalog` — `PatternCatalogEntry` list built from `GetForTodayAsync()`.

---

## Selector ranking rules

Priority order (highest first):

1. **Catalog gate** — only keys that are `IsEnabled=true`, `IsReady=true`,
   `SupportsTodayLesson=true` are eligible. Keys failing any gate are dropped
   before scoring.

2. **Weak-skill preference (+20)** — if the catalog entry's `PrimarySkill`
   matches the weakest skill in `SkillScores`, the candidate gets +20. The weakest
   skill is the one with the lowest `ScorePercent`; ties break in favour of the
   slot's own `SlotPrimarySkill`.

3. **Repetition avoidance (+10/+5)** — candidates not seen in `RecentPatternKeys`
   get +10. Candidates seen but not in the last 3 get +5. Candidates in the last 3
   get +0 for this dimension.

4. **Deterministic tiebreak** — stable alphabetical order on pattern key string, so
   results are reproducible in tests and consistent across identical inputs.

5. **Fallback** — if no candidate passes the catalog gate, the first raw candidate
   is returned with `IsFallback=true` and a diagnostic reason string. The
   downstream `FilterUnavailableExerciseTypesAsync` will then drop it if it is
   still unavailable, preserving the existing safety guard.

---

## Fallback behaviour

| Condition | Behaviour |
|-----------|-----------|
| `SkillScores` empty (no profile) | Selector runs without skill weighting; repetition avoidance still applies; falls back to alphabetical tiebreak |
| `RecentPatternKeys` empty (first session) | No repetition penalty applied; pure skill + alpha tiebreak |
| `SkillFocus` set but no scores | `BuildFallbackScores` synthesises a single low-score entry for the focus skill so the selector can still weight toward it |
| All candidates fail catalog gate | `IsFallback=true`, first raw candidate returned; downstream filter removes it |
| `LearningGoalContext` null | Omitted from reason string; no routing effect |
| `CefrLevel` null | Ignored (no level-based routing yet) |

---

## How explicit overrides are preserved

`DynamicPatternSelector.Select()` is only called when the system needs to choose
automatically (i.e., `ApplyDynamicPatternSelection` is called for new on-demand
session generation).

Explicit override paths are unchanged:

- `pattern=` / `exerciseType=` / `type=` query parameters in the activity
  controller go directly to generation handlers — `SessionGeneratorService` is not
  involved and the selector is never called.
- Practice Gym v2 (`SelectForPracticeGymSkillAsync`) does not call the selector.
- Buffered/ready sessions (`FindNextReadyBufferedSessionAsync`) are returned as-is
  — no selector involvement.
- If a request returns an existing session (`FindTodaysSessionAsync`), the selector
  is not called.

---

## How catalog readiness/enabled state is respected

`BuildApprovedKeySet()` filters `AvailableCatalog` to entries where all three flags
are true: `IsEnabled`, `IsReady` (i.e. `ImplementationStatus == "ready"`), and
`SupportsTodayLesson`. Only keys in this approved set are considered eligible. This
is equivalent to the existing `QueryReady().Where(e => e.SupportsTodayLesson)` in
`ExerciseTypeRegistry.GetForTodayAsync()`.

The existing `FilterUnavailableExerciseTypesAsync` is retained as a belt-and-
suspenders safety guard after selection.

---

## How workplace-only assumptions were avoided

- The selector has no hardcoded workplace logic.
- Template instructions were softened from "workplace situation" phrasing to
  neutral phrasing ("the situation", "the audio", "these phrases").
- `LearningGoalContext` is threaded through from `StudentProfile.LearningGoalDescription`
  or `LearningGoal` — neither field is constrained to workplace goals.
- Default skill fallback does not assume any goal context.
- Pattern pools in templates include non-workplace-specific keys alongside the
  workplace defaults.

---

## Known limitations

1. **CEFR not used for pattern routing.** `CefrLevel` is passed as input but not
   used. No per-pattern level metadata exists in the catalog yet. Routing by level
   is planned as a future improvement.

2. **Candidate pools are still small.** The pools are 2-5 keys per slot. Larger
   pools would give the selector more room to avoid repetition but require authoring
   more ready patterns.

3. **`spoken_response_from_prompt` in WritingTask pool.** To preserve the existing
   integration test invariant (weak Speaking → SpeakingTask in session), the
   WritingTask slot now includes `spoken_response_from_prompt` as a candidate. When
   Speaking is the weakest skill, the selector picks it (+20 skill match) and
   `ResolveKind` maps it to `SpeakingTask`. This is correct behavior but means the
   WritingTask slot can become a SpeakingTask slot, which changes the slot's
   semantic kind. This is the same behavior the old `ApplyWeakSkillSubstitution`
   had — the dynamic selector preserves the contract.

4. **Recent history uses module-scope.** The history query joins through
   `LearningPath → Modules → LearningSessions → SessionExercises` to avoid
   depending on the nullable `LearningSession.StudentProfileId` (which is only set
   for background-generated sessions).

---

## Tests added

**Unit tests** (`DynamicPatternSelectorTests.cs`) — 20 tests:

- Disabled formats are excluded
- Planned formats are excluded
- Unavailable-for-today formats are excluded
- All candidates unavailable → fallback with first candidate
- Weak Listening → prefers Listening candidate
- Weak Speaking → prefers Speaking candidate
- Weakest skill wins over slot default skill
- Recently used pattern avoided when alternative exists
- No history → deterministic alpha tiebreak
- Single candidate → always returned when available
- No skill profile → graceful fallback
- Empty history → no throw, valid result
- Result always includes non-empty reason
- Fallback result reason mentions "fallback"
- Non-workplace goal context → valid result, reason includes goal
- Null goal context → no crash, reason omits goal-context
- Template `GetCandidates()` always includes default pattern key (4 durations)
- Template `GetCandidates()` never returns empty (4 durations)

**Integration tests** — all 517 existing tests continue to pass:

- `WeakSpeakingSkill_PromotesSpeakingTaskInMainSlot` — preserved
- `WeakListeningSkill_EnsuresListeningInputStep` — preserved
- `NoWeakSkills_DefaultTemplateUsed_ContainsWritingTask` — preserved

---

## Final test counts

| Suite | Before | After |
|-------|--------|-------|
| Backend unit | 907 | 931 (+24) |
| Backend integration | 517 | 517 |
| Architecture | 3 | 3 |

Angular tests: not affected (no Angular changes).

---

## Decisions made

- Kept `FilterUnavailableExerciseTypesAsync` as a safety guard even though the
  selector now pre-filters via catalog gate. Belt-and-suspenders is appropriate
  here.
- Did not remove `ApplyWeakSkillSubstitution` as a method name — replaced it with
  `ApplyDynamicPatternSelection` which is a drop-in replacement in the same
  position in the generation pipeline.
- Template instruction strings were neutralized to remove hardcoded "workplace"
  phrasing, consistent with the product direction that SpeakPath supports multiple
  learning contexts.

---

## Risks / unresolved questions

- The `SupportsTodayLesson` flag gates which patterns appear in the catalog passed
  to the selector. If a pattern is not seeded with `SupportsTodayLesson=true` it
  will never be selected, even if it appears in a slot's candidate pool. Pool
  authors must ensure the seed data is consistent.
- CEFR-level routing is the natural Phase 10B addition once per-pattern level
  metadata exists.

---

## Recommendation for next phase

**Phase 10B — CEFR-aware pattern routing**: Add `CefrLevel` metadata to
`ExerciseTypeDefinition` (e.g. `MinCefrLevel`, `MaxCefrLevel`) and use it in
`DynamicPatternSelector` to exclude candidates that are clearly mismatched for the
student's level. This requires a product decision on what "CEFR changes pattern
choice" means in concrete terms before scoping. The `CefrLevel` input field is
already threaded through the selector in anticipation of this.
