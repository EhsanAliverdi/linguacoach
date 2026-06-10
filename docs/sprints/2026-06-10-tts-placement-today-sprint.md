---
status: complete
startDate: 2026-06-10
completedDate: 2026-06-11
owner: product
sprintName: Real TTS / Placement Onboarding Gap / Today Session Card Sprint
---

# Sprint: Real TTS / Placement Onboarding Gap / Today Session Card Sprint

**Date:** 2026-06-10
**Status:** Complete (2026-06-11)

---

## Motivation

Three concrete blockers stand between the current build and a credible pilot demo:

1. **Listening audio is silent.** `ListeningAudioService` and `PlacementAudioService` use `FakeTextToSpeechService`, which generates a short silent WAV. Students hear nothing.
2. **Placement does not collect professional context.** `ProfessionalExperienceLevel` and `RoleFamiliarity` are admin-only fields. The placement assessment has no context on seniority or domain complexity.
3. **Today page does not show the session engine entry point.** The `DashboardComponent` does not call `SessionService.getToday()`, so the existing fully-built session engine is invisible to students.

---

## Track 1 — Real TTS via Admin AI Config

### Goal

Allow admins to switch listening audio from silent fake to OpenAI TTS by changing `AiProviderConfig` rows for `tts.listening` and `tts.placement`. No env-var switch. No global flag.

### Changes

#### Backend

- **`AiProviderConfig`** — add `VoiceName` property (`string?`). New `UpdateVoice(string? voiceName)` method. `VoiceName` is free-form (not whitelist-validated — TTS voices are not LLM models). Constructor overload accepts `voiceName`.
- **`T35_AiProviderConfigVoice`** — EF migration adding nullable `VoiceName` column to `ai_provider_configs`.
- **`AiProviderConfigItem`** DTO — add `VoiceName` field.
- **`UpdateAiProviderConfigCommand`** — add `VoiceName` parameter.
- **`UpdateConfigAsync`** handler — apply `VoiceName` when present.
- **`OpenAiTextToSpeechService`** — calls `POST https://api.openai.com/v1/audio/speech`. Reads API key from `IConfiguration["OpenAi:ApiKey"]`. Returns `audio/mpeg`. Provider=`openai`, voice from `TextToSpeechOptions.Voice` or falls back to `onyx`. On any failure returns `TtsResult` with `Success=false` (never throws).
- **`TtsProviderResolver`** — reads `AiProviderConfig` for a given feature key. Returns `FakeTextToSpeechService` when provider=`fake` (or config not found). Returns `OpenAiTextToSpeechService` when provider=`openai`.
- **`ListeningAudioService`** — inject `TtsProviderResolver`, resolve with feature key `tts.listening` per call. Storage key uses extension from `result.AudioContentType` (`.wav` or `.mp3`) — not hardcoded.
- **`PlacementAudioService`** — inject `TtsProviderResolver`, resolve with feature key `tts.placement` per call. Same content-type-driven extension logic.
- **`DefaultAiSeeder`** — seed `tts.listening` and `tts.placement` with provider=`fake`, model=`fake`, voice=`fake`. Idempotent. Admin switches to `openai`/`tts-1`/`onyx` via UI.
- **`DependencyInjection.cs`** — register `OpenAiTextToSpeechService` and `TtsProviderResolver`. Keep `ITextToSpeechService → FakeTextToSpeechService` for all callers that don't use the resolver.

#### Admin UI

- `AiProviderConfigItem` model in Angular — add `voiceName?: string`.
- `AdminAiConfigComponent` — add **Voice** input field alongside provider/model selects for TTS feature rows. Free-text input. Saves on blur/change via existing `saveFeature` pathway.
- `AdminApiService.updateAiConfig` — pass `voiceName` in patch body.

### Constraints

- `dotnet test` must NOT require `OPENAI_API_KEY`. `FakeTextToSpeechService` is still the default.
- OpenAI TTS only activates when `AiProviderConfig.ProviderName == "openai"`.
- All failures from OpenAI return safe fallback — never 500.
- Existing `.wav` files stored under previous fake TTS remain readable; new files get the correct extension.

---

## Track 2 — Placement Onboarding Gap

### Goal

Collect `ProfessionalExperienceLevel` + `RoleFamiliarity` during onboarding so that `WorkplaceSeniority` is set before placement. New students complete this as step 5 (after skill). Existing completed students are not broken.

### Design decision

The domain state machine marks `OnboardingStatus.Complete` at step 4 (skill) and refuses further step transitions. Adding a 5th state-machine step would break all existing completed students. Instead, experience is collected via a **dedicated endpoint** `PATCH /api/onboarding/experience` that bypasses the state machine and directly calls `StudentProfile.UpdateAdminProfile` (the profile enrichment path that already sets experience fields without ordering constraints).

The Angular flow treats this as step 5 in the wizard: step 4 (skill) navigates to `/onboarding/step-5` on success; step 5 calls the experience endpoint then navigates to `/placement`.

### Changes

#### Backend

- **`SetExperienceRequest`** — new command record in `OnboardingCommands.cs`.
- **`IOnboardingExperienceHandler`** — new interface. Single method: `HandleAsync(SetExperienceRequest, ct)`.
- **`OnboardingController`** — new `PATCH /api/onboarding/experience` endpoint. Accepts `{ professionalExperienceLevel, roleFamiliarity }`. Calls `IOnboardingExperienceHandler`.
- **`OnboardingHandler`** — implement `IOnboardingExperienceHandler`. Loads profile, calls `profile.UpdateAdminProfile(...)` with only the experience fields updated, saves. Returns `{ success: true }`.
- **`DependencyInjection.cs`** — register `IOnboardingExperienceHandler → OnboardingHandler`.

#### Frontend

- **`step5-experience.component.ts`** — new standalone Angular component. Shows two dropdowns: Experience level and Role familiarity. Submit calls `PATCH /api/onboarding/experience` then navigates to `/placement`.
- **`onboarding.service.ts`** — add `submitExperience(level, familiarity)` method.
- **`app.routes.ts`** — add `step-5` route in the onboarding block.
- **`step4-skill.component.ts`** — change `router.navigate(['/placement'])` to `router.navigate(['/onboarding/step-5'])`.

### Constraints

- Existing students who already have `OnboardingStatus.Complete` are not affected.
- No redirect loop: step-5 navigates directly to `/placement` on success.
- Experience step is optional — if user skips or API fails, placement still proceeds.
- `WorkplaceSeniority` is computed from experience + familiarity by `WorkplaceSeniorityCalculator`.

---

## Track 3 — Today Page Session Card (already implemented)

As of the previous sprint (LearningSession Phase 4, 2026-06-10), `DashboardComponent` already calls `SessionService.getToday()` when `lifecycleStage === 'CourseReady'` / `'InLesson'` / `'ActiveLearning'`, displays the session card, and navigates to `/lesson/:sessionId`. No changes required.

---

## Acceptance criteria

### Track 1

- [ ] Admin can set `tts.listening` to `openai` / `tts-1` / `onyx` in AI Config UI.
- [ ] Listening activities generate `.mp3` audio when provider=openai; `.wav` when fake.
- [ ] Placement listening audio uses the same resolver.
- [ ] `dotnet test` passes with no `OPENAI_API_KEY` set.
- [ ] Default seed is `fake`/`fake`/`fake` — no real API calls in CI.

### Track 2

- [ ] New student completes step 5 before reaching placement.
- [ ] `ProfessionalExperienceLevel` and `RoleFamiliarity` are persisted on the profile.
- [ ] `WorkplaceSeniority` is computed and stored.
- [ ] Existing completed students are not broken.
- [ ] `PATCH /api/onboarding/experience` returns 200 for valid input.

### Track 3

- [x] Already complete from previous sprint (LearningSession Phase 4).

---

## Files changed (expected)

### Backend
- `src/LinguaCoach.Domain/Entities/AiProviderConfig.cs`
- `src/LinguaCoach.Application/Admin/AdminQueries.cs`
- `src/LinguaCoach.Application/Onboarding/OnboardingCommands.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminHandler.cs`
- `src/LinguaCoach.Infrastructure/Onboarding/OnboardingHandler.cs`
- `src/LinguaCoach.Infrastructure/Speaking/OpenAiTextToSpeechService.cs` (new)
- `src/LinguaCoach.Infrastructure/Speaking/TtsProviderResolver.cs` (new)
- `src/LinguaCoach.Infrastructure/Activity/ListeningAudioService.cs`
- `src/LinguaCoach.Infrastructure/Placement/PlacementAudioService.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Persistence/Migrations/T35_AiProviderConfigVoice.cs` (new)
- `src/LinguaCoach.Api/Controllers/OnboardingController.cs`

### Frontend
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts`
- `src/LinguaCoach.Web/src/app/core/services/onboarding.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`
- `src/LinguaCoach.Web/src/app/features/onboarding/step4-skill/step4-skill.component.ts`
- `src/LinguaCoach.Web/src/app/features/onboarding/step5-experience/step5-experience.component.ts` (new)
- `src/LinguaCoach.Web/src/app/app.routes.ts`

---

## Test baseline going in

```
dotnet test:  873 passed (451 unit + 422 integration)
npm run build: passed
Playwright:   167 passed
```

---

## Risks

- OpenAI TTS voices: the API accepts any string voice name; validation is at runtime not compile time.
- Audio file cleanup: storing mp3 alongside wav files in the same directory is safe because filenames include activity/assessment IDs.
- Onboarding experience step: if user refreshes mid-flow on step-5, they can re-submit — `UpdateAdminProfile` is idempotent.

---

## Status

- [x] Track 1 — Complete (873 tests pass; admin UI updated)
- [x] Track 2 — Complete (PATCH /api/onboarding/experience; step-5 Angular; 8 Playwright tests pass)
- [x] Track 3 — Complete (implemented in Today's Lesson / Learning Session sprint; audited and verified 2026-06-11)

## Track 3 audit — Today Page Session Card (verified 2026-06-11)

Track 3 was implemented in the `2026-06-10-today-lesson-learning-session-sprint`. Audit confirms all requirements met:

### Files implementing Track 3
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.ts` — calls `SessionService.getToday()` when lifecycle is `CourseReady`, `InLesson`, or `ActiveLearning`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html` — renders session card above Practice Gym secondary links; shows title, topic, durationMinutes, exercises.length, focusSkill; CTA adapts by status
- `src/LinguaCoach.Web/src/app/core/services/session.service.ts` — `getToday()` calls `GET /api/sessions/today`

### Behaviour confirmed
- `CourseReady` / `InLesson` / `ActiveLearning` → `getToday()` called → session card rendered
- Card shows: title, topic, duration (min), focus skill, exercise count
- CTA states: `notStarted` → "Start today's lesson", `inProgress` → "Resume lesson", `completed` → "Review today's lesson"
- CTA `[routerLink]` → `/lesson/:sessionId`
- Session start (`POST /api/sessions/{id}/start`) is called from the lesson page, not the dashboard CTA
- Loading state: pulse skeleton shown while `sessionLoading()` is true
- Error/no-session state: graceful fallback ("Your lesson is ready" heading, CTA still shown)
- Stat grid shows `--` for streak (not hardcoded); real `activityStats` values or `--` for practice/score

### Playwright tests (in `e2e/today-lesson.spec.ts`)
14 tests — all pass:
1. Dashboard shows Today's Lesson card when lifecycle is ActiveLearning
2. Not started badge shown
3. Correct CTA text for notStarted
4. In progress badge and Resume button when session is inProgress
5. Completed badge and Review button when session is completed
6. Today's Lesson is primary; Practice Gym is secondary link
7. Clicking CTA navigates to /lesson/:id
8. Lesson page loads with title and exercises
9. Exercises in correct order — first is vocabulary warmup
10. Start lesson button shown when notStarted
11. Start lesson button calls start endpoint and updates status
12. Complete exercise button shown in active exercise panel
13. Completing all exercises shows Complete lesson button
14. Completing lesson shows completion summary

### Final test results (2026-06-11)
```
dotnet test:   873 passed (451 unit + 422 integration) — no backend changes in Track 3
npm run build: passed
Playwright:    175 passed (167 pre-sprint baseline + 8 new Track 2 onboarding step-5 tests)
               (14 Track 3 today-lesson tests were already counted in the 167 baseline)
```
