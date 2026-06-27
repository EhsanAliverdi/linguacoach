using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Notifications;

/// <summary>
/// Unit tests for RoutingEmailSender provider dispatch logic.
/// Uses a TrackingServiceProvider (no mocking framework needed) to verify
/// which concrete sender type is resolved for each provider name.
/// </summary>
public sealed class RoutingEmailSenderTests
{
    // ── Stub resolver ─────────────────────────────────────────────────────────

    private sealed class StubResolver : INotificationChannelConfigResolver
    {
        private readonly ResolvedEmailConfig _config;
        public StubResolver(ResolvedEmailConfig config) => _config = config;
        public Task<ResolvedEmailConfig> ResolveEmailAsync(CancellationToken ct = default) => Task.FromResult(_config);
        public Task<ResolvedSmsConfig> ResolveSmsAsync(CancellationToken ct = default) =>
            Task.FromResult(new ResolvedSmsConfig(false, null, null, null, "Test"));
    }

    // ── Tracking service provider ─────────────────────────────────────────────

    private sealed class TrackingServiceProvider : IServiceProvider
    {
        private readonly SmtpEmailSender _smtp;
        private readonly ResendEmailSender _resend;
        private readonly SendGridEmailSender _sendGrid;

        public readonly List<Type> RequestedTypes = [];

        public TrackingServiceProvider(
            SmtpEmailSender smtp,
            ResendEmailSender resend,
            SendGridEmailSender sendGrid)
        {
            _smtp = smtp;
            _resend = resend;
            _sendGrid = sendGrid;
        }

        public object? GetService(Type serviceType)
        {
            RequestedTypes.Add(serviceType);
            if (serviceType == typeof(SmtpEmailSender))    return _smtp;
            if (serviceType == typeof(ResendEmailSender))  return _resend;
            if (serviceType == typeof(SendGridEmailSender)) return _sendGrid;
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IHttpClientFactory BuildHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("Resend");
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private static (RoutingEmailSender Sender, TrackingServiceProvider Tracker) Build(string? provider)
    {
        var config = new ResolvedEmailConfig(
            IsEnabled: false, Provider: provider,
            Host: null, Port: 587, UseSsl: false,
            FromAddress: null, FromDisplayName: null,
            Username: null, PlaintextSecret: null, Source: "Test");

        var resolver = new StubResolver(config);

        var smtp = new SmtpEmailSender(resolver, NullLogger<SmtpEmailSender>.Instance);
        var resend = new ResendEmailSender(resolver, BuildHttpClientFactory(), NullLogger<ResendEmailSender>.Instance);
        var sendGrid = new SendGridEmailSender(resolver, NullLogger<SendGridEmailSender>.Instance);

        var tracker = new TrackingServiceProvider(smtp, resend, sendGrid);
        var routing = new RoutingEmailSender(resolver, tracker, NullLogger<RoutingEmailSender>.Instance);
        return (routing, tracker);
    }

    private static readonly EmailMessage Dummy =
        new("t@t.com", "Test User", "Subject", "<p>body</p>", "body");

    // ── Routing by provider name ──────────────────────────────────────────────

    [Fact]
    public async Task SmtpProvider_ResolvesSmtpSender()
    {
        var (sender, tracker) = Build("Smtp");
        await sender.SendAsync(Dummy);
        Assert.Contains(typeof(SmtpEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(ResendEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(SendGridEmailSender), tracker.RequestedTypes);
    }

    [Theory]
    [InlineData("Resend")]
    [InlineData("resend")]
    [InlineData("RESEND")]
    public async Task ResendProvider_ResolvesResendSender(string provider)
    {
        var (sender, tracker) = Build(provider);
        await sender.SendAsync(Dummy);
        Assert.Contains(typeof(ResendEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(SmtpEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(SendGridEmailSender), tracker.RequestedTypes);
    }

    [Theory]
    [InlineData("SendGrid")]
    [InlineData("sendgrid")]
    [InlineData("SENDGRID")]
    public async Task SendGridProvider_ResolvesSendGridSender(string provider)
    {
        var (sender, tracker) = Build(provider);
        await sender.SendAsync(Dummy);
        Assert.Contains(typeof(SendGridEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(SmtpEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(ResendEmailSender), tracker.RequestedTypes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownProvider")]
    [InlineData("mailgun")]
    public async Task MissingOrUnknownProvider_DefaultsToSmtpSender(string? provider)
    {
        var (sender, tracker) = Build(provider);
        await sender.SendAsync(Dummy);
        Assert.Contains(typeof(SmtpEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(ResendEmailSender), tracker.RequestedTypes);
        Assert.DoesNotContain(typeof(SendGridEmailSender), tracker.RequestedTypes);
    }

    // ── Resend/SendGrid return distinctive skip messages when key missing ──────

    [Fact]
    public async Task ResendProvider_WhenNoApiKey_ReturnsSkippedWithResendMessage()
    {
        var config = new ResolvedEmailConfig(
            IsEnabled: true, Provider: "Resend",
            Host: null, Port: 587, UseSsl: false,
            FromAddress: "from@example.com", FromDisplayName: "Test",
            Username: null, PlaintextSecret: null, Source: "Test");

        var resolver = new StubResolver(config);
        var resend = new ResendEmailSender(resolver, BuildHttpClientFactory(), NullLogger<ResendEmailSender>.Instance);
        var smtp = new SmtpEmailSender(resolver, NullLogger<SmtpEmailSender>.Instance);
        var sendGrid = new SendGridEmailSender(resolver, NullLogger<SendGridEmailSender>.Instance);

        var tracker = new TrackingServiceProvider(smtp, resend, sendGrid);
        var routing = new RoutingEmailSender(resolver, tracker, NullLogger<RoutingEmailSender>.Instance);

        var result = await routing.SendAsync(Dummy);

        Assert.True(result.WasSkipped);
        Assert.Contains("Resend API key", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendGridProvider_WhenNoApiKey_ReturnsSkippedWithSendGridMessage()
    {
        var config = new ResolvedEmailConfig(
            IsEnabled: true, Provider: "SendGrid",
            Host: null, Port: 587, UseSsl: false,
            FromAddress: "from@example.com", FromDisplayName: "Test",
            Username: null, PlaintextSecret: null, Source: "Test");

        var resolver = new StubResolver(config);
        var resend = new ResendEmailSender(resolver, BuildHttpClientFactory(), NullLogger<ResendEmailSender>.Instance);
        var smtp = new SmtpEmailSender(resolver, NullLogger<SmtpEmailSender>.Instance);
        var sendGrid = new SendGridEmailSender(resolver, NullLogger<SendGridEmailSender>.Instance);

        var tracker = new TrackingServiceProvider(smtp, resend, sendGrid);
        var routing = new RoutingEmailSender(resolver, tracker, NullLogger<RoutingEmailSender>.Instance);

        var result = await routing.SendAsync(Dummy);

        Assert.True(result.WasSkipped);
        Assert.Contains("SendGrid API key", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
