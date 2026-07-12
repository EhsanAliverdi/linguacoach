---
status: current
lastUpdated: 2026-07-13 (Phase J5a)
owner: product
supersedes:
supersededBy:
---

# SpeakPath — Current Product State

Last updated: 2026-07-13 (Phase J5a)

## Phase J5a: Writing content-type import (2026-07-13)

First pass of Phase J5 (import content-type expansion). Sequenced as four small passes, easiest
first: **J5a Writing (done) → J5b Mixed → J5c Listening (real audio upload, user-selected) → J5d
Speaking**.

Import Content's "Content type" dropdown now has a fourth option, **Writing** — a staged writing
prompt (title + task instructions + optional genre/suggested word count, no rubric/answer key).
Writing candidates flow through the exact same staging → AI-analyze → deterministic-validate →
approve → publish pipeline as Vocabulary/Grammar/Reading, publishing into the same
`ResourceBankItem` table under a new `PublishedResourceType.Writing` discriminator, browsable on
`/admin/resource-bank` alongside the other types.

**Deliberately not wired into Lesson/Exercise/Module generation yet** — that consumption path
(`LessonResourceLookup`) doesn't recognize `Writing` yet, so the Resource Bank page's Generate
Learn/Activity/Module row actions are hidden for Writing rows (not shown-then-erroring). A future
phase would wire generation once there's a concrete plan for how Writing prompts become student
exercises.

Full detail: `docs/reviews/2026-07-13-phase-j5a-writing-content-import-review.md`.

## Phase J4B follow-up: Import Content tabs, run-candidates page, pagination (2026-07-13)

Direct user UI correction of the J4B Import Content redesign below, given while smoke-testing it
live. Three fixes, all frontend-only, no backend change:

1. **Tabs now use the shared admin design system.** Replaced the ad-hoc `sp-admin-button`
   solid/outline toggle pair with the same `sp-admin-tab-bar`/`sp-admin-tab` CSS-class pattern
   already used by AI Config, Notifications, and Student Detail — visually identical underline-tab
   style across all four pages now.
2. **Selecting a run in Import History navigates to its own page**, not an inline panel on the
   same URL. New route `/admin/content/import/runs/:runId` → new
   `AdminImportRunCandidatesComponent`, with a run summary card, its own candidates table, and a
   "Back to Import History" button. The New Import tab's own just-imported-candidates pipeline
   stays inline (a direct continuation of the action just taken, not a browse-history pattern).
3. **Both the runs table and the run-candidates table are now frontend+backend paginated**
   (`sp-admin-pagination` wired to real `page`/`totalPages` state) — the list endpoints
   (`AdminResourceImportRunService.list()`, `AdminResourceCandidateService.list()`) were already
   page-aware server-side, this was pure frontend wiring.

Full detail: `docs/reviews/2026-07-13-phase-j4b-import-content-tabs-pagination-followup-review.md`.

## Phase J4B: Student Submit Fix, Import Content Tabs, Nav Fix (2026-07-13)

Follow-up to the J3/J4 smoke test's findings. **Confirmed** (not just hypothesized) that the student
`/activity` page had the same missing-Form.io-submit-button gap as the J3 admin preview modal, and
fixed `exercise-renderer.component.ts`/`.html` with the identical `FormioRendererComponent.submitForm()`
pattern — no scoring change, no new scoring path, reuses the existing `POST api/activity/{id}/attempt`
submission flow unchanged. **Not verified against a real logged-in student** — creating a test
account and resetting an existing student's password were both blocked as unauthorized shared-
database writes; the user chose to accept code-level/pattern-reuse evidence over live testing
(tracked as `TODO-025` in `TODOS.md`).

Also redesigned Import Content around two tabs ("New Import" / "Import History") — the confusing
top-of-page "recent import" chips are gone, replaced by a proper history table where selecting a run
shows its candidates in that same tab (previously behavior was ambiguous about which surface a
click would update). No backend/API change. And fixed the admin mobile drawer's missing
`aria-hidden`/`inert` state, the actual root cause of the admin nav appearing "duplicated many
times" to DOM-text-reading tools (confirmed via every screenshot this session that the visually
rendered sidebar was never actually duplicated).

3,459/3,459 backend tests pass (unchanged — no backend files touched this phase). Full detail:
`docs/reviews/2026-07-13-phase-j4b-student-submit-import-tabs-nav-fix-review.md`.

## Live Browser Smoke Test: J3/J4 Confirmed, One Bug Fixed, One Hypothesis Flagged (2026-07-13)

Phases J3 (Module preview) and J4 (Exercise launch-support badges) were manually verified end-to-end
in a live browser session, closing the "not yet manually verified" caveat both carried. **One real
bug was found and fixed**: the J3 preview modal had no way to actually submit an answer — the
generated Form.io schemas (`gap_fill`/`multiple_choice_single`) don't include their own submit
button, and the modal wasn't calling `FormioRendererComponent.submitForm()` externally (the pattern
`PlacementComponent` already uses for the same reason). Fixed with a "Submit Answer" button; verified
working for both correct (100%, "Correct!") and incorrect (0%, "Not quite...") answers.

**Flagged, not fixed:** the same missing-submit-button condition may affect the real student
`/activity` page for these two activity types — `exercise-renderer.component.ts`/`.html` has no
external submit trigger either, and H10's own tests call the attempt API directly (bypassing the
UI), so they would not have caught a frontend-only gap. **This is an unconfirmed hypothesis, not a
verified bug** — it was inferred from reading the shared component code, not from a live
student-account click-through. Recommended as the next thing to verify, ahead of J5. Full detail:
`docs/reviews/2026-07-13-j3-j4-live-browser-smoke-test-review.md`.

## Honest Launch-Support Signaling for Exercises (Phase J4, 2026-07-11) — closes J0-J4

Closes the audit's last recommended phase (J5, import content-type expansion, remains open as
lower priority). `short_answer` Exercises can be generated, reviewed, and approved, but still have
no runtime launch path (only `gap_fill`/`multiple_choice_single` do) — decided (explicit product
decision) to make this honestly visible everywhere rather than build new AI grading infrastructure
this phase. `ExerciseDto` now carries `canLaunchOnceApproved`/`launchUnsupportedReason`, computed
centrally so every Exercise-returning endpoint gets it automatically; the admin Exercises list and
detail drawer both show it. No real runtime behavior changed — the full pre-existing backend test
suite passed unmodified. 3,459/3,459 backend tests pass (+2 new). Full detail:
`docs/reviews/2026-07-11-phase-j4-exercise-launch-support-honesty-review.md`.

**Audit summary (J0-J4 all closed):** the 2026-07-10 product architecture audit's five recommended
phases are now complete — docs sync (J0), published-bank duplicate detection (J1), AI-assisted
Lesson/Exercise/Module generation (J2a/b/c), admin Module preview-as-learner (J3), and honest
Exercise launch-support signaling (J4). Only J5 (import content-type expansion) remains open. Two
manual browser smoke tests are still pending from this work: the J3 preview modal and the J4 launch-
support badges — neither has been visually confirmed in a running dev server yet.

## Admin "Preview as Learner" for Modules (Phase J3, 2026-07-11)

Admins can now preview a Module exactly as a learner would — see the Lesson, complete the Exercise,
get a real score and feedback message — entirely **before approving it**. A new "Preview as
Learner" button on the Module admin page's detail drawer opens the Module's linked Lesson and
Exercise (student-safe schema only, no answer key), rendered with the same shared Form.io renderer
component the student app uses. Submitting an answer scores it with the exact same
`ComponentAnswerScorer`/`ExerciseLaunchEligibility` logic the real student runtime uses — no
separate/simplified scoring path. **Deliberately does not create any `LearningActivity`/
`ActivityAttempt`/`StudentExerciseLaunch` row** — a pure read/score-only diagnostic, entirely
separate from the real student launch path (`IExerciseLaunchService`, which still requires an
Approved Module + real student). This closes the second of the architecture audit's two Critical
product gaps (the first, AI-assisted generation, closed by Phase J2). 3,457/3,457 backend tests
pass (+9 new, including a dedicated test proving no runtime rows are created). Not yet manually
verified in a live browser session. Full detail:
`docs/reviews/2026-07-11-phase-j3-admin-module-preview-review.md`.

## AI-Assisted Module Generation (Phase J2c, 2026-07-11) — closes Phase J2

The Module slice of Phase J2 is complete, closing the entire Phase J2 AI-generation effort (J2a
Lesson, J2b Exercise, J2c Module — all committed 2026-07-11). Admins can now generate a Module
draft two ways: the existing deterministic "Generate Module" (unchanged), or a new "Generate
Module (AI)" action (resource entry point only) that writes the module's own title, description,
and feedback-plan copy (completion message, evaluation criteria, feedback focus) referencing the
actual linked Lesson and Exercise content. **AI still only composes EXISTING, already-approved
Lesson(s)/Exercise(s)** — it never cascade-generates a new one, the same hard invariant the
deterministic composer enforces. There is no answer key or scoring rule at the Module level, so
this carries the same low risk as J2a's Lesson generation, unlike J2b's Exercise generation.
3,448/3,448 backend tests pass (+6 new). Full detail:
`docs/reviews/2026-07-11-phase-j2c-ai-module-generation-review.md`.

**Phase J2 summary:** all three content types (Lesson, Exercise, Module) can now be generated with
genuine AI assistance, each as a separate action alongside its untouched deterministic composer, no
silent AI-then-deterministic fallback anywhere. Safety-critical invariants — no AI-supplied correct
answers at the Exercise level, no AI-cascaded content creation at the Module level — are preserved
and test-verified. Remaining audit phases: J3 (admin preview-as-learner for Modules), J4
(`short_answer` runtime support), J5 (import content-type expansion).

## AI-Assisted Exercise Generation (Phase J2b, 2026-07-11)

The Exercise slice of Phase J2 is complete, following the same pattern as J2a's Lesson slice but
deliberately narrower given the real correctness/security stakes of an answer key. Admins can now
generate an Exercise draft two ways: the existing deterministic "Generate Activity" (unchanged), or
a new "Generate Activity (AI)" action (resources entry point only — "generate from Lesson" has no AI
variant yet) that calls AI for framing content only — a natural gap-fill sentence, plausible-but-
wrong multiple-choice distractors, or a tailored comprehension question. **The correct answer and
scoring rule are never AI-supplied for any activity type** — always deterministically derived from
the resource's own fields, the same design constraint the 2026-07-08 `ActivityTemplate` generation-
instructions decision established. A new defensive check rejects any AI gap-fill sentence that
leaks the answer term outside the blank marker. Both actions produce the same pending-review
`Exercise` entity, same admin approve/reject workflow, same runtime launch-eligibility rules
(`short_answer` still isn't launchable regardless of generation method). Module generation (J2c)
remains deterministic-only. 3,442/3,442 backend tests pass (+9 new, including a dedicated
answer-leak-rejection test). Full detail:
`docs/reviews/2026-07-11-phase-j2b-ai-exercise-generation-review.md`.

## AI-Assisted Lesson Generation (Phase J2a, 2026-07-11)

The Lesson slice of Phase J2 (AI-assisted Lesson/Exercise/Module generation) is complete. Admins
can now generate a Lesson draft two ways: the existing deterministic "Generate Learn" (unchanged,
field-copies the selected Resource Bank row's own content), or a new "Generate Learn (AI)" action
that calls an AI provider to write genuine teaching prose (title, body, examples, common mistakes,
usage notes) about the selected resource(s) — metadata (CEFR/skill/subskill/tags/difficulty) stays
deterministic either way. This is a deliberately separate action, not a toggle: if AI is unavailable
or misconfigured, the AI action fails with a clear error and no draft is created — the deterministic
action stays available regardless, per explicit product decision (2026-07-11). Both actions produce
the same pending-review `Lesson` entity, going through the same admin approve/reject workflow.
Reuses the existing AI provider/prompt/usage-tracking infrastructure (same `llm.generation` category
already used by Exercise Pattern Engine generation features) — no new admin AI-config screen needed.
Exercise (J2b) and Module (J2c) generation remain deterministic-only, planned as separate future
passes. 3,433/3,433 backend tests pass (+7 new). Full detail:
`docs/reviews/2026-07-11-phase-j2a-ai-lesson-generation-review.md`.

## Post-Audit Cleanup: J0/J1 (2026-07-10)

Following the post-I4 product architecture audit
(`docs/reviews/2026-07-10-ai-content-pipeline-product-architecture-audit.md`), two of its
recommended phases are complete. **J0** (docs-only) synced this file with Phases I0-I4 (see below)
and fixed a leftover "Daily Lessons" string in the admin Resource Bank page subtitle. **J1** closed
the audit's "no duplicate detection against the published bank" gap: `ResourceCandidateValidationService`
now also checks a staged candidate's content fingerprint against already-published
`ResourceBankItem` rows (previously only checked against other pending candidates), advisory only
(forces `NeedsReview`, does not block publish — explicit product decision). 3,426/3,426 backend
tests pass (+2 new). Full detail: `docs/reviews/2026-07-10-phase-j1-published-bank-duplicate-detection-review.md`.
Remaining phases (J2 AI-assisted generation, J3 admin preview-as-learner, J4 `short_answer` runtime
support, J5 import content-type expansion) are not yet started.

## Today Plan Rename (Phase I4 Pass 3, 2026-07-10)

Final pass of the Phase I4 product-language rename. "Daily Lesson" (the H6 bank-first daily
content selection concept) becomes "Today Plan" throughout: `IDailyLessonModuleSelectionService`
-> `ITodayPlanModuleSelectionService`, `AdminDailyLessonModuleController` -> `AdminTodayPlanModuleController`
(routes `api/admin/daily-lesson/...` -> `api/admin/today-plan/...`), `StudentDailyModuleAssignment`
-> `StudentTodayPlanModuleAssignment` (lossless table/index rename), `TodaysSessionResult.ModuleSection`
-> `.TodayPlan`, and the student-facing dashboard copy ("Today's Lesson" -> "Today's Plan").
`StudentPracticeGymModuleAssignment`/`IPracticeGymModuleSelectionService` (H7's own, unrelated
concept) were deliberately left untouched. 3,424/3,424 backend tests pass; frontend production
build clean. This closes Phase I4 (Passes 1-3) as a whole: `LearnItem`->`Lesson`,
`ActivityDefinition`->`Exercise`, `ModuleDefinition`->`Module`, `Daily Lesson`->`Today Plan`, across
every backend layer, file/folder name, API route, and frontend page/component/nav label. Full
detail: `docs/reviews/2026-07-10-phase-i4-pass3-today-plan-rename-review.md`.

## Frontend Rename (Phase I4 Pass 2, 2026-07-10)

Angular frontend slice of the I4 rename, making the admin frontend consistent with Pass 1's
backend rename. Notably, the rename required a route-collision resolution: the pre-existing
"Today Delivery Health" diagnostics page (previously `admin-lessons`, at `/admin/lessons`) was
renamed to `AdminTodayDeliveryHealthComponent` so the `admin-lessons`/`AdminLessonsComponent`
name/folder could be freed up for the new Lesson-library page (previously `admin-learn-items`,
moved to `/admin/lesson-library`). `admin-activities` -> `admin-exercises` (route `/admin/exercises`).
Every admin page/service/model under `features/admin/`, `core/services/`, `core/models/`, plus the
student-facing dashboard/practice-gym pages that consume the renamed backend DTOs, were updated.
Full detail: `docs/reviews/2026-07-10-phase-i4-pass2-frontend-rename-review.md`.

## Backend Rename (Phase I4 Pass 1, 2026-07-10)

Backend-only slice of the I4 product-language rename decided in
`docs/architecture/product-language-renaming-i4.md`: pure rename, no data-model change —
`LearnItem` -> `Lesson`, `ActivityDefinition` -> `Exercise`, `ModuleDefinition` -> `Module`
(dropping the "Definition" suffix), applied consistently to file names, class/type/enum names,
DTOs, EF configurations, table/column names (`learn_items` -> `lessons`, etc.), API routes
(`api/admin/learn-items` -> `api/admin/lessons`, etc.), and doc comments across `src/` and
`tests/`. Historical EF migration files were deliberately left untouched as a historical schema
record, consistent with every prior I-track migration. Full detail:
`docs/reviews/2026-07-10-phase-i4-pass1-backend-rename-review.md`.

## Final Nav Consolidation (Phase I3, 2026-07-10)

Landed the 7-item Content Studio nav target set out in the I-track plan: **Import Content ->
Resource Bank -> Learn Items -> Activities -> Modules -> Onboarding -> Placement** (pre-I4 names;
renamed by I4 above), one section, no second "Content Ops" tier. Onboarding and Placement
("Placement items" -> "Placement") were promoted out of "Content Ops" into "Content Studio."
**Review Queue was deleted entirely** (controller, contracts, query handler, DI registration,
frontend component/service/models) — it only ever covered `PlacementItemDefinition` review after
Phase I2A deleted `ActivityTemplate` (the other half of what it used to cover), and the standalone
Placement Items page already does everything Review Queue did for that entity. Old
`/admin/review-queue` bookmarks redirect to `/admin/placement-items`. The now-empty "Content Ops"
section was removed from both desktop sidebar and mobile-drawer nav trees. "Today Delivery Health"
(`/admin/lessons`) was left untouched — flagged as a residual cleanup candidate for a future pass
now that its generation-retry actions are inert no-ops post-I2B, but out of this phase's scope.
3,424/3,424 backend tests pass (down 4 from pre-phase baseline — the deleted
`AdminReviewQueueEndpointTests.cs`, no lost coverage elsewhere); frontend production build clean.
Full detail: `docs/reviews/2026-07-10-phase-i3-final-nav-consolidation-review.md`.

## Readiness Pool Removal (Phase I2C, final pass of I2, 2026-07-10)

With Today and Practice Gym confirmed to have zero readers of the readiness pool after Passes A
and B (below), this pass deletes `StudentActivityReadinessItem`/
`IStudentActivityReadinessPoolService`/`ReadinessPoolReplenishmentService` entirely — a larger
blast radius than the readiness pool's own service/entity, with tendrils into
`AdminAiOperationsController`, the runtime feature-gate registry, `StudentReadinessAuditService`/
`StudentPilotReadinessRepairService`, `LearningPlanService`, `StudentMasteryEvaluationService`,
`PracticeGymSuggestionService`, and several admin frontend pages — each updated to remove its
readiness-pool dependency without losing unrelated functionality. `IAiActivityGenerator` was
narrowed to the one method it still needs (`EvaluateAttemptAsync` — attempt scoring/feedback,
unrelated to content generation). Full detail:
`docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md`.

## Today Module-Only Collapse (Phase I2B, Pass B of I2, 2026-07-10)

Today (the student's daily lesson feature) had two content-delivery paths layered on top of each
other: the legacy per-exercise `LearningSession`/`SessionExercise` generation pipeline
(`LessonBatchGenerationJob` -> `ActivityMaterializationJob` -> `TtsAudioGenerationJob`, plus
on-demand `ExercisePrepareHandler`/`SessionGeneratorService`), and the newer bank-first Daily
Lesson Module pipeline (H6's selection service, additive since it shipped). **Collapsed Today to
Module-only**: the entire legacy job pipeline was deleted
(`LessonBufferRefillJob`, `LessonBatchGenerationJob`, `ActivityMaterializationJob`,
`TtsAudioGenerationJob`, `GenerationHashing`, `ExercisePrepareHandler`, `SessionGeneratorService`,
their Quartz triggers/DI registrations). Today now calls only the Module selection service; when
it has nothing for the student, Today honestly reports "nothing available" — never an
AI-generation fallback. Full detail:
`docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md`.

## Practice Gym Legacy Fallback Deletion (Phase I2A, Pass A of I2, 2026-07-10)

The product had two content-delivery systems: a legacy AI-generation pipeline (on-demand,
per-request AI calls producing ~89% of exercise types) and the new bank-first pipeline (Learn
Item -> Activity Definition -> Module -> H10 launch bridge, covering `gap_fill`/
`multiple_choice_single` over vocabulary/grammar only). **Deleted the legacy `ActivityTemplate`
entity system entirely** (the Form.io-pilot template system, distinct from H4's
`ActivityDefinition`): domain entity, EF config/seeder, all Infrastructure/Application files, the
admin controller, the two admin frontend pages and their routes/nav item, and the now-dead
`PracticeGymFormIoPilot` feature-gate group. Practice Gym now serves only bank-first content;
when nothing eligible exists it returns a clean "nothing available" response, never an AI-generation
fallback. `AdminReviewQueueComponent`/Controller/QueryHandler were rewritten (not deleted) to drop
their now-dead `ActivityTemplates` union branch. Full detail:
`docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md`.

## Unified Import/Publish Pipeline (Phase I1, 2026-07-10)

Three separate admin pages — Resource Sources, Resource Import Runs, Resource Candidates — were
merged into one: **Import Content** (`/admin/content/import`), with two sections: Import (paste
form + new file-upload mode, with an inline "+ New source" picker) and Pipeline/review (candidates
for the current or a selected past import run, with inline Preview/Analyze/**Approve & Publish**/
Reject actions). New backend action `POST api/admin/resource-candidates/{id}/approve-and-publish`
collapses two admin clicks into one without skipping any validation gate (publish still
re-validates every gate live). Source defaults fixed: auto-created sources now default
`AllowsStudentDisplay`/`AllowsCommercialUse` to `true` (previously `false`), closing a workflow
trap where publish was silently blocked until someone manually edited a source's license flags on
a separate page. Backend controllers were not physically merged — the one-page admin experience
calls three existing controllers under the hood; a full controller merge was scoped out as
unnecessary risk for no additional user-visible benefit. Old Resource Sources/Import Runs/
Candidates routes redirect to `/admin/content/import`. 3,858/3,858 backend tests passing (+4 new).
Full detail: `docs/reviews/2026-07-10-phase-i1-unified-import-pipeline-review.md`.

## Physical ResourceBankItem Consolidation (Phase I0, 2026-07-10)

**Reverses Phase H9B's "do not consolidate" recommendation**, per explicit user direction: build
one unified content pipeline (Import -> Bank -> Learn -> Activities -> Modules -> Onboarding ->
Placement) with a single physical Resource Bank table, deleting legacy fallback infrastructure
rather than keeping it as a safety net. The four typed published bank tables
(`CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`/`CefrReadingPassage`) are
replaced by one physical table, `resource_bank_items` (entity `ResourceBankItem`): common/
DB-filterable fields (`Type`, `CefrLevel`, `Subskill`, `DifficultyBand`, `ContextTagsJson`,
`FocusTagsJson`, `SourceId`, `ContentFingerprint`) are real columns; type-specific payload is
packed into a `ContentJson` column deserialized per-`Type`. `ContentFingerprint` is now populated
for all 4 types (previously only reading passages had it). Original row IDs were preserved 1:1
during migration so existing resource links kept resolving with zero link-table migration needed.
`ResourceBankQueryService.ListUnifiedAsync` is now a real single-table DB-paginated query
(previously an in-memory 4-way scan/concat/sort/page). The 4 typed entity classes, their EF
configs, and the 8 typed admin API routes were deleted (their only caller, the typed admin bank
pages, was already removed in Phase H9A). Full detail:
`docs/reviews/2026-07-10-phase-i0-resourcebankitem-physical-consolidation-review.md`.

---

## Physical ResourceBankItem Consolidation Decision (Phase H9B, 2026-07-10)

No product-facing behavior changed in this phase — it was a written decision, not an
implementation. The question was whether the backend should physically merge its four separate
published-content tables (vocabulary, grammar, reading references, reading passages) into one
table, to match how the admin Resource Bank page already presents them as one unified list.

**Decision: not now, and probably not ever unless content volume grows substantially.** The four
content types turn out to have real structural differences — a reading passage carries word
counts, reading-time estimates, and quality scores that vocabulary/grammar entries simply don't
need — so merging them into one table would mean either a lot of unused columns or a JSON blob
that loses some of the direct filtering the system uses today. The one real friction point found
(the admin Resource Bank page's "show everything" view loads all matching rows into memory before
paging, instead of asking the database to page directly) has a much simpler fix available if it
ever becomes a real slowdown — a database view, not a new table — and that's the recommended next
step if this becomes a measured problem rather than a hypothetical one.

Nothing about how content is imported, published, searched, or served to students changed. Full
detail: `docs/reviews/2026-07-10-phase-h9b-resourcebankitem-consolidation-decision.md`; roadmap:
`docs/roadmap/road-map.md` §1.

---

## Legacy Admin/API/Code Path Removal Safety Pass (Phase H9A, 2026-07-10)

The four legacy typed admin bank pages (Vocabulary/Grammar/Reading reference/Reading passage
bank) — already removed from navigation in H8 — are now physically removed: their Angular pages,
route entries, the Angular service used only by them, and their dead model types are gone. Old
bookmarks to these pages redirect automatically to the unified Resource Bank page with the
matching type filter already applied, so nothing 404s and no admin capability is lost — the
unified page already had full filter parity with the typed pages it replaces.

The unified Resource Bank page's detail drawer no longer shows a "Typed bank page" link, since the
page it pointed to no longer exists; this was a latent dead-link risk found and fixed as part of
this cleanup (the backend value that fed that link is now always empty instead of pointing at a
removed page).

This is the first of four planned H9 cleanup phases (H9A–H9D). It is **frontend/admin cleanup
only** — nothing about how content is stored, imported, published, or served to students changed.
The typed database tables, the backend API actions that still serve them (kept for compatibility,
though nothing in the admin UI calls them anymore), and the backend service methods that Today's
lesson-selection logic depends on are all untouched and will stay that way until a future,
separately-audited phase (H9B decides whether/how to consolidate the underlying tables; H9C would
migrate data if that's chosen; H9D would only then remove the old tables/APIs).

No student-facing behavior changed. No table was dropped, no data was migrated, no runtime
dependency was touched. Full detail:
`docs/reviews/2026-07-10-phase-h9a-legacy-admin-code-path-removal-review.md`; roadmap:
`docs/roadmap/road-map.md` §1.

---

## ActivityDefinition Runtime Launch Path / Attempt Bridge (Phase H10, 2026-07-10)

Approved `ActivityDefinition` records now have a real launch/attempt/scoring path — the first
time this has been true since H4 introduced the entity. Reached only through an approved
`ModuleDefinition` suggestion in Practice Gym: when a suggestion's Activity Definition is
Approved, uses a supported activity type (`gap_fill` or `multiple_choice_single`), and has a
valid, auto-scorable Form.io schema, the student now sees a real **Start** button instead of
"Coming soon."

Starting a suggestion materializes it into a real `LearningActivity` — using the exact same
mechanism the existing `ActivityTemplate` Form.io pilot already uses — so the student is taken to
the same, completely unmodified `/activity` page, Form.io renderer, and submission/scoring flow
that already works today. No new scoring code was written; no new attempt entity was created. A
small new bridge table (`StudentActivityDefinitionLaunch`) records which Module/Activity
Definition/Learn Item each launched activity came from, purely for traceability and admin
diagnostics.

Activity types that can't be auto-scored yet (`short_answer`, anything requiring manual or
AI-assisted review, unsupported renderers, invalid schemas) fail gracefully with a clear,
student-safe reason — the Practice Gym page never breaks or shows a scary error.

No application code was removed. `ActivityTemplate`, `PracticeActivityCache`,
`StudentActivityReadinessItem`, and the full `LearningActivity`/`LearningSession`/
`SessionExercise` runtime are all untouched — H10 makes future H9 cleanup safer by proving one
more thing works, it doesn't remove anything itself. Today's module card remains display-only for
now (`TODO-H10-2`); a native `ActivityDefinition` attempt runtime remains deferred (`TODO-H10-3`).

+30 backend tests (3,925 total: 16 unit, 2 regression, 12 integration). Full detail:
`docs/architecture/product-model-realignment-h0.md` and
`docs/reviews/2026-07-10-phase-h10-activitydefinition-runtime-launch-bridge-review.md`; roadmap:
`docs/roadmap/road-map.md` §1.

---

## Content Studio/Admin IA Cleanup and Removal Readiness (Phase H8, 2026-07-10)

The admin sidebar's Content Studio navigation is now cleaned up. It splits into **Content
Studio** (Import Content → Resource Bank → Learn Items → Activities → Modules — the primary
content-authoring flow) and **Content Ops** (Resource sources/import runs/candidates, Activity
templates, Review queue, Placement items, Onboarding — still-live support/staging surfaces, just
no longer mixed into the primary flow). The four typed resource-bank pages (Vocabulary/Grammar/
Reading reference/Reading passage bank) were removed from primary navigation — this is
navigation-only: their routes, components, and backing tables/APIs remain fully reachable and
untouched, per Plan-Sync-After-H7's audit finding that this was the only proven-safe H8 action.

Learn Items/Activities/Modules page copy no longer says "future Modules" or "will power future
Daily Lessons and Practice Gym" — those are live today (H5-H7) — and now explicitly states that
launching a scored Module/Activity attempt is not implemented yet (planned for H10), so admin UX
never overstates current capability.

No backend file, migration, table, entity, or API was touched; no route or component was
deleted. `ActivityTemplate`, `PracticeActivityCache`, `StudentActivityReadinessItem`, the
runtime session entities, and both Today/Practice Gym legacy fallbacks are all untouched. Full
detail: `docs/reviews/2026-07-10-phase-h8-content-studio-admin-ia-cleanup-review.md`.

---

## Plan-Sync-After-H7 — Legacy Bank Removal Strategy (2026-07-09, docs-only)

With both H6 (Daily Lesson) and H7 (Practice Gym) module pipelines complete, additive, and
fallback-safe, the project now moves into cleanup/removal planning. The user clarified the
direction: **legacy invalid bank/admin structures should be removed, not merely hidden.**

A full audit of pre-H-track structures (Cefr* bank entities, resource-import staging,
`ActivityTemplate`, the `LearningActivity`/`LearningSession`/`SessionExercise`/`LearningModule`
runtime, `PracticeActivityCache`, `StudentActivityReadinessItem`, the Today/Practice Gym legacy
AI-generation fallbacks, old typed admin resource pages, legacy generation admin pages) found
that **almost everything old is still core runtime infrastructure or a live fallback path** —
nothing audited is yet a proven-safe destructive-removal candidate. The only concrete low-risk
action identified is trimming redundant admin *navigation* for the four typed resource-bank
pages (not their tables, APIs, or data).

Three future phases are now defined (**none implemented yet**):

- **H8 — Content Studio/Admin IA Cleanup and Removal Readiness** — safe UI/nav cleanup only.
- **H9 — Legacy Bank Structure Removal and Consolidation** — the first genuinely destructive
  phase, gated on a per-item safety audit.
- **H10 — ActivityDefinition Runtime Launch Path / Attempt Bridge** — must be resolved before H9
  could ever remove `ActivityTemplate` (still the only path that launches a scored Form.io pilot
  activity — Practice Gym module suggestions from H7 remain display-only with no launch path).

No application code, migration, table, entity, API, or UI page changed this phase. Full detail:
`docs/reviews/2026-07-09-plan-sync-after-h7-legacy-bank-removal-strategy.md`.

---

## Practice Gym Module Pipeline (Phase H7, 2026-07-09)

`ModuleDefinition` is now consumed by Practice Gym too — the second runtime consumer after H6's
Daily Lesson pipeline. A deterministic selector (`IPracticeGymModuleSelectionService`, no AI
call, no database writes) suggests approved Modules — one with at least one approved linked
Learn Item AND at least one approved linked Activity Definition — for a student's Practice Gym,
extending H6's approach with self-directed skill/subskill/objective/difficulty requests and
weakness-signal preferences. The result is attached **additively** to the existing suggestions
response as an optional `moduleSuggestions` section — the existing readiness-pool-backed
suggestion logic (both Practice Gym entry points) is completely unchanged, and every "no suitable
content" case safely degrades to the existing suggestions with no module section at all. Nothing
shown to students ever includes an answer key or scoring rule.

Students see a small, read-only "Recommended module practice" section on the Practice Gym page
when Modules were suggested (title, skill/CEFR badges, estimated minutes, reason) — there is no
"Start" button yet, only a "Coming soon" label, because `ActivityDefinition` has no attempt/
scoring runtime wired to it anywhere in the codebase; launching module-based practice safely is
future work. Admins get two new read-only diagnostics:
`GET api/admin/practice-gym/modules/preview` (what would be suggested for a student, with the
fallback reason if nothing would be) and `GET api/admin/practice-gym/students/{id}/assignments`
(what was actually recorded), plus a matching "Practice Gym module selection" card on the admin
student-detail page. A new additive `student_practice_gym_module_assignments` table records
suggestions per student, purely for admin diagnostics and a 14-day reuse guard — it is not a
student attempt/score record.

+40 backend tests (3,895 total: 26 unit, 14 integration). No PG-v2 started, no full Practice Gym
redesign, no student self-authored/custom module creation, no Module attempts, no module scoring,
no mastery updates from Modules, no `ActivityTemplate`/`LearningActivity`/`LearningSession`/
`PracticeActivityCache` replacement, no readiness/delivery-queue change, no Today/Practice Gym
fallback removed, no legacy bank/admin structure removal. **Decided but not yet scheduled:**
legacy invalid bank/admin structures should be removed (not hidden) once H6/H7 are proven, via a
future H8 (Content Studio/Admin IA cleanup and removal planning) and H9 (Legacy Bank Structure
Removal and Consolidation). Full detail: `docs/architecture/product-model-realignment-h0.md` and
`docs/reviews/2026-07-09-phase-h7-practice-gym-module-pipeline-review.md`; roadmap:
`docs/roadmap/road-map.md` §1.

---

## Daily Lesson Module Pipeline (Phase H6, 2026-07-09)

`ModuleDefinition` (Phase H5) is now consumed at runtime for the first time. A deterministic
selector (`IDailyLessonModuleSelectionService`, no AI call, no database writes) picks an
approved Module — one with at least one approved linked Learn Item AND at least one approved
linked Activity Definition — for a student's Today, preferring an exact CEFR match and only
broadening to another level as an explicit "review/scaffold... fallback" choice, never silently.
The result is attached **additively** to the existing Today response as an optional
`moduleSection` — the existing session-generation path is completely unchanged, and every "no
suitable content" case (no CEFR set, no compatible Module, everything recently used, an
unexpected error) safely degrades to the legacy Today content with no module section at all.
Nothing shown to students ever includes an answer key or scoring rule.

Students see a small, read-only "Today's module" card on the dashboard when a Module was
selected (title, description, Learn Item text, Activity instructions, CEFR/skill badges,
estimated minutes) — no attempt/submit loop yet. Admins get two new read-only diagnostics:
`GET api/admin/daily-lesson/modules/preview` (what would be selected for a student, with the
fallback reason if nothing would be) and `GET api/admin/daily-lesson/students/{id}/assignments`
(what was actually recorded), plus a matching "Daily Lesson module selection" card on the admin
student-detail page. A new additive `student_daily_module_assignments` table records which
Module (if any) was selected per student per day, purely for admin diagnostics and a 14-day
reuse guard — it is not a student attempt/score record.

+33 backend tests (3,855 total: 21 unit, 12 integration). No H7/PG-v2 started, no student
self-directed module selection, no Module attempts, no module scoring, no mastery updates from
Modules, no `LearningActivity`/`LearningSession`/`ActivityTemplate` replacement, no
readiness/delivery-queue change, no Today/Practice Gym fallback removed. Full detail:
`docs/architecture/product-model-realignment-h0.md` and
`docs/reviews/2026-07-09-phase-h6-daily-lesson-module-pipeline-review.md`; roadmap:
`docs/roadmap/road-map.md` §1.

---

## Module Foundation (Phase H5, 2026-07-09)

The top of `Resource Bank Item → Learn Item/Activity Definition → Module Definition` now exists.
A Module is a reusable, reviewable learning unit combining one or more Learn Items and Activity
Definitions plus a module-level feedback plan (title, description, objective key, CEFR/skill/
subskill/context/focus/difficulty/estimated-minutes metadata). **This is a new, separate entity
from the existing runtime `LearningModule`** (a per-student thematic group of activities within a
`LearningPath`) — Module Definition is not wired into any runtime selection/delivery path this
phase.

Admins can open `/admin/modules` ("Modules", added to the Content Banks nav right after
Activities) to browse/filter/review Modules, or generate one from: a Resource Bank row (the
now-live "Generate Module" row action on `/admin/resource-bank` — only succeeds when an already
**approved** Learn Item and an already **approved** Activity Definition are both linked to that
resource), a Learn Item (a "Generate Module" button on its detail drawer, finding compatible
approved Activities), or an Activity (the same button on its drawer, finding compatible approved
Learn Items), or by explicitly selecting a Learn Item id + Activity Definition id in the Modules
page's own generate modal. Generation is **deterministic** — no AI provider call, same reasoning
as Learn Item/Activity generation — and composes only existing, already-approved content; it never
cascade-generates a new Learn Item or Activity. A draft/pending source is rejected with a clear
message naming what to approve first. Every Module starts pending review; only an explicit admin
Approve/Reject changes that, and editing an approved Module is blocked (reject first to reopen).
**Additive-only migration** (three new tables, no change to any existing table, including runtime
`LearningModule`'s own) — no physical `ResourceBankItem` consolidation, no student assignment, no
Module attempts, no Daily Lesson/Practice Gym pipeline wiring.

+38 backend tests (3,822 total: 27 unit, 11 integration). No H6/H7/PG-v2 started. No external
datasets, no Persian/bilingual content, no direct final-table seeding. Today/Practice Gym legacy
fallback and the readiness/delivery queue are unchanged. Full detail:
`docs/architecture/product-model-realignment-h0.md` and
`docs/reviews/2026-07-09-phase-h5-module-foundation-review.md`; roadmap:
`docs/roadmap/road-map.md` §1, Decision Log (Phase H5 entry), §19a item 20m.

---

## Activity Foundation with Form.io (Phase H4, 2026-07-09)

The "Practice" half of `Resource Bank Item → Learn Item/Activity → Module` now exists. An Activity
is a reviewable, editable practice task design (title, description, student-facing instructions,
activity type, renderer type, Form.io schema, backend-only answer key/scoring rules/feedback plan,
CEFR/skill/subskill/context/focus/difficulty metadata, optional link to a Learn Item) traced back
to the published Resource Bank row(s) it's about. **This is a new, separate entity from two
existing similarly-named things** — `LearningActivity` (a per-student runtime/delivery record used
by Today/Practice Gym) and `ActivityTemplate` (an existing admin-authored template already wired
into the live Practice Gym Form.io pilot) — Activity has Resource Bank/Learn Item traceability
neither of those has, and is not wired into any runtime selection/delivery path this phase.

Admins can open `/admin/activities` ("Activities", added to the Content Banks nav right after
Learn Items) to browse/filter/review Activities, or generate one directly from a Resource Bank row
(the now-live "Generate Activity" row action on `/admin/resource-bank`) or from an existing Learn
Item (a new "Generate Activity" button on the Learn Item detail drawer). Generation is
**deterministic** — no AI provider call, same reasoning as Learn Item generation. Three supported
activity types: `gap_fill` and `multiple_choice_single` (Vocabulary/Grammar — deterministically
scored; multiple-choice generation is rejected outright, not degraded, when no sibling-resource
distractor exists) and `short_answer` (ReadingReference/ReadingPassage — open-ended, honestly
marked as requiring manual/AI evaluation, never a fake score). Scoring rules reuse the existing
shared scoring format already used by placement/onboarding; every generated Form.io schema is
validated through the existing schema-safety service before saving. Every Activity starts pending
review; only an explicit admin Approve/Reject changes that, and editing an approved Activity is
blocked (reject first to reopen). **Additive-only migration** (two new tables, no change to any
existing table, including `ActivityTemplate`'s own) — no physical `ResourceBankItem`
consolidation, no Module entity, no student assignment, no Today/Practice Gym runtime change.

+39 backend tests (3,784 total: 29 unit, 10 integration). No H5/H6/H7/PG-v2 started. No external
datasets, no Persian/bilingual content, no direct final-table seeding. Today/Practice Gym legacy
fallback and the readiness/delivery queue are unchanged. Full detail:
`docs/architecture/product-model-realignment-h0.md`; roadmap: `docs/roadmap/road-map.md` §1,
Decision Log (Phase H4 entry), §19a item 20l.

---

## Learn Item Foundation (Phase H3, 2026-07-09)

The "Learn" half of `Resource Bank Item → Learn Item/Activity → Module` now exists. A Learn Item
is a reviewable teaching/explanation block (title, body/explanation, examples, common mistakes,
usage notes, CEFR/skill/subskill/context/focus/difficulty metadata) traced back to the published
Resource Bank row(s) it's about. Admins can open `/admin/learn-items` ("Learn Items", added to the
Content Banks nav right after Resource Bank) to browse/filter/review Learn Items, or generate one
directly from a Resource Bank row via the now-live "Generate Learn" row action on
`/admin/resource-bank` (previously a disabled "coming soon" placeholder). Generation is
**deterministic** — the draft is composed directly from the selected resource's own fields, no AI
provider call — because no existing AI service in this codebase generates teaching prose from
source text; `GenerationProvider` is honestly stamped `"Deterministic"`, never a fake AI
attribution. Every Learn Item starts pending review; only an explicit admin Approve/Reject changes
that, and editing an approved Learn Item is blocked (reject first to reopen). **Additive-only
migration** (two new tables, no change to any existing table) — no physical `ResourceBankItem`
consolidation, no Activity/Module entity, no student assignment.

+30 backend tests (3,745 total: 22 unit, 8 integration). No H4/H5/H6/PG-v2 started. No external
datasets, no Persian/bilingual content, no direct final-table seeding. Today/Practice Gym legacy
fallback and the readiness/delivery queue are unchanged. Full detail:
`docs/architecture/product-model-realignment-h0.md`; roadmap: `docs/roadmap/road-map.md` §1,
Decision Log (Phase H3 entry), §19a item 20k.

---

## Import Content UX v1 (Phase H2, 2026-07-09)

A product-friendly admin entry point over the existing Phase E1 import pipeline. Admins can now
open `/admin/content/import` ("Import Content", first Content Banks nav item), paste text/CSV/
JSON, choose a broad resource type (vocabulary/grammar/reading — Listening/Speaking/Writing/Mixed
shown "coming soon", since `ResourceCandidateType` has no shape for them yet) and default metadata
(CEFR/skill/subskill/context tags/focus tags/difficulty band — applied only when a row doesn't
already carry its own value), and get pending `ResourceCandidate` rows staged through the same
gate/parse logic a file upload would use. **No schema/migration, no new published-bank writes, no
AI-guessed classification** — deterministic mapping only, honestly labeled as such. File upload
and async handling of very large imports remain on the existing Resource Import Runs page
(unchanged), out of scope for the new endpoint. Imported rows stay pending review; nothing is
published until the existing Resource Candidates approve/publish flow runs.

+22 backend tests (3,715 total: 16 unit, 6 integration). No H3/H4/H5/PG-v2 started. No external
datasets, no Persian/bilingual content, no direct final-table seeding, no student assignment.
Today/Practice Gym legacy fallback and the readiness/delivery queue are unchanged. Full detail:
`docs/architecture/product-model-realignment-h0.md`; roadmap: `docs/roadmap/road-map.md` §1,
Decision Log (Phase H2 entry), §19a item 20j.

---

## Unified Resource Bank Admin Read Model (Phase H1, 2026-07-09)

Implements H0's Option B direction. Admins now have one Resource Bank view
(`/admin/resource-bank`, "Resource Bank") aggregating the four existing typed published bank
tables (vocabulary/grammar/reading-references/reading-passages) with type/CEFR/skill/search
filters — instead of visiting four separate typed pages to see what content exists. **No physical
`ResourceBankItem` table, no schema/migration.** The four typed pages/APIs/tables are unchanged
and remain fully reachable; this is additive.

The unified page's row actions include **disabled "Generate Learn (coming soon)" / "Generate
Activity (coming soon)" / "Generate Module (coming soon)"** — real, visible placeholders (not
working buttons) for the H3/H4/H5 phases that don't exist yet. This is intentional: it tells every
admin who opens the page that the target Resource → Learn/Activity → Module model is coming,
without pretending it is already built.

+22 backend tests (3,693 total). No H2/H3/H4/H5/PG-v2 started. No external datasets, no
Persian/bilingual content, no direct final-table seeding. Today/Practice Gym legacy fallback and
the readiness/delivery queue are unchanged. Full detail:
`docs/architecture/product-model-realignment-h0.md`; roadmap: `docs/roadmap/road-map.md` §1,
Decision Log (Phase H1 entry), §19a item 20i.

---

## Product Model Realignment Opened (Phase H0, 2026-07-09, docs-only)

D6 closed the bank-first selector-quality track (E6–E10, D1–D6), but the admin/product model was
never realigned to match: no `Learn Item` concept, no `Module` concept, admin still sees many
separate technical bank pages. **Phase H0 is a docs-only planning phase** — it does not change
any code, migration, entity, API, Angular file, or test. It defines the intended target model:

```
Resource Bank Item → Learn Item / Activity → Module → Daily Lesson / Practice Gym
  → Attempt → Feedback + Rating → Learner Memory / Mastery
```

and a new **H-track** (H1 Unified Resource Bank Admin Read Model → H2 Import Content UX v1 → H3
Learn Item Foundation → H4 Activity Foundation with Form.io → H5 Module Foundation → H6 Daily
Lesson Module Pipeline → H7 Practice Gym Module Pipeline → H8 Admin IA Simplification).
**Recommended next implementation phase: H1** (a read-only admin aggregation over the existing
typed bank tables — no schema/migration). The direction chosen for the unified Resource Bank is
**Option B (admin read model over existing typed tables)**, not immediate physical consolidation.

**The existing bank-first work (E1–E10, D1–D6, described in the sections below) is not superseded
— it remains the real, tested substrate the H-track will build on.** Today/Practice Gym legacy
fallback, the readiness/delivery queue, and PG-v2's planned scope are all unchanged. Full detail:
`docs/architecture/product-model-realignment-h0.md`; roadmap sequencing:
`docs/roadmap/road-map.md` §1, Decision Log (Phase H0 entry), and §19a (items 20h–20p).

---

> **Note on scope of this file.** The dated sections below are a running
> product-state log that ends at Phase 20I (2026-07-03, the last
> live-deployed pilot state). Everything after that — the Clean-Architecture
> work, the Phase B/C Practice-Gym template migration, and the entire
> **bank-first Resource Bank track (Phase E0–E7) and Today bank-first
> composer track (Phase D1–D3)** — has been developed and tested locally but
> **not yet deployed**. For the authoritative current phase sequence, decision
> log, and test totals, see `docs/roadmap/road-map.md` (§1, §19 Decision Log,
> §19a Phase Sequence). The catch-up section immediately below summarizes only
> the most recent bank-first state; it does not replay the full E/D history.

---

## Bank-First Architecture — Current Direction and Phase D3 Closure (2026-07-09)

**Architecture direction.** SpeakPath / LinguaCoach is moving to a
**bank-first teaching architecture**:

* **Resource Banks / Resource Candidates / Activity Templates = the primary
  content model.** Original, English-only content flows through a real
  staging → validation → approval → publish pipeline; final published bank
  tables are never seeded directly.
* **AI generation = fallback, composition, evaluation, diagnostics, and
  personalization** — not the primary content source. The system is no longer
  described as mainly generating large per-student AI activity caches.
* **The readiness / delivery queue = a per-student assignment lifecycle**
  (`StudentActivityReadinessItem` / `IStudentActivityReadinessPoolService`),
  kept and load-bearing — not a random AI-generated cache. It is never deleted.

**Phase D3 is complete (commit `4fced4c7`, 2026-07-09).** It wired the E7 full
reading passage bank (`CefrReadingPassage`) into the Today bank-first composer:

* The Today composer now uses **`CefrReadingPassage` full passages** for
  full-passage-suitable Reading-primary patterns:
  `reading_multiple_choice_single`, `reading_multiple_choice_multi`,
  `reorder_paragraphs`.
* **`CefrReadingReference` (short excerpts) remains** for short/cloze/reference
  cases (`reading_fill_in_blanks`, `reading_writing_fill_in_blanks`) and as the
  fallback when no suitable full passage exists.
* If no suitable bank resource exists, or the selector/AI generation fails,
  **Today's legacy AI-generation fallback remains fully intact.**
* **Practice Gym legacy fallback remains.**
* **Readiness / delivery queue remains** (not deleted).
* `LearningActivity.BankResourceProvenanceJson` now records `ReadingPassage`
  provenance.
* Validation: `dotnet build --configuration Release` passed; `dotnet test
  --configuration Release` = 3,563 passed, 0 failed; Angular production build
  failed only on the known pre-existing bundle-size budget (no frontend files
  changed); Playwright skipped (backend selector/materialization logic only).
* **No Phase E8 started. No PG-v2 started. No external datasets. No
  Persian/bilingual seed content.**

**Phase E8 is complete (2026-07-09).** Plan-Sync-After-D3 chose E8 (more
resource depth/types) before Phase D4 (broader Today composer expansion), and
E8 then delivered that depth: a second original, English-only internal seed pack
(`InternalResourceSeedPackE8Seeder`, distinct source, idempotent) of **40
vocabulary + 20 grammar + 16 short reading references + 8 full reading passages
across A1–B2**, general-English-default with **workplace a minority context**,
all flowing through the existing staging → validation → approval → publish
pipeline. A narrow, additive metadata mapping now carries optional
`focusTags`/`difficultyBand` onto full passages. **No external datasets, no
copied third-party/test-prep content, no Persian/bilingual seed content, no
direct final-table seeding, no composer/selector/Practice-Gym/UI change, no
migration.** Validation: `dotnet build --configuration Release` passed; `dotnet
test --configuration Release` = 3,580 passed, 0 failed (+17 tests); no frontend
files changed. Today/Practice Gym legacy fallback and the readiness/delivery
queue all remain intact.

**Phase D4 is complete (2026-07-09).** It used the deeper E8 bank to make Today
bank-first composition richer and pattern-aware, **without rewriting the Today
composer and preserving every legacy fallback.** `TodayBankResourceSelector` now
builds **pattern-shaped multi-resource bundles**: vocabulary-primary patterns get
up to 3 vocabulary targets plus opportunistic grammar/reading; reading
comprehension/reorder patterns get a full reading-passage anchor plus supporting
vocabulary/grammar; reading cloze patterns get a short reference plus supporting
vocabulary/grammar (never a full passage). A compact pattern-specific instruction
layer shapes the prompt per pattern family. **General English stays the default**
— full passages tagged workplace-specific are skipped unless the learner's routed
goal context is workplace-specific. Provenance now records a per-resource `role`
(primary/supporting). Exact-CEFR/never-upward, novelty, and feedback exclusions
are preserved; AI remains composer/fallback. Validation: `dotnet build
--configuration Release` passed; `dotnet test --configuration Release` = 3,596
passed, 0 failed (+16 tests); no frontend files changed. Today legacy fallback,
Practice Gym fallback, and the readiness/delivery queue all remain intact; no
migration.

**Bank-first track state (through Phase D5, 2026-07-09):** **Phase E9** added
published-bank metadata parity — the lean `CefrVocabularyEntry`/
`CefrGrammarProfileEntry`/`CefrReadingReference` tables gained
`subskill`/`difficulty_band`/`context_tags_json`/`focus_tags_json`, a publish
mapping, an idempotent traceable backfill, and queryable filters (closing
`TODO-D4-1`). **Phase D5** then wired `TodayBankResourceSelector` to consume that
metadata: Today bank-first selection is now context-aware across **all** bank
types via a deterministic strict→loose relaxation ladder
(context/focus/subskill/difficulty, combined with exact-CEFR-first /
review-only-widen-down). The general-English default now applies to the lean
tables too — workplace-tagged vocabulary/grammar/reading-reference rows are
skipped for general learners (matching passages), and workplace content is
preferred when workplace-routed. Topic matching is deterministic metadata
matching only (no embeddings/vector search); D4 pattern instructions, roles,
novelty, and feedback exclusions are preserved; provenance records applied
filters + matched context tags; legacy AI fallback, Practice Gym fallback, and
the readiness/delivery queue are all intact. `TODO-E9-1` is closed; `TODO-D5-1`
notes the internal lean packs carry thin difficulty/focus metadata so those
filters are opportunistic (a content task).

**Phase E10 is complete (2026-07-09).** It closed the D5 metadata-depth gap
(`TODO-D5-1`): `InternalBankMetadataDepthSeeder` (idempotent startup step after the
E9 backfill) **derives** a difficulty band from each internal lean row's CEFR
(A1→1…B2→4, C1/C2→5) and a focus tag from its subskill (e.g.
`vocabulary.collocation` → `["collocation"]`), touching only `Internal/Original`
rows traceable to a single published candidate, filling only empty fields (never
overwriting authored values such as the E8 passages), preserving subskill +
context, never inserting a row, and no-op on rerun. After E10 every internal lean
row carries context + subskill + difficulty + focus, filterable through the
existing E9 query/admin filters. **No schema change (E9's columns already exist),
no external datasets, no Persian/bilingual content, no direct final-table content
insertion, no selector rewrite, no UI.** The D5 selector code was unchanged — E10
only improved the data it reads.

**Phase D6 is complete (2026-07-09).** It closed the runtime-feeding gap
(`TODO-E10-1`) and made Today bank-first bundles topic-aware, using deterministic
metadata matching only. `CurriculumRoutingRecommendation` now surfaces the matched
objective's `Subskill`, and `ActivityMaterializationJob` feeds
`PreferredSubskill = routing.Subskill`, `PreferredFocusTags` (routing focus tags,
falling back to learner focus areas), and `PreferredDifficultyBand` derived
conservatively from `StudentProfile.DifficultyPreference` relative to the routed
CEFR's normal band (shared `CefrDifficultyBand` helper: Gentle → one band lower,
Balanced → CEFR-normal, Challenging → one band higher, unknown → null) into
`TodayBankResourceSelector`. For reading bundles, the primary passage/reference's
first non-workplace context tag becomes a **topic anchor** — strict topic-anchor
rungs are prepended to the D5 relaxation ladder, so supporting vocabulary/grammar
prefer the passage topic (a travel passage pulls travel vocabulary). D5 relaxation,
CEFR policy, workplace-exclusion, flat provenance shape, novelty/feedback
exclusion, and legacy AI/Practice-Gym fallback are all preserved. **No schema
change, no migration, no content, no UI, no PG-v2.** Residual: E10's difficulty
bands are CEFR-uniform, so difficulty narrowing is a no-op for Balanced / a
relaxation otherwise until genuinely mixed-difficulty content exists (mechanism
correct, mixed-band-tested).

**Next-step decision:** a post-D6 checkpoint — **Phase PG-v2A** (backend
skill/objective-first Practice Gym selector), **Phase F** (legacy retirement), or
**Phase G2/G3** (backend/diagnostics cleanup). PG-v2 benefits from E9 parity + D5
wiring + E10 depth + D6 selection. Full reasoning: `docs/roadmap/road-map.md` §1,
§19 Decision Log (Phase D6 entry), and §19a; D6 detail in
`docs/architecture/learning-activity-engine.md` (Phase D6 notes).

---

## Onboarding V2 Cutover + MC-Render Fix (Phase 20I continued, 2026-07-03)

Follow-up manual QA by the product owner found onboarding step-ordering/content
problems and confirmed a previously-orphaned system: a fully-built,
admin-configurable onboarding flow ("V2", `/admin/onboarding`) existed
alongside the live hardcoded 5-step flow ("V1") but was never routed to.
Full findings and root cause: `docs/reviews/2026-07-03-phase-20i-onboarding-cutover-and-mc-render-fix-review.md`.

**Decision: V1 retired outright, all students (new and existing) now go
through V2.** All 12 existing student accounts were already
onboarding-complete, so there was no in-flight-student migration risk.
Closed V2's remaining answer-mapping gaps (career context, session
duration, work experience, learning-goal free text) by reusing existing
`StudentProfile` setters, added a conditional skip so career-context/work-
experience only show to students whose goals include "work," and fixed a
critical cross-cutting bug found during implementation: `OnboardingV2CompleteHandler`
never set the legacy `StudentProfile.OnboardingStatus` field that
`ActivityGetHandler`, `DashboardQueryHandler`, `GetProgressHandler`, and
several other handlers still gate on — without this fix, a V2-onboarded
student would have been silently blocked from Today lessons, dashboard,
and progress.

Also fixed: `ActivityGetHandler` passed `interactionMode: null` on 4
non-pattern-keyed code paths, which the frontend reads as free-text entry
regardless of the actual content shape — real content generated as
multiple-choice would render as a plain text box. Root-caused, not just
patched: resolves the `InteractionMode` of the exercise pattern that
normally serves each `ActivityType` instead.

Deferred to follow-up (approved plan, not yet built): an admin-configurable
placement item bank (replacing the static 72-item C# array, mirroring the
onboarding-step admin CRUD pattern) to also add real reading passages and
listening audio; and a live diagnosis of why Today-lesson TTS audio has
never been generated in production despite valid config. See
`TODO-20I-4`, `TODO-20I-5`, `TODO-20I-6`.

---

## Full Live Student/Admin QA & Data Consistency Audit (Phase 20I, 2026-07-03)

Deep live QA against `pilot.student.20e` (admin + DB views) and a fresh QA
account (full student-side walkthrough: onboarding → placement → Today
lesson → activity → feedback). Full findings, deferred scope, and
product-owner questions: `docs/reviews/2026-07-03-phase-20i-full-live-student-admin-qa-data-audit-review.md`.

Two P1 data-integrity bugs found and fixed, both silent/cosmetic to the
student but real for admin trust and data cleanliness:

**Admin "Adaptive placement assessment" panel always showed "No placement
assessment on record"** even for students with a genuinely completed
placement. Root cause: `AdminPlacementController.GetLatestPlacement`
returned `{ hasPlacement = false }` on the not-found path but just
`Ok(result)` (no `hasPlacement` field at all) on success — the frontend's
`!hasPlacement` check reads a missing field as falsy, so every successful
response was misread as "no placement." This is very likely how a stray
`InProgress` placement assessment ended up in `pilot.student.20e`'s
history from an earlier audit session (the panel's "Start placement"
button creates a new assessment when it thinks there isn't one). Fixed by
explicitly returning `hasPlacement = true` on success.

**Every completed placement assessment produced two identical rows per
skill in `placement_skill_results`** (12 rows instead of 6), visibly
duplicating the "Skill breakdown" grid on the student-facing placement
result page. Root cause: .NET config binding appends config-bound array
items to the class default instead of replacing it, so
`PlacementAssessmentOptions.SkillsToAssess` (6-skill default +
identical 6-skill `appsettings.json` list) resolved to 12 entries at
runtime. Several call sites already defended with `.Distinct()` — two
(`BuildSkillResults`, `CreateInitialItems`) did not. Fixed at the source
via `services.PostConfigure<PlacementAssessmentOptions>` deduplication in
DI setup, plus a unique index on `placement_skill_results
(placement_assessment_id, skill)` as defense-in-depth (with a
pre-index cleanup step in the migration for existing duplicate rows).

Both fixes have new regression tests; full backend suite (3151 tests)
green.

Product questions raised, not resolved: whether `language_pairs` (only
one seeded row, Persian↔English) should be expanded before inviting
non-Persian pilot students, and whether `pilot.student.20e`'s ~1614-item
Practice Gym queue backlog is expected job behavior or a bug.

---

## Live Pilot Stabilization: Readiness Edge Case + Practice Gym Deduplication (Phase 20H, 2026-07-03)

Fixed both issues left open by Phase 20G below: the admin readiness audit
500 for the pilot student (`TODO-20G-3`) and duplicate Practice Gym
"Suggested for you" cards (`TODO-20G-1`).

**Readiness audit 500 (`TODO-20G-3`):** root cause was 4 of
`StudentReadinessAuditService`'s 10 check-category methods
(`AddPracticeGymChecksAsync`, `AddActivityContentChecksAsync`,
`AddAudioTtsChecksAsync`, `AddFeedbackAndReviewScaffoldChecksAsync`)
having zero exception handling, unlike the other 6 which already
converted failures into structured `Warning` checks. Fixed by wrapping
all 4 in try/catch matching the existing pattern — an unexpected data
shape now becomes a structured `Warning` check, never a raw 500. Also
hardened a null-FK query in the audio/TTS check. A new integration test
reproduces the exact reported production shape (49 duplicate Practice Gym
readiness items for one objective, a `speaking` objective linked to a
`ListeningComprehension`-typed activity) and confirms 200.

**Practice Gym duplicates (`TODO-20G-1`):** root cause was
`ReadinessPoolReplenishmentService.FillShortfallAsync`'s duplicate-key
including `PatternKey`, which is only assigned during materialization —
so the queue-time key could never match a materialized item's key, and
replenishment kept re-queuing duplicates for the same objective/level
forever. Fixed by dropping `PatternKey` from the dedup key. Also added
defense-in-depth dedupe in `PracticeGymSuggestionService` so a single item
can never appear in more than one bucket (Continue/Review/Suggested),
with Continue winning ties, caps still applied after dedupe. **Live
validation found a residual gap:** pre-existing duplicate rows (queued
before the fix) each had a distinct materialized activity id, which the
same-activity/same-item dedupe didn't catch. Fixed same-day in a
follow-up commit (`80cb0eb`) that reprioritizes the dedupe key to group
by `(CurriculumObjectiveKey, PatternKey, ActivityType)` first.

**Test coverage:** 1,756 backend unit tests pass, 1,381 backend
integration tests pass, 5/5 architecture tests pass. Angular production
build succeeds (no Angular files touched this phase).

**Status: deployed and confirmed live against `https://speakpath.app`
(2026-07-03).** Committed as `4dc49cc`/`8d216fd`/`80cb0eb`. Readiness
audit for `pilot.student.20e@speakpath.app` returns 200 with a structured
`activities.check_failed` warning (`technicalDetail: "PostgresException"`)
— direct live confirmation the original 500's failure mode is now caught
safely. Practice Gym shows 6 distinct patterns/activity types for one
objective, zero literal duplicate rows. Dashboard/Today/Journey/Progress/
Profile all return 200. **Ready to invite one real controlled pilot
student.** One new, separately-scoped observation logged as `TODO-20H-1`
(Suggested list doesn't diversify across the plan's other objectives —
not a duplicate-data bug).

Review: `docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`.

---

## Live Student Pilot Golden Path Completion (Phase 20G, 2026-07-02)

Completed the Phase 20E/20F pilot walkthrough against production. The
pilot student (`pilot.student.20e@speakpath.app`) completed placement
(CEFR B2), got a real AI-generated lesson, completed an activity with
scored feedback, and every remaining student route (Dashboard, Today,
Practice Gym, Journey, Progress, Profile) loads with real data live.

**Three real, live bugs found and fixed this phase:**

- **Gap-fill activities rendered with zero fillable blanks.**
  `ExerciseRendererComponent.gapFillContent` never unwrapped
  `practiceContent.exerciseData` for pattern-engine-generated content
  (the `stagedExerciseData` unwrap other renderers already used). Fixed;
  confirmed live — the same activity re-rendered with real content and a
  word bank, was completed and scored (4/6).
- **`/api/placement/result` 400'd on every dashboard/profile load** after
  placement completion. `GetPlacementResultAsync` required a legacy
  `ResultJson` field the adaptive completion path never populates. Fixed
  with an adaptive-aware DTO builder from `PlacementSkillResults`, plus an
  assessment-lookup ordering fix for students with multiple assessment
  rows. Confirmed live — the dashboard's per-skill breakdown card, hidden
  until now, now renders.
- **`/journey` always showed "complete your placement assessment"**
  regardless of real state. The controller passed the JWT user ID to a
  method expecting a StudentProfile ID — different GUIDs, so the lookup
  never matched, for every student, always. Added
  `GetJourneyForUserAsync` to resolve correctly. Confirmed live.

**One admin-only P0 found, documented, not fixed in this phase:** the
Phase 20D readiness audit 500s again for this specific pilot student's
data (confirmed isolated — a different, more-advanced student returns
200; all individual sub-endpoints return 200 for the pilot student too —
only the combined audit fails). Root cause not identified; needs
production DB/log access. Tracked as `TODO-20G-3`. Does not block the
student experience. **Fixed in Phase 20H (2026-07-03), see above —
confirmed live.**

**One P1 found, documented:** Practice Gym's "Suggested for you" shows
the same suggestion 6 times — confirmed real duplicate backend data (one
curriculum objective, no diversification), not a rendering bug.
`TODO-20G-1`. **Fixed in Phase 20H (2026-07-03), see above — confirmed
live.**

**What is NOT changed:** No AI scoring, CEFR update, objective
completion, Learning Plan regeneration, or review scaffold behavior
changed. No runtime setting changed in production. No
attempts/submissions/evaluations deleted anywhere.

**Test coverage:** 1,750 backend unit (unchanged), 1,380 backend
integration (+2 new), 5/5 architecture tests (unchanged). 1,551/1,671
Angular unit tests pass (+3 new; same 120 pre-existing failures — 0 new
regressions). Production build clean.

Review: `docs/reviews/2026-07-02-phase-20g-live-student-pilot-golden-path-review.md`.

---

## Production Placement/Readiness P0 Unblocker (Phase 20F, 2026-07-02)

Fixed the P0 found in Phase 20E: `POST /api/student/placement/start` and
the readiness audit both 500'd in production for every student.

**Root cause:** 6 EF Core migration classes (`T62_AdaptivePlacementEngine`,
`T63_PlacementResponseSubmission`, `T65_SpeakingEvaluationFoundation`,
`T66_SpeakingEvaluationAppliedSignal`, `T67_WritingEvaluationTables`,
`T68_WritingEvaluationAppliedSignal`) were missing their `.Designer.cs`
companion file — the file that carries the `[Migration("id")]` attribute
EF Core's discovery relies on. Without it, a migration compiles and looks
correct but is **silently invisible** to `dotnet ef database update` /
`Database.Migrate()`, on every environment, forever. Compounded by 3 pairs
of migrations independently creating the same table, latent until the
"invisible" half of each pair was restored.

**Fix:** added the 6 missing Designer.cs files; made the 5 affected
migrations' `Up()` idempotent (`ADD COLUMN`/`CREATE TABLE`/`CREATE INDEX
... IF NOT EXISTS`) so whichever migration of a duplicate pair runs first
in any given environment's history doesn't conflict with the other. No
`DROP`, no data mutation, additive-only throughout.

**Validated against:** a from-scratch fresh local Postgres database (all
64 migrations apply cleanly, 0 errors), a local sandbox independently
drifted to match production's exact symptom, **and confirmed live against
`https://speakpath.app` immediately after deploy:**
`GET /api/admin/students/{id}/readiness` → 200 (was 500), `POST
/api/student/placement/start` → 201 (was 500), the placement UI rendered
and answered a real question, and admin diagnostics showed zero errors in
the 15 minutes spanning the deploy and this check. `TODO-20F-1` resolved.

**What is NOT changed:** No AI scoring, CEFR update, objective-completion,
or Learning Plan regeneration logic touched. No application/business logic
file changed — only migration files and a new regression test. No
attempts/submissions/evaluations were deleted or modified anywhere.

**New regression test:** `tests/LinguaCoach.ArchitectureTests/MigrationDiscoveryTests.cs`
reflects over every `Migration`-derived class and asserts each has a
`[Migration]` attribute — a fast, dependency-free guard against this exact
bug recurring. It caught a real instance (`T68_WritingEvaluationAppliedSignal`)
on its first run, before manual review had found it.

**Test coverage:** 1,750 backend unit tests pass (unchanged). 1,378
backend integration tests pass (unchanged — these use SQLite `EnsureCreated()`
and don't exercise migration files at all, which is exactly why this bug
was invisible to CI). 5/5 architecture tests pass (+2 new). No frontend
code changed this phase.

Review: `docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md`.

---

## Controlled Student Pilot Smoke QA (Phase 20E, 2026-07-02)

Ran the Phase 20D readiness/repair tooling against production for the
first time, using a freshly-created pilot student
(`pilot.student.20e@speakpath.app`), and walked the full intended student
journey by hand. **Verdict: not ready for a controlled student pilot.**

**What was found:**

- **P0, unresolved:** production returns `PostgresException` 500s on
  `POST /api/student/placement/start` and several related endpoints
  (`readiness`, `writing-evaluations`, `placement/latest`,
  `placement/status`, `placement/current`), plus two recurring background
  job failures (`writing-evaluation`, `writing-signal-application`). A
  brand-new student cannot start placement in production today — this
  blocks the entire pilot flow past onboarding and was **not fixed** in
  this session (root cause needs production DB/log access not available
  here). See `TODO-20E-1` in `TODOS.md`.
- **P0, fixed:** `/progress` (student) and the admin progress-summary
  endpoint both raced `Task.WhenAll(...)` across loaders that share one
  scoped `DbContext`, which EF Core forbids — this surfaced to the student
  as a raw, unstyled internal exception message ("A second operation was
  started on this context instance..."). Fixed in both
  `StudentProgressSummaryHandler` and `AdminStudentProgressHandler` by
  awaiting the loaders sequentially instead. Confirmed this pattern
  existed nowhere else in the codebase.
- **P2, fixed:** four more instances of the Phase 15H UTF-8 mojibake bug
  found and fixed in onboarding step 5, activity feedback text, the CEFR
  assessment prompt, and the onboarding-v2 summary step.

**What is NOT changed:**

- No AI scoring, CEFR update, objective-completion, or Learning Plan
  regeneration behavior changed.
- No runtime setting was changed in production.
- No existing student's data was read-write modified; only a newly
  created pilot student was touched. No attempts/submissions/evaluations
  were deleted anywhere.
- The readiness audit/repair logic itself (Phase 20D) was not modified —
  it could not be exercised in this session because its own endpoint is a
  casualty of the unresolved P0 above.

**Test coverage:** 1,750 backend unit tests pass (unchanged). 1,378
backend integration tests pass (unchanged). 3/3 architecture tests pass.
1,548/1,668 Angular unit tests pass (120 pre-existing, unrelated failures
— unchanged baseline; 0 new regressions from 4 text-only edits).
Production build clean. No Playwright smoke was added — the intended
golden-path flow cannot complete against production until the P0 above is
fixed.

Review: `docs/reviews/2026-07-02-phase-20e-controlled-student-pilot-smoke-qa-review.md`.
Runbook: `docs/pilot/student-pilot-runbook.md`.

---

## Student Data Readiness, Backfill & Pilot Cleanup (Phase 20D, 2026-07-02)

Admins can now answer "can this student safely use the app end-to-end
today?" for any student, and fix a limited set of common data gaps
without a database console or a redeploy.

**What changed:**

- New "Pilot readiness" panel on Admin Student Detail: a
  Ready / Needs attention / Blocked verdict, blocking/warning/info counts,
  an expandable list of ~20 individual checks (account, placement/CEFR,
  Learning Plan, Today lesson, Practice Gym, activity content, audio/TTS,
  review scaffold, progress), and a list of recommended repair actions.
- Admins can run a repair action as a dry run (preview only, no DB change)
  or for real (requires typing a reason). Every real repair writes one
  audit-log entry and is safe to run more than once — running it again
  when there's nothing left to fix is a no-op.
- Four real repair actions ship this phase: generate a missing Learning
  Plan, refill an empty Today lesson, expire readiness items that no
  longer match the student's CEFR level, and expire stale reserved items.
  A fifth button runs all four in sequence.
- Five further suggested repair actions (refill Practice Gym, backfill
  activity metadata, regenerate missing TTS audio, normalize lifecycle
  stage, refresh progress projection) are shown as "Not implemented yet"
  with the reason why, rather than guessed at — tracked in `TODOS.md`.

**What is NOT changed:**

- The audit itself never writes to the database — it only reads.
- No historical activity attempt, submission, or evaluation is ever
  deleted or modified by any repair action.
- No AI scoring, CEFR update, objective-completion, or Learning Plan
  regeneration-from-AI behavior changed.
- No existing student-facing UI or behavior changed except through an
  explicit, reason-required, audited repair action run by an admin.

**Test coverage:** 1,750 backend unit tests pass (+20 new). 1,378 backend
integration tests pass (+8 new). 1,548/1,668 Angular unit tests pass (120
pre-existing, unrelated failures — unchanged baseline; +10 new tests).
Production build clean.

Review: `docs/reviews/2026-07-02-phase-20d-student-data-readiness-backfill-pilot-cleanup-review.md`.
Architecture: `docs/architecture/student-readiness-and-backfill.md`.

---

## Runtime Settings Effective Wiring (Phase 20C, 2026-07-02)

Admin edits to review-scaffold and Practice Gym pilot settings now change real student-facing behavior on the next run/request — not just the admin display.

**What changed:**

- Admin overrides for `EnableReviewScaffoldGeneration`, `DryRunOnly`, `RequireAdminReview`, `MaxScaffoldItemsPerStudentPerDay`, `ScaffoldAllowedSources`, `AllowTodayLessonInsertion`, `MinimumConfidenceForReviewNeed`, `PracticeGymPilotEnabled`, `PracticeGymPilotLabel`, `PracticeGymPilotReason`, and `MaxStudentVisibleScaffoldSuggestions` now flow into `ReadinessPoolReplenishmentService` and `PracticeGymSuggestionService` — the actual background replenishment job and Practice Gym suggestion API. No app restart or redeploy needed; the change applies on the next job run or HTTP request.
- **Fixed a pre-existing gap:** `DryRunOnly` (default `true`, safe) existed since Phase 19A but was never actually consulted by the real generation code — only shown on an admin dry-run display. It's now enforced: when on, review/scaffold items are computed but not persisted; normal lesson generation is unaffected.
- Lesson-generation buffer settings (`ReadyLessonBufferSize`, `RefillThreshold`, `RefillBatchSize`, `EnableBackgroundGeneration`, and the Practice-Gym-per-type refill settings) were confirmed to already be effective — the background jobs read the same database row the admin page writes to.
- The admin feature-gates drawer now shows a "Runtime effective" or "Display only — requires deployment" badge next to each editable setting.

**What is NOT changed:**

- No AI scoring, CEFR update, objective completion, or Learning Plan regeneration behavior changed.
- AI signal-safety gates (mastery-signal application, review/positive signal gates, CEFR-update/objective-completion flags) remain locked and untouched — no dangerous AI learning gate was made editable or wired into behavior.
- Seven lesson-generation settings (`MaxGenerationAttempts`, `GenerationTimeoutSeconds`, `MaxConcurrentGenerationJobs`, `EnableTtsGeneration`, `TtsTimeoutSeconds`, `MaxConcurrentTtsJobs`, `PracticeGymReadyExercisesPerType`) remain editable/audited but display-only — no job in the codebase reads them today, and adding that enforcement was judged out of scope for this careful, limited phase (tracked as `TODO-20C-1`).
- Defaults are unchanged for every student where no admin override exists.

**Test coverage:** 1,731 backend unit tests pass (+14 new). 1,370 backend integration tests pass (+5 new). 1,538/1,658 Angular unit tests pass (120 pre-existing, unrelated failures; 0 new regressions). Production build clean.

Review: `docs/reviews/2026-07-02-phase-20c-runtime-settings-effective-wiring-review.md`.

---

## Admin Runtime Settings & Feature Gates (Phase 20B, 2026-07-02)

Admins can now view, safely edit, and audit operational flags without editing appsettings or redeploying.

**What changed:**

- **New: `/admin/settings/feature-gates` page.** Category/search/risk/status filters over 8 feature-gate groups, each opening a slide-in drawer showing description, risk level, effective value + source (appsettings / database override / default / hardcoded), default value, dependencies, and — where safe — editable controls.
- **Editable this phase:** Review scaffold generation (`EnableReviewScaffoldGeneration`, `DryRunOnly`, `RequireAdminReview`, allowed sources, Today-lesson insertion, confidence threshold, daily cap) and the Practice Gym pilot (enabled flag, student-facing label/reason, max visible suggestions) — previously appsettings-only, now backed by a new `RuntimeSettingOverride` table. Lesson-generation buffer/TTS/Practice-Gym-per-type settings — already DB-backed, now surfaced through the same registry/drawer instead of only the Lessons page form.
- **Read-only this phase:** AI signal-safety gates (speaking/writing mastery-signal application, review/positive signal gates, CEFR-update and objective-completion flags — the latter two are hardcoded `false` in code, not configurable at all) and Learning Plan regeneration (no dedicated flag exists; shown for visibility only).
- **Safety:** Every edit requires a reason; High/Critical-risk changes (enabling generation, allowing Today-lesson insertion) require typing `CONFIRM`; every change/reset writes an `AdminAuditLog` entry; server validates ranges/allowed-values/unknown-keys and rejects edits to locked gates.
- **Admin Lessons and Admin AI Operations pages** now show "Configure"/"Open settings" links (deep-linking via `?gate=`) instead of static "enable `ReadinessPool:X` in config" text.

**What is NOT changed:**

- No change to AI scoring, CEFR update logic, objective completion, or Learning Plan regeneration.
- No change to actual review-scaffold/Practice-Gym-pilot runtime behavior — `ReadinessPoolReplenishmentService` still reads only appsettings; wiring the new override table into that live read path is deferred.
- No secrets, provider API keys, or connection strings are stored or exposed by this registry.

**Test coverage:** 1,717 backend unit tests pass (+13 new). 1,365 backend integration tests pass (+14 new). 1,537/1,657 Angular unit tests pass (120 pre-existing, unrelated failures; all new/touched specs pass). Production build clean.

Review: `docs/reviews/2026-07-02-phase-20b-admin-runtime-settings-feature-gates-review.md`.

---

## Speaking Submission Review Visibility (Phase 16E, 2026-06-28)

Admins can now see all audio recordings submitted by a student before any AI evaluation exists.

**What changed:**

- **New: Admin Speaking Submissions card.** The student detail page (`/admin/students/{id}`) now shows a "Speaking Submissions" card listing all audio-submission attempts. Each row shows: activity title, type, submission date/time, audio format (MIME), and status badge (`Submitted`, `PendingEvaluation`, or `Evaluated`).
- **New: `GET /api/admin/students/{profileId}/speaking-attempts`.** Returns the full list of audio attempts for a student. Filters to attempts with a stored audio file (`AudioStorageKey != null`). Falls back gracefully if the linked activity has been deleted.
- **New: `GET /api/admin/students/{profileId}/speaking-attempts/{attemptId}/audio`.** Streams the audio file bytes to the admin. Verifies ownership before streaming. Never returns raw storage paths or keys.
- **Status model.** Three statuses derived without any schema change: `PendingEvaluation` (submitted, not yet scored), `Evaluated` (score present), `Submitted` (legacy/other attempts with audio).
- **Storage security.** Raw blob paths are never returned to any client. MIME type is derived from the file extension only. An integration test asserts `"speaking-recordings/"` does not appear in any HTTP response body.

**What is NOT changed:**

- No AI speaking evaluation, pronunciation scoring, or speech-to-text.
- No new activity formats or schema migrations.
- No changes to session completion, mastery, or learning plan logic.
- Admin audio playback is deferred: the endpoint exists and is secured, but the Angular admin UI shows "Audio submitted — playback not available in admin yet." (Requires auth-aware blob streaming to be wired in a future phase.)
- Student-facing flows are unchanged.

**Test coverage:** 1,525 Angular unit tests pass (+6 new). 1,242 backend integration tests pass (+8 new). Production build clean.

Review: `docs/reviews/2026-06-28-phase-16e-speaking-submission-visibility-review.md`.

---

## Voice Recording and Speaking Submission Foundation (Phase 16D, 2026-06-28)

Voice recording infrastructure for speaking activities. No AI evaluation, no pronunciation scoring, no new activity formats introduced.

**What changed:**

- **New: `audioResponse` activity flow.** Speaking activities with `interactionMode: "audioResponse"` now render a full recording UI (microphone button, recording indicator, playback preview, re-record, submit). Previously this interactionMode fell through to the default "Activity not available" fallback.
- **New: `VoiceRecorderComponent`.** Handles all `MediaRecorder` lifecycle: permission request, recording state, mic stream cleanup on stop and navigation, preview URL management. Covers all browser error states: unsupported, permission denied, active recording indicator.
- **New: `AudioResponseComponent`.** Thin orchestration layer wrapping `VoiceRecorderComponent`. Holds recorded audio in a signal, shows the Submit button only after recording completes.
- **New: `POST /api/activity/{id}/audio-attempt`.** Backend endpoint accepting audio uploads for any activity type. Applies the same file validation (MIME type, 10 MB limit, 50-file per-student cap, ownership check) as the existing `speaking-attempt` endpoint. Does not run STT or AI evaluation. Returns an empty `ActivityFeedbackDto` (all null) → existing Phase 16B "feedback pending" card shown automatically.
- **Updated: Audio retrieval endpoint.** `GET /api/activity/{id}/attempts/{attemptId}/audio` no longer restricts to `SpeakingRolePlay` activity type, so audio from `audio-attempt` submissions is retrievable. Ownership is still enforced at the attempt level.
- **Unchanged: existing speaking flows.** `SpeakingRolePlay` via `POST /speaking-attempt` (STT + AI evaluation) is fully unchanged. All other renderers (`readAloud`, `repeatSentence`, `respondToSituation`, etc.) remain text-only as before.

**What is NOT changed:**

- No AI speaking evaluation or pronunciation scoring.
- No speech-to-text integration.
- No new activity format definitions or schema changes.
- No changes to session completion, mastery, or learning plan logic.
- No admin UI changes.
- `read_aloud`, `repeat_sentence`, `respondToSituation` renderers remain text-based. The audio submission path is only triggered for activities with `interactionMode: "audioResponse"`.

**Pending state:** When a student submits via `audio-attempt`, the feedback page shows the existing "Your response was saved. Feedback will appear after this activity is evaluated." card. No manual feedback infrastructure is in scope for this phase.

**Test coverage:** 1,519 Angular unit tests pass (23 new). 1,234 backend integration tests pass (9 new). Production build clean.

Review: `docs/reviews/2026-06-28-phase-16d-audio-submission-foundation-review.md`.

---

## Audio/TTS and Listening Activity Reliability (Phase 16C, 2026-06-28)

Hardening-only pass. No new product features, no UI redesign, no AI changes.

**What changed:**

- **P0 fix:** Repeat Sentence activities with a real audio URL now show an audio player. Previously, the `audioUrl` field on the item model was never rendered. Students with Repeat Sentence exercises now receive a full audio playback affordance.
- **P1 fix:** Three listening renderers (Listening Fill-in-Blanks, Highlight Correct Summary, Highlight Incorrect Words) hid the audio player when `audioUrl` was set but `audioScript` was null. Fixed; the player now shows whenever either field is present.
- **P1 fix:** Retell Lecture and Summarize Group Discussion used raw `<audio>` elements, bypassing the shared AudioPlayerComponent. Both are now migrated to `app-audio-player`.
- **P1 fix:** The shared `AudioPlayerComponent` now has a full loading/error/retry state machine. Students see a loading indicator while audio fetches, an error message with a retry button if it fails, and the audio transcript as a fallback when audio is unavailable or fails.

**What is NOT changed:**

- No new exercise formats.
- No new TTS provider or AI evaluation paths.
- No backend API changes. The audio endpoint gate (`ListeningComprehension` only) is a known limitation, not a bug from this phase.
- No changes to session completion model, mastery, or learning plan logic.
- No admin UI changes.

**Test coverage:** 1,496 Angular unit tests pass (17 new). 2,732 backend tests pass. 262 Playwright E2E tests pass. Production build clean.

Review: `docs/reviews/2026-06-28-phase-16c-audio-tts-listening-reliability-review.md`.

---

## Activity Completion and Feedback Loop Hardening (Phase 16B, 2026-06-28)

Hardening-only pass. No new product features, no UI redesign, no AI changes.

**What changed:**

- **P0 fix:** The `/module/session-{sessionId}-{exerciseId}` route guard (`moduleRedirectGuard`) was silently misrouting all students whose session and exercise IDs are standard UUIDs. The `lastIndexOf('-')` split incorrectly cut inside the exerciseId. Fixed with a UUID regex. Students navigating from the lesson page to activities via the module guard now land on the correct activity.
- **P1 fix:** Score improvement message in the activity feedback page had garbled encoding (`â€"` instead of `—`) — same class of bug as the Phase 15H profile fix.
- **P1 fix:** When AI evaluation returns empty feedback, the feedback page now shows an honest "Feedback pending" card instead of a blank layout. Students are informed their response was saved and feedback will arrive.

**What is NOT changed:**

- No new exercise formats.
- No new AI evaluation paths.
- No changes to session completion model, mastery, or learning plan update logic — all verified correct as-is.
- No admin UI changes.

**Test coverage:** 1,479 Angular unit tests pass (35 new). 2,732 backend tests pass. Production build clean.

Review: `docs/reviews/2026-06-28-phase-16b-activity-completion-feedback-loop-hardening.md`.

---

## Student Experience QA Hardening (Phase 15H, 2026-06-28)

Completes a hardening-only pass across all six student pages. No new product features, no visual redesign, no AI changes.

**What changed:**

- CEFR explanation text in the Profile page was corrupted (UTF-8 mojibake — `â€"` instead of `—`). Fixed in `profile.component.ts`. Regression test added.
- Route guard coverage is now fully E2E-verified: all five unauthenticated redirect paths, role-based admin block, placement-required redirects for both `PlacementRequired` and `PlacementInProgress` states, and completed-student block on `/placement`.
- Browser refresh persistence is E2E-verified for all five main student routes (`/dashboard`, `/journey`, `/practice`, `/progress`, `/profile`). Auth survives `page.reload()` because `addInitScript` is used throughout (not `page.evaluate`).
- Mobile viewport (390×844) is now E2E-verified for `/dashboard`, `/journey`, and `/practice` — no horizontal overflow, bottom-nav visible. (Progress already had mobile coverage from Phase 15F.)
- New file: `e2e/student-smoke.spec.ts` with 18 mocked-API tests. Zero live-backend dependency.

**What is NOT changed:**

- No visual redesign. Student UI visual overhaul is intentionally deferred until after this hardening phase.
- No new AI logic or exercise formats.
- No new API endpoints.

**Test coverage:** 18 new Playwright E2E tests. All 2,732 backend tests and 1,464 Angular unit tests pass.

Review: `docs/reviews/2026-06-28-phase-15h-student-qa-flow-hardening-review.md`.

---

## Student Profile and Preferences (Phase 15G, 2026-06-28)

Completes the full student navigation set. Students can view and edit their learning preferences from a functional Profile page connected to real backend data.

**What changed:**

- Profile page (`/profile`) now loads from `GET /api/profile` and renders all preference fields from real data.
- CEFR level is read-only throughout the student UI. Backend, template, and E2E test all enforce the contract. Students see: "Your level is updated through placement, learning progress, and teacher/admin review."
- Students can update: learning goals, focus areas, support language, translation help preference, difficulty preference, and session length. Saved via `PUT /api/profile/preferences`. Triggers AI learning plan regeneration server-side (fire-and-forget).
- Profile page now includes a **Placement Summary** section pulled from `/api/student/placement/current` and `/api/student/placement/config`. Shows: confirmed/provisional badge, completion date, per-skill CEFR breakdown, and a retake button (gated by `allowPlacementRetake` config).
- Notification preferences section wired to `/api/notifications/preferences`.
- All six main student pages are now functionally complete: Dashboard, Today, Practice, Journey, Progress, Profile.

**What is NOT editable by students:**

- CEFR level (read-only, updated only via placement/admin)
- AI prompts (never exposed to students)

**Test coverage:** 13 new Angular unit tests, 10 new Playwright E2E tests. All 2,732 backend tests and 1,464 Angular unit tests pass.

Review: `docs/reviews/2026-06-28-phase-15g-student-profile-preferences-review.md`.

---

## Student Progress Experience (Phase 15F, 2026-06-28)

Gives students a full view of their improvement, skill gaps, mastery state, and recommended next actions — powered entirely by the adaptive learning engine, with no synthetic statistics.

**What changed:**

- New backend endpoint `GET /api/student/progress/summary` — aggregates CEFR level, learning plan progress, skill profiles, placement data, mastery/review counts, recent activity (placement completions, lesson sessions, practice gym events), and AI-generated focus recommendations. All data loaded in parallel via `Task.WhenAll`.
- Progress page (`/progress`) fully rewritten. Shows: Learning Summary (4 CEFR/phase/objectives stat cards + plan progress bar), CEFR Progress arc (starting→current with improvement indicator), Skill Progress (per-skill bars with "needs work" chips), Mastery & Review (4 stat grid + weak skill labels), Focus Recommendations (journey summary, next steps, recurring mistakes), Recent Activity timeline.
- New admin endpoint `GET /api/admin/students/{id}/progress-summary` — exposes CEFR levels, mastery/progress/review counts, skill summary, last activity date, and learning phase for admin oversight.
- Admin student detail page has a new **Progress Summary** card (CEFR & level panel + mastery & skills panel) after the Learning Journey card.
- Dashboard already linked to `/progress` — no dashboard changes needed.

**What is NOT shown:** No fake statistics. All sections show real data or a "not available yet" message if the student hasn't completed placement or activities.

**Files added:**
- `src/LinguaCoach.Application/Progress/StudentProgressSummaryQueries.cs`
- `src/LinguaCoach.Infrastructure/Progress/StudentProgressSummaryHandler.cs`
- `src/LinguaCoach.Api/Controllers/StudentProgressController.cs`
- `src/LinguaCoach.Application/Admin/AdminStudentProgressQueries.cs`
- `src/LinguaCoach.Infrastructure/Admin/AdminStudentProgressHandler.cs`
- `src/LinguaCoach.Api/Controllers/AdminStudentProgressController.cs`
- `src/LinguaCoach.Web/src/app/core/models/student-progress-summary.models.ts`
- `tests/LinguaCoach.IntegrationTests/Api/StudentProgressSummaryTests.cs`

**Test coverage:** 9 backend integration tests, 23 Angular unit tests (full progress component spec rewrite), 15 Playwright E2E tests.

Review: `docs/reviews/2026-06-28-phase-15f-student-progress-experience-review.md`.

---

## Adaptive Practice Gym Experience (Phase 15D, 2026-06-28)

Exposes the existing server-side adaptive suggestion engine in the Practice Gym UI.

**What changed:**
- Suggestion cards now show the recommendation reason (e.g. "Recommended because Listening is your weakest skill") via `data-testid="suggestion-reason"`. The `explanation` field was already populated server-side; only the template was missing.
- Review queue section is always visible. Shows "You're all caught up. Nothing needs review right now." when empty (`data-testid="review-queue-empty"`). Previously hidden.
- Error state has a retry button (`data-testid="suggestions-retry"`) that reloads suggestions.
- New admin endpoint `GET /api/admin/students/{id}/practice-summary` exposes practice state (status, review queue count, reserved count, weakest skill, top suggestion, replenishment flag). Admin student detail page shows a Practice Gym summary card.

**No new algorithms or exercise formats.** All adaptive intelligence was already implemented in `IPracticeGymSuggestionService`.

**Test coverage added:** 6 Karma (practice-gym), 7 Karma (admin-student-detail), 4 backend integration, 2 E2E (Playwright pending CI run).

---

## CourseReady Transition and First Lesson Dashboard (Phase 14B, 2026-06-27)

Closes the post-placement gap. After completing placement, students now transition from `PlacementCompleted` to `CourseReady` when their learning plan generates successfully.

**What changed:**
- `POST /api/student/placement/complete` now transitions lifecycle to `CourseReady` (was stuck at `PlacementCompleted`). Falls back to `PlacementCompleted` if plan generation fails.
- Dashboard for `PlacementCompleted` students now shows: "Your personalised course is being prepared. Practice Gym is available while you wait."
- Dashboard for `CourseReady` students (null session state) now shows: "Your first lesson is being prepared / Check back in a moment."
- Admin student detail page now shows **Learning Ready** badge, **Learning Plan** badge, and **Placement Completed** date under the profile card.

**Known limitation:** If no lesson session exists for a `CourseReady` student, `/api/sessions/today` returns an error that propagates to the main dashboard error state. A future phase should handle session unavailability gracefully (return null / 204 rather than an error).

Review: `docs/reviews/2026-06-27-phase-14b-courseready-transition-review.md`.

---

## Student Placement Journey — End-to-End (Phase 14A, 2026-06-27)

Students now experience a complete adaptive placement journey from first login through to their personalised learning plan.

**What it does:**
- Student lands on `/placement` after completing onboarding (lifecycle: `PlacementRequired`)
- Welcome screen explains the assessment (8–15 adaptive questions, ~5–10 minutes, no pass/fail)
- `POST /api/student/placement/start` creates a new assessment and transitions lifecycle to `PlacementInProgress`
- `GET /api/student/placement/next?assessmentId=` returns the next adaptive question (MCQ or gap-fill)
- `POST /api/student/placement/respond` scores the answer deterministically and returns the next item or signals completion
- `POST /api/student/placement/complete` finalises scoring, updates CEFR level, triggers learning plan regeneration, transitions lifecycle to `PlacementCompleted`
- Student is redirected to `/dashboard` after completion; `placementAccessGuard` blocks re-entry to `/placement` for completed students
- `POST /api/student/placement/resume` resumes an interrupted assessment or starts a new one if none exists
- `GET /api/student/placement/config` exposes gate flags to the UI: `PlacementRequiredBeforeLearning`, `AllowSkipPlacement`, `AllowPlacementRetake`, `AutoStartPlacement`

**Placement result screen:** Shows overall CEFR level (indigo badge), provisional warning when confidence is low, 2-column skill breakdown grid, and "Go to Today's lesson" button.

**Admin lifecycle actions:** Admin student detail page now shows Abandon and Expire buttons on in-progress assessments (`POST /api/admin/students/{id}/placement/{assessmentId}/abandon|expire`).

**Config flags (all in `appsettings.json` under `PlacementAssessment`):**
- `PlacementRequiredBeforeLearning` — default `true`
- `AllowSkipPlacement` — default `false`
- `AllowPlacementRetake` — default `false`
- `ResumeInterruptedPlacement` — default `true`
- `AutoStartPlacement` — default `false`

**No migration.** All lifecycle stages already existed in the enum and database column.

+17 backend integration tests. +19 Angular unit tests. All 2,690 backend tests pass.

Review: `docs/reviews/2026-06-27-phase-14a-student-placement-journey-review.md`.

---

## Adaptive Placement Response Submission and Real Scoring (Phase 13B, 2026-06-27)

Replaces the simulated 70% outcome with a fully deterministic adaptive placement engine.
Admin can now observe real per-item scoring, per-skill confidence, and adaptive item selection in real time.

**What it does:**
- `POST /api/admin/students/{id}/placement/{assessmentId}/items/{itemId}/submit` — scores a student's response deterministically (case-insensitive trim comparison, no AI).
- `GET /api/admin/students/{id}/placement/{assessmentId}/progress` — returns full progress: answered count, total items, per-skill confidence state, per-item history.
- `GET /api/admin/students/{id}/placement/{assessmentId}/items` — returns item history array.
- Adaptive next-item selection: last score ≥ 0.8 → harder; < 0.4 → easier; else same level. Targets least-evidenced skill first.
- Completion triggers: max items reached, all-skill confidence threshold met, or items exhausted.
- Duplicate submission is idempotent (re-returns existing result, no re-score).
- Admin student detail page shows live adaptive progress section and per-item history table while assessment is in progress.

**Confidence formula:** `(Min(count/6, 1.0) × 0.6) + (avgScore × 0.4)`. Bonus +0.10 for 3+ consecutive successes; penalty −0.15 for 3+ consecutive failures. Conservative CEFR = minimum across skills.

**New columns (migration T63):** `evaluation_notes varchar(1000)`, `duration_seconds integer` on `placement_assessment_items`.

**New service:** `IPlacementScoringService` / `PlacementScoringService` — deterministic, no LLM.

+23 tests. All 2684 pass (3 arch + 1504 unit + 1177 integration). 1384 Angular tests pass.

Review: `docs/reviews/2026-06-27-phase-13b-adaptive-placement-response-submission-real-scoring.md`.

---

## Adaptive Placement Engine Foundation (Phase 13A, 2026-06-27)

Delivers the complete foundation for adaptive placement assessment. Students can be
assessed via a deterministic 72-item seeded bank across 6 skills (listening, reading,
writing, vocabulary, grammar, speaking) at 4 CEFR levels (A1–B2). No AI calls in Phase 13A.

**What it does:**
- Admin can start a placement assessment for any student via `POST /api/admin/students/{id}/placement/start`
- Assessment creates items from seeded bank at the student's current CEFR level (or A2 fallback)
- Admin triggers completion via `POST /api/admin/students/{id}/placement/{assessmentId}/complete`
- Per-skill CEFR levels computed deterministically; overall CEFR is the minimum across skills (conservative)
- `StudentProfile.CefrLevel` updated automatically when confidence >= 0.6
- Learning Plan regenerated after completion (`placement_completed` reason)
- Admin student detail page shows placement status, CEFR, confidence, provisional warning, skill table

**Domain model:** `PlacementStatus` now has `Abandoned`, `Expired`, `Failed` states. `PlacementAssessment` supports `Abandon()`, `Expire()`, and `CompleteAdaptive()`. Two new entities: `PlacementAssessmentItem` and `PlacementSkillResult`.

**Migration T62** adds 7 new columns to `placement_assessments` and creates `placement_assessment_items` and `placement_skill_results` tables.

**What's next (Phase 13B):** Student response submission endpoint, real adaptive item selection, AI-generated item bank, expiry background job, student-facing adaptive placement flow.

+28 tests. All 2661 pass (3 arch + 1493 unit + 1165 integration).

Review: `docs/reviews/2026-06-27-phase-13a-adaptive-placement-engine-foundation-review.md`.

---

## Real-Time Learning Plan Progress Integration (Phase 12G, 2026-06-27)

Activity submissions now update learning plan objective status immediately — no wait
for the nightly mastery sweep. Background jobs are now reconciliation-only.

**Real-time pipeline:** `ActivitySubmitHandler` calls `TryUpdateObjectiveProgressAsync`
after recording a learning event on pattern-keyed activities. If mastery evidence is
sufficient (`Mastered` or `NeedsReview` signal), the objective transitions in the same
HTTP request. The call is best-effort — submission never fails because of plan update.

**Paths covered:** Pattern evaluation path and legacy writing path (when pattern key
is present). VocabularyPractice and ListeningComprehension excluded — those paths do
not record a learning event.

**Progress summary:** `LearningPlanProgressSummary` now includes `CurrentObjectiveKey`,
`NextObjectiveKey`, and `ObjectivesCompletedToday`. Admin progress endpoint
(`GET /api/admin/students/{id}/learning-plan/progress`) returns these automatically.

**No student UI changes. No new migrations. No new API endpoints.**

15 new real-time progress unit tests. All 2633 tests pass (3 arch + 1475 unit + 1155 integration).

Review: `docs/reviews/2026-06-27-phase-12g-realtime-learning-plan-progress-review.md`.

---

## Learning Plan Completion Lifecycle (Phase 12F, 2026-06-27)

Closes the lifecycle loop: Learning Plan objectives now transition deterministically
through `Active → InProgress → Completed → Mastered` driven by existing mastery evidence.

**Completion signals:** `StudentMasteryReport` gains `CompletedObjectiveKeys` — objectives
where mastery signal is `NeedsReview` (some consecutive successes, avg score 50-79). Full
mastery continues to populate `MasteredObjectiveKeys`.

**Completion service:** `ILearningPlanService` gains `MarkObjectiveCompletedAsync` and
`MarkObjectiveMasteredAsync`. Both are idempotent. Implemented in `LearningPlanService`
with shared `TransitionObjectiveAsync` helper. Logs a warning when all objectives are exhausted.

**Mastery job integration:** `StudentMasteryEvaluationJob` now calls both new methods for
each mastered/completed key before triggering plan regeneration. All calls are warning-only;
generation continues regardless of plan update failure.

**Progress metrics:** `LearningPlanProgressSummary` expanded with `TotalObjectives`,
`ObjectivesMastered`, `ObjectivesInProgress`, `DeferredObjectives`, `CompletionPercentage`,
and `LastCompletedAt`. `MasteryPercentage` now reflects Mastered/Total only.

**No student UI changes. No new migrations. No new API endpoints.**

16 new completion lifecycle unit tests. All 2618 tests pass (3 arch + 1460 unit + 1155 integration).

Review: `docs/reviews/2026-06-27-phase-12f-learning-plan-completion-lifecycle-review.md`.

---

## Learning Plan Guided Routing (Phase 12E, 2026-06-27)

Closes the Phase 12D gap: `PreferredObjectiveKey` is now consumed by `CurriculumRoutingService`.

**Routing change:** When a generation job passes a planned objective key, routing validates it against five safety rules (CEFR exact or one-level-lower-with-scaffold, skill match, runnable, mastery exclusion) and selects it first if all rules pass (`RoutingReason.LearningPlan`). Rejection always falls back to the existing routing pipeline — no generation failure, no silent CEFR downgrade.

**Status lifecycle:** `LearningPlanObjectiveStatus.InProgress` added. When routing returns `LearningPlan`, both `LessonBatchGenerationJob` and `PracticeGymGenerationJob` call `MarkObjectiveInProgressAsync` to advance the plan objective status.

**Admin diagnostics:** `POST /api/admin/curriculum/routing-preview` now accepts an optional `preferredObjectiveKey` field and returns `preferredObjectiveDisposition` ("accepted" / "rejected" / "fallback_used") so admins can test learning-plan routing without running a real generation job.

**No student UI changes. No new migrations.**

15 new routing tests. All 2602 tests pass.

---

## Learning Plan Orchestrator Foundation (Phase 12D, 2026-06-27)

Deterministic per-student learning plan layer that coordinates curriculum routing, mastery evaluation, and readiness pool into a coherent objective sequence. No AI calls. No student UI changes. No ReviewScaffold global enable.

**New domain entities:** `StudentLearningPlan` and `StudentLearningPlanObjective` (tables `student_learning_plans`, `student_learning_plan_objectives`). Migration T61. A student has at most one Active or Regenerating plan at a time. Old plans are Superseded on regeneration.

**Plan generation:** Builds a 10-objective sequence (configurable) from a balanced skill rotation (speaking × 2, writing × 2, listening × 2, reading × 2, vocabulary × 2). Inserts review objectives from weak/mastered mastery keys. Prevents duplicate objective keys within the plan.

**Regeneration triggers:** Automatic plan regeneration fires after mastery sweep (when mastery changes), CEFR level admin change, and student preference update. All triggers are non-blocking (failure is logged as warning only).

**Admin visibility:** `GET /api/admin/students/{id}/learning-plan` and `.../learning-plan/progress` endpoints for admin inspection. Read-only, no side effects.

**Job integration:** `LessonBatchGenerationJob` and `PracticeGymGenerationJob` now consult the learning plan for a preferred objective key and pass it to curriculum routing as a hint. Free routing fallback when no plan exists.

**Config (`LearningPlan` appsettings section):** `PlannedLessonCount` (default 10), `MaxUpcomingObjectives` (default 5), `MaxPracticeGymObjectives` (default 5), `MasteryCompletionThreshold` (default 70%).

38 new tests. All 2587 tests pass. Review: `docs/reviews/2026-06-27-phase-12d-learning-plan-orchestrator-foundation-review.md`.

---

## Prepared Lesson Pipeline and Readiness Lifecycle (Phase 12C, 2026-06-27)

Configurable buffer bounds and per-run observability for the readiness pool replenishment engine.

**New config options (`ReadinessPool` appsettings section):**
- `MinimumReadyThreshold` (default 3) — admin alert threshold; students with fewer Ready items than this are counted in `StudentsBelowMinimumThreshold`.
- `MaxBufferCount` (default 20) — hard cap on active items (Queued + Generating + Ready + Reserved) per student per source. Prevents unbounded over-fill. Must be ≥ `TodayLessonPoolTargetCount`.

**Replenishment summary fields added:**
- `SkippedAtMaxBuffer` — items skipped because student was already at the buffer cap.
- `ElapsedMs` — computed from `CompletedAt - StartedAt`.
- `GenerationSuccessRate` — `ItemsQueued / (ItemsQueued + SkippedDuplicates + SkippedAtMaxBuffer)`.

**Aggregate pool health fields added:**
- `StudentsBelowMinimumThreshold` — students with Ready < `MinimumReadyThreshold` (including zero-ready students).
- `AverageReadyPerStudent` — system-wide average, displayed in admin Lessons pool health stat grid.

**No student UI changes. No ReviewScaffold global enable. No migration.**

17 new tests. All 3933 tests pass. Review: `docs/reviews/2026-06-27-phase-12c-prepared-lesson-pipeline-readiness-lifecycle-review.md`.

---

## Mastery Re-evaluation Engine (Phase 10Z, 2026-06-26)

Deterministic mastery classification engine layered on top of the student learning event ledger. Evaluates skill/objective mastery from `StudentLearningEvent` history without any AI calls.

**Mastery thresholds (configurable via `"Mastery"` appsettings section):**

| Rule | Default |
|------|---------|
| Evidence required for any classification | 3 events |
| Mastered: evidence count | ≥ 5 events |
| Mastered: consecutive successes | last 3 |
| Mastered: average score | ≥ 80 |
| AtRisk: consecutive failures | ≥ 2 |
| AtRisk: average score | < 30 |
| Stale item age threshold | 90 days |

**Readiness pool demotion decisions:**
- `Mastered` + review-eligible item → `ConvertToReviewOnly`
- `Mastered` + not useful for review → `Skip`
- CEFR mismatch > 1 level → `MarkStale`
- Item age > 90 days, never consumed → `Expire`
- `AtRisk` or `NeedsPractice` → `KeepReady`
- Terminal state (Consumed/Expired/Failed/Skipped) → `NoChange`

**Background job:** `StudentMasteryEvaluationJob` runs daily via Quartz, evaluates all students with learning events.

**No admin UI added. No student UI added. No migration needed.**

---

## Admin Visual Fixes — Bounded Tables (Phase 10UI-PARITY-REBUILD-2A, 2026-06-24)

Screenshot-driven visual pass against `e2e/screenshots/prod/`. Three admin tables
rendered all rows unbounded versus the paginated design, producing very long pages.
Added client-side pagination (reusing `SpAdminPaginationComponent`) to: AI Usage
"By feature" (8/page), AI Usage "Calls over time" (8/page), and Curriculum objectives
(12/page). Exercise Types and Diagnostics already matched the design. Diagnostics
Background Jobs section remains absent (needs backend endpoint, deferred to 2B). Build
green, 1361 frontend tests pass. No secrets, no fake data, no student-UI or backend
changes. Review:
`docs/reviews/2026-06-24-phase-10ui-parity-rebuild-2-screenshot-driven-admin-visual-fixes.md`

---

## Admin Design Route Map — VERIFIED (Phase 10UI-PARITY-REBUILD-1, 2026-06-24)

Verified Angular admin shell, sidebar nav, and routes against the new design source
`docs/design/speakpath/admin/`. All 15 design nav entries map to existing Angular
routes and components. Sidebar sections and labels match the design exactly
(desktop sidebar plus mobile drawer). Added `/admin/students/create` redirect alias
for the design-canonical create-student path. Full route map in
`docs/design/admin-reference-alignment.md`. No secrets rendered; charts use
"No data available" placeholders only.

---

## Admin Standalone Visual Parity — CLOSED (Phase 10UI-PARITY-FINAL, 2026-06-24)

All admin shared components and feature pages aligned to `docs/design/SpeakPath Admin (standalone) V1.html`.

**Commits:** `104624a` (1C-A), `c051eb8` (1C-B1), `5a0d921` (docs), `6e9196d` (shared DS), `2075134` (FINAL sweep)

### What was aligned

- **Design tokens** — `admin-tokens.css` matches standalone `:root` exactly (`--ink`, `--muted`, `--border`, `--border-2`, `--surface`, `--canvas`, shadow vars, font)
- **Shared components** — card, kpi-card, button, badge, table, pagination, input, toggle all at exact token parity
- **Student detail hero** — radius 14px, gap 18px, sh-xs shadow, `#211B36` name, `#8B85A0` email, no monospace
- **AI config native select** — 36px height, 1.5px `#E2DEF0` border, focus indigo
- **Usage policies rules expansion** — `#FBFAFE` bg, `#ECE9F5` border, `#8B85A0` muted
- **Notifications/Security tabs** — `#8B85A0` inactive, `#5B4BE8` active + underline
- **Color sweep (16 files)** — all Tailwind gray fallbacks replaced with standalone tokens (`#8B85A0`, `#211B36`, `#E2DEF0`, `#ECE9F5`, `#13B07C`, `#F6F4FB`)

### Accepted gaps (P3)

- Graph/chart areas show placeholder divs — no charting library added (policy)
- No live screenshot comparison (backend not running this session)

### Gates

- ✅ Production build clean
- ✅ 1361/1361 frontend tests passing
- ✅ No fake data, no secrets, no student UI changes, no backend changes

Full audit: `docs/reviews/2026-06-24-phase-10ui-parity-final-standalone-admin-screenshot-closure-audit.md`

---

## UI / Backend Reconciliation Audit — complete (Phase 10UI-AUDIT-0, 2026-06-23)

Audit-only phase. No code changed. All admin and student routes audited against backend capabilities.

### Top P0 gaps found (fix immediately)

| Gap | Location | Fix |
|---|---|---|
| `/admin/usage-policies` has NO nav link | Admin sidebar | Add nav item — single HTML change |
| `/admin/curriculum` has NO nav link | Admin sidebar | Add nav item — single HTML change |

### Top P1 gaps found

| Gap | Location | Fix phase |
|---|---|---|
| Readiness pool health not shown | /admin/students/:id | 10UI-FIX-2 |
| Student activity history not shown | /admin/students/:id | 10UI-FIX-2 |
| Onboarding flow viewer missing | No admin page | 10UI-FIX-2 |
| Orphan AdminUsageComponent (stale placeholder) | admin-usage folder | 10UI-FIX-4 |
| Dashboard "AI provider" stat card always static | /admin dashboard | 10UI-FIX-4 |

### Student UI status

Student-facing routes (/today, /journey, /practice, /progress, /profile) are broadly aligned with backend. No P0 gaps. Minor P2 gaps: CEFR not shown on /progress, no Google login button on /login.

Full report: docs/reviews/2026-06-23-phase-10ui-audit-0-ui-backend-capability-reconciliation.md
New TODOs: TODO-UI-01 through TODO-UI-10 in TODOS.md.

---

## Enterprise Auth/Security — FULLY CLOSED (Phase 10Auth-F-FINAL, 2026-06-23)

All 6 implementation phases complete and verified. 2369/2369 .NET + 1025/1025 Angular tests pass. Production-ready for current single-host SpeakPath stage.

Gap check: docs/reviews/2026-06-23-phase-10auth-f-0-enterprise-auth-security-gap-check.md
Closure audit: docs/reviews/2026-06-23-phase-10auth-f-final-enterprise-auth-security-closure-audit.md

### Auth capability summary

| Capability | Status | Detail |
|---|---|---|
| Password policy | ✅ | 10+ chars, upper+lower+digit+special |
| Account lockout | ✅ | 5 attempts, 15-min duration, generic errors |
| Rate limiting | ✅ | 5 policies: AuthLogin/Reset/ChangePassword/ExternalLogin/Refresh |
| Security headers | ✅ | X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy |
| Auth event audit log | ✅ | 23 event types, migration T58, admin endpoint |
| Audit metadata safety | ✅ | No passwords, tokens, secrets, or Google IDs in audit |
| Security notifications | ✅ | 5 notification groups (password change/reset/lockout/external link) |
| Refresh token sessions | ✅ | Hash-only, rotation, reuse detection, migration T59 |
| Session revocation | ✅ | Logout, revoke-all, password change/reset all revoke sessions |
| Google external login | ✅ | Disabled by default, testable abstraction, no migration |
| Force-password-change middleware | ✅ | HTTP 403 on all endpoints except change-password |
| Admin security settings page | ✅ | /admin/security — read-only overview + auth events tab |

### Auth endpoints

- `POST /api/auth/login` — email/password, issues JWT + refresh token
- `POST /api/auth/refresh` — rotate refresh token, issue new access token
- `POST /api/auth/logout` — revoke single refresh token
- `POST /api/auth/revoke-sessions` — revoke all sessions (authenticated)
- `POST /api/auth/change-password` — change password, revoke all sessions
- `POST /api/auth/reset-password` — public, token-validated, revokes sessions
- `POST /api/auth/external/google` — Google ID token login

### Deferred (documented, not blocking)

- CSP header — requires Angular build nonce strategy
- HSTS — requires production TLS confirmation
- Distributed rate limiting — before horizontal scaling
- Admin-initiated per-user session revocation UI
- SMS security notifications — requires SMS provider + phone verification
- Cloud KMS for Data Protection keys — before horizontal scaling
- CAPTCHA / bot protection
- MFA, enterprise SSO/SAML/OIDC — not in current product scope
- Formal deployment guide (`docs/deployment/`)

---

## Enterprise Notification Platform — FULLY CLOSED (Phase 10W-FINAL-2, 2026-06-23)

All notification sub-phases complete and verified. 2291 .NET / 1004 Angular tests pass. Platform is production-ready for in-app and email on single-host Docker.

### Channels delivered

- **In-App:** live bell dropdown, unread count, mark read/all, archive. User-isolated. Committed component.
- **Email:** SMTP provider, SmtpEmailSender (resolves config at send time via `INotificationChannelConfigResolver`), NotificationDispatchJob (Quartz, every 2 min, batchSize=50). SMTP credentials never returned to frontend. Secrets encrypted at rest with ASP.NET Core Data Protection; keys persisted to `dp_keys` Docker volume; keys optionally encrypted via X.509 certificate (`KeyProtectionMode=Certificate`).
- **SMS:** foundation only — `ISmsSender` / `DisabledSmsSender` / `SmsOptions`. No real provider. Phone number collection deferred.

### Admin notification center

- Notifications list (filter by channel/status/category/severity/search, pagination).
- Delivery queue (filter by channel/status/failed-only, retry/cancel actions).
- Configuration tab (InApp/Email/SMS/dispatch status, SMTP safe fields, SMS safe fields, test-email). DB-backed channel config with editable forms (Phase 10W-5C-2/5C-3). Secrets protected with ASP.NET Core Data Protection. `hasPassword`/`hasApiKey` booleans only in API responses.
- Send notification slide-over (InApp + Email channels, recipient lookup, title/body/category/severity/deep-link).
- Templates tab (CRUD, preview, 4 seeded defaults).

### Templates

4 seeded templates: `account.password_reset`/Email, `account.student_created`/Email, `admin.manual_notification`/InApp, `admin.manual_notification`/Email. Simple `{{VarName}}` substitution. Missing variables logged + left visible in output. Password reset and student-created emails use templates with hard-coded fallback.

### Preferences

`notification_preferences` table (migration T56). Per-user category×channel preferences. Account/System categories required (cannot be disabled). SMS always deferred (returns false). User API GET/PUT. Admin read API. Profile section with required/coming-soon indicators.

### Security invariants

- Password reset token: never logged, never stored in metadata, never returned to admin, generic error on failure.
- SMTP password: `HasPassword` bool only in admin config DTO.
- SMS ApiKey: `HasApiKey` bool only in admin config DTO.
- User isolation: all notification queries filter `RecipientUserId == userId`.
- Email not sent inline during requests — always queued to outbox.

### Deferred

- `TODO-10W-5D-UNIQUE-CONSTRAINT`: DB unique index on `(template_key, channel)` for active templates.
- `TODO-10W-PHONE`: phone number collection and verification.
- `TODO-10W-SMS-PROVIDER`: real Twilio/other SMS sender (requires TODO-10W-PHONE).
- `TODO-10W-DP-CLOUD-KMS`: multi-instance production deployments need `PersistKeysToDbContext` or a cloud KMS (Azure KV / AWS Secrets Manager). Deferred until horizontal scaling is needed.
- `TODO-10W-5D-UNIQUE-CONSTRAINT`: DB unique index on `(template_key, channel)` for active templates.
- `TODO-10W-PHONE`: phone number collection and verification.
- `TODO-10W-SMS-PROVIDER`: real Twilio/other SMS sender (requires TODO-10W-PHONE).

Closure audit: `docs/reviews/2026-06-22-phase-10w-final-notification-platform-closure-audit.md`
DB config + secret encryption review: `docs/reviews/2026-06-23-phase-10w-5c-3-runtime-config-resolver-secret-encryption-review.md`
Key persistence review: `docs/reviews/2026-06-23-phase-10w-5c-4-data-protection-key-persistence-review.md`
Key encryption hardening review: `docs/reviews/2026-06-23-phase-10w-5c-5-data-protection-key-encryption-hardening-review.md`

---

## Enterprise Notification Platform — APIs + dispatch foundation complete (Phase 10W-2, 2026-06-21)

In-app notification APIs are live for authenticated users. Outbox dispatch processes InApp items end-to-end; Email/SMS safely queued.

- **APIs:** `GET /api/notifications` (paged, filtered, expires-excluded, archived-excluded), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`, `POST /api/notifications/{id}/archive`.
- **Filters:** `unreadOnly`, `category`, `severity`. Invalid values return 400. Current-user isolation enforced.
- **Dispatch:** `INotificationDispatchService.DispatchDueAsync` — InApp items delivered, Email/SMS items skipped with error until 10W-4/10W-6.
- **Tests:** 2131/2131 .NET (3 arch + 1278 unit + 850 integration).
- **Bell UI:** live notification dropdown at `src/app/design-system/student/notification-dropdown/` (committed, selector `sp-notification-dropdown`). Wired into `StudentAppLayoutComponent`. Gitignored vendor template no longer depended on (10W-4C).

Foundation review: `docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md`
API review: `docs/reviews/2026-06-21-phase-10w-2-in-app-notification-apis-dispatch-foundation-review.md`

## Enterprise Notification Platform — backend foundation complete (Phase 10W-1, 2026-06-21)

Backend notification foundation is in place. Entities, migration, service abstraction, and DI registration are done. No external delivery, no API, no UI yet.

- **Domain:** `Notification`, `NotificationOutboxItem` entities. 4 enums: `NotificationChannel`, `NotificationStatus`, `NotificationSeverity`, `NotificationCategory`.
- **Application:** `INotificationService` with `QueueInAppAsync`, `QueueEmailAsync`, `QueueSmsAsync`, `QueueAsync`.
- **Persistence:** `notifications` + `notification_outbox_items` tables (migration `T54_NotificationFoundation`). 6 indexes.
- **Behavior:** Queuing any channel creates a `Notification` row + a `NotificationOutboxItem` row. No external dispatch yet.
- **Tests:** 2108/2108 .NET (3 arch + 1278 unit + 827 integration).

Gap check: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`
Foundation review: `docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md`

Next: Phase 10W-2 — in-app notification APIs + dispatch worker.

---

## AI Usage + AI Pricing admin — full closure (Phases 10U + 10V, closed 2026-06-21)

Admin AI Usage page (`/admin/ai-usage`) is fully functional:

- **Summary cards**: total calls, success rate, failed, fallback, cost, input/output/total tokens. All respect active filters.
- **Zero-cost alert**: warning banner appears when any AI call in the filtered range was logged with $0 cost and tokens > 0. Includes call count and token total. Updates on every filter/date reload. Disappears when no zero-cost rows match active filters. (Phase 10V-3B)
- **Filter bar**: period preset (All time, Today, Last 7 days, Last 30 days, This month, Custom range). Custom range shows From/To date inputs + Apply/Clear dates; frontend validates both required and from ≤ to before calling APIs.
- **Column filters**: provider, model, feature key, status (success/failed/fallback), student (GUID). All filters compose with date range. Invalid status → 400. Invalid studentId GUID → 400.
- **Recent calls table**: server-side pagination (25/page, max 100), newest-first, paged envelope with `totalCount`/`totalPages`. Changing any filter resets to page 1. Empty/loading/error states.
- **CSV export**: `GET /api/admin/ai-usage/export.csv` — all active filters, up to 10,000 rows, RFC 4180, `Content-Disposition: attachment`. Columns: `CreatedAt, Provider, Model, FeatureKey, StudentId, WasSuccessful, IsFallback, FailureReason, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, CorrelationId`.
- **Daily trend table**: `GET /api/admin/ai-usage/trends` — grouped by calendar day (client-side), zero-fills missing days within a date range, all filters applied. Columns: Date, Calls, Success, Failed, Fallback, Tokens, Cost.
- **AI Pricing config**: `appsettings.json` holds pricing for 12 models (5 OpenAI, 4 Gemini, 3 Anthropic). Read by `AiPricingOptions.GetProviderPricing`. No hardcoded pricing in production C#. `AiModelPricingOverride` DB table added (migration `T53`). `IAiPricingResolver` resolves DB override first, config fallback second, null/0-cost third. All three providers (OpenAI, Gemini, Anthropic) use resolver for runtime cost. Missing pricing logs $0 and does not throw. Admin override management UI in AI Config page (list/create/edit/deactivate). Read-only pricing visibility panel shows current effective price per model.

Deferred: unique override constraint (TODO-10V-UNIQUE-CONSTRAINT), timezone selector, row cap config, student typeahead, charts/alerts, `AiUsageLog` schema extensions (GAP-1 through GAP-7).

**Tests (at closure):** 2080/2080 .NET, 896/896 Angular. All builds clean.

---

## AI Usage recent calls server-side filters (Phase 10U-5)

`GET /api/admin/ai-usage/recent` now accepts `provider`, `model`, `featureKey`, and `status` query params in addition to `from`/`to`/`page`/`pageSize`. Filters apply before count and pagination so `totalCount`/`totalPages` reflect the filtered universe. Invalid `status` returns 400.

Status semantics: `success` = WasSuccessful and not fallback; `failed` = not WasSuccessful; `fallback` = IsFallback (may also be successful).

Admin AI Usage page: four-filter bar above the recent calls table (provider, model, feature, status). Changing any filter resets to page 1 and reloads. "Clear filters" button appears when any filter is active; clearing resets column filters only and does not reset the date period. Pagination preserves active filters.

Summary stat cards are not affected by column filters.

**Tests:** 823/823 Angular tests pass. Backend: 1988/1988 pass (12 new filter integration tests).

---

## AI Usage recent calls server-side pagination (Phase 10U-4)

`GET /api/admin/ai-usage/recent` now returns a paged envelope `{ items, totalCount, page, pageSize, totalPages }` instead of a flat `{ total, items }`. Breaking change to the recent-calls endpoint shape.

Query params accepted: `page` (default 1), `pageSize` (default 25, max 100 — enforced server-side), `from`/`to` (ISO-8601 UTC, from 10U-3). The `/summary` endpoint is unchanged.

Admin AI Usage page: pagination is now server-driven. Changing the page calls `GET /recent?page=N&pageSize=25` with the active date range. Changing the period preset resets to page 1. `sp-admin-pagination` is shown when `totalPages > 1`. Date filtering (period preset select from 10U-3) and pagination compose correctly — `totalCount`/`totalPages` reflect the filtered universe.

Summary stat cards (total calls, cost, tokens, by-provider, by-feature) are independent from recent-call pagination and always reflect the full date-filtered dataset.

**Tests:** 813/813 Angular tests pass. Backend: 1977/1977 pass (10 new pagination integration tests).

---

## AI Usage date filtering (Phase 10U-3)

`GET /api/admin/ai-usage/summary` and `/recent` both accept `from`/`to` UTC query params. Admin AI Usage page has a period preset select above the stat grid: All time, Today, Last 7 days, Last 30 days, This month.

---

## AI Usage token totals and pricing config seed (Phase 10U-1/10U-2)

`GET /api/admin/ai-usage/summary` now includes `totalInputTokens`, `totalOutputTokens`, `totalTokens`. Admin AI Usage page shows three new stat cards for these. `appsettings.json` now has `OpenAI:Pricing`, `Gemini:Pricing`, `Anthropic:Pricing` sections with per-model pricing — unblocks `AiUsageLog.CostUsd` from always being $0.

---

## Student management final validation complete (Phase 10Students-F-H)

All enterprise student management work (Phases 10Students-F-A through 10X-L) validated end-to-end. Backend: 1944/1944 tests pass. Frontend: 791/791 Angular tests pass. Playwright: 6/6 reset tests pass. One E2E mock defect fixed: `admin-students-reset.spec.ts` mock returned old flat-array shape; updated to `PagedResponse` shape. No product contract changes. No student-facing changes.

---

## Admin shared UI: slide-over and table-action fixes (Phase 10X-L)

`sp-admin-slide-over` now renders above the entire admin shell (z-index 1000+, up from 400). Backdrop click no longer closes panels by default (`closeOnBackdrop` default changed to `false`). Stacked panels are supported via `[stackIndex]` input. Set CEFR and Assign Policy flows on `/admin/students/{id}` now use `sp-admin-slide-over` instead of a centred modal div. The three-dot action menu on admin tables no longer causes vertical scroll when opened near the bottom of the table; menu is now rendered `position:fixed` relative to the viewport.

No product behaviour changed for end users. Admin-only change.

**Tests:** 791/791 Angular tests pass. No backend change. No Playwright run (no pre-existing Playwright coverage for these flows).

---

## Student list filter selects available in admin UI (Phase 10Students-F-G)

Admin student list filter bar now has three filter selects: lifecycle stage (12 options), onboarding status (4 options), and CEFR level (A1–C2). Each filter change resets to page 1 and calls the backend. A "Clear filters" button appears when any of search/lifecycleStage/onboardingStatus/cefrLevel is active; clearing does not touch the "Show archived" toggle. Uses `sp-admin-select` component.

**Tests:** 32/32 Angular tests pass (admin-students). No backend change.

---

## Server-side student pagination, filtering, and sorting available (Phase 10Students-F-F)

`GET /api/admin/students` now returns a paged wrapper `{ items, totalCount, page, pageSize, totalPages }` instead of a flat array. Breaking change to the list endpoint shape.

Query params accepted: `page` (default 1), `pageSize` (default 25, max 100), `search` (email/name substring, case-insensitive), `includeArchived` (default false), `lifecycleStage`, `onboardingStatus`, `cefrLevel`, `sortBy` (student/name/email/onboardingStatus/lifecycleStage/cefrLevel/createdAt), `sortDir` (asc/desc, default desc).

Admin student list page is now server-driven: page, search, include-archived, and column sort all trigger a backend call. Pagination UI reflects `totalPages` from the server. All row actions (edit, reset password, reset data, archive) reload the current page after completion.

**Tests:** 756/756 Angular tests pass. Backend: 1944/1944 pass.

---

## Student audit history tab available (Phase 10Students-F-E)

`GET /api/admin/students/{id}/audit-history` returns up to 50 admin action history entries for a student, newest-first, combining `AdminAuditLog` (governance actions: SetCefr, Archive, lifecycle changes, policy assignments) and `StudentResetLog` (lifecycle reset records).

Admin student detail page now shows an "Audit history" section at the bottom with: action badge, source (Audit / Reset), actor ID prefix, reason, old→new value, details (inline for short; slide-over for long). No edit or delete controls. No password or secret fields exposed.

**Tests:** 751/751 Angular tests pass. Backend: 1932/1932 pass.

---

## Admin CEFR management available (Phase 10Students-F-D)

`PUT /api/admin/students/{id}/cefr` allows admins to set or clear a student's CEFR level from the admin student detail page. Valid values: A1, A2, B1, B2, C1, C2 (case-insensitive on input, stored normalised). Null or empty string clears the level.

Admin student detail profile section now shows current CEFR as a badge (or "Not set"), a "Set CEFR" button opening a modal with the level dropdown and optional reason field, and helper text confirming students cannot edit this field.

Each change writes an `AdminAuditLog` entry: action `SetCefr`, old/new value JSON, reason.

No migration required. No student-facing changes. Placement logic unchanged. Student `UpdateLearningPreferences` continues to explicitly exclude `CefrLevel`.

**Tests:** 743/743 Angular tests pass. Backend: 1925/1925 pass.

---

## Dedicated student detail endpoint + onboarding progress complete (Phase 10Students-F-B)

`GET /api/admin/students/{id}` is now a dedicated endpoint returning full student detail including onboarding progress (`StudentOnboardingProgressInfo`). Previously the component used the student list endpoint; it now calls the dedicated endpoint via `AdminApiService.getStudent(id)`.

Onboarding progress section added to `admin-student-detail.component`: status badge, current step (code pill), percentage complete, empty state when no progress row exists.

SQLite integration test blocker resolved: `OrderByDescending(p => p.StartedAt)` removed (unique index; SQLite does not support `DateTimeOffset` in ORDER BY). `OnboardingFlowSeeder` added to `ApiTestFactory` to satisfy FK constraint in onboarding progress tests.

**Tests:** 719/719 Angular tests pass. Backend: 1911/1911 pass. `git diff --check`: clean.

See: `docs/reviews/2026-06-19-phase-10students-f-b-dedicated-student-detail-endpoint-onboarding-progress-review.md`

---

## Admin read: student learning preferences complete (Phase 10Students-F-A)

Admins can now view all student-set learning preferences from the student detail page. A "Student preferences" summary card shows preference fields inline. A "View preferences" button opens the new `sp-admin-slide-over` panel for full detail. Admin edit of preferences is intentionally not implemented.

`sp-admin-slide-over` is now available as the design-system primitive for all admin secondary detail panels (student detail, policy editor, prompt preview, audit history).

No new migration or endpoint was required. Preference fields were already returned by `GET /api/admin/students` (added in Phase 10R-J).

**Tests:** 708/708 Angular tests pass. Backend: 1885 pass. `git diff --check`: clean.

See: `docs/reviews/2026-06-19-phase-10students-f-a-admin-read-student-preferences-review.md`

---

## Student usage policy assignment admin UI complete (Phase 10R-J)

Admins can now view, assign, and reset a student's usage policy from the student detail page.

What changed:
- New "Usage Policy" section on every student detail page.
- Shows effective policy name, scope, and source badge (Student override vs. Global default).
- "Assign Policy" button opens a modal — admin picks any active policy and optionally enters a reason. Saves immediately via `PUT /api/admin/students/{id}/usage-policy`.
- "Reset to Default" button visible only when a student override is active. Requires confirm dialog. Calls new `DELETE /api/admin/students/{id}/usage-policy`.
- If override is removed, student automatically falls back to the global default policy at next AI call.
- Assignment and removal both written to `AdminAuditLog`.
- `TODO-10R-STUDENT-ASSIGN` closed. 681/681 tests pass.

See: `docs/reviews/2026-06-19-phase-10r-j-student-usage-policy-assignment-admin-ui-review.md`

---

## Usage Policy Rule Editor admin UI complete (Phase 10R-H)

Admins can now create, edit, and delete individual usage policy rules directly in the Usage Policies admin page.

What changed:
- "Add rule" button in each expanded policy row opens a modal rule editor.
- Per-rule Edit and Delete buttons with modal forms and delete confirmation.
- Feature select (from definitions API) falls back to free-text input if definitions unavailable.
- Feature key is intentionally immutable on edit — shown as read-only with guidance to delete/re-add.
- All rule fields editable: enforcement mode, unit type, daily/weekly/monthly/cost limits, warning threshold, tracking enabled, active.
- Local state update on success — no full page reload; expanded row state preserved.
- Build clean; 670/670 tests pass.

See: `docs/reviews/2026-06-19-phase-10r-h-usage-policy-rule-editor-admin-ui-review.md`

---

## Usage Policy Rule CRUD backend complete (Phase 10R-G)

Admins can now manage individual usage policy rules via the API.

What changed:
- `UsagePolicyRule.Update(...)` domain method added — all limit fields are now mutable via domain layer.
- Three new admin API endpoints: `POST/PUT/DELETE /api/admin/usage-policies/{policyId}/rules[/{ruleId}]`.
- Duplicate-feature-key guard at application layer prevents two rules for the same feature in one policy.
- Frontend `UsageGovernanceService` has `addRule`, `updateRule`, `deleteRule` methods ready for a UI.
- No migration needed. No UI rule editor built yet (next phase: TODO-10R-RULE-MGMT-UI).

See: `docs/reviews/2026-06-19-phase-10r-g-usage-policy-rule-crud-backend-foundation-review.md`

---

## Usage Governance Admin UX complete (Phase 10R-F)

Usage Policies admin page is production-usable.

What changed:
- Summary stat cards (total / active / default policy name).
- Expandable rule rows showing feature name, enforcement mode, unit type, and limits.
- Feature display names resolved from feature-definitions API.
- All admin design system wrappers used.

See: `docs/reviews/2026-06-19-phase-10r-f-usage-governance-admin-ux-foundation-review.md`

---

## Frontend test cleanup complete (Phase 10X-J-T)

Frontend tests no longer lock admin and student UI work to Tailwind, TailAdmin, BEM, wrapper
implementation, border/radius/spacing, or inline style details unless a class is explicitly
documented as a public API.

What changed:
- Angular specs now prefer text, roles, ARIA attributes, form/CVA values, emitted events,
  open/close behavior, sorting events, and wrapper presence.
- Playwright tests now prefer page behavior and smoke flows: accessible row-action buttons,
  `aria-pressed` chip state, visible text, main landmarks, roles, and `data-testid` locators.
- Style-only tests were removed. Useful tests were rewritten to keep behavior coverage.
- Product behavior, API behavior, backend code, and UI functionality were unchanged.

See: `docs/reviews/2026-06-18-phase-10x-j-t-frontend-test-cleanup-review.md`

---

## Admin form and modal migration complete (Phase 10X-I)

Completed the three deferred admin UI CVA migration targets:

- **AI Config:** provider/model/voice selects kept native inside `sp-admin-form-field` (incompatible
  with `sp-admin-select` string binding). TTS voice, model name, API key (password), and Qwen
  endpoint inputs migrated to `sp-admin-input`. All action buttons migrated to `sp-admin-button`.
- **Integrations:** storage display fields → `sp-admin-input [disabled]`. Generation settings
  number fields kept native `<input type=”number”>` inside `sp-admin-form-field` (CVA string
  constraint). Background job controls and tables migrated to `sp-admin-button` and Tailwind
  table classes.
- **Student modals (all 3):** edit, reset-password, and reset-data page-local modals replaced with
  `sp-admin-modal`. All inner text/password inputs use `sp-admin-input`, multi-line fields use
  `sp-admin-textarea`, and actions use `sp-admin-button`. Page-local modal CSS removed entirely.

Wrapper enhancements this phase:
- `sp-admin-modal`: `maxWidth` input added (default 520px; student edit uses 720px).
- `sp-admin-input`: `[value]` input added for one-way display binding.
- `sp-admin-layout`: content area changed to `<main>` for `role=”main”` semantics (fixes Playwright).

Gates: .NET 1885 passed, Angular 421 passed (up from 411), build clean.

Closed TODOs: TODO-10X-G-AICONFIG-FORMS, TODO-10X-G-INTEGRATIONS-FORMS, TODO-10X-D-MODAL, TODO-10X-I.

See: `docs/reviews/2026-06-19-phase-10x-i-admin-form-modal-migration-review.md`

---

## Admin form wrapper CVA foundation (Phase 10X-H)

Made the TailAdmin-backed admin form wrappers safe for real Angular forms:

- `sp-admin-input`, `sp-admin-select`, and the new `sp-admin-textarea` now implement
  `ControlValueAccessor`. They two-way bind via `[(ngModel)]` or reactive `[formControl]`/
  `formControlName`, propagate disabled state from a disabled `FormControl`, and mark touched on blur.
- `sp-admin-form-field` renders the red `*` required marker via `[required]`.
- This unblocks per-field migration of the AI Config dense provider-credentials grid and the
  Integrations operational forms, which stay native this phase to avoid silent save regressions.
- Existing student-detail modals and the admin-only dark-mode boundary remain deferred.
- Gates: .NET 1885, Angular 394 (up from 379), Playwright 188.

See: `docs/reviews/2026-06-19-phase-10x-h-admin-form-cva-modal-foundation-review.md`

## Finish remaining admin page refactor (Phase 10X-G-F)

Completed the remaining wrapper consistency work after 10X-G:

- Students: the row table is wrapped in `sp-admin-table` (projected mode); lifecycle, onboarding,
  and CEFR pills now use the `sp-admin-badge` wrapper (was raw `.sp-admin-badge` class). Obsolete
  page-local pagination, row-action link, and badge CSS removed. Filter bar, pagination, sortable
  headers, and `sp-admin-table-actions` row menu were already in place from 10X-F/10X-G.
- Curriculum: create/edit and routing-preview form fields now use `sp-admin-form-field` for labels
  and hints (closing TODO-10X-G-CURRICULUM-FORMS). Native ngModel controls kept inside each field
  because `sp-admin-input`/`sp-admin-select` lack a ControlValueAccessor and cannot two-way bind.
- Verified the remaining priority pages (AI Usage, Prompts, Exercise Types, Diagnostics, Usage
  Policies, Integrations cards) were already wrapper-migrated in 10X-B/10X-G; no raw badge/table
  legacy markup remained except student-detail (out of scope this phase).

Intentionally deferred (unchanged, see TODOs): AI Config dense provider-credentials form fields,
Integrations operational forms, student management modals, the admin-only dark-mode class boundary,
and the full 10R-F/10U/10V redesigns.

Angular: 379 passed. .NET: 1885 passed. Playwright: 188 passed.

See: `docs/reviews/2026-06-18-phase-10x-g-f-finish-admin-page-refactor-review.md`

---

## Full admin page refactor (Phase 10X-G)

The highest-legacy admin pages now consume `sp-admin-*` wrappers consistently:

- Dashboard KPI tiles use `sp-admin-stat-card`; sections use `sp-admin-card` (including dashed
  analytics placeholders); status pills use `sp-admin-badge`. Page-local KPI/status/badge/table CSS removed.
- AI Config category Save/Test actions use `sp-admin-button`; duplicate in-card headings removed.
- Curriculum create/edit and routing-preview panels use `sp-admin-card`; actions use `sp-admin-button`.
- The admin header user/profile menu is wired through `sp-admin-dropdown` (open state, click-outside,
  and Escape handled by the wrapper; no page-local menu signal).

Admin-only dark mode remains scoped to `AdminThemeService` (`adminTheme` localStorage key) and does
not affect student UI. Full admin-only dark-mode class boundary is still future work.

Deferred: remaining page-local form fields (`.sp-ai-select`, `.sp-input`, Integrations operational
forms), student management modals, and the full usage-governance/AI-usage/prompt-playground redesigns.

Angular: 377 passed. .NET: 1885 passed. Playwright: 188 passed.

---

## Admin wrapper capability completion (Phase 10X-F)

New wrappers added (2026-06-18):
- `sp-admin-dropdown`: TailAdmin-backed dropdown with content projection, click-outside + Escape close.
- `sp-admin-table-actions`: row action three-dot dropdown. Generic actions API + content projection.
- `sp-admin-theme-toggle`: admin-only dark/light toggle. Uses `AdminThemeService` (isolated from student UI).
- `AdminThemeService`: admin-scoped theme service. `adminTheme` localStorage key.

Updated wrappers:
- `sp-admin-table`: sortable columns (`sortable`, `sortColumn`, `sortDirection`, `(sortChange)`). `hasActions` slot.
- `sp-admin-header`: named `[left]` and `[actions]` content slots. Theme toggle auto-rendered.
- `sp-admin-filter-bar`: named `[search]`, `[filters]`, `[actions]` slots.

`admin-students` row actions migrated to `sp-admin-table-actions`.
Angular: 373 passed. .NET: 1885 passed. Playwright: 188 passed.

Next: full admin page refactor (TODO-10X-G).

---

## TailAdmin wrapper adaptation (Phase 10X-E)

All 15 `sp-admin-*` wrapper components now use actual TailAdmin free Angular template patterns internally. Custom CSS approximations replaced with real TailAdmin class structures.

- Layout shell: `min-h-screen xl:flex`, `xl:ml-[290px]/xl:ml-[90px]` transition — exact TailAdmin Layout One.
- Sidebar: `fixed left-0 top-0 h-screen w-[290px]/w-[90px] bg-white border-r border-gray-200`.
- Header: `sticky top-0 flex w-full bg-white border-b border-gray-200 z-[99999]`.
- Button: brand-500 primary (`#465fff`), outline secondary. Rounded-lg, inline-flex.
- Badge: `rounded-full font-medium text-xs` — TailAdmin light variant color map (success/warning/info/primary/danger/neutral).
- Card / stat-card: `rounded-2xl border border-gray-200 bg-white`.
- Modal: `rounded-3xl bg-white`, backdrop `bg-gray-400/50 backdrop-blur-sm`.
- Table: `rounded-2xl border border-gray-200 bg-white`, th `text-xs text-gray-500 bg-gray-50`.
- Input / select: `h-11 rounded-lg border border-gray-200 bg-transparent`.
- Drawer: `fixed right-0 h-screen bg-white border-l border-gray-200`.
- Pagination / filter-bar: TailAdmin footer/filter bar structures.

Admin feature pages are unchanged — they use `sp-admin-*` only.
Wrapper public APIs (inputs/outputs) are stable.

See: `docs/architecture/admin-tailadmin-adapter-inventory.md`, `docs/reviews/2026-06-18-phase-10x-e-tailadmin-wrapper-adaptation-review.md`

Remaining: table sorting, dropdown, theme toggle (10X-F).

---

## TailAdmin template import and adapter plan (Phase 10X-D)

The actual free TailAdmin Angular template source is now imported as a vendor reference.

- **Source:** https://github.com/TailAdmin/free-angular-tailwind-dashboard (commit da992cf, MIT)
- **Location:** `src/LinguaCoach.Web/src/app/templates/tailadmin/free-angular-tailwind-dashboard/`
- **Gitignored:** yes — clone separately, not committed to main repo
- **Adapter inventory:** `docs/architecture/admin-tailadmin-adapter-inventory.md`
- The stale `admin-template/tailadmin/` placeholder has been removed.
- The target is no longer "TailAdmin-inspired". It is: use the actual TailAdmin source as vendor reference, exposed to SpeakPath only through `sp-admin-*` wrapper components.
- Feature pages must never import from `templates/`.

## Admin UI foundation, core migration, and gate closure (Phase 10X-A / 10X-B / 10X-C-F)

The admin app has a SpeakPath wrapper component layer aligned with TailAdmin Angular Layout One.

**Visual source of truth:** actual TailAdmin Angular free template — `src/app/templates/tailadmin/`.
Styling currently approximates TailAdmin Layout One via CSS custom properties. Full adapter alignment is 10X-E/10X-F.
See `docs/architecture/admin-ui-design-system.md` for the full architecture.

**Critical fix in 10X-C-F:** `AdminAppLayoutComponent` now uses `ViewEncapsulation.None` so
shell CSS (sidebar, nav items, header, drawer, profile flyout) reaches child component DOM.
Before this fix, the CSS existed but was blocked by Angular's default emulated encapsulation.
All admin pages now render with sidebar left, content right, header sticky — matching TailAdmin Layout One.

- Admin tokens: `src/app/design-system/admin/tokens/admin-tokens.css`.
- Barrel: `src/app/design-system/admin/index.ts`.
- Shell wrappers: `sp-admin-layout`, `sp-admin-sidebar`, `sp-admin-header`.
- Page wrappers: page header, card, stat card, button, badge, table, state components, form controls, pagination, filter bar, modal, drawer, and toast outlet.
- Service foundations: admin toast, modal confirm state, drawer state.
- All core admin pages migrated to wrapper layer: Dashboard, Students, AI Config, AI Usage, Prompts,
  Exercise Types, Integrations, Diagnostics, Curriculum, and Usage Policies.

Remaining admin polish is scoped to page-local legacy internals, not new product features.
Dashboard inline CSS, AI Config form internals, Integrations internals, Curriculum create/edit/preview
forms, and student management modals remain future cleanup areas.

See: `docs/architecture/admin-ui-design-system.md`

## What is built and verified

The following end-to-end flow is implemented and verified:

```
Admin logs in
â†’ Admin creates student (temp password shown once)
â†’ Student logs in
â†’ Student changes temporary password (enforced server-side)
â†’ Student completes onboarding (language pair, career profile, experience level)
â†’ Student reaches Today page (the student home/dashboard)
â†’ Student starts Today's Lesson or navigates to Journey, Practice, Progress, or Profile
â†’ Student starts an activity (Writing / Listening / Vocabulary / Speaking)
â†’ Student submits draft or recording
â†’ Student sees structured AI feedback
â†’ Student retries or continues to next activity
â†’ Student can revisit learning history
```

## Playwright gate status

Phase 10H-F restored the full Playwright suite after Practice Gym fixture drift.

Failure categories found and fixed:

- Selector drift from old fixed Practice Gym card IDs to catalog-driven
  `practice-format-*` cards.
- Fixture/test data drift around ready runnable exercise types and planned
  non-runnable AI role play rows.
- Copy/label drift for the landing hero and perfect-score result label.
- Shared audio fallback selector drift after listening formats moved to the
  shared audio player.

Final Playwright result: 175 passed. No tests remain failing or skipped from this
stabilisation pass. No product behaviour changed.

## Usage governance (Phase 10R)

All AI feature calls are tracked per student. Admins can define and assign quota policies with per-feature daily limits. Expensive on-demand AI calls are blocked (HTTP 429) before they incur cost when a student exhausts their quota. Prepared/pre-generated content is always allowed.

- Feature registry: 16 features across PreparedLearning, DynamicAi, ExpensiveAi categories.
- 3 seeded policies: Default Pilot Student (TrackOnly all), Low Cost Student (HardLimit 5/day writing, 3/day speaking, 5/day TTS), Test Unlimited (TrackOnly, for testing).
- Admin UI at `/admin/usage-policies` — create, edit, assign policies.
- HTTP 429 response includes `featureKey`, `availableAlternatives`, `resetAt`.
- Audit log written for every policy assignment change.

Deferred: workspace/cohort inheritance, billing, monthly/weekly limits, student-facing widget.

See: `docs/architecture/usage-governance.md`

---

## Learning preferences in AI context

Student profile preferences are used by AI generation and evaluation.
Generated Today lesson activities, Practice Gym activities, background Practice
Gym activities, buffered lesson activities, lesson batch planning summaries, and
AI activity evaluation (WritingScenario, SpeakingRolePlay) all receive compact
learner preference context when fields are present.

The context can include preferred name, learning language, support language,
translation help preference, learning goals, custom goal, focus areas, custom
focus, difficulty preference, and current CEFR level as system-estimated.

Prompt-editing fields, admin-only profile names, roles, quotas, lifecycle state,
account details, raw submitted text, and any student-editable CEFR override are
excluded. Missing preferences create no fake defaults.

`LearningGoalContext` uses custom goal first, then selected goals, then legacy
goal fields, then career context. If none are present, it remains null and does
not default to workplace.

## Preference enforcement rules (Phase 10K-F)

- Vocabulary cadence picks gate on `WorkplaceSpecific` from the resolved goal context.
  Non-workplace students (Day-to-day, Travel, Social, etc.) receive `PhraseMatch`.
  Workplace students receive `GapFillWorkplacePhrase`.
- Lesson batch generation compact summary includes `preferredSessionDurationMinutes`
  as a hint to the AI planner. `SessionDurationTemplates` in `SessionGeneratorService`
  is the authoritative session length gate.
- AI evaluation prompts receive `learnerPreferences` and `learningGoalContext`
  variable slots. Current evaluation prompt templates do not yet reference these
  variables — a prompt-engineering pass is needed to activate them.

## Student navigation model

The student app has five top-level sections:

| Section | Route | Question answered |
|---|---|---|
| **Today** | `/dashboard` | What should I do now? |
| **Journey** | `/journey` (also `/my-path`) | Where am I in my course? |
| **Practice** | `/practice` | What can I practise freely? |
| **Progress** | `/progress` | How am I improving? |
| **Profile** | `/profile` | What are my settings? |

- The student-facing label for the home page is **Today**, not Dashboard. The route `/dashboard` is preserved.
- `/journey` and `/my-path` both load the Learning Journey page. `/my-path` is kept for backwards compatibility.
- **Practice Gym** (`/practice`) is the student-facing landing for classroom-style free practice by skill, vocabulary class, exercise type, and future live practice. It does not auto-start an activity on load.
- Vocabulary is accessible from Practice Gym and Progress â€” it is not a top-level nav item.
- Writing and Email are valid activity types within Practice Gym and lessons. The student product is not writing/email-first.

## Implemented activity types

| Type | Status |
|---|---|
| `WritingScenario` | âœ… implemented |
| `ListeningComprehension` | âœ… implemented (with TTS audio) |
| `VocabularyPractice` | âœ… implemented |
| `SpeakingRolePlay` | âœ… implemented (MVP â€” fake STT) |

All four activity types use the unified `/activity` path.
`/api/writing/*` endpoints have been removed. See `docs/decisions/activity-flow-migration.md`.

## Practice Gym - activated pattern cards

Skill cards call `GET /api/activity/practice-gym/next?skill=<skill>`. Exact
exercise type cards call `GET /api/activity/practice-gym/next?exerciseType=<key>`.
Both serve a ready pre-generated activity from the pool (`source: "pool"`) when
available, or fall back to on-demand generation (`source: "onDemandFallback"`)
and route to `/activity?activityId=<id>&returnTo=/practice`.

`GET /api/activity/next` still accepts canonical `?exerciseType=<key>` plus legacy `?pattern=<key>` and `?type=` query parameters, unchanged, as the underlying fallback/compatibility path.

| Practice Gym card | Selection | Status |
|---|---|---|
| Vocabulary class | `/activity?exerciseType=phrase_match&returnTo=/practice` (module link, unaffected) | functional word-card lesson + matching practice |
| Listening | pool-aware skill selection | functional |
| Reading | pool-aware skill selection (`reading_multiple_choice_single`, `reading_multiple_choice_multi`, `reading_fill_in_blanks`, `reorder_paragraphs`) | functional |
| Writing | pool-aware skill selection | functional |
| Speaking | pool-aware skill selection | functional recorded prompt, no pronunciation claim |
| Matching | `/activity?exerciseType=phrase_match&returnTo=/practice` (module link, unaffected) | functional |
| Fill in the blanks | `/activity?exerciseType=gap_fill_workplace_phrase&returnTo=/practice` (module link, unaffected) | functional |
| Email | `/activity?exerciseType=email_reply&returnTo=/practice` (module link, unaffected) | functional |
| Workplace Chat | `/activity?exerciseType=teams_chat_simulation&returnTo=/practice` (module link, unaffected) | functional |
| Multiple choice | covered by Reading (`reading_multiple_choice_single` single, `reading_multiple_choice_multi` multi) | functional |
| Sentence transformation | - | Coming soon |
| Error correction | - | Coming soon |
| Word formation | - | Coming soon |
| Unscrambling | - | Coming soon |
| AI role play | - | Coming soon, live AI |
| Pronunciation | - | Coming soon, no STT/scoring support |


Practice Gym skill cards now use the ExerciseType registry. A skill card no
longer means one fixed activity type. The selected skill resolves to an enabled,
ready, generation-eligible, Practice Gym-supported exercise type with the same
`primarySkill`, then routes through canonical `exerciseType=<key>`. If no
eligible row exists, the frontend shows a safe unavailable message and does not
start broken generation. Planned future exercise format rows remain blocked. This is not the
final Practice Gym pre-generation pool; the future pool should reuse the same
registry selection rules.

All pattern-keyed activities go through `PatternEvaluationRouter`. Progress updates only after a submitted attempt. Returning from any pattern card goes back to `/practice` via `returnTo`. Ready Practice Gym cache entries are consumed before on-demand AI generation.

## Real TTS via Admin AI Config (complete â€” 2026-06-11)

`ListeningAudioService` and `PlacementAudioService` now resolve TTS provider at request time via `TtsProviderResolver`:

- `AiProviderConfig` rows `tts.listening` and `tts.placement` control which TTS service runs
- Default seed: `provider=fake, model=fake, voice=fake` â†’ silent WAV (tests never need `OPENAI_API_KEY`)
- Admin can switch to real TTS providers in Admin AI Config UI:
  - OpenAI: `provider=openai`, model `tts-1` or `tts-1-hd`, voice such as `onyx`
  - Gemini: `provider=gemini`, model must be a Gemini TTS model such as `gemini-2.5-flash-preview-tts`, voice such as `Kore`
  - Qwen: `provider=qwen`, model `cosyvoice-v2`, voice such as `longxiaochun_v2`
- TTS category saves reject non-TTS models. Existing Gemini TTS configs with a normal text model are defensively routed to the default Gemini TTS model by `GeminiTextToSpeechService`.
- `OpenAiTextToSpeechService` calls `POST /v1/audio/speech`; returns `audio/mpeg`; never throws
- `GeminiTextToSpeechService` calls the Gemini `generateContent` TTS path with `responseModalities=["AUDIO"]`, `speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName`, and returns `audio/wav`; never throws
- Activity audio endpoints remain JWT-protected. Angular fetches listening audio through `HttpClient` and converts it to a temporary `blob:` URL before rendering `<audio>`, so browser media requests do not hit `/api/activity/{id}/audio` anonymously.
- `PlacementAudioService` checks both `.wav` and `.mp3` on disk (backward compat with pre-existing files)
- T35 migration adds nullable `voice_name varchar(100)` to `ai_provider_configs`

## Onboarding experience step (complete â€” 2026-06-11)

A new step-5 collects professional context before placement:

- `PATCH /api/onboarding/experience` â€” sets `ProfessionalExperienceLevel`, `RoleFamiliarity`, computes `WorkplaceSeniority`
- Uses `StudentProfile.SetExperienceContext()` â€” bypasses onboarding state machine; can be called at any stage
- Angular: `step5-experience` component inserted between step-4 and `/placement`
- Step-4 now shows "Step 4 of 5"; navigates to `/onboarding/step-5` on finish
- Non-blocking: API failure still navigates to `/placement`; "Skip for now" skips without calling API
- Existing completed students not broken â€” endpoint accepts any auth token regardless of onboarding state

## Onboarding v2 foundation (complete — 2026-06-17, Phase 10I)

A configurable multi-step onboarding system (v2) runs in parallel with the existing v1 state machine. Existing students and v1 code are untouched.

### New API endpoints

- `GET /api/onboarding` — returns `OnboardingV2StatusDto`: current step, completed steps, percentage, preliminary CEFR level. Lazy-creates a `StudentOnboardingProgress` record on first call. Students who completed v1 onboarding are auto-marked complete.
- `POST /api/onboarding/steps/{stepKey}` — submits an answer for one step. Validates answer against step type (max length, valid option keys, max selections). Applies typed `OnboardingAnswerMapping` to `StudentProfile.UpdateLearningPreferences()`. Idempotent — upserts `StudentOnboardingResponse`.
- `POST /api/onboarding/complete` — validates all SystemRequired+enabled steps are done, scores assessment answers against server-side metadata, stores `PreliminaryCefrLevel` on progress, transitions `LifecycleStage` → `PlacementRequired`. Does **not** overwrite a real `CefrLevel` from PlacementAssessment.
- `GET /api/admin/onboarding/flow` (Admin role) — read-only view of the active `OnboardingFlowDefinition` including steps and answer mappings. Never exposes `AssessmentMetadataJson`, correct answers, or scoring weights.

### Architecture decisions

- v2 is parallel — v1 `OnboardingStatus`/`OnboardingStep` fields on `StudentProfile` remain as legacy compatibility.
- Single active flow enforced by PostgreSQL partial unique index (`WHERE is_active = true`).
- Flow versions are immutable once students have progress; admin edits must create a new version.
- `PreliminaryCefrLevel` stored on `StudentOnboardingProgress` only — never overwrites `StudentProfile.CefrLevel` unless it is null.
- `AssessmentMetadataJson` (correct answers, scoring weights) is server-side only — never returned to student or admin APIs.
- Percentage counts SystemRequired+IsEnabled steps only.
- Post-onboarding lifecycle → `PlacementRequired` (no “OnboardingComplete” stage exists).
- Unique `(progress_id, step_key)` constraint on `StudentOnboardingResponse`.

### Angular route

`/onboarding/v2` — standalone shell component with 11 step renderers (Welcome, PreferredName, SupportLanguage, LearningGoals, FocusAreas, DifficultyPreference, SingleChoice, MultipleChoice, FreeText, AssessmentQuestion, Summary).

### Known limitations

- No admin visual flow builder — flow is seeded via `OnboardingFlowSeeder`.
- Preliminary CEFR is a simple weight-band calculation, not a full adaptive placement engine.
- No curriculum routing or readiness pool based on v2 outcome.
- No Playwright E2E spec for v2 flow (no test user seeded with v2 progress).

### Migration

T47_OnboardingV2 — adds `onboarding_flow_definitions`, `onboarding_step_definitions`, `student_onboarding_progress`, `student_onboarding_responses`.

## Test suite baseline (as of tts-placement-today-sprint — 2026-06-11)

```
dotnet test:     873 passed (451 unit + 422 integration)
npm run build:   passed
Playwright:      175 passed (167 existing + 8 new onboarding step-5 tests)
```

## Admin capabilities

- Create students with temporary passwords
- Configure AI providers, model assignments, and prompt templates via Admin UI
- AI provider credentials stored securely in DB (never returned to client)
- AI usage logs accessible
- Student list is the admin entry point for create/edit/archive student management
- Create student returns to Students with a toast after success
- Archive uses `StudentLifecycleStage.Archived`, hides archived students by default, and disables sign-in
- AI Config shows category-level provider/model routing: Default LLM, Content Generation, Evaluation & Feedback, Memory & Learning Path, Listening TTS, and Placement TTS
- Integrations can trigger lesson generation, inspect recent generation batches, view ready lesson buffers, retry failed/partial batches, and cancel queued/running batches stuck from background generation failures
- Background lesson generation now materializes AI lesson plans into ready `LearningSession` rows instead of failing on generated `GenerationJobItem` tracking state
- Practice Gym background caching queues and materializes ready pattern-keyed activities for eligible active students
- Admin shell header is avatar-only; user email, role, profile placeholder, and sign out live in the avatar flyout menu
- Curriculum is hidden from admin navigation while its future purpose is redefined

## Placement Assessment â€” current state

Placement Assessment MVP is implemented:
- 6-section structured assessment (`PlacementAssessment`, `PlacementSection` entities)
- AI evaluation â†’ `PlacementResult` as CEFR source of truth
- Listening section uses **server-side TTS audio** (`PlacementAudioService`), not browser SpeechSynthesis
- `GET /api/placement/audio/{assessmentId}/listening` streams authenticated audio
- Frontend shows native `<audio controls>` when server audio is available; graceful fallback if not
- Transcript hidden by default behind "Show transcript"

## LearningSession data layer (Phase 1 complete â€” 2026-06-10)

- `LearningSession` and `SessionExercise` domain entities implemented
- `SessionStatus` and `ExerciseStatus` enums added
- EF configurations and migration T32 applied (`learning_sessions`, `session_exercises` tables)
- `LinguaCoachDbContext` updated with `LearningSessions` and `SessionExercises` DbSets
- 52 new tests added (284 unit, 247 integration â€” 531 total)

## LearningSession generator (Phase 2 complete â€” 2026-06-10)

- `ExerciseKind` enum added (`VocabularyWarmup`, `ContextInput`, `ListeningInput`, `ReadingInput`, `WritingTask`, `SpeakingTask`, `Review`)
- `ISessionGeneratorService` / `SessionGeneratorService` implemented
- Duration templates: 10 min (3 steps), 15 min (4 steps), 20 min (4 steps), 30 min (5 steps)
- Weak-skill substitution: Speaking weak â†’ SpeakingTask promoted; Listening weak â†’ ListeningInput enforced
- Idempotent: calling twice on the same day returns the same session
- Module progression: advances to next module after 5 completed sessions
- 65 new tests added in Phase 2 (609 total: 328 unit, 281 integration)

## LearningSession backend endpoints (Phase 3 complete â€” 2026-06-10)

- `SessionsController` at `src/LinguaCoach.Api/Controllers/SessionsController.cs`
- Endpoints: `GET /today`, `GET /{id}`, `POST /{id}/start`, `POST /{id}/complete`, `POST /{id}/exercises/{eid}/complete`, `GET /{id}/reflection` (501 stub)
- `SessionQueryHandler` and `SessionLifecycleHandler` in `LinguaCoach.Infrastructure/Sessions/`
- Lifecycle transitions: `CourseReady` â†’ `InLesson` (start), `InLesson` â†’ `ActiveLearning` (complete)
- All operations idempotent; ownership verified on every request
- 27 new integration tests added in Phase 3 (629 total: 328 unit, 301 integration)

## LearningSession frontend (Phase 4 complete â€” 2026-06-10)

- Today's Lesson card on dashboard â€” visible for `CourseReady`, `InLesson`, `ActiveLearning` lifecycle stages
  - Shows title, duration, skill focus, step count, status badge
  - Button label adapts: "Start today's lesson" / "Resume lesson" / "Review today's lesson"
  - Practice Gym remains secondary but visible
- `LessonComponent` at `/lesson/:sessionId` â€” Angular standalone component
  - Session detail loaded from `GET /api/sessions/{id}`
  - Ordered exercise steps, progress bar, per-step panel with instructions
  - Prepared buffered steps open directly; unprepared old-session steps show an explicit load action
  - Start, complete exercise, complete lesson flows fully wired
  - Completion summary shown on lesson complete
- `SessionService` + TypeScript models added to frontend core
- 14 new Playwright e2e tests â€” 81/81 pass total (no regressions)

## Exercise activity wiring (Phase 5A complete â€” 2026-06-10)

- `POST /api/sessions/{sessionId}/exercises/{exerciseId}/prepare` endpoint added
- Idempotent: calling twice returns the same `LearningActivity`
- ExerciseKind â†’ ActivityType deterministic mapping: VocabularyWarmupâ†’VocabularyPractice, ContextInputâ†’WritingScenario, ListeningInputâ†’ListeningComprehension, ReadingInputâ†’ReadingTask, WritingTaskâ†’WritingScenario, SpeakingTaskâ†’SpeakingRolePlay
- Review step returns a lightweight reflection placeholder (`isReview: true`), no AI generation
- VocabularyPractice and `ReadingTask` (not yet in `IAiActivityGenerator`) use `SystemFallback` placeholders
- 16 new integration + unit tests; 645 total (328 unit + 317 integration)

## Exercise activity wiring â€” frontend (Phase 5B complete â€” 2026-06-10)

- `LessonComponent` now calls `POST /api/sessions/{id}/exercises/{eid}/prepare` when student opens an exercise
- "Open activity" button navigates to `/activity?activityId=<id>&returnTo=/lesson/<sessionId>`
- `ActivityLessonComponent` supports `?activityId=<id>` (loads specific prepared activity) and `?returnTo=<path>`
- Review steps show a reflection prompt + "Mark complete" â€” no activity generated
- Server-assigned `learningActivityId` (persists across refresh) skips re-prepare
- `GET /api/activity/{id}` backend endpoint added
- 8 new Playwright tests; 90/90 pass

## Exercise Pattern Engine (complete â€” 2026-06-10)

- `exercise_patterns` table is seeded with the 8 MVP patterns.
- `LearningActivity.ExercisePatternKey` stores the durable pattern link.
- Pattern-aware prepare/generation sets `exercisePatternKey` and returns `interactionMode` on `ActivityDto`.
- Pattern-keyed activity responses include bounded `contentJson` for frontend renderers; legacy listening activities do not expose raw answer-bearing JSON before submission.
- `ActivityLessonComponent` now routes pattern-keyed activities through `ExerciseRendererComponent`.
- MVP renderers are wired: ReadOnly, FreeTextEntry, MatchingPairs, GapFill, AudioAndFreeText, AudioAndGapFill, ChatReply, EmailReply.
- All 7 active renderers (excluding ReadOnly) follow a Lesson â†’ Practice â†’ Evaluate structure: a "Goal" element (`learningGoal`) shown via `ChatReplyComponent`'s own goal display, `EmailReplyComponent`/`FreeTextEntryComponent`'s `coachNote`, or the shared `ExerciseLessonIntroComponent` (GapFill, MatchingPairs, AudioAndFreeText, AudioAndGapFill).
- Frontend renderer coverage added; full Playwright suite passes 97/97.
- Backend baseline: 762 tests pass (380 unit + 382 integration).
- `npm run build` passes; known non-blocking Angular warnings remain for admin CSS budgets and skipped selectors.

## Pattern Evaluation Engine (complete â€” 2026-06-10)

All 7 phases complete. `MarkingMode` is now first-class in the evaluation flow.

- **Evaluators**: `ExactMatchEvaluator` (gap_fill, listen_and_gap_fill), `KeyedSelectionEvaluator` (phrase_match), `NoMarkingEvaluator` (lesson_reflection), `AiStructuredEvaluator` (listen_and_answer, email_reply, teams_chat_simulation), `AiOpenEndedEvaluator` (spoken_response_from_prompt)
- **Router**: `IPatternEvaluationRouter` dispatches by `MarkingMode`; wired into `ActivitySubmitHandler`
- **Persistence**: `ActivityAttempt` stores structured `SubmittedAnswerJson`, `EvaluationResultJson`, `MaxScore`, `Percentage`, `Passed`, `Completed`, `MarkingMode`; EF migration T34 adds nullable columns only
- **Skill update**: `PatternSkillUpdateService` upserts `StudentSkillProfile` from `skillImpacts`; validates key allowlist, clamps delta, synthesises fallback from pattern key when impacts absent
- **Memory update**: compact memory packet (exercisePatternKey, score, coachSummary, top 3 corrections, top 5 impacts, top 3 signals) sent to `StudentMemoryService.UpdateMemoryAsync` â€” never includes raw submitted text; swallowed on failure
- **Frontend result UI**: `PatternEvaluationResultComponent` with 6 branches (MatchingPairs, GapFill, Chat/Email, ListenAndAnswer, SpokenResponse, ReadOnly); legacy non-pattern paths unchanged
- **Test counts**: 865 dotnet (451 unit + 414 integration) + 111 Playwright â€” all pass

## Student UX Alignment / Writing-Assumption Cleanup (complete â€” 2026-06-10)

All 7 phases complete. The student UI no longer implies SpeakPath is a writing/email-only app.

- **Nav**: student sidebar and mobile nav show Today, Journey, Practice, Progress, Profile. Dashboard label removed. Vocabulary removed from top-level nav.
- **Today** (`/dashboard`): motivational home page. Heading: "Today's Lesson". "Recommended next" section removed. Practice Gym grid moved off Today. Secondary links to `/journey` and `/practice`.
- **Journey** (`/journey`, `/my-path`): page heading "Learning Journey". Memory fallback copy updated from "workplace writing" to "workplace English". "Continue practising" CTA replaced with safe CTAs to `/dashboard` and `/practice`.
- **Practice Gym** (`/practice`): MVP landing page. Functional cards: Vocabulary (â†’`/vocabulary`), Listening, Writing, Speaking (â†’`/activity?type=X`). Coming soon: Workplace Chat, Email, Gap Fill, Phrase Match, Pronunciation. Does not auto-start on load.
- **Fixture cleanup**: generic writing/email-only fixture copy in Playwright tests updated to mixed-skill workplace English. Valid WritingScenario and email_reply test coverage preserved. No seed data deleted.
- **Test counts**: 865 dotnet (unchanged) + 165 Playwright â€” all pass

## Known gaps / not yet built

- Session reflection (`GET /api/sessions/{id}/reflection` returns 501; needs AI prompt key `session_reflection`)
- `ActivityShellComponent` not yet embedded inline in lesson page (navigates away instead)
- No real STT provider (SpeakingRolePlay uses `FakeSpeechToTextService`)
- No email delivery for temp passwords (admin copies manually)
- No admin CRUD for career profiles / learning tracks (seed data only)
- No audio cleanup job (50-file soft ceiling in place as mitigation)
- Dynamic pattern selection (week skills â†’ pattern choice) not yet implemented

See `docs/backlog/deferred-work.md` for the full deferred work list.

## Next recommended work

1. **Dynamic Pattern Selection** â€” choose Today's Lesson patterns from weak skills, CEFR, duration, and repetition history.
2. **Practice Gym Expansion** â€” deep pattern/skill selection within Practice Gym (Workplace Chat, Email, Gap Fill, Phrase Match unlock; dynamic session template).
3. **Session Reflection AI** â€” evaluation outputs now stable; wire `session_reflection` prompt.

See `docs/sprints/current-sprint.md` for the active sprint scope.

## Exercise Type Catalog foundation (Phase 3A)

The platform now has a durable exercise type catalog for future generation control.
Skills and exercise types are separate: a module can target primary and secondary
skills, while its Practice stage uses a catalog `exerciseType`.

Admins can list and enable or disable exercise types from Admin Exercise Types.
Disable affects future Today and Practice Gym generation only. Existing activities,
attempts, and history remain readable.

Planned future exercise formats are visible in the catalog as planned entries.
They are not generation-eligible until implementation status becomes ready, even
if an admin enables them.

## Phase 3B ExerciseType routing foundation

The backend now has an `IExerciseTypeRegistry` backed by the persisted exercise type catalog. It resolves `exerciseType` keys to renderer, evaluator, generation prompt, legacy `ActivityType`, and `ExercisePatternKey` metadata.

`GET /api/activity/next?exerciseType=<key>` is supported for ready runnable types. Existing `/activity?type=...` and `/activity?pattern=...` links still work. Practice Gym now routes implemented cards with `exerciseType` where safe. Today session generation validates deterministic pattern keys through the registry before creating steps.

Planned future exercise formats remain visible in Admin. They are not generation-eligible or routable to student activity flows until implementation status is `ready`.

## SpeakingRolePlay staged migration (Phase 5 — 2026-06-15)

`SpeakingRolePlay` now generates and serves `module_stage_v1` staged content,
matching the pattern established by `WritingScenario` and `ListeningComprehension`.

**What changed:**

- Generation prompt (`activity_generate_speaking_roleplay`) rewritten to produce
  `module_stage_v1` with `learnContent`, `practiceContent`, and `feedbackPlan`.
  Token budget increased: `maxInputTokens` 900 → 1600, `maxOutputTokens` 800 → 1200.
- `learnContent` explicitly forbids recording controls, microphone instructions,
  `startRecording`, and `stopRecording`.
- `practiceContent.exerciseData` requires: `role`, `partnerRole`, `situation`, `prompt`.
- `AiActivityGeneratorHandler` validates `SpeakingRolePlay` as staged (retry-once-then-fail).
- `ActivityGetHandler` detects legacy flat speaking JSON and adapts it to `legacy_adapted_v1`
  via `AdaptLegacySpeaking`. Old student data and history continue working unchanged.
- `SpeakingRolePlayEvaluator.ExtractExerciseDataJson` feeds only `practiceContent.exerciseData`
  into the evaluation prompt.
- Frontend `LegacySpeakingPresenter` returns `stagedLearning` block when `stageContent.learn`
  exists; falls back to legacy `speakingScenario` block for old rows.

**What was NOT changed:**

- No planned speaking format rows made runnable.
- No Practice Gym pre-generation changes.
- No Today pre-generation changes.
- No MinIO / audio lifecycle changes.
- No new planned future exercise renderer or evaluator.
- `/activity` endpoint and old compatibility params remain.

**Remaining staged migrations:** pattern-backed activities.

## Phase 6 — VocabularyPractice staged migration, completed

`VocabularyPractice` now uses `module_stage_v1` for newly generated deterministic vocabulary activities. The migration keeps the existing seeded vocabulary source. It does not add broad AI vocabulary generation.

The staged vocabulary module has exactly three pages: Learn, Practice, and Feedback. Learn teaches vocabulary meaning, usage, word form, example context, memory strategy, and common mistakes. Practice contains the fill-blank vocabulary task through `practiceContent.exerciseData`. Feedback uses the existing deterministic vocabulary evaluator with staged `practiceContent.exerciseData` support and legacy flat JSON fallback.

Completed staged migrations:

- `ListeningComprehension`
- `WritingScenario`
- `SpeakingRolePlay`
- `VocabularyPractice`

Remaining staged migrations are pattern-backed activities. Planned future exercise formats made runnable so far: `reading_multiple_choice_single` (Phase 8A), `reading_multiple_choice_multi` (Phase 8B), `reading_fill_in_blanks` (Phase 8C), `reorder_paragraphs` (Phase 8D), `reading_writing_fill_in_blanks` (Phase 8E), `summarize_written_text` (Phase 8F), `write_essay` (Phase 8G), `listening_multiple_choice_single` (Phase 8H — first runnable listening-primary format), `listening_multiple_choice_multi` (Phase 8I — second runnable listening-primary format), `listening_fill_in_blanks` (Phase 8J — third runnable listening-primary format, first runnable listening+writing format), `select_missing_word` (Phase 8K — fourth runnable listening-primary format), `highlight_correct_summary` (Phase 8L — fifth runnable listening-primary format, first runnable listening+reading format), `highlight_incorrect_words` (Phase 8M — sixth runnable listening-primary format, second runnable listening+reading format), `write_from_dictation` (Phase 8O — seventh runnable listening-primary format), and `summarize_spoken_text` (Phase 8Q — eighth runnable listening-primary format, first AI-evaluated listening+writing format). All reading-primary, writing, and listening planned future formats are now ready. All remaining planned future exercise formats are the speaking formats (`read_aloud`, `repeat_sentence`, `describe_image`, `respond_to_situation`, `retell_lecture`, `summarize_group_discussion`, `answer_short_question`), which remain planned and non-runnable. Today pre-generation remains a future phase. Phase 8P (2026-06-16) wired the audio lifecycle for all 9 listening pattern keys. `HandlePatternKeyedAsync` now calls `EnsureAudioAsync` after creating pattern-keyed listening activities. `ActivityDto` gains an `AudioStatus` string field (`"ready"` / `"pending"` / `"unavailable"`). A shared `app-audio-player` Angular component was created and all 5 listening renderer HTML templates now use it instead of inline `<audio>` tags. The exercise-renderer getters for `listeningFillInBlanks`, `highlightCorrectSummary`, and `highlightIncorrectWords` now fall back to `activity.audioUrl` from the API when `ed['audioUrl']` is absent from the content JSON. Audio is now generated on first fetch for all listening patterns; `audioUrl` will be non-null when TTS succeeds. Phase 8Q (2026-06-16) added `summarize_spoken_text` to `ListeningAudioService.ListeningPatternKeys` (now 10 keys) so it reuses the same shared audio lifecycle and `app-audio-player`. Its evaluation reuses the existing `AiStructuredEvaluator` AI path (same as `summarize_written_text` / `write_essay`); `learnContent` and the expected-answer `keyPoints` are never sent to the AI before submission.

Phase 8N (2026-06-16) added configurable practice item counts as a foundation (not a new format). Every `ExerciseTypeDefinition` now carries `MinItemsPerPractice`/`DefaultItemsPerPractice`/`MaxItemsPerPractice` and `MinOptionsPerItem`/`DefaultOptionsPerItem`/`MaxOptionsPerItem`, seeded per type, editable in the admin exercise-types page (with inline `min <= default <= max` and non-negative validation) and via admin PATCH. Counts feed generation prompt context and optional validator count enforcement. Counts are configuration only and never change readiness; no format was made runnable. See [practice-item-sets.md](../architecture/practice-item-sets.md).

## Phase 10O — Practice Gym Suggested Practice & Pool Serving, completed (2026-06-18)

Phase 10O connects the readiness pool to the student-facing Practice Gym. The pool built in 10M/10N is now surfaced as personalised suggestion cards via a new student API.

### New student-facing API endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/practice-gym/suggestions` | Returns SuggestedItems, ContinueItems, ReviewItems from the readiness pool |
| POST | `/api/practice-gym/suggestions/{id}/start` | Reserves an item; returns LearningActivityId / LearningSessionId for navigation |
| POST | `/api/practice-gym/suggestions/{id}/complete` | Best-effort marks item consumed |

### Sections returned

- **SuggestedItems** — Ready items ranked by focus-area match → goal context match → pool priority → expiry → FIFO. Max 6.
- **ContinueItems** — Reserved (in-progress) items not past expiry. Max 3.
- **ReviewItems** — ReviewOnly status items or Ready+lower-level content with review/scaffold/remediation routing reason. Max 4.

### Labels / wording

Normal → "Recommended for your current goal" | Review → "Review" | Scaffold → "Step back to strengthen basics" | Remediation → "Targeted fix" | Fallback → "General practice".

### What is NOT done yet (after 10O-F)

- Existing `GET /api/activity/practice-gym/next` (by skill/exercise type) is unchanged.
- Session/SessionExercise completion paths do not yet wire consumption (linked via LearningActivityId only).

### Tests (10O only)
+14 unit tests, +10 integration tests. Total: 1774 passed, 0 failed.

---

## Phase 10O-F — Practice Gym UI Integration & Completion Consumption Wiring, completed (2026-06-18)

Phase 10O-F connects the 10O backend API to the Angular Practice Gym page and wires completed activities back to readiness pool consumption.

### Angular UI changes

- **`PracticeGymSuggestionsService`** — new Angular service: `getSuggestions()`, `startSuggestion(id)`, `completeSuggestion(id)`.
- **`PracticeGymComponent`** — extended: suggestion signals, `loadSuggestions()`, `startSuggestion()` with loading/disabled state, `routingLabel()` helper.
- **Practice Gym template** — new sections: Suggested for you, Continue practice, Review practice. Cards show title, skill, CEFR level, estimated duration, context tags, routing label. Empty/loading/error states present. Existing By skill and By exercise type sections preserved.
- Student-friendly routing labels: Normal → "Recommended for your current goal", Review → "Review", Scaffold → "Step back to strengthen basics", Remediation → "Targeted fix", Fallback → "General practice".
- Lower-level content labelled with muted chip. No silent downgrade.

### Backend consumption wiring (TODO-014 resolved)

- `ActivitySubmitHandler` — injected `IPracticeGymSuggestionService`. `TryConsumeReadinessItemAsync` called best-effort after all completion paths:
  - WritingScenario / AI-evaluated: always called after save.
  - VocabularyPractice: called after deterministic evaluation.
  - ListeningComprehension: called after deterministic evaluation.
  - Pattern evaluation: called only when `evalResult.Completed == true`.
- Lookup scoped to `studentProfileId + activityId + Reserved status`. Exception swallowed — completion response never blocked.
- Idempotent: `TryMarkConsumedAsync` no-ops on already-consumed items.

### Tests added

- 4 integration tests (`ReadinessConsumptionWiringTests`): completion marks consumed, idempotent, no-item path safe, consumed item absent from suggestions.
- 12 Angular unit tests in `practice-gym.component.spec.ts`: load, empty, error, section rendering, start navigation, labels, existing sections preserved.

### Total test counts

- Architecture: 3 passed
- Unit: 1174 passed
- Integration: 601 passed (was 597 before this phase)
- Angular: 272 passed (was 247 before this phase)
- Total backend: 1778 passed, 0 failed

### TODOs closed

- TODO-014 — TryMarkConsumedAsync wired into ActivitySubmitHandler. Done.
- TODO-015 — Angular Practice Gym suggestion UI implemented. Done.

---

## Phase 10N — Background Replenishment Pipeline, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No Angular source changed.

**What was added:**

- `IReadinessPoolReplenishmentService` / `ReadinessPoolReplenishmentService` — background engine that sweeps expired/reserved items, recovers orphaned generating items, retries failed items, and fills pool shortfalls for all active students.
- `ReadinessPoolReplenishmentOptions` — appsettings-bound configuration (target count, expiry days, retry delay, max items per run, review/scaffold flag).
- `PoolHealthSummary` — health snapshot DTO: ready count, in-flight count, shortfall, `needsReplenishment` flag.
- `ReadinessPoolReplenishmentJob` — Quartz job running every 20 minutes.
- `GET /api/admin/students/{studentId}/readiness-pool/health` — new read-only admin endpoint showing health for both TodayLesson and PracticeGym pools.

**Key rules preserved:**
- `general_english` is the fallback context; workplace is not default.
- B2 students cannot silently receive B1 content as Normal.
- `EnableReviewScaffoldGeneration=false` by default; review/scaffold requires explicit flag AND ledger weakness signals.
- Existing on-demand generation paths unchanged (pool serving deferred to Phase 10O).
- No admin write endpoints added.

**Tests:** 16 new unit + 11 new integration = +27. Total: 1750 passed, 0 failed.

See: `docs/reviews/2026-06-17-phase-10n-background-replenishment-pipeline-review.md`

---

## Phase 10L — CEFR-Aware Activity Routing, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No Angular source changed.

**What was added:**

- `ICurriculumRoutingService` / `CurriculumRoutingService` — pure application-layer routing policy.
  Selects suitable CEFR band and curriculum objectives before every AI generation call.
  Does not call AI. Does not modify student data. Always returns a recommendation.
- `CurriculumRoutingRequest` — input: student context, raw CEFR level, primary skill, source label, resolved goal context, learner preferences, `AllowReviewOrScaffold`.
- `CurriculumRoutingRecommendation` — output: `TargetCefrLevel` (normalized), `CurriculumObjectiveKey/Title`, `ContextTags`, `FocusTags`, `DifficultyBand`, `RoutingReason`, `IsLowerLevelContent`, `Explanation`, `RoutingContextSummary` (for AI prompt injection).
- `CurriculumRoutingRequestFactory` — builds request from `StudentProfile` + `ResolvedLearningGoalContext`.
- CEFR normalization: `B2+` → `B2`, `C1-` → `C1`, null/unknown → `A1`. Does not modify `StudentProfile.CefrLevel`.
- `RoutingReason` enum: `Normal`, `Review`, `Scaffold`, `Remediation`, `Fallback`.

**Routing wired into all 5 generation handlers:**
- `ActivityGetHandler.HandlePatternKeyedAsync` (on-demand + Practice Gym)
- `ExercisePrepareHandler` (Today's Lesson)
- `PracticeGymGenerationJob.MaterializeAsync` (background Practice Gym)
- `ActivityMaterializationJob.MaterializeExerciseAsync` (background lesson batch)
- `LessonBatchGenerationJob.BuildCompactSummaryAsync` (AI lesson planning summary)

**AI prompt integration:**
- `ActivityGenerationContext` extended with `RoutingContext`, `RoutingReason`, `IsReviewOrScaffold`.
- `DbPromptAiContextBuilder` appends routing context before "Return ONLY".
- `cefrLevel` passed to AI is now the routing-normalized level, not raw profile value.

**Core rules enforced:**
- Routing never silently lowers CEFR level. Lower-level content requires `AllowReviewOrScaffold=true` and produces `RoutingReason.Review` + `IsLowerLevelContent=true`.
- Routing never defaults to workplace context. Non-workplace profiles always get `general_english` or goal-specific tags.
- DifficultyPreference maps to DifficultyBand: Gentle→1, Balanced→2, Challenging→4.

**What is NOT implemented (deferred to 10M+):**
`AllowReviewOrScaffold=true` in any handler (built but always false — enablement needs adaptive/ledger signals), session length → candidate count, CEFR-aware format matrix, readiness pools, background replenishment, Practice Gym UI redesign, admin write UI, `StudentProfile.CefrLevel` migration.

**Tests:** 16 new unit tests + 7 new integration tests. Total: 1692 passed (was 1656).

See: `docs/reviews/2026-06-17-phase-10l-cefr-aware-activity-routing-review.md`
See: `docs/architecture/curriculum-routing.md`

---

## Phase 10K — Curriculum Boundary / Level Syllabus Foundation, completed (2026-06-17)

Backend-only phase. No learner-facing behaviour changed. No CEFR-aware routing implemented.

**What was added:**

- `CurriculumObjective` domain entity — scoped by CEFR level (A1–C2), primary skill, context tags, focus tags, prerequisite keys, recommended order, difficulty band (1-5), active/reviewable/exam-inspired flags.
- `CefrLevelConstants`, `CurriculumSkillConstants`, `CurriculumContextTagConstants` — canonical validated string sets. `workplace` is one context tag among 13; it is not the default for any objective.
- `CurriculumObjectiveSeeder` — 22 starter objectives across A1/A2/B1/B2, all major skills, multiple learner contexts. **Seed-only-missing** (Phase 10Q): skips any key already in the DB so admin-edited objectives are never overwritten on startup.
- `ICurriculumSyllabusQuery` / `CurriculumSyllabusQueryService` — read-only query service: by CEFR, by CEFR+skill, by CEFR+context tag, by CEFR+focus area, prerequisites, and `GetCandidatesForStudent`. Candidates only — no activity selection.
- `IAdminCurriculumSyllabusQuery` — admin extension returning active and inactive objectives with optional filters (Phase 10Q).
- `CurriculumContextMapper` — maps `ResolvedLearningGoalContext` to curriculum context tags. Null-safe; fallback is `general_english`. Non-workplace profiles never default to `workplace`.
- `CurriculumObjective.ExamplePrompts` / `AdminUpdatedAt` — Phase 10Q audit fields. `AdminUpdate()` sets `AdminUpdatedAt`; seeder uses `UpdateDetails()` which does not.
- `ICurriculumObjectiveWriteService` / `CurriculumObjectiveWriteService` — full admin CRUD with validation (Phase 10Q): slug key, CEFR, skill, context tag, difficulty band 1–5, self-prereq, dangling prereq. `PreviewRoutingAsync` is read-only — no student state mutation.
- Admin API endpoints (Phase 10Q): `GET /objectives`, `GET /objectives/{key}`, `GET /taxonomy`, `POST /objectives`, `PUT /objectives/{key}`, `POST /objectives/{key}/activate`, `POST /objectives/{key}/deactivate`, `POST /routing-preview`.
- Angular `AdminCurriculumComponent` — list with filters, create/edit form, non-mutating routing preview panel (Phase 10Q).
- Migrations: `T50_CurriculumSyllabusFoundation`, `T52_CurriculumObjectiveAdminFields`.

**What is NOT implemented (deferred):**

Exercise format locking by level, `StudentProfile.CefrLevel` type migration.

**TODOS added:** See `TODOS.md` — TODO-001 (plus-levels), TODO-002 (StudentProfile.CefrLevel migration).

---

## Phase 10J-F — Student App Design System & Responsive UI Foundation, completed (2026-06-17)

Frontend-only phase. No product behaviour, API contracts, or backend logic changed.

**Design tokens extended (`styles.css`):**
- `--sp-brand` (solid brand colour, `#5B4BE8`), `--sp-r-md`, `--sp-nav-h`, `--sp-sidebar-w`, `--sp-sidebar-w-collapsed`, `--sp-content-max`, `--sp-content-max-desktop`, z-index layer tokens added to `:root`.
- `sp-card-hover` utility class added (transition, hover lift, active scale).
- `sp-pref-chip` / `sp-pref-chip--on` added for all preference chip toggles.
- Duplicate `sp-bottomnav` / `sp-navbtn` removed from global CSS (canonical definition in `student-app-layout.component.css`).

**Profile page:**
- All chip buttons (learning goals, focus areas, session length, difficulty) now use `sp-pref-chip--on` CSS class binding instead of inline `chipStyle()` method.
- `aria-pressed` attribute added to all chip buttons. `data-testid` added per chip.
- `focus-visible` keyboard ring included in chip CSS.
- `chipStyle()` method removed.

**Progress component:**
- All hardcoded hex colors replaced with design tokens (`--sp-success`, `--sp-warn`, `--sp-speaking`, `--sp-writing-ink`, `--sp-success-soft`, `--sp-warn-soft`, `--sp-canvas2`, `--sp-muted`).

**Practice Gym CSS:**
- `var(--sp-primary)` references (non-existent token) replaced with `var(--sp-brand)`.

**Shared student UI components (`src/app/design-system/student/`):**
- `StudentChipComponent` (`sp-chip`) — reusable toggle chip.
- `StudentBadgeComponent` (`sp-badge`) — reusable badge with variant input.

**Tests:** Angular 261 passed. Playwright 187 passed (12 new in `e2e/design-system-10jf.spec.ts`). Backend 1565 passed.

## Phase 10J — Learning Goal Context Resolver, completed (2026-06-17)

`ILearningGoalContextResolver` / `LearningGoalContextResolver` now provides a single consistent priority chain for resolving learning goal context from any `StudentProfile`. All 7 generation and ledger call sites use it. `LearnerPreferenceContextFormatter.BuildLearningGoalContext()` is kept but no longer called externally. Generic fallback is `"general English communication"` — never workplace-biased. `WorkplaceSpecific` flag is derived from keyword detection, not assumed. `LegacyFallbackUsed` flag enables future migration tracking.

## Phase 10X-J — Admin Wrapper Variant API, completed (2026-06-19)

All `sp-admin-*` wrapper components now expose typed variant/size/density/layout inputs. Feature pages request common TailAdmin variations through parameters — not inline class strings. TailAdmin class complexity stays inside wrappers.

**APIs added:** `appearance` on button, `dot`/`purple` on badge, `variant`/`hover`/`loading` on card, `size`/`loading` on stat-card, `variant`/`density`/`selectable`/`stickyHeader` on table, `layout`/`density` on filter-bar and form-field, `size`/`state`/`fullWidth` on input/select/textarea, `size`/`variant`/`showCloseButton` on modal, `side`/`size`/`closeOnBackdrop` on drawer.

**Backward compat:** `variant="ghost"` on button still works (alias to `appearance="ghost" variant="neutral"`). Modal `maxWidth` still works. Existing page code unchanged except two proof-usage calls.

**Tests:** 439 Angular unit tests pass. .NET 1885 pass. Angular build clean.

**Known gaps (tracked):** `sp-admin-input-number`, `sp-admin-select-object`, dashboard mini-table, breadcrumb wrapper.

## Phase 10Students-F-C — Targeted Lifecycle Controls, completed (2026-06-19)

Admin can now Pause, Unpause, and Reactivate students from the student detail page. Each action has a guarded server-side transition, audit log entry, and inline confirm modal. Reactivate reverses archive (sets EmailConfirmed=true, stage=OnboardingRequired). Pause blocks active students. Unpause restores paused students to OnboardingRequired. No database migration required.
