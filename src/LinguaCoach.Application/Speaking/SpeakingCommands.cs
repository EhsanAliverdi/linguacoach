namespace LinguaCoach.Application.Speaking;

// ── Create session ────────────────────────────────────────────────────────────

public sealed record CreateSpeakingSessionCommand(Guid UserId, Guid ScenarioId);

public sealed record SpeakingSessionDto(
    Guid SessionId,
    string ScenarioTitle,
    string ScenarioGoal,
    int MaxTurns,
    string FirstAiQuestion);

public interface ICreateSpeakingSessionHandler
{
    Task<SpeakingSessionDto> HandleAsync(CreateSpeakingSessionCommand command, CancellationToken ct = default);
}

// ── Submit turn ───────────────────────────────────────────────────────────────

public sealed record SubmitSpeakingTurnCommand(Guid UserId, Guid SessionId, string UserTranscript);

public sealed record SpeakingTurnResultDto(
    int TurnNumber,
    string AiReply,
    string FeedbackInSourceLanguage,
    IReadOnlyList<string> Mistakes,
    double? GrammarScore,
    double? VocabularyScore,
    double? FluencyScore,
    bool SessionComplete,
    double? OverallScore);

public interface ISubmitSpeakingTurnHandler
{
    Task<SpeakingTurnResultDto> HandleAsync(SubmitSpeakingTurnCommand command, CancellationToken ct = default);
}
