using LinguaCoach.Application.Dashboard;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Dashboard;

public sealed class DashboardQueryHandler : IDashboardQueryHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardQueryHandler(LinguaCoachDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<DashboardResult> HandleAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != OnboardingStatus.Complete)
            throw new InvalidOperationException("Dashboard is only available after onboarding is complete.");

        var user = await _userManager.FindByIdAsync(query.UserId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        var careerName = profile.CareerProfile?.Name ?? "your selected role";

        return new DashboardResult(
            StudentName: user.Email!,
            CareerProfileName: careerName,
            Message: "Your personalised plan is being prepared.");
    }
}
