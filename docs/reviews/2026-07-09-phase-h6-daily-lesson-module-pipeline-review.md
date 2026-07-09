---
title: Phase H6 — Daily Lesson Module Pipeline — Implementation Review
date: 2026-07-09
related: Phase H6 (Daily Lesson Module Pipeline), H-track (docs/architecture/product-model-realignment-h0.md)
status: complete
---

# Phase H6 — Daily Lesson Module Pipeline — Implementation Review

**Date:** 2026-07-09
**Related sprint/feature:** Phase H6 — Daily Lesson Module Pipeline, part of the H-track
(`Resource Bank Item → Learn Item/Activity Definition → Module Definition → Daily Lesson/Practice
Gym → Attempt → Feedback + Rating → Learner Memory`), following H5 (Module Foundation). First
phase to actually consume `ModuleDefinition` at runtime.

## Files reviewed / audited before implementation (Step 0)

- `src/LinguaCoach.Api/Controllers/SessionsController.cs`, `src/LinguaCoach.Application/Sessions/
  SessionGeneratorCommands.cs`/`SessionHandlers.cs`, `src/LinguaCoach.Infrastructure/Sessions/
  SessionQueryHandler.cs`/`SessionGeneratorService.cs` — confirmed the Today entry point:
  `GET /api/sessions/today` → `IGetTodaysSessionHandler.HandleAsync(GetTodaysSessionQuery)` →
  `SessionQueryHandler` → `ISessionGeneratorService.GetOrCreateTodaysSessionAsync`, returning
  `TodaysSessionResult`.
- `src/LinguaCoach.Infrastructure/Sessions/LessonBatchGenerationJob.cs`,
  `src/LinguaCoach.Infrastructure/Activity/ActivityMaterializationJob.cs`,
  `TodayBankResourceSelector.cs` — confirmed the **true** existing fallback chain: when the
  bank-first selector finds nothing, `ActivityMaterializationJob` falls back to the legacy
  `IAiActivityGenerator` free-form AI path. `StudentActivityReadinessItem` is a **parallel
  bookkeeping/health ledger**, not read on the Today page-load path — it is not the fallback.
- `src/LinguaCoach.Domain/Entities/LearningModule.cs`, `LearningSession.cs`, `SessionExercise.cs`,
  `LearningActivity.cs` — confirmed the existing per-student runtime hierarchy
  (`LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity →
  ActivityAttempt`) remains untouched; `LearningModule` (required non-nullable FK from
  `LearningSession`) is a thin per-student thematic bucket, structurally unrelated to H5's
  `ModuleDefinition`.
- `src/LinguaCoach.Application/PracticeGym/*`, `PracticeGymGenerationJob.cs` — confirmed Practice
  Gym has its own separate generation/materialization path, not touched this phase.
- `src/LinguaCoach.Domain/Entities/StudentLearningPlan.cs`/`StudentLearningPlanObjective.cs`,
  `src/LinguaCoach.Infrastructure/Curriculum/ICurriculumRoutingService.cs` — reviewed as the
  candidate "target CEFR/skill signal" source; **decision:** v1 uses `StudentProfile.CefrLevel`
  directly instead of fully wiring `ICurriculumRoutingService` (see Decisions below).
- `src/LinguaCoach.Domain/Entities/ModuleDefinition.cs`, `LearnItem.cs`, `ActivityDefinition.cs`
  (H3/H4/H5) — confirmed which fields are student-safe (`LearnItem.Title/Body/ExamplesJson/
  CommonMistakesJson/UsageNotes`; `ActivityDefinition.Title/Description/Instructions/ActivityType/
  FormSchemaJson`) versus backend-only (`ActivityDefinition.AnswerKeyJson`/`ScoringRulesJson` —
  explicitly documented on the entity as never sent to students).
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` — reused as the admin
  diagnostics endpoint convention (`[Authorize(Roles = nameof(UserRole.Admin))]`, read-only GET
  endpoints returning plain summary objects).

**Naming/conflict finding:** none. `ModuleDefinition` (H5) has no existing runtime consumer;
`LearningModule` remains structurally distinct and untouched.

## What was built

1. **Domain:** `StudentDailyModuleAssignment` (additive bookkeeping entity — `StudentId`,
   nullable `ModuleDefinitionId`, `AssignedForDate`, `Status`, `SelectionReason`,
   `FallbackReason`, `EstimatedMinutes`, `ConsumedAt`) and `DailyModuleAssignmentStatus` enum
   (`Selected`/`Presented`/`Skipped`/`Consumed`/`Expired`/`FallbackOnly`). Not a student
   attempt/score record — no answer, score, or mastery state is stored here.
2. **Persistence:** `StudentDailyModuleAssignmentConfiguration` (snake_case table
   `student_daily_module_assignments`, `DeleteBehavior.Restrict` FK to `ModuleDefinition`,
   indexes on `(StudentId, AssignedForDate)`, `(StudentId, ModuleDefinitionId)`, `Status`).
   Migration `Phase_H6_AddDailyLessonModulePipeline` — **additive-only**, one new table, no
   change to any existing table.
3. **Application:** `IDailyLessonModuleSelectionService.SelectAsync` (pure/read-only — no writes)
   with `DailyLessonModuleSelectionRequest`/`DailyLessonModuleSelectionResult`, plus student-safe
   `DailyLessonLearnItemView`/`DailyLessonActivityView` projections (deliberately omit
   `AnswerKeyJson`/`ScoringRulesJson`). `IDailyLessonModuleAssignmentRecorder` is the single write
   path. `TodaysSessionResult` extended with an additive, optional trailing
   `ModuleSection: DailyLessonModuleSelectionResult? = null` parameter (named-argument
   construction elsewhere in the codebase means this is a non-breaking change).
4. **Infrastructure:** `DailyLessonModuleSelectionService` — deterministic, no AI call.
   Eligibility: `ModuleDefinition.ReviewStatus == Approved` **and** at least one linked
   `LearnItem` with `ReviewStatus == Approved` **and** at least one linked `ActivityDefinition`
   with `ReviewStatus == Approved`. CEFR: exact match preferred; if none exists, only broadens to
   all eligible Modules when `AllowFallback` is true, and the returned `Reason` is explicitly
   tagged "review/scaffold selection... fallback" in that case — never a silent lower-level pick.
   Scoring (soft preferences, applied after the CEFR gate): requested skill match, focus/context
   tag overlap (JSON-parsed with a try/catch fallback to an empty list on malformed JSON),
   estimated-minutes closeness to the preferred session length. Reuse guard: excludes any
   `ModuleDefinition` assigned to the student within the last 14 days (via
   `StudentDailyModuleAssignment` history, plus any explicitly-passed
   `RecentAssignedModuleDefinitionIds`). The whole method is wrapped in a top-level try/catch so
   it **never throws** — any unexpected error degrades to `FallbackRequired = true`.
   `DailyLessonModuleAssignmentRecorder` performs an idempotent per-day upsert: if a row already
   exists for the student/date it is a no-op; when the selector required a fallback, **no row is
   written** — a missing row for a given date is itself the fallback signal (there is no
   `ModuleDefinitionId` to anchor a `FallbackOnly` row to in a way that's useful for admin
   diagnostics beyond "nothing was recorded"). Wired into `SessionQueryHandler.HandleAsync
   (GetTodaysSessionQuery)` additively: the existing `_generator.GetOrCreateTodaysSessionAsync`
   call is unchanged, then the module selection + recording is attempted in a **separate**
   try/catch (logged via `ILogger`, never rethrown) before attaching the result via
   `result with { ModuleSection = moduleSection }`.
5. **API:** `AdminDailyLessonModuleController` (admin-only) —
   `GET /api/admin/daily-lesson/modules/preview?studentId=&maxModules=&targetDate=` (calls the
   selector directly, bypassing the recorder — no side effects) and
   `GET /api/admin/daily-lesson/students/{studentId}/assignments?days=` (assignment history from
   `StudentDailyModuleAssignment`).
6. **Angular:** `session.models.ts` extended with `DailyLessonModuleSection`/
   `SelectedDailyLessonModule`/`DailyLessonLearnItemView`/`DailyLessonActivityView` and
   `TodaysSessionResponse.moduleSection`. Student dashboard makes an additive, best-effort call
   to the real `GET /api/sessions/today` (errors swallowed, never affects the primary Today's
   Lesson card which is driven by the separate summary endpoint) and shows a small read-only
   "Today's module" card when a Module was selected. Admin `admin-student-detail` page gets a
   new read-only "Daily Lesson module selection" card (mirrors the existing "Assignment /
   Delivery Queue health" card's loading/error/empty pattern) showing the preview result.
7. **Tests:** 21 new unit tests (`DailyLessonModuleSelectionServiceTests`, SQLite in-memory) + 12
   new integration tests (`DailyLessonModulePipelineEndpointTests`, `ApiTestFactory`) — see exact
   list below. Full backend suite: 3,855 tests (2,308 unit + 1,542 integration + 5 architecture),
   0 failures.
8. **Docs:** this review, plus road-map.md, current-sprint.md, current-product-state.md,
   product-backlog.md, product-model-realignment-h0.md, learning-activity-engine.md,
   architecture/README.md.

## Decisions made (P1 — design decisions requiring an explicit call)

1. **Materialization path.** Chose the ticket's own "preferred safe option": an additive,
   optional `ModuleSection` field on `TodaysSessionResult`, computed in a separate try/catch in
   `SessionQueryHandler`, rather than injecting `ModuleDefinition`-derived content into the
   AI-driven `SessionGeneratorService`/`ActivityMaterializationJob`/`LearningSession`/
   `SessionExercise`/`LearningActivity` creation flow. Rationale: zero risk to the existing,
   already-safe generation path; a Module-selection bug can only ever leave `ModuleSection` null.
2. **`StudentDailyModuleAssignment.ModuleDefinitionId` made nullable.** The originally-drafted
   entity (from the Domain-layer step of this phase) had a non-nullable `ModuleDefinitionId`,
   which cannot represent `DailyModuleAssignmentStatus.FallbackOnly` (no Module was selected that
   day). Changed to `Guid?` with a constructor invariant (`null` only allowed when
   `Status == FallbackOnly`) before the migration was generated — caught during Infrastructure
   implementation, before any migration was applied anywhere.
3. **No `FallbackOnly` rows are actually written.** Given the nullable FK above, a `FallbackOnly`
   row *could* be written, but the recorder deliberately doesn't: "no row for this student/date"
   is already an unambiguous fallback signal for the admin assignment-history endpoint, and
   writing a row with no Module reference adds bookkeeping without adding information. Documented
   directly in `DailyLessonModuleAssignmentRecorder`'s doc comment.
4. **CEFR signal: `StudentProfile.CefrLevel` only, not `ICurriculumRoutingService`.** The ticket
   allowed "learning plan or weakness signals where safely available." Fully wiring
   `ICurriculumRoutingService.RecommendAsync` requires constructing a `CurriculumRoutingRequest`
   via `CurriculumRoutingRequestFactory.Build`, which pulls in mastery/readiness-pool state not
   otherwise needed here. v1 uses `StudentProfile.CefrLevel` directly; `RequestedSkill`/
   `FocusAreas`/`ContextTags` are accepted on the request but the current `SessionQueryHandler`
   call site does not yet populate them from the student's learning plan — tracked as
   `TODO-H6-1` for a future phase to wire in real weak-skill/objective signals.
   `DailyLessonModuleSelectionRequest` already accepts these fields so no further request-shape
   migration is needed when that's done.
5. **Reuse/novelty guard: direct `StudentDailyModuleAssignment` lookback, not
   `IActivityNoveltyPolicy`/`IActivityContentFingerprintService`.** Those existing services target
   generated-`LearningActivity`-content dedup (exact-match/cooldown on synthesized content); Module
   identity reuse is a much simpler "was this same admin-curated Module assigned to this student
   in the last 14 days" check, which the new assignment table already answers directly.
6. **Skill-diversity dedup, not a full "balanced coverage" planner.** When `MaxModules > 1`, the
   selector skips a same-skill Module only if a more-diverse option remains in the ranked pool;
   otherwise it still fills the slot (duplicates are allowed rather than under-filling). Kept
   deliberately simple per the ticket's own "do not overbuild a full lesson planner" precedent
   from H5.

## AskUserQuestion decisions

None — the phase brief was fully self-contained; no ambiguity required a user clarification
during implementation.

## Backend tests added (33 total: 21 unit + 12 integration)

**Unit (`DailyLessonModuleSelectionServiceTests`):** approved module selected for matching
CEFR/skill; pending module not selected; rejected module not selected; approved module with
pending Learn Item not selected; approved module with pending Activity Definition not selected;
wrong-CEFR module not silently selected when an exact match exists; lower-level module selected
only with an explicit review/scaffold/fallback reason; context/focus tags influence selection;
learning-plan-derived skill signal influences selection; estimated minutes respects preferred
session length; recently-used module not selected again too soon (via explicit recent-ids list);
recently-used module excluded via assignment history; no suitable module returns
fallback-required, not an exception; malformed module JSON handled safely; no-CEFR student uses
safe broad matching; no-learning-plan-signal student uses safe broad matching; selection result
does not expose answer keys; selection creates no Module attempts; selection does not mutate
`ModuleDefinition`; selection does not mutate `LearnItem`/`ActivityDefinition`; selection creates
no Practice Gym records.

**Integration (`DailyLessonModulePipelineEndpointTests`):** Today returns a module section when a
compatible approved Module exists; Today falls back when none exists; Today's module section does
not expose answer keys; admin preview shows selected Modules; admin preview shows a fallback
reason when no Module is available; existing Today idempotency (same session on repeat calls)
still works; existing Practice Gym suggestions endpoint still works; H3 Learn Items endpoint still
works; H4 Activities endpoint still works; H5 Modules endpoint still works; non-admin rejected
(403) for the admin preview endpoint; existing readiness-pool health endpoint not broken.

## Risks or unresolved questions

- `TODO-H6-1`: `SessionQueryHandler` does not yet pass real learning-plan/weak-skill signals into
  `DailyLessonModuleSelectionRequest.RequestedSkill`/`FocusAreas`/`ContextTags` — the request
  shape supports it, but the call site currently only passes `CefrLevel`. Follow-up phase work,
  not a defect for H6's scope.
- No automated frontend test exercises the new dashboard module card or the admin preview card
  directly (consistent with H3/H4/H5 precedent for content-studio-adjacent UI) — the production
  build was used to confirm no new TS/Angular compile errors instead.
- PG-v2/H7 sequencing remains a future Plan-Sync checkpoint, unchanged by this phase.

## Final verdict

**Complete and accepted.** All 23 acceptance criteria from the phase brief are met: Today still
loads via the unchanged existing generation path in every case; a compatible approved Module (with
an approved Learn Item and an approved Activity Definition) is projected into an additive,
optional `ModuleSection` with student-safe content only (no answer keys, no scoring rules, no
admin-only notes); every "no suitable content" case (no CEFR, no plan, all recently used, malformed
JSON, pending/rejected Modules or links, an unexpected exception) degrades to
`FallbackRequired = true` and never throws or blocks Today; the selector performs zero database
writes and creates zero Module attempts, Practice Gym records, or mutations to
`ModuleDefinition`/`LearnItem`/`ActivityDefinition`; admin-only read-only diagnostics exist for
both "what would be selected" (preview) and "what was recorded" (assignment history); Practice
Gym, the readiness/delivery queue, and the H3/H4/H5 admin endpoints are all unchanged and covered
by regression tests; the full backend suite (3,855 tests) and the Angular production build both
pass (only the pre-existing bundle-size budget warning); committed locally, not pushed, not
deployed.

**Confirmed:** H7 not started. PG-v2 not started. No Practice Gym module pipeline. No student
self-directed module selection. No Module attempts. No final module scoring. No learner mastery
updates from Modules. No `ActivityTemplate`/`LearningActivity`/`LearningSession` replacement. No
readiness-pool deletion. No delivery-queue deletion. Today fallback remains intact. Practice Gym
fallback remains intact. No physical `ResourceBankItem` consolidation. No external datasets. No
Persian/bilingual content. No direct final-table seeding.

## Next recommended action

**Phase H7 — Practice Gym Module Pipeline** (per the H-track), the second runtime consumer of
`ModuleDefinition`, plus `TODO-H6-1` (real learning-plan/weak-skill signal wiring for Daily
Lesson selection) as a smaller near-term follow-up.
