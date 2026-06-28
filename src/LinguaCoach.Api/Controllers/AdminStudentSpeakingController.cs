using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only view of a student's submitted speaking/audio recordings.
/// Audio bytes are streamed through this endpoint — storage keys are never returned to clients.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminStudentSpeakingController : ControllerBase
{
    private readonly IAdminStudentSpeakingAttemptsQuery _query;
    private readonly SpeakingAudioService _audio;
    private readonly LinguaCoachDbContext _db;

    public AdminStudentSpeakingController(
        IAdminStudentSpeakingAttemptsQuery query,
        SpeakingAudioService audio,
        LinguaCoachDbContext db)
    {
        _query = query;
        _audio = audio;
        _db = db;
    }

    /// <summary>
    /// Returns up to 20 most-recent speaking/audio submissions for a student.
    /// Returns Status=Empty when no recordings exist.
    /// Returns Status=NotFound when the student profile does not exist.
    /// </summary>
    [HttpGet("api/admin/students/{studentProfileId:guid}/speaking-attempts")]
    public async Task<IActionResult> GetSpeakingAttempts(Guid studentProfileId, CancellationToken ct)
    {
        var result = await _query.HandleAsync(
            new AdminStudentSpeakingAttemptsQuery(studentProfileId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Streams the audio bytes for a specific speaking attempt.
    /// Admin-only. Never exposes storage keys or bucket paths.
    /// Returns 404 when the attempt or audio is not found.
    /// </summary>
    [HttpGet("api/admin/students/{studentProfileId:guid}/speaking-attempts/{attemptId:guid}/audio")]
    public async Task<IActionResult> GetAttemptAudio(
        Guid studentProfileId, Guid attemptId, CancellationToken ct)
    {
        var profileExists = await _db.StudentProfiles
            .AnyAsync(p => p.Id == studentProfileId, ct);
        if (!profileExists) return NotFound();

        var attempt = await _db.ActivityAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId
                                   && a.StudentProfileId == studentProfileId
                                   && a.AudioStorageKey != null, ct);
        if (attempt is null || string.IsNullOrWhiteSpace(attempt.AudioStorageKey))
            return NotFound();

        var audio = await _audio.GetAudioAsync(attempt.AudioStorageKey, ct);
        if (audio is null) return NotFound();

        return File(audio.Bytes, audio.ContentType);
    }
}
