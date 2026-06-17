---
status: current
lastUpdated: 2026-06-17 14:00
owner: engineering
supersedes:
supersededBy:
---

# Phase 10I — Configurable Multi-step Onboarding / Assessment v2 Foundation
# Engineering Review

Date: 2026-06-17
Related sprint: Phase 10I
Files reviewed: StudentProfile.cs, OnboardingController.cs, OnboardingCommands.cs, OnboardingStep.cs, OnboardingStatus.cs, existing migration chain (T46 latest)

---

## Decisions made

| # | Decision | Rationale |
|---|---|---|
| D1 | Run v2 in parallel with existing v1 state machine | Existing completed students must not be regressed. Old OnboardingStatus/OnboardingStep fields remain untouched. New StudentOnboardingProgress is created lazily. |
| D2 | Include 3-4 assessment questions with simple scoring | Spec intent: infrastructure plus minimal working assessment end-to-end. Simple score → preliminary CEFR stored as system-estimated. |
| D3 | Include read-only GET /api/admin/onboarding/flow | Small, consistent with existing admin patterns. Proves admin boundary is wired. No write endpoint in this phase. |

---

## Architecture

### New domain entities

```
OnboardingFlowDefinition
  Id (Guid, PK)
  Name (string, max 200)
  IsActive (bool)             -- only one active at a time (enforced by seeder)
  Version (int)
  CreatedAt (DateTimeOffset)

OnboardingStepDefinition
  Id (Guid, PK)
  FlowDefinitionId (Guid, FK)
  StepKey (string, max 100)   -- stable key e.g. "preferred_name"
  Title (string, max 200)
  Description (string?, max 2000)
  StepType (OnboardingStepTypeV2 enum)
  RequirementType (OnboardingStepRequirementType enum)
  StepOrder (int)
  IsEnabled (bool)
  OptionsJson (jsonb?)         -- for choice-based steps
  ValidationMetadataJson (jsonb?)
  AnswerMappingJson (jsonb?)   -- maps answer keys to StudentProfile method
  AssessmentMetadataJson (jsonb?)  -- correctAnswerKey, cefrScoreWeight

StudentOnboardingProgress
  Id (Guid, PK)
  UserId (Guid, unique index)  -- one record per student
  FlowDefinitionId (Guid, FK)
  CurrentStepKey (string?)
  CompletedStepKeys (jsonb)    -- string[]
  PercentageComplete (int, 0-100)
  StartedAt (DateTimeOffset)
  CompletedAt (DateTimeOffset?)
  IsComplete (bool)
  PreliminaryCefrLevel (string?) -- system-estimated, null until complete
  Version (uint)               -- EF concurrency token

StudentOnboardingResponse
  Id (Guid, PK)
  ProgressId (Guid, FK → StudentOnboardingProgress)
  StepKey (string, max 100)
  AnswerJson (jsonb)
  SubmittedAt (DateTimeOffset)
```

### New enums

```
OnboardingStepTypeV2:
  Welcome = 0
  PreferredName = 1
  SupportLanguage = 2
  LearningGoals = 3
  FocusAreas = 4
  DifficultyPreference = 5
  SingleChoice = 6
  MultipleChoice = 7
  FreeText = 8
  AssessmentQuestion = 9
  Summary = 10

OnboardingStepRequirementType:
  SystemRequired = 0
  AdminConfigured = 1
```

### Default flow seed (10 steps)

| Order | StepKey | Type | RequirementType | Enabled |
|---|---|---|---|---|
| 1 | welcome | Welcome | SystemRequired | true |
| 2 | preferred_name | PreferredName | SystemRequired | true |
| 3 | support_language | SupportLanguage | SystemRequired | true |
| 4 | learning_goals | LearningGoals | SystemRequired | true |
| 5 | focus_areas | FocusAreas | SystemRequired | true |
| 6 | difficulty_preference | DifficultyPreference | SystemRequired | true |
| 7 | assessment_intro | SingleChoice | SystemRequired | true |
| 8 | assessment_q1 | AssessmentQuestion | SystemRequired | true |
| 9 | assessment_q2 | AssessmentQuestion | SystemRequired | true |
| 10 | summary | Summary | SystemRequired | true |
| (11) | custom_why_learning | SingleChoice | AdminConfigured | false |

### Preliminary CEFR scoring

Assessment steps carry `cefrScoreWeight` in AssessmentMetadataJson.
Score = sum of weights for correct answers / max possible weight.

```
0–25%   → A1
26–45%  → A2
46–65%  → B1
66–80%  → B2
81–95%  → C1
96–100% → C2
```

Stored as `StudentOnboardingProgress.PreliminaryCefrLevel`.
Calls `StudentProfile.SetCefrLevel()` only if no CEFR is already set from real placement.

### API endpoints

```
GET  /api/onboarding
  → returns: flowId, currentStepKey, steps[], completedStepKeys[], percentageComplete, isComplete
  → lazy-creates StudentOnboardingProgress on first call
  → if existing completed v1 student: initialises v2 progress as isComplete=true

POST /api/onboarding/steps/{stepKey}
  → body: { answer: object }
  → validates: step exists, enabled, not completed (for system-required once-only steps)
  → saves StudentOnboardingResponse row
  → for system-required preference steps: calls StudentProfile.UpdateLearningPreferences()
  → recalculates percentage
  → advances CurrentStepKey to next enabled step

POST /api/onboarding/complete
  → validates: all SystemRequired+IsEnabled steps are in CompletedStepKeys
  → computes preliminary CEFR from assessment responses
  → marks IsComplete = true, sets CompletedAt
  → calls StudentProfile.SetCefrLevel() if CefrLevel is null
  → advances LifecycleStage to PlacementRequired

GET  /api/admin/onboarding/flow   [Admin role]
  → returns active flow definition with all step definitions (read-only)
```

### Backward compatibility

- Old `PATCH /api/onboarding` and `GET /api/onboarding/status` endpoints untouched.
- Old Angular step1-step5 components untouched; still serve existing v1 students.
- New Angular v2 shell is a parallel route (`/onboarding/v2` or replaces `/onboarding` with a version selector based on lifecycle stage).
- Students with `OnboardingStatus == Complete` get v2 progress auto-initialised as `IsComplete = true` on first GET /api/onboarding call.

### Layer assignments

| Layer | New files |
|---|---|
| Domain | OnboardingFlowDefinition.cs, OnboardingStepDefinition.cs, StudentOnboardingProgress.cs, StudentOnboardingResponse.cs, OnboardingStepTypeV2.cs (enum), OnboardingStepRequirementType.cs (enum), PreliminaryCefrCalculator.cs |
| Application | IOnboardingV2Query.cs, IOnboardingV2StepHandler.cs, IOnboardingV2CompleteHandler.cs, IAdminOnboardingFlowQuery.cs, OnboardingV2Commands.cs |
| Infrastructure | OnboardingV2QueryHandler.cs, OnboardingV2StepHandler.cs, OnboardingV2CompleteHandler.cs, AdminOnboardingFlowQueryHandler.cs |
| Persistence | OnboardingFlowDefinitionConfiguration.cs, OnboardingStepDefinitionConfiguration.cs, StudentOnboardingProgressConfiguration.cs, StudentOnboardingResponseConfiguration.cs, T47_OnboardingV2.cs (migration), OnboardingFlowSeeder.cs |
| API | OnboardingController.cs (extend with v2 endpoints), AdminOnboardingController.cs |

---

## Validation rules

- System-required steps cannot be skipped during complete
- Disabled steps cannot be submitted
- Invalid option keys rejected (checked against OptionsJson)
- Free text bounded by ValidationMetadataJson.maxLength (default 500)
- Multi-select bounded by ValidationMetadataJson.maxSelections (default 10)
- Progress percentage clamped 0–100
- Completed onboarding: POST /api/onboarding/steps returns 409 (already complete)
- Duplicate step keys in seed: seeder throws at startup
- AssessmentQuestion answers validated against OptionsJson keys

---

## Test targets

### Backend unit (+40 target)
- OnboardingFlowDefinition: duplicate step key detection
- OnboardingStepDefinition: SystemRequired flag enforced
- StudentOnboardingProgress: percentage calculation (0, partial, full)
- StudentOnboardingProgress: completion rules (all required steps present)
- PreliminaryCefrCalculator: all 6 CEFR level bands
- Disabled step submission rejected
- Invalid option key rejected
- Free text over limit rejected
- Multi-select over limit rejected
- Existing v1-complete student: v2 progress initialised as complete

### Backend integration (+20 target)
- GET /api/onboarding: lazy-creates progress for new student
- GET /api/onboarding: returns isComplete=true for existing v1-complete student
- POST /api/onboarding/steps: saves response, advances step
- POST /api/onboarding/steps: updates StudentProfile preferences
- POST /api/onboarding/steps: rejects disabled step
- POST /api/onboarding/complete: validates required steps present
- POST /api/onboarding/complete: computes and stores preliminary CEFR
- POST /api/onboarding/complete: advances lifecycle stage
- GET /api/admin/onboarding/flow: returns active flow (Admin only)
- POST /api/onboarding/complete: idempotent (second call returns 409)

### Angular unit (+20 target)
- V2 shell renders first step from backend definition
- Progress bar displays correct percentage
- SingleChoice step submits selected key
- MultipleChoice step submits selected keys array
- FreeText step validates max length
- LearningGoals step renders and submits
- FocusAreas step renders and submits
- DifficultyPreference step renders and submits
- AssessmentQuestion step renders options, submits answer key
- Summary step renders, triggers complete
- Already-complete student bypasses onboarding

### Playwright E2E (+8 target)
- New student: full v2 happy path → reaches Today page
- Validation on one required step (empty submit rejected)
- Completed student: Today/Practice accessible, no onboarding redirect
- Profile page reflects preferences set during onboarding
- Admin-configured custom step renders when enabled in fixture
- Completion advances lifecycle to placement

---

## Risks and open questions

1. **Angular route strategy for v2 vs v1:** New students should see v2. Students mid-v1-flow should finish v1. Route guard must check: if LifecycleStage == OnboardingRequired and no v1 progress → v2. If v1 InProgress → v1. If v1 Complete → bypass. This logic needs a clear guard decision in implementation.

2. **Assessment accuracy disclaimer:** The preliminary CEFR from 2 questions is not a real placement. It must be labelled "estimated" in the UI and overwritten when real PlacementAssessment completes.

3. **Admin UI for onboarding builder:** Explicitly deferred. Backend model supports admin-configured steps. Admin management UI/API (enabling, reordering, editing custom steps) is future work.

4. **v1 state machine retirement:** The old OnboardingStep/OnboardingStatus enums and StudentProfile state machine are NOT removed in this phase. They remain for existing students and backward compat. Retirement is a separate future phase.

---

## What is NOT in scope

- Full CEFR placement algorithm
- Curriculum boundary engine
- CEFR-aware content routing
- Admin drag-and-drop onboarding builder
- Admin PUT/PATCH /api/admin/onboarding/flow (write endpoints)
- v1 state machine removal
- Readiness pools, background lesson generation
- Notifications, quotas, payments

---

## Final verdict

Plan is sound. Parallel approach (D1) is the correct call — it protects existing students with zero migration risk. The 10-step seed gives a practical working default. Assessment scoring is appropriately minimal. Admin GET endpoint is small and wires the boundary. Angular dynamic rendering approach prevents hardcoded step-N routes from proliferating. CI should remain green with additive changes.

Next recommended action: begin implementation with Domain entities + migration T47, then Application/Infrastructure, then API, then Angular.

---

## GSTACK REVIEW REPORT

| Run | Status | Findings |
|---|---|---|
| Architecture review | PASS | Parallel v1/v2 design sound; layer boundaries respected |
| Code quality review | PASS | DRY risk identified (answer mapping); typed enum recommended over raw string keys in AnswerMappingJson |
| Test review | PASS | +40 unit / +20 integration / +20 Angular / +8 Playwright targets set |
| Performance review | PASS | Unique index on UserId; flow definition cacheable; no AI calls in this phase |
| Outside voice | PASS | AnswerMappingJson: use typed enum not raw strings; AssessmentMetadataJson is temporary home, document boundary |

VERDICT: APPROVED — proceed with implementation.

NO UNRESOLVED DECISIONS
