---
status: current
lastUpdated: 2026-06-12 00:00
owner: product
supersedes:
supersededBy:
---

# Sprint: Exercise UX / Admin Polish

Status: **In progress**

Related brainstorm: product owner 10-track brainstorm (2026-06-12), see "Recommended One-Sprint Scope" discussion in chat history (not persisted verbatim — superseded by this doc).

---

## Product goal

The Pattern Evaluation Engine sprint (2026-06-10) already delivered structured attempt storage, retry integrity, and deterministic/AI evaluation routing. This sprint focuses on the **remaining real gaps**:

1. Confirm attempt/retry integrity is solid (verification only — expected no-op).
2. Improve Workplace Chat exercise: surface existing `LearningGoal`/`ToneGuidance` fields in the UI and strengthen feedback rubric around goal-reaching.
3. Email exercise: add structured `Subject` + `Body` fields (currently single textarea; `EmailReplyContent` has no subject/body split today).
4. Standardize every active exercise renderer on a shared **Lesson → Practice → Evaluate** structure: a consistent "Lesson" intro section (teaching goal / scenario / instructions, using existing content fields like `learningGoal`, `scenario`, `teachingPurpose`), the existing practice interaction, and the existing pattern-evaluation result as "Evaluate". Applies to all 6 currently active renderers: `ChatReplyComponent`, `EmailReplyComponent` (new), `FreeTextEntryComponent`, `GapFillComponent`, `MatchingPairsComponent`, `AudioAndFreeTextComponent`, `AudioAndGapFillComponent`.
5. Admin nav: move "AI Usage" from "Analytics" group to "AI System" group.
6. Scoped design-token consistency pass on touched components only.
7. Record adaptive onboarding / multi-course / estimated-known-words as backlog architecture notes only — no implementation this sprint.

## Architecture decisions

- No changes to `ActivityAttempt` entity, `IPatternEvaluationRouter`, or evaluators (`ExactMatchEvaluator`, `KeyedSelectionEvaluator`, `AiStructuredEvaluator`) routing logic — these are confirmed working per Pattern Evaluation Engine sprint.
- Email gets a **new renderer component** (`EmailReplyComponent`) rather than reuse of `FreeTextEntryComponent`, since `FreeTextEntryComponent` remains the renderer for `lesson_reflection`/`spoken_response_from_prompt` and other single-textarea patterns.
- `EmailReplyContent` gets `Subject`/`Body`-related fields added (additive, nullable) — content DTO is JSON-serialized into `LearningActivity.AiGeneratedContentJson`, so additive fields are backward compatible with existing seeded/cached content.
- Chat multi-turn is **not** implemented this sprint unless trivial given existing `AiActivityGeneratorHandler` plumbing — default is single-turn-with-explicit-goal + rubric feedback (lower risk).

## In scope

- Phase 1: Verification of attempt/retry integrity (gap fill, phrase match, AI feedback non-empty).
- Phase 2: Workplace Chat — surface `learningGoal`/`toneGuidance` in `ChatReplyComponent`, update `email_reply`/`teams_chat_simulation` AI evaluation rubric for goal-reaching.
- Phase 3: Email exercise — `EmailReplyContent` Subject/Body fields, new `EmailReplyComponent` renderer, evaluator rubric update.
- Phase 4: Shared Lesson → Practice → Evaluate structure applied to all active exercise renderers (chat, email, gap-fill, phrase-match, free-text-entry, audio+free-text, audio+gap-fill).
- Phase 5: Admin nav — AI Usage moved to AI System group.
- Phase 6: Scoped design-token alignment for new/changed components.
- Phase 7: Docs close-out.

## Out of scope

- Re-implementing attempt storage / retry / evaluator routing (already complete).
- Adaptive onboarding engine, admin-configurable onboarding questions — backlog note only.
- Multi-course enrolment model — backlog note only.
- Estimated known words feature — backlog note only.
- App-wide design system rewrite.
- Multi-turn AI chat streaming, voice/pronunciation.
- Admin Student Edit slide-over (already implemented as right-side slide-over).

## API changes

- None expected for Phase 1, 2, 5.
- Phase 3: `EmailReplyContent` gains `Subject`/`Body`-prompt-related fields (additive). `SubmittedAnswerJson` shape for `email_reply` becomes `{ "subject": "...", "body": "..." }` instead of `{ "text": "..." }`. `AiStructuredEvaluator` rubric for `email_reply` updated to read subject/body.

## DB changes

- None expected. All content changes are JSON (`AiGeneratedContentJson`, `SubmittedAnswerJson`), no new columns/migrations.

## Frontend changes

- `ChatReplyComponent`/`.html` — render `learningGoal`/`toneGuidance` if present; adopt shared lesson-intro structure.
- New `EmailReplyComponent` (renderer for `email_reply`), registered in interaction-mode dispatch alongside `FreeTextEntryComponent`; adopts shared lesson-intro structure.
- New shared lesson-intro presentational component/template (e.g. `ExerciseLessonIntroComponent` or shared partial) used by all 6 active renderers to show teaching goal/scenario/instructions before the practice interaction.
- `FreeTextEntryComponent`, `GapFillComponent`, `MatchingPairsComponent`, `AudioAndFreeTextComponent`, `AudioAndGapFillComponent` — adopt shared lesson-intro structure.
- `admin-app-layout.component.html` — nav group change.

## Test plan

```
dotnet test
npm run build
npx playwright test
```

New/updated specs: chat reply goal display, email subject/body submission + feedback, admin nav AI Usage location.

## Phase 1 findings

**Real bug found and fixed.** Gap-fill (`gap_fill_workplace_phrase`, `MarkingMode.ExactMatch`) submissions were silently scored 0 regardless of correctness:

- `ExactMatchEvaluator.ParseSubmittedAnswers` deserializes `SubmittedAnswerJson` as `GapFillSubmittedAnswer { Dictionary<string,string?> Answers }` (object/dict shape) — confirmed by `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs`.
- The frontend (`activity-lesson.component.ts`, `onRendererSubmit`) was sending `JSON.stringify({ kind: 'gapFill', answers: [{gapId, value}, ...] })` for the `gapFill` payload kind — an array, not a dict, plus an extraneous `kind` field.
- `JsonSerializer.Deserialize<GapFillSubmittedAnswer>` on that shape throws `JsonException` (caught), `ParseSubmittedAnswers` returns an empty dict, and every gap is marked incorrect.

**Fix:** `activity-lesson.component.ts` now converts the `gapFill` payload to `{ "answers": { "gap_1": "value", ... } }` before submission, matching `matchingPairs` (`{ "pairs": {...} }`) which was already correct and matches `PhraseMatchSubmittedAnswer`.

**Phrase match (`phrase_match`, `MarkingMode.KeyedSelection`):** verified correct — `{ pairs: { phraseKey: meaningKey } }` matches `PhraseMatchSubmittedAnswer.Pairs`.

**AI feedback (email_reply, teams_chat_simulation):** `ActivitySubmitHandler.HandlePatternEvaluationAsync` passes `command.SubmittedContent` directly as `SubmittedAnswerJson` to `AiStructuredEvaluator` — non-empty whenever the renderer's `submit()` guards against empty text (confirmed in `ChatReplyComponent`/`FreeTextEntryComponent`). No issue found.

**Attempt/retry storage:** every submission creates a new append-only `ActivityAttempt` row via `_db.ActivityAttempts.Add(attempt)` + `SaveChangesAsync` — confirmed for both legacy and pattern-evaluation paths. No `AttemptNumber` column exists; attempt count is computed client-side (`attemptCount` signal incremented per response) and at query time for history. No issue found — no new column needed.

## Tasks

- [x] Phase 0: Sprint doc + current-sprint.md update + backlog notes (this doc)
- [x] Phase 1: Verify attempt/retry integrity — found and fixed gap-fill submission shape bug
- [ ] Phase 2: Workplace Chat — goal/tone framing + rubric update
- [ ] Phase 3: Email — Subject/Body structured renderer + rubric update
- [ ] Phase 4: Shared Lesson → Practice → Evaluate structure across all 6 active renderers
- [ ] Phase 5: Admin nav — AI Usage under AI System
- [ ] Phase 6: Design-token consistency pass (scoped)
- [ ] Phase 7: Docs close-out

## Risks / unresolved questions

- Multi-turn chat scope decision deferred to Phase 2 implementation (default: single-turn + explicit goal).
- Phase 4 expanded per product-owner decision (2026-06-12): apply Lesson → Practice → Evaluate to all 6 currently active renderers, not just chat/email/gap-fill/phrase-match. Patterns not yet implemented (40+ in the library) are NOT retrofitted — only active renderers.

## Final verdict

Pending — sprint in progress.

## Next recommended action

Proceed to Phase 1 verification.
</content>
