using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Curriculum;

/// <summary>
/// Read-only query service for the curriculum syllabus.
/// All queries use AsNoTracking — this service never writes.
/// Returns candidate lists only; does NOT select activities or exercise formats.
/// </summary>
public sealed class CurriculumSyllabusQueryService : ICurriculumSyllabusQuery
{
    private readonly LinguaCoachDbContext _db;

    public CurriculumSyllabusQueryService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CurriculumObjective>> GetActiveObjectivesAsync(CancellationToken ct = default)
        => await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CurriculumObjective>> GetByCefrAsync(
        string cefrLevel, CancellationToken ct = default)
    {
        var normalised = cefrLevel?.ToUpperInvariant() ?? string.Empty;
        return await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => o.IsActive && o.CefrLevel == normalised)
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndSkillAsync(
        string cefrLevel, string primarySkill, CancellationToken ct = default)
    {
        var normCefr = cefrLevel?.ToUpperInvariant() ?? string.Empty;
        var normSkill = primarySkill?.ToLowerInvariant() ?? string.Empty;
        return await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => o.IsActive && o.CefrLevel == normCefr && o.PrimarySkill == normSkill)
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndContextAsync(
        string cefrLevel, string contextTag, CancellationToken ct = default)
    {
        var normCefr = cefrLevel?.ToUpperInvariant() ?? string.Empty;
        var tag = contextTag?.ToLowerInvariant() ?? string.Empty;
        // JSON contains check — works on PostgreSQL text columns.
        return await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => o.IsActive
                     && o.CefrLevel == normCefr
                     && o.ContextTagsJson.Contains(tag))
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CurriculumObjective>> GetByCefrAndFocusAreaAsync(
        string cefrLevel, string focusArea, CancellationToken ct = default)
    {
        var normCefr = cefrLevel?.ToUpperInvariant() ?? string.Empty;
        var area = focusArea?.ToLowerInvariant() ?? string.Empty;
        return await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => o.IsActive
                     && o.CefrLevel == normCefr
                     && o.FocusTagsJson.Contains(area))
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CurriculumObjective>> GetPrerequisitesAsync(
        string objectiveKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectiveKey))
            return [];

        var target = await _db.CurriculumObjectives
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Key == objectiveKey, ct);

        if (target is null || target.PrerequisiteKeysJson == "[]")
            return [];

        var prereqKeys = ParseJsonStringArray(target.PrerequisiteKeysJson);
        return await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => prereqKeys.Contains(o.Key))
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CurriculumObjective>> GetCandidatesForStudentAsync(
        string? cefrLevel,
        IReadOnlyList<string> contextTags,
        IReadOnlyList<string> focusAreas,
        CancellationToken ct = default)
    {
        // Unknown/null CEFR falls back to A1 for safe generic path.
        var normCefr = CefrLevelConstants.IsValid(cefrLevel)
            ? cefrLevel!.ToUpperInvariant()
            : CefrLevelConstants.A1;

        var all = await _db.CurriculumObjectives
            .AsNoTracking()
            .Where(o => o.IsActive && o.CefrLevel == normCefr)
            .OrderBy(o => o.RecommendedOrder)
            .ToListAsync(ct);

        if (contextTags.Count == 0 && focusAreas.Count == 0)
            return all;

        // Filter: keep objectives that match at least one requested context tag or focus area.
        return all
            .Where(o =>
                contextTags.Any(tag => o.ContextTagsJson.Contains(tag, StringComparison.OrdinalIgnoreCase))
                || focusAreas.Any(area => o.FocusTagsJson.Contains(area, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<CurriculumObjective?> GetByKeyAsync(string key, CancellationToken ct = default)
        => await _db.CurriculumObjectives
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Key == key, ct);

    private static List<string> ParseJsonStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        return json
            .Trim('[', ']')
            .Split(',')
            .Select(s => s.Trim().Trim('"'))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
