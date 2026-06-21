using FluentAssertions;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Notifications;

public sealed class EmailSenderTests
{
    private static EmailMessage SampleMessage() => new(
        ToAddress: "student@example.com",
        ToDisplayName: "Student",
        Subject: "Test",
        BodyHtml: "<p>Hello</p>",
        BodyText: "Hello");

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

    // ── SmtpEmailSender — disabled config returns Skipped without network call ──

    [Fact]
    public async Task SmtpSender_WhenDisabled_ReturnsSkipped_NoNetworkCall()
    {
        var opts = Options.Create(new EmailOptions { Enabled = false, Host = "" });
        var sender = new SmtpEmailSender(opts, NullLogger<SmtpEmailSender>.Instance);

        var result = await sender.SendAsync(SampleMessage());

        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task SmtpSender_WhenEnabledButNoHost_ReturnsSkipped()
    {
        var opts = Options.Create(new EmailOptions { Enabled = true, Host = "" });
        var sender = new SmtpEmailSender(opts, NullLogger<SmtpEmailSender>.Instance);

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
}
