namespace LinguaCoach.Application.Notifications;

/// <summary>
/// Protects and unprotects short secret values (SMTP passwords, API keys).
/// Secrets must never be returned raw to API callers or written to logs.
/// Implementations should use a cryptographically secure mechanism (e.g. ASP.NET Core Data Protection).
/// </summary>
public interface ISecretProtector
{
    /// <summary>Returns an opaque protected representation of <paramref name="plaintext"/>.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Returns the original plaintext. Throws <see cref="CryptographicException"/> or
    /// <see cref="InvalidOperationException"/> if the value is tampered or cannot be unprotected.
    /// Returns null if <paramref name="protectedValue"/> is null or empty.
    /// </summary>
    string? Unprotect(string? protectedValue);
}
