---
status: resolved
lastUpdated: 2026-06-15
owner: architecture
relatedSprint: docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md
relatedArchitecture: docs/architecture/learning-activity-engine.md#exercise-type-registry
---

# T44 Exercise Type Catalog Migration — Production Crash Root Cause and Fix

## Date

2026-06-15

## Related sprint

`docs/sprints/2026-06-15-staged-activity-content-migration-sprint.md` (landed
alongside the Practice Gym pre-generation pool foundation PR, per explicit
user instruction to fix this issue "along with other requirement").

## Trigger

User reported a Docker production log dump showing the API container in a
crash-restart loop (5+ repeated identical stack traces):

```
fail: Microsoft.EntityFrameworkCore.Query[10100]
Npgsql.PostgresException (0x80004005): 42P01: relation "exercise_type_definitions" does not exist
... at LinguaCoach.Persistence.Seed.ExerciseTypeDefinitionSeeder.SeedAsync ...
... at Program.<Main>$(String[] args) in /repo/src/LinguaCoach.Api/Program.cs:line 208
Unhandled exception.
```

## Files reviewed

- `src/LinguaCoach.Persistence/Migrations/20260615090000_T44_ExerciseTypeCatalog.cs`
- `src/LinguaCoach.Persistence/Migrations/20260615090000_T44_ExerciseTypeCatalog.Designer.cs` (absent before fix)
- `src/LinguaCoach.Persistence/Migrations/LinguaCoachDbContextModelSnapshot.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Api/Program.cs`

## Findings

### Critical

- The `T44_ExerciseTypeCatalog` migration's `.cs` file (with `Up()`/`Down()`)
  existed and was committed (commit `5ed8fd3`), but its companion
  `.Designer.cs` — which carries the `[Migration("...")]` and
  `[DbContext(...)]` attributes EF Core uses to discover and apply
  migrations — was never generated/committed.
- Without `.Designer.cs`, `Database.Migrate()` in `Program.cs` never
  discovered this migration, so `exercise_type_definitions` (and the
  `exercise_patterns` table, also missing from the snapshot) were never
  created in any real database.
- `ExerciseTypeDefinitionSeeder.SeedAsync` queries `exercise_type_definitions`
  unconditionally on startup with no guard, threw `PostgresException 42P01`,
  and the resulting unhandled exception crashed `Program.Main`, causing the
  container restart loop.
- `LinguaCoachDbContextModelSnapshot.cs` was also out of date — it did not
  reflect the `ExercisePatternDefinition` entity (`exercise_patterns` table),
  compounding the drift between model and applied schema.

### Medium

- No automated check currently fails CI when a migration `.cs` file is added
  without its `.Designer.cs`. This class of bug (migration silently never
  applied) is not caught by `dotnet build`.

## Decisions made

- Regenerate the migration pair rather than hand-write the missing
  `.Designer.cs`, to guarantee snapshot/model consistency:
  1. `dotnet ef migrations remove --project src/LinguaCoach.Persistence --startup-project src/LinguaCoach.Persistence --force`
  2. `dotnet ef migrations add T44_ExerciseTypeCatalog --project src/LinguaCoach.Persistence --startup-project src/LinguaCoach.Persistence`
  3. EF generated new files with timestamp `20260615000748`. Renamed both
     `.cs` and `.Designer.cs` back to `20260615090000_T44_ExerciseTypeCatalog*`
     to preserve historical migration ordering/naming, and edited the
     `[Migration("20260615090000_T44_ExerciseTypeCatalog")]` attribute string
     in the `.Designer.cs` to match.
- Verified via `git diff` that the regenerated `.cs` migration body is
  functionally equivalent to the original (only cosmetic column-order swap of
  `created_at`/`updated_at` and `Down()` formatting differences).
- Verified the regenerated `LinguaCoachDbContextModelSnapshot.cs` now correctly
  includes the `ExercisePatternDefinition` entity block, fixing the
  pre-existing model/snapshot drift.
- Confirmed via `grep` that no other file in the repo references the old
  `20260615000748` timestamp.

`--startup-project src/LinguaCoach.Persistence` was required because only
`LinguaCoach.Persistence.csproj` references `Microsoft.EntityFrameworkCore.Design`;
`LinguaCoach.Api` does not.

## Verification

- `dotnet build src/LinguaCoach.Api -c Release` — 0 errors.
- `dotnet test tests/LinguaCoach.IntegrationTests -c Release` (full run) —
  the integration test factory calls `Database.Migrate()` against a real
  Postgres test database. All tests pass, confirming
  `exercise_type_definitions` is now created by migration and
  `ExerciseTypeDefinitionSeeder.SeedAsync` no longer throws.

## Risks / unresolved questions

- Production databases that already attempted to start with the broken
  migration set (i.e., never had `exercise_type_definitions` created) will
  pick up this migration on next `Database.Migrate()` run as a normal new
  migration — no manual intervention expected, but should be confirmed on
  next deploy.
- No CI guard added for "migration `.cs` without `.Designer.cs`" — flagged as
  a medium finding but not fixed in this change (out of scope for the
  Practice Gym pool PR this fix rode along with).

## Verdict

Fixed and verified against a real test database via the integration test
suite. Safe to deploy — `Database.Migrate()` will now create the missing
table and seeding will succeed.

## Next recommended action

Consider adding a CI check (e.g., a script asserting every
`*Migration.cs` under `Migrations/` has a matching `*.Designer.cs`) to prevent
recurrence. Not implemented here — out of scope for this change.
