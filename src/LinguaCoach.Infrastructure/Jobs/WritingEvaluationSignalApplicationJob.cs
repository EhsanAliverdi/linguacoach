using LinguaCoach.Application.Writing;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Applies config-gated writing evaluation signals to student learning state.
/// Runs every 10 minutes. Processes only Completed evaluations with no existing applied signal.
/// No-op when ApplyMasterySignals=false (safe default).
/// </summary>
[DisallowConcurrentExecution]
public sealed class WritingEvaluationSignalApplicationJob : IJob
{
    public const string JobName = "writing-signal-application";

    private readonly IWritingEvaluationSignalApplicationService _service;
    private readonly ILogger<WritingEvaluationSignalApplicationJob> _logger;

    public WritingEvaluationSignalApplicationJob(
        IWritingEvaluationSignalApplicationService service,
        ILogger<WritingEvaluationSignalApplicationJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("WritingEvaluationSignalApplicationJob started.");
        var result = await _service.ApplyPendingSignalsAsync(maxBatch: 20, ct);
        _logger.LogInformation(
            "WritingEvaluationSignalApplicationJob completed Processed={Processed} Applied={Applied} " +
            "BlockedByConfig={Config} BlockedByConfidence={Confidence} DuplicateSkipped={Dup} Failed={Failed}.",
            result.Processed, result.Applied, result.BlockedByConfig,
            result.BlockedByConfidence, result.DuplicateSkipped, result.Failed);
    }
}
