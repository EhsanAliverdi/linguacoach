# Real Browser QA Bug Bash Report

**Date:** 2026-06-08  
**Tester:** Claude Code (gstack /qa)  
**Branch:** main  

---

## Summary

| Metric | Value |
|--------|-------|
| Routes tested | 12 |
| Dashboard cards tested | 5 |
| Bugs found | 3 |
| Bugs fixed | 3 |
| Bugs deferred | 0 |
| dotnet tests | 421 passed, 0 failed |
| npm build | passed |
| Playwright tests | 49 passed, 0 failed |

---

## Bug Bash Results

### Dashboard → Practice Vocabulary

| | |
|---|---|
| **Expected** | `/activity?type=VocabularyPractice` loads VocabularyPractice UI |
| **Actual (before fix)** | Navigated to `/vocabulary` (vocabulary list page). No vocabulary _practice_ entry point existed on dashboard. |
| **Root cause** | `dashboard.component.html` line 169: Vocabulary card used `routerLink="/vocabulary"` instead of `/activity?type=VocabularyPractice` |
| **Fix** | Changed `routerLink` to `/activity` with `[queryParams]="{type:'VocabularyPractice'}"`. Updated subtitle to "Practice saved phrases". |
| **Test added** | `disabled-actions-cleanup.spec.ts`: dashboard vocabulary card requests VocabularyPractice activity type |
| **Status** | ✅ Fixed |

---

### Dashboard → Vocabulary Practice → API returns WritingScenario (silent fallback)

| | |
|---|---|
| **Expected** | `GET /api/activity/next?type=VocabularyPractice` returns `vocabularyPractice` when prerequisites exist; returns 400 with friendly message when not |
| **Actual (before fix)** | When student had <3 vocab items, typed request silently returned `writingScenario`. The catch block in `ActivityGetHandler` set `activityType = WritingScenario` and fell through. |
| **Root cause** | `ActivityGetHandler.cs` lines 106-112: exception in vocab generation caused silent fallback to WritingScenario regardless of whether the request was typed or auto |
| **Fix** | Added explicit prerequisite check before generation. If `HasEnoughVocabularyAsync` returns false, throws 400 with friendly message. Catch block no longer silently falls back — re-throws with controlled error. |
| **Test added** | `VocabularyPracticeActivityTests.cs`: two new integration tests for typed route contract |
| **Status** | ✅ Fixed |

---

### Activity component — stale state on query param switch

| | |
|---|---|
| **Expected** | Navigating from `/activity?type=WritingScenario` to `/activity?type=VocabularyPractice` loads fresh VocabularyPractice activity |
| **Actual (before fix)** | `ngOnInit` used `route.snapshot.queryParamMap` — not reactive. Angular reuses the component, `ngOnInit` not called again, previous activity/type/state persists. |
| **Root cause** | `activity-lesson.component.ts` line 50: `this.route.snapshot.queryParamMap` does not react to query param changes |
| **Fix** | Subscribe to `this.route.queryParamMap` (Observable). Added `resetState()` and `loadActivity()` helpers. `nextActivity()` now uses these instead of duplicated inline code. |
| **Test added** | Covered by existing Playwright navigation tests which now exercise real query param switching. New integration tests cover backend typed routing contract. |
| **Status** | ✅ Fixed |

---

## Routes Verified (Code Audit)

### Student routes

| Route | Expected | Status |
|-------|----------|--------|
| `/login` | Login form renders | ✅ Code verified |
| `/change-password` | Password change form | ✅ Code verified |
| `/dashboard` | Dashboard with skill grid, stats, learning path | ✅ Code verified |
| `/activity` | Auto-selects activity type based on attempts | ✅ Code verified |
| `/activity?type=WritingScenario` | Returns WritingScenario | ✅ Integration test |
| `/activity?type=VocabularyPractice` | Returns VocabularyPractice or 400 | ✅ Integration test |
| `/activity?type=ListeningComprehension` | Returns ListeningComprehension | ✅ Integration test |
| `/activity/:id/history` | History for activity | ✅ Code verified |
| `/vocabulary` | Vocabulary list with status management | ✅ Playwright test |
| `/progress` | Progress summary, scores, skills | ✅ Playwright test |
| `/my-path` | Learning path and module details | ✅ Code verified |
| `/profile` | Student profile | ✅ Code verified |

### Admin routes

| Route | Expected | Status |
|-------|----------|--------|
| `/admin` | Admin dashboard | ✅ Code verified |
| `/admin/create-student` | Create student form | ✅ Playwright test |
| `/admin/ai-config` | AI configuration | ✅ Code verified |
| `/admin/usage` | AI usage stats and recent calls | ✅ Playwright test |
| `/admin/diagnostics` | Diagnostic events | ✅ Playwright test |
| `/admin/prompts` | Prompt management | ✅ Code verified |
| `/admin/careers` | Career profiles | ✅ Code verified |

---

## Dashboard Cards Audit

| Card | Link | Status |
|------|------|--------|
| Continue learning (hero) | `/activity` (auto) | ✅ Works |
| Writing | `/activity?type=WritingScenario` | ✅ Works |
| Listening | `/activity?type=ListeningComprehension` | ✅ Works |
| Vocabulary | `/activity?type=VocabularyPractice` | ✅ Fixed (was `/vocabulary`) |
| Speaking | Coming soon (div, not link) | ✅ Correct |
| Pronunciation | Coming soon (div, not link) | ✅ Correct |

---

## Key Behavioral Contracts Verified

### No silent type fallback
- `GET /api/activity/next?type=VocabularyPractice` with prerequisites → 200 `vocabularyPractice` ✅
- `GET /api/activity/next?type=VocabularyPractice` without prerequisites → 400 with friendly message ✅
- `GET /api/activity/next?type=ListeningComprehension` → 200 `listeningComprehension` ✅
- `GET /api/activity/next?type=WritingScenario` → 200 `writingScenario` ✅
- `GET /api/activity/next?type=InvalidType` → 400 (model binding rejects unknown enum) ✅
- `GET /api/activity/next` (no type) with <3 vocab items → 200 `writingScenario` (auto selection) ✅

### Transcript/answer security
- `audioScript` never returned in activity response before submit ✅
- `expectedAnswer` never returned in activity/listening response before submit ✅
- Transcript revealed only in feedback response after submit ✅

### CORS
- Port 4300 added to allowed origins for dev (alongside 4200) ✅

---

## Console Error Checks

All Playwright tests run with console error monitoring. No unexpected errors in:
- Dashboard page
- Activity page (all types)
- Admin AI usage page
- Diagnostics page

Pre-existing harmless warnings:
- CSS selector warnings from `&` empty sub-selector (Angular build, pre-existing)
- `No AI provider API key` warning at startup in dev (expected)

---

## Final Test Evidence

```
dotnet test LinguaCoach.slnx
  Unit tests:        233 passed, 0 failed
  Integration tests: 188 passed, 0 failed
  Total:             421 passed, 0 failed

npm run build
  Application bundle generation complete. [6.334 seconds]
  Status: succeeded

npx playwright test
  49 passed, 0 failed
```
