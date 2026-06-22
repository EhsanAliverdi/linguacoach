namespace LinguaCoach.Application.Notifications;

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
}
