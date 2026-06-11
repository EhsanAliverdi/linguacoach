---
status: historical
---

> **Scope correction (2026-06-12):** Despite this sprint's title and framing, vocabulary
> extraction is a cross-cutting engine, not a writing-only feature. As of 2026-06-12,
> `VocabularyExtractionService.ExtractAsync` also fires from `HandlePatternEvaluationAsync`
> for any pattern-evaluated activity that produces AI `Corrections` (AiStructured /
> AiOpenEnded patterns: `email_reply`, `teams_chat_simulation`, `listen_and_answer`,
> `spoken_response_from_prompt`, etc.), not only legacy `WritingScenario` attempts.
> Deterministic patterns (`ExactMatch`, `KeyedSelection`, `NoMarking` — e.g. gap fill,
> phrase match) never populate `Corrections` and so never trigger this, preserving the
> no-AI-call guarantee for them. See
> `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md` for the implementation
> note.

# Vocabulary Extraction from Writing Attempts Sprint

**Status:** In progress  
**Date started:** 2026-06-07  
**Depends on:** Real Progress Page Sprint (complete)

---

## Sprint goal

Add a lightweight vocabulary learning layer based on the student's own writing attempts and AI feedback.

The app extracts useful workplace words, phrases, tone softeners, grammar patterns, connectors, and common wording mistakes from writing practice, saves them per student, and lets the student review and track their personal vocabulary list.

This sprint prepares the foundation for a future `VocabularyPractice` activity type but does not implement it.

---

## Why vocabulary comes before listening/speaking

- Writing attempts already produce AI feedback — vocabulary extraction reuses this data with a small marginal AI cost.
- The student's actual mistakes and useful phrases are already in `ActivityAttempt.FeedbackJson` and `SuggestedImprovedVersion`.
- No audio, microphone, or real-time infrastructure needed.
- Builds a student-specific vocabulary list organically — better UX than a generic word bank.
- Establishes the data model and extraction flow that `VocabularyPractice` activity type will consume later.

---

## Current state (before this sprint)

- `VocabularyEntry` entity exists but is designed for SM-2 flashcard tracking (full spaced repetition).
- No extraction from writing attempts.
- No vocabulary page.
- `ActivitySubmitHandler` fires memory update after submission but not vocabulary extraction.

---

## Data model

### New entity: `StudentVocabularyItem`

Table: `student_vocabulary_items`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| StudentProfileId | Guid | FK to StudentProfiles |
| SourceActivityAttemptId | Guid? | FK to ActivityAttempts (nullable) |
| SourceLearningActivityId | Guid? | FK to LearningActivities (nullable) |
| Term | string(300) | normalised lower, trimmed |
| SuggestedPhrase | string? | example usage in context |
| MeaningOrExplanation | string | learner-friendly explanation |
| ExampleSentence | string? | example in a workplace sentence |
| Category | string(50) | enum-backed: workplace_phrase, polite_request, grammar_pattern, connector, tone_softener, project_vocabulary, common_mistake, useful_expression |
| Status | string(20) | New, Practising, Mastered, Ignored |
| Source | string(50) | AiExtractedFromWritingAttempt, ManualLater, SystemLater |
| SeenCount | int | incremented on dedup |
| LastSeenAtUtc | DateTime? | updated on dedup |
| NextReviewAtUtc | DateTime? | placeholder, deferred SR |
| CreatedAt | DateTime | auto |
| UpdatedAt | DateTime | updated on status change / dedup |

**Dedup rule:** same `StudentProfileId` + normalised `Term` (lower, trim) + `Category` = same entry. On collision: increment `SeenCount`, update `LastSeenAtUtc`, update `UpdatedAt`. Do not insert duplicate.

**Existing `VocabularyEntry`** remains unchanged. It is for future SM-2 spaced repetition activity. `StudentVocabularyItem` is the lightweight extracted-phrase model.

---

## AI prompt

**Key:** `vocabulary_extract_from_attempt`

Input template variable: `{{extractionContext}}`

Input JSON shape:
```json
{
  "studentProfile": { "cefrLevel": "B1", "careerProfile": "Document Controller" },
  "activityTitle": "...",
  "moduleTitle": "...",
  "studentSubmission": "...",
  "feedbackChanges": [...],
  "improvedVersion": "...",
  "knownTermsSample": ["could you please", "follow up on"]
}
```

Output JSON shape:
```json
{
  "items": [
    {
      "term": "Could you please",
      "suggestedPhrase": "Could you please send me the updated file?",
      "meaningOrExplanation": "A polite way to make a request in workplace English.",
      "exampleSentence": "Could you please confirm the meeting time?",
      "category": "polite_request",
      "reason": "The student used a direct request and needs softer workplace language."
    }
  ]
}
```

Rules: 0–5 items, prefer tone softeners/phrases/patterns, no private data.

---

## Extraction flow

1. Student submits writing attempt
2. AI evaluates and returns feedback
3. `ActivityAttempt` is saved
4. Memory update (best-effort, 8s timeout) — unchanged
5. **NEW:** Vocabulary extraction (best-effort, 8s timeout) — fires after memory update, does not block response
6. Response returned to student

If extraction fails → log warning with correlation ID → continue. Activity submission is never failed.

---

## API design

### `GET /api/vocabulary`

- Auth required (student)
- Query params: `?status=New|Practising|Mastered|Ignored&category=polite_request`
- Returns: paged or full list of `StudentVocabularyItemDto`

### `PATCH /api/vocabulary/{id}/status`

- Auth required (student)
- Body: `{ "status": "Practising" }`
- Allowed values: New, Practising, Mastered, Ignored
- Student can only update own entries

---

## Frontend

### Route: `/vocabulary`

- Title: "Vocabulary"
- Subtitle: "Review useful words and phrases from your writing practice."
- Sections: Summary cards (New/Practising/Mastered/Total), filters, vocabulary cards, empty state
- Each card: term, suggested phrase, explanation, example, category badge, status, Practise/Mastered/Ignore buttons

### Navigation

- Add to student sidebar and mobile bottom nav (if not too crowded)
- Mobile nav currently has: Dashboard, Practice, My Path, Progress — adding Vocabulary makes 5. Add to sidebar only on mobile nav; document the decision.

### Progress page preview

- Section: "Vocabulary snapshot" — 3 newest/practising entries + link to `/vocabulary`
- Shown only if entries exist

---

## Tests

### Backend integration tests (`VocabularyEndpointTests.cs`)
- GET /api/vocabulary requires auth
- Returns only current student's entries
- Status filter works
- PATCH updates own entry
- PATCH rejects invalid status
- Student cannot update another student's entry

### Backend unit/service tests (`VocabularyExtractionTests.cs`)
- Parses valid JSON
- Caps at 5 items
- Duplicate term+category increments SeenCount
- Extraction failure does not fail activity submission
- AI usage recorded if call attempted

### Frontend unit tests
- Empty state renders
- List renders
- Summary cards count correctly
- Status buttons call API
- No raw JSON

### Playwright tests
- Vocabulary nav item visible
- Vocabulary page loads
- Empty state works
- Mocked entries render
- Status change works

---

## Limitations

- No spaced repetition scheduling in this sprint
- `NextReviewAtUtc` placeholder field only
- No native-language explanations
- No admin vocabulary insights
- No vocabulary quiz/flashcard mode
- Mobile nav gets vocabulary in sidebar only (not bottom nav) to avoid crowding

---

## Deferred work

- `VocabularyPractice` activity type
- Full SM-2 spaced repetition
- Vocabulary quiz modes
- Admin vocabulary insights
- Export/import vocabulary
- Native-language vocabulary explanations
- Listening/speaking modules
- Vocabulary analytics on admin dashboard
