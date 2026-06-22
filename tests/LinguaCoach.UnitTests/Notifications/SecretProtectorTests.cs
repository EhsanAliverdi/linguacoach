using FluentAssertions;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.UnitTests.Notifications;

public sealed class SecretProtectorTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly ISecretProtector _protector;

    public SecretProtectorTests()
    {
        _sp = new ServiceCollection()
            .AddDataProtection()
            .SetApplicationName("LinguaCoach.Test")
            .Services
            .AddSingleton<ISecretProtector, DataProtectionSecretProtector>()
            .BuildServiceProvider();

        _protector = _sp.GetRequiredService<ISecretProtector>();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void Protect_Unprotect_RoundTrips_Correctly()
    {
        const string plaintext = "super-secret-password-123";

        var ciphertext = _protector.Protect(plaintext);
        var recovered = _protector.Unprotect(ciphertext);

        recovered.Should().Be(plaintext);
    }

    [Fact]
    public void Protect_DoesNotReturnPlaintext()
    {
        const string plaintext = "my-smtp-password";

        var ciphertext = _protector.Protect(plaintext);

        ciphertext.Should().NotBe(plaintext);
        ciphertext.Should().NotContain(plaintext);
    }

    [Fact]
    public void Protect_ProducesUniqueValuesEachCall()
    {
        const string plaintext = "same-value";

        var a = _protector.Protect(plaintext);
        var b = _protector.Protect(plaintext);

        // Data Protection uses nonce-based encryption — each call differs
        a.Should().NotBe(b);
    }

    [Fact]
    public void Unprotect_Null_ReturnsNull()
    {
        var result = _protector.Unprotect(null);
        result.Should().BeNull();
    }

    [Fact]
    public void Unprotect_Base64Fallback_ReturnsDecodedString()
    {
        // Simulate a value stored by the old Base64 placeholder path
        var plaintext = "legacy-base64-secret";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        var result = _protector.Unprotect(base64);

        result.Should().Be(plaintext);
    }

    [Fact]
    public void Unprotect_InvalidValue_ReturnsNull()
    {
        // Not a valid Data Protection token and not valid Base64
        var result = _protector.Unprotect("not-a-real-token!!!");
        result.Should().BeNull();
    }

    [Fact]
    public void Protect_EmptyString_Throws()
    {
        var act = () => _protector.Protect(string.Empty);
        act.Should().Throw<ArgumentException>();
    }
}
