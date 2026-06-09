using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Admin;

public sealed record CreateStudentCommand(
    string Email,
    string TemporaryPassword,
    bool MustChangePassword = true,
    string? FirstName = null,
    string? LastName = null,
    string? DisplayName = null,
    string? CareerContext = null,
    string? LearningGoal = null,
    int? PreferredSessionDurationMinutes = null,
    ProfessionalExperienceLevel? ProfessionalExperienceLevel = null,
    RoleFamiliarity? RoleFamiliarity = null);

public sealed record CreateStudentResult(Guid StudentProfileId, Guid UserId);

public interface ICreateStudentHandler
{
    Task<CreateStudentResult> HandleAsync(CreateStudentCommand command, CancellationToken ct = default);
}
