using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Read-only query interface for the curriculum syllabus.
/// Returns candidate objectives only — does NOT select activities, exercise formats,
/// or generate content. Activity selection belongs to 10L routing.
/// </summary>
public interface ICurriculumSyllabusQuery
{
    /// <summary>Returns all active objectives ordered by RecommendedOrder.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetActiveObjectivesAsync(CancellationToken ct = default);

    /// <summary>Returns active objectives for the given CEFR level.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetByCefrAsync(string cefrLevel, CancellationToken ct = default);

    /// <summary>Returns active objectives for the given CEFR level and primary skill.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndSkillAsync(string cefrLevel, string primarySkill, CancellationToken ct = default);

    /// <summary>Returns active objectives whose ContextTagsJson contains the given tag.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndContextAsync(string cefrLevel, string contextTag, CancellationToken ct = default);

    /// <summary>Returns active objectives whose FocusTagsJson contains the given focus area.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndFocusAreaAsync(string cefrLevel, string focusArea, CancellationToken ct = default);

    /// <summary>Returns objectives whose PrerequisiteKeysJson references the given key.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetPrerequisitesAsync(string objectiveKey, CancellationToken ct = default);

    /// <summary>
    /// Returns candidate active objectives for the given student context.
    /// Filters by CEFR level and any matching context tags from the provided tag list.
    /// Returns ordered by RecommendedOrder ascending.
    ///
    /// IMPORTANT: This method returns candidates only. It does NOT select a single
    /// objective, choose an exercise format, or trigger content generation.
    /// Activity routing belongs to 10L.
    /// </summary>
    Task<IReadOnlyList<CurriculumObjective>> GetCandidatesForStudentAsync(
        string? cefrLevel,
        IReadOnlyList<string> contextTags,
        IReadOnlyList<string> focusAreas,
        CancellationToken ct = default);

    /// <summary>Returns a single objective by key, or null if not found.</summary>
    Task<CurriculumObjective?> GetByKeyAsync(string key, CancellationToken ct = default);
}
