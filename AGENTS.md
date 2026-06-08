# SpeakPath / LinguaCoach — Agent Instructions

Read this file before making any changes to the repository.

The public product is **SpeakPath**.
The internal repo/project name is **LinguaCoach** (keep in .NET namespaces, migrations, infrastructure — do not rename unless asked).
User-facing UI, page titles, copy, and public docs should use **SpeakPath**.

---

# 1. Product Identity

SpeakPath is an **AI-powered workplace English class platform**.

It is **professional communication coaching software for immigrant professionals**.

First wedge:

```
Persian-speaking professionals in Australia improving workplace English.
First role-specific path: Document Controller / admin-document-control worker.
```

Core positioning: **Professional dignity software, not a generic language app.**

SpeakPath aims to combine:
- the **structure** of a real English course (guided sessions, placement, teaching sequence)
- the **personalisation** of AI memory (adapts to real mistakes and career goals)
- the **realism** of workplace simulations (emails, Teams chat, calls, meetings)
- the **convenience** of on-demand AI practice (Practice Gym, Call Mode later)
- the **feedback depth** of a writing and pronunciation coach

Main differentiator:
> **Workplace-specific English lessons that adapt to the learner's real mistakes and daily professional goals.**

The user is buying the ability to:

```
send workplace emails
speak in meetings
follow up professionally
explain delays clearly
ask for clarification politely
communicate at work without asking a colleague to fix every message
```

SpeakPath is **not**:

- only an email correction tool
- a generic grammar checker
- a static scenario generator
- a random AI exercise generator
- a collection of isolated activity cards

**The product must feel like a structured English class**, not a practice card generator.

Current implemented activity types:

- `WritingScenario` ✅
- `ListeningComprehension` ✅ (with TTS audio)
- `VocabularyPractice` ✅
- `SpeakingRolePlay` ✅ (MVP)

Architecture direction (course-session-placement-redesign-sprint):

- Placement assessment before first guided course
- Session-based lesson model (`LearningSession` → `SessionExercise`)
- Exercise patterns that combine multiple skills (not isolated activity types)
- Practice Gym for on-demand practice (secondary, not primary)
- `IFileStorageService` abstraction for all audio files
- Student lifecycle stages and admin reset tools

Long-term direction (do not build until sprint explicitly asks):

**P1:**
- TeamsChatSimulation pattern and Practice Gym entry
- Vocabulary queue cards (cloze, collocation, phrase, scheduling)
- Micro lessons (teach → practise flow, `micro_lesson_*` patterns)
- Weekly plan generation and Today page weekly calendar

**P2:**
- Call Mode (multi-turn AI-first voice conversation; requires real STT)
- Pronunciation MVP (problem words, repeat-after-me, word stress, intonation)
- Real STT provider (OpenAI Whisper, Azure Speech, or Google STT)
- Real TTS provider (OpenAI, Azure, or Google TTS)
- AI tutor persona / teacher voice

**P3:**
- AI avatar / visual tutor
- Video micro lessons
- Multimodal workplace uploads
- MinIO production file storage (Phase 5)

---

# 2. Product Promise

```
Practise the workplace message before you send it.
Sound clear, polite, and professional.
Build confidence for real work situations.
```

Good copy:

```
Practise the email before you send it.
Your next practice: follow up a pending document approval.
Learn the phrases you actually need at work.
Stop depending on colleagues to fix every message.
```

Avoid generic copy:

```
Learn English fast. AI-powered English learning.
Improve your grammar. Chat with your AI tutor.
Become fluent in 30 days.
```

---

# 3. Tech Stack

```
Backend:   .NET 10 Web API, ASP.NET Identity, JWT, EF Core, PostgreSQL
           Clean Architecture + Modular Monolith
Frontend:  Angular, Tailwind CSS, mobile-first
Infra:     Docker, Docker Compose, GitHub Actions, VPS (speakpath.app)
AI:        Provider abstraction, prompt templates in PostgreSQL,
           token budgets, AI usage logging, fallback providers
```

---

# 4. Core Architecture

## Project structure

```
src/LinguaCoach.Domain
src/LinguaCoach.Application
src/LinguaCoach.Infrastructure
src/LinguaCoach.Persistence
src/LinguaCoach.Api
src/LinguaCoach.Worker
src/LinguaCoach.Web
```

## Rules

- Domain must not depend on EF Core, ASP.NET Identity, Infrastructure, or API.
- Application defines interfaces and use cases.
- Infrastructure implements external providers.
- Persistence owns EF Core, Identity mapping, migrations, DB config.
- API owns controllers, middleware, auth wiring, CORS, health checks.
- Angular owns UI only.
- Do not put business logic in controllers.
- Do not put EF Core attributes in Domain entities.

## Core learning model

```
LearningPath
  → LearningModule
    → LearningSession        ← session-based lesson (new)
      → SessionExercise      ← one step in lesson (new)
        → LearningActivity   ← existing, reused
          → ActivityAttempt  ← existing, reused
```

Practice Gym uses `LearningActivity` directly without a session.

See: docs/architecture/course-session-learning-model.md

**Agents must not bring back the old writing-only architecture.**

Do not recreate:

- `WritingExerciseController`
- `/api/writing/*` endpoints
- old writing pages or writing service flow

`/activity` is the main student practice path.

Current `ActivityType` values:

- `WritingScenario` ✅ implemented
- `ListeningComprehension` ✅ implemented (with TTS audio)
- `VocabularyPractice` ✅ implemented
- `SpeakingRolePlay` ✅ implemented (MVP)

Future values (do not implement unless sprint asks):

- `PronunciationPractice`
- `ReadingTask`

Note: `ActivityType` values are implementation tools, not the product experience.  
The product experience is defined by `ExercisePattern` within `LearningSession`.  
See: docs/architecture/exercise-pattern-library.md

## Completed activity vs retry

A "completed activity" = at least one `ActivityAttempt` submitted for a given `LearningActivity`.

Count `COUNT(DISTINCT LearningActivityId)`, not total `ActivityAttempt` rows.

A student retrying the same activity 3 times does not count as completing 3 activities.

---

# 5. Language-Pair Architecture

MVP: Persian → English.
Architecture: any source language → any target language.

Do not hard-code `IsPersianUser`, `LearningEnglish`, `PersianEnglishOnly`.

Prefer: `SourceLanguage`, `TargetLanguage`, `LanguagePair`, `SourceLanguageDirection`, `TargetLanguageDirection`.

Prompt variables: `{{SourceLanguageName}}`, `{{TargetLanguageName}}`, `{{SourceLanguageCode}}`, `{{TargetLanguageCode}}`.

Persian and English are allowed as seed data, but not as hard-coded assumptions in business logic.

---

# 6. AI Architecture Rules

AI is expensive and must be controlled.

AI must not be the system planner.

The backend owns: lesson selection, vocabulary selection, review scheduling, progress calculation, context reduction, scenario selection, token budget enforcement.

AI should only: generate or evaluate one small task at a time, return structured JSON, explain or correct user text.

Correct flow:

```
PostgreSQL
→ LearningPlanner / AiContextBuilder
→ IAiProvider
→ validated JSON response
→ saved result
→ UI display
```

Do not send full student history to AI and ask it what to do next.

---

# 7. AI Context Rules

Do not send: full history, full vocabulary list, all previous attempts, full curriculum.

Send compact packets only. Example writing feedback context:

```json
{
  "sourceLanguage": "Persian",
  "targetLanguage": "English",
  "careerProfile": "Document Controller",
  "level": "A2",
  "domainComplexity": "JuniorRole",
  "professionalExperienceLevel": "Junior_0_2Years",
  "roleFamiliarity": "CurrentlyWorkingInRole",
  "scenario": "Follow up pending document approval",
  "targetVocabulary": ["pending approval", "revised version", "could you please review"],
  "recentWeaknesses": ["too direct tone", "confuses approve and approval"],
  "userDraft": "..."
}
```

AI context must be bounded by token budgets.

**Two-dimension difficulty rule:** All AI content generation prompts must include both `level` (CEFR) and `domainComplexity` (workplace experience level). Do not introduce workplace concepts beyond the student's `domainComplexity` unless a micro-lesson in the same session introduces the concept first. See: docs/architecture/professional-experience-domain-complexity.md

---

# 8. Student Learning Memory Rules

Student learning memory is a first-class product concept.

The student should feel:

- the app remembers what they already practised
- the next module connects to previous mistakes
- the app avoids repeating the same scenario
- the path feels like a continuing learning journey

When working on learning generation, prefer compact database-driven memory over passing full history to AI.

Use compact summaries such as:

```
journey summary          strong skills
weak skills              recurring mistakes
covered scenarios        next recommended focus
skill mastery scores     recent performance summary
completed module fingerprints
```

Memory update is best-effort:

- if memory update fails, do not fail the student's activity submission
- log the failure with correlation ID
- record AI usage if an AI call was attempted
- continue returning evaluation feedback to the student

---

# 9. Adaptive Module Generation Rules

Initial onboarding may generate a starter path.

After the student has history, **do not generate another generic full path**.

Prefer generating:

- next 3–5 recommended modules
- continuation modules
- targeted reinforcement modules
- modules based on memory and progress

Adaptive generation must consider:

```
completed modules         last few modules
module fingerprints       recurring mistakes
weak skills               strong skills
covered scenarios         current CEFR level
career context            curriculum map
```

Each generated module should include where supported:

```
title        description    focusSkill
reason       difficulty     fingerprint
avoidsRepeating
```

---

# 10. Anti-Repetition Rules

Generated modules and activities must avoid repetition.

Do not rely only on module titles to detect repetition.

Use module/activity fingerprints:

```
communicationMode    scenarioType    audience
tone                 difficulty      grammarFocus
vocabularyTheme
```

Avoid generating modules with the same `scenarioType + audience + communicationMode`.

These may look different but are effectively duplicates:

```
Explaining Delays
Reporting a Schedule Delay
Notifying Manager of Delay
```

Prefer:

- new workplace situations for weak skills
- gradual difficulty progression
- reinforcement without repeating the exact same task
- career-relevant communication

---

# 11. Feedback and Coaching Rules

AI feedback should coach the student, not simply replace their work with a perfect answer.

Feedback should be iterative:

- explain what changed
- show before/after changes
- prioritise top issues (3–5 max when many issues exist)
- provide mini lesson
- suggest next improvement
- allow retry/improve

The "improved version" is a suggestion, not "the correct answer".

---

# 12. Native-Language Support Rules

Native-language explanations are support, not the main learning experience.

Main feedback must remain in English.

For Persian/Farsi learners:

- Persian explanation may be generated
- hidden/collapsed by default
- user clicks "Show Persian explanation" to reveal
- do not show all feedback in Farsi by default

Do not hard-code Farsi everywhere — use generic support language names where the model supports it.

---

# 13. AI Provider Resilience Rules

Respect: primary provider, fallback provider, Qwen provider support, provider/model configuration, API key safety, AI usage tracking.

If primary provider fails:

- log failure with correlation ID
- record failed usage
- try fallback if configured

If fallback succeeds:

- record fallback usage with `IsFallback = true`
- return result

If all providers fail:

- return a controlled AI unavailable response
- frontend shows friendly message with correlation ID
- do not show broken JSON, do not fake detailed AI feedback

Never expose: API keys, tokens, secrets, raw Authorization headers.

---

# 14. AI Usage Tracking Rules

Every attempted AI provider call must be tracked.

Track:

```
featureKey          provider          model
userId              isFallback        wasSuccessful
failureReason       durationMs        inputTokens
outputTokens        estimatedCost     correlationId
```

Do not invent token counts or costs.
Tests must not depend on real AI providers — use fake/mock providers.

---

# 15. Observability Rules

Every backend request must preserve correlation ID behaviour.

Respect: `CorrelationIdMiddleware`, `GlobalExceptionMiddleware`, `RequestLoggingMiddleware`, structured logging, admin diagnostics.

When adding handlers/services:

- add useful structured logs
- include correlation ID through logging scope
- do not log secrets
- do not log full student submitted text in Production
- do not log full prompts or AI responses in Production unless explicitly allowed by safe Development settings

---

# 16. UI Rules

Do not redesign the whole app unless the sprint explicitly asks.

Respect layout architecture: `PublicLayout`, `StudentAppLayout`, `AdminAppLayout`.

Pages render page content only. Layouts own sidebar/header/shell.

Avoid: duplicated headers, duplicated sidebars, raw JSON shown to users, unprofessional placeholder text, horizontal overflow, broken mobile layout.

The UI should feel: modern SaaS, warm, trustworthy, professional, mobile-first, workplace-confidence focused.

Avoid: childish gamification, excessive gradients, emoji-heavy UI, generic AI startup copy, dense enterprise clutter, raw Angular scaffold styling.

Every page should answer:

```
Where am I?
What should I do next?
Why does this matter?
What happens if something goes wrong?
```

---

# 17. Testing Rules

Before reporting completion, run:

```
Backend:   dotnet test
Frontend:  npm run build
           npx playwright test
```

When AI prompts or AI provider logic changes: test fallback behaviour, safe failure, usage tracking, no secrets exposed.

When learning memory/adaptive generation changes: test memory creation/update, duplicate prevention, module fingerprint persistence, adaptive context construction, generation does not pass full attempt history.

Do not declare a frontend feature complete based only on unit tests.

---

# 18. Documentation Rules

Every major sprint creates/updates:

```
docs/sprints/<sprint-name>.md
```

Every major architecture change creates/updates:

```
docs/architecture/<topic>.md
```

Maintain:

```
docs/backlog/product-backlog.md
```

Agent-specific files:

- `AGENTS.md` = shared rules for all agents
- `CLAUDE.md` = Claude-specific rules, should align with AGENTS.md
- `QWEN.md` = Qwen-specific rules, should align with AGENTS.md

Sprint documents must include: sprint name, current state, product goal, architecture decisions, in scope, out of scope, API changes, DB changes, frontend changes, test plan, tasks, risks.

---

# 19. Auth and Security Rules

Do not commit secrets.

JWT signing key must come from environment variables/secrets outside Development. Production must fail startup if JWT key is missing, too short, or equals placeholder.

`MustChangePassword` must be enforced server-side. Students with temporary passwords cannot access onboarding, dashboard, or any protected feature until password is changed.

Admin-only endpoints require Admin role. Student endpoints require authentication. No public registration. Invalid login must not leak whether user exists.

---

# 20. Deployment Rules

Domain: `speakpath.app`

Must support: HTTPS, API health check, frontend route refresh, `/api` routing, Docker Compose, GitHub Actions, VPS deployment.

Production checks must verify: web responds, `/health` returns API health (not SPA HTML), unauthenticated protected API returns 401 not 500.

---

# 21. Scope Rules

Do not add unless explicitly requested:

```
public registration          payments              organisations
real-time voice              new AI providers      full spaced repetition
large dashboard analytics    teacher features      mobile native app
Call Mode                    Pronunciation         avatar / video lessons
```

Do not overbuild. Do not add new architecture when a UI/UX or flow fix is needed.

Do not treat `ActivityType` values as isolated product features. They are implementation tools. The product experience is defined by `ExercisePattern` within `LearningSession`.

Do not introduce workplace concepts beyond the student's `DomainComplexity` / `WorkplaceSeniority`. Include a `micro_lesson_*` step first if a new concept must be used.

---

# 22. Git and Commit Rules

Keep changes scoped. After editing, report: changed files, what changed, tests run, build result, known risks, what was not done.

Do not commit: secrets, generated junk, local-only tool settings.

---

# 23. Review Rules

Run full review for: auth/security changes, AI provider/prompt changes, new migrations, major frontend flow, deployment, before ship.

Lightweight self-check for other work: build passes, tests pass, scope is clean, architecture boundaries preserved, no secrets, no obvious user-flow breakage.

---

# 24. Current Product Priority

Current priority: **Placement Assessment MVP, then Guided Course (LearningSession / Today page).**

All four activity types (WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay) are implemented. The next phase is structural: placement → guided sessions → exercise pattern engine.

Do not add more isolated activity types. Build the course structure that organises existing ones.

**When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.**

---

# 25. Current Critical Flow

The product currently supports this complete flow (all steps verified):

```
Admin logs in
→ Admin creates student
→ Student logs in
→ Student changes temporary password
→ Student completes onboarding
→ Student reaches dashboard
→ Student starts Writing / Listening / Vocabulary / Speaking activity
→ Student submits draft or recording
→ Student sees structured AI feedback
→ Student retries or continues to next activity
→ Student can revisit learning history
```

Next flow to build:

```
Student completes onboarding
→ Student completes Placement Assessment (6 sections)
→ PlacementResult sets CEFR level and skill profile
→ First LearningPath and LearningSession generated
→ Student sees Today's Lesson
→ Student works through ordered session exercises
→ Session reflection / AI summary
→ Next session scheduled
```

---

# 26. Final Principle

Do not optimise for tasks completed.

Optimise for:

```
A real user can understand it.
A real user can complete the flow.
A real user can get value.
The product feels credible.
The system remains safe and maintainable.
```
