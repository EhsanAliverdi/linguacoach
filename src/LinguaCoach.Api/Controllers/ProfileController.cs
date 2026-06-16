using System.Security.Claims;
using LinguaCoach.Application.Profile;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly IGetStudentProfileQueryHandler _queryHandler;
    private readonly IUpdateLearningPreferencesCommandHandler _commandHandler;

    public ProfileController(
        IGetStudentProfileQueryHandler queryHandler,
        IUpdateLearningPreferencesCommandHandler commandHandler)
    {
        _queryHandler = queryHandler;
        _commandHandler = commandHandler;
    }

    /// <summary>GET /api/profile — student reads own profile and preferences.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _queryHandler.HandleAsync(new GetStudentProfileQuery(userId), ct);
        if (result is null) return NotFound(new { error = "Profile not found." });

        return Ok(new
        {
            profileId = result.ProfileId,
            userId = result.UserId,
            firstName = result.FirstName,
            lastName = result.LastName,
            displayName = result.DisplayName,
            preferredName = result.PreferredName,
            email = result.Email,
            cefrLevel = result.CefrLevel,
            learningGoals = result.LearningGoals,
            customLearningGoal = result.CustomLearningGoal,
            focusAreas = result.FocusAreas,
            customFocusArea = result.CustomFocusArea,
            supportLanguageCode = result.SupportLanguageCode,
            supportLanguageName = result.SupportLanguageName,
            translationHelpPreference = result.TranslationHelpPreference?.ToString(),
            preferredSessionDurationMinutes = result.PreferredSessionDurationMinutes,
            difficultyPreference = result.DifficultyPreference?.ToString(),
            learningPreferencesUpdatedAt = result.LearningPreferencesUpdatedAt,
        });
    }

    /// <summary>PUT /api/profile/preferences — student updates own editable preferences.</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateLearningPreferencesRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var command = new UpdateLearningPreferencesCommand(
                UserId: userId,
                PreferredName: request.PreferredName,
                SupportLanguageCode: request.SupportLanguageCode,
                SupportLanguageName: request.SupportLanguageName,
                TranslationHelpPreference: request.TranslationHelpPreference,
                LearningGoals: request.LearningGoals,
                CustomLearningGoal: request.CustomLearningGoal,
                FocusAreas: request.FocusAreas,
                CustomFocusArea: request.CustomFocusArea,
                DifficultyPreference: request.DifficultyPreference,
                PreferredSessionDurationMinutes: request.PreferredSessionDurationMinutes);

            await _commandHandler.HandleAsync(command, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record UpdateLearningPreferencesRequest(
    string? PreferredName,
    string? SupportLanguageCode,
    string? SupportLanguageName,
    TranslationHelpPreference? TranslationHelpPreference,
    List<string>? LearningGoals,
    string? CustomLearningGoal,
    List<string>? FocusAreas,
    string? CustomFocusArea,
    DifficultyPreference? DifficultyPreference,
    int? PreferredSessionDurationMinutes);
