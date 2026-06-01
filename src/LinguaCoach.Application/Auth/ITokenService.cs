using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Auth;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email, UserRole role);
}
