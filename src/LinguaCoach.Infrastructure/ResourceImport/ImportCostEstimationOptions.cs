namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Mandatory Import Execution Plan addendum (2026-07-15) — bound from configuration section
/// "ImportCostEstimation". Text-generation AI cost reuses the existing
/// <c>IAiPricingResolver</c>/<c>AiModelPricingOverride</c> pricing system (per the addendum's
/// "do not hardcode provider prices if pricing configuration already exists"); STT/TTS/image have
/// no existing pricing table in this codebase, so their per-unit assumed rates live here instead,
/// each one an admin-configurable estimate rather than a hardcoded constant in business logic.
/// </summary>
public sealed class ImportCostEstimationOptions
{
    public const string SectionName = "ImportCostEstimation";

    /// <summary>Provider/model assumed for the AI structuring/enrichment cost estimate — the
    /// actual provider used at execution time may differ (resolved per feature key), but the
    /// plan must show a concrete assumption, never a vague range with no stated basis.</summary>
    public string AssumedAiProviderName { get; set; } = "openai";
    public string AssumedAiModelName { get; set; } = "gpt-4o-mini";

    public decimal SttCostPerMinute { get; set; } = 0.006m; // Whisper-class pricing assumption
    public decimal TtsCostPerThousandCharacters { get; set; } = 0.015m;
    public decimal ImageAnalysisCostPerImage { get; set; } = 0.005m;

    /// <summary>Rough input-token assumption per AI-structured candidate, used only when no
    /// sample-derived token count is available yet.</summary>
    public int AssumedInputTokensPerCandidate { get; set; } = 600;
    public int AssumedOutputTokensPerCandidate { get; set; } = 300;

    /// <summary>Uncertainty band applied around the "expected" figure to produce min/max —
    /// explicit and configurable rather than a hidden constant, per "do not claim false
    /// precision."</summary>
    public double CostRangeUncertaintyFraction { get; set; } = 0.20;

    /// <summary>Assumed average processing throughput used for the (labeled, non-precise) time
    /// estimate — candidates per minute of background-job wall time.</summary>
    public double AssumedCandidatesPerMinute { get; set; } = 20.0;

    /// <summary>Small tolerance so an estimation-noise-level cost overage doesn't pause a job for
    /// a rounding error — must be explicit and tested, per Part 6.</summary>
    public double CostCeilingToleranceFraction { get; set; } = 0.05;

    public int MaxSamplingRounds { get; set; } = 2;
    public int MaxSampleBytesPerRound { get; set; } = 200_000;
}
