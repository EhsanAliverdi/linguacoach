namespace LinguaCoach.Application.History;

// ── Module activity history ───────────────────────────────────────────────────

public sealed record GetModuleActivitiesQuery(Guid UserId, Guid ModuleId);

public interface IGetModuleActivitiesHandler
{
    Task<ModuleActivityHistoryDto> HandleAsync(GetModuleActivitiesQuery query, CancellationToken ct = default);
}

// ── Activity attempt history ──────────────────────────────────────────────────

public sealed record GetActivityAttemptsQuery(Guid UserId, Guid ActivityId);

public interface IGetActivityAttemptsHandler
{
    Task<ActivityAttemptHistoryDto> HandleAsync(GetActivityAttemptsQuery query, CancellationToken ct = default);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ModuleActivityHistoryDto(
    Guid ModuleId,
    string Title,
    string Description,
    int CompletedActivities,
    int TotalRequired,
    double? AverageScore,
    double? LatestScore,
    bool IsReadyToComplete,
    bool IsCompleted,
    IReadOnlyList<ActivitySummaryDto> Activities);

public sealed record ActivitySummaryDto(
    Guid ActivityId,
    string Title,
    string ActivityType,
    int AttemptCount,
    double? BestScore,
    double? LatestScore,
    DateTime? LatestAttemptAt,
    bool HasFeedback);

public sealed record ActivityAttemptHistoryDto(
    Guid ActivityId,
    string Title,
    string ActivityType,
    string? Situation,
    string? LearningGoal,
    IReadOnlyList<string> TargetPhrases,
    IReadOnlyList<AttemptDetailDto> Attempts);

public sealed record AttemptDetailDto(
    Guid AttemptId,
    int AttemptNumber,
    DateTime SubmittedAt,
    double? Score,
    string? CoachSummary,
    bool FocusFirst,
    IReadOnlyList<AttemptChangeDto> Changes,
    IReadOnlyList<string> WhatYouDidWell,
    IReadOnlyList<string> GrammarIssues,
    IReadOnlyList<string> VocabularyIssues,
    IReadOnlyList<string> ToneIssues,
    IReadOnlyList<string> ClarityIssues,
    string? MiniLesson,
    string? NextImprovementStep,
    string? SuggestedImprovedVersion,
    string? NativeLanguageExplanation,
    string? SubmittedContent,
    IReadOnlyList<ListeningAttemptQuestionDto>? ListeningQuestionFeedback = null,
    string? Transcript = null,
    string? ResponseFeedback = null);

public sealed record ListeningAttemptQuestionDto(
    string QuestionId,
    string Question,
    string StudentAnswer,
    string ExpectedAnswerSummary,
    bool IsCorrect,
    double Score,
    string Feedback);

public sealed record AttemptChangeDto(
    string Type,
    string? Original,
    string? Suggested,
    string? Reason,
    string? Category,
    string? Severity);
