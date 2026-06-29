using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Applies config-gated speaking evaluation signals to student learning state.
/// Runs every 10 minutes. Processes only Completed evaluations with no existing applied signal.
/// No-op when ApplyMasterySignals=false (safe default).
/// </summary>
[DisallowConcurrentExecution]
public sealed class SpeakingEvaluationSignalApplicationJob : IJob
{
    public const string JobName = "speaking-signal-application";

    private readonly ISpeakingEvaluationSignalApplicationService _service;
    private readonly ILogger<SpeakingEvaluationSignalApplicationJob> _logger;

    public SpeakingEvaluationSignalApplicationJob(
        ISpeakingEvaluationSignalApplicationService service,
        ILogger<SpeakingEvaluationSignalApplicationJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("SpeakingEvaluationSignalApplicationJob started.");
        var result = await _service.ApplyPendingSignalsAsync(maxBatch: 20, ct);
        _logger.LogInformation(
            "SpeakingEvaluationSignalApplicationJob completed Processed={Processed} Applied={Applied} " +
            "BlockedByConfig={Config} BlockedByConfidence={Confidence} DuplicateSkipped={Dup} Failed={Failed}.",
            result.Processed, result.Applied, result.BlockedByConfig,
            result.BlockedByConfidence, result.DuplicateSkipped, result.Failed);
    }
}
