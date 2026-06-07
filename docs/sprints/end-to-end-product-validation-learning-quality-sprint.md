# End-to-End Product Validation & Learning Quality Sprint

## Current State

The Student Learning Memory UI sprint is complete. SpeakPath now exposes learning memory on the dashboard and `/my-path`, supports `POST /api/learning-path/generate-next`, and renders adaptive module metadata (`focusSkill`, `reason`, `difficulty`) without raw JSON.

This sprint validates the existing writing-learning journey from a real student perspective. It does not add new activity types, redesign the UI, or change the learning memory architecture unless validation finds a real defect.

## Product Goal

Prove that the writing-learning loop is coherent and useful:

- The activity feels like a realistic workplace task.
- Feedback is specific enough to help a student improve.
- Retry/improve shows progress.
- Previous attempts are easy to review.
- Learning memory and dashboard focus match the student's attempts.
- Continue-path recommendations are not repetitive and explain why they were recommended.

## Test Personas

### Persona 1: Project Planner

- Career context: Project Planner
- Level: B1
- Source language: Persian
- Target language: English
- Goal: Workplace writing
- Expected weaknesses: direct tone, long sentences, weak summarising, tense/article mistakes
- Setup: use the admin create-student flow, then select Persian to English, Workplace English, closest available career profile. If Project Planner is not present in seed data, document the run as a manual persona variant using Document Controller until career seed data is expanded.

### Persona 2: Customer Support Officer

- Career context: Customer Support Officer
- Level: B1/B1+
- Source language: Persian, or another supported source language when available
- Target language: English
- Goal: Professional customer communication
- Expected weaknesses: polite complaint responses, clear explanations, softening negative messages
- Setup: use the admin create-student flow. If Customer Support Officer is not present in seed data, document the run as a manual persona variant using the available Workplace English career profile.

Seeded user accounts are not currently appropriate because the app intentionally has no public registration and admin-created students must enforce temporary password change. This sprint uses manual setup instructions plus deterministic Playwright mocks.

## End-to-End Journeys

Primary validation journey:

1. Admin logs in.
2. Admin creates a student.
3. Student logs in with temporary password.
4. Student changes password.
5. Student completes onboarding.
6. Dashboard shows the generated learning path.
7. Student opens `/activity`.
8. Student reads a workplace writing scenario.
9. Student submits the first answer.
10. Structured feedback renders.
11. Suggested changes show before/after guidance.
12. Persian explanation is hidden by default and can be revealed.
13. Student clicks Improve my answer.
14. Student submits a second attempt.
15. Score comparison appears.
16. Attempt history shows both attempts.
17. `/my-path` shows updated learning memory.
18. Student completes the module when ready.
19. Student clicks Continue my learning path.
20. New modules appear with reason, focus, and difficulty.
21. Dashboard learning focus summary updates.

## Validation Criteria

Activity quality:

- Scenario has a clear role, audience, tone, task, and workplace reason.
- Expected writing length is implied by the task and examples.
- Task matches the current module focus and B1/B1+ level.
- Scenario does not repeat the same situation with only a different title.

Feedback quality:

- Feedback is specific, not generic.
- Changes array references the student's actual text.
- Before/after guidance is useful.
- Mini lesson and next step are practical.
- Improved version is presented as a suggestion, not the answer.
- Persian explanation is concise and hidden by default.

Memory quality:

- Strengths and weak areas match submitted attempts.
- Recurring mistakes remain compact.
- Next focus is believable from recent feedback.
- Memory arrays remain capped and no submitted text is stored in full.

Adaptive module quality:

- Generated modules include non-empty reason, focusSkill, difficulty, and fingerprint.
- Duplicate scenarioType + audience + communicationMode combinations are rejected.
- Difficulty progression is gradual.
- Module reason connects to learning memory.

## Architecture Decisions

- Keep validation tests deterministic with mocked AI responses.
- Do not add demo user seeds in this sprint; manual persona setup is safer because student creation and temporary password flow are part of the product validation.
- Add guardrails at existing test boundaries: domain memory caps, adaptive endpoint metadata, duplicate fingerprint rejection, Playwright full-flow behavior.
- Do not expose module fingerprint JSON in the frontend; verify it through backend persistence tests.

## In Scope

- Sprint and QA documentation.
- Product backlog updates.
- Playwright full learning-loop coverage.
- Lightweight backend tests for adaptive metadata and duplicate fingerprint rejection.
- Manual quality criteria for activity, feedback, memory, and adaptive generation.

## Out of Scope

- Speaking, listening, vocabulary, pronunciation, or reading activities.
- Public registration.
- New admin student detail workflow.
- New prompt rewrites unless validation finds a concrete quality issue.
- New learning memory architecture.
- Live AI-dependent tests.

## API Changes

No new API endpoints are added in this sprint.

Existing endpoints validated:

- `GET /api/learning-path`
- `GET /api/learning-path/memory`
- `POST /api/learning-path/generate-next`
- `POST /api/learning-path/modules/{moduleId}/complete`
- `GET /api/activity/next`
- `POST /api/activity/{activityId}/attempt`
- `GET /api/activity/{activityId}/attempts`
- `GET /api/admin/ai-usage/*`
- `GET /api/admin/diagnostics/*`

## DB Changes

No schema changes are planned.

Persistence guardrails validate that adaptive modules store `fingerprint_json` while the UI continues to show only friendly metadata.

## Frontend Changes

- Extend the core mocked Playwright journey through retry, history review, memory panel, module completion, continue-path generation, adaptive metadata display, and dashboard focus summary.
- Keep existing page designs and layouts.
- No new UI components are planned unless a real validation defect appears.

## Test Plan

Automated:

- `dotnet test LinguaCoach.slnx`
- `npm run build`
- `npx playwright test`

Guardrails:

- Adaptive generated modules include reason, focusSkill, difficulty, and stored fingerprint.
- Duplicate fingerprints are rejected.
- Memory arrays stay capped.
- Native-language explanation remains hidden by default in the activity and history flows.
- `/my-path` and dashboard do not expose raw JSON.
- Continue-path 503 displays a friendly message with correlation ID.

Manual:

- Run at least one Project Planner-style and one Customer Support Officer-style validation pass using available career profiles.
- Record quality observations in `docs/testing/e2e-learning-journey-validation-report.md`.

## Known Issues Found

- Seed data currently provides Document Controller as the main career profile. Project Planner and Customer Support Officer are documented as manual persona variants until career seed expansion is explicitly prioritised.
- Admin learning memory UI remains deferred because there is no admin student detail page.

## Fixes Made

- Added deterministic full learning-loop Playwright coverage.
- Added adaptive generation guardrails for metadata and duplicate fingerprints.
- Added sprint documentation and QA report.

## Deferred Improvements

- Add Project Planner and Customer Support Officer career seed rows in a future reference-data sprint.
- Add richer side-by-side attempt comparison after the existing history page is expanded.
- Add admin student detail page before surfacing admin learning memory.
- Add numeric skill scores after validating the current weak/strong skill profile model.

## Risks

- Manual quality judgement still depends on live AI provider behavior in production. Automated tests validate contracts and UI behavior with fake AI responses.
- Prompt quality may need adjustment after real persona runs; avoid prompt rewrites until concrete examples show repetition or generic feedback.

## Final Release Confidence

Pending final test run for this sprint. Confidence target is medium-high once automated tests pass and at least one manual persona run confirms activity/feedback/memory quality.
