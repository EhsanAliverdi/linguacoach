using System.Security.Claims;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize]
public sealed class OnboardingController : ControllerBase
{
    private readonly IOnboardingHandler _handler;
    private readonly IOnboardingStatusQuery _statusQuery;

    public OnboardingController(IOnboardingHandler handler, IOnboardingStatusQuery statusQuery)
    {
        _handler = handler;
        _statusQuery = statusQuery;
    }

    [HttpPatch]
    public async Task<IActionResult> Step([FromBody] OnboardingStepDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            OnboardingStepRequest request = dto.Step?.ToLowerInvariant() switch
            {
                "language" when dto.LanguagePairId.HasValue =>
                    new SetLanguageRequest(userId, dto.LanguagePairId.Value),
                "track" when dto.LearningTrackId.HasValue =>
                    new SetTrackRequest(userId, dto.LearningTrackId.Value),
                "career" when dto.CareerProfileId.HasValue =>
                    new SetCareerRequest(userId, dto.CareerProfileId.Value),
                "skill" when dto.SkillFocus.HasValue =>
                    new SetSkillRequest(userId, dto.SkillFocus.Value),
                _ => null!
            };

            if (request is null)
                return BadRequest(new { error = "Invalid step or missing required field for the step." });

            var result = await _handler.HandleAsync(request, ct);
            return Ok(new { lastCompletedStep = result.LastCompletedStep, isComplete = result.IsComplete });
        }
        catch (OnboardingStepOutOfOrderException ex)
        {
            return BadRequest(new { error = $"Step out of order. Expected: {ex.ExpectedStep}, got: {ex.RequestedStep}." });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await _statusQuery.HandleAsync(new OnboardingStatusQuery(userId), ct);
        return Ok(new { currentStep = result.CurrentStep, isComplete = result.IsComplete, languagePairId = result.LanguagePairId });
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed class OnboardingStepDto
{
    public string? Step { get; set; }
    public Guid? LanguagePairId { get; set; }
    public Guid? LearningTrackId { get; set; }
    public Guid? CareerProfileId { get; set; }
    public SkillFocus? SkillFocus { get; set; }
}
