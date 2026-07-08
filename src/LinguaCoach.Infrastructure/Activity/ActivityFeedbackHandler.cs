using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Handles student-submitted activity feedback (difficulty/clarity/usefulness/repeat
/// preference) — Phase B2. Upserts by (StudentProfileId, ActivityAttemptId) when an attempt is
/// known, else by (StudentProfileId, LearningActivityId), backfilling provenance from the
/// matching <see cref="StudentActivityUsageLog"/> row when one exists.
/// </summary>
public sealed class ActivityFeedbackHandler : ISubmitActivityFeedbackHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<ActivityFeedbackHandler> _logger;

    public ActivityFeedbackHandler(LinguaCoachDbContext db, ILogger<ActivityFeedbackHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ActivityFeedbackSignalDto> HandleAsync(
        SubmitActivityFeedbackCommand command,
        CancellationToken ct = default)
    {
        if (command.OptionalComment is { Length: > ActivityFeedbackSignal.MaxOptionalCommentLength })
            throw new ArgumentException(
                $"OptionalComment must be at most {ActivityFeedbackSignal.MaxOptionalCommentLength} characters.",
                nameof(command));

        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == command.LearningActivityId, ct)
            ?? throw new InvalidOperationException("Activity not found.");

        Domain.Entities.ActivityAttempt? attempt = null;
        if (command.ActivityAttemptId.HasValue)
        {
            attempt = await _db.ActivityAttempts
                .FirstOrDefaultAsync(a => a.Id == command.ActivityAttemptId.Value, ct);

            if (attempt is null)
                throw new InvalidOperationException("Activity attempt not found.");
            if (attempt.StudentProfileId != profile.Id)
                throw new UnauthorizedAccessException("This activity attempt does not belong to the requesting student.");
        }

        // Best-effort provenance backfill from the matching usage-log row, if one exists.
        var usageLog = await _db.StudentActivityUsageLogs
            .AsNoTracking()
            .Where(l => l.StudentProfileId == profile.Id && l.LearningActivityId == activity.Id)
            .OrderByDescending(l => l.ConsumedAtUtc)
            .FirstOrDefaultAsync(ct);

        var existing = command.ActivityAttemptId.HasValue
            ? await _db.ActivityFeedbackSignals.FirstOrDefaultAsync(
                s => s.StudentProfileId == profile.Id && s.ActivityAttemptId == command.ActivityAttemptId.Value, ct)
            : await _db.ActivityFeedbackSignals.FirstOrDefaultAsync(
                s => s.StudentProfileId == profile.Id
                  && s.LearningActivityId == activity.Id
                  && s.ActivityAttemptId == null, ct);

        if (existing is null)
        {
            var created = new ActivityFeedbackSignal(
                studentProfileId: profile.Id,
                learningActivityId: activity.Id,
                difficultyRating: command.DifficultyRating,
                clarityRating: command.ClarityRating,
                usefulnessRating: command.UsefulnessRating,
                repeatPreference: command.RepeatPreference,
                activityAttemptId: command.ActivityAttemptId,
                studentActivityUsageLogId: usageLog?.Id,
                studentActivityReadinessItemId: usageLog?.StudentActivityReadinessItemId,
                sourceTemplateId: usageLog?.SourceTemplateId,
                sourceBankItemId: usageLog?.SourceBankItemId,
                patternKey: usageLog?.PatternKey ?? activity.ExercisePatternKey,
                skill: usageLog?.Skill,
                subskill: usageLog?.Subskill,
                cefrLevel: usageLog?.CefrLevel,
                curriculumObjectiveKey: usageLog?.CurriculumObjectiveKey,
                optionalComment: command.OptionalComment);

            _db.ActivityFeedbackSignals.Add(created);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ActivityFeedbackSignal created Id={Id} ActivityId={ActivityId} AttemptId={AttemptId}",
                created.Id, activity.Id, command.ActivityAttemptId);

            return ToDto(created);
        }

        existing.UpdateRatings(
            difficultyRating: command.DifficultyRating,
            clarityRating: command.ClarityRating,
            usefulnessRating: command.UsefulnessRating,
            repeatPreference: command.RepeatPreference,
            optionalComment: command.OptionalComment,
            studentActivityUsageLogId: usageLog?.Id,
            studentActivityReadinessItemId: usageLog?.StudentActivityReadinessItemId,
            sourceTemplateId: usageLog?.SourceTemplateId,
            sourceBankItemId: usageLog?.SourceBankItemId,
            patternKey: usageLog?.PatternKey ?? activity.ExercisePatternKey,
            skill: usageLog?.Skill,
            subskill: usageLog?.Subskill,
            cefrLevel: usageLog?.CefrLevel,
            curriculumObjectiveKey: usageLog?.CurriculumObjectiveKey);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ActivityFeedbackSignal updated Id={Id} ActivityId={ActivityId} AttemptId={AttemptId}",
            existing.Id, activity.Id, command.ActivityAttemptId);

        return ToDto(existing);
    }

    private static ActivityFeedbackSignalDto ToDto(ActivityFeedbackSignal signal) => new(
        Id: signal.Id,
        LearningActivityId: signal.LearningActivityId,
        ActivityAttemptId: signal.ActivityAttemptId,
        DifficultyRating: signal.DifficultyRating,
        ClarityRating: signal.ClarityRating,
        UsefulnessRating: signal.UsefulnessRating,
        RepeatPreference: signal.RepeatPreference,
        OptionalComment: signal.OptionalComment,
        UpdatedAt: signal.UpdatedAt);
}
