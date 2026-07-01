using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ReadinessPool;

/// <summary>
/// Application-layer service for managing the student activity readiness pool lifecycle.
/// Reservation uses a transactional select-then-update with optimistic concurrency (xmin)
/// to prevent double-reservation under concurrent callers.
/// </summary>
public sealed class StudentActivityReadinessPoolService : IStudentActivityReadinessPoolService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<StudentActivityReadinessPoolService> _logger;

    public StudentActivityReadinessPoolService(
        LinguaCoachDbContext db,
        ILogger<StudentActivityReadinessPoolService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> CreateQueuedAsync(CreateReadinessItemRequest request, CancellationToken ct = default)
    {
        var item = new StudentActivityReadinessItem(
            studentId: request.StudentId,
            source: request.Source,
            targetCefrLevel: request.TargetCefrLevel,
            routingReason: request.RoutingReason,
            isLowerLevelContent: request.IsLowerLevelContent,
            curriculumObjectiveKey: request.CurriculumObjectiveKey,
            curriculumObjectiveTitle: request.CurriculumObjectiveTitle,
            primarySkill: request.PrimarySkill,
            secondarySkillsJson: request.SecondarySkillsJson,
            contextTagsJson: request.ContextTagsJson,
            focusTagsJson: request.FocusTagsJson,
            patternKey: request.PatternKey,
            activityType: request.ActivityType,
            difficultyBand: request.DifficultyBand,
            originalCefrLevelSnapshot: request.OriginalCefrLevelSnapshot,
            routingExplanation: request.RoutingExplanation,
            preferredSessionDurationMinutes: request.PreferredSessionDurationMinutes,
            difficultyPreference: request.DifficultyPreference,
            supportLanguageCode: request.SupportLanguageCode,
            supportLanguageName: request.SupportLanguageName,
            translationHelpPreference: request.TranslationHelpPreference,
            generatedBy: request.GeneratedBy,
            priority: request.Priority,
            expiresAt: request.ExpiresAt,
            requiresAdminReview: request.RequiresAdminReview);

        _db.StudentActivityReadinessItems.Add(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "ReadinessPool: created queued item {ItemId} for student {StudentId} source={Source} cefr={Cefr}",
            item.Id, request.StudentId, request.Source, request.TargetCefrLevel);

        return item.Id;
    }

    public async Task MarkGeneratingAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkGenerating();
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("ReadinessPool: {ItemId} → Generating (attempt {Attempt})", itemId, item.AttemptCount);
    }

    public async Task MarkReadyAsync(
        Guid itemId,
        Guid? learningSessionId = null,
        Guid? learningActivityId = null,
        Guid? sessionExerciseId = null,
        CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkReady(learningSessionId, learningActivityId, sessionExerciseId);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug(
            "ReadinessPool: {ItemId} → Ready activityId={ActivityId}",
            itemId, learningActivityId);
    }

    public async Task MarkFailedAsync(
        Guid itemId,
        string? errorCode,
        string? errorMessage,
        CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkFailed(errorCode, errorMessage);
        await _db.SaveChangesAsync(ct);
        _logger.LogWarning(
            "ReadinessPool: {ItemId} → Failed code={Code} msg={Msg}",
            itemId, errorCode, errorMessage);
    }

    public async Task<StudentActivityReadinessItem?> ReserveNextReadyAsync(
        Guid studentId,
        ReadinessPoolSource source,
        string? patternKey = null,
        string? primarySkill = null,
        CancellationToken ct = default)
    {
        // Use a retry loop to handle optimistic concurrency conflicts from concurrent callers.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var query = _db.StudentActivityReadinessItems
                .Where(i => i.StudentId == studentId
                         && i.Source == source
                         && i.Status == ReadinessPoolStatus.Ready);

            if (patternKey is not null)
                query = query.Where(i => i.PatternKey == patternKey);

            if (primarySkill is not null)
                query = query.Where(i => i.PrimarySkill == primarySkill);

            var item = await query
                .OrderBy(i => i.Priority)
                .ThenBy(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (item is null)
                return null;

            try
            {
                item.Reserve();
                await _db.SaveChangesAsync(ct);
                _logger.LogDebug("ReadinessPool: {ItemId} → Reserved for student {StudentId}", item.Id, studentId);
                return item;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another caller reserved this item. Detach and retry.
                _db.Entry(item).State = EntityState.Detached;
                _logger.LogDebug(
                    "ReadinessPool: reservation concurrency conflict on {ItemId}, attempt {Attempt}",
                    item.Id, attempt + 1);
            }
        }

        _logger.LogDebug("ReadinessPool: could not reserve item for student {StudentId} after retries.", studentId);
        return null;
    }

    public async Task MarkConsumedAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkConsumed();
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("ReadinessPool: {ItemId} → Consumed", itemId);
    }

    public async Task ExpireAsync(Guid itemId, string? reason = null, CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.Expire(reason);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("ReadinessPool: {ItemId} → Expired reason={Reason}", itemId, reason);
    }

    public async Task MarkStaleAsync(Guid itemId, string? reason = null, CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkStale(reason);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("ReadinessPool: {ItemId} → Stale reason={Reason}", itemId, reason);
    }

    public async Task MarkReviewOnlyAsync(Guid itemId, string? reason = null, CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkReviewOnly(reason);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("ReadinessPool: {ItemId} → ReviewOnly reason={Reason}", itemId, reason);
    }

    public async Task MarkSkippedAsync(Guid itemId, string? reason = null, CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.MarkSkipped(reason);
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("ReadinessPool: {ItemId} → Skipped reason={Reason}", itemId, reason);
    }

    public async Task LinkMaterializedIdsAsync(
        Guid itemId,
        Guid? learningSessionId,
        Guid? learningActivityId,
        Guid? sessionExerciseId,
        CancellationToken ct = default)
    {
        var item = await RequireItemAsync(itemId, ct);
        item.LinkMaterializedIds(learningSessionId, learningActivityId, sessionExerciseId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StudentActivityReadinessItem>> GetReadyForStudentAsync(
        Guid studentId,
        ReadinessPoolSource? source = null,
        CancellationToken ct = default)
    {
        var query = _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == studentId && i.Status == ReadinessPoolStatus.Ready);

        if (source.HasValue)
            query = query.Where(i => i.Source == source.Value);

        return await query
            .OrderBy(i => i.Priority)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ReadinessPoolSummary> GetPoolSummaryAsync(Guid studentId, CancellationToken ct = default)
    {
        var items = await _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == studentId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

        var dtos = items.Select(i => new ReadinessItemDto
        {
            Id = i.Id,
            Source = i.Source.ToString(),
            Status = i.Status.ToString(),
            TargetCefrLevel = i.TargetCefrLevel,
            CurriculumObjectiveKey = i.CurriculumObjectiveKey,
            CurriculumObjectiveTitle = i.CurriculumObjectiveTitle,
            PrimarySkill = i.PrimarySkill,
            RoutingReason = i.RoutingReason.ToString(),
            IsLowerLevelContent = i.IsLowerLevelContent,
            PatternKey = i.PatternKey,
            ActivityType = i.ActivityType,
            DifficultyBand = i.DifficultyBand,
            LearningSessionId = i.LearningSessionId,
            LearningActivityId = i.LearningActivityId,
            AttemptCount = i.AttemptCount,
            ErrorCode = i.ErrorCode,
            ReservedAt = i.ReservedAt,
            ConsumedAt = i.ConsumedAt,
            ExpiresAt = i.ExpiresAt,
            LastEvaluatedAtUtc = i.LastEvaluatedAtUtc,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        }).ToList();

        return new ReadinessPoolSummary
        {
            StudentId = studentId,
            QueuedCount = items.Count(i => i.Status == ReadinessPoolStatus.Queued),
            GeneratingCount = items.Count(i => i.Status == ReadinessPoolStatus.Generating),
            ReadyCount = items.Count(i => i.Status == ReadinessPoolStatus.Ready),
            ReservedCount = items.Count(i => i.Status == ReadinessPoolStatus.Reserved),
            ConsumedCount = items.Count(i => i.Status == ReadinessPoolStatus.Consumed),
            ExpiredCount = items.Count(i => i.Status == ReadinessPoolStatus.Expired),
            FailedCount = items.Count(i => i.Status == ReadinessPoolStatus.Failed),
            StaleCount = items.Count(i => i.Status == ReadinessPoolStatus.Stale),
            SkippedCount = items.Count(i => i.Status == ReadinessPoolStatus.Skipped),
            ReviewOnlyCount = items.Count(i => i.Status == ReadinessPoolStatus.ReviewOnly),
            Items = dtos
        };
    }

    private async Task<StudentActivityReadinessItem> RequireItemAsync(Guid itemId, CancellationToken ct)
    {
        var item = await _db.StudentActivityReadinessItems.FindAsync([itemId], ct);
        if (item is null)
            throw new InvalidOperationException($"StudentActivityReadinessItem {itemId} not found.");
        return item;
    }
}
