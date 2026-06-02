namespace LinguaCoach.Application.Assessment;

public sealed record CefrAssessmentCommand(Guid UserId, string StudentSample);

public sealed record CefrAssessmentResult(
    string Level,
    string Rationale,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> AreasForImprovement);

public interface ICefrAssessmentHandler
{
    Task<CefrAssessmentResult> HandleAsync(CefrAssessmentCommand command, CancellationToken ct = default);
}
