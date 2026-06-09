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
            MustChangePassword = command.MustChangePassword,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, command.TemporaryPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var profile = new StudentProfile(user.Id);

        var hasProfileContext = !string.IsNullOrWhiteSpace(command.FirstName)
            || !string.IsNullOrWhiteSpace(command.LastName)
            || !string.IsNullOrWhiteSpace(command.DisplayName)
            || !string.IsNullOrWhiteSpace(command.CareerContext)
            || !string.IsNullOrWhiteSpace(command.LearningGoal)
            || command.PreferredSessionDurationMinutes.HasValue
            || command.ProfessionalExperienceLevel.HasValue
            || command.RoleFamiliarity.HasValue;

        if (hasProfileContext)
        {
            profile.SetInitialProfile(
                command.FirstName,
                command.LastName,
                command.DisplayName,
                command.CareerContext,
                command.LearningGoal,
                command.PreferredSessionDurationMinutes,
                command.ProfessionalExperienceLevel,
                command.RoleFamiliarity);
        }

        var lifecycle = command.MustChangePassword
            ? StudentLifecycleStage.PasswordChangeRequired
            : hasProfileContext && command.ProfessionalExperienceLevel.HasValue && command.RoleFamiliarity.HasValue
                ? StudentLifecycleStage.PlacementRequired
                : StudentLifecycleStage.OnboardingRequired;

        profile.SetLifecycleStage(lifecycle);

        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        return new CreateStudentResult(profile.Id, user.Id);
    }
}
