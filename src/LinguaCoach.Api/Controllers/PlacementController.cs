using System.Security.Claims;
using LinguaCoach.Application.Placement;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Student-facing placement assessment endpoints. Authenticated students only;
/// each student can access only their own placement.
/// See: docs/architecture/placement-assessment-model.md
/// </summary>
[ApiController]
[Route("api/placement")]
[Authorize]
public sealed class PlacementController : ControllerBase
{
    private readonly IStartPlacementHandler _start;
    private readonly ISavePlacementAnswersHandler _save;
    private readonly ICompletePlacementHandler _complete;
    private readonly IGetPlacementStatusHandler _status;
    private readonly IGetPlacementCurrentSectionHandler _current;
    private readonly IGetPlacementResultHandler _result;
    private readonly PlacementAudioService _audio;
    private readonly LinguaCoachDbContext _db;

    public PlacementController(
        IStartPlacementHandler start,
        ISavePlacementAnswersHandler save,
        ICompletePlacementHandler complete,
        IGetPlacementStatusHandler status,
        IGetPlacementCurrentSectionHandler current,
        IGetPlacementResultHandler result,
        PlacementAudioService audio,
        LinguaCoachDbContext db)
    {
        _start = start;
        _save = save;
        _complete = complete;
        _status = status;
        _current = current;
        _result = result;
        _audio = audio;
        _db = db;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _status.HandleAsync(new GetPlacementStatusQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _start.HandleAsync(new StartPlacementCommand(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _current.HandleAsync(new GetPlacementCurrentSectionQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("answers")]
    public async Task<IActionResult> SaveAnswers([FromBody] SavePlacementAnswersDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.SectionKey))
            return BadRequest(new { error = "Section key is required." });

        var answers = (dto.Answers ?? [])
            .Select(a => new PlacementAnswerDto(a.QuestionKey, a.ResponseText, a.SelectedOption))
            .ToList();

        try
        {
            var result = await _save.HandleAsync(
                new SavePlacementAnswersCommand(userId, dto.SectionKey, answers), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _complete.HandleAsync(new CompletePlacementCommand(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("result")]
    public async Task<IActionResult> Result(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _result.HandleAsync(new GetPlacementResultQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Streams the server-generated TTS audio for the placement listening section.
    /// Authenticated student only; student can only access their own placement audio.
    /// </summary>
    [HttpGet("audio/{assessmentId:guid}/listening")]
    public async Task<IActionResult> ListeningAudio(Guid assessmentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        // Ownership check: assessmentId must belong to this student.
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound();

        var assessment = await _db.PlacementAssessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.StudentProfileId == profile.Id, ct);
        if (assessment is null) return NotFound();

        var file = await _audio.GetListeningAudioAsync(assessmentId, ct);
        if (file is null) return NotFound();

        return File(file.Bytes, file.ContentType);
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed class SavePlacementAnswersDto
{
    public string? SectionKey { get; set; }
    public List<PlacementAnswerInputDto>? Answers { get; set; }
}

public sealed class PlacementAnswerInputDto
{
    public string QuestionKey { get; set; } = string.Empty;
    public string? ResponseText { get; set; }
    public string? SelectedOption { get; set; }
}
