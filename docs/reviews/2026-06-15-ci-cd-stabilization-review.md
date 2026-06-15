---
status: current
lastUpdated: 2026-06-15 00:00
owner: engineering
supersedes:
supersededBy:
---

# CI/CD Stabilization Review — 2026-06-15

## Related sprint

`docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` (Phases 7A-7D, just completed).
This review covers the follow-up stabilization sprint (no new features).

## Files reviewed

- `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`
- `tests/LinguaCoach.ArchitectureTests/LinguaCoach.ArchitectureTests.csproj`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Infrastructure/Ai/DbPromptAiContextBuilder.cs`
- `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs` (FakeAiProvider fixture)
- Failing tests: `VocabularyPracticeActivityTests`, `PracticeGymNextEndpointTests`, `PatternKeyedActivityEndpointTests`

## Findings, grouped by priority

### P1 — ArchitectureTests project was empty (CI failure)

`tests/LinguaCoach.ArchitectureTests` had a valid `.csproj` referencing xunit but zero
`.cs` files and no project references. CI reported "No test is available" because
the assembly contained no test classes.

### P1 — Token budget regressions from Phase 7A-7D module_stage_v1 migration

The staged `module_stage_v1` prompt content (learnContent + practiceContent +
feedbackPlan) is significantly larger than the old flat-format prompts, but the
per-prompt `maxInputTokens` values seeded in `DefaultAiSeeder` were not increased
to match. `DbPromptAiContextBuilder.BuildAsync` throws `TokenBudgetExceededException`
when the rendered prompt exceeds the configured budget, which `AiActivityGeneratorHandler`
turns into a 503 ServiceUnavailable.

Affected prompts and observed estimated-vs-budget tokens (4 chars ≈ 1 token estimate):

| Prompt key | Estimated | Old budget | New budget |
|---|---|---|---|
| `activity_generate_phrase_match` | 670-675 | 600 | 800 |
| `activity_generate_gap_fill_workplace_phrase` | 740 | 600 | 900 |
| `activity_generate_listen_and_answer` | 836 | 800 | 1000 |
| `activity_generate_email_reply` | 1055 | 900 | 1300 |
| `activity_generate_teams_chat_simulation` | 1054 | 700 | 1300 |
| `activity_generate_open_writing_task` | 1000-1018 | 900 | 1200 |

All values include headroom for per-request variable substitution
(`{{recentMistakes}}`, `{{careerContext}}`, etc.) growing beyond the fixed
template text.

Since `SeedOrUpgradePromptAsync` only re-seeds when the prompt *content* hash
changes, and the content for all these prompts already changed during Phase
7A-7D (so the hash differs from any previously-deployed row), these new budget
values will take effect automatically on next deploy via the existing reseed
path. No separate migration/backfill needed.

### P1 — FakeAiProvider fixture missing pattern-specific `practiceContent.exerciseData` fields

`ModuleStageContentValidator.RequiredPracticeKeysByPatternKey` requires
pattern-specific keys inside `practiceContent.exerciseData`:

- `phrase_match` → `pairs`
- `gap_fill_workplace_phrase` → `items`
- `email_reply` → `prompt`, `incomingMessage`
- `teams_chat_simulation` → `prompt`, `chatHistory`
- `speaking_roleplay_turn` → `prompt`, `partnerTurn`

The single shared `FakeAiProvider` fixture in
`tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs` only had
`audioScript`/`questions`/`gaps`/`prompt` inside `exerciseData` (a
listening-comprehension shaped fixture reused for every pattern). Pattern-keyed
routes for `phrase_match`, `gap_fill_workplace_phrase`, `email_reply`,
`teams_chat_simulation`, `speaking_roleplay_turn` failed AI response validation
("AI staged activity failed validation after retry: practiceContent.exerciseData
is missing required field ...") and returned 503.

Fix: added `pairs`, `incomingMessage`, `chatHistory`, `partnerTurn`, `gaps`,
`items`, `practiceMode` to the shared fixture's `practiceContent.exerciseData`
object so it satisfies every pattern's required-key set simultaneously. This is
a single shared fixture used across many tests — adding extra keys is additive
and does not change behaviour for patterns that don't require them (validator
only checks for required keys, doesn't reject extra ones).

### Not a regression — pre-existing flaky test

`VocabularyPracticeActivityTests.GetNext_WithEnoughVocabAtEveryFourthAttempt_ReturnsVocabPractice`
was failing on `main` before this sprint with `InvalidOperationException: Sequence
contains no elements` (unrelated `.First()`/`.FirstOrDefault()` EF Core query
issue, separate from the 503 issues). After the fixture fix above (which adds
`items`/vocabulary data to the fake AI response), this test now passes as a side
effect. No separate fix was required, but worth noting in case it re-flakes —
root cause is an unguarded `.First()` somewhere in the vocabulary practice
selection path that depends on seeded data ordering.

## Decisions made

1. Fix token budgets by increasing `maxInputTokens` in `DefaultAiSeeder` rather
   than shrinking the module_stage_v1 prompt content — the richer prompts are the
   intended Phase 7 outcome and shrinking them would regress content quality.
2. Fix the shared `FakeAiProvider` fixture by adding missing keys rather than
   creating per-pattern fixtures — keeps the existing single-fixture test
   infrastructure pattern, minimal diff, no test rewiring needed.
3. Add `tests/LinguaCoach.ArchitectureTests/LayerDependencyTests.cs` using
   `NetArchTest.Rules` to enforce the Clean Architecture layer rules already
   documented in `AGENTS.md` section 3 (Domain/Application must not depend on
   Infrastructure, Persistence, API, EF Core, or ASP.NET Identity). Added
   `AssemblyMarker` classes to `LinguaCoach.Domain` and `LinguaCoach.Application`
   so NetArchTest can locate the assemblies, and added project references from
   `LinguaCoach.ArchitectureTests.csproj` to all five `src/` projects.

## Implementation tasks produced

None — all fixes were applied directly as part of this stabilization pass.

## Risks / unresolved questions

- `dotnet ef migrations list` fails locally with "startup project doesn't
  reference Microsoft.EntityFrameworkCore.Design" — this is a pre-existing
  tooling gap (not a regression from this change) and was not in scope for this
  sprint. Migrations themselves apply correctly via `EnsureCreated`/seeding in
  integration tests (472/472 pass), so app startup and DB seeding are verified
  working.
- Playwright/e2e is not part of `.github/workflows/ci.yml` (only backend
  `dotnet test` and frontend `npm test` + `npm run build` run in CI). Ran one
  representative e2e spec (`core-flow-smoke.spec.ts`) locally with mocked API —
  passed (1/1). Did not run the full e2e suite against a live backend; this would
  require Docker/Postgres setup not currently part of CI and was out of scope.
- No Google Fonts 403 issue observed in the production Angular build (only
  pre-existing CSS budget warnings on admin-dashboard and admin-app-layout
  components, ~1KB and ~164B over 4KB budgets respectively — cosmetic, not
  build-breaking).

## Final verdict

CI failures resolved. Backend integration+unit tests: 472/472 passing (up from
445/472, i.e. all 27 originally-failing tests now pass). ArchitectureTests:
3/3 real tests passing (was 0 tests / CI failure). Angular unit tests: 103/103
passing. Angular dev build (via Playwright webServer) and production build both
succeed.

No new product features added. Phase 8 not started. No future exercise format
renderers/evaluators made runnable. Today pre-generation not implemented.
MinIO/audio lifecycle not implemented.

## Next recommended action

Safe to proceed to Phase 8 from a CI/CD stability standpoint. Recommend a small
follow-up (separate ticket) to fix the unguarded `.First()`/`.FirstOrDefault()`
EF Core query in the vocabulary practice selection path flagged by the EF Core
warning `10103` (currently masked because it now passes, but is a latent
ordering bug), and to add `Microsoft.EntityFrameworkCore.Design` to the API
project so `dotnet ef migrations` CLI commands work for future migration work.
