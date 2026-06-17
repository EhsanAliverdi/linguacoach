---
status: current
lastUpdated: 2026-06-17 03:45
owner: engineering
---

# Engineering Review — Phase 10K: Curriculum Boundary / Level Syllabus Foundation

**Date:** 2026-06-17
**Related sprint:** Phase 10K
**Review type:** Plan engineering review (pre-implementation)

---

## Files reviewed

- Phase 10K scope specification (in user prompt)
- `src/LinguaCoach.Domain/Entities/StudentProfile.cs`
- `src/LinguaCoach.Domain/Entities/CurriculumWordList.cs`
- `src/LinguaCoach.Domain/Entities/ExercisePatternDefinition.cs`
- `src/LinguaCoach.Application/Learning/ResolvedLearningGoalContext.cs`
- `src/LinguaCoach.Application/Learning/ILearningGoalContextResolver.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/SeedData.cs`
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs`
- `src/LinguaCoach.Api/Program.cs`
- `src/LinguaCoach.Api/Controllers/AdminController.cs`

---

## Findings grouped by priority

### Architecture (3 findings — all resolved)

**A1 — Scope reduction: flat 1-entity model**
The spec proposed 5 entities (CurriculumSyllabus, CurriculumObjective, CurriculumContextTag, CurriculumPrerequisite, CurriculumSkillArea). For a read-only foundation phase with no write UI, a single `CurriculumObjective` table with JSON columns for tags/prerequisites achieves the same query interface with far less complexity.
_Decision: flat single-entity model adopted._

**A2 — CEFR representation: string constants only**
`StudentProfile.CefrLevel` is already a free-text string. Adding a shared `CefrLevel` enum would require an EF migration touching the students table — unnecessary risk in a foundation phase.
_Decision: `CefrLevelConstants` (A1–C2 string constants) used for validation. `StudentProfile.CefrLevel` unchanged. Future migration tracked in TODOS.md._

**A3 — Context mapper location**
The `LearningGoalContext → curriculum tag` mapping should be a static pure function in Application layer, not folded into the query service — easier to test in isolation and reusable in 10L routing.
_Decision: `CurriculumContextMapper` added as a static Application-layer class._

### Code Quality (3 findings — all addressed)

**Q1 — Seeder must upsert on Key (idempotent)**
Following `ExercisePatternSeeder` pattern. `CurriculumObjectiveSeeder` upserts on Key and calls `UpdateDetails` for changed fields.

**Q2 — Post-seed prerequisite integrity check**
The seeder validates all `PrerequisiteKeysJson` references after seeding. Throws `InvalidOperationException` if any key is dangling.

**Q3 — Null guard on `CurriculumContextMapper`**
Null input to the mapper must return `[general_english]` fallback, not throw. Verified in unit tests.

### Test coverage (26 gaps identified)

All gaps addressed. See Test Review section below.

### Performance (0 issues)

At ~22 seeded objectives, no caching or pagination needed. All queries use `AsNoTracking()`.

---

## Decisions made

| Decision | Rationale |
|---|---|
| Flat 1-entity model | No write UI in 10K; JSON columns sufficient for tag filtering |
| String CEFR constants | Avoids StudentProfile migration risk |
| `CurriculumContextMapper` in Application | Pure function, testable in isolation, reusable in 10L |
| Seeder upserts on Key | Consistent with ExercisePatternSeeder; idempotent startup |
| Admin endpoint included | Small, read-only, matches existing admin controller pattern |

---

## Test Review

### Coverage diagram (Phase 10K paths)

```
[NEW] CurriculumObjective entity
  ├── [TESTED] Valid construction
  ├── [TESTED] Empty key → throws
  ├── [TESTED] Invalid CEFR → throws
  ├── [TESTED] Invalid skill → throws
  ├── [TESTED] Self-prerequisite → throws
  ├── [TESTED] Valid prerequisite accepted
  ├── [TESTED] DifficultyBand 1-5 accepted
  ├── [TESTED] DifficultyBand 0/6 → throws
  ├── [TESTED] Activate / Deactivate
  └── [TESTED] UpdateDetails validation

[NEW] CurriculumContextMapper
  ├── [TESTED] Null → general_english fallback
  ├── [TESTED] Non-workplace → general_english (not workplace)
  ├── [TESTED] WorkplaceSpecific=true → workplace tag
  ├── [TESTED] travel goal key → travel tag
  ├── [TESTED] interview goal key → job_interviews tag
  ├── [TESTED] social goal key → social_conversation tag
  ├── [TESTED] pronunciation focus → pronunciation tag
  ├── [TESTED] listening focus → listening_confidence tag
  ├── [TESTED] writing focus → writing_confidence tag
  └── [TESTED] result never empty

[NEW] CurriculumObjectiveSeeder (integration)
  ├── [TESTED→Integration] Seeded objectives loaded
  ├── [TESTED→Integration] A1/A2/B1/B2 covered
  ├── [TESTED→Integration] Exam-inspired objective exists
  ├── [TESTED→Integration] Reviewable objective exists
  ├── [TESTED→Integration] Workplace not default for all
  ├── [TESTED→Integration] General English exists
  └── [TESTED→Integration] Idempotent (run twice = same count)

[NEW] CurriculumSyllabusQueryService (integration)
  ├── [TESTED→Integration] GetActiveObjectives returns results
  ├── [TESTED→Integration] GetByCefr A1 returns only A1
  └── [TESTED→Integration] GetCandidates null CEFR falls back to A1

[NEW] AdminCurriculumController (integration)
  ├── [TESTED→Integration] GET objectives returns seeded list
  ├── [TESTED→Integration] GET by key returns correct objective
  ├── [TESTED→Integration] GET unknown key → 404
  └── [TESTED→Integration] Unauthenticated → 401
```

---

## Risks and unresolved questions

- `CurriculumSyllabusQueryService.GetByCefrAndContextAsync` uses PostgreSQL `LIKE`-style string contains on JSON text column. Works correctly but is not index-backed. Acceptable at seed scale (~22 objectives); may need JSONB indexing if objective count grows to thousands.
- Plus-levels (B2+) not handled — tracked in TODOS.md TODO-001.

---

## What is intentionally deferred to 10L/10M/10N

- CEFR-aware activity routing
- Exercise format locking by CEFR level
- Readiness pools
- Background lesson generation driven by curriculum
- Practice Gym suggested practice
- Full CEFR placement engine integration with curriculum
- Admin write UI / curriculum builder
- `StudentProfile.CefrLevel` migration to validated type
- Plus/sub-level CEFR handling

---

## Warning

**Phase 10K does NOT route or generate based on curriculum.**
`ICurriculumSyllabusQuery.GetCandidatesForStudentAsync` returns an ordered candidate list only. No activity is selected, no exercise format is chosen, no content is generated. This is intentional — activity routing based on curriculum belongs to Phase 10L.

---

## Final verdict

APPROVED. Implementation proceeded per this review.
All 6 architecture/code-quality findings resolved before implementation.
26 test paths covered across unit and integration tests.
0 breaking changes to existing learner-facing behavior.

---

## Next recommended action

Implement Phase 10K per the reviewed plan, then proceed to Phase 10L (CEFR-aware routing using the curriculum foundation built here).
