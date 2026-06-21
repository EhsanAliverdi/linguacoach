using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class NotificationTemplate : BaseEntity
{
    public string TemplateKey { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string? Title { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public NotificationCategory Category { get; private set; }
    public NotificationSeverity Severity { get; private set; }
    public bool IsActive { get; private set; }
    public int Version { get; private set; }
    public string? SupportedVariablesJson { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private NotificationTemplate() { }

    public static NotificationTemplate Create(
        string templateKey,
        NotificationChannel channel,
        string name,
        string body,
        NotificationCategory category,
        NotificationSeverity severity,
        string? subject = null,
        string? title = null,
        string? description = null,
        string? supportedVariablesJson = null)
    {
        ValidateCreate(templateKey, channel, name, body, subject, title);

        return new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = templateKey.Trim(),
            Channel = channel,
            Name = name.Trim(),
            Subject = subject?.Trim(),
            Title = title?.Trim(),
            Body = body.Trim(),
            Category = category,
            Severity = severity,
            IsActive = true,
            Version = 1,
            Description = description?.Trim(),
            SupportedVariablesJson = supportedVariablesJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null,
        };
    }

    public void Update(
        string name,
        string body,
        string? subject,
        string? title,
        NotificationCategory category,
        NotificationSeverity severity,
        string? description,
        string? supportedVariablesJson)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body is required.", nameof(body));
        if (Channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required for Email templates.", nameof(subject));
        if (Channel == NotificationChannel.InApp && string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required for InApp templates.", nameof(title));

        Name = name.Trim();
        Body = body.Trim();
        Subject = subject?.Trim();
        Title = title?.Trim();
        Category = category;
        Severity = severity;
        Description = description?.Trim();
        SupportedVariablesJson = supportedVariablesJson;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateCreate(
        string templateKey, NotificationChannel channel,
        string name, string body, string? subject, string? title)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            throw new ArgumentException("TemplateKey is required.", nameof(templateKey));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));
        if (channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required for Email templates.", nameof(subject));
        if (channel == NotificationChannel.InApp && string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required for InApp templates.", nameof(title));
    }
}
