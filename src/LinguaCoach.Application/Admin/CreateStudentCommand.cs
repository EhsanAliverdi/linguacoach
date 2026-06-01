namespace LinguaCoach.Application.Admin;

public sealed record CreateStudentCommand(string Email, string TemporaryPassword);

public sealed record CreateStudentResult(Guid StudentProfileId, Guid UserId);

public interface ICreateStudentHandler
{
    Task<CreateStudentResult> HandleAsync(CreateStudentCommand command, CancellationToken ct = default);
}
