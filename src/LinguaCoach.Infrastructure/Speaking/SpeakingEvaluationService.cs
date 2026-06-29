using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Speaking;

public sealed class SpeakingEvaluationService : ISpeakingEvaluationService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ISpeakingEvaluationProvider _provider;
    private readonly SpeakingEvaluationOptions _options;
    private readonly ILogger<SpeakingEvaluationService> _logger;

    public SpeakingEvaluationService(
        LinguaCoachDbContext db,
        ISpeakingEvaluationProvider provider,
        IOptions<SpeakingEvaluationOptions> options,
        ILogger<SpeakingEvaluationService> logger)
    {
        _db = db;
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RequestEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        Guid activityId,
        CancellationToken ct = default)
    {
        try
        {
            var existing = await _db.SpeakingEvaluations
                .AnyAsync(e => e.ActivityAttemptId == attemptId, ct);
            if (existing) return;

            var evaluation = SpeakingEvaluation.CreatePending(attemptId, studentProfileId, activityId);
            _db.SpeakingEvaluations.Add(evaluation);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "SpeakingEvaluation created AttemptId={AttemptId} EvaluationId={EvaluationId}",
                attemptId, evaluation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SpeakingEvaluation request failed (non-fatal) AttemptId={AttemptId}. " +
                "Audio submission is not affected.", attemptId);
        }
    }

    public async Task<SpeakingEvaluationDto?> GetEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var evaluation = await _db.SpeakingEvaluations
            .FirstOrDefaultAsync(
                e => e.ActivityAttemptId == attemptId && e.StudentProfileId == studentProfileId, ct);
        return evaluation is null ? null : ToDto(evaluation);
    }

    public async Task<int> ProcessPendingAsync(int maxBatch, CancellationToken ct = default)
    {
        var pending = await _db.SpeakingEvaluations
            .Where(e => e.Status == SpeakingEvaluationStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(maxBatch)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        var processed = 0;
        foreach (var evaluation in pending)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await ProcessSingleAsync(evaluation, ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SpeakingEvaluationJob: unexpected error processing EvaluationId={Id}", evaluation.Id);
            }
        }
        return processed;
    }

    private async Task ProcessSingleAsync(SpeakingEvaluation evaluation, CancellationToken ct)
    {
        if (!_options.Enabled || !_provider.IsSupported)
        {
            evaluation.MarkNotSupported();
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "SpeakingEvaluation marked NotSupported EvaluationId={Id} Provider={Provider} Enabled={Enabled}",
                evaluation.Id, _provider.ProviderName, _options.Enabled);
            return;
        }

        var attempt = await _db.ActivityAttempts
            .FirstOrDefaultAsync(a => a.Id == evaluation.ActivityAttemptId, ct);
        if (attempt is null)
        {
            evaluation.MarkFailed("Attempt record not found.");
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (evaluation.RetryCount >= _options.MaxRetries)
        {
            evaluation.MarkFailed("Max retries exceeded.");
            await _db.SaveChangesAsync(ct);
            return;
        }

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == evaluation.LearningActivityId, ct);

        evaluation.MarkEvaluating(_provider.ProviderName, null);
        await _db.SaveChangesAsync(ct);

        var request = new SpeakingEvaluationRequest(
            AttemptId: attempt.Id,
            StudentProfileId: evaluation.StudentProfileId,
            ActivityId: evaluation.LearningActivityId,
            AudioStorageKey: attempt.AudioStorageKey,
            ActivityPrompt: activity?.AiGeneratedContentJson,
            ActivityTitle: activity?.Title,
            CefrLevel: null,
            CorrelationId: evaluation.Id.ToString("N"));

        SpeakingEvaluationProviderResult result;
        try
        {
            result = await _provider.EvaluateAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SpeakingEvaluationProvider threw EvaluationId={Id} Provider={Provider}",
                evaluation.Id, _provider.ProviderName);
            evaluation.MarkFailed($"Provider error: {ex.GetType().Name}");
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (result.Success)
        {
            evaluation.MarkCompleted(
                result.Transcript,
                result.OverallScore,
                result.FluencyScore,
                result.PronunciationScore,
                result.CompletenessScore,
                result.RelevanceScore,
                result.FeedbackText,
                result.SuggestedImprovement);
        }
        else
        {
            evaluation.MarkFailed(result.FailureReason ?? "Provider returned failure.");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SpeakingEvaluation processed EvaluationId={Id} Status={Status}",
            evaluation.Id, evaluation.Status);
    }

    private static SpeakingEvaluationDto ToDto(SpeakingEvaluation e) => new(
        AttemptId: e.ActivityAttemptId,
        Status: e.Status.ToString(),
        FeedbackText: e.FeedbackText,
        SuggestedImprovement: e.SuggestedImprovement,
        Transcript: e.Transcript,
        OverallScore: e.OverallScore,
        FluencyScore: e.FluencyScore,
        PronunciationScore: e.PronunciationScore,
        CompletenessScore: e.CompletenessScore,
        RelevanceScore: e.RelevanceScore,
        CompletedAtUtc: e.CompletedAtUtc,
        FailureReason: e.Status == SpeakingEvaluationStatus.Failed ? e.FailureReason : null,
        ProviderName: e.ProviderName,
        ModelName: e.ModelName);
}
