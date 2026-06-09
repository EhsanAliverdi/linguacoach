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
