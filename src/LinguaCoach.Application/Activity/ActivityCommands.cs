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

public sealed record ActivityFeedbackDto(
    Guid AttemptId,
    double? Score,
    // Writing-specific feedback fields
    string? CorrectedText,
    IReadOnlyList<string> WhatYouDidWell,
    IReadOnlyList<string> MainMistakes,
    string? GrammarExplanation,
    string? ToneExplanation,
    IReadOnlyList<string> VocabularyToRemember,
    string? RewriteChallenge,
    string? NextPracticeSuggestion,
    // Generic feedback in source language
    string? FeedbackInSourceLanguage);

public interface ISubmitActivityAttemptHandler
{
    Task<ActivityFeedbackDto> HandleAsync(SubmitActivityAttemptCommand command, CancellationToken ct = default);
}
