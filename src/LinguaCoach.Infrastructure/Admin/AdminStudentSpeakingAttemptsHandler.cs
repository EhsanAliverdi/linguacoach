using LinguaCoach.Application.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminStudentSpeakingAttemptsHandler : IAdminStudentSpeakingAttemptsQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminStudentSpeakingAttemptsHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminStudentSpeakingAttemptsResult> HandleAsync(
        AdminStudentSpeakingAttemptsQuery query, CancellationToken ct = default)
    {
        var profileExists = await _db.StudentProfiles
            .AnyAsync(p => p.Id == query.StudentProfileId, ct);
        if (!profileExists)
            return new AdminStudentSpeakingAttemptsResult("NotFound", []);

        var attempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == query.StudentProfileId
                     && a.AudioStorageKey != null)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .Join(_db.LearningActivities,
                a => a.LearningActivityId,
                act => act.Id,
                (a, act) => new
                {
                    a.Id,
                    a.LearningActivityId,
                    ActivityTitle = act.Title,
                    ActivityType = act.ActivityType.ToString(),
                    a.CreatedAt,
                    a.AudioStorageKey,
                    a.PromptKey,
                    a.Score,
                })
            .ToListAsync(ct);

        if (attempts.Count == 0)
        {
            // Check with left join — activity might be soft-deleted or missing
            var rawAttempts = await _db.ActivityAttempts
                .Where(a => a.StudentProfileId == query.StudentProfileId
                         && a.AudioStorageKey != null)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .Select(a => new
                {
                    a.Id,
                    a.LearningActivityId,
                    ActivityTitle = (string?)null,
                    ActivityType = (string?)null,
                    a.CreatedAt,
                    a.AudioStorageKey,
                    a.PromptKey,
                    a.Score,
                })
                .ToListAsync(ct);

            if (rawAttempts.Count == 0)
                return new AdminStudentSpeakingAttemptsResult("Empty", []);

            var dtos = rawAttempts
                .Select(a => new AdminStudentSpeakingAttemptDto(
                    AttemptId: a.Id,
                    ActivityId: a.LearningActivityId,
                    ActivityTitle: null,
                    ActivityType: null,
                    SubmittedAt: a.CreatedAt,
                    MimeType: MimeTypeFromKey(a.AudioStorageKey),
                    Status: DetermineStatus(a.PromptKey, a.Score)))
                .ToList();

            return new AdminStudentSpeakingAttemptsResult("Ready", dtos);
        }

        var result = attempts
            .Select(a => new AdminStudentSpeakingAttemptDto(
                AttemptId: a.Id,
                ActivityId: a.LearningActivityId,
                ActivityTitle: a.ActivityTitle,
                ActivityType: a.ActivityType,
                SubmittedAt: a.CreatedAt,
                MimeType: MimeTypeFromKey(a.AudioStorageKey),
                Status: DetermineStatus(a.PromptKey, a.Score)))
            .ToList();

        return new AdminStudentSpeakingAttemptsResult("Ready", result);
    }

    private static string DetermineStatus(string? promptKey, double? score) =>
        promptKey == "audio_submission_pending" ? "PendingEvaluation" :
        score.HasValue ? "Evaluated" :
        "Submitted";

    private static string? MimeTypeFromKey(string? key) =>
        Path.GetExtension(key ?? string.Empty).ToLowerInvariant() switch
        {
            ".webm" => "audio/webm",
            ".wav"  => "audio/wav",
            ".mp3"  => "audio/mpeg",
            ".mp4"  => "audio/mp4",
            ".m4a"  => "audio/mp4",
            ".ogg"  => "audio/ogg",
            _       => null,
        };
}
