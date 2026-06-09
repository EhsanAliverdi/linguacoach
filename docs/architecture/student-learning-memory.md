---
status: current
lastUpdated: 2026-06-09 13:11
owner: architecture
supersedes:
supersededBy:
---

# Architecture: Student Learning Memory

## Why this component exists

SpeakPath generates learning paths from a student's profile at onboarding. Without memory,
every subsequent generation starts from the same static profile — the system has no idea
what the student has already practised, what keeps going wrong, or what to prioritise next.

`StudentLearningMemory` (implemented as extensions to `UserLearningSummary` + new
`StudentSkillProfile` table) is the compact, database-driven summary that gets passed to
AI instead of raw attempt history.

**Key invariant:** Full attempt history lives in `ActivityAttempt`. The memory layer is a
rolling summary — never a copy of the history.

---

## Data flow

```
WRITE PATH (after each activity attempt)
═══════════════════════════════════════════════════════════
ActivitySubmitHandler
  │
  ├── evaluate + save ActivityAttempt   ← unchanged
  │
  └── StudentMemoryService.UpdateMemoryAsync()   ← NEW (best-effort, 8s timeout)
        │
        ├── load UserLearningSummary (get or create)
        ├── load StudentSkillProfile rows
        ├── build compact context for AI (≤ 1000 tokens)
        ├── AiExecutionService → "student_memory_update" prompt
        ├── parse delta
        ├── UserLearningSummary.ApplyDelta()
        ├── upsert StudentSkillProfile rows
        └── SaveChangesAsync()

        On failure: log warning with correlationId, continue (never blocks feedback)

READ PATH (adaptive generation)
═══════════════════════════════════════════════════════════
AdaptivePathGeneratorHandler
  │
  ├── load UserLearningSummary
  ├── load StudentSkillProfile rows
  ├── load LearningModule.fingerprint_json for existing modules
  ├── build adaptive context packet
  ├── AiExecutionService → "learning_path_generate_adaptive" prompt
  ├── dedup new modules against existing fingerprints
  ├── SaveChangesAsync() (with xmin concurrency guard on LearningPath)
  └── LearningPathDtoBuilder.BuildAsync()
```

---

## Database tables

```
user_learning_summaries (extended)
  id, student_profile_id (unique), created_at, updated_at
  recent_weaknesses (legacy text)        ← kept, not removed
  recent_progress (legacy text)          ← kept, not removed
  journey_summary (text, nullable)       ← NEW
  strong_skills_json (text, default '[]') ← NEW
  weak_skills_json (text, default '[]')   ← NEW
  recurring_mistakes_json (text, '[]')    ← NEW
  covered_scenarios_json (text, '[]')     ← NEW
  next_focus_json (text, '[]')            ← NEW

student_skill_profiles (new)
  id, student_profile_id, skill_key (varchar 100), skill_label (varchar 200)
  is_weak (bool), last_updated_utc, created_at
  UNIQUE (student_profile_id, skill_key)

learning_modules (extended)
  ... existing columns ...
  fingerprint_json (text, nullable)      ← NEW

learning_paths (extended)
  ... existing columns ...
  xmin (PostgreSQL system column, mapped as concurrency token)
```

---

## Shared infrastructure

### AiExecutionService

Extracted from `AiActivityGeneratorHandler`. Owns the primary→fallback→log pattern.

```
AiExecutionService.ExecuteWithFallbackAsync(featureKey, variables, profileId, ct)
  │
  ├── ResolveWithFallback(featureKey)
  ├── try primary:
  │     CompleteAsync() → LogUsage(isFallback=false, success=true)
  │     return response
  └── catch:
        LogUsage(isFallback=false, success=false)
        if fallback exists:
          try fallback:
            CompleteAsync() → LogUsage(isFallback=true, success=true)
            return response
          catch:
            LogUsage(isFallback=true, success=false)
        throw AiUnavailableException
```

Used by: `AiActivityGeneratorHandler`, `StudentMemoryService`, `AdaptivePathGeneratorHandler`.

### LearningPathDtoBuilder

Extracted from `AiLearningPathGeneratorHandler`. Computes module progress, current module,
and completion status from database state.

Used by: `AiLearningPathGeneratorHandler`, `AdaptivePathGeneratorHandler`.

---

## List caps (enforced by UserLearningSummary.ApplyDelta)

| Field | Cap | Dedup |
|---|---|---|
| strong_skills | 10 | case-insensitive |
| weak_skills | 10 | case-insensitive |
| recurring_mistakes | 10 | case-insensitive |
| covered_scenarios | 20 | case-insensitive |
| next_focus | 5 | replaced (not merged) |

---

## Fingerprint deduplication

Before saving generated modules, each is checked against existing module fingerprints:

```
duplicate = (
    same scenarioType + audience + communicationMode
    OR
    normalised title (lowercase, collapsed whitespace) shares first 6 words
)
```

Duplicate modules are skipped. If all generated modules are duplicates, return the current
path without appending. Log the event.

---

## Concurrency safety

`LearningPath` has a PostgreSQL `xmin` concurrency token.
`DbUpdateConcurrencyException` on `SaveChangesAsync()` → handler returns 409.
Frontend disables the generate-next button on click (client-side guard).

---

## Bootstrapping existing learners

On first `GetOrCreateWithBootstrapAsync()` call for a learner with no `UserLearningSummary`:

1. Call `StudentProgressService.GetCurrentFocusAreaAsync()` to derive a weak skill from
   existing attempt feedback categories.
2. Create `UserLearningSummary` with `weak_skills_json = [focusArea.FriendlyLabel]` if found.
3. Create `StudentSkillProfile` row for that skill with `is_weak = true`.

Subsequent memory updates build from there.

---

## Failure handling

| Failure | Behaviour |
|---|---|
| Memory update AI timeout (8s) | Log warning, continue, feedback returned |
| Memory update AI invalid JSON | Log warning, continue |
| Memory update DB save fails | Log error, continue |
| All above together | Feedback always returned. Memory may lag. |
| generate-next AI unavailable | 503 with correlationId |
| generate-next DB concurrency | 409 |

**Known limitation:** No staleness flag, no retry queue, no alert on repeated memory update
failures. If AI is down for hours, memory becomes stale but the app continues working.
Add staleness detection to backlog.

---

## Key invariants that must not be violated

1. `ActivityAttempt` is append-only. Memory update does not modify or delete attempts.
2. Memory update never blocks or fails the student's activity submission.
3. AI context packets sent to memory update prompt must not include raw `SubmittedContent`
   from `ActivityAttempt` — pass only structured feedback fields.
4. `covered_scenarios_json` deduplication is case-insensitive.
5. `LearningPath.xmin` must be mapped before `generate-next` is deployed.
6. `StudentSkillProfile` skill keys are lowercase snake_case. AI delta must be normalised
   before upserting.
