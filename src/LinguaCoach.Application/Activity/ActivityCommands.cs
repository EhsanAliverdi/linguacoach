using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

// ── Get next activity ──────────────────────────────────────────────────────────

public sealed record GetNextActivityQuery(Guid UserId, ActivityType? PreferredType = null);

public sealed record ActivityDto(
    Guid ActivityId,
    ActivityType ActivityType,
    ActivitySource Source,
    string Title,
    string Difficulty,
    // WritingScenario fields — null for other activity types
    string? Situation,
    string? LearningGoal,
    string[] TargetPhrases,
    string[] TargetVocabulary,
    string? ExampleText,
    string? CommonMistakeToAvoid,
    string? InstructionInSourceLanguage);

public interface IGetNextActivityHandler
{
    Task<ActivityDto> HandleAsync(GetNextActivityQuery query, CancellationToken ct = default);
}

// ── Submit activity attempt ────────────────────────────────────────────────────

public sealed record SubmitActivityAttemptCommand(
    Guid UserId,
    Guid ActivityId,
    string SubmittedContent,
    string? AudioUrl = null);

/// <summary>A single targeted change suggestion from the AI coach.</summary>
public sealed record FeedbackChangeDto(
    string Type,       // replace | add | remove | reorder
    string? Original,
    string? Suggested,
    string? Reason,
    string? Category,  // grammar | vocabulary | tone | clarity | structure | punctuation
    string? Severity); // high | medium | low

public sealed record ActivityFeedbackDto(
    Guid AttemptId,
    double? Score,
    // Coach summary (new)
    string? CoachSummary,
    // Focus mode: true when there are many issues and the list is limited to top 3-5
    bool FocusFirst,
    // Targeted change list (new — the primary coaching output)
    IReadOnlyList<FeedbackChangeDto> Changes,
    // Writing-specific feedback fields
    string? CorrectedText,           // mapped from improvedVersion (backward compat alias)
    IReadOnlyList<string> WhatYouDidWell,
    IReadOnlyList<string> MainMistakes,
    IReadOnlyList<string> GrammarIssues,
    IReadOnlyList<string> VocabularyIssues,
    IReadOnlyList<string> ToneIssues,
    IReadOnlyList<string> ClarityIssues,
    string? GrammarExplanation,
    string? ToneExplanation,
    IReadOnlyList<string> VocabularyToRemember,
    string? MiniLesson,              // new: concise teaching moment
    string? NextImprovementStep,     // new: actionable rewrite instruction
    string? RewriteChallenge,
    string? NextPracticeSuggestion,
    // Generic feedback in source language
    string? FeedbackInSourceLanguage);

public interface ISubmitActivityAttemptHandler
{
    Task<ActivityFeedbackDto> HandleAsync(SubmitActivityAttemptCommand command, CancellationToken ct = default);
}
