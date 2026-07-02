namespace LinguaCoach.Application.Admin.StudentReadiness;

/// <summary>Result of a single readiness check.</summary>
public enum ReadinessCheckStatus
{
    Pass,
    Warning,
    Fail,
    NotApplicable,
    NotImplemented,
}

/// <summary>How much a check's failure matters for "ready for pilot."</summary>
public enum ReadinessCheckSeverity
{
    Info,
    Warning,
    Blocking,
}

/// <summary>Overall pilot-readiness verdict for a student.</summary>
public enum ReadinessOverallStatus
{
    Ready,
    NeedsAttention,
    Blocked,
    NotStarted,
}

/// <summary>Risk level of a repair action, mirrors Phase 20B's FeatureGateRiskLevel naming.</summary>
public enum ReadinessRepairRiskLevel
{
    Low,
    Medium,
    High,
}
