using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace LinguaCoach.Persistence.Identity;

/// <summary>
/// ASP.NET Identity user. Extends IdentityUser with a platform role.
/// Domain entities reference UserId (Guid) — they do not import this class.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public UserRole Role { get; set; }

    // Signals that the user must change their password on next login.
    // Set to true when admin creates a student account.
    public bool MustChangePassword { get; set; }
}
