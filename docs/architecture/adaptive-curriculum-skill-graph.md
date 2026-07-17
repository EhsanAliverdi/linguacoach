---
status: draft
lastUpdated: 2026-07-17
owner: architecture
supersedes:
supersededBy:
---

# Adaptive Curriculum — Skill Graph + Goal Vector + AI Composer

Target architecture for replacing the current flat curriculum-objective model. Not yet implemented — this is a locked-in design direction, agreed in a product/architecture discussion, meant to guide the phased implementation that follows.

Related: `docs/architecture/curriculum-routing.md` (current, flat-objective implementation — this doc replaces it), `docs/architecture/readiness-pool.md` (superseded), `docs/reviews/2026-07-17-today-delivery-health-bank-first-rehaul-review.md`, `docs/architecture/bank-first-admin-backend-surface-audit.md`.

---

## Context / decision

The user's product goal: replace a human English teacher entirely, not build a flashcard app. A real teacher builds a curriculum around why a student is learning ("I want to improve my work English"), sequences grammar/skills properly, and re-plans when the student's goal shifts ("now I want travel English") without starting over. The bar is explicitly "better than Duolingo," not "good enough." Human intervention should be minimal and concentrated at the import/content-authoring stage; bank generation is expected to get smarter over time without re-architecting this layer. The user explicitly framed this as a decision that cannot be revisited later, so it was treated as a full architecture discussion rather than a quick fix.

### Why the current model doesn't reach that bar

Investigation this session (see the two AskUserQuestion exchanges and prior review docs) found the current system conflates two orthogonal concerns into one field:

1. **Skill/grammar progression** — a real prerequisite order (present tense before conditionals). CEFR level is a coarse proxy for this; `Module.DifficultyBand` refines it slightly.
2. **Topic/theme** — Work English, Travel English, day-to-day chat. These don't need strict ordering and a single module can legitimately serve several themes at once.

`CurriculumObjective` + `Module.ObjectiveKey` tries to be both, and does neither well:
- `Module.ObjectiveKey` is a single free-text string, not a many-to-many relationship — one module cannot belong to multiple objectives even though that's the normal case (a "day-to-day informal" module and a "workplace small talk" module can easily be the same content).
- It's only set at manual Module creation (`ModuleCommandHandlers.cs`), never by import/AI/auto-link, and cannot even be edited afterward — the update handler doesn't include it.
- It has no picker or reverse-lookup UI — an admin has to type a string by hand with nothing validating it matches a real `CurriculumObjective.Key`.
- **Neither `TodayPlanModuleSelectionService` nor `PracticeGymModuleSelectionService` ever reads `CurriculumObjective` at all.** Delivery today runs purely on `CefrLevel`/`Skill`/tags. `CurriculumObjective` only feeds `LearningPlanService`/`StudentMasteryEvaluationService`/`CurriculumRoutingService` — the progress-narrative layer, not delivery.

So today's system already proves tags-and-CEFR selection works mechanically, but it has no concept of a student's actual goal, and no real prerequisite graph — it's neither a proper skill sequence nor a proper theme system.

---

## Target architecture

### 1. Skill graph (replaces the flat `CurriculumObjective` list)

Nodes are discrete teachable competencies: grammar structures, functional-language chunks ("ordering food"), vocabulary sets, subskills ("listening for gist"). Edges are prerequisite relationships — a true DAG, not a flat enum. CEFR level becomes a coarse readout of position in the graph, not the primary driver.

**Authoring (decided):** AI drafts nodes and prerequisite edges from CEFR frameworks and the existing imported content; a human reviews/approves once per batch, not per node. This keeps ongoing human effort near the "import only" bar the user set, while keeping a safety gate on prerequisite logic — the piece most costly to get wrong.

### 2. Per-node mastery (replaces per-objective-key mastery)

Every attempt updates a probability-style mastery estimate per skill-graph node per student, not per hand-typed objective key. This extends `StudentMasteryEvaluationService`'s existing classification math (`ComputeSignal`/`ClassifyStatus`) — the mechanism is proven, it just needs to operate over graph nodes instead of free-text keys, and one content item needs to be able to touch multiple nodes.

### 3. Goal vector (replaces a single "active goal" field)

Not a single enum switch. A weighted set — e.g. Work 0.7, Travel 0.3.

**Evolution (decided):** explicit + implicit blend. The student states/edits goals directly (onboarding and anytime after); weights also drift from real engagement and performance signals. A goal is never hard-deleted when its weight drops — it can resurface later without the student re-declaring it. This is what makes "I want to switch to travel for a while" a reweighting, not data loss.

### 4. Content carries two independent, many-to-many tag sets

- Which skill-graph nodes a module/lesson/resource teaches or practices.
- Which goal/theme tags it's relevant to (Work, Travel, DayToDay, ExamPrep, …).

Both are AI-generated at import/authoring time and can be re-generated later as import AI improves, without touching the graph or the mastery model — this is the mechanism that satisfies "bank generation should get a lot smarter over time" without re-architecting anything above it.

### 5. AI composer (replaces the current CEFR+tag selector filter)

A planner, not a filter query. Each session it reasons over: skill-graph gaps in valid prerequisite order, goal-vector relevance, forgetting-curve/spacing signals, and novelty — then composes the next best content sequence. This is the actual "AI teacher" layer; everything above it is just the data the teacher reasons over. `TodayPlanModuleSelectionService`/`PracticeGymModuleSelectionService` are replaced or subsumed by this component, not extended in place.

---

## Why goal-switching becomes free

Mastery lives on the skill graph, which is goal-independent. Switching or blending goals is purely a reweighting of the goal vector — no mastery data is discarded, no re-architecture is needed. This is the concrete mechanism behind "after several work-English lessons, the student can shift toward travel English without losing progress."

---

## Decisions made (AskUserQuestion, 2026-07-17)

1. **Skill-graph authoring:** AI drafts, human approves once per batch (not fully autonomous, not fully hand-curated).
2. **Goal vector evolution:** explicit input blended with implicit engagement signal, weights drift rather than reset, no goal is ever silently discarded.

---

## Implementation phasing (proposed, not started)

This is foundational and touches mastery, learning plan, and delivery at once — it should not land as one giant change. Proposed phase breakdown for a future planning pass:

1. **Skill graph foundation** — new entity/schema for graph nodes + prerequisite edges; AI-assisted seed generation from CEFR frameworks + existing bank content; admin approval UI (batch review, not per-node curation).
2. **Content re-tagging** — extend Module/Lesson/Exercise (and Resource Candidate import) to carry many-to-many node coverage + many-to-many goal/theme tags, AI-generated at import/authoring time. Backfill existing content.
3. **Mastery model upgrade** — extend `StudentMasteryEvaluationService` to operate per graph node instead of per objective key; migrate/backfill existing mastery data where mappable.
4. **Goal vector on student profile** — new field(s) replacing the current single-goal framing; onboarding capture + in-app editing; implicit-weight drift mechanism.
5. **AI composer** — the real replacement for `TodayPlanModuleSelectionService`/`PracticeGymModuleSelectionService`'s current filter logic; likely the largest and most novel piece, probably its own multi-pass implementation.
6. **Retirement** — once the composer is live and proven, retire `CurriculumObjective`/`Module.ObjectiveKey`/the old selectors, following this codebase's established "prove replacement first, retire after" discipline (see Phase I2's readiness-pool retirement for precedent).

Each phase should get its own scoping/plan pass before implementation, per this repo's usual phase discipline — this doc fixes the target, not the step-by-step build order.

---

## Risks / unresolved questions

- Skill-graph size/granularity is undetermined — too coarse loses the sequencing value, too fine becomes an unmaintainable graph even with AI-assisted authoring. Needs a concrete node-count target before Phase 1 starts.
- The AI composer (Phase 5) is a genuinely novel component with no direct precedent in this codebase — it deserves its own dedicated design pass, not a quick extension of the existing selectors.
- Backfilling mastery data from the old per-objective-key model onto graph nodes may be lossy or ambiguous for existing students; needs an explicit migration decision when Phase 3 is scoped.
- No student-facing UX for the goal vector has been designed yet (how goals are presented, edited, and explained to the student).

## Final verdict

Direction locked: skill graph + per-node mastery + weighted goal vector + AI composer, replacing the flat objective/tag/CEFR filter model. This is a foundational, deliberately hard-to-reverse decision per the user's explicit framing, and is judged to be the correct bar for a "replace the human teacher" product rather than a tags-and-filter shortcut.

## Next recommended action

Scope Phase 1 (skill-graph foundation) as its own plan before writing any code — in particular, nail down node granularity/count target and the AI-draft-then-human-approve workflow shape.
