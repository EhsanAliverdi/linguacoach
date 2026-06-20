# Phase 10U-6 — AI Usage Student Filter — Engineering Review

**Date:** 2026-06-20
**Sprint:** Phase 10U-6 (AI Usage Student Filter)
**Commit:** phase 10u ai usage student filter
**Reviewer:** Claude Sonnet 4.6

---

## Related sprint

`docs/sprints/current-sprint.md`

---

## Files reviewed / changed

### Backend
- `src/LinguaCoach.Application/Admin/AiUsageQueries.cs` — added `StudentId` field to `AiUsageRecentFilter`
- `src/LinguaCoach.Infrastructure/Admin/AiUsageHandler.cs` — added `StudentId` LINQ clause in `ApplyRecentFilter`
- `src/LinguaCoach.Api/Controllers/AiUsageController.cs` — added `studentId` query param with GUID validation
- `tests/LinguaCoach.IntegrationTests/Api/AiUsageStudentFilterTests.cs` — 8 new integration tests

### Frontend
- `src/LinguaCoach.Web/src/app/core/services/ai-usage.service.ts` — added `studentId` to `AiUsageRecentCallFilter`
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.ts` — student options signal, `loadStudentOptions()`, `onRecentStudentChange()`, student filter state
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.html` — student select in filter bar (conditional on `studentOptions().length > 0`)
- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-usage/admin-ai-usage.component.spec.ts` — 8 new component tests
- `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` — added `AdminApiService` mock to AI Usage test case

---

## Findings by priority

### P0 — Resolved before commit

**FK constraint in integration tests**
Original seed used `Guid.NewGuid()` as `StudentProfileId`. `AiUsageLog` has a real `HasForeignKey` constraint to `student_profiles`. SQLite enforced it in tests. Fixed by calling `CreateStudentAndGetTokenAsync()` to create real users, then querying `db.StudentProfiles.Join(db.Users...)` to resolve the `StudentProfile.Id` (PK, not `ApplicationUser.Id`).

**DI failure in `admin-wrapper-migration.spec.ts`**
`AdminAiUsageComponent` now requires `AdminApiService` in constructor. The existing AI Usage test case in this spec file did not provide it. Fixed by adding spy mock returning empty student list.

### P1 — Design decisions

**Student select conditional on `studentOptions().length > 0`**
Hides the filter when no students are loaded (error case, empty system). Avoids showing a useless empty select. Silently ignores load errors — admins in systems with students will always see it.

**Uses `studentProfileId` (the profile PK, not the user ID)**
`AiUsageLog.StudentProfileId` references `student_profiles.Id`. `StudentListItem.studentProfileId` from the admin students endpoint is the same field. These are consistent. The integration test seed confirms this.

**Unknown studentId returns 200 + empty paged result (not 404)**
Consistent with filter semantics. A valid GUID that matches no logs is not an error.

**Invalid (non-GUID) studentId returns 400 with `{ error: "..." }` body**
Consistent with 10U-5 pattern for invalid status values.

---

## Decisions made

- No migration required. `AiUsageLog.StudentProfileId` column already exists.
- Student list loaded on `ngOnInit` via `adminApi.listStudents({ pageSize: 50 })`. Cap at 50 is sufficient for a filter dropdown. Large student bases can be addressed in a future phase if needed.
- Provider routing and usage governance are unchanged.

---

## Implementation tasks produced

None. Phase is complete.

---

## Risks / unresolved questions

- **Student list capped at 50.** Systems with more than 50 students will silently not show all of them in the student filter select. Could be addressed with a typeahead autocomplete in a future phase (10U-7 or similar).

---

## Gates

| Gate | Result |
|------|--------|
| `git diff --check` | PASS |
| `dotnet build --configuration Release` | PASS (0 errors, 5 warnings pre-existing) |
| `dotnet test --configuration Release` | PASS (745/745 integration + 1248/1248 unit + 3/3 arch) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS (831/831) |

---

## Final verdict

**SHIP.** All gates pass. No regressions. FK constraint issue resolved correctly. DI fix in wrapper spec is clean.

---

## Next recommended action

Phase 10U-6 is complete. Next phase of the 10U series (student filter) is done. Consider a typeahead student search in a future phase if the student base exceeds 50.
