# Investigation: Learn / Practice / Feedback structure (Practice Gym & Today)

**Date:** 2026-06-15
**Related feature:** Practice Gym, Today/Daily Lesson, Activity stepper (Learn/Practice/Feedback)
**Type:** Read-only investigation (no code changed)

## 1. Executive summary

The product UI presents a 3-step "Lesson / Practice / Feedback" stepper, but for
ListeningComprehension activities, Step 1 ("Lesson") renders the **full exercise**
(scenario, audio player, "Answer questions" CTA, transcript-lock notice) — identical
in substance to Step 2 ("Practice"). There is no true teaching-only Learn stage for
this activity type. "Module" terminology in Practice Gym and Today is a UI/routing
label layered on top of the existing `LearningActivity` / `SessionExercise` /
`LearningSession` entities — no new persisted module or stage concept was added.

## 2. Main root cause

Three compounding causes, in order of impact:

1. **AI prompt design** — `activity_generate_listening`
   (`src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs:160-202`) generates a single
   exercise-shaped JSON object (`scenario`, `audioScript`, `questions[]`,
   `responseTask`). There are no `lessonContent`/`teachingContent` fields and no
   concept of a separate teaching pass.
2. **Presenter layer** — `LegacyListeningPresenter.teachContent()`
   (`src/LinguaCoach.Web/src/app/features/activity/presenters/legacy-listening.presenter.ts:11-18`)
   maps the entire activity object to a `'listeningLearning'` block with
   `ctaLabel: 'Answer questions'`. There is no transformation that strips the
   exercise content for Step 1.
3. **Template rendering** —
   `activity-teach-page.component.html:108-147` (`@case ('listeningLearning')`)
   renders the scenario card, audio player ("Listen to the message, then answer the
   questions"), and "Transcript unlocks after you answer" message — i.e. the actual
   task — inside the "Lesson" step.

## 3. Frontend architecture findings (verified)

- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.ts` /
  `.html` — stateless component, 14 cards, each `routerLink="/module/gym-{key}"`.
- `src/LinguaCoach.Web/src/app/core/guards/module-redirect.guard.ts` — pure
  URL-redirect guard. `GYM_MODULES` dict maps `gym-listening` →
  `/activity?type=ListeningComprehension&returnTo=/practice`. No entity is created;
  it is a redirect only.
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
  — central 3-step stepper. `stepDots`: `{key:'learning',label:'Lesson'}`,
  `{key:'writing',label:'Practice'}`, `{key:'feedback',label:'Feedback'}`. State
  transitions: `loading`→`learning` on load, `learning`→`writing` on "Start
  Practice" click, `writing`→`feedback` after submit.
- `activity-lesson.component.html` renders `ActivityTeachPageComponent` (Step 1),
  `ActivityPracticePageComponent` (Step 2), `ActivityFeedbackPageComponent` (Step 3).
- `src/LinguaCoach.Web/src/app/features/activity/activity-teach-page/activity-teach-page.component.html:108-147`
  — `'listeningLearning'` case renders scenario, audio player, and "Transcript
  unlocks after you answer" — full exercise content.
- `src/LinguaCoach.Web/src/app/features/activity/activity-practice-page/activity-practice-page.component.html:219+`
  — `'listeningPractice'` case (Step 2) renders scenario recap, audio again, question
  inputs, Submit — overlapping content with Step 1.
- `src/LinguaCoach.Web/src/app/features/activity/presenters/legacy-listening.presenter.ts`
  — comment confirms: *"Bridges the legacy ListeningComprehension shape until it
  migrates to `listening_comprehension`."* This is acknowledged-temporary code.
- **Today flow**: `dashboard.component.html` (read in full this pass) shows "Today's
  Lesson" heading, a "Today's lesson" card with title/duration/focus/step-count from
  `todaysSession()` (a `LearningSession`), and a CTA `lessonButtonLabel()` linking to
  `/lesson/{sessionId}`.
- `src/LinguaCoach.Web/src/app/features/lesson/lesson.component.ts:141,145-146` —
  `moduleCtaLabel()` returns "Start module" / "Continue module"; link target is
  `/module/session-{sessionId}-{exerciseId}` (line 141), which `moduleRedirectGuard`
  rewrites to `/activity?activityId=X&returnTo=/lesson/{sessionId}` — same
  `LearningActivity`/3-step stepper as Practice Gym.

## 4. Backend/domain model findings

- Entity chain: `StudentProfile` → `LearningPath` (1 active) → `LearningModule[]` →
  `LearningSession[]` → `SessionExercise[]` → `LearningActivity` (nullable FK) →
  `ActivityAttempt[]`.
- `LearningActivity`: `ActivityType` enum (WritingScenario, ListeningComprehension,
  VocabularyPractice, SpeakingRolePlay), `AiGeneratedContentJson` (JSONB),
  `ExercisePatternKey` (nullable), `LearningModuleId` (nullable). Same entity serves
  both module-attached exercises and standalone Practice Gym activities.
- `ActivityAttempt`: `SubmittedContent`, `Answers/SubmittedAnswerJson`,
  `EvaluationResultJson`, `FeedbackJson`, `Score`.
- **No persisted "module run" or "stage" entity exists.** "Module" in routing/UI
  refers to either a `SessionExercise` (Today) or a freestanding `LearningActivity`
  (Practice Gym) — not a new domain object.

## 5. API/DTO findings

- `src/LinguaCoach.Api/Controllers/ActivityController.cs`:
  - `GET /api/activity/next` (~63-91) — query `?type=` or `?pattern=`, returns a
    single `ActivityDto`.
  - `POST /api/activity/{activityId}/attempt` (~206-258) — request
    `SubmitAttemptRequest{SubmittedContent, Answers[], ResponseText}`, response
    `ActivityFeedbackDto`.
- `ActivityDto` for ListeningComprehension (~529-543) carries:
  `scenario, speakerRole, listenerRole, transcriptAvailableAfterSubmit,
  listeningQuestions[], responseTask, audioAvailable, audioUrl, audioContentType,
  audioDurationSeconds`. **All fields are exercise-shaped — none represent a
  separate "lesson"/teaching payload.**
- The DTO returned for Step 1 and Step 2 is the **same object** — the frontend
  presenter is what splits it into `teachContent()` / `practiceContent()`, and for
  listening both map back to the same underlying fields.

## 6. AI prompt findings

`activity_generate_listening`
(`src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`, key constant at line 17,
prompt body ~160-202) produces exactly:

```json
{
  "activityType": "ListeningComprehension",
  "title": "...", "scenario": "...", "instructions": "...",
  "speakerRole": "...", "listenerRole": "...", "difficulty": "...",
  "audioScript": "...",
  "transcriptAvailableAfterSubmit": true,
  "questions": [ { "id": "q1", "question": "...", "expectedAnswer": "...", "type": "short_answer" } ],
  "responseTask": { "prompt": "...", "expectedFocus": "..." }
}
```

There is no `lessonContent`, `teachingNotes`, `vocabularyPreview`, or any field
intended for a Learn-only stage. The prompt is designed to generate **one exercise**,
not a Learn+Practice+Feedback module. Other generation prompts in the same file
(`ActivityGenerateSpeakingRolePlayContent` ~204+, writing/evaluation prompts) follow
the same single-exercise-JSON pattern (confirmed by file structure; full content of
those prompts not individually re-verified in this pass beyond key location).

## 7. Listening flow deep dive

Click path: Practice Gym "Listening" card → `/module/gym-listening` →
`moduleRedirectGuard` → `/activity?type=ListeningComprehension&returnTo=/practice` →
`ActivityLessonComponent` loads via `GET /api/activity/next?type=ListeningComprehension`
→ `ActivityDto` (exercise-shaped, from `activity_generate_listening` JSON) →
`LegacyListeningPresenter.teachContent()` → `'listeningLearning'` block →
`activity-teach-page.component.html:108-147` renders scenario + audio + "Answer
questions" CTA + "Transcript unlocks after you answer" (Step 1, labeled "Lesson").
Clicking CTA → `'writing'` state → `activity-practice-page.component.html:219+`
`'listeningPractice'` renders scenario recap + audio + question inputs + Submit
(Step 2, labeled "Practice") — **near-duplicate of Step 1's content**.
`POST /api/activity/{id}/attempt` → `ActivityFeedbackDto` → Step 3 ("Feedback").

## 8. Today/Daily Lesson flow deep dive

`dashboard.component.html:97-137` — "Today's lesson" card shows `todaysSession()`
(a `LearningSession`: title, durationMinutes, focusSkill, `exercises.length` as
"steps"), with status badge and CTA → `/lesson/{sessionId}`.
`lesson.component.ts` loads the `SessionDetailResponse` (ordered `SessionExercise[]`)
and renders a "Start module"/"Continue module" button per exercise
(`moduleCtaLabel()`, line 145-146) → `/module/session-{sessionId}-{exerciseId}`
(line 141) → same `moduleRedirectGuard` → same `/activity?activityId=...` 3-step
stepper as Practice Gym. So "Today's Lesson" → "module" → same Learn/Practice/Feedback
activity stepper, same per-activity-type duplication issue as section 7.

## 9. Learn/Practice/Feedback separation: real or cosmetic?

**Mostly cosmetic for ListeningComprehension.** The stepper UI (labels, step dots,
state machine) is real and generic. But:
- The underlying data is a single exercise DTO with no Learn-only fields.
- The presenter (`LegacyListeningPresenter`) does not derive distinct Learn content;
  it forwards the same fields to both Step 1 and Step 2.
- The template for Step 1 (`'listeningLearning'`) renders task-execution UI (audio
  player, CTA "Answer questions", transcript-lock notice) — not teaching content.

Whether other activity types (WritingScenario, SpeakingRolePlay, VocabularyPractice,
pattern-backed types like GapFill/Matching/Email/WorkplaceChat) have the same issue
was **not independently re-verified in this pass** — the original Explore agent
flagged this as unconfirmed without running the app or reading every presenter/prompt
pair. `LegacyListeningPresenter`'s own comment ("Bridges the legacy
ListeningComprehension shape until it migrates to `listening_comprehension`")
suggests this is a known, scoped legacy issue rather than the general pattern for
newer/pattern-backed activity types — but this is an **assumption**, not confirmed.

## 10. UI label changes that did not change structure

- Stepper label "Lesson" (Step 1) — renders the exercise itself for listening, not
  teaching content (`activity-lesson.component.ts` stepDots + `activity-teach-page.component.html:108-147`).
- "Start module" / "Continue module" button (`lesson.component.ts:145-146`) — routes
  to `/module/session-...`, which is purely redirected by `moduleRedirectGuard` to the
  same `/activity?activityId=...` stepper. No module entity/run is created.
- Practice Gym cards labeled with skill names route to `/module/gym-{key}`, again
  redirected to `/activity?type=...` — "module" is a routing alias, not a new entity.

## 11. Sidebar duplicate streak card

**Re-investigated and largely resolved.** Only **one** streak pill exists in
`src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.html:88-91`
(header, `.sp-header-actions`), driven by `streakDays` signal in
`student-app-layout.component.ts:19,41-46` (from `DashboardService.getDashboard()`).

`dashboard.component.html:140-145` separately renders a "Streak" stat card inside the
"Quick progress snapshot" grid (`data()?.streakDays`). This is a **second, different
UI element** (a stat card, not a pill) showing the same underlying number in a
different place (page body vs. header). It is not a duplicate render of the same
component — it is two separate displays of the same `streakDays` value by design.
**No bug found here** in this pass; if the user perceives "duplication," it is two
distinct UI affordances showing the same stat, not a rendering defect.

## 12. Exact files involved

- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.ts` / `.html`
- `src/LinguaCoach.Web/src/app/core/guards/module-redirect.guard.ts`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html`
- `src/LinguaCoach.Web/src/app/features/lesson/lesson.component.ts` / `.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts` / `.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-teach-page/activity-teach-page.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-practice-page/activity-practice-page.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/presenters/legacy-listening.presenter.ts`
- `src/LinguaCoach.Web/src/app/features/activity/presenters/activity-page-presenter.ts` (interface)
- `src/LinguaCoach.Api/Controllers/ActivityController.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` (key: `activity_generate_listening`, ~lines 160-202)
- `src/LinguaCoach.Domain/Entities/LearningActivity.cs`, `LearningSession.cs`, `SessionExercise.cs`, `ActivityAttempt.cs`
- `src/LinguaCoach.Web/src/app/layouts/student-app-layout/student-app-layout.component.html` (streak pill)

## 13. Risk areas

- Any fix to `LegacyListeningPresenter`/`activity-teach-page.component.html` must
  account for both Practice Gym entry (`type=ListeningComprehension`, no session) and
  Today/module entry (`activityId=...`, tied to a `SessionExercise`) — same component
  serves both.
- The AI prompt (`activity_generate_listening`) would need a new field set
  (e.g. teaching content) without breaking existing stored `AiGeneratedContentJson`
  for already-generated activities (backward compatibility for existing rows).
- `transcriptAvailableAfterSubmit` and "Transcript unlocks after you answer" UX is
  tied to the current single-stage design; separating Learn from Practice changes
  when/whether a transcript should appear.
- Pattern-backed activity types (`ExercisePatternKey` non-null, e.g. phrase_match,
  gap_fill_workplace_phrase, teams_chat_simulation) use a different presenter path
  (`PatternBackedPresenter`, not independently audited this pass) — may or may not
  share this issue.

## 14. Recommended fix direction (no code)

1. Confirm scope: audit each `ActivityPagePresenter` implementation
   (`WritingScenario`, `SpeakingRolePlay`, `VocabularyPractice`,
   `PatternBackedPresenter`) for the same Learn==Practice duplication before deciding
   if this is a listening-only legacy issue or systemic.
2. If listening-only: extend `activity_generate_listening` prompt output with a
   distinct teaching-only field set (e.g. short explanation + vocabulary/strategy tips,
   no questions/audio-as-task), and update `LegacyListeningPresenter.teachContent()`
   to render only that field set in Step 1, deferring scenario/audio/questions to
   Step 2 (`practiceContent()`).
3. If systemic across pattern-backed types: treat as a larger prompt-schema and
   presenter-interface change — define a shared `lessonContent` shape in the
   `ActivityPagePresenter` contract and require all generation prompts to populate it.
4. "Module" terminology (Practice Gym cards, "Start module" button) is currently a
   pure routing alias. Decide whether to (a) keep it as a label only and ensure docs/
   copy don't imply a persisted module-run concept, or (b) introduce an actual
   module-run/stage entity if reporting/progress tracking at module granularity is
   needed — this is a product decision, not purely a bug fix.

## Decisions made

None — this is an investigation-only pass. No implementation, refactor, or rename was
performed.

## AskUserQuestion decisions

None asked in this pass.

## Implementation tasks produced

None created yet (intentionally, per "no code" investigation scope). Recommended next
steps are in section 14.

## Final verdict

The 3-step Learn/Practice/Feedback structure exists at the UI/state-machine level but
is **not backed by distinct content** for ListeningComprehension — Step 1 renders the
exercise. Root cause is the combination of an exercise-only AI prompt schema
(`activity_generate_listening`) and a presenter (`LegacyListeningPresenter`) that
forwards the same data to both stages. "Module" labels in Practice Gym and Today are
routing aliases over existing `LearningActivity`/`SessionExercise` records with no new
persisted entity. The reported "duplicate streak card" is not a duplicate — it is two
distinct, intentional UI elements (header pill + dashboard stat card) showing the same
value.

## Next recommended action

Audit the remaining `ActivityPagePresenter` implementations (Writing, Speaking,
Vocabulary, pattern-backed) to determine whether the Learn==Practice duplication is
listening-specific (legacy bridge, as the code comment suggests) or systemic, before
scoping a fix.
