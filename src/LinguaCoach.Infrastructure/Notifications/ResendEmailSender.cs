using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Sends email via the Resend REST API (https://resend.com).
/// PlaintextSecret in ResolvedEmailConfig holds the Resend API key (re_...).
/// No third-party NuGet package — uses HttpClient directly for net10 compatibility.
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private readonly INotificationChannelConfigResolver _configResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ResendEmailSender> _logger;

    private const string ResendApiUrl = "https://api.resend.com/emails";

    public ResendEmailSender(
        INotificationChannelConfigResolver configResolver,
        IHttpClientFactory httpClientFactory,
        ILogger<ResendEmailSender> logger)
    {
        _configResolver = configResolver;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var config = await _configResolver.ResolveEmailAsync(ct);

        if (!config.IsEnabled)
            return EmailSendResult.Skipped("Email sender is disabled.");

        if (string.IsNullOrWhiteSpace(config.PlaintextSecret))
            return EmailSendResult.Skipped("Resend API key is not configured.");

        if (string.IsNullOrWhiteSpace(config.FromAddress))
            return EmailSendResult.Skipped("Email sender has no From address configured.");

        var from = string.IsNullOrWhiteSpace(config.FromDisplayName)
            ? config.FromAddress
            : $"{config.FromDisplayName} <{config.FromAddress}>";

        var payload = new
        {
            from,
            to = new[] { string.IsNullOrWhiteSpace(message.ToDisplayName)
                ? message.ToAddress
                : $"{message.ToDisplayName} <{message.ToAddress}>" },
            subject = message.Subject,
            html = message.BodyHtml,
            text = message.BodyText,
        };

        try
        {
            var client = _httpClientFactory.CreateClient("Resend");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.PlaintextSecret);

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(ResendApiUrl, content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Resend email sent to {To} subject={Subject} via {Source}",
                    message.ToAddress, message.Subject, config.Source);
                return EmailSendResult.Ok();
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Resend API error {Status} to {To}: {Body}",
                (int)response.StatusCode, message.ToAddress, body);
            return EmailSendResult.Failure($"Resend API returned {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend send failed to {To}", message.ToAddress);
            return EmailSendResult.Failure(ex.Message);
        }
    }
}
