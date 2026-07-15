using LinguaCoach.Application.ResourceImport;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Phase 4 (2026-07-15), Part 8 — advances approved Import Packages through extraction and
/// candidate creation on a scheduled interval. Only ever touches packages with an approved plan
/// (see <see cref="IImportPackageProcessingService"/>) — never starts work on an unapproved one.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ImportPackageProcessingJob : IJob
{
    public const string JobName = "import-package-processing";

    private readonly IImportPackageProcessingService _service;
    private readonly ILogger<ImportPackageProcessingJob> _logger;

    public ImportPackageProcessingJob(IImportPackageProcessingService service, ILogger<ImportPackageProcessingJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("ImportPackageProcessingJob started.");
        var outcomes = await _service.ProcessPendingAsync(maxPackages: 3, ct);
        var completed = outcomes.Count(o => o.Completed);
        var paused = outcomes.Count(o => o.PausedForCostApproval);
        _logger.LogInformation(
            "ImportPackageProcessingJob completed. Packages={Total} Completed={Completed} PausedForCostApproval={Paused}.",
            outcomes.Count, completed, paused);
    }
}
