using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Profile;

// ── PUT student learning preferences ─────────────────────────────────────────

/// <summary>
/// Updates only student-editable preference fields.
/// Never updates CEFR, prompts, admin profile fields, or onboarding state.
/// </summary>
public sealed record UpdateLearningPreferencesCommand(
    Guid UserId,
    string? PreferredName,
    string? SupportLanguageCode,
    string? SupportLanguageName,
    TranslationHelpPreference? TranslationHelpPreference,
    List<string>? LearningGoals,
    string? CustomLearningGoal,
    List<string>? FocusAreas,
    string? CustomFocusArea,
    DifficultyPreference? DifficultyPreference,
    int? PreferredSessionDurationMinutes);

public interface IUpdateLearningPreferencesCommandHandler
{
    Task HandleAsync(UpdateLearningPreferencesCommand command, CancellationToken ct = default);
}
