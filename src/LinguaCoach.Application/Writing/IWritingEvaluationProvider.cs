namespace LinguaCoach.Application.Writing;

/// <summary>
/// Narrow interface for written-text evaluation providers.
/// Separate from IAiProvider — writing evaluation uses a rubric-scoring contract with nullable scores.
/// </summary>
public interface IWritingEvaluationProvider
{
    string ProviderName { get; }

    /// <summary>False when provider is disabled or not configured. Job uses NoOp fallback.</summary>
    bool IsSupported { get; }

    /// <summary>Describes provider capabilities for admin status reporting and safe result parsing.</summary>
    WritingEvaluationProviderCapabilities Capabilities { get; }

    Task<WritingEvaluationProviderResult> EvaluateAsync(
        WritingEvaluationRequest request,
        CancellationToken ct = default);
}

public sealed record WritingEvaluationRequest(
    Guid AttemptId,
    Guid StudentProfileId,
    Guid ActivityId,
    string? WrittenText,
    string? ActivityPrompt,
    string? ActivityTitle,
    string? CefrLevel,
    string? PatternKey,
    string CorrelationId);

public sealed record WritingEvaluationProviderResult(
    bool Success,
    double? OverallScore,
    double? GrammarScore,
    double? VocabularyScore,
    double? CoherenceScore,
    double? TaskCompletionScore,
    string? FeedbackText,
    string? SuggestedImprovement,
    string? CorrectedText,
    string? FailureReason,
    string? ModelName,
    int InputTokens = 0,
    int OutputTokens = 0,
    decimal CostUsd = 0m);
