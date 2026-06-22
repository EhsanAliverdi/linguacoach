using LinguaCoach.Application.Notifications;
using Microsoft.AspNetCore.DataProtection;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Protects notification channel secrets using ASP.NET Core Data Protection.
/// Keys are managed by the runtime key ring (file system by default).
///
/// Deployment note: configure key persistence for production:
///   services.AddDataProtection()
///           .PersistKeysToFileSystem(new DirectoryInfo("/var/keys"))
///           .SetApplicationName("LinguaCoach");
/// Without persistent keys, protected values cannot be unprotected after a restart.
///
/// Base64 fallback: if an existing stored value cannot be unprotected by Data Protection
/// (e.g. it was written by the old Base64 placeholder), this class attempts to decode it
/// as Base64 UTF-8. This allows existing values to be read until the next update re-protects them.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "LinguaCoach.NotificationChannelSecret.v1";
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return _protector.Protect(plaintext);
    }

    public string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return null;

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch
        {
            // Fallback: try Base64 UTF-8 from the old placeholder implementation.
            // If this also fails, return null (treat as no secret configured).
            try
            {
                var bytes = Convert.FromBase64String(protectedValue);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
