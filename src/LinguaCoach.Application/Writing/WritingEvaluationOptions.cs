namespace LinguaCoach.Application.Writing;

public sealed class WritingEvaluationOptions
{
    public const string SectionName = "WritingEvaluation";

    /// <summary>When false, all pending evaluations are immediately resolved as NotSupported.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Provider name: "NoOp" (default) or "OpenAI". Must match a registered IWritingEvaluationProvider.</summary>
    public string Provider { get; init; } = "NoOp";

    /// <summary>Max evaluations processed per job execution.</summary>
    public int MaxBatchSize { get; init; } = 10;

    /// <summary>Max retry attempts before a Failed evaluation is not retried.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>LLM model used for evaluation (text rubric scoring). Default: gpt-4o-mini.</summary>
    public string Model { get; init; } = "gpt-4o-mini";

    /// <summary>Per-evaluation timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    // Phase 17C — Controlled signal integration (safe defaults: all off).

    /// <summary>When true, the signal application job applies mastery signals from completed evaluations.</summary>
    public bool ApplyMasterySignals { get; init; } = false;

    /// <summary>Minimum confidence band required to apply a mastery signal. Default: High.</summary>
    public string MinimumConfidenceForMasterySignal { get; init; } = "High";

    /// <summary>When true, review/weakness signals can be applied. Requires ApplyMasterySignals=true.</summary>
    public bool AllowReviewSignals { get; init; } = true;

    /// <summary>When true, positive signals can be applied. Requires ApplyMasterySignals=true and High confidence.</summary>
    public bool AllowPositiveSignals { get; init; } = false;

    /// <summary>Phase 17C: objective completion from writing AI is always disabled.</summary>
    public bool AllowObjectiveCompletion => false;

    /// <summary>Phase 17C: CEFR updates from writing AI are always disabled.</summary>
    public bool AllowCefrUpdate => false;
}
