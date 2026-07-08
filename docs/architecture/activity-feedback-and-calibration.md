---
status: draft
lastUpdated: 2026-07-08 (Plan-Sync-B2)
owner: architecture
supersedes:
supersededBy:
---

# Activity Feedback and Calibration (Phase B2 — planned, not started)

This document is a **plan**, written during Plan-Sync-B2 (2026-07-08, docs-only). No app code,
migrations, or config for this phase exist yet. It is inserted into the phase sequence directly
after Phase C2 and before Phase C3 — see `docs/roadmap/road-map.md` §19a — because explicit
student feedback/rating/calibration signals should start accumulating before Practice Gym
migration continues further, so later Practice Gym batches (C3/C4/C-Final) and eventually Phase D
can be informed by real quality/difficulty signal instead of only structural selection criteria.

---

## Purpose

Phase B (`docs/architecture/repetition-and-novelty.md`) implemented **implicit, deterministic**
content-usage tracking: fingerprints, cooldown windows, all computed automatically with no
student input. It answers "has this student seen this content before," not "did the student think
this content was good, clear, at the right level, or worth repeating."

Phase B2 closes that gap: a short, optional-by-default, post-activity feedback capture that
becomes a first-class calibration signal, distinct from and complementary to Phase B's usage log.

## Scope

- A small, student-facing feedback prompt shown after an activity is submitted/scored.
- A backend record of that feedback, linked to the specific `ActivityAttempt`/`LearningActivity`
  (and, where applicable, the `ActivityTemplate`/`SourceTemplateId` that produced it).
- Admin-configurable policy for whether/when feedback is shown (see "Admin policy settings"
  below).
- Downstream consumption of that feedback into calibration signals (see "How it will help").

## Out of scope (for Phase B2)

- A large analytics/reporting dashboard beyond a simple admin summary view.
- Any redesign of student-facing UI beyond the feedback prompt itself.
- Automatic AI retraining or prompt self-modification based on feedback.
- Phase D (bank-first Today lesson composer) — Phase B2 does not build or depend on it.
- Phase E (resource-import platform) — Phase B2 does not build or depend on it.
- Automatically changing cooldown windows, CEFR levels, or template approval status from feedback
  without human review — this phase collects and surfaces signal; it does not wire feedback
  directly into automated state changes.

## Admin policy settings

A single policy dimension, admin-configurable, with three states:

- **Off** — no feedback prompt shown anywhere.
- **Optional** — feedback prompt shown, student may skip it.
- **Required** — student must respond (at minimum, the difficulty/clarity/usefulness/repeat
  fields — the comment stays optional even when the rest is required) before the activity is
  considered fully complete.

### Future per-surface policy

The policy is expected to be settable independently per surface once implemented:

- **Today** — optional or required, independently configurable.
- **Practice Gym** — optional or required, independently configurable.
- **UAT/pilot environments** — required, to maximize signal collection during controlled testing.
- **Production** — optional by default, to avoid friction for real students until the feedback UX
  is proven.

No admin UI or config schema for this exists yet — this is the intended shape, not a built
feature.

## Student feedback fields

- **Difficulty**: too easy / right level / too hard.
- **Clarity/structure**: clear / okay / confusing.
- **Usefulness**: useful / not useful.
- **Repeat/recommendation**: more like this / repeat this / don't show similar soon.
- **Optional comment**: free text, always optional even under a "Required" policy.

## Data flow (planned)

```
Student answer
  → score/evaluation (existing ActivitySubmitHandler path)
  → student feedback/rating (Phase B2, new)
  → usage log (StudentActivityUsageLog, Phase B, existing)
  → learning event (StudentLearningEvent, existing)
  → template/resource quality update (Phase B2, new — signal surfaced, not auto-applied)
  → routing/tag adjustment (Phase B2, new — signal surfaced, not auto-applied)
  → admin review if needed (existing admin review-queue pattern, extended)
```

Feedback attaches to the same submission event as scoring and usage logging — it does not
introduce a separate, disconnected flow.

## How it will help

Once implemented, aggregated feedback signal is intended to inform:

- **CEFR calibration** — cross-referencing "too easy"/"too hard" feedback against the activity's
  assigned CEFR level, as a check on placement/level-assignment accuracy over time.
- **Difficulty band calibration** — similar to CEFR, but at the finer-grained difficulty-band
  level already tracked on `ActivityTemplate`/`PlacementItemDefinition`.
- **`ActivityTemplate`/resource quality** — templates or bank items that repeatedly draw
  "confusing" or "not useful" feedback become candidates for admin review or retirement.
- **AI-generated content quality** — the same signal applies to legacy freeform-generated
  activities, not just template-sourced ones, giving a quality signal for the AI generation path
  itself.
- **Novelty/cooldown tuning** — see `docs/architecture/repetition-and-novelty.md`'s "Relationship
  to Phase B2" section for how "more like this" / "don't show similar soon" / "I need to repeat
  this" are intended to eventually influence cooldown behavior (not implemented yet).
- **Admin review triggers** — a pattern or template accumulating negative feedback above some
  threshold could surface in the existing cross-entity admin review queue (see
  `docs/architecture/README.md`'s "Admin review queue" entry) for human triage.

## Out of scope for B2 (restated)

- Large analytics dashboard.
- Student UI redesign beyond the feedback prompt.
- Automatic AI retraining.
- Phase D Today composer.
- Phase E resource import platform.

## Status

Planned only. No entities, endpoints, migrations, jobs, or UI exist for this phase. See
`docs/roadmap/road-map.md` §19a for where Phase B2 sits in the overall phase sequence, and
`docs/sprints/current-sprint.md` for current-sprint status.
