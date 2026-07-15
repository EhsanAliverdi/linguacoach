# Content Creation Pipeline — Architecture Audit

**Date:** 2026-07-15
**Related sprint/feature:** Phase K series (Admin Content Studio bank-first pipeline), audited against the intended target architecture: Import → Resource Bank → Lesson → Exercise → Module → Published Module Catalogue.
**Type:** Architecture / gap analysis (read-only audit — no code changes made)

## Files reviewed

Backend (Domain/Application/Infrastructure/Api), by stage:

- **Import**: `AdminContentImportController.cs`, `AdminResourceImportController.cs`, `ContentImportService.cs`, `ResourceImportService.cs`, `ResourceCandidateAnalysisService.cs`, `ResourceCandidateValidationService.cs`, `ResourceCandidatePublishService.cs`, `ResourceCandidate.cs`, `ResourceCandidateValidationStatus.cs`, `ResourceCandidateType.cs`, `ResourceImportRun.cs`, `IResourceImportColumnMappingService` call sites.
- **Resource Bank**: `ResourceBankItem.cs`, `PublishedResourceType.cs`, `ResourceBankItemContent.cs`, `LessonResourceLink.cs`, `LessonResourceLinkConfiguration.cs`, admin-resource-bank-* Angular components.
- **Lesson**: `Lesson.cs`, `LessonSourceMode.cs`, `LessonGenerationService.cs`, `AiLessonGenerationService.cs`, `LessonResourceLookup.cs`, `AdminLessonController.cs`, migration `Phase_H3_AddLearnItemFoundation`/`Phase_I4_RenameLessonExerciseModule`, `docs/architecture/product-language-renaming-i4.md`.
- **Exercise**: `Exercise.cs`, `ExerciseGenerationContracts.cs`, `ExerciseGenerationService.cs`, `AiExerciseGenerationService.cs`, `LessonExerciseBatchGenerationService.cs`, `ExerciseResourceLink.cs`, `ExerciseTypeDefinition.cs`, `ExerciseTypeDefinitionSeeder.cs`, `AdminExerciseController.cs`, `admin-exercise.service.ts`, `admin-lesson-detail.component.ts`, `ExercisePatternDefinition.cs`.
- **Module**: `Module.cs`, `LearningModule.cs`, `ModuleLessonLink.cs`, `ModuleExerciseLink.cs`, `AdminModuleController.cs`, `ModuleAutoLinkService.cs`, `ModuleStageContent.cs`, `ActivityGetHandler.cs`, `PracticeGymModuleSelectionService.cs`, `TodayPlanModuleSelectionService.cs`, `ExerciseLaunchService.cs`, `ModuleQueryHandlers.cs`, `ModuleRepairService.cs`.
- **Prior art**: `docs/architecture/product-model-realignment-h0.md`, `docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md`, `docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md`.

Method: four parallel read-only research passes (one per pipeline stage), cross-checked against each other for entity relationships that span stage boundaries (e.g. `Lesson.ReviewStatus` vs. Exercise-generation gating, `Module.IsArchived` vs. student assignment).

---

## Executive summary

The codebase implements roughly **70% of the intended pipeline faithfully**, with the middle stages (Resource Bank, Lesson, Exercise, Module review) built to a genuinely high standard — real review/approval state machines, real provenance links, real duplicate detection. The two biggest deviations from the intended design are:

1. **Import does not do what "Import" is supposed to do.** There is no AI-driven splitting of raw content into structured candidates. Candidate generation for pasted text is a literal `content.Split('\n')`; for structured file uploads it's a 1-row-in-1-candidate-out deterministic mapper. AI only *classifies/enriches* an already-staged candidate (CEFR level, tags, quality score) — it never *discovers* candidates from unstructured prose. The intended "AI understands the content and splits it into candidates" step is explicitly disclaimed in code comments as not implemented.
2. **"Published Module Catalogue" does not exist as a distinct concept.** There is no publish action, no `IsPublished` flag, no catalogue table, and no versioning. `AdminReviewStatus.Approved` alone is overloaded to mean both "reviewed" and "published," and that gate is re-implemented ad hoc in at least three separate call sites rather than centralized. A real gap was found here: an `Approved` **but archived** Module can still be suggested to students and launched (the student-facing selection/launch code never checks `IsArchived`).

Everything else — Resource Bank lifecycle, Lesson generation/review, Exercise generation/review (including a valid direct Resource→Exercise path the intended design doesn't mention), Module review — is implemented, generally to spec or beyond spec (structural M:N reuse exists at several joints where the intended design only requires 1:N). The codebase also carries real, well-documented legacy debt: an entire deleted-but-partially-remnant "Pattern Engine"/on-demand AI generation system, and a second, unrelated "Module" concept (`LearningModule`) that predates and is not yet unified with the new bank-first `Module`.

**Overall assessment: the implementation is closer to the intended architecture in its middle (Resource Bank → Lesson → Exercise → Module-review) than at its two edges (Import's AI-splitting promise, and Module's publish/catalogue promise).**

---

## Current pipeline diagram (as implemented today)

```
                                   ┌─────────────────────────┐
                                   │   IResourceImportColumn  │  (AI: column-mapping suggestions only)
                                   │   MappingService (Phase K1)│
                                   └─────────────────────────┘
Admin paste/upload
        │
        ▼
ContentImportService / ResourceImportService
        │  (pasted text → split by '\n'; CSV/JSON/JSONL → 1 row = 1 candidate)
        │  deterministic dedup-hash + English-only heuristic + field-shape check
        ▼
ResourceCandidate (ValidationStatus: Pending→Passed/Failed/NeedsReview)
        │                                   ▲
        │  admin: analyze (AI, single item) │  admin: edit AdminNotes ONLY
        │  ResourceCandidateAnalysisService  │  (no content editing)
        ▼                                   │
ResourceCandidate (ReviewStatus: PendingReview → Approved/Rejected)
        │
        │  admin: approve, then PUBLISH (separate action)
        ▼
ResourceCandidatePublishService  ── sole writer of ResourceBankItem ──┐
        │                                                              │
        ▼                                                              │
ResourceBankItem  (no ReviewStatus of its own — publish IS approval)  │
        │  IsArchived / UpdateContent (post-publish edit)      ◄──────┘
        │
        ├──────────────────────────────┐
        ▼                              ▼
LessonGenerationService /      ActivityGenerationService.GenerateActivityFromResources
AiLessonGenerationService      (Exercise generated DIRECTLY from Resources,
        │                       bypassing Lesson — API exists, admin UI unused today)
        ▼
Lesson (ReviewStatus: PendingReview → Approved/Rejected)
   ── LessonResourceLink → ResourceBankItem (N:M, reuse untracked in UI)
        │
        │  admin: "Generate Exercises" (Lesson-detail page — the only UI path actually used)
        ▼
ActivityGenerationService.GenerateActivityFromLesson /
LessonExerciseBatchGenerationService (some sub-types route through
    the Resources-with-AI handler, losing LessonId even when
    triggered from the Lesson page)
        ▼
Exercise (ReviewStatus: PendingReview → Approved/Rejected)
   ── ExerciseResourceLink → ResourceBankItem
   ── Exercise.LessonId (nullable scalar, no join entity)
        │
        │  ModuleAutoLinkService: create-or-extend exactly ONE Module per Lesson
        ▼
Module (ReviewStatus: PendingReview → Approved/Rejected; IsArchived)
   ── ModuleLessonLink (N:M capable, 1:1-in-practice today)
   ── ModuleExerciseLink (N:M capable, not exercised across Lessons today)
   ── FeedbackPlanJson (static text, no runtime wiring)
        │
        │  NO publish step. NO catalogue. NO version.
        │  "Approved" re-checked ad hoc, independently, in 3+ places:
        ▼
PracticeGymModuleSelectionService / TodayPlanModuleSelectionService
  (both filter ReviewStatus==Approved + linked Lesson/Exercise Approved,
   NEITHER checks Module.IsArchived  ⚠ GAP)
        ▼
StudentPracticeGymModuleAssignment / StudentTodayPlanModuleAssignment
        ▼
ExerciseLaunchService.LaunchAsync (re-checks Approved, still not IsArchived)
        ▼
LearningActivity (materialized copy of Exercise.FormSchemaJson)
        │
        ▼
ActivityAttempt (student submission/scoring)


  ── separate, unmigrated legacy system, still live ──
LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity
  (older per-student runtime hierarchy; ActivityMaterializationJob/CompleteModuleHandler
   never reference Module/AdminReviewStatus at all — fully parallel to the above)
```

## Intended pipeline diagram (for comparison)

```
Import
  (choose Source + type, submit content, "Import as candidates")
        │
        ▼  AI analyses & UNDERSTANDS content, SPLITS into structured candidates,
        │  maps to Resource type, VALIDATES, flags DUPLICATES + issues
        ▼
Admin reviews EVERY candidate → edits / approves / rejects / skips
        │
        │  ONLY approved candidates publish
        ▼
Resource Bank  (approved content only, reusable, never assigned to students directly)
        │
        │  admin creates/generates Lesson from one or more Resources
        ▼
Lesson  (teaches ONE concept, retains source links, reviewed before use)
        │
        │  admin creates Exercises FROM Lessons (implicitly: not directly from Resources)
        ▼
Exercise  (reusable practice task, one Lesson → many Exercises, stays linked to Lesson)
        │
        │  combine Lessons + their Exercises
        ▼
Module  (Learn → Practice → Feedback flow; reviewed before publication)
        │
        │  PUBLISH (distinct step from approval)
        ▼
Published Module Catalogue  (only published Modules assignable; versioned)
        │
        ▼
Student assignment
```

---

## Stage-by-stage analysis

### Stage 1 — Import

**Current implementation.** Two entry points converge on one pipeline: `AdminContentImportController` (paste text/CSV/JSON) and `AdminResourceImportController` (raw file upload). Both stage rows into `ResourceCandidate` via `ResourceImportService.ImportAsync`, gated by three deterministic checks (within-run hash dedup, English-only script heuristic, "does this row have a recognizable content field" check). An admin can then run `ResourceCandidateAnalysisService.AnalyzeAsync` (AI) per candidate, edit only its `AdminNotes`, approve/reject via `AdminReviewStatus`, and finally run a **separate** publish action (`ResourceCandidatePublishService`) that is the sole writer of `ResourceBankItem` rows.

**Where it differs from intended.**
- **"AI analyses content and splits it into candidates"** does not happen. For pasted free text, splitting is `content.Split('\n')` — one candidate per non-blank line, no AI involved. For structured files, it's a deterministic 1-row-in-1-candidate-out mapper (`InferCandidateType` is field-name pattern matching). AI (`resource_candidate_analyze` prompt) only classifies/enriches a candidate that already exists — CEFR level, tags, quality score, `needsHumanReview` flag — explicitly documented as "advisory only... never itself decides validation status." A second, narrower AI use (`IResourceImportColumnMappingService`) only suggests CSV/JSON column-header mappings.
- **No "Skip" state.** The candidate lifecycle has Approve/Reject only; an un-actioned candidate just sits `PendingReview` indefinitely — functionally equivalent to skip, but not a modeled state.
- **Candidates are not really editable.** Only `AdminNotes` can be changed. Any content correction requires re-import, not in-place editing — a real gap against "administrator... edits candidates."

**Where intended behaviour already exists / exceeds intent.**
- Duplicate detection is real and multi-layered (SHA-256 within-run hash + `IActivityContentFingerprintService` fingerprinting checked across four scopes: within-run, within-source, global-candidate-table, and already-published `ResourceBankItem`), all advisory (flags `NeedsReview`, never silently auto-rejects/removes) — this is more sophisticated than a bare uniqueness constraint.
- Import never creates Lessons/Exercises/Modules — this boundary is respected and confirmed by a repo-wide grep (only `ResourceCandidatePublishService` constructs `ResourceBankItem`).
- Full import history/audit trail exists (`ResourceImportRun`: who, when, file hash, parser version, success/reject/warning counts), plus per-candidate publish provenance (`PublishedAtUtc`/`PublishedByUserId`).
- Publish re-validates every gate live at publish time rather than trusting stale approval state — a defensive design choice not explicitly required by the intended spec but a good practice.

**Missing.** True AI content-understanding/segmentation (explicitly disclaimed in a code comment as future work). Candidate content editing beyond notes. A "skip" state. `ActivityTemplateCandidate` and `Unknown` candidate types are permanently blocked at publish (by design, not a bug) — meaning not all 7 Resource Bank types are import-reachable for that specific candidate shape (though the 7 `PublishedResourceType`s themselves are otherwise supported for Vocabulary/Grammar/Writing/Speaking fully, Reading dual-routed by length, Listening gated on a separately-uploaded audio file).

**More/less advanced than intended.** Less advanced on the "AI understands and splits content" promise (this is the single largest gap in the whole audit). More advanced than intended on duplicate detection and audit-trail depth.

---

### Stage 2 — Resource Bank

**Current implementation.** `ResourceBankItem` is a single physical table (post-Phase-I0 consolidation from four typed tables) covering all 7 `PublishedResourceType`s via a polymorphic `ContentJson` payload. It carries no review/approval field of its own — publishing (Stage 1's final action) *is* the approval event; post-publish, only `IsArchived` (soft-delete) and `UpdateContent` (in-place correction) exist as lifecycle operations.

**Where it differs from intended.** The intended design implies Resource Bank rows are "approved content" as a durable state on the resource itself; in practice, approval lives entirely upstream on `ResourceCandidate` and a `ResourceBankItem` has no way to represent "this published resource is now under re-review" — editing it post-publish is unreviewed (any admin with edit rights can silently change live, reusable content with no approval gate on the edit itself).

**Where intended behaviour already exists.** Resources are never assigned directly to students — confirmed by the absence of any `StudentResource*` entity and explicit doc comments stating Lesson generation "never assigns anything to a student." Reuse is modeled at the schema level (`LessonResourceLink` keyed by `(ResourceType, ResourceId)`, no uniqueness constraint, indexed for many-to-many lookups).

**Missing / partially implemented.** Reuse is *possible* but not *observable* — no admin UI or query surfaces "this resource is used in N Lessons," and the only "Generate Lesson" entry point (Resource Bank detail page) passes a single resource per generation action, so in practice resources are used roughly 1-per-Lesson today even though the schema supports many. No review workflow exists for post-publish edits (see above).

**More/less advanced than intended.** Roughly matches intent on the core lifecycle; slightly behind intent on "reusable" being demonstrably exercised rather than just schema-permitted.

---

### Stage 3 — Lesson

**Current implementation.** `Lesson` is a fully-fledged entity with a real review/approval state machine (`AdminReviewStatus`: PendingReview → Approved/Rejected, re-opens to PendingReview on edit-after-rejection, blocks edits once Approved). Two generation paths exist: `LessonGenerationService` (deterministic — concatenates linked resources' title+body) and `AiLessonGenerationService` (AI-authored teaching prose from the same resources). Both create `LessonResourceLink` rows recording provenance, both always start `PendingReview`.

**Where it differs from intended.** Nothing structurally significant — this stage is close to spec. One soft deviation: "teaches a single learning concept" is a documented *intent* (in a doc comment) but is **not code-enforced** — a Lesson can link resources of any mixed type/topic with no validation preventing concept-sprawl; scoping is left entirely to admin judgment.

**Where intended behaviour already exists.** Generation from one-or-more Resources ✓. Source links retained and queryable (`LessonResourceLink`, surfaced in the returned DTO) ✓. Review before use ✓ (full Approve/Reject workflow with reasons and re-submission). "Learn Item" is confirmed dead/legacy terminology — Phase I4 renamed it to Lesson across the codebase; "LearnItem" only survives in old migration class names and historical docs, not in any live code path.

**Missing.** Not all Resource types can seed a Lesson yet — an admin component comment (`admin-resource-bank-detail.component.ts`) explicitly states `LessonResourceLookup` "doesn't read Writing/Listening/Speaking yet" for the Lesson-generation entry point (worth noting this is likely stale relative to later Phase K17 work that *did* extend `LessonResourceLookup` for Writing/Listening/Speaking per this session's own prior work — see Architectural Inconsistencies below for the discrepancy this implies). No explicit code-level gate was confirmed requiring `Lesson.ReviewStatus == Approved` before that Lesson can be used to generate Exercises (see Exercise stage — this deserves direct verification, flagged as an open question below).

**More/less advanced than intended.** Matches intent closely; this is one of the best-implemented stages in the audit.

---

### Stage 4 — Exercise

**Current implementation.** `Exercise` is a rich entity (Form.io schema + answer key + scoring rules + feedback plan + full review state machine, identical shape to Lesson's). Two generation entry points exist as separate interfaces implemented by one class: `IGenerateActivityFromResourcesHandler` (Resource Bank → Exercise **directly**, no Lesson involved — `Exercise.LessonId` stays null) and `IGenerateActivityFromLessonHandler` (Lesson → Exercise, `Exercise.LessonId` populated). A third, AI-assisted variant exists for the resources-only path.

**Where it differs from intended.** This is the most significant *structural* deviation from the intended design: the intended pipeline states Exercises are "created from Lessons," implying Lesson is the sole source. The codebase instead supports (and fully implements, API-complete) a **direct Resource→Exercise path that bypasses Lesson entirely**. In practice today the admin UI only exercises the Lesson-sourced path (the direct-from-resources UI wiring exists in the Angular service layer but no component calls it) — so *current behaviour* matches intent, but the *architecture* already diverges and the unused capability is one UI change away from breaking the "Exercises come from Lessons" invariant.

A second, subtler deviation: inside the Lesson-sourced batch-generation flow itself, certain AI-preferred exercise types (multiple-choice variants) are silently routed through the resources-only AI handler even when triggered from the Lesson detail page — and that handler hardcodes `lessonId: null`. So some Exercises generated *from the Lesson UI* still end up with `Exercise.LessonId == null`, with their only link back to the Lesson being indirect (shared `ExerciseResourceLink` resource references). This is a real provenance gap against "Exercises remain linked to their Lesson."

**Where intended behaviour already exists.** One Lesson → many Exercises works end-to-end (no unique constraint, batch generation explicitly loops, capped at 50 per call) ✓. Exercises retain a link to their Lesson via a direct nullable scalar FK (not a join table) ✓ (when populated — see gap above). Full review/approval workflow, matching Lesson's shape ✓. `ExerciseResourceLink` independently traces provenance to Resources (structurally identical to `LessonResourceLink`) — this exceeds intent by giving Exercises *two* independent provenance chains (to Lesson, and to Resources) rather than only through Lesson.

**Missing / partially implemented.** No FK between `Exercise.ActivityType` (free text) and `ExerciseTypeDefinition.Key` — these are two parallel systems joined only by string convention, requiring manual sync. Cross-Module Exercise reuse is schema-possible (`ModuleExerciseLink` has no unique constraint) but never exercised by the only writer (`ModuleAutoLinkService`, which is strictly one-Module-per-Lesson).

**More/less advanced than intended.** More advanced in some respects (dual provenance, richer generation options); less faithful to the specific "Exercises come from Lessons" constraint since a same-page bypass exists both at the API layer (unused direct-resources handler) and inside the Lesson flow itself (the AI-preferred-type sub-path losing `LessonId`).

---

### Stage 5 — Module

**Current implementation.** `Module` (Phase H5) is a genuinely new, reviewable, design-time entity — explicitly documented in its own doc comment as distinct from the older `LearningModule` runtime entity. It has the same `AdminReviewStatus` shape as Lesson/Exercise, plus `IsArchived`. `ModuleLessonLink` and `ModuleExerciseLink` join tables support N:M Lesson/Exercise membership at the schema level (no unique constraints).

**Where it differs from intended.** The "Learn → Practice → Feedback" structure exists only as a **static composition**, not a runtime stage machine: Learn = linked Lessons, Practice = linked Exercises, Feedback = a single static `FeedbackPlanJson` string explicitly documented as "never sent to students... no runtime wiring exists yet." There is no `ModuleStage` entity or student-facing sequencing through these three phases as a distinct in-Module flow — a superficially similarly-named `ModuleStageContent`/`StageContentDto` type family exists but is unrelated (it shapes a single Exercise/`LearningActivity`'s AI-generated content envelope, not the Module's own stage progression). This is confirmed by the architecture doc itself, which pre-H5 stated plainly that nothing packaged a Learn+Practice+Feedback unit — H5 built the packaging (the links + a text field) but not the runtime flow.

**Where intended behaviour already exists.** Real review/approval workflow (Approve/Reject/Archive, full admin API+UI) ✓. Modules combine one-or-more Lessons and their Exercises ✓ (schema-level N:M, `ModuleAutoLinkService` in practice builds exactly one Module per Lesson so real-world usage is closer to 1:1-cardinality-per-Lesson than the schema permits).

**Missing.** No versioning at all — no version field, no mechanism to have an old Module version live for already-assigned students while a new version is in review. **No distinct "publish" action or Published Module Catalogue concept** — `AdminReviewStatus.Approved` is the sole and only gate, checked independently and ad hoc in at least three separate consumer call sites (`PracticeGymModuleSelectionService`, `TodayPlanModuleSelectionService`, `ExerciseLaunchService`) rather than through one centralized "what's published" query/service.

**A concrete gap, not just an absence:** neither student-facing selection service (`PracticeGymModuleSelectionService`, `TodayPlanModuleSelectionService`) nor the launch-time re-check (`ExerciseLaunchService`) filters on `Module.IsArchived`. Only admin-facing list/repair queries do. This means an **Approved-but-archived Module can still be suggested to students and successfully launched** — a genuine, reproducible leak against the intended "only Published Modules become available for student assignment" principle (archiving was presumably intended by an admin as a form of un-publishing, and it does not behave that way for *new* suggestions, only for already-existing assignments where it's explicitly documented as intentional).

**More/less advanced than intended.** Less advanced specifically on publish/version/catalogue — this stage has the review half of the intended design but not the publish half.

---

### Stage 6 — Published Module Catalogue

**Current implementation.** Does not exist as a distinct entity, table, or endpoint. "What's published" is inferred, not modeled — it's `Module.ReviewStatus == Approved`, re-derived independently in each of the ≥3 consumer services listed above, each also independently re-checking that linked Lessons/Exercises are individually Approved.

**Where it differs from intended.** Entirely — the intended design calls for approval and publication to be two distinct steps/states, with the catalogue as a queryable, student-facing "what's actually available" surface, ideally versioned. None of that exists. This is the single largest structural gap in the Module/Catalogue side of the audit, mirroring the Import stage's AI-splitting gap as the other major missing piece of the intended architecture.

**Where intended behaviour partially exists.** The *effect* of gating unapproved content from students is achieved — both selection services and the launch-time re-check do correctly filter on `ReviewStatus == Approved` for Module and its linked Lesson/Exercise. So the *security/correctness* property ("students can't receive unreviewed content") mostly holds, modulo the `IsArchived` leak noted above — it's the *architecture* (single source of truth, versioning, explicit publish semantics) that's missing, not primarily student-facing safety (except for the one confirmed leak).

**Missing.** A `ModuleCatalogue`/`PublishedModule` concept. A `Publish()`/`Unpublish()` action distinct from `Approve()`. Versioning of any kind. A single shared query/service that all consumers use instead of re-deriving "what's published" independently.

**More/less advanced than intended.** Substantially less advanced — this stage is the least-built relative to the intended design, alongside Import's AI-splitting gap.

---

## Entity mapping (current vs. intended)

| Intended concept | Current entity/entities | Match quality |
|---|---|---|
| Import candidate | `ResourceCandidate` (+ `ResourceRawRecord`, `ResourceImportRun`) | Strong structurally; weak on AI-splitting behaviour |
| Resource (approved) | `ResourceBankItem` | Strong |
| Lesson | `Lesson` | Strong — closely matches intent |
| Lesson↔Resource link | `LessonResourceLink` | Strong |
| Exercise | `Exercise` | Strong entity; generation-path architecture diverges (see Stage 4) |
| Exercise↔Resource link | `ExerciseResourceLink` | Exists, but implies a second provenance path the intended design doesn't have |
| Exercise↔Lesson link | `Exercise.LessonId` (nullable scalar) | Weaker than intended — not always populated even when Lesson-sourced |
| Module | `Module` | Strong entity; publish/version/catalogue half missing |
| Module↔Lesson/Exercise links | `ModuleLessonLink`, `ModuleExerciseLink` | Strong structurally, underused (1 Module : 1 Lesson in practice) |
| Learn/Practice/Feedback | `ModuleLessonLink`+`ModuleExerciseLink`+`FeedbackPlanJson` | Static composition only, no runtime stage machine |
| Published Module Catalogue | **None** — `Module.ReviewStatus == Approved`, re-derived ad hoc | Missing |
| (not in intended design) | `LearningModule`/`LearningPath`/`LearningSession` runtime hierarchy | Legacy parallel system, pre-dates `Module`, not unified with it |
| (not in intended design) | `ExerciseTypeDefinition` catalog | Parallel metadata system, joined to `Exercise` only by string convention |
| (not in intended design) | `ExercisePatternDefinition`, `Exercise.PatternKey` | Remnant of a deleted legacy "Pattern Engine" generation system |

---

## Gap analysis table

| Stage | Intended Design | Current Implementation | Gap | Severity | Recommendation |
|---|---|---|---|---|---|
| Import | AI analyses raw content and splits it into structured, type-mapped candidates | Pasted text is split by newline only; structured files map 1 row → 1 candidate deterministically; AI only classifies/enriches an already-staged candidate | No AI-driven content understanding/segmentation exists | High | Consider whether "Import" should be renamed/rescoped to match current behaviour, or whether true AI segmentation is still a target for a future phase |
| Import | Admin can edit a candidate before approval | Only `AdminNotes` is editable; no content-field editing | Editing capability is narrower than intended | Medium | Clarify whether content-editing is in scope, or whether "re-import to correct" is the accepted workflow |
| Import | Admin can skip a candidate | No modeled "Skip" state (un-actioned = de facto skip) | Terminology/state gap, low functional impact | Low | Low priority — behaviourally close enough |
| Resource Bank | Resources are approved content only | Publishing is the approval event; post-publish `UpdateContent` edits are unreviewed | Post-publish edits bypass any review gate | Medium | Consider whether post-publish edits should re-enter a review state |
| Resource Bank | Resources are reusable across Lessons | Schema supports N:M reuse; no UI/query demonstrates or tracks actual reuse | Reuse is possible but invisible/unverified in practice | Low | Consider surfacing "used in N Lessons" if reuse is a product priority |
| Lesson | Lesson teaches a single concept | Not code-enforced; multiple resources of mixed type/topic can be linked freely | Concept-scoping relies entirely on admin discipline | Low–Medium | Flag as a design question, not necessarily a defect |
| Exercise | Exercises are created from Lessons | A parallel, fully-built direct Resource→Exercise generation path exists and is reachable via API/service layer (unused by current UI) | Architecture permits bypassing Lesson; current UI usage happens to match intent, but is one component change away from violating it | Medium–High | Decide whether the direct-from-resources path should be deprecated, hidden, or formally endorsed as a supported alternative |
| Exercise | Exercises remain linked to their Lesson | Some Exercises generated from the Lesson-detail UI itself (AI-preferred multiple-choice types) end up with `LessonId == null` due to a sub-path hardcoding `lessonId: null` | Provenance gap even within the "intended" flow | Medium | Worth a direct fix-scoping conversation — this affects data correctness, not just architecture purity |
| Module | Module represents Learn → Practice → Feedback as a real flow | Only a static composition (linked Lessons + linked Exercises + one text field); no runtime stage sequencing exists | Feedback stage has no runtime wiring at all | Medium | Clarify whether Learn/Practice/Feedback needs to become a real runtime construct, or remains a compositional/organizational grouping only |
| Module | Modules are reviewed before publication | Full review workflow exists (Approve/Reject/Archive) | None — matches intent for the review half | — | — |
| Module → Catalogue | Distinct publish step, separate from approval | No publish action exists; `Approved` alone is treated as "published" | Publish/approval conflation | High | Clarify whether a distinct publish step is still required, since Approved-gating already achieves the safety property in most paths |
| Published Module Catalogue | Only published Modules are assignable | `ReviewStatus == Approved` re-checked independently in 3+ places; **`IsArchived` is never checked in the student-facing selection/launch paths** | An Approved-but-archived Module can leak to students | **High (confirmed leak, not just architectural gap)** | This is the one item in this audit that behaves like a live correctness bug rather than a pure architecture gap — worth prioritizing regardless of the broader catalogue question |
| Published Module Catalogue | Versioning exists | No version field/mechanism anywhere on `Module` | No versioning at all | Medium | Assess whether versioning is near-term needed given no current mechanism to safely edit an in-flight Module |
| Published Module Catalogue | Single queryable catalogue | "What's published" logic duplicated across ≥3 independent call sites | No single source of truth for "what's published" | Medium | Consider consolidating into one shared query/service, independent of whether a formal "catalogue" entity is introduced |

---

## Architectural inconsistencies

- **Two unrelated "Module" concepts coexist without unification**: `Module` (new, bank-first, reviewable) and `LearningModule` (older, per-student runtime, part of the pre-existing `LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity` hierarchy). They are not materialized into each other; `ExerciseLaunchService` bypasses `LearningModule`/`LearningPath` entirely and writes straight to `LearningActivity`. Both systems are live simultaneously. This is explicitly acknowledged in `docs/architecture/product-model-realignment-h0.md` as intentional naming-collision-avoidance during a phased migration, not an accident — but it means anyone reading "Module" in code or docs must determine which system is meant from context.
- **Two unrelated "Stage" concepts share overlapping vocabulary**: Module's conceptual Learn→Practice→Feedback grouping vs. the `ModuleStageContent`/`StageContentDto`/`LearnContentDto`/`PracticeContentDto`/`FeedbackPlanDto` type family, which actually shapes a single Exercise/`LearningActivity`'s AI-generated content envelope (used by `ActivityGetHandler`), unrelated to the Module entity at all despite the shared "Module"/"Learn"/"Practice"/"Feedback" naming. This is a real source of confusion for anyone navigating the codebase by keyword search.
- **Two parallel exercise-type systems joined only by string convention**: `ExerciseTypeDefinition` (a metadata/config catalog governing what's offered in the UI and validating item counts) and `ActivityGenerationService`'s hardcoded `ActivityType*` constants (which govern what's actually composable). There is no FK between `Exercise.ActivityType` and `ExerciseTypeDefinition.Key` — the two must be kept in sync by convention, which is exactly the kind of drift risk that caused this session's own earlier work (converting catalog rows between "Ready" and "BankFirst") to require careful manual verification.
- **Legacy "Pattern Engine" remnants persist alongside the new bank-first system**: `ExercisePatternDefinition` (still a live entity, used elsewhere e.g. `DynamicPatternSelection.cs`) and `Exercise.PatternKey`/`ExerciseTypeDefinition.LegacyActivityType`/`.ExercisePatternKey` are explicit legacy pointers kept for backward compatibility. A separate, larger deletion (Phase I2A/I2B, documented in `docs/reviews/2026-07-10-phase-i2a-*`/`i2b-*`) already removed the bulk of the old on-demand AI generation pipeline (`ActivityTemplate`, `PracticeActivityCache`, `PracticeGymGenerationJob`, `GET api/activity/next`), but inert historical columns (`SourceTemplateId` on `ActivityFeedbackSignal`/`StudentActivityReadinessItem`/`StudentActivityUsageLog`) and a cosmetic reporting bug (`PracticeGymSuggestionsDto.ReadyCount`/etc. reporting on a now-empty readiness pool) were explicitly deferred rather than cleaned up.
- **A second, older Practice Gym suggestion path may still exist in parallel** with the new `PracticeGymModuleSelectionService` — the H0 architecture doc flags that Practice Gym v2 "currently scoped selects an ActivityTemplate/resource/format directly, not a Module," suggesting the unification toward Module-based selection isn't fully complete even where Module-based selection has been built.
- **Conflicting terminology, resolved but not fully cleaned up**: "LearnItem" → "Lesson" (Phase I4 rename) is complete in live code but the rename left the original migration class name (`Phase_H3_AddLearnItemFoundation`) and historical docs unchanged — not a functional issue, but a source of confusion when reading migration history or grepping old docs.
- **Stale doc comments describing since-superseded behaviour**: `Exercise.GenerationProvider`'s doc comment still says AI generation is "not implemented this phase," despite `AiExerciseGenerationService`/`AiLessonGenerationService` existing and being wired in. Similarly, an admin component comment claims `LessonResourceLookup` doesn't support Writing/Listening/Speaking resources for Lesson generation — this directly conflicts with this session's own prior work in this conversation, which found and used a `LessonResourceLookup.FindAsync` switch case for exactly those three types (added, per that code's own comment, in "Phase K17"). This is either a genuinely stale comment that should be corrected, or (less likely) evidence of two different `LessonResourceLookup`-adjacent code paths with different capabilities — worth a direct verification pass, flagged below as an open question.

---

## Risks and technical debt

1. **Confirmed leak, not just a gap**: `Module.IsArchived` is never checked by the student-facing selection or launch paths. If any admin workflow archives an Approved Module expecting it to stop being offered to new students, that expectation is silently violated. This is the single most concrete, actionable finding in this audit.
2. **Provenance can silently degrade inside the "intended" happy path**: Exercises generated from the Lesson-detail "Generate Exercises" UI can still end up with `LessonId == null` for certain AI-preferred types, meaning even a workflow that looks fully spec-compliant from the UI produces data that doesn't structurally confirm Lesson provenance — anyone later querying "all Exercises belonging to Lesson X" by `LessonId` alone will undercount.
3. **Unused-but-live API surface as a latent architecture risk**: the direct Resource→Exercise generation handler is fully implemented, reachable, and untested-in-anger by any UI — a future frontend change that wires it up would silently reintroduce the "Exercises can bypass Lessons" behaviour without anyone deciding that as a product choice.
4. **No versioning anywhere in the review-gated content chain** (Lesson, Exercise, Module all lack it) — combined with "editing blocked once Approved, editing forces re-review," this means there is no way to publish a correction to already-assigned content without either leaving the correction unreviewed (Resource Bank's `UpdateContent`) or briefly making the content unavailable to new assignments while re-review completes (Lesson/Exercise/Module).
5. **Duplicated "what's published" logic** across ≥3 call sites is a maintainability risk independent of whether a formal catalogue is ever built — a future change to the gating rule (e.g. adding a new required check) has to be replicated correctly in every consumer, and this audit already found one consumer (`Module.IsArchived`) where that replication silently failed.
6. **Two coexisting Module systems and two coexisting exercise-generation pipelines** raise the cost of onboarding and of AI-agent-assisted changes (like this session's own work) — any change touching "Module" or "Exercise type" requires first determining which of the parallel systems is actually in scope.

---

## Questions or ambiguities discovered during the audit

1. Is the direct Resource→Exercise generation path (`IGenerateActivityFromResourcesHandler`) intentionally kept as a supported alternative to Lesson-sourced generation, or is it vestigial from an earlier design iteration that predates the "Exercises come from Lessons" framing? The audit found it fully built and API-exposed but functionally unused by any current UI.
2. Is `Module.IsArchived` intended to affect *new* student suggestions (making the archived-but-approved leak a genuine bug) or only to leave *existing* assignments intact while blocking new ones (in which case the current behaviour is half-right — it correctly preserves existing assignments but incorrectly also permits new ones)? The in-code doc comment only speaks to the "don't break existing assignments" half.
3. Is `LessonResourceLookup`'s support for Writing/Listening/Speaking resources actually complete today (as this session's own prior conversation confirmed adding in "Phase K17"), or does the stale-looking Angular comment reflect a genuinely separate/still-limited code path for Lesson generation specifically (as opposed to Exercise generation, which this session's prior work focused on)? This should be verified directly rather than assumed either way.
4. Is a formal "Publish" step (distinct from "Approve") still a product requirement for Module, given that the `Approved` gate already achieves the practical safety property in every consumer except the one archived-Module leak found here? Understanding whether the gap is purely architectural (naming/separation of concerns) or also functional (something Approved-alone genuinely cannot express, e.g. "approved but not yet ready to publish this week") would change how urgent this gap is.
5. Is cross-Module Exercise/Lesson reuse (schema-permitted N:M, but never exercised by `ModuleAutoLinkService`'s strict one-Module-per-Lesson behaviour) an intended future capability, or is the "one Module per Lesson" behaviour actually the intended long-term shape, making the N:M schema over-provisioned for a 1:1 reality?
6. Should Import's candidate-editing scope be expanded beyond `AdminNotes`, or is "re-import to correct" the deliberately chosen workflow (avoiding a second editing surface for content that will itself become editable once published as a `ResourceBankItem` via `UpdateContent`)?

---

## Overall assessment

The middle of the pipeline — Resource Bank lifecycle, Lesson generation and review, Exercise generation and review, Module review — is implemented to a genuinely high standard, in several places (dual provenance links, real duplicate fingerprinting, defensive re-validation at publish time) exceeding what the intended design strictly requires. The two edges of the pipeline are where the implementation diverges most from intent: **Import's "AI understands and splits content" promise is not built** (AI only enriches pre-existing candidates), and **the Published Module Catalogue as a distinct, versioned, single-source-of-truth concept does not exist** — "Approved" alone carries the full weight of "published," re-derived independently across multiple consumers, with one confirmed leak (`IsArchived` not gating student-facing selection) as a result of that duplication.

Structurally, the codebase is not confused about the target architecture — its own internal documentation (`product-model-realignment-h0.md`, the Phase I2A/I2B deletion reviews, the Phase I4 rename doc) shows a team that understands the gap between the legacy system and the intended bank-first pipeline, and has been closing it phase by phase. What remains open (the Import AI gap, the Catalogue/versioning gap, and the archived-Module leak) reads as deliberately sequenced future work rather than oversight — but the archived-Module leak in particular is worth treating as a near-term fix candidate rather than pure architecture backlog, since it's a live behavioural gap against the intended safety guarantee, not just a missing concept.

---

## Phase 1 follow-up (2026-07-15)

The two confirmed correctness findings from this audit (`IsArchived` not gating student-facing
Module selection/launch, and Lesson-generated Exercises losing `LessonId` for AI-preferred types)
were fixed in a narrowly scoped follow-up phase, without acting on any of the broader
architecture/versioning/catalogue findings above. See
`docs/reviews/2026-07-15-phase-1-pipeline-safety-data-integrity-fixes.md` for the fix, root-cause
detail, tests, and validation.
