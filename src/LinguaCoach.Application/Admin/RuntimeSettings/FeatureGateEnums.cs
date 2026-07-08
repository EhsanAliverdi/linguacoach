namespace LinguaCoach.Application.Admin.RuntimeSettings;

public enum FeatureGateCategory
{
    ReviewScaffoldPracticeGymPilot,
    ReadinessPoolLessonGeneration,
    AiSignalSafety,
    PracticeGymFormIoTemplatePilot,
    ActivityFeedback,
}

public enum FeatureGateDataType
{
    Boolean,
    Integer,
    String,
    StringArray,
}

public enum FeatureGateRiskLevel
{
    Low,
    Medium,
    High,
    Critical,
}

public enum FeatureGateValueSource
{
    AppSettings,
    DatabaseOverride,
    Default,
    Hardcoded,
}

/// <summary>Where a group's effective value/editing logic is backed from.</summary>
public enum FeatureGateBackingStore
{
    /// <summary>Backed by <c>ReadinessPoolReplenishmentOptions</c> (appsettings) + <c>RuntimeSettingOverride</c> rows.</summary>
    ReadinessPoolOverride,

    /// <summary>Backed by the existing single-row <c>LessonGenerationSettings</c> DB table.</summary>
    LessonGenerationSettingsTable,

    /// <summary>Backed by appsettings only. Always read-only in this phase.</summary>
    AppSettingsReadOnly,

    /// <summary>No backing flag exists in code. Informational/visibility only.</summary>
    Informational,
}
