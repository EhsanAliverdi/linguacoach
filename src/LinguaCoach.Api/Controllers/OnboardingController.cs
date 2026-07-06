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
    private readonly IOnboardingExperienceHandler _experienceHandler;
    private readonly IStudentOnboardingActiveQuery _activeQuery;
    private readonly IStudentOnboardingSaveDraftHandler _saveDraftHandler;
    private readonly IStudentOnboardingSubmitHandler _submitHandler;

    public OnboardingController(
        IOnboardingHandler handler,
        IOnboardingStatusQuery statusQuery,
        IOnboardingExperienceHandler experienceHandler,
        IStudentOnboardingActiveQuery activeQuery,
        IStudentOnboardingSaveDraftHandler saveDraftHandler,
        IStudentOnboardingSubmitHandler submitHandler)
    {
        _handler = handler;
        _statusQuery = statusQuery;
        _experienceHandler = experienceHandler;
        _activeQuery = activeQuery;
        _saveDraftHandler = saveDraftHandler;
        _submitHandler = submitHandler;
    }

    [HttpPatch]
    [HttpPost]
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
                "preference" when dto.PreferredDurationMinutes.HasValue =>
                    new SetSessionPreferenceRequest(userId, dto.PreferredDurationMinutes.Value),
                // backward compat: old track step (tests / existing students)
                "track" when dto.LearningTrackId.HasValue =>
#pragma warning disable CS0618
                    new SetTrackRequest(userId, dto.LearningTrackId.Value),
#pragma warning restore CS0618
                // Free-text career path takes priority when CareerContext is provided.
                "career" when dto.CareerContext is { Length: > 0 } =>
                    new SetCareerContextTextRequest(userId, dto.CareerContext),
                "career" when dto.CareerProfileId.HasValue =>
                    new SetCareerRequest(userId, dto.CareerProfileId.Value),
                // Skill step with optional learning goal fields.
                "skill" when dto.SkillFocus.HasValue =>
                    new SetSkillGoalRequest(userId, dto.SkillFocus.Value, dto.LearningGoalDescription, dto.DifficultSituationsText),
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

    [HttpPatch("experience")]
    public async Task<IActionResult> Experience([FromBody] ExperienceStepDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _experienceHandler.HandleAsync(
                new SetExperienceRequest(
                    userId,
                    dto.ProfessionalExperienceLevel,
                    dto.RoleFamiliarity),
                ct);
            return Ok(new { success = result.Success });
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

    // ── Form.io onboarding flow endpoints ─────────────────────────────────────

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        try
        {
            var result = await _activeQuery.HandleAsync(new GetStudentOnboardingActiveQuery(userId), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("save-draft")]
    public async Task<IActionResult> SaveDraft([FromBody] OnboardingSubmissionDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.SubmissionJson))
            return BadRequest(new { error = "submissionJson is required." });

        try
        {
            await _saveDraftHandler.HandleAsync(new SaveOnboardingDraftCommand(userId, dto.SubmissionJson), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] OnboardingSubmissionDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.SubmissionJson))
            return BadRequest(new { error = "submissionJson is required." });

        try
        {
            var result = await _submitHandler.HandleAsync(new SubmitOnboardingCommand(userId, dto.SubmissionJson), ct);
            return Ok(result);
        }
        catch (OnboardingV2ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
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

public sealed class ExperienceStepDto
{
    public ProfessionalExperienceLevel ProfessionalExperienceLevel { get; set; }
    public RoleFamiliarity RoleFamiliarity { get; set; }
}

public sealed class OnboardingSubmissionDto
{
    public string? SubmissionJson { get; set; }
}

public sealed class OnboardingStepDto
{
    public string? Step { get; set; }
    public Guid? LanguagePairId { get; set; }
    public Guid? LearningTrackId { get; set; }
    public int? PreferredDurationMinutes { get; set; }
    public Guid? CareerProfileId { get; set; }
    public string? CareerContext { get; set; }
    public SkillFocus? SkillFocus { get; set; }
    public string? LearningGoalDescription { get; set; }
    public string? DifficultSituationsText { get; set; }
}
