using System.Security.Claims;
using LinguaCoach.Application.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Authorize]
public sealed class StudentProgressController : ControllerBase
{
    private readonly IStudentProgressSummaryHandler _handler;

    public StudentProgressController(IStudentProgressSummaryHandler handler) => _handler = handler;

    [HttpGet("api/student/progress/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _handler.HandleAsync(new GetStudentProgressSummaryQuery(userId), ct);
            return Ok(ToResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private static object ToResponse(StudentProgressSummaryDto dto) => new
    {
        learning = new
        {
            currentCefrLevel = dto.Learning.CurrentCefrLevel,
            placementCompletedAt = dto.Learning.PlacementCompletedAt,
            currentLearningPhase = dto.Learning.CurrentLearningPhase,
            totalObjectives = dto.Learning.TotalObjectives,
            objectivesCompleted = dto.Learning.ObjectivesCompleted,
            objectivesMastered = dto.Learning.ObjectivesMastered,
            objectivesInProgress = dto.Learning.ObjectivesInProgress,
            objectivesRemaining = dto.Learning.ObjectivesRemaining,
            completionPercentage = dto.Learning.CompletionPercentage,
            currentObjectiveKey = dto.Learning.CurrentObjectiveKey,
            currentObjectiveSkill = dto.Learning.CurrentObjectiveSkill,
            objectivesCompletedToday = dto.Learning.ObjectivesCompletedToday,
        },
        skills = dto.Skills.Select(s => new
        {
            skillKey = s.SkillKey,
            skillLabel = s.SkillLabel,
            isWeak = s.IsWeak,
            scorePercent = s.ScorePercent,
        }),
        cefr = new
        {
            startingCefrLevel = dto.Cefr.StartingCefrLevel,
            currentCefrLevel = dto.Cefr.CurrentCefrLevel,
            cefrImproved = dto.Cefr.CefrImproved,
            placementDate = dto.Cefr.PlacementDate,
            note = dto.Cefr.Note,
        },
        mastery = new
        {
            masteredObjectivesCount = dto.Mastery.MasteredObjectivesCount,
            inProgressObjectivesCount = dto.Mastery.InProgressObjectivesCount,
            reviewQueueCount = dto.Mastery.ReviewQueueCount,
            weakSkillsCount = dto.Mastery.WeakSkillsCount,
            weakSkillLabels = dto.Mastery.WeakSkillLabels,
        },
        recentActivity = dto.RecentActivity.Select(e => new
        {
            eventType = e.EventType,
            description = e.Description,
            detail = e.Detail,
            occurredAt = e.OccurredAt,
        }),
        focus = new
        {
            recommendations = dto.Focus.Recommendations,
            recurringMistakes = dto.Focus.RecurringMistakes,
            journeySummary = dto.Focus.JourneySummary,
        },
    };

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
