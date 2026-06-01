namespace LinguaCoach.Application.Writing;

// ── Get exercise (scenario description, no AI call) ───────────────────────────

public sealed record GetWritingExerciseQuery(Guid UserId);

public sealed record WritingExerciseDto(
    string ScenarioTitle,
    string ScenarioDescription,
    string InstructionInSourceLanguage,
    string[] TargetPhrases,
    string[] TargetVocabulary);

public interface IGetWritingExerciseHandler
{
    Task<WritingExerciseDto> HandleAsync(GetWritingExerciseQuery query, CancellationToken ct = default);
}

// ── Submit draft (triggers AI call) ──────────────────────────────────────────

public sealed record SubmitWritingDraftCommand(Guid UserId, string DraftText);

public sealed record WritingFeedbackDto(
    Guid SubmissionId,
    double? OverallScore,
    string CorrectedEmail,
    string FeedbackInSourceLanguage,
    IReadOnlyList<string> GrammarIssues,
    IReadOnlyList<string> VocabularyIssues,
    IReadOnlyList<string> ToneIssues,
    IReadOnlyList<string> SuggestedPhrases,
    IReadOnlyList<string> MistakesToTrack);

public interface ISubmitWritingDraftHandler
{
    Task<WritingFeedbackDto> HandleAsync(SubmitWritingDraftCommand command, CancellationToken ct = default);
}
