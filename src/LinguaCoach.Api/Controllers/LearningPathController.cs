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
    private readonly IAdaptivePathGenerator _adaptiveGenerator;
    private readonly IStudentMemoryQuery _memoryQuery;

    public LearningPathController(
        IGetLearningPathHandler handler,
        ICompleteModuleHandler completeModule,
        IAdaptivePathGenerator adaptiveGenerator,
        IStudentMemoryQuery memoryQuery)
    {
        _handler = handler;
        _completeModule = completeModule;
        _adaptiveGenerator = adaptiveGenerator;
        _memoryQuery = memoryQuery;
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

    [HttpPost("generate-next")]
    public async Task<IActionResult> GenerateNext([FromBody] GenerateNextRequest? request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var path = await _adaptiveGenerator.GenerateNextAsync(new GenerateNextModulesCommand(userId, request?.PathId), ct);
            return Ok(ToResponse(path));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Path was updated concurrently. Please refresh." });
        }
        catch (LinguaCoach.Application.Ai.AiUnavailableException)
        {
            return StatusCode(503, new { error = "AI unavailable.", correlationId = (string?)null });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("memory")]
    public async Task<IActionResult> GetMemory(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return Ok(await _memoryQuery.GetForUserAsync(userId, ct));
    }

    private static object ToResponse(LearningPathDto path) => new
    {
        pathId = path.PathId,
        title = path.Title,
        isActive = path.IsActive,
        modulesCompleted = path.ModulesCompleted,
        totalModules = path.TotalModules,
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
            focusSkill = m.FocusSkill,
            reason = m.Reason,
            difficulty = m.Difficulty,
        })
    };

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record GenerateNextRequest(Guid? PathId);
