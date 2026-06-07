# Real Progress Page Sprint

**Status:** In progress  
**Replaces:** Placeholder `/progress` page with real practice insights  
**Date started:** 2026-06-07

---

## Sprint goal

Replace the hardcoded placeholder on the student Progress page with real data drawn from existing domain models. Students should be able to see how much they have practised, whether scores are improving, which skills are strong or weak, module progress, and what to practise next — all without requiring live AI.

---

## Current placeholder state

`src/LinguaCoach.Web/src/app/features/progress/progress.component.ts`

- Hardcoded "—" for all stats; "Coming soon" labels on all tiles
- Writing skill bar hardcoded to 64 %
- Recent results section always shows empty state
- No API calls; no services injected

---

## Data sources

| Data | Source |
|---|---|
| Total attempts, latest/average/best score, activities this week | `ActivityAttempt` rows for the student |
| Completed distinct activities | `StudentProgressService.GetModuleProgressAsync` (distinct `LearningActivityId`) |
| Modules completed / current module | `LearningPath`, `LearningModule.CompletedAt`, threshold logic |
| Score trend (last 10 attempts) | `ActivityAttempt` joined to `LearningActivity` and `LearningModule` |
| Skill profile | `StudentSkillProfile` rows |
| Learning memory | `UserLearningSummary` → `IStudentMemoryQuery` |

---

## API design

### `GET /api/progress`

Requires: authenticated student (`[Authorize]`)  
Returns: `ProgressSummaryResponse` (see below)

**Response shape:**

```json
{
  "summary": {
    "activitiesCompleted": 7,
    "totalAttempts": 11,
    "retryAttempts": 4,
    "averageScore": 78,
    "latestScore": 82,
    "bestScore": 91,
    "activitiesThisWeek": 3,
    "modulesCompleted": 2,
    "currentModuleProgress": {
      "moduleId": "...",
      "title": "Workplace Emails",
      "completedActivities": 2,
      "totalRequired": 3,
      "averageScore": 75,
      "latestScore": 82,
      "isReadyToComplete": false
    }
  },
  "scoreTrend": [
    {
      "attemptDate": "2026-06-07T10:00:00Z",
      "score": 82,
      "activityTitle": "Polite request message",
      "moduleTitle": "Workplace Emails",
      "attemptNumber": 2
    }
  ],
  "skillProgress": {
    "skills": [
      { "skillKey": "grammar_accuracy", "skillLabel": "Grammar accuracy", "isWeak": false }
    ],
    "topStrengths": ["Grammar accuracy", "Formal tone"],
    "weakestSkills": ["Sentence clarity"]
  },
  "learningFocus": {
    "journeySummary": "...",
    "nextRecommendedFocus": ["..."],
    "recurringMistakes": ["..."],
    "weakSkills": ["..."],
    "strongSkills": ["..."]
  },
  "moduleProgress": [
    {
      "moduleId": "...",
      "title": "Workplace Emails",
      "status": "completed",
      "completedActivities": 3,
      "averageScore": 80,
      "latestScore": 85,
      "isReadyToComplete": true,
      "completedAt": "2026-06-06T09:00:00Z"
    }
  ]
}
```

**Empty state:** If student has no attempts, all nullable fields are null/empty and frontend shows friendly empty state.

---

## Calculations

| Metric | Rule |
|---|---|
| `activitiesCompleted` | COUNT DISTINCT `LearningActivityId` with ≥ 1 attempt |
| `totalAttempts` | All `ActivityAttempt` rows for student |
| `retryAttempts` | `totalAttempts − activitiesCompleted` |
| `averageScore` | Average of scored attempts |
| `latestScore` | Most recent attempt with a score |
| `bestScore` | Max score across all attempts |
| `activitiesThisWeek` | Distinct activities attempted Mon–Sun current week (UTC) |
| `modulesCompleted` | Modules where `CompletedAt` is set OR distinct completed ≥ threshold |
| `scoreTrend` | Last 10 scored attempts ordered newest first |

Streak omitted — deferred, too unreliable without a reliable session clock.

---

## UI sections

1. **Header** — "Your progress" / "Track your writing practice, skill growth, and next focus."
2. **Summary cards** — Activities completed, Average score, Latest score, Attempts this week, Modules completed, Retry attempts
3. **Score trend** — Clean list of last 10 scored attempts (score · activity title · attempt number · date)
4. **Skill progress** — Progress bars from `StudentSkillProfile` with strengths/weak splits
5. **Module progress** — Module list with status badge, completed activities, scores, ready/completed state
6. **Learning focus** — Journey summary, next focus, recurring mistakes
7. **Empty state** — Shown when no attempts; "Start practising →" CTA to `/activity`
8. **Loading / error states** — Skeleton, friendly error, retry button

---

## Limitations

- Streak deferred
- Speaking/listening/vocabulary/pronunciation deferred
- No charting library; score trend is a formatted list
- Module progress links to `/my-path` for drill-down

---

## Tests

### Backend integration tests (`ProgressEndpointTests.cs`)

- `GET /api/progress` returns 401 without token
- Empty state returned safely when student has no attempts
- `activitiesCompleted` counts distinct activities (not retries)
- `retryAttempts` = total − distinct
- Average / latest / best score calculated correctly
- Score trend ordered newest first, max 10
- Skill profile included (may be empty list)
- Learning memory mapped safely, no raw JSON
- Module progress included
- Another student's attempts not included

### Frontend unit tests

- Progress page renders empty state when no data
- Summary cards show real values
- Score trend list renders
- Skill progress bars render
- Module progress renders
- No raw JSON displayed
- API error shows friendly message

### Playwright tests

- Student progress page loads
- Progress page shows data after mocked attempts
- Empty state displays correctly
- No unexpected console errors
- Mobile layout does not overflow

---

## Deferred analytics

- Streak counter
- Weekly practice chart
- Comparison vs. CEFR benchmark
- Speaking / listening / vocabulary / pronunciation skill bars
- Score percentile
