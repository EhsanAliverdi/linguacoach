using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
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

        // Load attempts with audio, then left-join evaluation data
        var attempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == query.StudentProfileId
                     && a.AudioStorageKey != null)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .GroupJoin(
                _db.SpeakingEvaluations,
                a => a.Id,
                e => e.ActivityAttemptId,
                (a, evals) => new { Attempt = a, Evals = evals })
            .SelectMany(
                x => x.Evals.DefaultIfEmpty(),
                (x, eval) => new
                {
                    x.Attempt.Id,
                    x.Attempt.LearningActivityId,
                    x.Attempt.CreatedAt,
                    x.Attempt.AudioStorageKey,
                    x.Attempt.PromptKey,
                    x.Attempt.Score,
                    EvalStatus = eval == null ? (SpeakingEvaluationStatus?)null : eval.Status,
                    EvalProvider = eval == null ? null : eval.ProviderName,
                    EvalModel = eval == null ? null : eval.ModelName,
                    EvalCompletedAt = eval == null ? (DateTime?)null : eval.CompletedAtUtc,
                    EvalFeedbackText = eval == null ? null : eval.FeedbackText,
                    EvalSuggestedImprovement = eval == null ? null : eval.SuggestedImprovement,
                    EvalFailureReason = eval == null ? null : eval.FailureReason,
                    EvalOverallScore = eval == null ? (double?)null : eval.OverallScore,
                })
            .ToListAsync(ct);

        // Activity title lookup — best-effort, null when activity deleted
        var activityIds = attempts.Select(a => a.LearningActivityId).Distinct().ToList();
        var activityTitles = await _db.LearningActivities
            .Where(a => activityIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.Title, Type = a.ActivityType.ToString() }, ct);

        if (attempts.Count == 0)
            return new AdminStudentSpeakingAttemptsResult("Empty", []);

        var dtos = attempts
            .Select(a =>
            {
                activityTitles.TryGetValue(a.LearningActivityId, out var act);
                return new AdminStudentSpeakingAttemptDto(
                    AttemptId: a.Id,
                    ActivityId: a.LearningActivityId,
                    ActivityTitle: act?.Title,
                    ActivityType: act?.Type,
                    SubmittedAt: a.CreatedAt,
                    MimeType: MimeTypeFromKey(a.AudioStorageKey),
                    Status: DetermineStatus(a.PromptKey, a.Score, a.EvalStatus),
                    EvaluationStatus: a.EvalStatus?.ToString(),
                    EvaluationProvider: a.EvalProvider,
                    EvaluationModel: a.EvalModel,
                    EvaluationCompletedAt: a.EvalCompletedAt,
                    EvaluationFeedbackText: a.EvalFeedbackText,
                    EvaluationSuggestedImprovement: a.EvalSuggestedImprovement,
                    EvaluationFailureReason: a.EvalStatus == SpeakingEvaluationStatus.Failed
                        ? a.EvalFailureReason : null,
                    OverallScore: a.EvalOverallScore);
            })
            .ToList();

        return new AdminStudentSpeakingAttemptsResult("Ready", dtos);
    }

    private static string DetermineStatus(
        string? promptKey, double? score, SpeakingEvaluationStatus? evalStatus) =>
        evalStatus switch
        {
            SpeakingEvaluationStatus.Completed => "Evaluated",
            SpeakingEvaluationStatus.Failed => "EvaluationFailed",
            SpeakingEvaluationStatus.NotSupported => "EvaluationUnavailable",
            SpeakingEvaluationStatus.Pending or SpeakingEvaluationStatus.Evaluating => "PendingEvaluation",
            _ => promptKey == "audio_submission_pending" ? "PendingEvaluation" :
                 score.HasValue ? "Evaluated" :
                 "Submitted",
        };

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
