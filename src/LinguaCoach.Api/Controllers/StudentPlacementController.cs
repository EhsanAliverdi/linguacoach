using System.Security.Claims;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Student-facing adaptive placement endpoints (Phase 14A).
/// Students can only access their own assessment data.
/// </summary>
[ApiController]
[Route("api/student/placement")]
[Authorize]
public sealed class StudentPlacementController : ControllerBase
{
    private readonly IPlacementAssessmentService _placement;
    private readonly LinguaCoachDbContext _db;
    private readonly PlacementAssessmentOptions _opts;
    private readonly LinguaCoach.Infrastructure.Placement.AdaptivePlacementAudioService _audio;
    private readonly ILogger<StudentPlacementController> _logger;

    public StudentPlacementController(
        IPlacementAssessmentService placement,
        LinguaCoachDbContext db,
        IOptions<PlacementAssessmentOptions> opts,
        LinguaCoach.Infrastructure.Placement.AdaptivePlacementAudioService audio,
        ILogger<StudentPlacementController> logger)
    {
        _placement = placement;
        _db = db;
        _opts = opts.Value;
        _audio = audio;
        _logger = logger;
    }

    /// <summary>Returns placement gate configuration flags for the student UI.</summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(new
    {
        placementRequiredBeforeLearning = _opts.PlacementRequiredBeforeLearning,
        allowSkipPlacement = _opts.AllowSkipPlacement,
        allowPlacementRetake = _opts.AllowPlacementRetake,
        autoStartPlacement = _opts.AutoStartPlacement,
    });

    /// <summary>Returns the student's latest placement assessment, or null if none exists.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        var result = await _placement.GetLatestAssessmentAsync(profile.Id, ct);
        if (result is null) return Ok(new { hasPlacement = false });
        return Ok(result);
    }

    /// <summary>Returns the next unanswered adaptive item for an in-progress assessment.
    /// When <paramref name="skill"/> is supplied (placement-cards flow), selection is scoped
    /// to that one skill only.</summary>
    [HttpGet("next")]
    public async Task<IActionResult> GetNext([FromQuery] Guid assessmentId, [FromQuery] string? skill, CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        if (!await AssessmentBelongsAsync(profile.Id, assessmentId, ct))
            return NotFound(new { error = "Assessment not found." });

        try
        {
            var item = await _placement.GetNextItemAsync(assessmentId, skill, ct);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Returns per-skill status (percent complete / completed) for the placement
    /// cards page — one entry per configured skill.</summary>
    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills(CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        var statuses = await _placement.GetSkillStatusAsync(profile.Id, ct);
        return Ok(statuses);
    }

    /// <summary>
    /// Starts a new adaptive placement assessment.
    /// Returns the existing in-progress assessment when one already exists.
    /// Returns 409 when placement is already completed and AllowPlacementRetake is false.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        // Block retake when disabled
        if (!_opts.AllowPlacementRetake &&
            (profile.LifecycleStage == StudentLifecycleStage.PlacementCompleted ||
             profile.LifecycleStage == StudentLifecycleStage.CourseReady ||
             profile.LifecycleStage == StudentLifecycleStage.InLesson ||
             profile.LifecycleStage == StudentLifecycleStage.ActiveLearning))
        {
            return Conflict(new { error = "Placement already completed. Retake is not enabled." });
        }

        try
        {
            var result = await _placement.StartAssessmentAsync(profile.Id, "student", ct);
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot start placement for student profile {ProfileId}", profile.Id);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resumes an interrupted placement.
    /// Returns the existing in-progress assessment when ResumeInterruptedPlacement is enabled,
    /// or creates a new one when none exists.
    /// </summary>
    [HttpPost("resume")]
    public async Task<IActionResult> Resume(CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        if (profile.LifecycleStage == StudentLifecycleStage.PlacementCompleted ||
            profile.LifecycleStage == StudentLifecycleStage.CourseReady ||
            profile.LifecycleStage == StudentLifecycleStage.InLesson ||
            profile.LifecycleStage == StudentLifecycleStage.ActiveLearning)
        {
            var completed = await _placement.GetLatestAssessmentAsync(profile.Id, ct);
            if (completed is not null) return Ok(completed);
        }

        try
        {
            var result = await _placement.StartAssessmentAsync(profile.Id, "student_resume", ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot resume placement for student profile {ProfileId}", profile.Id);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Submits a student's response to an adaptive placement item.</summary>
    [HttpPost("respond")]
    public async Task<IActionResult> Respond(
        [FromBody] StudentPlacementRespondRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Response))
            return BadRequest(new { error = "Response is required." });

        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        if (!await AssessmentBelongsAsync(profile.Id, request.AssessmentId, ct))
            return NotFound(new { error = "Assessment not found." });

        try
        {
            var result = await _placement.SubmitResponseAsync(
                request.AssessmentId, request.ItemId, request.Response, request.DurationSeconds, request.Skill, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot submit response for item {ItemId}", request.ItemId);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Triggers assessment completion (called when student reaches the end).</summary>
    [HttpPost("complete")]
    public async Task<IActionResult> Complete(
        [FromBody] StudentPlacementCompleteRequest request,
        CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        if (!await AssessmentBelongsAsync(profile.Id, request.AssessmentId, ct))
            return NotFound(new { error = "Assessment not found." });

        try
        {
            var result = await _placement.CompleteAssessmentAsync(request.AssessmentId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot complete placement {AssessmentId}", request.AssessmentId);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Streams (generating on first request) the listening audio for an adaptive placement item.
    /// Phase 20I-5: replaces the never-wired legacy PlacementAudioService/PlacementController
    /// path for the currently-live adaptive engine.
    /// </summary>
    [HttpGet("audio/{assessmentId:guid}/items/{itemId:guid}/listening")]
    public async Task<IActionResult> GetItemAudio(Guid assessmentId, Guid itemId, CancellationToken ct)
    {
        var profile = await GetStudentProfileAsync(ct);
        if (profile is null) return NotFound(new { error = "Student profile not found." });

        if (!await AssessmentBelongsAsync(profile.Id, assessmentId, ct))
            return NotFound(new { error = "Assessment not found." });

        var item = await _db.PlacementAssessmentItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.PlacementAssessmentId == assessmentId, ct);
        if (item is null) return NotFound(new { error = "Item not found." });

        await _audio.EnsureAudioAsync(item, "en-GB", ct);
        var file = await _audio.GetAudioAsync(item, ct);
        if (file is null) return NotFound(new { error = "Audio is not available for this item." });

        return File(file.Bytes, file.ContentType);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<Domain.Entities.StudentProfile?> GetStudentProfileAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return null;
        return await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    private Task<bool> AssessmentBelongsAsync(Guid studentProfileId, Guid assessmentId, CancellationToken ct) =>
        _db.PlacementAssessments.AnyAsync(
            a => a.Id == assessmentId && a.StudentProfileId == studentProfileId, ct);

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record StudentPlacementRespondRequest(
    Guid AssessmentId,
    Guid ItemId,
    string Response,
    int? DurationSeconds,
    string? Skill = null);

public sealed record StudentPlacementCompleteRequest(Guid AssessmentId);
