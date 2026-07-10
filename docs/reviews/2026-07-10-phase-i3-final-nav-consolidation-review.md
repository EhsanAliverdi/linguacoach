---
title: Phase I3 — Final Nav Consolidation (Implementation)
date: 2026-07-10
related: I-track, closes the structural sequence started by I0
status: complete
---

# Phase I3 — Final Nav Consolidation

**Date:** 2026-07-10
**Type:** implementation phase, closes the structural half of the I-track (I0-I3). Docs-language
cleanup (I4) and coverage expansion (I5/I6) remain separate, later phases.

**HEAD before work:** `bde08503` (I4 planning docs)

## What changed

Landed the 7-item Content Studio nav target set out in the original I-track plan:
**Import Content → Resource Bank → Learn Items → Activities → Modules → Onboarding → Placement**,
one section, no second "Content Ops" tier.

- **Promoted** Onboarding and Placement (renamed "Placement items" → "Placement" in the nav label)
  out of "Content Ops" into "Content Studio."
- **Deleted Review Queue entirely** — controller (`AdminReviewQueueController`), contracts
  (`AdminReviewQueueContracts.cs`), query handler (`AdminReviewQueueQueryHandler.cs`), its DI
  registration, and the frontend component/service/models. It only ever covered
  `PlacementItemDefinition` review after Phase I2A deleted `ActivityTemplate` (the other half of
  what it used to cover) — the standalone Placement Items page already does everything Review
  Queue did for that entity (list, filter, approve/reject), so nothing was lost. Old
  `/admin/review-queue` bookmarks redirect to `/admin/placement-items`.
- **Removed the now-empty "Content Ops" section** from both the desktop sidebar and mobile-drawer
  nav trees.

**"Delivery" section (Today Delivery Health) was left untouched** — it's now mostly inert per
Phase I2B (its retry/manual-generate actions became honest no-ops once the legacy generation jobs
were deleted), but that wasn't in this phase's scope; flagged as a residual cleanup candidate for
a future pass, not addressed here.

## Validation

- `dotnet build --configuration Release` — clean, 0 errors.
- `dotnet test --configuration Release` — 3,424/3,424 passing (5 architecture, 2,107 unit, 1,312
  integration; down 4 from the 3,428 pre-phase baseline — the deleted
  `AdminReviewQueueEndpointTests.cs`, no lost coverage elsewhere).
- `npm run build -- --configuration production` — no new TS/Angular errors.
- Grep sweep confirmed zero remaining source references to `AdminReviewQueue`/`admin-review-queue`
  outside compiled build artifacts (`bin`/`obj`, not source-controlled).

## What was NOT touched

- "Today Delivery Health" (`/admin/lessons`) — left as-is; a candidate for a future cleanup pass
  now that its generation-retry actions are no-ops, but out of this phase's scope.
- `Activity Templates` nav item — already removed in Phase I2A (the entity itself was deleted
  there).
- The I4 renaming decision (`LearnItem`→Lesson, `ActivityDefinition`→Exercise,
  `ModuleDefinition`→Module, "Daily Lesson"→Today Plan) — decided but not implemented; this phase
  used the current (pre-I4) names throughout, since I4 hasn't landed yet.

## Explicit confirmations

- No EF migration (no entity/schema change — pure nav/route/controller-deletion cleanup).
- No `ActivityTemplate`/`PracticeActivityCache`/readiness-pool/`LearningActivity`-family changes.
- No PG-v2, external datasets, Persian/bilingual content, or direct final-table seeding.
- Committed locally only; not pushed; not deployed; branch unchanged (`main`).

## I-track status after this phase

I0 (bank consolidation) → I1 (import pipeline) → I2A/B/C (legacy fallback deletion) → I3 (nav
consolidation) are all complete. The structural half of the I-track is done: the admin content
model is now genuinely unified (one bank, one import pipeline, one nav section, no legacy
fallback). Remaining I-track work is language/coverage, not structure:
- **I4** — rename `LearnItem`/`ActivityDefinition`/`ModuleDefinition`/"Daily Lesson" to
  Lesson/Exercise/Module/Today Plan (decided, not implemented — see
  `docs/architecture/product-language-renaming-i4.md`).
- **I5** — expand bank-first coverage beyond `gap_fill`/`multiple_choice_single` vocab/grammar to
  the ~31 other exercise types (speaking, listening, writing, matching, reorder) that I2 stopped
  serving via the legacy fallback.
- **I6** — real AI-driven import structuring and Learn/Activity generation, replacing today's
  deterministic composers.
