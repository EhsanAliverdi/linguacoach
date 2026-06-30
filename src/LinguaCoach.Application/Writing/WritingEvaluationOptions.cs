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

    /// <summary>Phase 17A: objective completion from writing AI is always disabled.</summary>
    public bool AllowObjectiveCompletion => false;

    /// <summary>Phase 17A: CEFR updates from writing AI are always disabled.</summary>
    public bool AllowCefrUpdate => false;

    /// <summary>Phase 17A: mastery signals from writing AI are always disabled (17B+ only).</summary>
    public bool AllowMasterySignals => false;
}
