---
status: current
lastUpdated: 2026-06-13 00:00
owner: engineering
supersedes:
supersededBy:
---

# Activity 3-page restructure (Teach / Practice / Feedback) — engineering plan

Date: 2026-06-13
Related sprint: docs/sprints/current-sprint.md

## Future direction note (2026-06-13)

Out of scope for this plan, but recorded as the direction we're heading so
implementation choices below don't paint us into a corner:

- **Multi-course per student.** Today `StudentProfile -> LearningPath` is
  "one active path at a time" (`LearningPath.IsActive`, deactivate-old-to-replace).
  Future direction: a student may run multiple concurrent courses/tracks (e.g.
  "Workplace English" + "Day-to-day English"), i.e.
  `StudentProfile -> LearningPath[]` with each path representing a course track.
  This is a separate future architecture review (changes the "one active path"
  invariant, dashboard "current module" queries, and path generation). It is
  **not** scoped here.
- **Why it matters for this plan:** `LearningActivity` / `ExercisePatternDefinition`
  / the unified `{teach, practice}` content contract (Phase 2 below) already sit
  *below* `LearningPath` in the hierarchy and are course-agnostic — a pattern key
  like `phrase_match` works identically regardless of which course track it's
  used in. Multi-course makes pattern reuse *more* valuable, not less. As long as
  Steps 1-5 don't bake in "one course" assumptions (e.g. hardcoding topic/context
  strings, assuming a single `LearningModule` ordering global to the student),
  they remain compatible with multi-course when that work happens.
- **Language pair scalability.** Already reasonably scalable —
  `StudentProfile.LanguagePairId` drives AI generation context
  (`SourceLanguageName`/`TargetLanguageName`), and `LearningActivity` content is
  just generated text in JSONB. The unified `{teach, practice}` contract (Phase 2)
  should keep `instructionInSourceLanguage` as a first-class field (per existing
  invariant in `learning-activity-engine.md`) so RTL/source-language rendering
  keeps working for any language pair, not just Persian->English.
- **General scalability guidance for Steps 1-5:** prefer driving UI/copy from
  `ExercisePatternDefinition` fields (`primarySkill`, `teachingPurpose`, etc.) and
  `ActivityDto` data over hardcoded per-type strings, so adding a new course
  track, language pair, or pattern doesn't require touching
  `ActivityPagePresenter` implementations.

## Request

User request (verbatim):

> lets make sure everything works. I want to restructure and re design. for both
> lessons and practice gyme, this is the strucute, should be, almoste every
> teaching material should be this. 3 pages, first page teaches what you need to
> learn, seceond page is excersixe one of the supported excersies, and 3rd page is
> feedback, while doing this sprint confirm this flow every where and make sure
> everything aligns with this. the strucure of this should be scalable and
> enterprise, make sure you use inheritatance, then we have lesson which includes
> modules, each module has (teaching first page, Practice (supported exercise,
> feedback)

Goal: every activity (Today's Lesson session steps AND Practice Gym standalone
activities) should present as **Teach -> Practice -> Feedback**, implemented in a
scalable way that doesn't require copy-pasting per-activity-type code, without
breaking any of the 10 currently working activity types or the redo/improve/next
loop.

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts` (576 lines)
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.html` (876 lines)
- `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.*`
- `src/LinguaCoach.Web/src/app/features/activity/pattern-evaluation-result/`
- `docs/architecture/course-session-learning-model.md`
- `docs/architecture/practice-gym.md`
- `docs/architecture/exercise-pattern-library.md`

## Step 0 — Scope challenge

**What already solves part of this?**

- The 3-step model **already exists** conceptually: `stepDots` =
  `[Lesson, Practice, Feedback]` and `PageState` already groups states into these
  three buckets via `stepState()`.
- **Page 2 (Practice/exercise) is already mostly unified** for 8 of 10 activity
  types via `ExerciseRendererComponent` + `interactionMode`
  (matchingPairs, gapFill, audioAndFreeText, audioAndGapFill, readOnly, chatReply,
  freeTextEntry, emailReply). Only `speakingRolePlay` (record/playback UI) and the
  pre-pattern-engine legacy `vocabularyPractice` / `listeningComprehension` /
  `writingScenario` branches sit outside it.
- **Page 3 (Feedback) is already mostly unified** via
  `app-pattern-evaluation-result` for `patternEvaluation`-shaped feedback. Legacy
  feedback sections exist only for non-pattern activity types.

**What is actually broken / missing?**

The real gap is **Page 1 (Teach)**. There is no shared "teach" component — instead
there are 4 large parallel `@if` blocks
(SpeakingRolePlay / VocabularyPractice / ListeningComprehension / WritingScenario)
each duplicating the same skeleton: skill badge, AI/fallback badge, difficulty,
title, scenario/situation card, learning goal, source-language instruction, target
phrases / vocab items / expected points, examples, "Start" button.

**Minimum change set (recommendation):**

1. Extract a single `ActivityTeachPageComponent` that takes `activity: ActivityDto`
   as input and internally branches on `activity.activityType` /
   `activity.interactionMode` for the handful of fields that differ (situation vs.
   scenario vs. speaking-scenario card, vocab list vs. target phrases vs. expected
   points). One component, one template, type-specific fragments as `@switch`
   blocks — not 4 separate classes.
2. Extract `ActivityPracticePageComponent` that wraps the existing
   `ExerciseRendererComponent` for pattern-engine types, and contains the existing
   SpeakingRolePlay record/playback states and the 3 legacy writing/vocab/listening
   "practice" blocks as `@switch` fragments, for the activity types not yet on the
   pattern engine.
3. Extract `ActivityFeedbackPageComponent` that wraps `app-pattern-evaluation-result`
   for pattern-based feedback and contains the legacy feedback fragments for the
   rest.
4. `ActivityLessonComponent` becomes a thin **orchestrator/shell**: owns the state
   machine (`PageState`, signals, HTTP calls, recording logic), renders the step
   dots, and projects one of the 3 page components based on `stepState()`.

This is **3 new presentational components + 1 simplified orchestrator** = 4 files
touched/added on the frontend. No backend or data-model changes. This is under the
8-file / 2-new-service complexity threshold, so Step 0 does not trigger the
scope-reduction gate.

## Inheritance vs. composition — overriding the request

The user asked for "inheritance" to make this "scalable and enterprise". Recommend
**composition over class inheritance** for this case:

- Angular's idiomatic pattern for "one shell, varying content per type" is
  presentational child components driven by `@Input()`/`@Output()` and internal
  `@switch` on a discriminant (`activityType` / `interactionMode`) — not a class
  hierarchy of `TeachPageBase` -> `VocabTeachPage`, `WritingTeachPage`, etc.
- Class inheritance across Angular components couples templates, DI, and lifecycle
  hooks in ways that are hard to test and that Angular's own style guide advises
  against for components (composition/content-projection is preferred; inheritance
  is reserved for sharing *behavior*, not templates).
- The "scalable for new activity types" goal is better served by **one switch
  statement per page component** (3 places to add a `case`, total) than by **a new
  subclass per type x 3 pages** (30 classes for 10 types).

**[Layer 1]** — boring, idiomatic Angular composition. **Recommendation:**
composition with type-discriminated `@switch` fragments inside 3 shared page
components. If the user specifically wants OO inheritance for non-Angular reasons
(e.g. shared business logic classes), that can live in TypeScript service/helper
classes (e.g. `ActivityTypePresenter` base class with per-type subclasses providing
labels/icons/copy) consumed by the page components — composition at the component
level, inheritance (if desired) at the data/presenter level. This satisfies both
asks without fighting the framework.

## Lesson -> Module -> (Teach, Practice+Feedback) hierarchy

Per `docs/architecture/course-session-learning-model.md`:

```
LearningPath -> LearningModule -> LearningSession -> SessionExercise -> LearningActivity
```

A `LearningSession` already follows a "Teaching Sequence" (7 steps: Warm-up, Input,
Noticing, Controlled Practice, Semi-controlled Practice, Productive Task,
Feedback/Reflection) — which collapses cleanly into **Teach (steps 1-3) -> Practice
(steps 4-6) -> Feedback (step 7)** at the UI level. No data model change needed —
this is a **view-layer grouping** of existing `SessionExercise` steps into 3 pages.

**Practice Gym** (`docs/architecture/practice-gym.md`) explicitly has no session —
a `LearningActivity` stands alone. The 3-page Teach/Practice/Feedback structure
still applies because it's a property of `ActivityLessonComponent` rendering a
single `LearningActivity`, regardless of whether that activity belongs to a
session. **No conflict** — confirmed.

```
┌─────────────────────────────────────────────────────────────┐
│ ActivityLessonComponent (orchestrator/shell)                 │
│  - state machine (PageState, signals)                        │
│  - step dots (Teach / Practice / Feedback)                    │
│  - HTTP: load activity, submit answer, load feedback          │
│                                                                │
│  ┌──────────────┐   ┌──────────────────┐  ┌────────────────┐│
│  │ TeachPage     │   │ PracticePage      │  │ FeedbackPage   ││
│  │ @switch       │   │ @switch           │  │ @switch        ││
│  │ activityType  │   │ ExerciseRenderer  │  │ PatternEval    ││
│  │               │   │  | SpeakingRecord │  │  | legacy frag ││
│  │               │   │  | legacy frags   │  │                ││
│  └──────────────┘   └──────────────────┘  └────────────────┘│
└─────────────────────────────────────────────────────────────┘
        Used identically for Today's Lesson (session step)
        and Practice Gym (standalone activity) — same component,
        same 3 pages, source of LearningActivity differs only in
        how loadActivity() fetches it (already abstracted today).
```

## Risks / edge cases

- **SpeakingRolePlay** has extra sub-states (`mic-permission`, `ready`, `recording`,
  `recorded`, `submitting-audio`) that don't map 1:1 onto Teach/Practice/Feedback —
  they're all "Practice" sub-states. `PracticePageComponent` must own these
  sub-states internally (today they're flat siblings in the 876-line template);
  this is a real refactor, not just a move.
- **Retry/improve loop** (`attemptCount`, `previousScore`, `tryAgain`,
  `improveAnswer`, `nextActivity`) is currently feedback-page-driven and must keep
  emitting events the orchestrator handles — straightforward `@Output()`s on
  `FeedbackPageComponent`.
- **Audio blob lifecycle** (`activityAudioBlobUrl`, recording cleanup) stays in the
  orchestrator (it's tied to `ngOnDestroy`/HTTP, not presentation).
- Must preserve every existing `@if` condition's *content* exactly — this is a
  structural move, not a redesign of copy/markup, to avoid regressing any of the 10
  activity types.

## Decisions

1. Composition (3 shared page components + `@switch` per type) over class
   inheritance, for the reasons above.
2. Practice Gym and Today's Lesson use the identical 3-page shell; the existing
   `loadActivity()` abstraction (by `activityId` or `pattern`) already handles the
   data-source difference.
3. SpeakingRolePlay's recording sub-states move into `PracticePageComponent` as
   internal sub-state, not new top-level `PageState` values.

---

## Phase 2 (user-requested expansion): full-stack unification

**User decision (2026-06-13):** scope is not frontend-only. The "Teach / Practice
/ Feedback, scalable, enterprise" structure should also be reflected in the
backend `LearningActivity` model, AI prompts, and the pattern engine — not just
the Angular templates.

### What already exists (good news)

`docs/architecture/exercise-pattern-library.md` already defines this exact
contract for 8 of 10 activity types via `ExercisePatternDefinition`:

```
ExercisePatternDefinition
  ├── AiGeneratePromptKey   → "Teach" + "Practice" content generation (one prompt)
  ├── InteractionMode       → which Practice-page renderer to use
  ├── MarkingMode           → which evaluator produces the Feedback page
  └── AiEvaluatePromptKey   → "Feedback" content generation
```

This **is** the unification mechanism the user is describing — it's just not yet
applied to all activity types. The gap is two **legacy paths** that predate the
pattern engine:

| Activity type | Pattern-engine coverage today | Gap |
|---|---|---|
| VocabularyPractice, ListeningComprehension | Partial — gap-fill/matching patterns exist, but original `activity_generate_vocabularyPractice` / `activity_generate_listeningComprehension` + bespoke `ActivityDto` fields (`vocabItems`, `listeningQuestions`) still used in `ActivityGetHandler.MapToDto` | Needs migration to pattern-engine prompt keys + `contentJson` |
| WritingScenario (non-pattern) | Legacy `activity_generate_writing` / `activity_evaluate_writing`, bespoke fields (`situation`, `targetPhrases`, `exampleText`) | Needs a `writing_scenario`-equivalent pattern (closest existing: `teams_chat_simulation`, `email_reply` — but generic open writing has no pattern yet) |
| SpeakingRolePlay | Legacy `activity_generate_speakingRolePlay`, `FakeSpeechToTextService` | `roleplay_speaking_turn` / `spoken_response_from_prompt` patterns exist but speaking-specific recording UI isn't pattern-driven |

### Proposed unified contract

Introduce a documented, type-agnostic **content contract** that every
`AiGeneratePromptKey` response must conform to, replacing today's per-type bespoke
JSON shapes:

```json
{
  "teach": {
    "title": "...",
    "skillLabel": "Vocabulary | Listening | Writing | Speaking | ...",
    "instructionInSourceLanguage": "...",
    "context": "...",          // situation / scenario / passage / chat thread — one field, type renders it appropriately
    "learningGoal": "...",
    "targetPhrases": ["..."],   // optional
    "supportItems": [...],      // optional — vocab items, expected points, etc.
    "exampleText": "..."        // optional
  },
  "practice": {
    "interactionMode": "GapFill | MatchingPairs | ChatReply | ...",
    "payload": { ... }           // interactionMode-specific, as today's contentJson
  }
}
```

`"feedback"` is **not** part of generation output — it remains evaluation-time,
produced by `AiEvaluatePromptKey` via `IPatternEvaluationRouter` exactly as today
(`PatternEvaluationResult`). No change needed there; it already maps cleanly to
"Page 3".

### Backend changes

1. **`LearningActivity`** stays JSONB-backed (per `learning-activity-engine.md`
   invariant: "JSONB content is type-specific but schema-less at the DB level").
   No schema/migration change. `AiGeneratedContentJson` now stores
   `{ teach: {...}, practice: {...} }` for *newly migrated* types — old rows keep
   their legacy shape (read-path must handle both during migration, see below).
2. **`ActivityGetHandler.MapToDto`** — for migrated types, deserialize
   `teach`/`practice` into `ActivityDto.teach` / `ActivityDto.practice` (new
   typed sub-objects) instead of the current flat bag of ~30 optional fields.
   Legacy (unmigrated) types continue mapping to today's flat fields — additive,
   not breaking.
3. **New `ExercisePatternDefinition` rows** for the 3 gap areas:
   - `vocabulary_practice` / `listening_comprehension` — formalize existing
     gap-fill/matching coverage as the canonical pattern, retire bespoke
     `vocabItems`/`listeningQuestions` DTO fields once migrated.
   - `open_writing_task` — new pattern for free-form WritingScenario (closest
     `MarkingMode`: `AiOpenEnded`, `InteractionMode`: `FreeTextEntry`).
   - `speaking_roleplay_turn` — formalize `roleplay_speaking_turn` as the
     canonical pattern for SpeakingRolePlay; recording UI becomes a Practice-page
     `InteractionMode` variant (`AudioResponse`) rather than component-local
     `PageState` branches.
4. **AI prompts** (`ai_prompts` table) — add `activity_generate_{new pattern key}`
   prompt templates producing the unified `{teach, practice}` shape. Existing
   `activity_generate_{activityType}` prompts for the 3 gap types are
   **deprecated, not deleted** (fallback continuity during migration — see Risks).

### Frontend changes (supersedes/extends Phase 1)

Add the presenter/strategy class layer discussed earlier, now backed by the real
unified contract:

```typescript
// One interface, implemented per ExercisePatternKey (not per ActivityType)
interface ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel;   // reads activity.teach (or legacy fields as fallback)
  practiceRenderer(): InteractionMode;                    // reads activity.practice.interactionMode
  feedbackLayout(): 'pattern' | 'legacy';
}

class PatternBackedPresenter implements ActivityPagePresenter { ... }  // generic, data-driven — works for ALL migrated patterns
class LegacyWritingPresenter implements ActivityPagePresenter { ... }  // bridges old flat fields until migrated
class LegacyVocabPresenter implements ActivityPagePresenter { ... }
class LegacyListeningPresenter implements ActivityPagePresenter { ... }
```

`ActivityPresenterFactory.for(activity)` picks `PatternBackedPresenter` when
`activity.exercisePatternKey` is set and the pattern is migrated, else the
matching `Legacy*Presenter`. **This is the actual "inheritance for
scalability" the user wants** — new pattern keys need zero new presenter
classes (handled generically by `PatternBackedPresenter`); only the 3 legacy
types need (temporary) bridge classes, deleted once migrated.

`TeachPageComponent` / `PracticePageComponent` / `FeedbackPageComponent` (Phase 1)
take `presenter: ActivityPagePresenter` + `activity` as inputs and render
`presenter.teachContent(activity)` etc. — zero `@switch (activityType)` in
templates.

### Migration order (incremental, strangler-fig — not big bang)

```
Step 1: Frontend composition split (Phase 1 plan above) — ships independently,
        all 10 types still legacy-presenter-backed, ZERO behavior change.
        ↓
Step 2: Introduce ActivityPagePresenter interface + PatternBackedPresenter +
        3 Legacy*Presenter bridge classes. Still zero behavior change — this
        is a pure refactor of Step 1's @switch into presenter classes.
        ↓
Step 3: Migrate VocabularyPractice + ListeningComprehension to pattern engine
        (new prompt keys, new ExercisePatternDefinition rows, contentJson).
        LegacyVocabPresenter / LegacyListeningPresenter become dead code once
        all active LearningActivity rows of these types are regenerated or
        retired — delete then.
        ↓
Step 4: New `open_writing_task` pattern for WritingScenario. Same retire process
        for LegacyWritingPresenter.
        ↓
Step 5: SpeakingRolePlay → `speaking_roleplay_turn` pattern + AudioResponse
        InteractionMode. Recording sub-states move from component-local
        PageState into PracticePageComponent's AudioResponse renderer.
```

Each step is independently shippable and testable. Steps 3-5 are genuinely
multi-day backend+AI+frontend changes each (new prompts need calibration against
real AI providers, per `live-ai-quality-review-prompt-calibration-sprint.md`
precedent) — **do not compress into one PR**.

## Risks / edge cases (Phase 2 additions)

- **Old `LearningActivity` rows with legacy JSON shapes never get migrated**
  (no backfill job planned) — `Legacy*Presenter` bridge classes must remain until
  every active row of that type is naturally regenerated (AI activities expire/
  regenerate; `SystemFallback` seed rows would need explicit reseeding under the
  new shape, or the bridge presenters become permanent for fallback rows).
- **AI prompt changes require calibration**, not just a template edit — new
  `{teach, practice}` shape must be validated against real provider output for
  each migrated pattern before flipping `ExercisePatternKey` assignment in
  `ActivityGetHandler`/`ExercisePrepareHandler`.
- **SpeakingRolePlay is the highest-risk migration** — `FakeSpeechToTextService`
  and the recording UI are tightly coupled; moving to `AudioResponse`
  InteractionMode touches `MediaRecorder` lifecycle code that's currently
  orchestrator-owned (Phase 1 risk list). Recommend doing this step last, after
  the pattern is proven on the other 2 legacy types.
- **`docs/architecture/learning-activity-engine.md` "Activity Type Roadmap"
  table** and `exercise-pattern-library.md` "Pattern Implementation Priority"
  table both need updates as each step lands — these are the canonical source of
  truth for which types/patterns are migrated.

## Decisions (Phase 2)

5. Unified `{teach, practice}` JSONB content contract for pattern-generated
   activities; `feedback` stays evaluation-time via existing
   `PatternEvaluationResult` (no change there).
6. `ActivityPagePresenter` interface + `PatternBackedPresenter` (generic, data-
   driven, works for all current and future pattern keys) + 3 temporary
   `Legacy*Presenter` bridge classes (deleted once their type is migrated). This
   is where "inheritance for scalability" actually lives — at the presenter
   layer, not the page-component layer (composition, per Phase 1 decision 1).
7. Strangler-fig migration, 5 independently-shippable steps. Steps 3-5 each get
   their own sprint doc + AI calibration pass — not combined.
8. No `LearningActivity`/DB schema changes (JSONB stays schema-less per existing
   invariant); legacy bridge presenters handle un-migrated rows indefinitely if
   needed.

## Implementation tasks

1. **Step 1** — Create `ActivityTeachPageComponent`, `ActivityPracticePageComponent`,
   `ActivityFeedbackPageComponent`; slim `ActivityLessonComponent` to
   orchestrator + `@switch(stepState)`. (Phase 1 tasks 1-5, unchanged.)
2. **Step 2** — Define `ActivityPagePresenter` interface,
   `ActivityPresenterFactory`, `PatternBackedPresenter`, and 3
   `Legacy*Presenter` bridges. Refactor Step 1's `@switch` blocks into these
   classes. Unit tests per presenter. `PatternBackedPresenter` must derive
   display data (skill label, teaching purpose, badges) from
   `ExercisePatternDefinition`/`ActivityDto` fields, not hardcoded per-type
   strings — so new pattern keys (future courses/languages) need zero presenter
   changes (see Future direction note).
3. **Step 3** — New `ExercisePatternDefinition` rows + AI prompts for
   `vocabulary_practice` / `listening_comprehension` patterns; migrate
   `ActivityGetHandler`/`ExercisePrepareHandler` to assign these pattern keys for
   new activities; calibrate prompts against live AI providers; retire
   `LegacyVocabPresenter`/`LegacyListeningPresenter` once no active rows need them.
4. **Step 4** — `open_writing_task` pattern + prompt; same migration/retirement
   for `LegacyWritingPresenter`.
5. **Step 5** — `speaking_roleplay_turn` pattern + `AudioResponse`
   InteractionMode; move recording UI into `PracticePageComponent`; retire
   component-local speaking `PageState` values.
6. Update `course-session-learning-model.md`, `practice-gym.md`,
   `exercise-pattern-library.md` (Pattern Implementation Priority table), and
   `learning-activity-engine.md` (Activity Type Roadmap table) as each step lands.

## Final verdict

Plan approved for Step 1 (frontend composition split, zero behavior change) to
start immediately. Steps 2-5 are real, separately-scoped sprints — each needs its
own sprint doc, and Steps 3-5 need AI prompt calibration passes. Composition at
the page-component layer (Phase 1) + presenter-interface inheritance at the
content layer (Phase 2) satisfies both "enterprise/scalable" and "use
inheritance" without fighting Angular's component model.

## Next recommended action

Begin Step 1 (frontend split — Phase 1 tasks). Step 2 (presenter interfaces) can
follow in the same sprint since it's a refactor of Step 1 with no behavior change.
Steps 3-5 each become their own sprint doc, scheduled after Step 1+2 ship and are
verified against all 10 activity types.

## UNRESOLVED DECISIONS

- Whether `SystemFallback` seed rows for the 3 legacy types get reseeded under the
  new `{teach, practice}` shape (full migration) or whether `Legacy*Presenter`
  bridges become permanent fallback-only code paths. Defer to Step 3 planning —
  depends on how many active fallback rows exist per type (check
  `LearningActivitySeeder` row counts before deciding).
