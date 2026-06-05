# Student Learning Memory & Adaptive Curriculum Sprint

**Date**: 2026-06-05
**Status**: Planned — engineering review complete
**Branch**: main

---

## Sprint Goal

Introduce compact, database-driven student learning memory and use it to make module and activity generation adaptive.

After this sprint, the app should:

- Remember what the student has already practised
- Track recurring mistakes, strengths, weaknesses, and covered scenarios
- Track skill states per student (weak/strong per skill key)
- Avoid repeating modules with the same scenario type + audience + communication mode
- Generate the next 3–5 recommended modules based on memory and progress
- Update memory after each evaluated attempt (best-effort, never blocks feedback)
- Show the student a friendly "Your learning focus" summary

---

## Current Problem

The current model is close to:

```
Student profile → AI → modules
```

This is a static path. After completing modules, the student gets another generic path.
There is no memory of what was practised, what went wrong, or what to prioritise next.

The `ActivityGenerationContext.RecentMistakesSummary` field exists but is never populated.
`StudentProgressService.GetCurrentFocusAreaAsync()` computes focus area but the result is not
persisted or fed back into generation.

---

## What Already Exists

| Component | Existing state | Sprint action |
|---|---|---|
| `UserLearningSummary` | `RecentWeaknesses` + `RecentProgress` text fields | Extend with 6 new JSON fields |
| `ActivityGenerationContext.RecentMistakesSummary` | Defined, never populated | Populate from memory |
| `StudentProgressService.GetCurrentFocusAreaAsync()` | Computes focus area from feedback | Use to bootstrap memory for existing learners |
| `AiActivityGeneratorHandler.ExecuteWithFallbackAsync()` | Private, embedded | Extract to `AiExecutionService` |
| `AiLearningPathGeneratorHandler.BuildDtoAsync()` | Private, embedded | Extract to `LearningPathDtoBuilder` |
| `AiProviderResolver.ResolveWithFallback()` | Works for any feature key | New prompts pass new keys |
| `LearningModule` | No fingerprint | Add `fingerprint_json` nullable column |

---

## Architecture Decisions

### D1 — Extend UserLearningSummary, do not create a new entity
Adding 6 new JSON fields to the existing `user_learning_summaries` table.
One migration, no duplicate entity, existing code unchanged.

### D2 — Fingerprint as JSON column on LearningModule
`fingerprint_json` nullable column on `learning_modules`.
No separate table, no JOIN required. Queryable in C# after deserialization.

### D3 — Memory update: synchronous with 8-second timeout
After `ActivityAttempt` is saved, `StudentMemoryService.UpdateMemoryAsync()` is called
with a `CancellationTokenSource` deadline of 8 seconds.
If it times out or fails: log with correlation ID, continue, return feedback to student.

### D4 — New prompt key: `learning_path_generate_adaptive`
Separate from `learning_path_generate`. Different input shape. Independent versioning.

### D5 — Extract `AiExecutionService`
Shared fallback execution pattern extracted from `AiActivityGeneratorHandler`.
Used by: `AiActivityGeneratorHandler`, `StudentMemoryService`, `AdaptivePathGeneratorHandler`.

### D6 — Extract `LearningPathDtoBuilder`
Shared DTO construction extracted from `AiLearningPathGeneratorHandler`.
Used by: `AiLearningPathGeneratorHandler`, `AdaptivePathGeneratorHandler`.

### D7 — Concurrency on generate-next: xmin optimistic concurrency token
`LearningPath` gets PostgreSQL `xmin` concurrency token via EF Core.
Concurrent `generate-next` calls: second write throws `DbUpdateConcurrencyException` → 409.

### D8 — Domain mutation: `UserLearningSummary.ApplyDelta(MemoryUpdateDeltaDto)`
Single domain method enforces list caps (max 10 strengths/weaknesses/mistakes, max 20 scenarios, max 5 next focus).

### D9 — StudentSkillProfile: simplified (no score, no evidence_count in this sprint)
New table with `skill_key`, `is_weak`, `last_updated_utc`. No numeric score this sprint.
Narrative form (strong/weak skills) lives in `UserLearningSummary` JSON.
Structured form (skill key + weak flag) lives in `StudentSkillProfile`.

### D10 — Bootstrap existing learners: first-call get-or-create
First `UpdateMemoryAsync` or `GenerateNextAsync` call for a learner with no `UserLearningSummary`
seeds initial memory from `StudentProgressService.GetCurrentFocusAreaAsync()` and recent attempt feedback.
No background migration job needed.

---

## Data Model

### UserLearningSummary — new fields

```sql
ALTER TABLE user_learning_summaries ADD COLUMN journey_summary TEXT;
ALTER TABLE user_learning_summaries ADD COLUMN strong_skills_json TEXT NOT NULL DEFAULT '[]';
ALTER TABLE user_learning_summaries ADD COLUMN weak_skills_json TEXT NOT NULL DEFAULT '[]';
ALTER TABLE user_learning_summaries ADD COLUMN recurring_mistakes_json TEXT NOT NULL DEFAULT '[]';
ALTER TABLE user_learning_summaries ADD COLUMN covered_scenarios_json TEXT NOT NULL DEFAULT '[]';
ALTER TABLE user_learning_summaries ADD COLUMN next_focus_json TEXT NOT NULL DEFAULT '[]';
```

Domain invariants enforced by `ApplyDelta()`:

```
strong_skills: max 10 items
weak_skills: max 10 items
recurring_mistakes: max 10 items
covered_scenarios: max 20 items (case-insensitive dedup)
next_focus: max 5 items
```

### StudentSkillProfile — new table

```sql
CREATE TABLE student_skill_profiles (
    id UUID PRIMARY KEY,
    student_profile_id UUID NOT NULL REFERENCES student_profiles(id),
    skill_key VARCHAR(100) NOT NULL,
    skill_label VARCHAR(200) NOT NULL,
    is_weak BOOLEAN NOT NULL DEFAULT FALSE,
    last_updated_utc TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ix_student_skill_profiles_student_key
    ON student_skill_profiles(student_profile_id, skill_key);
```

Skill keys:

```
grammar_accuracy       formal_tone            sentence_clarity
message_structure      workplace_vocabulary   concise_writing
softening_language     summarising_information clarifying_questions
escalation_language
```

### LearningModule — new column

```sql
ALTER TABLE learning_modules ADD COLUMN fingerprint_json TEXT;
```

Fingerprint JSON shape:

```json
{
  "communicationMode": "email",
  "scenarioType": "delay_explanation",
  "audience": "manager",
  "tone": "professional_apologetic",
  "difficulty": "B1",
  "grammarFocus": "past_simple_future_actions",
  "vocabularyTheme": "project_schedule"
}
```

### LearningPath — concurrency token

```
xmin column mapped via EF Core UseXminAsConcurrencyToken()
```

---

## New AI Prompts

### `student_memory_update`

Input:

```json
{
  "studentProfile": { "cefrLevel": "B1", "careerProfile": "Document Controller" },
  "currentMemory": {
    "journeySummary": "...",
    "strongSkills": [...],
    "weakSkills": [...],
    "recurringMistakes": [...],
    "coveredScenarios": [...],
    "nextRecommendedFocus": [...]
  },
  "activityMetadata": { "scenarioType": "delay_explanation", "communicationMode": "email" },
  "submittedText": "...",
  "evaluationFeedback": { "score": 72, "coachSummary": "...", "changes": [...] }
}
```

Output (delta — not full rewrite):

```json
{
  "journeySummaryDelta": "...",
  "newStrengths": [...],
  "newWeaknesses": [...],
  "recurringMistakesToAdd": [...],
  "coveredScenariosToAdd": [...],
  "weakSkillKeys": ["formal_tone", "sentence_clarity"],
  "strongSkillKeys": ["workplace_vocabulary"],
  "recommendedNextFocus": [...]
}
```

### `learning_path_generate_adaptive`

Input:

```json
{
  "studentProfile": { "cefrLevel": "B1", "careerProfile": "Document Controller" },
  "memory": {
    "journeySummary": "...",
    "strongSkills": [...],
    "weakSkills": [...],
    "recurringMistakes": [...],
    "coveredScenarios": [...],
    "nextRecommendedFocus": [...]
  },
  "skillProfile": [
    { "skillKey": "formal_tone", "isWeak": true },
    { "skillKey": "workplace_vocabulary", "isWeak": false }
  ],
  "existingFingerprints": [
    { "scenarioType": "delay_explanation", "audience": "manager", "communicationMode": "email" }
  ],
  "moduleCount": 4
}
```

Output:

```json
{
  "journeySummary": "These modules focus on polite escalation...",
  "modules": [
    {
      "order": 1,
      "title": "Softening Manager Requests",
      "description": "...",
      "focusSkill": "softening_language",
      "reason": "Recent attempts showed direct requests.",
      "difficulty": "B1+",
      "fingerprint": {
        "communicationMode": "teams_message",
        "scenarioType": "polite_request",
        "audience": "manager",
        "tone": "polite_professional",
        "grammarFocus": "modal_verbs",
        "vocabularyTheme": "project_information"
      },
      "avoidsRepeating": ["delay explanation email to manager"]
    }
  ]
}
```

---

## Memory Update Flow

```
ActivitySubmitHandler.HandleAsync()
  │
  ├── [existing] EvaluateAttemptAsync() → feedbackJson
  ├── [existing] new ActivityAttempt() → SaveChangesAsync()
  │
  └── [new] StudentMemoryService.UpdateMemoryAsync() ← try/catch, 8s timeout
              │
              ├── GetOrCreateWithBootstrapAsync()
              │     └── if new: seed from StudentProgressService.GetCurrentFocusAreaAsync()
              │
              ├── Load StudentSkillProfile rows (student_profile_id = ?)
              │
              ├── Build compact context packet (≤ 1000 tokens)
              │
              ├── AiExecutionService.ExecuteWithFallbackAsync("student_memory_update", ...)
              │     ├── try primary → AiUsageLog(isFallback=false)
              │     └── catch → try fallback → AiUsageLog(isFallback=true)
              │
              ├── Parse delta JSON
              │
              ├── UserLearningSummary.ApplyDelta(delta)
              │     ├── merge strengths (cap 10)
              │     ├── merge weaknesses (cap 10)
              │     ├── merge mistakes (cap 10, dedup)
              │     ├── merge scenarios (cap 20, case-insensitive dedup)
              │     └── replace next focus (cap 5)
              │
              ├── upsert StudentSkillProfile rows
              │
              └── SaveChangesAsync()

On any exception: _logger.LogWarning(..., correlationId), return (don't rethrow)
```

---

## Adaptive Generation Flow

```
POST /api/learning-path/generate-next
  │
  └── AdaptivePathGeneratorHandler.GenerateNextAsync()
        │
        ├── Load StudentProfile + active LearningPath + existing modules + fingerprints
        ├── Load UserLearningSummary (get or create with bootstrap)
        ├── Load StudentSkillProfile rows
        │
        ├── Build adaptive context packet
        │
        ├── AiExecutionService.ExecuteWithFallbackAsync("learning_path_generate_adaptive", ...)
        │
        ├── Parse modules with fingerprints
        │
        ├── Dedup check per generated module:
        │     ├── fingerprint match: same scenarioType + audience + communicationMode?
        │     └── title match: normalised (lower, collapse whitespace) first 6 words overlap?
        │     └── if duplicate → skip
        │
        ├── Concurrency guard:
        │     └── re-query COUNT(modules) WHERE learning_path_id = ?
        │         if changed → throw ConcurrencyException → 409
        │
        ├── SaveChangesAsync() → DbUpdateConcurrencyException → 409
        │
        └── LearningPathDtoBuilder.BuildAsync() → return LearningPathDto
```

---

## API Changes

### New: POST /api/learning-path/generate-next

- Auth: Student (own path only)
- Request: empty body or `{ "pathId": "..." }` (optional override)
- Response 200: `LearningPathDto` with new modules appended
- Response 409: `{ "error": "Path was updated concurrently. Please refresh." }`
- Response 503: `{ "error": "AI unavailable.", "correlationId": "..." }`

### New: GET /api/learning-path/memory

- Auth: Student (own memory only)
- Response 200:

```json
{
  "journeySummary": "...",
  "strongSkills": [...],
  "weakSkills": [...],
  "recurringMistakes": [...],
  "nextRecommendedFocus": [...],
  "coveredScenarioCount": 7,
  "skillProfile": [
    { "skillKey": "formal_tone", "skillLabel": "Polite workplace tone", "isWeak": true }
  ]
}
```

### New: GET /api/admin/students/{studentId}/learning-memory

- Auth: Admin only
- Response 200: same shape as above
- Response 404: student not found

---

## Frontend Changes

### Dashboard / /my-path — "Your learning focus" panel

Show when `UserLearningSummary` exists with at least one non-empty field.

```
Your learning focus

You are improving at basic workplace updates. Your next focus is
polite tone and concise follow-up messages.

Strengths
• Clear main message
• Useful workplace vocabulary

Practise next
• Softening requests
• Shorter professional sentences
• Summarising meeting outcomes
```

Rules:
- No raw JSON exposed
- Collapsed by default on mobile (expandable)
- Show "Building your profile..." if memory is empty

### Module cards on /my-path

Add:
- `focusSkill` label if present
- `reason` text if present (e.g. "Recommended because recent attempts show direct tone")
- `difficulty` badge

### "Generate next modules" button

Show when: active path has ≥ 1 completed module AND no upcoming modules exist.

Behaviour:
- Calls `POST /api/learning-path/generate-next`
- Loading state: disabled button + spinner
- Success: scroll to new modules
- 409: "Path updated — refreshing..." then reload
- 503: "AI is temporarily unavailable. [Correlation ID: ...] Please try again."

---

## Curriculum Map (Seeded, Static)

A simple seeded curriculum map for Workplace Writing, stored as seed data or JSON config.

Three levels: B1, B1+, B2

Skills per level:

```
B1:   simple work updates, requesting information, explaining problems,
      confirming details, writing short emails, summarising simply

B1+:  explaining delays professionally, softening requests,
      writing follow-up emails, reporting progress,
      clarifying misunderstandings, summarising meeting outcomes

B2:   escalating diplomatically, writing recommendations,
      challenging decisions politely, writing incident reports,
      comparing options, negotiating deadlines
```

Used as context in `learning_path_generate_adaptive` prompt.
Not a full admin editor — static seed for this sprint.

---

## Test Plan

### Backend

#### UserLearningSummary entity

- `ApplyDelta()` — happy path: all 5 field types updated
- `ApplyDelta()` — strong skills capped at 10
- `ApplyDelta()` — recurring mistakes deduplicated (same string)
- `ApplyDelta()` — covered scenarios deduplicated case-insensitively
- `ApplyDelta()` — next focus capped at 5
- `ApplyDelta()` — null/empty delta fields skipped gracefully

#### StudentSkillProfile entity

- constructor rejects empty `skill_key`
- `is_weak` sets correctly
- unique index: duplicate insert for same student + skill key → DB exception

#### AiExecutionService

- primary succeeds → logs usage, returns response
- primary fails → fallback configured → fallback succeeds → usage logged with `isFallback=true`
- primary fails → no fallback → throws `AiUnavailableException`
- usage logged for both primary failure and fallback success

#### StudentMemoryService

- `GetOrCreateWithBootstrapAsync()` creates new record with seeded weak skills from focus area
- `GetOrCreateWithBootstrapAsync()` returns existing record unchanged on second call
- `UpdateMemoryAsync()` happy path: delta applied, skill profiles upserted
- `UpdateMemoryAsync()` AI call fails → logs with correlation ID → no exception thrown
- `UpdateMemoryAsync()` AI returns invalid JSON → logs → no exception thrown
- `UpdateMemoryAsync()` AI usage recorded even on failure

#### ActivitySubmitHandler integration

- attempt saved and feedback returned even when memory update fails
- memory update timeout (simulated slow AI) → feedback still returned
- memory update called after attempt is saved (not before)

#### AdaptivePathGeneratorHandler

- generates 3–5 modules appended with correct order numbers
- deduplicates by fingerprint (same scenarioType + audience + communicationMode rejected)
- deduplicates by normalised title (first 6 words match → rejected)
- AI unavailable → 503 with correlation ID
- AI usage recorded
- `fingerprint_json` persisted on each new module
- concurrency: `DbUpdateConcurrencyException` → handler returns 409

#### Access control

- `GET /api/learning-path/memory` unauthenticated → 401
- `POST /api/learning-path/generate-next` unauthenticated → 401
- `GET /api/admin/students/{id}/learning-memory` student role → 403
- `GET /api/admin/students/{id}/learning-memory` admin role → 200
- student cannot call generate-next for another student's path → 403

### Frontend (Playwright)

- submit activity → learning focus panel appears on dashboard
- `/my-path` shows personalised focus summary
- generate-next button appears when path has completed modules
- generate-next adds new module cards
- generate-next AI unavailable shows message with correlation ID
- no raw JSON rendered anywhere
- existing smoke flow (admin → student → activity → feedback) still passes

---

## Failure Modes

| Failure | Test covers? | Error handling? | User sees? |
|---|---|---|---|
| Memory update AI timeout (8s) | Yes | Yes — continue with feedback | Nothing (silent) |
| Memory update AI invalid JSON | Yes | Yes — log, continue | Nothing |
| generate-next AI unavailable | Yes | Yes — 503 with correlationId | Friendly message |
| generate-next DB concurrency | Yes | Yes — 409 | "Refresh and retry" |
| generate-next produces 0 non-duplicate modules | No | Partial — returns current path | No new modules added |
| Memory drift from repeated failures | No | No retry/staleness flag | Nothing (silent drift) |

**Known limitation / deferred:** no staleness flag or retry queue for memory update failures.
If AI is down for an extended period, `UserLearningSummary` will not update but will not break.
This is acceptable for this sprint. Add staleness detection to backlog.

---

## Known Limitations

- Memory update AI failures are silently logged — no retry, no alert, no staleness indicator
- First adaptive generation for an existing learner has sparse context (bootstrapped from focus area only)
- Dedup fingerprint matching can miss semantically equivalent scenarios with different wording
- No admin curriculum map editor — curriculum is seeded/static
- No score tracking on StudentSkillProfile (deferred to future sprint)
- Generate-next button trigger condition is simple (completed modules count) — future: smarter trigger

---

## Out of Scope

- Speaking / listening / vocabulary / pronunciation activities
- Full admin curriculum designer
- Background job for memory update
- Numeric skill scores (0-100) — deferred from StudentSkillProfile
- Streak tracking, dashboard stat tiles (backlog)
- Memory export or student-facing history of memory updates

---

## Implementation Tasks

```
Phase 1 — Data model and infrastructure (no UI, parallelisable)
  T1  Extract AiExecutionService from AiActivityGeneratorHandler
  T2  Extract LearningPathDtoBuilder from AiLearningPathGeneratorHandler
  T3  Extend UserLearningSummary entity: new fields + ApplyDelta()
  T4  Add StudentSkillProfile entity + configuration + migration
  T5  Add fingerprint_json to LearningModule + migration
  T6  Add xmin concurrency token to LearningPath + migration
  T7  Add UserLearningSummaryConfiguration updates + migration

Phase 2 — Memory update (depends on Phase 1)
  T8  Add student_memory_update prompt to DB seed
  T9  Implement StudentMemoryService
  T10 Wire memory update into ActivitySubmitHandler (try/catch, 8s timeout)
  T11 Backend tests for memory update (entity, service, integration)

Phase 3 — Adaptive generation (depends on Phase 1, parallelisable with Phase 2)
  T12 Add learning_path_generate_adaptive prompt to DB seed
  T13 Implement AdaptivePathGeneratorHandler
  T14 Add POST /api/learning-path/generate-next endpoint
  T15 Add GET /api/learning-path/memory endpoint
  T16 Add GET /api/admin/students/{id}/learning-memory endpoint
  T17 Backend tests for adaptive generation (dedup, concurrency, access control)

Phase 4 — UI (depends on Phase 2 + 3)
  T18 Learning focus panel on dashboard/my-path
  T19 Module card enrichment (focusSkill, reason, difficulty)
  T20 Generate next modules button + loading/error states
  T21 Playwright tests

Phase 5 — Final
  T22 dotnet test — all pass
  T23 npm run build — clean
  T24 npx playwright test — all pass
  T25 Update docs/backlog/product-backlog.md
  T26 Update docs/architecture/student-learning-memory.md
```

---

## Worktree Parallelisation

| Workstream | Modules touched | Depends on |
|---|---|---|
| Lane A: Data model + AiExecutionService | Domain, Persistence, Infrastructure/Ai | — |
| Lane B: Memory update service + prompt | Infrastructure/Activity, Persistence | Lane A complete |
| Lane C: Adaptive generation + endpoints | Infrastructure/LearningPath, Api | Lane A complete |
| Lane D: UI + Playwright | Web | Lane B + C endpoints deployed |

Launch A first. Then B and C in parallel. Then D.

---

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Codex Review | `/codex review` | Independent 2nd opinion | 1 | issues_found | 7 findings, 3 actioned (D11/D12/D13) |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | CLEAR | 13 decisions made, 0 unresolved |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | — | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | — | — |

**CROSS-MODEL:** Outside voice and eng review agreed on xmin concurrency token (D12), simplified StudentSkillProfile (D11), and first-call bootstrap (D13). All three incorporated.

**UNRESOLVED:** 0

**VERDICT:** ENG CLEARED — ready to implement. Consider /plan-design-review before shipping the UI phase (T18-T20).
