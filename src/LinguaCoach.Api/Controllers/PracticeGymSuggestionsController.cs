using System.Security.Claims;
using LinguaCoach.Application.ActivityDefinitionLaunch;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly IActivityDefinitionLaunchService _activityDefinitionLaunch;
    private readonly LinguaCoachDbContext _db;

    public PracticeGymSuggestionsController(
        IPracticeGymSuggestionService suggestions,
        IActivityDefinitionLaunchService activityDefinitionLaunch,
        LinguaCoachDbContext db)
    {
        _suggestions = suggestions;
        _activityDefinitionLaunch = activityDefinitionLaunch;
        _db = db;
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

    /// <summary>
    /// Phase H10 — launches an approved, launch-eligible Activity Definition from an approved
    /// Module suggestion into a real, runnable practice attempt. Returns 200 with
    /// Success=false and UnsupportedReason for every non-launchable case (safe, non-throw) — the
    /// existing Practice Gym suggestions above remain the fallback either way.
    /// </summary>
    [HttpPost("module-suggestions/{moduleDefinitionId:guid}/start")]
    public async Task<IActionResult> StartModuleSuggestion(Guid moduleDefinitionId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return Unauthorized();

        var result = await _activityDefinitionLaunch.LaunchAsync(
            new ActivityDefinitionLaunchRequest(profile.Id, moduleDefinitionId, ActivityDefinitionLaunchSource.PracticeGym),
            ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
