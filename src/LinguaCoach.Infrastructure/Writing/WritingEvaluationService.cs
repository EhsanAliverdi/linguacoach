using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Writing;

public sealed class WritingEvaluationService : IWritingEvaluationService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IWritingEvaluationProvider _provider;
    private readonly WritingEvaluationOptions _options;
    private readonly ILogger<WritingEvaluationService> _logger;

    public WritingEvaluationService(
        LinguaCoachDbContext db,
        IWritingEvaluationProvider provider,
        IOptions<WritingEvaluationOptions> options,
        ILogger<WritingEvaluationService> logger)
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
            // Phase 17A — only track evaluations when the feature is enabled.
            if (!_options.Enabled) return;

            var existing = await _db.WritingEvaluations
                .AnyAsync(e => e.ActivityAttemptId == attemptId, ct);
            if (existing) return;

            var evaluation = WritingEvaluation.CreatePending(attemptId, studentProfileId, activityId);
            _db.WritingEvaluations.Add(evaluation);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "WritingEvaluation created AttemptId={AttemptId} EvaluationId={EvaluationId}",
                attemptId, evaluation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WritingEvaluation request failed (non-fatal) AttemptId={AttemptId}. " +
                "Writing submission is not affected.", attemptId);
        }
    }

    public async Task<WritingEvaluationDto?> GetEvaluationAsync(
        Guid attemptId,
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var evaluation = await _db.WritingEvaluations
            .FirstOrDefaultAsync(
                e => e.ActivityAttemptId == attemptId && e.StudentProfileId == studentProfileId, ct);
        return evaluation is null ? null : ToDto(evaluation);
    }

    public async Task<int> ProcessPendingAsync(int maxBatch, CancellationToken ct = default)
    {
        // Include Failed evaluations that have not yet hit MaxRetries so the job retries them.
        var pending = await _db.WritingEvaluations
            .Where(e => e.Status == WritingEvaluationStatus.Pending
                     || (e.Status == WritingEvaluationStatus.Failed && e.RetryCount < _options.MaxRetries))
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
                    "WritingEvaluationJob: unexpected error processing EvaluationId={Id}", evaluation.Id);
            }
        }
        return processed;
    }

    private async Task ProcessSingleAsync(WritingEvaluation evaluation, CancellationToken ct)
    {
        if (!_options.Enabled || !_provider.IsSupported)
        {
            evaluation.MarkNotSupported();
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "WritingEvaluation marked NotSupported EvaluationId={Id} Provider={Provider} Enabled={Enabled}",
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

        var cefrLevel = await _db.StudentProfiles
            .Where(p => p.Id == evaluation.StudentProfileId)
            .Select(p => p.CefrLevel)
            .FirstOrDefaultAsync(ct);

        evaluation.MarkEvaluating(_provider.ProviderName, null);
        await _db.SaveChangesAsync(ct);

        var request = new WritingEvaluationRequest(
            AttemptId: attempt.Id,
            StudentProfileId: evaluation.StudentProfileId,
            ActivityId: evaluation.LearningActivityId,
            WrittenText: attempt.SubmittedContent,
            ActivityPrompt: activity?.AiGeneratedContentJson,
            ActivityTitle: activity?.Title,
            CefrLevel: cefrLevel,
            PatternKey: activity?.ExercisePatternKey,
            CorrelationId: evaluation.Id.ToString("N"));

        WritingEvaluationProviderResult result;
        try
        {
            result = await _provider.EvaluateAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WritingEvaluationProvider threw EvaluationId={Id} Provider={Provider}",
                evaluation.Id, _provider.ProviderName);
            evaluation.MarkFailed($"Provider error: {ex.GetType().Name}");
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (result.Success)
        {
            evaluation.MarkCompleted(
                result.OverallScore,
                result.GrammarScore,
                result.VocabularyScore,
                result.CoherenceScore,
                result.TaskCompletionScore,
                result.FeedbackText,
                result.SuggestedImprovement,
                result.CorrectedText);
        }
        else
        {
            evaluation.MarkFailed(result.FailureReason ?? "Provider returned failure.");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WritingEvaluation processed EvaluationId={Id} Status={Status}",
            evaluation.Id, evaluation.Status);
    }

    private static WritingEvaluationDto ToDto(WritingEvaluation e) => new(
        AttemptId: e.ActivityAttemptId,
        Status: e.Status.ToString(),
        FeedbackText: e.FeedbackText,
        SuggestedImprovement: e.SuggestedImprovement,
        CorrectedText: e.CorrectedText,
        OverallScore: e.OverallScore,
        GrammarScore: e.GrammarScore,
        VocabularyScore: e.VocabularyScore,
        CoherenceScore: e.CoherenceScore,
        TaskCompletionScore: e.TaskCompletionScore,
        CompletedAtUtc: e.CompletedAtUtc,
        FailureReason: e.Status == WritingEvaluationStatus.Failed ? e.FailureReason : null,
        ProviderName: e.ProviderName,
        ModelName: e.ModelName);
}
