---
status: historical
---
# Live AI Quality Review & Prompt Calibration Sprint

## Current State

The end-to-end writing-learning journey is covered by deterministic tests and has been pushed in commit `a1e760a`.

Automated validation proves the flow works:

- `dotnet test LinguaCoach.slnx` passed with 208 unit and 146 integration tests.
- `npm run build` passed with existing CSS budget/selector warnings.
- `npx playwright test` passed with 23/23 tests.

This sprint validates the real generated learning experience with a live configured AI provider. Prompt changes must be evidence-based and limited to the prompt that caused the observed issue.

## Product Goal

Prove that live AI content is useful enough for another real tester:

- Initial learning paths are career-relevant and level-appropriate.
- Writing activities feel realistic and are not repetitive.
- Feedback identifies the most important issues without overwhelming the learner.
- Memory summaries are compact and believable.
- Adaptive modules use memory and avoid repeated scenarios.
- Provider/fallback/usage tracking still works without exposing secrets.

## Test Personas

### Persona 1: Project Planner

- Career context: Project Planner
- Level: B1
- Source language: Persian
- Target language: English
- Goal: Workplace writing
- Expected weaknesses: direct tone, long sentences, weak summarising, tense/article mistakes, difficulty softening requests

Manual setup:

1. Admin creates a new student with a temporary password.
2. Student logs in and changes password.
3. Student selects Persian to English.
4. Student selects Workplace English.
5. If Project Planner exists as a career profile, select it.
6. If Project Planner is not seeded yet, select Document Controller and record this as a proxy run in the report.
7. Student selects Writing as the first skill focus.

### Persona 2: Customer Support Officer

- Career context: Customer Support Officer
- Level: B1/B1+
- Source language: Persian
- Target language: English
- Goal: Professional customer communication
- Expected weaknesses: polite complaint responses, clear explanations, softening negative messages, empathy in written replies

Manual setup:

1. Admin creates a new student with a temporary password.
2. Student logs in and changes password.
3. Student selects Persian to English.
4. Student selects Workplace English.
5. If Customer Support Officer exists as a career profile, select it.
6. If Customer Support Officer is not seeded yet, select Document Controller and record this as a proxy run in the report.
7. Student selects Writing as the first skill focus.

Seeded persona rows are deferred. The admin-created student flow is part of the product validation, and adding demo users would bypass temporary password enforcement.

## AI Provider Used

Record in `docs/testing/live-ai-quality-review-report.md` for each run:

- Provider name
- Model
- Primary or fallback
- Feature keys exercised
- Correlation IDs
- Whether usage rows appeared in AI Usage

Do not record API keys, secrets, Authorization headers, or full raw prompts.

## Live Journey

Run at least one full journey with a real configured provider:

1. Admin creates student.
2. Student logs in.
3. Student completes onboarding.
4. Initial learning path is generated.
5. Student opens first activity.
6. Student submits a deliberately imperfect answer from `docs/testing/live-ai-quality-fixtures.md`.
7. Review AI feedback.
8. Student improves the answer.
9. Submit second attempt.
10. Review score improvement and diff.
11. Open attempt history.
12. Check memory update.
13. Check dashboard focus.
14. Check `/my-path` learning focus panel.
15. Complete module when ready or use prepared test data.
16. Continue learning path.
17. Review generated adaptive modules.

## Initial Learning Path Review

Prompt under review: `learning_path_generate`.

Assess whether generated modules:

- Match career context.
- Match CEFR level.
- Are workplace realistic.
- Are ordered progressively.
- Are distinct from each other.
- Avoid generic titles.
- Include clear focus.
- Do not over-focus on email only.
- Include useful workplace communication variety.

Rating per module:

- good
- acceptable
- weak
- duplicate
- wrong level
- too generic

Only update `learning_path_generate` if the live path shows concrete issues.

## Activity Generation Review

Prompt under review: `activity_generate_writing`.

Assess whether generated activities:

- Match the current module.
- Have clear context.
- Define audience.
- Define tone.
- Define expected length.
- Match the student's level.
- Feel realistic for the career context.
- Avoid repeating the same task type too often.
- Avoid overly long or confusing instructions.

Only update `activity_generate_writing` if live activities show concrete issues.

## Feedback Review

Prompt under review: `activity_evaluate_writing`.

Use imperfect answers that include:

- Direct Persian-style tone.
- Missing articles.
- Long sentence.
- Tense issue.
- Unclear request.
- Too informal phrase.

Assess whether feedback:

- Identifies the right issues.
- Does not overwhelm the student.
- Gives useful before/after changes.
- Explains why each change matters.
- Keeps the improved version as a suggestion.
- Gives a useful mini lesson.
- Gives a practical next step.
- Keeps main feedback in English.
- Keeps Persian explanation concise and hidden by default in UI.

Only update `activity_evaluate_writing` if live feedback shows concrete issues.

## Memory Update Review

Prompt under review: `student_memory_update`.

Assess whether learning memory:

- Creates realistic strengths.
- Creates realistic weak areas.
- Avoids noisy or exaggerated weaknesses.
- Deduplicates recurring mistakes.
- Stays compact.
- Recommends sensible next focus.
- Does not drift after only one attempt.

Existing code guardrails already cap memory arrays and deduplicate case-insensitively through domain/value-object logic. Add more code guardrails only if live evidence shows vague or bloated memory is still getting through.

## Adaptive Module Review

Prompt under review: `learning_path_generate_adaptive`.

Assess whether adaptive modules:

- Use the student's weaknesses.
- Avoid old scenario fingerprints.
- Explain why they are recommended.
- Progress difficulty gradually.
- Avoid generating another generic full path.
- Generate only next 3-5 modules.
- Reuse weak skills through new situations.
- Stay career-relevant.

Only update `learning_path_generate_adaptive` or duplicate guardrails if live adaptive generation shows concrete issues.

## Provider And Fallback Check

Lightly verify:

- Configured provider works.
- Qwen option appears in AI Config.
- Fallback provider fields appear.
- AI Usage records live calls.
- Diagnostics shows useful correlation events.
- Provider failures surface friendly AI unavailable messages.

Do not over-test infrastructure in this sprint; existing automated tests already cover fallback and Qwen support.

## Prompt Calibration Rules

For every prompt change:

- Document the observed issue.
- Document the prompt changed.
- Preserve valid JSON output.
- Preserve DTO contracts.
- Update fake AI/test fixtures only if contracts change.
- Run full tests.

Do not rewrite prompts speculatively.

## In Scope

- Live manual quality review documentation.
- Sample answer fixtures for manual review.
- Focused prompt changes if supported by live evidence.
- Backlog updates from findings.
- Final automated verification.

## Out Of Scope

- New activity types.
- UI redesign.
- Admin student detail page.
- Seeded demo users.
- Live-AI-dependent automated tests.
- Logging secrets or raw prompts.

## Test Plan

Before completing this sprint:

```bash
dotnet test LinguaCoach.slnx
npm run build
npx playwright test
```

If prompt content changes, also verify prompt seed/versioning by inspecting the active `AiPrompt` rows in a safe environment.

## Known Issues Found

None from live AI yet. This sprint is prepared for manual execution; live provider access is required to populate findings.

## Prompt Changes Made

None. No live AI evidence has been collected in this environment.

## Deferred Improvements

- Add Project Planner and Customer Support Officer career profile seed rows in a future reference-data sprint.
- Run the live review in staging/production with configured provider credentials.
- Add prompt-specific fixes only after documented examples show a quality problem.

## Final Quality Verdict

Pending live AI run.

Current readiness: automated flow confidence is high; live content quality confidence is not yet established.
