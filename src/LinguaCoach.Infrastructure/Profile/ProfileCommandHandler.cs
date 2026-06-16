using LinguaCoach.Application.Profile;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Profile;

public sealed class ProfileCommandHandler : IUpdateLearningPreferencesCommandHandler
{
    private readonly LinguaCoachDbContext _db;

    public ProfileCommandHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(UpdateLearningPreferencesCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        profile.UpdateLearningPreferences(
            preferredName: command.PreferredName,
            supportLanguageCode: command.SupportLanguageCode,
            supportLanguageName: command.SupportLanguageName,
            translationHelpPreference: command.TranslationHelpPreference,
            learningGoals: command.LearningGoals,
            customLearningGoal: command.CustomLearningGoal,
            focusAreas: command.FocusAreas,
            customFocusArea: command.CustomFocusArea,
            difficultyPreference: command.DifficultyPreference,
            preferredSessionDurationMinutes: command.PreferredSessionDurationMinutes);

        await _db.SaveChangesAsync(ct);
    }
}
