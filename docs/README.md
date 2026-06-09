# SpeakPath / LinguaCoach Documentation

All long-form project documentation lives here.

Root-level markdown is limited to `README.md`, `AGENTS.md`, and agent adapter files (`CLAUDE.md`, `QWEN.md`, `CODEX.md`).

---

## Folders

| Folder | What belongs here |
|---|---|
| `architecture/` | System design, domain model, learning model, AI architecture |
| `sprints/` | Sprint plans and sprint reports. `current-sprint.md` = active sprint |
| `backlog/` | Deferred work, product backlog, technical debt |
| `engineering-plans/` | Implementation plans and task breakdowns |
| `handoffs/` | Chat-to-chat and agent-to-agent handoff notes. `current-product-state.md` = latest state |
| `decisions/` | Technical and product decisions, ADR-style notes |
| `qa/` | QA plans, smoke tests, screenshot reviews, E2E test notes |
| `deployment/` | Deployment, infrastructure, CI/CD notes |
| `testing/` | Test reports, quality audits, live AI quality reviews |

---

## Key files

| File | Purpose |
|---|---|
| `handoffs/current-product-state.md` | What is built, verified, and known gaps |
| `sprints/current-sprint.md` | Active sprint: priority, scope, out of scope |
| `backlog/deferred-work.md` | Deferred TODOs and technical debt |
| `architecture/course-session-learning-model.md` | Learning model: LearningPath → Session → Exercise |
| `architecture/placement-assessment-model.md` | Placement assessment design |
| `architecture/exercise-pattern-library.md` | ExercisePattern design |
| `deployment/production-ai-feedback.md` | AI provider config and VPS setup |
| `qa/production-gemini-smoke-test.md` | Manual smoke test procedure |

---

## Rules

- Do not create `.md` files in the repository root (except `README.md`, `AGENTS.md`, and agent adapters).
- Do not create `.md` files in random source folders.
- When major work completes, create or update the relevant doc in the correct folder.
- Sprint docs must include: sprint name, status, goal, in scope, out of scope, architecture decisions, test plan, risks.
