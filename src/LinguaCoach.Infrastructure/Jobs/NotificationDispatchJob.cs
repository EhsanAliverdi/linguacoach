using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Scheduled Quartz job that processes due notification_outbox_items.
/// Runs every 2 minutes. InApp items are marked delivered. Email items are
/// sent via IEmailSender. SMS items are skipped until 10W-6.
/// </summary>
[DisallowConcurrentExecution]
public sealed class NotificationDispatchJob : IJob
{
    public const string JobName = "notification-dispatch";

    private readonly INotificationDispatchService _dispatch;
    private readonly ILogger<NotificationDispatchJob> _logger;

    public NotificationDispatchJob(
        INotificationDispatchService dispatch,
        ILogger<NotificationDispatchJob> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("NotificationDispatchJob starting.");

        try
        {
            var result = await _dispatch.DispatchDueAsync(
                batchSize: 50,
                ct: context.CancellationToken);

            _logger.LogInformation(
                "NotificationDispatchJob complete. processed={P} skipped={S} failed={F}",
                result.Processed, result.Skipped, result.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationDispatchJob failed.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
