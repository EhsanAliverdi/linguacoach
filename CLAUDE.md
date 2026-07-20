# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Claude Adapter — SpeakPath / LinguaCoach

Before doing any work, read:

1. `AGENTS.md`
2. `docs/architecture/README.md`
3. Relevant docs under `docs/`

`AGENTS.md` is the single source of truth.
If this file conflicts with `AGENTS.md`, `AGENTS.md` wins.

---

## Claude-specific notes

* Claude Code must follow the shared gstack workflow defined in `AGENTS.md`.
* Use Claude/gstack skills when available.
* Do not duplicate product, architecture, AI, UI, testing, security, deployment, scope, gstack, or skill-routing rules here.
* Keep changes scoped and report completion using the format required by `AGENTS.md`.

---

## Documentation persistence rule

Claude Code must not leave important decisions, reviews, plans, or implementation findings only in chat output.

Whenever Claude performs any of the following:

* engineering review
* architecture review
* code review
* test review
* adversarial review
* outside voice review
* implementation readiness review
* sprint planning
* implementation plan
* major debugging analysis
* product/architecture decision
* AskUserQuestion decision summary

Claude must save the result into the repository under the appropriate docs folder.

Use these locations:

```text
docs/reviews/
docs/sprints/
docs/architecture/
docs/testing/
docs/backlog/
```

Review documents should usually go under:

```text
docs/reviews/
```

Recommended review filename format:

```text
yyyy-mm-dd-<sprint-or-topic>-<review-type>.md
```

Example:

```text
docs/reviews/2026-06-09-onboarding-post-placement-ux-engineering-review.md
```

Each persisted review or plan should include:

* title
* date
* related sprint or feature
* files reviewed
* findings grouped by priority
* decisions made
* AskUserQuestion answers, if any
* implementation tasks produced
* risks or unresolved questions
* final verdict
* next recommended action

If the review produces implementation tasks, update the relevant sprint doc and backlog.

If the review changes architectural direction, update the relevant architecture doc.

A review or plan is not complete until it is saved in the repo and linked or referenced from the related sprint/backlog/architecture doc where appropriate.

---

## Documentation impact

Claude Code must perform a documentation impact review for every code change.

Before reporting completion, include:

```text
Documentation impact:
- Docs reviewed:
- Docs updated:
- Docs intentionally not updated:
- Reason:
```

If code changes product behaviour, user flow, API contract, database model, lifecycle, architecture, tests, deployment, security, AI prompts, or agent workflow, update the relevant docs in the same change.

For full rules, see `AGENTS.md`.

---

## Important

Chat output is not a durable project record.

If future Claude/Codex/Qwen agents need the information to continue safely, it must be written to docs.

---

## Output style (token saving)

Short sentences only, 8-10 words max.
No filler, no preamble, no pleasantries.
Tool first. Result first. No explain unless asked.
Code stays normal. English gets compressed.
Output sounds human. Never AI-generated.
Never use em-dashes or replacement hyphens.
Avoid parenthetical clauses entirely.
Hyphens map to standard grammar only.

This style applies to chat output only.
Persisted docs (reviews, sprints, architecture) keep full prose, per documentation persistence rule above.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

---

## Commands

### Backend

```bash
# Run API locally (requires PostgreSQL or Docker)
cd src/LinguaCoach.Api && dotnet run

# Run all tests (SQLite in-memory, no PostgreSQL needed)
dotnet test

# Run a single test project
dotnet test tests/LinguaCoach.UnitTests
dotnet test tests/LinguaCoach.IntegrationTests
dotnet test tests/LinguaCoach.ArchitectureTests

# Run a single test by name filter
dotnet test --filter "FullyQualifiedName~YourTestName"

# Release build
dotnet build --configuration Release
dotnet test --configuration Release
```

### Frontend

```bash
cd src/LinguaCoach.Web

# Dev server
npm start                          # ng serve

# Production build
npm run build -- --configuration production

# Unit tests (Karma/Jasmine, headless)
npm test -- --watch=false --browsers=ChromeHeadless

# Playwright E2E (run from web dir)
npm run e2e                        # playwright test

# Single Playwright spec
npx playwright test e2e/admin-students-reset.spec.ts
```

### Docker

The `web` container serves a **baked production build** (multi-stage Dockerfile: `ng build` →
copied into nginx) — it is the only supported way to run the frontend locally in this repo.
**Never run `npm start`/`ng serve` locally against this repo**: it binds to the same port 4200 as
the Docker `web` container and silently shadows it on IPv6, so `curl`/browser hits against
`localhost:4200` land on the stray local process instead of Docker, making live verification lie.
If you ever see this (e.g. `curl http://localhost:4200` doesn't show `@vite/client`-free static
HTML), find and kill the stray `node.exe`/`ng serve` process before trusting any Docker-based check.

Because `web` is a baked build, a plain `docker compose up -d` does **not** pick up new frontend
code — it keeps serving whatever was last built. Use one of:

```bash
# Start API + PostgreSQL together (migrations run on startup)
docker compose up --build

# One-off rebuild of just the web (and/or api) image + redeploy after a code change
docker compose build --no-cache api web
docker compose up -d api web

# Active development: auto-rebuild + restart the web container on every source change.
# `--watch` cannot combine with `-d` — start detached first, then watch in its own terminal:
docker compose up -d
docker compose watch

# Teardown including volumes
docker compose down -v
```

Every code-change verification cycle in this repo (see the mandatory per-step workflow) must end
with an actual rebuild+redeploy of any container whose image changed — never assume `docker compose
up -d` alone reflects the latest code.

---

## Architecture

### Backend layers (Clean Architecture)

```
LinguaCoach.Domain         — Entities, enums, value objects. No EF Core, no ASP.NET.
LinguaCoach.Application    — Use cases, interfaces (IAdminStudentQuery etc.), DTOs, queries/commands.
LinguaCoach.Infrastructure — AI providers, email, external services. Implements Application interfaces.
LinguaCoach.Persistence    — EF Core DbContext, Identity, migrations, query handlers.
LinguaCoach.Api            — Controllers, middleware, JWT wiring, CORS, health checks.
LinguaCoach.Worker         — Background jobs.
LinguaCoach.Web            — Angular SPA (standalone components, Signals, Tailwind CSS).
```

Dependency rule: Domain ← Application ← {Infrastructure, Persistence} ← Api. No reverse deps.

### Test projects

```
tests/LinguaCoach.UnitTests          — Domain/Application logic, mocked dependencies.
tests/LinguaCoach.IntegrationTests   — API + EF Core against SQLite in-memory.
tests/LinguaCoach.ArchitectureTests  — NetArchTest layer boundary enforcement.
src/LinguaCoach.Web/e2e/             — Playwright browser tests.
```

### Learning model hierarchy

```
LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity → ActivityAttempt
```

Practice Gym uses `LearningActivity` directly, without a session.

A completed activity = at least one `ActivityAttempt` submitted.
Count `COUNT(DISTINCT LearningActivityId)`, not total attempt rows.

### Frontend structure

```
src/app/design-system/
  admin/    — sp-admin-* wrapper components, services, tokens, utils,
              + layouts/admin-app-layout/
  student/  — student-ui components, notification-dropdown,
              + layouts/student-app-layout/
  public/   — layouts/public-layout/ (only, for now)

src/app/features/
  admin/    — admin feature pages (unchanged)
  student/  — activity, assessment, dashboard, learning-path, lesson,
              onboarding, placement, practice, profile, progress,
              speaking, vocabulary
  public/   — landing, auth

src/app/core/ — Services, models, guards, interceptors
```

Admin feature pages import from `src/app/design-system/admin`.
Layout components: `PublicLayout`, `StudentAppLayout`, `AdminAppLayout`. Pages render content only; layouts own shell/sidebar/header.

### AI flow

```
PostgreSQL → LearningPlanner / AiContextBuilder → IAiProvider → validated JSON → saved result → UI
```

AI receives compact bounded packets only. Every provider call is tracked with `featureKey`, `provider`, `model`, `userId`, `isFallback`, `wasSuccessful`, token counts, cost, and `correlationId`. Tests use fake/mock providers — never real AI.

### Admin query/handler pattern

Admin features follow `IAdminStudentQuery` → handler in `LinguaCoach.Infrastructure/Admin/` → controller in `LinguaCoach.Api/Controllers/`. DTOs are defined in `LinguaCoach.Application/Admin/`.

### Key env vars

| Var | Purpose |
|-----|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL |
| `JWT_KEY` | Must be ≥ 32 chars outside Development |
| `AI__WritingFeedback__Provider` | `OpenAI`, `Gemini`, or `Anthropic` |
| `OPENAI_API_KEY` / `GEMINI_API_KEY` / `ANTHROPIC_API_KEY` | Per-provider keys |
