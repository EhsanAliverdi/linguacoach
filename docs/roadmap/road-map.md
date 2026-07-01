---
status: current
lastUpdated: 2026-07-02
owner: product / engineering
---

# SpeakPath / LinguaCoach Roadmap

**Accurate as of: 2026-07-02 (Phase 19C complete)**

This is the canonical project memory document. It captures completed work, current state, known gaps, deferred items, and the recommended order of future phases.

---

## 1. Current Project Status

**Latest phase completed:** Phase 19C — Review Scaffold Practice Gym Pilot Rollout (2026-07-02)

**Branch:** main

**Test totals (as of 19C):**
- Backend unit: 1,715 (+11 from Phase 19C: pilot gate on/off, cap enforcement, label override, rollback, cross-student isolation, Today-lesson-exclusion proof, safe/friendly config defaults)
- Backend integration: 1,342 (+4 from Phase 19C: pilot-summary endpoint auth guards + count reporting)
- Architecture: 3
- **Backend total: 3,060**
- Angular unit (Karma): 1,505/1,626 success (121 pre-existing failures in `AdminStudentDetailComponent`/`AdminAiConfigComponent`, unrelated to this phase, 0 new regressions; the two files this phase touched — `admin-lessons.component.spec.ts` + `practice-gym.component.spec.ts` — 116/116 passing)
- Playwright E2E: unchanged (existing suite has no admin-lessons/practice-gym review-queue spec to extend; backend integration + Angular unit tests are the coverage for this phase's UI)

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

### Observability
- No production-level APM, distributed tracing, or alerting stack.
- AI cost/latency not tracked at per-call level in `SpeakingEvaluation`.
- Memory staleness detection (TODO-4) not implemented.

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

### Tier 3 — Medium-term (phases 8–10)

| Priority | Phase | Area | Why Next | Dependencies |
|---------:|-------|------|----------|-------------|
| 8 | 20A | Admin AI operations dashboard | Provider health, queues, retry tools, failure dashboard, cost/latency visibility | Speaking/writing eval stable |
| 9 | 21A | Enterprise SaaS organisation model | Organisations, teachers, groups, cohorts, org roles | 20A or parallel |
| 10 | 22A | Production operations hardening | Monitoring, backup/restore runbooks, smoke tests, deployment verification | 20A |

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

### Phase 20A — Admin AI Operations Dashboard

**Purpose:** Give admins full visibility into the AI evaluation pipeline: provider health, queue depths, retry queues, failure analysis, cost/latency per feature.

**Scope:**
- Provider health check endpoint (ping each configured provider)
- Speaking evaluation queue dashboard (Pending / Evaluating / Failed counts)
- Writing evaluation queue dashboard
- Per-feature cost and latency summary
- Retry tools (re-queue failed evaluations)
- Admin alert when failure rate exceeds threshold

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

### Phase 22A — Production Operations Hardening

**Purpose:** Ensure the production environment is observable, recoverable, and deployable with confidence.

**Scope:**
- Backup/restore runbook (PostgreSQL + MinIO)
- Post-deploy smoke test automation
- Deployment verification job (health check + DB migration verification)
- APM/tracing integration (OpenTelemetry or equivalent)
- Alert policy for: AI provider failures, evaluation queue depth, DB lag, 5xx rate

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
| Observability gap | Medium | Failures are silent; no production visibility | Phase 20A operations dashboard; OpenTelemetry integration |
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
| Production readiness | 40% | App runs in Docker. No smoke test automation, no backup runbook, no monitoring. |

---

## 19. Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
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
