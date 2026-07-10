---
title: Phase I0 — Physical ResourceBankItem Consolidation (Implementation)
date: 2026-07-10
related: I-track (unified content pipeline & legacy retirement), reverses H9B (docs/reviews/2026-07-10-phase-h9b-resourcebankitem-consolidation-decision.md)
status: complete
---

# Phase I0 — Physical ResourceBankItem Consolidation

**Date:** 2026-07-10
**Type:** implementation phase. Reverses Phase H9B's "do not consolidate" recommendation, per
explicit user direction this session: build one unified content pipeline (Import → Bank → Learn →
Activities → Modules → Onboarding → Placement) with a single physical Resource Bank table, and
delete legacy fallback infrastructure rather than keep it as a safety net.

**HEAD before work:** `052996ea` (Phase H9B)

## What changed

The four typed published bank tables — `CefrVocabularyEntry`, `CefrGrammarProfileEntry`,
`CefrReadingReference`, `CefrReadingPassage` — are replaced by one physical table,
`resource_bank_items` (entity `ResourceBankItem`, `src/LinguaCoach.Domain/Entities/ResourceBankItem.cs`).

**Schema:** common/DB-filterable fields (`Type`, `CefrLevel`, `Subskill`, `DifficultyBand`,
`ContextTagsJson`, `FocusTagsJson`, `SourceId`, `ContentFingerprint`) are real columns; the
genuinely type-specific payload (word/grammar point/reading excerpt/passage text, etc.) is packed
into a `ContentJson` column and deserialized per-`Type` via typed content records
(`VocabularyContent`/`GrammarContent`/`ReadingReferenceContent`/`ReadingPassageContent` in
`src/LinguaCoach.Application/ResourceImport/ResourceBankItemContent.cs`) — the hybrid design H9B's
audit had already worked out, reused as-is. `ContentFingerprint` is now populated for all 4 types
(previously only `CefrReadingPassage` had it). Original row `Id`s were preserved 1:1 during
migration, so `LearnItemResourceLink`/`ActivityResourceLink`'s existing `ResourceType`+`ResourceId`
values kept resolving with zero link-table migration needed.

**Migration:** two EF migrations — `Phase_I0_AddResourceBankItem` (additive: creates the new
table) and `Phase_I0_DropTypedBankTables` (drops the 4 old tables). A one-time idempotent backfill
seeder (`ResourceBankItemBackfillSeeder`, since removed — see below) migrated any existing rows
between the two migrations.

**Write path:** `ResourceCandidatePublishService.BuildTargetEntity` now builds `ResourceBankItem`
directly — no dual-write, no projector. Same 5 live-revalidated publish gates, same
500-character reference-vs-passage routing rule as before.

**Read paths, switched in H9B's documented lowest-risk-first order:**
1. `ResourceBankQueryService` — `ListUnifiedAsync` is now a **real single-table DB-paginated
   query** (previously an in-memory 4-way scan/concat/sort/page — this is a straightforward
   performance win from consolidation). The 8 typed `List*Async`/`Get*DetailAsync` methods on
   `IResourceBankQueryService` were **kept** (not deleted, contrary to the original plan draft) —
   `TodayBankResourceSelector` depends on their typed DTO shapes directly; they're now thin
   type-filtered projections over the one table instead of separate table queries.
2. `LearnItemResourceLookup` — one query by `Id`+`Type` instead of a 4-way switch over 4 DbSets.
3. `ActivityGenerationService.FindDistractorDefinitionsAsync` — one query filtered to
   Vocabulary/Grammar types instead of two separate typed queries.
4. `TodayBankResourceSelector` — **needed zero code changes.** It only calls
   `IResourceBankQueryService`'s typed methods, whose DTO shapes are unchanged; what's behind them
   changed, not what they return.

**Deleted:** the 4 typed entity classes and their EF configurations; `AdminResourceBankController`'s
8 typed HTTP routes (`vocabulary`/`grammar`/`reading-references`/`reading-passages`, list+detail) —
their only caller was the admin frontend's typed bank pages, already removed in Phase H9A; only the
unified `GET api/admin/resource-bank` route remains. `PublishedBankMetadataBackfillSeeder` and
`InternalBankMetadataDepthSeeder` — both repaired missing E9 metadata directly on the 4 typed
tables; dead once those tables are gone, since metadata is now set correctly at publish time.
`ResourceBankItemBackfillSeeder` itself was also removed once the drop migration landed (nothing
left for it to read). `CefrBankMetadata` (a shared validation helper only the 4 deleted entities
used).

## Correction made mid-implementation

The original plan draft assumed the 8 typed `IResourceBankQueryService` methods had no real
caller (based on the H9B audit's finding that the *admin HTTP routes* were unused post-H9A). That
finding was about the HTTP layer only — `TodayBankResourceSelector` (student-facing, real-time)
calls those same *service-layer* methods directly via DI, not HTTP. The plan was corrected before
implementation: the 8 typed HTTP actions were deleted, the 8 typed service methods were kept and
repointed at the new table.

## Validation

- `dotnet build --configuration Release` — clean, 0 errors.
- `dotnet test --configuration Release` — 3,854/3,854 passing (5 architecture, 2,306 unit, 1,543
  integration). Down from the 3,925 pre-phase baseline: 2 dead-code seeder test files deleted (their
  seeders were deleted), 3 domain entity test files deleted and replaced by one
  `ResourceBankItemTests.cs` covering the same validation rules generically, and several
  `AdminResourceBankEndpointTests.cs` cases removed for the deleted typed HTTP routes.
- `npm run build -- --configuration production` (frontend) — no new TS/Angular errors. Frontend
  DTOs (`UnifiedResourceBankItemDto` etc.) are unchanged, so no frontend code needed updating.

## What was NOT touched in this phase

`ActivityTemplate`, `PracticeActivityCache`, `StudentActivityReadinessItem`, the legacy
`IAiActivityGenerator` fallback, `LearningActivity`/`LearningSession`/`SessionExercise`, and every
Today/Practice Gym delivery path are all untouched — those are I2's scope (legacy fallback
deletion), not I0's. `ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate`/
`CefrResourceSource` and their admin pages/controllers are untouched — that's I1's scope (unified
import/publish pipeline).

## Next steps

I1 (unified import/publish pipeline), I2 (delete legacy fallback runtime — user has confirmed this
should proceed even though it narrows Today/Practice Gym content coverage to
`gap_fill`/`multiple_choice_single` vocab/grammar only, until I5 expands coverage), and I3 (final
nav/IA consolidation) remain to be scoped in detail and implemented. See the session's plan file
for the full I-track roadmap.

## Explicit confirmations

- EF migration added (2: create + drop) — expected and intentional for this phase, unlike prior
  H9A/H9B phases which were migration-free.
- `ResourceBankItem` table added; 4 typed tables dropped.
- Data migration performed (backfill seeder), then removed once its job was done.
- No `ActivityTemplate`/`PracticeActivityCache`/`StudentActivityReadinessItem`/runtime session
  entity removal in this phase — that's I2.
- No Today/Practice Gym fallback removal in this phase.
- No PG-v2, external datasets, Persian/bilingual content, or direct final-table seeding.
- Committed locally only; not pushed; not deployed; branch unchanged (`main`).
