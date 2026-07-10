---
status: current
lastUpdated: 2026-07-10 23:10
owner: architecture
supersedes:
supersededBy:
---

# Product Architecture Audit — AI Content Pipeline, Module Flow, and Runtime Readiness

**Date:** 2026-07-10
**Related sprint/feature:** Post-I4 checkpoint. Audits the state left by Phases H0-H10 and I0-I4
(`ResourceBankItem` consolidation, unified import pipeline, legacy Practice Gym/readiness-pool
deletion, Today-Plan-to-Module-only collapse, Content Studio nav consolidation, and the
Lesson/Exercise/Module/Today Plan product-language rename).
**Type:** Audit/report only. No application code, migration, or product-facing doc changed. Working
tree confirmed clean before and after (`git status --short` empty both times).
**HEAD at start and end of review:** `20f0a369` (unchanged)

**Files/docs reviewed:** `AGENTS.md`; `docs/handoffs/current-product-state.md` (partial — 812 of
2654 lines, truncated by size; grepped for later sections); `docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md`;
`docs/reviews/2026-07-10-phase-i0-resourcebankitem-physical-consolidation-review.md`;
`docs/reviews/2026-07-10-phase-i1-unified-import-pipeline-review.md`;
`docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md`;
`docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md`;
`docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md`;
`docs/reviews/2026-07-10-phase-i3-final-nav-consolidation-review.md`;
`docs/reviews/2026-07-10-phase-i4-pass1-backend-rename-review.md`;
`docs/reviews/2026-07-10-phase-i4-pass2-frontend-rename-review.md`;
`docs/reviews/2026-07-10-phase-i4-pass3-today-plan-rename-review.md`;
plus direct code inspection (via three parallel research passes) of: `ResourceBankItem`,
`ResourceCandidate`, `ResourceImportService`, `ResourceCandidateValidationService`,
`ResourceCandidateAnalysisService`, `ResourceCandidatePublishService`, `CefrResourceSource`,
`Lesson`/`LessonGenerationService`, `Exercise`/`ExerciseGenerationService`/`ExerciseLaunchEligibility`,
`Module`/`ModuleGenerationService`/`ModuleLessonLink`/`ModuleExerciseLink`, `AdminModuleController`,
`ExerciseLaunchService`, `TodayPlanModuleSelectionService`, `PracticeGymModuleSelectionService`,
`ActivityContentFingerprintService`, `admin-app-layout.component.html`, `app.routes.ts`,
`admin-content-import.component.ts/html`, `admin-resource-bank-unified.component.html`.

**Commands run:**
```
git status --short          -> clean
git diff --check            -> clean
dotnet build --configuration Release   -> Build succeeded, 0 errors, 19 pre-existing warnings
dotnet test --configuration Release    -> 3,424/3,424 passed (5 architecture, 2,107 unit, 1,312 integration)
npm run build -- --configuration production -> fails on pre-existing bundle-size budget only
                                              (1.00 MB budget, 2.56 MB actual initial bundle)
```
Playwright and Karma were not run: Karma is blocked by the pre-existing, already-tracked
`TODO-H8-2` (five unrelated spec-fixture compile failures, confirmed via prior-phase docs to predate
this audit and to be unrelated to any code touched here); Playwright was out of scope for a
read-only inspection pass per the task's own instruction to not expand scope beyond documented
blockers.

---

## A. Executive summary

**Does the app currently match the intended model? Partial — closer than expected.**

The product has already moved substantially past the vision's starting assumptions. As of Phases
I0–I4 (2026-07-10, same day as this audit), the app is *already* running the target
`Import Content → Resource Bank → Lessons → Exercises → Modules` pipeline end to end, the legacy
`ActivityTemplate`/readiness-pool/always-fresh-AI-generation paths have been *physically deleted*
(not just hidden), and Today Plan / Practice Gym both select **only** from approved Modules with a
safe, non-throwing fallback when nothing suitable exists. Admin nav is exactly
`Import Content → Resource Bank → Lessons → Exercises → Modules → Onboarding → Placement`, matching
the vision's own suggested order. Naming is clean apart from two small leftovers (below).

**The biggest gap is not pipeline plumbing — it's that every generation step (Lesson, Exercise,
Module) is deterministic template composition, not AI.** No `IAiProvider` call exists anywhere in
`LessonGenerationService`, `ExerciseGenerationService`, or `ModuleGenerationService`. The vision
describes AI-analysed import, AI-generated Lessons/Exercises, and AI-composed Modules; the shipped
system does field-mapping and rule-based composition at every one of those steps, honestly labeled
`GenerationProvider = "Deterministic"`. This is a real, large gap against the stated product vision,
not a bug — the code is internally honest about what it is.

**Biggest 5 gaps:**
1. No AI anywhere in the content pipeline — Import classification, Lesson generation, Exercise
   generation, and Module composition are all deterministic, not AI-assisted as the vision requires.
2. No admin "preview Module as a learner, complete the Exercise, see scored Feedback" flow exists
   before approval — `AdminModuleController` has no preview/try-it endpoint, and the only runtime
   launch path (`ExerciseLaunchService`) requires an already-Approved Module plus a real student ID.
3. Duplicate detection at import stops at the `ResourceCandidate` stage — it never checks against
   already-published `ResourceBankItem` rows, despite `ResourceBankItem.ContentFingerprint`
   existing as a column for exactly this. A stale code comment says published tables have "no
   fingerprint-shaped column," which is no longer true post-I0.
4. Duplicate detection is advisory-only everywhere (a warning that forces `NeedsReview`, no unique
   DB constraint) — an admin can knowingly approve and publish a flagged duplicate.
5. Only 2 of 3 auto-generatable Exercise types (`gap_fill`, `multiple_choice_single`) are actually
   launchable/scorable; `short_answer` is generated, reviewable, and approvable, but silently
   unsupported at runtime (`ExerciseLaunchEligibility`).

**Biggest 5 bugs/risks:**
1. `docs/handoffs/current-product-state.md` — the designated running product-state log — has **not
   been updated for Phases I0–I4** despite those phases landing the same day as its last recorded
   entry (H9B). Any agent reading it as "current" will believe `ActivityTemplate`/readiness-pool
   still exist and Today/Practice Gym still have legacy AI fallbacks; both are false as of `HEAD`.
2. `ResourceCandidateType.ActivityTemplateCandidate` (an import-content type, UI-exposed in
   `admin-content-import.component`) is a confusing name collision with the deleted `ActivityTemplate`
   entity from I2A — publishing it is blocked/deferred, but the name itself will mislead readers.
3. `admin-resource-bank-unified.component.html:3` still has live user-facing subtitle copy
   referencing "Daily Lessons" — missed by the I4 Pass 3 rename that changed this everywhere else.
4. Frontend production build fails its bundle-size budget (1.00 MB budget vs 2.56 MB actual) — a
   pre-existing, already-tracked condition, but worth flagging since it means CI-grade `npm run
   build -- --configuration production` does not currently exit 0.
5. Frontend Karma unit tests remain blocked by `TODO-H8-2` (five unrelated pre-existing spec-fixture
   compile failures) — confirmed still open, not touched by this audit per scope.

**What is already correct:**
- Full `Import Content → Resource Bank → Lesson → Exercise → Module → Today Plan/Practice Gym`
  pipeline is wired and live, not aspirational.
- Legacy always-fresh-AI generation (`SessionGeneratorService`, `LessonBatchGenerationJob`,
  `ActivityMaterializationJob`) is **physically gone** from `src`/`tests` — confirmed via repo-wide
  grep, not just hidden behind a flag.
- Today Plan and Practice Gym both select only Approved Modules with an approved linked Lesson AND
  Exercise, degrade safely (never throw) when nothing suitable exists, and both apply CEFR/skill/
  context/focus/difficulty/novelty-cooldown filtering.
- Real content-level fingerprinting exists (`ActivityContentFingerprintService`, SHA-256 over
  normalized content + metadata, volatile keys stripped) — this is genuine dedup infrastructure,
  a marked improvement over the dead `Guid.NewGuid()`-salted fingerprint flagged in the 2026-07-08
  plan doc.
- Source metadata/licensing is enforced at publish time (`CefrResourceSource` license/attribution
  fields; publish hard-blocks without display/commercial-use permission).
- Approval state machine (`AdminReviewStatus`: PendingReview/Approved/Rejected, reject-to-reopen) is
  consistent across Lesson, Exercise, and Module.
- Lesson/Exercise/Module many-to-many reuse is real — no uniqueness constraint restricts an approved
  Lesson or Exercise from being linked into multiple Modules.
- Admin nav matches the target IA exactly; no orphaned live pages, only documented redirect aliases.
- Backend build and full test suite (3,424 tests) are green.

---

## B. Pipeline checklist table

| Step | Expected behavior | Current behavior | Status | Evidence/files | Recommended phase |
|---|---|---|---|---|---|
| Import — paste/upload | Admin pastes/uploads unstructured content | Paste (text/CSV/JSON) + file upload, 5 MB cap | Done | `admin-content-import.component.ts:264-366`, `ResourceImportService.cs:456-465` | — |
| Import — choose type | Admin selects content type | 3 of 7 types live (Vocabulary/Grammar/Reading); Listening/Speaking/Writing/Mixed are UI-disabled "coming soon" | Partial | `admin-resource-import.models.ts:354-360` | New phase: extend `ResourceCandidateType` shapes |
| Import — AI analysis | AI analyses and classifies content | Deterministic field-mapping is primary; AI "Analyze" exists but is a separate, manual, advisory-only action that never sets validation status | Partial/Bug (vs vision) | `ResourceImportService.cs:276-362`, `ResourceCandidateAnalysisService.cs:27-59` | AI-generation gap, see §C |
| Import — structured output | CEFR/type/skill/subskill/context/focus/difficulty/tags populated | Most fields present on `ResourceBankItem`; no top-level `Skill`, `PrimarySkill` dropped at publish, `DifficultyBand`/tags null unless explicitly set | Partial | `ResourceBankItem.cs:16-27`, `ResourceCandidatePublishService.cs:298` | Small fix: persist `PrimarySkill` |
| Import — source metadata | Source/licensing retained | `CefrResourceSource` with license/attribution fields, FK from every bank item, publish blocked without permission | Done | `CefrResourceSource.cs:22-172`, `ResourceCandidatePublishService.cs:109-116` | — |
| Import — duplicate detection | Same content not added twice | Exact-fingerprint check within-run/within-source/global across candidates only; never checked against already-published `ResourceBankItem`; advisory warning only, no unique constraint | Missing (vs published bank) / Partial (vs candidates) | `ResourceCandidateValidationService.cs:247-297`, stale comment at 281-289 | Critical gap, see §C |
| Import — review/approve gate | Can't accidentally publish unreviewed content | Live re-validation gate at publish time, idempotent, no bypass found | Done | `ResourceCandidatePublishService.cs:57-148` | — |
| Resource Bank — unified view | Single searchable/filterable bank | `ResourceBankItem` physically consolidated (Phase I0), unified admin page | Done | Phase I0 review | — |
| Resource Bank → Lesson | Select bank item(s), AI generates Lesson | Deterministic composition, not AI; rich fields (examples, mistakes, usage notes, tags) | Partial (vs vision: not AI) | `LessonGenerationService.cs:22`, `Lesson.cs:18-181` | AI-generation gap |
| Resource Bank/Lesson → Exercise | AI generates Exercise (schema, answer key, scoring, feedback) | Deterministic; full Form.io schema/answer key/scoring/feedback fields present; 3 types generated, only 2 launchable | Partial | `ExerciseGenerationService.cs:47-53`, `ExerciseLaunchEligibility.cs:18-22` | AI-generation gap + launch-support gap |
| Lessons/Exercises → Module | AI combines compatible Lessons+Exercises+Feedback, many-to-many | Deterministic composition; many-to-many confirmed (no uniqueness constraint); module-level feedback plan present | Partial (not AI) | `ModuleGenerationService.cs:25`, `ModuleLessonLinkConfiguration.cs`, `ModuleExerciseLinkConfiguration.cs` | AI-generation gap |
| Module — admin preview-as-learner | Admin completes Exercise, sees scored Feedback, before approving | No such endpoint/UI exists anywhere | Missing | `AdminModuleController.cs` (no preview action), `ExerciseLaunchService.cs` (requires Approved + real student) | Critical product gap, see §C |
| Module → Today Plan | Approved Modules feed Today Plan, safe fallback | Selects only Approved Modules w/ approved Lesson+Exercise; CEFR/skill/context/focus/difficulty/weakness/14-day-cooldown; never throws | Done | `TodayPlanModuleSelectionService.cs:41-108,200` | — |
| Module → Practice Gym | Approved Modules feed Practice Gym, safe fallback | Same pattern; real launch path via `ExerciseLaunchEligibility` (not "Coming soon") | Done | `PracticeGymModuleSelectionService.cs:44-49,215-221,257-263` | — |
| Admin nav IA | Import Content → Resource Bank → Lessons → Exercises → Modules → Onboarding → Placement | Exact match | Done | `admin-app-layout.component.html:252-277` | — |
| Naming cleanliness | No leftover LearnItem/ActivityDefinition/Daily Lesson/Review Queue/readiness-pool in live code/UI | Fully removed except: `ActivityTemplateCandidate` enum value (UI-exposed), one "Daily Lessons" string in `admin-resource-bank-unified.component.html:3` | Mostly done, 2 leftovers | See naming table below | Trivial cleanup |
| Content-level dedup/novelty | Real content fingerprinting prevents repeats | `ActivityContentFingerprintService` — real SHA-256 over normalized content, not a GUID | Done | `ActivityContentFingerprintService.cs` | — |

---

## C. Gap backlog

**Critical product gaps**
- No admin "preview Module as a learner → complete Exercise → see scored Feedback → then approve"
  flow exists. This is an explicit, named requirement in the product vision and has no code path
  today, not even a partial one.
- Duplicate detection is never checked against already-published `ResourceBankItem` rows — only
  against other pending `ResourceCandidate` rows. A resource can be re-imported and republished
  indefinitely without the system ever comparing it to what's already live.
- Duplicate detection everywhere is advisory-only (forces `NeedsReview`, no unique DB constraint) —
  an admin can knowingly approve and publish a duplicate with no system-level block.

**AI-generation gaps**
- Import classification: deterministic field-mapping is the real pipeline; AI "Analyze" is a
  separate, optional, advisory action that never influences `ValidationStatus`.
- Lesson generation: 100% deterministic (`GenerationProvider = "Deterministic"`), no `IAiProvider`
  call in `LessonGenerationService`.
- Exercise generation: same — deterministic, no AI call in `ExerciseGenerationService`.
- Module generation: same — deterministic composition of already-approved Lessons/Exercises, no AI
  call in `ModuleGenerationService`.
- All three entities reserve `GenerationProvider`/`GenerationModel` fields in their doc comments "for
  when AI generation is wired in" — the schema anticipates AI, the implementation doesn't use it yet.

**Admin UX gaps**
- No Module preview/try-it endpoint (see Critical, above).
- `PrimarySkill` is captured on `ResourceCandidate` but silently dropped, not persisted onto the
  published `ResourceBankItem`.
- `ActivityTemplateCandidate` import-type name collides confusingly with the deleted `ActivityTemplate`
  entity.

**Runtime/student gaps**
- `short_answer` Exercises can be generated, reviewed, and approved, but are never launchable/scorable
  at runtime (`ExerciseLaunchEligibility` restricts to `gap_fill`/`multiple_choice_single` only) —
  admins have no signal at generation time that this type is a dead end.
- 4 of 7 target import content types (Listening/Speaking/Writing/Mixed) are UI-disabled stubs.

**Data/duplicate-detection gaps**
- (See Critical gaps above — both listed there since they are also data-integrity issues.)
- No fuzzy/near-duplicate detection anywhere; exact fingerprint match only.
- No PDF/DOCX/HTML extraction for import — CSV/JSON/JSONL text only.

**Naming/docs leftovers**
- `docs/handoffs/current-product-state.md` not updated for Phases I0–I4 (see Executive Summary #1) —
  this is the single largest documentation-freshness risk found.
- `admin-resource-bank-unified.component.html:3` — live "Daily Lessons" string missed by the I4
  rename.
- `ActivityTemplateCandidate` enum value name collision (also listed under Admin UX gaps).

**Test/quality gaps**
- Frontend production build fails the bundle-size budget (2.56 MB vs 1.00 MB budget) — pre-existing,
  not caused by this audit, but means `npm run build -- --configuration production` does not
  currently exit 0 in CI-equivalent conditions.
- Karma frontend unit tests remain blocked by pre-existing `TODO-H8-2` (5 unrelated spec-fixture
  compile failures) — confirmed still open.
- Playwright E2E was not run in this audit pass (read-only inspection scope; no runtime/browser
  verification performed).

---

## D. Recommended next phases

1. **Phase J0 — Documentation sync (docs-only, low risk).** Update
   `docs/handoffs/current-product-state.md` with Phases I0–I4 (it currently stops at H9B despite I0–I4
   landing the same day) and fix the "Daily Lessons" leftover string in
   `admin-resource-bank-unified.component.html`. This should come first because every subsequent
   phase's own documentation-impact review depends on the "current state" doc actually being current.

2. **Phase J1 — Duplicate detection against the published bank (data-integrity, medium risk).**
   Add a fingerprint check in `ResourceCandidateValidationService`/`ResourceCandidatePublishService`
   against already-published `ResourceBankItem.ContentFingerprint` rows (the column already exists
   post-I0; only the query is missing). Decide, as a product call, whether an exact match should stay
   advisory or become a hard publish-block — the current advisory-everywhere stance lets duplicates
   through even when flagged. This should come before AI generation work (J2) because AI-assisted
   generation will increase content volume and duplicate risk faster than deterministic composition
   does.

3. **Phase J2 — AI-assisted Lesson/Exercise/Module generation (the core vision gap, high value, higher
   risk).** Wire a real `IAiProvider` call into `LessonGenerationService`/`ExerciseGenerationService`/
   `ModuleGenerationService`, following the existing AI rules in `AGENTS.md` §4 (backend-owned
   planning, AI generates one bounded task, validated JSON, usage tracking, fallback). Keep the
   deterministic path as an explicit fallback rather than deleting it — this mirrors how the I-track
   phases treated every prior legacy path (additive first, delete only once proven). Should follow J1
   so duplicate detection covers AI-generated content too.

4. **Phase J3 — Admin preview-as-learner for Modules (critical UX gap, medium risk).** Add a
   preview/try-it endpoint + admin UI that lets an admin render an unapproved Module's Exercise using
   the existing `ExerciseLaunchService`/Form.io renderer, submit an answer, and see scored Feedback —
   without requiring `Approved` status or a real student ID. This is explicitly named in the product
   vision as a precondition for Module approval and currently has zero code path.

5. **Phase J4 — Runtime coverage for `short_answer` Exercises (or explicit UI gating).** Either wire a
   manual/AI-assisted grading path so `short_answer` Exercises become launchable, or block their
   generation/approval with a clear "not launchable yet" message at generation time instead of letting
   admins approve content with no runtime path. Low risk either way; mainly an honesty/UX fix.

6. **Phase J5 — Import content-type expansion (Listening/Speaking/Writing/Mixed).** Lowest priority of
   the five; only pursue once `ResourceCandidateType` has real shapes for these types. Should follow
   J2 since AI-assisted classification will matter more for these harder-to-parse content types than
   for the current deterministic-mapping-friendly Vocabulary/Grammar/Reading types.

---

## E. Scope confirmation

No fixes were implemented as part of this audit beyond writing this report file. No code, migration,
or product-facing documentation file was changed. This review is not complete as an implementation
task until the user explicitly requests work on one of the phases in §D.

---

## AskUserQuestion decisions

None were required — the task was fully scoped by the provided prompt and existing repo state was
sufficient to answer all audit questions.

## Risks or unresolved questions

- Whether exact-duplicate detection (once wired against the published bank in J1) should hard-block
  publish or remain advisory is a product decision, not resolved here.
- Whether AI-assisted generation (J2) should be introduced pattern-by-pattern (Lesson first, then
  Exercise, then Module) or all at once — the I-track's own precedent favors incremental, reversible
  phases; not decided here.
- `docs/handoffs/current-product-state.md` exceeds 250 KB and could not be read in full in one pass
  during this audit (truncated at 812 of 2,654 lines, remainder grepped selectively) — a future split
  of this file into smaller dated logs may be worth considering independent of this audit's findings.

## Final verdict

The bank-first, Module-centric pipeline the vision describes is already built and wired end to end,
including the hard parts (legacy path deletion, safe fallback behavior, many-to-many reuse, admin nav
alignment). The two things standing between the current state and the full vision are: (1) AI is
absent from every generation step, where the vision explicitly calls for AI-assisted (not
deterministic) content creation, and (2) there is no admin preview-as-learner flow, which the vision
names as a precondition for Module approval. Neither requires architectural rework — both are
additive phases on top of a structurally sound foundation.

## Next recommended action

Start with Phase J0 (documentation sync) since it is the lowest-risk, fastest phase and unblocks
every subsequent phase's own documentation-impact review; then proceed to J1 (published-bank
duplicate detection) before starting the higher-value, higher-risk AI-generation work in J2.
