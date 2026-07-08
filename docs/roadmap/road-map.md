---
status: current
lastUpdated: 2026-07-08 (Bugfix-D1A)
owner: product / engineering
---

# SpeakPath / LinguaCoach Roadmap

**Accurate as of: 2026-07-08 (Bugfix-D1A — see §19a for the current phase sequence).
The 2026-07-03 "Phase 20H" line below is the last entry confirmed live against speakpath.app;
everything since then (Clean-A/A2, Phase B, Phase C1, Plan-Sync-After-C1, Phase C2, Plan-Sync-B2,
Phase B2, Phase C3, Phase C-Final, Phase E0, Plan-Sync-PG-v2, Phase E1, Phase E2, Phase E3,
Phase E4, Plan-Sync-After-E4, Phase E5, Plan-Sync-E6-Decision, Phase E6, Phase D1, Bugfix-D1A)
has been developed and tested locally but not yet deployed — see the "Current Project Status"
and Decision Log sections below for what's actually landed.**

This is the canonical project memory document. It captures completed work, current state, known gaps, deferred items, and the recommended order of future phases.

**✅ Live student golden path confirmed working end-to-end (2026-07-02):** Phase 20G completed the Phase 20E/20F pilot walkthrough against production: the pilot student completed placement (CEFR B2), got a real generated lesson, completed an activity with scored feedback, and Dashboard/Practice Gym/Journey/Progress/Profile all load with real data. Three real live bugs were found and fixed in-session (gap-fill activity unfillable, placement-result 400, Journey always-empty). **One admin-only regression remains open:** the readiness audit 500s for this specific student's data (`TODO-20G-3`) — isolated, not systemic, does not block the student experience.

**✅ Both remaining Phase 20G issues fixed and confirmed live (2026-07-03, Phase 20H):** `TODO-20G-3` (readiness audit 500 for the pilot student) and `TODO-20G-1` (Practice Gym duplicate suggestions) are both resolved and live-validated against `https://speakpath.app` for `pilot.student.20e@speakpath.app` — readiness audit returns 200 (with a structured warning confirming the exact original failure now degrades safely instead of 500ing), Practice Gym shows zero literal duplicate rows, and Dashboard/Today/Journey/Progress/Profile all load. **Ready to invite one real controlled pilot student.**

---

## 1. Current Project Status

**Latest phase completed (local, not yet deployed):** Bugfix-D1A — Fix
`LearningSession.GenerationStatus` Default/State Persistence Bug (2026-07-08). A
correctness/hardening phase run before Phase D2, fixing the bug D1 discovered rather than
building on top of it. **Root cause**: `LearningSessionConfiguration` configured
`GenerationStatus` with EF `.HasDefaultValue(GenerationStatus.Ready)`. Since
`GenerationStatus.Pending == 0` is also the enum's CLR default, EF Core's "omit CLR-default
property values from the INSERT, let the DB default apply" convention silently discarded an
explicit `MarkGenerationPending()` call made before a brand-new session's first
`SaveChangesAsync` — the row always persisted as `Ready` regardless.
`LessonBatchGenerationJob.MaterializeSessionsAsync` uses exactly that construction order, so
every background-generated session silently skipped a real `Pending` state. **Practical
impact confirmed**: `StudentReadinessAuditService`'s "no stuck session generation" check (which
flags sessions stuck in `Pending`/`Failed` for 30+ minutes) could never fire, since affected
sessions always read back as `Ready` immediately — a diagnostic blind spot for real stuck
generation. **Fix**: removed the `HasDefaultValue(...)` configuration (migration
`Bugfix_D1A_RemoveGenerationStatusDefault`, a clean `ALTER COLUMN ... DROP DEFAULT` — no data
change, no column type change, no data loss); `LearningSession` already defaults to `Ready` via
its own property initializer in code, so no DB-side default was ever needed. **Audited similar
enum-default patterns** across all `Configurations/*.cs` — `AdminReviewStatus.NotRequired` and
`FormRendererKind.FormIo` defaults are both configured to ordinal 0 (their own CLR default), so
they carry no equivalent risk; no other live instance of this bug class was found. +5 backend
tests (3,513 → 3,518 total: 5 architecture + 2,044 unit + 1,469 integration) proving every
`GenerationStatus` value round-trips correctly through save/reload, plus a corrected assertion
in `LessonBatchGenerationJobTests` (it had been unknowingly asserting the bug's symptom, not the
intended behavior). **No external dataset imported, no Persian/bilingual/support-language
content added, Today/Practice Gym legacy fallback not removed, no data loss.** Full detail:
`docs/architecture/learning-activity-engine.md`.

**Latest phase completed before this:** Phase D1 — First Bank-First Today
Composer Slice (2026-07-08, `2039d115`). New `ITodayBankResourceSelector`/
`TodayBankResourceSelector` inject published Resource Bank content into
`ActivityMaterializationJob`'s AI prompt (`TopicHint`) for Vocabulary/Reading-primary-skill
Today patterns only; legacy freeform generation is the unchanged fallback everywhere else.
+13 backend tests (3,500 → 3,513 total). Discovered (fixed by Bugfix-D1A, this entry, above)
the `GenerationStatus` default-value bug. Full detail:
`docs/architecture/learning-activity-engine.md`.

**Before that:** Phase E6 — First Real English Resource
Depth (2026-07-08, `0c46519d`). Added an original, internally-authored, English-only seed pack —
32 vocabulary entries (A1-B2), 12 grammar profile entries, 10 short reading excerpts — routed
through the full staging→analysis/validation→approval→publish pipeline via
`InternalResourceSeedPackSeeder`, no direct final-table seeding. +14 backend tests (3,500 total).
Full detail: `docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Plan-Sync-E6-Decision — Choose E6 before
Today composer (2026-07-08, `97c4d35e`, docs-only — no app code, migrations, or config
changed). Resolved the Phase D1 decision checkpoint opened by Phase E5: continue with Phase
E6 before Phase D1. Full detail: `docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Phase E5 — Published Bank Browsing, Search, and Admin
Management (2026-07-08, `394bb4ff`). Added `ResourceBankQueryService` — list + detail queries
for `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference` (search text, CEFR
level, source id filters; pagination capped at 200; sort newest-first by `CreatedAt`). **Key
design finding**: none of the three published bank entities carries a forward reference to the
`ResourceCandidate` that produced it — traceability is a **reverse lookup** matching
`ResourceCandidate.PublishedEntityType`/`PublishedEntityId` against the bank row being viewed,
never throwing when no match exists. New admin API and 3 new read-only admin pages — **no edit
or delete actions**, mutation remains exclusively on Resource Candidates. +31 backend tests. Full
detail: `docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Plan-Sync-After-E4 — Move E5 before D1 (2026-07-08,
`4849875d`, docs-only). Although Phase D1's "E0-E4 before D1" technical gate was met, decided to
sequence Phase E5 before Phase D1 since the published banks held only small synthetic/test data
with no browsing/search/admin-management surface. Full detail:
`docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Phase E4 — Publish Approved Resource
Candidates to First English Banks (2026-07-08, `ab4e2d1d`). Added new
`ResourceCandidate.Approve(notes?)`/`.Reject(reason)` methods (separate from validation) and
`ResourceCandidatePublishService`, which re-checks every gate **live** at publish time rather
than trusting an earlier staging/validation snapshot (English-only,
`CefrResourceSource.IsImportApproved`, `AllowsStudentDisplay`/`AllowsCommercialUse` —
**hard-blocked here**, unlike E2's validation pass which only warns for this — `ValidationStatus
== Passed`, `ReviewStatus == Approved`) and is idempotent (a repeated publish returns the existing
published-entity reference, never a duplicate row). **Candidate-type decisions**:
`VocabularyEntry`→`CefrVocabularyEntry` and `GrammarProfileEntry`→`CefrGrammarProfileEntry` are
fully supported; `ReadingPassage`→`CefrReadingReference` is supported **only for staged text
≤500 characters**, since that entity's own doc comment says it holds "only a short excerpt/
citation, not a full copyrighted text" — longer passages are blocked with a clear error rather
than silently truncated; `ActivityTemplateCandidate` publishing is **deferred entirely** —
`ActivityTemplate` needs a stable Key, valid Skill/Subskill, and real hand-authored
`GenerationInstructions` that a CSV/JSON-staged row was never designed to carry, and inventing
placeholder text to force it through would publish something dishonest. New approve/reject/
publish admin endpoints; admin UI gained Approve/Reject/Publish actions (Publish disabled with a
clear reason when ineligible) and a published-state indicator. +16 backend tests. **Known
limitation vs. the original E0 plan**: no "preview must be opened before approval" tracking
exists (E3 didn't build a preview-viewed flag) — the underlying safety property holds in practice
(preview is one click away on the same page) but isn't mechanically enforced. Full detail:
`docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Phase E3 — Admin Rendered Preview for Resource
Candidates (2026-07-08, `c9831599`). Added `GET .../preview` (`ResourceCandidatePreviewService`),
bank-type-specific rendered models, read-only, student-visible/admin-only separation. +14
backend tests. Full detail: `docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Phase E2 — AI Analysis, Rule Validation, Dedup/
Fingerprint, and Candidate Quality Gates (2026-07-08, `18015671`). Implemented gates 4-6:
AI-advisory analysis (`ResourceCandidateAnalysisService`) plus fully deterministic rule
validation and exact-fingerprint dedup (`ResourceCandidateValidationService`, sole authority on
`ValidationStatus`). +21 backend tests. Full detail:
`docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Phase E1 — English Resource Source Registry, Import
Runs, Raw Records, and Candidate Staging (2026-07-08, `874ee423`). Implemented the first Phase E
slice: `CefrResourceSource` extended as source registry, new `ResourceImportRun`/
`ResourceRawRecord`/`ResourceCandidate` staging entities, gates 1-3 (English-only,
license/source-approval, parser), CSV/JSON/JSONL import, admin CRUD/API/UI for Sources/Import
Runs/Candidates. Zero rows published. Full detail:
`docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Plan-Sync-PG-v2 — Add skill-first Practice Gym v2 to
later roadmap (2026-07-08, `23aa3e2c`, docs-only). Added a future **Practice Gym v2** track to
the roadmap — after Phase E5-E8 and before Phase F/G. Practice Gym should eventually let students
choose or be guided toward a skill/subskill/weak-area/objective/review/challenge/recommended-
practice target rather than a raw internal exercise type; the system should internally select the
best format. **`ExerciseTypeDefinition`/`ExercisePatternDefinition` are NOT deleted** — reframed
as an internal capability registry. Full detail: `docs/architecture/practice-gym.md`'s "Future
target: skill-first Practice Gym" section and `docs/backlog/product-backlog.md`'s "Practice Gym
v2" section.

**Before that:** Phase E0 — English Resource Bank Import Platform final
model and implementation plan (2026-07-08, `0fa92a25`, planning/docs only). Finalized the entity
model for the not-yet-started Phase E platform: the source registry reuses the existing
`CefrResourceSource` entity directly (supersedes the earlier informal proposal to add a separate
`ResourceImportSource`); new staging entities `ResourceImportRun`/`ResourceRawRecord`/
`ResourceCandidate`; published resources use a **hybrid model** — E4 reuses the existing typed
`CefrVocabularyEntry`/`CefrGrammarProfileEntry` (no new polymorphic `ResourceBankItem` table).
Finalized a 7-gate status/gate model reusing `AdminReviewStatus`,
`IFormIoSchemaValidationService`, `IActivityContentFingerprintService`, and `IFileStorageService`.
Defined E1's exact scope and E2-E4 boundaries, plus an admin nav plan. Full detail:
`docs/architecture/english-resource-bank-import-platform.md`.

**Before that:** Phase C-Final — Practice Gym bank-first migration closure
and readiness audit (2026-07-08, `5279c083`). Verified all 8 template-enabled Practice Gym keys
(templates approved/published, seeders idempotent, schemas leak-safe, gating/fallback/novelty/
feedback-policy wiring intact); produced a definitive 33-row pattern audit table (8
template-enabled, 25 legacy, corrected from an off-by-one "26 legacy" figure that had propagated
through Phase C3's own docs); added 4 explicit backlog entries for the deferred pattern families
(listening/audio, speaking/audio, open-ended AI-evaluated, fuzzy/short-answer). **Closes the
deterministic Practice Gym migration track — no Phase C4.** Full detail:
`docs/architecture/practice-gym.md`.

**Before that:** Phase C3 — Continue Practice Gym bank-first migration (2026-07-08, `ce4d76c6`).
Migrated **exactly one** additional pattern, `reorder_paragraphs`, to the bank-first Form.io
template path via a new generic `ordered_sequence` `ComponentAnswerScorer` kind and a stock
Form.io `datagrid` component (no new custom Form.io component, no frontend code changes). +8
backend tests (3,371 → 3,379). 8 of 33 pattern rows template-enabled.

**Before that:** Phase B2 — Activity Feedback, Repeat Policy, and Calibration Signals (2026-07-08,
`08de5c70`). Implemented the persistence/API/minimal-UI foundation for explicit student feedback
on completed activities across both Today and Practice Gym. **This is a foundation, not a
calibration engine** — nothing yet automatically consumes this data for CEFR/difficulty-band
calibration, template/resource quality scoring, novelty/cooldown adjustment, or admin review; it
is collected and queryable for future phases. Full detail:
`docs/architecture/activity-feedback-and-calibration.md`.

Preceded by Plan-Sync-B2 — docs-only roadmap update inserting Phase B2 ahead of Phase C3
(2026-07-08, `5536ad07`), Phase C2 — Expand Practice Gym bank-first template coverage to the next
safe batch (`c84279a0`, 7 of ~28 pattern keys template-enabled at the time), Plan-Sync-After-C1
(`2b099e5b`), Phase C1 — Generalize the Form.io Practice Gym pilot to a small first batch of
patterns (`fd996acc`), Phase B — Repetition/Novelty Foundation (`7b425f02`), Clean-A/Clean-A2
cleanup (`1bada3c1`), and the 2026-07-07 bank-first architecture (Phases 1-10, `ac68677d`). See
§19/§19a for the full decision log and current phase sequence. Today lesson generation is
unmodified throughout.

**Latest phase confirmed live against `speakpath.app`:** Phase 20H — Live Pilot Stabilization
(2026-07-03) — see the entry below; everything after this line is developed/tested locally only.

**Branch:** main

**Test totals (as of Bugfix-D1A, 2026-07-08, local only):**
- Backend: 3,518 passed (5 architecture + 2,044 unit + 1,469 integration), 0 failed — net +5 vs Phase D1 (3,513): new `LearningSessionGenerationStatusPersistenceTests` (5 tests, all under `IntegrationTests`: Pending set before first save round-trips as Pending; Ready-by-constructor-default round-trips as Ready; Ready set explicitly before first save round-trips as Ready; Failed set before first save round-trips as Failed; Pending→Ready across two separate saves round-trips correctly). One pre-existing test corrected: `LessonBatchGenerationJobTests`'s assertion changed from `GenerationStatus.Ready` to `GenerationStatus.Pending` — it had been unknowingly asserting the bug's symptom (`ActivityMaterializationJob`, the only caller of `MarkGenerationReady`, never actually runs in that test).
- Angular unit (Karma): not run this phase — no frontend files touched (Bugfix-D1A is backend-only); baseline unchanged at 120 pre-existing failures.
- Angular production build (`ng build --configuration production`): still fails on the pre-existing `initial` bundle-size budget (1.56MB over the 1MB threshold) — confirmed not a new regression, no frontend files changed.
- Playwright E2E: not run this phase — no UI changed; `e2e/core-flow-smoke.spec.ts` remains `test.skip`'d (see Clean-A2 decision log entry).

**Test totals (as of Phase D1, 2026-07-08, local only):**
- Backend: 3,513 passed (5 architecture + 2,044 unit + 1,464 integration), 0 failed — net +13 vs Phase E6 (3,500): new `TodayBankResourceSelectorTests` (11 tests: matching vocabulary/opportunistic-grammar/reading selection by CEFR, unsupported-pattern skip, empty-bank graceful result, novelty-blocked exclusion, English-only regression guard, discovers E6 seed-pack content — all under `UnitTests`) and `ActivityMaterializationJobBankFirstTests` (2 tests: bank context injected into `TopicHint` when matching bank rows exist at the routed CEFR level, unchanged legacy fallback when none exist — under `IntegrationTests`).
- Angular unit (Karma): not run this phase — no frontend files touched (D1 is backend-only); baseline unchanged at 120 pre-existing failures.
- Angular production build (`ng build --configuration production`): still fails on the pre-existing `initial` bundle-size budget (1.56MB over the 1MB threshold) — confirmed not a new regression, no frontend files changed in D1.
- Playwright E2E: not run this phase — no UI changed; `e2e/core-flow-smoke.spec.ts` remains `test.skip`'d (see Clean-A2 decision log entry).

**Test totals (as of Phase E6, 2026-07-08, local only):**
- Backend: 3,500 passed (5 architecture + 2,033 unit + 1,462 integration), 0 failed — net +14 vs Phase E5 (3,486): new `InternalResourceSeedPackSeederTests` (source idempotency, import-run/raw-record/candidate creation, deterministic CEFR/skill/subskill mapping distinct from AI output, validation without AI, publish to all 3 target tables, traceability, full-rerun idempotency, no-direct-final-table-bypass guarantee) — all under `UnitTests`, no new `IntegrationTests`.
- Angular unit (Karma): not run this phase — no frontend files touched (E6 is backend/seed-data only); baseline unchanged at 120 pre-existing failures.
- Angular production build (`ng build --configuration production`): still fails on the pre-existing `initial` bundle-size budget (1.56MB over the 1MB threshold) — confirmed not a new regression, no frontend files changed in E6.
- Playwright E2E: not run this phase — no UI changed; `e2e/core-flow-smoke.spec.ts` remains `test.skip`'d (see Clean-A2 decision log entry).

**Test totals (as of Plan-Sync-E6-Decision, 2026-07-08, local only — unchanged from Phase E5 since that phase was docs-only):**
- Backend: 3,486 passed (5 architecture + 2,019 unit + 1,462 integration), 0 failed — net +31 vs Phase E4 (3,455): new `ResourceBankQueryServiceTests` and `AdminResourceBankEndpointTests` (list/filter/pagination/detail-traceability for all 3 bank types, unpublished-candidate-never-appears invariant, no-matching-candidate detail case, admin-only auth).
- Angular unit (Karma): not run this phase — same judgment call as E1-E4, no dedicated Angular test suite exists for the resource-import admin pages to extend cheaply; baseline unchanged at 120 pre-existing failures.
- Angular production build (`ng build --configuration production`): still fails on the pre-existing `initial` bundle-size budget (1.56MB over the 1MB threshold) — confirmed not a new regression, no new TypeScript/template compile errors from the E5 bank-browsing pages.
- Playwright E2E: not run this phase — no new routed *existing* UI behavior changed, only new read-only admin-only pages; `e2e/core-flow-smoke.spec.ts` remains `test.skip`'d (see Clean-A2 decision log entry).

**Test totals (as of 20D, last live-confirmed baseline):**
- Backend unit: 1,750 (+20 from Phase 20D: `StudentReadinessAuditServiceTests`, `StudentPilotReadinessRepairServiceTests`)
- Backend integration: 1,378 (+8 from Phase 20D: `AdminStudentReadinessEndpointTests` — auth, 404, dry-run/real repair via API, repair visibly fixes a check, no secrets/prompts in response)
- Architecture: 3
- **Backend total: 3,131**
- Angular unit (Karma): 1,548/1,668 success (120 pre-existing failures in `AdminStudentDetailComponent`/`AdminAiConfigComponent`, unchanged baseline, 0 new regressions; +10 new pilot-readiness-panel tests)
- Playwright E2E: unchanged (no existing admin-student-detail Playwright pattern to extend cheaply)

**Build:** Clean production build. No known open build errors.

**Deployment:** Docker Compose (API + PostgreSQL). MinIO for file storage. AI providers: OpenAI, Gemini, Anthropic (configurable per feature key). Real SMTP email delivery supported.

---

## 2. Executive Summary

SpeakPath is an AI-powered English language learning SaaS targeting adult learners — particularly immigrant professionals. The backend is .NET 10 Clean Architecture. The frontend is Angular with Tailwind CSS and a custom `sp-*` design system. The database is PostgreSQL.

The project has progressed through approximately 80+ named phases. Many phases in the completed phase timeline (Section 4) group multiple implementation sub-phases, bug-fix commits, and review passes into a single summarized row; the actual commit and review count is higher. Areas covered: platform foundation, security hardening, notification infrastructure, admin platform, curriculum and activity engine, adaptive placement, the full student learning journey, voice recording, and asynchronous AI speaking evaluation.

As of Phase 16J, all six student pages are functionally complete, the speaking evaluation pipeline is operational behind a config gate, and mastery signals from AI evaluation are applied conservatively under strict invariants. Signal thresholds are now configurable (positive requires overall/completeness/relevance ≥ 80; review requires overall ≤ 55); a middle band (56–79) produces no signal. A safety summary endpoint confirms all three invariants programmatically. Admin visibility now shows per-attempt applied signal state with invariant labels.

**What remains:** AI writing evaluation, writing mastery signals, advanced feedback UX, enterprise org model, full production hardening, observability stack, and long-term product polish.

---

## 3. Completed Epics

| Epic | Phases | Status | Key Deliverable |
|------|--------|--------|----------------|
| Platform Foundation | T1–T6 | Complete | Clean Architecture skeleton, PostgreSQL, Angular, JWT auth, Docker/CI |
| First AI Writing Exercise | T7–T8 | Complete | Writing exercise generation, feedback display, AI usage logging |
| Learning Engine | T9 | Complete | Vocabulary mastery, LearningPlanner, spaced repetition |
| Authentication / Security Hardening | 10Auth-F-0 to 10Auth-F-FINAL | Complete | Lockout, rate limiting, refresh tokens, Google OAuth, audit log, security UI |
| Enterprise Notification Platform | 10W-0 to 10W-FINAL-2 | Complete | In-app, email (SMTP), SMS foundation, dispatch job, templates, preferences, data protection |
| AI Usage / AI Config Admin | 10U series | Complete | Summary, filters, pagination, CSV export, trend, pricing config, zero-cost alert |
| Admin UI Redesign | 10UI series + STYLE-1 | Complete | Full 14-route admin UI aligned to SpeakPath reference design; `sp-admin-*` component library |
| Admin Onboarding Builder | Phase 11A | Complete | Configurable onboarding flows with step management |
| Curriculum Objective Coverage | Phase 11B | Complete | 33 objectives A1–B2, validation service, coverage admin UI |
| Readiness Pool | Phase 10Y, 10Z, 12A, 12C | Complete | Pool lifecycle, mastery evaluation engine, pool health, replenishment pipeline |
| Learning Plan Orchestrator | Phase 12D–12G | Complete | 10-objective plan, guided routing, completion lifecycle, real-time progress |
| Adaptive Placement Engine | Phase 13A | Complete | Deterministic 72-item bank, per-skill CEFR scoring, admin API |
| Student Placement Journey | Phase 14A | Complete | Full end-to-end student adaptive placement flow |
| CourseReady Lifecycle | Phase 14B | Complete | Post-placement transition, dashboard preparing state |
| Student Dashboard + Summary | Phase 15A–15B | Complete | Learning Plan powered dashboard, consolidated summary endpoint |
| Today Lesson Player | Phase 15C | Complete | Session stepper, exercise navigation, CEFR badge, preparing state |
| Practice Gym | Phase 15D | Complete | Adaptive suggestions, review queue, explanation display, admin parity |
| Student Progress Page | Phase 15F | Complete | CEFR arc, skill bars, mastery grid, focus recommendations, recent activity |
| Student Profile / Preferences | Phase 15G | Complete | Learning preferences edit, placement summary, CEFR read-only enforced |
| Student QA / Flow Hardening | Phase 15H | Complete | 18 E2E smoke tests, route guard audit, mobile verification |
| Student UI Visual Rehaul | Phase 15I | Complete | Skeleton shimmer, mobile FAB label, sp-stat-grid, sp-skill-tag cleanup |
| Activity Completion / Feedback | Phase 16B | Complete | UUID guard fix, feedback-pending card, encoding fixes |
| Audio / TTS Reliability | Phase 16C | Complete | AudioPlayerComponent loading/retry state, player migration, repeat-sentence fix |
| Voice Recording Foundation | Phase 16D | Complete | VoiceRecorderComponent, AudioResponseComponent, audio-attempt endpoint |
| Speaking Submission Visibility | Phase 16E | Complete | Admin speaking submissions card, admin audio stream endpoint |
| AI Speaking Evaluation Foundation | Phase 16F | Complete | Async evaluation pipeline, NoOp/config-gated provider, student polling |
| Speaking Evaluation Quality Validation | Phase 16H | Complete | Dry-run signal mapper, quality summary admin endpoint, per-attempt dry-run fields |
| Speaking Mastery Signal Integration | Phase 16I | Complete | Config-gated signal application job, audit entity, 5-gate pipeline, admin summary |

---

## 4. Completed Phase Timeline

| Order | Phase | Area | Date | Summary |
|------:|-------|------|------|---------|
| 1 | T1–T6 | Platform Foundation | Pre-2026-06 | Solution structure, PostgreSQL, Angular, Docker, CI/CD, JWT auth |
| 2 | T7–T8 | First AI Feature | Pre-2026-06 | Writing exercise generation and feedback |
| 3 | T9 | Vocabulary / Learning | Pre-2026-06 | Spaced repetition, LearningPlanner |
| 4 | Admin UX Cleanup | Admin | 2026-06-09 | Student management, AI config, archive, toast, reset |
| 5 | Onboarding / Post-Placement Alignment | Student Journey | 2026-06-09 | Free-text career context, goals, guard fixes |
| 6 | Listening Comprehension Text MVP | Activity | 2026-06-09 | Hidden transcript, comprehension Qs, scoring |
| 7 | Listening Audio/TTS | Activity | 2026-06-10 | ITextToSpeechService, audio storage, audio player |
| 8 | TTS / Placement / Today | Activity | 2026-06-10 | Server-side TTS for placement listening |
| 9 | Speaking Role Play MVP | Activity | 2026-06-08 | SpeakingRolePlay activity type, fake STT, audio upload, AI eval |
| 10 | Vocabulary Practice Activity | Activity | 2026-06-09 | Deterministic vocabulary fill-blank, scoring, history |
| 11 | Vocabulary Extraction | Learning Engine | 2026-06-12 | Post-submit vocabulary extraction from corrections |
| 12 | Exercise Pattern Engine | Activity | 2026-06-10–15 | Exercise pattern library, renderer coverage (Phases 8a–8n) |
| 13 | Student UX Alignment | Student Journey | 2026-06-10 | Writing assumption cleanup |
| 14 | Practice Gym Activation | Activity | 2026-06-10 | Pool-backed Practice Gym |
| 15 | Adaptive Learning Foundation | Learning Engine | 2026-06-12 | AiStructured/AiOpenEnded patterns, scoring pipeline |
| 16 | Lesson Buffer / MinIO | Infrastructure | 2026-06-11 | Background lesson generation, MinIO file storage |
| 17 | Exercise UX / Admin Polish | Admin | 2026-06-12 | Submission scoring bug fixes, admin lesson page |
| 18 | Activity 3-Page Restructure | UX | 2026-06-13 | Teach/Learn/Practice 3-page activity flow |
| 19 | Audit / Bug Fix Plan | QA | 2026-06-14 | Admin E2E audit, bug bash |
| 20 | Learn/Practice/Feedback Restructure | UX | 2026-06-14 | Lesson structure realignment |
| 21 | Admin Student Detail Page | Admin | 2026-06-14 | Student detail, learning memory, activity history |
| 22 | Activity Teach Page Micro-Lesson | Activity | 2026-06-15 | Micro-lesson content for Teach page |
| 23 | Practice Gym Pool Foundation | Learning Engine | 2026-06-15 | Readiness pool architecture decision |
| 24 | Phase 8a–8n: Activity Renderer Coverage | Activity | 2026-06-15–16 | 14 exercise pattern renderers implemented and tested |
| 25 | Phase 10Y: Activity Lifecycle Completion | Learning Engine | 2026-06-26 | Skipped status, CEFR mismatch demotion, pool health fields |
| 26 | Phase 10Z: Mastery Re-evaluation Engine | Learning Engine | 2026-06-26 | Deterministic mastery, daily sweep job |
| 27 | Phase 10U series: AI Usage/Config Admin | Admin | 2026-06-20–21 | Summary, filters, pagination, CSV, trends, custom date range |
| 28 | Phase 10V-3B: AI Pricing Zero-Cost Alert | Admin | 2026-06-21 | Zero-cost call detection and admin alert |
| 29 | Phase 10W series: Notification Platform | Platform | 2026-06-21–23 | Full in-app + email notification stack |
| 30 | Phase 10Auth series: Auth/Security | Security | 2026-06-23 | Lockout, rate limiting, refresh tokens, Google OAuth, audit log |
| 31 | Phase 10UI series: Admin UI Redesign | Admin UI | 2026-06-23 | 14-route admin UI redesign, sp-admin-* component library |
| 32 | Phase 11A: Admin Onboarding Builder | Admin | 2026-06-26 | Configurable onboarding flows |
| 33 | Phase 11B: Curriculum Objective Coverage | Curriculum | 2026-06-26 | 33 objectives, validation service |
| 34 | Phase 12A: Pool Health + Welcome Email | Platform | 2026-06-27 | Aggregate pool health, email routing audit |
| 35 | Phase 12C: Lesson Pipeline / Readiness | Learning Engine | 2026-06-27 | MinBuffer/MaxBuffer bounds, replenishment observability |
| 36 | Phase 12D: Learning Plan Foundation | Learning Engine | 2026-06-27 | 10-objective plan, domain entities, migrations |
| 37 | Phase 12E: Learning Plan Guided Routing | Learning Engine | 2026-06-27 | Preferred objective routing, InProgress status |
| 38 | Phase 12F: Learning Plan Completion | Learning Engine | 2026-06-27 | Objective lifecycle Active→InProgress→Completed→Mastered |
| 39 | Phase 12G: Real-Time Plan Progress | Learning Engine | 2026-06-27 | Submission-path plan progress, CurrentObjectiveKey |
| 40 | Phase 13A: Adaptive Placement Engine | Placement | 2026-06-27 | 72-item bank, per-skill scoring, admin API |
| 41 | Phase 14A: Student Placement Journey | Student Journey | 2026-06-27 | Full adaptive placement flow, student API, Angular state machine |
| 42 | Phase 14B: CourseReady Transition | Student Journey | 2026-06-27 | Post-placement lifecycle, dashboard preparing |
| 43 | Phase 15A: Learning Plan Dashboard | Student Journey | 2026-06-27 | LP-powered dashboard |
| 44 | Phase 15B: Dashboard Summary API | Student Journey | 2026-06-27 | Consolidated summary endpoint, 8 named sections |
| 45 | Phase 15C: Today Lesson Player | Student Journey | 2026-06-28 | Session stepper, exercise renderer, CEFR badge |
| 46 | Phase 15D: Practice Gym Experience | Student Journey | 2026-06-28 | Adaptive suggestions UI, review queue, admin parity |
| 47 | Phase 15F: Student Progress | Student Journey | 2026-06-28 | Full progress page, CEFR arc, mastery grid |
| 48 | Phase 15G: Student Profile/Preferences | Student Journey | 2026-06-28 | Preferences edit, placement summary, CEFR read-only |
| 49 | Phase 15H: Student QA / Flow Hardening | QA | 2026-06-28 | 18 E2E smoke tests, guard audit, mobile |
| 50 | Phase 15I: Student UI Visual Rehaul | UX | 2026-06-28 | Skeleton shimmer, FAB label, stat grid, chip cleanup |
| 51 | Phase 16B: Activity Feedback Hardening | QA | 2026-06-28 | UUID guard fix, feedback-pending card |
| 52 | Phase 16C: Audio/TTS Reliability | QA | 2026-06-28 | AudioPlayerComponent state machine, player migration |
| 53 | Phase 16D: Voice Recording Foundation | Speaking | 2026-06-28 | VoiceRecorderComponent, audio-attempt endpoint |
| 54 | Phase 16E: Speaking Submission Visibility | Admin | 2026-06-28 | Admin speaking submissions card, audio stream |
| 55 | Phase 16F: AI Speaking Evaluation | AI | 2026-06-28 | Async evaluation pipeline, Quartz job, student polling |
| 56 | Phase 16H: Speaking Evaluation Quality | AI | 2026-06-30 | Dry-run signal mapper, quality summary endpoint |
| 57 | Phase 16I: Speaking Mastery Signals | AI | 2026-06-30 | Config-gated signal job, audit entity, 5-gate pipeline |
| 58 | Phase 16J: Speaking Signal Quality Tuning | AI | 2026-06-30 | Configurable thresholds, safety summary endpoint, middle band (56–79)=NoSignal, per-student applied signal visibility |
| 59 | Phase 17B: Writing Evaluation Quality Validation | AI | 2026-06-30 | Dry-run signal mapper, quality summary admin endpoint, per-attempt dry-run fields |
| 60 | Phase 17C: Writing Mastery Signal Controlled Integration | AI | 2026-06-30 | Config-gated signal application job, audit entity, 5-gate pipeline, admin summary |
| 61 | Phase 18A: Lesson Quality and Content Generation Upgrade | AI / Quality | 2026-07-01 | CEFR calibration tables in writing/listening/speaking prompts; support-language optional in 6 prompts; CEFR-aware pattern selection in batch planner; validator: empty-string check + option ID consistency |
| 62 | Phase 18A-F: Generation Quality Admin Visibility | Admin / Quality | 2026-07-01 | GenerationValidationFailure entity + T69 migration; generation validation failures persisted from AiActivityGeneratorHandler; GET /api/admin/generation-quality/summary endpoint; Generation Quality card on Diagnostics page; prompt SeededAtUtc visibility; privacy/safety hardened |
| 63 | Phase 19A: Review Scaffold Controlled Enablement | Readiness Pool | 2026-07-02 | Source restriction, per-student daily cap, deterministic confidence banding, global admin-review hold flag (T71 migration); ReadinessPool appsettings section added; admin dry-run summary + pending-review endpoint; EnableReviewScaffoldGeneration remains false by default |
| 64 | Phase 19B: Review Scaffold Per-Item Admin Approval | Readiness Pool / Admin | 2026-07-02 | `AdminReviewStatus` per-item state (T72 migration) with Approve/Reject/Reopen transitions + idempotency guards; admin API + audit log (`AdminAuditLog`); Practice Gym gate updated to require per-item Approved; admin UI approval table with Approve/Reject/Reopen actions; global safety gates (EnableReviewScaffoldGeneration/DryRunOnly/RequireAdminReview/AllowTodayLessonInsertion) unchanged |
| 65 | Phase 19C: Review Scaffold Practice Gym Pilot Rollout | Readiness Pool / Admin | 2026-07-02 | `PracticeGymPilotEnabled` gate (default false) layered on top of 19A/19B; friendly non-negative student-facing pilot label/reason override; `MaxStudentVisibleScaffoldSuggestions` cap; admin pilot-summary endpoint + monitoring card; instantly-reversible rollback with no data deletion; Today lesson insertion still disabled by default |
| 66 | Phase 20A: Admin AI Operations Dashboard | Admin / AI | 2026-07-02 | Read-only `GET /api/admin/ai-operations/summary` aggregating existing speaking/writing evaluation, generation quality, AI usage, and readiness-pool/pilot services; new `/admin/ai-operations` page (provider/model usage, evaluation queue counts, generation failures, 10-flag signal safety gate card, combined recent-failures table); no new AI behaviour, scoring, or mutation path |
| 67 | Phase 20B: Admin Runtime Settings & Feature Gates | Admin / Platform | 2026-07-02 | Typed feature-gate registry (8 groups) + `RuntimeSettingOverride` table for previously appsettings-only review-scaffold/Practice-Gym-pilot flags; existing `LessonGenerationSettings` table wrapped by the same registry; AI signal-safety gates surfaced read-only; new `/admin/settings/feature-gates` page with slide-in drawer, `?gate=` deep links, server-side validation, typed-`CONFIRM` for High/Critical risk changes, `AdminAuditLog` on every change/reset; Admin Lessons/AI Operations pages now link to it instead of showing static config text; control-plane only — no AI/CEFR/objective/Learning-Plan/review-scaffold runtime behaviour changed |
| 68 | Phase 20C: Runtime Settings Effective Wiring | Admin / Platform | 2026-07-02 | New `IEffectiveReadinessPoolSettingsProvider` wires review-scaffold/Practice-Gym-pilot admin overrides into `ReadinessPoolReplenishmentService`/`PracticeGymSuggestionService` (fresh DI scope per job/request = no caching needed); fixed a pre-existing gap where `DryRunOnly` was displayed but never enforced in the real generation path; lesson-generation-buffer settings confirmed already runtime-effective (jobs read the same DB row admin writes to); 7 unconsumed lesson-generation fields marked "display only" rather than inventing new enforcement behaviour (`TODO-20C-1`); AI signal-safety gates untouched/still locked; defaults unchanged when no override exists |
| 69 | Phase 20D: Student Data Readiness, Backfill & Pilot Cleanup | Admin / Platform | 2026-07-02 | New `IStudentReadinessAuditService` (~20 read-only checks: account, placement/CEFR, Learning Plan, Today lesson, Practice Gym, activity content, audio/TTS, review scaffold, progress) and `IStudentPilotReadinessRepairService` (4 real, idempotent, audited repair actions — generate missing plan, refill empty Today lesson, expire CEFR-invalid readiness items, expire stale reserved items — plus run-all; 5 further suggested actions registered as "Not implemented yet" with a documented reason, `TODO-20D-1..4`); new admin API `GET/POST /api/admin/students/{id}/readiness[/repair\|/repair-safe-all]`; new "Pilot readiness" panel on Admin Student Detail with a reason-required repair slide-over; never deletes attempts/submissions/evaluations; every real repair requires a reason and writes one `AdminAuditLog` row; no AI/CEFR/objective/Learning-Plan-regeneration behaviour changed |

---

## 5. Current Architecture and Product Capabilities

### Backend

- **.NET 10 Clean Architecture**: Domain / Application / Infrastructure / Persistence / Api / Worker
- **PostgreSQL** with EF Core and hand-authored migrations (T1–T66)
- **Quartz.NET** background jobs: lesson generation, practice gym generation, mastery sweep, speaking evaluation, speaking signal application, notification dispatch
- **MinIO** file storage: listening audio, speaking audio, placement audio
- **AI providers**: OpenAI, Gemini, Anthropic — configurable per feature key via `AiProviderConfig`; fallback provider support; cost tracking per call
- **Notification platform**: in-app (live bell), email (SMTP), SMS foundation, templates, preferences, data-protection encryption
- **Auth**: JWT + refresh tokens (rotation, reuse detection, hash-only storage), password lockout, IP rate limiting, Google OAuth, audit log, security UI

### Frontend

- **Angular 19** (standalone components, Signals-based state)
- **Tailwind CSS** with custom `sp-*` student design tokens and `sp-admin-*` admin component library
- **14 admin routes** all using `sp-admin-*` wrappers, aligned to SpeakPath reference design
- **6 student routes** all functionally complete: Dashboard, Today, Practice (Gym), Journey, Progress, Profile

### Learning Model Hierarchy

```
LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity → ActivityAttempt
```

Practice Gym uses `LearningActivity` directly. A completed activity = at least one `ActivityAttempt` submitted.

### Exercise Pattern Library

18+ renderer/pattern variants implemented: GapFill, ChatReply, ReadAloud, RepeatSentence, RespondToSituation, ReadingMultipleChoiceSingle, ReadingMultipleChoiceMulti, ReadingFillInBlanks, ReorderParagraphs, ReadingWritingFillInBlanks, SummarizeWrittenText, WriteEssay, ListeningMultipleChoiceSingle, ListeningMultipleChoiceMulti, ListeningFillInBlanks, SelectMissingWord, HighlightCorrectSummary, HighlightIncorrectWords. Additional legacy patterns (VocabularyPractice, ListeningComprehension, WritingScenario) are handled via separate activity type branches.

### AI Flow

```
PostgreSQL → LearningPlanner / AiContextBuilder → IAiProvider → validated JSON → saved result → UI
```

Every provider call tracked: featureKey, provider, model, userId, isFallback, wasSuccessful, token counts, cost, correlationId.

---

## 6. Current Student Experience

1. **Onboarding**: Free-text career context, language goals, focus skills, support language.
2. **Placement**: Adaptive 72-item bank (6 skills × 4 CEFR levels), deterministic scoring, per-skill CEFR result.
3. **CourseReady transition**: After placement, student transitions to `CourseReady`; Learning Plan generated.
4. **Dashboard**: Consolidated summary (profile, course readiness, today session, learning plan, practice, progress, stats, warnings). CEFR-aware lifecycle states.
5. **Today Lesson**: Session stepper, exercise navigation, CEFR badge, preparing/error states.
6. **Practice Gym**: Adaptive suggestions (pool-backed), review queue, explanation, empty/retry states.
7. **Journey**: Learning Plan objectives with InProgress/Active/Completed/Mastered status.
8. **Progress**: CEFR arc, skill bars, mastery grid, focus recommendations, recent activity timeline.
9. **Profile**: Learning preferences edit, placement summary, CEFR read-only enforced, notification prefs.
10. **Activity Player**: 18+ exercise patterns, audio player with retry, speaking audio submission, feedback-pending card.
11. **Speaking**: Voice recording (VoiceRecorderComponent), audio upload (audio-attempt endpoint), async AI evaluation polling (10s intervals, max 12 polls), Completed/Pending/Failed/NotSupported states.

---

## 7. Current Admin Experience

- **Dashboard**: Live KPI strip, weekly snapshot banner, onboarding funnel, at-risk students, CEFR distribution.
- **Students**: List with search/filter/sort/pagination, create student, student detail page.
- **Student Detail**: Hero section, lifecycle/CEFR/pool badges, KPI strip, Learning Plan, Placement, Practice Gym, Progress, Speaking Submissions, activity history, danger zone.
- **AI Config**: Per-feature provider/model/fallback config.
- **AI Usage**: Summary, trend, recent calls, filters (provider/model/feature/status/student/date), CSV export, zero-cost alert.
- **Lessons**: Pool health dashboard, replenishment controls, batch management, admin generate button.
- **Curriculum**: Objectives list, validation summary, coverage gaps.
- **Onboarding**: Flow configuration, step management.
- **Notifications**: In-app, email, SMS config, templates, test-send.
- **Security**: Auth events log, password policy, rate limit summary, refresh token config, Google OAuth config.
- **Speaking Evaluation**: Applied signal summary, quality metrics, per-student dry-run outcomes.

---

## 8. Current AI / Evaluation Capabilities

| Capability | Status | Config Gate | Notes |
|------------|--------|-------------|-------|
| Writing scenario generation (AI) | Active | `AI__WritingFeedback__Provider` | Generates exercises via AI; uses pattern key routing |
| Writing evaluation / feedback (AI) | Active | Per-feature key | AiStructured / AiOpenEnded patterns score submissions and return feedback |
| Vocabulary extraction | Active | Always on | Post-submit, best-effort |
| Listening audio TTS | Active | `TTS_PROVIDER` | MinIO-backed; real provider configurable |
| Student memory update | Active | Best-effort | 8s timeout, post-submit |
| Learning Plan generation | Active | Always on | Deterministic, triggered by placement/preference/CEFR change |
| Placement evaluation | Deterministic | N/A | No AI calls; 72-item bank, scoring algorithm |
| Speaking evaluation | Config-gated | `SpeakingEvaluation__Enabled` (default false) | Async pipeline; NoOp provider by default; student polls for result |
| Speaking mastery signals | Config-gated | `SpeakingEvaluation__ApplyMasterySignals` (default false) | Review signals only; CEFR update = never; objective completion = never |
| Review scaffold generation | Not enabled globally | Deferred | Dry-run infrastructure exists |
| Provider-backed writing evaluation pipeline | Not implemented | N/A | Phase 17A target; writing feedback today is AI-generated but not via a dedicated evaluation pipeline with mastery integration |
| Writing mastery signals | Implemented (controlled) | Phase 17C | Config-gated 5-gate pipeline; `ApplyMasterySignals` defaults false; review signals only by default; CEFR/objective/LP-regen permanently disabled |
| STT (speech-to-text) pipeline | Not implemented as reusable service | N/A | `FakeSpeechToTextService` used for legacy SpeakingRolePlay; speaking evaluation provider may do transcription internally when a real provider is configured; no standalone `ISpeechToTextService` is wired to a real provider |
| Real-time AI conversation | Deferred | N/A | Call Mode is P3; requires real STT + privacy review |

---

## 9. Current Test and Quality Position

| Layer | Count | Notes |
|-------|------:|-------|
| Backend unit | 1,565 | Domain + Application logic |
| Backend integration | 1,281 | API + EF Core against SQLite in-memory |
| Architecture | 3 | NetArchTest layer boundary enforcement |
| Angular unit | 1,525 | Karma/Jasmine, headless Chrome |
| Playwright E2E | 262+ | Mocked-API smoke tests; some live-backend tests |

**Gaps:**
- No Playwright tests for speaking evaluation flow (live-backend)
- No live AI quality review run for full placement → lesson → speaking loop
- AI latency, audio duration, and per-call cost not yet stored on `SpeakingEvaluation`

---

## 10. Known Gaps

### Speaking Evaluation
- `SpeakingEvaluation` entity does not yet store latency, audio duration, or cost. Documented in Phase 16H review.
- No reusable real STT pipeline. `FakeSpeechToTextService` is used for legacy `SpeakingRolePlay`. Speaking audio submissions via `audio-attempt` have no STT at all. A real `ISpeechToTextService` provider is not yet wired.
- Pronunciation scoring claimed conservatively; no phoneme-level ASR provider wired.
- Admin audio playback is not yet wired in the admin Angular UI. The backend stream endpoint (`GET /api/admin/students/{id}/speaking-attempts/{attemptId}/audio`) exists and is secured, but the admin UI shows "Audio submitted — playback not available in admin yet." Bearer-token-aware blob streaming in the admin UI is deferred.

### Writing Evaluation
- AI writing evaluation mastery signals implemented in Phase 17C (controlled, default off). Positive signals disabled by default. Enable via `ApplyMasterySignals = true` in config.

### Review Scaffold
- Phase 19A added controlled-enablement gating (source restriction, per-student daily cap, deterministic confidence banding, global admin-review hold) but `EnableReviewScaffoldGeneration` still defaults `false` and `DryRunOnly` defaults `true`. Global enablement is an operator decision, not yet exercised in production.
- Phase 19B added per-item admin approval (`AdminReviewStatus`: PendingReview/Approved/Rejected, with Approve/Reject/Reopen endpoints + UI actions and an `AdminAuditLog` trail). An item now only reaches Practice Gym when it is individually `Approved` (or never required review) — the old "flip the global flag to release everything at once" behavior is gone. `EnableReviewScaffoldGeneration`/`DryRunOnly`/`RequireAdminReview`/`AllowTodayLessonInsertion` remain server-side config only; no global "enable" toggle exists in the admin UI.
- Phase 19C added a dedicated `PracticeGymPilotEnabled` gate (default `false`) layered on top of 19A/19B: an item can now be generated and individually approved while still hidden from students until this one flag flips, and flipping it back off hides everything again with no data deletion. Added friendly, configurable, non-negative student-facing copy (`PracticeGymPilotLabel`/`PracticeGymPilotReason`) and a scaffold-specific visible-suggestion cap (`MaxStudentVisibleScaffoldSuggestions`, default 2), plus an admin pilot-summary endpoint/monitoring card. `PracticeGymPilotEnabled=false` in production today — the pilot has not been switched on for real students yet.
- Phase 20B made `EnableReviewScaffoldGeneration`/`DryRunOnly`/`RequireAdminReview`/`AllowTodayLessonInsertion`/`ScaffoldAllowedSources`/`MinimumConfidenceForReviewNeed` and the Practice Gym pilot flags admin-runtime-editable (via `RuntimeSettingOverride`, no redeploy) through `/admin/settings/feature-gates` — the "server-side config only" limitation called out above is resolved for these specific flags. `ReadinessPoolReplenishmentService` itself still reads only appsettings, so the live replenishment behavior is unchanged until that read path is wired to the override table (deferred, see `TODOS.md`).

### Observability
- No production-level APM, distributed tracing, or alerting stack.
- AI cost/latency not tracked at per-call level in `SpeakingEvaluation`.
- Memory staleness detection (TODO-4) not implemented.
- Phase 20A added a read-only Admin AI Operations dashboard (`/admin/ai-operations`) aggregating speaking/writing evaluation, generation quality, AI usage, and readiness-pool/pilot state into one page — this narrows the gap but does not replace real APM/tracing. Provider health-check/ping and retry tooling remain deferred (see Phase 20A entry above).

### Admin Operational Config
- Phase 20B added a typed feature-gate registry and admin control plane (`/admin/settings/feature-gates`) for review-scaffold/Practice-Gym-pilot and lesson-generation settings, with validation, typed-`CONFIRM` for High/Critical risk changes, and `AdminAuditLog` audit trail. AI signal-safety gates (`ApplyMasterySignals`, `AllowReviewSignals`, `AllowPositiveSignals`, `AllowObjectiveCompletion`, `AllowCefrUpdate`) are surfaced for visibility but remain read-only this phase — changing them still requires an appsettings edit and redeploy. "AI can regenerate Learning Plan" has no dedicated flag in code; the registry shows this as an informational, locked entry rather than inventing one.
- Phase 20C wired the review-scaffold/Practice-Gym-pilot settings into the actual `ReadinessPoolReplenishmentService`/`PracticeGymSuggestionService` read paths (via `IEffectiveReadinessPoolSettingsProvider`), so admin edits now take effect on the next job run/HTTP request — resolving the limitation called out above for that specific group. Also fixed `DryRunOnly`, which existed since Phase 19A but was never actually enforced in generation. Lesson-generation-buffer settings were confirmed already effective (jobs read the same DB row admin writes). Seven lesson-generation fields (`MaxGenerationAttempts`, `GenerationTimeoutSeconds`, `MaxConcurrentGenerationJobs`, `EnableTtsGeneration`, `TtsTimeoutSeconds`, `MaxConcurrentTtsJobs`, `PracticeGymReadyExercisesPerType`) remain editable/audited but display-only — no job in the codebase reads them, and building that enforcement was judged out of this phase's safe/limited scope (`TODO-20C-1`). AI signal-safety gates remain untouched and locked.

### Audio Cleanup
- No background job to delete old speaking audio (TODO-6). 50-file per-student cap is the interim guard.

### Production Hardening
- No smoke test automation on deploy.
- No backup/restore runbook.
- No deployment verification job.
- Docker audio volume permission issue deferred pending MinIO migration completion.

### Enterprise / Multi-Tenancy
- No organisation, teacher, cohort, or employer model.
- No multi-course enrolment model.

### Student Polish
- No streak tracking backed by database.
- No coach insights card with real data.
- No weekly plan / calendar strip.
- No vocabulary page UI (`/vocabulary` endpoint planned but not built).
- Configurable lesson session templates deferred (TODO-8).

---

## 11. Deferred Items

| Item | Why Deferred | Revisit After |
|------|-------------|---------------|
| Real STT provider (Whisper/Azure) | Privacy review required; `FakeSpeechToTextService` sufficient for pilot; no reusable STT pipeline wired to a real provider | Pilot produces real recordings; privacy review complete |
| Real TTS production provider (OpenAI TTS) | Fake TTS sufficient for dev; volume permission issue in Docker | MinIO migration complete |
| Speaking CEFR update from AI | Overclaiming risk; no validated rubric yet | Mastery signal dry-run extended review |
| Objective completion from speaking AI | Same risk | Speaking signal production validation |
| Call Mode / real-time conversation | Requires real STT; product spec not finalized | Post-pilot P2 |
| Pronunciation MVP | Requires phoneme-level provider evaluation | Post-pilot P2 |
| Vocabulary page UI | Backend extraction done; UI not built | Phase 17x or 18x |
| Configurable lesson templates | Admin UI complexity; code templates sufficient for pilot | Post-pilot admin tooling phase |
| Multi-course / enrolment model | Conflicts with current single-track model; needs architecture review | Post-enterprise org phase |
| Adaptive onboarding staged assessment | Large scope; needs dedicated review | Post-pilot stabilisation |
| Configurable placement questions | Hardcoded questions sufficient for pilot | After pilot feedback |
| Weekly plan / calendar strip | Session model must stabilise first | Post-16x |
| Streak system (DB-backed) | Low priority for pilot | Post-launch monitoring |
| Coach insights card (real data) | Needs CoachInsight entity; low pilot priority | Post-launch |
| ActivityDto discriminated union refactor | High regression risk; not blocking | When all activity types stabilise |
| GenerationBatch SummarySnapshotJson cleanup | Not critical at current scale | > 100 students |
| Memory staleness detection | Not critical at current scale | Production monitoring phase |
| Admin audio playback (bearer-token streaming) | Admin can review metadata without playback | Post-16I |
| Legacy writing_scenarios / writing_submissions tables | Requires backup confirmation before drop | Explicit confirmation + backup |
| Enterprise SSO, MFA, distributed rate limiting | Deferred in 10Auth-F-FINAL | Enterprise phase |
| Production AI feedback prompt calibration | Requires live AI calls in staging | Staging deployment |

---

## 12. Recommended Future Roadmap

Phases recommended in order of priority. Dependencies are noted.

### Tier 1 — Immediate (next 3 phases)

| Priority | Phase | Area | Why Next | Dependencies |
|---------:|-------|------|----------|-------------|
| ~~1~~ | ~~16J~~ | ~~Speaking signal quality tuning~~ | ~~Complete (2026-06-30)~~ | ~~Phase 16I complete~~ |
| 1 | 17A | AI writing evaluation foundation | Writing is the largest unscored skill area; rubric-based feedback; admin + student visibility | 16J complete |
| 2 | 17B | Writing evaluation quality validation + dry-run signals | Same quality gate pattern as 16H before enabling mastery signals | **Complete 2026-06-30** |

### Tier 2 — Near-term (phases 4–7)

| Priority | Phase | Area | Why Next | Dependencies |
|---------:|-------|------|----------|-------------|
| ~~4~~ | ~~17C~~ | ~~Writing mastery signal controlled integration~~ | ~~**Complete 2026-06-30**~~ | ~~Phase 17B complete~~ |
| ~~5~~ | ~~18A~~ | ~~Lesson quality and content generation upgrade~~ | ~~**Complete 2026-07-01**~~ | ~~17C complete~~ |
| ~~6~~ | ~~18B~~ | ~~Advanced feedback UX~~ | ~~**Complete 2026-07-01**~~ | ~~18A complete~~ |
| ~~7~~ | ~~19A~~ | ~~Review scaffold controlled enablement~~ | ~~**Complete 2026-07-02** — config gates added; EnableReviewScaffoldGeneration remains off by default~~ | ~~17C complete~~ |
| ~~7b~~ | ~~19B~~ | ~~Review scaffold per-item admin approval~~ | ~~**Complete 2026-07-02** — per-item AdminReviewStatus approve/reject/reopen workflow~~ | ~~19A complete~~ |
| ~~7c~~ | ~~19C~~ | ~~Review scaffold Practice Gym pilot rollout~~ | ~~**Complete 2026-07-02** — PracticeGymPilotEnabled gate; pilot remains off by default~~ | ~~19B complete~~ |
| ~~7d~~ | ~~20A~~ | ~~Admin AI operations dashboard~~ | ~~**Complete 2026-07-02** — read-only aggregation endpoint + page over existing AI/eval/generation data~~ | ~~19C complete~~ |

### Tier 3 — Medium-term (phases 8–10)

| Priority | Phase | Area | Why Next | Dependencies |
|---------:|-------|------|----------|-------------|
| 8 | 21A | Enterprise SaaS organisation model | Organisations, teachers, groups, cohorts, org roles | 20A complete |
| 9 | 22A | Production operations hardening | Monitoring, backup/restore runbooks, smoke tests, deployment verification | 20A complete |
| 10 | 20B (proposed) | AI provider health check + retry tooling | Provider ping endpoint, re-queue failed evaluations — deferred out of 20A's read-only scope | 20A complete |

---

## 13. Next 10 Phases (Detailed)

### Phase 16J — Speaking Signal Quality Tuning and Production Dry-Run Review ✓ Complete (2026-06-30)

**Purpose:** Verify applied speaking signals remain safe in a production-like environment. Tune confidence thresholds. Inspect false positives and false negatives on accumulated evaluation data. Add admin tooling to review signal quality over time. CEFR update and objective completion remain disabled.

**Delivered:**
- `SpeakingSignalThresholds` value type — 6 configurable thresholds, `Default` and `FromOptions()` factory
- `SpeakingEvaluationOptions` — 6 new threshold config properties (positive ≥80 overall/completeness/relevance; review max ≤55)
- `SpeakingDryRunSignalMapper` — thresholds now explicit; middle band (56–79) = NoSignal; review direction `score <= MaxReviewOverall`
- `SpeakingEvaluationQualitySummaryDto` — 13 new metrics: applied/blocked breakdown, provider distribution, blocked reasons, avg pronunciation score
- `GET /api/admin/speaking-evaluation/signal-safety-summary` — programmatic invariant confirmation (CEFR/objective/LP all disabled)
- Per-student applied signal visibility in admin detail with invariant labels
- Config status `"DryRunOnly"` when enabled but `ApplyMasterySignals = false`
- `RuleVersion` → "16J-v1"
- 22 new/updated unit tests, 4 new integration tests, 2 new Angular tests

**Review:** `docs/reviews/2026-06-30-phase-16j-speaking-signal-quality-tuning-review.md`

**Out of scope:** CEFR update, objective completion, real STT, new speaking formats.

---

### Phase 17A — AI Writing Evaluation Foundation

**Purpose:** Build a provider-backed asynchronous writing evaluation pipeline. Mirror the 16F speaking pattern for writing. Add rubric-based feedback (grammar, vocabulary, coherence, task completion). Add student visibility (feedback card) and admin visibility (evaluation results on student detail).

**Scope:**
- `WritingEvaluation` entity (mirrors `SpeakingEvaluation`)
- `IWritingEvaluationProvider` interface
- `NoOpWritingEvaluationProvider` (default)
- `WritingEvaluationService` + Quartz job
- Fire evaluation after writing submission (non-fatal, wrapped)
- Student feedback polling card
- Admin student detail: writing evaluations section
- Config: `WritingEvaluation__Enabled` (default false), `Provider`, `MaxBatchSize`, `MaxRetries`

**Out of scope:** Mastery signals, CEFR update, objective completion.

---

### Phase 17B — Writing Evaluation Quality Validation and Dry-Run Signals — COMPLETE (2026-06-30)

**Purpose:** Same quality gate as Phase 16H. Dry-run signal mapper for writing evaluations. Quality summary admin endpoint. No mastery state changes.

**Delivered:**
- `WritingDryRunSignalOutcome` and `WritingDryRunConfidenceBand` enums
- `WritingDryRunSignalMapper` (pure static, no DB, no side effects)
- `WritingEvaluationDryRunSignal`, `WritingEvaluationQualitySummaryDto`, `WritingEvaluationDryRunSignalDto`, `WritingEvaluationWithDryRunDto`
- `GET /api/admin/writing-evaluation/quality-summary` — admin pipeline metrics
- `GET /api/admin/writing-evaluation/{id}/dry-run` — per-evaluation dry-run signal
- Angular: `WritingEvaluationQualitySummaryDto` and `WritingEvaluationWithDryRunDto` interfaces; `getWritingEvaluationQualitySummary()` and `getWritingEvaluationWithDryRun()` service methods
- 15 unit tests + 6 integration tests added

**Out of scope:** Mastery signals, CEFR update, objective completion, admin UI component (deferred to 17C).

---

### Phase 17C — Writing Mastery Signal Controlled Integration — COMPLETE (2026-06-30)

**Purpose:** Config-gated writing mastery signal application. Same 5-gate pipeline as 16I. Review signals only by default. No CEFR update, no objective completion, no Learning Plan regeneration from writing AI — all structurally enforced.

**Delivered:**
- `WritingEvaluationAppliedSignal` entity (audit record, unique per evaluation, rule version "17C-v1")
- `IWritingEvaluationSignalApplicationService` + `WritingEvaluationSignalApplicationService` (5-gate pipeline)
- `WritingEvaluationSignalApplicationJob` (Quartz, `[DisallowConcurrentExecution]`, every 10 minutes, batch 20)
- Config: `ApplyMasterySignals` (default false), `MinimumConfidenceForMasterySignal` (default "High"), `AllowReviewSignals` (default true), `AllowPositiveSignals` (default false)
- `GET /api/admin/writing-evaluation/applied-signals-summary` and `signal-safety-summary` admin endpoints
- Angular Writing Evaluations card in admin-student-detail with invariant labels
- Migration T68 — `writing_evaluation_applied_signals` table with unique index on `evaluation_id`
- 13 unit tests + 8 integration tests; all green

**Safety invariants permanently enforced:**
- `AllowCefrUpdate = false` (computed, not configurable)
- `AllowObjectiveCompletion = false` (computed, not configurable)
- No `ILearningPlanService` dependency — Learning Plan cannot be regenerated

**Tests:** Unit 1,626 / Integration 1,311 / Arch 3 — all pass. Angular production build clean.

---

### Phase 18A — Lesson Quality and Content Generation Upgrade

**Purpose:** Improve the quality and relevance of generated lesson content. Better micro-lesson templates, richer hints, support-language explanations in activities, more accurate skill-targeting.

**Scope:**
- Prompt calibration for `activity_generate_writing`, `activity_generate_listening`, `activity_generate_speaking_roleplay`
- Support language variable wired into all activity generation prompts
- Hint quality improvement (non-generic, targeted to the exercise pattern)
- Difficulty calibration per CEFR level
- Live AI quality review run against staging (first real live review)

---

### Phase 18B — Advanced Feedback UX

**Purpose:** Give students a richer post-activity feedback experience. Retry/revise flow, feedback breakdowns by category, "try again" affordance, reflection prompts, model examples.

**Scope:**
- Retry/revise flow in activity feedback page
- Feedback breakdown component (grammar / vocabulary / structure / tone cards)
- "See an example" button (AI-generated or seeded model answer)
- Reflection prompt ("What would you change next time?")
- Admin feedback quality flag (admin can mark poor AI feedback)

---

### Phase 19A — Review Scaffold Controlled Enablement — complete (2026-07-02)

**Purpose:** Add the missing safety gates around review scaffold generation (daily cap, source restriction, confidence banding, admin-review hold) and surface config in appsettings/admin UI. `EnableReviewScaffoldGeneration` remains `false` by default — this phase does not turn generation on.

**Delivered:**
- New config: `RequireAdminReview` (default true), `MaxScaffoldItemsPerStudentPerDay` (default 3), `ScaffoldAllowedSources` (default `["PracticeGym"]`), `AllowTodayLessonInsertion` (default false), `MinimumConfidenceForReviewNeed` (default `"Medium"`); `DryRunOnly` default flipped `false → true`
- `ReadinessPool` appsettings.json section added (previously missing — only class defaults applied)
- Deterministic `ReviewNeedConfidence` banding (Low/Medium/High) derived from existing mastery classification — no new AI signal
- Per-student daily scaffold cap enforced in `FillShortfallAsync`; new `SkippedDailyCapReached` counter
- `StudentActivityReadinessItem.RequiresAdminReview` (migration T71) — global config-snapshot flag, not per-item approval; `PracticeGymSuggestionService` excludes held items from all suggestion buckets
- Admin: extended dry-run summary with config/counts, new `GET .../review-scaffold/pending-review` read-only endpoint, admin-lessons UI card + table
- Deferred: per-item approve/reject workflow (see Known Gaps)

Review: `docs/reviews/2026-07-01-phase-19a-review-scaffold-controlled-enablement-review.md`.

---

### Phase 19B — Review Scaffold Per-Item Admin Approval — complete (2026-07-02)

**Purpose:** Replace the Phase 19A global "flip the flag to release everything" hold with a per-item admin approve/reject/reopen workflow, so admins can make individual decisions on scaffold items instead of an all-or-nothing gate.

**Delivered:**
- `AdminReviewStatus` enum (NotRequired/PendingReview/Approved/Rejected) + 4 new fields on `StudentActivityReadinessItem` (migration T72, with data backfill for existing held items)
- Entity transition methods `ApproveAdminReview`/`RejectAdminReview`/`ReopenAdminReview` — idempotent, enforce lifecycle guards (cannot approve expired/failed/stale, cannot reject/reopen consumed), never touch CEFR/objectives/Learning Plan
- `PracticeGymSuggestionService` gate updated: `RequiresAdminReview=true` items now need `AdminReviewStatus=Approved` specifically, not just the old global flag
- Admin API: `GET .../pending-review` (now full detail across all review statuses), `POST .../approve`, `POST .../reject`, `POST .../reopen` — safe 404/409/400, `AdminAuditLog` trail
- Admin UI: "Review scaffold — approval" table replaces the old read-only list, with per-row Approve/Reject/Reopen actions and status/visibility badges
- No global "enable" toggle added; `EnableReviewScaffoldGeneration`/`DryRunOnly`/`RequireAdminReview`/`AllowTodayLessonInsertion` remain server-side config only

Review: `docs/reviews/2026-07-02-phase-19b-review-scaffold-admin-approval-review.md`.

---

### Phase 19C — Review Scaffold Practice Gym Pilot Rollout — complete (2026-07-02)

**Purpose:** Let approved review scaffold items reach students in Practice Gym under a controlled, instantly-reversible pilot, without exercising the Phase 19A/19B generation and approval gates any differently than before.

**Delivered:**
- `PracticeGymPilotEnabled` config gate (default `false`) — additional AND condition on top of the existing per-item `AdminReviewStatus=Approved` gate, applied to all three suggestion buckets (Suggested/Continue/Review) that could carry a scaffold item
- `PracticeGymPilotLabel`/`PracticeGymPilotReason` (default "Review" / "This helps you practise a skill you are building.") — override the routing-reason-specific `CallToAction`/`Explanation` copy for any scaffold-origin item, so pilot wording is friendly, non-negative, and centrally configurable
- `MaxStudentVisibleScaffoldSuggestions` (default 2) — scaffold-specific visible-items cap, independent of the general `MaxReview=4` page cap
- Admin API: `GET .../review-scaffold/pilot-summary` — pilot/Today-insertion status flags + approved/student-visible/pending/rejected/consumed/skipped-or-expired counts + recent items (no admin diagnostics)
- Admin UI: new "Practice Gym review scaffold pilot" monitoring card on `admin-lessons` (reuses existing design-system components)
- No changes to the Practice Gym or dashboard Angular templates — existing "Review queue" section and DTO shape already satisfied the visual-distinguishability and no-diagnostics-leak requirements
- Rollback: `PracticeGymPilotEnabled=false` hides all approved-but-unconsumed scaffold items instantly, with no data deletion

Review: `docs/reviews/2026-07-02-phase-19c-review-scaffold-practice-gym-pilot-rollout-review.md`.

---

### Phase 20A — Admin AI Operations Dashboard — complete (2026-07-02)

**Purpose:** Give admins one operational view across the AI evaluation/generation pipeline, read-only, aggregating existing data — no new AI behaviour.

**Delivered:**
- `GET /api/admin/ai-operations/summary` (`AdminAiOperationsController`, admin-only) — aggregates `IAdminAiUsageHandler`, `ISpeakingEvaluationQualityQuery`/`ISpeakingEvaluationSignalApplicationService`, `IAdminWritingEvaluationQuery`/`IWritingEvaluationSignalApplicationService`, `IAdminGenerationQualityHandler`, and `ReadinessPoolReplenishmentOptions` — every existing, already-DI-registered service, not new query logic
- `OverallStatus` (Healthy/Degraded/AttentionNeeded) computed from invariant violations, abandoned-generation warnings, and elevated failure rates
- Combined recent-failures table (speaking + writing + generation, capped at 15, newest first) — the one genuinely new small query this phase added
- Signal safety gate summary: 10 explicit per-pipeline (speaking vs. writing) booleans for CEFR update / objective completion / Learning Plan auto-regen / positive signals / review signals, plus a combined invariant-violation flag — CEFR-update and objective-completion flags are always false because the underlying options (`SpeakingEvaluationOptions.AllowCefrUpdate` etc.) are hardcoded, not runtime-configurable
- New Angular page `/admin/ai-operations`, added to the existing "AI System" sidebar section, built entirely from existing `sp-admin-*` design-system components
- `unavailableSections` — explicitly flags two genuinely-unavailable metrics (real-time job-queue depth; cost estimation for zero-cost/NoOp providers) rather than approximating them

**Deferred (explicitly out of scope for this phase):**
- Provider health-check/ping endpoint
- Retry tooling (re-queue failed evaluations)
- Real-time job-queue depth (no dedicated queue table exists in this codebase)
- Cost estimation for zero-cost/NoOp provider calls

Review: `docs/reviews/2026-07-02-phase-20a-admin-ai-operations-dashboard-review.md`.

---

### Phase 21A — Enterprise SaaS Organisation Model

**Purpose:** Multi-tenancy for organisations. Allow employers, language schools, or cohort owners to manage groups of students.

**Scope:**
- `Organisation` entity
- `Teacher` role
- `StudentCohort` / `Group` entity
- Org-scoped admin portal
- Teacher dashboard (cohort progress view)
- Org enrolment model (students belong to org)
- Billing anchor at org level

**Note:** This is a large architectural change. Requires dedicated `/plan-eng-review` before any implementation.

---

### Phase 21A Precursors — Deferred, Not Scheduled

Two smaller items surfaced while scoping the access-control gap ahead of
Phase 21A. Both are fully planned but **not started** — deprioritized in
favor of current work. Each has a dedicated implementation plan doc.

**Teacher role (minimal, admin-provisioned, read-only)**
Adds `UserRole.Teacher` so instructors can log in and see a read-only view
of the full student roster, without the full Organisation/Cohort model.
Admin-provisioned only, no self-signup, no per-teacher scoping (teachers see
ALL students — cohort-based scoping remains genuine Phase 21A scope).
Plan: `docs/architecture/teacher-role-and-read-access.md`.

**"View as user" — admin impersonation**
Lets an Admin view the app as a specific student (without a second login)
to verify the effect of admin-side changes from the student's perspective.
Short-lived, audited, admin-only, Student-target-only impersonation token
with a persistent "Viewing as X — Return to Admin" banner. Interim
workaround in use today: separate browser contexts (incognito/second
profile) per role, which requires no code since JWTs are stored per browser
context.
Plan: `docs/architecture/view-as-user-impersonation.md`.

---

### Phase 22A — Production Operations Hardening

**Purpose:** Ensure the production environment is observable, recoverable, and deployable with confidence.

**Scope:**
- Backup/restore runbook (PostgreSQL + MinIO)
- Post-deploy smoke test automation
- Deployment verification job (health check + DB migration verification)
- APM/tracing integration (OpenTelemetry or equivalent)
- Alert policy for: AI provider failures, evaluation queue depth, DB lag, 5xx rate

---

### Phase 22B — Full-App QA Bug Bash — complete (2026-07-08)

**Purpose:** Post-Phase-10 regression pass — full browser-driven QA of the entire student journey (onboarding → placement → Today lesson → Practice Gym, all 6 skills → vocabulary → progress → profile), cross-checked against the admin panel.

**Report:** `docs/testing/2026-07-08-full-app-qa-bug-bash-report.md`

**Bugs found and fixed (7):**
- Placement `MaxItems=20` made it mathematically impossible to test all 6 configured skills (needed ≥30) — raised to 48. Untested skills were previously silently marked "100% complete" with a fabricated fallback CEFR level.
- Activity-content JSON validation had zero retry tolerance for a malformed-JSON LLM response — added retry-once coverage (`AiActivityGeneratorHandler`).
- Free-text Practice Gym/lesson answers crashed with a Postgres `22P02` error (raw text written into a `jsonb` column) — now JSON-encoded before persistence, 100% reproducible before the fix (`ActivitySubmitHandler`).
- Student memory/learning-path personalization silently failed on every pattern-evaluated activity submission (a `default(JsonElement)` serialization bug) — fixed (`StudentMemoryService`).
- Fixing the above uncovered a second gap the fix exposed: memory update wasn't gated by marking mode, so deterministic patterns (phrase_match etc.) started making an unwanted AI call once the crash was fixed — caught by `dotnet test` (not manual QA), fixed with a marking-mode guard consistent with the existing vocab-extraction guard (`ActivitySubmitHandler`). Full suite (5 architecture + 1917 unit + 1410 integration) passes.
- Two unstyled-CSS-class bugs (missing `skill-badge` styles; 5 components using undefined Bootstrap-style `.btn` classes) — added the missing CSS.

**Bugs found, documented, deferred (2 — tracked as TODO-11/TODO-12 in `docs/backlog/deferred-work.md`):**
- "Answer Short Question" speaking pattern loses all answers when the mic-denied typed fallback is used (likely wrong renderer dispatch for multi-item speaking patterns).
- CEFR level shown inconsistently between Admin ("Not set") and student Progress page ("A1 current level") for the same provisional-confidence student.

---

## 14. Longer-Term Product Roadmap

| Order | Epic | Status | Description |
|------:|------|--------|-------------|
| 1 | Vocabulary Page UI | Planned | `/vocabulary` page with status, filters, review queue; backend extraction already done |
| 2 | Streak System | Planned | DB-backed streak tracking; weekly calendar strip |
| 3 | Coach Insights Card | Planned | Real latest feedback summary on dashboard |
| 4 | Weekly Plan / Calendar | Planned | Pre-generated session slots, day-of-week calendar strip |
| 5 | Configurable Lesson Templates | Planned | Admin-managed lesson templates (DB-backed, versioned) |
| 6 | Real STT Provider | Planned | OpenAI Whisper or Azure Speech-to-Text; requires privacy review |
| 7 | Real TTS Production | Planned | OpenAI TTS `tts-1`; `OPENAI_API_KEY` already wired |
| 8 | Pronunciation MVP | Planned | Phoneme-level feedback; requires STT/ASR provider evaluation |
| 9 | Teams Chat Simulation | Planned | `teams_chat_simulation` pattern; AI multi-turn chat exercise |
| 10 | Call Mode / Open AI Speaking | Deferred | Multi-turn real-time voice conversation; requires real STT |
| 11 | Configurable Placement Questions | Planned | Admin-manageable placement bank without code changes |
| 12 | Adaptive Onboarding Staged Assessment | Future | Staged model: onboarding → placement → ongoing diagnostic |
| 13 | Multi-Course / Enrolment Model | Future | Students enrol in multiple courses (Casual, Workplace, Academic) |
| 14 | Micro Lessons | Planned | AI-generated short teaching moments before exercises |
| 15 | Vocabulary Queue Cards | Planned | Spaced repetition card deck; cloze, collocation, phrase types |
| 16 | AI Tutor Persona | Planned | Named AI teacher voice; consistent persona across sessions |
| 17 | Advanced TTS Voice Config | Planned | Per-feature voice assignment; admin preview; voice regeneration on change |

---

## 15. Enterprise SaaS Roadmap

These items are post-Phase 21A (Organisation Model) dependencies.

| Item | Description |
|------|-------------|
| Teacher Portal | Cohort progress view, assignment tools, feedback review |
| Employer Dashboard | Aggregate analytics, CEFR distribution, improvement trends for a cohort |
| Org Admin | Enrolment management, cohort creation, billing contact |
| SSO / SAML | Enterprise SSO for corporate deployments |
| Multi-Tenancy Isolation | Row-level security or schema separation per org |
| Org-Level AI Config | Per-org provider selection and cost caps |
| Advanced Analytics | Skill improvement trends, dropout risk, engagement heatmaps |
| Certificates | CEFR level certificates for completed programmes |
| API / Webhooks | Progress webhooks for HR systems, LMS integration |

---

## 16. Operations / Production Roadmap

| Item | Description |
|------|-------------|
| Backup/Restore Runbook | PostgreSQL + MinIO point-in-time recovery documented |
| Smoke Test Automation | Post-deploy E2E smoke suite against staging/production |
| OpenTelemetry | Distributed tracing, metric export, log correlation |
| Alert Policy | AI failure rate, queue depth, DB lag, 5xx rate, cost spikes |
| Deployment Verification Job | Health check + migration assertion after each deploy |
| Audio Cleanup Job | Delete speaking recordings older than 90 days (TODO-6) |
| GenerationBatch Cleanup | Prune old `GenerationBatch` rows / truncate `SummarySnapshotJson` (TODO-9) |
| Memory Staleness Detection | Alert when `UserLearningSummary.UpdatedAt` is stale and activity exists (TODO-4) |
| MinIO Retention Policy | Object lifecycle rules for audio file expiry |
| Docker Audio Volume Fix | Non-root container user ownership; resolved by MinIO migration |

---

## 17. Risks and Guardrails

### Risk Table

| Risk | Severity | Why It Matters | Mitigation |
|------|----------|---------------|------------|
| AI feedback quality inconsistency | High | Students receive poor feedback; trust erodes | Dry-run validation before mastery integration; admin quality review |
| False mastery signal from speaking AI | High | Student incorrectly marked as mastered; learning plan skips needed content | 5-gate pipeline; confidence threshold; review-only signals; config gate off by default |
| False mastery signal from writing AI | High | Same risk as speaking | Same pattern: dry-run phase required before 17C |
| Provider cost / latency spikes | Medium | AI budget consumed unexpectedly; slow student experience | Cost tracking per call; per-feature max retries; NoOp fallback; alert on zero-cost calls |
| Overclaiming pronunciation accuracy | High | Promising phoneme-level accuracy without phoneme-level provider | Pronunciation scoring is conservative; no phoneme claims until real ASR wired |
| Review scaffold activation risk | Medium | Review scaffold generates inappropriate content for student level | Extended dry-run validation before enablement; admin preview required |
| Observability gap | Medium | Failures are silent outside the admin UI; no APM/tracing | Phase 20A operations dashboard shipped (2026-07-02); real APM/tracing (OpenTelemetry) and provider health-check/retry tooling still deferred |
| Enterprise complexity | Medium | Multi-tenancy is a large architectural change | Phase 21A requires dedicated architecture review before implementation |
| Production deployment / backup risk | High | No verified backup/restore procedure | Phase 22A: runbook, smoke tests, deployment verification |
| UI polish / product-market fit risk | Medium | Student UI is functionally complete but not fully polished | Phase 18B advanced feedback UX; deferred full redesign |
| Real STT privacy risk | High | Australian data residency may restrict sending audio to US providers | Privacy review required before choosing STT provider |
| Audio file growth | Low-Medium | Unbounded audio file accumulation | TODO-6 cleanup job planned; 50-file cap is interim guard |

### Product Guardrails (Non-Negotiable)

- **SpeakPath must not be workplace-specific by default.** Workplace English is one selectable context only.
- **Students must not edit AI prompts.** Prompts are admin-managed and never exposed to students.
- **Students must not directly edit CEFR.** CEFR changes must come from placement, performance evidence, or admin action only.
- **AI scoring must not be overclaimed.** No precision claims for pronunciation or phoneme accuracy without a validated provider.
- **Speaking evaluation mastery integration must remain config-gated.** `ApplyMasterySignals` defaults to false.
- **CEFR update from speaking AI is permanently disabled** unless explicitly redesigned with confidence evidence.
- **Objective completion from speaking AI is permanently disabled** unless explicitly redesigned.
- **CEFR update from writing AI is permanently disabled** until Phase 17C+ validates quality.
- **Real-time AI conversation (Call Mode) is deferred** until real STT is wired and privacy review complete.
- **Full student UI redesign is deferred** until functional learning flows are mature.
- **No raw storage keys (audio paths) in any API response.** Integration tests enforce this invariant.
- **Admin creates all student accounts.** No public self-registration.
- **Failed / NotSupported evaluations never affect mastery.** Enforced in all signal application services.

---

## 18. Current Maturity Estimates

These are planning estimates, not exact metrics. Provided to guide sequencing decisions.

| Area | Maturity | Notes |
|------|:--------:|-------|
| Backend architecture | 85% | Clean Architecture, test coverage, AI tracking all strong. Observability gap remains. |
| Admin platform | 80% | 14-route UI complete, sp-admin-* library mature. AI ops dashboard not yet built. |
| Student core journey | 80% | All 6 pages functional. Streak, coach insights, weekly plan not yet real. |
| Adaptive learning engine | 75% | Learning Plan, mastery sweep, routing all working. Writing signals not yet wired. |
| Activity player | 85% | 18+ renderers, audio player, voice recording, feedback-pending card all complete. |
| Audio / listening | 75% | TTS wired; real production provider not confirmed. Audio player reliable. |
| Voice recording | 70% | VoiceRecorderComponent complete; admin playback deferred; no STT. |
| AI speaking evaluation | 60% | Pipeline complete behind config gate. No real STT. Dry-run validated. Threshold tuning needed. |
| AI speaking mastery integration | 30% | Config-gated infrastructure in place. Review signals only. Not yet production-enabled. |
| AI writing evaluation | 0% | Not yet built. Phase 17A target. |
| AI writing mastery integration | 0% | Phase 17C target. |
| Enterprise SaaS | 5% | No org/teacher/cohort model. Phase 21A. |
| Observability / ops | 20% | AI usage admin exists. No APM, tracing, alerting. Phase 22A. |
| Student UI polish | 60% | Visual rehaul done (15I). Advanced feedback UX, streak, coach not yet real. |
| Production readiness | 25% | App runs in Docker. Phase 20E found production placement-start is broken (`TODO-20E-1`, `PostgresException`). No smoke test automation, no backup runbook, no monitoring. |

---

## 19. Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-07-02 | Phase 20E ran directly against production, with explicit user sign-off per action | No staging environment exists; user provided prod admin credentials and made two explicit AskUserQuestion scope decisions (create-and-repair one fresh pilot student only; document the discovered `PostgresException` production issue rather than root-cause/fix it in-session) |
| 2026-06-09 | Non-workplace generalisation | SpeakPath must not be workplace-specific by default. Workplace is one selectable context. |
| 2026-06-09 | Placement is a standalone entity, not a LearningModule | PlacementAssessment decoupled from learning path to allow independent lifecycle |
| 2026-06-12 | Exercise pattern library replaces activity type enum | Patterns are more composable; content model is in JSON not enum-driven code |
| 2026-06-12 | Practice Gym uses readiness pool, not ad-hoc generation | Pool pre-generates content; Gym consumes from pool for low-latency experience |
| 2026-06-15 | 3-page activity structure (Teach/Learn/Practice) | Separates instruction from practice; mirrors spaced-repetition pedagogy |
| 2026-06-21 | Notification platform: in-app + SMTP first, SMS foundation only | SMS provider cost/reliability; SMTP sufficient for pilot |
| 2026-06-23 | Refresh tokens: hash-only storage, rotation on use, reuse detection | Security best practice; prevents token theft from DB compromise |
| 2026-06-23 | Google OAuth: disabled by default, domain restriction, no auto-provisioning | Controls student creation; admin creates accounts; Google login is opt-in |
| 2026-06-27 | Learning Plan: 10-objective sequence, deterministic (no AI calls) | AI in plan layer adds cost and non-determinism; deterministic is cheaper and auditable |
| 2026-06-27 | Placement: conservative CEFR (minimum of per-skill estimates, confidence >= 0.6) | Avoids overclaiming; better to start lower and advance than to misplace a student |
| 2026-06-28 | Speaking evaluation: NoOp provider by default, never blocks student flow | Evaluation failures must not block learning; async-only |
| 2026-06-28 | Audio response: no STT, separate from SpeakingRolePlay | STT requires privacy review; audio-attempt endpoint is a clean separation of concerns |
| 2026-06-30 | Speaking mastery signals: review-only, no CEFR update, no objective completion | Conservative integration path; AI evaluation not yet validated for hard state changes |
| 2026-06-30 | CEFR update from AI evaluation: permanently disabled in current design | Overclaiming risk; CEFR is a high-value signal that must come from validated sources |
| 2026-07-03 | Phase 20I QA used a freshly created QA student for all student-side UI testing, not `pilot.student.20e`'s real credentials | Avoids disrupting the real pilot student's account (forced password reset); admin/DB views used for `pilot.student.20e`-specific checks instead |
| 2026-07-06 | Onboarding/placement migrated to Form.io (frontend-only, `@formio/js`, MIT) | Custom question designer/renderer had grown into more machinery than needed; Form.io gives admin-authorable forms for free. Old onboarding tables dropped cleanly (confirmed UAT-only, no production student data at risk); placement's adaptive engine kept fully intact, Form.io used only for per-item rendering/authoring |
| 2026-07-06 | Placement stays adaptive — no static per-skill Form.io wizard | The adaptive engine picks each next item live from per-skill CEFR confidence; a pre-authored static form can't replicate that. Form.io only renders one already-selected item at a time |
| 2026-07-08 | Placement `MaxItems` raised from 20 to 48 | 20 made it mathematically impossible to give all 6 configured skills even their `MinItems=5` floor (needs ≥30); real convergence runs ~6-7 items/skill (~42 total), so 48 gives headroom. Discovered via full-app QA bug bash — see `docs/testing/2026-07-08-full-app-qa-bug-bash-report.md` |
| 2026-07-08 | Left the pre-fix `qastudent1` placement assessment data untouched rather than repairing it | Serves as a live before/after example of the MaxItems bug for future reference; a second fresh student account should be used to verify the fix end-to-end instead of mutating this one |
| 2026-07-08 | Clean-A cleanup pass (dead code/routes/API): deleted 3 dead onboarding enums (`OnboardingAnswerMapping`, `OnboardingStepRequirementType`, `OnboardingStepTypeV2`), an orphaned `OnboardingShellComponent`, the `admin/careers`→`curriculum`, `admin/ai-usage`→`usage`, `students/new`/`students/create` dead redirect aliases, and the fully-orphaned career/curriculum-word admin CRUD chain (`AdminCareersComponent`, its 4 API-service methods, `IAdminCurriculumHandler` + its 4 controller actions/DTOs) — see `docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md` | UAT, not production — evidence-based deletion after confirming zero live references per entity/route; `CareerProfile`/`CurriculumWordList` entities and data themselves were NOT touched, only the unreachable authoring UI/API surface (no route ever loaded it) |
| 2026-07-08 | `PracticeActivityCache.ContentFingerprint` computation fixed (no longer salted with `Guid.NewGuid()`) but NOT turned into real content-level dedup | It is a queue-slot uniqueness key only — no activity content exists yet at queue time. Real content-level repetition/novelty avoidance remains Phase B of the bank-first plan, not yet implemented |
| 2026-07-08 | Left `LearningTrack` (`[Obsolete]`, backend-compat-only) and `WritingSubmission` (write-path dead, only read for student-reset purge) in place rather than deleting | Both are still reachable/referenced by live code paths (pragma-suppressed obsolete branch; reset/purge delete call); deleting either needs a dedicated follow-up phase to verify no live student data depends on the old shape first, not a blind Clean-A deletion |
| 2026-07-08 | Left `e2e/core-flow-smoke.spec.ts`'s onboarding section unfixed despite asserting on UI text that no longer exists (broken since the 2026-07-03 V1 onboarding retirement) | Rewriting it correctly requires driving the current Form.io onboarding flow through the test's `mockApi` fixture — a nontrivial, higher-risk change outside this pass's evidence-based dead-code-deletion scope; flagged for a dedicated fix |
| 2026-07-08 | Clean-A2: doc-root remediation — rewrote `docs/architecture/README.md`'s Current Product Direction/Architecture Docs table/Implementation State to reflect bank-first + Clean-A/A2; restructured `docs/sprints/current-sprint.md` (moved stale Phase 20D "Active sprint" block down to "Previous sprint", wrote a new Active-sprint summary) | Both docs were 4+ weeks stale relative to the 2026-07-07 bank-first work per Clean-A's own finding; this closes that gap rather than deferring it further |
| 2026-07-08 | Clean-A2: fixed the Karma compile blocker (`admin-placement-item*.spec.ts` fixtures missing Phase 7 calibration fields) by adding the 6 missing fields to `ITEM_A`/`ITEM_B` fixtures in both spec files | Proven small, pure fixture-alignment fix — no production code changed; Karma now runs to completion (120 pre-existing failures, unchanged baseline, confirmed via 2 repeat runs to rule out flakiness) |
| 2026-07-08 | Clean-A2: `e2e/core-flow-smoke.spec.ts` marked `test.skip(...)` with a detailed inline comment, rather than rewritten or deleted | Confirmed not a small fix — requires mocking the V2 Form.io endpoints and driving a dynamically-rendered component tree instead of fixed V1 headings/buttons; skipping preserves the test for a future correct rewrite instead of leaving it silently failing or deleting real (if currently mis-targeted) coverage |
| 2026-07-08 | Clean-A2: deleted `LearningTrack` (entity, config, DbSet, `SetTrackRequest`/`SetLearningTrack`, the "track" onboarding-controller branch, the dead `GET /api/reference/tracks` endpoint, `LearningTrackDto`) and `WritingSubmission` (entity, config, DbSet, the reset/purge delete call), across backend + frontend + 8 test files. Migration `T_CleanA2_DropLearningTrackAndWritingSubmission` created and applied to the local dev DB | Both proven dead in code (zero reads/writes reachable from any live frontend flow) **and** verified via a live, read-only SQL query against the running local dev Postgres container: `learning_tracks` had 1 unused seed row with zero `student_profiles.learning_track_id` references, `writing_submissions` had 0 rows. **Caveat: this check was only run against the local Docker Compose dev DB, not the separately-deployed `speakpath.app` pilot database** — the same query must be re-run there (`SELECT count(*) FROM student_profiles WHERE learning_track_id IS NOT NULL;` and `SELECT count(*) FROM writing_submissions;`) and confirmed zero before this migration is ever deployed to that environment, since migrations run automatically on container startup |
| 2026-07-08 | Deleted `CurriculumWordList.UpdateDetails()` (zero callers anywhere after the Clean-A AdminCareers removal made it orphaned) | Proven dead by grep across `src/` and `tests/`; the only other `UpdateDetails` hits belong to the unrelated `CurriculumObjective` entity |
| 2026-07-08 | Phase B — Repetition/novelty foundation implemented: new `StudentActivityUsageLog` entity (migration `T_PhaseB_StudentActivityUsageLog`, applied to local dev DB), `IActivityContentFingerprintService`/`ActivityContentFingerprintService` (deterministic content fingerprint, exact-match only), `IActivityNoveltyPolicy`/`ActivityNoveltyPolicy` (fingerprint/template/topic/scenario cooldowns, `NoveltyPolicySettings` code defaults). Wired into `ActivitySubmitHandler` (usage logging on completion), `PracticeGymGenerationJob`'s Form.io pilot (template-cooldown pre-check + content-fingerprint post-check, bounded retry), and `ActivityMaterializationJob` (avoid-repeating prompt hint + content-fingerprint post-check, bounded retry, safe fallback — never blocks Today lessons) | Deterministic/exact-match cooldown foundation only — explicitly no embeddings/pgvector/semantic near-duplicate detection this phase. See docs/architecture/repetition-and-novelty.md |
| 2026-07-08 | `TopicKey`/`ScenarioKey`/`PassageKey` extraction from activity content was NOT implemented in Phase B — the fields exist on `StudentActivityUsageLog` and the novelty policy already enforces cooldowns on them when present, but nothing populates them yet | Deferred: extracting a stable topic/scenario key from `ModuleStageSchema`'s `practiceContent.scenario`/`.task` (or the Form.io equivalent) is a natural next increment, not required for this phase's fingerprint+cooldown enforcement (which works today on `ContentFingerprint` and `SourceTemplateId` alone) |
| 2026-07-08 | No job-level integration test was written for `PracticeGymGenerationJob`'s template-cooldown branch or `ActivityMaterializationJob`'s retry loop end-to-end | Consistent with the existing project convention for this exact job (the 2026-07-07 Form.io pilot's own documented gap: "judged low value relative to cost"); the underlying `ActivityNoveltyPolicy`/`ActivityContentFingerprintService` logic is fully unit-tested instead |
| 2026-07-08 | Phase C1 — generalized the Form.io Practice Gym template path from 1 pilot pattern to 4 total (`formio_practice_gym_pilot`, `phrase_match`, `gap_fill_workplace_phrase`, `reading_multiple_choice_single`), via a new code-level `PracticeGymGenerationJob.TemplateMigratedPatternKeys` allow-list — no new admin UI, reuses the existing `PracticeGymFormIoPilot.Enabled` toggle as the master switch for all 4 keys. Seeded 3 new approved/published `ActivityTemplate` rows (`ActivityTemplateSeeder`, idempotent) — original, English-only, workplace-context content | Chosen as the smallest safe next increment after Phase B; all 3 new patterns are deterministic (KeyedSelection/ExactMatch), audio-free, already `SupportsPracticeGym=true` in production. ~24 of ~28 patterns and all Today lessons remain untouched legacy generation |
| 2026-07-08 | Critical generalization fix: `ActivitySubmitHandler`'s Form.io-scored evaluation dispatch changed from pattern-driven (`pattern.MarkingMode == FormIoScored`) to content-driven (`!string.IsNullOrWhiteSpace(activity.FormIoSchemaJson)`), and the `MarkingMode` sent to `PatternEvaluationRouter` now follows the same content-driven check | Without this fix, a template-generated instance of e.g. `phrase_match` (whose `ExercisePatternDefinition.MarkingMode` stays `KeyedSelection` to preserve the legacy fallback) would have been routed to the wrong evaluator and scored incorrectly. Verified safe via full 3344-test suite before and after — 0 regressions |
| 2026-07-08 | Seeded `ActivityTemplate.GenerationInstructions` for the 3 new templates explicitly forbid the AI from renaming component keys or changing which option/value is correct | `ActivityTemplateInstanceGenerator` never regenerates `ScoringModelJson` — it stays static and keyed by component `key`, applied as-is to whatever the AI personalizes. This is enforced by instruction + `requiredComponentKeys` validation (catches renamed/missing keys) but NOT by an automated check that the AI kept the correct answer identity. Same constraint the original pilot template design already accepted — not a new risk |
| 2026-07-08 | **Plan-Sync-After-C1**: Phase C re-planned as a sequence (C2/C3/C4/C-Final) rather than one large "generalize the rest of Practice Gym" phase; Phase E re-planned from an informal "seed CEFR-J/UniversalCEFR data" task into a full multi-step English-only resource **import/review/preview/publishing platform** (E0-E8), documented in new `docs/architecture/english-resource-bank-import-platform.md` | Phase C1 proved the migration pattern works but only touched 3 patterns out of ~28 — doing "the rest" as one phase would repeat the same undersized-scope mistake the original Phase E framing made. Splitting both into small, provable increments matches the project's established phase discipline (Clean-A/A2, Phase B, Phase C1 were all deliberately small) |
| 2026-07-08 | Phase E is explicitly **English-only** — supported languages (Persian, etc.) are runtime-only support (onboarding language-pair selection, support-language hints/translation help), never seeded learning content. No Persian seed corpus, no bilingual phrase bank, no English–Persian (or English–any-language) parallel import, at any phase | SpeakPath teaches English with English; a bilingual seed corpus would contradict the product's own teaching model and the existing `CefrResourceSource`/`CefrVocabularyEntry` schema design (English-only fields, no target-language-pair column) |
| 2026-07-08 | Phase D (bank-first Today lesson composer) sequenced to start only after Phase C (Practice Gym migration) reaches a mature state AND enough of Phase E's resource-bank platform exists to give Phase D real bank content to compose from | Today lessons are the primary, highest-blast-radius student-facing surface (per the 2026-07-08 clean-architecture plan's own risk assessment) — starting Phase D before the bank/template pattern is proven across more of Practice Gym, or before there's real resource-bank content to draw from, would repeat the exact "per-student throwaway generation" problem this whole initiative exists to fix |
| 2026-07-08 | Phase C2 — migrated a second small batch of 3 reading-family patterns (`reading_multiple_choice_multi`, `reading_fill_in_blanks`, `reading_writing_fill_in_blanks`) to the Form.io template path, bringing the total to 7 of ~28 pattern keys. Seeded 3 more approved/published `ActivityTemplate` rows (`reading_mcq_multi_workplace_seed_v1`, `reading_fill_in_blanks_workplace_seed_v1`, `reading_writing_fill_in_blanks_workplace_seed_v1`) | Continues C1's small-batch discipline; all 3 reuse existing `ComponentAnswerScorer` kinds (`single_choice`, `multiple_choice` via a Form.io `selectboxes` component, `text_normalized`) with no new scorer or frontend component needed |
| 2026-07-08 | Phase C2 deliberately excluded all "listening" patterns despite their catalog `RequiresAudio=false` flag, and excluded `ReorderParagraphs` | The listening patterns' content DTOs/generation flow are still built around an audio script/URL (e.g. `ListeningFillInBlanksContent.AudioUrl`), so the `RequiresAudio` flag alone isn't "strong evidence" of audio-free compatibility per the migration rule; `ReorderParagraphs` needs a new sequencing/reorder scorer kind that doesn't exist yet. Both are flagged as candidates for a future phase after a dedicated review/scorer addition, not silently dropped |
| 2026-07-08 | **Plan-Sync-B2**: inserted a new **Phase B2 — Activity Feedback, Repeat Policy, and Calibration Signals** into the phase sequence, between the just-completed Phase C2 and the not-yet-started Phase C3. Docs-only change: `docs/architecture/activity-feedback-and-calibration.md` created; `road-map.md`, `current-sprint.md`, `architecture/README.md`, `repetition-and-novelty.md` updated. No app code, migrations, or config changed | Phase B (repetition/novelty) implemented deterministic usage logging and cooldowns, but never collected explicit student-reported difficulty/clarity/usefulness/repeat-preference feedback. As more Practice Gym patterns get template-migrated (7 of ~28 after C2), it is safer to start building the feedback/calibration signal now — informing CEFR calibration, difficulty-band calibration, `ActivityTemplate`/resource quality, AI-generation quality, novelty/cooldown tuning, and admin review triggers — before committing to further large-scale migration batches in C3/C4/C-Final |
| 2026-07-08 | **Phase B2 implemented**: new `ActivityFeedbackSignal` entity (migration `AddActivityFeedbackSignal`) capturing difficulty/clarity/usefulness/repeat-preference/optional-comment, idempotent per `(StudentProfileId, ActivityAttemptId)` (fallback `(StudentProfileId, LearningActivityId)` when no attempt) via two partial unique indexes; `IActivityFeedbackPolicyProvider`/`ActivityFeedbackPolicyProvider` (Off/Optional/Required per surface, reusing the existing feature-gate/`RuntimeSettingOverride` system — new group `activity-feedback-policy`, keys `ActivityFeedback.TodayPolicy`/`ActivityFeedback.PracticeGymPolicy`, default `Optional`, no new admin UI needed); `ISubmitActivityFeedbackHandler`/`ActivityFeedbackHandler` (upsert, ownership check, comment-length validation, provenance backfill from `StudentActivityUsageLog`); new endpoint `POST /api/activity/attempt/{attemptId}/feedback`; `FeedbackPolicy` added to the existing `ActivityFeedbackDto` attempt-submission response, populated by `ActivitySubmitHandler` in both its pattern-eval and legacy dispatch paths; minimal `activity-feedback-prompt` Angular component shown from the existing student result screen only when policy is not Off, with Skip shown only when Optional. +14 backend tests (3,357 → 3,371). Did not wire the collected signal into any automated calibration/novelty/admin-review logic — that remains future work | This is a foundation/collection layer, not a calibration engine, per the explicit scope given for this phase: build persistence + policy + API + minimal UI now so signal starts accumulating, defer automated consumption to a later phase. `ActivitySubmitHandler` was the correct single insertion point since it audited as the shared completion path for both Today and Practice Gym (confirmed via `SessionExercise`-link surface detection, the same mechanism already used for `StudentLearningEvent.Source`) |
| 2026-07-08 | **Phase C3 — re-audited the full remaining Practice Gym catalog against actual content DTOs (not just catalog flags) and migrated exactly one pattern, `reorder_paragraphs`**, to the Form.io template path. Built a new generic `ScoringRuleKinds.OrderedSequence` (`ordered_sequence`) `ComponentAnswerScorer` kind reusing the exact positional-comparison semantics `ExactMatchEvaluator.EvaluateReorderParagraphsAsync` already used; one seeded `ActivityTemplate` (`reorder_paragraphs_workplace_seed_v1`, B1, `reading.inference`) using a **stock Form.io `datagrid`** component with its built-in `reorder` setting (no new custom Form.io component); `FormIoSchemaValidationService`'s allow-list extended with `datagrid`/`hidden`; `reorder_paragraphs` added to `TemplateMigratedPatternKeys`. **No frontend code changes were needed** — `ExerciseRendererComponent` already routes to Form.io purely on `formIoSchemaJson` presence, and a stock datagrid's `{"paragraphs":[{"itemId":...}]}` submission shape is consumed as-is by the new scorer. +8 backend tests (3,371 → 3,379): seeder count/no-leak, template-path materialization, 3 `ordered_sequence` evaluator cases (correct/scrambled/no-leak), 2 schema-validation cases for `datagrid` (allowed with valid nested components / rejected with a disallowed nested type), plus 2 pre-existing schema-validation tests updated to use `iframe` instead of `datagrid` as their still-disallowed-type example. Fixed one bug found during validation: the seed template originally used subskill `reading.coherence`, which isn't in `CurriculumSubskillConstants`'s allowed list for `reading` — corrected to `reading.inference` (caught by the full backend test suite, not by a targeted test, underscoring why "run the whole suite" remains part of this project's validation discipline) | The full audit (not just re-reading the C2 exclusion doc) found that 3 additional patterns (`answer_short_question`, `read_aloud`, `repeat_sentence`) are ALSO excluded — audio-referencing item DTOs plus fuzzy/word-overlap scoring incompatible with `ComponentAnswerScorer`'s binary kinds — not previously named explicitly in the C2 exclusion list. With those confirmed excluded alongside the already-known listening family and all AI-evaluated patterns, no further deterministic/simple candidate remains in the ~25-key legacy set. `reorder_paragraphs` was chosen as C3's sole pattern specifically because it was the one remaining candidate needing only a small, generic, reviewable scorer addition — not a renderer rebuild — consistent with C1/C2's small-batch discipline |
| 2026-07-08 | **Recommend Phase C-Final over a forced Phase C4** | The C3 audit's negative result (no further safe deterministic patterns) means a real Phase C4 would have to open new scope — either a dedicated audio-compatibility review (deciding whether text-only variants of the listening family can be authored, and inventing a new fuzzy/partial-credit scorer kind for `answer_short_question`/`read_aloud`/`repeat_sentence`) or dedicated Form.io renderer/evaluator support for `AiStructured`/`AiOpenEnded` marking modes — neither is a small, low-risk batch like C1/C2/C3 were. Forcing a C4 under the same "small batch" framing would either misrepresent the scope or produce a rushed, unreviewed evaluator/renderer addition; closing the deterministic-pattern track at C-Final (8/~33 template-enabled, 25 legacy documented with concrete exclusion reasons) is the more honest outcome, with either audio-compatibility or AI-evaluated-pattern support becoming its own dedicated future phase if the product decides to pursue it |
| 2026-07-08 | **Phase C-Final implemented**: verified all 8 template-enabled Practice Gym keys (approved+published templates, idempotent seeders, leak-safe schemas, intact gating/fallback/novelty/feedback-policy wiring — no code gaps found, docs-only closure); produced a definitive 33-row pattern audit table (8 template-enabled, 25 legacy) correcting an off-by-one "26 legacy" figure that had propagated through Phase C3's own docs; added 4 explicit backlog entries (`docs/backlog/product-backlog.md`) for the deferred pattern families (listening/audio, speaking/audio, open-ended AI-evaluated, fuzzy/short-answer) so future migration scope is tracked, not left as an implicit doc note | This closure pass found no code/test gaps — the verification confirmed C1-C3's work was already correct and complete, so the phase stayed docs-only as scoped. Explicit backlog entries exist so a future "should we do Phase C4" conversation starts from a tracked list, not a re-derivation of the C3 audit |
| 2026-07-08 | **Phase E0 implemented — finalized the Phase E entity/status/gate model**: source registry reuses the existing `CefrResourceSource` entity directly (supersedes the Plan-Sync-After-C1 placeholder's proposed separate `ResourceImportSource`); new staging entities `ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate` (naming unchanged from the placeholder); published resources use a hybrid model — E4 reuses existing typed `CefrVocabularyEntry`/`CefrGrammarProfileEntry` (rejected a polymorphic `ResourceBankItem` + JSON-blob table). Defined a 7-gate model (English-only, license, parser, AI-analysis-advisory, rule-validation, dedup/fingerprint, admin review+publish) reusing `AdminReviewStatus`, `IFormIoSchemaValidationService`, `IActivityContentFingerprintService`, `IFileStorageService` — no new parallel mechanisms invented. Defined E1's exact scope (gates 1-3 only, no publishing) and E2-E4 boundaries; new admin pages placed under the existing Content sidebar group. See `docs/architecture/english-resource-bank-import-platform.md` | The E0 audit found `CefrResourceSource` already has every field a source registry needs (name/license/URL/notes/approval/imported-at) — adding a second near-identical `ResourceImportSource` entity would have duplicated it for no reason, violating this project's own "don't duplicate existing good entities" convention. The hybrid publish-target decision follows the same reasoning: the existing typed `Cefr*` entities already have clean, purpose-built schemas that a polymorphic JSON-blob table would be a step backward from |
| 2026-07-08 | **Plan-Sync-PG-v2**: added a new future track, **Practice Gym v2 (skill/subskill/objective-first)**, to the roadmap after Phase E5-E8 and before Phase F/G. Practice Gym should eventually let students choose or be guided toward a **skill/subskill/weak-area/objective/review/challenge/recommended-practice** target rather than primarily choosing a raw internal exercise type (gap fill, phrase match, reorder paragraphs, multiple choice, listening fill-in-blanks, etc.); the system should internally select the best `ActivityTemplate`/resource/activity format based on CEFR, skill/subskill, weakness evidence, novelty/cooldown, feedback signals, available published bank items, and renderer/scorer/evaluator capability. **`ExerciseTypeDefinition`/`ExercisePatternDefinition` are NOT deleted** — they are reframed as an internal capability registry (renderer capability, scorer/evaluator capability, audio/image/speaking/open-ended requirements, Form.io compatibility, supported skills/subskills, CEFR suitability, Practice Gym/Today compatibility, fallback/generation capability), not the student-facing product model. Docs-only; no app code, migrations, or config changed; does not reopen or change the Phase C-Final closure decision | Phase C1-C3/C-Final proved the bank-first *content* migration works, but migrating individual pattern keys to templates is orthogonal to *how students choose what to practice* — the current Practice Gym UX still surfaces raw pattern/type names, which is a fundamentally different (and, long-term, less pedagogically sound) mental model than skill/objective-first practice. Sequencing PG-v2 after Phase E5-E8 (not immediately after C-Final) is deliberate: a good skill-first selector depends on enough published bank/resource content and search/selector coverage to have real options to choose from — attempting PG-v2 before Phase E matures would just recreate the current pattern-first UX with extra steps |
| 2026-07-08 | **Phase E1 implemented — first Phase E implementation slice**: `CefrResourceSource` extended with `LanguageCode` (enforced to `"en"`), `AllowsStudentDisplay`, `AllowsCommercialUse`, `AttributionText`, `SourceVersion`, `DownloadUrl`, `UpdatedAtUtc`, `Update(...)` — no duplicate source-registry entity created. New staging entities `ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate` (migration `AddResourceImportStaging`) plus `ResourceImportRunStatus`/`ResourceRawRecordStatus`/`ResourceCandidateValidationStatus`/`ResourceCandidateType`/`ResourceImportMode` enums. `ResourceImportService` implements gates 1-3 only (English-only via explicit language field or a conservative Arabic/Persian-script + non-Latin heuristic; license/source-approval, blocking before any run is created; parser gate requiring a recognizable content field) with continue-on-error per-row processing (one malformed row never aborts a run) and within-run duplicate-hash detection. `ContentFingerprint` reuses `IActivityContentFingerprintService`. Admin CRUD/API/UI for Resource Sources/Import Runs/Candidates under the existing Content sidebar group (Raw Records nested under run detail, not top-level), reusing shared admin components — no rendered preview, no approve/publish action (both explicitly deferred to E3/E4). +17 backend tests including a dedicated assertion that zero rows are ever written to `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`/`CefrDescriptor` | Deviates from the E0 plan in two small, justified ways: (1) `CefrResourceSource` needed new fields after all — E0 assumed none were needed, but E1's own admin-page requirements (license/commercial-use/student-display flags, attribution, version/download URL) required them; extending the existing entity (not creating a new one) is still consistent with E0's core "no duplicate source registry" decision. (2) The uploaded import file is processed in-memory from the request stream rather than persisted via `IFileStorageService` — the file is ephemeral import input, not a long-lived asset like audio, so persisting it added no value for this phase |
| 2026-07-08 | **Phase E2 implemented — gates 4-6**: `ResourceCandidateAnalysisService` (gate 4, advisory-only AI enrichment reusing `ActivityTemplateInstanceGenerator`'s AI-call pattern, new prompt key `resource_candidate_analyze`) suggests CEFR/skill/subskill/difficulty/tags/quality/safety metadata, degrading gracefully (never throwing, never corrupting candidate data) on AI failure or unavailability rather than the synchronous retry-then-throw behavior the student-facing template generator uses. `ResourceCandidateValidationService` (gates 5-6, fully deterministic) is the sole authority on `ValidationStatus` — the AI never sets it directly. Judgment calls: CEFR-confidence review threshold 0.6 (below → `NeedsReview`, never auto-pass); max `CanonicalText` length 5000 chars; any AI-reported safety tag is a hard `Failed`; attribution "required" when a source's `LicenseType` name contains `"BY"` (Creative-Commons-Attribution family), missing `AttributionText` in that case is a warning not a fail; `CandidateType.Unknown` always needs human review; a source's approval revoked after original import fails re-validation immediately. Exact-fingerprint dedup checked within-run/within-source/globally across `ResourceCandidate` — never against published `Cefr*` tables (they have no fingerprint column; adding one is a published-table schema change, out of scope) — a match is `NeedsReview`, never auto-deleted. New endpoints: analyze-one, validate-one (deterministic re-check only, no AI), analyze-import-run (batched, capped at 50/call — E7 owns real background processing). Admin UI gained Analyze/Re-validate actions and CEFR/skill/quality/validation display; no approve/publish action. +21 backend tests including a dedicated zero-published-rows assertion | AI-advisory-only was the explicit product rule for this phase — separating the AI-suggestion step (gate 4) from the deterministic-decision step (gates 5-6) into two services, rather than one combined service, makes this separation structurally enforced rather than just a convention a future edit could accidentally violate. The published-bank dedup cross-check was skipped because retrofitting a fingerprint column onto already-live `Cefr*` entities is genuinely out-of-scope schema work for a staging-phase gate, not a shortcut taken to save time |
| 2026-07-08 | **Phase E3 implemented — admin rendered preview**: `GET /api/admin/resource-candidates/{id}/preview` (`ResourceCandidatePreviewService`) returns a bank-type-specific rendered model (Vocabulary/Grammar/Reading), reusing `app-formio-renderer` only for `ActivityTemplateCandidate` rows and only after re-validating the schema live through `IFormIoSchemaValidationService` at preview time — never trusting E2's earlier validation pass as still-current. Any scoring/rubric metadata on template candidates stays in a separate admin-only field. Read-only end to end (no `SaveChangesAsync`, `UpdatedAtUtc` unchanged, asserted by test). Unsupported/malformed candidate shapes degrade to `CanPreview=false` + a warning rather than throwing. Admin UI gained a dedicated Preview drawer with a green "student-visible" panel and a slate "admin-only" panel, plus a persistent "E3 preview only" banner — no approve/reject/publish control added anywhere. +14 backend tests | **Corrects a scope assumption in the original Phase E0 plan**: E0's E3 section assumed an approve action would already exist by E3 and be UI-gated on the preview having been viewed. No approve action exists yet at all — that is E4's own deliverable, not something to retrofit a gate onto in E3. E3's actual job was narrower and correctly scoped once this was noticed: build the preview capability E4 will depend on, and document that E4 must build both the approve action itself and the "preview viewed before approve enabled" gate as part of its own deliverable, not inherit a half-built gate from E3 |
| 2026-07-08 | **Phase E4 implemented — publish to first banks**: new `ResourceCandidate.Approve(notes?)`/`.Reject(reason)` and `ResourceCandidatePublishService`, which re-checks every gate live at publish time (English-only, source approval, `AllowsStudentDisplay`/`AllowsCommercialUse` — hard-blocked here unlike E2's warn-only validation pass, `ValidationStatus == Passed`, `ReviewStatus == Approved`) and is idempotent (repeat publish returns the existing reference, never a duplicate row). `VocabularyEntry`→`CefrVocabularyEntry` and `GrammarProfileEntry`→`CefrGrammarProfileEntry` fully supported; `ReadingPassage`→`CefrReadingReference` supported only for staged text ≤500 characters (`CefrReadingReference`'s own doc comment: "only a short excerpt/citation, not a full copyrighted text"); `ActivityTemplateCandidate` publishing deferred entirely (`ActivityTemplate` needs a stable Key, valid Skill/Subskill, and real hand-authored `GenerationInstructions` a staged row was never designed to carry). New approve/reject/publish admin endpoints; admin UI gained Approve/Reject/Publish actions with clear disabled-reason messaging and a published-state indicator. +16 backend tests | Rejected two shortcuts that would have "worked" but been dishonest: silently truncating a full reading passage into `CefrReadingReference.ReferenceExcerpt` (lossy, misrepresents what was published), and inventing placeholder `GenerationInstructions` text to force an `ActivityTemplate` through (would publish a "template" that looks authored but wasn't). Both candidate types are cleanly blocked with explanatory errors instead, left for a future phase once a real staging shape exists. The `AllowsStudentDisplay`/`AllowsCommercialUse` hard-block (vs. E2's warn-only treatment) reflects that publish is the actual step moving content to a live, paying-student-facing table — by that point a missing permission is no longer just a note for a human, it's a real blocker. No "preview must be viewed before approve" tracking was built (E3 never added a preview-viewed flag) — documented as a known limitation, not silently dropped |
| 2026-07-08 | **Plan-Sync-After-E4**: decided to sequence **Phase E5 before Phase D1**, even though Phase D1's "E0-E4 before D1" technical gate is now met. E4 completed the first controlled publishing pipeline, but the published banks currently hold only small synthetic/test data with no browsing/search/admin-management surface — starting Today's bank-first composer now would have essentially nothing real to compose from. Phase E5 (published-bank browsing/search for the first supported banks: vocabulary, grammar, short reading references — surfacing source/license/provenance, CEFR, tags, quality, published status, and candidate traceability) is the next implementation phase. **After E5, a product decision checkpoint follows**: either start Phase D1 using whatever published banks exist by then, or continue Phase E6 (reading/listening resource depth) first — this doc does not resolve that choice now, it only sequences E5 ahead of both. Docs-only; no app code, migrations, or config changed; does not start E5 implementation, Phase D, or PG-v2 | A technical gate being met ("Phase E reached E4") is not the same as the gate's underlying intent being satisfied ("Phase D has real bank content to compose from"). The intent was always "enough of the resource-bank platform exists to give Phase D real bank content" (per the original Plan-Sync-After-C1 rationale) — a handful of synthetic/test rows with no way for an admin (or eventually Today's composer) to browse, search, or assess quality/coverage does not meet that bar, even though the E0-E4 pipeline itself is complete and correct |
| 2026-07-08 | **Phase E5 implemented — published bank browsing/search/admin management**: `ResourceBankQueryService` — list + detail queries for all 3 published bank types, filters (search text, CEFR level, source id), pagination capped at 200, sort newest-first by `CreatedAt` (documented fallback — none of the 3 entities has its own "published at" field). **Key finding**: no published bank entity carries a forward reference to its originating `ResourceCandidate` — traceability is implemented as a **reverse lookup** (`ResourceCandidate.PublishedEntityType`/`PublishedEntityId` matched against the bank row), returning an explicit "unavailable" result rather than throwing when no match exists; no new columns were added to any bank entity. A dedicated test confirms the pre-existing invariant that unpublished/rejected candidates can never appear in a bank-browse list. New `GET /api/admin/resource-banks/{vocabulary,grammar,reading-references}` list+detail endpoints; 3 new read-only admin pages (search/CEFR/source filters, paginated table, detail drawer with source/license/provenance/traceability) — **no edit or delete actions**, all mutation remains on Resource Candidates. +31 backend tests | The "no forward reference" finding confirms E0's original hybrid-entity decision was sound — a reverse query against `ResourceCandidate` is sufficient for E5's read-only browsing needs, so no schema change was needed on the already-live `Cefr*` tables just to support browsing. Keeping E5 strictly read-only (no edit/delete) was a deliberate scope discipline choice, not an oversight — Resource Candidates remains the single place mutation happens, avoiding two parallel edit surfaces for the same underlying data |
| 2026-07-08 | **Plan-Sync-E6-Decision**: resolved the Phase D1 decision checkpoint (opened by Phase E5) — **continue with Phase E6 before Phase D1**. Phase E5 built the published-bank browsing/search/admin-management surface, but the banks themselves still hold only small synthetic/test data — not enough real English content depth for Today's bank-first composer to produce useful lessons from. Phase E6 (deepen real English resource/content support — reading/listening resources per the original E0-E8 plan, still English-only, still no Persian/bilingual/support-language seed content, still no direct import-to-final-table bypass) is now the next recommended implementation phase. **Phase D1 remains deferred until after Phase E6, or until a later explicit product decision.** Docs-only; no app code, migrations, or config changed; does not start E6 implementation, Phase D, or PG-v2 | The Phase D1 decision checkpoint exists precisely to force this kind of explicit choice rather than let a technical gate ("the pipeline exists") silently stand in for a product judgment ("the pipeline has produced something worth composing from"). Phase E5 closed the *visibility* gap (an admin can now see what's published) but did not — and was never scoped to — close the *content depth* gap. Choosing E6 next keeps the same discipline the project has followed throughout Phase E: don't start the next consumer (Phase D) until the producer side (Phase E) has given it something real to consume |
| 2026-07-08 | **Phase E6 implemented — first real English content depth**: E6 was deliberately scoped narrower than its original E0-sketch ("reading/listening resource expansion") — a **controlled first content-depth slice** instead: 32 vocabulary entries (A1-B2), 12 grammar profile entries, 10 short reading excerpts (150-225 chars, under the 500-char publish limit), 100% original/internally-authored, English-only. Routed through the real staging→analysis/validation→approval→publish pipeline via a new `InternalResourceSeedPackSeeder`, not direct final-table seeding. New `ResourceImportService.ApplyDeterministicRowMetadata` copies an already-known `cefrLevel`/`skill`/`subskill` straight onto a candidate (reusing the existing `ApplyAnalysis` mutator, `cefrConfidence=1.0`, marked `"import-row-deterministic-mapping"`) instead of routing internally-authored content through AI-advisory analysis for metadata the author already asserts — no AI provider is invoked anywhere in this path; rows without these columns are unaffected. The seeder itself calls `ResourceCandidate.Approve(...)` on behalf of the reviewing admin, consistent with `ActivityTemplateSeeder`'s existing precedent for its own hand-authored, pre-reviewed content — every deterministic validation gate still runs for real. A dedicated test proves every published bank row resolves back to a `ResourceCandidate` marked published by the real publish workflow (no direct-final-table-bypass). Idempotent (source-name existence check; full rerun creates no duplicates). +14 backend tests (3,500 total). **No external dataset imported, no Persian/bilingual/support-language content added.** **A third Phase D1 decision checkpoint now applies, not resolved by this phase.** | Proving the E1-E5 pipeline could carry real, useful, original content end to end — not just synthetic test fixtures — was the actual goal, and doing it as a small slice (rather than a large import) kept the phase reviewable and low-risk. The deterministic-mapping fix avoids a backwards design (asking AI to "discover" metadata the content author already knows) and keeps the seeder's tests free of any live-AI dependency, matching this codebase's established testing convention. The seeder-performs-approval judgment call was made explicit and code-documented rather than silently assumed, following the exact precedent `ActivityTemplateSeeder` already set |
| 2026-07-08 | **Phase D1 implemented — first bank-first Today composer slice**: resolved the third Phase D1 decision checkpoint by starting D1 itself rather than deferring further. New `ITodayBankResourceSelector`/`TodayBankResourceSelector` queries the published Resource Bank (`IResourceBankQueryService`, unchanged) at the routing-recommended CEFR level, scoped to Today patterns whose `PrimarySkill` is `"Vocabulary"` or `"Reading"` only (grammar bank content included only opportunistically for `gap_fill_workplace_phrase`'s `Grammar` secondary skill — Today has no dedicated grammar pattern). `ActivityMaterializationJob` appends the selector's short supplement text onto the existing free-text `TopicHint` (the same mechanism `avoidRepeatingHint` already used) — **no AI prompt template changes needed**. Candidates are novelty-prechecked via a synthetic fingerprint (`"bank-vocab-precheck:{id}"` etc.), mirroring `PracticeGymGenerationJob`'s per-template precheck. Every unsupported pattern and every case with no matching bank rows falls back to legacy freeform generation completely unchanged — verified by a dedicated integration test asserting the `TopicHint` carries no bank marker in that case. Provenance is best-effort only: the single "primary" selected resource id is recorded via the pre-existing, previously-unused `StudentActivityReadinessItem.SetBankItemProvenance(...)` method when a readiness-pool item exists, flowing automatically into `StudentActivityUsageLog.SourceBankItemId` at attempt-submit time (no schema change); the full selected-resource list lives only in a structured log line. +13 backend tests (3,513 total: 5 architecture + 2,044 unit + 1,464 integration) — no migration required. **Discovered but explicitly not fixed this phase (out of scope, pre-existing, unrelated to D1's own code)**: `LearningSession.GenerationStatus` is configured with EF `HasDefaultValue(GenerationStatus.Ready)`, and since `Pending=0` is also the enum's CLR default, EF's "skip sending CLR-default values on insert" convention silently persists `Ready` instead of an explicit `MarkGenerationPending()` call made before a new session's first `SaveChangesAsync` — `LessonBatchGenerationJob` itself already uses this exact pattern (line 238) when creating background-generated sessions. Flagged as a new backlog item; a dedicated test writer had to route around it with a raw SQL fix-up to exercise `ActivityMaterializationJob`'s actual pending-session code path. **No external dataset imported, no Persian/bilingual/support-language content added, Today/Practice Gym legacy fallback not removed, PG-v2 implementation not started.** | The bank platform (E0-E6) had proven it could carry real content, but nothing consumed it yet — D1's job was to prove the *other* half: that Today's generator could actually use that content safely. Scoping to only Vocabulary/Reading patterns (rather than attempting all patterns) kept the first slice narrow and matched what the bank already has real content for; appending to the existing `TopicHint` field rather than adding new prompt-template variables avoided touching any AI prompt content, the highest-blast-radius surface in the whole system per the 2026-07-08 clean-architecture plan's own risk assessment. The discovered `GenerationStatus` default-value bug was flagged rather than silently fixed, since fixing it would touch `LessonBatchGenerationJob`'s tested, unrelated production behavior — outside this phase's narrow scope |
| 2026-07-08 | **Bugfix-D1A implemented — fixed the `LearningSession.GenerationStatus` default-value bug D1 discovered, before starting Phase D2**: root cause was `LearningSessionConfiguration`'s EF `.HasDefaultValue(GenerationStatus.Ready)` — since `GenerationStatus.Pending == 0` is also the enum's CLR default, EF's "omit CLR-default property values from the INSERT, let the DB default apply" convention silently discarded an explicit `MarkGenerationPending()` call made before a new session's first `SaveChangesAsync`, always persisting `Ready` instead. `LessonBatchGenerationJob.MaterializeSessionsAsync` uses exactly that construction order for every background-generated session. **Confirmed practical impact**: `StudentReadinessAuditService`'s "no stuck session generation" check (looking for sessions stuck in `Pending`/`Failed` 30+ minutes) could never fire, since affected sessions always read back as `Ready` immediately — a real diagnostic blind spot, not just a cosmetic mismatch. **Fix**: removed the `HasDefaultValue(...)` configuration entirely — migration `Bugfix_D1A_RemoveGenerationStatusDefault` is a clean `ALTER COLUMN generation_status DROP DEFAULT`, no column type change, no data touched, no data loss; `LearningSession` already defaults to `Ready` via its own property initializer in code, so the DB-side default was redundant and only ever created risk. **Audited all `HasDefaultValue(...)` configurations across `Configurations/*.cs`** for the same enum-CLR-default-collision class of bug: `AdminReviewStatus.NotRequired` (used in `ActivityTemplateConfiguration`, `PlacementItemDefinitionConfiguration`, `StudentActivityReadinessItemConfiguration`) and `FormRendererKind.FormIo` (`PlacementItemDefinitionConfiguration`, `StudentFlowTemplateVersionConfiguration`) are both configured to ordinal 0 — their own enum's CLR default — so no divergence is possible; no other live instance of this bug found. A handful of non-enum `HasDefaultValue(1)`-style numeric defaults (`ActivityTemplate.VersionNumber`, `ExerciseTypeDefinition`'s item/option counts, `PlacementItemDefinition.DifficultyBand`/`ItemVersion`) were not individually call-site-audited — flagged as a lower-priority backlog item, not the same confirmed-live bug class. +5 backend tests (`LearningSessionGenerationStatusPersistenceTests`: Pending/Ready/Failed each round-trip correctly through a real save/reload, plus a two-save Pending→Ready transition) proving the fix; one pre-existing test (`LessonBatchGenerationJobTests`) had its assertion corrected from `Ready` to `Pending` — it had been unknowingly asserting the bug's own symptom, since `ActivityMaterializationJob` (the only caller of `MarkGenerationReady`) never actually runs within that test. 3,513 → 3,518 total. **No external dataset imported, no Persian/bilingual/support-language content added, Today/Practice Gym legacy fallback not removed, Phase D2/E7-E8/PG-v2 implementation not started.** | This was scoped as a correctness/hardening phase specifically *before* expanding Today's bank-first composer (Phase D2) — building more consumers on top of a data-layer bug that silently discards explicit state transitions would only compound the risk. Removing the DB default (rather than renumbering the enum or adding conditional EF configuration) was the least risky fix: it required no data migration, no behavior change for any code path that doesn't explicitly set `GenerationStatus` (those already default to `Ready` in the CLR and always did), and directly targets the actual defect (a redundant, risk-creating DB-side default) rather than working around it. Renumbering `GenerationStatus` so `Pending != 0` was rejected as needlessly invasive — it would require a data migration to remap every existing row's stored ordinal and touch every enum comparison in the codebase, for no benefit over simply removing the unnecessary default |

---

## 19a. Phase Sequence (as of 2026-07-08, Bugfix-D1A)

Preferred order, each phase gated on the previous one's completion review:

1. ~~**Plan-Sync-B2**~~ — done (2026-07-08, docs-only): inserted Phase B2 into the sequence below, ahead of Phase C3.
2. ~~**Phase B2**~~ — done (2026-07-08): Activity Feedback, Repeat Policy, and Calibration Signals — **foundation only** (entity/migration, policy, API, minimal UI). See `docs/architecture/activity-feedback-and-calibration.md` for full scope and status. Cross-surface (Today + Practice Gym); admin-configurable per-surface feedback policy (off/optional/required). Automated calibration consumption of this signal is deferred to a future phase, not part of B2.
3. ~~**Phase C3**~~ — done (2026-07-08): migrated one pattern, `reorder_paragraphs`, via a new generic `ordered_sequence` scorer + stock Form.io `datagrid`. A full re-audit found **no further safe deterministic candidates** in the remaining ~25 legacy keys — see `docs/architecture/practice-gym.md`'s "Phase C3" and "Excluded patterns" sections.
4. ~~**Phase C4**~~ — **skipped, not pursued**: the C3 audit's negative result meant a real C4 would need genuinely new scope (audio-compatibility review or AI-evaluated-pattern Form.io support), not a small batch — see the Decision Log. Superseded by going straight to Phase C-Final.
5. ~~**Phase C-Final**~~ — done (2026-07-08): closed the deterministic-pattern Practice Gym migration track at 8/~33 pattern rows template-enabled; verified all 8 keys stable; documented the remaining 25 legacy keys with concrete exclusion reasons and 4 tracked backlog items. See `docs/architecture/practice-gym.md` and `docs/backlog/product-backlog.md`.
6. ~~**Phase E0**~~ — done (2026-07-08): finalized the resource-import-platform entity/status/gate model (planning only, no code). See `docs/architecture/english-resource-bank-import-platform.md`.
7. ~~**Plan-Sync-PG-v2**~~ — done (2026-07-08, docs-only): added the future skill-first Practice Gym v2 track (items 15-18 below) to the roadmap, after Phase E5-E8 and before Phase F/G. Does not change the Phase C-Final closure or start any implementation.
8. ~~**Phase E1**~~ — done (2026-07-08): first Phase E implementation slice — `CefrResourceSource` extended as source registry (no duplicate entity), new `ResourceImportRun`/`ResourceRawRecord`/`ResourceCandidate` staging entities, gates 1-3 only (English-only, license/source-approval, parser), CSV/JSON/JSONL import, admin CRUD/API/UI for Sources/Import Runs/Candidates. **Zero rows published to any `Cefr*` bank table** (E4's job, not started). See `docs/architecture/english-resource-bank-import-platform.md`.
9. ~~**Phase E2**~~ — done (2026-07-08): gates 4-6 — `ResourceCandidateAnalysisService` (gate 4, advisory-only AI enrichment) + `ResourceCandidateValidationService` (gates 5-6, sole deterministic authority on `ValidationStatus`, including exact-fingerprint dedup). Admin analyze/validate/batch-analyze endpoints and UI extensions. **Still zero rows published to any `Cefr*` bank table.** See `docs/architecture/english-resource-bank-import-platform.md`.
10. ~~**Phase E3**~~ — done (2026-07-08): admin rendered preview — `ResourceCandidatePreviewService` (bank-type-specific rendered models; `app-formio-renderer` reused only for `ActivityTemplateCandidate`, re-validated live for leak-safety), read-only, never mutates a candidate. Admin UI gained a Preview drawer with distinct student-visible/admin-only panels. **No approve action exists yet** — that is E4's own deliverable, along with the "preview viewed before approve" UI gate. See `docs/architecture/english-resource-bank-import-platform.md`.
11. ~~**Phase E4**~~ — done (2026-07-08): publish to first banks — `ResourceCandidatePublishService`, live gate re-checks, idempotent. `VocabularyEntry`/`GrammarProfileEntry` fully supported; short-excerpt `ReadingPassage` supported; `ActivityTemplateCandidate` publishing deferred (see Decision Log). **Some rows are now published** — vocabulary and grammar banks are no longer necessarily empty, though still likely small/sparse pending real source import (still no external dataset imported). See `docs/architecture/english-resource-bank-import-platform.md`.
12. ~~**Plan-Sync-After-E4**~~ — done (2026-07-08, docs-only): decided **Phase E5 comes before Phase D1**, even though D1's technical "E0-E4" gate is now met — the published banks are still too thin (small synthetic/test data, no browsing/search/admin-management surface) for Today's composer to have anything real to work with. See the Decision Log entry above for the full reasoning.
13. ~~**Phase E5**~~ — done (2026-07-08): published bank browsing/search/admin management — `ResourceBankQueryService` (list/detail, search/CEFR/source filters, reverse-lookup candidate traceability, no forward reference needed on the bank entities themselves), read-only admin pages for vocabulary/grammar/reading-references, no edit/delete actions. See `docs/architecture/english-resource-bank-import-platform.md`.
14. ~~**Phase D1 decision checkpoint**~~ — **resolved (2026-07-08, Plan-Sync-E6-Decision)**: continue with **Phase E6 before Phase D1**. The published-bank browsing/search surface exists (E5), but real English content depth is still too thin (small synthetic/test data only) for Today's composer to produce anything useful. See the Decision Log entry above.
15. ~~**Phase E6**~~ — done (2026-07-08): first real English content depth — 32 vocabulary / 12 grammar / 10 reading-excerpt rows, original/internally-authored/English-only, flowed through the full staging→validation→approval→publish pipeline via `InternalResourceSeedPackSeeder`. See `docs/architecture/english-resource-bank-import-platform.md` and the Decision Log entry above.
16. ~~**Phase D1 decision checkpoint (third instance)**~~ — **resolved (2026-07-08)**: started **Phase D1** itself rather than deferring further. See the Decision Log entry above.
16a. ~~**Phase D1**~~ — done (2026-07-08): first bank-first Today composer slice — `ITodayBankResourceSelector`/`TodayBankResourceSelector` inject published vocabulary/grammar/reading bank content into `ActivityMaterializationJob`'s AI prompt (`TopicHint`) for Vocabulary/Reading-primary-skill Today patterns only; legacy freeform generation is the unchanged fallback for every other pattern and every no-bank-match case. See `docs/architecture/learning-activity-engine.md` and the Decision Log entry above. Discovered (fixed by Bugfix-D1A, next item) the `GenerationStatus` default-value bug. **A follow-on decision point now applies**: expand Today bank-first support to more patterns/skills (Phase D2), continue Phase E7/E8 for more resource depth/search, or plan a larger Today composer migration — not resolved by this phase.
16b. ~~**Bugfix-D1A**~~ — done (2026-07-08): fixed `LearningSession.GenerationStatus`'s EF default-value bug discovered during D1 — removed `HasDefaultValue(GenerationStatus.Ready)` (migration `Bugfix_D1A_RemoveGenerationStatusDefault`, no data loss); +5 regression tests; one pre-existing test corrected. Run deliberately **before** Phase D2 as a correctness/hardening pass. See `docs/architecture/learning-activity-engine.md` and the Decision Log entry above.
17. **Phase E7-E8** — larger import support, RAG/search enrichment — available as a next step alongside Phase D2, per the follow-on decision above. Not started.
18. **Phase PG-v2A** — backend skill/objective-first Practice Gym selector (planned, not started; see `docs/backlog/product-backlog.md`). Sequenced after Phase E5-E8, not immediately after C-Final — a good skill-first selector needs enough published bank/resource content and search/selector coverage to have real options to choose from.
19. **Phase PG-v2B** — student Practice Gym UI simplified around skills, weak areas, review, challenge, recommended practice (planned, not started).
20. **Phase PG-v2C** — admin capability-registry cleanup / internal pattern management, reframing `ExerciseTypeDefinition`/`ExercisePatternDefinition` as internal capability config rather than the student-facing model (planned, not started; these entities are **not deleted** at any point in this sequence).
21. **Phase PG-v2D** — legacy type-driven Practice Gym path retirement, **only after the skill-first selector (PG-v2A/B) is proven** — not a forced cutover (planned, not started).
22. **Phase F** — legacy freeform-generation retirement, **per-pattern only, destructive only after each pattern's replacement is proven** — not a bulk deletion, and not started until Phase C-Final (done) and Phase D have each individually proven their replacement paths. Not started.
23. **Phase G** — admin bank/content navigation cleanup (consolidate the "Content" vs "AI System" nav split flagged in the 2026-07-08 clean-architecture plan) — deferred until enough new bank-first admin pages exist (Phase C-Final done + Phase E's admin pages, now started) to make a single consolidated redesign worthwhile rather than premature. Not started.

**Phase E has now reached E6, and Phase D1 has started and is complete.** The original
"E0-E4 before D1" gate, the Plan-Sync-After-E4 "E5 closes the browsing/search gap" gate, and the
Plan-Sync-E6-Decision "add real content depth" gate are all met, and the third Phase D1 decision
checkpoint they fed into was resolved by starting D1 itself. The published banks hold real,
original, English-only content (32 vocabulary / 12 grammar / 10 reading-excerpt rows — still no
external dataset imported, gated on real licensing approval per
`docs/architecture/cefr-resource-licensing-review.md`), can be browsed/filtered/searched by an
admin (Phase E5), and are now also consumed by Today's generator for a narrow first slice (Phase
D1: Vocabulary/Reading-primary-skill patterns only, legacy fallback unchanged everywhere else).
**Phase E6 is complete. Phase D1 is complete.** D1's own regression tests then surfaced a
pre-existing data-layer bug (`LearningSession.GenerationStatus`'s EF default-value convention
silently discarding explicit `Pending` transitions) — **Bugfix-D1A (2026-07-08) fixed it** as a
deliberate correctness/hardening pass before any further Today composer expansion (migration
`Bugfix_D1A_RemoveGenerationStatusDefault`, +5 regression tests, no data loss). **Bugfix-D1A is
complete.** A follow-on decision point now applies (item 17 above, not resolved by this phase):
expand Today bank-first support (Phase D2), continue Phase E7/E8 for more resource depth/search,
or plan a larger Today composer migration. **Full Phase D implementation (beyond D1's narrow
slice) has not started. Phase E7/E8 and PG-v2 implementation have not started.**

**Practice Gym v2 (PG-v2A-D) is planned, not started**, and is sequenced deliberately late — after
Phase E5-E8, before Phase F/G — because a skill/objective-first selector needs mature bank/resource
search and selector coverage to have real content to choose from. It does not change anything about
the already-closed Phase C-Final deterministic-pattern migration track, and it does **not** delete
`ExerciseTypeDefinition`/`ExercisePatternDefinition` at any point — see
`docs/architecture/practice-gym.md`'s "Future target: skill-first Practice Gym" section for the
full design intent.

**Phase C-Final is complete** (8 of ~33 Practice Gym pattern rows template-enabled:
`formio_practice_gym_pilot`, `phrase_match`, `gap_fill_workplace_phrase`,
`reading_multiple_choice_single`, `reading_multiple_choice_multi`, `reading_fill_in_blanks`,
`reading_writing_fill_in_blanks`, `reorder_paragraphs`; 25 legacy keys formally documented with
concrete exclusion reasons and 4 tracked backlog items). **No Phase C4** — the C3 audit found no
further safe deterministic candidates, so this track closed at C-Final instead.
**Phase B2 is complete as a foundation** (persistence/API/minimal UI; no automated calibration
consumption yet — see `docs/architecture/activity-feedback-and-calibration.md`).
**Phase E0 is complete** (entity/status/gate model finalized, no code — see
`docs/architecture/english-resource-bank-import-platform.md`).
**Phase E1 is complete** (staging foundation — source registry extension, import runs, raw
records, candidates, gates 1-3; zero rows published to any bank table).
**Phase E2 is complete** (gates 4-6 — AI-advisory analysis, deterministic rule validation,
exact-fingerprint dedup; still zero rows published).
**Phase E3 is complete** (admin rendered preview, read-only, student-visible/admin-only
separation; still zero rows published).
**Phase E4 is complete** (publish to first banks — `VocabularyEntry`/`GrammarProfileEntry`/short-
excerpt `ReadingPassage` supported, `ActivityTemplateCandidate` deferred; some rows now published,
from small synthetic/test staged data only, not real external content).
**Plan-Sync-After-E4 is complete** (docs-only — sequenced Phase E5 before Phase D1 despite D1's
technical gate now being met, since the published banks were still too thin for Today's composer
to use).
**Phase E5 is complete** (published-bank browsing/search/admin management — read-only, reverse
candidate traceability, no edit/delete).
**Plan-Sync-E6-Decision is complete** (docs-only — resolved the Phase D1 decision checkpoint:
continue with Phase E6 before Phase D1, since bank *visibility* now exists but real English
content *depth* does not — see `docs/architecture/english-resource-bank-import-platform.md`).
**Phase E6 is the next recommended implementation phase. Phase D implementation has not
started** — explicitly deferred until after E6 (or a later explicit decision); PG-v2
implementation also remains not started.

**Today lesson generation and all non-migrated Practice Gym patterns remain on the legacy
`IAiActivityGenerator` freeform path, unmodified, throughout this entire sequence** until their
specific Phase D/C-Final replacement is proven — this is not incidental, it is the explicit
safety discipline this whole roadmap segment is built on.

---

## 20. Maintenance Notes

- **Migrations are hand-authored**, named T1–T66 in sequence. Never auto-generate migrations.
- **AIProviderConfig** drives which model is used per feature; change in admin UI without code deploy.
- **Quartz jobs** are registered in `QuartzConfiguration.cs`. All production jobs use `[DisallowConcurrentExecution]`.
- **Audio files** are stored in MinIO under `speaking-recordings/`, `listening-audio/`, `placement-audio/` prefixes. Never expose storage keys in API responses.
- **StudentProfile.CefrLevel** is the student's current level. Placement updates it on completion (confidence >= 0.6). Admin can override. Students cannot edit it.
- **LearningPlan regeneration** is triggered by: placement completion, preference change, CEFR change (admin), mastery sweep. Always fire-and-forget; failure logged, never blocks caller.
- **ApplyMasterySignals (speaking)** defaults to false in `appsettings.json`. Must be explicitly enabled per environment.
- **Test projects** use SQLite in-memory. Never connect to real PostgreSQL in tests.
- **JWT_KEY** must be >= 32 chars outside Development. Startup fails if too short.
- **PublicApp:BaseUrl** must be set correctly for password reset links to work in production.
- **Data Protection keys** are persisted to file system (`dp_keys` Docker volume). Certificate protection optional for production hardening.
- **Email** is disabled by default (`Email__Enabled: false`). Set `Email__Enabled`, `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password` for SMTP delivery.
