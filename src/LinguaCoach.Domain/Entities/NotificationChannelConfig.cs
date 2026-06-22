namespace LinguaCoach.Domain.Entities;

/// <summary>
/// DB-backed override for a notification channel's configuration.
/// Appsettings remains the safe fallback; a row here wins when present.
/// Secrets are stored encrypted (SecretEncrypted) and never returned raw to callers.
/// </summary>
public sealed class NotificationChannelConfig
{
    public Guid Id { get; private set; }

    /// <summary>Channel name: "Email", "Sms", "InApp".</summary>
    public string Channel { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    /// <summary>Provider name, e.g. "Smtp", "Twilio". Null for InApp.</summary>
    public string? Provider { get; private set; }

    // ── Email / SMTP fields ───────────────────────────────────────────────────

    public string? FromAddress { get; private set; }
    public string? FromDisplayName { get; private set; }
    public string? Host { get; private set; }
    public int? Port { get; private set; }
    public bool? UseSsl { get; private set; }
    public string? Username { get; private set; }

    // ── SMS fields ────────────────────────────────────────────────────────────

    public string? SenderId { get; private set; }

    // ── Secret storage ────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypted secret (password or API key). Never returned to frontend.
    /// Null means no secret stored — appsettings value will be used by the resolver.
    /// </summary>
    public string? SecretEncrypted { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByAdminUserId { get; private set; }

    // ── EF Core constructor ───────────────────────────────────────────────────

    private NotificationChannelConfig() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static NotificationChannelConfig Create(string channel)
        => new()
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            IsEnabled = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

    // ── Mutation methods ──────────────────────────────────────────────────────

    public void UpdateEmail(
        bool isEnabled,
        string? host,
        int? port,
        bool? useSsl,
        string? fromAddress,
        string? fromDisplayName,
        string? username,
        string? secretEncrypted,
        bool clearSecret,
        Guid updatedByAdminUserId)
    {
        IsEnabled = isEnabled;
        Provider = "Smtp";
        Host = host;
        Port = port;
        UseSsl = useSsl;
        FromAddress = fromAddress;
        FromDisplayName = fromDisplayName;
        Username = username;

        if (clearSecret)
            SecretEncrypted = null;
        else if (secretEncrypted is not null)
            SecretEncrypted = secretEncrypted;
        // If neither: preserve existing secret unchanged.

        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    public void UpdateSms(
        bool isEnabled,
        string? provider,
        string? senderId,
        string? secretEncrypted,
        bool clearSecret,
        Guid updatedByAdminUserId)
    {
        IsEnabled = isEnabled;
        Provider = provider;
        SenderId = senderId;

        if (clearSecret)
            SecretEncrypted = null;
        else if (secretEncrypted is not null)
            SecretEncrypted = secretEncrypted;

        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    public void UpdateInApp(bool isEnabled, Guid updatedByAdminUserId)
    {
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    public bool HasSecret => !string.IsNullOrWhiteSpace(SecretEncrypted);
}
