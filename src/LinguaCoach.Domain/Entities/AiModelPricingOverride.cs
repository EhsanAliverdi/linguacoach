using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Admin-managed override for AI model pricing. Resolved before appsettings config at cost-calculation time.
/// </summary>
public sealed class AiModelPricingOverride : BaseEntity
{
    public string ProviderName { get; private set; }
    public string ModelName { get; private set; }
    public decimal InputPricePer1KTokens { get; private set; }
    public decimal OutputPricePer1KTokens { get; private set; }
    public string Currency { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? CreatedByAdminUserId { get; private set; }
    public Guid? UpdatedByAdminUserId { get; private set; }

    private AiModelPricingOverride()
    {
        ProviderName = string.Empty;
        ModelName = string.Empty;
        Currency = string.Empty;
    }

    public AiModelPricingOverride(
        string providerName,
        string modelName,
        decimal inputPricePer1KTokens,
        decimal outputPricePer1KTokens,
        string currency,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes,
        Guid? createdByAdminUserId)
    {
        Validate(providerName, modelName, inputPricePer1KTokens, outputPricePer1KTokens, currency, effectiveFromUtc, effectiveToUtc);

        ProviderName = providerName.Trim().ToLowerInvariant();
        ModelName = modelName.Trim();
        InputPricePer1KTokens = inputPricePer1KTokens;
        OutputPricePer1KTokens = outputPricePer1KTokens;
        Currency = currency.Trim().ToUpperInvariant();
        IsActive = true;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByAdminUserId = createdByAdminUserId;
    }

    public void Update(
        decimal inputPricePer1KTokens,
        decimal outputPricePer1KTokens,
        string currency,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes,
        Guid? updatedByAdminUserId)
    {
        Validate(ProviderName, ModelName, inputPricePer1KTokens, outputPricePer1KTokens, currency, effectiveFromUtc, effectiveToUtc);

        InputPricePer1KTokens = inputPricePer1KTokens;
        OutputPricePer1KTokens = outputPricePer1KTokens;
        Currency = currency.Trim().ToUpperInvariant();
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    public void Deactivate(Guid? updatedByAdminUserId)
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByAdminUserId = updatedByAdminUserId;
    }

    private static void Validate(
        string providerName, string modelName,
        decimal inputPrice, decimal outputPrice,
        string currency,
        DateTime effectiveFrom, DateTime? effectiveTo)
    {
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("ProviderName is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("ModelName is required.", nameof(modelName));
        if (inputPrice < 0) throw new ArgumentOutOfRangeException(nameof(inputPrice), "Input price must be >= 0.");
        if (outputPrice < 0) throw new ArgumentOutOfRangeException(nameof(outputPrice), "Output price must be >= 0.");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.", nameof(currency));
        if (effectiveTo.HasValue && effectiveTo.Value <= effectiveFrom)
            throw new ArgumentException("EffectiveToUtc must be after EffectiveFromUtc.", nameof(effectiveTo));
    }
}
