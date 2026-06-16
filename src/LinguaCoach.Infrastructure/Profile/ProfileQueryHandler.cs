using LinguaCoach.Application.Profile;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Profile;

public sealed class ProfileQueryHandler : IGetStudentProfileQueryHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileQueryHandler(LinguaCoachDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<StudentProfileResult?> HandleAsync(GetStudentProfileQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct);

        if (profile is null) return null;

        var user = await _userManager.FindByIdAsync(query.UserId.ToString());

        return new StudentProfileResult(
            ProfileId: profile.Id,
            UserId: profile.UserId,
            FirstName: profile.FirstName,
            LastName: profile.LastName,
            DisplayName: profile.DisplayName,
            PreferredName: profile.PreferredName,
            Email: user?.Email,
            CefrLevel: profile.CefrLevel,
            LearningGoals: profile.LearningGoals,
            CustomLearningGoal: profile.CustomLearningGoal,
            FocusAreas: profile.FocusAreas,
            CustomFocusArea: profile.CustomFocusArea,
            SupportLanguageCode: profile.SupportLanguageCode,
            SupportLanguageName: profile.SupportLanguageName,
            TranslationHelpPreference: profile.TranslationHelpPreference,
            PreferredSessionDurationMinutes: profile.PreferredSessionDurationMinutes,
            DifficultyPreference: profile.DifficultyPreference,
            LearningPreferencesUpdatedAt: profile.LearningPreferencesUpdatedAt);
    }
}
