using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Profile;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Profile;

public sealed class ProfileCommandHandler : IUpdateLearningPreferencesCommandHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly ILogger<ProfileCommandHandler> _logger;

    public ProfileCommandHandler(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        ILogger<ProfileCommandHandler> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _logger = logger;
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

        // Phase 12D — regenerate plan when learner preferences change
        try
        {
            await _learningPlan.RegeneratePlanAsync(profile.Id, "preference_change", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Plan regeneration failed after preference update for student {StudentProfileId}.", profile.Id);
        }
    }
}
