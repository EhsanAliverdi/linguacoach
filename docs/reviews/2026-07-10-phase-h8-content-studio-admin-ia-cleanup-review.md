---
title: Phase H8 — Content Studio/Admin IA Cleanup and Removal Readiness — Implementation Review
date: 2026-07-10
related: H-track (docs/architecture/product-model-realignment-h0.md), Plan-Sync-After-H7 (docs/reviews/2026-07-09-plan-sync-after-h7-legacy-bank-removal-strategy.md)
status: complete
---

# Phase H8 — Content Studio/Admin IA Cleanup and Removal Readiness — Implementation Review

**Date:** 2026-07-10
**Related sprint/feature:** Phase H8, part of the H-track, following Plan-Sync-After-H7's legacy
bank removal strategy. Frontend/docs-only admin-IA cleanup phase — **not** the destructive
backend/table cleanup phase (that's H9).

## Step 0 — admin IA and route audit

Audited `app.routes.ts` (all `/admin/*` routes), `admin-app-layout.component.html` (sidebar nav,
both mobile drawer and desktop, which were near-duplicate markup blocks), and the page-level
header/subtitle/empty-state text of every Content Studio-adjacent admin page.

| Surface | Classification | Action |
|---|---|---|
| `/admin/resource-banks/vocabulary`, `-grammar`, `-reading-references`, `-reading-passages` nav entries | Remove in H8 | Removed from both the desktop sidebar and mobile drawer "Content Banks" list. Routes/components themselves untouched — still reachable by direct URL for deep-link compatibility, browsing, and legacy bookmarks (confirmed via `app.routes.ts`, unmodified). |
| `/admin/content/import`, `/admin/resource-bank`, `/admin/learn-items`, `/admin/activities`, `/admin/modules` nav entries | Keep, promote | Regrouped into a new, explicitly-labeled "Content Studio" nav section — the primary content-authoring flow, matching the phase brief's target sequence exactly. |
| `/admin/resource-sources`, `/admin/resource-import-runs`, `/admin/resource-candidates`, `/admin/activity-templates`, `/admin/review-queue`, `/admin/placement-items`, `/admin/onboarding` nav entries | Keep because still linked to active workflow | Regrouped into a new "Content Ops" nav section, separated from Content Studio — these are staging/support pages (import pipeline, `ActivityTemplate` review, placement item bank, onboarding template bank), not duplicates of the unified Resource Bank, and Plan-Sync-After-H7 classified their backing entities "do not remove"/"keep." |
| `/admin/lessons` ("Today Delivery Health") | Keep temporarily, runtime/debug dependency | Already correctly labeled by Phase G1 as fallback/composition generation, not part of the Learn/Activity/Module pipeline. No change needed — left in its existing "Delivery" nav section, unrelated to Content Studio. |
| Admin student-detail Daily Lesson / Practice Gym module diagnostic cards (H6/H7) | Keep, already correct | These are embedded cards on the existing student-detail page, not standalone nav-level pages — nothing to reorganize; confirmed no separate "Daily Lesson module diagnostics" / "Practice Gym module diagnostics" top-level page exists to move. |
| `admin-resource-bank-unified` (Resource Bank) page header/actions | Keep, already accurate | "Generate Learn"/"Generate Activity"/"Generate Module" row actions are live (not `disabled` placeholders except while an individual row's generation is in-flight) and correctly worded — no stale "coming soon" language found. |
| Learn Items / Activities / Modules page subtitles | Rename/relabel | All three said "become the ... part of **future** Modules" / "will power **future** Daily Lessons and Practice Gym flows" — stale now that H5/H6/H7 shipped. Reworded to describe the current, live state (see below) and to be explicit that launching a scored attempt from a Module/Activity Definition is **not implemented yet** (H10), so no admin reads more capability into the page than exists. |
| Four typed resource-bank page subtitles | Rename/relabel | Added a one-sentence pointer to the unified Resource Bank page for day-to-day content work, so an admin who reaches one of these pages via a bookmark or deep link understands it's a secondary, browse-only view. |
| `admin-content-import` ("Import Content") page | Keep, already accurate | Subtitle and its one "coming soon" note (Listening/Speaking/Writing/Mixed import types — genuinely not built yet, tracked on the roadmap) are both accurate; not touched. |
| `admin-review-queue`, `admin-resource-candidates`, `admin-dashboard`, `admin-ai-config`, `admin-diagnostics`, `admin-notifications`, `admin-usage-analytics` "coming soon"/"not yet available" strings | Unknown scope, out of H8 | Grepped for stale placeholder language app-wide; all remaining hits describe genuinely unbuilt, unrelated features (SMS provider, background-job history API, a WebSocket event feed, AI-config auto-detection) — none imply Learn/Activity/Module/Today/Practice-Gym capability that doesn't exist. Left untouched; not H8's scope. |

**No dead frontend code was found to be safely removable.** Every component/route audited this
phase is still reachable and, per the table above, either promoted (Content Studio) or
intentionally retained (Content Ops, typed bank pages, Today Delivery Health). H8 therefore made
no route or component deletions — only navigation regrouping and copy edits.

## What changed

1. **`admin-app-layout.component.html`** (both the mobile drawer nav and the desktop sidebar nav,
   which are separate markup blocks in this component): split the single, 14-item "Content Banks"
   section into two sections:
   - **"Content Studio"** (5 items) — Import Content, Resource Bank, Learn Items, Activities,
     Modules — exactly the phase brief's target sequence, in that order.
   - **"Content Ops"** (7 items) — Resource sources, Resource import runs, Resource candidates,
     Activity templates, Review queue, Placement items, Onboarding — everything that isn't part
     of the primary authoring flow but is still a live, needed admin surface per Plan-Sync-
     After-H7's classification.
   - The four typed resource-bank nav entries (Vocabulary/Grammar/Reading reference/Reading
     passage bank) were **removed from both nav lists**. Their routes and components are
     untouched in `app.routes.ts` — still reachable by direct URL.
2. **`admin-learn-items.component.html`**, **`admin-activities.component.html`**,
   **`admin-modules.component.html`** — updated page-header subtitles to describe the current,
   live state (Modules exist and are consumed by Daily Lesson/Practice Gym today, not "in the
   future") and to explicitly flag that a Module/Activity Definition has no launch/attempt path
   yet (H10), so nothing in the admin UI overstates current capability.
3. **`admin-resource-bank-vocabulary/-grammar/-reading-references/-reading-passages.component.html`**
   — added a one-sentence pointer to the unified Resource Bank page in each subtitle, since these
   pages remain reachable by direct link but are no longer promoted in primary nav.
4. **`admin-app-layout.component.spec.ts`** — updated the existing nav-routes test (removed a
   stale required-route assertion for `/admin/resource-banks/reading-passages` that a Phase G1
   test had added when that route was *missing* from nav; it's now intentionally absent from nav
   again, for a different, documented reason) and added two new, cheap assertions: the Content
   Studio flow's five routes are present, and the four typed bank routes are **not** present in
   primary nav.

No backend file was touched. No route or component was deleted. No `app.routes.ts` change.

## Frontend build result

`npm run build -- --configuration production` — **no new TypeScript/Angular compile errors**.
Only the pre-existing, documented bundle-size budget failure (`bundle initial exceeded maximum
budget. Budget 1.00 MB was not met by 1.56 MB`), unrelated to this phase and present before H8.

**Karma unit tests could not be run as a full suite** — the shared test bundle fails to compile
because several pre-existing spec files (`dashboard.component.spec.ts`,
`practice-gym.component.spec.ts`, `activity-feedback-page.component.spec.ts`,
`activity-lesson-submission.component.spec.ts`, `activity-lesson-vocab.component.spec.ts`,
`presenters/test-helpers.ts`) construct fixture objects missing fields
(`TodaysSessionResponse.moduleSection`, `PracticeGymSuggestionsResponse.moduleSuggestions`,
`ActivityFeedbackDto.feedbackPolicy`) that were added as **required** properties in earlier
phases (H6/H7 and an earlier feedback-policy phase) without the corresponding spec fixtures being
updated. Confirmed via `git log` that none of these spec files were touched this session or in
H8's scope — this is pre-existing test debt, not something introduced by H8. Fixing it would mean
updating five unrelated spec files with no connection to Content Studio/admin-IA, well outside
H8's boundaries; flagged as a new follow-up TODO instead (`TODO-H8-2`) rather than silently
expanding scope.

## Backend validation

Not run — no backend file changed (`git status --short` before commit showed only files under
`src/LinguaCoach.Web/` and `docs/`/`TODOS.md`). Per the phase brief, full backend build/test is
only required if backend code changed unexpectedly; it did not.

## Decisions made

1. **"Content Studio" / "Content Ops" split, not a flatter reorg.** The phase brief's suggested
   structure (Content Studio: Import/Bank/Learn/Activities/Modules; optional Delivery/Diagnostics
   group) maps cleanly onto the existing nav's two natural clusters — the five-step authoring
   flow, and everything else that was previously lumped into one 14-item list. Kept "Delivery"
   (Today Delivery Health) as its own pre-existing section rather than merging it into "Content
   Ops," since it's operational/runtime, not content-authoring — consistent with the brief's own
   "avoid mixing old typed bank pages into the primary content authoring flow" framing extended
   one step further (avoid mixing runtime delivery health into content ops either).
2. **Typed bank pages: nav removal only, not deletion.** Exactly per Plan-Sync-After-H7's own
   audit finding — the redundant *navigation* was the only proven-safe H8 action; the routes,
   components, and backing tables (`CefrVocabularyEntry` etc.) all remain untouched. Removing the
   component files themselves would be premature without a dependency check for hard-coded deep
   links elsewhere in the app (none found this pass, but that's a narrower guarantee than "no code
   anywhere ever links here") — that level of certainty is H9's job, not H8's.
3. **No new "Daily Lesson module diagnostics" / "Practice Gym module diagnostics" nav items.**
   The phase brief's suggested Delivery/Diagnostics group example included these, but no such
   standalone admin pages exist — H6/H7 built their diagnostics as embedded cards on the existing
   student-detail page. Inventing new top-level pages to satisfy the example would be scope
   creep/a small redesign, explicitly out of bounds for H8 ("avoid broad layout redesign").
4. **Page-copy edits were scoped to the three pages with genuinely stale wording** (Learn Items,
   Activities, Modules — all said "future" for things that now exist) plus the four typed bank
   pages (added a pointer, since their role changed from "one of several equal options" to
   "secondary/browse-only"). Every other admin page's copy was checked and found already accurate
   — no changes made purely for consistency's sake where the existing wording wasn't actually
   wrong.

## Docs updated

`docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`,
`docs/handoffs/current-product-state.md`, `docs/backlog/product-backlog.md`,
`docs/architecture/product-model-realignment-h0.md`,
`docs/architecture/learning-activity-engine.md`,
`docs/architecture/english-resource-bank-import-platform.md`, `docs/architecture/README.md`,
`TODOS.md` (`TODO-H8-1` marked done; `TODO-H9-1`/`TODO-H10-1` left open; new `TODO-H8-2` added
for the pre-existing Karma spec-fixture debt), plus this review.

## Known limitations / retained legacy surfaces

- The four typed resource-bank admin pages, their routes, and their backing tables/APIs are all
  still present and functional — intentionally. H9 is the phase that would decide whether to
  remove them, after its own fresh safety audit.
- `ActivityTemplate`, `PracticeActivityCache`, `StudentActivityReadinessItem`, the
  `LearningActivity`/`LearningSession`/`SessionExercise` runtime, the Today and Practice Gym
  legacy AI-generation fallbacks, and the import-staging tables are all untouched, per the
  explicit H8 boundary list.
- `ActivityDefinition` still has no attempt/launch/scoring path — the Modules/Activities page
  subtitles now say so explicitly rather than implying otherwise. H10 is unstarted.
- The pre-existing Karma test-bundle compile failure (five unrelated spec files, `TODO-H8-2`)
  means the full frontend unit-test suite cannot currently run at all, not just the tests touched
  this phase. The production build (which doesn't compile specs) was used as the primary
  validation gate instead, per the phase brief's own fallback guidance.

## Final verdict

**Complete.** Admin Content Studio navigation is now clearly split into the primary authoring
flow (Content Studio: Import → Resource Bank → Learn Items → Activities → Modules) and secondary
support/staging tooling (Content Ops), with the four redundant typed bank pages removed from both
only where Plan-Sync-After-H7 proved it safe — navigation only, no route/table/API deletion. Page
copy on the three primary content-studio pages now accurately reflects that H5-H7 shipped (no
more "future Modules" language) while being explicit that Module/Activity launch-and-score is not
implemented yet. No backend file, migration, table, entity, or API was touched. No route or
component was deleted. `ActivityTemplate`, `PracticeActivityCache`,
`StudentActivityReadinessItem`, the runtime session entities, and both legacy fallback paths are
all untouched. Frontend production build has no new errors (only the pre-existing bundle-size
warning). Committed locally, not pushed, not deployed.

## Next recommended action

**H9 — Legacy Bank Structure Removal and Consolidation**, gated on its own fresh per-item safety
audit (as Plan-Sync-After-H7 specified), and **H10 — ActivityDefinition Runtime Launch Path /
Attempt Bridge**, which should be decided before H9 could ever remove `ActivityTemplate`.
Independently, `TODO-H8-2` (Karma spec-fixture debt) should be picked up whenever unit-test
coverage work is next prioritized — it blocks the whole frontend test suite, not just
Content-Studio-adjacent components.
