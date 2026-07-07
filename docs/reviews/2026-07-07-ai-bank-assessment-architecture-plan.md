# AI Bank-First Teaching Architecture — Assessment & Plan

**Date:** 2026-07-07
**Related sprint/feature:** Long-term AI teaching architecture (post Form.io placement/onboarding phase)
**Type:** Architecture evaluation / phased roadmap (planning only — no code changed)
**Files reviewed:** see per-section citations below (Domain entities, Infrastructure services, Persistence seeders/migrations, Angular Form.io components, Admin pages)
**HEAD at start of review:** `a4b42d1722be760ad625b1cbb3d46e2d346b28e7` (clean tree)

---

## 1. Current-state findings

### 1.1 Placement / Form.io (native, adaptive, backend-scored)

- `PlacementItemDefinition` (`Domain/Entities/PlacementItemDefinition.cs`) is the item bank row: `Skill`, `CefrLevel`, `ItemOrder`, `IsEnabled`, `FormIoSchemaJson` (student-safe), `ScoringRulesJson` (backend-only), `ScoringRulesVersion`, `RendererKind` (`FormIo` only today), `AuthoringSchemaJson` (admin builder schema with inline quiz annotations, split server-side by `IFormIoQuizSchemaSplitter`). Legacy `ItemType/Prompt/CorrectAnswer/ReadingPassage/ListeningAudioScript/ContentJson` were dropped in migration `20260706225124_DropPlacementItemPromptItemType` — the item bank is now Form.io-native end to end.
- Seed data: `Persistence/Seed/PlacementItemBankSeeder.cs` — 6 skills × 4 levels (A1–B2) × 3 items = 72 items, idempotent per (skill, level).
- `PlacementAssessment` / `PlacementAssessmentItem` / `PlacementSkillResult` carry adaptive state (`OverallConfidence`, `IsProvisional`, `Source`, `IsAdaptive`), per-item snapshots (`FormIoSchemaJson`, `ScoringRulesJsonSnapshot`, `ScoringRulesVersionSnapshot`, `SubmissionDataJson`, `NormalizedAnswerJson`), and per-skill evidence rollups (`EstimatedCefrLevel`, `Confidence`, `EvidenceCount`, `Strengths/Weaknesses`, `RecommendedStartingObjectiveKeys`).
- Adaptive selection: `PlacementAssessmentService` (Infrastructure/Placement) — per-skill confidence state, pass-rate + evidence-depth weighting, streak bonuses/penalties, next-item selection by level and skill scarcity, dedup via `SourceItemDefinitionId`.
- **Scoring is confirmed backend-only**: `PlacementFormIoScoringValidator` rejects scoring keys not present in the paired schema; `FormIoSchemaValidationService` rejects answer/scoring-leak keys (`correctAnswer(s)`, `score`, `rubric`, `scoringWeight`, `quiz`) in any student-facing schema; `PlacementScoringService.ScoreSubmission` reads scoring rules server-side only; an integration test asserts `ScoringRulesJson` never appears in a student DTO.
- Form.io Angular layer: `FormioBuilderComponent` (admin-only authoring) and `FormioRendererComponent` (shared — used by both student pages and admin live-preview). Custom components `audioPlayer` and `speakingResponse` already exist (commit `a4b42d17`); `speakingResponse` uploads recorded audio via `placementContext.uploadSpeakingAudio(...)` and only puts `{storageKey, mimeType, durationSeconds}` into the Form.io value — raw audio never touches the submission JSON.
- Onboarding (`StudentFlowTemplate`/`StudentFlowTemplateVersion`/`StudentFlowSubmission`) is **architecturally separate** from placement — different entities, different tables, same `IFormIoSchemaValidationService` and renderer, `FlowKind.Placement` reserved but unused.

**Assessment:** this is already a working, reasonably rigorous item bank with backend-enforced answer secrecy — the strongest existing piece of "bank" architecture in the app. Gaps (see §2) are mostly missing calibration/versioning/subskill metadata, not structural flaws.

### 1.2 Activity / lesson generation (AI-heavy, per-student, throwaway)

- `SessionGeneratorService` (deterministic, no AI) builds `LearningSession` + ordered `SessionExercise` rows from duration templates, skill profile scores, `DynamicPatternSelector`, and ledger signals. No `LearningActivity` content yet at this stage.
- `LessonBatchGenerationJob` → AI call (`AiExecutionService`, prompt `LessonBatchPlanKey`) produces session/exercise plans only (no activity content), then triggers `ActivityMaterializationJob`.
- `ActivityMaterializationJob` calls `IAiActivityGenerator.GenerateActivityContentAsync` per `SessionExercise` to generate the actual `LearningActivity.AiGeneratedContentJson`, then triggers `TtsAudioGenerationJob`.
- `PracticeGymGenerationJob` (Quartz, every 10 min) generates speculatively into `PracticeActivityCache` (per-student, pattern-keyed buffer), claimed at request time by `ActivityGetHandler`.
- AI generation goes through `IAiActivityGenerator.GenerateActivityContentAsync`, prompts request staged JSON (`schemaVersion`, `learnContent`, `practiceContent`, `feedbackPlan`); validated via `ModuleStageContentValidator`/`ValidateIsJson`, one retry, then `AiResponseValidationException` + logged `GenerationValidationFailure` row.
- **No Form.io involvement anywhere in this pipeline** — Form.io is exclusive to onboarding + placement today.
- **Reusability finding (important):** `LearningActivity` rows are strictly per-student and per-session/exercise (or per practice-cache slot). There is no cross-student content bank or template entity in this pipeline — every AI call produces a fresh, disposable JSON blob owned by one student's attempt chain. `ExercisePatternDefinition` is a reusable *prompt/config* template, not reusable *content*. This is the single biggest structural gap relative to the target "bank-first" vision.
- Readiness pool: `StudentActivityReadinessItem` with `ReadinessPoolStatus` = `Queued → Generating → Ready → Reserved → Consumed`, branching to `Failed, Expired, Stale, ReviewOnly, Skipped`. Separately gated by `AdminReviewStatus` (`NotRequired/PendingReview/Approved/Rejected`). This lifecycle is already very close to what the target "per-student generated instance" pool needs (see §4).

### 1.3 Progress / evidence / mastery / curriculum routing

- `StudentLearningEvent` is the append-only evidence record: skill, CEFR-at-event, concepts taught/practised, mistake tags, score, normalized score, `LearningEventOutcome` (`Introduced/Practised/Reviewed/Mastered/NeedsReview/Failed/Skipped`), `LearningEventSource` (`TodayLesson/PracticeGym/Placement/Manual/SpeakingEvaluation/WritingEvaluation`). **No `CurriculumObjectiveKey` field** — mastery groups events by `PatternKey ?? PrimarySkill`, a weak proxy for true per-objective evidence.
- `StudentMasteryEvaluationService` is deterministic (no AI): evidence-count + consecutive-streak + average-score thresholds → `MasteryStatus` (`InsufficientEvidence/AtRisk/NeedsPractice/NeedsReview/Mastered`), also drives readiness-pool demotion decisions (ReviewOnly/Skip/Stale/Expire).
- `CurriculumObjective` + `CurriculumObjectiveSeeder`: 34 objectives today, A1–B2 only (no C1/C2), keyed `{level}.{skill}.{topic}`, with prerequisite graph, difficulty band (1–5), review/exam-inspired flags.
- `CurriculumRoutingService` already implements a genuine routing policy: normalize CEFR → map context tags → filter runnable/non-mastered candidates → prefer plan's `PreferredObjectiveKey` → best-fit by difficulty band/order → drop a level for review/scaffold → fallback to `general_english`. `RoutingReason` enum (`Normal/Review/Scaffold/Remediation/Fallback/LearningPlan`) is solid provenance data already.
- **"Subskill" does not exist as a concept anywhere** in Domain/Application/Persistence — only a single `PrimarySkill` string + `SecondarySkillsJson` array of the *same* skill taxonomy (writing/reading/listening/speaking/vocabulary/grammar/pronunciation/fluency/confidence). Focus tags are free-text JSON with no constants class.
- `StudentLearningPlan`/`StudentLearningPlanObjective` track active/regenerating/superseded plans and per-objective status (Active/Completed/Mastered/Blocked/Deferred/Review/InProgress), feeding `PreferredObjectiveKey` into routing.

### 1.4 Assets

- Object storage abstraction **already exists**: `IFileStorageService` (Save/Read/Delete/Exists/Move/GenerateSignedUrl/GenerateKey/HealthCheck), implemented by `MinioFileStorageService` (prod), `LocalFileStorageService` (dev), `FakeFileStorageService` (tests). Doc: `docs/architecture/file-storage-minio.md`.
- `AudioAsset` entity is metadata-only (`ObjectKey`, `ContentType`, `DurationSeconds`, hashes for idempotency, provider/model, generation status) — **no binary bytes in the DB anywhere**; confirmed by grep (no `varbinary/bytea/byte[]/base64` columns for media).
- TTS generation (`ListeningAudioService` → provider → `IFileStorageService.SaveAsync`) and student speaking uploads (`speakingResponse` component → `placementContext.uploadSpeakingAudio` → temp-key/commit pattern in `AdaptivePlacementAudioService`/`SpeakingAudioService`, MIME allowlist, 50-file/student cap) both already follow the target "asset reference, not blob" pattern this task asked us to evaluate.
- **This is the most mature layer relative to the target architecture — the asset model described in the task's Section F already exists and works.** No new abstraction is needed; only extension of the existing `AssetType` enum / `AudioAsset`-style entities to cover any new asset kinds (e.g. generated images) if introduced later.

### 1.5 Admin UX

- Placement item bank admin (`admin-placement-items` + `admin-placement-item-editor`, Form.io authoring UI) — exists.
- Curriculum admin (`admin-curriculum`) — objectives CRUD, taxonomy, routing preview, CEFR coverage matrix — exists.
- Lesson/pool health (`admin-lessons`) — buffer/generation controls, `AggregatePoolHealthSummary` (failed items, students-with-failures, avg-ready-per-student) — exists.
- AI config/usage (`admin-ai-config`, `admin-ai-usage`, `admin-usage-policies`, `admin-usage-analytics`) — cost/token/provider/fallback dashboards — exists, backed by `docs/architecture/usage-governance.md`.
- Review/approval: `AdminReviewStatus` already gates `StudentActivityReadinessItem` visibility; `StudentFlowTemplateStatus` gates onboarding/placement Form.io template publishing. **A generalized "approve this bank/template item" workflow does not yet exist for a not-yet-created `ActivityTemplate` bank**, but the review-status pattern is proven and should be reused rather than reinvented.

---

## 2. Existing code areas inspected

`PlacementItemDefinition`, `PlacementAssessment`, `PlacementAssessmentItem`, `PlacementSkillResult`, `PlacementAssessmentService`, `PlacementFormIoScoringValidator`, `PlacementScoringService`, `PlacementItemBankSeeder`, `FormioBuilderComponent`, `FormioRendererComponent`, `IFormIoSchemaValidationService`/`FormIoSchemaValidationService`, `StudentFlowTemplate`/`Version`/`Submission`, `SessionGeneratorService`, `LessonBatchGenerationJob`, `ActivityMaterializationJob`, `PracticeGymGenerationJob`, `ActivityGetHandler`, `ExercisePrepareHandler`, `IAiActivityGenerator`/`AiActivityGeneratorHandler`, `IAiProvider`, `StudentActivityReadinessItem`/`ReadinessPoolStatus`/`AdminReviewStatus`, `LearningActivity`, `ActivityAttempt`, `StudentLearningEvent`/`LearningEventSource`/`LearningEventOutcome`, `StudentMasteryEvaluationService`/`MasteryStatus`, `CurriculumObjective`/`CurriculumObjectiveSeeder`, `CurriculumRoutingService`/`RoutingReason`, `CefrLevelConstants`, `CurriculumSkillConstants`, `CurriculumContextTagConstants`, `ILearningGoalContextResolver`, `IFileStorageService` + Minio/Local/Fake impls, `AudioAsset`, `speaking-response.component.ts`, `mic-recorder.ts`, `AdaptivePlacementAudioService`, `SpeakingAudioService`, `admin-placement-items*`, `admin-curriculum*`, `admin-lessons*`, `admin-ai-usage*`/`admin-ai-config*`.

---

## 3. Proposed target architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  CEFR Resource Bank (reference data — vocab/grammar/descriptors)     │
│  Used to VALIDATE and CONSTRAIN generation & bank authoring          │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ validates against
┌────────────────────────────────▼──────────────────────────────────────┐
│  Global Banks (reusable, admin-reviewed, cross-student)                │
│   • Placement Item Bank (exists — extend with calibration fields)      │
│   • Activity Template Bank (NEW — ActivityTemplate)                    │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ AI personalizes a template →
┌────────────────────────────────▼──────────────────────────────────────┐
│  AI Generation + Validation Pipeline                                   │
│   IAiActivityGenerator (exists) + template-bound prompt + schema/CEFR   │
│   validation (extends existing ModuleStageContentValidator pattern)    │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ produces
┌────────────────────────────────▼──────────────────────────────────────┐
│  Per-Student Generated Instance Pool                                   │
│   StudentActivityReadinessItem (exists) — EXTEND, don't duplicate:      │
│   add TemplateId/SourceBankItemId, FormIoSchemaSnapshot,                │
│   ScoringRulesSnapshot, ValidationStatus                                │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ reserved/consumed by student
┌────────────────────────────────▼──────────────────────────────────────┐
│  Attempt / Submission (ActivityAttempt — extend with Form.io           │
│  submission JSON + normalized answers, once module rendering moves     │
│  to Form.io)                                                            │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ writes
┌────────────────────────────────▼──────────────────────────────────────┐
│  Evidence / Mastery (StudentLearningEvent — extend with                │
│  CurriculumObjectiveKey + Subskill; StudentMasteryEvaluationService     │
│  unchanged in logic, richer grouping key)                              │
└─────────────────────────────────────────────────────────────────────┘

Assets: IFileStorageService + AssetType entities (exists, extend only).
Admin: reuse AdminReviewStatus pattern for ActivityTemplate approval.
```

The key design principle: **this is an extension of four existing subsystems (placement item bank, readiness pool, mastery/evidence, asset storage), plus one genuinely new subsystem (Activity Template Bank) and one genuinely new taxonomy field (Subskill).** It is not a rewrite.

---

## 4. Recommended data model additions/changes

### 4.1 New: `ActivityTemplate` (the missing bank)
Fields: `Key` (stable, human-readable), `Skill`, `Subskill?` (see 4.4), `CefrLevel`, `ContextTagsJson`, `FocusTagsJson`, `CurriculumObjectiveKey?`, `ActivityType`/`PatternKey`, `FormIoBaseSchemaJson` (or module-stage schema once Form.io reaches modules — see §6), `GenerationInstructions` (prompt fragment/constraints for AI personalization), `ScoringModelJson` (backend-only, mirrors `ScoringRulesJson` pattern from placement), `ValidationRulesJson`, `ReviewStatus` (reuse `AdminReviewStatus` enum), `IsPublished`, `Version`/`VersionOf` (self-referencing, mirrors `StudentFlowTemplateVersion` pattern), `EstimatedDurationSeconds`, `AssetRequirementsJson` (e.g. "needs TTS audio", "needs image prompt"). Calibration fields optional at v1 (see 4.2).

### 4.2 Extend `PlacementItemDefinition` (calibration gaps identified by task)
Add: `Subskill?`, `DifficultyBand` (align with `CurriculumObjective.DifficultyBand` 1–5 scale, currently placement only has `CefrLevel`), `DiscriminationIndex?`/`CalibrationSampleSize?` (nullable — populate later from attempt statistics, don't block on it now), `EvidenceWeight` (default 1.0, lets some items count more toward skill confidence), `ReviewStatus` (reuse `AdminReviewStatus`, currently only `IsEnabled` bool exists — no review workflow on the bank itself), `Version`/supersedes-chain (currently only `ScoringRulesVersion` exists, not a full item version).

### 4.3 Extend `StudentActivityReadinessItem` (do NOT create a parallel `StudentActivityInstance`)
The task's proposed `StudentActivityInstance`/`StudentGeneratedActivity` entity would duplicate a lifecycle that already exists almost exactly (`ReadinessPoolStatus` already covers Queued/Generating/Ready/Reserved/Consumed/Expired/Failed/Stale/ReviewOnly/Skipped — a superset of the task's proposed status list). Instead, add fields:
- `SourceTemplateId?` (FK to `ActivityTemplate`)
- `SourceBankItemId?` (FK to `PlacementItemDefinition`, for bank-derived non-placement activities)
- `FormIoSchemaSnapshotJson?` / `ScoringRulesSnapshotJson?` (once activity rendering moves to Form.io — see §6; until then this stays null and `LearningActivity.AiGeneratedContentJson` is unaffected)
- `PersonalizationReason?` (why AI varied this instance from the template)
- `GeneratedByModel`/`GeneratedByProvider` (already partially covered by `GeneratedBy` — confirm/extend)
- `ValidationStatus` (Passed/Failed/NeedsReview — distinct from `AdminReviewStatus`, this is the automated-validation gate that runs before admin review)

### 4.4 New taxonomy: `Subskill`
Currently `PrimarySkill`/`SecondarySkillsJson` only reference the 9-value `CurriculumSkillConstants` list — there is no finer grain anywhere. Add a `CurriculumSubskillConstants` (or a `CurriculumSubskill` lookup entity if the list needs admin curation rather than a static const class) scoped per skill, e.g. `writing.email`, `speaking.roleplay_ordering`, `grammar.past_simple`, `listening.gist`. Add nullable `Subskill` string to: `CurriculumObjective`, `PlacementItemDefinition`, `ActivityTemplate`, `StudentActivityReadinessItem`, `StudentLearningEvent`. This is the single highest-leverage taxonomy change — almost every other entity in §4 references it optionally.

### 4.5 Extend `StudentLearningEvent` (close the evidence gap)
Add `CurriculumObjectiveKey?` (currently absent — mastery relies on `PatternKey` as a weak proxy) and `Subskill?`. This directly improves `StudentMasteryEvaluationService`'s grouping without changing its threshold logic.

### 4.6 CEFR Resource Bank (net-new subsystem, smallest first slice)
`CefrDescriptor` (level × skill × can-do statement, source-cited), `CefrVocabularyEntry` (word, level, source, license tag), `CefrGrammarProfileEntry`, `CefrReadingReference`, `CefrResourceSource` (name, license type, url, import date, usage restriction notes — e.g. "non-commercial only", "attribution required"). Every content-bearing row carries a `SourceId` FK to `CefrResourceSource` so provenance/license is always traceable per-row, not just per-import.

---

## 5. How this fits current Form.io placement/onboarding work

No conflict, no rework needed. Placement's `PlacementItemDefinition` *is* effectively phase-1 of the Activity Template Bank pattern — its `FormIoSchemaJson`/`ScoringRulesJson` split, `IFormIoSchemaValidationService` allow-listing, and backend-only scoring enforcement should be the **template** the new `ActivityTemplate` bank copies exactly, rather than a new pattern being invented. The custom `speakingResponse`/`audioPlayer` Form.io components built for placement are already reusable as-is for any future Form.io-rendered activity content — no placement-specific coupling was found in them beyond the `placementContext` upload hook, which can be generalized to an `activityContext` hook when Form.io reaches Practice/Lesson rendering.

---

## 6. How this fits current Today/Practice/Lesson generation

This is where the real gap is. Recommendation, evaluating the task's six options:

- **Reject**: one Form.io schema for an entire Today session (too coarse — breaks per-exercise adaptivity, mixes concerns, one giant submission blob is hard to score/evidence per exercise).
- **Reject for now**: Form.io for practice only, nothing for lessons (creates two rendering paths long-term with no clear reason skill-by-skill).
- **Recommend**: **one Form.io schema per module stage** (Learn / Practice / Feedback as separate schemas), matching the `ModuleStageSchema`/`StageContentDto` split that already exists in `ActivityDto`/`ExercisePrepareHandler` today. This is a rendering-layer swap, not a data-model rewrite — `LearnContentDto`/`PracticeContentDto`/`FeedbackPlanDto` already partition the content the way Form.io wizard pages would need it partitioned.
- Practice Gym activities (single-exercise, no session context) are the lowest-risk pilot surface — they already stand alone (`LearningModuleId: null`), have no multi-stage session coupling, and reuse the exact same `speakingResponse`/`audioPlayer` components proven in placement.
- Do not attempt Lesson/Today Form.io migration until the template bank + validation pipeline (§C/§E in the task) exist — otherwise Form.io schemas for lesson content would be authored ad hoc per AI call, reproducing today's non-reusability problem inside a new renderer.

---

## 7. AI vs bank responsibility boundaries

| Layer | Responsibility |
|---|---|
| AI (`IAiActivityGenerator`, prompts) | Draft activity instances from a template, personalize wording/scenario to student profile, generate variants, explain mistakes, generate hints/feedback, generate audio/image *prompts* (not the final validated schema) |
| Bank (`ActivityTemplate`, extended `PlacementItemDefinition`) | Approved pattern definitions, skill/subskill/CEFR/difficulty-band metadata, scoring model shape, reviewed reusable base content, analytics/calibration anchor |
| Backend (existing validators + services) | Validate every AI output against `ActivityTemplate.ValidationRulesJson` (extends `ModuleStageContentValidator`/`FormIoSchemaValidationService` pattern), store schema/scoring separately from student-visible payload (existing split), select the correct template via `CurriculumRoutingService` (unchanged), score/evaluate (existing `PlacementScoringService`-style separation), update evidence/mastery (existing `StudentMasteryEvaluationService`), manage assets via `IFileStorageService` (existing, unchanged) |

This boundary is already how placement works today — the task is to apply the same separation to lesson/practice content via the new template bank, not invent a new separation model.

---

## 8. Asset/object storage recommendation

No new abstraction needed. `IFileStorageService` + `AudioAsset`-style metadata entities already implement exactly what the task's Section F asked for: object storage holds bytes, DB holds `ObjectKey` references, Form.io schemas carry only opaque keys (`speakingResponse` component proves this pattern end-to-end already). The only work item is extending `AssetType` (currently audio-focused) if generated images or other media types are introduced by the Activity Template Bank later — this is a small enum + entity-field addition, not new infrastructure.

---

## 9. CEFR dataset integration recommendation

Do not import in this phase (per task constraint). Conceptual mapping only:

- **CEFR-J / Open Language Profiles** (`openlanguageprofiles/olp-en-cefrj`): maps to `CefrVocabularyEntry`/`CefrGrammarProfileEntry` rows with `SourceId` → a `CefrResourceSource` row citing OLP. License appears more permissive (academic/open-license project) but **must be verified against the actual repo LICENSE file before import** — do not assume.
- **UniversalCEFR**: maps similarly, but the task correctly flags this may carry non-commercial/research-only restrictions. **Flag explicitly: if LinguaCoach is or becomes a commercial product, UniversalCEFR content may be unusable without separate licensing — verify before any import, and prefer CEFR-J if both would satisfy a given need.**
- Every imported row must carry: source name, license type, url, import date, usage restriction text (the `CefrResourceSource` entity in §4.6) — this is non-negotiable for audit purposes given the licensing uncertainty.
- Use in generation: `ActivityTemplate.GenerationInstructions` and the validation pipeline would reference `CefrVocabularyEntry`/`CefrDescriptor` rows to constrain AI vocabulary choice per level and to validate reading-passage difficulty — this is a Phase 3+ concern, not blocking the template-bank work in Phase 4.

---

## 10. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Duplicating the readiness-pool lifecycle with a new `StudentActivityInstance` entity | Extend `StudentActivityReadinessItem` instead (§4.3) — its status enum already covers the needed states |
| AI-generated lesson content stays unvalidated/unreusable indefinitely if template bank is deprioritized | Sequence roadmap so template bank (Phase 4) lands before any Form.io lesson-rendering pilot (Phase 10) |
| CEFR dataset license violation | Explicit `CefrResourceSource` license field + do-not-import gate until legal/licensing review (§9) |
| Subskill taxonomy sprawl (uncontrolled free-text growth) | Start with a static `CurriculumSubskillConstants` per skill (mirrors existing `CurriculumSkillConstants` pattern) rather than free text; promote to an admin-curated lookup entity only if the static list proves too rigid |
| Placement-proven answer-leak protections not consistently applied to new Activity Template schemas | Reuse `IFormIoSchemaValidationService`'s allow-list/leak-key rejection verbatim for template bank schema validation — do not write a second validator |
| Scope creep: attempting Today/Lesson Form.io migration before template bank exists | Explicit phase gate in roadmap (§11) — Practice Gym pilot only, after Phase 4/5 |

---

## 11. Phased implementation roadmap

1. **Phase 1 — Codebase audit + architecture doc** — this document. Done.
2. **Phase 2 — Subskill taxonomy — IMPLEMENTED 2026-07-07.** Added `Domain/Constants/CurriculumSubskillConstants.cs` (36 subskills across the 9 `CurriculumSkillConstants` groups, plus `All`/`ForSkill`/`IsValid`/`IsValidForSkill` helpers). Added nullable `Subskill` (varchar 128) to `CurriculumObjective`, `PlacementItemDefinition`, `StudentLearningEvent`, and — since it was straightforward and directly serves the future per-student instance pool described in §4.3 — `StudentActivityReadinessItem` as well (not deferred). `CurriculumObjective`/`PlacementItemDefinition` also gained a dedicated `SetSubskill(string?)` setter (rather than folding subskill into `AdminUpdate`/`Update`) so a future admin edit that doesn't touch subskill never silently resets it. Migration `T_CurriculumSubskillTaxonomy` (additive nullable columns only, no data migration). Seed data was **not** backfilled with subskill values in this phase — `PlacementItemBankSeeder`/`CurriculumObjectiveSeeder` are large (300–600 lines) and a broad edit was judged noisy relative to the phase's scope; population is deferred to whenever admin UI or AI generation starts writing subskill values. 3213 backend tests pass (5 architecture + 1826 unit + 1382 integration), including 2 new dedicated test files (`CurriculumSubskillConstantsTests`, `PlacementItemDefinitionTests`) and additions to `CurriculumObjectiveTests`, `StudentLearningEventTests`, `StudentActivityReadinessItemTests`.
3. **Phase 3 — CEFR resource bank foundation — IMPLEMENTED 2026-07-07.** Added `CefrResourceSource` (provenance/license record with `IsImportApproved` gate and `ApproveForImport`/`RevokeApproval`/`RecordImport` — the latter throws unless approved, so no content can be marked imported without an explicit approval step), `CefrDescriptor` (level × skill × optional subskill can-do statement, ties into the Phase 2 `CurriculumSubskillConstants` taxonomy), `CefrVocabularyEntry`, `CefrGrammarProfileEntry`, `CefrReadingReference` — all FK'd to `CefrResourceSource` with `DeleteBehavior.Restrict` so a source can't be deleted while content references it. Migration `T_CefrResourceBankFoundation` (new empty tables only, no seed rows, no data import). License-review doc: `docs/architecture/cefr-resource-licensing-review.md` — flags CEFR-J as higher-priority/needs-verification and UniversalCEFR as likely non-commercial/research-only pending per-dataset confirmation. 3248 backend tests pass (5 architecture + 1858 unit + 1385 integration), including 5 new entity test files and 3 new DB round-trip/constraint tests in `DbContextMappingTests` (FK restrict-delete, unique source name, descriptor round-trip).
4. **Phase 4 — ActivityTemplate bank foundation — IMPLEMENTED 2026-07-07.** New `ActivityTemplate` entity (`Domain/Entities/ActivityTemplate.cs`): `Key`, `VersionNumber`/`PreviousVersionId` (self-referencing, `Restrict` delete), `Skill`/`Subskill`(validated via Phase 2 taxonomy)/`CefrLevel`, `ContextTagsJson`/`FocusTagsJson`, `CurriculumObjectiveKey?`, `ActivityType`/`PatternKey?`, `FormIoBaseSchemaJson?` (student-safe), `GenerationInstructions?`/`ScoringModelJson?`/`ValidationRulesJson?` (all backend-only), `ReviewStatus` (reuses `AdminReviewStatus` — `Approve()`/`Reject(reason)`/`ResetToPendingReview()`), `IsPublished` (`Publish()` throws if `Rejected`), `EstimatedDurationSeconds?`, `AssetRequirementsJson?`. Migration `T_ActivityTemplateBankFoundation` (one new table, unique index on `(key, version_number)`).
   Full admin CRUD stack mirroring the placement-item-bank pattern exactly: `Application/ActivityTemplates/ActivityTemplateBankContracts.cs` (DTOs/commands/queries/`ActivityTemplateValidationException`), 7 Infrastructure handlers (List/Get/Add/Update/Remove/Review/Publish) in `Infrastructure/ActivityTemplates/`, `AdminActivityTemplateController` at `api/admin/activity-templates` (+ `/review`, `/publish` sub-routes), DI registration in `DependencyInjection.cs`. Reuses `IFormIoSchemaValidationService` for `FormIoBaseSchemaJson` (same answer/scoring-leak-key rejection as placement) — but unlike placement items, `ScoringModelJson`/`ValidationRulesJson`/`GenerationInstructions` are plain admin-authored JSON/text fields, not derived via the Quiz-tab annotation splitter, since templates aren't necessarily single-answer quiz content.
   Full Angular admin UI: `admin-activity-templates` (list, KPI strip, skill/level/review-status filters, search, pagination — mirrors `admin-placement-items`) and `admin-activity-template-editor` (metadata form, `FormioBuilderComponent` for the base schema + preview modal, backend-only JSON/text fields via `sp-admin-textarea`, review/publish action bar). Routes added under `/admin/activity-templates` and `/admin/activity-templates/:templateId`; sidebar nav entry added (mobile drawer + desktop, both breakpoints).
   **Deferred/known limitations:** no create-new-version command exposed yet (the entity supports versioning via `VersionNumber`/`PreviousVersionId`, but `AddActivityTemplateCommand` always creates version 1 with a globally-unique `Key` — promoting an existing template to a new version is a later increment); no AI generation wiring (per phase scope — hand-authored only); Angular production build has a pre-existing bundle-size budget failure (2.55MB vs 1MB configured budget) confirmed present on `main` before this phase's changes via `git stash` — not introduced by this work, not fixed here (out of scope).
   32 new backend tests (20 domain + 12 integration endpoint), full suite 3280 passed / 0 failed (5 architecture + 1878 unit + 1397 integration).
5. **Phase 5 — AI generation validation pipeline against templates — IMPLEMENTED 2026-07-07.** Real, live-provider pipeline (Gemini, per user's local config) — not a stub. New `IActivityTemplateInstanceGenerator`/`ActivityTemplateInstanceGenerator` (`Infrastructure/ActivityTemplates/`) personalizes a template's `FormIoBaseSchemaJson` via a dedicated AI prompt (`activity_template_generate_instance`, seeded in `DefaultAiSeeder`, category `llm.generation` — reuses whatever provider/model is already configured for that category, Gemini in this environment), mirroring `AiActivityGeneratorHandler`'s exact call/retry/log shape (`AiExecutionService.ExecuteWithMetaAsync`, `GenerationValidationFailure` logging on failure, one retry, throw `AiResponseValidationException` if the retry also fails).
   Validation is real, not a placeholder: every candidate is (a) checked for valid JSON, (b) run through the *same* `IFormIoSchemaValidationService.ValidateSchema` used by placement/onboarding (component allow-list, no script/eval properties, no answer/scoring-leak keys), and (c) checked against a new `ActivityTemplateValidationRules` parser/validator reading the template's own `ValidationRulesJson` — `requiredComponentKeys` (recursively walks nested panel/columns/table components), `maxSchemaLength`, `forbiddenWords`. Malformed/absent `ValidationRulesJson` means no extra constraints, not a validation failure — the shared Form.io check still applies independently.
   Surfaced via `POST /api/admin/activity-templates/{id}/generate-preview` (400 if template/authoring fields missing, 422 `AiResponseValidationException` if validation fails after retry, 503 `AiUnavailableException` if the provider is unreachable) and a "Generate AI preview" button in the Angular editor (existing templates only) that renders the personalized schema read-only in a modal — proves the pipeline end-to-end without persisting anything (persistence into a per-student pool is Phase 6).
   15 new backend tests (10 validator unit tests + 5 integration tests against fake AI providers — well-formed success, template-not-found, missing-generation-instructions, malformed-AI-response-after-retry, missing-required-component-key-after-retry). Full suite: 3295 passed / 0 failed (5 architecture + 1888 unit + 1402 integration). No live API calls happen in the test suite — a dedicated `ActivityTemplateGenerationTestFactory` swaps in fake `IAiProvider`s per the project's existing convention (`ActivityTestFactory`).
6. **Phase 6 — Extend `StudentActivityReadinessItem` for template-sourced instances — IMPLEMENTED 2026-07-07 (after Phase 10, per the reordering decision below).** See §14 below for full detail.
7. **Phase 7 — Placement item bank calibration fields — IMPLEMENTED 2026-07-07.** See §15 below for full detail.
8. **Phase 8 — Evidence/mastery extension — IMPLEMENTED 2026-07-07.** See §16 below for full detail.
9. **Phase 9 — Admin review/approval workflow — IMPLEMENTED 2026-07-07 (roadmap complete).** See §17 below for full detail.
10. **Phase 10 — Form.io Practice Gym pilot — IMPLEMENTED 2026-07-07 (reordered ahead of Phase 6).** See §13 below for why, and full implementation detail.

### Reordering decision (2026-07-07)

Phase 6 ("extend `StudentActivityReadinessItem` for template-sourced instances, wire `PracticeGymGenerationJob` feature-flagged") was attempted next per the original roadmap order. Mid-implementation this surfaced a real format collision: `ActivityTemplate.FormIoBaseSchemaJson` is a Form.io schema, but `LearningActivity.AiGeneratedContentJson` (what Practice Gym renders today) is a completely different shape (`ModuleStageSchema` — Learn/Practice/Feedback JSON). Wiring templates into `PracticeGymGenerationJob` without Practice Gym being able to render Form.io content would produce content with nowhere to go — exactly the collision the original roadmap's Phase 10 gate existed to prevent. Decision (user-confirmed): do Phase 10 first, then return to Phase 6 with a working Form.io rendering path already in place. Phase 6 as originally scoped (`StudentActivityReadinessItem.SourceTemplateId`/`ValidationStatus`/`PersonalizationReason` fields) is **not yet done** — it is next after this entry.

## 13. Phase 10 — Form.io Practice Gym pilot (implementation detail)

**Scope:** a single, dedicated, double-gated pilot exercise pattern (`formio_practice_gym_pilot`) that personalizes an approved `ActivityTemplate`'s Form.io schema via the Phase 5 generator and renders/scores it natively in Practice Gym — proving the end-to-end bank → AI → Form.io → deterministic-scoring chain without touching any of the ~37 existing exercise types/patterns.

**Safety gating (defense in depth — all three must be true for this to ever run for a real student):**
1. `ExerciseTypeDefinition.ImplementationStatus = "planned"` (not "ready") — blocks both `PracticeGymBufferRefillJob` queueing and `PracticeGymGenerationJob` materialization entirely. Admin must explicitly promote to "ready" via the existing Exercise Types admin page.
2. Feature flag `PracticeGymFormIoPilot.Enabled` (new Feature Gate group `practice-gym-formio-template-pilot`, `RiskLevel.High`, `RequiresConfirmation = true`, default `false`) — checked live by `PracticeGymGenerationJob` before ever attempting the template path.
3. At least one `ActivityTemplate` with `PatternKey = "formio_practice_gym_pilot"`, `IsPublished = true`, `ReviewStatus = Approved` must exist — the job silently falls back to standard free-form generation if none is found.

Any single one of these three being false makes the pilot completely inert. Fastest rollback: flip the feature flag off.

**Backend changes:**
- `LearningActivity`: new nullable `FormIoSchemaJson` (student-safe) / `ScoringRulesJson` (backend-only) fields + `SetFormIoContent(schema, scoringRules)`. Migration `T_PracticeGymFormIoTemplatePilot` (2 additive nullable columns).
- `MarkingMode.FormIoScored` (new enum value, appended — never reorders existing values).
- `PatternEvaluationRequest.ScoringRulesJson` (new optional trailing field) — sourced server-side only from `LearningActivity.ScoringRulesJson`, never derived from anything sent to the client.
- New `FormIoPatternEvaluator : IPatternEvaluator` (`Application/Activity/Evaluators/`) — reuses `IPlacementScoringService`/`ComponentAnswerScorer` directly (the exact same deterministic single_choice/multiple_choice/text_exact/text_normalized engine placement items use), registered as a 6th `IPatternEvaluator` alongside the existing five with zero changes to their registrations.
- `PracticeGymGenerationJob`: new `TryMaterializeFromTemplateAsync` helper, invoked only for the pilot pattern key when the feature flag is on; queries the newest published+approved `ActivityTemplate`, calls `IActivityTemplateInstanceGenerator` (Phase 5, unchanged), builds the `LearningActivity` with a harmless `"{}"` `AiGeneratedContentJson` placeholder + `SetFormIoContent(...)`. Any failure (no template, generation/validation failure) falls back to the pre-existing standard generation path unchanged.
- `ActivitySubmitHandler.HandlePatternEvaluationAsync`: for `MarkingMode.FormIoScored`, sources `ContentJson`/`ScoringRulesJson` from the activity's Form.io fields instead of `AiGeneratedContentJson`.
- `ActivityGetHandler.MapToDto`: short-circuits to a minimal `ActivityDto` (with `FormIoSchemaJson` populated) before any `AiGeneratedContentJson`/`ModuleStageSchema` parsing, avoiding the `"{}"` placeholder ever being misinterpreted as staged content.
- Seed data: `ExercisePatternSeeder` + `ExerciseTypeDefinitionSeeder` (`Planned(...)`) additions, `ExercisePatternKey.FormIoPracticeGymPilot` constant.
- Feature flag: `IPracticeGymFormIoTemplatePilotSettingsProvider`/impl — a single boolean backed by a `RuntimeSettingOverride` row (fails safe to `false` on any DB/parse error), registered in `FeatureGateDefinitions` so it's manageable from the existing admin Feature Gates page with no new UI needed.

**Frontend changes:**
- `ActivityDto.formIoSchemaJson` (TS model).
- `ExerciseRendererComponent`: new `formIoSchema` getter + template branch (checked before the existing `interactionMode` switch) rendering `<app-formio-renderer>` — reuses the exact same component already proven for placement/onboarding, zero new Angular infrastructure.
- `activity-lesson.component.ts` (`onRendererSubmit`, the single shared submission entry point for both Today lessons and Practice Gym): new `formIo` payload case, stringifying the raw Form.io submission data — matched server-side by `FormIoPatternEvaluator`.

**Test/count updates:** existing hardcoded-count tests bumped for the one new pattern/exercise-type (`ExercisePatternPhase1Tests`, `ExerciseTypeCatalogTests`, `InteractionModeMarkingModeTests`); two "no planned formats remain" invariant tests updated with an explicit, commented exception for this pattern. New tests: `FormIoPatternEvaluatorTests` (6), `LearningActivityTests.SetFormIoContent*` (3). **Known gap:** no end-to-end integration test exercises `PracticeGymGenerationJob`'s template branch directly (standing up its full dependency graph — routing/mastery/learning-plan services — for a first-ever test of this job was judged low value relative to cost, given the branch's own sub-pieces — generator, evaluator, entity, DTO mapping — are each already tested in isolation, and the pattern is inert-by-default regardless). Flagged as a follow-up if this pilot is ever promoted toward general availability.

Full suite after this phase: 3305 backend tests passed / 0 failed (5 architecture + 1898 unit + 1402 integration). Angular production build: zero new errors (the pre-existing 1MB bundle-budget failure, unrelated to this work, persists as documented in Phase 4/5).

## 14. Phase 6 — extend StudentActivityReadinessItem for template-sourced instances — IMPLEMENTED 2026-07-07

Returned to Phase 6 now that the Phase 10 pilot gives it a real, working target to wire into.

**New fields on `StudentActivityReadinessItem`** (migration `T_ReadinessItemTemplateProvenance`, all additive/nullable):
- `SourceTemplateId` (Guid?, FK → `ActivityTemplate`, `Restrict` delete)
- `SourceBankItemId` (Guid?, FK → `PlacementItemDefinition`, `Restrict` delete) — reserved for a future bank-sourced-but-not-template flow; not populated by any pipeline yet
- `FormIoSchemaSnapshotJson` / `ScoringRulesSnapshotJson` (string?, jsonb) — now genuinely usable thanks to the Phase 10 pilot's Form.io rendering path (previously would have stayed permanently null)
- `PersonalizationReason` (string?) — human-readable note on how the AI varied this instance from its template
- `GeneratedByModel` / `GeneratedByProvider` (string?) — supplements the existing free-text `GeneratedBy` label with the actual model/provider that produced this instance
- `ValidationStatus` (`ActivityValidationStatus?` — new enum: `Passed/Failed/NeedsReview`, distinct from `AdminReviewStatus` which is the human-approval gate) — null means "not applicable" for free-form-generated items; only template-sourced items populate it

**Entity API:** two new mutators, deliberately separate from the existing lifecycle transitions (mirrors the codebase's own established pattern of admin-review actions being separate from status transitions): `SetTemplateProvenance(...)` and `SetBankItemProvenance(bankItemId)`.

**Service API:** `IStudentActivityReadinessPoolService.SetTemplateProvenanceAsync(...)` — thin wrapper (load item, call entity mutator, save), same shape as the existing `LinkMaterializedIdsAsync`.

**Wiring:** `PracticeGymGenerationJob.TryMaterializeFromTemplateAsync` now calls `SetTemplateProvenanceAsync` right after `MarkReadyAsync` succeeds, recording the template id, both Form.io snapshots (the generated schema + the template's scoring model), a generated personalization-reason string, the actual model/provider from the Phase 5 generator's result, and `ValidationStatus.Passed` — accurate, not a placeholder, since reaching this line means the Phase 5 generator's own validation gate already passed (it throws on failure).

**Tests:** 6 new entity unit tests (`SetTemplateProvenance`/`SetBankItemProvenance` success + empty-guid-throws cases). No dedicated integration test was added for the new `StudentActivityReadinessPoolService` method — it's a thin wrapper with the same shape as the existing untested `LinkMaterializedIdsAsync`, consistent with this codebase's existing test-depth convention for that service.

Full suite: 3309 backend tests passed / 0 failed (5 architecture + 1902 unit + 1402 integration). No Angular changes this phase.

## 15. Phase 7 — Placement item bank calibration fields — IMPLEMENTED 2026-07-07

**New fields on `PlacementItemDefinition`** (migration `T_PlacementItemCalibrationFields`, all additive with sensible defaults):
- `DifficultyBand` (int, 1–5, default 1) — same scale as `CurriculumObjective.DifficultyBand`, validated in both the constructor and `Update`.
- `EvidenceWeight` (double, default 1.0) — multiplier for how strongly this item's result should feed into `PlacementAssessmentService`'s skill-confidence computation. Field exists and is settable; **not yet consumed** by `PlacementAssessmentService`'s confidence calculation — that wiring is a follow-up, not part of this phase's scope (the plan explicitly separates "add the field" from "recalibrate the selection algorithm").
- `DiscriminationIndex` (double?) / `CalibrationSampleSize` (int?) — both null until populated; no automatic statistics job exists yet, so population is via a manual admin entry point (`SetCalibrationStats`) for now, exactly as the original plan flagged ("populate later from attempt statistics, don't block on it now").
- `ReviewStatus` (`AdminReviewStatus`, default `NotRequired`) — reuses the same enum as `ActivityTemplate`/`StudentActivityReadinessItem`, not a new one.
- `ItemVersion` (int, default 1) / `PreviousVersionId` (Guid?, self-referencing FK, `Restrict` delete) — same minimal versioning shape as `ActivityTemplate`: fields exist, but **no "create next version" command is exposed yet** (same documented limitation as Phase 4).

**Entity API:** `SetCalibrationStats(discriminationIndex, calibrationSampleSize)`, `Approve()`/`Reject(reason)`/`ResetToPendingReview()` (mirrors `ActivityTemplate`'s review methods exactly; `Reject` also disables the item via `IsEnabled = false` since a rejected placement item should not be selectable). `DifficultyBand`/`EvidenceWeight` are also settable via the existing `Update(...)` method (both optional, so a caller that doesn't pass them leaves the current values untouched — same "don't silently reset on partial update" discipline established in earlier phases).

**Admin surface:** extended `AdminPlacementItemDto`/`AddPlacementItemCommand`/`UpdatePlacementItemCommand`, two new endpoints on the existing `AdminPlacementItemController` — `POST /api/admin/placement-items/{id}/review` and `POST /api/admin/placement-items/{id}/calibration` — mirroring the `ActivityTemplate` controller's action shape exactly. New shared `PlacementItemMapper.ToDto` DRYs up what were previously four separate inline DTO-construction call sites (Add/Update/Get/List handlers). Angular: new numeric fields on the placement item editor's main card (DifficultyBand/EvidenceWeight) plus a "Review & calibration" card (Approve/Reject/Reset buttons, discrimination-index/calibration-sample-size inputs) shown only for existing (non-new) items — same layout pattern as the `ActivityTemplate` editor's review/publish card.

**Tests:** 12 new domain unit tests (defaults, range validation for `DifficultyBand`/`EvidenceWeight`, `SetCalibrationStats`, `Approve`/`Reject`/`ResetToPendingReview`) + 4 new integration endpoint tests (persisting calibration fields on add, approve→reject flow, reject-without-reason 400, calibration-stats round trip).

Full suite: 3325 backend tests passed / 0 failed (5 architecture + 1914 unit + 1406 integration). Angular production build: zero new errors (same pre-existing bundle-budget failure only).

## 16. Phase 8 — Evidence/mastery extension — IMPLEMENTED 2026-07-07

**New field:** `StudentLearningEvent.CurriculumObjectiveKey` (string?, migration `T_StudentLearningEventCurriculumObjectiveKey`, additive nullable column + `(StudentProfileId, CurriculumObjectiveKey)` index mirroring the existing `(StudentProfileId, PatternKey)` index).

**Grouping fix:** `StudentMasteryEvaluationService.cs` already had a `file static class LearningEventExtensions` with a `CurriculumObjectiveKey(this StudentLearningEvent e)` extension method — its own doc comment admitted it was a proxy: `=> e.PatternKey`, because the real field didn't exist. Updated to `=> e.CurriculumObjectiveKey ?? e.PatternKey`. This is the entire fix to the mastery/evidence grouping logic — no threshold logic, no other method in that file changed. Fully backward compatible: any event without a real objective key (all pre-existing events, and any future event a writer doesn't resolve one for) behaves exactly as before.

**Wiring:** `ActivitySubmitHandler` writes `StudentLearningEvent` rows in two places (legacy path, pattern-evaluation path). Added `TryGetReadinessObjectiveKeyAsync(profileId, activityId, ct)` — a best-effort, read-only lookup of the `CurriculumObjectiveKey` snapshot already stored on the linked `StudentActivityReadinessItem` (queried before consumption, while still `Reserved`; returns null and logs a warning on any failure, never throws) — called once per submission and threaded into both event constructions. This means real objective keys start flowing into evidence immediately for any activity that came from the readiness pool with a routing-resolved objective (the common case for Practice Gym/Today lesson content), without needing a broader schema or pipeline change.

**Not done (explicitly out of scope for this phase, per the plan's own framing of Phase 8 as grouping-only):** no change to `StudentMasteryEvaluationService`'s threshold/classification logic; no backfill of `CurriculumObjectiveKey` on historical events (they simply keep using the `PatternKey` fallback forever, which is fine — grouping is computed at read time, not stored).

**Tests:** 2 new entity unit tests (`CurriculumObjectiveKey` stored/trimmed, defaults to null) + 1 new mastery-service test proving events from two *different* `PatternKey`s sharing one `CurriculumObjectiveKey` are now grouped together into a single objective's evidence (previously they'd have been split and neither pattern alone would reach the 5-event mastery threshold). The extension method itself is `file`-scoped (C# file-local visibility) and can't be unit-tested directly — coverage is via the public `StudentMasteryEvaluationService.EvaluateStudentAsync` behavior instead.

Full suite: 3328 backend tests passed / 0 failed (5 architecture + 1917 unit + 1406 integration). No Angular changes this phase.

Phases 2–4 can proceed in parallel with normal feature work; Phase 5+ should wait until Phase 4 has at least a handful of hand-authored templates to validate against.

## 17. Phase 9 — Admin review/approval workflow — IMPLEMENTED 2026-07-07 (roadmap complete)

**Scope:** by the time this phase was reached, `ActivityTemplate` (Phase 4) and `PlacementItemDefinition` (Phase 7) each already had their own ad hoc approve/reject action bar, reusing the shared `AdminReviewStatus` enum. What was missing was a single place for an admin to *find* what needs reviewing, rather than checking each entity's own list one at a time. Phase 9 built exactly that — a read-only, cross-entity review queue — without touching either entity's existing review endpoints.

**Explicit exclusion:** `StudentActivityReadinessItem`'s review-scaffold pilot (`AdminReviewStatus` gate on per-student generated instances) is deliberately **not** included in this queue. It's a different concern — per-student content triage with its own dedicated pilot admin surface (`practice-gym-review-scaffold-pilot` feature gate) and urgency model — not bank-content curation. Folding it in would have mixed two different review workflows into one UI for no real benefit.

**Backend:** new read-only `GET /api/admin/review-queue` endpoint (`AdminReviewQueueController`/`IAdminReviewQueueQuery`/`AdminReviewQueueQueryHandler`) that queries `ActivityTemplates` and `PlacementItemDefinitions` independently, maps both into one `AdminReviewQueueItemDto` shape (`EntityType` discriminator + `DisplayKey` — `ActivityTemplate.Key` or the existing `PlacementItemSchemaLabel.ExtractLabel` derived label for placement items, which have no natural key), unions and sorts oldest-first (fairest triage order), and paginates in memory. `PendingCount` in the response is always computed unfiltered across both entity types regardless of the request's own `reviewStatus` filter — same KPI-strip convention as `AdminActivityTemplateListResult.PublishedCount`/`AdminPlacementItemListResult.EnabledCount`. No new write path — approve/reject actions still go through each entity's own existing controller.

**Frontend:** new `admin-review-queue` page (list only, no editor) with entity-type/review-status filters, inline Approve/Reject buttons that call the correct existing service (`AdminActivityTemplateService`/`AdminPlacementItemService`) based on each row's `EntityType`, and a "View" link routing to that entity's own editor for full detail. Added to the admin sidebar nav (mobile + desktop).

One real bug caught during the build: joining two different-but-structurally-similar `Observable<T>` types behind a ternary broke TypeScript's `subscribe()` overload resolution (`This expression is not callable`) — fixed by branching into two full `if/else` calls instead of trying to unify into one `obs` variable.

**Tests:** 4 new integration endpoint tests (401 unauthenticated, a pending `ActivityTemplate` appears in the queue, `entityType` filter excludes the other type, `pendingCount` stays unfiltered by the request's own `reviewStatus` param).

Full suite: 3332 backend tests passed / 0 failed (5 architecture + 1917 unit + 1410 integration). Angular production build: zero new errors (same pre-existing bundle-budget failure only).

**Roadmap status: all 10 phases from §11 are now implemented** (Phases 1–10, with the Phase 6/Phase 10 reordering documented in the "Reordering decision" note under §11). See the Final verdict below.

---

## 12. First implementation phase prompt recommendation

Recommend starting with **Phase 2 (Subskill taxonomy)** as the first actual implementation ticket after this planning phase: it is the smallest, lowest-risk, and every later phase (template bank, calibration, evidence) references it as an optional field. A reasonable next-session prompt:

> "Implement Phase 2 from docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md: add a `CurriculumSubskillConstants` class mirroring `CurriculumSkillConstants`, and a nullable `Subskill` string column to `CurriculumObjective`, `PlacementItemDefinition`, and `StudentLearningEvent` via `dotnet ef migrations add`. No behavior changes — additive schema only."

---

## Documentation impact

- Docs reviewed: `AGENTS.md`, `docs/architecture/README.md`, `docs/architecture/formio-onboarding-placement-model.md`, `docs/architecture/file-storage-minio.md`, `docs/architecture/usage-governance.md`
- Docs updated: this new file only (`docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`)
- Docs intentionally not updated: `docs/architecture/formio-onboarding-placement-model.md` (no architectural change made yet — this is a plan, not an implementation); sprint/backlog docs (no sprint exists yet for this initiative — recommend creating one when Phase 2 is scheduled)
- Reason: this is a planning-only deliverable per task instructions; no code, schema, or product behavior changed

---

## Risks or unresolved questions

- CEFR-J and UniversalCEFR licenses have not been legally verified — do not import either until confirmed (§9).
- `CurriculumObjective` has no C1/C2 content yet — out of scope for this plan but relevant if the bank vision extends to advanced levels.
- Whether `ActivityTemplate` should carry a full Form.io schema (matching placement) or a `ModuleStageSchema`-shaped schema (matching lesson/practice content) — **resolved during Phase 10**: `ActivityTemplate` carries a Form.io schema, and the Practice Gym pilot proved a Form.io rendering path can coexist with the existing `ModuleStageSchema` path behind a triple safety gate.
- `PlacementItemDefinition.EvidenceWeight` (Phase 7) exists but is not yet consumed by `PlacementAssessmentService`'s confidence calculation — a real follow-up if calibration data is meant to actually change adaptive item selection, not just be recorded.
- No "create next version" command exists for either `ActivityTemplate` or `PlacementItemDefinition` — both entities have version-chain fields (`VersionNumber`/`PreviousVersionId`, `ItemVersion`/`PreviousVersionId`) but no exposed workflow to use them yet.
- The Practice Gym Form.io pilot pattern (`formio_practice_gym_pilot`) has no dedicated end-to-end integration test for `PracticeGymGenerationJob`'s template branch — each sub-piece is tested in isolation; flagged as a follow-up if the pilot is ever promoted toward general availability.
- CEFR-J/UniversalCEFR dataset import remains not started (by design — Phase 3 was schema-only); the licensing questions in `docs/architecture/cefr-resource-licensing-review.md` still need a legal/product decision before any import.

## Final verdict

Bank-first direction is architecturally sound and turned out to be almost entirely additive in practice: every phase (1–10) shipped as schema/service extensions, new files, and new endpoints — no existing subsystem was replaced, and no destructive migration was needed anywhere. The placement item bank, readiness pool, mastery/evidence pipeline, and asset storage abstraction were already close to the target model at the start; the real gaps (subskill taxonomy, the `ActivityTemplate` bank itself, real AI generation validated against it, Form.io rendering in Practice Gym, template-sourced provenance on the readiness pool, calibration fields, evidence grouping, and a cross-entity review queue) are now all built, each behind the safety gates appropriate to its blast radius (feature flags, `IsPublished`/`ReviewStatus` gates, `ImplementationStatus="planned"` for the pilot pattern). Total: 3332 backend tests passing, 0 failures, across the full set of changes; no code was committed at any point in this session — everything described here is uncommitted working-tree state pending review.

## Next recommended action

All 10 roadmap phases are implemented. Recommended next steps, in rough priority order:
1. **Review and commit** this working tree in reviewable chunks (the phases were built and tested independently, so they can likely be split into separate commits/PRs along phase boundaries if a smaller-diff review is preferred).
2. Decide whether to wire `EvidenceWeight` into `PlacementAssessmentService`'s confidence calculation (currently recorded but inert).
3. Decide whether to invest in a "create next version" workflow for `ActivityTemplate`/`PlacementItemDefinition`, or leave versioning fields dormant until a concrete need arises.
4. If the Practice Gym Form.io pilot is promoted beyond a pilot, add the deferred end-to-end job-level integration test first.
5. Resolve the CEFR-J/UniversalCEFR licensing questions before considering any dataset import.
