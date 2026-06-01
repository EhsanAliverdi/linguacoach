using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class CreateStudentHandler : ICreateStudentHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LinguaCoachDbContext _db;

    public CreateStudentHandler(UserManager<ApplicationUser> userManager, LinguaCoachDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<CreateStudentResult> HandleAsync(CreateStudentCommand command, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
            throw new InvalidOperationException($"A user with email '{command.Email}' already exists.");

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
            Role = UserRole.Student,
            MustChangePassword = true,
            // Admin-created accounts are confirmed immediately.
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, command.TemporaryPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var profile = new StudentProfile(user.Id);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        return new CreateStudentResult(profile.Id, user.Id);
    }
}
