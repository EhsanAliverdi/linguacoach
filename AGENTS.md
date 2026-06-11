# SpeakPath / LinguaCoach — Agent Instructions

Read this file before making any changes to the repository.

The public product is **SpeakPath**.
The internal repo/project name is **LinguaCoach** (keep in .NET namespaces, migrations, infrastructure — do not rename unless asked).
User-facing UI, page titles, copy, and public docs should use **SpeakPath**.

---

# 0. Agent Workflow / gstack

All AI-assisted work in this repository uses the shared gstack workflow where available.

Before doing any implementation work:
1. Read `AGENTS.md`.
2. Read `docs/handoffs/current-product-state.md` for current product state.
3. Read `docs/sprints/current-sprint.md` for active sprint scope.
4. Read the relevant docs under `docs/architecture/`, `docs/decisions/`, or `docs/engineering-plans/` for the area you are working on.
5. Use the appropriate gstack skill for the task type (see Skill Routing below).

Prefer project-local agent instructions, skills, commands, and config over global user-level files.

If gstack is unavailable in the current agent/tool, follow the same workflow manually:
- plan before implementing
- keep changes scoped
- inspect relevant docs first
- run review/QA before reporting completion
- report files changed, tests run, risks, and what was not done

## gstack install check (Claude Code)

Before doing ANY work in Claude Code, verify gstack is installed:

```bash
test -d ~/.claude/skills/gstack/bin && echo "GSTACK_OK" || echo "GSTACK_MISSING"
```

If GSTACK_MISSING: STOP. Do not proceed. Tell the user:

> gstack is required for all AI-assisted work in this repo.
> Install it:
> ```bash
> git clone --depth 1 https://github.com/garrytan/gstack.git ~/.claude/skills/gstack
> cd ~/.claude/skills/gstack && ./setup --team
> ```
> Then restart your AI coding tool.

Do not skip skills, ignore gstack errors, or work around missing gstack.

Use `~/.claude/skills/gstack/...` for gstack file paths (the global path).

**Never use `mcp__claude-in-chrome__*` tools. Use `/browse` from gstack for all web browsing.**

## Skill / Workflow Routing

When the request matches a workflow, use the relevant gstack skill where available.

- Product ideas / brainstorming → `/office-hours`
- Strategy / scope → `/plan-ceo-review`
- Architecture → `/plan-eng-review`
- Design system / design plan → `/design-consultation` or `/plan-design-review`
- Bugs / errors → `/investigate`
- QA / browser testing → `/qa` or `/qa-only`
- Code review / diff check → `/review`
- Visual polish → `/design-review`
- Ship / deploy / PR → `/ship` or `/land-and-deploy`
- Full review pipeline → `/autoplan`
- Documentation generation → `/document-generate`
- Release documentation → `/document-release`
- Save progress → `/context-save`
- Resume context → `/context-restore`
- Author a backlog-ready spec/issue → `/spec`

---

# 1. Product Identity

SpeakPath is an **AI-powered workplace English class platform** for immigrant professionals.

Core positioning: **Professional dignity software, not a generic language app.**

The product must feel like a structured English class, not a practice card generator.

SpeakPath is **not**:
- an email correction tool
- a generic grammar checker
- a static scenario generator
- a random AI exercise generator
- a collection of isolated activity cards

For the full product description, positioning, and copy rules, see `docs/handoffs/current-product-state.md`.

---

# 2. Tech Stack

```
Backend:   .NET 10 Web API, ASP.NET Identity, JWT, EF Core, PostgreSQL
           Clean Architecture + Modular Monolith
Frontend:  Angular, Tailwind CSS, mobile-first
Infra:     Docker, Docker Compose, GitHub Actions, VPS (speakpath.app)
AI:        Provider abstraction, prompt templates in PostgreSQL,
           token budgets, AI usage logging, fallback providers
```

---

# 3. Architecture Rules

## Layer boundaries

- Domain must not depend on EF Core, ASP.NET Identity, Infrastructure, or API.
- Application defines interfaces and use cases.
- Infrastructure implements external providers.
- Persistence owns EF Core, Identity mapping, migrations, DB config.
- API owns controllers, middleware, auth wiring, CORS, health checks.
- Angular owns UI only.
- Do not put business logic in controllers.
- Do not put EF Core attributes in Domain entities.

## Learning model

```
LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity → ActivityAttempt
```

Practice Gym uses `LearningActivity` directly without a session.

See: `docs/architecture/course-session-learning-model.md`

## Activity path

`/activity` is the main student practice path.

`ActivityType` values are implementation tools, not the product experience. The product experience is defined by `ExercisePattern` within `LearningSession`. See: `docs/architecture/exercise-pattern-library.md`

Do not recreate:
- `WritingExerciseController`
- `/api/writing/*` endpoints
- old writing pages or writing service flow

## Counting completions

A "completed activity" = at least one `ActivityAttempt` submitted for a given `LearningActivity`.
Count `COUNT(DISTINCT LearningActivityId)`, not total `ActivityAttempt` rows.

## Language-pair architecture

MVP: Persian → English. Architecture: any source language → any target language.

Do not hard-code `IsPersianUser`, `LearningEnglish`, `PersianEnglishOnly`.
Prefer: `SourceLanguage`, `TargetLanguage`, `LanguagePair`, `SourceLanguageDirection`, `TargetLanguageDirection`.
Prompt variables: `{{SourceLanguageName}}`, `{{TargetLanguageName}}`, `{{SourceLanguageCode}}`, `{{TargetLanguageCode}}`.

---

# 4. AI Rules

## AI must not own planning

AI is expensive and must be controlled. AI must not be the system planner.

The backend owns: lesson selection, vocabulary selection, review scheduling, progress calculation, context reduction, scenario selection, token budget enforcement.

AI should only: generate or evaluate one small task at a time, return structured JSON, explain or correct user text.

Correct flow:
```
PostgreSQL → LearningPlanner / AiContextBuilder → IAiProvider → validated JSON → saved result → UI
```

## Token budget rules

Do not send: full history, full vocabulary list, all previous attempts, full curriculum.

Send compact packets only. AI context must be bounded by token budgets.

**Two-dimension difficulty rule:** All AI content generation prompts must include both `level` (CEFR) and `domainComplexity` (workplace experience level). Do not introduce workplace concepts beyond the student's `domainComplexity` unless a micro-lesson in the same session introduces the concept first.

See: `docs/architecture/professional-experience-domain-complexity.md`

## Student learning memory

Student learning memory is a first-class product concept. Prefer compact database-driven memory over passing full history to AI.

Memory update is best-effort: if it fails, do not fail the student's activity submission. Log the failure with correlation ID and continue returning feedback.

See: `docs/architecture/student-learning-memory.md`

## Adaptive generation

Do not generate another generic full path after the student has history. Prefer next 3–5 recommended modules, continuation modules, and targeted reinforcement based on mistakes and progress.

Use module fingerprints to prevent repetition:
```
communicationMode    scenarioType    audience    tone    difficulty    grammarFocus    vocabularyTheme
```

## AI feedback coaching rules

Feedback should coach the student, not simply replace their work with a perfect answer. Prioritise top issues (3–5 max). The "improved version" is a suggestion, not "the correct answer".

## Native-language support

Main feedback must remain in English. Native-language explanations are support only — hidden by default, revealed on request.

## AI provider resilience

If primary provider fails: log with correlation ID, record failed usage, try fallback.
If all providers fail: return a controlled AI unavailable response. Never expose API keys, tokens, or secrets.

## AI usage tracking

Every attempted AI provider call must be tracked:
```
featureKey  provider  model  userId  isFallback  wasSuccessful  failureReason  durationMs  inputTokens  outputTokens  estimatedCost  correlationId
```

Do not invent token counts or costs. Tests must not depend on real AI providers — use fake/mock providers.

---

# 5. Observability Rules

Every backend request must preserve correlation ID behaviour.

Respect: `CorrelationIdMiddleware`, `GlobalExceptionMiddleware`, `RequestLoggingMiddleware`, structured logging, admin diagnostics.

- Add useful structured logs including correlation ID.
- Do not log secrets, full student submitted text in Production, or full prompts/AI responses in Production.

---

# 6. UI Rules

Do not redesign the whole app unless the sprint explicitly asks.

Respect layout architecture: `PublicLayout`, `StudentAppLayout`, `AdminAppLayout`.
Pages render page content only. Layouts own sidebar/header/shell.

The UI should feel: modern SaaS, warm, trustworthy, professional, mobile-first, workplace-confidence focused.

Avoid: duplicated headers/sidebars, raw JSON, unprofessional placeholder text, horizontal overflow, broken mobile layout, childish gamification, excessive gradients, emoji-heavy UI, generic AI startup copy.

Every page should answer: Where am I? What should I do next? Why does this matter? What happens if something goes wrong?

See: `docs/architecture/frontend-layout-system.md`, `docs/architecture/speakpath-design-system.md`

---

# 7. Testing Rules

Before reporting completion, run:

```
Backend:   dotnet test
Frontend:  npm run build
           npx playwright test
```

When AI prompts or AI provider logic changes: test fallback behaviour, safe failure, usage tracking, no secrets exposed.

When learning memory/adaptive generation changes: test memory creation/update, duplicate prevention, module fingerprint persistence, adaptive context construction.

Do not declare a frontend feature complete based only on unit tests.

---

# 8. Security Rules

Do not commit secrets.

JWT signing key must come from environment variables/secrets outside Development. Production must fail startup if JWT key is missing, too short, or equals placeholder.

`MustChangePassword` must be enforced server-side. Students with temporary passwords cannot access any protected feature until password is changed.

Admin-only endpoints require Admin role. No public registration. Invalid login must not leak whether user exists.

---

# 9. Deployment Rules

Domain: `speakpath.app`

Must support: HTTPS, API health check, frontend route refresh, `/api` routing, Docker Compose, GitHub Actions, VPS deployment.

Production checks must verify: web responds, `/health` returns API health (not SPA HTML), unauthenticated protected API returns 401 not 500.

See: `docs/deployment/production-ai-feedback.md`, `docs/qa/production-gemini-smoke-test.md`

---

# 10. Scope Rules

Do not add unless explicitly requested:

```
public registration    payments            organisations
real-time voice        new AI providers    full spaced repetition
large analytics        teacher features    mobile native app
Call Mode              Pronunciation       avatar / video lessons
```

Do not overbuild. Do not add new architecture when a UI/UX or flow fix is needed.

Do not treat `ActivityType` values as isolated product features.

---

# 11. Git and Review Rules

Keep changes scoped. After editing, report: changed files, what changed, tests run, build result, known risks, what was not done.

Do not commit: secrets, generated junk, local-only tool settings.

Run full review for: auth/security changes, AI provider/prompt changes, new migrations, major frontend flow, deployment, before ship.

Lightweight self-check for other work: build passes, tests pass, scope is clean, architecture boundaries preserved, no secrets, no obvious user-flow breakage.

---

# 12. Documentation Rules

Root-level markdown files are limited to:
- `README.md`
- `AGENTS.md`
- agent adapters such as `CLAUDE.md`, `QWEN.md`, `CODEX.md`

All other documentation must live under `/docs`. See `docs/README.md` for the folder map.

## Mandatory Documentation Impact Review

Every code change requires a documentation impact review.

Before reporting completion, agents must answer:

1. Did this change alter product behaviour, user flow, API contract, database model, lifecycle, architecture, tests, deployment, security, AI prompts, or agent workflow?
2. Which source-of-truth docs are affected?
3. Were the affected docs updated in this change?
4. If no docs were updated, why was no documentation update needed?

This must be included in the final report.

A code task is not complete until the documentation impact review is done.

Agents must not update every document blindly.
Agents must update the relevant documents only.

If implementation changes the system and docs still describe the old behaviour, the task is incomplete.

Examples:

| Code change | Required documentation action |
|---|---|
| New/changed student flow | Update current sprint doc and current product state/handoff |
| New/changed API endpoint | Update sprint doc and relevant architecture/API doc |
| New/changed DB entity/migration | Update sprint doc and relevant architecture doc |
| New lifecycle stage or transition | Update lifecycle/placement/session architecture docs |
| New AI prompt or provider behaviour | Update AI/prompt architecture or sprint doc |
| New frontend route/page/guard | Update sprint doc and relevant UI/flow doc |
| New test strategy or QA finding | Update `docs/testing/` or `docs/qa/` |
| Completed/deferred work | Update backlog |
| New review/engineering decision | Save review under `docs/reviews/` |

## Documentation Freshness and Timestamp Rule

Major project docs must include metadata near the top so agents can judge freshness.

Use this metadata block:

```yaml
---
status: current | draft | historical | superseded
lastUpdated: YYYY-MM-DD HH:mm
owner: product | architecture | engineering | qa | deployment
supersedes:
supersededBy:
---
```

Rules:

* `lastUpdated` must use Sydney local time unless the doc already uses UTC.
* When a doc is substantially updated, update its `lastUpdated` value.
* When a doc is no longer the source of truth, mark it `historical` or `superseded`.
* Do not delete old docs just because they are stale.
* Do not mass-edit every doc during normal feature work.
* For this cleanup task only, add metadata to the main existing source-of-truth docs.

Agents must prefer:

1. `AGENTS.md`
2. `docs/architecture/README.md`
3. docs marked `current`
4. newer `lastUpdated`
5. latest sprint docs
6. docs marked `historical` or `superseded` only as context

## Required Final Report Documentation Section

Every agent completion report must include:

```text
Documentation impact:
- Docs reviewed:
- Docs updated:
- Docs intentionally not updated:
- Reason:
```

If code changed and this section is missing, the work is incomplete.

## Reading Order

Before planning or implementing work, agents must read:

1. `AGENTS.md`
2. `docs/README.md`
3. `docs/sprints/current-sprint.md`
4. `docs/handoffs/current-product-state.md`
5. Any relevant architecture, decision, QA, deployment, or engineering-plan docs for the task

Do not rely only on `AGENTS.md` for current implementation state.

## Documentation Updates

When work changes the system, update the matching docs in the same change.

- New sprint scope or changed priority → `docs/sprints/current-sprint.md`
- Completed or verified product flow → `docs/handoffs/current-product-state.md`
- Major sprint work → `docs/sprints/<sprint-name>.md`
- Architecture change → `docs/architecture/<topic>.md`
- Technical decision → `docs/decisions/<decision>.md`
- Deferred work / TODO → `docs/backlog/deferred-work.md`
- Handoff summary → `docs/handoffs/<handoff-name>.md`
- QA finding or audit → `docs/qa/<topic>.md` or `docs/testing/`
- Deployment / infrastructure notes → `docs/deployment/<topic>.md`

Sprint documents must include: sprint name, status, product goal, architecture decisions, in scope, out of scope, API changes, DB changes, frontend changes, test plan, tasks, risks.

Do not create random `.md` files in the repository root or in source folders.
Do not duplicate long-form documentation inside `AGENTS.md`.
Do not duplicate shared rules across agent adapter files.

---

# 13. Current Priority

The current priority must be read from:
- `docs/sprints/current-sprint.md`
- `docs/handoffs/current-product-state.md`

At the time of this file's last update: **Placement Assessment MVP, then Guided Course using `LearningSession` / `SessionExercise` / Today page.**

If the sprint doc conflicts with this file, the sprint doc wins.

---

## Documentation Persistence Rule

Important project decisions must be durable.

Agents must not leave engineering reviews, architecture decisions, sprint plans, implementation plans, debugging findings, or AskUserQuestion decisions only in chat output.

If the information is needed by a future agent, it must be saved in the repository.

Use:

* `docs/reviews/` for engineering/code/test/adversarial/outside-voice reviews
* `docs/sprints/` for sprint plans and sprint completion records
* `docs/architecture/` for architecture decisions and long-term design
* `docs/testing/` for QA reports and validation evidence
* `docs/backlog/product-backlog.md` for future work and deferred tasks

Recommended review filename format:

```text
yyyy-mm-dd-<topic>-<review-type>.md
```

A review, plan, or major decision is not considered complete until:

1. it is written to the appropriate docs location, and
2. the related sprint/backlog/architecture doc is updated if needed.

---

# 14. Final Principle

Do not optimise for tasks completed.

Optimise for:
```
A real user can understand it.
A real user can complete the flow.
A real user can get value.
The product feels credible.
The system remains safe and maintainable.
```


## Approach
- Read existing files before writing. Don't re-read unless changed.
- Thorough in reasoning, concise in output.
- Skip files over 100KB unless required.
- No sycophantic openers or closing fluff.
- No emojis or em-dashes.
- Do not guess APIs, versions, flags, commit SHAs, or package names. Verify by reading code or docs before asserting.

# Core Rules

Short sentences only (8-10 words max).
No filler, no preamble, no pleasantries.
Tool first. Result first. No explain unless asked.
Code stays normal. English gets compressed.

---

## Formatting

Output sounds human. Never AI-generated.
Never use em-dashes or replacement hyphens.
Avoid parenthetical clauses entirely.
Hyphens map to standard grammar only.

---

## Usage

Paste at session start or drop as CLAUDE.md in project root.