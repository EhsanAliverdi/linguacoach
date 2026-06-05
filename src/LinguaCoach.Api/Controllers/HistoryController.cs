using System.Security.Claims;
using LinguaCoach.Application.History;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class HistoryController : ControllerBase
{
    private readonly IGetModuleActivitiesHandler _moduleActivities;
    private readonly IGetActivityAttemptsHandler _activityAttempts;

    public HistoryController(
        IGetModuleActivitiesHandler moduleActivities,
        IGetActivityAttemptsHandler activityAttempts)
    {
        _moduleActivities = moduleActivities;
        _activityAttempts = activityAttempts;
    }

    /// <summary>GET /api/learning-path/modules/{moduleId}/activities — module history</summary>
    [HttpGet("learning-path/modules/{moduleId:guid}/activities")]
    public async Task<IActionResult> GetModuleActivities(Guid moduleId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _moduleActivities.HandleAsync(
                new GetModuleActivitiesQuery(userId, moduleId), ct);

            return Ok(new
            {
                moduleId = result.ModuleId,
                title = result.Title,
                description = result.Description,
                completedActivities = result.CompletedActivities,
                totalRequired = result.TotalRequired,
                averageScore = result.AverageScore,
                latestScore = result.LatestScore,
                isReadyToComplete = result.IsReadyToComplete,
                isCompleted = result.IsCompleted,
                activities = result.Activities.Select(a => new
                {
                    activityId = a.ActivityId,
                    title = a.Title,
                    activityType = a.ActivityType,
                    attemptCount = a.AttemptCount,
                    bestScore = a.BestScore,
                    latestScore = a.LatestScore,
                    latestAttemptAt = a.LatestAttemptAt,
                    hasFeedback = a.HasFeedback,
                }),
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>GET /api/activity/{activityId}/attempts — attempt history for an activity</summary>
    [HttpGet("activity/{activityId:guid}/attempts")]
    public async Task<IActionResult> GetActivityAttempts(Guid activityId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _activityAttempts.HandleAsync(
                new GetActivityAttemptsQuery(userId, activityId), ct);

            return Ok(new
            {
                activityId = result.ActivityId,
                title = result.Title,
                activityType = result.ActivityType,
                situation = result.Situation,
                learningGoal = result.LearningGoal,
                targetPhrases = result.TargetPhrases,
                attempts = result.Attempts.Select(a => new
                {
                    attemptId = a.AttemptId,
                    attemptNumber = a.AttemptNumber,
                    submittedAt = a.SubmittedAt,
                    score = a.Score,
                    coachSummary = a.CoachSummary,
                    focusFirst = a.FocusFirst,
                    changes = a.Changes.Select(c => new
                    {
                        type = c.Type,
                        original = c.Original,
                        suggested = c.Suggested,
                        reason = c.Reason,
                        category = c.Category,
                        severity = c.Severity,
                    }),
                    whatYouDidWell = a.WhatYouDidWell,
                    grammarIssues = a.GrammarIssues,
                    vocabularyIssues = a.VocabularyIssues,
                    toneIssues = a.ToneIssues,
                    clarityIssues = a.ClarityIssues,
                    miniLesson = a.MiniLesson,
                    nextImprovementStep = a.NextImprovementStep,
                    suggestedImprovedVersion = a.SuggestedImprovedVersion,
                    nativeLanguageExplanation = a.NativeLanguageExplanation,
                    submittedContent = a.SubmittedContent,
                }),
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
