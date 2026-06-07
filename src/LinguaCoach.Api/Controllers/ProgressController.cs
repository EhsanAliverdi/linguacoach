using System.Security.Claims;
using LinguaCoach.Application.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/progress")]
[Authorize]
public sealed class ProgressController : ControllerBase
{
    private readonly IGetProgressHandler _handler;

    public ProgressController(IGetProgressHandler handler) => _handler = handler;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _handler.HandleAsync(new GetProgressQuery(userId), ct);
            return Ok(ToResponse(result));
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

    private static object ToResponse(ProgressSummaryDto dto) => new
    {
        summary = new
        {
            activitiesCompleted = dto.Stats.ActivitiesCompleted,
            totalAttempts = dto.Stats.TotalAttempts,
            retryAttempts = dto.Stats.RetryAttempts,
            averageScore = dto.Stats.AverageScore,
            latestScore = dto.Stats.LatestScore,
            bestScore = dto.Stats.BestScore,
            activitiesThisWeek = dto.Stats.ActivitiesThisWeek,
            modulesCompleted = dto.Stats.ModulesCompleted,
            currentModuleProgress = dto.Stats.CurrentModuleProgress is null ? null : new
            {
                moduleId = dto.Stats.CurrentModuleProgress.ModuleId,
                title = dto.Stats.CurrentModuleProgress.Title,
                completedActivities = dto.Stats.CurrentModuleProgress.CompletedActivities,
                totalRequired = dto.Stats.CurrentModuleProgress.TotalRequired,
                averageScore = dto.Stats.CurrentModuleProgress.AverageScore,
                latestScore = dto.Stats.CurrentModuleProgress.LatestScore,
                isReadyToComplete = dto.Stats.CurrentModuleProgress.IsReadyToComplete,
            },
        },
        scoreTrend = dto.ScoreTrend.Select(t => new
        {
            attemptDate = t.AttemptDate,
            score = t.Score,
            activityTitle = t.ActivityTitle,
            moduleTitle = t.ModuleTitle,
            attemptNumber = t.AttemptNumber,
        }),
        skillProgress = new
        {
            skills = dto.SkillProgress.Skills.Select(s => new
            {
                skillKey = s.SkillKey,
                skillLabel = s.SkillLabel,
                isWeak = s.IsWeak,
            }),
            topStrengths = dto.SkillProgress.TopStrengths,
            weakestSkills = dto.SkillProgress.WeakestSkills,
        },
        learningFocus = dto.LearningFocus is null ? null : new
        {
            journeySummary = dto.LearningFocus.JourneySummary,
            nextRecommendedFocus = dto.LearningFocus.NextRecommendedFocus,
            recurringMistakes = dto.LearningFocus.RecurringMistakes,
            weakSkills = dto.LearningFocus.WeakSkills,
            strongSkills = dto.LearningFocus.StrongSkills,
        },
        moduleProgress = dto.ModuleProgress.Select(m => new
        {
            moduleId = m.ModuleId,
            title = m.Title,
            status = m.Status,
            completedActivities = m.CompletedActivities,
            totalRequired = m.TotalRequired,
            averageScore = m.AverageScore,
            latestScore = m.LatestScore,
            isReadyToComplete = m.IsReadyToComplete,
            completedAt = m.CompletedAt,
        }),
    };

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
