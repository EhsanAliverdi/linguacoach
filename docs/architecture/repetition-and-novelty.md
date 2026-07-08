---
status: current
lastUpdated: 2026-07-08 (Plan-Sync-B2)
owner: architecture
supersedes:
supersededBy:
---

# Repetition and Novelty Foundation (Phase B)

Implements the real content-usage history and cooldown enforcement flagged as missing by
`docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md` (§9.2) and by
Clean-A's fix to `PracticeActivityCache.ContentFingerprint` (which made that field honest about
being a queue-slot uniqueness key, not content-level dedup — it still is not content-level dedup;
this doc is the actual content-level layer).

## What this phase is — and is not

This phase implements **deterministic, exact-match fingerprinting + cooldown windows**. It is:

- Real content-usage history (`StudentActivityUsageLog`), written on every completed activity.
- A deterministic content fingerprint (`IActivityContentFingerprintService`) computed from actual
  activity content (normalized JSON), not just pattern keys or session titles.
- A cooldown policy (`IActivityNoveltyPolicy`) that blocks exact-fingerprint, same-template, and
  same-topic/scenario repeats within a configurable window, unless explicitly labelled as an
  intentional review/remediation repeat.

It is **not**:

- Embedding-based or semantic near-duplicate detection. No pgvector, no vector search, no
  "this is basically the same scenario reworded" detection. Two activities with different
  wording but the same underlying idea will NOT be caught by this phase — only exact
  (post-normalization) content matches are caught.
- A replacement for `PracticeActivityCache.ContentFingerprint`. That field remains a **queue-slot
  uniqueness key** — computed before any content exists, satisfying a DB unique index so
  concurrent refill runs don't collide. It answers "is this queue row unique," not "has the
  student seen this content before." `StudentActivityUsageLog.ContentFingerprint` is the
  content-level signal; the two must not be confused.
- A migration of Today lessons or non-migrated Practice Gym patterns to the bank-first template
  path. Both continue to use the legacy `IAiActivityGenerator` freeform generation path exactly
  as before — this phase only adds a duplicate-content check around that existing generation,
  it does not replace it. See Phase C/D in the bank-first plan for that future work.

## Components

| Component | Layer | Purpose |
|---|---|---|
| `StudentActivityUsageLog` | Domain/Persistence | Append-only real content-usage history per student |
| `IActivityContentFingerprintService` / `ActivityContentFingerprintService` | Application/Infrastructure | Deterministic content fingerprint from actual activity JSON |
| `IActivityNoveltyPolicy` / `ActivityNoveltyPolicy` | Application/Infrastructure | Cooldown check: allowed/blocked + reason |
| `NoveltyPolicySettings` | Infrastructure | Code-level cooldown-window defaults, optionally overridable via the `Novelty` config section — no admin UI in this phase |

## `StudentActivityUsageLog`

One row per real consumption event (not per generation attempt). Fields: `StudentProfileId`,
`LearningActivityId`/`StudentActivityReadinessItemId`/`SourceTemplateId`/`SourceBankItemId`
(provenance), `PatternKey`/`ActivityType`/`Skill`/`Subskill`/`CefrLevel`/`CurriculumObjectiveKey`
(classification snapshot), `ContentFingerprint` (required), `TopicKey`/`ScenarioKey`/`PassageKey`/
`PromptKey`/`ContextTagsJson`/`FocusTagsJson` (content-identity, populated where available —
**not yet populated in this phase**, see Limitations), `IsIntentionalReview`/`ReviewReason`,
`ConsumedAtUtc`/`CreatedAt`.

A partial unique index `(StudentProfileId, LearningActivityId) WHERE LearningActivityId IS NOT
NULL` enforces idempotency at the DB level — a retried/duplicate completion for the same
activity cannot create a second row. `ActivitySubmitHandler.TryWriteUsageLogAsync` also checks
before inserting, as a first line of defense; both together are documented at
`ActivitySubmitHandler.cs`.

Indexes: `(StudentProfileId, ConsumedAtUtc)`, `(StudentProfileId, ContentFingerprint)`,
`(StudentProfileId, SourceTemplateId)`, `(StudentProfileId, PatternKey)`,
`(StudentProfileId, TopicKey)`, `(StudentProfileId, ScenarioKey)` — the exact set the novelty
policy's queries need, plus the unique idempotency index above.

## Fingerprint normalization (documented limitations)

`ActivityContentFingerprintService` handles two known content shapes — `ModuleStageSchema`
(legacy `LearningActivity.AiGeneratedContentJson`) and `FormIoSchema`
(`LearningActivity.FormIoSchemaJson` / `ActivityTemplate.FormIoBaseSchemaJson`) — with the same
generic normalization: recursively sort object keys, strip a small fixed list of volatile
property names (`id`, timestamps, correlation/request IDs, generation-provenance fields),
collapse whitespace, and lowercase string leaves. The normalized JSON is combined with any
supplied metadata (pattern key, skill/subskill/CEFR, topic/scenario/passage/prompt keys) and
SHA-256 hashed.

Known limitations:
- **Exact-match only.** Reworded-but-equivalent content produces a different fingerprint.
- **Callers must never pass submission data.** The service takes whatever `ContentJson` it's
  given at face value — it does not (and cannot) detect if a caller mistakenly passes
  `ActivityAttempt.SubmittedAnswerJson` instead of activity content. Every current caller
  (`ActivitySubmitHandler`, `PracticeGymGenerationJob`, `ActivityMaterializationJob`) only ever
  passes `LearningActivity`/generation-result content, never submission data.
- **Invalid/missing JSON falls back to a lower-fidelity fingerprint** (plain trimmed/lowercased
  text hash, or metadata-only if there's no content at all) rather than throwing. This
  degrades content-level dedup to text- or metadata-level dedup in that fallback case.
- **`TopicKey`/`ScenarioKey`/`PassageKey` are not yet populated from content.** The fingerprint
  request and the usage log both support these fields, and the novelty policy already enforces
  cooldowns on them when present — but no code in this phase extracts a topic/scenario/passage
  key out of `ModuleStageSchema`'s `practiceContent.scenario`/`practiceContent.task` fields (or
  the Form.io equivalent). This is real, designed-in infrastructure with no populator yet — a
  natural next increment, not a blocker for this phase's fingerprint+cooldown enforcement (which
  works today purely on `ContentFingerprint` and `SourceTemplateId`).

## Novelty policy

`ActivityNoveltyPolicy.CheckAsync` checks, in order, unless `IsIntentionalReview` is true (which
bypasses everything immediately):

1. Exact `ContentFingerprint` match within `FingerprintCooldownDays` (default 60) — the strongest
   signal, blocked longest by design (an exact repeat is close to a generation bug).
2. Same `SourceTemplateId` (bank-first path only) within `TemplateCooldownDays` (default 3).
3. Same `TopicKey` within `TopicCooldownDays` (default 7), if present.
4. Same `ScenarioKey` within `ScenarioCooldownDays` (default 7), if present.

`IsIntentionalReview` should be set true when routing labelled the request Review/Scaffold/
Remediation (`RoutingReason`) — deliberate spaced-review/remediation repeats are allowed by
design, not treated as accidental duplicates.

Defaults live in `NoveltyPolicySettings`, optionally overridable via the `Novelty:` config
section. No admin UI was built for this in Phase B, per scope.

## Integration points

- **Usage logging**: `ActivitySubmitHandler.TryWriteUsageLogAsync`, called from both the legacy
  and pattern-evaluation completion paths, right alongside the existing
  `TryConsumeReadinessItemAsync`. Best-effort — logs and swallows any exception, never blocks or
  fails activity submission.
- **Practice Gym bank-first template path** (`PracticeGymGenerationJob.TryMaterializeFromTemplateAsync`):
  checks the template cooldown *before* spending an AI call (using a synthetic
  `template-precheck:{templateId}` fingerprint so only the template-cooldown branch can match),
  then checks the real content fingerprint *after* generation, retrying generation up to
  `MaxTemplateGenerationAttempts` (2) times before falling back to the standard freeform path.
  As of Phase C2 (2026-07-08) this applies to 7 pattern keys
  (`TemplateMigratedPatternKeys` — the original pilot, C1's `phrase_match`,
  `gap_fill_workplace_phrase`, `reading_multiple_choice_single`, and C2's
  `reading_multiple_choice_multi`, `reading_fill_in_blanks`, `reading_writing_fill_in_blanks`),
  generalized from the single original pilot pattern. It still does not touch any pattern outside
  that set — see docs/architecture/practice-gym.md for the full list and safety gates.
- **Today lessons / legacy generation** (`ActivityMaterializationJob.MaterializeExerciseAsync`):
  builds an "avoid repeating" hint from the student's recent `StudentActivityUsageLog` history
  for the same pattern (last 14 days, by `TopicKey` — currently usually empty, see limitations
  above) and appends it to the AI prompt's `TopicHint`; after generation, checks the content
  fingerprint and retries up to `MaxGenerationAttempts` (2) times. If still blocked after
  retries, **serves the content anyway and logs a clear warning** — Today lesson generation must
  never be blocked by a novelty-check failure or exhausted retries.

Both job-level integrations fail open: any exception from the novelty check itself is caught and
treated as "allowed," so a bug in this new code cannot break existing generation.

## What was deliberately not built in this phase

- Embeddings / vector search / pgvector (explicitly out of scope).
- An admin UI for cooldown-window configuration.
- Topic/scenario/passage key extraction from content (infrastructure exists, no populator yet).
- Job-level integration tests exercising `PracticeGymGenerationJob`'s template-cooldown branch or
  `ActivityMaterializationJob`'s retry loop end-to-end — consistent with the existing project
  convention for this job (the 2026-07-07 Form.io pilot's own known gap: "no end-to-end
  integration test exercises PracticeGymGenerationJob's template branch directly ... judged low
  value relative to cost"). The underlying `ActivityNoveltyPolicy`/
  `ActivityContentFingerprintService` logic these branches call is fully unit-tested instead.
- Any change to Phase C (generalizing the Form.io template path across Practice Gym) or Phase D
  (bank-first Today lesson composer) — both remain not started.

## Relationship to Phase B2 (planned, not started)

Phase B implements **implicit, deterministic** repetition avoidance — content fingerprints and
cooldown windows computed automatically from usage history, with no student input. It does not
collect any explicit student-reported signal.

A separate, planned **Phase B2 — Activity Feedback, Repeat Policy, and Calibration Signals** (see
`docs/architecture/activity-feedback-and-calibration.md`) will add **explicit** student feedback
after an activity: difficulty (too easy / right level / too hard), clarity (clear / okay /
confusing), usefulness (useful / not useful), and a repeat/recommendation preference ("more like
this" / "I need to repeat this" / "don't show similar soon"), plus an optional comment.

Phase B2's repeat/recommendation signal is designed to eventually **feed into** this phase's
cooldown policy — e.g. "more like this" could shorten a topic/scenario cooldown, "don't show
similar soon" could lengthen one, and "I need to repeat this" could deliberately bypass a cooldown
as an intentional review request. **None of this is implemented yet.** This section documents the
intended relationship only; `ActivityNoveltyPolicy` today reacts solely to
`ContentFingerprint`/`SourceTemplateId`/`TopicKey`/`ScenarioKey` cooldowns as described above, with
no feedback-driven adjustment of any kind.
