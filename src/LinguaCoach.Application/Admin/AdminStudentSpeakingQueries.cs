namespace LinguaCoach.Application.Admin;

public sealed record AdminStudentSpeakingAttemptsQuery(Guid StudentProfileId);

public sealed record AdminStudentSpeakingAttemptDto(
    Guid AttemptId,
    Guid ActivityId,
    string? ActivityTitle,
    string? ActivityType,
    DateTime SubmittedAt,
    string? MimeType,
    /// <summary>Submitted | PendingEvaluation | Evaluated | Failed</summary>
    string Status,
    /// <summary>Pending | Evaluating | Completed | Failed | Skipped | NotSupported. Null if no evaluation record.</summary>
    string? EvaluationStatus,
    string? EvaluationProvider,
    string? EvaluationModel,
    DateTime? EvaluationCompletedAt,
    string? EvaluationFeedbackText,
    string? EvaluationSuggestedImprovement,
    string? EvaluationFailureReason,
    double? OverallScore,
    // Dry-run learning signal preview — never applied to mastery, CEFR, or Learning Plan.
    string? DryRunOutcome,
    string? DryRunConfidence,
    string? DryRunCandidateSkill,
    string? DryRunBlockedReason,
    // Phase 16J — applied signal detail
    bool IsApplied,
    string? AppliedSignalType,
    string? AppliedSignalConfidence,
    string? AppliedSignalBlockedReason,
    DateTime? AppliedAt,
    /// <summary>Always false — structural invariant. Displayed explicitly in admin UI.</summary>
    bool SignalUpdatesCefr,
    /// <summary>Always false — structural invariant. Displayed explicitly in admin UI.</summary>
    bool SignalCompletesObjectives);

public sealed record AdminStudentSpeakingAttemptsResult(
    /// <summary>Ready | Empty | NotFound</summary>
    string Status,
    IReadOnlyList<AdminStudentSpeakingAttemptDto> Attempts);

public interface IAdminStudentSpeakingAttemptsQuery
{
    Task<AdminStudentSpeakingAttemptsResult> HandleAsync(
        AdminStudentSpeakingAttemptsQuery query, CancellationToken ct = default);
}
