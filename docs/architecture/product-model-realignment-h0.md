---
status: current
lastUpdated: 2026-07-09 (Phase H0)
owner: product
supersedes:
supersededBy:
---

# Product Model Realignment — Content Studio, Learn, Activity, Module, Lesson, Practice Gym

**Phase H0, 2026-07-09. Docs-only.** No code, migration, entity, API, Angular, or test change was
made in this phase. This document defines the intended product model, audits the current mismatch
against it, and proposes a safe phased implementation path (the H-track). It does not implement
any of it.

---

## 1. Why this phase exists

D6 closed the bank-first selector-quality track (E6→E10, D1→D6): Today lessons now pull
context/subskill/topic-aware bank content through a deterministic relaxation ladder. That work is
real and stays. But it was all built as **selector engineering on top of the existing admin/data
model** — `ResourceCandidate` → typed `Cefr*` bank tables → `ActivityMaterializationJob` prompt
injection. The admin/product model itself never caught up: there is no `Learn Item` concept, no
`Module` concept, admin still sees many separate technical bank/source/candidate pages, and
"Activity" and "Template" are used inconsistently. Continuing to invest in selector-only work
(more relaxation rungs, more metadata columns) would compound that mismatch rather than close it.

This phase defines the target model — **Resource → Learn/Activity → Module → Daily Lesson /
Practice Gym → Attempt → Feedback + Rating → Learner Memory** — and a safe, incremental path from
today's state to it, without deleting or destabilizing anything that works today (Today fallback,
Practice Gym fallback, readiness/delivery queue, D1–D6 selector logic).

---

## 2. Intended product model

```
Resource Bank Item
      │
      ├──► Learn Item
      │
      └──► Activity  (optionally linked to a Learn Item)
                │
                ▼
             Module  (Learn + Practice + Feedback)
                │
        ┌───────┴───────┐
        ▼               ▼
  Daily Lesson     Practice Gym
        │               │
        └───────┬───────┘
                ▼
             Attempt
                │
                ▼
       Feedback + Rating
                │
                ▼
       Learner Memory / Mastery
```

### Resource Bank Item

A raw, approved learning content item, imported or created by admin/AI. Not a student-facing
activity — a substrate. May be any type: vocabulary, grammar, reading, listening, speaking,
writing, examples, prompts, passages, transcripts. Admin should experience this as **one unified
Resource Bank with typed rows**, not many separate bank pages.

Today's equivalent: `CefrVocabularyEntry` / `CefrGrammarProfileEntry` / `CefrReadingReference` /
`CefrReadingPassage`, published via `ResourceCandidate` → `ResourceCandidatePublishService`. This
*is* the Resource Bank Item concept already — the gap is presentation (many typed admin pages, no
unified surface), not the underlying data shape.

### Learn Item

The teaching/explanation part generated from one or more resources.

Fields: title; body/explanation; examples; common mistakes (where useful); linked source resource(s);
CEFR; skill; subskill; context tags; focus tags; difficulty; approval status; rating/quality signals.

Today's equivalent: **does not exist.** `ActivityMaterializationJob`'s bank-content prompt block
(D1–D6) is the closest analog, but it is a transient, per-request AI prompt fragment, not a stored,
admin-reviewable, reusable entity with its own lifecycle.

### Activity

A student exercise/task generated from one or more resources, optionally linked to a Learn Item.
Examples: gap fill, multiple choice, sentence correction, rewrite sentence, short writing, reading
comprehension, listening comprehension, speaking prompt.

Fields (Form.io-supported activities): Form.io schema/config; prompt/instructions; answer/scoring
rules; feedback rules/plan; linked resource rows; optional linked Learn Item; CEFR; skill; subskill;
context tags; focus tags; difficulty; approval status; rating/quality signals. Admin-editable.

Today's equivalent: `ActivityTemplate` (Form.io-native, admin CRUD, review/publish workflow — 8 of
33 Practice Gym pattern keys migrated as of Phase C-Final) plus the always-fresh legacy
`IAiActivityGenerator` path for everything else, plus `LearningActivity` as the per-student
materialized instance. `ActivityTemplate` is the closest existing analog to "Activity" in this
model — it is not being replaced, it is being reframed as the thing Resource Bank rows generate
into.

### Module

The core student learning unit: **Learn → Practice → Feedback.** Links Learn Item(s) and
Activity/Activities around the same objective/concept/tags.

Today's equivalent: **does not exist.** `CurriculumObjective` is the closest conceptual anchor
(an objective/concept key), but nothing packages a Learn Item + Activity/Activities + feedback plan
into one reviewable, assignable unit today.

### Daily Lesson

The daily student plan: several Modules selected for that day based on student level, weakness,
goals/context, available time, learning plan, and novelty/reuse controls.

Today's equivalent: Today's `LearningSession` → `SessionExercise` → `LearningActivity` chain,
composed by `ActivityMaterializationJob`/`TodayBankResourceSelector`. This is activity-first, not
module-first — the D6 selector picks bank resources and a pattern per exercise, not a Module.

### Practice Gym

Self-directed or weakness-based module practice. Student picks a skill area (speaking, listening,
writing, reading, grammar/vocabulary); the system offers suitable modules/practice based on
weakness, unseen activities, review needs, skill choice, and mastery state.

Today's equivalent: type-first Practice Gym (`ExercisePatternDefinition`/`ExerciseTypeDefinition`
catalog, `PracticeGymSuggestionService`), already planned to move toward skill-first via PG-v2
(see `docs/backlog/product-backlog.md`'s "Practice Gym v2" section) — but PG-v2 as currently
scoped selects an `ActivityTemplate`/resource/format directly, not a Module. This model implies
PG-v2's target should eventually be Module selection, not just Activity selection.

### Attempt, Feedback + Rating, Learner Memory / Mastery

At the end of a Module: the student submits attempt(s), receives feedback, learner memory/mastery
updates, and user ratings on Learn/Activity/Module influence future selection.

Today's equivalent: `ActivityAttempt` (attempt), the pattern evaluation engine + AI feedback
(feedback), `ActivityFeedbackSignal` (Phase B2 foundation — collected, not yet consumed by
calibration), `StudentSkillProfile`/`UserLearningSummary`/`StudentLearningEvent` (learner memory).
These layers exist and are **not changed by this phase** — the gap is that they operate at the
Activity level, not the Module level, because Module doesn't exist yet.

---

## 3. Intended import flow

```
Admin imports file/content/dataset
  → admin chooses broad resource category/type (vocabulary, grammar, reading, listening,
    speaking, writing, mixed/AI-detect)
  → AI analyzes input structure, maps columns/rows, detects CEFR if present, normalizes tags,
    proposes typed Resource Bank rows, flags warnings/ambiguous rows
  → rows go to Pending Review as Resource Bank candidates (not final student activities)
  → admin approves/rejects/edits
  → approved rows become published Resource Bank Items
  → admin selects one or many published Resource Bank rows
  → admin chooses: Generate Learn Item / Generate Activity / Generate Learn + Activity /
    Generate Module draft
  → AI generates the corresponding Learn/Activity/Module record(s), with strong
    tags (CEFR/skill/subskill/context/focus/difficulty) and approval status
  → generated records go to Pending Review
  → approved Learn/Activity/Module records become usable for Daily Lesson creation,
    Practice Gym module selection, Today selector/routing, and feedback/mastery updates
```

Non-negotiable properties of this flow, carried over unchanged from the existing E1–E4 pipeline:

- Import never immediately assigns content to students.
- Import never directly creates published final activities without review.
- Every row preserves source/import-run/raw-record references (`ResourceImportRun`/
  `ResourceRawRecord`/`ResourceCandidate` lineage — unchanged).
- AI helps structure and generate; **deterministic backend validation + admin approval control
  quality**, not AI judgment alone (unchanged E2 rule: AI analysis is advisory only).

This flow is already implemented for the Resource Bank Item stage (E1–E9). It is **not yet
implemented** for the "select rows → generate Learn/Activity/Module draft" stage — that stage does
not exist today (H3/H4/H5 below).

---

## 4. Unified Resource Bank — direction

**Decision: Option B — unified admin read model/API over existing typed tables, not immediate
physical consolidation.**

**Option A — physical unified table.** A single `ResourceBankItem` table with a `Type` discriminator
and a `StructuredJson` payload column. Pros: one real table, simplest long-term query shape. Cons:
destructive migration across `CefrVocabularyEntry`/`CefrGrammarProfileEntry`/`CefrReadingReference`/
`CefrReadingPassage` (and, later, `ActivityTemplate`); every existing query/selector/test that reads
those typed tables (`ResourceBankQueryService`, `TodayBankResourceSelector`, the D1–D6 selection
ladder, `ResourceCandidatePublishService`'s routing) would need to change in lockstep; high risk of
regressing D5/D6's just-stabilized selector behavior for a purely administrative UX win.

**Option B — unified admin read model over existing typed tables (recommended, near-term).** Add
one admin-facing Resource Bank API/page that queries across the existing typed tables (a thin
aggregation/read layer, similar in spirit to `ResourceBankQueryService`'s existing cross-type
browsing), exposing type/CEFR/skill/subskill/context/focus/difficulty/source/status/approval/
created-by/linked-Learn-Activity-Module filters — without moving any data. Old typed pages may
remain reachable under Advanced/Diagnostics during the transition (H8).

**Why Option B first:**
- Lower risk — no destructive migration, no forced rewrite of D1–D6's selector/publish logic.
- Preserves the just-stabilized, fully-tested selector/publish/relaxation-ladder code paths.
- Lets the admin UX become correct (one Resource Bank, not five pages) before any backend
  consolidation is attempted.
- Physical consolidation (Option A) can be evaluated later, once Learn/Activity/Module (H3–H5)
  exist and there is real evidence of what shape actually serves them best — deciding the physical
  schema before Learn/Activity/Module exist risks designing the wrong shape.

Do not require immediate physical DB consolidation.

---

## 5. Learn / Activity / Module / Lesson / Practice Gym — model requirements

### Learn Item (proposed fields)
title; body/explanation; examples; common mistakes; source resource links; CEFR; skill; subskill;
context tags; focus tags; difficulty; status; approval state; rating/quality signals.

### Activity (proposed fields)
title; activity type/pattern key; renderer type (Form.io/custom); Form.io schema/config where
applicable; prompt/instructions; answer/scoring rules; feedback rules/plan; source resource links;
optional linked Learn Item; CEFR; skill; subskill; context tags; focus tags; difficulty; status;
approval state; rating/quality signals.

### Module (proposed fields)
module title; objective/concept key; Learn item link(s); Activity link(s); feedback plan; CEFR;
skill; subskill; context/focus tags; difficulty; estimated time; status/approval state;
source/resource traceability.

### Daily Lesson (proposed fields)
student id; date/window; selected module ids; estimated total time; reason/routing metadata;
status lifecycle.

### Practice Gym (proposed fields)
student id; selectable skill area; module ids or module assignment ids; weakness/review reason;
novelty/reuse controls; status lifecycle.

### Feedback + Rating (behavior, not new fields)
At the end of a Module: student receives feedback based on attempts; learner memory/mastery is
updated; user ratings on Learn/Activity/Module influence future selection.

None of these are implemented in H0. H3 (Learn Item), H4 (Activity), and H5 (Module) are where
these become real entities.

---

## 6. Current-state mismatch audit

Reviewed: `docs/roadmap/road-map.md`, `docs/sprints/current-sprint.md`,
`docs/handoffs/current-product-state.md`, `docs/backlog/product-backlog.md`,
`docs/architecture/learning-activity-engine.md`,
`docs/architecture/english-resource-bank-import-platform.md`, `docs/architecture/README.md`,
`TODOS.md`. Code concepts inspected by name (not modified): `ResourceCandidate`,
`ResourceImportRun`, `ResourceCandidatePublishService`, `CefrVocabularyEntry`,
`CefrGrammarProfileEntry`, `CefrReadingReference`, `CefrReadingPassage`, `ActivityTemplate`,
`LearningActivity`, `StudentActivityReadinessItem`, the Today/Practice Gym materialization paths,
Form.io placement/onboarding.

| Current state | Target state |
|---|---|
| Many separate bank pages (`/admin/resource-banks/vocabulary`, `/reading-passages`, etc.) | One Content Studio Resource Bank with typed rows + filters |
| Technical source/import/candidate pages visible in main nav (reframed into "Content Banks" by G1, but still technical-first) | Import/review technical internals hidden under Advanced/Diagnostics |
| No `Learn Item` concept — teaching content is a transient AI prompt fragment, not a stored entity | `Learn Item` as a first-class, admin-reviewable, reusable entity |
| `Activity`/`Template` concepts mixed (`ActivityTemplate`, `ExercisePatternDefinition`, `ExerciseTypeDefinition`, `LearningActivity` all called "activity" in different contexts) | Clear `Activity` definition: admin-editable exercise generated from resources, optionally linked to a Learn Item |
| `Lesson`/Today concepts mixed with per-exercise activity materialization (`ActivityMaterializationJob` composes at the exercise level) | Module-first Today: a Daily Lesson is a bundle of Modules, not a bundle of independently-selected exercises |
| Practice Gym is activity-type-first (student picks a pattern name), PG-v2 (planned) is skill-first but still Activity-target, not Module-target | Practice Gym is module-first: student picks skill/weak-area/review/challenge, system selects Modules |
| Admin import UX exists (E1–E9 pipeline) but ends at published Resource Bank rows — no "generate Learn/Activity/Module from selected rows" step | Admin can select Resource Bank rows and generate Learn Item / Activity / Module drafts for review |
| Generated content lifecycle is Resource → (AI prompt injection at request time) → `LearningActivity` instance | Generated content lifecycle is Resource → Learn/Activity (stored, reviewable) → Module → Lesson/Practice Gym |

**Nothing in this table implies deleting any current mechanism.** The bank-first selector work
(D1–D6) remains useful substrate — it proves the selection/relaxation/novelty/CEFR-policy logic
that Module-level selection will eventually reuse. The mismatch is in the *admin/product IA and
the missing Learn/Module layer*, not in the underlying data or selection logic.

---

## 7. Proposed admin information architecture (target, not implemented)

```
Content Studio
  Import content
  Review generated content
  Resource bank
  Learn items
  Activities
  Modules

Learning Setup
  Onboarding
  Placement

Delivery
  Today lessons
  Practice Gym pipeline
  Student assignment / delivery queue

Advanced / Diagnostics
  Resource sources
  Import runs
  Candidate records
  AI operations
  Usage / cost
  Runtime settings
```

This supersedes G1's three-way nav split (Content Banks / Delivery / Learning Setup, done
2026-07-09) as the longer-term target — G1's split is a real, valid intermediate step (technical
import internals are still visible under "Content Banks" today), and **is not undone by this
phase.** H8 is where the nav actually changes to the structure above, once Learn/Activity/Module
pages exist to populate "Content Studio." Not implemented in H0.

---

## 8. Proposed phased implementation roadmap — the H-track

| Phase | Scope | Depends on |
|---|---|---|
| **H0 — Product Model Realignment** | Docs-only. This phase. | E10, D6 |
| **H1 — Unified Resource Bank Admin Read Model** | One admin-facing Resource Bank API/page over existing typed published bank tables (Option B, §4). No physical consolidation. Old typed pages may remain under Advanced temporarily. | H0 |
| **H2 — Import Content UX v1** | Admin upload/paste/import page; admin chooses broad type/category/default tags; AI analyze/mapping preview; creates pending Resource Candidates/Bank rows through the existing E1–E9 pipeline; async for large imports; no student assignment. | H1 |
| **H3 — Learn Item Foundation** | Introduce `Learn Item` entity/table/API/admin review; generate Learn Item from selected Resource Bank rows; approval lifecycle; tags/source traceability. | H2 |
| **H4 — Activity Foundation with Form.io** | Introduce/align `Activity` as an editable generated exercise (builds on existing `ActivityTemplate`); Form.io schema/config for supported types; answer/scoring/feedback plan; generated from selected Resource Bank rows; approval lifecycle; tags/source traceability. | H2 (parallel with H3) |
| **H5 — Module Foundation** | `Module` = Learn + Activity/Activities + Feedback Plan; create/generate module drafts from selected resources/Learn Items/Activities; approval lifecycle; objective/estimated-time metadata. | H3, H4 |
| **H6 — Daily Lesson Module Pipeline** | Daily Lesson contains several Modules based on student time/weakness/plan; preserve Today fallback until proven replacement; map existing Today materialization into the module-first model safely. | H5 |
| **H7 — Practice Gym Module Pipeline** | Practice Gym becomes skill/weakness/self-directed Module selection; uses approved Modules and unseen Activities; preserve legacy Practice Gym fallback until proven replacement. | H5 (may run alongside/after PG-v2A's selector work) |
| **H8 — Admin IA Simplification** | Move technical pages under Advanced/Diagnostics; make Content Studio the main admin surface (§7). | H1–H7 substantially landed |

**Not scheduled by this phase:** destructive cleanup of any kind. Phase F (legacy generation
retirement), G2/G3 (backend/diagnostics cleanup), and PG-v2 (skill-first Activity selector)
**remain later, sequenced after the H-track proves its replacement** — same discipline as every
prior bank-first phase (D1–D6, E1–E10, G0/G1).

**Relationship to PG-v2:** PG-v2A–D (backend skill-first selector, UI, capability-registry
cleanup, legacy retirement — see `docs/backlog/product-backlog.md`) is still a valid near-term
track and is not blocked by H0. It targets Activity selection given a skill/objective. H7 later
extends that pattern to Module selection. PG-v2 can proceed in parallel with early H-track phases
(H1–H4) if prioritized that way — that is a future decision, not made in this phase.

---

## 9. Recommended next phase

**H1 — Unified Resource Bank Admin Read Model.** Lowest risk (no schema/migration, read-only
aggregation over existing tables), highest immediate admin-UX value, and a safe first step that
does not require Learn/Activity/Module to exist yet. A PG-v2A/H1 sequencing decision (which comes
first) is a future Plan-Sync checkpoint, not resolved by this phase.

---

## 10. What this phase explicitly did not do

Docs-only. No migrations, entities, Angular changes, API changes, test changes, pushes, or
deploys. No bank tables deleted. No Today or Practice Gym fallback deleted. No readiness/delivery
queue deleted. No PG-v2 implementation started. No Content Studio implementation started. No
external datasets imported. No Persian/bilingual/support-language seed content added. No direct
seeding of final published bank tables.
