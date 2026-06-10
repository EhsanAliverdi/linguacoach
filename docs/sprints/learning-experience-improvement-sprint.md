---
status: historical
---
# Learning Experience Improvement Sprint

**Date**: 2026-06-05
**Status**: In progress
**Branch**: main

---

## Sprint Goal

Make the existing writing activity feel like a real iterative English coaching experience with strong visual feedback.

The student should feel:
> "I can see exactly what was wrong."
> "I understand why it changed."
> "I can improve my own answer."
> "I want to try again."

---

## Current State (entering this sprint)

The trusted tester release prep sprint stabilised the core product flow:

```
Admin creates student → Student logs in → Onboarding → Dashboard → Writing exercise → Feedback
```

The layout stabilisation sprint fixed all shell/layout issues. The activity page renders three states: learning, writing, and feedback.

The current feedback returned by AI:
- `overallScore` — numeric 0–100
- `correctedEmail` — a fully rewritten version (the dominant output)
- `feedbackInSourceLanguage` — Persian encouragement
- `whatYouDidWell` — list of strengths
- `mainMistakes` — list of mistakes
- `grammarExplanation` — one teaching moment
- `toneExplanation` — one teaching moment
- `vocabularyToRemember` — list of words
- `rewriteChallenge` — challenge sentence
- `nextPracticeSuggestion` — what to practise next

**The problem**: The corrected email is the dominant output. The student receives a perfect version but does not understand *what* changed or *why*. The learning opportunity is missed.

---

## Desired Iterative Coaching Model

The AI should coach the student to improve their own writing, not replace it.

Learning loop:

```
Student writes → Submits → AI returns targeted changes with reasons
→ Student sees what changed and why → Student rewrites → Submits again
→ Each attempt is a new ActivityAttempt → Score improves over time
```

Key principles:

1. Show targeted changes, not just a rewrite.
2. Show why each change matters.
3. Each retry creates a new `ActivityAttempt` — never overwrite.
4. Show attempt number and score trend.
5. If many issues, focus on top 3–5 first ("Focus on these first").
6. Label the improved version clearly as "Suggested improved version", not "Correct answer".

---

## AI Feedback Structure (new)

### Prompt output fields

The `activity_evaluate_writing` prompt now returns the following JSON structure:

```json
{
  "overallScore": 74,
  "coachSummary": "Good effort — your message is clear but the tone needs polishing.",
  "whatYouDidWell": ["Used 'pending approval' correctly", "Clear subject line"],
  "focusFirst": true,
  "changes": [
    {
      "type": "replace",
      "original": "I am agree with you",
      "suggested": "I agree with you",
      "reason": "In English, 'agree' is a verb, so we don't say 'am agree'.",
      "category": "grammar",
      "severity": "high"
    },
    {
      "type": "replace",
      "original": "please send me this today",
      "suggested": "Could you please send this to me today?",
      "reason": "This sounds more polite and professional in a workplace context.",
      "category": "tone",
      "severity": "medium"
    }
  ],
  "grammarIssues": ["Subject-verb agreement error: 'I am agree'"],
  "vocabularyIssues": ["'send me this' is too direct — prefer 'forward this to me'"],
  "toneIssues": ["Direct commands sound impolite — use modal verbs: could, would, please"],
  "clarityIssues": [],
  "feedbackInSourceLanguage": "...(Persian)...",
  "miniLesson": "Use modal verbs (could, would, might) to make requests polite.",
  "nextImprovementStep": "Try rewriting the request sentences using 'Could you please...'",
  "improvedVersion": "...(full suggested improved version)...",
  "rewriteChallenge": "Rewrite your second paragraph using 'Could you please'.",
  "nextPracticeSuggestion": "Practise polite requests in the next activity."
}
```

### Change types

| Type | Meaning |
|---|---|
| `replace` | A phrase was replaced with a better one |
| `add` | A missing element should be added |
| `remove` | An unnecessary phrase should be removed |
| `reorder` | Sentence order should change |

### Categories

| Category | Meaning |
|---|---|
| `grammar` | Grammar error |
| `vocabulary` | Word choice or vocabulary issue |
| `tone` | Formality or politeness issue |
| `clarity` | Unclear or ambiguous sentence |
| `structure` | Paragraph or email structure |
| `punctuation` | Punctuation error |

### Severity

| Severity | Meaning |
|---|---|
| `high` | Must fix — significant communication impact |
| `medium` | Should fix — noticeable to a native speaker |
| `low` | Could improve — minor polish |

---

## Visual Diff UI Direction

The feedback page shows the student's original submission alongside a visual change list, similar to a GitHub/code review diff but styled as a friendly coaching interface.

### Feedback sections (in order)

1. **Score card** — ring score, band label (Great work / Good effort / Keep going), attempt number
2. **Coach summary** — avatar + warm summary message
3. **What you did well** — green checkmarks
4. **Suggested changes** — diff-style change list:
   - Removed text styled distinctly (amber/warm strikethrough background)
   - Added text styled distinctly (green/teal highlight)
   - Each change shows: original → suggested + reason chip + category badge
5. **Persian explanation** — RTL block (dir=rtl lang=fa)
6. **Mini lesson** — compact teaching moment
7. **Suggested improved version** — collapsible, clearly labelled (not "Correct answer")
8. **Try again / Improve my answer button**

### Visual diff style

Do not use harsh red/green only. Use the SpeakPath design token palette:

- Removed text: `--sp-warn-soft` background (`#FFF1DC`), `--sp-warn` colour (`#F0982C`), text-decoration: line-through
- Added text: `--sp-success-soft` background (`#E0F6EE`), `--sp-success` colour (`#13B07C`)
- Category badge: `sp-skill-badge` variants using existing tokens

---

## Retry / Improve Loop

### Behaviour

After feedback, the student sees two action buttons:

- **Improve my answer** (primary) — pre-fills the writing textarea with the student's previous submission so they can edit and resubmit
- **Try again from scratch** (ghost) — clears the textarea

Both actions:
- Keep the same `LearningActivity`
- Call `POST /api/activity/{activityId}/attempt` — creates a new `ActivityAttempt`
- Do NOT overwrite the previous attempt
- Increment attempt number display

### Attempt history (simple)

The UI shows:
```
Attempt 2 of 2  •  Previous: 62 → Now: 74  (+12)
```

If score improved: "Great improvement!"
If score unchanged: "Keep going — try the suggestions above."
If score dropped (edge case): "Don't worry — each attempt is practice."

### Data flow

- Frontend tracks attempt count and previous score in component state (not persisted across page reload for now)
- Backend: each call to submit creates a new `ActivityAttempt` row — no change required in backend logic
- `ActivityAttempt.Id` returned as `attemptId` in the feedback response — already correct

---

## Gradual Improvement Mode

If the student's answer has many issues (`changes.length >= 5`), the AI flags `focusFirst: true` and limits the change list to the 3–5 highest-severity items.

The UI shows:
> "There are more improvements possible. Focus on these first, then try again."

This prevents overwhelming the student when their draft has many problems.

---

## WritingScenario Variety (no new ActivityType)

The AI activity generation prompt is updated to generate varied workplace writing tasks within `ActivityType.WritingScenario`. The activity specifies:

- `context` — situation description
- `audience` — who the student is writing to
- `tone` — expected tone (formal / semi-formal / polite)
- `expectedLength` — approximate word count guidance
- `skillFocus` — e.g. "polite requests", "follow-up emails", "apology"
- `difficultyLevel` — matching student CEFR level

Supported task types now explicitly seeded in the generation prompt:
- workplace email
- Teams / chat message
- incident explanation
- polite request
- complaint response
- meeting follow-up
- apology message
- clarification message
- update to manager
- customer support response

---

## Dashboard Stats (real data)

Simple stats added when backend data supports it:
- Completed activities (count of `ActivityAttempt` rows)
- Latest score
- Average score
- Current module progress

No new analytics engine. No complex aggregations. Simple query on existing `ActivityAttempt` data.

---

## In Scope

- [x] Sprint documentation
- [ ] Updated `activity_evaluate_writing` prompt with structured changes/diff output
- [ ] Extended `ActivityFeedbackPayload` and `ActivityFeedbackDto` with new fields
- [ ] Updated Angular `ActivityFeedbackDto` model
- [ ] Visual diff/change list on feedback page
- [ ] Retry / improve flow (new attempt per retry, attempt counter)
- [ ] Attempt score comparison display
- [ ] Gradual improvement mode (focusFirst flag)
- [ ] WritingScenario variety prompt update
- [ ] Real dashboard stats (activities done, latest score, avg score)
- [ ] Backlog updated with new future items
- [ ] Backend tests for new feedback parsing and retry logic
- [ ] Frontend build passing

---

## Out of Scope

- Speaking / listening / vocabulary / pronunciation activity types
- Old `/api/writing` endpoints
- Full attempt history page or side-by-side diff viewer (future backlog)
- Inline sentence-level comment UI
- Teacher review UI
- Full analytics engine
- Any new major architecture

---

## API Changes

### `POST /api/activity/{activityId}/attempt` (existing)

Response `ActivityFeedbackDto` is extended with new fields:

```json
{
  "attemptId": "...",
  "score": 74,
  "coachSummary": "...",
  "whatYouDidWell": [...],
  "focusFirst": true,
  "changes": [
    {
      "type": "replace",
      "original": "...",
      "suggested": "...",
      "reason": "...",
      "category": "grammar",
      "severity": "high"
    }
  ],
  "grammarIssues": [...],
  "vocabularyIssues": [...],
  "toneIssues": [...],
  "clarityIssues": [...],
  "feedbackInSourceLanguage": "...",
  "miniLesson": "...",
  "nextImprovementStep": "...",
  "improvedVersion": "...",
  "rewriteChallenge": "...",
  "nextPracticeSuggestion": "..."
}
```

Backward compatibility: all new fields are nullable. The `correctedText` field is mapped from `improvedVersion` to preserve the existing `correctedText` field name in the DTO.

---

## Prompt Keys

| Key | Purpose |
|---|---|
| `activity_evaluate_writing` | Evaluate student writing attempt — updated this sprint |
| `activity_generate_writing` | Generate new WritingScenario — updated this sprint (variety) |

---

## DB Changes

None required. `ActivityAttempt.FeedbackJson` stores whatever JSON the AI returns. The new fields are included in the JSON; old attempts without them will return null/empty for new fields.

---

## Frontend Changes

| File | Change |
|---|---|
| `activity.models.ts` | Add new fields to `ActivityFeedbackDto` interface |
| `activity-lesson.component.ts` | Add attempt count, previous score tracking, improved-answer pre-fill |
| `activity-lesson.component.html` | Rebuild feedback phase: diff list, score comparison, retry buttons |
| `styles.css` | Add `.sp-diff-remove`, `.sp-diff-add`, `.sp-diff-item` classes |

---

## Test Plan

### Backend tests

- [ ] `ActivityFeedbackPayload` parses new `changes` array correctly
- [ ] `ActivityFeedbackPayload` parses `coachSummary`, `focusFirst`, `miniLesson`, `nextImprovementStep`, `improvedVersion`, `clarityIssues` correctly
- [ ] Submit attempt → new `ActivityAttempt` row created
- [ ] Submit attempt twice → two separate `ActivityAttempt` rows (no overwrite)
- [ ] Previous attempt data preserved after second submission
- [ ] AI failure → attempt saved with empty feedback, no data loss
- [ ] Fallback feedback has safe default structure (null fields, not exceptions)

### Frontend tests

- [ ] Activity loads in learning state
- [ ] Submit transitions to feedback state
- [ ] Feedback sections render (score, coach summary, changes, Persian block)
- [ ] Change list renders each item (original, suggested, reason)
- [ ] Improved version renders inside collapsible
- [ ] "Improve my answer" button populates textarea with previous submission
- [ ] "Try again" creates a new submission (attempt count increments)
- [ ] Score comparison shows when attempt > 1
- [ ] `focusFirst` flag shows "Focus on these first" message

### E2E (Playwright)

- [ ] Existing smoke test still passes
- [ ] New smoke step: submit → feedback → "Improve my answer" → resubmit → feedback shows attempt 2

---

## Implementation Tasks (ordered)

1. **Sprint doc** — this file ✓
2. **Update `activity_evaluate_writing` prompt** — `DefaultAiSeeder.cs` + migration
3. **Extend `ActivityFeedbackPayload`** — add `changes`, `coachSummary`, `focusFirst`, `miniLesson`, `nextImprovementStep`, `improvedVersion`, `clarityIssues`
4. **Extend `ActivityFeedbackDto`** — match new payload fields, keep `correctedText` mapping
5. **Update `activity_generate_writing` prompt** — variety of task types
6. **Update `ActivityFeedbackDto` interface in Angular**
7. **Rebuild feedback UI** — diff list, attempt counter, retry buttons
8. **Add `sp-diff-*` CSS classes**
9. **Add/update dashboard stats endpoint** — completed activities, scores
10. **Backend tests**
11. **npm run build + playwright test**

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| AI does not reliably return `changes` array | Defensive parsing — empty array if missing; fallback renders standard feedback without diff |
| New prompt fields bloat token usage | Max output tokens increased to 2000; compact field names |
| Old `ActivityAttempt` rows have no `changes` field | Null-safe rendering in Angular; old attempts show legacy feedback UI |
| Score comparison confuses student if attempt order is wrong | Use `attemptId` sequence, not local counter |

---

## Future Follow-up Items (post-sprint)

- Richer attempt history page showing all attempts side by side
- Inline sentence-level comment annotations
- Teacher / admin review of feedback quality
- Skill-based progress analytics derived from `changes.category` data
- Speaking and listening activity types
- Vocabulary extraction from writing attempt mistakes
- Client-side diff algorithm for richer visual comparison (LCS-based)
