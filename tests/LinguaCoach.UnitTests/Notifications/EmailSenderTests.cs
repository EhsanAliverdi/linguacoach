using FluentAssertions;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Notifications;

public sealed class EmailSenderTests
{
    private sealed class FakeResolver(ResolvedEmailConfig config) : INotificationChannelConfigResolver
    {
        public Task<ResolvedEmailConfig> ResolveEmailAsync(CancellationToken ct = default) => Task.FromResult(config);
        public Task<ResolvedSmsConfig> ResolveSmsAsync(CancellationToken ct = default) =>
            Task.FromResult(new ResolvedSmsConfig(false, null, null, null, "AppSettings"));
    }

    // Tracks which concrete sender was resolved by RoutingEmailSender
    private sealed class TrackingServiceProvider : IServiceProvider
    {
        public Type? LastResolved { get; private set; }

        private readonly SmtpEmailSender _smtp;
        private readonly ResendEmailSender _resend;
        private readonly SendGridEmailSender _sendGrid;

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
            LastResolved = serviceType;
            if (serviceType == typeof(SmtpEmailSender)) return _smtp;
            if (serviceType == typeof(ResendEmailSender)) return _resend;
            if (serviceType == typeof(SendGridEmailSender)) return _sendGrid;
            return null;
        }
    }

    // HttpClientFactory stub — ResendEmailSender never makes real network calls in routing tests
    // because the config is disabled, so the client is never used.
    private sealed class FakeHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name) => new();
    }

    private static SmtpEmailSender MakeSmtp(INotificationChannelConfigResolver resolver) =>
        new(resolver, NullLogger<SmtpEmailSender>.Instance);

    private static ResendEmailSender MakeResend(INotificationChannelConfigResolver resolver) =>
        new(resolver, new FakeHttpClientFactory(), NullLogger<ResendEmailSender>.Instance);

    private static SendGridEmailSender MakeSendGrid(INotificationChannelConfigResolver resolver) =>
        new(resolver, NullLogger<SendGridEmailSender>.Instance);

    private static EmailMessage SampleMessage() => new(
        ToAddress: "student@example.com",
        ToDisplayName: "Student",
        Subject: "Test",
        BodyHtml: "<p>Hello</p>",
        BodyText: "Hello");

    private static INotificationChannelConfigResolver ResolverWith(ResolvedEmailConfig config) => new FakeResolver(config);

    private static ResolvedEmailConfig DisabledConfig() => new(
        IsEnabled: false, Provider: "Smtp", Host: "", Port: 587, UseSsl: false,
        FromAddress: null, FromDisplayName: null,
        Username: null, PlaintextSecret: null, Source: "AppSettings");

    private static ResolvedEmailConfig EnabledNoHostConfig() => new(
        IsEnabled: true, Provider: "Smtp", Host: "", Port: 587, UseSsl: false,
        FromAddress: "a@b.com", FromDisplayName: null,
        Username: null, PlaintextSecret: null, Source: "AppSettings");

    // ── DisabledEmailSender ───────────────────────────────────────────────────

    [Fact]
    public async Task DisabledSender_ReturnsSkipped_DoesNotThrow()
    {
        var sender = new DisabledEmailSender(NullLogger<DisabledEmailSender>.Instance);

        var result = await sender.SendAsync(SampleMessage());

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DisabledSender_NeverThrows_EvenWithNullBody()
    {
        var sender = new DisabledEmailSender(NullLogger<DisabledEmailSender>.Instance);
        var msg = SampleMessage() with { BodyText = null };

        var act = () => sender.SendAsync(msg);

        await act.Should().NotThrowAsync();
    }

    // ── EmailOptions binding ──────────────────────────────────────────────────

    [Fact]
    public void EmailOptions_DefaultsToDisabled()
    {
        var opts = new EmailOptions();
        opts.Enabled.Should().BeFalse();
        opts.Host.Should().BeEmpty();
    }

    [Fact]
    public void EmailOptions_CanBindToEnabled()
    {
        var opts = new EmailOptions
        {
            Enabled = true,
            Host = "smtp.example.com",
            Port = 587,
            Username = "user",
            Password = "pass",
            FromAddress = "no-reply@example.com",
            FromDisplayName = "SpeakPath",
            UseSsl = true,
        };

        opts.Enabled.Should().BeTrue();
        opts.Host.Should().Be("smtp.example.com");
        opts.Port.Should().Be(587);
        opts.FromDisplayName.Should().Be("SpeakPath");
    }

    // ── SmtpEmailSender — disabled/unconfigured returns Skipped without network call ──

    [Fact]
    public async Task SmtpSender_WhenDisabled_ReturnsSkipped_NoNetworkCall()
    {
        var sender = new SmtpEmailSender(ResolverWith(DisabledConfig()), NullLogger<SmtpEmailSender>.Instance);

        var result = await sender.SendAsync(SampleMessage());

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task SmtpSender_WhenEnabledButNoHost_ReturnsSkipped()
    {
        var sender = new SmtpEmailSender(ResolverWith(EnabledNoHostConfig()), NullLogger<SmtpEmailSender>.Instance);

        var result = await sender.SendAsync(SampleMessage());

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task SmtpSender_WhenEnabledButNoFromAddress_ReturnsSkipped()
    {
        var config = new ResolvedEmailConfig(
            IsEnabled: true, Provider: "Smtp", Host: "smtp.test.com", Port: 587, UseSsl: false,
            FromAddress: null, FromDisplayName: null,
            Username: null, PlaintextSecret: null, Source: "Database");
        var sender = new SmtpEmailSender(ResolverWith(config), NullLogger<SmtpEmailSender>.Instance);

        var result = await sender.SendAsync(SampleMessage());

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
    }

    // ── EmailSendResult factory methods ──────────────────────────────────────

    [Fact]
    public void EmailSendResult_Ok_IsSucceeded()
    {
        var r = EmailSendResult.Ok();
        r.Succeeded.Should().BeTrue();
        r.WasSkipped.Should().BeFalse();
        r.Error.Should().BeNull();
    }

    [Fact]
    public void EmailSendResult_Skipped_IsSkipped()
    {
        var r = EmailSendResult.Skipped("No provider");
        r.Succeeded.Should().BeFalse();
        r.WasSkipped.Should().BeTrue();
        r.Error.Should().Be("No provider");
    }

    [Fact]
    public void EmailSendResult_Failure_IsFailure()
    {
        var r = EmailSendResult.Failure("SMTP timeout");
        r.Succeeded.Should().BeFalse();
        r.WasSkipped.Should().BeFalse();
        r.Error.Should().Be("SMTP timeout");
    }

    // ── RoutingEmailSender — provider routing ────────────────────────────────

    private RoutingEmailSender MakeRouter(string provider, TrackingServiceProvider sp)
    {
        var config = new ResolvedEmailConfig(
            IsEnabled: false, Provider: provider, Host: null, Port: 587, UseSsl: false,
            FromAddress: null, FromDisplayName: null,
            Username: null, PlaintextSecret: null, Source: "AppSettings");
        var resolver = new FakeResolver(config);
        return new RoutingEmailSender(resolver, sp, NullLogger<RoutingEmailSender>.Instance);
    }

    private TrackingServiceProvider MakeTracker(INotificationChannelConfigResolver? resolver = null)
    {
        var r = resolver ?? new FakeResolver(DisabledConfig());
        return new TrackingServiceProvider(MakeSmtp(r), MakeResend(r), MakeSendGrid(r));
    }

    [Fact]
    public async Task Router_SmtpProvider_ResolvesSmtpSender()
    {
        var tracker = MakeTracker();
        var router = MakeRouter("Smtp", tracker);

        await router.SendAsync(SampleMessage());

        tracker.LastResolved.Should().Be(typeof(SmtpEmailSender));
    }

    [Fact]
    public async Task Router_ResendProvider_ResolvesResendSender()
    {
        var tracker = MakeTracker();
        var router = MakeRouter("Resend", tracker);

        await router.SendAsync(SampleMessage());

        tracker.LastResolved.Should().Be(typeof(ResendEmailSender));
    }

    [Fact]
    public async Task Router_SendGridProvider_ResolvesSendGridSender()
    {
        var tracker = MakeTracker();
        var router = MakeRouter("SendGrid", tracker);

        await router.SendAsync(SampleMessage());

        tracker.LastResolved.Should().Be(typeof(SendGridEmailSender));
    }

    [Fact]
    public async Task Router_ProviderCaseInsensitive_ResendLowercase_ResolvesResendSender()
    {
        var tracker = MakeTracker();
        var router = MakeRouter("resend", tracker);

        await router.SendAsync(SampleMessage());

        tracker.LastResolved.Should().Be(typeof(ResendEmailSender));
    }

    [Fact]
    public async Task Router_NullOrEmptyProvider_DefaultsToSmtp()
    {
        var tracker = MakeTracker();
        var router = MakeRouter("", tracker);

        await router.SendAsync(SampleMessage());

        tracker.LastResolved.Should().Be(typeof(SmtpEmailSender));
    }

    [Fact]
    public async Task Router_UnknownProvider_DefaultsToSmtp()
    {
        var tracker = MakeTracker();
        var router = MakeRouter("Mailgun", tracker);

        await router.SendAsync(SampleMessage());

        tracker.LastResolved.Should().Be(typeof(SmtpEmailSender));
    }
}
