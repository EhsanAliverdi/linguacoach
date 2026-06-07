# End-to-End Learning Journey Validation Report

## Scope

This report covers the End-to-End Product Validation & Learning Quality Sprint for SpeakPath's current writing-learning journey.

The validation focuses on the existing workplace writing flow only. Speaking, listening, vocabulary, pronunciation, and reading activities remain out of scope.

## Personas

### Persona 1: Project Planner Variant

- Intended career: Project Planner
- Available test career used when seed data is limited: Document Controller
- Level: B1
- Source language: Persian
- Target language: English
- Expected weaknesses: direct tone, long sentences, weak summarising, tense/article mistakes

### Persona 2: Customer Support Officer Variant

- Intended career: Customer Support Officer
- Available test career used when seed data is limited: Document Controller / Workplace English
- Level: B1/B1+
- Source language: Persian
- Target language: English
- Expected weaknesses: polite complaint responses, clear explanations, softening negative messages

## Journey Steps Tested

Automated Playwright coverage now validates:

- Admin login and student creation.
- Student first login and mandatory password change.
- Four-step onboarding.
- Dashboard path visibility.
- Activity lesson state.
- First writing attempt submission.
- Structured feedback rendering.
- Suggested changes / before-after guidance.
- Native-language explanation hidden by default and revealable.
- Improve my answer retry flow.
- Second attempt with score comparison.
- Attempt history page showing both attempts.
- Learning memory panel after attempts.
- Module ready-to-complete state.
- Module completion action.
- Continue my learning path action.
- New adaptive module rendering with reason, focus, and difficulty.
- Dashboard compact learning focus summary.
- No raw JSON visible in the validated student surfaces.

## Screenshots

Existing screenshot workflow remains in `src/LinguaCoach.Web/e2e/admin-screenshots.spec.ts`.

Relevant screenshot artifacts:

- `src/LinguaCoach.Web/e2e/screenshots/student-01-dashboard.png`
- `src/LinguaCoach.Web/e2e/screenshots/student-03-my-path.png`
- `src/LinguaCoach.Web/e2e/screenshots/student-03-my-path-empty-memory.png`
- `src/LinguaCoach.Web/e2e/screenshots/student-03-my-path-generated.png`
- `src/LinguaCoach.Web/e2e/screenshots/mobile-profile.png`

## Learning Quality Review

Activity quality:

- Deterministic test activity uses a realistic pending document approval follow-up.
- Role, audience, workplace blocker, and tone expectation are clear.
- Task is appropriate for B1 workplace writing.

Feedback quality:

- Feedback includes score, coach summary, what went well, targeted changes, mini lesson, next step, rewrite challenge, and suggested improved version.
- Suggested changes use before/after labels and reasons.
- The improved version is labelled as a suggestion.
- Persian explanation is hidden by default.

Memory quality:

- Memory summary reflects the attempted follow-up email.
- Weak areas connect to direct tone and long sentences.
- Next focus uses softening requests and concise progress updates.
- No full submitted text is displayed in memory.

Adaptive module quality:

- New modules include reason, focusSkill, difficulty, and stored fingerprint.
- Duplicate scenarioType + audience + communicationMode combinations are rejected by backend guardrail tests.
- Friendly metadata renders in `/my-path`; fingerprint JSON is not shown.

## Issues Found

- The repo does not currently seed Project Planner or Customer Support Officer career profiles. Manual validation can use Document Controller as a proxy until a reference-data sprint expands career seed data.
- There is no admin student detail page, so admin learning memory visibility remains deferred.

## Issues Fixed

- Added full learning-loop Playwright coverage beyond the first feedback screen.
- Added adaptive module metadata/fingerprint guardrail tests.
- Added duplicate fingerprint rejection test.
- Added sprint and QA documentation.

## Deferred

- Seed additional pilot career profiles.
- Add admin student detail page and learning memory section.
- Add richer side-by-side attempt comparison.
- Add numeric skill scores after validating current skill profile behavior.
- Run live AI persona review in staging/production and record prompt-quality findings before changing prompts.

## Final Confidence

Automated confidence is high once the final `dotnet test`, `npm run build`, and `npx playwright test` pass.

Product-quality confidence is medium-high for the current writing loop, with the remaining risk concentrated in live AI prompt quality and limited seed career variety.
