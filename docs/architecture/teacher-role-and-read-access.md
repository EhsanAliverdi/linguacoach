# Teacher Role and Read-Only Student Access

**Status:** Deferred — not scheduled. Fully planned, ready to implement
when prioritized.

**Date:** 2026-07-03

**Related:** Phase 21A precursor, `docs/roadmap/road-map.md`

---

## Problem

LinguaCoach currently supports exactly two login roles: `Admin` and
`Student` (`UserRole` enum, `src/LinguaCoach.Domain/Enums/UserRole.cs`).
There is no way for an instructor/teacher to have their own account. The
full fix — an Organisation/Teacher/Cohort model (Phase 21A) — is a large
multi-sprint effort. This doc scopes a smaller, immediately useful
increment: a real `Teacher` role that can log in and see a read-only view
of student progress, without building Organisation/Cohort/scoping yet.

## Scope

**In scope:**
- Teacher can log in with an admin-provisioned account (no self-signup,
  matching how students are created today).
- Teacher can view a list of all students and their progress/activity
  (read-only).
- Teacher has their own basic dashboard/landing area (`/teacher` route),
  distinct from student and admin.

**Explicitly out of scope for this slice** (remains genuine Phase 21A
work):
- `Organisation` entity
- `StudentCohort`/`Group` entity
- Teacher-to-student assignment/scoping — a teacher initially sees **all**
  students, same visibility as Admin's read views, not a filtered subset
- Org billing, org admin portal

## Design Decisions

**1. Separate `/api/teacher` controller, not an expanded
`[Authorize(Roles="Admin,Teacher")]` on `/api/admin`.**

`AdminController` exposes full CRUD (archive, reactivate, pause, reset
password, CEFR override, lifecycle reset, prompt/curriculum/AI config
management) behind one blanket `[Authorize(Roles = nameof(UserRole.Admin))]`
class-level attribute. Widening that single attribute to include Teacher
would grant Teacher access to every action on the controller unless every
endpoint got a per-method override — a maintenance trap where a future
admin-only endpoint silently becomes teacher-visible by default. A new
`TeacherController` at `[Route("api/teacher")]` with
`[Authorize(Roles = nameof(UserRole.Teacher))]` at the class level, backed
by a new `ITeacherStudentQuery` interface with only the 2-3 read methods
teachers need, makes the authorization boundary self-evident and fails
closed. Teacher-only — Admin keeps using its own richer `/api/admin/*`
surface, not the teacher one.

**2. Parallel `CreateTeacherHandler`, not a generalized
`CreateStudentHandler(UserRole)`.**

`CreateStudentHandler.HandleAsync` is tightly coupled to student-specific
side effects (creates a `StudentProfile`, computes
`StudentLifecycleStage`). Since this slice has no `TeacherProfile`, teacher
creation is simpler than student creation, so forcing them into one
parameterized method inverts the natural shape. A small parallel
`CreateTeacherHandler : ICreateTeacherHandler` keeps each handler linear.

**3. No `TeacherProfile` entity for this scope.** Add nullable
`FirstName`/`LastName` directly to `ApplicationUser` (one small migration,
reusable by both Student and Teacher) rather than a full profile table. The
real Teacher entity work happens in Phase 21A.

**4. Single generic `account.provisioned` notification template**, reused
by both student and teacher account creation, rather than forking a second
template — both flows pass identical variables (`AppName`, `DisplayName`,
`LoginUrl`).

## Backend Changes

**Domain**
- `src/LinguaCoach.Domain/Enums/UserRole.cs` — add `Teacher = 2`. Int
  column, no schema impact by itself.
- `src/LinguaCoach.Domain/Enums/AuthEventType.cs` — add
  `TeacherAccountCreated`.

**Persistence**
- `src/LinguaCoach.Persistence/Identity/ApplicationUser.cs` — add
  `FirstName`/`LastName` (nullable `string?`).
- New EF migration via `dotnet ef migrations add AddNameToApplicationUser
  --project src/LinguaCoach.Persistence --startup-project
  src/LinguaCoach.Api` (per CLAUDE.md: always use `dotnet ef migrations
  add`, never hand-write).
- Before finishing, grep for exhaustive `switch`/pattern matches on
  `UserRole` (e.g. `grep -rn "UserRole.Student"` / `"case UserRole"`) —
  seeders, permission checks, or reporting code may need a new `Teacher`
  arm.

**Application layer (new)**
- `src/LinguaCoach.Application/Teacher/TeacherQueries.cs` —
  `ITeacherStudentQuery` with `ListStudentsPagedAsync`,
  `GetStudentDetailAsync`, `GetActivityHistoryAsync`. Reuses existing
  `StudentListItem`, `StudentListQuery`, `PagedResponse<T>`,
  `AdminStudentDetailDto`, `AdminActivityHistoryItem` from
  `LinguaCoach.Application.Admin`
  (`src/LinguaCoach.Application/Admin/AdminQueries.cs:156`) — no new DTOs.
- `src/LinguaCoach.Application/Teacher/CreateTeacherCommand.cs` —
  `CreateTeacherCommand(Email, TemporaryPassword, FirstName?, LastName?,
  MustChangePassword)`, `CreateTeacherResult(UserId)`,
  `ICreateTeacherHandler`.

**Infrastructure layer (new)**
- `src/LinguaCoach.Infrastructure/Teacher/TeacherStudentQueryHandler.cs` —
  implements `ITeacherStudentQuery` as a thin wrapper delegating to the
  already-injected `IAdminStudentQuery` (reuse the query implementation,
  don't duplicate it — the controller boundary enforces authorization, not
  the query layer).
- `src/LinguaCoach.Infrastructure/Teacher/CreateTeacherHandler.cs` —
  mirrors `src/LinguaCoach.Infrastructure/Admin/CreateStudentHandler.cs:47`
  but simpler: creates `ApplicationUser` with `Role = UserRole.Teacher`,
  `FirstName`/`LastName`, `MustChangePassword`, `EmailConfirmed = true`; no
  profile entity; records `AuthEventType.TeacherAccountCreated`; queues the
  `account.provisioned` template email.
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — register
  `ITeacherStudentQuery -> TeacherStudentQueryHandler` and
  `ICreateTeacherHandler -> CreateTeacherHandler` near the existing Admin
  registrations (~line 216-227).
- Locate the seeder for `account.student_created`
  (`NotificationTemplateSeeder` or similar under
  `src/LinguaCoach.Persistence/Seed/`) and generalize it to a role-neutral
  `account.provisioned` template if not already generic.

**API layer**
- `src/LinguaCoach.Api/Controllers/TeacherController.cs` (new):
  `[Route("api/teacher")] [Authorize(Roles = nameof(UserRole.Teacher))]`
  with `GET students`, `GET students/{id}`, `GET students/{id}/activity`.
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — add
  `POST create-teacher` calling `ICreateTeacherHandler` (already covered by
  the controller's class-level `Authorize(Roles = nameof(UserRole.Admin))`).
- `src/LinguaCoach.Api/Controllers/AuthController.cs` — confirm the login
  response's role mapping is a plain `role.ToString()`, not a hardcoded
  Admin/Student switch. `JwtTokenService.cs:36` already embeds role
  generically via `new Claim(ClaimTypes.Role, role.ToString())` — no change
  needed there.

## Frontend Changes

- `src/LinguaCoach.Web/src/app/core/models/auth.models.ts` — widen
  `UserRole` type to include `'Teacher'`.
- `src/LinguaCoach.Web/src/app/core/guards/teacher.guard.ts` (new) —
  mirror `admin.guard.ts`: `mustChangePassword` check first, then
  `role === 'Teacher'`, else redirect (`/dashboard` if authenticated wrong
  role, `/login` if unauthenticated).
- `src/LinguaCoach.Web/src/app/app.routes.ts` — add a `/teacher` route
  block (own layout + guard), mirroring the existing `/admin` block:
  `students` (list) and `students/:id` (detail) lazy-loaded children.
- `src/LinguaCoach.Web/src/app/design-system/teacher/layouts/teacher-app-layout/`
  (new, minimal) — a lightweight nav/shell ("Students" only, no AI
  config/security/curriculum links). Reuse presentational components from
  `design-system/admin` directly (table, badge, pagination, page-header)
  rather than duplicating them.
- `src/LinguaCoach.Web/src/app/features/teacher/teacher-students/` and
  `teacher-student-detail/` (new) — modeled on
  `features/admin/admin-students/` and `admin-student-detail/`, stripped of
  all mutation UI (no edit/archive/reset/pause/create actions).
- `src/LinguaCoach.Web/src/app/core/services/teacher.api.service.ts` (new)
  — thin `HttpClient` wrapper for `/api/teacher/*`, reusing existing
  `StudentListItem`/`AdminStudentDetailDto`-shaped models from
  `core/models/admin.models.ts`.
- `src/LinguaCoach.Web/src/app/features/public/auth/login/login.component.ts`
  and `.../change-password/change-password.component.ts` — add a `Teacher`
  branch to the post-login/post-password-change role redirect (alongside
  the existing `Admin` branch), routing to `/teacher`.
- `src/LinguaCoach.Web/src/app/features/admin/create-teacher/` (new) —
  admin-side creation form mirroring `features/admin/create-student/`, plus
  an `AdminApiService.createTeacher()` method and an
  `admin/create-teacher` route.

## Tests

- `tests/LinguaCoach.IntegrationTests/Api/TeacherEndpointTests.cs` (new) —
  Teacher sees full student roster; Teacher gets 403 on `/api/admin/*`;
  Admin gets 403 on `/api/teacher/*`; Student gets 403 on both; `POST
  /api/admin/create-teacher` creates a `Role = Teacher` user and records
  `AuthEventType.TeacherAccountCreated`.
- `tests/LinguaCoach.IntegrationTests/Api/ApiTestFactory.cs` — add
  `CreateTeacherAndGetTokenAsync(...)` mirroring the existing
  `CreateStudentAndGetTokenAsync` (~line 128-153).
- `src/LinguaCoach.Web/src/app/core/guards/teacher.guard.spec.ts` (new) —
  mirror `admin.guard.spec.ts`.
- Component specs for the new teacher feature pages if
  `admin-students.component.spec.ts` sets that precedent.

## Verification Plan

**Manual:**
1. Apply the new migration; run the API.
2. As an Admin, call `POST /api/admin/create-teacher` (or use the new admin
   UI form) to create a teacher account.
3. Log in as the teacher — confirm redirect to `/teacher`, not
   `/dashboard`/`/admin`.
4. Confirm `/teacher/students` shows the same roster count as
   `/admin/students`.
5. Confirm teacher is blocked from `/admin/*` routes and `/api/admin/*`
   (guard redirect + 403).
6. Confirm a Student account is blocked from `/teacher/*` and
   `/api/teacher/*`.
7. Confirm the teacher detail view renders read-only (no edit controls).

**Automated:**
- `dotnet test tests/LinguaCoach.IntegrationTests` — new
  `TeacherEndpointTests.cs`, plus re-run `AdminEndpointTests.cs` for
  regressions.
- `dotnet build` — catch any non-exhaustive `switch` on `UserRole` broken
  by the new enum value.
- `npm test -- --watch=false --browsers=ChromeHeadless` — new
  `teacher.guard.spec.ts` plus regression on `admin.guard.spec.ts`.

## Documentation Impact When Implemented

- `docs/roadmap/road-map.md` — move this item from "Deferred" to
  in-progress/done, update Phase 21A framing to reflect that a minimal
  Teacher slice has shipped ahead of the full org model.
- `docs/backlog/product-backlog.md` — update the Teacher-role backlog
  item(s) (see "Admin / teacher progress view" around line 881/903).

## Open Questions

None blocking — all three original open questions were resolved to
defaults during planning (add name columns, generic email template,
Teacher-only endpoint access). Revisit if requirements change before
implementation.
