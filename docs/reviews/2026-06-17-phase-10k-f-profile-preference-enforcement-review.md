---
status: current
lastUpdated: 2026-06-17 22:00
owner: engineering
---

# Phase 10K-F — Profile Preference Enforcement Audit & Routing Fix

**Date:** 2026-06-17
**Sprint:** Phase 10K-F
**HEAD before work:** 82ba8c6
**Related sprint:** Phase 10K (curriculum syllabus foundation)

---

## Purpose

Audit whether student profile preferences (learning goals, focus areas, support language, translation help, difficulty, session length, CEFR level) are fully propagated into Today lessons, Practice Gym, activity generation, background jobs, AI prompt context, and ledger metadata. Fix any missing propagation before Phase 10L CEFR-aware routing.

---

## Files Reviewed

- `src/LinguaCoach.Application/Activity/IAiActivityGenerator.cs`
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`
- `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Infrastructure/Ai/LearnerPreferenceContextFormatter.cs`
- `src/LinguaCoach.Application/Learning/ResolvedLearningGoalContext.cs`
- `src/LinguaCoach.Infrastructure/Learning/LearningGoalContextResolver.cs`
- `src/LinguaCoach.Application/Curriculum/CurriculumContextMapper.cs`
- `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs`
- `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs`
- `src/LinguaCoach.Infrastructure/Jobs/PracticeGymGenerationJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/ActivityMaterializationJob.cs`
- `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs`
- `src/LinguaCoach.Domain/Entities/StudentProfile.cs`

---

## Audit Findings

### (a) Correctly Propagated (pre-existing)

| Preference | Propagation path |
|---|---|
| `PreferredName` | `LearnerPreferenceContextFormatter` → all 5 generation surfaces |
| `SupportLanguageName` | Formatter + `ResolvedLearningGoalContext` → all 5 generation surfaces |
| `SupportLanguageCode` | `ResolvedLearningGoalContext` (available, but not in `ActivityGenerationContext`) |
| `TranslationHelpPreference` | Formatter → all 5 generation surfaces |
| `DifficultyPreference` | Formatter + resolver → all 5 generation surfaces |
| `LearningGoals` / `FocusAreas` | Formatter + resolver → all 5 generation surfaces |
| `WorkplaceSpecific` | Derived by `LearningGoalContextResolver` from keyword scan; never hardcoded |
| `PreferredSessionDurationMinutes` | `SessionGeneratorService` → `SessionDurationTemplates.NormalizeDuration()` |
| `CefrLevel` | `ActivityGenerationContext.CefrLevel` on all 5 generation surfaces |
| `CurriculumContextMapper` null guard | Returns `[general_english]` on null input — correct |

All five AI-calling generation surfaces (`ActivityGetHandler`, `ExercisePrepareHandler`, `ActivityMaterializationJob`, `PracticeGymGenerationJob`, `LessonBatchGenerationJob`) correctly pass both `LearnerPreferenceContext` and `LearningGoalContext`.

### (b) Missing / Partial (fixed in this phase)

#### P1 — `ActivityEvaluationContext` carried zero preference data

The legacy AI evaluation path (used for `WritingScenario` and `SpeakingRolePlay`) constructed `ActivityEvaluationContext` without `LearnerPreferenceContext` or `LearningGoalContext`. Students' difficulty preference, support language, and goals could not influence AI feedback tone or framing.

**Fix:** Added `LearnerPreferenceContext` and `LearningGoalContext` optional fields to `ActivityEvaluationContext` (record in `IAiActivityGenerator.cs`). `ActivitySubmitHandler` now passes both fields from the formatter and resolver. `AiActivityGeneratorHandler.EvaluateAttemptAsync` now includes `learnerPreferences` and `learningGoalContext` in the evaluation prompt variable dict.

#### P2 — `VocabularyPractice` cadence unconditionally routed to `GapFillWorkplacePhrase`

`ActivityGetHandler` line 104 always called `HandlePatternKeyedAsync(GapFillWorkplacePhrase, ...)` for cadence vocabulary picks, regardless of the student's learning goals. A student with "Day-to-day English" or "Travel English" would receive workplace-labelled vocabulary exercises.

**Fix:** The cadence path now resolves the student's goal context and gates on `WorkplaceSpecific`:
- `WorkplaceSpecific == true` → `GapFillWorkplacePhrase`
- `WorkplaceSpecific == false` → `PhraseMatch`

This ensures a non-workplace student receives vocabulary practice without workplace framing.

#### P3 — `PreferredSessionDurationMinutes` absent from `LessonBatchGenerationJob` compact summary

The AI lesson planner received student skill data, CEFR level, career context, and preference context — but not the student's preferred session duration. The AI planned sessions of arbitrary length. `preferredSessionDurationMinutes` is now included in `learnerPreferences` in the compact summary JSON sent to the planner.

### (c) Noted but not fixed in this phase

| Gap | Decision |
|---|---|
| `SupportLanguageCode` (BCP-47) not in `ActivityGenerationContext` | Current prompts use `SupportLanguageName` (prose). No prompt template uses the code. Deferred to 10L if prompt templates need it. |
| `TranslationHelpPreference` not on `ResolvedLearningGoalContext` | Low risk — formatter covers all AI surfaces. Deferred. |
| `WorkplaceSpecific` sent as text summary to prompts, not as explicit boolean | Prompts infer from goal summary text. Acceptable for now. Revisit in 10L. |
| `DifficultyPreference` absent from pattern evaluation path | Pattern evaluators (`ExactMatch`, `KeyedSelection`) are deterministic — no AI call, no preference needed. AI-evaluated patterns (`AiStructured`, `AiOpenEnded`) receive preference via the shared evaluation path — now fixed by P1. |

---

## Decisions Made

1. `PhraseMatch` is the general-English fallback for vocabulary cadence. `GapFillWorkplacePhrase` is workplace-only. Both pattern keys are seeded and available.
2. `ActivityEvaluationContext` gains optional preference fields with `null` default — fully backward compatible.
3. `AiActivityGeneratorHandler.EvaluateAttemptAsync` now passes `learnerPreferences` and `learningGoalContext` variables into the evaluation prompt variable dict. Existing prompts that don't reference these variables ignore them safely.
4. `preferredSessionDurationMinutes` is added to the batch generation compact summary as a hint; the AI planner may use or ignore it. The `SessionDurationTemplates` enforcement in `SessionGeneratorService` is the authoritative gate.

---

## Tests Added

File: `tests/LinguaCoach.UnitTests/Application/PreferenceEnforcementTests.cs`

20 new unit tests covering:

| Category | Tests |
|---|---|
| General English fallback | Empty profile resolves to non-workplace; Day-to-day, Travel, Social goals are not workplace-specific |
| Workplace gate | `WorkplaceEnglish` goal correctly sets `WorkplaceSpecific = true` |
| Goals/focus areas in formatter | Learning goals and focus areas appear in formatter output; Day-to-day does not produce workplace copy |
| Support language + translation | `SupportLanguageName` in formatter and resolver; `AlwaysAvailable` renders correctly |
| Difficulty preference | Included in formatter output and resolver context |
| Session length | `PreferredSessionDurationMinutes` stored on profile; null when not set |
| `CurriculumContextMapper` | Null input → `general_english`; non-workplace → no `workplace` tag; workplace context → `workplace` tag |
| Vocabulary cadence guard | Non-workplace → `PhraseMatch`; workplace → `GapFillWorkplacePhrase` |

---

## Test Counts

| Suite | Before | After |
|---|---|---|
| Unit | 1078 | 1098 (+20) |
| Integration | 555 | 555 |
| Architecture | 3 | 3 |
| **Total** | **1636** | **1656** |

---

## Files Changed

| File | Change |
|---|---|
| `src/LinguaCoach.Application/Activity/IAiActivityGenerator.cs` | Added `LearnerPreferenceContext` and `LearningGoalContext` optional fields to `ActivityEvaluationContext` |
| `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs` | Pass formatter and resolver output into evaluation context |
| `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs` | Wire `learnerPreferences` and `learningGoalContext` into evaluation prompt variable dict |
| `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` | Gate vocabulary cadence on `WorkplaceSpecific` from resolved goal context |
| `src/LinguaCoach.Infrastructure/Jobs/LessonBatchGenerationJob.cs` | Add `preferredSessionDurationMinutes` to compact summary |
| `tests/LinguaCoach.UnitTests/Application/PreferenceEnforcementTests.cs` | 20 new preference enforcement tests (new file) |

---

## CI Gates

| Gate | Result |
|---|---|
| `git diff --check` | Clean |
| `dotnet restore` | Clean |
| `dotnet build --configuration Release` | Clean (0 errors, pre-existing warnings only) |
| `dotnet test --configuration Release` | 1656 passed, 0 failed |
| Angular build | Blocked — pre-existing Node 24 + path-with-space issue (`c:\My GitHub\...`). No Angular source changed in this phase. |
| Angular unit tests | Same blocker. |
| Playwright | Same blocker. |

Frontend gates are blocked by the same pre-existing environment issue documented in Phase 10K. No Angular source was changed in Phase 10K-F. The blocker is environment-only.

---

## Risks and Unresolved Questions

- Existing evaluation prompt templates (`activity_evaluate_writing`, `activity_evaluate_speaking_role_play`) do not yet reference `{{learnerPreferences}}` or `{{learningGoalContext}}` variables. The variables are now available in the dict; prompts must be updated in a subsequent prompt-engineering pass to actually use them. Adding unused prompt variables is safe — `AiPromptContextBuilder.BuildAsync` ignores unreferenced variables.
- `SupportLanguageCode` is not yet in `ActivityGenerationContext`. If a future prompt needs the BCP-47 code (e.g. for structured language switching), it must be added to the generation context as well.

---

## Explicit Confirmation: Out-of-Scope Items Not Implemented

The following were explicitly excluded from Phase 10K-F:

- Full 10L CEFR-aware format/content routing
- Exercise format locking by CEFR
- Readiness pools
- Background replenishment lifecycle states
- Practice Gym suggested practice UI redesign
- Admin curriculum write UI
- `StudentProfile.CefrLevel` migration
- Plus-level routing
- Full placement engine

---

## Next Recommended Action

Phase 10L: CEFR-aware content routing — use `CefrLevel` and `WorkplaceSpecific` to constrain which `CurriculumObjective` candidates are selected, and begin enforcing exercise format constraints by CEFR band.
