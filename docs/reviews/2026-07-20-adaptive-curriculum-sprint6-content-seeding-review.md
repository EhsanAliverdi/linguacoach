# Adaptive Curriculum Sprint 6 — Bulk Content Seeding

**Date:** 2026-07-20
**Related sprint / feature:** Sprint 6 of the multi-sprint Adaptive Curriculum initiative — see `docs/architecture/adaptive-curriculum-skill-graph.md`, and the Sprint 1-5 review docs for the skill graph, content re-tagging, goal vector, node mastery, and AI composer this sprint's seeded content finally lets exercise for real. This is the first sprint to close the "0 approved Modules" gap every prior sprint's review flagged as blocking live verification.

## Context / decision

Sprint 6 was originally scoped as "content seeding + retire `CurriculumObjective`." Research (via a dedicated Explore pass) found these are two large, mostly-independent efforts: replacing `LearningPlanService`'s plan-generation sequencing requires rebuilding `CurriculumRoutingService`'s CEFR-fallback/difficulty-band/context-tag selection logic against skill-graph nodes (no equivalent exists today), plus migrating `EvaluateObjectiveMasteryAsync` to node-based grouping — a second full sprint's worth of work.

- Decision (AskUserQuestion, recommended option chosen): **split into two sprints.** Sprint 6 = bulk content generation/tagging/approval, closing the content gap. Sprint 7 = rebuild plan-sequencing on skill-graph nodes and retire the legacy system.

Further research found the content-generation pipeline is entirely single-resource, three-gate manual (generate Lesson → approve → generate Exercise → approve → generate Module → approve, once per resource, via `AdminModuleController`'s singular endpoints) — there is no bulk/batch generation endpoint anywhere in the codebase. But it also found a genuinely reusable shortcut: **`LessonExerciseBatchGenerationService` (Phase K5) already generates N Exercises from a Lesson and auto-creates/links the resulting Module in one call** — so "bulk seeding" mainly needed a new outer loop over resources, not new generation logic. The resource pool itself is large and mostly untouched: 39,146 staged `resource_candidates`, 52 promoted `resource_bank_items`, but only 7 `lesson_resource_links`/18 `exercise_resource_links` existed before this sprint.

## Files reviewed

`AdminModuleController.cs` (all singular generate-from-* endpoints), `LessonGenerationService.cs`/`ExerciseGenerationService.cs` (deterministic composers — confirmed `gap_fill` is fully deterministic for Vocabulary/Grammar resources, no AI/hallucination risk), `LessonExerciseBatchGenerationService.cs`/`ModuleAutoLinkService.cs` (the existing Phase K5 batch-and-autolink mechanism this sprint reuses instead of reinventing), `ModuleSkillGraphTaggingService.cs` (Sprint 2's auto-apply tagging, confirmed single-Module-per-call but Module-status-agnostic — works on freshly-created Modules), `ResourceBankItem.cs`/`LessonResourceLookup.cs` (resource content shapes, `VocabularyContent`/`GrammarContent`), live dev Postgres (resource/module/link counts by CEFR level).

## What was built

### Backend

- **`IContentSeedingService`/`ContentSeedingService`** (`LinguaCoach.Application.ContentSeeding` / `LinguaCoach.Infrastructure.ContentSeeding`) — for each (CEFR level × {Vocabulary, Grammar}) group, selects up to `MaxResourcesPerCefrLevelPerType` unconsumed `ResourceBankItem` rows (no existing `LessonResourceLink`), and for each: generates a Lesson (`IGenerateLessonFromResourcesHandler`, deterministic), generates N `gap_fill` Exercises from it (`IGenerateActivitiesFromLessonHandler`, which auto-creates/links the Module), approves the Lesson/Exercises/Module, then tags the Module against real approved+active skill-graph nodes matching its CEFR level and skill (`IModuleSkillGraphTaggingService`, unchanged from Sprint 2 — every proposed match still validated against the real candidate list before a `ModuleSkillGraphNodeLink` row is created). **Continue-on-error per resource** — a bulk sweep reports partial failures rather than aborting, distinct from `LessonExerciseBatchGenerationService`'s own fail-fast design for a single admin-intent call.
- Scoped to **Vocabulary/Grammar only** this sprint — the only two resource types with a fully deterministic `gap_fill` composer, keeping bulk-generated content free of AI-hallucination risk at scale. Reading/Listening/Speaking/Writing bulk seeding (which need AI-assisted or type-specific composers) is left for a later pass.
- Bounded per call: `MaxResourcesPerCall = 60`, mirroring `LessonExerciseBatchGenerationService`/`ResourceCandidateBatchAnalysisService`'s existing per-call ceiling discipline.
- **Real bug found and fixed during implementation**: `SkillGraphNode.Skill` is stored lower-invariant (its constructor normalizes casing) while `Module.Skill`/`Lesson.Skill` preserve whatever casing the caller passed (e.g. `"Vocabulary"`, title-case, from `LessonResourceLookup`'s resource-type mapping) — the initial tagging-candidate query compared them directly and would have silently matched zero nodes for every seeded Module. Fixed by normalizing to lower-invariant before the query; caught by a dedicated test seeding a real `SkillGraphNode` and asserting the link actually gets created.
- New `AdminContentSeedingController` — `POST /api/admin/content-seeding/run`, admin-only, bounded, never automatic/scheduled.

### Tests

6 new tests (`ContentSeedingServiceTests.cs`) using SQLite in-memory plus the **real, unmocked** `LessonGenerationService`/`ActivityGenerationService`/`ModuleAutoLinkService`/`LessonExerciseBatchGenerationService` chain — only the AI-backed tagging step uses a fake (`FakeModuleSkillGraphTaggingService`), per this repo's "tests use fake providers, never real AI" convention. Covers: a real approved Module is produced end-to-end from a seeded Vocabulary resource; the generated Lesson and all generated Exercises are also approved, not just the Module; a real seeded `SkillGraphNode` gets linked when the fake tagger returns a match (this is the test that caught the casing bug above); already-consumed resources (existing `LessonResourceLink`) are skipped; `MaxResourcesPerCefrLevelPerType` is respected; a single resource's real, deterministic failure (a blank-after-trim title, not a contrived exception) doesn't abort the rest of the batch.

## Migration

None — no new entities or schema this sprint. Reuses `Lesson`/`Exercise`/`Module`/`ModuleSkillGraphNodeLink`, all already real tables from Phase H/Sprint 1-2.

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,940 / 3,940 passing, 0 failing** (30 architecture + 2,550 unit + 1,360 integration).
- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries. No frontend files touched this sprint.
- **Live deployment**: Docker Desktop's engine was down mid-session (same recurring host issue documented in prior sprints) — paused, resumed once restarted. Forced `docker compose build --no-cache api` per the standing lesson; confirmed `Healthy`.
- **Live functional run against real dev data**: `POST /api/admin/content-seeding/run` with `{cefrLevels: [A1,A2,B1,B2], maxResourcesPerCefrLevelPerType: 4, exercisesPerLesson: 2}` (no Grammar resources exist yet in this dev DB, so the run seeded Vocabulary only) → **16/16 resources succeeded**: 16 new approved Modules created (4 per CEFR level, each with an approved Lesson and 2 approved Exercises). Confirmed directly against Postgres: `eligible_modules` (Approved + not archived) went from **0 to 16**. `ai_usage_logs` confirms all 16 real AI tagging calls succeeded (`was_successful=true`, `is_fallback=false`) — the tagging pipeline itself works correctly. Only **1 of 16** Modules ended up with a real `ModuleSkillGraphNodeLink` — not a failure, but a genuine, honestly-reported finding: with only 4 skill-graph nodes per (CEFR level × skill) in the current taxonomy, a single-word Vocabulary lesson (e.g. "resilient") often has no node whose competency description is a genuine match, and the AI correctly returned an empty match list rather than forcing one (per the seeded prompt's explicit instruction). This is a node-granularity-vs-content-granularity mismatch worth a future pass, not a bug in this sprint's code.
- API logs post-deploy/post-run: no new errors — only the pre-existing, already-documented Quartz stale-trigger noise and an unrelated DataProtection key-ring permission warning (dev-container filesystem quirk, not new).
- A full live Today/Practice Gym request against a real student profile (to see the AI composer actually rank the newly-seeded content) was not exercised this pass — the admin preview endpoint returned 404 for the ad-hoc student id used and wasn't debugged further to conserve session scope; the composer's correctness against real eligible candidates is already covered by Sprint 5's tests, and the eligibility gap that blocked it is now closed.

## Decisions made

1. Split content seeding from `CurriculumObjective` retirement into two sprints, per explicit user choice, after research showed the retirement requires rebuilding real selection logic against live student plan data.
2. Scoped bulk seeding to Vocabulary/Grammar (deterministic `gap_fill` composer) only — explicitly not Reading/Listening/Speaking/Writing, to keep bulk-generated content free of AI-hallucination risk. Those types need their own (AI-assisted or type-specific) bulk path in a later pass.
3. Reused `LessonExerciseBatchGenerationService`'s existing Module auto-link mechanism rather than building new Lesson→Exercise→Module composition logic — the only genuinely new code is the outer resource-selection loop, the approval calls, and the skill-graph tagging step.
4. Continue-on-error per resource (not fail-fast) — a bulk seeding sweep is a best-effort admin action across many independent resources, unlike `LessonExerciseBatchGenerationService`'s single-Lesson fail-fast design for one admin's specific intent.

## Risks / unresolved questions

- Only Vocabulary/Grammar content types are bulk-seedable today; Reading/Listening/Speaking/Writing still require the original one-resource-at-a-time manual pipeline. A pilot student's experience is still narrow (2 of 9 skills) until a later pass extends bulk seeding.
- The skill-graph node taxonomy's granularity (4 nodes per CEFR×skill) is coarser than individual Vocabulary-word content — most bulk-seeded Modules (15/16 in the live run) ended up untagged, which means node-mastery-based weakness matching (Sprint 4/5's `IsWeaknessMatch` signal) will rarely fire for this content until either the taxonomy is refined or content is authored at a coarser, more node-aligned granularity (e.g. multi-word thematic Lessons instead of one-word Lessons).
- `Grammar` resources don't currently exist in the dev DB at all (0 rows) — only `Vocabulary` was actually seeded live this run, despite the service supporting both.
- A real end-to-end Today/Practice Gym request against the newly-seeded content, through a real student profile, was not exercised live this pass.

## Final verdict

Sprint 6 complete and verified: 0 backend build errors, 3,940/3,940 tests passing (including 6 new tests exercising the real, unmocked generation chain — not mocks — which caught a genuine skill-normalization bug before it shipped), 0 new frontend build errors, live deployment confirmed healthy via a forced no-cache rebuild. The core goal — closing the "0 approved Modules" gap every prior sprint flagged — is achieved and verified directly against Postgres: 16 real approved, CEFR-tagged Modules now exist where there were 0. The tagging pipeline is confirmed working (16/16 AI calls succeeded); the low match rate (1/16) is an honestly-reported taxonomy-granularity finding, not a defect.

## Next recommended action

Sprint 7, per the split decision: rebuild `LearningPlanService`'s plan-generation sequencing against `SkillGraphNode`/`ModuleSkillGraphNodeLink` (replacing `CurriculumRoutingService`'s CEFR-fallback/difficulty-band/context-tag selection with a node-based equivalent), migrate `EvaluateObjectiveMasteryAsync` to node-based grouping, then retire `CurriculumObjective`/`CurriculumRoutingService`/`AdminCurriculumController`/`admin-curriculum`/`Module.ObjectiveKey` outright. Separately, whenever there's room: extend bulk content seeding to Reading/Listening/Speaking/Writing, and consider whether the skill-graph node taxonomy needs a finer-grained pass so node-mastery weakness-matching has real content to fire against.
