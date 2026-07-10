---
status: current
lastUpdated: 2026-07-11 00:30
owner: engineering
supersedes:
supersededBy:
---

# Phase J2a — AI-Assisted Lesson Generation

**Date:** 2026-07-11
**Related sprint/feature:** First pass of Phase J2 from `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md` (§D). J2 was split into three small passes (J2a Lesson, J2b Exercise, J2c Module) per explicit user direction, matching the project's established convention (I2A/I2B/I2C, I4 Pass 1/2/3).
**Files reviewed/changed:**
- `src/LinguaCoach.Application/Lessons/LessonGenerationContracts.cs`
- `src/LinguaCoach.Infrastructure/Lessons/AiLessonGenerationService.cs` (new)
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Infrastructure/Ai/AiExecutionService.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Controllers/AdminLessonController.cs`
- `src/LinguaCoach.Web/src/app/core/models/admin-lesson.models.ts`
- `src/LinguaCoach.Web/src/app/core/services/admin-lesson.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-resource-bank-unified/admin-resource-bank-unified.component.ts`
- `tests/LinguaCoach.UnitTests/Lessons/AiLessonGenerationServiceTests.cs` (new)

## Problem

The architecture audit found that `LessonGenerationService` (and Exercise/Module generation) is
100% deterministic field-copying, with no `IAiProvider` call anywhere — despite the product vision
explicitly requiring AI-assisted content generation at every step. This phase closes that gap for
Lessons only; Exercise (J2b) and Module (J2c) generation remain deterministic-only until their own
passes.

## Decisions

Asked the user two questions before implementing:

1. **Pass scope** — Lesson first, then Exercise, then Module (three small passes) vs. all three in
   one pass. **Decided: three small passes.** Matches the project's own established convention.
2. **AI vs deterministic UX** — a new, separate "Generate with AI" action alongside the existing
   deterministic "Generate" (deterministic untouched, AI failure surfaces as a clear error), vs.
   toggling/replacing the existing action with a silent AI-then-deterministic fallback. **Decided:
   separate action, deterministic untouched.** Avoids silent quality downgrade and keeps the
   already-relied-upon deterministic endpoint's behavior unchanged.

## What changed

**New interface** `IGenerateLessonFromResourcesWithAiHandler` (`LessonGenerationContracts.cs`) —
same `GenerateLessonFromResourcesRequest`/`GenerateLessonFromResourcesResult` shape as the existing
deterministic `IGenerateLessonFromResourcesHandler`, so the frontend and any future caller reuse the
identical request/response contract; only the generation mechanism differs.

**New service** `AiLessonGenerationService` — resolves the selected Resource Bank row(s) the same
way the deterministic composer does (`LessonResourceLookup`, shared internal type), builds a prompt
via the existing `IAiContextBuilder`/`AiPrompt`/`AiExecutionService` infrastructure (the same
pattern `ResourceCandidateAnalysisService` already uses for AI-advisory classification), and parses
the JSON response into `{title, body, examples, commonMistakes, usageNotes}`. Metadata fields
(CEFR/skill/subskill/tags/difficulty) stay deterministic from the selected resources/request —
**only the teaching prose itself is AI-generated**, matching the audit's own framing of what "AI
generates the Lesson" should mean here.

**Retry/failure behavior** mirrors `ResourceCandidateAnalysisService`'s retry-once-on-bad-JSON
pattern, but diverges on what happens after: this is a synchronous, admin-triggered action the
admin is actively waiting on (like `ActivityTemplateInstanceGenerator`), so failure after the retry
**throws** `LessonValidationException` with a message pointing back to the deterministic action,
rather than degrading silently. No Lesson row is created on any failure path.

**Prompt** (`lesson_generate_from_resources`, seeded via `DefaultAiSeeder`) teaches only the
selected source material, includes a CEFR-calibration table for prose complexity/length, and is
explicit that it must not invent unrelated content. Mapped to the existing `llm.generation` AI
config category (`AiExecutionService.ResolveLlmCategory`) — the same category `activity_generate_*`
features already use, so no new admin AI-config screen is needed; an admin who has already
configured a provider for content generation gets this feature working immediately, and one who
hasn't sees the same "AI provider is not configured" `AiConfigurationUnavailableException` every
other generation feature surfaces today.

**Endpoint** `POST api/admin/lessons/generate-from-resources/ai` — a new sibling route next to the
existing `POST api/admin/lessons/generate-from-resources`, same request body shape, same
`LessonValidationException` → 400 error handling.

**Frontend**: a new "Generate Learn (AI)" row action on the Resource Bank page, next to the existing
"Generate Learn", with its own loading-state signal (`generatingLearnAiId`) so the two actions don't
interfere with each other. On success, the success banner text makes clear the draft was
AI-generated; on failure, the backend's error message (e.g. "AI generation is currently
unavailable...") surfaces directly in the existing error banner.

## What was NOT changed

- `LessonGenerationService` (the deterministic composer) — completely untouched, still the default/
  fallback-safe path, still reachable regardless of AI configuration or availability.
- Exercise and Module generation — still 100% deterministic; J2b and J2c are separate future passes.
- No schema/migration change — `Lesson`'s existing `GenerationProvider`/`GenerationModel` fields
  (already present since Phase H3, previously always `"Deterministic"`) now genuinely carry a real
  AI provider/model name for AI-generated drafts; no new columns needed.
- No new admin AI-config UI — reuses the existing `llm.generation` category, already configurable
  via the existing AI Config admin page.

## Tests

Added `AiLessonGenerationServiceTests.cs` (7 tests), reusing the existing
`SwappableFakeAiProvider`/`FakeAiProviderResolver`/`NeverCalledUsageQuotaService` fakes already
defined in `ResourceCandidateAnalysisServiceTests.cs` (same assembly, `internal` visibility is
assembly-scoped) rather than duplicating them:
- Valid AI response creates a pending-review Lesson with real AI provider/model attribution.
- Bad JSON on the first attempt retries once and succeeds on a valid second response.
- Bad JSON on both attempts throws `LessonValidationException`, no Lesson row created.
- A response missing `title`/`body` is treated as unparseable and retried (same as bad JSON).
- AI provider unavailable throws `LessonValidationException`, no Lesson row created.
- No resources / unknown resource both throw before any AI call is attempted (fail-fast validation
  reused unchanged from the deterministic composer).

## Validation

- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline).
- `dotnet test --configuration Release` — 3,433/3,433 passing (5 architecture, 2,116 unit [+7 new],
  1,312 integration).
- `npm run build -- --configuration production` — no new TS/Angular compile errors; fails only the
  same pre-existing bundle-size budget (1.00 MB budget vs 2.56 MB actual), unrelated to this change.
- Frontend unit tests (Karma) not run — still blocked by pre-existing, unrelated `TODO-H8-2`.
- Playwright not run — backend/admin-page logic change, no new end-to-end flow to drive beyond what
  the existing admin Resource Bank page E2E coverage (if any) already exercises.

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`,
  `ResourceCandidateAnalysisService.cs` (pattern source), `AiExecutionService.cs`,
  `AiProviderResolver.cs`, `DefaultAiSeeder.cs`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry).
- Docs intentionally not updated: `docs/architecture/product-model-realignment-h0.md` — still
  accurate as the original target-model doc; this phase implements a slice of it rather than
  changing the model itself.
- Reason: n/a.

## Risks or unresolved questions

- No admin has configured the `llm.generation` category with a real (non-"fake") provider in any
  environment this session touched, so the new action has not been exercised against a real AI
  provider — only against the fake provider in tests. First real use in dev/UAT should be verified
  once an admin configures a provider.
- The prompt was written and reviewed for structure/safety but not iteratively tuned against real
  model output (no live AI calls were made this session, consistent with AGENTS.md's "tests must
  not depend on real AI providers" rule) — prompt quality may need a follow-up tuning pass once real
  usage data exists.

## Final verdict

Closes the Lesson slice of the audit's "no AI anywhere in generation" gap, as a deliberately small,
reversible, additive-only pass. The deterministic path remains fully intact and available regardless
of AI configuration or availability — matching the project's consistent precedent of adding new
paths alongside proven ones rather than replacing them outright.

## Next recommended action

Proceed to Phase J2b (AI-assisted Exercise generation), following the same pattern established here:
same `IAiContextBuilder`/`AiExecutionService` infrastructure, a new separate "Generate with AI"
action, retry-once-then-throw failure behavior, deterministic composer left untouched.
