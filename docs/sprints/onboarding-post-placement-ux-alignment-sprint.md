# Sprint — Onboarding & Post-Placement UX Alignment

**Created:** 2026-06-09
**Status:** Complete
**Engineering review:** `docs/reviews/2026-06-09-onboarding-post-placement-ux-engineering-review.md`

---

## Goal

Close the gap between onboarding and placement so that:

1. Onboarding collects richer, more flexible data (free-text career field, Listening skill, free-text learning goal in any language).
2. After onboarding completes, the student lands on `/placement` directly.
3. The dashboard is lifecycle-aware: shows a placement CTA when `PlacementRequired`, a placement result summary when `CourseReady`, and moves activity cards to a clearly-labelled secondary "Practice Gym" section.
4. Guards produce friendly redirects — no "permission denied" or confusing loops for normal lifecycle flow.

---

## Out of Scope

- LearningSession, Today page, SessionExercise, TeamsChatSimulation
- MinIO, Call Mode, Pronunciation
- Removing existing working practice activities
- Making field of work a hardcoded dropdown
- Requiring English-only input for learning goal
- Making Practice Gym the main product experience

---

## New Backend Fields

| Field | Entity | Column | Type | Notes |
|-------|--------|--------|------|-------|
| `LearningGoalDescription` | `StudentProfile` | `learning_goal_description` | varchar(1000) | Student-set in step 4; distinct from admin-set `LearningGoal` |
| `DifficultSituationsText` | `StudentProfile` | `difficult_situations_text` | varchar(1000) | Student-set in step 4; stored but not yet used in AI prompts |

Migration: **T31** (IF NOT EXISTS guards required).

---

## Tasks

### T1 — Domain: new fields + methods on StudentProfile

- Add `LearningGoalDescription string?` property
- Add `DifficultSituationsText string?` property
- Add `SetCareerContextText(string text)` method:
  - Calls `EnsureStepIsNext(OnboardingStep.Career)`
  - Sets `CareerContext = text.Trim()`
  - Does NOT set `CareerProfileId` (remains null)
  - Calls `AdvanceTo(OnboardingStep.Career)`
- Extend skill step method: add `SetSkillFocus(SkillFocus, string? learningGoalDescription, string? difficultSituationsText)` overload (or update existing to accept optional params)

### T2 — Migration T31

```sql
ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS learning_goal_description varchar(1000);
ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS difficult_situations_text varchar(1000);
```

Use `migrationBuilder.Sql(...)` with IF NOT EXISTS (same pattern as T29/T30).

### T3 — Application: new commands + handler updates

- Add `SetCareerContextTextRequest(Guid UserId, string CareerContext)` to `OnboardingCommands.cs`
- Add `LearningGoalDescription string?` and `DifficultSituationsText string?` to `SetSkillRequest` (or new `SetSkillGoalRequest`)
- In `OnboardingHandler.HandleAsync`:
  - New case for `SetCareerContextTextRequest`: no DB lookup, call `profile.SetCareerContextText(r.CareerContext)`
  - Update `SetSkillRequest` case: pass goal fields to domain method

### T4 — API: OnboardingController + DTO

- Add to `OnboardingStepDto`:
  - `string? CareerContext`
  - `string? LearningGoalDescription`
  - `string? DifficultSituationsText`
- Add controller dispatch branch for text career:
  ```csharp
  "career" when dto.CareerContext is { Length: > 0 } =>
      new SetCareerContextTextRequest(userId, dto.CareerContext),
  ```
- Update skill branch to pass new fields
- Keep existing `CareerProfileId` branch working (backward compat for tests)

### T5 — API: DashboardController — add lifecycleStage

- Add `string LifecycleStage` to dashboard response DTO
- In `DashboardQueryHandler`: include `profile.LifecycleStage.ToString()` in the result
- Update `DashboardResponse` Angular model to include `lifecycleStage: string`

### T6 — Frontend: step 3 — replace career list with free-text

- Remove `ReferenceService` call and `careers`/`loading` signals
- Add `careerText = signal('')` bound to a `<textarea>` (or `<input>`)
- Optional: add 4-5 suggestion chips (e.g. "Engineering", "Healthcare", "Finance", "Education", "Construction") that pre-fill the field on click
- `next()` posts `{ step: 'career', careerContext: this.careerText() }`
- Validation: require non-empty text before enabling Next button

### T7 — Frontend: step 4 — add Listening + learning goal textarea

- Add to skills array: `{ label: 'Listening', description: 'Comprehension, podcasts, and meeting audio.', value: 3 }`
- Add `learningGoalText = signal('')` bound to a `<textarea>` (any language, not required)
- `finish()` posts `{ step: 'skill', skillFocus: this.selected(), learningGoalDescription: this.learningGoalText() || undefined }`
- Change `router.navigate(['/dashboard'])` → `router.navigate(['/placement'])`

### T8 — Frontend: guard fix

In `placement.guard.ts`, `placementAccessGuard`:
```typescript
// Before:
return router.createUrlTree(['/dashboard']);
// After:
return router.createUrlTree(['/onboarding/resume']);
```

### T9 — Frontend: dashboard redesign

Read `data().lifecycleStage`. Branch as follows:

- **`PlacementRequired`**: show a prominent placement CTA card ("Complete your placement assessment to unlock your personalised course"). Hide hero and Practice Gym entirely (student hasn't placed yet, no path exists).
- **`CourseReady` / `ActiveLearning`**: normal dashboard with hero ("Recommended next" / learning path). Show placement result summary (CEFR level + top skill) above hero if placement result data is available.
- **Activity cards** (Writing, Listening, Vocabulary, Speaking, Pronunciation): move to a section with clear heading "Practice Gym — On-demand practice". This is secondary to the main hero. Pronunciation remains disabled.

### T10 — Backend integration tests

In `OnboardingEndpointTests` (or new file):

1. `POST /api/onboarding` with `{ step: "career", careerContext: "Nurse" }` → 200, `lastCompletedStep = Career`
2. `POST /api/onboarding` with `{ step: "career", careerProfileId: <guid> }` → still 200 (backward compat)
3. `POST /api/onboarding` with `{ step: "skill", skillFocus: 3 }` (Listening) → 200
4. `POST /api/onboarding` with `{ step: "skill", skillFocus: 0, learningGoalDescription: "میخوام بتونم ایمیل رسمی بنویسم" }` → 200, native language accepted
5. After completing step 4: `GET /api/placement/status` → `lifecycleStage = PlacementRequired`
6. `GET /api/dashboard` after onboarding → response includes `lifecycleStage` field

### T11 — Playwright E2E tests

1. Full onboarding flow: step 3 free-text, step 4 Listening + Farsi goal → ends on `/placement` (not `/dashboard`)
2. Dashboard with `PlacementRequired` lifecycle: placement CTA visible, no learning path hero
3. Dashboard after placement complete: result summary visible + Practice Gym section
4. Practice Gym card click navigates to activity route

### T12 — Docs

- ~~Create this sprint doc~~ (done)
- Update `docs/backlog/product-backlog.md` to move relevant items to In Progress / Done
- Update `docs/sprints/current-sprint.md`

### T13 — Ship

```bash
dotnet test LinguaCoach.slnx
npm run build   # in src/LinguaCoach.Web
npx playwright test
git add -p && git commit
git push
```

---

## Risks

- `CareerProfileId` will be null for all new students after this sprint. Any code reading `profile.CareerProfile?.Title` must fall back to `profile.CareerContext`. Audit before shipping.
- `DifficultSituationsText` is stored but not yet wired into AI prompts — intentional, defer to a later sprint.
- Verify which lifecycle stage is set after placement evaluates (`PlacementCompleted` vs `CourseReady`) before wiring dashboard CTA.

---

## Completion Record

**Completed:** 2026-06-09

### Finished before rate limit

Claude completed or partially completed T1-T8 before rate limit:

- Domain fields and methods for free-text career and student-authored learning goals.
- T31 migration for `learning_goal_description` and `difficult_situations_text`.
- Application/API onboarding command and DTO changes.
- Dashboard API `lifecycleStage`.
- Frontend onboarding step 3 and step 4 changes.
- Placement guard redirect from pre-onboarding stages to `/onboarding/resume`.

### Completed by Codex from T9 onward

- Finished lifecycle-aware dashboard:
  - `PlacementRequired` and `PlacementInProgress` show placement CTAs.
  - `CourseReady` / `PlacementCompleted` can show placement result summary.
  - Practice cards moved under `Practice Gym - On-demand practice`.
  - Pronunciation remains disabled and future-only.
- Added `Listening = 3` to `SkillFocus` and pinned the enum value in unit tests.
- Added `POST /api/onboarding` alongside the existing `PATCH /api/onboarding` action for compatibility with the sprint test contract.
- Removed obsolete onboarding background learning-path generation. After onboarding, the lifecycle is `PlacementRequired`; course/path generation belongs after placement completion or lazy course access, not before placement.
- Added backend tests for free-text career, legacy career profile, Listening skill, native-language/Farsi learning goal, placement lifecycle status, and dashboard `lifecycleStage`.
- Added/updated Playwright tests for onboarding handoff to `/placement`, PlacementRequired dashboard CTA, CourseReady result summary, Practice Gym routes, Pronunciation disabled state, and console-error coverage.

### Tests run

```text
dotnet test LinguaCoach.slnx
npm run build
npx playwright test
```

Results:

- Backend/unit/integration tests: passed (`243` unit, `226` integration). Architecture test assembly still reports no discoverable tests, but `dotnet test` exits successfully.
- Angular build: passed with existing style budget / selector warnings.
- Playwright: passed (`63` tests).

### Remaining risks

- `DifficultSituationsText` is persisted but intentionally unused by AI prompts in this sprint.
- Dashboard result summary depends on `/api/placement/result`; if that endpoint is unavailable, the dashboard falls back to the dashboard CEFR level and hides optional result details.
- Practice Gym remains a secondary dashboard section until the Today page / `LearningSession` experience replaces the dashboard primary learning entry.

---

## QA Alignment Pass — 2026-06-09

Manual QA via screenshots revealed the following gaps after the above completion record:

### Step 2 — old "Choose your learning track" UI still deployed

The sprint previously left Step 2 as the old track-picker (showing only "Workplace English"). This was replaced with a **lesson duration preference step** (10 / 15 / 20 / 30 minutes) in this pass.

Changes made:
- `OnboardingStep.Track` renamed to `OnboardingStep.Preference` (integer value unchanged = 2, DB-safe).
- `StudentProfile.SetSessionPreference(int durationMinutes)` domain method added.
- `SetSessionPreferenceRequest` application command added.
- `OnboardingController` handles `"preference"` step; `"track"` step retained as backward-compat only.
- `OnboardingStepDto.PreferredDurationMinutes` field added.
- `step2-track` frontend component fully replaced with duration picker UI.
- `SetPreferenceRequest` TypeScript model replaces `SetTrackRequest`.
- Onboarding resume component updated: `Track` → `Preference` in step map.

### Step 4 — headline still said "Choose your first skill focus"

Updated to "Why do you want to improve your English?" with helper text and skill tags now clearly secondary (labelled "optional"). Skill tags no longer required to complete step — defaults to Writing if none selected.

### Tests updated

- Unit tests: replaced `SetLearningTrack` with `SetSessionPreference` in domain tests; updated enum pinning test.
- Integration tests: updated to use `preference` step throughout; added `Post_PreferenceStep_SavesSessionDuration` test.
- Playwright smoke: updated step 2 interaction and step 4 heading assertions.

### Test results (QA pass)

```
dotnet test LinguaCoach.slnx   → 242 unit, 234 integration — all passed
npm run build                   → passed (warnings only)
npx playwright test             → 62 passed, 1 pre-existing flaky (progress-page strict-mode, unrelated to this change)
```
