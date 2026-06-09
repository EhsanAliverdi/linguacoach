---
status: current
lastUpdated: 2026-06-09 13:11
owner: engineering
supersedes:
supersededBy:
---

# Documentation Impact and Freshness Rules Review

Date: 2026-06-09

## Why this rule was added

Agents were already expected to update documentation when system behaviour changed, but the rule did not require an explicit documentation impact review for every code change.

The new rule closes that gap: every code-change completion report must identify docs reviewed, docs updated, docs intentionally not updated, and the reason. This prevents stale source-of-truth docs without encouraging blind mass updates.

## Files updated

- `AGENTS.md`
- `CLAUDE.md`
- `docs/architecture/README.md`
- `docs/backlog/product-backlog.md`
- `docs/sprints/current-sprint.md`
- Main source-of-truth docs listed below

## Metadata strategy

Major source-of-truth docs now include a metadata block near the top:

```yaml
---
status: current | draft | historical | superseded
lastUpdated: YYYY-MM-DD HH:mm
owner: product | architecture | engineering | qa | deployment
supersedes:
supersededBy:
---
```

For this cleanup, the selected source-of-truth docs were marked `current` because they are actively referenced by `AGENTS.md`, `docs/architecture/README.md`, the current sprint, or the current product handoff.

`lastUpdated` uses Sydney local time: `2026-06-09 13:11`.

## Docs that received metadata

- `docs/architecture/README.md`
- `docs/handoffs/current-product-state.md`
- `docs/sprints/current-sprint.md`
- `docs/backlog/product-backlog.md`
- `docs/architecture/course-session-learning-model.md`
- `docs/architecture/exercise-pattern-library.md`
- `docs/architecture/placement-assessment-model.md`
- `docs/architecture/professional-experience-domain-complexity.md`
- `docs/architecture/practice-gym.md`
- `docs/architecture/file-storage-minio.md`
- `docs/architecture/student-lifecycle-reset-tools.md`
- `docs/architecture/student-learning-memory.md`
- `docs/architecture/frontend-layout-system.md`
- `docs/architecture/speakpath-design-system.md`
- `docs/engineering-plans/implementation-roadmap.md` (`historical`, superseded by `docs/architecture/README.md`)

## Docs intentionally not updated

- Application code, migrations, Angular logic, C# logic: not changed because this was a documentation-only task.
- Other markdown files under `docs/`: not mass-edited because the rule requires relevant updates only.
- `docs/implementation-roadmap.md`: not updated because that path does not exist. The actual historical roadmap at `docs/engineering-plans/implementation-roadmap.md` was updated instead.

## Docs needing human confirmation

None identified in this cleanup. The requested main source-of-truth docs exist except `docs/implementation-roadmap.md`, which is absent.

## Final rule summary

Every code change now requires a documentation impact review before completion. If implementation changes product behaviour, user flow, API contract, database model, lifecycle, architecture, tests, deployment, security, AI prompts, or agent workflow, the relevant source-of-truth docs must be updated in the same change.

Every final agent report must include:

```text
Documentation impact:
- Docs reviewed:
- Docs updated:
- Docs intentionally not updated:
- Reason:
```
