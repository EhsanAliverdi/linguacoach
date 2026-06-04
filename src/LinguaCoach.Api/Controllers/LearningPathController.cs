using System.Security.Claims;
using LinguaCoach.Application.LearningPath;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/learning-path")]
[Authorize]
public sealed class LearningPathController : ControllerBase
{
    private readonly IGetLearningPathHandler _handler;
    private readonly ICompleteModuleHandler _completeModule;

    public LearningPathController(IGetLearningPathHandler handler, ICompleteModuleHandler completeModule)
    {
        _handler = handler;
        _completeModule = completeModule;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var path = await _handler.HandleAsync(new GetLearningPathQuery(userId), ct);
            if (path is null)
                return NotFound(new { error = "No active learning path found. Complete onboarding to generate your path." });

            return Ok(new
            {
                pathId = path.PathId,
                title = path.Title,
                isActive = path.IsActive,
                modulesCompleted = path.ModulesCompleted,
                totalModules = path.TotalModules,
                currentFocus = path.CurrentFocus is null ? null : new
                {
                    category = path.CurrentFocus.Category,
                    friendlyLabel = path.CurrentFocus.FriendlyLabel,
                    frequency = path.CurrentFocus.Frequency,
                },
                modules = path.Modules.Select(m => new
                {
                    moduleId = m.ModuleId,
                    title = m.Title,
                    description = m.Description,
                    order = m.Order,
                    completedActivities = m.CompletedActivities,
                    totalActivities = m.TotalActivities,
                    isCurrent = m.IsCurrent,
                    isCompleted = m.IsCompleted,
                    isReadyToComplete = m.IsReadyToComplete,
                    averageScore = m.AverageScore,
                    latestScore = m.LatestScore,
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("modules/{moduleId:guid}/complete")]
    public async Task<IActionResult> CompleteModule(Guid moduleId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            await _completeModule.HandleAsync(new CompleteModuleCommand(userId, moduleId), ct);
            return NoContent();
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

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
