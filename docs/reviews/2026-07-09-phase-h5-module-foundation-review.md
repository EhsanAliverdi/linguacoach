---
title: Phase H5 — Module Foundation — Implementation Review
date: 2026-07-09
related: Phase H5 (Module Foundation), H-track (docs/architecture/product-model-realignment-h0.md)
status: complete
---

# Phase H5 — Module Foundation — Implementation Review

**Date:** 2026-07-09
**Related sprint/feature:** Phase H5 — Module Foundation, part of the H-track
(`Resource Bank Item → Learn Item/Activity Definition → Module Definition → Daily Lesson/Practice
Gym → Attempt → Feedback + Rating → Learner Memory`), following H3 (Learn Item Foundation) and H4
(Activity Foundation with Form.io).

## Files reviewed / audited before implementation

Step 0 audit (existing-code conflict check) covered:

- `src/LinguaCoach.Domain/Entities/LearningModule.cs` — existing per-student runtime entity (a
  thematic group of `LearningActivity` rows within a `LearningPath`, tracks its own
  `CompletedAt`/`IsCompleted`). Confirmed as the only existing "Module"-named entity in the domain
  layer.
- `src/LinguaCoach.Domain/Entities/LearnItem.cs`, `ActivityDefinition.cs` (H3/H4) — reused patterns
  for the review lifecycle (ctor → `PendingReview`, `UpdateDraft` blocked once `Approved`,
  `Approve`/`Reject`).
- `src/LinguaCoach.Domain/Enums/LearnItemResourceRole.cs`, `PublishedResourceType.cs` (H3) — reused
  directly for `ModuleDefinitionLearnItemLink.Role` and the resource-lookup helper
  (`LearnItemResourceLookup`, H3) rather than duplicating a resource-type parser.
- `src/LinguaCoach.Infrastructure/LearnItems/LearnItemResourceLookup.cs` (H3) — reused as-is by
  `ModuleGenerationService.HandleAsync(GenerateModuleFromResourceRequest)`.
- `src/LinguaCoach.Infrastructure/ResourceImport/ResourceBankQueryService.cs` (H1/H3/H4) —
  extended `WithLinkedCountsAsync` to also populate `LinkedModuleCount` via a two-hop join
  (resource → Learn Item/Activity → Module link tables).

**Naming-conflict finding:** `LearningModule` already exists as a runtime entity. `ModuleDefinition`
(the name suggested first in the phase brief) has no conflict anywhere in the codebase — chosen as
the final name, exactly mirroring H4's `ActivityDefinition`-vs-`LearningActivity`/`ActivityTemplate`
naming decision. Documented in `ModuleDefinition`'s own doc comment, `docs/architecture/
learning-activity-engine.md` (Phase H5 note), and `docs/architecture/product-model-realignment-h0.md`.

## Findings grouped by priority

**P0 (blocking) — none.** No existing entity/table/route collided with the new Module Definition
foundation; the additive migration applied cleanly.

**P1 (design decisions requiring an explicit call):**

1. *Approval requirement scope.* The phase brief's "Compatibility rules" section says Modules
   should "prefer" approved Learn Items/Activities but only explicitly requires it for the
   auto-discovery generation paths (from-resource/from-learn-item/from-activity). Decision: applied
   the `Approved`-required rule uniformly to **every** generation entry point, including
   `generate-from-items` (explicit id selection) — not just the "find compatible" ones. Manual
   `POST /api/admin/modules` (admin explicitly composing a draft) remains unrestricted, mirroring
   H4's manual-create flexibility. Rationale: a Module is the top of the content-studio hierarchy;
   letting a still-draft Learn Item/Activity silently compose into a generated Module would let an
   unreviewed defect propagate two layers up before anyone re-reviews it.
2. *Compatibility matching algorithm.* The brief lists CEFR/skill/subskill/context tags/focus
   tags/difficulty band/linked source resources/approval status as compatibility signals. Decision:
   implemented CEFR-level equality + skill-string equality only (both skipped when the anchor item
   has no value set), capped at 5 matches, for the from-learn-item/from-activity entry points — kept
   intentionally simple per the brief's own "do not overbuild a full lesson planner" instruction.
   Tracked as `TODO-H5-1` for future refinement (tag/resource-overlap scoring) once there's real
   admin usage to tune against.
3. *Deterministic vs AI generation.* No existing AI service in this codebase composes a lesson
   plan/module from other structured content (every AI generator audited in H3/H4 remains scoped to
   activity/exercise/learning-path/teaching-prose generation, none of them "assemble a module").
   Decision: deterministic composer only, `GenerationProvider = "Deterministic"`, consistent with
   H3/H4's precedent and the phase brief's explicit permission to skip AI "unless an existing safe
   service already fits perfectly" (none did).

**P2 (minor / documented limitations):** see `TODO-H5-1` in `TODOS.md` — id-typing UI instead of a
picker, single-Learn-Item + single-Activity per row-action generate call, simple CEFR+skill
compatibility matching.

## Decisions made

- Entity named `ModuleDefinition` (not bare "Module") — see naming-conflict finding above.
- Reused `AdminReviewStatus`, `LearnItemResourceRole` (for the Learn Item link's role), and the H3
  `LearnItemResourceLookup` helper rather than introducing parallel types.
- New `ModuleActivityRole` enum (`PrimaryPractice`/`SupportingPractice`/`Review`/`Extension`) — the
  brief's suggested roles for the Activity link, richer than the Learn Item link's Primary/
  Supporting because a Module's practice activities can play more distinct roles.
- Three new tables only: `module_definitions`, `module_definition_learn_item_links`,
  `module_definition_activity_links` — no FK from `module_definitions` to `learn_items`/
  `activity_definitions` tables directly (links carry the FK; mirrors H3/H4's soft-reference
  convention for cross-aggregate references).
- `EstimatedMinutes` on a generated Module = sum of its linked Activities' own `EstimatedMinutes`
  (when any are set), not editable input at generation time — deterministic, no admin override
  needed for v1.
- Feedback plan is module-level only (`{"completionMessage": ..., "note": ...}`), distinct from
  each linked Activity's own `FeedbackPlanJson` — no merging/inheritance logic in H5.

## AskUserQuestion decisions

None — the phase brief was fully self-contained; no ambiguity required a user clarification during
implementation.

## Implementation tasks produced

All completed in this phase (see commit for full file list):

1. Domain: `ModuleDefinition`, `ModuleDefinitionLearnItemLink`, `ModuleDefinitionActivityLink`
   entities; `ModuleSourceMode`, `ModuleActivityRole` enums.
2. Persistence: 3 EF configurations, `LinguaCoachDbContext` DbSets, migration
   `Phase_H5_AddModuleDefinitionFoundation`.
3. Application: `ModuleDefinitionContracts.cs`, `ModuleGenerationContracts.cs`.
4. Infrastructure: query/command handlers, `ModuleGenerationService` (4 generation interfaces),
   `ResourceBankQueryService.LinkedModuleCount` wiring, DI registration.
5. API: `AdminModuleDefinitionController` (`api/admin/modules`, 9 endpoints).
6. Angular: models, service, `/admin/modules` page (list/filter/drawer/approve-reject/generate
   modal), nav entry, "Generate Module" wired into Resource Bank/Learn Items/Activities pages.
7. Tests: 27 new unit tests, 11 new integration tests (38 total; 3,784 → 3,822).
8. Docs: this review, plus updates to road-map.md, current-sprint.md, current-product-state.md,
   product-backlog.md, product-model-realignment-h0.md, english-resource-bank-import-platform.md,
   learning-activity-engine.md, architecture/README.md, TODOS.md (`TODO-H5-1`).

## Risks or unresolved questions

- **PG-v2A/H6 sequencing** — not resolved by this phase, remains a future Plan-Sync checkpoint (per
  the phase brief's own framing, consistent with every prior H-phase handoff).
- Compatibility matching (CEFR+skill only) may surface too few or too many candidates as real
  content volume grows past the current internal seed packs — flagged in `TODO-H5-1`, not a defect,
  just an intentionally-simple v1 heuristic.
- No automated test exercises the Angular `admin-modules` component directly (consistent with H3/H4
  precedent — no lightweight frontend test pattern exists yet for these content-studio pages); the
  production build was used to confirm no new TS/Angular compile errors instead.

## Final verdict

**Complete and accepted.** All 23 acceptance criteria from the phase brief are met: entity/schema
exists and links to Learn Items and Activity Definitions; module-level feedback plan and full
metadata set are stored; review lifecycle works exactly like Learn Item/Activity; every generation
path composes existing content only (no cascade-generation, no Learn Item/Activity mutation, no
student assignment); admin API and page exist; Generate Module is enabled everywhere it's safe
(Resource Bank row, Learn Item, Activity) and returns a clear validation error everywhere it isn't
yet possible; Today/Practice Gym runtime and the readiness/delivery queue are untouched; the full
backend suite (3,822 tests) and the Angular production build both pass; committed locally, not
pushed, not deployed.

## Next recommended action

**Phase H6 — Daily Lesson Module Pipeline** (per the H-track), which will be the first phase to
actually consume `ModuleDefinition` at runtime — subject to the same PG-v2A/H6 sequencing
Plan-Sync checkpoint noted above.
