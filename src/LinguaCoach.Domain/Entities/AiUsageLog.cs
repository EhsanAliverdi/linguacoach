using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records AI provider usage and cost per request.
/// Schema placeholder — fields will evolve when AI provider is confirmed.
/// </summary>
public sealed class AiUsageLog : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public string ProviderName { get; private set; }
    public string ModelName { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal CostUsd { get; private set; }

    private AiUsageLog() { ProviderName = string.Empty; ModelName = string.Empty; }

    public AiUsageLog(
        Guid studentProfileId,
        string providerName,
        string modelName,
        int inputTokens,
        int outputTokens,
        decimal costUsd)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.", nameof(modelName));
        if (inputTokens < 0) throw new ArgumentOutOfRangeException(nameof(inputTokens), "Input token count cannot be negative.");
        if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens), "Output token count cannot be negative.");
        if (costUsd < 0) throw new ArgumentOutOfRangeException(nameof(costUsd), "Cost cannot be negative.");

        StudentProfileId = studentProfileId;
        ProviderName = providerName.Trim();
        ModelName = modelName.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
    }
}
