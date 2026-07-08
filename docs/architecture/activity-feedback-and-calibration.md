---
status: current
lastUpdated: 2026-07-08 (Phase C3)
owner: architecture
supersedes:
supersededBy:
---

# Activity Feedback and Calibration (Phase B2 — persistence/API/minimal UI foundation implemented)

Phase B2 (2026-07-08) implemented the **foundation** for this system: entity, migration, admin
policy, API endpoints, and a minimal student-facing prompt. It is **not** a full calibration
engine — no automated CEFR/difficulty/template-quality recalculation runs on this data yet; it is
collected and queryable, ready for that future work. It was inserted into the phase sequence
directly after Phase C2 and before Phase C3.

**Phase C3 (2026-07-08)** migrated one additional Practice Gym pattern (`reorder_paragraphs`) to
the bank-first template path. No changes were needed to the `FeedbackPolicy` wiring in
`ActivitySubmitHandler` (added in Phase B2) — it is gated only on Today-vs-Practice-Gym surface
detection (via the `SessionExercise` link), never on a specific pattern key, so it applies to
`reorder_paragraphs` (and any future migrated pattern) automatically.

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

**Implemented (2026-07-08):** a single policy dimension per surface (Today, Practice Gym),
admin-configurable via the existing generic feature-gate/runtime-settings system — group
`activity-feedback-policy`, keys `ActivityFeedback.TodayPolicy` / `ActivityFeedback.PracticeGymPolicy`,
each a string constrained to `Off|Optional|Required`, default `Optional`. This reuses
`RuntimeSettingOverride` (the same mechanism as `ReadinessPool.*` and
`PracticeGymFormIoPilot.Enabled`) via a new `IActivityFeedbackPolicyProvider` /
`ActivityFeedbackPolicyProvider` (mirrors `EffectiveReadinessPoolSettingsProvider`). No dedicated
admin UI was built — the existing `/admin/settings/feature-gates` page picks up the new group
automatically since it is data-driven from the backend registry.

## Student feedback fields

- **Difficulty**: too easy / right level / too hard (`ActivityFeedbackDifficultyRating`).
- **Clarity/structure**: clear / okay / confusing (`ActivityFeedbackClarityRating`).
- **Usefulness**: useful / not useful (`ActivityFeedbackUsefulnessRating`).
- **Repeat/recommendation**: more like this / repeat this / don't show similar soon / neutral
  (`ActivityFeedbackRepeatPreference`).
- **Optional comment**: free text, max 500 chars, always optional even under a "Required" policy.

**Implemented (2026-07-08):** `ActivityFeedbackSignal` entity (`src/LinguaCoach.Domain/Entities/`)
persists exactly these fields, plus provenance (`StudentProfileId`, `LearningActivityId`,
`ActivityAttemptId?`, `StudentActivityUsageLogId?`, `StudentActivityReadinessItemId?`,
`SourceTemplateId?`, `SourceBankItemId?`, `PatternKey?`, `Skill?`, `Subskill?`, `CefrLevel?`,
`CurriculumObjectiveKey?`) backfilled best-effort from the matching `StudentActivityUsageLog` row.
Idempotent: one row per `(StudentProfileId, ActivityAttemptId)` when an attempt is known, else one
per `(StudentProfileId, LearningActivityId)` — enforced by two partial unique indexes
(`ux_feedback_signals_student_attempt`, `ux_feedback_signals_student_activity_no_attempt`).
Resubmitting updates the existing row (`ActivityFeedbackSignal.UpdateRatings`) rather than
duplicating it.

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

**Implemented (2026-07-08):** `ActivitySubmitHandler` (both the pattern-evaluation and legacy/AI
paths) now populates a `FeedbackPolicy` field on the existing `ActivityFeedbackDto` response
(the same response returned by `POST /api/activity/{activityId}/attempt`), determined via
`IActivityFeedbackPolicyProvider` using the same Today-vs-Practice-Gym surface detection already
used for `StudentLearningEvent.Source` (presence/absence of a linked `SessionExercise`). This is
best-effort/non-blocking, matching the handler's existing defensive style. The student submits
feedback via a separate call: `POST /api/activity/attempt/{attemptId}/feedback`
(`ActivityController.SubmitAttemptFeedback`), which the client makes only when the returned
policy is not `Off`.

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

## Status (updated 2026-07-08, Phase B2 implemented)

**Foundation implemented, not a calibration engine.** What exists:

- `ActivityFeedbackSignal` entity + EF configuration + migration `AddActivityFeedbackSignal`.
- `IActivityFeedbackPolicyProvider` / `ActivityFeedbackPolicyProvider` (Off/Optional/Required per
  surface, via the existing feature-gate/runtime-settings system — no new admin UI needed).
- `ISubmitActivityFeedbackHandler` / `ActivityFeedbackHandler` (upsert, ownership validation,
  comment-length validation, provenance backfill from `StudentActivityUsageLog`).
- API: `POST /api/activity/attempt/{attemptId}/feedback` (submit/update); `FeedbackPolicy` added
  to the existing attempt-submission response DTO.
- Minimal student UI: `activity-feedback-prompt` component, shown from the existing
  `activity-feedback-page` only when policy is not `Off`, with a Skip button shown only when
  `Optional`.
- Backend tests: policy resolution, submit/upsert, ownership rejection, comment-length validation,
  provenance backfill, wiring into `ActivitySubmitHandler`'s two dispatch paths.

**What does NOT exist yet (deferred, not this phase's scope):**

- Any automated consumption of the collected feedback — no CEFR/difficulty-band recalculation, no
  automatic `ActivityTemplate`/resource quality scoring, no automatic novelty/cooldown adjustment
  (`docs/architecture/repetition-and-novelty.md`'s "Relationship to Phase B2" section still
  describes this as not implemented — that remains true).
  This phase collects and stores signal; a human or a future phase must still act on it.
- Admin analytics dashboard or review-queue automation beyond what already existed — deferred, per
  scope.
- Any wiring into the `/speaking-attempt` or `/audio-attempt` endpoints (they do not go through
  `ActivitySubmitHandler`) — out of scope for this phase.

See `docs/roadmap/road-map.md` §19a for where Phase B2 sits in the overall phase sequence
(**Phase C3 is next and has not started**), and `docs/sprints/current-sprint.md` for
current-sprint status.
