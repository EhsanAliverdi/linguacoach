using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Read-only admin endpoints for inspecting the seeded curriculum syllabus.
/// No write endpoints in Phase 10K — admin curriculum builder is a future phase.
/// </summary>
[ApiController]
[Route("api/admin/curriculum")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminCurriculumController : ControllerBase
{
    private readonly ICurriculumSyllabusQuery _query;

    public AdminCurriculumController(ICurriculumSyllabusQuery query)
    {
        _query = query;
    }

    /// <summary>Returns all active curriculum objectives ordered by CEFR level and recommended order.</summary>
    [HttpGet("objectives")]
    public async Task<IActionResult> ListObjectives(
        [FromQuery] string? cefrLevel,
        [FromQuery] string? skill,
        CancellationToken ct)
    {
        IReadOnlyList<Domain.Entities.CurriculumObjective> objectives;

        if (!string.IsNullOrWhiteSpace(cefrLevel) && !string.IsNullOrWhiteSpace(skill))
            objectives = await _query.GetByCefrAndSkillAsync(cefrLevel, skill, ct);
        else if (!string.IsNullOrWhiteSpace(cefrLevel))
            objectives = await _query.GetByCefrAsync(cefrLevel, ct);
        else
            objectives = await _query.GetActiveObjectivesAsync(ct);

        return Ok(objectives.Select(CurriculumObjectiveDto.From));
    }

    /// <summary>Returns a single curriculum objective by its stable key.</summary>
    [HttpGet("objectives/{key}")]
    public async Task<IActionResult> GetObjective(string key, CancellationToken ct)
    {
        var objective = await _query.GetByKeyAsync(key, ct);
        if (objective is null)
            return NotFound(new { error = $"Curriculum objective '{key}' not found." });

        return Ok(CurriculumObjectiveDto.From(objective));
    }
}

/// <summary>Read-only DTO for curriculum objective admin inspection.</summary>
public sealed record CurriculumObjectiveDto(
    Guid Id,
    string Key,
    string Title,
    string Description,
    string CefrLevel,
    string PrimarySkill,
    string SecondarySkillsJson,
    string ContextTagsJson,
    string FocusTagsJson,
    string PrerequisiteKeysJson,
    int RecommendedOrder,
    int DifficultyBand,
    bool IsActive,
    bool IsReviewable,
    bool IsExamInspired,
    string? TeachingNotes)
{
    public static CurriculumObjectiveDto From(Domain.Entities.CurriculumObjective o) =>
        new(o.Id, o.Key, o.Title, o.Description, o.CefrLevel, o.PrimarySkill,
            o.SecondarySkillsJson, o.ContextTagsJson, o.FocusTagsJson,
            o.PrerequisiteKeysJson, o.RecommendedOrder, o.DifficultyBand,
            o.IsActive, o.IsReviewable, o.IsExamInspired, o.TeachingNotes);
}
