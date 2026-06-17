using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin endpoints for curriculum objective management and routing preview.
/// Phase 10K: read-only list/get.
/// Phase 10Q: full CRUD, activate/deactivate, taxonomy, routing preview.
/// </summary>
[ApiController]
[Route("api/admin/curriculum")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminCurriculumController : ControllerBase
{
    private readonly ICurriculumSyllabusQuery _query;
    private readonly IAdminCurriculumSyllabusQuery _adminQuery;
    private readonly ICurriculumObjectiveWriteService _writeService;

    public AdminCurriculumController(
        ICurriculumSyllabusQuery query,
        IAdminCurriculumSyllabusQuery adminQuery,
        ICurriculumObjectiveWriteService writeService)
    {
        _query = query;
        _adminQuery = adminQuery;
        _writeService = writeService;
    }

    // ── Read endpoints ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns curriculum objectives with optional filters.
    /// includeInactive=true returns both active and inactive objectives.
    /// </summary>
    [HttpGet("objectives")]
    public async Task<IActionResult> ListObjectives(
        [FromQuery] string? cefrLevel,
        [FromQuery] string? skill,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var objectives = await _adminQuery.GetAllObjectivesForAdminAsync(cefrLevel, skill, isActive, ct);
        return Ok(objectives.Select(AdminCurriculumObjectiveDto.From));
    }

    /// <summary>Returns a single curriculum objective by key (active or inactive).</summary>
    [HttpGet("objectives/{key}")]
    public async Task<IActionResult> GetObjective(string key, CancellationToken ct)
    {
        var objective = await _query.GetByKeyAsync(key, ct);
        if (objective is null)
            return NotFound(new { error = $"Curriculum objective '{key}' not found." });

        return Ok(AdminCurriculumObjectiveDto.From(objective));
    }

    /// <summary>Returns the known taxonomy: CEFR levels, skills, context tags.</summary>
    [HttpGet("taxonomy")]
    public IActionResult GetTaxonomy() => Ok(CurriculumTaxonomyDto.Build());

    // ── Write endpoints ───────────────────────────────────────────────────────

    /// <summary>Creates a new curriculum objective.</summary>
    [HttpPost("objectives")]
    public async Task<IActionResult> CreateObjective(
        [FromBody] AdminCurriculumObjectiveUpsertRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _writeService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetObjective), new { key = result.Key }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Updates all mutable fields of an existing curriculum objective.</summary>
    [HttpPut("objectives/{key}")]
    public async Task<IActionResult> UpdateObjective(
        string key,
        [FromBody] AdminCurriculumObjectiveUpsertRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _writeService.UpdateAsync(key, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Activates a deactivated curriculum objective.</summary>
    [HttpPost("objectives/{key}/activate")]
    public async Task<IActionResult> ActivateObjective(string key, CancellationToken ct)
    {
        try
        {
            var result = await _writeService.ActivateAsync(key, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Deactivates a curriculum objective without deleting it or historical records.</summary>
    [HttpPost("objectives/{key}/deactivate")]
    public async Task<IActionResult> DeactivateObjective(string key, CancellationToken ct)
    {
        try
        {
            var result = await _writeService.DeactivateAsync(key, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Previews routing without mutating student state or generating AI content.
    /// Safe to call multiple times for the same student.
    /// </summary>
    [HttpPost("routing-preview")]
    public async Task<IActionResult> RoutingPreview(
        [FromBody] AdminRoutingPreviewRequest request,
        CancellationToken ct)
    {
        var result = await _writeService.PreviewRoutingAsync(request, ct);
        return Ok(result);
    }
}

/// <summary>Read-only DTO for curriculum objective admin inspection (Phase 10K compat).</summary>
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
