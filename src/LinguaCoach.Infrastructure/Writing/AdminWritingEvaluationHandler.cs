using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Writing;

/// <summary>
/// Admin read-only query handler for a student's writing evaluations.
/// Joins evaluation records with attempt and activity context.
/// Never exposes raw provider payloads or submitted text beyond the corrected version.
/// </summary>
public sealed class AdminWritingEvaluationHandler : IAdminWritingEvaluationQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminWritingEvaluationHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<AdminWritingEvaluationItemDto>> GetForStudentAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var query =
            from e in _db.WritingEvaluations.AsNoTracking()
            where e.StudentProfileId == studentProfileId
            join a in _db.ActivityAttempts.AsNoTracking()
                on e.ActivityAttemptId equals a.Id into attempts
            from a in attempts.DefaultIfEmpty()
            join act in _db.LearningActivities.AsNoTracking()
                on e.LearningActivityId equals act.Id into activities
            from act in activities.DefaultIfEmpty()
            orderby e.CreatedAt descending
            select new
            {
                e.Id,
                e.ActivityAttemptId,
                e.LearningActivityId,
                ActivityTitle = act != null ? act.Title : null,
                ActivityType = act != null ? (ActivityType?)act.ActivityType : null,
                e.Status,
                e.ProviderName,
                e.ModelName,
                SubmittedAtUtc = (DateTime?)(a != null ? a.CreatedAt : (DateTime?)null),
                e.CompletedAtUtc,
                e.OverallScore,
                e.GrammarScore,
                e.VocabularyScore,
                e.CoherenceScore,
                e.TaskCompletionScore,
                e.FeedbackText,
                e.SuggestedImprovement,
                e.CorrectedText,
                e.FailureReason,
            };

        var rows = await query.ToListAsync(ct);

        return rows.Select(r => new AdminWritingEvaluationItemDto(
            EvaluationId: r.Id,
            AttemptId: r.ActivityAttemptId,
            ActivityId: r.LearningActivityId,
            ActivityTitle: r.ActivityTitle,
            ActivityType: r.ActivityType?.ToString(),
            Status: r.Status.ToString(),
            ProviderName: r.ProviderName,
            ModelName: r.ModelName,
            SubmittedAtUtc: r.SubmittedAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            OverallScore: r.OverallScore,
            GrammarScore: r.GrammarScore,
            VocabularyScore: r.VocabularyScore,
            CoherenceScore: r.CoherenceScore,
            TaskCompletionScore: r.TaskCompletionScore,
            FeedbackText: r.FeedbackText,
            SuggestedImprovement: r.SuggestedImprovement,
            CorrectedText: r.CorrectedText,
            FailureReason: r.Status == WritingEvaluationStatus.Failed ? r.FailureReason : null))
            .ToList();
    }
}
