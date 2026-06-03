namespace LinguaCoach.Application.Writing;

// ── Scenario list (no AI call) ────────────────────────────────────────────────

public sealed record GetWritingScenariosQuery(Guid UserId);

public sealed record WritingScenarioDto(
    Guid Id,
    string Title,
    string Situation,
    string LearningGoal,
    string Difficulty,
    string[] TargetPhrases,
    string[] TargetVocabulary);

public interface IGetWritingScenariosHandler
{
    Task<IReadOnlyList<WritingScenarioDto>> HandleAsync(GetWritingScenariosQuery query, CancellationToken ct = default);
}

// ── Get exercise (scenario description + learning section, no AI call) ────────

public sealed record GetWritingExerciseQuery(Guid UserId, Guid ScenarioId);

public sealed record WritingExerciseDto(
    string ScenarioTitle,
    string ScenarioDescription,
    string LearningGoal,
    string InstructionInSourceLanguage,
    string[] TargetPhrases,
    string[] TargetVocabulary,
    string ExampleText,
    string CommonMistakeToAvoid);

public interface IGetWritingExerciseHandler
{
    Task<WritingExerciseDto> HandleAsync(GetWritingExerciseQuery query, CancellationToken ct = default);
}

// ── Submit draft (triggers AI call) ──────────────────────────────────────────

public sealed record SubmitWritingDraftCommand(Guid UserId, string DraftText, Guid? ScenarioId = null);

public sealed record WritingFeedbackDto(
    Guid SubmissionId,
    double? OverallScore,
    string CorrectedEmail,
    string FeedbackInSourceLanguage,
    IReadOnlyList<string> GrammarIssues,
    IReadOnlyList<string> VocabularyIssues,
    IReadOnlyList<string> ToneIssues,
    IReadOnlyList<string> SuggestedPhrases,
    IReadOnlyList<string> MistakesToTrack,
    // Teaching fields (new in v2)
    IReadOnlyList<string> WhatYouDidWell,
    IReadOnlyList<string> MainMistakes,
    string GrammarExplanation,
    string ToneExplanation,
    IReadOnlyList<string> VocabularyToRemember,
    string RewriteChallenge,
    string NextPracticeSuggestion);

public interface ISubmitWritingDraftHandler
{
    Task<WritingFeedbackDto> HandleAsync(SubmitWritingDraftCommand command, CancellationToken ct = default);
}
