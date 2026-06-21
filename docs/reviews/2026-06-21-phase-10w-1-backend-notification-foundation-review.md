# Phase 10W-1 — Backend Notification Foundation Review

**Date:** 2026-06-21
**Sprint:** Phase 10W-1
**Type:** Engineering review
**Related gap check:** docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md

---

## 1. Files Changed

### Domain layer

- `src/LinguaCoach.Domain/Enums/NotificationChannel.cs` — new: InApp, Email, Sms
- `src/LinguaCoach.Domain/Enums/NotificationStatus.cs` — new: Queued, Delivered, Read, Failed, Archived
- `src/LinguaCoach.Domain/Enums/NotificationSeverity.cs` — new: Info, Success, Warning, Error
- `src/LinguaCoach.Domain/Enums/NotificationCategory.cs` — new: System, Account, Learning, BillingUsage, Admin, BackgroundJob
- `src/LinguaCoach.Domain/Entities/Notification.cs` — new entity with factory method and state transitions
- `src/LinguaCoach.Domain/Entities/NotificationOutboxItem.cs` — new entity with attempt tracking

### Application layer

- `src/LinguaCoach.Application/Notifications/NotificationRequest.cs` — new record
- `src/LinguaCoach.Application/Notifications/INotificationService.cs` — new interface

### Infrastructure layer

- `src/LinguaCoach.Infrastructure/Notifications/NotificationService.cs` — new implementation
- `src/LinguaCoach.Infrastructure/DependencyInjection.cs` — added `INotificationService` → `NotificationService` scoped registration

### Persistence layer

- `src/LinguaCoach.Persistence/Configurations/NotificationConfiguration.cs` — new EF config
- `src/LinguaCoach.Persistence/Configurations/NotificationOutboxItemConfiguration.cs` — new EF config
- `src/LinguaCoach.Persistence/LinguaCoachDbContext.cs` — added `Notifications` and `NotificationOutboxItems` DbSets
- `src/LinguaCoach.Persistence/Migrations/20260621093645_T54_NotificationFoundation.cs` — new migration
- `src/LinguaCoach.Persistence/Migrations/20260621093645_T54_NotificationFoundation.Designer.cs` — generated
- `src/LinguaCoach.Persistence/Migrations/LinguaCoachDbContextModelSnapshot.cs` — updated

### Tests

- `tests/LinguaCoach.UnitTests/Domain/NotificationEntityTests.cs` — 20 unit tests
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationTestFactory.cs` — factory stub
- `tests/LinguaCoach.IntegrationTests/Notifications/NotificationServiceTests.cs` — 13 integration tests

### Docs (10W-0 included)

- `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`
- `docs/sprints/current-sprint.md`
- `TODOS.md`
- `docs/handoffs/current-product-state.md`

---

## 2. Migration Added

Yes. Migration `T54_NotificationFoundation`.

Tables created:
- `notifications` — persistent notification records
- `notification_outbox_items` — queued delivery items with retry state

Indexes:
- `ix_notifications_recipient_created` — (recipient_user_id, created_at_utc)
- `ix_notifications_recipient_read` — (recipient_user_id, read_at_utc)
- `ix_notifications_channel_status` — (channel, status)
- `ix_notification_outbox_status_next_attempt` — (status, next_attempt_at_utc)
- `ix_notification_outbox_recipient` — (recipient_user_id)
- `ix_notification_outbox_notification_id` — (notification_id)

---

## 3. Entities and Tables Added

| Entity | Table | Notes |
|---|---|---|
| `Notification` | `notifications` | Title 200, Body 2000, enums as strings, optional deep link/expiry/metadata |
| `NotificationOutboxItem` | `notification_outbox_items` | Linked to Notification via nullable FK, payload JSON, attempt tracking |

`NotificationDeliveryAttempt` deferred to 10W-4 per plan (only needed when external dispatch is wired).

---

## 4. Enums / Constants Added

- `NotificationChannel`: InApp, Email, Sms
- `NotificationStatus`: Queued, Delivered, Read, Failed, Archived
- `NotificationSeverity`: Info, Success, Warning, Error
- `NotificationCategory`: System, Account, Learning, BillingUsage, Admin, BackgroundJob

All stored as strings in the database for readability and schema stability.

---

## 5. INotificationService Added

Yes. Interface in `LinguaCoach.Application.Notifications`.

API:
- `QueueAsync(NotificationRequest)` — generic entry point
- `QueueInAppAsync(...)` — convenience wrapper for InApp channel
- `QueueEmailAsync(...)` — convenience wrapper for Email channel (no external send in this phase)
- `QueueSmsAsync(...)` — convenience wrapper for SMS channel (no external send in this phase)

`NotificationRequest` is a record with: RecipientUserId, Title, Body, Channel, Category, Severity, DeepLinkUrl?, ExpiresAtUtc?, MetadataJson?.

---

## 6. In-App Queue Behavior

Yes. `QueueInAppAsync` creates both a `Notification` row and a `NotificationOutboxItem` row in a single `SaveChangesAsync` call. The notification starts in `Queued` status, unread.

---

## 7. Email / SMS Outbox Behavior

Yes. `QueueEmailAsync` and `QueueSmsAsync` both create a `Notification` row and a `NotificationOutboxItem` row. No external delivery occurs. Outbox items start with `AttemptCount=0`, `ProcessedAtUtc=null`.

---

## 8. External Email / SMS Sending

No. No `IEmailSender` or `ISmsProvider` in this phase. Dispatch worker deferred to 10W-4/10W-6.

---

## 9. Notification API Added

No. No controller endpoints in this phase. Bell API is 10W-2.

---

## 10. Dispatch Worker Added

No. `NotificationDispatchJob` (Quartz) deferred to 10W-2/10W-4.

---

## 11. Tests Added

**Unit tests** (`LinguaCoach.UnitTests.Domain.NotificationEntityTests` — 20 tests):
- `Create_ValidArgs_SetsAllFields`
- `Create_EmptyRecipient_Throws`
- `Create_EmptyTitle_Throws`
- `Create_EmptyBody_Throws`
- `MarkRead_SetsStatusAndTimestamp`
- `MarkRead_Idempotent_DoesNotChangeTimestamp`
- `MarkDelivered_SetsDeliveredStatus`
- `MarkDelivered_WhenAlreadyRead_DoesNotDowngrade`
- `MarkFailed_SetsFailedStatus`
- `Archive_SetsArchivedStatus`
- `OutboxItem_Create_SetsAllFields`
- `OutboxItem_Create_EmptyRecipient_Throws`
- `OutboxItem_Create_EmptyPayload_Throws`
- `OutboxItem_RecordAttempt_Success_SetsDelivered`
- `OutboxItem_RecordAttempt_Failure_SetsFailed`
- `OutboxItem_ResetForRetry_SetsQueued`

**Integration tests** (`LinguaCoach.IntegrationTests.Notifications.NotificationServiceTests` — 13 tests):
- `QueueInApp_CreatesNotificationRow`
- `QueueInApp_StartsWithQueuedStatus`
- `QueueInApp_CreatesOutboxItem`
- `QueueInApp_OutboxItemLinkedToNotification`
- `QueueInApp_StoresDeepLinkAndExpiry`
- `QueueInApp_StoresCategoryAndSeverity`
- `QueueEmail_CreatesNotificationAndOutboxItem`
- `QueueEmail_NoDeliveryAttempts_InThisPhase`
- `QueueSms_CreatesNotificationAndOutboxItem`
- `QueueSms_NoExternalDelivery_InThisPhase`
- `QueueAsync_WithMetadata_StoresMetadataJson`
- `QueueMultiple_EachCreatesOwnRows`

---

## 12. Frontend Changes

None. No frontend changes in this phase.

---

## 13. Gate Results

- `git diff --check`: PASS (CRLF warning only from EF snapshot, not a failure)
- `dotnet build --configuration Release`: PASS (0 errors, 5 warnings pre-existing)
- `dotnet test --configuration Release`: PASS — 2108/2108 (3 arch + 1278 unit + 827 integration; +28 from 2080)
- `npm run build -- --configuration production`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS — 896/896

---

## 14. Confirmation of No Unintended Changes

- Auth / reset-password behavior: unchanged.
- Usage governance behavior: unchanged.
- AI pricing / usage behavior: unchanged.
- Admin UI: unchanged.
- No new API endpoints.
- No seed data.

---

## 15. Risks and Deferred Items

| Item | Deferred to |
|---|---|
| `NotificationDeliveryAttempt` entity | 10W-4 (when dispatch worker is added) |
| `NotificationDispatchJob` (Quartz) | 10W-2 / 10W-4 |
| In-app bell API (`GET /api/notifications`, PATCH read) | 10W-2 |
| Bell UI (live data) | 10W-3 |
| `IEmailSender` + SMTP/SendGrid | 10W-4 |
| Reset-password email flow | 10W-4 |
| `NotificationTemplate` entity + renderer | 10W-5 |
| `NotificationPreference` per-user | 10W-5 |
| `ISmsProvider` + Twilio | 10W-6 |

---

## 16. Final Verdict

Phase 10W-1 complete. Backend notification foundation is in place: entities, enums, service abstraction, persistence, migration, DI registration, and full test coverage. The outbox is ready for the dispatch worker in 10W-2/10W-4. No external delivery occurs yet.

**Next recommended action:** Phase 10W-2 — in-app notification APIs and dispatch worker.

---

## Documentation impact

- Docs reviewed: `docs/reviews/2026-06-21-phase-10w-0-enterprise-notification-platform-gap-check.md`, `docs/sprints/current-sprint.md`, `TODOS.md`, `docs/handoffs/current-product-state.md`
- Docs updated: `docs/sprints/current-sprint.md`, `TODOS.md`, `docs/handoffs/current-product-state.md`
- Docs intentionally not updated: architecture README (no architecture change, only additive layer)
- Reason: 10W-1 is purely additive backend foundation; existing architecture is unchanged.
