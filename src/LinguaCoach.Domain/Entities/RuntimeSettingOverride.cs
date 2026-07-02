using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Admin-controlled runtime override for a single feature-gate registry key.
/// Backs settings that otherwise only live in appsettings.json (e.g. ReadinessPool
/// review-scaffold / Practice Gym pilot flags), letting an admin change them without
/// a redeploy. Never stores secrets, provider keys, or connection strings.
/// </summary>
public sealed class RuntimeSettingOverride : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string ValueJson { get; private set; } = string.Empty;
    public string DataType { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string? Reason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // EF Core.
    private RuntimeSettingOverride() { }

    public RuntimeSettingOverride(string key, string valueJson, string dataType, Guid createdByUserId, string? reason)
    {
        Key = key;
        ValueJson = valueJson;
        DataType = dataType;
        IsActive = true;
        Reason = reason;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Apply(string valueJson, Guid updatedByUserId, string? reason)
    {
        ValueJson = valueJson;
        IsActive = true;
        Reason = reason;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId, string? reason)
    {
        IsActive = false;
        Reason = reason;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
