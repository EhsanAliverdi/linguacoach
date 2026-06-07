# Live AI Quality Review Report

## Run Summary

- Date:
- Environment:
- Reviewer:
- AI provider:
- Model:
- Primary or fallback:
- Feature keys exercised:
- Correlation IDs:

Do not paste API keys, Authorization headers, raw prompts, or sensitive student data into this report.

## Persona

- Persona:
- Career context selected:
- Career context intended:
- Source language:
- Target language:
- CEFR level:
- Goal:
- Notes about proxy career profile, if used:

## Journey Checklist

- [ ] Admin created student.
- [ ] Student logged in.
- [ ] Student changed temporary password.
- [ ] Student completed onboarding.
- [ ] Initial learning path generated.
- [ ] Dashboard showed path.
- [ ] `/my-path` showed modules.
- [ ] Student opened first activity.
- [ ] First imperfect answer submitted.
- [ ] Feedback rendered.
- [ ] Native-language explanation hidden by default.
- [ ] Persian explanation revealed on click.
- [ ] Improve my answer used.
- [ ] Second attempt submitted.
- [ ] Score comparison appeared.
- [ ] Attempt history showed both attempts.
- [ ] Learning memory updated.
- [ ] Dashboard focus summary updated.
- [ ] `/my-path` learning focus panel updated.
- [ ] Module completed or prepared test data used.
- [ ] Continue learning path generated adaptive modules.
- [ ] Adaptive modules showed reason/focus/difficulty.
- [ ] AI Usage showed calls.
- [ ] Diagnostics showed useful events.

## Initial Learning Path Review

| Order | Title | Rating | Career fit | Level fit | Distinct? | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 |  |  |  |  |  |  |
| 2 |  |  |  |  |  |  |
| 3 |  |  |  |  |  |  |
| 4 |  |  |  |  |  |  |
| 5 |  |  |  |  |  |  |

Verdict:

- [ ] good
- [ ] acceptable
- [ ] needs prompt calibration

Prompt to change if needed: `learning_path_generate`

## Activity Generation Review

Activity reviewed:

- Title:
- Module:
- Task type:
- Audience:
- Tone:
- Expected length:
- Difficulty:

Quality checks:

- [ ] Matches current module.
- [ ] Clear context.
- [ ] Clear audience.
- [ ] Clear tone.
- [ ] Clear expected length.
- [ ] Level-appropriate.
- [ ] Career-realistic.
- [ ] Not repetitive.
- [ ] Instructions are concise.

Issues:

Prompt to change if needed: `activity_generate_writing`

## Feedback Review

Submitted answer fixture:

- Fixture name:
- Intended mistakes:

Checks:

- [ ] Identified direct tone.
- [ ] Identified missing articles.
- [ ] Identified long sentence or clarity issue.
- [ ] Identified tense issue where present.
- [ ] Identified unclear request.
- [ ] Identified informal phrase where present.
- [ ] Changes list used before/after guidance.
- [ ] Reasons explained why changes matter.
- [ ] Mini lesson was useful.
- [ ] Next step was practical.
- [ ] Improved version was framed as a suggestion.
- [ ] Main feedback stayed in English.
- [ ] Persian explanation was concise.

Issues:

Prompt to change if needed: `activity_evaluate_writing`

## Memory Review

Memory observed:

- Journey summary:
- Strengths:
- Weak areas:
- Recurring mistakes:
- Covered scenarios:
- Next recommended focus:
- Skill profile:

Checks:

- [ ] Strengths are realistic.
- [ ] Weak areas match feedback.
- [ ] Recurring mistakes are not noisy.
- [ ] Next focus makes sense.
- [ ] Memory stays compact.
- [ ] Memory does not store full submitted text.
- [ ] Memory does not drift after one attempt.

Issues:

Prompt/code to change if needed: `student_memory_update`

## Adaptive Module Review

| Order | Title | Focus | Difficulty | Reason quality | Duplicate? | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 |  |  |  |  |  |  |
| 2 |  |  |  |  |  |  |
| 3 |  |  |  |  |  |  |
| 4 |  |  |  |  |  |  |
| 5 |  |  |  |  |  |  |

Checks:

- [ ] Uses student weaknesses.
- [ ] Avoids old scenario fingerprints.
- [ ] Explains recommendation.
- [ ] Progresses difficulty gradually.
- [ ] Generates next modules, not a generic full path.
- [ ] Generates 3-5 modules.
- [ ] Career-relevant.

Issues:

Prompt/code to change if needed: `learning_path_generate_adaptive`

## Provider And Observability

- Provider configured:
- Fallback configured:
- Qwen appears in AI Config:
- AI Usage rows visible:
- Diagnostics events visible:
- Friendly unavailable message tested:
- Secrets exposed anywhere:

Issues:

## Prompt Changes Made

| Prompt | Issue evidence | Change made | DTO/JSON contract preserved? | Tests updated? |
| --- | --- | --- | --- | --- |
|  |  |  |  |  |

## Deferred Issues

- 

## Final Quality Verdict

- [ ] Ready for another real tester.
- [ ] Ready with minor known issues.
- [ ] Not ready; prompt calibration required.

Reason:
