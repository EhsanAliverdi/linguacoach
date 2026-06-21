# Phase 10W-2 — In-App Notification APIs + Dispatch Foundation Review

**Date:** 2026-06-21
**Sprint:** Phase 10W-2
**Type:** Engineering review
**Related:** docs/reviews/2026-06-21-phase-10w-1-backend-notification-foundation-review.md

---

## 1. Files Changed

### Application layer

- `src/LinguaCoach.Application/Notifications/NotificationDto.cs` — new: `NotificationDto` record, `PagedNotificationResult` record
- `src/LinguaCoach.Application/Notifications/NotificationQuery.cs` — new: `NotificationListQuery` record
- `src/LinguaCoach.Application/Notifications/INotificationQueryService.cs` — new interface: `ListAsync`, `GetUnreadCountAsync`, `MarkReadAsync`, `MarkAllReadAsync`, `ArchiveAsync`
- `src/LinguaCoach.Application/Notifications/INotificationDispatchService.cs` — new interface: `DispatchDueAsync`, `DispatchResult` record

### Infrastructure layer

- `src/LinguaCoach.Infrastructure/Notifications/NotificationQueryService.cs` — new implementation of `INotificationQueryService`
- `src/LinguaCoach.Infrastructure/Notifications/NotificationDispatchService.cs` — new implementation of `INotificationDispatchService`
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — added `INotificationQueryService` and `INotificationDispatchService` scoped registrations

### API layer

- `src/LinguaCoach.Api/Controllers/NotificationsController.cs` — new controller with 5 endpoints

### Tests

- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationApiTestFactory.cs` — factory for HTTP tests
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationApiTests.cs` — 16 API integration tests
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationDispatchTests.cs` — 9 dispatch integration tests

---

## 2. Notification APIs Added

Yes. `NotificationsController` at `api/notifications`.

### Routes

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/notifications` | Authenticated | List current user's in-app notifications (paged, filtered) |
| GET | `/api/notifications/unread-count` | Authenticated | Count unread, non-expired, non-archived notifications |
| POST | `/api/notifications/{id}/read` | Authenticated | Mark one notification read |
| POST | `/api/notifications/read-all` | Authenticated | Mark all unread notifications read |
| POST | `/api/notifications/{id}/archive` | Authenticated | Archive one notification (hides from default list) |

### Pagination

- `page` (default 1), `pageSize` (default 20, max 100).
- Response: `{ items, totalCount, page, pageSize, totalPages }`.
- Sorted newest-first.

### Filters

- `unreadOnly` (bool, default false) — only unread notifications.
- `category` (string, case-insensitive) — one of the `NotificationCategory` enum values.
- `severity` (string, case-insensitive) — one of the `NotificationSeverity` enum values.
- Invalid category or severity returns 400.
- Expired notifications excluded by default (ExpiresAtUtc < now).
- Archived notifications excluded from default list.

### DTO fields exposed

`id`, `title`, `body`, `category`, `severity`, `channel`, `status`, `createdAtUtc`, `readAtUtc`, `expiresAtUtc`, `deepLinkUrl`, `metadataJson`.

Internal outbox details not exposed.

---

## 3. Current-User Isolation

Yes. All queries filter by `RecipientUserId == currentUserId`. Mark-read, archive operations silently no-op if the notification belongs to another user. No cross-user leakage.

---

## 4. Mark Read / Read-All / Archive

Yes.
- `MarkRead`: sets `ReadAtUtc`, transitions to `Read` status. Idempotent.
- `MarkAllRead`: bulk update for all unread notifications of the current user.
- `Archive`: transitions to `Archived` status; excluded from default list query.

---

## 5. Dispatch Service Added

Yes. `INotificationDispatchService` / `NotificationDispatchService`.

`DispatchDueAsync(batchSize)`:
- Queries `NotificationOutboxItems` where `Status == Queued AND (NextAttemptAtUtc == null OR NextAttemptAtUtc <= now)`.
- Orders by `CreatedAtUtc` ascending (FIFO).
- **InApp**: marks outbox item `Delivered`, syncs linked `Notification` to `Delivered`. `AttemptCount++`, `ProcessedAtUtc` set.
- **Email / SMS**: records attempt as failed with error "No provider registered for channel X. Deferred to 10W-4/10W-6." Sets backoff `NextAttemptAtUtc = now + 5 * AttemptCount minutes`. Counted as `Skipped` in result.
- Returns `DispatchResult(Processed, Skipped, Failed)`.
- Exception in any item is caught; item recorded as failed; batch continues.

---

## 6. Quartz Job Added

No. `NotificationDispatchJob` (Quartz) deferred. Service is ready to be called by any scheduler. Document wiring for 10W-4 when email provider is added and a regular dispatch cadence is required.

---

## 7. External Email / SMS Sending

No. Email and SMS outbox items are recorded as skipped/failed-with-error until providers are wired in 10W-4 (email) and 10W-6 (SMS).

---

## 8. Migration Added

No. No new tables or columns in this phase. All features use existing `notifications` and `notification_outbox_items` tables from 10W-1.

---

## 9. Tests Added

**API integration tests** (`NotificationApiTests` — 16 tests):
- `List_ReturnsCurrentUserNotifications`
- `List_DoesNotReturnOtherUsersNotifications`
- `List_PaginationWorks`
- `List_UnreadOnlyFilter_ExcludesReadNotifications`
- `List_CategoryFilter_Works`
- `List_InvalidCategory_Returns400`
- `List_SeverityFilter_Works`
- `List_ExpiredNotifications_Excluded`
- `UnreadCount_OnlyCountsCurrentUserAndUnread`
- `MarkRead_SetsReadState`
- `MarkRead_AnotherUsersNotification_IsNoOp_NotError`
- `MarkAllRead_MarksAllCurrentUserUnread`
- `Archive_HidesNotificationFromDefaultList`
- `Archive_AnotherUsersNotification_IsNoOp`
- `List_Unauthenticated_Returns401`

**Dispatch integration tests** (`NotificationDispatchTests` — 9 tests):
- `Dispatch_InAppItem_MarkedDelivered`
- `Dispatch_InApp_SyncsNotificationToDelivered`
- `Dispatch_EmailItem_NotSentExternally_MarkedFailed`
- `Dispatch_SmsItem_NotSentExternally_MarkedFailed`
- `Dispatch_AfterFailure_AttemptCountIncreases`
- `Dispatch_MixedBatch_CountsCorrectly`
- `Dispatch_EmptyQueue_ReturnsZeros`
- `Dispatch_AlreadyDelivered_NotPickedUp`

---

## 10. Frontend Changes

None.

---

## 11. Gate Results

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS (0 errors)
- `dotnet test --configuration Release`: PASS — 2131/2131 (3 arch + 1278 unit + 850 integration; +23 from 2108)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS — 896/896

---

## 12. Confirmation of No Unintended Changes

- Auth / reset-password behavior: unchanged.
- Usage governance behavior: unchanged.
- Admin UI: unchanged.
- No new DB migrations.
- No frontend changes.

---

## 13. Deferred Items

| Item | Deferred to |
|---|---|
| `NotificationDispatchJob` (Quartz schedule) | 10W-4 |
| `IEmailSender` + SMTP/SendGrid | 10W-4 |
| Reset-password email flow | 10W-4 |
| Bell UI wiring (live data) | 10W-3 |
| `NotificationTemplate` entity | 10W-5 |
| `NotificationPreference` per-user | 10W-5 |
| `ISmsProvider` + Twilio | 10W-6 |
| `NotificationDeliveryAttempt` entity | 10W-4 |

---

## 14. Final Verdict

Phase 10W-2 complete. Current-user notification APIs are live. Dispatch foundation is in place — InApp items flow end-to-end; Email/SMS are safely queued and skipped until providers arrive. No external delivery occurs. No migration needed. All gates pass.

**Next recommended action:** Phase 10W-3 — Angular bell UI wiring to live notification APIs.

---

## Documentation impact

- Docs reviewed: sprint, TODOS, handoff
- Docs updated: `docs/sprints/current-sprint.md`, `TODOS.md`, `docs/handoffs/current-product-state.md`
- Docs intentionally not updated: architecture README (additive only, no structural change)
