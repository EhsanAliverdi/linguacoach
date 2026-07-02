namespace LinguaCoach.Application.Admin.StudentReadiness;

public sealed record StudentReadinessCheckDto
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required ReadinessCheckStatus Status { get; init; }
    public required ReadinessCheckSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? TechnicalDetail { get; init; }
    public string? RecommendedActionKey { get; init; }
    public bool CanRepair { get; init; }
    public ReadinessRepairRiskLevel? RepairRiskLevel { get; init; }
    public required DateTime LastCheckedAtUtc { get; init; }
}

public sealed record StudentReadinessSummaryDto
{
    public required Guid StudentId { get; init; }
    public string? StudentEmail { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required bool ReadyForPilot { get; init; }
    public required ReadinessOverallStatus ReadinessStatus { get; init; }
    public required int BlockingIssueCount { get; init; }
    public required int WarningCount { get; init; }
    public required int InfoCount { get; init; }
    public DateTime? LastRepairAtUtc { get; init; }
    public required IReadOnlyList<StudentReadinessCheckDto> Checks { get; init; }
    public required IReadOnlyList<string> RecommendedActions { get; init; }
    public required IReadOnlyList<string> UnavailableSections { get; init; }
}

public sealed record StudentReadinessRepairActionDefinitionDto
{
    public required string ActionKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required ReadinessRepairRiskLevel RiskLevel { get; init; }
    public required bool IsImplemented { get; init; }
    public bool SupportsDryRun { get; init; } = true;
}

public sealed record StudentReadinessRepairRequestDto
{
    public required string ActionKey { get; init; }
    public string? Reason { get; init; }
    public bool DryRun { get; init; } = true;
}

public sealed record StudentReadinessRepairResultDto
{
    public required string ActionKey { get; init; }
    public required bool DryRun { get; init; }
    public required int ChangedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public string? BeforeSummary { get; init; }
    public string? AfterSummary { get; init; }
    public Guid? AuditLogId { get; init; }
}
