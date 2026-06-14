---
status: planned
lastUpdated: 2026-06-14 00:00
owner: engineering
supersedes:
supersededBy:
---

# Activity Teach Page — micro-lesson content (Option B)

Date: 2026-06-14
Related: docs/reviews/2026-06-14-activity-teach-page-content-leak-review.md

## Context

The teach/practice content-leak fix (Option A, shipped 2026-06-14) moved
exercise-setup fields (scenario, target phrases/vocab, examples, common
mistakes) from Page 1 (Learn) to Page 2 (Practice). This stopped Page 1 from
showing exercise content, but Page 1 now only has thin fields
(`learningGoal`, `skillFocus`, `instructions`, `teachingNote`,
`instructionInSourceLanguage`, `toneGuidance`) — there is still no real
"teach a grammar point / communication skill" micro-lesson.

## Goal

Per the user's original spec: Page 1 should actually **teach** something —
e.g. "how conditional sentences work" or "how to politely ask for something"
— tied to the grammar/communication focus the Page 2 exercise is built
around.

## Scope

1. Extend the unified `{teach, practice}` AI generation content contract
   (proposed in `docs/reviews/2026-06-13-activity-3-page-restructure-eng-plan.md`
   Phase 2) so `teach` includes a structured micro-lesson:
   - `microLessonTitle` (e.g. "Conditional sentences: first conditional")
   - `microLessonExplanation` (2-4 sentence plain-language explanation)
   - `microLessonExamples` (1-3 short example sentences illustrating the
     point, distinct from the exercise's own `exampleText`)
2. Update AI prompt templates (`ai_prompts` table) for the 10 pattern keys to
   produce these fields, derived from each `ExercisePatternDefinition`'s
   `teachingPurpose` + the specific grammar/skill focus chosen for that
   activity instance.
3. `PatternBackedPresenter.teachContent()` maps the new fields into
   `PatternLearningViewModel`.
4. `activity-teach-page.component.html` (`patternLearning` case) renders the
   micro-lesson section above the existing `learningGoal`/`instructions`
   content.
5. AI prompt calibration pass against live providers for all 10 patterns
   (per `live-ai-quality-review-prompt-calibration-sprint.md` precedent) —
   verify explanations are accurate, concise, and CEFR-appropriate.
6. Update `docs/architecture/exercise-pattern-library.md` and
   `learning-activity-engine.md` content-contract sections.

## Risks

- Largest risk: AI-generated grammar/communication explanations being
  inaccurate or too advanced/simple for the student's CEFR level. Needs
  calibration per pattern, not a one-shot prompt change.
- `SystemFallback` seed rows (used when AI generation fails) need
  hand-written micro-lesson content per pattern — can't rely on AI fallback
  for fallback rows.
- Backward compatibility: old `contentJson` rows without micro-lesson fields
  must render Page 1 gracefully (no empty "Learn" section) until
  regenerated — `teachContent()` must treat these fields as optional.

## Status

Planned, not started. Independent of and does not block the Option A fix
already shipped.
