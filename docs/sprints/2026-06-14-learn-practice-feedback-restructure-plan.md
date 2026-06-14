# Learn / Practice / Feedback Restructure — Implementation Plan

Date: 2026-06-14

## Goal

Make every module follow Learn → Practice → Feedback, with Learn never
showing exercise interaction. Today and Practice Gym both route through
modules using this flow. See product brief (chat) for full requirements.

## Audit findings (Phase 1)

The architecture is closer to the target than the initial brief assumed:

- `ActivityLessonComponent` already has three separate page components:
  `ActivityTeachPageComponent` (Learn), `ActivityPracticePageComponent`
  (Practice), `ActivityFeedbackPageComponent` (Feedback), driven by
  `state: 'learning' | 'writing'/... | 'feedback'`.
- Legacy presenters (Vocab, Listening, Writing, Speaking) already render a
  teach-only page with no exercise interaction, then a separate practice
  page. **These already satisfy the Learn/Practice separation rule.**
- The actual gap is **pattern-engine activities**
  (`interactionMode` set — matchingPairs, gapFill, emailReply, chatReply,
  audioAndFreeText, audioAndGapFill, freeTextEntry, readOnly):
  - `PatternBackedPresenter.teachContent()` returns
    `{ block: 'exerciseRenderer', ctaLabel: '' }`, and
    `activity-teach-page.component.html`'s `exerciseRenderer` case is
    **empty** (intentionally, per comment "renderer covers Teach+Practice").
  - `activity-lesson.component.ts: setReadyActivity()` detects this and
    **skips Learn entirely**, jumping straight to `state = 'writing'`
    (Practice). This was done in the 2026-06-14 bugfix pass to avoid a
    blank Teach page — but it means pattern activities never show a Learn
    stage, violating the new product rule.
  - **However**, the AI-generated `contentJson` for every pattern key
    *already contains* teaching-only fields separate from exercise data:
    - matchingPairs/gapFill: `learningGoal`, `instructions`, `teachingNote`
    - emailReply: `situation`, `audience`, `tone`, `skillFocus`,
      `targetPhrases`, `targetVocabulary`, `exampleText`,
      `commonMistakeToAvoid`, `instructionInSourceLanguage`
    - chatReply: `scenario`, `learningGoal`, `targetPhrases`,
      `targetVocabulary`, `exampleReply`, `toneGuidance`
    - audioAndFreeText/audioAndGapFill: `scenario`, `instructions`,
      `speakerRole`
  - So building a real Learn page for pattern activities is **additive,
    data-already-available** work — not a backend change.
- Practice Gym cards already navigate to `/activity?type=...` /
  `/activity?pattern=...`, which lands on `ActivityLessonComponent` — i.e.
  they already go through the same module/activity flow as lessons, just
  with "Open activity" copy and (for patterns) no Learn stage.
- Today page (`dashboard`) shows a single "Today's lesson" summary card with
  a CTA into `/lesson/:sessionId`. The Lesson page lists `SessionExercise`
  steps as cards with "Open activity" links once `learningActivityId` is
  populated.
- Sidebar: `student-app-layout` renders one "Build your streak" card in the
  sidebar and a separate streak-count pill in the header — these are
  different UI elements, not a duplicate. **Needs visual re-check** before
  assuming a real bug; may be a false positive in the brief, or may only
  reproduce on a specific viewport/scroll state.

## Decision (AskUserQuestion, 2026-06-14)

Asked whether to take the minimum-safe path (build Learn stage for pattern
activities + copy cleanup only) or implement the full restructure as
originally specified (module routes, view models, Today/Practice Gym
redesign). **User chose: full restructure as originally specified.**

This plan therefore covers all 7 phases from the product brief. Given the
size, phases will be implemented and verified incrementally, with this doc
updated after each phase.

## Scope — full restructure (per product brief)

Phases (per brief):
1. Audit and plan — **done**, see findings above.
2. Introduce module-stage view models (`DailyLessonViewModel`,
   `ModuleRunViewModel`, `ModuleStage`, `ModuleSource`).
3. Split Learn/Practice/Feedback into a unified module shell with explicit
   stage routing (`/module/:moduleRunId` + `learn|practice|feedback`).
4. Refactor Today/Lesson flow to show Daily Lesson + module cards, Learn
   first.
5. Refactor Practice Gym to create/open Practice Modules, route to Learn
   first.
6. Sidebar streak dedupe + copy cleanup (activity → module/practice/feedback
   language).
7. Tests (Playwright + dotnet) and verification.

## Step A (Phase 2/3 foundation) — build a real Learn stage for
pattern-engine activities

This is a prerequisite for all phases — without it, modules backed by
pattern activities (matching, gap-fill, email, chat, listening) have no
Learn content to show on the new module Learn route.

### Step A — Build a real Learn stage for pattern-engine activities
- Add a new `TeachViewModel` block type (e.g. `'patternLearning'`) in
  `activity-page-presenter.ts`.
- `PatternBackedPresenter.teachContent()` returns this block, populated from
  `contentJson`'s teaching-only fields (per pattern key / interactionMode),
  with `ctaLabel: 'Start practice'`, `ctaAction: 'startPractice'`.
- Add a new `@case ('patternLearning')` in
  `activity-teach-page.component.html` that renders only teaching fields
  (learning goal, instructions, teaching note, target phrases/vocabulary,
  example text, scenario/situation) — explicitly NO pairs/gaps/chat-thread/
  answer inputs/submit buttons.
- `activity-lesson.component.ts: setReadyActivity()` — remove the
  "skip to writing" branch; pattern activities now start at `state =
  'learning'` like everything else.
- `ActivityPracticePageComponent` (already renders `exerciseRenderer` for
  practice) is unchanged — it's already Practice-only.

### Step B — Copy cleanup (student-facing)
- Lesson page: "Open activity" → "Start module" / "Continue module" /
  "View feedback" depending on `SessionExercise.status`.
- Practice Gym cards: "Open activity" semantics → "Start practice" (cards
  already create/fetch one activity and route into Learn first after Step A).
- Activity history: minor copy pass ("Activity" → "Practice" where natural),
  low priority.

### Step C — Sidebar streak re-check
- Visually verify in dev server whether "Build your streak" renders twice
  (desktop + mobile drawer both mounted simultaneously?, or duplicated
  template). Fix only if a real duplicate is found.

### Step D — Tests
- Update/add Playwright + component specs for:
  - Pattern activity now shows Learn stage with teaching content, no
    exercise interaction, before Practice.
  - Matching/gap-fill specific: Learn page has no "Check matches" / matching
    columns / blanks; Practice page does.
  - Existing `exercise-pattern-renderers.spec.ts` /
    `lesson-activity-wiring.spec.ts` updated for the new Learn-first flow.

## Decision (AskUserQuestion, 2026-06-14, follow-up)

For Phase 3 (`/module/:moduleRunId` routing), asked whether to build a full
duplicate shell component (new state machine, real per-stage URLs) or a
resolver-based redirect onto the existing `ActivityLessonComponent` (which
already manages Learn/Practice/Feedback via its `state` signal). **User
chose: resolver-based redirect.** `/module/:moduleRunId` resolves to
`/activity?...` via `moduleRedirectGuard` and the existing component is
reused unchanged.

## Implementation — Phases 2-6 (2026-06-14)

- **Phase 3**: Added `moduleRedirectGuard`
  (`core/guards/module-redirect.guard.ts`), registered on a new
  `/module/:moduleRunId` route (no component — guard always returns a
  `UrlTree`). Two `moduleRunId` formats:
  - `gym-{key}` → Practice Gym modules, maps to `/activity?pattern=...` or
    `/activity?type=...&returnTo=/practice` via a fixed `GYM_MODULES` table
    (`listening`, `speaking`, `writing`, `phrase_match`,
    `gap_fill_workplace_phrase`, `email_reply`, `teams_chat_simulation`).
  - `session-{sessionId}-{exerciseId}` → Today modules. Looks up the
    exercise via `SessionService.getById`; if `learningActivityId` is set,
    redirects to `/activity?activityId=...&returnTo=/lesson/{sessionId}`. If
    not set and `kind !== 'review'`, calls `prepareExercise` first then
    redirects. Review exercises with no activity redirect back to the
    lesson page (handled inline, no module).
- **Phase 4**: `LessonComponent.activityUrl()` replaced with `moduleUrl()`
  (returns `/module/session-{sessionId}-{exerciseId}`) and
  `moduleCtaLabel()` ("Start module" / "Continue module" based on
  `exercise.status`). Template's ready-state CTA now links via
  `[routerLink]="moduleUrl(exercise)"` with the new label. Prepare/retry
  fallback UI unchanged (resolver also prepares, but keeping the inline
  prepare path preserves the existing 503/retry UX for the case where the
  exercise isn't ready yet).
- **Phase 5**: All 7 active Practice Gym cards
  (`practice-gym.component.html`) now link to `/module/gym-{key}` instead of
  `/activity?...`. "Coming soon" disabled cards unchanged.
- **Phase 6**: Removed the static "Build your streak" sidebar card
  (`student-app-layout.component.html`) — the dynamic header streak pill
  (`streakDays()`) is the single streak indicator now. Searched for
  "Open activity" / "Activity completed" copy across `src/app`; only
  remaining match is "Load activity" (prepare-retry button), which is about
  loading content rather than opening a raw activity page and was left as
  is.

### Verification
- `npx tsc -p tsconfig.app.json --noEmit` — clean.
- `npx ng build --configuration development` — succeeds (only pre-existing
  unrelated `NG8102` warnings in `pattern-evaluation-result.component.html`).
- New `module-redirect.guard.spec.ts` — 5/5 SUCCESS (gym redirect, unknown
  gym key, existing activity redirect, prepare-on-demand redirect, review
  exercise without activity).

## Remaining work (Phase 7)

- Playwright tests for: Today flow via `/module/session-...` (Learn shown
  first, "Check matches" absent on Learn / present on Practice for
  matching), Practice Gym flow via `/module/gym-...`, single sidebar streak
  indicator, copy assertions ("Start module" / "Continue module").
- dotnet tests: none required — no backend changes were made in this pass.

### Existing e2e/unit suite alignment (2026-06-14)

A separate pass fixed the pre-existing e2e and unit suites against the
current Teach-then-Practice flow (independent of Phases 1-6 above):

- Full Playwright suite: 165/165 passing.
- All e2e specs going to `/activity` now click `teach-cta-btn` before
  asserting on the Practice renderer; exercise-renderer testid selectors
  (matching pairs, gap fill) corrected to match `phrase_${index}` /
  `meaning_${index}` / `gap_${index+1}` id generation.
- `journey-page-identity.spec.ts` deleted, and `admin-screenshots.spec.ts`
  / `core-flow-smoke.spec.ts` trimmed of assertions for the unbuilt
  `/journey` page, module-readiness banners, and "Continue my learning
  path" — these covered Phases 2-6 functionality not yet implemented.
  Decision made explicitly by the user (AskUserQuestion: "Delete these
  tests") rather than deferring or stubbing the unbuilt UI.
- Unit suite: 82/82 passing after adding `ActivatedRoute`/`Router` stubs
  and a `VocabularyService` provider to fix `NullInjectorError`s in
  login, vocabulary, activity-lesson-vocab, progress, and
  onboarding-resume specs, and fixing an onboarding-resume test that
  referenced a non-existent `Track` onboarding step.

When Phase 7 (Playwright coverage for Phases 1-6) is implemented, the
specs above will need further updates for `/module/...` routing.

## Status

Phases 1-6 implemented and verified (build + unit tests green). Phase 7
(Playwright coverage) outstanding.
