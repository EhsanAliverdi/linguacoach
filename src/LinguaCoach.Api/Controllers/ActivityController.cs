using System.Security.Claims;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize]
public sealed class ActivityController : ControllerBase
{
    private readonly IGetNextActivityHandler _getNextActivity;
    private readonly ISubmitActivityAttemptHandler _submitAttempt;

    public ActivityController(
        IGetNextActivityHandler getNextActivity,
        ISubmitActivityAttemptHandler submitAttempt)
    {
        _getNextActivity = getNextActivity;
        _submitAttempt = submitAttempt;
    }

    /// <summary>
    /// Returns the next recommended activity for the student.
    /// Primary: AI-generated or deterministic. Fallback: SystemFallback from seed data.
    /// </summary>
    [HttpGet("next")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> GetNext(
        [FromQuery] ActivityType? type = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _getNextActivity.HandleAsync(new GetNextActivityQuery(userId, type), ct);
            return Ok(ToActivityResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submits a student attempt. Supports both WritingScenario (text) and VocabularyPractice (answers array).
    /// </summary>
    [HttpPost("{activityId:guid}/attempt")]
    [EnableRateLimiting("WritingAi")]
    public async Task<IActionResult> SubmitAttempt(
        Guid activityId,
        [FromBody] SubmitAttemptRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        // For VocabularyPractice, answers array is used; submittedContent may be empty.
        // For WritingScenario, submittedContent is required.
        var hasContent = !string.IsNullOrWhiteSpace(request.SubmittedContent);
        var hasAnswers = request.Answers is { Count: > 0 };

        if (!hasContent && !hasAnswers)
            return BadRequest(new { error = "Either SubmittedContent or Answers is required." });

        var vocabAnswers = request.Answers?
            .Select(a => new VocabAnswerDto(a.VocabularyItemId, a.Answer ?? string.Empty))
            .ToList()
            as IReadOnlyList<VocabAnswerDto>;

        try
        {
            var result = await _submitAttempt.HandleAsync(
                new SubmitActivityAttemptCommand(
                    userId, activityId,
                    request.SubmittedContent ?? string.Empty,
                    request.AudioUrl,
                    vocabAnswers),
                ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object ToActivityResponse(ActivityDto dto) => new
    {
        activityId = dto.ActivityId,
        activityType = ToCamelCase(dto.ActivityType.ToString()),
        source = ToCamelCase(dto.Source.ToString()),
        title = dto.Title,
        difficulty = dto.Difficulty,
        // WritingScenario fields
        situation = dto.Situation,
        learningGoal = dto.LearningGoal,
        targetPhrases = dto.TargetPhrases,
        targetVocabulary = dto.TargetVocabulary,
        exampleText = dto.ExampleText,
        commonMistakeToAvoid = dto.CommonMistakeToAvoid,
        instructionInSourceLanguage = dto.InstructionInSourceLanguage,
        // VocabularyPractice fields
        instructions = dto.Instructions,
        practiceMode = dto.PracticeMode,
        vocabItems = dto.VocabItems?.Select(i => new
        {
            vocabularyItemId = i.VocabularyItemId,
            term = i.Term,
            prompt = i.Prompt,
            hint = i.Hint,
            explanation = i.Explanation,
        }),
    };

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record SubmitAttemptRequest(
    string? SubmittedContent,
    string? AudioUrl = null,
    IReadOnlyList<AnswerRequest>? Answers = null);

public sealed record AnswerRequest(Guid VocabularyItemId, string? Answer);
