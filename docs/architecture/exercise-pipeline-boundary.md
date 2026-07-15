---
status: current
lastUpdated: 2026-07-15 (Phase 2)
owner: architecture
supersedes:
supersededBy:
---

# Exercise Pipeline Boundary — Final Content Creation Rule

**Established:** Phase 2, 2026-07-15 (`docs/reviews/2026-07-15-phase-2-exercise-pipeline-boundary-review.md`),
closing the gap the original architecture audit found
(`docs/reviews/2026-07-15-content-creation-pipeline-architecture-audit.md`).

## The rule

```text
Resources create Lessons.
Lessons create Exercises.
Exercises cannot be created directly from Resources.
Every Exercise has exactly one required instructional parent Lesson.
Exercises may retain links to the Lesson's source Resources for provenance.
```

Concretely:

```text
Resource Bank
  ↓
Lesson
  ↓
Exercise
  ↓
Module
```

- **Resource Bank items** provide source material (a definition, a passage, a prompt).
- **Lessons** convert that source material into an instructional knowledge unit — the thing a
  student learns.
- **Exercises** practise or assess a Lesson. Every Exercise's `LessonId` is a required, non-nullable
  `Guid` at the domain, EF configuration, and database level — this is not a convention, it is
  enforced.
- `ExerciseResourceLink` records still exist and are still populated on every generated Exercise —
  they are the *provenance* trail back to the Resource Bank row(s) the Exercise's content was
  drawn from. They do not replace `Exercise.LessonId` and are not a second way to create an
  Exercise; they are only ever written as a side effect of a Lesson-based generation call.

## What this does not change

- **Resource Bank → Lesson** generation (`AdminLessonController`, `LessonGenerationService`,
  `AiLessonGenerationService`) is unaffected — a Lesson is still generated directly from selected
  Resource Bank rows.
- **Resource Bank → Module** direct generation (`ModuleGenerationService.HandleAsync(GenerateModuleFromResourceRequest)`)
  is unaffected — untouched, out of scope for Phase 2.
- **Lesson → Module** and **Exercise → Module** linking/auto-linking is unaffected.

## Enforcement

- `Exercise.LessonId` — `Guid` (domain), `IsRequired()` (EF), non-nullable column (Postgres
  migration `Phase_2_RequireExerciseLessonId`).
- `IGenerateActivityFromLessonHandler` (deterministic) and `IGenerateActivityFromLessonWithAiHandler`
  (AI-assisted) are the only two Exercise-generation entry points. Both take a
  `GenerateActivityFromLessonRequest` (a `LessonId`, never a raw resources list) and resolve the
  Lesson's own `LessonResourceLink` rows internally.
- `tests/LinguaCoach.ArchitectureTests/ExercisePipelineBoundaryTests.cs` fails the build if a type
  or endpoint matching the removed direct-generation pattern is reintroduced anywhere in
  `Domain`/`Application`/`Infrastructure`/`Api`.

## History

- **Phase H4 (2026-07-xx):** Exercise foundation introduced with *two* generation entry points —
  direct-from-Resources and from-Lesson — by original design, mirroring Lesson generation's own
  two entry points at the time.
- **2026-07-15, architecture audit:** flagged the direct-from-Resources path as a structural risk
  ("Exercise ← Resources" was still reachable via API even though the current UI never used it) and
  as the root cause of a confirmed data-integrity bug (some Lesson-originated AI-preferred Exercise
  types ended up with `LessonId == null`).
- **Phase 1 (2026-07-15):** fixed the immediate data-integrity bug (Lesson ID dropped for
  AI-preferred types) without removing the direct path — narrowly scoped safety fix.
- **Phase 2 (2026-07-15, this phase):** removed the direct-from-Resources path entirely, made
  `Exercise.LessonId` mandatory, and added the architecture guard above.
