---
title: Phase H9B — Physical ResourceBankItem Consolidation Decision and Design
date: 2026-07-10
related: H-track (docs/architecture/product-model-realignment-h0.md), H9A (docs/reviews/2026-07-10-phase-h9a-legacy-admin-code-path-removal-review.md)
status: complete
---

# Phase H9B — Physical ResourceBankItem Consolidation Decision and Design

**Date:** 2026-07-10
**Type:** docs/design-only planning phase. No EF migration, no new table, no data migration, no
table/API/data removal, no `ResourceBankQueryService` rewrite, no selector rewrite, no
import/publish rewrite in this phase.

**HEAD before work:** `c4c4ae38` (Phase H9A)

## Mandatory Step -1 result

- `git log --oneline --decorate -n 18` confirmed HEAD = `c4c4ae38`.
- `git status --short` was clean.
- Proceeded per the ticket's instructions.

## The question this phase answers

> Should we physically consolidate typed published resource tables into a unified
> `ResourceBankItem` model, or keep typed tables with the unified read model for longer?

**Recommendation: Option A/E — keep the typed tables and the unified admin read model. Do not
build a physical `ResourceBankItem` table now.** Full reasoning below.

---

## Step 0 — Physical resource model audit

This audit was performed by direct code inspection (entities, EF configurations, services,
controllers, callers). File paths and line numbers are cited where useful.

### 1. The 4 typed entities and their structural diff

All four inherit `BaseEntity` (`Id: Guid`, `CreatedAt: DateTime`), live in
`src/LinguaCoach.Domain/Entities/`, and each carry a Phase E9 "lean selection metadata" block
added retroactively: `Subskill: string?`, `DifficultyBand: int?`, `ContextTagsJson: string?`,
`FocusTagsJson: string?`.

| Entity | Table | Fields unique to this type |
|---|---|---|
| `CefrVocabularyEntry` | `cefr_vocabulary_entries` | `Word`, `PartOfSpeech?`, `Notes?` |
| `CefrGrammarProfileEntry` | `cefr_grammar_profile_entries` | `GrammarPoint`, `Description?` |
| `CefrReadingReference` | `cefr_reading_references` | `TextType?`, `DifficultyNotes?`, `ReferenceExcerpt?` (≤500 chars by convention) |
| `CefrReadingPassage` | `cefr_reading_passages` | `Title`, `PassageText`, `Summary?`, `PrimarySkill`, `TopicTagsJson?`, `WordCount` (computed), `EstimatedReadingMinutes` (computed), `AttributionText?`, `ContentFingerprint?`, `QualityScore?`, `UpdatedAtUtc` |

**Key finding:** the common core is small (`Id`, `CreatedAt`, `SourceId`, `CefrLevel`, plus the
4 E9 metadata fields) — everything else is genuinely type-specific, not just differently named.
`CefrReadingPassage` in particular carries 8 fields none of the other 3 have, including the only
`ContentFingerprint`/`QualityScore`/`UpdatedAtUtc` columns in the whole group. Reading Reference
and Reading Passage are also **intentionally not interchangeable** even though both hold "reading"
content: Reference is capped at a short excerpt (never a full copyrighted text), Passage holds
full original/license-approved text — the 500-char length at publish time is the routing rule
between them (`ResourceCandidatePublishService.MaxReadingExcerptLength`), not a data-shape
accident.

A single `ResourceBankItem` table would need either (a) a wide table of mostly-nullable columns
(union of all fields, most null on most rows), or (b) a narrow common table plus a `ContentJson`
blob for the type-specific payload, or (c) a hybrid (common columns + JSON for the rest). None of
these is free — see the schema design section below for the tradeoff if this were pursued.

### 2. Import/publish pipeline (`ResourceImportRun` → `ResourceRawRecord` → `ResourceCandidate` → typed table)

`ResourceCandidatePublishService.PublishAsync` (`src/LinguaCoach.Infrastructure/ResourceImport/ResourceCandidatePublishService.cs:57`)
re-validates 5 gates live at publish time (source approval, `AllowsStudentDisplay`,
`AllowsCommercialUse`, `ValidationStatus == Passed`, `ReviewStatus == Approved`), is idempotent,
and routes by `ResourceCandidateType` (`BuildTargetEntity`, line 164):

- `VocabularyEntry` → `CefrVocabularyEntry`
- `GrammarProfileEntry` → `CefrGrammarProfileEntry`
- `ReadingPassage` candidate, staged text ≤ 500 chars → `CefrReadingReference`; longer (requires a
  `title`) → `CefrReadingPassage`

This is the **only** write path into the 4 typed tables. Confirmed (see item 12) that every
seeder — including the two "backfill"/"depth" metadata seeders — either flows through this exact
pipeline or only updates existing rows' metadata columns, never inserts directly. There is no
direct final-table seeding anywhere in the codebase today.

### 3. `ResourceBankQueryService` — the existing "unification" is a read-time projection, not a table

`ListUnifiedAsync` (`src/LinguaCoach.Infrastructure/ResourceImport/ResourceBankQueryService.cs:382`)
is explicitly documented in its own code comments as a workaround, not a real unified query: it
calls 4 private `BuildUnified*Async` helpers, each pulling the **entire filtered result set** for
one type into memory (no `Skip/Take` per type — lines 521/569/618/669), concatenates all 4 lists,
applies the `Skill` filter and sort in memory, and only then pages in memory. The typed
`List*Async` methods, by contrast, are real DB-side paginated queries (`MaxPageSize = 200`).

The code comment already anticipated this exact H9B question: it explicitly says a "genuinely
large multi-table cross-page query would need a real unified projection (a DB view or [a] physical
table)." This is the single strongest argument *for* eventually building something — but the
current content volume (internal seed packs, dozens of rows per type) means the in-memory approach
is not yet a real user-facing performance problem, and a DB view is a much cheaper fix than a
physical table if/when it becomes one (see Option C discussion below).

### 4. Callers of typed methods — 3 production call sites bypass the query service entirely

Besides `ResourceBankQueryService` itself and `AdminResourceBankController` (which just proxies
all 9 methods to the admin API), three other production classes query the 4 typed tables or typed
service methods directly:

- **`TodayBankResourceSelector`** (`src/LinguaCoach.Infrastructure/Activity/TodayBankResourceSelector.cs`)
  calls `ListVocabularyAsync`/`ListGrammarAsync`/`ListReadingReferencesAsync`/
  `ListReadingPassagesAsync`/`GetReadingPassageDetailAsync` (never `ListUnifiedAsync`) to build a
  CEFR/context/focus/subskill/difficulty-matched bundle injected into Today's lesson-generation
  prompt, via a deterministic strict→loose relaxation ladder applied identically across all 4.
  **Student-facing, must never regress.**
- **`LearnItemResourceLookup`** (`src/LinguaCoach.Infrastructure/LearnItems/LearnItemResourceLookup.cs`)
  switches on `PublishedResourceType` and does a direct `FirstOrDefaultAsync` against each of the
  4 `DbSet`s to resolve a `LearnItemResourceLink`'s polymorphic `ResourceType`/`ResourceId` pair
  back into a snapshot (title/body/CEFR/tags) at Learn Item generation time.
- **`ActivityGenerationService.FindDistractorDefinitionsAsync`** (`src/LinguaCoach.Infrastructure/ActivityDefinitions/ActivityGenerationService.cs:326`)
  queries `CefrVocabularyEntries`/`CefrGrammarProfileEntries` directly (Vocabulary/Grammar cases
  only) to source distractor text for auto-generated multiple-choice activities.

**Consolidation would require touching 4 places** (these 3 plus `ResourceCandidatePublishService`'s
write switch) even in the best case, not just `ResourceBankQueryService`.

### 5. Links store a polymorphic type+id pair, not a typed FK — and this already works

`LearnItemResourceLink` and `ActivityResourceLink` (`src/LinguaCoach.Domain/Entities/`) both use
`ResourceType: PublishedResourceType` + `ResourceId: Guid` (no physical FK — same pattern
`ResourceCandidate.PublishedEntityType`/`PublishedEntityId` already established) plus a
denormalized `SnapshotTitle` that survives edits/deletes of the underlying typed row, plus
`ContentFingerprint` (only ever populated when the source is `CefrReadingPassage` — the only one
of the 4 tables with that column). `ModuleDefinitionLearnItemLink`/`ModuleDefinitionActivityLink`
reference Learn Items/Activity Definitions directly (real FKs, since those are single tables) and
never reference typed bank rows at all — Modules only reach resources *indirectly*, through Learn
Items/Activity Definitions.

**This means the polymorphic-link pattern is already the system's chosen answer to "many typed
source tables, one link shape"** — a physical `ResourceBankItem` table would only replace the
*type discriminator + 4 tables* half of this pattern, not remove the pattern itself, since
`ResourceType`/`ResourceId` would become `ResourceBankItem.Type`/`ResourceBankItem.Id`. The link
tables themselves would not need to change shape, only what `ResourceId` points at.

### 6. `PublishedResourceType` enum

`src/LinguaCoach.Domain/Enums/PublishedResourceType.cs`: `Vocabulary=0, Grammar=1,
ReadingReference=2, ReadingPassage=3`. Its own doc comment already notes it "deliberately mirrors
but does not reference" `UnifiedResourceBankItemType` (the H1 Application-layer read-model enum),
because Domain must not depend on Application — Infrastructure maps between the two
(`ResourceBankQueryService.MatchesUnifiedType`). A `ResourceBankItem.Type` column would reuse this
exact enum.

### 7. H10 launch bridge and H6/H7 assignment tables — none reference typed resources directly

`StudentActivityDefinitionLaunch` (H10) references only `ModuleDefinition`/`ActivityDefinition`/
`LearnItem`/`LearningActivity` — no Cefr* reference at all.
`StudentDailyModuleAssignment`/`StudentPracticeGymModuleAssignment` (H6/H7) reference only
`ModuleDefinition`. **None of H6/H7/H10's new tables would need any change under any
consolidation option** — they sit two hops away from the typed tables (Module → LearnItem/
ActivityDefinition → ResourceLink → typed row), so they're insulated from this decision entirely.

### 8. EF configuration — inconsistent tag storage, no fingerprint uniqueness anywhere

- `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` store `ContextTagsJson`/
  `FocusTagsJson` as plain `text` (a deliberate choice so a portable `.Contains("\"tag\"")`
  LIKE-filter works identically on Postgres and SQLite-in-memory tests).
- `CefrReadingPassage` stores `TopicTagsJson`/`ContextTagsJson`/`FocusTagsJson` as `jsonb` —
  **inconsistent with the other 3**, a pre-existing wrinkle a consolidated table would have to
  resolve one way or the other.
- **No unique constraint on `ContentFingerprint` anywhere** in this subsystem — not on
  `ResourceCandidate`, not on `CefrReadingPassage` (the only typed table that even has the
  column). Dedup is exact-match application logic at Phase E2 validation time, not DB-enforced.
  Only 3 of 4 typed tables have no `ContentFingerprint` column at all.

### Answers to the audit's 10 required questions

1. **True source of truth today:** the 4 typed Cefr* tables — no other table or cache holds
   published resource content.
2. **Services depending on typed resource IDs:** `TodayBankResourceSelector`,
   `LearnItemResourceLookup`, `ActivityGenerationService`, `ResourceBankQueryService`,
   `ResourceCandidatePublishService`, `AdminResourceBankController` (transitively).
3. **Links storing typed resource type+ID:** `LearnItemResourceLink`, `ActivityResourceLink` (both
   via `PublishedResourceType` + `Guid`, polymorphic, no FK).
4. **Import/publish flows writing typed resources:** only `ResourceCandidatePublishService`,
   reached from every import path (file upload, H2 Content Import) via the same
   `ResourceCandidate` → approve → publish pipeline.
5. **Today/Practice Gym selectors reading typed resources:** `TodayBankResourceSelector` reads
   directly (Today's lesson generation); no separate Practice Gym selector reads typed resources
   directly — Practice Gym's H7 module suggestions reach resources only indirectly through
   Module → LearnItem/ActivityDefinition → ResourceLink, and the legacy free-form Practice Gym
   fallback doesn't consult the bank at all (it's `IAiActivityGenerator`-only).
6. **Learn/Activity/Module generation depending on typed resource references:** `LearnItem`/
   `ActivityDefinition` generation (H3/H4) resolve resources via `LearnItemResourceLookup`/direct
   query; Module generation (H5) never touches typed resources — it composes only from
   already-Approved Learn Items/Activity Definitions.
7. **Field differences across the 4 types:** substantial and type-specific, not superficial — see
   item 1 above; `CefrReadingPassage` alone has 8 fields none of the others have.
8. **Can one table represent all 4 types without data loss:** yes, but only via a wide
   mostly-nullable table or a JSON payload column — a straight single flat-columns table would be
   an awkward fit given how different the type-specific fields are (see schema design below).
9. **What breaks if typed tables disappeared today:** Today's lesson generation
   (`TodayBankResourceSelector`), Learn Item resource resolution
   (`LearnItemResourceLookup`), Activity Definition distractor generation
   (`ActivityGenerationService`), the admin Resource Bank page (`ResourceBankQueryService`), and
   all publish flows (`ResourceCandidatePublishService`) — i.e. essentially the entire bank-first
   content model, both admin and student-facing.
10. **What's safe to consolidate now vs. later:** nothing is safe to physically consolidate *now*
    without a real migration plan — every one of the 4 direct-query call sites plus the publish
    write-switch would need to move together, in lockstep, with a verified rollback path. Nothing
    audited here is "free" to consolidate today.

---

## Decision options compared

### Option A — Keep typed tables and unified read model for now

**Pros:** lowest risk; no data migration; existing imports/selectors (`TodayBankResourceSelector`,
`LearnItemResourceLookup`, `ActivityGenerationService`, `ResourceCandidatePublishService`) remain
completely untouched; typed resource shape stays clear and type-safe (no JSON blob, no
mostly-null wide table); the polymorphic-link pattern already proven working since H3.

**Cons:** the admin unified read model (`ListUnifiedAsync`) remains an in-memory, non-paginated
per-type scan under the hood — not a genuine DB-level union; 4 places in the codebase still
reference the 4 typed tables by name; the physical model still doesn't match the admin-facing
mental model 1:1.

### Option B — Add physical `ResourceBankItem` as canonical table, keep typed tables temporarily

**Pros:** new model gets a real canonical identity; future links could simplify to a single FK;
typed tables could eventually retire.

**Cons:** requires a migration + backfill for every existing published row; requires either
dual-write (publish to both typed table and `ResourceBankItem` simultaneously — divergence risk if
one write succeeds and the other fails) or a projector/sync job (staleness risk); requires
migrating `LearnItemResourceLink`/`ActivityResourceLink` to a new identity space without breaking
`SnapshotTitle`/`ContentFingerprint` semantics; requires updating all 4 direct-query call sites
audited above; the structural field diff between the 4 types (item 1) means the new table is
either a wide mostly-null table or needs a `ContentJson` blob, either of which loses some of the
type safety/DB-level filterability the current typed columns give `TodayBankResourceSelector`'s
context/focus/subskill/difficulty relaxation ladder today.

### Option C — `ResourceBankItem` as read-through/materialized projection only

**Pros:** improves `ListUnifiedAsync` performance without touching source-of-truth or any writer;
lower risk than Option B since nothing reads from it except the admin unified view (which already
tolerates staleness — it's an admin browsing page, not student-facing).

**Cons:** introduces a second model that must stay in sync (rebuild-on-write or scheduled rebuild)
for a problem (`ListUnifiedAsync`'s in-memory scan) that isn't yet a proven bottleneck at current
content volume, and that has a much cheaper fix available — a SQL `UNION ALL` **database view**
gets the same DB-side pagination benefit without owning any data, needing zero backfill, and zero
migration risk. A materialized table is solving this specific problem with more mechanism than the
problem currently justifies.

### Option D — Full immediate consolidation

**Rejected outright, not seriously compared.** Combines every risk of Option B (migration,
dual-write/projector, link migration, 4-call-site rewrite) with the additional risk of retiring
typed tables/APIs in the same phase, with no soak period and no rollback plan. Given the audit
found genuine type-specific field differences (not superficial naming) and zero unique-constraint
safety net on fingerprints today, an immediate full cutover has materially higher blast radius than
the current, proven-working system. Rejected.

### Option E — Do not physically consolidate; typed tables are permanent

**Pros:** all of Option A's pros, stated as a permanent commitment rather than a "for now"; removes
the ongoing cognitive overhead of treating physical consolidation as a live open question in every
future H-phase's audit section.

**Cons:** if content volume grows by an order of magnitude (many more sources, much larger seed
packs, or a public-facing content marketplace), the in-memory `ListUnifiedAsync` scan and the
4-call-site-per-typed-table pattern would eventually need revisiting anyway — "permanent" is doing
more work than the evidence currently supports.

---

## Recommendation

```text
Recommended: Option A, converging toward Option E unless content-volume growth proves otherwise.
Reason: The 4 typed tables hold genuinely different field shapes (not superficial naming
  differences), the only real pain point (ListUnifiedAsync's in-memory per-type scan) has a much
  cheaper fix than a physical table (a SQL view, see H11 below), the polymorphic-link pattern
  already works and would only partially simplify under Option B, and current content volume
  (internal seed packs, dozens of rows per type) does not justify the migration/dual-write/
  4-call-site-rewrite risk that any physical consolidation (B/C/D) would require.
Risks: if published content volume grows an order of magnitude, ListUnifiedAsync's in-memory scan
  could become a real admin-page performance problem before a lighter fix (H11) is built. This is
  the one condition that should trigger revisiting this decision.
What must happen before implementation: nothing — this recommendation is "do not implement
  physical consolidation," so there is no implementation to gate.
What should happen next: if `ListUnifiedAsync`'s in-memory scan ever becomes a measured
  performance problem (not a hypothetical one), pursue a lightweight **H11 — Strengthen
  ResourceBankQueryService with a SQL-side unified view**, which gets DB-level pagination for the
  admin Resource Bank page via a `UNION ALL` view (or equivalent EF-mapped keyless entity) over the
  4 typed tables, with zero data migration, zero dual-write risk, and zero change to any of the 4
  typed tables, their writers, or their 3 direct-query readers. This is strictly cheaper than
  Option B/C and solves the one concretely-identified pain point without taking on migration risk
  for a problem that doesn't exist yet.
```

**H9C/H9D/H9E/H9F/H9G are not scheduled.** Since consolidation is not recommended, there is no
migration/link-migration/typed-table-removal sequence to define at this time. `TODO-H9C-1` and
`TODO-H9D-1` are updated to reflect this — see Docs updated below.

---

## Data model design (documented for completeness, not scheduled for implementation)

If a future re-evaluation reverses this recommendation (Option B), the target schema sketch would
be:

```text
Table: resource_bank_items
Entity: ResourceBankItem

Key fields:
  Id: Guid (PK)
  Type: PublishedResourceType (reuse the existing Domain enum, no new enum)
  CefrLevel: string
  Subskill: string?
  DifficultyBand: int?
  ContextTagsJson: string  (plain text, matching 3-of-4 current tables — NOT jsonb;
                             standardizes on the more portable of the two current conventions)
  FocusTagsJson: string
  SourceId: Guid (FK → CefrResourceSources, unchanged)
  ContentFingerprint: string?  (populate for ALL types this time, not just ReadingPassage —
                                 closes a gap the audit found)
  AdminReviewStatus / PublishedStatus: reuse existing AdminReviewStatus, always Published for rows
                                        in this table (unpublished rows stay in ResourceCandidate)
  CreatedAt: DateTime
  UpdatedAt: DateTime?

Content payload strategy: HYBRID — common/filterable fields as real columns (everything above),
  type-specific payload (Word/PartOfSpeech, GrammarPoint/Description, TextType/DifficultyNotes/
  ReferenceExcerpt, Title/PassageText/Summary/WordCount/EstimatedReadingMinutes/AttributionText/
  QualityScore) as a single ContentJson column, deserialized per-Type into a typed DTO at the
  application layer. Rejected pure-columns (too many always-null columns, one per type's unique
  fields) and pure-JSON (loses DB-side filtering on fields TodayBankResourceSelector's relaxation
  ladder needs, which must stay real columns).

Compatibility mapping strategy: none needed if migration is a true one-time cutover per row (the
  old Id can be preserved as the new Id, since ResourceBankItem.Id is a fresh Guid space with no
  collision risk against the 4 old tables' Guid spaces). A ResourceBankItemSourceLink table would
  only be needed if BOTH old and new tables must stay live and independently queryable during a
  transition window — likely necessary in practice for a real rollback path.

Link migration strategy: LearnItemResourceLink/ActivityResourceLink's ResourceType+ResourceId
  columns would repoint at ResourceBankItem's Type+Id instead of the 4 typed tables' Type+Id —
  since PublishedResourceType is reused unchanged and Ids are preserved 1:1, this could be a
  no-op if old Ids are kept, or a single backfill UPDATE keyed by ContentFingerprint/SourceId+
  natural-key lookup if new Ids are minted instead.

Index strategy: mirror the current typed-table indexes (CefrLevel, SourceId, ContentFingerprint,
  CreatedAt) plus a new (Type, CefrLevel) composite, since Type now needs to do what "which table"
  used to do.

Rollback strategy: keep the 4 typed tables physically present and populated (do not drop) for a
  full soak period after cutover; a rollback is "point readers back at the typed tables," not "run
  a reverse migration" — this is why Option D (full immediate consolidation including typed-table
  removal) was rejected: it would remove exactly the thing that makes rollback cheap.
```

This design is **not being implemented in H9B**. It is documented so a future re-evaluation does
not have to re-derive it from scratch.

---

## Link migration analysis

`LearnItemResourceLink`/`ActivityResourceLink` currently use `PublishedResourceType` + `Guid`
(polymorphic, no FK) — see Step 0 item 5. **If** consolidation were ever pursued, links should
migrate to `ResourceBankItem.Type` + `ResourceBankItem.Id`, reusing the exact same
`PublishedResourceType` enum (no new type discriminator needed) and, if old Ids are preserved
1:1 during migration (recommended in the schema sketch above), no dual-link period would even be
necessary — the existing `ResourceType`/`ResourceId` pair would simply start resolving against the
new table instead of the old 4. A dual-link compatibility period would only be needed if new Ids
are minted for `ResourceBankItem` rows independent of the old tables' Ids, which the schema sketch
above recommends against specifically to avoid this complexity. **Not implemented in H9B.**

## Import/publish pipeline analysis

Recommended future flow, if consolidation were pursued: **publish to the typed table, then a
same-transaction projector inserts/updates the corresponding `ResourceBankItem` row** (not
dual-write from two independent call sites, and not publish-to-ResourceBankItem-only, which would
strand the typed tables mid-migration with no way to verify parity). This keeps
`ResourceCandidatePublishService`'s existing 5-gate validation and idempotency check as the single
source of truth for "is this candidate allowed to be published," and makes `ResourceBankItem`
purely a downstream projection of that decision, never a second place the same decision could
diverge. **Not implemented in H9B** — current publish flow (typed tables only) is completely
unchanged.

## Selector/runtime analysis

If consolidation were pursued, recommended migration order (highest-value/lowest-risk first):

1. `AdminResourceBankController`'s unified endpoint (`ListUnifiedAsync`) — admin-only, already
   tolerant of the current in-memory approach's staleness/limitations, lowest blast radius.
2. `LearnItemResourceLookup` — resolves at Learn Item generation time (admin-triggered, not
   student-facing in real time).
3. `ActivityGenerationService.FindDistractorDefinitionsAsync` — same admin-triggered generation
   context.
4. `TodayBankResourceSelector` — **last**, since it is the only student-facing, real-time read
   path in this list; any migration risk here directly risks Today's lesson generation for real
   students. Should only move once 1-3 have been live and verified for a full soak period.

**Not implemented in H9B** — no selector code was touched.

---

## Removal safety gates for later phases (H9D+, if ever scheduled)

These gates must ALL be true before any typed resource table can be removed, if consolidation is
ever pursued and reaches that stage:

- All active read paths (`ResourceBankQueryService`, `TodayBankResourceSelector`,
  `LearnItemResourceLookup`, `ActivityGenerationService`) use `ResourceBankItem` or a verified
  compatibility adapter.
- All active write/publish paths (`ResourceCandidatePublishService`) write `ResourceBankItem`
  (directly or via the projector design above).
- All `LearnItemResourceLink` records resolve correctly against `ResourceBankItem` (migrated or
  dual-linked, with a passing verification pass).
- All `ActivityResourceLink` records resolve correctly against `ResourceBankItem` (same).
- All backend tests pass against migrated data (full `dotnet test --configuration Release`, 0
  failures).
- The admin Resource Bank page works with the typed pages already removed (true today, since
  H9A — not blocked on this decision).
- Today's fallback path (legacy free-form generation) still works if `ResourceBankItem` selection
  fails for any reason.
- Practice Gym's fallback path still works under the same condition.
- The H10 `ActivityDefinition` launch bridge still works (it has no typed-resource dependency
  today per Step 0 item 7, so this gate should already be trivially true, but must be re-verified).
- A rollback/export plan exists and has been tested (per the schema design's rollback strategy:
  typed tables remain populated and readable, not dropped, for the full soak period).
- A production data audit has been completed confirming row-count/content parity between the
  typed tables and `ResourceBankItem` before any typed-table drop.

---

## Docs updated

- `TODOS.md` — `TODO-H9B-1` marked done; `TODO-H9C-1`/`TODO-H9D-1` updated to reflect that
  consolidation is not recommended at this time, with the design/gates above preserved for a
  future re-evaluation; added `TODO-H11-1` (the SQL-view alternative).
- `docs/roadmap/road-map.md`
- `docs/sprints/current-sprint.md`
- `docs/handoffs/current-product-state.md`
- `docs/backlog/product-backlog.md`
- `docs/architecture/product-model-realignment-h0.md`
- `docs/architecture/learning-activity-engine.md`
- `docs/architecture/english-resource-bank-import-platform.md`
- `docs/architecture/README.md`
- This review doc.

## Known open questions

- If content volume does grow enough to revisit this, is a SQL view (H11, recommended) sufficient,
  or would the admin page's filter/search requirements outgrow what a `UNION ALL` view can express
  efficiently? Not answerable without real growth data.
- Should `ContentFingerprint` be added to the 3 typed tables that lack it (Vocabulary/Grammar/
  ReadingReference) independent of any consolidation decision, purely to close the dedup gap the
  audit found? Out of scope for H9B, worth a small standalone follow-up ticket if dedup quality
  becomes a concern.

## Final verdict

**Option A (converging toward E) is recommended: do not build a physical `ResourceBankItem` table
now.** The typed tables plus the existing unified admin read model remain the system's model.
No physical consolidation, migration, or removal happened in this phase — this was a decision and
design phase only.

## Explicit confirmations

- No EF migration added.
- No `ResourceBankItem` table added.
- No data migration performed.
- No typed bank tables removed.
- No typed bank APIs removed.
- No `ResourceBankQueryService` rewrite.
- No Today selector rewrite.
- No Practice Gym selector rewrite.
- No import/publish rewrite.
- No `ActivityTemplate` removal.
- No `PracticeActivityCache` removal.
- No `StudentActivityReadinessItem`/readiness queue removal.
- No `LearningActivity`/`LearningSession`/`SessionExercise` removal.
- No Today fallback removal.
- No Practice Gym fallback removal.
- No ActivityDefinition launch bridge removal.
- No PG-v2 started.
- No external datasets.
- No Persian/bilingual content.
- No direct final-table seeding.
- Committed locally only; not pushed; not deployed; branch unchanged (`main`).

## Next recommended action

No H9C/H9D/H9E/H9F/H9G implementation is scheduled. If the `ListUnifiedAsync` in-memory scan ever
becomes a measured (not hypothetical) performance problem, pursue **H11 — Strengthen
ResourceBankQueryService with a SQL-side unified view**, a strictly cheaper alternative to physical
consolidation. See `TODO-H11-1`.
