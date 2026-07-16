using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4.4D — the AI-candidate-enrichment analog of <see cref="ImportSttOperation"/>: one
/// durable, retry-safe billable AI operation record. Exactly one row exists per
/// <see cref="LogicalOperationKey"/> (unique database index — see
/// <c>ImportAiEnrichmentOperationConfiguration</c>), mutated in place across retries rather than
/// accumulating a new row per attempt. A successful result's <see cref="ResultReferenceJson"/> (the
/// bounded, already-parsed analysis output — never the raw AI response body) is reused on retry:
/// the provider is never called again and the package's accrued cost is never incremented twice
/// for the same logical operation.
/// </summary>
public sealed class ImportAiEnrichmentOperation : BaseEntity
{
    public Guid ImportPackageId { get; private set; }
    public Guid ImportProfileId { get; private set; }
    public Guid ResourceCandidateId { get; private set; }

    /// <summary>Stable identity derived from package, candidate, candidate content checksum,
    /// provider/model, prompt version, and processing mode — see
    /// <c>ImportAiEnrichmentOperationKey.Compute</c>. Any material change to prompt, model, or
    /// profile produces a different key, so a changed configuration always creates a new
    /// operation rather than silently reusing a stale result.</summary>
    public string LogicalOperationKey { get; private set; } = string.Empty;

    public string OperationType { get; private set; } = string.Empty;
    public ImportAiOperationStatus Status { get; private set; }
    public int AttemptNumber { get; private set; }

    public string ProviderName { get; private set; } = string.Empty;
    public string? ModelName { get; private set; }
    public string PromptVersion { get; private set; } = string.Empty;
    public string ProcessingMode { get; private set; } = string.Empty;

    /// <summary>Set only once, on success — the bounded, already-parsed analysis output (never
    /// the raw AI response body), so a reuse can re-apply the result to the candidate without
    /// calling the provider again.</summary>
    public string? ResultReferenceJson { get; private set; }

    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public decimal? InputPricePer1KTokensSnapshot { get; private set; }
    public decimal? OutputPricePer1KTokensSnapshot { get; private set; }
    public decimal? CalculatedCost { get; private set; }
    public string Currency { get; private set; } = "USD";

    public string? FailureReason { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private ImportAiEnrichmentOperation() { }

    public ImportAiEnrichmentOperation(
        Guid importPackageId, Guid importProfileId, Guid resourceCandidateId, string logicalOperationKey,
        string operationType, string providerName, string promptVersion, string processingMode,
        DateTimeOffset startedAtUtc)
    {
        if (importPackageId == Guid.Empty)
            throw new ArgumentException("ImportPackageId must not be empty.", nameof(importPackageId));
        if (resourceCandidateId == Guid.Empty)
            throw new ArgumentException("ResourceCandidateId must not be empty.", nameof(resourceCandidateId));
        if (string.IsNullOrWhiteSpace(logicalOperationKey))
            throw new ArgumentException("LogicalOperationKey is required.", nameof(logicalOperationKey));
        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("OperationType is required.", nameof(operationType));
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("ProviderName is required.", nameof(providerName));

        ImportPackageId = importPackageId;
        ImportProfileId = importProfileId;
        ResourceCandidateId = resourceCandidateId;
        LogicalOperationKey = logicalOperationKey.Trim();
        OperationType = operationType.Trim();
        ProviderName = providerName.Trim();
        PromptVersion = promptVersion?.Trim() ?? string.Empty;
        ProcessingMode = processingMode?.Trim() ?? string.Empty;
        StartedAtUtc = startedAtUtc;
        Status = ImportAiOperationStatus.Pending;
        AttemptNumber = 1;
    }

    public void MarkSucceeded(
        string resultReferenceJson, decimal calculatedCost, string currency, int inputTokens, int outputTokens,
        decimal inputPricePer1KTokensSnapshot, decimal outputPricePer1KTokensSnapshot, string? modelName,
        DateTimeOffset completedAtUtc)
    {
        if (Status != ImportAiOperationStatus.Pending)
            throw new InvalidOperationException($"Cannot mark an AI operation succeeded from status '{Status}'.");
        if (calculatedCost < 0)
            throw new ArgumentOutOfRangeException(nameof(calculatedCost));
        if (string.IsNullOrWhiteSpace(resultReferenceJson))
            throw new ArgumentException("ResultReferenceJson is required on success.", nameof(resultReferenceJson));

        Status = ImportAiOperationStatus.Succeeded;
        ResultReferenceJson = resultReferenceJson;
        CalculatedCost = calculatedCost;
        Currency = currency;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        InputPricePer1KTokensSnapshot = inputPricePer1KTokensSnapshot;
        OutputPricePer1KTokensSnapshot = outputPricePer1KTokensSnapshot;
        ModelName = modelName;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string reason, DateTimeOffset completedAtUtc)
    {
        if (Status != ImportAiOperationStatus.Pending)
            throw new InvalidOperationException($"Cannot mark an AI operation failed from status '{Status}'.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A failure reason is required.", nameof(reason));

        Status = ImportAiOperationStatus.Failed;
        FailureReason = reason.Trim();
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>A prior attempt failed — allow another attempt under the same logical key. Never
    /// callable from Succeeded — a completed, billed result is never re-attempted.</summary>
    public void BeginRetry(DateTimeOffset startedAtUtc)
    {
        if (Status != ImportAiOperationStatus.Failed)
            throw new InvalidOperationException($"Cannot retry an AI operation from status '{Status}' — only a Failed operation may retry.");

        Status = ImportAiOperationStatus.Pending;
        AttemptNumber++;
        FailureReason = null;
        CompletedAtUtc = null;
        StartedAtUtc = startedAtUtc;
    }
}
