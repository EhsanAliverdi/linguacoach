using System.Security.Claims;
using LinguaCoach.Application.Vocabulary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/vocabulary")]
[Authorize]
public sealed class VocabularyController : ControllerBase
{
    private readonly IGetVocabularyHandler _getHandler;
    private readonly IUpdateVocabularyStatusHandler _updateHandler;

    public VocabularyController(
        IGetVocabularyHandler getHandler,
        IUpdateVocabularyStatusHandler updateHandler)
    {
        _getHandler = getHandler;
        _updateHandler = updateHandler;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? status,
        [FromQuery] string? category,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var items = await _getHandler.HandleAsync(new GetVocabularyQuery(userId, status, category), ct);
            return Ok(items.Select(ToResponse));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request?.Status))
            return BadRequest(new { error = "Status is required." });

        try
        {
            await _updateHandler.HandleAsync(new UpdateVocabularyStatusCommand(userId, id, request.Status), ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object ToResponse(StudentVocabularyItemDto dto) => new
    {
        id = dto.Id,
        term = dto.Term,
        suggestedPhrase = dto.SuggestedPhrase,
        meaningOrExplanation = dto.MeaningOrExplanation,
        exampleSentence = dto.ExampleSentence,
        category = dto.Category,
        status = dto.Status,
        source = dto.Source,
        seenCount = dto.SeenCount,
        lastSeenAtUtc = dto.LastSeenAtUtc,
        nextReviewAtUtc = dto.NextReviewAtUtc,
        createdAt = dto.CreatedAt,
        sourceActivityTitle = dto.SourceActivityTitle,
        sourceModuleTitle = dto.SourceModuleTitle,
    };

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record UpdateStatusRequest(string? Status);
