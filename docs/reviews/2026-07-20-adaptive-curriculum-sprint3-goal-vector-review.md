# Adaptive Curriculum Sprint 3 — Goal Vector (Explicit + Implicit)

**Date:** 2026-07-20
**Related sprint / feature:** Sprint 3 of the multi-sprint Adaptive Curriculum initiative — see `docs/architecture/adaptive-curriculum-skill-graph.md`, and Sprint 1/2 review docs for the skill graph foundation and content re-tagging this sprint builds alongside. Still additive only — Today/Practice Gym selection does not consume the goal vector yet (that's Sprint 5).

## Context / decision

Per the target architecture, Sprint 3 builds a per-student weighted goal vector: explicit input (student says "I want Work at 70%") blended with implicit drift (weight nudges up when the student engages with goal-tagged content). Two research findings materially shaped this sprint's scope, both confirmed before writing code and both resolved via explicit user decisions:

1. **No implicit-engagement signal existed yet.** `StudentActivityUsageLog.ContextTagsJson` was built for exactly this but `ActivitySubmitHandler` always passed `null`. Decision: wire it now, not deferred to a follow-up sprint.
2. **A second, unrelated "goals" concept already existed on `/profile`.** `StudentProfile.LearningGoals` (a free-text-list field, student-editable via a chip UI) uses a completely different key vocabulary (`work`, `study`, `migration`, `job_interview`, `social`, `pronunciation`, `listening_confidence`, `writing_confidence`, `exam_inspired_practice` — seeded independently in `OnboardingFlowSeeder.cs`) than `CurriculumContextTagConstants.GoalTags` (`workplace`, `study_academic`, `migration_settlement`, `job_interviews`, `social_conversation`, ...) — the taxonomy actually tagged onto approved Module content since Sprint 1/2. The old field was confirmed (prior research) to feed no generation/planning logic. Decision: replace the old chip UI with the new weighted vector now, including a one-time backfill of existing students' data, rather than running two parallel "what are your goals" concepts.

## Files reviewed

`StudentProfile.cs` (full field list, nullability, `UpdateLearningPreferences` signature), `ModuleLessonLink.cs`/`ModuleSkillGraphNodeLink.cs` (join-table convention), `ActivitySubmitHandler.cs` (`TryWriteUsageLogAsync` — the actual write site, confirmed live not legacy), `StudentExerciseLaunch.cs` (the H10 bridge from `LearningActivity` back to the bank-first `Module`, `ModuleId`/`LearningActivityId` fields), `StudentActivityUsageLog.cs` (`ContextTagsJson` column, confirmed unpopulated before this sprint), `ProfileController.cs` (self-service endpoint conventions), `profile.component.ts` (the existing chip-based Learning Goals UI being replaced), `CurriculumContextTagConstants.cs` (Sprint 2's `GoalTags` subset).

## What was built

### Backend

- **`StudentGoalWeight`** (`StudentId`/`GoalTag`/`Weight`/`Source`/`UpdatedAtUtc`) — real FK to `student_profiles` (cascade delete), unique `(StudentId, GoalTag)` index. `SetExplicitWeight` overwrites directly; `ApplyImplicitEngagement(alpha)` is a bounded EMA nudge toward 1.0 (`weight += alpha * (1 - weight)`) — a single activity can never push a goal past a shrinking fraction of its remaining gap to 1.0. Both write paths update the same row — the "explicit + implicit blend" decided earlier, not two scores merged later.
- **`IStudentGoalVectorService`/`StudentGoalVectorService`** — `GetGoalsAsync`, `SetExplicitWeightAsync` (validates against `CurriculumContextTagConstants.IsGoalTag`), `RecordImplicitEngagementAsync` (filters a content item's context tags down to real goal tags — silently ignores non-goal tags like "pronunciation" rather than erroring, since a Module's tag set legitimately mixes both). `ImplicitEngagementAlpha = 0.1` — fixed, not tuned per student, per the "bounded, testable, not open-ended ML" constraint from the architecture doc.
- **`ActivitySubmitHandler` wired**: traced the real path from a completed attempt back to its bank-first Module — `LearningActivity.Id` → `StudentExerciseLaunch.LearningActivityId` → `Module.ContextTagsJson` (a plain JSON array, the same shape used throughout this codebase) — populates `StudentActivityUsageLog.ContextTagsJson` (previously always null) and calls `RecordImplicitEngagementAsync`, wrapped in its own try/catch isolated from the usage-log write so a drift failure can never block a student's real attempt from saving. Legacy-generated activities (no `StudentExerciseLaunch` row) simply yield no context tags, same as before.
- **`ProfileController` extended**: `GET /api/profile/goals` (own vector + the 8-tag taxonomy, so the UI can show unset goals too), `PUT /api/profile/goals/{goalTag}` (explicit set, 400 on an unrecognized/non-goal tag).
- **`IStudentGoalVectorBackfillService`/`StudentGoalVectorBackfillService`** — one-time, idempotent backfill mapping old `LearningGoals` keys to the new taxonomy (`day_to_day→DayToDay`, `travel→Travel`, `work→Workplace`, `study→StudyAcademic`, `migration→MigrationSettlement`, `job_interview→JobInterviews`, `social→SocialConversation`; `pronunciation`/`listening_confidence`/`writing_confidence`/`exam_inspired_practice` deliberately left unmapped — skill/format descriptors, not motivations). Backfilled weight starts at 0.6, not 1.0 — a real signal, but an invitation to adjust, not a finished answer. New `AdminGoalVectorController` (`POST /api/admin/goal-vector/backfill-from-learning-goals`) triggers it.
- Migration `Sprint3_AddStudentGoalWeights` via `dotnet ef migrations add` — one new table, no existing table touched (the old `LearningGoals` column stays in place; onboarding still writes to it, out of scope to touch this sprint per the earlier "in-app editor only" decision).

### Frontend

`/profile`'s old "Learning goals" chip section (free-text keys, custom-goal text input) replaced with "My Goals": one slider per recognized goal tag, live-saving on change via the new endpoints, independent of the page's bulk "Save" button (goal weights save individually per tag, not bundled into `UpdateLearningPreferencesRequest`). `learningGoals`/`customLearningGoal` removed from the component's form model and save payload (backend DTO fields left in place, unused by this UI — deliberately bounded scope, not touching onboarding's write path).

### Tests

67 new tests, all passing: 9 `StudentGoalWeight` entity tests (construction/validation, `SetExplicitWeight`, `ApplyImplicitEngagement` bounds including a 1000-iteration never-exceeds-1 check), 10 `StudentGoalVectorService` unit tests, 5 `StudentGoalVectorBackfillService` unit tests (mapping, unmappable-keys, idempotency, doesn't-overwrite-explicit), 15 `ProfileController` integration tests (goals GET/PUT, invalid/non-goal tag 400s, update-in-place, 401s), 3 `AdminGoalVectorController` integration tests, updated `profile.component.spec.ts` (13 tests rewritten from the old chip-model assertions to the new slider model).

## Migration

`dotnet ef migrations add Sprint3_AddStudentGoalWeights` — adds `student_goal_weights` only. Verified applied against the real dev Postgres.

## Validation

- `dotnet build --configuration Release`: 0 errors.
- `dotnet test --configuration Release`: **3,929 / 3,929 passing, 0 failing** (30 architecture + 2,539 unit + 1,360 integration).
- `npm run build -- --configuration production`: exit code 0, 0 `[ERROR]` entries.
- `npx ng test --include='**/profile.component.spec.ts'`: could not run to completion — same pre-existing, unrelated 5-file karma blocker documented in prior reviews. Confirmed via targeted `ng test --include` that `profile.component.spec.ts` itself produces no compile errors — only the 5 pre-existing files are cited in the failure output.
- **Live deployment + real verification**: Docker Desktop was down mid-session (host-level, not a code issue) — paused, resumed once the user restarted it. Forced `--no-cache` rebuild (per the Sprint 1/2 lesson), confirmed the migration applied against the real dev Postgres and the new table exists, confirmed API healthy.
  - **Explicit set/get verified end-to-end live**: created a fresh test student, confirmed `GET /api/profile/goals` starts empty, `PUT .../travel {weight:0.8}` returns 204, subsequent `GET` shows `travel: 0.8, source: Explicit`.
  - **Backfill verified live against real data**: 6 students scanned, 2 had a mappable `LearningGoals` entry, 2 `StudentGoalWeight` rows created (`travel`, `workplace`, both 0.6/Explicit) — confirmed directly in Postgres. Re-ran the same call: `weightsCreated: 0, weightsSkippedAlreadySet: 2` — idempotency confirmed against real data, not just tests.
  - **Implicit drift wiring not exercised live** — same root cause Sprint 2 found: existing Modules are archived/have no launchable content with real goal tags, so there's no real attempt to submit through the full pipeline yet. The wiring itself (entity EMA math, service filtering, `ActivitySubmitHandler`'s Module-lookup query) is thoroughly unit-tested and the full solution builds/tests clean with it in place, but "does a real student's real attempt actually nudge a real weight" remains unverified end-to-end until real launchable content exists.

## Decisions made

1. Implicit drift wired now (not deferred) — required populating `StudentActivityUsageLog.ContextTagsJson` via the `StudentExerciseLaunch → Module` lookup, a real piece of new wiring beyond just the goal-vector entity itself.
2. In-app "My Goals" editor only — Form.io onboarding wizard left untouched this sprint, per the earlier explicit decision.
3. Old `LearningGoals` chip UI replaced now, not left running in parallel — includes a one-time, idempotent backfill with an explicit, documented key-mapping table; unmappable old keys (skill/format descriptors) are deliberately not guessed at.
4. Backend `LearningGoals`/`CustomLearningGoal` fields and their `UpdateLearningPreferences` DTO surface left in place (not deleted) — bounded scope, since onboarding still populates them and touching that write path was explicitly out of scope this sprint.
5. Goal weights are independent 0-1 relevance scores, not a probability simplex required to sum to 1 — simpler and robust to implicit drift adding new tags over time without needing to renormalize existing ones.

## Risks / unresolved questions

- Implicit drift's real-world behavior is unverified against an actual student attempt — flagged above, blocked on content availability (the same gap Sprint 2 found), not a defect in this sprint's code.
- The old `StudentProfile.LearningGoals`/`CustomLearningGoal` fields and their backend DTO surface are now write-only-from-onboarding, read-by-nothing-else — a genuine candidate for full removal (including from onboarding) in a later, dedicated pass once someone confirms nothing else depends on them.
- The whole-suite frontend karma run remains blocked by the same 5 pre-existing broken spec files — still unresolved, still out of this sprint's scope.
- `BackfillFromLearningGoalsAsync` loads every `StudentProfile`'s `LearningGoals` column into memory before filtering client-side (a SQLite/EF JSON-column translation limitation, same category of issue found in earlier sprints) — fine at current student volume for a one-time admin-triggered call, would need a different approach at much larger scale.

## Final verdict

Sprint 3 complete and verified: 0 backend build errors, 3,929/3,929 tests passing, 0 new frontend build errors, migration applied cleanly against the real dev database. Explicit goal-setting and the data backfill are both verified working end-to-end against live data. The implicit-drift wiring is correctly built and thoroughly unit-tested but not yet exercised by a real student attempt, due to the same thin-content gap Sprint 2 already flagged for Sprint 6.

## Next recommended action

Sprint 4 (per-node mastery) can begin — it doesn't depend on goal-vector data. Separately, whenever real launchable content exists (Sprint 6, or an earlier ad-hoc content pass), do a real end-to-end check: complete a real activity attempt for a goal-tagged Module and confirm the corresponding `StudentGoalWeight` row actually moves.
