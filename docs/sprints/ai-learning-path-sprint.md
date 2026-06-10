---
status: historical
---
# AI Learning Path Sprint

## Sprint Name

**AI Learning Path Sprint** — wire `LearningPath` and `LearningModule` into the live student flow so SpeakPath feels like a personalised learning journey, not a one-shot activity generator.

---

## Current State (as of sprint start)

- `/activity` is the sole student-facing writing/activity path.
- `LearningActivity`, `LearningPath`, `LearningModule`, and `ActivityAttempt` domain entities and DB tables all exist (migration `T18_LearningActivities`).
- `ActivityGetHandler` ignores `LearningPath` and `LearningModule` entirely. It picks an activity type from the query param, calls AI with no module context, and persists a standalone `LearningActivity` (no `LearningModuleId`).
- `DashboardQueryHandler` returns `studentName`, `cefrLevel`, `careerProfile`, `message` — no path or module data.
- `IAiActivityGenerator.GenerateActivityContentAsync` already accepts a `TopicHint` parameter but it is never populated.
- No `LearningPath` rows exist in production or test databases.
- The legacy `writing_scenarios` and `writing_submissions` tables remain (data safety). `LearningActivity.SourceWritingScenarioId` still references `WritingScenario` for SystemFallback rows.

---

## Product Goal

Make SpeakPath feel like a personalised English learning app with a journey, not just one activity at a time.

A student should be able to see:
- their active learning path title ("Workplace English for Document Controllers — B1")
- which module they are currently in ("Module 2 of 5: Formal email writing")
- how far along they are in that module
- the next recommended activity, contextualised to their current module

---

## Architecture Decision

**Path generation is server-initiated, not student-initiated.**

1. When a student completes onboarding, the API triggers `ILearningPathGenerator.GenerateAsync(StudentProfile)`.
2. AI returns a structured JSON: path title + 5 ordered modules (title, description).
3. If AI fails, `DefaultPathFactory` creates a safe hard-coded Workplace English path — no 500s.
4. The generated `LearningPath` and `LearningModules` are persisted immediately.
5. `ActivityGetHandler` is updated to read the student's active path, determine the current module (lowest order module with fewest completed attempts), and pass the module title/description as `TopicHint` to AI generation.
6. `LearningActivity` rows are now linked to a module via `LearningModuleId`.

**Lazy fallback:** If a student has no active `LearningPath` (e.g. existing test students), `ActivityGetHandler` generates a default path lazily on first `GET /api/activity/next`.

**Dashboard data:** `DashboardQueryHandler` is extended to include a `learningPath` block — path title, current module, module progress counts.

---

## In Scope

- `ILearningPathGenerator` interface and application contracts
- `AiLearningPathGeneratorHandler` — AI-based path generation
- `DefaultPathFactory` — safe hard-coded fallback (5 Workplace English modules)
- Trigger path generation on onboarding complete
- Lazy path generation in `ActivityGetHandler` if no active path exists
- `ActivityGetHandler` reads current module and passes it as `TopicHint`
- `LearningActivity` rows linked to `LearningModuleId`
- `GET /api/learning-path` — returns active path + module progress
- `DashboardQueryHandler` extended with `learningPath` summary
- Dashboard UI: path title, current module title, progress bar ("Module 1 of 5 · 2 activities done")
- Angular `LearningPathService`
- Seed `learning_path_generate` prompt into `AiPrompt` table
- Unit tests: `LearningPath`, `LearningModule`, `DefaultPathFactory`
- Integration tests: fallback path generation, module-aware activity context, dashboard path data
- `WritingScenario` activity type only

---

## Out of Scope

- Speaking, Listening, Vocabulary, Pronunciation, Reading activity types (future sprints)
- Student-visible module navigation (jumping between modules)
- Path regeneration when student advances beyond all modules
- Admin path management UI
- Spaced repetition scheduling tied to module position
- Dropping `writing_scenarios` / `writing_submissions` tables (T19)
- Removing `SourceWritingScenarioId` from `LearningActivity` (T19 dependency)
- CEFR level re-assessment mid-path

---

## API Changes

### Extended: `GET /api/dashboard`

Adds `learningPath` field to existing response:

```json
{
  "studentName": "Ali",
  "cefrLevel": "B1",
  "careerProfile": "Document Controller",
  "message": "...",
  "learningPath": {
    "pathId": "uuid",
    "title": "Workplace English for Document Controllers — B1",
    "currentModule": {
      "moduleId": "uuid",
      "title": "Email writing for approvals",
      "description": "Practice formal request and follow-up emails",
      "order": 1,
      "completedActivities": 2,
      "totalActivities": 5
    },
    "modulesCompleted": 0,
    "totalModules": 5
  }
}
```

`learningPath` is `null` if no active path exists yet (path generation still in progress or lazy).

### New: `GET /api/learning-path`

Returns the student's full active learning path with per-module progress.

```json
{
  "pathId": "uuid",
  "title": "Workplace English for Document Controllers — B1",
  "isActive": true,
  "modules": [
    {
      "moduleId": "uuid",
      "title": "Email writing for approvals",
      "description": "Practice formal request and follow-up emails",
      "order": 1,
      "completedActivities": 3,
      "totalActivities": 5,
      "isCurrent": true
    }
  ]
}
```

Returns `404` if no active path exists.

### Updated: `GET /api/activity/next`

No change to the endpoint signature. Internally, the handler now:
1. Loads the student's active `LearningPath` → current module.
2. Passes `TopicHint` = `"{module.Title}: {module.Description}"` to AI generation.
3. Persists the returned `LearningActivity` with `LearningModuleId` set.

---

## Prompt Keys

| Key | Purpose | Expected AI response shape |
|-----|---------|---------------------------|
| `learning_path_generate` | Given student's CEFR level, career context, language pair, skill focus → generate a 5-module learning path | `{ "pathTitle": "...", "modules": [ { "title": "...", "description": "...", "order": 1 } ] }` |

The existing `activity_generate_writing` prompt already has a `topicHint` slot and does not need a new key.

---

## DB Changes

**No new migrations required.** All tables exist from `T18_LearningActivities`.

The only change is that `LearningActivity.LearningModuleId` (already a nullable FK) is now populated for newly generated activities.

Module completion is derived at query time from attempt counts — no new columns needed this sprint.

### Future (T19 — not this sprint)

- Remove `LearningActivity.SourceWritingScenarioId` FK dependency
- Backfill `LearningActivity` content if needed from `writing_scenarios`
- Drop `writing_scenarios` and `writing_submissions` tables after backup/export confirmation

---

## Frontend Changes

### `DashboardComponent`

- Add `learningPath` to `DashboardResponse` model
- Show path title as eyebrow above hero card ("Your learning path")
- Replace generic hero card copy with current module title and description
- Add progress line: "Module 1 of 5 · 2 activities done"
- "Start activity" CTA remains, routes to `/activity`

### New: `LearningPathService`

- `GET /api/learning-path` → `LearningPathDto`

### New: `LearningPathComponent` (optional for this sprint)

- Full module list view accessible from dashboard
- Shows all 5 modules with status (completed, current, locked)
- Route: `/learning-path`

> If time-boxed, defer the full module list view and ship only the dashboard card changes.

### Angular models to add

```typescript
interface LearningPathSummary {
  pathId: string;
  title: string;
  currentModule: LearningModuleSummary | null;
  modulesCompleted: number;
  totalModules: number;
}

interface LearningModuleSummary {
  moduleId: string;
  title: string;
  description: string;
  order: number;
  completedActivities: number;
  totalActivities: number;
}
```

---

## Test Plan

### Unit Tests

| Test | Layer | Covers |
|------|-------|--------|
| `LearningPathTests` — valid construction | Domain | Title required, `IsActive = true` on create, `Deactivate()` sets false |
| `LearningPathTests` — invalid StudentProfileId | Domain | Empty GUID throws `ArgumentException` |
| `LearningModuleTests` — order validation | Domain | Negative order throws; zero allowed |
| `DefaultPathFactory_ReturnsValidPath` | Application | Returns 5 modules with non-empty titles and descriptions |
| `DefaultPathFactory_ModulesAreOrdered` | Application | Module orders are 1–5, sequential |

### Integration Tests

| Test | Covers |
|------|--------|
| `GeneratePath_WhenAiUnavailable_UsesFallback` | `ILearningPathGenerator` AI throws → DB gets 5-module default path, no 500 |
| `GetNextActivity_AfterPathGenerated_UsesModuleContext` | Activity generation context includes module title as `TopicHint` |
| `GetNextActivity_WhenNoActivePath_GeneratesDefaultPathLazily` | Student with no path gets one created before activity returned |
| `GetDashboard_IncludesLearningPathSummary` | Dashboard response has `learningPath` block with `title`, `currentModule`, `modulesCompleted` |
| `GetLearningPath_ReturnsActivePathWithModules` | `GET /api/learning-path` returns 5 modules with progress counts |
| `GetLearningPath_WhenNoPath_Returns404` | No active path → 404 |

---

## Implementation Tasks

| Task | Description | Layer |
|------|-------------|-------|
| **T20** | Application layer: `ILearningPathGenerator` interface, `GenerateLearningPathCommand`, `LearningPathDto`, `LearningModuleDto` | Application |
| **T21** | Infrastructure: `AiLearningPathGeneratorHandler` (calls AI, parses JSON) + `DefaultPathFactory` (safe 5-module Workplace English fallback) | Infrastructure |
| **T22** | Seed `learning_path_generate` prompt in `AiPromptSeeder` | Persistence |
| **T23** | Trigger path generation after onboarding completes; add lazy generation in `ActivityGetHandler` if student has no active path | Infrastructure |
| **T24** | Update `ActivityGetHandler` — load active module, pass as `TopicHint`, set `LearningModuleId` on persisted `LearningActivity` | Infrastructure |
| **T25** | Update `DashboardQueryHandler` — include `LearningPath` summary (path title, current module, progress counts) | Infrastructure |
| **T26** | Add `GET /api/learning-path` controller and handler | API + Infrastructure |
| **T27** | Angular: add `LearningPathService`, update `DashboardResponse` model, update dashboard component HTML with path title, module, progress bar | Frontend |
| **T28** | Unit tests: `LearningPath`, `LearningModule`, `DefaultPathFactory` | Tests |
| **T29** | Integration tests: fallback path generation, module-aware activity context, dashboard path data, learning-path endpoint | Tests |

---

## Risks and Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| AI path generation adds 2–5 s latency at onboarding complete | Medium | Fire-and-forget async; student lands on dashboard immediately. Show "Generating your path…" skeleton if path not ready |
| Existing students and test users have no `LearningPath` row | Low | Lazy generation in `ActivityGetHandler` on first call; default path is instant (no AI) |
| `learning_path_generate` prompt not seeded → generation fails silently | High | Sprint includes prompt seeding; fallback fires and student gets default path anyway |
| "Current module" calculation is undefined if student completes all modules | Medium | Clamp to last module; show "path complete" state on dashboard (future sprint) |
| Module attempt count query is expensive without index | Low | Existing index on `activity_attempts(student_profile_id)` covers it; re-evaluate at scale |
| `SourceWritingScenarioId` FK blocks T19 clean-up | Low | Documented below as follow-up; not blocking this sprint |

---

## Future Follow-Up Items

### T19 — Drop Legacy Writing Tables (deferred, separate sprint)

1. Take a database backup / export `writing_scenarios` and `writing_submissions` to cold storage.
2. Product owner confirms no historical submission data is needed in the live app.
3. Remove `LearningActivity.SourceWritingScenarioId` FK — backfill any `SystemFallback` content if needed.
4. Remove `WritingScenario` domain entity (currently kept as FK target).
5. Create EF migration `T19_DropLegacyWritingTables`.
6. Review and merge.

### Future Activity Types

Each new activity type requires:
1. A new prompt key (e.g. `activity_generate_speaking`, `activity_evaluate_speaking`).
2. A new JSON content schema documented on `LearningActivity`.
3. A new Angular component (e.g. `SpeakingSessionComponent` already exists as a stub).
4. Extend `ActivityGetHandler` content-type dispatch to deserialise the new payload shape.
5. No domain or DB changes — the JSONB column is intentionally schema-less.

Planned future types:
- `SpeakingRolePlay` — role-play dialogue with STT/TTS
- `ListeningComprehension` — AI-narrated audio + comprehension questions
- `VocabularyPractice` — spaced repetition flashcard sessions
- `PronunciationPractice` — TTS target + STT student recording comparison
- `ReadingTask` — passage + comprehension + vocabulary extraction

### Path Regeneration

When a student advances significantly in CEFR level or completes all modules, regenerate the learning path with updated goals. Design: deactivate old path, generate new path, preserve attempt history.

### Admin Path Management

Admin UI to view, override, or regenerate a specific student's learning path. Out of scope until enough students exist to warrant it.
