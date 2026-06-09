---
status: historical
lastUpdated: 2026-06-09 13:11
owner: engineering
supersedes:
supersededBy: docs/architecture/README.md
---

# LinguaCoach — Implementation Roadmap

> **HISTORICAL DOCUMENT — Not the current source of truth.**
>
> This roadmap reflects the original MVP plan (T1–T12 task breakdown) written before the
> Course Session & Placement Redesign sprint (2026-06-09).
>
> **Superseded items:**
> - T10 "CEFR assessment" is superseded by the Placement Assessment MVP (standalone `PlacementAssessment` entity, 6-section structured assessment). The old T10 was a simple AI call; the new design is a full separate entity and flow.
> - T11 "Speaking sessions" is superseded by the implemented SpeakingRolePlay MVP and future Call Mode design. The old speaking session model (`SpeakingSession` / `SpeakingTurn`) was replaced by `LearningActivity` + `ActivityAttempt` with `ActivityType = SpeakingRolePlay`.
> - T12 "Admin management panel" remains valid but is lower priority than Placement and Course Session work.
> - The old "no audio upload in MVP" guidance is superseded — SpeakingRolePlay audio upload is now live.
>
> **Current source of truth:**
> - AGENTS.md
> - docs/architecture/course-session-learning-model.md
> - docs/architecture/placement-assessment-model.md
> - docs/architecture/exercise-pattern-library.md
> - docs/sprints/course-session-placement-redesign-sprint.md
> - docs/backlog/product-backlog.md
> - docs/architecture/README.md
>
> **When docs conflict, prefer current architecture docs and AGENTS.md over this file.**

Last updated: 2026-06-02 (historical)
Branch: main

---

## Status summary

| Task | Name | Status |
|------|------|--------|
| T1 | Domain model and solution structure | ✅ Done |
| T2 | Persistence and PostgreSQL migrations | ✅ Done |
| T3 | AI/learning/speaking schema and interfaces | ✅ Done |
| T4 | Auth + API + hardening | ✅ Done |
| T5 | Docker + CI/CD skeleton | ✅ Done |
| T6 | Angular + Tailwind frontend skeleton | ✅ Done |
| T7 | First AI writing exercise | ✅ Done |
| T8 | Writing feedback display and storage | ✅ Done |
| T9 | Vocabulary mastery and LearningPlanner implementation | ✅ Done |
| T10 | CEFR assessment | ✅ Done |
| T11 | Speaking sessions | ✅ Done |
| T12 | Admin management panel | ⬜ Next |

**Month 2 — First AI feature live (T7–T8): COMPLETE**
**Month 3 — Learning engine and speaking (T9–T11): COMPLETE**
**T12 is the final remaining task.**

---

## Completed tasks

### T1 — Domain model and solution structure
**Commits:** `db35b04`, `d328dc5`

Clean Architecture solution with five projects: Domain, Application, Persistence, Infrastructure, Api. Core domain entities: `Language`, `LanguagePair`, `LearningTrack`, `CareerProfile`, `StudentProfile`. Onboarding state machine with out-of-order guard. `AiPrompt` and `AiUsageLog` schema placeholders. Domain has zero EF Core or Identity dependencies.

---

### T2 — Persistence and PostgreSQL migrations
**Commits:** `03ca974`, `ee6c9be`

EF Core configurations for all T1 entities. `InitialSchema` migration with seeded Persian→English language pair, Workplace English track, Document Controller career profile. SQLite in-memory integration tests for entity mappings.

---

### T3 — AI/learning/speaking schema and interfaces
**Commit:** `fe8098c`

Six new domain entities: `VocabularyEntry` (SM-2 mastery lifecycle), `CurriculumWordList`, `UserLearningSummary` (200-char compact summaries), `SpeakingScenario`, `SpeakingSession` (state machine), `SpeakingTurn` (transcript-only MVP, `userAudioUrl` nullable). Five Application interfaces: `IAiProvider`, `IAiContextBuilder`, `ILearningPlanner`, `ISpeechToTextService`, `ITextToSpeechService`. `AiPrompt` extended with `MaxInputTokens`/`MaxOutputTokens` token budget columns. `T3_VocabularyAndSpeakingSchema` migration.

---

### T4 — Auth + API + hardening
**Commit:** `fe8098c`

ASP.NET Identity + JWT authentication. `ApplicationUser` with `Role` and `MustChangePassword`. Five API controllers: `AuthController`, `AdminController`, `OnboardingController`, `DashboardController`, `ReferenceController`. Application handlers and Infrastructure implementations for login, change-password, create-student, onboarding steps, dashboard query, reference queries. No-op STT/TTS stubs. `T4_IdentitySchema` migration. `WebApplicationFactory` integration tests (45 tests). JWT startup guard rejects placeholder key outside Development. TODO documented for `MustChangePassword` enforcement before first real user.

---

## Remaining tasks

---

### T5 — Docker + CI/CD skeleton

**Goal:** The API and PostgreSQL can run together locally via Docker Compose. Every PR and push to main runs a CI pipeline that builds and tests the backend. The deployment step exists as a placeholder but does not deploy anywhere yet.

**Scope:**
- `Dockerfile` for the .NET 10 API (multi-stage, build + runtime)
- `docker-compose.yml`: API service + PostgreSQL service, environment variables for connection string and JWT key
- `.github/workflows/ci.yml`: GitHub Actions pipeline that runs `dotnet build` and `dotnet test` on pull_request and push to main
- Angular build step in CI is a **placeholder** (`echo "Angular: pending T6"`) until T6 adds the project
- Deployment job is a **commented-out placeholder** for Fly.io; no actual deploy
- `appsettings.Development.json` documents which env vars to override for local Docker use
- CI must be green on the current codebase before T6 starts

**Not in scope:**
- Actual Fly.io deployment or any cloud infrastructure
- Angular scaffold (T6)
- Any new backend features
- Docker image publishing or registry push

**Acceptance criteria:**
- `docker-compose up` starts API and PostgreSQL locally without manual steps
- API `/api/reference/language-pairs` returns the seeded data when running in Docker
- GitHub Actions CI pipeline passes on main
- Build and test steps match what runs locally

**Test expectations:**
- No new tests required; existing 158 tests must pass in CI
- CI pipeline result is the test

**Review:** Lightweight self-check sufficient. No `/review` required unless CI wiring is non-trivial.

---

### T6 — Angular + Tailwind frontend skeleton

**Goal:** A working Angular application that a student can open in a browser, log in, complete all four onboarding steps, and reach the dashboard placeholder. Mobile-first layout with Tailwind CSS.

**Scope:**
- Angular project scaffold (`ng new`) inside `frontend/` directory
- Tailwind CSS configured
- Routing: `/login`, `/onboarding/step-1` through `/step-4`, `/onboarding/resume`, `/dashboard`
- `AuthGuard`: redirects unauthenticated users to `/login`
- Login page: email + password form, calls `POST /api/auth/login`, stores JWT in memory (not localStorage)
- Onboarding steps 1–4: each step calls the appropriate `PATCH /api/onboarding` endpoint
- Onboarding resume: on login, if `GET /api/onboarding/status` is not complete, resume from `lastCompletedStep`
- Dashboard placeholder: shows student's selected career profile name and "Your personalised plan is being prepared."
- Angular HTTP interceptor: attaches JWT to every request
- Angular build added to CI pipeline (replaces the placeholder from T5)
- `ng build` must pass in CI

**Not in scope:**
- Any AI features or lesson UI
- Speaking UI
- Vocabulary UI
- Admin UI (T12)
- Real-time features
- PWA or app store packaging
- OAuth / social login

**Acceptance criteria:**
- Admin creates a student via API, student can log in at `/login`
- Student completes 4 onboarding steps and reaches `/dashboard`
- Student who closes browser mid-onboarding resumes from the correct step on next login
- `ng build` passes in CI with no errors
- AuthGuard prevents unauthenticated access to protected routes

**Test expectations:**
- Angular component tests (Jest): login form, each onboarding step component, AuthGuard
- E2E (Playwright): happy path — login → onboarding → dashboard. Config only; may not run in CI until T6 is stable
- Backend integration tests already cover the API endpoints; Angular tests cover the UI layer

**Review:** Full `/review` required before marking T6 complete — this is the first user-facing slice and the first Angular code in the repo.

---

### T7 — First AI writing exercise

**Goal:** A student who has completed onboarding can request a writing exercise. The backend generates the exercise using GPT-4o via `IAiProvider`, returning a structured prompt. AI usage and cost are logged. This is the first real AI call in the system.

**Scope:**
- OpenAI GPT-4o implementation of `IAiProvider`
- `AiContextBuilder` implementation: fetches prompt template from DB, substitutes variables, enforces token budget from `AiPrompt.MaxInputTokens`/`MaxOutputTokens`
- `LearningPlanner` first implementation: selects vocabulary from `CurriculumWordList` for the student's career profile (new words only at this stage — no spaced repetition yet)
- First prompt template seeded in DB: `lesson.writing.v1` for Document Controller scenario ("polite follow-up email for pending approval")
- Seed `CurriculumWordList` entries for Document Controller (10–20 starter words)
- `POST /api/lessons/writing` endpoint: returns exercise prompt and target vocabulary
- `AiUsageLog` written after every AI call
- API key read from environment variable `OPENAI_API_KEY`; startup fails if missing in non-Development

**Not in scope:**
- Student submitting a draft response (T8)
- Spaced repetition scheduling (T9)
- CEFR level in the context packet (T10 — use a default "B1" until T10 is done)
- Speaking features

**Acceptance criteria:**
- `POST /api/lessons/writing` returns a structured JSON exercise with a scenario, vocabulary list, and instruction
- `AiUsageLog` has a row after the call with correct provider, model, token counts, and cost
- Token budget is enforced: a rendered prompt that exceeds `MaxInputTokens` logs a warning and fails with a controlled error, not a 500
- Exercise is deterministically shaped (AI content varies, but the structure — scenario, vocabulary items — is always present)

**Test expectations:**
- Unit tests: `LearningPlanner` word selection logic (mock DB), `AiContextBuilder` template substitution and token budget enforcement
- Integration test: mock `IAiProvider` returns a fixed JSON response; verify endpoint returns 200 with correct structure and `AiUsageLog` is written
- No test should make a real OpenAI call

**Review:** Full `/review` required. First AI call and first money-spending code in the system.

---

### T8 — Writing feedback display and storage

**Goal:** A student submits their draft response to a writing exercise. The backend sends the draft to AI for evaluation and returns structured feedback (score, mistakes, suggestions). Feedback and mistakes are stored.

**Scope:**
- `Lesson` and `LessonFeedback` domain entities and migration (new tables)
- `POST /api/lessons/writing/{lessonId}/submit` endpoint: accepts student draft, calls AI, returns `WritingFeedbackResult`
- Feedback stored in DB: overall score, mistakes JSON, suggestions, vocabulary used
- Student's `VocabularyEntry` records updated: `RecordUsage(correct)` called for each target word used correctly
- `UserLearningSummary` updated after submission with a session summary
- Angular: writing exercise UI shows the prompt, accepts draft text, submits, displays feedback

**Not in scope:**
- Spaced repetition scheduling (T9)
- CEFR re-assessment based on results (T10)

**Acceptance criteria:**
- Student submits a draft → receives score, corrected mistakes, and suggestions
- Feedback is stored and retrievable
- Vocabulary usage is reflected in `VocabularyEntry` counters
- `AiUsageLog` written for the feedback call

**Test expectations:**
- Integration tests: submit endpoint with mocked AI provider; verify feedback stored, vocabulary entries updated
- Unit tests: `WritingFeedbackResult` parsing from AI JSON response

**Review:** Lightweight self-check sufficient unless the AI response parsing is complex.

---

### T9 — Vocabulary mastery and LearningPlanner implementation

**Goal:** The LearningPlanner selects vocabulary using SQL-driven spaced repetition. Words due for review appear in lessons. The mastery lifecycle (New → Mastered) advances based on student performance.

**Scope:**
- Full `LearningPlanner` implementation: SM-2 algorithm for `nextReviewDate` and `easeFactor` scheduling
- Selection mix per lesson enforced: 3–5 new + 2–3 weak/review + 1–2 mastered-in-context
- Anti-repetition: words seen in the last 24 hours or last 3 lessons are excluded from new-word selection
- `LessonVocabularyLog` table and migration: records which words appeared in each lesson
- Status transitions enforced in domain and persisted correctly after each lesson
- `masteryScore` composite calculation updated after each interaction

**Not in scope:**
- CEFR-level-specific word selection (T10)
- Speaking vocabulary selection (T11)

**Acceptance criteria:**
- A student who sees a word and gets it correct twice transitions from New → Practised → Learning
- A student who gets a word wrong transitions to Weak; subsequent correct answers recover to Learning
- Words with `nextReviewDate <= today` are prioritised in lesson selection
- No word appears twice as a target word in consecutive lessons

**Test expectations:**
- Unit tests: SM-2 scheduling calculations, status transition rules, anti-repetition logic
- Integration tests: lesson generation selects correct mix from seeded vocabulary

**Review:** Full `/review` required — spaced repetition bugs are silent and hard to catch in production.

---

### T10 — CEFR assessment

**Goal:** After onboarding completes, the student is assessed for CEFR level. The level is stored on the student profile and used in every subsequent AI context packet.

**Scope:**
- `CefrLevel` field added to `StudentProfile` (new migration)
- `POST /api/assessment/cefr` endpoint: calls AI with a structured CEFR test, returns assessed level (A1–C2)
- CEFR assessment prompt template seeded: `cefr.assessment.v1`
- Dashboard shows CEFR level after assessment
- All AI context packets (lesson generation, writing feedback, speaking) use `CefrLevel` from DB; the "B1" default from T7 is replaced

**Not in scope:**
- Adaptive re-assessment over time (future)
- CEFR level affecting vocabulary difficulty selection (can be deferred to a follow-up)

**Acceptance criteria:**
- Student completes onboarding → is prompted to take CEFR assessment → level stored
- Subsequent lesson generation uses the stored level
- Assessment call is logged in `AiUsageLog`

**Test expectations:**
- Integration test: assessment endpoint with mocked AI; verify `CefrLevel` persisted on student profile
- Unit test: CEFR level parsing from AI structured response

**Review:** Lightweight self-check sufficient.

---

### T11 — Speaking sessions

**Goal:** A student can start a push-to-talk speaking session based on a `SpeakingScenario`. The browser sends a transcript per turn. The backend calls AI per turn, returns a structured response, and stores all turn data.

**Scope:**
- `POST /api/speaking/sessions` — create session from scenario
- `POST /api/speaking/sessions/{id}/turns` — submit transcript for a turn, receive AI reply + feedback
- `SpeakingTurnContext` assembled by `AiContextBuilder` (compact context packet per turn — no full history)
- Browser STT via Web Speech API handles transcription; backend receives transcript string only (`userAudioUrl` remains null)
- Speaking mistakes stored in `SpeakingTurn.MistakesJson`; surfaced to student after each turn
- Session completion: `SpeakingSession.Complete()` called after `MaxTurns`; session summary written
- Seed one `SpeakingScenario` for Document Controller
- Angular: speaking UI with push-to-talk button, AI question display, transcript display, per-turn feedback

**Not in scope:**
- Cloud STT/TTS (`ISpeechToTextService`/`ITextToSpeechService` remain no-op stubs)
- Audio upload or storage (`userAudioUrl` stays nullable)
- Real-time streaming or interruption handling

**Acceptance criteria:**
- Student starts a session, speaks for up to 6 turns, session completes with overall score
- Each turn stored with transcript, AI reply, scores, and mistakes
- Speaking mistakes feed into `UserLearningSummary` after session
- AI call is token-budgeted and logged

**Test expectations:**
- Integration tests: full session flow with mocked AI provider
- Unit tests: `SpeakingSession` state machine (already done in T3)

**Review:** Full `/review` required — first multi-turn stateful AI interaction.

---

### T12 — Admin management panel

**Goal:** An admin can manage the platform configuration through a UI: AI providers, model assignments, prompt templates, learning tracks, career profiles, and curriculum word lists.

**Scope:**
- Angular admin section (route-guarded to Admin role): `/admin/*`
- Manage prompt templates: list, view, create new version, activate/deactivate
- Manage career profiles and curriculum word lists: add/edit/reorder words
- Manage AI provider settings: which model is assigned to which feature (stored in DB, not hard-coded)
- `AiProviderConfig` table and migration: maps feature key → provider name + model name
- Student list: admin can see all students, their onboarding status, and CEFR level

**Not in scope:**
- Payments or subscription management
- Multi-organisation support
- Real-time analytics dashboard

**Acceptance criteria:**
- Admin can create a new prompt template version and activate it without a code deploy
- Admin can add a new curriculum word for Document Controller and it appears in the next lesson
- `AiProviderConfig` drives which model is used for each feature; changing it in the UI takes effect immediately

**Test expectations:**
- Integration tests: admin CRUD endpoints for prompt templates and career profiles
- Angular component tests: admin list and form components

**Review:** Full `/review` required before shipping — admin panel controls AI costs and prompt content.

---

## Key architecture constraints (all tasks)

- **PostgreSQL is the source of truth.** AI never drives progression or selects vocabulary.
- **Domain has no EF Core or Identity dependency.** Never import them into `LinguaCoach.Domain`.
- **AI receives only small structured context packets.** Never send full history.
- **Every AI call is logged** in `AiUsageLog` with provider, model, token counts, and cost.
- **Token budgets are enforced** by `AiContextBuilder` before every call.
- **No audio upload in MVP.** `SpeakingTurn.UserAudioUrl` stays nullable until cloud STT is confirmed.
- **No public registration.** Admin creates all accounts.
- **No hard-coded secrets.** All keys via environment variables or secrets manager.

---

## Month milestones

**Month 1 — Skeleton complete (T1–T6):**
The basic app runs. Admin creates a student. Student logs in, completes onboarding, reaches dashboard. Full CI/CD pipeline green. Docker runs locally.

**Month 2 — First AI feature live (T7–T8):**
A Document Controller student can request a writing exercise, submit a draft, and receive AI feedback. AI usage is logged. At least one real user completes session two.

**Month 3+ — Learning engine and speaking (T9–T12):**
Spaced repetition, CEFR assessment, speaking sessions, admin panel.
