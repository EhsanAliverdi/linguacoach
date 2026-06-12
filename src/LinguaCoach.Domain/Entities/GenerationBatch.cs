using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Product-level tracking of a background lesson-generation batch for one student.
/// Separate from Quartz internals so Admin can see what happened.
/// </summary>
public sealed class GenerationBatch : BaseEntity
{
    public const string AdminCancelledFailureReason = "Cancelled by admin.";

    public Guid StudentProfileId { get; private set; }
    public GenerationTriggerReason TriggerReason { get; private set; }
    public GenerationBatchStatus Status { get; private set; }

    public int RequestedSessionCount { get; private set; }
    public int CompletedSessionCount { get; private set; }

    public string? SummarySnapshotJson { get; private set; }
    public string? PromptVersion { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ModelName { get; private set; }
    public string? CorrelationId { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Safe, admin-visible error message (sanitized — never raw provider errors with secrets).</summary>
    public string? FailureReason { get; private set; }

    public IReadOnlyList<GenerationJobItem> Items => _items.AsReadOnly();
    private readonly List<GenerationJobItem> _items = [];

    private GenerationBatch() { }

    public GenerationBatch(
        Guid studentProfileId,
        GenerationTriggerReason triggerReason,
        int requestedSessionCount,
        string? correlationId = null)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (requestedSessionCount < 1)
            throw new ArgumentException("RequestedSessionCount must be positive.", nameof(requestedSessionCount));

        StudentProfileId = studentProfileId;
        TriggerReason = triggerReason;
        RequestedSessionCount = requestedSessionCount;
        CorrelationId = correlationId;
        Status = GenerationBatchStatus.Queued;
    }

    public void MarkRunning(string? providerName = null, string? modelName = null, string? promptVersion = null, string? summarySnapshotJson = null)
    {
        Status = GenerationBatchStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
        if (providerName is not null) ProviderName = providerName;
        if (modelName is not null) ModelName = modelName;
        if (promptVersion is not null) PromptVersion = promptVersion;
        if (summarySnapshotJson is not null) SummarySnapshotJson = summarySnapshotJson;
    }

    public void IncrementCompleted() => CompletedSessionCount++;

    public void MarkCompleted()
    {
        Status = CompletedSessionCount >= RequestedSessionCount
            ? GenerationBatchStatus.Completed
            : (CompletedSessionCount > 0 ? GenerationBatchStatus.Partial : GenerationBatchStatus.Failed);
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string failureReason)
    {
        Status = GenerationBatchStatus.Failed;
        FailureReason = failureReason;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkCancelledByAdmin() => MarkFailed(AdminCancelledFailureReason);

    public void ResetForRetry()
    {
        Status = GenerationBatchStatus.Queued;
        FailureReason = null;
        CompletedAtUtc = null;
    }

    public GenerationJobItem AddItem(GenerationJobItemType itemType, Guid? targetEntityId = null)
    {
        var item = new GenerationJobItem(Id, itemType, targetEntityId);
        _items.Add(item);
        return item;
    }
}
