namespace LinguaCoach.Application.Notifications;

public enum DataProtectionKeyMode
{
    /// <summary>
    /// Keys are persisted to disk with no additional encryption.
    /// Safe for development. Not recommended for production — anyone with
    /// filesystem access can read the key XML files.
    /// </summary>
    None,

    /// <summary>
    /// Keys are encrypted at rest using an X.509 certificate.
    /// Set CertificatePath + CertificatePassword, or CertificateThumbprint
    /// (Windows certificate store) to activate this mode.
    /// Recommended for production single-host deployments.
    /// </summary>
    Certificate,
}

public sealed class NotificationKeyProtectionOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Directory where Data Protection keys are persisted.
    /// Must be outside the container ephemeral layer in production.
    /// Override via DataProtection__KeysPath env var or appsettings.
    /// </summary>
    public string KeysPath { get; set; } = "./app-data/data-protection-keys";

    /// <summary>
    /// Application name used to scope the key ring.
    /// Must be identical across all instances that share encrypted data.
    /// </summary>
    public string ApplicationName { get; set; } = "SpeakPath";

    /// <summary>
    /// Key-at-rest protection mode. Default: None (keys stored as plaintext XML).
    /// Set to Certificate for production deployments.
    /// </summary>
    public DataProtectionKeyMode KeyProtectionMode { get; set; } = DataProtectionKeyMode.None;

    /// <summary>
    /// Path to a PFX/PKCS12 certificate file used to encrypt keys at rest.
    /// Only used when KeyProtectionMode=Certificate.
    /// Never commit certificate files to source control.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for the PFX certificate. Never logged or returned by any API.
    /// Only used when KeyProtectionMode=Certificate and CertificatePath is set.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Windows certificate store thumbprint. Alternative to CertificatePath.
    /// Only used on Windows when KeyProtectionMode=Certificate.
    /// </summary>
    public string? CertificateThumbprint { get; set; }
}
