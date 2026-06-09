using System.Security.Claims;
using LinguaCoach.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardQueryHandler _handler;

    public DashboardController(IDashboardQueryHandler handler)
    {
        _handler = handler;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _handler.HandleAsync(new DashboardQuery(userId), ct);
            return Ok(new
            {
                studentName = result.StudentName,
                careerProfile = result.CareerProfileName,
                cefrLevel = result.CefrLevel,
                message = result.Message,
                lifecycleStage = result.LifecycleStage,
                activityStats = result.ActivityStats is null ? null : new
                {
                    activitiesCompleted = result.ActivityStats.ActivitiesCompleted,
                    latestScore = result.ActivityStats.LatestScore,
                    averageScore = result.ActivityStats.AverageScore,
                },
                currentFocus = result.CurrentFocus is null ? null : new
                {
                    category = result.CurrentFocus.Category,
                    friendlyLabel = result.CurrentFocus.FriendlyLabel,
                },
                nextRecommendedPractice = result.NextRecommendedPractice,
                latestImprovement = result.LatestImprovement,
                learningPath = result.LearningPath is null ? null : new
                {
                    pathId = result.LearningPath.PathId,
                    title = result.LearningPath.Title,
                    modulesCompleted = result.LearningPath.ModulesCompleted,
                    totalModules = result.LearningPath.TotalModules,
                    currentModule = result.LearningPath.CurrentModule is null ? null : new
                    {
                        moduleId = result.LearningPath.CurrentModule.ModuleId,
                        title = result.LearningPath.CurrentModule.Title,
                        description = result.LearningPath.CurrentModule.Description,
                        order = result.LearningPath.CurrentModule.Order,
                        completedActivities = result.LearningPath.CurrentModule.CompletedActivities,
                        totalActivities = result.LearningPath.CurrentModule.TotalActivities,
                        isReadyToComplete = result.LearningPath.CurrentModule.IsReadyToComplete,
                        averageScore = result.LearningPath.CurrentModule.AverageScore,
                    }
                }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("onboarding"))
        {
            return StatusCode(403, new { error = ex.Message });
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
