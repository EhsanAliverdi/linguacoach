---
status: historical
---
# VocabularyPractice Activity Sprint

**Status:** In progress  
**Date started:** 2026-06-07  
**Depends on:** Vocabulary Extraction from Writing Attempts Sprint (complete)

---

## Sprint goal

Introduce `VocabularyPractice` as the second real `ActivityType` after `WritingScenario`.

Students practice vocabulary and workplace phrases extracted from their own writing attempts, via a fill-in-the-blank exercise mode, using the existing `LearningActivity → ActivityAttempt` architecture.

---

## Activity design

### Practice mode: `fill_blank`

Student sees a sentence with a blank and must type the missing phrase.

Example:
- **Term:** could you please
- **Prompt:** _____ send me the updated schedule by tomorrow?
- **Expected answer:** could you please
- **Hint:** Use a polite request phrase.
- **Explanation:** This phrase makes the request sound professional and polite.

### Scoring

- Each item: correct (case-insensitive, trimmed) = 100, partial match = 50, incorrect = 0
- Overall score = average across all items (0–100, rounded)
- No AI call needed for scoring

### Vocabulary status after practice

| Before | Condition | After |
|---|---|---|
| New | Practised ≥ 1 time | Practising |
| Practising | StrengthScore ≥ 90 | Mastered |
| Any | StrengthScore update: +10 correct, -5 incorrect, clamp 0–100 | — |

### Activity content JSON (stored in `LearningActivity.AiGeneratedContentJson`)

```json
{
  "title": "Practise polite workplace requests",
  "instructions": "Fill in the blank with the most professional phrase.",
  "practiceMode": "fill_blank",
  "items": [
    {
      "vocabularyItemId": "uuid",
      "term": "could you please",
      "prompt": "_____ send me the updated schedule?",
      "expectedAnswer": "could you please",
      "hint": "Use a polite request phrase.",
      "explanation": "This phrase makes the request sound professional and polite."
    }
  ]
}
```

### Submission JSON (stored in `ActivityAttempt.SubmittedContent`)

```json
{
  "answers": [
    {
      "vocabularyItemId": "uuid",
      "answer": "could you please"
    }
  ]
}
```

### Feedback JSON (stored in `ActivityAttempt.FeedbackJson`)

```json
{
  "overallScore": 80,
  "coachSummary": "Good work — you got 4 out of 5 correct.",
  "itemFeedback": [
    {
      "vocabularyItemId": "uuid",
      "term": "could you please",
      "isCorrect": true,
      "studentAnswer": "could you please",
      "expectedAnswer": "could you please",
      "explanation": "Correct — this polite request phrase shows workplace communication confidence."
    }
  ],
  "miniLesson": "Polite request phrases use modal verbs ('could', 'would') to soften direct commands.",
  "nextImprovementStep": "Try using 'Could you please' in your next writing activity.",
  "whatYouDidWell": ["Correct use of 'could you please'"],
  "mainMistakes": ["Missed: 'at your earliest convenience'"]
}
```

---

## Activity selection logic

In `ActivityGetHandler.HandleAsync()`:

1. If `query.PreferredType` is set, use it (explicit override).
2. If student has ≥ 3 vocabulary items with status `New` or `Practising`, AND total attempt count is a multiple of 4 (i.e., `totalAttempts % 4 == 0 && totalAttempts > 0`): select `VocabularyPractice`.
3. Otherwise: `WritingScenario` (default).

This ensures students practice vocabulary roughly every 4th activity, but only when they have enough vocabulary to practice.

---

## Data flow

```
GET /api/activity/next
  → ActivityGetHandler selects type
  → VocabularyPracticeGenerator.GenerateAsync()
      loads student vocabulary (New/Practising, first 5)
      builds fill_blank items deterministically
      creates LearningActivity (VocabularyPractice, AiGenerated source)
      returns ActivityDto with vocabItems[] field

POST /api/activity/{id}/attempt
  → ActivitySubmitHandler detects VocabularyPractice
  → VocabularyPracticeEvaluator.EvaluateAsync()
      parses submitted answers from SubmittedContent JSON
      matches against expected answers (case-insensitive)
      calculates score
      updates StudentVocabularyItem: SeenCount, StrengthScore, Status
      builds feedbackJson
  → ActivityAttempt saved
  → memory update best-effort (unchanged)
  → vocabulary extraction skipped for VocabularyPractice type
```

---

## New domain additions

### StudentVocabularyItem.StrengthScore

New field: `int StrengthScore` (0–100, default 0).

- `RecordPractice(bool correct)` domain method:
  - correct: +10, clamp 0–100
  - incorrect: -5, clamp 0–100
  - RecordSeen() (increments SeenCount, updates LastSeenAtUtc)
  - Status transition: New → Practising, Practising → Mastered if StrengthScore ≥ 90

### Migration T27_VocabularyStrengthScore

Adds `strength_score` column to `student_vocabulary_items`.

---

## API changes

### GET /api/activity/next

Extended response when `activityType == "vocabularyPractice"`:

```json
{
  "activityId": "...",
  "activityType": "vocabularyPractice",
  "title": "...",
  "difficulty": "B1",
  "instructions": "...",
  "practiceMode": "fill_blank",
  "vocabItems": [
    {
      "vocabularyItemId": "uuid",
      "term": "could you please",
      "prompt": "_____ send me the updated file?",
      "hint": "Use a polite request phrase.",
      "explanation": "..."
    }
  ]
}
```

Note: `expectedAnswer` is NOT sent to frontend (prevents trivial cheating). Answer validation is backend-only.

### POST /api/activity/{id}/attempt

Extended request body for VocabularyPractice:

```json
{
  "submittedContent": "",
  "answers": [
    { "vocabularyItemId": "uuid", "answer": "could you please" }
  ]
}
```

`submittedContent` can be empty string for VocabularyPractice; `answers` array is used.

---

## Frontend changes

`activity-lesson.component` branches on `activityType`:

- **Learning state (WritingScenario):** Unchanged
- **Learning state (VocabularyPractice):** Shows instructions, vocab item list preview
- **Writing state (WritingScenario):** Unchanged (textarea)
- **Writing state (VocabularyPractice):** Shows fill-blank exercise cards with text inputs and hint toggle
- **Feedback state:** Both types use the same coach summary / score ring / next step structure

---

## Tests

### Backend
- `GET /api/activity/next` returns `VocabularyPractice` when conditions met
- `VocabularyPractice` not returned when fewer than 3 vocab items
- Generated activity content is valid JSON with `items` array
- Items belong to current student only
- Submitting correct answers gives score ≥ 80
- Submitting wrong answers gives score < 50
- `SeenCount` increments after practice
- `StrengthScore` increases for correct answers
- `StrengthScore` decreases for incorrect answers, clamped at 0
- `StrengthScore` clamps at 100 for repeated correct answers
- `New` status → `Practising` after first practice
- `Practising` → `Mastered` when StrengthScore ≥ 90
- Another student's vocab items are not used
- Activity history returns VocabularyPractice attempt safely

### Frontend
- Activity page renders VocabularyPractice fill-blank inputs
- Submit works for VocabularyPractice
- Feedback shows score and item results
- History page renders vocabulary practice safely
- Progress page not broken

### Playwright
- Mocked VocabularyPractice activity renders
- Student fills answers and submits
- Feedback appears
- No unexpected console errors

---

## Limitations

- Only `fill_blank` mode implemented (no multiple choice, no rewrite)
- No AI generation for VocabularyPractice (deterministic only)
- No spaced-repetition scheduling (NextReviewAtUtc remains placeholder)
- Activity history displays vocabulary practice with basic format
- No native-language explanations for vocabulary items

---

## Deferred

- `choose_best_phrase` and `rewrite_sentence` modes
- AI-generated vocabulary quizzes
- Spaced repetition scheduler using `NextReviewAtUtc`
- Richer quiz modes
- Admin vocabulary analytics
- Audio pronunciation
- Listening/speaking integration
- Native-language vocabulary explanations
