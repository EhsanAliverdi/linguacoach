# Lesson/Practice Structure Redesign — Phase 3 Implementation Plan

Date: 2026-06-12
Related sprint: `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md` (Phase 3)
Status: Planning only — no code changes in this pass

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`
  (submit handlers, `attemptCount`, `improveAnswer`/`tryAgain`/`nextActivity`)
- `src/LinguaCoach.Web/src/app/features/activity/pattern-evaluation-result/pattern-evaluation-result.component.html`
- `src/LinguaCoach.Web/src/app/features/activity/renderers/exercise-lesson-intro/exercise-lesson-intro.component.ts`
  and `.html`
- `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md` (Phase 3 scope note)
- `docs/sprints/current-sprint.md`

## Current architecture summary

### "What we learn" (today)

`ExerciseLessonIntroComponent` renders a single line: `Goal: {{ goal }}`, fed via
`[goal]="content.learningGoal"` (or pattern-specific equivalent). Used by GapFill,
MatchingPairs, AudioAndGapFill, AudioAndFreeText. WritingScenario, ChatReply, EmailReply,
and FreeTextEntry render their own equivalent "Goal" blocks inline in
`activity-lesson.component.html` (e.g. line 357-362, `activity()!.learningGoal`).

This is a single string, not structured grammar/vocab/phrase breakdown. Phase 3's
"What we learn" framing (grammar/vocab/phrases) does not exist yet — it would need new
DTO fields.

### "Practice"

Existing renderers (`app-exercise-renderer` dispatch, plus per-pattern legacy branches in
`activity-lesson.component.html`). No structural change needed here — Phase 3 wraps
around this, doesn't replace it.

### "Feedback"

`pattern-evaluation-result.component.html` shows: score ring, coach summary
(`result.coachSummary`, AI-generated), per-pattern item results (matching pairs / gap
fill / chat-email corrections / listen-and-answer / spoken response). This is already
grounded in the AI evaluation of *this attempt* — it is not generic/random.

What's missing per the Phase 3 brief ("based on lesson AND Student characteristic"): the
feedback view has no visibility into `StudentSkillProfile.ScorePercent` (Phase 2,
done) or `StudentLearningMemory`. The coach summary is produced by the evaluation prompt
per-attempt; it does not currently receive the student's skill-profile context as input.

### "Redo / Next"

`activity-lesson.component.ts`:
- `tryAgain()` (line 537) — resets local form state, returns to `writing`/`ready`. Pure
  client-side redo of the *same* activity.
- `improveAnswer()` (line 533) — returns to `writing` state, keeping the draft, so the
  student revises in place.
- `nextActivity()` (line 551) — if `returnTo` query param present, navigates back to the
  lesson/practice list page; otherwise loads a fresh activity via `loadActivity()`.
- `attemptCount` signal tracks attempt number, shown in the writing-state UI
  ("Attempt N — apply the suggestions and rewrite").

This redo/next loop already exists and is functional for both Lesson and Practice
contexts (via `returnTo`). Phase 3 doesn't need a new state machine — it needs the
feedback step to *use* the existing `ScorePercent` data and the "What we learn" step to
show richer pre-practice framing.

## Findings by priority

### P1 — Feed StudentSkillProfile context into per-attempt feedback

The Phase 3 brief's core ask ("Feedback... grounded in lesson AND Student
characteristic") is the only part requiring new wiring. Two options:

- **Option A (prompt-level):** `PatternEvaluationRouter` / AI evaluation prompts already
  receive lesson context. Add relevant `StudentSkillProfile.ScorePercent` entries (for
  skills targeted by this activity's pattern) to the AI evaluation prompt context, so
  `coachSummary` can reference "this is an area you've been improving in" etc. Low
  surface area — extends an existing prompt-context builder
  (`StudentMemoryService.BuildAdaptiveContextJsonAsync` already has the pattern for this).
- **Option B (UI-level):** Surface the relevant skill's current `ScorePercent` directly
  in `pattern-evaluation-result.component.html` as a small "Your progress in this skill"
  indicator, independent of the AI prompt. Simpler, no AI prompt change, but less
  "grounded feedback" and more "extra stat".

Recommendation: Option A for the actual feedback text (matches "Feedback...based on...
Student characteristic" literally), Option B as a cheap additive UI element if wanted.
Both are additive — no breaking changes to `PatternEvaluationResult` contract beyond a
new optional context input to the prompt.

### P2 — "What we learn" structured framing (grammar/vocab/phrases)

Today's single `learningGoal` string is reused across renderers via
`ExerciseLessonIntroComponent`. Expanding to grammar/vocab/phrases breakdown would
require:
- New optional fields on the relevant content DTOs (e.g. `targetGrammarPoint`,
  `targetVocabulary: string[]`, already partially covered by `targetPhrases` on
  WritingScenario).
- Extending `ExerciseLessonIntroComponent` template to render these as a small
  structured list instead of one line, while keeping `[goal]` as a fallback for
  renderers that don't populate the new fields.

This is additive but touches AI-generation prompts (content generators must populate
the new fields) across multiple activity types — larger surface than P1. Should be
scoped as its own follow-up once P1 is shipped and validated, not bundled.

### P3 — Redo/Next loop

No code change required. `tryAgain`/`improveAnswer`/`nextActivity` + `attemptCount`
already implement "Redo -> Next" for both Lesson and Practice (`returnTo` handles
Practice's "return to gym" case). Confirm this satisfies the brief; if the product owner
wants an explicit "Redo" vs "Next" choice screen (currently both buttons are always
shown together in the feedback state, lines 860-873 of
`activity-lesson.component.html`), that's a copy/layout tweak, not a flow change.

## Decisions made

- Phase 3 will be split: P1 (feedback grounded in StudentSkillProfile) is the next
  implementable unit, scoped tightly to avoid AI-prompt sprawl. P2 (structured "what we
  learn") is deferred to its own phase/sprint given its cross-cutting prompt impact. P3
  requires no implementation, only product confirmation that the existing redo/next UX
  meets the brief.
- AskUserQuestion: none asked in this pass (planning was self-contained from existing
  docs/code); the prior AskUserQuestion ("Planning pass now") is what authorized this
  document.

## Implementation tasks produced

1. (P1) Extend `StudentMemoryService`'s adaptive-context builder (or
   `PatternEvaluationRouter` prompt input) to include the relevant
   `StudentSkillProfile.ScorePercent` for skills tied to the current `ExercisePattern`,
   and update the evaluation prompt(s) to reference it when producing `coachSummary`.
2. (P1, optional/cheap) Add a small "your progress in this skill" indicator to
   `pattern-evaluation-result.component.html` using `ScorePercent` directly (no prompt
   change).
3. (P2, deferred) Scope a follow-up phase for structured "What we learn" (grammar/vocab/
   phrases) — new optional DTO fields + `ExerciseLessonIntroComponent` template
   extension + AI content-generator prompt updates across activity types.
4. (P3) Product confirmation only — no code task unless product wants a UX change to the
   existing redo/next buttons.

## Risks / unresolved questions

- P1 requires identifying, per `ExercisePatternKey`, which skill keys are "relevant" to
  feed into the prompt context — this mapping may already exist via
  `PatternSkillUpdateService`'s pattern-to-skill lookup; needs confirmation before P1
  implementation starts.
- P2's scope (which activity types get structured "what we learn") needs product
  decision — doing it for all types in one pass is a large prompt-engineering effort.
- No new entities/migrations needed for any of P1-P3.

## Final verdict

Phase 3 is mostly already implemented (Practice rendering, redo/next loop, per-attempt
AI feedback). The one genuine gap is grounding feedback in `StudentSkillProfile`
(P1) — small, additive, and now unblocked by Phase 2. P2 (structured "what we learn") is
a separate, larger phase and should not be bundled with P1.

## Next recommended action

Implement P1 (StudentSkillProfile-grounded feedback) as the next concrete Phase 3
deliverable. Defer P2 to a future phase/sprint with its own scoping pass.
