using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Processes pending speaking evaluations on a scheduled interval.
/// Picks up SpeakingEvaluation records in Pending status and invokes ISpeakingEvaluationService.
/// With the current NoOp provider, all pending evaluations resolve to NotSupported immediately.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SpeakingEvaluationJob : IJob
{
    public const string JobName = "speaking-evaluation";

    private readonly ISpeakingEvaluationService _service;
    private readonly ILogger<SpeakingEvaluationJob> _logger;

    public SpeakingEvaluationJob(
        ISpeakingEvaluationService service,
        ILogger<SpeakingEvaluationJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("SpeakingEvaluationJob started.");
        var processed = await _service.ProcessPendingAsync(maxBatch: 10, ct);
        _logger.LogInformation("SpeakingEvaluationJob completed Processed={Count}.", processed);
    }
}
