---
status: current
lastUpdated: 2026-06-09 14:45
owner: product
supersedes:
supersededBy:
---

# SpeakPath Product Backlog

Status labels: `Not started` Â· `Planned` Â· `Blocked` Â· `Done`

Items are grouped by theme. Each item is a discrete unit of work; sub-bullets are acceptance criteria or notes.

---

## Documentation Governance `Done`

- [x] Documentation impact review rule for all code changes. `Done`
  - AGENTS.md now requires a documentation impact review in every code-change final report.
  - Major source-of-truth docs now carry freshness metadata.

---

## Admin UX, Student Management & AI Config Cleanup `Done`

Sprint doc: [admin-ux-student-management-ai-config-cleanup-sprint.md](../sprints/admin-ux-student-management-ai-config-cleanup-sprint.md)

- [x] Fix admin content width and dashboard responsiveness. `Done`
- [x] Remove permanent Create student sidebar item; Students page owns create action. `Done`
- [x] Add reusable toast service/component and create-student success toast. `Done`
- [x] Add admin student profile edit flow. `Done`
- [x] Add soft archive using `StudentLifecycleStage.Archived`; archived students are hidden by default and cannot sign in. `Done`
- [x] Hide Curriculum from admin navigation/dashboard while keeping route/API/data intact. `Done`
- [x] Complete AI feature routing rows for active runtime keys. `Done`
- [x] Add fallback provider/model/enabled controls to AI Config. `Done`

Deferred follow-ups:

- [ ] Redefine or remove Curriculum when LearningSession / ExercisePattern implementation decides whether curated seed/fallback content is needed. `Planned`
- [x] Add secure admin password reset flow for students. `Done`
- [x] Add student detail page with learning memory and reset tools. `Done` (2026-06-14)
  - Route `/admin/students/:id`, see `docs/sprints/2026-06-14-admin-student-detail-page.md`
  - Activity history not included — separate item below.
- [x] Add activity history to admin student detail page. `Done` (2026-06-14)

---

## Onboarding & Post-Placement UX Alignment `Done`

Engineering review complete (2026-06-09). See: [2026-06-09-onboarding-post-placement-ux-engineering-review.md](../reviews/2026-06-09-onboarding-post-placement-ux-engineering-review.md)
Sprint doc: [onboarding-post-placement-ux-alignment-sprint.md](../sprints/onboarding-post-placement-ux-alignment-sprint.md)

- [x] T1: Domain - `LearningGoalDescription`, `DifficultSituationsText` fields; `SetCareerContextText()` and extended skill method. `Done`
- [x] T2: Migration T31 - add two new varchar columns. `Done`
- [x] T3: Application - new `SetCareerContextTextRequest`, extend skill command with goal fields. `Done`
- [x] T4: API - extend `OnboardingStepDto` + controller dispatch for text career and skill+goal. `Done`
- [x] T5: API - add `lifecycleStage` to `DashboardResponse` and handler. `Done`
- [x] T6: Frontend step 3 - replace career list with free-text input. `Done`
- [x] T7: Frontend step 4 - add Listening, learning goal textarea, navigate to `/placement`. `Done`
- [x] T8: Frontend guard - redirect pre-onboarding to `/onboarding/resume` not `/dashboard`. `Done`
- [x] T9: Frontend dashboard - lifecycle-aware states (PlacementRequired CTA, CourseReady summary, Practice Gym section). `Done`
- [x] T10: Backend integration tests. `Done`
- [x] T11: Playwright E2E tests. `Done`

Completion notes:

- Onboarding supports free-text career context and native-language learning goals.
- After onboarding, students go to `/placement`; onboarding no longer starts background learning-path generation.
- Dashboard is lifecycle-aware and keeps Practice Gym secondary until Today / `LearningSession` is implemented.
- Verification passed: `dotnet test LinguaCoach.slnx`, `npm run build`, `npx playwright test`.

---

## Adaptive Onboarding & Staged Assessment `Not started`

From product owner brainstorm (2026-06-12). Architecture/planning notes only — not scoped for implementation.

**Direction:**

- Stage 1: Initial onboarding — quick entry, basic profile (language, goals, work/casual preference, confidence).
- Stage 2: Initial placement — starts at A1/A2, adapts difficulty based on answers, stops when confidence threshold reached.
- Stage 3: Ongoing diagnostic progress — grammar, vocabulary, listening, speaking, writing, reading, workplace communication, casual conversation each tracked as percentage completion via lessons over time.
- Stage 4: Adaptive course generation — lessons become more accurate as diagnostics improve.

**A1 support requirements:**

- Simple English in onboarding/placement questions.
- Optional native-language instruction support.
- Early tasks favor matching/short-phrase/listening over open writing.

**Relationship to [Configurable Onboarding and Placement Assessment](#configurable-onboarding-and-placement-assessment-not-started) below:** that item covers making *existing* onboarding/placement questions admin-configurable. This item is the larger product direction (staged assessment model) that configurable questions would eventually plug into. Do not implement either without further scoping — both require dedicated architecture review.

---

## Multi-Course / Enrolment Model `Not started`

From product owner brainstorm (2026-06-12). Future architecture direction — not current sprint implementation.

**Direction:**

```
Student
  -> Enrolments
      -> Course: Casual English
      -> Course: Workplace English
      -> Future: Academic English, Interview English, etc.
```

Today/Journey/Practice Gym and activity generation would be scoped to the student's active enrolment/course, allowing one student to study multiple English tracks with separate vocabulary, scenarios, tone, and progress tracking. AI prompts would receive course context.

**Notes:**

- Conflicts with current single-track `LearningPath`/`StudentProfile` model — would require an `Enrolment` entity and significant changes to session/path generation, AI context building, and progress tracking.
- Do not begin without a dedicated architecture review (`/plan-eng-review`).

---

## Estimated Known Words `Not started`

From product owner brainstorm (2026-06-12).

**Direction:** Show an estimated vocabulary range (e.g. "Estimated vocabulary: about 400–600 words"), workplace phrases known, recently learned, and needs-review counts — framed as an estimate/range, not a fake-precise number.

**Possible calculation basis:**

- Count mastered `StudentVocabularyItem` rows (status/strength-based).
- Add an estimated baseline range from CEFR level.
- Adjust from successful vocabulary/listening/reading attempt history.

**Notes:**

- Depends on `StudentVocabularyItem` / vocabulary extraction work (see [Vocabulary extraction from writing attempts](#vocabulary-extraction-from-writing-attempts-in-sprint-vocabulary-extraction-from-writing-attempts-sprint)) being further along.
- Display as a range, never a single precise count, per product direction.

---

## Configurable Onboarding and Placement Assessment `Not started`

**Priority:** P1 — after Placement MVP stabilisation, before serious pilot expansion

**Reason:** We are still learning what onboarding and placement should ask. Hardcoded questions slow iteration and make the product hard to improve without code changes.

**Description:**

Onboarding and placement questions should be configurable from admin/product configuration rather than hardcoded in Angular/backend. Admins should be able to modify and improve onboarding steps, onboarding questions, placement sections, placement questions, answer options, helper text, examples, and scoring/evaluation prompts without code changes.

**Scope:**

- [ ] Configurable onboarding steps (order, labels, instructions). `Not started`
- [ ] Configurable onboarding questions (per step: type, prompt, options, helper text). `Not started`
- [ ] Configurable placement sections (order, title, instructions, section type). `Not started`
- [ ] Configurable placement questions (per section: type, prompt, answer options, correct answer, scoring weight). `Not started`
- [ ] Configurable answer options (add/remove/reorder without code changes). `Not started`
- [ ] Configurable examples and helper text for each question. `Not started`
- [ ] Configurable skill tags per section (which skills a section scores). `Not started`
- [ ] Configurable listening scripts or script templates (replace hardcoded placement audio script). `Not started`
- [ ] Configurable placement evaluation prompt/template via DB prompt system (extends existing AI prompt infrastructure). `Not started`
- [ ] Versioning of assessment/question sets (track which version a student started). `Not started`
- [ ] Safe migration path for students who started an older version (resume on same version or notify admin of mismatch). `Not started`
- [ ] Admin preview/test mode (preview placement as a student without committing results). `Not started`

**Notes:**

- Current hardcoded location: `PlacementContent.cs` (backend) and `PlacementContent` static class
- Placement audio script is currently hardcoded in the `listening` section definition
- The existing AI prompt DB infrastructure (`AiPromptTemplate`, `DefaultAiSeeder`) is the natural extension point for configurable evaluation prompts
- Do not implement as part of Placement MVP stabilisation — backlog only until pilot feedback confirms which questions need to change

---

## Course Session & Placement Redesign â€” Implementation Phases

Architecture sprint complete (2026-06-09). See sprint doc: [course-session-placement-redesign-sprint.md](../sprints/course-session-placement-redesign-sprint.md)

### Phase 1 â€” Placement Assessment MVP `Partially done`

- [x] Add `StudentLifecycleStage` enum and column to `StudentProfile` + migration. `Done` (T29)
- [x] Add `PreferredSessionDurationMinutes` to `StudentProfile` + migration. `Done` (T29)
- [x] Add `ProfessionalExperienceLevel` enum and column to `StudentProfile` + migration. `Done` (T29)
- [x] Add `RoleFamiliarity` enum and column to `StudentProfile` + migration. `Done` (T29)
- [x] Add `PlacementAssessment` entity, EF config, migration. `Done` (T29)
- [x] Implement placement section handlers and `PlacementService`. `Done`
- [x] Add placement flow to Angular (6 sections, progress, result screen). `Done`
- [x] Add lifecycle-aware routing guard. `Done` (placement.guard.ts â€” guard fix pending in UX alignment sprint)
- [x] Add backend integration tests for placement flow. `Done`

### Phase 1 â€” Placement Assessment MVP `Not started` (remaining)

- [ ] Add `StudentLifecycleStage` enum and column to `StudentProfile` + migration. `Not started`
- [ ] Add `PreferredSessionDurationMinutes` to `StudentProfile` + migration. `Not started`
- [ ] Add `ProfessionalExperienceLevel` enum and column to `StudentProfile` + migration. `Not started`
- [ ] Add `RoleFamiliarity` enum and column to `StudentProfile` + migration. `Not started`
- [ ] Add `WorkplaceSeniority` (DomainComplexity) computed column or property to `StudentProfile`. `Not started`
- [ ] Update onboarding to collect session duration preference, professional experience level, and role familiarity. `Not started`
- ~~Add `ModuleType` column to `LearningModule` (Standard, Placement)~~ `Superseded` â€” Placement is a standalone `PlacementAssessment` entity, not a LearningModule. No ModuleType column needed.
- [ ] Add `PlacementAssessment` entity, EF config, migration. `Not started`
- [ ] Add `PlacementSection` entity, EF config, migration. `Not started`
- [ ] Add `placement_assessment_evaluate` AI prompt seed. `Not started`
- [ ] Implement placement section handlers (self-check, vocab/grammar, reading, listening, writing, speaking). `Not started`
- [ ] Implement `PlacementResultGeneratorService` (AI-evaluated result). `Not started`
- [ ] Feed placement result into `StudentSkillProfile` and `UserLearningSummary`. `Not started`
- [ ] Add placement flow to Angular (6 sections, progress, result screen). `Not started`
- [ ] Add lifecycle-aware routing guard (redirect to correct stage). `Not started`
- [ ] Add backend integration tests for placement flow. `Not started`
- [ ] Add Playwright tests for placement flow. `Not started`

### Phase 2 â€” Course Session MVP `Not started`

- [ ] Add `LearningSession` entity, EF config, migration. `Not started`
- [ ] Add `SessionExercise` entity, EF config, migration. `Not started`
- [ ] Implement session generator (backend-driven, not AI-driven). `Not started`
- [ ] Generate sessions based on duration, level, career context, learning memory. `Not started`
- [ ] Add Today page (replaces activity-card dashboard as primary student entry point). `Not started`
- [ ] Add session progress component to Today page. `Not started`
- [ ] Add session completion tracking. `Not started`
- [ ] Add backend integration tests for session generation. `Not started`
- [ ] Add Playwright tests for Today page and session flow. `Not started`

- [ ] Activity Teach page (Page 1) micro-lesson content - see `docs/sprints/2026-06-15-activity-teach-page-microlesson-content-sprint.md`. `Planned`

### Phase 3 â€” Exercise Pattern Engine `Not started`

- [ ] Define exercise pattern library in code (pattern key â†’ pattern config). `Not started`
- [ ] Implement session generator pattern selection logic. `Not started`
- [ ] Implement `teams_chat_simulation` pattern (content model, UI, evaluation). `Not started`
- [ ] Implement `read_and_answer`, `gap_fill_with_workplace_phrase`, `phrase_match`, `collocation_match`. `Not started`
- [ ] Link `SessionExercise` to `LearningActivity` via pattern-to-activity mapping. `Not started`
- [ ] Add pattern-level integration tests. `Not started`

### Phase 4 â€” Practice Gym `Not started`

- [ ] Add Practice tab to student navigation. `Not started`
- [ ] Move dashboard activity cards under Practice tab. `Not started`
- [ ] Today page becomes primary student home. `Not started`
- [ ] Keep existing `/activity?type=...` routing unchanged. `Not started`
- [ ] Add Playwright tests for Practice tab navigation. `Not started`

### Phase 5 â€” MinIO File Storage `Not started`

- [ ] Define `IFileStorageService` interface in Application. `Not started`
- [ ] Implement `LocalFileStorageService` in Infrastructure. `Not started`
- [ ] Implement `MinioFileStorageService` in Infrastructure (Minio .NET SDK). `Not started`
- [ ] Migrate `ListeningAudioService` to use `IFileStorageService`. `Not started`
- [ ] Migrate `SpeakingAudioService` to use `IFileStorageService`. `Not started`
- [ ] Migrate `PlacementAudioService` to use `IFileStorageService`. `Not started`
- [ ] Update audio streaming endpoints to use `IFileStorageService`. `Not started`
- [ ] Add MinIO to Docker Compose (staging). `Not started`
- [ ] Add unit tests for both file storage implementations. `Not started`
- [ ] Verify audio playback end-to-end in staging with MinIO. `Not started`
- [ ] **Fix placement audio volume permissions in Docker.** `Blocked — deferred to MinIO migration` The named Docker volume mounted at `/app/audio-data` is not writable by the container user (non-root). Placement TTS audio generation fails with `Permission denied` in production. The frontend correctly shows the fallback message. Fix options: (a) set correct ownership in the Dockerfile (`RUN mkdir -p /app/audio-data && chown app:app /app/audio-data`), or (b) migrate to MinIO (the planned Phase 5 path, already has a shared MinIO instance running on the VPS). Option (b) is preferred — do not spend time on option (a) unless MinIO migration is blocked.

### Phase 6 â€” Admin Reset Tools `Not started`

- [ ] Add `StudentResetLog` entity, EF config, migration. `Not started`
- [ ] Implement `POST /api/admin/students/{id}/reset` endpoint. `Not started`
- [ ] Implement lifecycle stage transition logic in reset handler. `Not started`
- [ ] Implement audio file cleanup via `IFileStorageService` on reset. `Not started`
- [ ] Add admin UI: lifecycle stage badge on student detail page. `Not started`
- [ ] Add admin UI: reset modal with confirmation and reason input. `Not started`
- [ ] Add backend integration tests for reset endpoint. `Not started`
- [ ] Add Playwright tests for admin reset flow. `Not started`

---

## Professional Experience Level & Domain Complexity `Not started`

Architecture doc: [professional-experience-domain-complexity.md](../architecture/professional-experience-domain-complexity.md)

Priority: P0/P1 â€” affects onboarding, placement, and session generation quality. Without this, SpeakPath may give students linguistically appropriate tasks that are professionally inappropriate.

- [ ] Define `ProfessionalExperienceLevel` and `RoleFamiliarity` enums in domain. `Not started`
- [ ] Define `DomainComplexity` enum (`BasicWorkplace`, `JuniorRole`, `IndependentContributor`, `SeniorSpecialist`, `LeadOrManager`). `Not started`
- [ ] Add experience level and role familiarity steps to Angular onboarding flow. `Not started`
- [ ] Implement `WorkplaceSeniority` computation (experience level Ã— role familiarity â†’ DomainComplexity). `Not started`
- [ ] Add `WorkplaceSeniority` field to `StudentProfile` (stored, updated after onboarding). `Not started`
- [ ] Add `{{DomainComplexity}}` and `{{ProfessionalExperienceLevel}}` prompt variables to all AI content generation prompts. `Not started`
  - `activity_generate_writing`
  - `activity_generate_listening`
  - `activity_generate_speaking_roleplay`
  - `placement_assessment_evaluate`
  - `learning_path_generate`
  - `learning_path_generate_adaptive`
- [ ] Add domain complexity rule to all prompts: do not introduce concepts beyond student's DomainComplexity unless a micro-lesson teaches it first. `Not started`
- [ ] Update session generator to filter workplace scenario topics by `WorkplaceSeniority`. `Not started`
- [ ] Update placement assessment prompt to use `BasicWorkplace`/`JuniorRole` domain complexity by default. `Not started`
- [ ] Add domain complexity override option to Practice Gym (simple / normal / challenge). `Not started`
- [ ] Add `AvoidedDomainComplexity` tracking: when a new concept is introduced, mark it as "introduced" so it can be reused without a micro lesson. `Not started`
- [ ] Add backend integration tests for WorkplaceSeniority computation and prompt variable inclusion. `Not started`
- [ ] Add Playwright tests for onboarding experience level and role familiarity steps. `Not started`

---

## Competitive Gap â€” P1 Features `Not started`

From competitive gap review (2026-06-09). See sprint doc for full matrix.

### TeamsChatSimulation (P1)

- [ ] Design `teams_chat_simulation` content model and API response shape. `Not started`
- [ ] Implement `TeamsChatSimulationGenerator` (AI-generated Teams chat scenario). `Not started`
- [ ] Implement `TeamsChatSimulationEvaluator` (tone, phrase use, conciseness, completeness). `Not started`
- [ ] Add Teams chat UI to Angular activity-lesson (chat bubble layout, word counter, hint phrases). `Not started`
- [ ] Add TeamsChatSimulation to Practice Gym. `Not started`
- [ ] Add backend integration tests for TeamsChatSimulation. `Not started`
- [ ] Add Playwright tests for Teams chat flow. `Not started`

### Vocabulary Queue Cards (P1)

- [ ] Design vocabulary card types: cloze, collocation, phrase, use-in-sentence. `Not started`
- [ ] Implement card queue scheduling (new/weak/mastered spaced repetition). `Not started`
- [ ] Add `/vocabulary` card mode UI (swipe-style or inline card deck). `Not started`
- [ ] Add collocation card generation from student's existing vocabulary queue. `Not started`
- [ ] Add backend integration tests for vocabulary card scheduling. `Not started`

### Micro Lessons (P1)

- [ ] Implement `micro_lesson_phrases` pattern: AI generates 3â€“5 target phrases with usage examples before a lesson session. `Not started`
- [ ] Implement `micro_lesson_dialogue` pattern: AI generates a short workplace dialogue to model before speaking/writing tasks. `Not started`
- [ ] Implement `micro_lesson_mistake` pattern: pulls a recurring mistake from student memory and explains it before a correction exercise. `Not started`
- [ ] Add micro lesson step to Angular session exercise flow (read-only, no submission, auto-advance). `Not started`
- [ ] Add micro lesson AI prompts to seed data. `Not started`

### Weekly Plan (P1, part of Phase 2 session model)

- [ ] Add weekly session schedule generation after placement completes. `Not started`
- [ ] Store weekly plan as pre-generated `LearningSession` slots for the coming week. `Not started`
- [ ] Add Today page weekly calendar strip (days of week, completed/upcoming indicators). `Not started`
- [ ] Respect student's preferred practice frequency from onboarding. `Not started`

---

## Competitive Gap â€” P2 Features `Not started`

### Call Mode / Open AI Speaking (P2)

- [ ] Design Call Mode product spec: multi-turn AI-first voice conversation. `Not started`
- [ ] Implement `call_mode_single_turn` pattern (AI speaks, student responds). `Not started`
- [ ] Implement `call_mode_multi_turn` pattern (3â€“5 AI/student turns, post-call transcript + feedback). `Not started`
- [ ] Add Call Mode UI to Practice Gym (phone-style interface, AI speaks first). `Not started`
- [ ] Add post-call feedback screen (transcript, per-turn coaching, vocabulary, tone summary). `Not started`
- [ ] Wire real STT provider (OpenAI Whisper or Azure Speech) for Call Mode transcription. `Not started`
- [ ] Add backend integration tests for call mode flow. `Not started`
- [ ] Add Playwright tests for Call Mode UI. `Not started`
- **Note:** Call Mode requires real STT. Do not implement with fake STT only.

### Pronunciation MVP (P2)

- [ ] Design pronunciation engine product spec (problem words, repeat-after-me, word stress, intonation). `Not started`
- [ ] Evaluate STT/ASR providers for phoneme-level feedback (ELSA-style vs simpler). `Not started`
- [ ] Implement `PronunciationPractice` activity type (backend + frontend). `Not started`
- [ ] Add pronunciation patterns to exercise library: problem word drills, repeat-after-me, stress/intonation. `Not started`
- [ ] Add Pronunciation section to Practice Gym. `Not started`
- **Note:** Pronunciation is separate from speaking communication. Do not conflate with SpeakingRolePlay.

### Real STT Provider (P2)

- [ ] Evaluate OpenAI Whisper vs Azure Speech vs Google STT for accuracy and cost. `Not started`
- [ ] Add real STT provider implementation behind `ISpeechToTextService`. `Not started`
- [ ] Wire into SpeakingRolePlay and Call Mode flows. `Not started`
- [ ] Add STT usage cost tracking. `Not started`

### Real TTS Provider (P2)

- [ ] Evaluate OpenAI TTS vs Azure TTS vs Google TTS for quality and cost. `Not started`
- [ ] **Implement `OpenAiTextToSpeechService` behind `ITextToSpeechService`.** `Not started` OpenAI TTS (`tts-1` model, `onyx` or `echo` voice) is the preferred first provider. `OPENAI_API_KEY` is already wired in production compose. Add `TTS_PROVIDER=OpenAI` env var and register via `DependencyInjection.cs` based on config. This will make placement listening audio audible in production — currently `FakeTextToSpeechService` generates silent WAV (correct for tests, silent in prod).
- [ ] Wire into listening activity generation (`ListeningAudioService`). `Not started`
- [ ] Wire into placement listening audio (`PlacementAudioService`). `Not started`
- [ ] Add TTS usage cost tracking. `Not started`
- [ ] Add TTS audio cache cleanup job to `LinguaCoach.Worker`. `Not started`

### Advanced TTS Voice Configuration (P2)

**Priority:** P2 — after Real TTS Provider is wired and audible in production

**Reason:** Once a real TTS provider is live, product quality depends on voice selection matching the professional tone and learning context. Hardcoded voice choices are a short-term expedient; configurable per-feature voice settings are needed before scaling to multiple activity types and student cohorts.

**Scope:**

- [ ] Define voice configuration model: accent, gender, voice style (e.g. neutral, warm, authoritative), speech rate. `Not started`
- [ ] Add per-feature TTS voice assignment: each AI feature key (`activity_generate_listening`, `placement_assessment_evaluate`) can have its own default voice. `Not started`
- [ ] Add fallback voice/provider: if the primary TTS voice is unavailable, fall back to a configured secondary voice or provider. `Not started`
- [ ] Expose voice configuration in Admin AI Config UI: admin can select voice, accent, style, and speed per feature. `Not started`
- [ ] Add admin preview: play a sample sentence using the configured voice without triggering real activity generation. `Not started`
- [ ] Add regeneration rule: when voice settings change for a feature, mark cached audio for that feature as stale and regenerate on next use. `Not started`
- [ ] Add backend support for `voiceId`, `accent`, `style`, `speedFactor` fields in `AiProviderConfig` or a new `TtsVoiceConfig` entity. `Not started`
- [ ] Evaluate provider-specific voice IDs for OpenAI TTS (`alloy`, `echo`, `fable`, `onyx`, `nova`, `shimmer`) and Azure Neural Voices. `Not started`
- [ ] Document voice/accent choices and their intended use case (e.g. `onyx` for listening exercises — neutral professional male). `Not started`

**Notes:**

- Do not implement before `OpenAiTextToSpeechService` is wired and audible (see Real TTS Provider above).
- Voice config is separate from AI model config — TTS provider selection is environment-configured; voice selection is content/UX policy.
- Current hardcoded voice: `onyx` / `echo` in `PlacementAudioService` and `ListeningAudioService`.

---

### AI Tutor Persona (P2)

- [ ] Define AI teacher name and voice persona (e.g. "Alex" â€” encouraging, professional tone). `Not started`
- [ ] Add AI teacher voice to session-opening micro lessons (text first, TTS audio when provider available). `Not started`
- [ ] Add tutor persona to lesson_reflection step output. `Not started`
- [ ] Avatar is P3 â€” do not design now. `Not started`

---

## Competitive Gap â€” P3 Features `Not started`

- [ ] AI avatar / visual tutor interface. `Not started`
- [ ] Video micro lessons. `Not started`
- [ ] Multimodal workplace uploads (email/doc/screenshot â†’ AI converts to exercise). `Not started`
- [ ] Advanced enterprise analytics (employer dashboard, cohort progress). `Not started`
- [ ] Organisations / teams / employer accounts. `Not started`

---

## SpeakingRolePlay activity MVP (in sprint: speaking-role-play-mvp-sprint) â€” **COMPLETE**

> SpeakingRolePlay MVP was delivered in the speaking-role-play-mvp-sprint (2026-06-08).
> All items below are Done. The Speaking dashboard card is active.

- [x] Add sprint documentation for SpeakingRolePlay MVP. `Done`
- [x] Add `ISpeechToTextService` interface and `FakeSpeechToTextService`. `Done`
- [x] Add `SpeakingAudioService` (store, commit, serve, per-student DB count limit). `Done`
- [x] Add `SpeakingRolePlayEvaluator` (AI evaluation of transcript). `Done`
- [x] Add `activity_generate_speaking_roleplay` prompt seed. `Done`
- [x] Add `activity_evaluate_speaking_roleplay` prompt seed. `Done`
- [x] Add SpeakingRolePlay to `AiActivityGeneratorHandler` (generation + evaluation guards). `Done`
- [x] Add SpeakingRolePlay branch to `ActivityGetHandler` (AI + inline fallback + typed routing guard). `Done`
- [x] Add SpeakingRolePlay branch to `ActivitySubmitHandler` (STT â†’ evaluator dispatch). `Done`
- [x] Extend `ActivityDto` with 8 speaking fields. `Done`
- [x] Extend `ActivityFeedbackDto` with 4 speaking feedback fields. `Done`
- [x] Add `POST /api/activity/{id}/speaking-attempt` (multipart) to `ActivityController`. `Done`
- [x] Add `GET /api/activity/{id}/attempts/{attemptId}/audio` to `ActivityController`. `Done`
- [x] Add `AudioStorageKey` nullable column to `ActivityAttempt` + migration. `Done`
- [x] Add SpeakingRolePlay branch to `ActivityAttemptsHandler` (history). `Done`
- [x] Add speaking states to Angular `activity-lesson` PageState union. `Done`
- [x] Implement speaking recording UI (record, stop, preview, submit). `Done`
- [x] Implement speaking feedback view (transcript, coach summary, strengths, improvements). `Done`
- [x] Update activity history component for SpeakingRolePlay attempts. `Done`
- [x] Activate dashboard Speaking card (remove "Coming soon"). `Done`
- [x] Add config defaults and `.env.example` entries for STT and speaking audio. `Done`
- [x] Add backend integration tests for SpeakingRolePlay. `Done`
- [x] Add Angular unit tests for speaking states. `Done`
- [x] Add Playwright E2E tests for speaking flow. `Done`

---

## VocabularyPractice activity (in sprint: vocabulary-practice-activity-sprint)

- [x] Add `StrengthScore` to `StudentVocabularyItem` + migration T27. `Done`
- [x] Extend `ActivityDto` + `SubmitActivityAttemptCommand` for VocabularyPractice fields. `Done`
- [x] Add `VocabularyPracticeGenerator` (deterministic fill-blank, no AI). `Done`
- [x] Add VocabularyPractice selection logic to `ActivityGetHandler`. `Done`
- [x] Add `VocabularyPracticeEvaluator` (deterministic scoring + vocab status updates). `Done`
- [x] Update `ActivitySubmitHandler` for VocabularyPractice evaluation. `Done`
- [x] Update `ActivityController` response to include VocabularyPractice fields. `Done`
- [x] Update Angular activity-lesson component to render VocabularyPractice. `Done`
- [x] Update activity-history component for VocabularyPractice attempts. `Done`
- [x] Add backend integration tests for VocabularyPractice. `Done`
- [x] Add Angular unit tests and Playwright tests. `Done`

---

## Full app verification and disabled actions cleanup (in sprint: full-app-verification-disabled-actions-cleanup-sprint)

- [x] Add sprint documentation for full app verification and disabled-actions cleanup. `Done`
- [x] Enable implemented Writing, Listening, and Vocabulary dashboard entry points. `Done`
- [x] Keep only unimplemented Speaking and Pronunciation dashboard cards marked "Coming soon". `Done`
- [x] Route dashboard Writing/Listening cards through typed `/activity` requests. `Done`
- [x] Remove stale listening/vocabulary "coming soon" copy from profile and landing surfaces. `Done`
- [x] Add Playwright coverage for implemented dashboard actions. `Done`
- [x] Add Playwright coverage for admin AI usage loading. `Done`
- [x] Add Playwright coverage for student denial on admin-only routes. `Done`

---

## ListeningComprehension text MVP (in sprint: listening-comprehension-text-mvp-sprint)

- [x] Add sprint documentation for the text-based listening MVP. `Done`
- [x] Add `activity_generate_listening` prompt seed. `Done`
- [x] Extend `ActivityDto` and attempt submission contract for listening fields. `Done`
- [x] Add `ListeningComprehension` selection rule in `ActivityGetHandler`. `Done`
- [x] Hide transcript/audioScript and expected answers before submit. `Done`
- [x] Add deterministic listening comprehension evaluation. `Done`
- [x] Reveal transcript and expected answer summaries after submit. `Done`
- [x] Update Angular `/activity` to render listening comprehension. `Done`
- [x] Add backend integration tests for selection, safe DTO, scoring, transcript reveal, and ownership. `Done`
- [ ] Add richer activity history UI for listening attempts. `Planned`
- [ ] Add Playwright coverage for listening activity flow. `Planned`
- [ ] Add real generated audio/TTS. `Not started`
- [ ] Add audio player, replay controls, speed controls, timed captions. `Not started`
- [ ] Add listening-specific memory and skill profile updates. `Not started`

---

## Listening audio/TTS (in sprint: listening-audio-tts-sprint)

- [x] Add sprint documentation for listening audio/TTS. `Done`
- [x] Upgrade `ITextToSpeechService` for generated listening audio. `Done`
- [x] Add fake deterministic TTS provider for tests and local development. `Done`
- [x] Add local audio storage service for listening activities. `Done`
- [x] Store audio metadata in listening activity content. `Done`
- [x] Add authenticated `GET /api/activity/{activityId}/audio` endpoint. `Done`
- [x] Hide transcript and expected answers before submit while exposing only audio URL. `Done`
- [x] Add native audio player to `/activity`. `Done`
- [x] Add listening audio support to activity history. `Done`
- [x] Add config defaults and `.env.example` entries. `Done`
- [x] Add backend tests for safe DTO and audio endpoint auth/ownership/content type. `Done`
- [x] Add Playwright coverage for audio player and no-audio fallback. `Done`
- [ ] Integrate real provider-backed TTS. `Not started`
- [ ] Add audio replay limits, speed controls, and timed captions. `Not started`
- [ ] Add audio cache cleanup. `Not started`
- [ ] Add admin audio usage reporting. `Not started`

---

## Vocabulary extraction (cross-cutting engine, in sprint: vocabulary-extraction-from-writing-attempts-sprint)

> Note: despite the sprint name, extraction is not limited to writing attempts — it fires
> from any activity that produces AI-generated `Corrections` (AiStructured/AiOpenEnded
> patterns), not only legacy `WritingScenario`. See
> `docs/sprints/2026-06-12-adaptive-learning-foundation-sprint.md`.

- [x] Add `StudentVocabularyItem` entity, EF config, and migration. `Done`
- [x] Add `vocabulary_extract_from_attempt` AI prompt to `DefaultAiSeeder`. `Done`
- [x] Add `VocabularyExtractionService` (best-effort, post-submit). `Done`
- [x] Wire extraction into `ActivitySubmitHandler` for legacy writing attempts and
      pattern-evaluated activities (`HandlePatternEvaluationAsync`, gated on
      `Corrections.Count > 0`). `Done` (2026-06-12)
- [ ] Add `GET /api/vocabulary` and `PATCH /api/vocabulary/{id}/status` endpoints. `Planned`
- [ ] Add Angular `/vocabulary` page with summary cards, filters, and status buttons. `Planned`
- [ ] Add vocabulary preview section to progress page. `Planned`
- [ ] Add vocabulary nav item to sidebar. `Planned`
- [ ] Add backend integration tests for vocabulary endpoints and extraction. `Planned`
- [ ] Add frontend unit tests and Playwright tests for vocabulary page. `Planned`

---

## Real progress page (in sprint: real-progress-page-sprint)

- [ ] Add `GET /api/progress` endpoint returning summary stats, score trend, skill profile, module progress, learning focus. `Planned`
- [ ] Replace placeholder progress component with real data-driven UI. `Planned`
- [ ] Add backend integration tests for `/api/progress`. `Planned`
- [ ] Add frontend unit tests for progress component. `Planned`
- [ ] Add Playwright tests for progress page (desktop + mobile). `Planned`

---

## Live AI quality review (in sprint: live-ai-quality-review-prompt-calibration-sprint)

- [x] Document live AI quality review sprint plan. `Done`
  - Created `docs/sprints/live-ai-quality-review-prompt-calibration-sprint.md`
- [x] Add live AI quality review report template. `Done`
  - Created `docs/testing/live-ai-quality-review-report.md`
- [x] Add synthetic answer fixtures for manual prompt review. `Done`
  - Created `docs/testing/live-ai-quality-fixtures.md`
  - Fixtures cover direct tone, long unclear answer, missing articles/tense issue, overly casual customer reply, and improved second attempt
- [ ] Run live AI review for Project Planner persona. `Blocked`
  - Requires staging/production access and configured live AI provider credentials
  - Use Project Planner career if seeded; otherwise document Document Controller proxy run
- [ ] Run live AI review for Customer Support Officer persona. `Blocked`
  - Requires staging/production access and configured live AI provider credentials
  - Use Customer Support Officer career if seeded; otherwise document proxy career used
- [ ] Calibrate `learning_path_generate` prompt only if live path evidence shows generic, repetitive, wrong-level, or career-mismatched modules. `Not started`
- [ ] Calibrate `activity_generate_writing` prompt only if live activities lack audience/tone/length clarity or repeat task types. `Not started`
- [ ] Calibrate `activity_evaluate_writing` prompt only if live feedback misses important issues, overwhelms learners, or frames improved text as the answer. `Not started`
- [ ] Calibrate `student_memory_update` prompt only if live memory is noisy, exaggerated, bloated, or drifts after minimal evidence. `Not started`
- [ ] Calibrate `learning_path_generate_adaptive` prompt only if live adaptive modules ignore memory, repeat fingerprints, or generate a generic full path. `Not started`

---

## End-to-end product validation (in sprint: end-to-end-product-validation-learning-quality-sprint)

- [x] Document validation sprint plan and quality criteria. `Done`
  - Created `docs/sprints/end-to-end-product-validation-learning-quality-sprint.md`
- [x] Add UI QA validation report for the writing-learning loop. `Done`
  - Created `docs/testing/e2e-learning-journey-validation-report.md`
- [x] Extend Playwright full-flow coverage through retry, history, memory, module completion, and adaptive generation. `Done`
- [x] Add adaptive module guardrail tests for reason, focusSkill, difficulty, and fingerprint persistence. `Done`
- [x] Add duplicate adaptive fingerprint rejection test. `Done`
- [ ] Seed additional pilot career profiles for validation personas. `Not started`
  - Project Planner
  - Customer Support Officer
  - Deferred because current seed model is reference data, not demo users, and the admin-created student flow must remain part of validation
- [ ] Run live AI quality review with the two validation personas in staging/production. `Not started`
  - Record repetitive activities, generic feedback, noisy memory, or weak adaptive module reasons before changing prompts

---

## Student learning memory (in sprint: student-learning-memory-adaptive-curriculum-sprint)

- [x] Extend `UserLearningSummary` with 6 new JSON fields. `Planned`
  - `journey_summary`, `strong_skills_json`, `weak_skills_json`,
    `recurring_mistakes_json`, `covered_scenarios_json`, `next_focus_json`
  - Domain method `ApplyDelta(MemoryUpdateDeltaDto)` enforces list caps
- [x] Add `StudentSkillProfile` table (skill_key, is_weak per student). `Planned`
  - 10 skill keys: grammar_accuracy, formal_tone, sentence_clarity, message_structure,
    workplace_vocabulary, concise_writing, softening_language, summarising_information,
    clarifying_questions, escalation_language
- [x] Add `fingerprint_json` column to `LearningModule`. `Planned`
  - Fields: communicationMode, scenarioType, audience, tone, difficulty, grammarFocus, vocabularyTheme
- [x] Add xmin concurrency token to `LearningPath`. `Planned`
- [x] Extract `AiExecutionService` shared fallback pattern. `Planned`
- [x] Extract `LearningPathDtoBuilder` shared DTO builder. `Planned`
- [x] Add `student_memory_update` AI prompt to seed data. `Planned`
- [x] Implement `StudentMemoryService` with best-effort update. `Planned`
- [x] Wire memory update into `ActivitySubmitHandler` (8s timeout). `Planned`
- [x] Add `learning_path_generate_adaptive` AI prompt. `Planned`
- [x] Implement `AdaptivePathGeneratorHandler`. `Planned`
- [x] `POST /api/learning-path/generate-next` endpoint. `Planned`
- [x] `GET /api/learning-path/memory` endpoint. `Planned`
- [x] `GET /api/admin/students/{id}/learning-memory` endpoint. `Planned`
- [x] "Your learning focus" panel on dashboard / /my-path. `Planned`
- [x] Module card enrichment (focusSkill, reason, difficulty). `Planned`
- [x] "Generate next modules" button with loading / error states. `Planned`
- [ ] Add staleness flag / alert when memory update fails repeatedly. `Not started`
  - If `UserLearningSummary.UpdatedAt` is > 7 days old and student has recent attempts,
    surface an admin alert or background refresh attempt
- [ ] Add numeric skill score tracking (0â€“100) to `StudentSkillProfile`. `Not started`
  - Deferred from current sprint â€” add after validating the is_weak approach
- [ ] Admin curriculum map editor. `Not started`
  - Currently curriculum is seeded/static for Workplace Writing B1/B1+/B2
  - Future: admin can add career-specific curriculum maps
- [ ] Move memory update to background job. `Not started`
  - Currently synchronous with 8-second timeout in ActivitySubmitHandler
  - When student volume grows, move to LinguaCoach.Worker queue

---

## Progress and activity tracking

- [ ] Implement real practice streak tracking. `Not started`
  - Persist a streak counter per student in the database
  - Increment on each day a new ActivityAttempt is submitted
  - Reset if a calendar day is skipped
- [ ] Track minutes practised this week. `Not started`
  - Derive from ActivityAttempt.CreatedAt timestamps
  - Approximate based on activity type (e.g. WritingScenario â‰ˆ 8 min)
- [ ] Track total activities completed per student. `Not started`
  - Count of `ActivityAttempt` rows per `StudentProfileId`
- [ ] Replace dashboard stat tile placeholders with real backend data. `Not started`
  - Dashboard API (`GET /api/dashboard`) must return: `streakDays`, `minutesThisWeek`, `activitiesDone`
  - Remove `â€”` placeholders once endpoint delivers values
- [ ] Add progress history data for the Progress page. `Not started`
  - `GET /api/progress` or extend dashboard endpoint
  - Return recent ActivityAttempts with score, date, activity type
- [ ] Add per-skill progress values. `Not started`
  - Return progress percentage for: Writing, Speaking, Listening, Vocabulary, Pronunciation
  - Implemented activity types: WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay
  - Pronunciation is not yet implemented; return `null` â€” UI shows "Not started"
  - UI must never fake data

---

## Coach insights

- [ ] Store and retrieve latest AI coach feedback summaries. `Not started`
  - After each ActivityAttempt, persist `whatYouDidWell`, `mainMistakes`, `toneExplanation` to a `CoachInsight` or similar table
  - Retrieve most recent N coach messages per student
- [ ] Show "Latest from your coach" using real recent ActivityAttempt feedback. `Not started`
  - Dashboard coach card currently shows placeholder text
  - Replace with last feedback `feedbackSummary` or first item of `whatYouDidWell`
  - Include score, activity title, and timestamp
- [ ] Show latest completed activity with score and tone summary. `Not started`
  - The completed activity card inside the coach card is currently a placeholder
  - Bind to the most recent `ActivityAttempt` for the student
- [ ] Add coaching trend insights over time. `Not started`
  - Future: "Your tone is improving" / "Grammar errors decreasing" type summaries
  - Deferred until sufficient attempt history exists (suggest 5+ attempts minimum)

---

## Streak system

- [ ] Add daily practice streak persistence. `Not started`
  - Store `LastPracticeDate` and `CurrentStreak` on `StudentProfile` or a separate `StreakRecord` table
  - Increment on new ActivityAttempt, reset if gap > 1 calendar day
- [ ] Add weekly streak calendar data to the API. `Not started`
  - Return an array of 7 booleans (`[M, T, W, T, F, S, S]`) for the current ISO week
  - Dashboard streak calendar will render filled dots for `true` days
- [ ] Add streak reset/continuation logic. `Not started`
  - Grace period: streak continues if the student practises before midnight of the missed day's timezone
  - Timezone must be stored with the student profile
- [ ] Replace empty streak calendar placeholder with real data. `Not started`
  - Currently all 7 dots are empty with "Coming soon" caption
  - Remove caption and bind to real weekly data once available

---

## Future activity types

- [x] Implement SpeakingRolePlay activity type. `Done`
  - Backend: ActivityType value, prompt templates, AI handler, audio upload endpoint, fake STT
  - Frontend: recording UI, transcript, feedback view, history support
  - Dashboard Speaking card active â€” routes to `/activity?type=SpeakingRolePlay`
  - See sprint: speaking-role-play-mvp-sprint.md
- [x] Implement ListeningComprehension text MVP activity type. `Done`
  - Backend: hidden transcript generation, comprehension questions, deterministic scoring
  - Frontend: text-based listening task with transcript reveal after submit
  - Real audio/TTS remains deferred
- [x] Implement VocabularyPractice activity type. `Done`
  - Backend: deterministic practice generation and evaluation
  - Frontend: vocabulary practice rendering, submission, and history support
  - Dashboard links to the implemented vocabulary experience
- [ ] Implement PronunciationPractice activity type. `Not started`
  - Backend: target word/sentence selection, pronunciation scoring via AI or speech API
  - Frontend: microphone UI, waveform or score display
  - Keep Pronunciation card as "Coming soon" until fully implemented
- [ ] Implement ReadingTask activity type. `Not started`
  - Backend: workplace text generation, comprehension questions
  - Frontend: reading + Q&A layout
  - Keep Reading card (if surfaced in UI) as "Coming soon" until implemented
- [ ] Keep unimplemented skill cards (Pronunciation, Reading) visually present but disabled. `Planned`
  - Writing, Listening, Vocabulary, Speaking are implemented and active
  - Pronunciation and Reading remain "Coming soon"
  - Remove "Coming soon" label only when the backend feature is fully wired

---

## Profile page

- [ ] Replace placeholder profile rows with real user/profile data. `Not started`
  - Learning goal: read from `StudentProfile.LearningGoal` or `LearningTrack.Name`
  - Current level: read from `PlacementResult.estimatedOverallLevel` (source of truth); fall back to `StudentProfile.CefrLevel` only if placement not yet completed
  - Practising: read from `LanguagePair.TargetName` + skill focus
  - Career context: read from `CareerProfile.Name`
- [ ] Add editable learning preferences if needed. `Not started`
  - Deferred â€” read-only display is sufficient for pilot phase
- [ ] Add language pair and career context display. `Not started`
  - Show "Persian â†’ English Â· Document Controller" or equivalent
  - Read from `StudentProfile` joined to `LanguagePair` and `CareerProfile`
- [ ] Add account/security links if needed. `Not started`
  - Change password link at minimum
  - Privacy / data deletion for compliance if required later

---

## Progress page

- [ ] Replace placeholder stat tiles with real ActivityAttempt summaries. `Not started`
  - Day streak: from streak system (see Streak system section)
  - Activities done: count of `ActivityAttempt` rows
  - Avg score: mean of `ActivityAttempt.Score` values (exclude null)
- [ ] Add skill progress bars with real values. `Not started`
  - Writing progress: derive from number of completed Writing activities vs. module target
  - Other skills: return `0` or `null` with "Not started" label until implemented
- [ ] Add module completion history. `Not started`
  - Show completed modules with completion date
  - Show in-progress module with current activity count
- [ ] Add recent scores list with improvement trend. `Not started`
  - Show last 5â€“10 ActivityAttempts: title, score, skill badge, date
  - Optionally show trend arrow (improving / stable / needs work) if 3+ results available

---

## My Path improvements

- [ ] Add richer module details from real progress data. `Not started`
  - `isCurrent`, `completedActivities`, `totalActivities` already returned by API
  - Ensure `moduleStatus()` in `LearningPathComponent` reflects real data correctly
- [ ] Add ability to tap a module card to view its activities. `Not started`
  - Currently module cards link to `/activity` generically
  - Future: `/my-path/modules/:moduleId` showing the activity list for that module
- [ ] Add path regeneration or adjustment capability. `Not started`
  - Admin or AI trigger to regenerate path based on progress
  - Not needed for pilot phase â€” defer

---

## Design system follow-ups

- [x] Extract repeated app shell into layout components. `Done`
  - Shell HTML removed from all page components
  - `StudentAppLayoutComponent` owns sidebar, header, bottom nav
  - `AdminAppLayoutComponent` owns left sidebar, header
  - `PublicLayoutComponent` owns centered background wrapper
  - Pages render content only â€” no shell duplication remains
- [ ] Extract `StatCard` component. `Planned`
  - Repeated 3Ã— on dashboard and progress page
  - Input: `icon`, `value`, `label`, `color`, `bg`
- [ ] Extract `SkillCard` component. `Planned`
  - Repeated on dashboard and progress page
  - Input: `skill`, `level`, `pct`, `active`, `routerLink`
- [ ] Extract `ModuleCard` component. `Planned`
  - Used on My Path page; candidate for Activity selection page later
- [ ] Extract `CoachCard` / `CoachMessage` component. `Not started`
  - Used on dashboard right column and feedback phase
- [ ] Extract `ScoreRing` component. `Not started`
  - SVG ring used in My Path header and feedback phase
  - Input: `value`, `size`, `stroke`, `color`
- [ ] Keep `sp-*` utility classes documented in `speakpath-design-system.md`. `Planned`
  - Update whenever new classes are added to `styles.css`
- [ ] Add screenshot/visual reference notes for future agents. `Not started`
  - Annotated screenshots of prototype screens in `docs/design/references/`
  - Reference prototype JSX files when making UI decisions

---

## Admin dashboard â€” real data

- [ ] Replace KPI card placeholders with real counts. `Not started`
  - "Total students" already live from `GET /api/admin/students` count
  - "Onboarded" already live from same endpoint (filter by `onboardingStatus === 'Complete'`)
  - "Activities tracked": requires `GET /api/admin/stats` endpoint returning `totalActivityAttempts`
  - "AI provider": hardcoded "Configured" â€” wire to check if at least one provider has a non-null API key
- [ ] Implement real usage analytics. `Not started`
  - AI token usage per provider per day: log `promptTokens` + `completionTokens` from AI responses
  - Cost estimate per student: requires provider pricing table in DB
  - Expose via `GET /api/admin/usage?from=&to=`
- [ ] Implement activity completion trends. `Not started`
  - `GET /api/admin/analytics/activity-completion` returning daily counts over a date range
  - Chart on admin analytics page once data exists
- [ ] Implement feedback quality review. `Not started`
  - Score distribution histogram across all ActivityAttempts
  - Average score per skill type, per career profile
  - Flag unusually low-quality feedback (score = null more than X% of attempts)
- [ ] Add system health / API health card to admin dashboard. `Not started`
  - Real-time ping to each configured AI provider (or display last test result from ai-config)
  - Show green/amber/red per provider
  - Do not fake â€” only show if a recent test result is stored
- [ ] Build admin settings page. `Not started`
  - Route: `/admin/settings`
  - Planned content: platform name/branding config, pilot programme dates, allowed email domains
  - Currently a placeholder / disabled nav item
- [x] Improve admin student list page. `Done` (2026-06-14)
  - [x] Add search/filter by email and name
  - [x] Add sort by name, onboarding status, or joined date
  - [x] Add pagination (25 per page)
  - [x] Add ability to view individual student (detail page with learning memory). `Done` (2026-06-14)
  - [x] Activity history view. `Done` (2026-06-14)
- [x] Add admin student learning memory view. `Done` (2026-06-14)
  - `GET /api/admin/students/{id}/learning-memory` now consumed by `/admin/students/:id`
  - Shows journey summary, strengths, weaknesses, recurring mistakes, next focus, covered scenario count, skill profile
- [x] Design system: use `sp-admin-*` classes consistently across all admin components. `Done` (2026-06-14)
  - `admin-prompts` and `admin-careers` migrated from raw Tailwind to sp-admin-* (sp-admin-table, sp-admin-form-card, sp-admin-field-grid, sp-input, sp-admin-btn-primary, sp-admin-badge)
- [x] Improve admin mobile drawer. `Done` (2026-06-14)
  - Added swipe-to-close gesture
  - Added route-change auto-close (NavigationStart subscription in AdminAppLayoutComponent)
  - Added keyboard Escape to close drawer and profile menu
- [ ] Add admin mobile bottom navigation or persistent tab bar as alternative to drawer. `Not started`
  - Drawer pattern is sufficient for a desktop-first admin tool used on mobile rarely
  - If admin mobile usage is significant, consider a simplified bottom tab bar for admin

---

## Learning path progression (post Learning Path Progression sprint)

- [ ] Persist explicit module completion confirmation to a dedicated `ModuleCompletion` table instead of `CompletedAt` column, to support future multi-path learners. `Not started`
- [ ] Show per-module score history chart (last 5 attempts per module). `Not started`
- [ ] Module completion certificates or achievement badges. `Not started`
- [ ] Admin / teacher progress view per student (current module, focus area, average score). `Not started`
- [ ] Long-term trend chart: score progression over 10+ attempts. `Not started`
- [ ] Spaced repetition for repeated mistakes (re-surface activities in weak categories). `Not started`
- [ ] Auto-advance module when ready without explicit student confirmation (optional preference). `Not started`
- [ ] Notify student when module is ready to complete (push notification or dashboard badge). `Not started`

---

## Learning experience improvements (post Learning Experience sprint)

- [ ] Richer attempt history page showing all attempts side by side. `Not started`
  - Route: `/activity/history/:activityId`
  - Show all `ActivityAttempt` rows for a given activity, ordered by date
  - Each row: attempt number, score, date, first few words of submission
- [ ] Side-by-side diff viewer for attempts. `Not started`
  - Compare attempt N with attempt N-1 visually
  - Highlight what changed between submissions
  - Deferred â€” requires richer attempt history page first
- [ ] Inline sentence-level comment annotations. `Not started`
  - AI returns comments anchored to specific sentence positions
  - UI renders inline margin annotations (like Google Docs)
  - Requires new AI prompt output format
- [ ] Teacher / admin review of AI feedback quality. `Not started`
  - Admin can browse recent `ActivityAttempt` feedback JSONs
  - Admin can flag poor feedback for prompt review
  - Requires admin UI extension
- [ ] Skill-based progress analytics from `changes.category` data. `Not started`
  - Aggregate `changes.category` values from recent attempts per student
  - Show: "Grammar is your most common issue this week"
  - Requires data aggregation query on `FeedbackJson` or new field on `ActivityAttempt`
- [ ] Vocabulary extraction from writing attempt mistakes. `Not started`
  - Extract vocabulary from `vocabularyIssues` and `changes` with category=vocabulary
  - Add to student's vocabulary list for spaced repetition
  - Requires vocabulary tracking feature
- [ ] Speaking and listening activity types. `Not started`
  - See future activity types section above
- [ ] Client-side LCS-based visual diff for richer comparison. `Not started`
  - Compute diff between student's draft and improved version in the browser
  - Highlight word-level insertions and deletions
  - Lower priority â€” server-side `changes` list already covers this

---

## Legacy database cleanup

> âš ï¸ These items require explicit confirmation before execution. Do not run without a backup.

- [ ] T19: Confirm no active FK dependency on `SourceWritingScenarioId` in `LearningActivity`. `Planned`
  - Verify all current activities use `aiGeneratedContentJson` not `SourceWritingScenarioId`
  - Query: `SELECT COUNT(*) FROM learning_activities WHERE source_writing_scenario_id IS NOT NULL`
- [ ] Backfill any required legacy scenario content into `aiGeneratedContentJson`. `Blocked`
  - Blocked on T19 confirmation above
- [ ] Export and back up `writing_scenarios` and `writing_submissions` tables. `Not started`
  - Export to CSV or S3 before any schema changes
- [ ] Remove `SourceWritingScenarioId` FK from `LearningActivity`. `Not started`
  - EF Core migration: drop column and FK constraint
  - Remove `WritingScenario` domain entity and all references
- [ ] Drop legacy `writing_scenarios` and `writing_submissions` tables. `Not started`
  - Only after backup confirmed and no active references
  - Requires explicit confirmation from user before execution
