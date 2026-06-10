---
status: planned
createdAt: 2026-06-11
owner: product
related:
  - docs/testing/deployed-student-e2e-audit-2026-06-11.md
  - docs/handoffs/current-product-state.md
---

# Sprint: AI Config Overhaul / No-Fallback Rule / Journey Fix

**Date planned:** 2026-06-11  
**Motivation:** Post-QA audit findings and product owner corrections.

---

## What this sprint fixes

### From the QA audit

| Bug | Area | Severity |
|---|---|---|
| BUG-001 | Audio / TTS — 0 seconds everywhere | Critical |
| BUG-002 | Vocab / Gap Fill render empty ("Fallback activity") | Critical |
| BUG-003 | Activity page blank on mobile | Critical |
| BUG-004 | Phrase-match submit returns 400 | High |
| BUG-005 | Streak shows "--" instead of 0 | Medium |
| BUG-006 | Profile / Lesson layout clipped by sidebar | High |

### From post-audit product owner direction

1. **No-fallback rule** — everywhere AI is used, if AI is unavailable, show "Service not available" — never hardcoded fallback content, never silent empty states.
2. **Admin AI Config overhaul** — current UI has too many individual feature-key rows. Redesign around categories with a single provider/model per category, plus a flexible TTS section.
3. **Journey page** — currently shows old LearningPath module cards. Must show actual lesson and activity history based on `LearningSession` records.

---

## Product owner rules (must be preserved)

- No AI → no service. Replace every SystemFallback / hardcoded-fallback path with a proper 503 and a user-visible "Service not available" message.
- TTS must be independently configurable (its own provider, model, and voice per TTS use case).
- Admin must be able to set one provider for an entire category (e.g. all content generation uses OpenAI) without touching 12 individual rows.
- Journey must be grounded in what the student actually did — LearningSession and activity history — not the old LearningPath module structure.

---

## Track 1 — No-Fallback Rule (Backend + Frontend)

### Context

`ExercisePrepareHandler` has two fallback paths:

1. **VocabularyPractice (no pattern key)** — creates a `SystemFallback` placeholder with no real content. This causes BUG-002: phrase-match / gap-fill render empty.
2. **`catch (Exception)`** after AI generation failure — calls `CreatePatternFallback()` with hardcoded minimal content. This causes BUG-002 for pattern-keyed activities when AI fails.

The `IAiActivityGenerator` contract doc comment says: "callers must catch and fall back to SystemFallback." That rule is now **abolished**.

The `ActivityGetHandler` may also serve `SystemFallback` records to the frontend without surfacing the failure condition. The frontend receives a seemingly valid activity response and renders an empty or near-empty shell.

### New rule

If AI is required and is not available or fails:
- Backend: return HTTP 503 with `{ "error": "AI service not available", "retryable": true }`.
- Frontend: render a "Service not available" error card — not a loading skeleton, not a blank page, not a fallback form. Offer a "Try again" button.

### Phase 1A — Application layer exception

**File:** `src/LinguaCoach.Application/Ai/AiServiceUnavailableException.cs` (new)

```csharp
public sealed class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException(string featureKey)
        : base($"AI service is not available for feature '{featureKey}'.") { }

    public AiServiceUnavailableException(string featureKey, Exception inner)
        : base($"AI service is not available for feature '{featureKey}'.", inner) { }
}
```

This replaces `AiConfigurationUnavailableException` for the "provider not available" case. Distinguish:
- `AiConfigurationUnavailableException` — misconfigured (admin needs to fix)
- `AiServiceUnavailableException` — provider configured but temporarily unreachable

Both result in a 503 response. Both should be logged at `Warning` level.

### Phase 1B — Remove SystemFallback paths from ExercisePrepareHandler

**File:** `src/LinguaCoach.Infrastructure/Sessions/ExercisePrepareHandler.cs`

Remove:
- `CreatePlaceholder()` method and all calls to it (used for VocabularyPractice without pattern key — this activity type should not be AI-generated without a pattern; see Phase 1E)
- `CreatePatternFallback()` method and the `catch (Exception)` block that calls it

Replace the `catch (Exception)` block:
```csharp
catch (NotSupportedException ex)
{
    _logger.LogWarning(ex,
        "AI generation not supported for ActivityType={ActivityType} PatternKey={PatternKey}",
        activityType, patternKey ?? "(none)");
    throw new AiServiceUnavailableException(patternKey ?? activityType.ToString(), ex);
}
catch (Exception ex)
{
    _logger.LogWarning(ex,
        "AI generation failed for ExerciseId={ExerciseId} PatternKey={PatternKey}",
        exercise.Id, patternKey ?? "(none)");
    throw new AiServiceUnavailableException(patternKey ?? activityType.ToString(), ex);
}
```

Remove the VocabularyPractice-without-patternKey early return (lines 144–155 in the current file). If `VocabularyPractice` with no pattern key is requested, it either goes through AI generation or returns 503 — no silent placeholder.

### Phase 1C — SessionsController catches AiServiceUnavailableException

**File:** `src/LinguaCoach.Api/Controllers/SessionsController.cs`

In `PrepareExercise`:
```csharp
catch (AiServiceUnavailableException ex)
{
    _logger.LogWarning(ex, "AI service unavailable for exercise prepare");
    return StatusCode(503, new { error = "The AI service is not available. Please try again shortly.", retryable = true });
}
```

Also check `ActivityController.GetNext` for similar `SystemFallback` fallback paths in `ActivityGetHandler` — if the handler can serve a `SystemFallback`-sourced activity without signalling failure, that path must also return 503.

### Phase 1D — Review CreateReviewPlaceholder

The `lesson_reflection` review step uses `CreateReviewPlaceholder()` — this creates a hardcoded reflection card with static prompts. This is acceptable by design (it is not an AI-generated step; it is a structured reflection form). Keep this path. It does NOT violate the no-fallback rule because it is not a failed AI call — it is a deliberate non-AI step.

The `source` field on the resulting `LearningActivity` should be `ActivitySource.SystemGenerated` (not `SystemFallback`) to make the intent clear. If `SystemGenerated` does not exist in the enum, add it.

### Phase 1E — Frontend error card

**File:** `src/LinguaCoach.Web/src/app/features/activity/exercise-renderer/exercise-renderer.component.ts` (or the lesson component that calls prepare)

When `POST /api/sessions/{id}/exercises/{eid}/prepare` returns 503:

- Show a "Service not available" error card:
  ```
  ╔══════════════════════════════╗
  ║  AI service not available    ║
  ║  This activity could not     ║
  ║  be generated right now.     ║
  ║  [ Try again ]               ║
  ╚══════════════════════════════╝
  ```
- The "Try again" button retries the prepare call.
- Do not hide the error behind a loading spinner indefinitely.
- The error card must be styled consistently with existing error states in the app.

When `GET /api/activity/next` returns 503 (Practice Gym):
- Same error card pattern on the activity page.

### Phase 1F — Remove SystemFallback service from the activity get path

**File:** `src/LinguaCoach.Infrastructure/Activity/ActivityGetHandler.cs`

Audit whether this handler can ever return a `SystemFallback`-sourced `LearningActivity` to the frontend. If it can:
- For `SystemFallback` records with no meaningful content (phrase-match/gap-fill with empty pairs), return 503 instead of a broken activity.
- For `SystemFallback` records that ARE the review placeholder (lesson_reflection), those are fine — return them.

The distinguishing test: `activity.Source == ActivitySource.SystemFallback && !IsReviewStep(activity)` → return 503.

---

## Track 2 — Admin AI Config Overhaul

### Context

Current state: one `AiProviderConfig` row per feature key (12+ rows). Each row has its own provider + model + fallback provider + fallback model. The admin must configure each row individually. This creates:

- No concept of "all LLM features use OpenAI" as a single setting
- No concept of "all TTS features use a specific voice"
- 12+ rows to update when the team switches from one provider to another
- No clear separation between TTS config and LLM config

### New design — AI Config categories

Replace the current flat list of feature-key rows with a **category-based** config model.

#### Categories

| Category key | Slug | Covers |
|---|---|---|
| `llm.default` | **Default LLM** | All AI features that do not have a category-specific override |
| `llm.generation` | **Content Generation** | Activity generation prompts: write, listen, speak, pattern variants |
| `llm.evaluation` | **Evaluation & Feedback** | Activity evaluation + feedback prompts: write, speak, pattern evaluators |
| `llm.memory` | **Student Memory & Path** | Learning path generation, adaptive path, student memory update, vocabulary extraction |
| `tts.listening` | **Listening TTS** | Audio for listening comprehension activities |
| `tts.placement` | **Placement TTS** | Audio for placement assessment listening section |

Rules:
- `llm.default` applies to any LLM feature key that does not have a more specific category config.
- Category configs coexist with the existing `AiProviderConfig` feature-key rows for backward compatibility. Feature-key resolution order: `feature-specific → category → llm.default → error (503)`.
- TTS categories are independent; they do NOT inherit from `llm.default`.

#### Feature key to category mapping (hard-coded in resolver)

| Feature key | Category |
|---|---|
| `activity_generate_writing` | `llm.generation` |
| `activity_generate_listening` | `llm.generation` |
| `activity_generate_speaking_roleplay` | `llm.generation` |
| `activity_generate_phrase_match` | `llm.generation` |
| `activity_generate_gap_fill_workplace_phrase` | `llm.generation` |
| `activity_generate_listen_and_answer` | `llm.generation` |
| `activity_generate_listen_and_gap_fill` | `llm.generation` |
| `activity_generate_email_reply` | `llm.generation` |
| `activity_generate_teams_chat_simulation` | `llm.generation` |
| `activity_generate_spoken_response_from_prompt` | `llm.generation` |
| `activity_generate_lesson_reflection` | `llm.generation` |
| `activity_evaluate_writing` | `llm.evaluation` |
| `activity_evaluate_speaking_roleplay` | `llm.evaluation` |
| `activity_evaluate_phrase_match` | `llm.evaluation` |
| `activity_evaluate_gap_fill_workplace_phrase` | `llm.evaluation` |
| `activity_evaluate_listen_and_answer` | `llm.evaluation` |
| `activity_evaluate_listen_and_gap_fill` | `llm.evaluation` |
| `activity_evaluate_email_reply` | `llm.evaluation` |
| `activity_evaluate_teams_chat_simulation` | `llm.evaluation` |
| `activity_evaluate_spoken_response_from_prompt` | `llm.evaluation` |
| `activity_evaluate_lesson_reflection` | `llm.evaluation` |
| `learning_path_generate` | `llm.memory` |
| `learning_path_generate_adaptive` | `llm.memory` |
| `student_memory_update` | `llm.memory` |
| `vocabulary_extract_from_attempt` | `llm.memory` |
| `writing.exercise` | `llm.evaluation` |
| `placement_assessment_evaluate` | `llm.evaluation` |
| `tts.listening` | `tts.listening` |
| `tts.placement` | `tts.placement` |

### Phase 2A — Domain: AiConfigCategory entity

**File:** `src/LinguaCoach.Domain/Entities/AiConfigCategory.cs` (new)

```csharp
public sealed class AiConfigCategory : BaseEntity
{
    public string CategoryKey { get; private set; }   // e.g. "llm.generation"
    public string DisplayName { get; private set; }   // e.g. "Content Generation"
    public string ProviderName { get; private set; }
    public string ModelName { get; private set; }
    public string? VoiceName { get; private set; }    // TTS only
    public DateTime UpdatedAt { get; private set; }
    // ... Update(), UpdateVoice() methods matching AiProviderConfig pattern
}
```

EF migration: new `ai_config_categories` table.

Note: keep `AiProviderConfig` (feature-key rows) intact. `AiConfigCategory` is additive. Feature-key rows override category-level config when present.

### Phase 2B — Infrastructure: AiProviderResolver (new service)

**File:** `src/LinguaCoach.Infrastructure/Ai/AiProviderResolver.cs` (new)

This service replaces the ad-hoc per-use-case config lookups currently scattered through the codebase. Resolution order:

```
1. Look up AiProviderConfig by exact featureKey → use if found and provider ≠ "fake"
2. Look up AiConfigCategory by category(featureKey) → use if found and provider ≠ "fake"
3. Look up AiConfigCategory by "llm.default" → use if found and provider ≠ "fake"
4. Throw AiServiceUnavailableException
```

For TTS:
```
1. Look up AiProviderConfig by exact featureKey (tts.listening / tts.placement)
2. Look up AiConfigCategory by category (tts.listening / tts.placement)
3. Throw AiServiceUnavailableException (no silent fake fallback)
```

**Important:** The `provider=fake` seed is for CI/testing only. Production must have a real provider configured or the service returns 503. `AiProviderResolver` never silently falls back to `fake`.

### Phase 2C — Update TtsProviderResolver

**File:** `src/LinguaCoach.Infrastructure/Speaking/TtsProviderResolver.cs`

Use `AiProviderResolver` instead of the current direct DB query. Remove the "return FakeTTS if not found" behaviour. Instead:
- If resolved provider is `fake`: return `FakeTextToSpeechService` (acceptable in test/dev environments where the admin has explicitly set fake).
- If no config at all: throw `AiServiceUnavailableException("tts.listening")`.

Update callers (`ListeningAudioService`, `PlacementAudioService`) to propagate the exception rather than swallow it.

### Phase 2D — Seeder update

**File:** `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`

Seed the following `AiConfigCategory` rows (idempotent):

| CategoryKey | DisplayName | Provider | Model | Notes |
|---|---|---|---|---|
| `llm.default` | Default LLM | `fake` | `fake` | Changed to real provider by admin in production |
| `llm.generation` | Content Generation | *(inherit from llm.default)* | *(inherit)* | Leave as empty/null initially — falls through to default |
| `llm.evaluation` | Evaluation & Feedback | *(inherit)* | *(inherit)* | Leave as empty/null |
| `llm.memory` | Memory & Learning Path | *(inherit)* | *(inherit)* | Leave as empty/null |
| `tts.listening` | Listening TTS | `fake` | `fake` | Voice: `null` |
| `tts.placement` | Placement TTS | `fake` | `fake` | Voice: `null` |

Categories with empty provider inherit from `llm.default` at runtime. The seeder should upsert these rows without overwriting admin changes.

Keep existing `AiProviderConfig` feature-key rows seeded as before for backward compatibility.

### Phase 2E — Admin UI redesign

**File:** `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.ts`

Replace the long flat feature-key table with a two-section layout:

#### Section A — LLM Configuration

Four cards, one per LLM category:

```
╔══════════════════════════════════════════════════════════╗
║ Default LLM                                              ║
║ Applies to all features without a specific override.     ║
║                                                          ║
║ Provider:  [ OpenAI       ▼ ]   Model: [ gpt-4o-mini ▼ ]║
║                                                  [Saved] ║
╚══════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════╗
║ Content Generation                                       ║
║ Writing, Listening, Speaking, Pattern activities.        ║
║ Leave blank to use Default LLM.                          ║
║                                                          ║
║ Provider:  [ (default)    ▼ ]   Model: [ (default)   ▼ ]║
╚══════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════╗
║ Evaluation & Feedback                                    ║
║ Writing, Speaking, Pattern evaluators, Placement.        ║
║ Leave blank to use Default LLM.                          ║
║ ...                                                      ║
╚══════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════╗
║ Memory & Learning Path                                   ║
║ Learning path generation, adaptive path, student memory. ║
║ Leave blank to use Default LLM.                          ║
║ ...                                                      ║
╚══════════════════════════════════════════════════════════╝
```

"Leave blank to use Default LLM" means the dropdown shows `(use default)` as first option. Selecting it clears the category config and falls through to `llm.default` at runtime.

#### Section B — TTS Configuration

Two cards, one per TTS use case:

```
╔══════════════════════════════════════════════════════════╗
║ Listening TTS                                            ║
║ Audio for listening comprehension activities.            ║
║                                                          ║
║ Provider:  [ OpenAI ▼ ]  Model: [ tts-1 ▼ ]  Voice: [onyx]
║                                                  [Saved] ║
╚══════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════╗
║ Placement TTS                                            ║
║ Audio for placement assessment.                          ║
║                                                          ║
║ Provider:  [ OpenAI ▼ ]  Model: [ tts-1 ▼ ]  Voice: [nova]
║                                                  [Saved] ║
╚══════════════════════════════════════════════════════════╝
```

TTS cards always show the Voice field. No "use default" concept for TTS — each must be explicitly configured.

#### Section C — Provider credentials

Unchanged from current implementation (API key management per provider).

#### Remove the old Feature routing section

The 12-row feature-key table is replaced entirely by the category cards. The individual feature-key `AiProviderConfig` rows are preserved in the database and respected by the resolver, but they are no longer shown in the admin UI. Admins configure at the category level only.

### Phase 2F — Backend API endpoints for category config

**New endpoints (Admin only):**

```
GET  /api/admin/ai/categories        → list all AiConfigCategory rows
PATCH /api/admin/ai/categories/{key} → update provider/model/voice for a category
```

Replace or supplement existing `GET/PATCH /api/admin/ai-config` endpoints. The existing feature-key endpoints can remain for backward compatibility but are no longer called by the new admin UI.

---

## Track 3 — Journey Page: Lesson History

### Context

Current Journey (`/journey`) loads `LearningPathDetail` — a list of AI-personalised modules from the old `LearningPath` entity. The user sees "Writing module 1", "Writing module 2", etc. — the old writing-era curriculum structure.

Per product owner: Journey should show **what the student actually did** — their LearningSession records and the activities within each session.

### New Journey design

```
Learning Journey
─────────────────────────────────────────
Completed lessons  (X total)

▼ Today — 2026-06-11
  ● Workplace Communication — B1 — 20 min
    ✓ Vocabulary warmup (phrase_match) — 87%
    ✓ Listening — Listen and Answer — 72%
    ✗ Writing — Email Reply — in progress
    ○ Review — not reached

▼ Yesterday — 2026-06-10
  ● Introduction to Professional Tone — B1 — 15 min
    ✓ Vocabulary warmup — 91%
    ✓ Writing — Teams Chat — 80%
    ○ Review — reflection completed

── Skills snapshot ──
  Strong: workplace_vocabulary, professional_tone
  Developing: grammar_accuracy, sentence_clarity

── Continue ──
  [ Today's Lesson ]   [ Practice Gym ]
```

Group by date, most recent first. Show session title, duration, and per-step result. If a step has an `ActivityAttempt`, show score. If not started, show "○". If completed with no score (review), show "✓ reflection".

### Phase 3A — Backend: GET /api/sessions/history

**New endpoint:** `GET /api/sessions/history?limit=20&offset=0`

Returns paginated list of completed and in-progress `LearningSession` records for the current student, with per-exercise details and attempt summary.

Response shape:

```json
{
  "sessions": [
    {
      "id": "...",
      "title": "Workplace Communication",
      "date": "2026-06-11",
      "status": "InLesson | ActiveLearning | CourseReady",
      "durationMinutes": 20,
      "exercises": [
        {
          "id": "...",
          "stepNumber": 1,
          "patternKey": "phrase_match",
          "exerciseKind": "VocabularyWarmup",
          "status": "Completed | InProgress | NotStarted",
          "activityTitle": "Workplace Phrases: Following Up",
          "score": 87,
          "completedAt": "2026-06-11T06:12:00Z"
        }
      ]
    }
  ],
  "total": 12,
  "hasMore": true
}
```

**Handler:** `SessionHistoryHandler` in `LinguaCoach.Infrastructure/Sessions/`.

Query: join `LearningSessions` → `SessionExercises` → `LearningActivities` → `ActivityAttempts` (latest per exercise). Filter by student profile. Order by session start date descending.

### Phase 3B — Frontend: Replace LearningPathComponent content

**File:** `src/LinguaCoach.Web/src/app/features/learning-path/learning-path.component.ts`

The route `/journey` and `/my-path` still points to this component. Replace the module-list rendering with the session history rendering described above.

Keep:
- The student memory / skills snapshot section (it is already correct and useful)
- The "Continue practising" CTAs at the bottom

Replace:
- The `LearningPathDetail` load with `GET /api/sessions/history`
- The module cards list with a date-grouped session card list

If the student has no sessions yet (new user), show: "You haven't started a lesson yet. [Start today's lesson]"

If the session history API fails: show error message, not empty state.

### Phase 3C — Remove or deprecate generateNextModules / completeModule from the Journey UI

These actions (`POST /api/learning-paths/{id}/modules` and `POST /api/learning-paths/{id}/modules/{mid}/complete`) were part of the old module progression model. They are no longer surfaced from the Journey page. Do not delete the backend endpoints — they may be used elsewhere — but remove the Angular UI for them in this component.

---

## Track 4 — Audio / TTS 503 handling

### Context

BUG-001: audio endpoint returns 401. The root cause has two parts:
1. `provider=fake` in production → `FakeTextToSpeechService` → silent WAV → 0 seconds (not a 401).
2. The 401 errors seen in browser console are from the audio `<audio src="...">` element trying to load the audio URL without the cookie. Browsers do not attach cookies to media element src requests in all configurations.

### Phase 4A — Audio endpoint auth

**File:** `src/LinguaCoach.Api/Controllers/ActivityController.cs` — `GetAudio` action.

Verify: the audio endpoint uses cookie-based auth (`.AddCookie()`) like all other endpoints. If `[Authorize]` relies on Bearer token and the frontend is cookie-based, fix the scheme.

Add: the endpoint should also handle the case where `provider=fake` produced an empty audio file — return 404 with `{ "error": "Audio not available — TTS not configured." }` rather than returning a 0-byte file.

### Phase 4B — Frontend audio player graceful failure

**Files:** listening activity component, placement audio component.

When the audio request returns 404 or 503:
- Show a banner: "Audio is not available for this activity. Check AI configuration."
- Do not render a broken audio player with 0:00 duration.
- Allow the student to complete the activity (read the transcript instead) — transcript is already hidden until submit; allow it to become visible if audio fails.

---

## Track 5 — QA Bug Fixes (lower severity)

These are independent and can be done in any order within the sprint.

### BUG-003: Activity page blank on mobile

**Area:** Frontend responsive layout.

**File:** The activity shell component / lesson component that wraps the activity renderer.

At ≤375px viewport:
- The skeleton/loading state should be replaced by actual content once the prepare call resolves.
- The likely cause is a CSS overflow hidden or a width: 0 on a parent flex container at small viewports.
- Fix: add `min-width: 0` and verify `overflow-x: hidden` is not clipping the content on the activity shell.

### BUG-004: Phrase-match submit returns 400

**Area:** Backend — `PatternEvaluationRouter`.

At submission: `POST /api/activity/{id}/attempt` with phrase-match answers returns 400.

Likely cause: the submitted answers payload for `phrase_match` is malformed (wrong field name) or `KeyedSelectionEvaluator` is not receiving the expected `selectedKeys` structure. Debug by checking the submission request body shape vs. the evaluator contract.

### BUG-005: Streak shows "--" instead of 0

**Area:** Frontend — dashboard / Today component.

Replace `"--"` fallback in the streak display with `0` when streak count is null or undefined.

### BUG-006: Profile / lesson layout clipped by open sidebar

**Area:** Frontend layout.

When the sidebar is open on desktop, the main content area does not shrink. Apply `margin-left` or `padding-left` transition consistent with the sidebar open/close state. Check sidebar state service and ensure the main content wrapper subscribes to it.

---

## Migration

One EF migration required:

**T36 — ai_config_categories**

```sql
CREATE TABLE ai_config_categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    category_key VARCHAR(100) NOT NULL UNIQUE,
    display_name VARCHAR(200) NOT NULL,
    provider_name VARCHAR(100) NOT NULL,
    model_name VARCHAR(200) NOT NULL,
    voice_name VARCHAR(100),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

---

## Backend new files

| File | Purpose |
|---|---|
| `src/LinguaCoach.Application/Ai/AiServiceUnavailableException.cs` | New exception for when AI provider is unavailable |
| `src/LinguaCoach.Domain/Entities/AiConfigCategory.cs` | New domain entity for category-level AI config |
| `src/LinguaCoach.Infrastructure/Ai/AiProviderResolver.cs` | New resolver: feature key → category → default → 503 |
| `src/LinguaCoach.Infrastructure/Sessions/SessionHistoryHandler.cs` | Query handler for session history list |
| `src/LinguaCoach.Application/Sessions/ISessionHistoryHandler.cs` | Interface |

## Backend modified files

| File | Change |
|---|---|
| `ExercisePrepareHandler.cs` | Remove SystemFallback paths; throw AiServiceUnavailableException |
| `SessionsController.cs` | Catch AiServiceUnavailableException → 503 |
| `ActivityController.cs` | Handle fake/missing TTS audio gracefully |
| `TtsProviderResolver.cs` | Use AiProviderResolver; remove silent fake fallback |
| `DefaultAiSeeder.cs` | Seed AiConfigCategory rows |
| `AiExecutionService.cs` | Propagate unavailability as AiServiceUnavailableException |
| Admin API controller | Add GET/PATCH /api/admin/ai/categories endpoints |

## Frontend modified files

| File | Change |
|---|---|
| `admin-ai-config.component.ts` | Full redesign — category cards + TTS section |
| `learning-path.component.ts` | Replace module list with session history |
| `learning-path.component.html` | New session history template |
| Lesson component | Handle 503 from prepare → "Service not available" card |
| Activity shell | Fix mobile blank page (Track 5 BUG-003) |
| Dashboard / Today component | Fix streak "--" → 0 (BUG-005) |
| Audio player | Graceful 404/503 handling (Track 4B) |

---

## Test additions

- Unit: `AiProviderResolver` — feature key resolution, category fallthrough, llm.default fallthrough, AiServiceUnavailableException on no config
- Integration: `ExercisePrepareHandler` — returns 503 when AI fails; no SystemFallback records created
- Integration: `SessionHistoryHandler` — returns sessions in date order, correct exercise status, correct scores
- Playwright: Journey page shows session history (not module list)
- Playwright: Listening activity shows "Service not available" when TTS not configured

---

## Sequencing

Recommended order within the sprint:

1. **Phase 1A + 1B + 1C** (No-fallback backend) — unblocks everything else
2. **Phase 2A + 2B + 2C** (AI category model + resolver) — needed before admin UI
3. **Phase 2D** (Seeder) — depends on 2A
4. **Phase 4A** (Audio endpoint auth) — independent; can run in parallel with 2
5. **Phase 1E** (Frontend 503 card) — can run in parallel with backend phases
6. **Phase 2E + 2F** (Admin UI + API) — depends on 2A, 2B
7. **Phase 3A** (Session history endpoint) — independent
8. **Phase 3B + 3C** (Journey frontend) — depends on 3A
9. **Track 5 bug fixes** — all independent; fill remaining capacity
10. **Phase 4B** (Audio player graceful failure) — depends on 4A

---

## Constraints

- Do not delete any existing `AiProviderConfig` rows from the database or seeder.
- Do not delete the `LearningPath` or module endpoints — they are still used by the admin module list and potentially by the progress page.
- `FakeTextToSpeechService` must remain available and functional for CI/test environments. The constraint is: production must not silently use it.
- `CreateReviewPlaceholder` is NOT removed — lesson reflection is a deliberate non-AI step.
- All existing Playwright tests must continue to pass. If a test relied on `SystemFallback` activity content, update the test to use a properly-configured AI path or to assert the "Service not available" state.

---

## Definition of done

- [ ] All four activity types generate successfully with a real LLM provider configured
- [ ] When LLM is not configured (or set to fake), prepare returns 503 and the frontend shows "Service not available"
- [ ] When TTS is not configured, audio returns 404 with a clear message; activity remains completable
- [ ] Admin can configure all LLM features with 4 dropdowns (default + 3 category overrides)
- [ ] Admin can configure TTS voice independently per TTS use case
- [ ] Journey page shows date-grouped lesson history, not old module cards
- [ ] Mobile activity page renders content at 375px
- [ ] Phrase-match submit completes without 400
- [ ] Streak shows 0, not "--"
- [ ] All dotnet tests pass
- [ ] Playwright suite passes
- [ ] `npm run build` passes

---

## Risks

| Risk | Mitigation |
|---|---|
| Removing SystemFallback breaks existing Playwright tests that rely on fallback content | Audit all tests before removing; update or mock as needed |
| Category resolver adds a DB query per AI call | Cache category configs in memory with a short TTL (30s) |
| Journey history endpoint slow for users with many sessions | Add pagination (default limit 20); add index on `learning_sessions.student_profile_id + started_at` |
| Old module-based Journey expected by other parts of the app (Progress page?) | Audit all routes that reference `/journey`, `/my-path`, `LearningPathComponent` before removing module-list code |
