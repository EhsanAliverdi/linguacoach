namespace LinguaCoach.Application.Admin;

public sealed record AdminStudentProgressQuery(Guid StudentProfileId);

public sealed record AdminStudentProgressResult(
    string? CurrentCefrLevel,
    string? PlacementCefrLevel,
    DateTime? PlacementCompletedAt,
    int MasteredObjectivesCount,
    int InProgressObjectivesCount,
    int ReviewQueueCount,
    int TotalObjectives,
    double CompletionPercentage,
    string? StrongestSkill,
    string? WeakestSkill,
    int WeakSkillsCount,
    DateTime? LastLearningActivityAt,
    string CurrentLearningPhase);

public interface IAdminStudentProgressQuery
{
    Task<AdminStudentProgressResult?> HandleAsync(
        AdminStudentProgressQuery query, CancellationToken ct = default);
}
