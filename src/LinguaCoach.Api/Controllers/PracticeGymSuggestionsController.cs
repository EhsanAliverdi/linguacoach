using System.Security.Claims;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Student-facing Practice Gym suggestions API.
/// Returns personalised suggestion cards from the readiness pool
/// and provides a safe start/reservation endpoint.
/// Existing /api/activity/practice-gym/next (by skill / by exercise type) is unchanged.
/// </summary>
[ApiController]
[Route("api/practice-gym")]
[Authorize]
public sealed class PracticeGymSuggestionsController : ControllerBase
{
    private readonly IPracticeGymSuggestionService _suggestions;

    public PracticeGymSuggestionsController(IPracticeGymSuggestionService suggestions)
    {
        _suggestions = suggestions;
    }

    /// <summary>
    /// Returns personalised Practice Gym suggestions for the authenticated student.
    /// Sections: SuggestedItems, ContinueItems, ReviewItems.
    /// IsReplenishmentRecommended signals that the pool is below target.
    /// Does not block on AI generation — items come from the pre-filled readiness pool.
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _suggestions.GetSuggestionsForStudentAsync(userId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Reserves a readiness pool item so the student can start it.
    /// Idempotent: re-starting an already-reserved item returns the existing navigation target.
    /// Returns 200 with Success=false and FailureReason for safe (non-throw) error cases.
    /// </summary>
    [HttpPost("suggestions/{readinessItemId:guid}/start")]
    public async Task<IActionResult> StartSuggestion(Guid readinessItemId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _suggestions.StartSuggestionAsync(userId, readinessItemId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Marks a readiness pool item consumed when the student completes the linked activity.
    /// Best-effort: returns 204 even when item is not found or already consumed.
    /// </summary>
    [HttpPost("suggestions/{readinessItemId:guid}/complete")]
    public async Task<IActionResult> CompleteSuggestion(Guid readinessItemId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _suggestions.TryMarkConsumedAsync(userId, readinessItemId, ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
