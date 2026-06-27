using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Routes to SmtpEmailSender, ResendEmailSender, or SendGridEmailSender
/// based on the resolved provider name at send time.
/// Registered as IEmailSender in DI.
/// </summary>
public sealed class RoutingEmailSender : IEmailSender
{
    private readonly INotificationChannelConfigResolver _configResolver;
    private readonly IServiceProvider _sp;
    private readonly ILogger<RoutingEmailSender> _logger;

    public RoutingEmailSender(
        INotificationChannelConfigResolver configResolver,
        IServiceProvider sp,
        ILogger<RoutingEmailSender> logger)
    {
        _configResolver = configResolver;
        _sp = sp;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var config = await _configResolver.ResolveEmailAsync(ct);

        var provider = string.IsNullOrWhiteSpace(config.Provider) ? "Smtp" : config.Provider.Trim();

        IEmailSender sender = provider.ToLowerInvariant() switch
        {
            "resend"   => _sp.GetRequiredService<ResendEmailSender>(),
            "sendgrid" => _sp.GetRequiredService<SendGridEmailSender>(),
            _          => _sp.GetRequiredService<SmtpEmailSender>(),
        };

        _logger.LogDebug("RoutingEmailSender: routing to {Provider} for {To}", provider, message.ToAddress);
        return await sender.SendAsync(message, ct);
    }
}
