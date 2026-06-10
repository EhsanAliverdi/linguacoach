---
status: current
lastUpdated: 2026-06-10
owner: engineering
supersedes:
supersededBy:
---

# Sprint: Practice Gym Activation / Pattern-Based Free Practice

**Date:** 2026-06-10
**Follows:** Student UX Alignment / Writing-Assumption Cleanup Sprint (complete)

---

## Audit Findings

### Current Practice Gym state

`/practice` → `PracticeGymComponent` — a static HTML template with no logic in the `.ts` file.

**Functional cards (link to `/activity?type=...`):**
- Vocabulary → `/vocabulary` (direct route, not `/activity`)
- Listening → `/activity?type=ListeningComprehension`
- Writing → `/activity?type=WritingScenario`
- Speaking → `/activity?type=SpeakingRolePlay`

**Coming-soon cards (no link, `aria-disabled`, opacity 0.62):**
- Workplace Chat
- Email
- Gap Fill
- Phrase Match
- Pronunciation

### Activity loading flow (existing)

```
/activity?type=X
  → ActivityLessonComponent.loadActivity()
  → ActivityService.getNext(type?)
  → GET /api/activity/next?type=X
  → ActivityGetHandler.HandleAsync(GetNextActivityQuery(userId, PreferredType?))
```

`GetNextActivityQuery` accepts only `ActivityType? PreferredType`. There is no `patternKey` parameter in the query or the controller endpoint today.

### Pattern infrastructure already built

All four target pattern keys are seeded and fully wired:

| Pattern key | ActivityType | InteractionMode | MarkingMode |
|---|---|---|---|
| `phrase_match` | VocabularyPractice | MatchingPairs | KeyedSelection |
| `gap_fill_workplace_phrase` | VocabularyPractice | GapFill | ExactMatch |
| `email_reply` | WritingScenario | FreeTextEntry | AiStructured |
| `teams_chat_simulation` | WritingScenario | ChatReply | AiStructured |

- `ExercisePatternKey` constants exist in `LinguaCoach.Domain`.
- `ExercisePatternSeeder` seeds all four with `aiGeneratePromptKey` values.
- `PatternEvaluationRouter`, `AiStructuredEvaluator`, `KeyedSelectionEvaluator`, `ExactMatchEvaluator` all handle these patterns.
- `PatternEvaluationResultComponent` renders results for all four.
- `ExerciseRendererComponent` handles all four `InteractionMode` values.
- `ActivityLessonComponent` already uses `usesExerciseRenderer()` and `onRendererSubmit()`.

### Gap: no pattern-keyed entry point

`GET /api/activity/next` accepts only `?type=ActivityType`. It has no `?pattern=` parameter.

`ActivityGetHandler` resolves activity type from `PreferredType` but has no path to request a specific `ExercisePatternKey` and generate accordingly via `IAiActivityGenerator`.

`ActivityLessonComponent.preferredActivityType()` maps only the four legacy `ActivityType` names — `phrase_match` etc. are not mapped.

### Current Playwright tests that will need updating

`practice-gym.spec.ts` currently asserts:
- Workplace Chat, Email, Gap Fill, Phrase Match → `Coming soon` with no link.

These four tests will be **inverted** when the cards are activated.

---

## Recommended Free-Practice Flow

```
Practice Gym card click
  → /activity?pattern=phrase_match   (new query param)
  → ActivityLessonComponent reads ?pattern= param
  → ActivityService.getNext(undefined, patternKey)
  → GET /api/activity/next?pattern=phrase_match
  → ActivityGetHandler: resolve ActivityType from pattern, call AiActivityGenerator with patternKey
  → Activity created with ExercisePatternKey set
  → ExerciseRendererComponent renders correct interaction mode
  → Student submits → PatternEvaluationRouter routes to correct evaluator
  → PatternEvaluationResultComponent shows result
  → nextActivity() returns to /practice (via returnTo or default nav)
```

**Chosen approach:** add `?pattern=` query parameter to the existing `/api/activity/next` endpoint. This reuses all existing infrastructure — no new endpoint, no new frontend route, no new backend service.

**Why not a new `/api/practice/start` endpoint?**
The existing endpoint already resolves type, creates the activity, runs AI generation, and applies fallback. A new endpoint would duplicate that logic. The pattern param is a minimal extension to an existing query.

**Why not a frontend-only mapping first?**
The four cards (Phrase Match, Gap Fill, Email, Workplace Chat) all require AI-generated content specific to their pattern prompt key. A frontend-only mapping would silently generate the wrong activity type without pattern context.

---

## Card Activation Plan

| Card | Route | Pattern key | Notes |
|---|---|---|---|
| Vocabulary | `/vocabulary` | — | Already functional, no change |
| Listening | `/activity?type=ListeningComprehension` | — | Already functional, no change |
| Writing | `/activity?type=WritingScenario` | — | Already functional, no change |
| Speaking | `/activity?type=SpeakingRolePlay` | — | Already functional. No pronunciation claim. Subtitle: "Workplace role-play" not "pronunciation". |
| **Phrase Match** | `/activity?pattern=phrase_match` | `phrase_match` | NEW — deterministic keyed selection |
| **Gap Fill** | `/activity?pattern=gap_fill_workplace_phrase` | `gap_fill_workplace_phrase` | NEW — exact match |
| **Email** | `/activity?pattern=email_reply` | `email_reply` | NEW — AI structured evaluation |
| **Workplace Chat** | `/activity?pattern=teams_chat_simulation` | `teams_chat_simulation` | NEW — AI structured evaluation |
| Pronunciation | Coming soon | — | No change. Stays disabled. |

---

## Backend Impact

### 1. Extend `GetNextActivityQuery`

Add optional `string? PreferredPatternKey = null` to the record in [ActivityCommands.cs](../../src/LinguaCoach.Application/Activity/ActivityCommands.cs).

### 2. Extend `ActivityGetHandler`

In [ActivityGetHandler.cs](../../src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs), when `query.PreferredPatternKey` is set:
- Load the `ExercisePatternDefinition` by key via `_patternRepo`.
- Use its `ActivityType` as the resolved type.
- Build `ActivityGenerationContext` with `ExercisePatternKey` set.
- Set `ExercisePatternKey` on the created `LearningActivity`.
- Use pattern's `AiGeneratePromptKey` in the generation context.
- Apply the same AI fallback strategy as existing types (no new fallback path needed — WritingScenario and VocabularyPractice fallbacks already exist).

`IAiActivityGenerator.GenerateActivityContentAsync` already accepts `ExercisePatternKey` on `ActivityGenerationContext`:
```csharp
// ActivityGenerationContext already has:
string? ExercisePatternKey = null
```
So no change to the AI generator interface.

### 3. Extend `ActivityController.GetNext`

Add `[FromQuery] string? pattern = null` parameter to [ActivityController.cs](../../src/LinguaCoach.Api/Controllers/ActivityController.cs).

Pass it through: `new GetNextActivityQuery(userId, type, PreferredPatternKey: pattern)`.

### 4. No new endpoints, no migrations

`ExercisePatternKey` already exists on `LearningActivity` (see entity and existing usage in `ExercisePrepareHandler`). No schema change needed.

---

## Frontend Impact

### 1. Extend `ActivityService.getNext`

Add optional `patternKey?: string` param to [activity.service.ts](../../src/LinguaCoach.Web/src/app/core/services/activity.service.ts).

When set, pass `pattern: patternKey` in the HTTP params.

### 2. Extend `ActivityLessonComponent`

In [activity-lesson.component.ts](../../src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts):
- Read `?pattern=` from `queryParamMap` alongside `?type=`.
- If `?pattern=` is present, call `activityService.getNext(undefined, patternKey)`.
- The rest of the flow (renderer, submission, feedback) is unchanged.

### 3. Activate four cards in `PracticeGymComponent`

In [practice-gym.component.html](../../src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.html):

Replace `<div aria-disabled>` with `<a routerLink="/activity" [queryParams]="{pattern:'...'}">` for:
- Phrase Match → `pattern: 'phrase_match'`
- Gap Fill → `pattern: 'gap_fill_workplace_phrase'`
- Email → `pattern: 'email_reply'`
- Workplace Chat → `pattern: 'teams_chat_simulation'`

Remove "Coming soon" text and opacity from these four. Keep Pronunciation as-is.

`PracticeGymComponent` class remains logic-free — only template changes.

---

## Data / Content Strategy

- AI generation is already bounded by `aiGeneratePromptKey` per pattern — no unbounded prompts.
- Each click creates one fresh activity (same as clicking Writing or Listening today). No batching, no noise.
- Progress updates only after a submitted attempt — no change to existing `ActivitySubmitHandler` behaviour.
- No seed data changes. Existing system-fallback activities for `WritingScenario` and `VocabularyPractice` cover the fallback path for all four new cards.
- `Phrase Match` and `Gap Fill` are `VocabularyPractice` type — they will hit the vocab AI generation path. Note: vocab generation currently requires saved vocabulary items. For free practice from Practice Gym, the pattern-keyed path bypasses the `VocabPracticeIntervalAttempts` guard but must gracefully handle the "not enough vocabulary" error. **Risk: see below.**

---

## Safety Rules

- Pronunciation card stays `Coming soon`. No change.
- Speaking card subtitle stays "Workplace role-play" — no pronunciation claims.
- `PatternEvaluationRouter` is not bypassed — all four new cards submit through the existing pipeline.
- No seed data deleted.
- No real user data deleted.
- No broken links — all four new `<a>` tags point to `/activity` with valid query params.

---

## Implementation Phases

### Phase 1 — Audit and plan (this document)

**Done.** Sprint doc written.

### Phase 2 — Backend: `?pattern=` query parameter

Files:
- `LinguaCoach.Application/Activity/ActivityCommands.cs` — add `PreferredPatternKey`
- `LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs` — resolve pattern → type + patternKey
- `LinguaCoach.Api/Controllers/ActivityController.cs` — add `[FromQuery] string? pattern`

Acceptance: `GET /api/activity/next?pattern=phrase_match` returns a `VocabularyPractice` activity with `exercisePatternKey: "phrase_match"` set.

### Phase 3 — Activate deterministic pattern cards (Phrase Match, Gap Fill)

Files:
- `activity.service.ts` — add `patternKey` param
- `activity-lesson.component.ts` — read `?pattern=`
- `practice-gym.component.html` — activate Phrase Match and Gap Fill cards

Acceptance:
- Phrase Match card opens a matching-pairs activity.
- Gap Fill card opens a gap-fill activity.
- Both can be submitted and evaluated.

### Phase 4 — Activate AI/open-ended pattern cards (Email, Workplace Chat)

Files:
- `practice-gym.component.html` — activate Email and Workplace Chat cards

No additional backend changes needed — same `?pattern=` flow handles WritingScenario + AiStructured.

Acceptance:
- Email card opens a `FreeTextEntry` writing activity with `exercisePatternKey: "email_reply"`.
- Workplace Chat card opens a `ChatReply` writing activity with `exercisePatternKey: "teams_chat_simulation"`.
- Both submit and receive structured AI evaluation via `AiStructuredEvaluator`.

### Phase 5 — Return flow polish

Files:
- `activity-lesson.component.ts` — `nextActivity()` already checks `returnTo`. Verify that when no `returnTo` is present (Practice Gym entry), the student stays in free-practice mode rather than auto-loading the next activity of the same type.
- Decision: after completing a free-practice activity, the "Next" button should return to `/practice` rather than auto-generating another. This prevents infinite loops and keeps Practice Gym the intentional re-entry point.
- Add `returnTo=/practice` to the Practice Gym card links so the existing `returnTo` logic handles this cleanly.

Card link format:
```
/activity?pattern=phrase_match&returnTo=/practice
```

### Phase 6 — Progress / memory verification

Verify (no code changes expected):
- `ActivitySubmitHandler` already records `ActivityAttempt` for all activity types — no gap.
- `PatternSkillUpdateService` already updates skill profile when a pattern-keyed activity is submitted.
- `StudentProgressService` / learning memory already triggered by `ActivitySubmitHandler`.

If any gap is found, fix in this phase.

### Phase 7 — Tests, docs, sprint closure

**Playwright tests to update:**

`practice-gym.spec.ts`:
- Change the four "Coming soon" tests (Workplace Chat, Email, Gap Fill, Phrase Match) to assert that cards now have `<a>` links with the correct `pattern=` param.
- Add tests:
  - Phrase Match card links to `/activity?pattern=phrase_match&returnTo=/practice`
  - Gap Fill card links to `/activity?pattern=gap_fill_workplace_phrase&returnTo=/practice`
  - Email card links to `/activity?pattern=email_reply&returnTo=/practice`
  - Workplace Chat card links to `/activity?pattern=teams_chat_simulation&returnTo=/practice`
  - Pronunciation remains Coming soon with no link
  - Speaking card does not contain text "pronunciation" (case-insensitive)

**Backend tests:**
- `ActivityGetHandlerTests`: add case for `PreferredPatternKey = "phrase_match"` → returns VocabularyPractice activity with correct pattern key.
- `ActivityGetHandlerTests`: add case for `PreferredPatternKey = "email_reply"` → returns WritingScenario activity with correct pattern key.
- `ActivityControllerTests` or integration: `GET /api/activity/next?pattern=teams_chat_simulation` → 200 with pattern key in response.

**Docs:**
- Update `docs/handoffs/current-product-state.md`: add four newly activated exercise types to Practice Gym section.
- Update `docs/architecture/README.md` if the `?pattern=` parameter introduces a meaningful new query contract.
- No new architecture doc needed — this extends, not replaces, the existing activity flow.

---

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| `phrase_match` / `gap_fill` generation fails when student has no saved vocab | Medium | `ActivityGetHandler` pattern path bypasses `VocabPracticeIntervalAttempts` but still calls AI via `aiGeneratePromptKey`. AI generation does not require saved vocab — only `VocabPracticeGenerator` does. So this risk is lower than it appears. Verify in Phase 2. |
| AI generation for `email_reply` / `teams_chat_simulation` is slow on first Practice Gym click | Low | Same as clicking Writing today. Rate limiting already in place. Fallback to `WritingScenario` system-fallback if AI fails. |
| `?pattern=` param ignored if AI generator doesn't use it | Medium | Verify `AiActivityGeneratorHandler` reads `ExercisePatternKey` from `ActivityGenerationContext` and uses the correct prompt key. If not, wire it in Phase 2. |
| Existing Playwright tests fail immediately after Phase 3 (Coming-soon assertions inverted) | Certain | Expected — update tests in Phase 7 alongside activation. Keep CI passing at each phase boundary. |
| `returnTo=/practice` nav loop if student keeps clicking Next | Low | `nextActivity()` navigates to `returnTo` once, then student is back at Practice Gym, not auto-generating the next free-practice activity. |

---

## Acceptance Criteria

- [ ] `GET /api/activity/next?pattern=phrase_match` returns an activity with `exercisePatternKey: "phrase_match"`.
- [ ] `GET /api/activity/next?pattern=email_reply` returns an activity with `exercisePatternKey: "email_reply"`.
- [ ] Practice Gym Phrase Match card is a link and opens the correct matching-pairs activity.
- [ ] Practice Gym Gap Fill card is a link and opens the correct gap-fill activity.
- [ ] Practice Gym Email card is a link and opens the correct free-text writing activity.
- [ ] Practice Gym Workplace Chat card is a link and opens the correct chat-reply activity.
- [ ] All four new cards submit through `PatternEvaluationRouter` — not the legacy writing evaluator.
- [ ] Returning from any new card goes back to `/practice`.
- [ ] Speaking card subtitle does not mention pronunciation.
- [ ] Pronunciation card remains Coming soon with no link.
- [ ] `dotnet test` passes (865 + new tests).
- [ ] `npm run build` passes.
- [ ] `npx playwright test --workers=1` passes (165 + new tests, updated assertions).
- [ ] No real user data deleted.
- [ ] No seed data deleted.

---

## Files Expected to Change

### Backend
- `src/LinguaCoach.Application/Activity/ActivityCommands.cs`
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`
- `src/LinguaCoach.Api/Controllers/ActivityController.cs`

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/activity.service.ts`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.html`

### Tests
- `src/LinguaCoach.Web/e2e/practice-gym.spec.ts`
- New or extended backend test cases in `ActivityGetHandlerTests` or integration tests

### Docs
- `docs/handoffs/current-product-state.md`
- `docs/sprints/2026-06-10-practice-gym-activation-sprint.md` (this file)
