namespace LinguaCoach.Application.Speaking;

public sealed class SpeakingEvaluationOptions
{
    public const string SectionName = "SpeakingEvaluation";

    /// <summary>When false, all pending evaluations are immediately resolved as NotSupported.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Provider name to use when Enabled=true. Must match a registered ISpeakingEvaluationProvider.</summary>
    public string Provider { get; init; } = "NoOp";

    /// <summary>Max evaluations processed per job execution.</summary>
    public int MaxBatchSize { get; init; } = 10;

    /// <summary>Max retry attempts before a Failed evaluation is not retried.</summary>
    public int MaxRetries { get; init; } = 3;
}
