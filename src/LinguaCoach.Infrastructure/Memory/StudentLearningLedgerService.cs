using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Memory;

public sealed class StudentLearningLedgerService : IStudentLearningLedger
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<StudentLearningLedgerService> _logger;

    public StudentLearningLedgerService(
        LinguaCoachDbContext db,
        ILogger<StudentLearningLedgerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordAsync(StudentLearningEvent learningEvent, CancellationToken ct = default)
    {
        try
        {
            _db.StudentLearningEvents.Add(learningEvent);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LearningLedger: failed to record event StudentProfileId={StudentProfileId} PatternKey={PatternKey} Source={Source}",
                learningEvent.StudentProfileId, learningEvent.PatternKey, learningEvent.Source);
        }
    }

    public async Task<IReadOnlyList<StudentLearningEvent>> GetRecentAsync(
        Guid studentProfileId,
        int limit = 50,
        CancellationToken ct = default)
    {
        return await _db.StudentLearningEvents
            .Where(e => e.StudentProfileId == studentProfileId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StudentLearningEvent>> GetRecentByPatternKeysAsync(
        Guid studentProfileId,
        IEnumerable<string> patternKeys,
        int limit = 20,
        CancellationToken ct = default)
    {
        var keys = patternKeys.ToList();
        return await _db.StudentLearningEvents
            .Where(e => e.StudentProfileId == studentProfileId && e.PatternKey != null && keys.Contains(e.PatternKey))
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetRecentPatternKeysAsync(
        Guid studentProfileId,
        int limit = 20,
        CancellationToken ct = default)
    {
        var recent = await _db.StudentLearningEvents
            .Where(e => e.StudentProfileId == studentProfileId && e.PatternKey != null)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => e.PatternKey!)
            .Take(limit * 3)
            .ToListAsync(ct);

        return recent.Distinct().Take(limit).ToList();
    }

    public async Task<IReadOnlyList<StudentLearningEvent>> GetWeakEventsAsync(
        Guid studentProfileId,
        int limit = 20,
        CancellationToken ct = default)
    {
        return await _db.StudentLearningEvents
            .Where(e => e.StudentProfileId == studentProfileId
                && (e.Outcome == LearningEventOutcome.NeedsReview || e.Outcome == LearningEventOutcome.Failed))
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }
}
