# Learning Path Progression & Personalisation Sprint

**Date**: 2026-06-05
**Status**: In progress
**Branch**: main

---

## Sprint Goal

Make SpeakPath feel like a guided learning journey.

After completing writing activities and retry attempts, the app should:
- Track module progress accurately (without counting retries as separate completions)
- Identify the student's main improvement area from structured feedback
- Recommend the next activity focused on that weakness
- Make `/my-path` feel like a real learning journey with enriched module data
- Make the dashboard progress feel personalised and actionable

The student should feel:
> "I know where I am in my learning path."
> "I can see my progress."
> "The app knows what I need to practise next."
> "My next activity feels connected to my previous mistakes."

---

## Current State (entering this sprint)

From the Learning Experience Improvement Sprint:
- AI feedback returns structured `changes` array with categories (grammar, vocabulary, tone, clarity, structure, punctuation)
- `ActivityFeedbackDto` includes `CoachSummary`, `FocusFirst`, `Changes[]`, `MiniLesson`, `NextImprovementStep`
- Retry/improve flow creates separate `ActivityAttempt` rows for the same `LearningActivity`
- Dashboard shows real `ActivitiesCompleted`, `LatestScore`, `AverageScore`
- 312 backend tests + 16 Playwright tests all passing

### Current gaps

- No focus area detection — the AI generates whatever it decides; no systematic weakness tracking
- Module completion threshold (3) is hardcoded but module progress shows no average score or improvement data
- No `POST /api/learning-path/modules/{moduleId}/complete` endpoint — module advancement is purely automatic/calculated
- `/my-path` shows module cards but no per-module score, focus area, or ready-to-complete notice
- Dashboard personalisation message is static ("You are on module X of Y")
- `ActivityGenerationContext.RecentMistakesSummary` field exists but is never populated
- Retries count toward completion threshold — a student can complete a module just by retrying the same activity 3 times (unfair inflation)

---

## Key Architecture Decisions

### Completed activity vs retry

**Problem**: A student who retries the same `LearningActivity` 3 times currently satisfies the `CompletionThreshold = 3` for the whole module, which is wrong.

**Fix**: Count **distinct** `LearningActivityId` values per module (not total `ActivityAttempt` rows).

A "completed activity" = at least one `ActivityAttempt` submitted for a given `LearningActivity`.

So `completedActivities` = `COUNT(DISTINCT LearningActivityId)` among all `ActivityAttempt` rows for the module's activities.

This is the correct definition everywhere: dashboard, module progress, `/my-path`, completion threshold check.

### Module completion (no new domain field needed)

`LearningModule` has no `IsCompleted` field and we will not add one in this sprint.

Module readiness is calculated:
- `distinctCompletedActivities >= CompletionThreshold (3)` AND `averageScore >= 75`

The student sees a "Ready to advance" notice but is **not** automatically moved. They choose to proceed.

`POST /api/learning-path/modules/{moduleId}/complete` is added as a simple mechanism that marks the module's activities as done by updating the `LearningPath`'s active module pointer. Since there is no domain field, we implement this by resetting the "current module" calculation: all modules with distinct completions >= threshold are treated as complete; calling the endpoint for a module that is ready just persists a `CompletedModuleId` record.

**Simplest safe implementation**: Add a `CompletedModule` entity (or nullable set on `LearningModule`) that stores explicitly-confirmed completions. This avoids recomputing readiness from attempt counts on every request and allows the student's confirmation to be recorded.

**Chosen approach for this sprint**: Add `LearningModule.CompletedAt` (nullable DateTime). When the student calls `POST /api/learning-path/modules/{moduleId}/complete`, set `CompletedAt = now`. Module is "complete" if `CompletedAt != null` OR `distinctCompleted >= 3 AND averageScore >= 75` (whichever comes first — either the student confirmed it or the auto-threshold triggered it). Migration required.

### Focus area detection

Look at the latest 5 `ActivityAttempt` records for the student. For each attempt, parse `FeedbackJson` and extract `changes[].category`. Count frequency across all changes. Most frequent category becomes `CurrentFocusArea`.

Category → friendly label mapping:
- `grammar` → Grammar accuracy
- `vocabulary` → Professional vocabulary
- `tone` → Polite workplace tone
- `clarity` → Clear sentence meaning
- `structure` → Message organisation
- `punctuation` → Punctuation and readability

If no attempts or no changes, return null (no focus area yet).

This calculation happens in `DashboardQueryHandler` and `LearningPathQueryHandler`. It is not cached — computed per request (fast enough: max 5 attempts × ~10 changes each).

### Wiring focus area into next activity

`ActivityGenerationContext.RecentMistakesSummary` already exists but is unused. Wire the current focus area description into this field when calling `GetNextActivity`.

The `activity_generate_writing` prompt already includes `{{recentMistakes}}`. The AI will factor it in when generating the task type and scenario.

---

## Module Progression Rules

A module is **ready to complete** when:
1. `distinctCompletedActivities >= 3` (at least 3 different activities attempted)
2. `averageScore >= 75` across all attempts in the module

A module is **complete** when:
- `CompletedAt` is set (student confirmed via API), OR
- Auto-threshold: distinctCompleted >= 3 AND averageScore >= 75 (auto-flagged as complete on read if student hasn't manually confirmed — sets `CompletedAt` on read to persist it)

**Actually for this sprint**: to keep migrations minimal and avoid race conditions with auto-setting on read, we will use the **explicit confirm only** approach: the endpoint sets `CompletedAt`. The UI shows "Ready to advance" and the student clicks "Complete this module →". Without that click, the module stays `isCurrent` even if scores qualify.

---

## DB Changes

### Migration: add `CompletedAt` to `LearningModule`

```sql
ALTER TABLE learning_modules ADD COLUMN completed_at TIMESTAMP WITH TIME ZONE NULL;
```

No data migration needed — all existing modules have `completed_at = null` (not explicitly completed).

---

## API Changes

### Extended module summary

`LearningModuleSummary` DTO gains new fields:
- `AverageScore: double?` — average of all attempts in this module
- `LatestScore: double?` — most recent attempt score in this module
- `IsReadyToComplete: bool` — computed: distinctCompleted >= 3 AND averageScore >= 75
- `IsCompleted: bool` — `CompletedAt != null`

### New endpoint

`POST /api/learning-path/modules/{moduleId}/complete`

- Auth: student JWT required
- Validates: module belongs to student's active path
- Validates: `IsReadyToComplete` is true (or admin override — not needed now)
- Sets `CompletedAt = now` on the `LearningModule`
- Returns: `204 No Content`

### Extended DashboardResult

New fields added to `DashboardResult`:
- `CurrentFocusArea: string?` — e.g. "tone"
- `CurrentFocusDescription: string?` — e.g. "Polite workplace tone"
- `NextRecommendedPractice: string?` — e.g. "Complete 1 more activity to make this module ready to finish."
- `LatestImprovement: string?` — e.g. "Your score improved from 62 to 78 in your last session."

---

## Frontend Changes

| File | Change |
|---|---|
| `learning-path.models.ts` | Add `averageScore`, `latestScore`, `isReadyToComplete`, `isCompleted` to module summary |
| `learning-path.component.ts` | Add `completeModule()` method, update `moduleStatus()` logic |
| `learning-path.component.html` | Show per-module score, ready-to-complete notice, complete button |
| `dashboard.models.ts` | Add `currentFocusArea`, `currentFocusDescription`, `nextRecommendedPractice`, `latestImprovement` |
| `dashboard.component.html` | Show focus area and personalised message in coach card |

---

## Test Plan

### Backend tests

- [ ] `distinctCompletedActivities` counts only distinct `LearningActivityId`, not retry attempts
- [ ] Module `IsReadyToComplete` = true when distinctCompleted >= 3 AND averageScore >= 75
- [ ] Module `IsReadyToComplete` = false when score < 75 even with 3 distinct activities
- [ ] `POST /api/learning-path/modules/{moduleId}/complete` sets `CompletedAt`
- [ ] `POST .../complete` returns 404 if module does not belong to student
- [ ] `POST .../complete` returns 400 if module is not ready to complete
- [ ] Focus area detection returns most frequent category from last 5 attempts
- [ ] Focus area returns null when no attempts exist
- [ ] `ActivityGenerationContext.RecentMistakesSummary` is populated from focus area
- [ ] Dashboard `CurrentFocusArea` is correct
- [ ] Dashboard `NextRecommendedPractice` message is present

### Frontend tests (component/unit)

- [ ] `/my-path` renders `averageScore` and `isReadyToComplete` per module
- [ ] "Ready to advance" notice renders when `isReadyToComplete = true`
- [ ] "Complete this module" button visible when `isReadyToComplete = true`
- [ ] Dashboard coach card shows `currentFocusDescription`

### Playwright

- [ ] Existing smoke test still passes (no regression)
- [ ] `/my-path` page loads without error (static check only)

---

## In Scope

- [x] Sprint documentation
- [ ] Add `CompletedAt` to `LearningModule` + EF migration
- [ ] Fix completed-activity count: distinct activities, not total attempts
- [ ] Add `IsReadyToComplete`, `AverageScore`, `LatestScore`, `IsCompleted` to module summaries
- [ ] Add `POST /api/learning-path/modules/{moduleId}/complete`
- [ ] Add focus area detection from last 5 attempts' `changes` JSON
- [ ] Wire focus area into `ActivityGenerationContext.RecentMistakesSummary`
- [ ] Extend `DashboardResult` with focus area and personalised message
- [ ] Update `/my-path` to show enriched module data
- [ ] Update dashboard to show focus area in coach card
- [ ] Backend tests
- [ ] All checks green

---

## Out of Scope

- Speaking/listening/vocabulary/pronunciation
- Old `/api/writing` endpoints
- Complex spaced repetition
- Full skill analytics engine
- Module certificates or badges
- Admin progress view
- Long-term trend charts
- Teacher review UI

---

## Future Follow-up Items (post-sprint)

- Richer skill analytics per module (grammar trend over time)
- Module completion certificates/badges
- Teacher/admin progress view per student
- Long-term trend charts (score over 10+ attempts)
- Spaced repetition for repeated mistakes (flash back activities from weak areas)
- Vocabulary extraction from writing attempt mistakes
- Speaking/listening activity types
