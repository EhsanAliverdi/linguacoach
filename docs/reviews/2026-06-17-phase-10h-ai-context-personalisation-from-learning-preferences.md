---
status: current
lastUpdated: 2026-06-17 09:43
owner: engineering
supersedes:
supersededBy:
---

# Phase 10H - AI Context Personalisation from Learning Preferences

## Summary

Phase 10H wires Phase 10G learning preferences into AI content generation.
It does not add onboarding v2, curriculum routing, pre-generation pools, or new
exercise formats.

## Fields wired into AI context

The compact learner preference section can include:

- preferred name
- learning language
- support language
- translation help preference
- selected learning goals
- custom learning goal
- selected focus areas
- custom focus area
- difficulty preference
- current CEFR level, labelled system-estimated

Missing fields are omitted. No fake defaults are emitted.

## Fields intentionally excluded

The AI generation context excludes:

- prompt-editing fields
- admin-only profile names
- roles
- quotas
- lifecycle state
- account details
- raw submitted text
- student-editable CEFR override

CEFR is included only from `StudentProfile.CefrLevel` as read-only,
system-estimated context.

## LearningGoalContext priority

`LearningGoalContext` now uses this priority:

1. `CustomLearningGoal`
2. `LearningGoals`
3. `LearningGoalDescription`
4. legacy single `LearningGoal`
5. `CareerContext`

If none are present, it stays null. It never defaults to workplace.

The value is bounded to 200 characters before ledger storage or selector use.

## Support language behaviour

Support language is presented as optional support only.
Prompt insertion adds rules telling AI to use it for optional help or explanations.
It must not translate the whole activity by default.

## Translation help behaviour

`TranslationHelpPreference` is rendered as:

- `Never` -> `never`
- `WhenDifficult` -> `when difficult`
- `AlwaysAvailable` -> `always available`

The prompt rules keep support language optional.
Feature-specific UI can still decide what is shown.

## Token budget considerations

The learner preference section is capped at 500 characters.
List values are deduplicated and capped to five items.
Individual list items are capped at 80 characters.
Custom goal and focus text are capped at 160 characters.

Prompt insertion runs before existing `MaxInputTokens` validation.
If the added context pushes a prompt over budget, `TokenBudgetExceededException`
is thrown before the provider call.

No prompt budget was increased in this phase.

## Workplace assumptions avoided

The preference section does not default to workplace.
Prompt insertion explicitly tells AI not to assume workplace context unless the
goal or topic asks for it.

Practice Gym's generic background topic fallback changed from workplace English
practice to English class practice.

Workplace remains valid when selected through goals, legacy context, career
context, or a workplace-specific exercise type.

## Known limitations

Existing seed prompt bodies still contain many workplace examples because the
product originally focused there.
This phase adds context and behaviour rules without rewriting every prompt.

Lesson batch planning receives preferences through the compact summary JSON.
It still uses the older direct prompt render path.

## Recommendation for next phase

Run a prompt quality pass across generation prompts.
Replace workplace-only wording in generic exercise prompts with goal-aware
language while keeping workplace examples available when selected.
