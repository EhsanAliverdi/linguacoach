---
status: historical
---
# Full App Verification and Disabled Actions Cleanup Sprint

## Current State

SpeakPath now has implemented student practice surfaces for workplace writing, vocabulary review/practice, and listening comprehension with generated audio support. The student dashboard still presented some implemented actions as future-only, which made the product feel less complete than the backend and activity UI already are.

## Product Goal

Make the implemented learning journey feel coherent and demo-ready by removing stale "coming soon" states from completed features, keeping truly future features disabled, and verifying the core student/admin routes still load.

## Architecture Decisions

- Keep `/activity` as the main student practice route.
- Use a `type` query parameter when a dashboard card should request a specific implemented activity type.
- Preserve backend enum names in API requests, while keeping frontend DTO activity types in the existing camel-case shape.
- Do not implement future activity types during this sprint.

## In Scope

- Verify the student dashboard does not disable implemented practice actions.
- Enable dashboard entry points for writing, listening comprehension, and vocabulary.
- Keep Speaking and Pronunciation visibly future-only.
- Verify `/activity` can request a listening activity from the dashboard.
- Verify admin AI usage loads.
- Verify students cannot access admin-only routes.
- Update sprint documentation and backlog.

## Out of Scope

- Speaking practice.
- Pronunciation practice.
- Reading practice.
- New AI providers or live AI prompt calibration.
- Full redesign of the student or admin app shell.

## API Changes

No backend API contract changes.

Frontend now passes API enum names through the existing `GET /api/activity/next?type=...` query parameter:

- `WritingScenario`
- `ListeningComprehension`
- `VocabularyPractice`

## DB Changes

None.

## Frontend Changes

- Dashboard:
  - Writing links to `/activity?type=WritingScenario`.
  - Listening links to `/activity?type=ListeningComprehension`.
  - Vocabulary links to `/vocabulary`.
  - Speaking and Pronunciation remain marked "Coming soon".
- Activity lesson:
  - Reads the optional `type` query parameter.
  - Requests the matching activity type from the activity service.
  - Keeps the same requested type when moving to the next activity from that page.
- Activity service:
  - Converts frontend activity type values to backend enum names before calling the API.
- Profile and landing copy:
  - Removed stale "coming soon" language for implemented listening/vocabulary capability.

## Tests Added

- `disabled-actions-cleanup.spec.ts`
  - Dashboard exposes implemented writing/listening/vocabulary actions.
  - Dashboard listening card requests `ListeningComprehension`.
  - Admin AI usage page loads usage summary and recent-call data.
  - Student users are redirected away from admin-only routes.

## Test Plan

- Backend: `dotnet test`
- Frontend build: `npm run build`
- Browser: `npx playwright test`

## Tasks

- [x] Audit frontend for stale disabled/coming-soon labels.
- [x] Enable implemented dashboard actions.
- [x] Add typed dashboard navigation to `/activity`.
- [x] Add Playwright verification for dashboard action states.
- [x] Add Playwright verification for admin AI usage and admin guard behavior.
- [x] Update sprint documentation.
- [x] Update product backlog.

## Risks

- The dashboard now asks for a specific activity type. If a student has no generated activity of that type, the existing activity fallback/error behavior must remain clear.
- Playwright screenshots may change because implemented cards are no longer disabled on the dashboard.

## Final Verification Result

- `dotnet test` passed: 416 discovered unit/integration tests passed; architecture test assembly reported no discoverable tests.
- `npm run build` passed with existing CSS budget and selector warnings.
- `npx playwright test` passed: 47 browser tests.
- Screenshot workflow refreshed affected artifacts.
