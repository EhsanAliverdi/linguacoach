# Phase 10W-PREFS — Notification Preferences Foundation Review

**Date:** 2026-06-22
**Sprint/Feature:** Phase 10W-PREFS — Notification Preferences Foundation
**Related sprint:** Enterprise Notification Platform (Phase 10W)
**Prior phase:** 10W-5D-RESET-INTEGRATION (commit 416bf6e)

---

## Summary

Adds per-user per-category per-channel notification preference support. Users can opt out of non-critical notification channels (Learning, BillingUsage, Admin, BackgroundJob) for InApp and Email. Account and System categories are required and cannot be disabled. SMS remains permanently disabled (deferred). Preferences are checked at queue time in `NotificationService.QueueAsync` — skipped notifications produce a debug log only, no error.

---

## Files Changed

### Backend — New

- `src/LinguaCoach.Domain/Entities/NotificationPreference.cs` — entity with `Create`, `SetEnabled`, `IsRequired(category)` static helper
- `src/LinguaCoach.Application/Notifications/INotificationPreferenceService.cs` — interface + `NotificationPreferenceItem` + `UpdateNotificationPreferenceRequest` records
- `src/LinguaCoach.Infrastructure/Notifications/NotificationPreferenceService.cs` — implementation; defaults to enabled for InApp/Email, always false for SMS, always true for required categories
- `src/LinguaCoach.Persistence/Configurations/NotificationPreferenceConfiguration.cs` — EF table config; unique index on `(user_id, category, channel)`
- `src/LinguaCoach.Persistence/Migrations/*T56_NotificationPreferences*` — migration
- `tests/LinguaCoach.IntegrationTests/Api/NotificationPreferencesEndpointTests.cs` — 12 integration tests

### Backend — Modified

- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — added `DbSet<NotificationPreference>`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — registered `INotificationPreferenceService`
- `src/LinguaCoach.Infrastructure/Notifications/NotificationService.cs` — injected `INotificationPreferenceService`; `QueueAsync` checks `IsChannelEnabledAsync` before writing DB rows
- `src/LinguaCoach.Api/Controllers/NotificationsController.cs` — added `GET /api/notifications/preferences` and `PUT /api/notifications/preferences`
- `src/LinguaCoach.Api/Controllers/AdminController.cs` — added `GET /api/admin/notifications/preferences/{userId}`
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationServiceTests.cs` — updated 2 SMS tests to reflect new skip behavior
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationDispatchTests.cs` — updated 1 SMS test to reflect new skip behavior
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationApiTests.cs` — updated 4 `NotificationService` constructions to pass `NotificationPreferenceService`
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationEmailDispatchTests.cs` — updated 1 construction
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationDispatchTests.cs` — updated 1 construction

### Frontend — New

- `src/LinguaCoach.Web/src/app/core/services/notification-preferences.service.ts` — `NotificationPreferencesService` with `getPreferences()` / `updatePreferences()`

### Frontend — Modified

- `src/LinguaCoach.Web/src/app/features/profile/profile.component.ts` — added notification preferences section (category×channel table, SMS "Coming soon", Required badge, save button), injected `NotificationPreferencesService`, added signals and helper methods
- `src/LinguaCoach.Web/src/app/features/profile/profile.component.spec.ts` — added `NotificationPreferencesService` mock to `beforeEach`; added 11 new preference tests

### Docs — Updated

- `TODOS.md` — `TODO-10W-PREFS` marked complete
- `docs/reviews/2026-06-22-phase-10w-prefs-notification-preferences-foundation-review.md` — this file

---

## Architecture

Preferences are checked at the `INotificationService.QueueAsync` level — before any DB write. This means:

1. `IsChannelEnabledAsync` is called for every notification.
2. Required categories (Account, System) always return `true` regardless of DB rows.
3. SMS always returns `false` regardless of DB rows (deferred channel).
4. For all other category+channel combinations, the DB row is checked; if absent, defaults to `true` (opt-in by default).
5. If skipped: `LogDebug` only, no exception, no row written.

The preference service is registered as `Scoped` (owns DbContext). No caching — each queue call hits the DB once. Suitable for current load; can be cached later if needed.

---

## Security Invariants

- Reset-password email is `NotificationCategory.Account` → always bypasses preference check → behavior unchanged.
- Student-created email is `NotificationCategory.Account` → same.
- No secrets exposed through preference API.
- Users can only read/write their own preferences (JWT userId from claims).
- Admin read endpoint is admin-role only.

---

## Product Rules Enforced

| Rule | Implementation |
|------|---------------|
| Account/System categories cannot be disabled | `NotificationPreference.IsRequired()` + `IsChannelEnabledAsync` always returns true for these |
| SMS not yet deliverable | `IsChannelEnabledAsync` always returns false for SMS regardless of preference row |
| UI shows SMS as "Coming soon" | Static text in profile template; no checkbox |
| Required rows show "Required" badge | `isRequired` field in API response drives badge rendering |

---

## Test Coverage

### Backend (12 new integration tests)

| Test | What it verifies |
|------|-----------------|
| `GetPreferences_Unauthenticated_Returns401` | Auth guard |
| `UpdatePreferences_Unauthenticated_Returns401` | Auth guard |
| `AdminGetPreferences_Unauthenticated_Returns401` | Auth guard |
| `AdminGetPreferences_AsStudent_Returns403` | Role guard |
| `GetPreferences_DefaultsReturnedWhenNoRowsExist` | Default values with no DB rows |
| `GetPreferences_AccountCategoryIsRequired` | Account rows have isRequired=true |
| `UpdatePreferences_UserCanDisableLearningEmail` | User can opt out of non-critical channel |
| `UpdatePreferences_RequiredCategoryForcedEnabled` | Required category stays enabled even if client sends false |
| `DisabledEmailPreference_PreventsNonCriticalEmailQueueing` | Preference service returns false after opt-out |
| `AccountEmail_AlwaysEnabledRegardlessOfPreference` | Required category always enabled |
| `SmsChannel_AlwaysDisabled` | SMS always disabled regardless of preference |
| `UpdatePreferences_EmptyBody_Returns400` | Validation |
| `AdminGetPreferences_ReturnsPreferencesForUser` | Admin read endpoint works |

### Frontend (11 new unit tests)

`loads notification preferences on init`, `renders notification preferences section`, `renders prefs table after loading`, `shows SMS as coming soon`, `required category shows Required badge`, `getPref returns true for enabled preference`, `setPref updates local state`, `savePrefs calls updatePreferences`, `savePrefs shows error on failure`, `isPrefRequired returns true for Account`, `prefsLoading is false after load`

---

## Test Results

| Gate | Result |
|------|--------|
| `dotnet build --configuration Release` | PASS, 0 errors |
| `dotnet test --configuration Release` | PASS — 2246 total (3 arch + 1287 unit + 956 integration) |
| `npm run build -- --configuration production` | PASS |
| `npm test -- --watch=false --browsers=ChromeHeadless` | PASS — 1011 tests |

Prior counts: 2233 .NET / 999 Angular. Net new: +13 .NET, +12 Angular.

---

## Decisions Made

1. **Opt-in by default.** No preference row = enabled. This preserves existing behavior for all current users.
2. **Required enforcement at service layer, not domain.** `IsRequired()` is a pure static helper — no DB constraint needed. The service enforces it silently (no error returned to caller).
3. **SMS always blocked in preference service.** The SMS channel flag in `NotificationChannelConfig` (appsettings) continues to block delivery; the preference layer adds a second guard. Both must be removed/enabled when SMS is eventually delivered.
4. **No admin edit UI.** Admin can read student preferences via `GET /api/admin/notifications/preferences/{userId}`. Edit UI deferred — not needed for ops.
5. **Profile page, not a separate settings page.** Preferences added as Section 7 in the existing profile component. No new route needed.

---

## Risks / Unresolved Questions

- `TODO-10W-5D-UNIQUE-CONSTRAINT`: DB unique index on `(template_key, channel)` for active notification templates still deferred.
- `TODO-10W-6`: SMS provider still deferred. When added, both the `NotificationChannelConfig` flag and the preference layer SMS block must be lifted.
- `TODO-10W-FINAL`: Platform audit still deferred.
- Preference check adds one DB query per `QueueAsync` call. For high-volume scenarios (e.g. bulk admin notifications), batching or caching would reduce load.

---

## Final Verdict

**PASS.** Phase 10W-PREFS complete. Entity, migration, service, API endpoints, preference-aware queueing, frontend UI, and tests all in place. All security and product rules enforced. All 4 gates pass.

---

## Next Recommended Action

`TODO-10W-6` (SMS provider), `TODO-10W-5D-UNIQUE-CONSTRAINT` (optional DB index on templates), or `TODO-10W-FINAL` (platform audit).
