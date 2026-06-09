# Engineering Review — Onboarding & Post-Placement UX Alignment

**Date:** 2026-06-09
**Related sprint:** `docs/sprints/onboarding-post-placement-ux-alignment-sprint.md`
**Reviewer:** Claude Code + gstack plan-eng-review

---

## Files Reviewed

- `src/LinguaCoach.Domain/Entities/StudentProfile.cs`
- `src/LinguaCoach.Application/Onboarding/OnboardingCommands.cs`
- `src/LinguaCoach.Infrastructure/Onboarding/OnboardingHandler.cs`
- `src/LinguaCoach.Api/Controllers/OnboardingController.cs`
- `src/LinguaCoach.Web/src/app/features/onboarding/step3-career/step3-career.component.ts`
- `src/LinguaCoach.Web/src/app/features/onboarding/step4-skill/step4-skill.component.ts`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html`
- `src/LinguaCoach.Web/src/app/core/guards/placement.guard.ts`
- `src/LinguaCoach.Web/src/app/app.routes.ts`
- `src/LinguaCoach.Persistence/Migrations/20260608222354_T29_PlacementAssessmentAndLifecycle.cs`
- `src/LinguaCoach.Persistence/Migrations/20260609000124_T30_AdminProfileFields.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`

---

## Decisions Made (AskUserQuestion)

### D1 — Step 3 backend: how to store free-text career description
**Answer:** `SetCareerContextText()` — skip `CareerProfileId`, save to existing `CareerContext` field only.

`StudentProfile.CareerContext` (varchar 500, added in T30) stores the value.
`CareerProfileId` remains null for students who enter free-text. Downstream AI context code already uses `CareerContext` string where it exists.

### D2 — LearningGoalDescription: reuse LearningGoal or add new field
**Answer:** Add `LearningGoalDescription` as a **distinct new field** alongside the existing `LearningGoal`.

Rationale: `LearningGoal` is admin-set; `LearningGoalDescription` is student-set during onboarding step 4. Keeping them separate avoids admin-set data being overwritten. Requires T31 migration.
Also add `DifficultSituationsText` (varchar 1000) as a new field per spec.

### D3 — Dashboard lifecycle: embed lifecycleStage in DashboardResponse or separate call
**Answer:** Add `lifecycleStage` to the existing `GET /api/dashboard` response.

One round-trip. Frontend uses it to branch between PlacementRequired CTA, CourseReady summary, and normal dashboard view.

---

## Findings by Priority

### P1 — Guard redirect loop for pre-onboarding stages

**[P1] (confidence: 9/10) `placement.guard.ts:41` — `placementAccessGuard` redirects pre-onboarding to `/dashboard` instead of `/onboarding/resume`**

```typescript
// placement.guard.ts:41
const blockedStages = ['Created', 'PasswordChangeRequired', 'OnboardingRequired', 'OnboardingInProgress'];
if (blockedStages.includes(status.lifecycleStage)) {
  return router.createUrlTree(['/dashboard']);  // ← wrong target
}
```

After this sprint, step 4 `finish()` navigates to `/placement`. If a student somehow hits `/placement` mid-onboarding (e.g. typed URL), `placementAccessGuard` sends them to `/dashboard`, which fires `placementRequiredRedirectGuard`, which for `OnboardingInProgress` (not `PlacementRequired`) allows them through — showing the wrong screen. Fix: redirect to `/onboarding/resume`.

### P2 — Backend API breaks on free-text career submission

**[P2] (confidence: 9/10) `OnboardingController.cs:38` — "career" step requires `CareerProfileId` Guid**

```csharp
"career" when dto.CareerProfileId.HasValue =>
    new SetCareerRequest(userId, dto.CareerProfileId.Value),
```

After step 3 becomes free-text, the frontend posts `{ step: "career", careerContext: "Nurse" }`. The controller produces 400 "Invalid step" because `CareerProfileId` is null. The DTO and dispatch need a `CareerContext` text branch.

### P2 — Step 4 goal fields lost in API

**[P2] (confidence: 9/10) `OnboardingController.cs:41` — no `LearningGoalDescription` or `DifficultSituationsText` in DTO**

```csharp
"skill" when dto.SkillFocus.HasValue =>
    new SetSkillRequest(userId, dto.SkillFocus.Value),
```

`OnboardingStepDto` has no fields for the new step 4 textarea values. They must be added to the DTO and threaded through the command and handler.

### P2 — Step 4 navigates to wrong route

**[P2] (confidence: 9/10) `step4-skill.component.ts:33` — navigates to `/dashboard` instead of `/placement`**

```typescript
next: () => this.router.navigate(['/dashboard']),
```

Must change to `['/placement']`. Currently, the guard double-hops the student there anyway, but this should be direct.

### P2 — Dashboard has no lifecycleStage in response

**[P2] (confidence: 9/10) `DashboardController` / `DashboardQueryHandler` — `lifecycleStage` not included in dashboard API response**

`DashboardResponse` DTO does not include the student's lifecycle stage. The frontend dashboard redesign requires it to branch between PlacementRequired CTA and normal view. Must be added to the handler and response DTO.

### P3 — Domain method: `SetCareerProfile` step machine must be mirrored in `SetCareerContextText`

**[P3] (confidence: 9/10) `StudentProfile.cs:96` — new method must call `EnsureStepIsNext` and `AdvanceTo`**

```csharp
public void SetCareerProfile(CareerProfile profile)
{
    EnsureStepIsNext(OnboardingStep.Career);
    // ...
    AdvanceTo(OnboardingStep.Career);
}
```

`SetCareerContextText(string text)` must replicate the step machine calls. Missing either causes a silent invariant violation (step state corrupted, future steps blocked).

---

## Implementation Tasks

See `docs/sprints/onboarding-post-placement-ux-alignment-sprint.md` for full task list. Summary:

| Task | Area | Description |
|------|------|-------------|
| T1 | Domain | Add `LearningGoalDescription`, `DifficultSituationsText` fields; add `SetCareerContextText()` and extended `SetSkillFocus` method |
| T2 | Migration | T31: add two new varchar columns with IF NOT EXISTS guards |
| T3 | Application | New `SetCareerContextTextRequest` command; extend `SetSkillRequest` with goal fields; update `OnboardingHandler` |
| T4 | API | Extend `OnboardingStepDto` + controller dispatch for text career and skill+goal steps |
| T5 | API | Add `lifecycleStage` to `DashboardResponse` and `DashboardQueryHandler` |
| T6 | Frontend | Step 3: replace career list with free-text input + optional suggestion chips |
| T7 | Frontend | Step 4: add Listening skill, add goal textarea, navigate to `/placement` on finish |
| T8 | Frontend | Guard fix: `placementAccessGuard` redirect → `/onboarding/resume` |
| T9 | Frontend | Dashboard redesign: lifecycle-aware states (PlacementRequired CTA, CourseReady summary, Practice Gym section) |
| T10 | Tests | Backend integration tests for free-text career, Listening skill, Farsi goal text, lifecycle after onboarding, lifecycleStage in dashboard response |
| T11 | Tests | Playwright E2E: full onboarding flow, dashboard states, Practice Gym routing |
| T12 | Docs | Create sprint doc; update backlog and current-sprint |
| T13 | Ship | `dotnet test`, `npm run build`, `npx playwright test`, commit, push |

**Execution order:** T1 → T2 → T3 → T4 → T5 (sequential, each depends on previous) → T6, T7, T8, T9 (parallel after T5) → T10, T11 → T12 → T13

---

## Constraints (from sprint spec — DO NOT violate)

- Do NOT implement LearningSession, Today page, SessionExercise, TeamsChatSimulation, MinIO, Call Mode, Pronunciation
- Do NOT remove existing working practice activities
- Do NOT make field of work hardcoded-only dropdown
- Do NOT make English-only input required for learning goal
- Do NOT show permission-denied for normal lifecycle redirects
- Do NOT make Practice Gym the main product experience

---

## Risks and Unresolved Questions

- `CareerProfileId` will be null for all new students after this sprint. Downstream code that reads `profile.CareerProfile?.Title` (e.g. AI prompt builders) must fall back to `profile.CareerContext`. Verify all callers handle null `CareerProfile` gracefully before shipping.
- `DifficultSituationsText` is added to the domain and DB but the sprint does not yet surface it in any AI prompt — that is intentional (spec scope). Leave as stored-but-unused for now.
- Dashboard `PlacementCompleted` vs `CourseReady`: the spec mentions both. Verify which lifecycle stage is set after placement evaluates before wiring the dashboard CTA state.

---

## Final Verdict

**Ready to implement.** All architectural decisions made. No blocking unknowns. Proceed with T1.
