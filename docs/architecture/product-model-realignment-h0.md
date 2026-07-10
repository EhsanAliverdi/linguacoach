---
status: current
lastUpdated: 2026-07-10 (Phase H9B)
owner: product
supersedes:
supersededBy:
---

# Product Model Realignment — Content Studio, Learn, Activity, Module, Lesson, Practice Gym

**Phase H0, 2026-07-09. Docs-only.** No code, migration, entity, API, Angular, or test change was
made in this phase. This document defines the intended product model, audits the current mismatch
against it, and proposes a safe phased implementation path (the H-track). It does not implement
any of it.

**Phase H1, 2026-07-09. Implemented.** H1 built the §4 Option B direction: `ResourceBankQueryService
.ListUnifiedAsync` (new `UnifiedResourceBankContracts.cs` DTOs) aggregates the four typed published
bank tables into one filtered/paginated view — no physical `ResourceBankItem` table, no schema/
migration. New `GET /api/admin/resource-bank` endpoint and `/admin/resource-bank` admin page
("Resource Bank"), added as the first "Content Banks" nav item; the four typed pages/APIs/tables
are unchanged and remain fully reachable. Generate Learn/Activity/Module row actions are disabled
"coming soon" placeholders — H3/H4/H5 do not exist yet. +22 backend tests (3,693 total).

**Phase H2, 2026-07-09. Implemented.** H2 built Import Content UX v1: a product-friendly admin
wrapper (`IContentImportService`/`ContentImportService`, `POST /api/admin/content-imports`,
`/admin/content/import` page, "Import Content" nav item — first Content Banks item) over the
existing Phase E1 pipeline (`IResourceImportService.ImportAsync`). Admin pastes text/CSV/JSON
(`pasted_text`/`csv_text`/`json_text` — file upload stays on the existing Resource Import Runs
page, out of scope here), picks a broad resource type (vocabulary/grammar/reading — Listening/
Speaking/Writing/Mixed-AI-detect are "coming soon": `ResourceCandidateType` has no shape for them
yet) and default metadata (CEFR/skill/subskill/context tags/focus tags/difficulty band). Defaults
apply only when a row doesn't already carry its own value; an invalid row or default CEFR falls
back and produces a raw-record warning rather than rejecting the row. `ResourceImportRequest`
gained optional `DefaultCandidateType`/`Default*` fields (all null for every existing file-upload
caller — zero behavior change there). `ContentImportService` finds-or-creates (and
auto-approves) the named `CefrResourceSource` — no new entity, no schema/migration change; every
field it writes already existed on `ResourceCandidate` since E1/E6/E8. Result staged exactly like
a file import: pending `ResourceCandidate` rows, reviewed/approved/published only through the
existing Resource Candidates page (unchanged). No AI structure analysis — deterministic mapping
only, honestly labeled as such in the UI. +16 unit tests, +6 integration tests (3,715 total).
Full detail: `docs/roadmap/road-map.md` §1, Decision Log (Phase H2 entry).

**Phase H3, 2026-07-09. Implemented.** H3 built the Learn Item foundation — the first half of
`Resource Bank Item → Learn Item/Activity → Module`. New `LearnItem` entity (reviewable
teaching/explanation block: title/body/examples/common mistakes/usage notes, CEFR/skill/subskill/
context/focus/difficulty metadata, `SourceMode` Manual/GeneratedFromResources/Imported,
`GenerationProvider`/`GenerationModel`, reuses `AdminReviewStatus` for its approval lifecycle —
always starts `PendingReview`, never auto-published) and `LearnItemResourceLink` (traceability
back to the published `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`/
`CefrReadingPassage` row(s) it's about, keyed by a new Domain `PublishedResourceType` enum +
`LearnItemResourceRole` Primary/Supporting). Additive-only migration
(`Phase_H3_AddLearnItemFoundation`) — two new tables, no changes to any existing table.
`IGenerateLearnItemFromResourcesHandler`/`LearnItemGenerationService` composes a **deterministic**
draft directly from the selected resources' own fields — no AI provider call, because no existing
AI service in this codebase generates teaching prose from source text (every existing generator is
scoped to activity/exercise/learning-path content) and adding a new AI feature key was judged out
of scope for a foundation phase; `GenerationProvider` is honestly stamped `"Deterministic"`, never
a fake AI attribution. New endpoints under `api/admin/learn-items` (list/get/create/
generate-from-resources/update/approve/reject, admin-only). New Angular page `/admin/learn-items`
("Learn Items"), added to the Content Banks nav right after Resource Bank. The H1 unified Resource
Bank page's "Generate Learn" row action is un-disabled (Generate Activity/Generate Module stay
"coming soon" — H4/H5 don't exist yet); `UnifiedResourceBankItemDto.LinkedLearnCount` is now a real
count instead of always-null. +22 unit, +8 integration tests (3,715 → 3,745). No Activity/Module entity, no
student assignment, no Today/Practice Gym change. Full detail: `docs/roadmap/road-map.md` §1,
Decision Log (Phase H3 entry).

**Phase H4, 2026-07-09. Implemented.** H4 built the Activity foundation — the "Practice" half of
`Resource Bank Item → Learn Item/Activity → Module`. New `ActivityDefinition` entity (reviewable,
editable practice task design: title/description/instructions, `ActivityType`/`PatternKey`,
`RendererType` Formio/Custom/Legacy, `FormSchemaJson` (student-safe Form.io schema),
`AnswerKeyJson`/`ScoringRulesJson`/`FeedbackPlanJson` (backend-only), CEFR/skill/subskill/context/
focus/difficulty metadata, optional `LearnItemId`, `SourceMode` Manual/GeneratedFromResources/
GeneratedFromLearnItem/Imported, `GenerationProvider`/`GenerationModel`, reuses `AdminReviewStatus`
— always starts `PendingReview`, editing an approved Activity is blocked) and
`ActivityResourceLink` (traceability back to published resources, structurally identical to
`LearnItemResourceLink`, reusing the same `PublishedResourceType`/`LearnItemResourceRole` enums).
**Explicitly distinct from two existing similarly-named entities**: `LearningActivity` (a
per-student runtime/delivery record — Today materialization, Practice Gym) and `ActivityTemplate`
(an existing admin-authored template already wired into the live Practice Gym Form.io pilot
runtime) — `ActivityDefinition` is a new H4 foundation-phase entity with Resource Bank/Learn Item
traceability that neither of those has, and is **not wired into any runtime selection/delivery
path** in this phase. Additive-only migration (`Phase_H4_AddActivityFoundation`) — two new tables,
no change to any existing table (including `ActivityTemplate`'s own tables). Deterministic
composer (`IGenerateActivityFromResourcesHandler`/`IGenerateActivityFromLearnItemHandler`/
`ActivityGenerationService`) — no AI call, same reasoning as H3's Learn Item generation. Three
initial `ActivityType`s: `gap_fill` (Vocabulary/Grammar — type the term given its definition,
deterministically scored) and `multiple_choice_single` (Vocabulary/Grammar — choose the correct
definition among distractors pulled from sibling published resources; generation is rejected
outright, not degraded to a single-option choice, when no distractor exists) and `short_answer`
(ReadingReference/ReadingPassage — open-ended comprehension prompt, honestly marked
`RequiresManualOrAiEvaluation=true`, never a fake score). `ScoringRulesJson` is serialized straight
from the existing shared `ScoringRulesDocument`/`ComponentScoringRule` types (already used by
placement/onboarding/reorder_paragraphs scoring) and every generated `FormSchemaJson` is validated
through the existing `IFormIoSchemaValidationService` before saving — no new scoring format, no new
schema-safety logic. New endpoints `api/admin/activities` (list/get/create/
generate-from-resources/generate-from-learn-item/update/approve/reject, admin-only). New Angular
page `/admin/activities` ("Activities"), added to the Content Banks nav right after Learn Items.
"Generate Activity" is now live on both the H1 unified Resource Bank page's row action (previously
"coming soon") and the H3 Learn Item detail drawer; `UnifiedResourceBankItemDto.LinkedActivityCount`
is now a real count (was always null). +29 unit, +10 integration tests (3,745 → 3,784). No Module
entity, no student assignment, no Today/Practice Gym runtime change. Full detail:
`docs/roadmap/road-map.md` §1, Decision Log (Phase H4 entry).

**Phase H5, 2026-07-09. Implemented.** H5 built the Module Definition foundation — the top of
`Resource Bank Item → Learn Item/Activity Definition → Module Definition`. New `ModuleDefinition`
entity (reviewable learning unit: title/description/objective key, CEFR/skill/subskill/context/
focus/difficulty metadata, module-level `FeedbackPlanJson`, `SourceMode` Manual/
GeneratedFromLearnAndActivities/GeneratedFromResources/Imported, reuses `AdminReviewStatus` — always
starts `PendingReview`, editing an approved Module blocked, same policy as Learn Item/Activity) and
two link tables, `ModuleDefinitionLearnItemLink` (reuses `LearnItemResourceRole` Primary/Supporting)
and `ModuleDefinitionActivityLink` (new `ModuleActivityRole` PrimaryPractice/SupportingPractice/
Review/Extension), both carrying a `SortOrder` and a denormalized `SnapshotTitle`. **Named
`ModuleDefinition`, deliberately distinct from the existing runtime `LearningModule`** (a
per-student thematic group of `LearningActivity` rows within a `LearningPath`, tracks its own
completion) — mirrors H4's `ActivityDefinition`-vs-`LearningActivity`/`ActivityTemplate` naming
decision. Additive-only migration (`Phase_H5_AddModuleDefinitionFoundation`, three new tables — no
change to any existing table). `ModuleGenerationService` implements all four generation entry
points (`IGenerateModuleFromItemsHandler`/`IGenerateModuleFromResourceHandler`/
`IGenerateModuleFromLearnItemHandler`/`IGenerateModuleFromActivityHandler`) — **deterministic, no
AI call**, and composes only EXISTING Learn Items/Activity Definitions (never cascade-generates
new ones). Every generation entry point requires its source Learn Item(s)/Activity Definition(s)
to already be `Approved` — a draft/pending source is rejected with a clear message naming what to
approve first, never silently pulled in. New endpoints `api/admin/modules` (list/get/create/
generate-from-items/generate-from-resource/generate-from-learn-item/generate-from-activity/update/
approve/reject, admin-only). New Angular page `/admin/modules` ("Modules"), added to the Content
Banks nav right after Activities, including a simple generate-from-items modal (admin types an
approved Learn Item id + an approved Activity Definition id). "Generate Module" is now live on the
H1 unified Resource Bank page's row action (previously "coming soon" — only succeeds when an
approved Learn Item AND an approved Activity Definition are both already linked to that resource),
the H3 Learn Item detail drawer, and the H4 Activity detail drawer;
`UnifiedResourceBankItemDto.LinkedModuleCount` is now a real count (reachable via either the
Learn-Item or the Activity link chain). +27 unit, +11 integration tests (3,784 → 3,822). No
student assignment, no Module attempts, no Today/Practice Gym runtime change. Full detail:
`docs/roadmap/road-map.md` §1, Decision Log (Phase H5 entry), and
`docs/reviews/2026-07-09-phase-h5-module-foundation-review.md`.

**Phase H6, 2026-07-09. Implemented.** H6 is the first phase to actually consume
`ModuleDefinition` at runtime. New `IDailyLessonModuleSelectionService.SelectAsync` — pure/
read-only (no database writes), deterministic (no AI call) — selects an Approved
`ModuleDefinition` with at least one Approved linked `LearnItem` AND at least one Approved
linked `ActivityDefinition`. Prefers an exact CEFR match; only broadens to another level as an
explicit "review/scaffold... fallback" selection when `AllowFallback` is true and no exact match
exists — the returned `Reason` always names this explicitly, never a silent lower-level pick.
Soft preferences (applied after the CEFR gate): requested skill match, focus/context tag overlap
(malformed tag JSON degrades safely to an empty list, never throws), estimated-minutes fit to the
preferred session length. New additive `StudentDailyModuleAssignment` bookkeeping table
(`Phase_H6_AddDailyLessonModulePipeline` migration — one new table, no change to any existing
table) drives a 14-day reuse guard and admin diagnostics; it is not a student attempt/score
record. The whole selector is wrapped in a top-level try/catch so it **never throws** — every "no
suitable content" case (no CEFR, no compatible Module, everything recently used, an unexpected
error) returns `FallbackRequired = true` instead. Wired into
`SessionQueryHandler.HandleAsync(GetTodaysSessionQuery)` **additively**: the existing
`ISessionGeneratorService.GetOrCreateTodaysSessionAsync` call is completely unchanged; module
selection runs in a **separate** try/catch (logged, never rethrown) and attaches the result via
`result with { ModuleSection = moduleSection }` on a new optional trailing field of
`TodaysSessionResult`. Student-safe projections only — `DailyLessonLearnItemView`/
`DailyLessonActivityView` deliberately omit `ActivityDefinition.AnswerKeyJson`/`ScoringRulesJson`.
New admin-only, read-only `GET api/admin/daily-lesson/modules/preview` (calls the selector
directly, bypassing the recorder — no side effects) and
`GET api/admin/daily-lesson/students/{id}/assignments`. Minimal Angular additions: a read-only
"Today's module" card on the student dashboard (best-effort, errors swallowed, never affects the
primary Today's Lesson card driven by the separate dashboard-summary endpoint) and a "Daily
Lesson module selection" diagnostic card on the admin student-detail page. +21 unit, +12
integration tests (3,822 → 3,855). No H7/Practice-Gym-module-pipeline started, no PG-v2 started,
no student self-directed module selection, no Module attempts, no module scoring, no learner
mastery updates from Modules, no `ActivityTemplate`/`LearningActivity`/`LearningSession`
replacement, no readiness-pool/delivery-queue deletion, no Today/Practice-Gym fallback removed.
Full detail: `docs/roadmap/road-map.md` §1, and
`docs/reviews/2026-07-09-phase-h6-daily-lesson-module-pipeline-review.md`. **`TODO-H6-1`:**
`SessionQueryHandler` only passes `CefrLevel` into the selection request today —
`RequestedSkill`/`FocusAreas`/`ContextTags` are accepted by the request shape but not yet
populated from the student's learning plan; a future phase should wire those in.

**Phase H7, 2026-07-09. Implemented.** H7 is the second phase to consume `ModuleDefinition` at
runtime — Practice Gym, after H6's Daily Lesson. Before implementation, H6's ancestry was
verified (`git merge-base --is-ancestor <H5 commit> <H6 commit>` confirmed H6 includes H5) per the
phase brief's mandatory Step -1. New `IPracticeGymModuleSelectionService.SelectAsync` — pure/
read-only, deterministic, no AI call — uses the same eligibility rule as H6 (Approved
`ModuleDefinition` with at least one Approved linked `LearnItem` AND at least one Approved linked
`ActivityDefinition`), extended with Practice Gym's self-directed signals
(`RequestedSkill`/`RequestedSubskill`/`RequestedObjectiveKey`/`RequestedDifficulty`/
`WeaknessSignals`) and per-suggestion `IsReview`/`IsScaffold`/`IsRemediation` flags. Self-directed
requests narrow the eligible pool but degrade gracefully to the broader pool when over-narrow
(Practice Gym is a soft preference, unlike Today's automatic selection). Same "exact CEFR match
preferred, only ever broadens as an explicit review/scaffold/remediation/fallback selection"
behavior as H6. New additive `StudentPracticeGymModuleAssignment` bookkeeping table
(`Phase_H7_AddPracticeGymModulePipeline` migration — one new table, no change to any existing
table), idempotent per student per calendar day (Practice Gym suggestions recompute on every page
load, unlike Today's once-per-day generation) — the 14-day reuse-guard query is a
fetch-then-filter-client-side pattern because SQLite's EF Core provider cannot translate
`DateTimeOffset` comparisons server-side (a test-only limitation; the client-side filter is
correct on PostgreSQL too). Never throws. Wired into
`PracticeGymSuggestionService.GetSuggestionsForStudentAsync` **additively**: the existing
readiness-pool-backed suggestion logic (ranking, dedup, pilot gating) is completely unchanged;
module selection runs in a **separate** try/catch and attaches the result via a new optional
`PracticeGymSuggestionsDto.ModuleSuggestions` property. Student-safe projections only —
`PracticeGymModuleLearnItemSummary`/`PracticeGymModuleActivitySummary` deliberately omit
`ActivityDefinition.AnswerKeyJson`/`ScoringRulesJson`. **No new student "start" endpoint**: the
Step 0 audit confirmed `ActivityDefinition` has no attempt/scoring runtime wired to it anywhere —
building one safely was out of scope, so module suggestions are display-only this phase (a
"Coming soon" label), consistent with the phase brief's own permission to fall back to
suggestions-only. New admin-only, read-only `GET api/admin/practice-gym/modules/preview` (calls
the selector directly, bypassing the recorder) and
`GET api/admin/practice-gym/students/{id}/assignments`. Minimal Angular additions: a read-only
"Recommended module practice" section on the student Practice Gym page (no new network call — the
module section rides on the existing suggestions response) and a "Practice Gym module selection"
diagnostic card on the admin student-detail page, mirroring H6's exactly. +26 unit, +14
integration tests (3,855 → 3,895). No PG-v2 started, no full Practice Gym redesign, no student
self-authored/custom module creation, no Module attempts, no module scoring, no learner mastery
updates from Modules, no `ActivityTemplate`/`LearningActivity`/`LearningSession`/
`PracticeActivityCache` replacement, no readiness-pool/delivery-queue deletion, no
Today/Practice-Gym fallback removed, no legacy bank/admin structure removal. Full detail:
`docs/roadmap/road-map.md` §1, and
`docs/reviews/2026-07-09-phase-h7-practice-gym-module-pipeline-review.md`.

**Plan-Sync-After-H7, 2026-07-09. Docs-only, complete.** Following H7, this planning phase
confirmed H6/H7 are both additive and fallback-safe (neither replaced any existing runtime path)
and recorded the user's clarified cleanup direction: legacy invalid bank/admin structures should
be **removed, not merely hidden**. A full Step 0 audit classified every pre-H-track structure —
Cefr* bank entities, `ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate`,
`ResourceBankQueryService`/`UnifiedResourceBankItemDto`, `ActivityTemplate`,
`LearningActivity`/`LearningSession`/`SessionExercise`/`LearningModule`, `PracticeActivityCache`,
`StudentActivityReadinessItem`, `PracticeGymSuggestionService`/`PracticeGymPoolService`,
`ActivityMaterializationJob`/`LessonBatchGenerationJob`/`PracticeGymGenerationJob`, the Today and
Practice Gym legacy AI-generation fallbacks, the four typed admin resource-bank pages, and
`admin-lessons.component.ts` — by removal risk. **Finding: almost every audited structure is
still either core runtime infrastructure the live Today/Practice Gym pipelines actively depend
on, or a live legacy AI-generation fallback still covering content the bank-first/module-first
paths can't yet serve — nothing is yet a proven-safe H9-style removal candidate.** The only
concrete, low-risk action identified is trimming the redundant admin *navigation* for the four
typed resource-bank pages (not their tables, APIs, or data) — an H8 job. Defined three new future
phases (**scope only, not implemented**): H8 (Content Studio/Admin IA Cleanup and Removal
Readiness — safe UI/nav cleanup), H9 (Legacy Bank Structure Removal and Consolidation — the first
genuinely destructive phase, gated on a per-item safety audit; may split into H9A-H9D for
physical `ResourceBankItem` consolidation), and H10 (ActivityDefinition Runtime Launch Path /
Attempt Bridge — must resolve before H9 could ever remove `ActivityTemplate`, since it remains
the only path that launches a scored Form.io pilot activity). No application code, migration,
table, entity, API, or UI page changed. Full detail:
`docs/reviews/2026-07-09-plan-sync-after-h7-legacy-bank-removal-strategy.md`.

**Phase H8, 2026-07-10. Implemented.** H8 executed the one concrete, low-risk action
Plan-Sync-After-H7 identified — frontend/docs-only admin cleanup, **not** the destructive
backend/table cleanup (that's H9). Split the admin sidebar's single "Content Banks" nav section
into **Content Studio** (Import Content → Resource Bank → Learn Items → Activities → Modules —
the primary content-authoring flow, in that order) and **Content Ops** (Resource sources/import
runs/candidates, Activity templates, Review queue, Placement items, Onboarding — still-live
support/staging surfaces per Plan-Sync-After-H7's classification, just no longer mixed into the
primary flow). Removed the four typed resource-bank nav entries (Vocabulary/Grammar/Reading
reference/Reading passage bank) from both the desktop sidebar and mobile drawer —
**navigation-only**: their routes, components, and backing tables/APIs (`CefrVocabularyEntry`
etc.) all remain fully reachable and untouched. Updated Learn Items/Activities/Modules page
subtitles to drop stale "future Modules"/"will power future..." language (all live since H5-H7)
and to state explicitly that launching a scored Module/Activity attempt is not implemented yet
(H10); added a pointer from each typed bank page to the unified Resource Bank. Updated the
existing admin-nav Karma spec (removed an obsolete required-route assertion, added two cheap
assertions for the Content Studio flow and the typed-bank-pages-removed-from-nav state). **No
backend file, migration, table, entity, or API was touched; no route or component was deleted.**
`ActivityTemplate`, `PracticeActivityCache`, `StudentActivityReadinessItem`, the runtime session
entities, and both Today/Practice Gym legacy fallbacks are all untouched. Frontend production
build: no new TS/Angular errors, only the pre-existing bundle-size budget warning. The full Karma
suite could not run — pre-existing, unrelated spec-fixture gaps from H6/H7 and an earlier
feedback-policy phase block the shared test bundle, confirmed via `git log` to predate H8;
tracked as new `TODO-H8-2` rather than expanding H8's scope to fix. Full detail:
`docs/reviews/2026-07-10-phase-h8-content-studio-admin-ia-cleanup-review.md`.

**Phase H10, 2026-07-10. Implemented.** H10 gives an approved `ActivityDefinition` (H4) its first
real launch/attempt/scoring path, reached only through an approved `ModuleDefinition` suggestion
in Practice Gym (H7). Deliberately out of order relative to H9 (destructive legacy cleanup) — the
user's H10 brief explicitly said not to start H9 yet, since `ActivityTemplate` couldn't safely be
removed until `ActivityDefinition` had some proven launch path. **Chosen option: a hybrid bridge
(Option C framing, executed via Option B's mechanism).** The Step 0 audit found the entire
existing scored Form.io path — `ActivityTemplate` materialization →
`LearningActivity.SetFormIoContent` → `POST api/activity/{id}/attempt` →
`ActivitySubmitHandler.HandlePatternEvaluationAsync` (content-driven dispatch on
`FormIoSchemaJson`, not pattern-driven) → `FormIoPatternEvaluator` →
`IPlacementScoringService.ScoreSubmission` → `ComponentAnswerScorer.Score` — is keyed entirely off
`LearningActivity.FormIoSchemaJson`/`ScoringRulesJson`, and confirmed by reading
`ActivityGenerationService`'s actual serialization code that `ActivityDefinition.ScoringRulesJson`
is already byte-compatible with `ComponentScoringRule`/`ScoringRulesDocument` — no adapter needed.
`ActivityAttempt.LearningActivityId` is a required, non-nullable, constructor-enforced field, so a
native parallel attempt entity (Option A) would have to reimplement the ledger write, multi-skill
progress, memory update, and readiness/usage-log bookkeeping that today live entirely inside
`ActivitySubmitHandler` — pure risk with no offsetting benefit. H10 therefore materializes an
eligible `ActivityDefinition` into a real `LearningActivity` (reusing the existing, already-seeded
`ExercisePatternKey.FormIoPracticeGymPilot` generic marker pattern — the same one
`ActivityTemplate`'s pilot uses) via `SetFormIoContent`, so the entire downstream pipeline works
unchanged. A new, minimal `StudentActivityDefinitionLaunch` bridge table
(`Phase_H10_AddActivityDefinitionLaunchBridge` migration — one new table, no change to any
existing table, including `LearningActivity`/`ActivityAttempt`) preserves traceability to
`ModuleDefinition`/`ActivityDefinition`/`LearnItem` — it is not itself a student attempt/score
record. New `ActivityDefinitionLaunchEligibility` — a pure, static, exception-safe check (Approved
Module + Approved Activity Definition + `Formio` renderer + valid Form.io schema + supported
`ActivityType` (`gap_fill`/`multiple_choice_single` only — `short_answer` and anything flagged
`RequiresManualOrAiEvaluation` stay unsupported) — shared by H7's selector (to precompute
`CanLaunch`/`UnsupportedReason` on every suggestion, no extra round trip) and the new
`IActivityDefinitionLaunchService` (to re-validate fresh at click time). New
`POST api/practice-gym/module-suggestions/{moduleDefinitionId}/start` on the existing
`PracticeGymSuggestionsController` — resolves the caller's own profile server-side (a student can
never launch on another's behalf), always 200 with `Success=false` for every non-launchable case,
matching `StartSuggestionResult`'s existing non-throw convention. Student-safe launch result omits
`AnswerKeyJson`/`ScoringRulesJson` entirely. Practice Gym's H7 "Recommended module practice"
section now shows a real Start button when launchable (navigates to the existing, unmodified
`/activity?activityId=...` page — same Form.io renderer, same submit endpoint, zero new frontend
rendering/scoring code) and a clear, student-safe "not launchable yet" reason otherwise. Admin
diagnostic card extended with a Launchable/reason badge per suggestion — no controller change
needed, since the admin preview endpoint already returns the raw selection result. +16 unit, +2
regression, +12 integration tests (3,895 → 3,925). Frontend production build clean (only the
pre-existing bundle-size warning); the pre-existing Karma compile failure (`TODO-H8-2`) is
unchanged — re-confirmed the same six spec files fail identically and none were touched by H10.
No H9 destructive cleanup, no PG-v2, no full Practice Gym redesign, no learner mastery updates
from Modules, no native `ActivityDefinition` attempt runtime (deferred, `TODO-H10-3`), no Today
launch integration (deferred, `TODO-H10-2`). `ActivityTemplate`, `PracticeActivityCache`,
`StudentActivityReadinessItem`, and the full runtime session-entity chain are all untouched. Full
detail: `docs/roadmap/road-map.md` §1, and
`docs/reviews/2026-07-10-phase-h10-activitydefinition-runtime-launch-bridge-review.md`.

**Phase H9A, 2026-07-10. Implemented.** First H9 cleanup phase (split H9A/H9B/H9C/H9D — see
`TODO-H9-1`). **Frontend/admin cleanup only** — deliberately did not attempt physical
`ResourceBankItem` consolidation (that's H9B's decision to make) or touch any typed bank table,
backend service method, or runtime dependency. Removed the four legacy typed admin bank Angular
pages/components/routes (vocabulary/grammar/reading-references/reading-passages) — H8 had already
removed their nav links; H9A removed the actual page components and route definitions, confirmed
unreachable and unused via grep before deletion. Removed the orphaned `AdminResourceBankService`
Angular service (8 typed list/detail methods, used only by the deleted pages) and 12 dead frontend
model interfaces. Old routes now redirect via Angular's `RedirectFunction` (confirmed supported in
this project's Angular 19.2.0) to the unified Resource Bank with a matching type filter (e.g.
`/admin/resource-banks/vocabulary` → `/admin/resource-bank?type=vocabulary`);
`AdminResourceBankUnifiedComponent` now injects `ActivatedRoute` and reads `?type=` on init to
pre-seed its filter, falling back to "all" on an unrecognized value. One small, non-destructive
backend fix: `UnifiedResourceBankItemDto.DetailRoute` — previously hardcoded to the just-removed
typed routes at 4 construction sites in `ResourceBankQueryService.cs`, which would otherwise have
become a dead 404 link in the unified page's own detail drawer — is now always `null`; the dead
link block was removed from the template. `AdminResourceBankController`'s 8 typed HTTP actions and
`IResourceBankQueryService`'s 8 typed methods were deliberately kept — the latter is load-bearing
for `TodayBankResourceSelector`, a student-facing Today feature completely unrelated to the admin
UI being cleaned up. +3 Angular unit tests (Karma bundle still blocked by pre-existing
`TODO-H8-2`). All 3,925 backend tests still pass; frontend production build clean (only the
pre-existing bundle-size warning). No typed bank table/data removed, no destructive EF migration,
no physical `ResourceBankItem` consolidation, no import/publish pipeline, `ActivityTemplate`,
`PracticeActivityCache`, `StudentActivityReadinessItem`, runtime session entities, Today/Practice
Gym fallback, or ActivityDefinition launch bridge touched. Full detail:
`docs/roadmap/road-map.md` §1, and
`docs/reviews/2026-07-10-phase-h9a-legacy-admin-code-path-removal-review.md`.

**Phase H9B, 2026-07-10. Implemented (docs/design-only).** Answered the question H9A left open:
should the 4 typed published bank tables be physically consolidated into one `ResourceBankItem`
table? **Recommendation: no — keep the typed tables and the existing unified admin read model**
(Option A, converging toward Option E, from a 5-option comparison against B/C/D). A code-level
audit of the 4 typed entities, `ResourceCandidatePublishService`'s publish routing,
`ResourceBankQueryService.ListUnifiedAsync`'s in-memory unification, every caller of typed
resource methods (found `TodayBankResourceSelector`, `LearnItemResourceLookup`, and
`ActivityGenerationService` all bypass `ResourceBankQueryService` and query typed tables/methods
directly), the `LearnItemResourceLink`/`ActivityResourceLink` polymorphic link pattern
(`ResourceType`+`ResourceId`, already working), and every seeder (confirmed none writes typed
tables directly, bypassing the candidate pipeline) found the 4 types hold genuinely different
field shapes (`CefrReadingPassage` alone has 8 fields none of the others have), and the one real
pain point (`ListUnifiedAsync`'s per-type in-memory scan, not a genuine DB-side union) has a
materially cheaper fix than a physical table — a SQL `UNION ALL` view (new `TODO-H11-1`). A full
target schema (hybrid columns + `ContentJson`), link-migration strategy, publish-flow strategy,
selector migration order, and removal safety gate checklist are documented for a future
re-evaluation but **not implemented**. No EF migration, no new table, no data migration, no typed
table/API removal, no `ResourceBankQueryService`/selector/import-publish rewrite. `TODO-H9B-1`
closed; `TODO-H9C-1`/`TODO-H9D-1` re-scoped as conditional placeholders; `TODO-H11-1` added. Full
detail: `docs/roadmap/road-map.md` §1, and
`docs/reviews/2026-07-10-phase-h9b-resourcebankitem-consolidation-decision.md`.

---

## 1. Why this phase exists

D6 closed the bank-first selector-quality track (E6→E10, D1→D6): Today lessons now pull
context/subskill/topic-aware bank content through a deterministic relaxation ladder. That work is
real and stays. But it was all built as **selector engineering on top of the existing admin/data
model** — `ResourceCandidate` → typed `Cefr*` bank tables → `ActivityMaterializationJob` prompt
injection. The admin/product model itself never caught up: there is no `Learn Item` concept, no
`Module` concept, admin still sees many separate technical bank/source/candidate pages, and
"Activity" and "Template" are used inconsistently. Continuing to invest in selector-only work
(more relaxation rungs, more metadata columns) would compound that mismatch rather than close it.

This phase defines the target model — **Resource → Learn/Activity → Module → Daily Lesson /
Practice Gym → Attempt → Feedback + Rating → Learner Memory** — and a safe, incremental path from
today's state to it, without deleting or destabilizing anything that works today (Today fallback,
Practice Gym fallback, readiness/delivery queue, D1–D6 selector logic).

---

## 2. Intended product model

```
Resource Bank Item
      │
      ├──► Learn Item
      │
      └──► Activity  (optionally linked to a Learn Item)
                │
                ▼
             Module  (Learn + Practice + Feedback)
                │
        ┌───────┴───────┐
        ▼               ▼
  Daily Lesson     Practice Gym
        │               │
        └───────┬───────┘
                ▼
             Attempt
                │
                ▼
       Feedback + Rating
                │
                ▼
       Learner Memory / Mastery
```

### Resource Bank Item

A raw, approved learning content item, imported or created by admin/AI. Not a student-facing
activity — a substrate. May be any type: vocabulary, grammar, reading, listening, speaking,
writing, examples, prompts, passages, transcripts. Admin should experience this as **one unified
Resource Bank with typed rows**, not many separate bank pages.

Today's equivalent: `CefrVocabularyEntry` / `CefrGrammarProfileEntry` / `CefrReadingReference` /
`CefrReadingPassage`, published via `ResourceCandidate` → `ResourceCandidatePublishService`. This
*is* the Resource Bank Item concept already — the gap is presentation (many typed admin pages, no
unified surface), not the underlying data shape.

### Learn Item

The teaching/explanation part generated from one or more resources.

Fields: title; body/explanation; examples; common mistakes (where useful); linked source resource(s);
CEFR; skill; subskill; context tags; focus tags; difficulty; approval status; rating/quality signals.

Today's equivalent: **does not exist.** `ActivityMaterializationJob`'s bank-content prompt block
(D1–D6) is the closest analog, but it is a transient, per-request AI prompt fragment, not a stored,
admin-reviewable, reusable entity with its own lifecycle.

### Activity

A student exercise/task generated from one or more resources, optionally linked to a Learn Item.
Examples: gap fill, multiple choice, sentence correction, rewrite sentence, short writing, reading
comprehension, listening comprehension, speaking prompt.

Fields (Form.io-supported activities): Form.io schema/config; prompt/instructions; answer/scoring
rules; feedback rules/plan; linked resource rows; optional linked Learn Item; CEFR; skill; subskill;
context tags; focus tags; difficulty; approval status; rating/quality signals. Admin-editable.

Today's equivalent: `ActivityTemplate` (Form.io-native, admin CRUD, review/publish workflow — 8 of
33 Practice Gym pattern keys migrated as of Phase C-Final) plus the always-fresh legacy
`IAiActivityGenerator` path for everything else, plus `LearningActivity` as the per-student
materialized instance. `ActivityTemplate` is the closest existing analog to "Activity" in this
model — it is not being replaced, it is being reframed as the thing Resource Bank rows generate
into.

### Module

The core student learning unit: **Learn → Practice → Feedback.** Links Learn Item(s) and
Activity/Activities around the same objective/concept/tags.

Today's equivalent: **does not exist.** `CurriculumObjective` is the closest conceptual anchor
(an objective/concept key), but nothing packages a Learn Item + Activity/Activities + feedback plan
into one reviewable, assignable unit today.

### Daily Lesson

The daily student plan: several Modules selected for that day based on student level, weakness,
goals/context, available time, learning plan, and novelty/reuse controls.

Today's equivalent: Today's `LearningSession` → `SessionExercise` → `LearningActivity` chain,
composed by `ActivityMaterializationJob`/`TodayBankResourceSelector`. This is activity-first, not
module-first — the D6 selector picks bank resources and a pattern per exercise, not a Module.

### Practice Gym

Self-directed or weakness-based module practice. Student picks a skill area (speaking, listening,
writing, reading, grammar/vocabulary); the system offers suitable modules/practice based on
weakness, unseen activities, review needs, skill choice, and mastery state.

Today's equivalent: type-first Practice Gym (`ExercisePatternDefinition`/`ExerciseTypeDefinition`
catalog, `PracticeGymSuggestionService`), already planned to move toward skill-first via PG-v2
(see `docs/backlog/product-backlog.md`'s "Practice Gym v2" section) — but PG-v2 as currently
scoped selects an `ActivityTemplate`/resource/format directly, not a Module. This model implies
PG-v2's target should eventually be Module selection, not just Activity selection.

### Attempt, Feedback + Rating, Learner Memory / Mastery

At the end of a Module: the student submits attempt(s), receives feedback, learner memory/mastery
updates, and user ratings on Learn/Activity/Module influence future selection.

Today's equivalent: `ActivityAttempt` (attempt), the pattern evaluation engine + AI feedback
(feedback), `ActivityFeedbackSignal` (Phase B2 foundation — collected, not yet consumed by
calibration), `StudentSkillProfile`/`UserLearningSummary`/`StudentLearningEvent` (learner memory).
These layers exist and are **not changed by this phase** — the gap is that they operate at the
Activity level, not the Module level, because Module doesn't exist yet.

---

## 3. Intended import flow

```
Admin imports file/content/dataset
  → admin chooses broad resource category/type (vocabulary, grammar, reading, listening,
    speaking, writing, mixed/AI-detect)
  → AI analyzes input structure, maps columns/rows, detects CEFR if present, normalizes tags,
    proposes typed Resource Bank rows, flags warnings/ambiguous rows
  → rows go to Pending Review as Resource Bank candidates (not final student activities)
  → admin approves/rejects/edits
  → approved rows become published Resource Bank Items
  → admin selects one or many published Resource Bank rows
  → admin chooses: Generate Learn Item / Generate Activity / Generate Learn + Activity /
    Generate Module draft
  → AI generates the corresponding Learn/Activity/Module record(s), with strong
    tags (CEFR/skill/subskill/context/focus/difficulty) and approval status
  → generated records go to Pending Review
  → approved Learn/Activity/Module records become usable for Daily Lesson creation,
    Practice Gym module selection, Today selector/routing, and feedback/mastery updates
```

Non-negotiable properties of this flow, carried over unchanged from the existing E1–E4 pipeline:

- Import never immediately assigns content to students.
- Import never directly creates published final activities without review.
- Every row preserves source/import-run/raw-record references (`ResourceImportRun`/
  `ResourceRawRecord`/`ResourceCandidate` lineage — unchanged).
- AI helps structure and generate; **deterministic backend validation + admin approval control
  quality**, not AI judgment alone (unchanged E2 rule: AI analysis is advisory only).

This flow is already implemented for the Resource Bank Item stage (E1–E9). It is **not yet
implemented** for the "select rows → generate Learn/Activity/Module draft" stage — that stage does
not exist today (H3/H4/H5 below).

---

## 4. Unified Resource Bank — direction

**Decision: Option B — unified admin read model/API over existing typed tables, not immediate
physical consolidation.**

**Option A — physical unified table.** A single `ResourceBankItem` table with a `Type` discriminator
and a `StructuredJson` payload column. Pros: one real table, simplest long-term query shape. Cons:
destructive migration across `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`/
`CefrReadingPassage` (and, later, `ActivityTemplate`); every existing query/selector/test that reads
those typed tables (`ResourceBankQueryService`, `TodayBankResourceSelector`, the D1–D6 selection
ladder, `ResourceCandidatePublishService`'s routing) would need to change in lockstep; high risk of
regressing D5/D6's just-stabilized selector behavior for a purely administrative UX win.

**Option B — unified admin read model over existing typed tables (recommended, near-term).** Add
one admin-facing Resource Bank API/page that queries across the existing typed tables (a thin
aggregation/read layer, similar in spirit to `ResourceBankQueryService`'s existing cross-type
browsing), exposing type/CEFR/skill/subskill/context/focus/difficulty/source/status/approval/
created-by/linked-Learn-Activity-Module filters — without moving any data. Old typed pages may
remain reachable under Advanced/Diagnostics during the transition (H8).

**Why Option B first:**
- Lower risk — no destructive migration, no forced rewrite of D1–D6's selector/publish logic.
- Preserves the just-stabilized, fully-tested selector/publish/relaxation-ladder code paths.
- Lets the admin UX become correct (one Resource Bank, not five pages) before any backend
  consolidation is attempted.
- Physical consolidation (Option A) can be evaluated later, once Learn/Activity/Module (H3–H5)
  exist and there is real evidence of what shape actually serves them best — deciding the physical
  schema before Learn/Activity/Module exist risks designing the wrong shape.

Do not require immediate physical DB consolidation.

---

## 5. Learn / Activity / Module / Lesson / Practice Gym — model requirements

### Learn Item (proposed fields)
title; body/explanation; examples; common mistakes; source resource links; CEFR; skill; subskill;
context tags; focus tags; difficulty; status; approval state; rating/quality signals.

### Activity (proposed fields)
title; activity type/pattern key; renderer type (Form.io/custom); Form.io schema/config where
applicable; prompt/instructions; answer/scoring rules; feedback rules/plan; source resource links;
optional linked Learn Item; CEFR; skill; subskill; context tags; focus tags; difficulty; status;
approval state; rating/quality signals.

### Module (proposed fields)
module title; objective/concept key; Learn item link(s); Activity link(s); feedback plan; CEFR;
skill; subskill; context/focus tags; difficulty; estimated time; status/approval state;
source/resource traceability.

### Daily Lesson (proposed fields)
student id; date/window; selected module ids; estimated total time; reason/routing metadata;
status lifecycle.

### Practice Gym (proposed fields)
student id; selectable skill area; module ids or module assignment ids; weakness/review reason;
novelty/reuse controls; status lifecycle.

### Feedback + Rating (behavior, not new fields)
At the end of a Module: student receives feedback based on attempts; learner memory/mastery is
updated; user ratings on Learn/Activity/Module influence future selection.

None of these are implemented in H0. H3 (Learn Item), H4 (Activity), and H5 (Module) are where
these become real entities.

---

## 6. Current-state mismatch audit

Reviewed: `docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`,
`docs/handoffs/current-product-state.md`, `docs/backlog/product-backlog.md`,
`docs/architecture/learning-activity-engine.md`,
`docs/architecture/english-resource-bank-import-platform.md`, `docs/architecture/README.md`,
`TODOS.md`. Code concepts inspected by name (not modified): `ResourceCandidate`,
`ResourceImportRun`, `ResourceCandidatePublishService`, `CefrVocabularyEntry`,
`CefrGrammarProfileEntry`, `CefrReadingReference`, `CefrReadingPassage`, `ActivityTemplate`,
`LearningActivity`, `StudentActivityReadinessItem`, the Today/Practice Gym materialization paths,
Form.io placement/onboarding.

| Current state | Target state |
|---|---|
| Many separate bank pages (`/admin/resource-banks/vocabulary`, `/reading-passages`, etc.) | One Content Studio Resource Bank with typed rows + filters |
| Technical source/import/candidate pages visible in main nav (reframed into "Content Banks" by G1, but still technical-first) | Import/review technical internals hidden under Advanced/Diagnostics |
| No `Learn Item` concept — teaching content is a transient AI prompt fragment, not a stored entity | `Learn Item` as a first-class, admin-reviewable, reusable entity |
| `Activity`/`Template` concepts mixed (`ActivityTemplate`, `ExercisePatternDefinition`, `ExerciseTypeDefinition`, `LearningActivity` all called "activity" in different contexts) | Clear `Activity` definition: admin-editable exercise generated from resources, optionally linked to a Learn Item |
| `Lesson`/Today concepts mixed with per-exercise activity materialization (`ActivityMaterializationJob` composes at the exercise level) | Module-first Today: a Daily Lesson is a bundle of Modules, not a bundle of independently-selected exercises |
| Practice Gym is activity-type-first (student picks a pattern name), PG-v2 (planned) is skill-first but still Activity-target, not Module-target | Practice Gym is module-first: student picks skill/weak-area/review/challenge, system selects Modules |
| Admin import UX exists (E1–E9 pipeline) but ends at published Resource Bank rows — no "generate Learn/Activity/Module from selected rows" step | Admin can select Resource Bank rows and generate Learn Item / Activity / Module drafts for review |
| Generated content lifecycle is Resource → (AI prompt injection at request time) → `LearningActivity` instance | Generated content lifecycle is Resource → Learn/Activity (stored, reviewable) → Module → Lesson/Practice Gym |

**Nothing in this table implies deleting any current mechanism.** The bank-first selector work
(D1–D6) remains useful substrate — it proves the selection/relaxation/novelty/CEFR-policy logic
that Module-level selection will eventually reuse. The mismatch is in the *admin/product IA and
the missing Learn/Module layer*, not in the underlying data or selection logic.

---

## 7. Proposed admin information architecture (target, not implemented)

```
Content Studio
  Import content
  Review generated content
  Resource bank
  Learn items
  Activities
  Modules

Learning Setup
  Onboarding
  Placement

Delivery
  Today lessons
  Practice Gym pipeline
  Student assignment / delivery queue

Advanced / Diagnostics
  Resource sources
  Import runs
  Candidate records
  AI operations
  Usage / cost
  Runtime settings
```

This supersedes G1's three-way nav split (Content Banks / Delivery / Learning Setup, done
2026-07-09) as the longer-term target — G1's split is a real, valid intermediate step (technical
import internals are still visible under "Content Banks" today), and **is not undone by this
phase.** H8 is where the nav actually changes to the structure above, once Learn/Activity/Module
pages exist to populate "Content Studio." Not implemented in H0.

---

## 8. Proposed phased implementation roadmap — the H-track

| Phase | Scope | Depends on |
|---|---|---|
| **H0 — Product Model Realignment** | Docs-only. This phase. | E10, D6 |
| **H1 — Unified Resource Bank Admin Read Model** `Done (2026-07-09)` | One admin-facing Resource Bank API/page over existing typed published bank tables (Option B, §4). No physical consolidation. Old typed pages remain (not yet moved to Advanced — that's H8). | H0 |
| **H2 — Import Content UX v1** `Done (2026-07-09)` | Admin paste (text/CSV/JSON)/import page; admin chooses broad type/category/default tags; deterministic mapping (no AI analyze yet — labeled "coming soon"); creates pending Resource Candidates through the existing E1 pipeline; no student assignment. File upload and async large-import handling remain on the existing Resource Import Runs page. | H1 |
| **H3 — Learn Item Foundation** `Done (2026-07-09)` | `LearnItem`/`LearnItemResourceLink` entities/tables/API/admin review; deterministic "Generate Learn" from selected Resource Bank rows (no AI call yet); reuses `AdminReviewStatus`; approval lifecycle; source-resource traceability. | H2 |
| **H4 — Activity Foundation with Form.io** `Done (2026-07-09)` | New `ActivityDefinition`/`ActivityResourceLink` entities (additive-only migration, two new tables) — deliberately separate from `ActivityTemplate`, not built on top of it; Form.io schema/scoring/feedback-plan storage for `gap_fill`/`multiple_choice_single`/`short_answer`; deterministic (no AI) generation from Resource Bank rows or a Learn Item; approval lifecycle; source-resource + optional Learn Item traceability. | H2 (parallel with H3) |
| **H5 — Module Foundation** `Done (2026-07-09)` | New `ModuleDefinition`/`ModuleDefinitionLearnItemLink`/`ModuleDefinitionActivityLink` entities (additive-only migration, three new tables), deliberately separate from runtime `LearningModule`; `ModuleDefinition` = Learn Item(s) + Activity Definition(s) + module-level Feedback Plan; deterministic (no AI) generation from selected items, a Resource Bank row, a Learn Item, or an Activity Definition — every entry point requires Approved sources; approval lifecycle; objective/CEFR/skill/subskill/context/focus/difficulty/estimated-minutes metadata. | H3, H4 |
| **H6 — Daily Lesson Module Pipeline** `Done (2026-07-09)` | Deterministic, read-only `IDailyLessonModuleSelectionService` selects an Approved Module (with an Approved Learn Item and Approved Activity Definition) for Today, attached additively as an optional `TodaysSessionResult.ModuleSection`; existing session generation and Today legacy fallback unchanged; new additive `StudentDailyModuleAssignment` bookkeeping table for a 14-day reuse guard and admin diagnostics. | H5 |
| **H7 — Practice Gym Module Pipeline** `Done (2026-07-09)` | Deterministic, read-only `IPracticeGymModuleSelectionService` suggests Approved Modules (with Approved linked Learn Item/Activity Definition) for Practice Gym, attached additively as an optional `PracticeGymSuggestionsDto.ModuleSuggestions`; adds self-directed skill/subskill/objective/difficulty/weakness-signal preferences on top of H6's shape; no student "start" flow yet (display-only). | H6 |
| **H8 — Content Studio/Admin IA Cleanup and Removal Readiness** `Done (2026-07-10)` | Frontend/docs-only. Split admin nav into Content Studio (Import → Bank → Learn Items → Activities → Modules) and Content Ops (staging/support pages); removed the four typed resource-bank nav entries (navigation only — routes/tables/APIs untouched); updated stale "future Modules" page copy. No table/API/route/component deletion. | H1–H7 substantially landed, Plan-Sync-After-H7 |
| **H9A — Legacy Admin/API/Code Path Removal Safety Pass** `Done (2026-07-10)` | First H9 cleanup phase. Frontend/admin cleanup only — removed the four typed admin bank Angular pages/routes/components (already unreachable via nav since H8), the orphaned `AdminResourceBankService`, and 12 dead model interfaces; old routes redirect to the unified Resource Bank with a matching `?type=` filter; nulled a dead-link `DetailRoute` value backend-side. No typed table/data/service-method/runtime-dependency removed. | H8 |
| **H9B — Physical ResourceBankItem Consolidation Decision and Design** `Done (2026-07-10)` | Docs/design-only. Audited the 4 typed tables' field shapes, all typed-method callers, the polymorphic link pattern, and every seeder. **Recommended against physical consolidation** (Option A, converging toward Option E) — type-specific field shapes, a cheaper fix exists for the one real pain point (SQL view, `TODO-H11-1`), current content volume doesn't justify migration risk. Full schema/migration/gate design documented for a future re-evaluation, not implemented. See `TODO-H9B-1`. | H9A |
| **H9C — Data Migration/Compatibility Adapters** `Not scheduled — consolidation not recommended` | H9B found no justification to start this; kept as a conditional placeholder only. See `TODO-H9C-1`. | H9B |
| **H9D — Typed Table/API Removal** `Not scheduled — blocked on H9C, itself not recommended` | See `TODO-H9D-1`. | H9C |
| **H11 — Strengthen ResourceBankQueryService with a SQL-side unified view** `Planned` | Lightweight alternative to physical consolidation — a SQL `UNION ALL` view over the 4 typed tables for real DB-side pagination on the unified admin Resource Bank page. Zero data migration, zero change to any typed table or its writers/readers. Only pursue if `ListUnifiedAsync`'s in-memory scan becomes a measured performance problem. See `TODO-H11-1`. | H9B |
| **H10 — ActivityDefinition Runtime Launch Path / Attempt Bridge** `Done (2026-07-10)` | Chose (C) hybrid, executed via (B)'s mechanism: materializes an eligible Activity Definition into a real `LearningActivity` via `SetFormIoContent` (same as `ActivityTemplate`'s pilot), reusing the entire existing scoring/attempt/ledger pipeline unchanged. New additive `StudentActivityDefinitionLaunch` bridge table for traceability. Practice Gym Start button now live for `gap_fill`/`multiple_choice_single` suggestions. | H7 |

**Not scheduled by this phase:** destructive cleanup of any kind. Phase F (legacy generation
retirement), G2/G3 (backend/diagnostics cleanup), and PG-v2 (skill-first Activity selector)
**remain later, sequenced after the H-track proves its replacement** — same discipline as every
prior bank-first phase (D1–D6, E1–E10, G0/G1).

**Relationship to PG-v2:** PG-v2A–D (backend skill-first selector, UI, capability-registry
cleanup, legacy retirement — see `docs/backlog/product-backlog.md`) is still a valid near-term
track and is not blocked by H0. It targets Activity selection given a skill/objective. H7 later
extends that pattern to Module selection. PG-v2 can proceed in parallel with early H-track phases
(H1–H4) if prioritized that way — that is a future decision, not made in this phase.

---

## 9. Recommended next phase

**H1 — Unified Resource Bank Admin Read Model — done (2026-07-09).** Lowest risk (no
schema/migration, read-only aggregation over existing tables), highest immediate admin-UX value,
and a safe first step that did not require Learn/Activity/Module to exist yet. See
`docs/roadmap/road-map.md` §1, Decision Log (Phase H1 entry), and §19a item 20i for full
implementation detail.

**H2 — Import Content UX v1 — done (2026-07-09).** A thin, deterministic admin wrapper over the
already-built E1 pipeline — no schema/migration, no new published-bank writes, no AI-guessed
classification. See `docs/roadmap/road-map.md` §1, Decision Log (Phase H2 entry) for full detail.

**H3 — Learn Item Foundation — done (2026-07-09).** An additive-only two-table migration and a
deterministic "Generate Learn" composer — no AI call, no Activity/Module, no student assignment.
See `docs/roadmap/road-map.md` §1, Decision Log (Phase H3 entry) for full detail.

**H4 — Activity Foundation with Form.io — done (2026-07-09).** An additive-only two-table
migration, a deterministic (no AI) composer producing validated Form.io schemas + scoring rules in
the existing shared format, and a new entity kept explicitly separate from both `LearningActivity`
(runtime) and `ActivityTemplate` (existing Practice Gym Form.io pilot). No Module, no student
assignment, no Today/Practice Gym runtime change. See `docs/roadmap/road-map.md` §1, Decision Log
(Phase H4 entry) for full detail.

**H5 — Module Foundation — done (2026-07-09).** An additive-only three-table migration, a
deterministic (no AI) composer over EXISTING Approved Learn Items/Activity Definitions only (never
cascade-generates new ones), and a new entity kept explicitly separate from runtime
`LearningModule`. No student assignment, no Module attempts, no Today/Practice Gym runtime change.
See `docs/roadmap/road-map.md` §1, Decision Log (Phase H5 entry) and
`docs/reviews/2026-07-09-phase-h5-module-foundation-review.md` for full detail.

**H6 — Daily Lesson Module Pipeline — done (2026-07-09).** First runtime consumer of
`ModuleDefinition`; deterministic, read-only selection additively attached to Today; existing
session generation, Today legacy fallback, and Practice Gym all unchanged. See
`docs/roadmap/road-map.md` §1 and
`docs/reviews/2026-07-09-phase-h6-daily-lesson-module-pipeline-review.md` for full detail.

**H7 — Practice Gym Module Pipeline — done (2026-07-09).** Second runtime consumer of
`ModuleDefinition`; deterministic, read-only selection additively attached to Practice Gym
suggestions; existing readiness-pool suggestion logic (both entry points) and Today all
unchanged; no student "start" flow yet (display-only). See `docs/roadmap/road-map.md` §1 and
`docs/reviews/2026-07-09-phase-h7-practice-gym-module-pipeline-review.md` for full detail.

**Plan-Sync-After-H7 — Legacy Bank Removal Strategy — done (2026-07-09, docs-only).** Confirmed
H6/H7 are additive and fallback-safe; audited every pre-H-track legacy structure by removal risk
(finding almost everything is still core runtime infrastructure or a live fallback — nothing yet
proven safe to remove); defined H8/H9/H10 scope without implementing any of them. See
`docs/roadmap/road-map.md` §1 and
`docs/reviews/2026-07-09-plan-sync-after-h7-legacy-bank-removal-strategy.md` for full detail.

**H8 — Content Studio/Admin IA Cleanup and Removal Readiness — done (2026-07-10).**
Frontend/docs-only nav and copy cleanup; no backend/table/API/route/component change. See
`docs/roadmap/road-map.md` §1 and
`docs/reviews/2026-07-10-phase-h8-content-studio-admin-ia-cleanup-review.md` for full detail.

**H10 — ActivityDefinition Runtime Launch Path / Attempt Bridge — done (2026-07-10).** Chose a
hybrid bridge (Option C framing, Option B mechanism): materializes an eligible
`ActivityDefinition` into a real `LearningActivity`, reusing the entire existing scoring/attempt/
ledger pipeline unchanged. `ActivityTemplate` was not removed — H10 gives `ActivityDefinition` a
proven launch path so H9 will eventually have a real basis for deciding whether `ActivityTemplate`
is still needed, but that decision is still H9's to make, not resolved here. See
`docs/roadmap/road-map.md` §1 and
`docs/reviews/2026-07-10-phase-h10-activitydefinition-runtime-launch-bridge-review.md` for full
detail.

**H9A — Legacy Admin/API/Code Path Removal Safety Pass — done (2026-07-10).** First H9 cleanup
phase; frontend/admin cleanup only. See
`docs/reviews/2026-07-10-phase-h9a-legacy-admin-code-path-removal-review.md`.

**H9B — Physical ResourceBankItem Consolidation Decision and Design — done (2026-07-10,
docs/design-only).** Recommended against physical consolidation — see
`docs/reviews/2026-07-10-phase-h9b-resourcebankitem-consolidation-decision.md`.

**No H9C/H9D implementation scheduled.** H9B found no current justification for physical
consolidation, so the migration chain it would have gated (H9C data migration, H9D typed
table/API removal) has nothing to build. **Recommended next: H11 — Strengthen
ResourceBankQueryService with a SQL-side unified view**, only if `ListUnifiedAsync`'s in-memory
scan ever becomes a measured performance problem. The PG-v2 track remains a separate, still-open
decision not resolved by this phase. `TODO-H10-2` (Today module launch) and `TODO-H10-3` (native
ActivityDefinition attempt runtime) are smaller, independent follow-ups that can proceed in
parallel.

---

## 10. What this phase explicitly did not do

Docs-only. No migrations, entities, Angular changes, API changes, test changes, pushes, or
deploys. No bank tables deleted. No Today or Practice Gym fallback deleted. No readiness/delivery
queue deleted. No PG-v2 implementation started. No Content Studio implementation started. No
external datasets imported. No Persian/bilingual/support-language seed content added. No direct
seeding of final published bank tables.
