using LinguaCoach.Application.Writing;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Processes pending writing evaluations on a scheduled interval.
/// Picks up WritingEvaluation records in Pending status and invokes IWritingEvaluationService.
/// With the current NoOp provider, all pending evaluations resolve to NotSupported immediately.
/// </summary>
[DisallowConcurrentExecution]
public sealed class WritingEvaluationJob : IJob
{
    public const string JobName = "writing-evaluation";

    private readonly IWritingEvaluationService _service;
    private readonly ILogger<WritingEvaluationJob> _logger;

    public WritingEvaluationJob(
        IWritingEvaluationService service,
        ILogger<WritingEvaluationJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("WritingEvaluationJob started.");
        var processed = await _service.ProcessPendingAsync(maxBatch: 10, ct);
        _logger.LogInformation("WritingEvaluationJob completed Processed={Count}.", processed);
    }
}
