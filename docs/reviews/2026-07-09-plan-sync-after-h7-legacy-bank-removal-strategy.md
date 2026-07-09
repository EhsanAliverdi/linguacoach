---
title: Plan-Sync-After-H7 — Legacy Bank Removal Strategy
date: 2026-07-09
related: H-track (docs/architecture/product-model-realignment-h0.md), Phase G0 audit (docs/architecture/bank-first-admin-backend-surface-audit.md)
status: complete
---

# Plan-Sync-After-H7 — Legacy Bank Removal Strategy

**Date:** 2026-07-09
**Type:** docs-only planning phase. No application code, migrations, tables, entities, APIs, or
UI pages changed or removed in this phase.

## Where things stand after H7

The active bank-first content model is now fully built, end to end:

```
Resource Bank → Learn Items → Activity Definitions → Module Definitions
  → Daily Lesson Module Pipeline (H6) → Practice Gym Module Pipeline (H7)
```

All eight H-track phases so far are complete: H0 (Product Model Realignment, docs-only), H1
(Unified Resource Bank Admin Read Model), H2 (Import Content UX v1), H3 (Learn Item Foundation),
H4 (Activity Foundation with Form.io), H5 (Module Foundation), H6 (Daily Lesson Module Pipeline),
H7 (Practice Gym Module Pipeline).

**H6 and H7 are both additive and fallback-safe.** Neither replaced any existing runtime path —
they each attach an optional, try/catch-isolated module section onto an existing response
(`TodaysSessionResult.ModuleSection` for H6, `PracticeGymSuggestionsDto.ModuleSuggestions` for
H7). The pre-H-track Today and Practice Gym pipelines — bank-first selection with a legacy
free-form AI fallback — are completely unchanged and still the only thing that actually delivers
gradeable content to a student.

**No legacy cleanup has happened yet.** Every entity, service, job, and admin page audited below
that predates the H-track is still present, unmodified, and — in most cases — still load-bearing.

## The cleanup direction

The user has clarified: **old invalid bank/admin structures should be removed, not merely
hidden.** This plan-sync exists to turn that direction into a concrete, safety-first sequence
rather than a single undifferentiated "delete the old stuff" phase — because, as the audit below
shows, most of what looks old is still a live runtime dependency, not dead code.

This document is that sequence. It defines H8, H9, and H10 (scope only — none of them are
implemented here) and classifies every audited legacy structure by removal risk so those future
phases have a concrete, non-speculative starting point.

## Step 0 audit — legacy structure classification

Classification key: **Keep** (still valid long-term as-is) · **Keep temporarily** (real runtime
dependency, remove later once its replacement is proven) · **Replace** (new model already
supersedes it) · **Remove in H8** (safe UI/admin/code cleanup, no backend/data risk) · **Remove
in H9** (needs a backend/data migration or compatibility guard) · **Do not remove** (core runtime
infrastructure, still valid indefinitely) · **Unknown** (needs a deeper, code-level audit before
any decision).

| # | Structure | Where | Classification | Why |
|---|---|---|---|---|
| 1 | `CefrVocabularyEntry`, `CefrGrammarProfileEntry`, `CefrReadingReference`, `CefrReadingPassage` | `src/LinguaCoach.Domain/Entities/` | **Do not remove** | These *are* the Resource Bank Item concept from H0's model, not a legacy predecessor of it. Populated via `ResourceCandidatePublishService` + seed packs; read by `TodayBankResourceSelector` (Today), `ResourceBankQueryService` (admin), and now `LearnItemResourceLookup`/`LearnItemResourceLink`/`ActivityResourceLink` (H3/H4 traceability). The gap is admin presentation only (already addressed by H1's unified read model; physical consolidation is a deliberate H0 Option-B deferral, not a defect). |
| 2 | `ResourceImportRun`, `ResourceRawRecord`, `ResourceCandidate` | `src/LinguaCoach.Domain/Entities/` | **Do not remove** | The only staging pipeline by which any content — including H2's Import Content UX — becomes a published Resource Bank row. H2 builds on top of this pipeline, not around it. |
| 3 | `ResourceBankQueryService`, `UnifiedResourceBankItemDto` | `src/LinguaCoach.Infrastructure/`, `src/LinguaCoach.Application/` | **Keep** | The H1 unified admin read model — still the only aggregated view over the four typed tables; `LinkedLearnCount`/`LinkedActivityCount`/`LinkedModuleCount` are now real counts (H3-H5). No physical `ResourceBankItem` table exists yet (Option B, deliberate). |
| 4 | `ActivityTemplate` | `src/LinguaCoach.Domain/Entities/ActivityTemplate.cs` | **Do not remove** | Still actively queried by `PracticeGymGenerationJob` (`TemplateMigratedPatternKeys`, 8 of ~33 patterns) before it falls back to legacy free-form AI generation. H4's `ActivityDefinition` is an explicitly separate, parallel foundation — not built on top of `ActivityTemplate` and not yet wired to replace it (see H10 below). |
| 5 | `LearningActivity`, `LearningSession`, `SessionExercise`, `LearningModule` | `src/LinguaCoach.Domain/Entities/` | **Do not remove** | The live Today/Practice Gym runtime/delivery hierarchy. `SessionQueryHandler`/`ActivityMaterializationJob` still drive it unchanged; H6's module selection is a strictly additive, separately-try/catched attachment on top, never a replacement. `LearningModule` remains explicitly distinct from H5's `ModuleDefinition`. |
| 6 | `PracticeActivityCache` | `src/LinguaCoach.Domain/Entities/PracticeActivityCache.cs` | **Keep temporarily** | Populated by `PracticeGymBufferRefillJob`, consumed by `PracticeGymGenerationJob`/`IPracticeGymPoolService`. Its eventual retirement is tied to the separate PG-v2 track (skill-first Activity selector), not to H8/H9 — H7 explicitly left it untouched. |
| 7 | `StudentActivityReadinessItem` | `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs` | **Do not remove** | The live readiness/delivery queue — written by replenishment/generation jobs, read by `PracticeGymSuggestionService` and `AdminReadinessPoolController`. Phase G0 already classified this "kept, reframed only, never delete"; H6/H7 both explicitly preserved it. |
| 8 | `PracticeGymSuggestionService`, `PracticeGymPoolService` | `src/LinguaCoach.Infrastructure/PracticeGym/`, `PracticeGymPoolService.cs` | **Do not remove** | Both are still the live Practice Gym entry points (two parallel routes, per H7's own Step 0 audit: the newer suggestions API and the older `activity/practice-gym/next` API). H7 attaches its module suggestions additively without touching either. |
| 9 | `ActivityMaterializationJob`, `LessonBatchGenerationJob`, `PracticeGymGenerationJob` | `src/LinguaCoach.Infrastructure/Jobs/` (or Sessions/Activity as applicable) | **Do not remove** | The live background jobs powering Today and Practice Gym generation/materialization. Unmodified by H6/H7 by design. |
| 10 | Today fallback path (bank-first selector → legacy free-form `IAiActivityGenerator`) | `ActivityMaterializationJob` | **Keep temporarily** | A real, still-live legacy AI-generation fallback, not vestigial code — used whenever the bank-first selector can't find a match for a given exercise slot. Retirement is Phase F's job (per-pattern, only after each bank-first replacement is proven), explicitly out of scope for H8/H9. |
| 10b | Practice Gym fallback path (`ActivityTemplate` Form.io pilot → legacy free-form `IAiActivityGenerator`) | `PracticeGymGenerationJob` | **Keep temporarily** | Same reasoning as Today's fallback — still covers the ~25 of ~33 patterns not yet migrated to the Form.io template pilot. Retirement is Phase F's job. |
| 11 | Old typed admin resource pages (`admin-resource-bank-vocabulary`, `-grammar`, `-reading-references`, `-reading-passages`) | `src/LinguaCoach.Web/src/app/features/admin/` | **Remove in H8** | Still routed and still in the sidebar nav alongside the newer unified Resource Bank page (H1) — coexisting by design during the transition per H0 §4/§7. Safe to move under Advanced/Diagnostics or fully remove from primary nav in H8 (no backend/data change); the *pages/routes themselves* are functionally redundant with the unified view for browsing, but the underlying typed tables and their admin CRUD are still needed (see #1) — H8 removes the redundant *navigation surface*, not the tables or the CRUD capability itself. |
| 12 | `admin-lessons.component.ts` (legacy "generate lessons" admin page) and similar pre-H-track generation admin pages | `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/` | **Keep temporarily** | Already reframed by Phase G1 (2026-07-09) — relabeled "Today Delivery Health," manual generation is now explicitly labeled "AI fallback/composition," delivery-queue language throughout. The *backend* action it triggers still calls the same legacy `LessonBatchGenerationJob`/free-form-AI path from #10 — the underlying behavior is unreframed, only the label. A further backend route split (health vs. generation vs. buffer settings) remains explicitly deferred (G1 finding, "deferred to G2"), not H8/H9's job. |

**Overall finding:** nothing audited in this pass is currently a safe candidate for H9-style
data-migration removal. Every genuinely "old" thing that predates the H-track is still either
(a) core runtime infrastructure the current Today/Practice Gym pipelines actively depend on, or
(b) a legacy AI-generation fallback that is still the only thing covering content the bank-first/
module-first paths can't yet serve. The only concrete, low-risk H8 action identified is trimming
the redundant *navigation surface* for the four typed admin resource pages (#11) — not deleting
their underlying tables, CRUD APIs, or data.

## H8 — Content Studio/Admin IA Cleanup and Removal Readiness

**Not implemented in this phase.** Scope for a future phase:

- Remove or relocate admin nav entries that duplicate the unified Content Studio surface (Content
  Import → Resource Bank → Learn Items → Activities → Modules → Daily Lesson module diagnostics →
  Practice Gym module diagnostics) — primarily item #11 above (move the four typed resource-bank
  pages under Advanced/Diagnostics, or drop them from primary nav; keep the pages/routes/tables
  themselves reachable).
- Remove obsolete admin labels/copy that still describe the pre-H-track model, wherever found.
- Remove placeholder "coming soon" generation actions that have since been superseded by a real
  Learn/Activity/Module generation entry point (audit needed — several were closed out across
  H3-H5, e.g. Generate Learn/Activity/Module row actions going from "coming soon" to live).
- Remove UI components/routes only where the Step 0 audit (or a follow-up refresh of it) proves
  no active backend/runtime dependency remains.
- **Must not** remove: Today fallback, Practice Gym fallback (either entry point),
  `ActivityTemplate` (still needed for the live Form.io pilot), `PracticeActivityCache` (still
  needed by legacy Practice Gym), `StudentActivityReadinessItem`/the readiness queue,
  `LearningActivity`/`LearningSession`/`SessionExercise`/`LearningModule` (still the runtime
  Today/Practice Gym delivers through), or any import-staging table (`ResourceImportRun`/
  `ResourceRawRecord`/`ResourceCandidate`).
- **Must not** remove backend tables in H8 at all — H8 is UI/nav/admin-surface cleanup only,
  gated on "no active backend/runtime dependency," never a data change.

## H9 — Legacy Bank Structure Removal and Consolidation

**Not implemented in this phase.** First genuinely destructive cleanup phase — sequenced after H8
and only once a concrete safety audit (dependency audit, data audit, migration strategy,
compatibility strategy, rollback/backup notes, test coverage plan) has been produced for each
candidate. Per this plan-sync's own Step 0 audit, **no current structure is yet a proven-safe H9
candidate** — H9's first real job is re-running that audit against whatever H8 and Phase F have
retired by the time it starts, not acting on this document's snapshot directly.

If/when physical `ResourceBankItem` consolidation (H0 Option A, deferred at H0) is judged worth
doing, it should not be attempted as one large H9 phase. Split it:

- **H9A** — remove obsolete admin/API/code paths already proven dead by H8's audit.
- **H9B** — introduce a physical `ResourceBankItem` table or compatibility/migration adapters
  (additive; the four typed tables keep working throughout).
- **H9C** — migrate/deprecate the typed resource tables behind the new table, keeping both
  readable during a transition window.
- **H9D** — remove the old typed tables only after verification that nothing still depends on
  them directly (admin pages, `ResourceBankQueryService`, `LearnItemResourceLookup`, etc. all
  re-pointed and tested first).

## H10 — ActivityDefinition Runtime Launch Path / Attempt Bridge

**Not implemented in this phase.** Directly motivated by H7's own documented known limitation:
Practice Gym module suggestions are **display-only** — there is no launch path, because
`ActivityDefinition` (H4) has no attempt/scoring runtime anywhere in the codebase.
`ActivityTemplate` remains the *only* path that actually launches a scored Form.io pilot activity
today. This decision should be made **before** H9 removes any `ActivityTemplate`-dependent
runtime, since removing it prematurely would leave Practice Gym with no way to launch scored
practice at all.

H10 should decide one of:

- **Option A** — build a real `ActivityDefinition` attempt/scoring runtime from scratch (new
  attempt entity, scoring pipeline, feedback loop) — the most work, but the cleanest long-term
  separation from `ActivityTemplate`.
- **Option B** — bridge `ActivityDefinition` into the existing `LearningActivity`/
  `ActivityTemplate` materialization path (e.g. a `ModuleDefinition`'s linked `ActivityDefinition`
  gets projected into a `LearningActivity` the way an `ActivityTemplate` instance already does) —
  faster, reuses proven scoring/attempt infrastructure, but couples the new content-studio model
  back to the legacy runtime it was designed to eventually replace.
- **Option C** — hybrid: bridge first (Option B) to unblock a real Practice Gym "start" flow
  quickly, then build the full `ActivityDefinition` runtime (Option A) once usage data justifies
  the investment.

## Docs updated this phase

`docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`,
`docs/handoffs/current-product-state.md`, `docs/backlog/product-backlog.md`,
`docs/architecture/product-model-realignment-h0.md`,
`docs/architecture/learning-activity-engine.md`,
`docs/architecture/english-resource-bank-import-platform.md`, `docs/architecture/README.md`,
`TODOS.md` (`TODO-H8-1`, `TODO-H9-1`, `TODO-H10-1`), plus this document.

## Known open questions

- Which specific "coming soon" placeholder admin actions (if any remain) are actually safe to
  remove in H8 — needs a fresh sweep at H8 kickoff, since several were closed out live across
  H3-H5 and this document did not re-verify each one individually.
- Whether H9's physical `ResourceBankItem` consolidation is worth doing at all, versus keeping
  the four typed tables + unified read model indefinitely — not decided here, deliberately left
  open for a future Plan-Sync-Before-H9.
- Exact H10 option (A/B/C) is not decided here — this document only frames the decision and its
  urgency (it blocks any `ActivityTemplate` removal), the actual choice is H10's job.
- Whether Phase F (legacy free-form AI generation retirement) should be sequenced before, after,
  or interleaved with H8/H9/H10 — not resolved here; the existing roadmap discipline is that Phase
  F, G2/G3, and PG-v2 all remain separately-scoped tracks not blocked by the H-track, and vice
  versa.

## Final verdict

**Complete.** This phase is planning-only: it documents H7's current state, confirms H6/H7 are
additive and fallback-safe, records the user's removal-not-hiding cleanup direction, classifies
every audited legacy structure by removal risk (finding that essentially everything old is still
either core runtime infrastructure or a live fallback, with only redundant admin *navigation* —
not data or APIs — currently safe to touch), and defines H8/H9/H10 scope without implementing any
of them. No application code, migration, table, entity, API, or UI page was changed or removed.
