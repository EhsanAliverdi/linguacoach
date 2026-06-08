# Course Session & Placement Redesign Sprint

**Status:** Design complete — ready for phased implementation  
**Date:** 2026-06-09  
**Type:** Planning / Architecture sprint (no large code changes)

---

## Sprint Goal

Redesign SpeakPath from an "activity generator" into an **AI-powered workplace English class platform**.

The app must feel like a structured English class — with placement, lessons, mixed-skill exercises, and guided progression — not a collection of isolated practice cards.

---

## Problem Statement

Manual testing after the SpeakingRolePlay MVP revealed a fundamental product issue:

- Speaking task says "Explain a delay to your manager" — useful as one exercise, but not a full lesson
- Vocabulary practice is disconnected unless the student already has saved vocabulary
- Listening, speaking, writing, and vocab are treated as isolated categories
- There is no real placement assessment
- Onboarding does not properly identify the student's level
- The app does not replicate the structure of a real English class

Current state when this sprint started:

```
dotnet test:        437 passed
npm run build:      passed
Playwright:         56 passed
```

---

## What SpeakPath Becomes

```
An AI-powered workplace English class platform.
```

Not:
- A random AI exercise generator
- A grammar checker
- A writing correction app
- A set of isolated practice cards

The app replicates and eventually replaces a real English class by providing:
- Placement assessment
- Structured lessons with a teaching sequence
- Mixed-skill exercises (listening + gap fill + vocabulary in one step)
- Guided practice
- Productive workplace tasks
- AI teacher feedback
- Review and repetition
- Adaptive next lessons
- On-demand Practice Gym

---

## Current State

- `ActivityType` enum: WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay
- Activity-card dashboard model
- No placement assessment
- No session structure
- No lesson teaching sequence
- No lifecycle stages
- Local filesystem audio (not abstracted)

---

## Architecture Decisions Made

### 1. New learning model layer

```
LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity → ActivityAttempt
```

See: [course-session-learning-model.md](../architecture/course-session-learning-model.md)

### 2. ExercisePattern concept

Patterns define teaching structure (not content). Content is generated per-instance.

Library includes 30+ named patterns across: input, vocabulary, grammar, workplace communication, speaking, reflection.

See: [exercise-pattern-library.md](../architecture/exercise-pattern-library.md)

### 3. Placement Assessment as first module

- Two-stage onboarding: basic preferences → placement
- 6 sections covering all core skills
- AI produces structured `PlacementResult` JSON
- Feeds `StudentSkillProfile` and `UserLearningSummary`
- Not skippable

See: [placement-assessment-model.md](../architecture/placement-assessment-model.md)

### 4. Session duration controlled by student preference

Options: 10 / 15 / 20 / 30 minutes.  
Each maps to a different number and type of exercises.

### 5. TeamsChatSimulation as priority pattern

Short, realistic Teams-style chat scenario. Evaluates tone, conciseness, phrase use.  
Works in both guided lesson and Practice Gym.

### 6. IFileStorageService abstraction

- `LocalFileStorageService` for dev
- `MinioFileStorageService` for production
- Frontend never receives raw file paths

See: [file-storage-minio.md](../architecture/file-storage-minio.md)

### 7. Student lifecycle stages and admin reset tools

13 lifecycle stages from Created to Archived.  
Admin reset endpoint with clear-option matrix and permanent audit log.

See: [student-lifecycle-reset-tools.md](../architecture/student-lifecycle-reset-tools.md)

### 8. Practice Gym separation

Existing `/activity?type=...` route becomes the Practice Gym.  
Guided course (Today / My Course) is the new primary experience.

See: [practice-gym.md](../architecture/practice-gym.md)

### 9. Professional Experience Level & Domain Complexity

Two independent difficulty dimensions for every session and exercise:

- `LanguageDifficulty` — CEFR level from placement
- `DomainComplexity` — workplace/professional complexity from onboarding (experience level + role familiarity)

Students with the same CEFR level but different professional backgrounds receive different workplace scenario complexity. A junior professional is not exposed to senior-level domain concepts (stakeholder negotiation, production incidents, technical trade-offs) until those concepts have been introduced via a micro lesson.

New onboarding fields: `ProfessionalExperienceLevel`, `RoleFamiliarity`.  
New computed field on StudentProfile: `WorkplaceSeniority` (effective DomainComplexity).  
All AI content generation prompts must include both `{{CEFRLevel}}` and `{{DomainComplexity}}`.

See: [professional-experience-domain-complexity.md](../architecture/professional-experience-domain-complexity.md)

---

## In Scope (this sprint)

- Architecture documentation only
- No new database migrations
- No new API endpoints
- No frontend changes
- No new activity types
- No pronunciation work

---

## Out of Scope

- All implementation (deferred to Phases 1–6)
- Pronunciation
- Real STT provider
- Real TTS provider
- Payments
- Public registration

---

## Implementation Phases

### Phase 1 — Placement Assessment MVP

- `StudentLifecycleStage` field on `StudentProfile`
- Onboarding: add preferred session duration, professional experience level, role familiarity
- `PlacementAssessment`, `PlacementSection` entities and migrations (standalone, not a LearningModule)
- 6-section placement flow (backend + frontend)
- AI placement result generation (`placement_assessment_evaluate` prompt)
- Feeds `StudentSkillProfile` and `UserLearningSummary`
- Generates first `LearningPath` after placement completes
- Lifecycle transitions: OnboardingRequired → PlacementRequired → PlacementInProgress → PlacementCompleted → CourseReady
- Admin: view student lifecycle stage

### Phase 2 — Course Session MVP

- `LearningSession` and `SessionExercise` entities and migrations
- Session generator (backend, not AI-driven)
- Today page (replaces activity-card dashboard)
- Generate 10/15/20/30 minute sessions
- Basic teaching sequence (input → practice → productive task → reflection)
- Session progress tracking

### Phase 3 — Exercise Pattern Engine

- Exercise pattern library in code/config
- Session generator selects patterns based on duration, level, memory, career
- Pattern-to-activity mapping (links SessionExercise to LearningActivity)
- TeamsChatSimulation pattern fully implemented
- `read_and_answer`, `gap_fill_with_workplace_phrase`, `phrase_match`, `collocation_match`

### Phase 4 — Practice Gym

- Dashboard activity cards moved under Practice tab
- Today page becomes the primary student entry point
- Practice tab navigation added
- Existing `/activity?type=...` routing unchanged

### Phase 5 — MinIO File Storage

- `IFileStorageService` interface
- `LocalFileStorageService` implementation
- `MinioFileStorageService` implementation
- Migrate `ListeningAudioService` and `SpeakingAudioService` to use abstraction
- Update streaming endpoints
- MinIO in Docker Compose (staging)
- Verify audio playback end-to-end

### Phase 6 — Admin Reset Tools

- `StudentLifecycleStage` transitions enforced
- `StudentResetLog` table and migration
- `POST /api/admin/students/{id}/reset` endpoint
- Audio file cleanup via `IFileStorageService`
- Admin UI: lifecycle stage display and reset modal

---

## API Changes (future phases)

| Phase | Endpoint | Notes |
|---|---|---|
| 1 | `POST /api/placement/start` | Begin placement assessment |
| 1 | `POST /api/placement/sections/{key}/submit` | Submit one section |
| 1 | `GET /api/placement/result` | Get placement result |
| 2 | `GET /api/session/today` | Get today's lesson |
| 2 | `POST /api/session/{id}/start` | Start a session |
| 2 | `POST /api/session/{id}/exercises/{eid}/complete` | Mark exercise complete |
| 6 | `POST /api/admin/students/{id}/reset` | Admin lifecycle reset |

---

## DB Changes (future phases)

| Phase | Table | Notes |
|---|---|---|
| 1 | `student_profiles` | Add `lifecycle_stage`, `preferred_session_duration_minutes`, `professional_experience_level`, `role_familiarity`, `workplace_seniority` |
| 1 | `placement_assessments` | New table (standalone — not tied to LearningModule) |
| 1 | `placement_sections` | New table |
| 2 | `learning_sessions` | New table |
| 2 | `session_exercises` | New table |
| 6 | `student_reset_logs` | New table |

---

## Frontend Changes (future phases)

| Phase | Change |
|---|---|
| 1 | Onboarding: add session duration preference, professional experience level, and role familiarity steps |
| 1 | Placement flow: 6-section placement UI |
| 1 | Lifecycle-aware routing guard |
| 2 | Today page (new primary student home) |
| 2 | Session progress component |
| 4 | Practice tab in student navigation |
| 6 | Admin student detail: lifecycle + reset modal |

---

## Test Plan

No code changes in this sprint → no test run required.

Each future implementation phase must pass:
- `dotnet test` (all tests)
- `npm run build`
- `npx playwright test`

New tests required per phase:
- Phase 1: placement API integration tests, placement flow Playwright tests
- Phase 2: session generation tests, Today page Playwright tests
- Phase 5: file storage service unit tests, audio endpoint integration tests
- Phase 6: reset endpoint integration tests, audit log verification

---

## Risks

| Risk | Mitigation |
|---|---|
| Session layer complexity increases migration risk | Additive tables only; existing model unchanged |
| Placement takes longer than 15–20 minutes for students | Calibrate with real users; allow continuation from last section |
| MinIO adds infrastructure ops burden | Local fallback always available; MinIO optional in dev |
| Admin reset can destroy data accidentally | Explicit confirmation required; reset log is permanent |
| Exercise pattern library grows unmanaged | Code/config defined, versioned in repo |

---

## Unresolved Questions

1. Should `LearningSession` and `LearningModule` have a 1:many or many:many relationship? (Current design: one module contains many sessions — simpler for now)
2. Should placement be resumable across browser sessions without expiry? (Proposed: yes, no expiry)
3. Should students be able to access Practice Gym before completing placement? (Proposed: yes, with a banner prompting them to complete placement)
4. How long to keep TTS audio files for completed listening activities? (Proposed: background cleanup job, Phase 5 backlog)
5. Should `TeamsChatSimulation` be a new `ActivityType` or just an `ExercisePattern`? (Proposed: ExercisePattern first; promote to ActivityType if Practice Gym needs standalone access)

---

## Recommended First Implementation Sprint

**Phase 1: Placement Assessment MVP**

Rationale:
- Placement unblocks everything else: the guided course needs a starting CEFR level
- It is fully self-contained (new tables, new prompt, new flow, no disruption to existing features)
- It makes the product feel dramatically more real — the first experience after onboarding is a proper assessment, not random activity cards
- Once placement works, Phase 2 (Today page and sessions) can use the placement result immediately

---

## Competitive Feature Gap Review

SpeakPath's target product compared against the strongest features from current AI language-learning apps.

Inspiration sources: Langua, Speak, Praktika, ELSA Speak, TalkPal.

Important: these are used as product and pedagogical inspiration only. No proprietary content, lesson scripts, branding, or UI is copied.

---

### Product Positioning

SpeakPath should combine:

- the **structure** of a real English course
- the **personalisation** of AI memory
- the **realism** of workplace simulations
- the **convenience** of on-demand AI chat/call practice
- the **feedback depth** of a pronunciation and writing coach

Its main differentiator:

> **Workplace-specific English lessons that adapt to the learner's real mistakes and daily professional goals.**

---

### Feature Gap Matrix

| Feature Area | Competitor Inspiration | Current SpeakPath | Gap | Proposed SpeakPath Version | Priority |
|---|---|---|---|---|---|
| Structured class experience | Speak: guided lesson path, session objectives, warm-up→production flow | Activity cards only, no lesson sequence | No teaching sequence, no Today page, no session structure | `LearningSession` + `SessionExercise` + Today page with lesson objectives and step progress | **P0** |
| Placement assessment | Speak: beginner flow, level detection; ELSA: fluency baseline | Onboarding collects self-reported CEFR only | No real level measurement before first course | 6-section workplace placement covering all skills; AI-produced per-skill CEFR result | **P0** |
| Mixed-skill exercise patterns | Langua: drills from mistakes; Speak: interactive drills combining input + output | Single-skill isolated activity types | No lesson step can combine listening + gap fill + vocabulary | `ExercisePattern` library: `listen_and_gap_fill`, `read_and_reply`, `collocation_match`, etc. | **P0** |
| Student lifecycle management | Speak: clear onboarding gate; Praktika: organised lesson paths | No lifecycle stages; no placement gate | Students can reach the dashboard without a proper level | `StudentLifecycleStage` with gates: OnboardingRequired → PlacementRequired → CourseReady | **P0** |
| Admin reset / pilot support | (internal need) | No reset tools | Cannot re-test flows without manual DB intervention | Admin reset endpoint with data clear options and audit log | **P0** |
| Call Mode / open AI speaking | Langua: AI conversation + hands-free call; TalkPal: open-ended voice/text roleplay | SpeakingRolePlay MVP (single turn, recorded) | No multi-turn, no AI-first speaking, no post-call summary | Phone-style AI call: AI speaks first, student replies, multi-turn, post-call transcript + feedback | **P2** |
| Pronunciation engine | ELSA Speak: phoneme-level correction, word stress, intonation, fluency metrics | No pronunciation; SpeakingRolePlay evaluates clarity only | No phoneme feedback, no accent/stress/intonation | Pronunciation section: problem words, repeat-after-me, phoneme/stress/intonation feedback (provider-dependent) | **P2** |
| Vocabulary queue cards | Langua: flashcards + saved vocab; Speak: drills from mistakes | VocabularyPractice fill-blank exists; no card/queue UI | No flashcard mode, no collocation cards, no scheduling | Vocabulary queue: cloze cards, collocation cards, phrase cards, weak/new/mastered scheduling | **P1** |
| Micro lessons (teach before practise) | Speak: video/micro lessons before drills; Praktika: organised lesson paths with input before output | No input/teaching moment before activities | Students practise without any instruction or phrase introduction | Micro lesson text at session start: target phrases, common mistakes, example dialogues; AI teacher text/audio first | **P1** |
| AI tutor persona | Praktika: AI avatar + visual tutor; Langua: human-like AI | No persona | No consistent teacher voice or identity | Named AI teacher with encouraging tutor style; text first, audio persona later, avatar is P3 | **P2** |
| Teams Chat simulation | TalkPal: multiple conversation modes; Langua: conversation history | WritingScenario covers email; no chat simulation | No short-form professional chat mode | `teams_chat_simulation` pattern: turn-based Teams-style reply, tone + conciseness scoring | **P1** |
| Post-conversation feedback | TalkPal: post-conversation correction; Langua: conversation history | Per-attempt AI feedback exists for writing | No session-level summary, no post-call review | Session-end `lesson_reflection` exercise: AI coach summary, best moment, focus for next session | **P0** (in session model) |
| Weekly plan / daily habit | Speak: daily practice cadence; Praktika: daily practice | Streak placeholder only; no scheduled lesson plan | No weekly plan, no daily lesson nudge | Weekly session plan generated from placement result and duration preference; Today page as daily anchor | **P0** (in session model) |
| Multimodal workplace uploads | Praktika: photo/audio/document uploads for personalised learning | Not available | No upload-to-lesson capability | Future: upload workplace email/doc/screenshot → AI converts to exercise | **P3** |
| Professional experience + domain complexity | (all competitors miss this) | Career context collected but not used for complexity calibration | Linguistically appropriate tasks may be professionally inappropriate for junior students | Two-dimension difficulty: `LanguageDifficulty` (CEFR) + `DomainComplexity` (workplace experience); onboarding collects experience level + role familiarity; session generator respects both | **P0/P1** |
| File storage (production) | (infrastructure) | Local filesystem only | Not portable across containers; no MinIO/S3 | `IFileStorageService`: Local (dev) + MinIO (production); backend streams, frontend never sees paths | **P1** |
| Real STT provider | Langua: voice recognition; ELSA: accurate phoneme detection | Fake STT only (deterministic placeholder transcript) | SpeakingRolePlay transcript is not real | Wire OpenAI Whisper or Azure STT; evaluate on real recorded audio | **P2** |
| Real TTS provider | Praktika: immersive audio; Langua: human-like AI voice | Fake TTS (WAV placeholder) | Listening audio is not real speech | Wire OpenAI TTS, Azure, or Google TTS; use for listening exercises and future teacher persona | **P2** |
| Interview practice mode | TalkPal: roleplay modes; Langua: roleplay | Not available | No job interview or formal meeting simulation | Practice Gym mode: Interview Practice (multi-turn AI roleplay, structured feedback) | **P2** |
| Reading tasks | (general) | Not started | No reading comprehension | `read_and_answer`, `read_and_summarise`, `read_teams_thread_and_identify_tone` patterns | **P1** (in pattern engine) |

---

### Priority Summary

**P0 — Must have before launch (blocks core product value)**

- Placement assessment (without it, courses are unanchored)
- Guided sessions / Today's Lesson (without it, the app is still just activity cards)
- Exercise pattern library and mixed-skill lesson generation
- Session-end lesson reflection
- Weekly plan anchored to session duration preference
- Student lifecycle stages and gates
- Admin lifecycle reset tools

**P1 — High value, implement in next phases**

- Practice Gym separation (Today page as primary, Practice as secondary)
- TeamsChatSimulation pattern
- Vocabulary queue cards (cloze, collocation, phrase, scheduling)
- Micro lessons (teach → practise flow)
- MinIO file storage abstraction
- Reading patterns in exercise library

**P2 — Important differentiators, implement after core course is stable**

- Call Mode / open AI speaking (multi-turn, AI-first, post-call feedback)
- Pronunciation MVP (problem words, repeat-after-me, word stress)
- Real STT provider (Whisper or Azure)
- Real TTS provider (OpenAI or Azure)
- AI tutor persona / teacher voice
- Interview practice mode in Practice Gym

**P3 — Future vision, do not plan now**

- AI avatar / visual tutor
- Video micro lessons
- Multimodal workplace uploads (email/doc/screenshot → exercise)
- Advanced enterprise analytics
- Organisations / teams / employer accounts

---

### What SpeakPath Does Better than Competitors (to protect)

| Advantage | Description |
|---|---|
| Workplace-specific | All exercises use real workplace scenarios, not generic travel/social English |
| Career-aware | Career context (Document Controller, Project Planner, etc.) shapes all lesson content |
| Mistake memory | AI memory tracks recurring mistakes and avoids repeating the same scenario |
| Writing coach depth | Structured feedback with before/after, mini lesson, and retry loop |
| Admin control | Admin creates students, manages AI providers and prompt templates |
| Clean Architecture | Maintainable, testable codebase with clear layer boundaries |

These must not be weakened when adding new features.
