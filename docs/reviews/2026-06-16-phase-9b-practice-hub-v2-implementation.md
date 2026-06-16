# Phase 9B — Practice Hub v2 Implementation Review

**Date:** 2026-06-16
**Sprint/Feature:** Phase 9B — Catalog-driven Practice Hub
**Commit:** 7a6f7d7

---

## Files Changed

- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.html`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.css`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`

---

## What Changed

### Angular UI

Replaced the hardcoded skill-card grid with a catalog-driven per-format card layout.

**Component (`practice-gym.component.ts`):**
- Reads `ExerciseTypeDefinition[]` from the existing `GET /activity/exercise-types` endpoint (no new API needed).
- Computes `skillGroups` signal: ordered list of skill buckets (listening → reading → writing → speaking → vocabulary → grammar), each containing `FormatCard[]` derived from catalog data.
- Each `FormatCard` carries: key, displayName, description, primarySkill, secondarySkills, defaultItemCount, estimatedMinutes, runnable flag, icon.
- `startFormat(card)`: calls `getPracticeGymNext({ skill, exerciseType })` and navigates to `/activity?activityId=...&returnTo=/practice`. Identical flow to old `selectSkill`.
- `loadState` signal: `'loading' | 'ready' | 'error'` drives template branching.
- Backward-compat helpers kept: `selectSkill`, `hasSkillAvailable`, `skillStatusText`, `isAvailable`, `statusText`. All existing tests pass unchanged.

**Template (`practice-gym.component.html`):**
- Loading skeleton (animated dots).
- Error state with guidance.
- Empty state when catalog returns zero types.
- Per-skill `<section>` rendered with `@for` over `skillGroups`.
- Runnable formats: `<button>` with "Ready" badge, secondary skill chips, item count, estimated time, "Start practice" CTA.
- Locked formats: `<div aria-disabled>` with "Coming soon" badge.

**CSS (`practice-gym.component.css`):**
- Auto-fill grid layout (`minmax(200px, 1fr)`).
- Loading animation (bounce dots).
- Status badge variants: ready (primary-tinted), loading (muted), soon (muted lighter).
- Secondary skill chips.
- Meta row for count + time.

### Backend

No backend changes. The existing `GET /activity/exercise-types` endpoint already exposes all required fields: `displayName`, `description`, `primarySkill`, `secondarySkills`, `isEnabled`, `implementationStatus`, `isAvailableForGeneration`, `supportsPracticeGym`, `defaultItemsPerPractice`, `estimatedDurationMinutes`.

---

## Tests Added/Updated

**`practice-gym.component.spec.ts`:**
- Added `readyAnswerShortQuestion` and `plannedFormat` fixtures.
- Updated `ALL_READY` list with all 15 current ready formats.
- Added `defaultItemsPerPractice` fields to all existing fixtures (were missing).
- New catalog-driven tests:
  - Renders practice heading
  - Renders skill sections for each skill in catalog
  - Renders runnable button card for `answer_short_question`
  - Planned format renders as locked non-button card
  - Shows item count for `answer_short_question`
  - Shows secondary skill chips for `listening_fill_in_blanks` and `reading_writing_fill_in_blanks`
  - Loading state before catalog arrives
  - Error state when catalog load fails
  - Empty state when catalog returns no types
  - `startFormat` navigates on success
  - `startFormat` shows message when no activity available
  - `startFormat` shows fallback message on API error
  - `startFormat` is no-op for locked/planned card
  - Skill groups ordered: listening < reading < writing < speaking
  - `answer_short_question` is ready and available
  - Clicking Speaking routes correctly for `answer_short_question`

Total: 47 practice-gym tests (up from 30).

---

## Final Test Counts

| Suite | Tests | Result |
|---|---|---|
| LinguaCoach.UnitTests | 818 | PASS |
| LinguaCoach.IntegrationTests | 506 | PASS |
| LinguaCoach.ArchitectureTests | 3 | PASS |
| Angular unit tests | 154 | PASS |
| Angular prod build | — | PASS |

**Total backend: 1327. Total Angular: 154. Zero failures.**

---

## Decisions Made

1. **No new API endpoint.** The existing `GET /activity/exercise-types` already returns all catalog data needed. Adding a new "student-safe catalog" endpoint was unnecessary overhead.
2. **Backward-compat helpers kept.** `selectSkill`, `hasSkillAvailable`, `isAvailable`, `statusText` are preserved so the 30 existing spec tests needed zero changes to pass.
3. **`startFormat` passes both `skill` and `exerciseType`.** The `getPracticeGymNext` endpoint already accepts `exerciseType`; passing it allows the backend to pick the specific format rather than a random skill match.
4. **No hardcoded format list.** Skill groups and cards are fully derived from the catalog signal. Adding a new ready format in future requires no UI change.

---

## Planned Formats Remain Non-Runnable

Confirmed: `implementationStatus !== 'ready'` OR `!isAvailableForGeneration` OR `!supportsPracticeGym` → `runnable = false` → card renders as `<div aria-disabled>` not a `<button>`. Backend enforcement unchanged.

---

## Compatibility Preserved

- `/activity` route: unchanged.
- `exerciseType=`, `type=`, `pattern=` query params: unchanged (resolved in ActivityController).
- `selectSkill` flow: unchanged behaviour, still calls `getPracticeGymNext({ skill })`.

---

## Risks / Unresolved

- No Playwright E2E test added — the existing project has Playwright specs only for admin flows. A Practice Hub E2E test would be a future addition.
- `supportsPracticeGym` flag on `ExerciseTypeDefinition` controls whether a ready format appears as runnable. If an admin disables this flag, the format moves to locked. This is expected behaviour.

---

## Documentation Impact

- Docs reviewed: `AGENTS.md`, `docs/architecture/README.md`
- Docs updated: this review doc
- Docs intentionally not updated: sprint backlog (no active sprint doc for Phase 9B), architecture docs (no architectural change)
