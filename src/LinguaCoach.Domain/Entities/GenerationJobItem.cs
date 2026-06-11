using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>Tracks one unit of work inside a GenerationBatch.</summary>
public sealed class GenerationJobItem : BaseEntity
{
    public Guid GenerationBatchId { get; private set; }
    public GenerationJobItemType ItemType { get; private set; }
    public Guid? TargetEntityId { get; private set; }
    public GenerationJobItemStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextRetryAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private GenerationJobItem() { }

    public GenerationJobItem(Guid generationBatchId, GenerationJobItemType itemType, Guid? targetEntityId = null)
    {
        if (generationBatchId == Guid.Empty)
            throw new ArgumentException("GenerationBatchId must not be empty.", nameof(generationBatchId));
        GenerationBatchId = generationBatchId;
        ItemType = itemType;
        TargetEntityId = targetEntityId;
        Status = GenerationJobItemStatus.Queued;
    }

    public void MarkRunning()
    {
        Status = GenerationJobItemStatus.Running;
        AttemptCount++;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void SetTarget(Guid targetEntityId) => TargetEntityId = targetEntityId;

    public void MarkCompleted()
    {
        Status = GenerationJobItemStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkSkipped()
    {
        Status = GenerationJobItemStatus.Skipped;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error, DateTime? nextRetryAtUtc = null)
    {
        Status = GenerationJobItemStatus.Failed;
        LastError = error;
        NextRetryAtUtc = nextRetryAtUtc;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
