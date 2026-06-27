namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Bound from configuration section "Email".
/// App does not crash when values are missing — DisabledEmailSender is used instead.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = false;
    /// <summary>Provider: "Smtp" (default), "Resend", or "SendGrid".</summary>
    public string Provider { get; set; } = "Smtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "SpeakPath";
    public bool UseSsl { get; set; } = true;
}
