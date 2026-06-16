using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Profile;

// ── GET student profile ───────────────────────────────────────────────────────

public sealed record GetStudentProfileQuery(Guid UserId);

public sealed record StudentProfileResult(
    Guid ProfileId,
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? PreferredName,
    string? Email,
    // Level (read-only — set by assessment only)
    string? CefrLevel,
    // Learning goals
    List<string> LearningGoals,
    string? CustomLearningGoal,
    // Focus areas
    List<string> FocusAreas,
    string? CustomFocusArea,
    // Support language
    string? SupportLanguageCode,
    string? SupportLanguageName,
    TranslationHelpPreference? TranslationHelpPreference,
    // Practice preferences
    int? PreferredSessionDurationMinutes,
    DifficultyPreference? DifficultyPreference,
    // Timestamps
    DateTimeOffset? LearningPreferencesUpdatedAt);

public interface IGetStudentProfileQueryHandler
{
    Task<StudentProfileResult?> HandleAsync(GetStudentProfileQuery query, CancellationToken ct = default);
}
