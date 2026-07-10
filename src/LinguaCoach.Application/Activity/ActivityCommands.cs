using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

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
    string? InstructionInSourceLanguage,
    // VocabularyPractice fields — null for other activity types
    string? Instructions = null,
    string? PracticeMode = null,
    IReadOnlyList<VocabPracticeItemDto>? VocabItems = null,
    // ListeningComprehension fields — transcript and expected answers are not exposed before submit
    string? Scenario = null,
    string? SpeakerRole = null,
    string? ListenerRole = null,
    bool? TranscriptAvailableAfterSubmit = null,
    IReadOnlyList<ListeningQuestionDto>? ListeningQuestions = null,
    ListeningResponseTaskDto? ResponseTask = null,
    bool? AudioAvailable = null,
    string? AudioUrl = null,
    string? AudioContentType = null,
    double? AudioDurationSeconds = null,
    string? AudioUnavailableMessage = null,
    string? AudioStatus = null,
    // SpeakingRolePlay fields — null for other activity types
    string? SpeakingScenario = null,
    string? StudentRole = null,
    string? SpeakingListenerRole = null,
    string? SpeakingGoal = null,
    string? SpeakingPrompt = null,
    IReadOnlyList<string>? ExpectedPoints = null,
    IReadOnlyList<string>? SuggestedPhrases = null,
    int? MaxDurationSeconds = null,
    // Exercise Pattern Engine fields (Phase 2)
    InteractionMode? InteractionMode = null,
    string? ExercisePatternKey = null,
    // Raw content JSON for pattern-specific renderers (Phase 3)
    string? ContentJson = null,
    // Staged learning content (module_stage_v1) — populated for activity types
    // that have been migrated; null for types not yet migrated.
    StageContentDto? StageContent = null,
    // Form.io Practice Gym pilot — student-safe schema only, never scoring rules.
    // When present, the client renders via the Form.io renderer instead of ContentJson/StageContent.
    string? FormIoSchemaJson = null);

/// <summary>A single fill-blank item for a VocabularyPractice activity.</summary>
public sealed record VocabPracticeItemDto(
    Guid VocabularyItemId,
    string Term,
    string Prompt,
    string Hint,
    string Explanation);

// ── Vocabulary practice submission answers ────────────────────────────────────

public sealed record VocabAnswerDto(Guid VocabularyItemId, string Answer);

public sealed record ListeningAnswerDto(string QuestionId, string Answer);

public sealed record ListeningQuestionDto(
    string Id,
    string Question,
    string Type);

public sealed record ListeningResponseTaskDto(
    string Prompt,
    string? ExpectedFocus = null);


// ── Get activity by id ─────────────────────────────────────────────────────────

public sealed record GetActivityByIdQuery(Guid UserId, Guid ActivityId);

public interface IGetActivityByIdHandler
{
    Task<ActivityDto> HandleAsync(GetActivityByIdQuery query, CancellationToken ct = default);
}

// ── Submit activity attempt ────────────────────────────────────────────────────

public sealed record SubmitActivityAttemptCommand(
    Guid UserId,
    Guid ActivityId,
    string SubmittedContent,
    string? AudioUrl = null,
    IReadOnlyList<VocabAnswerDto>? VocabAnswers = null,
    IReadOnlyList<ListeningAnswerDto>? ListeningAnswers = null,
    string? ResponseText = null);

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
    string? FeedbackInSourceLanguage,
    // ListeningComprehension feedback fields
    IReadOnlyList<ListeningQuestionFeedbackDto>? QuestionFeedback = null,
    string? Transcript = null,
    string? ResponseFeedback = null,
    // SpeakingRolePlay feedback fields
    IReadOnlyList<string>? SpeakingStrengths = null,
    IReadOnlyList<string>? SpeakingImprovements = null,
    IReadOnlyList<string>? MissingExpectedPoints = null,
    string? SuggestedImprovedResponse = null,
    PatternEvaluationDto? PatternEvaluation = null,
    // Phase B2 — effective feedback policy for the surface this attempt came from (Today vs
    // Practice Gym). Null when not resolved (best-effort; never blocks submission).
    ActivityFeedbackPolicyDto? FeedbackPolicy = null);

public sealed record PatternEvaluationDto(
    string? ExercisePatternKey,
    MarkingMode? MarkingMode,
    double Score,
    double MaxScore,
    double Percentage,
    bool Passed,
    bool Completed,
    IReadOnlyList<PatternEvaluationItemResultDto> ItemResults,
    string? CoachSummary,
    IReadOnlyList<PatternEvaluationCorrectionDto> Corrections,
    string? SuggestedImprovedAnswer,
    IReadOnlyList<PatternEvaluationSkillImpactDto> SkillImpacts,
    IReadOnlyList<PatternEvaluationMemorySignalDto> MemorySignals);

public sealed record PatternEvaluationItemResultDto(
    string ItemKey,
    string? StudentAnswer,
    string? CorrectAnswer,
    IReadOnlyList<string> AcceptedAnswers,
    bool IsCorrect,
    double Score,
    double MaxScore,
    string? Feedback);

public sealed record PatternEvaluationCorrectionDto(
    string Category,
    string? Original,
    string Suggestion,
    string Explanation);

public sealed record PatternEvaluationSkillImpactDto(
    string SkillKey,
    string Label,
    double Delta,
    string? Evidence);

public sealed record PatternEvaluationMemorySignalDto(
    string Type,
    string Key,
    string Summary,
    double Confidence);

public sealed record ListeningQuestionFeedbackDto(
    string QuestionId,
    string Question,
    string StudentAnswer,
    string ExpectedAnswerSummary,
    bool IsCorrect,
    double Score,
    string Feedback);

public interface ISubmitActivityAttemptHandler
{
    Task<ActivityFeedbackDto> HandleAsync(SubmitActivityAttemptCommand command, CancellationToken ct = default);
}
