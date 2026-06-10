---
status: historical
---
# Learning History, Language Assistance & AI Resilience Sprint

**Date**: 2026-06-05
**Status**: In progress
**Branch**: main

---

## Sprint Goal

Three improvement areas:

1. **Learning History** — students can revisit previous modules, activities, feedback, and all retry iterations.
2. **Optional Native-Language Assistance** — Farsi/native-language explanation is hidden by default and shown on demand.
3. **AI Resilience** — fallback providers, Qwen support, production-ready AI configuration, comprehensive usage tracking.

---

## Current State (entering this sprint)

From previous sprints:
- `ActivityAttempt` is append-only; retries create new rows on the same `LearningActivity`
- `FeedbackJson` contains `changes[]`, `coachSummary`, `miniLesson`, etc.
- `StudentProgressService` computes distinct-activity counts and focus area
- `AiProviderConfig` maps feature keys to provider+model (DB-configurable)
- `AiUsageLog` already exists but lacks: `FeatureKey`, `IsFallback`, `WasSuccessful`, `CorrelationId`, `DurationMs`
- `IAiProviderResolver` has one method `ResolveWritingFeedbackProvider()` — no fallback concept
- `AiProviderConfig` knows openai/gemini/anthropic — no Qwen
- Farsi explanation (`FeedbackInSourceLanguage`) always rendered if non-null

---

## Part 1 — Learning History

### Data model

```
LearningPath
  └─ LearningModule (has CompletedAt)
       └─ LearningActivity (one per generated scenario)
            └─ ActivityAttempt[] (one per submit/retry, append-only)
```

A student's history for a module is the set of `LearningActivity` rows linked to that module that have at least one `ActivityAttempt` from the student.

### APIs added

#### GET /api/learning-path/modules/{moduleId}/activities

Returns enriched activity list for a module with per-activity stats.

Response:
```json
{
  "moduleId": "...",
  "title": "Writing professional emails",
  "completedActivities": 2,
  "totalRequired": 3,
  "averageScore": 74.0,
  "activities": [
    {
      "activityId": "...",
      "title": "Follow-up email to manager",
      "activityType": "writingScenario",
      "attemptCount": 3,
      "bestScore": 82.0,
      "latestScore": 82.0,
      "latestAttemptAt": "2026-06-05T...",
      "hasFeedback": true
    }
  ]
}
```

#### GET /api/activity/{activityId}/attempts

Returns attempt history for a specific activity.

Response:
```json
{
  "activityId": "...",
  "title": "...",
  "situation": "...",
  "attempts": [
    {
      "attemptId": "...",
      "attemptNumber": 1,
      "submittedAt": "...",
      "score": 65.0,
      "coachSummary": "...",
      "changes": [...],
      "miniLesson": "...",
      "nextImprovementStep": "...",
      "suggestedImprovedVersion": "...",
      "nativeLanguageExplanation": "..."
    }
  ]
}
```

### /my-path drill-down UX

- Each current/completed module card shows "View activities" button
- Opens inline collapsible section or navigates to module history
- Shows activity list with attempt count, best score, improve-again button

---

## Part 2 — Optional Native-Language Assistance

### Change

`FeedbackInSourceLanguage` (Farsi explanation) is currently always rendered if non-null.

New behaviour:
- Hidden by default
- Show/Hide toggle button
- Button label uses source language name from learner profile if available
- If no Farsi explanation exists, button not shown

### Approach

Frontend-only change to `activity-lesson.component.html` + `.ts`.
No backend change needed — the field is already optional/nullable.

---

## Part 3 — AI Provider Fallback & Resilience

### Architecture changes

**`AiProviderConfig`** gains:
- `FallbackProviderName` (nullable)
- `FallbackModelName` (nullable)
- `FallbackEnabled` (bool, default false)

**`IAiProviderResolver`** gains:
- `ResolvePrimaryAndFallback(featureKey)` → returns `(primary, fallback?)` tuple
- Old `ResolveWritingFeedbackProvider()` remains for backward compat

**`AiActivityGeneratorHandler`** wraps calls with:
1. Try primary
2. On failure: log, try fallback if configured
3. On both failing: throw `AiUnavailableException` with correlation ID

**New exception**: `AiUnavailableException` — caught by global exception middleware → friendly "AI coach is temporarily unavailable. Reference: {cid}"

**Qwen provider**: OpenAI-compatible endpoint, `QwenProvider : IAiProvider`, provider name `"qwen"`, configured model IDs: `qwen-plus`, `qwen-max`, `qwen-turbo`, `qwen3-235b-a22b`

### AiProviderConfig known models updated

Add Qwen models to `KnownModelsByProvider`.

---

## Part 4 — AI Usage Tracking

### AiUsageLog fields added

- `FeatureKey` — e.g. `activity_generate_writing`
- `IsFallback` — whether this was a fallback call
- `WasSuccessful` — false if provider threw
- `FailureReason` (nullable) — short safe error type
- `CorrelationId` (nullable) — from logging scope
- `DurationMs` — provider call duration

Migration: `T20_AiUsageLogEnrichment`

### Admin AI Usage API

`GET /api/admin/ai-usage/summary` — total calls, success/fail/fallback counts, by provider, by feature
`GET /api/admin/ai-usage/recent?limit=100` — recent calls table

### Admin AI Usage UI

Route: `/admin/ai-usage` (was "soon" in sidebar)
Shows: summary cards, by-provider breakdown, recent calls table

---

## DB Migrations

- `T20_AiUsageLogEnrichment` — add FeatureKey, IsFallback, WasSuccessful, FailureReason, CorrelationId, DurationMs to ai_usage_logs
- `T21_AiProviderConfigFallback` — add fallback_provider_name, fallback_model_name, fallback_enabled to ai_provider_configs

---

## Tests

See sprint tasks for full test list.

---

## In Scope

- [x] Sprint documentation
- [ ] Learning history APIs (module activities + attempt history)
- [ ] /my-path module drill-down
- [ ] Activity attempt history UI
- [ ] Native-language explanation show/hide toggle
- [ ] AiProviderConfig fallback fields + migration
- [ ] AI fallback execution logic
- [ ] Qwen provider
- [ ] AiUsageLog enrichment + migration
- [ ] Admin AI usage API + UI
- [ ] Backend tests
- [ ] Playwright tests

## Out of Scope

- Speaking/listening/vocabulary/pronunciation
- Full multilingual UI
- Database log storage for audit
- Real-time provider health monitoring
- Token cost estimation for Qwen
