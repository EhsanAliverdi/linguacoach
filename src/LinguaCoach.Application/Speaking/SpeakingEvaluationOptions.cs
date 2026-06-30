namespace LinguaCoach.Application.Speaking;

public sealed class SpeakingEvaluationOptions
{
    public const string SectionName = "SpeakingEvaluation";

    /// <summary>When false, all pending evaluations are immediately resolved as NotSupported.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Provider name: "NoOp" (default) or "OpenAI". Must match a registered ISpeakingEvaluationProvider.</summary>
    public string Provider { get; init; } = "NoOp";

    /// <summary>Max evaluations processed per job execution.</summary>
    public int MaxBatchSize { get; init; } = 10;

    /// <summary>Max retry attempts before a Failed evaluation is not retried.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>LLM model used for evaluation (text rubric scoring). Default: gpt-4o-mini.</summary>
    public string Model { get; init; } = "gpt-4o-mini";

    /// <summary>Transcription model used for audio-to-text. Default: whisper-1 (OpenAI Whisper).</summary>
    public string TranscriptionModel { get; init; } = "whisper-1";

    /// <summary>Per-evaluation timeout in seconds. Applies to both transcription and evaluation calls combined.</summary>
    public int TimeoutSeconds { get; init; } = 90;

    /// <summary>Maximum audio file size in bytes accepted for evaluation. Default: 25 MB (OpenAI Whisper limit).</summary>
    public long MaxAudioSizeBytes { get; init; } = 25 * 1024 * 1024;

    /// <summary>Maximum audio duration in seconds accepted for evaluation. Informational limit; enforced by provider.</summary>
    public int MaxAudioDurationSeconds { get; init; } = 120;

    // --- Mastery integration (Phase 16I) ---

    /// <summary>
    /// When true, completed evaluation signals meeting confidence and type gates are applied to
    /// StudentLearningEvent and StudentSkillProfile. Default false — safe.
    /// </summary>
    public bool ApplyMasterySignals { get; init; } = false;

    /// <summary>
    /// Minimum confidence band required before any signal is applied.
    /// "Low" | "Medium" | "High". Default "High" — conservative.
    /// </summary>
    public string MinimumConfidenceForMasterySignal { get; init; } = "High";

    /// <summary>
    /// When true and ApplyMasterySignals is true, CandidateReviewSignal evaluations produce
    /// a weakness/review learning event and a StudentSkillProfile MarkWeak update.
    /// Default true (takes effect only when ApplyMasterySignals is also true).
    /// </summary>
    public bool AllowReviewSignals { get; init; } = true;

    /// <summary>
    /// When true and ApplyMasterySignals is true, CandidatePositiveSignal evaluations produce
    /// a positive learning event. Default false — more restrictive gate.
    /// </summary>
    public bool AllowPositiveSignals { get; init; } = false;

    // --- Signal score thresholds (Phase 16J) ---

    /// <summary>Minimum overall score (0–100) for CandidatePositiveSignal. Default 80 — conservative.</summary>
    public double MinimumOverallScoreForPositiveSignal { get; init; } = 80.0;

    /// <summary>Minimum relevance score for positive signal. Null relevance is treated as passing. Default 80.</summary>
    public double MinimumRelevanceScoreForPositiveSignal { get; init; } = 80.0;

    /// <summary>Minimum completeness score for positive signal. Null completeness is treated as passing. Default 80.</summary>
    public double MinimumCompletenessScoreForPositiveSignal { get; init; } = 80.0;

    /// <summary>
    /// Maximum overall score at or below which a CandidateReviewSignal is produced.
    /// Scores above this and below MinimumOverallScoreForPositiveSignal → CandidateNoSignal.
    /// Default 55.
    /// </summary>
    public double MaximumOverallScoreForReviewSignal { get; init; } = 55.0;

    /// <summary>Maximum relevance score for review candidate. Null passes. Default 55.</summary>
    public double MaximumRelevanceScoreForReviewSignal { get; init; } = 55.0;

    /// <summary>Maximum completeness score for review candidate. Null passes. Default 55.</summary>
    public double MaximumCompletenessScoreForReviewSignal { get; init; } = 55.0;

    /// <summary>Phase 16I: objective completion from speaking AI is always disabled.</summary>
    public bool AllowObjectiveCompletion => false;

    /// <summary>Phase 16I: CEFR updates from speaking AI are always disabled.</summary>
    public bool AllowCefrUpdate => false;
}
