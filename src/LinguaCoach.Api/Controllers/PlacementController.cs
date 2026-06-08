using System.Security.Claims;
using LinguaCoach.Application.Placement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public PlacementController(
        IStartPlacementHandler start,
        ISavePlacementAnswersHandler save,
        ICompletePlacementHandler complete,
        IGetPlacementStatusHandler status,
        IGetPlacementCurrentSectionHandler current,
        IGetPlacementResultHandler result)
    {
        _start = start;
        _save = save;
        _complete = complete;
        _status = status;
        _current = current;
        _result = result;
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
