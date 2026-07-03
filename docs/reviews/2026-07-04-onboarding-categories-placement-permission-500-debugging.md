# Production 500s on Onboarding/Placement Admin Endpoints — DB Permission Gap

Date: 2026-07-04
Related: Unified JSON Question-Schema plan (Phase 6b), commit `13513ec`

## Symptom

After Phase 6b (`13513ec`) deployed, the user reported live 500 errors on `speakpath.app/admin`:

- `GET /api/admin/placement-items` — 500 ("Could not load placement items")
- `GET /api/admin/onboarding/flow` — 500 ("Could not load onboarding configuration" / "Could not load active flow detail")

Both endpoints had worked before this deploy.

## Files reviewed

- `src/LinguaCoach.Infrastructure/Onboarding/AdminOnboardingFlowQueryHandler.cs`
- `src/LinguaCoach.Domain/Questions/OnboardingContentConverter.cs`
- `src/LinguaCoach.Domain/Questions/QuestionContentJson.cs`
- Production Postgres schema/data (via `docker run postgres:16-alpine psql`, live connection, no credentials written to disk)

## Investigation

Initially suspected an app-level bug (unsafe JSON deserialization, enum parse failure, choice-key casing mismatch) since those were the classes of bugs found and fixed earlier this session. Ruled out by direct inspection of the live database:

- `onboarding_step_definitions.step_type` values all match live `OnboardingStepTypeV2` enum members — no parse failures.
- `options_json` casing (`key`/`label`) is consistent between writer (`OnboardingContentConverter`) and reader (`AdminOnboardingFlowQueryHandler.ParseOptions`).
- Migrations `20260703103013_T20I_Unified_Onboarding_ContentJson` and `20260703114306_T20I_Unified_Onboarding_Categories` are both applied (`__EFMigrationsHistory` confirms), and both new tables/columns exist with the expected shape.
- Active onboarding flow (v21, `7688aa6a-be4a-46c7-bf75-5844bb98673c`) has valid `category_id` on every step and `content_json` populated for every question step.

## Root cause

**Database role permission gap, not an application bug.**

```
tablename                        | tableowner
----------------------------------+-------------
placement_item_definitions       | linguacoach
onboarding_category_definitions  | linguacoach
onboarding_step_definitions      | linguacoach
```

All tables are owned by role `linguacoach` (the role migrations run as). But the app's runtime connection role, `linguacoach_dba`, only has explicit grants on `onboarding_step_definitions`:

```
grantee         | table_name                   | privilege_type
linguacoach_dba | onboarding_step_definitions  | SELECT/INSERT/UPDATE/DELETE/TRUNCATE/REFERENCES/TRIGGER
```

`onboarding_category_definitions` and `placement_item_definitions` have **zero grants** to `linguacoach_dba` (placement_item_definitions has zero grants to any role at all). There is no `ALTER DEFAULT PRIVILEGES` configured for the `linguacoach` role, so every new table created by a migration silently ships with no access for the app's runtime user — it works until the endpoint touching that specific new table is hit, then 500s with a permission-denied error from Npgsql.

This explains why the earlier `290a919` fix (defensive `TryDeserializeContent`) didn't help here: that fix only guards against malformed JSON in an *already-readable* row. It cannot help when the row can't be read at all.

Confirmed `linguacoach_dba` cannot self-remediate:

```
rolname          | rolsuper | rolcreaterole | member_of
linguacoach_dba  | f        | f             | (none)
```

Not a superuser, no CREATEROLE, not a member of `linguacoach` — structurally unable to GRANT on tables it doesn't own. Fix requires the `linguacoach` owner role's own credentials or a Postgres superuser.

## Fix (not yet applied — blocked on credentials)

```sql
GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
  ON onboarding_category_definitions, placement_item_definitions
  TO linguacoach_dba;

-- prevents recurrence on every future migration that adds a table:
ALTER DEFAULT PRIVILEGES FOR ROLE linguacoach
  GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
  ON TABLES TO linguacoach_dba;
```

Must be run as the `linguacoach` owner role or a superuser — the app's own `linguacoach_dba` credential cannot run it.

## Decisions made

- Did not attempt to work around the sandbox's block on running GRANT statements against production (correctly classified as a permission-escalation change requiring explicit, specific authorization) — asked the user instead.
- User was asked (via AskUserQuestion) whether they have owner/superuser credentials, DB console access, or want to pause this — no response received; proceeding with other unblocked work (Phase 7 prep) per standing "continue autonomously" instruction, since this specific action is correctly gated on credentials only the user can supply.

## Risks / unresolved questions

- Until the GRANT is applied, `/admin/onboarding` and `/admin/placement-items` admin pages remain broken in production.
- Any *other* table added by a future migration will hit the same gap unless `ALTER DEFAULT PRIVILEGES` is set up for the `linguacoach` role — this is a recurring infra risk, not a one-off.
- Unknown whether other already-shipped tables from earlier migrations this session (before Phase 6b) have the same gap but haven't been hit yet by a live code path.

## Update — a second, independent bug was masked by the permission gap

The user applied the GRANT fix directly against production. Both endpoints kept returning 500 afterward, with the same generic `"Something went wrong"` body (production hides stack traces). The user then separately supplied a VPS log excerpt (`docker logs linguacoach-api-1`) showing the real exception:

```
System.NotSupportedException: The JSON payload for polymorphic interface or abstract type
'LinguaCoach.Domain.Questions.QuestionContent' must specify a type discriminator.
   at LinguaCoach.Domain.Questions.QuestionContentJson.TryDeserializeContent(String json)
   at LinguaCoach.Domain.Entities.OnboardingStepDefinition.get_Content()
```

Reproduced this locally (no production code paths touched) by building a throwaway console app referencing `LinguaCoach.Persistence`/`LinguaCoach.Infrastructure`, pointed at the production connection string via an env var, and calling `AdminPlacementItemListQueryHandler`/`AdminOnboardingFlowQueryHandler` directly to get the un-redacted .NET exception. Then iterated row-by-row over both tables to confirm **every** row with a populated `ContentJson` threw the same exception — not a single bad row, a systemic one.

### Real root cause

Postgres `jsonb` columns do not preserve JSON object key insertion order — internally they store keys in their own canonical order (roughly: shorter keys first), and return them in that order on read, regardless of how the JSON was originally written. Sample data confirmed this: content written with `"type"` as one of several properties came back as `{"Id": "q1", "type": "single_choice", ...}` — `"Id"` before `"type"`.

System.Text.Json's built-in polymorphic deserialization (`[JsonPolymorphic]` / `[JsonDerivedType]`, used by the shared `QuestionContent` schema) requires the type discriminator to be the **first** property in the JSON object, or it throws `NotSupportedException`. This is a documented STJ limitation, not a bug in .NET — but it collided directly with `jsonb`'s lack of order preservation.

The earlier defensive fix (commit `290a919`, `QuestionContentJson.TryDeserializeContent` catching `JsonException`) did not help here because `NotSupportedException` is a different exception type — it was never caught, and propagated up through `OnboardingStepDefinition.Content` / `PlacementItemDefinition.Content` to crash the whole list endpoint on the very first row with any content.

### Fix

`src/LinguaCoach.Domain/Questions/QuestionContentJson.cs`: before handing the JSON to `JsonSerializer.Deserialize<QuestionContent>`, walk the parsed `JsonNode` tree and rebuild every JSON object with `"type"` moved to the first property — recursively, so nested group sub-questions (`ListeningGroupQuestion.Questions` / `ReadingGroupQuestion.Questions`) are also normalized. Also broadened the catch to include `NotSupportedException` (still returning `null` — the existing "swallow and let callers fall back" contract), as a second line of defense for any future STJ limitation of the same shape.

No column type or write-path changes were needed — writes already produce valid content; jsonb was just reordering it on the way back out.

Verified against live production data (read-only diagnostic, no writes): re-ran the row-by-row check across every row in both `placement_item_definitions` and `onboarding_step_definitions` with the fix applied — zero failures, down from dozens of `NotSupportedException`s per table.

Added regression tests in `tests/LinguaCoach.UnitTests/Questions/QuestionContentJsonTests.cs` covering: discriminator-after-other-properties (top-level), discriminator-after-other-properties inside a nested group sub-question, missing discriminator (returns null, doesn't throw), malformed JSON, and null input. Full backend suite (3234 tests: 5 Architecture + 1785 Unit + 1444 Integration) passes.

## Final verdict

Two independent, compounding issues, both now fixed:
1. DB permission gap (`linguacoach_dba` missing grants on migrated tables) — fixed by the user directly via the GRANT script.
2. `QuestionContentJson` polymorphic deserialization breaking on jsonb's key reordering — fixed in code (this commit), with regression tests and full suite passing.

## Next recommended action

1. Ship the `QuestionContentJson` fix (commit, push, deploy) and verify both `/api/admin/onboarding/flow` and `/api/admin/placement-items` return 200 in production with a real admin session.
2. Keep the `ALTER DEFAULT PRIVILEGES` follow-up in the backlog (`TODO-10`) so future migrated tables don't repeat the permission gap.
3. Resume Phase 7 cleanup once both endpoints are confirmed healthy live.
