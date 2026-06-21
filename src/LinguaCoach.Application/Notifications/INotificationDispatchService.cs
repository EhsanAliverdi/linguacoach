namespace LinguaCoach.Application.Notifications;

public interface INotificationDispatchService
{
    /// <summary>
    /// Processes due outbox items. InApp items are marked delivered.
    /// Email/SMS items are skipped until external providers are wired (10W-4/10W-6).
    /// </summary>
    Task<DispatchResult> DispatchDueAsync(int batchSize = 50, CancellationToken ct = default);
}

public sealed record DispatchResult(int Processed, int Skipped, int Failed);
