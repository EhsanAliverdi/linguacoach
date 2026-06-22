namespace LinguaCoach.Infrastructure.Notifications;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;

    // Secret — never returned to frontend. Check HasApiKey instead.
    public string ApiKey { get; set; } = string.Empty;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Provider) && HasApiKey;
}
