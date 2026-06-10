---
status: historical
---
# Real Browser QA Bug Bash Sprint

**Status:** Complete  
**Date:** 2026-06-08  
**Goal:** Fix every implemented feature so it actually works when clicked by a real user. No fake passing tests.

---

## Known Bugs Fixed

### Bug 1 — Vocabulary dashboard card opened Vocabulary List instead of VocabularyPractice activity

**Root cause:** Dashboard Vocabulary card (`dashboard.component.html`) routed to `/vocabulary` (the vocabulary list page), not to `/activity?type=VocabularyPractice`. There was no dashboard entry point that launched the vocabulary practice activity.

**Fix:** Changed the Vocabulary skill card `routerLink` from `/vocabulary` to `/activity` with `[queryParams]="{type:'VocabularyPractice'}"`. Updated the subtitle from "Review saved phrases" to "Practice saved phrases" to set correct user expectations.

**Files changed:**
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html`

---

### Bug 2 — VocabularyPractice typed request silently returned WritingScenario when prerequisites missing

**Root cause:** `ActivityGetHandler.cs` — when `VocabularyPractice` generation failed (no vocab items), the catch block set `activityType = ActivityType.WritingScenario` and fell through to the AI generation path. A typed request (`GET /api/activity/next?type=VocabularyPractice`) would silently return `WritingScenario`, which is exactly what the user reported.

**Fix:**
1. Added an explicit prerequisite check before generation: calls `HasEnoughVocabularyAsync` and throws `InvalidOperationException` with a friendly message if insufficient.
2. Removed the silent fallback to `WritingScenario` — the catch block now re-throws with a controlled error message instead.

The friendly message: *"Vocabulary practice unlocks after you save at least 3 vocabulary items from writing activities. Complete more writing activities to build your vocabulary bank."*

**Behavioral contract after fix:**
- `GET /api/activity/next?type=VocabularyPractice` with ≥3 vocab items → 200 `vocabularyPractice`
- `GET /api/activity/next?type=VocabularyPractice` with <3 vocab items → 400 with friendly prerequisite message
- `GET /api/activity/next` (auto) with <3 vocab items → 200 `writingScenario` (unchanged, auto-selection already checked this)
- `GET /api/activity/next?type=ListeningComprehension` → 200 `listeningComprehension` (no fallback to writing)

**Files changed:**
- `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`

---

### Bug 3 — Activity component loaded stale state when switching between typed dashboard cards

**Root cause:** `ActivityLessonComponent.ngOnInit()` called `getNext()` once and used `route.snapshot.queryParamMap`. When Angular reuses the component (navigating from `/activity?type=WritingScenario` to `/activity?type=VocabularyPractice`), `ngOnInit` is not re-triggered. The component kept the previous activity's state and type.

**Fix:** Changed `ngOnInit` to subscribe to `route.queryParamMap` (Observable), which fires on every query param change. Extracted a `resetState()` and `loadActivity()` method. `nextActivity()` now calls these instead of duplicating logic.

**Files changed:**
- `src/LinguaCoach.Web/src/app/features/activity/activity-lesson/activity-lesson.component.ts`

---

## Tests Added

### Integration Tests (backend)

**File:** `tests/LinguaCoach.IntegrationTests/Api/VocabularyPracticeActivityTests.cs`
- `GetNext_TypedVocabularyPractice_WithNoVocabItems_Returns400NotWritingScenario` — typed request with no vocab must return 400, not WritingScenario
- `GetNext_TypedVocabularyPractice_WithEnoughVocabItems_ReturnsVocabularyPractice` — typed request with sufficient vocab returns correct type

**File:** `tests/LinguaCoach.IntegrationTests/Api/ListeningComprehensionActivityTests.cs`
- `GetNext_TypedListeningComprehension_ReturnsListeningNotWritingScenario` — typed listening request returns listening, never WritingScenario
- `GetNext_TypedWritingScenario_ReturnsWritingScenario` — typed writing request works
- `GetNext_InvalidType_Returns400` — invalid type string returns 400

### Playwright Tests (frontend)

**File:** `src/LinguaCoach.Web/e2e/disabled-actions-cleanup.spec.ts`
- `dashboard vocabulary card requests VocabularyPractice activity type` — verifies URL has `type=VocabularyPractice`, vocab UI renders, writing textarea absent
- `dashboard vocabulary card shows prerequisite message when vocab items insufficient` — verifies 400 error shows prerequisite message, no WritingScenario fallback
- Updated existing test: vocabulary card now checks `href` contains `type=VocabularyPractice` instead of `/vocabulary`

---

## Test Results

- `dotnet test LinguaCoach.slnx`: **421 passed, 0 failed** (was 416; +5 new integration tests)
- `npm run build`: **succeeded** (CSS warnings only, pre-existing)
- `npx playwright test`: **49 passed, 0 failed** (was 47; +2 new Playwright tests)

---

## CORS Fix (Infrastructure)

**File:** `src/LinguaCoach.Api/Program.cs`

Added `http://localhost:4300` to CORS allowed origins for development. The Angular dev server can run on different ports depending on what else is running on the machine; allowing both 4200 and 4300 prevents CORS blocks during local testing.

---

## What Was NOT Done

- Speaking and Pronunciation remain "Coming soon" — correct.
- No new major features added.
- No UI redesign.
- No silent fallbacks introduced.
