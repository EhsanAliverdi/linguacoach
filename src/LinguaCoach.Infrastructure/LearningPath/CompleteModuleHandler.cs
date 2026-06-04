using LinguaCoach.Application.LearningPath;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.LearningPath;

public sealed class CompleteModuleHandler : ICompleteModuleHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly StudentProgressService _progress;
    private readonly ILogger<CompleteModuleHandler> _logger;

    public CompleteModuleHandler(
        LinguaCoachDbContext db,
        StudentProgressService progress,
        ILogger<CompleteModuleHandler> logger)
    {
        _db = db;
        _progress = progress;
        _logger = logger;
    }

    public async Task HandleAsync(CompleteModuleCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        // Verify the module belongs to the student's active path
        var module = await _db.LearningModules
            .Include(m => m.Activities)
            .Where(m => m.Id == command.ModuleId)
            .FirstOrDefaultAsync(ct);

        _logger.LogInformation("Complete module requested ModuleId={ModuleId} UserId={UserId}",
            command.ModuleId, command.UserId);

        if (module is null)
        {
            _logger.LogWarning("Complete module rejected — module not found ModuleId={ModuleId}", command.ModuleId);
            throw new KeyNotFoundException($"Module {command.ModuleId} not found.");
        }

        var pathBelongsToStudent = await _db.LearningPaths
            .AnyAsync(p => p.Id == module.LearningPathId
                        && p.StudentProfileId == profile.Id
                        && p.IsActive, ct);

        if (!pathBelongsToStudent)
        {
            _logger.LogWarning("Complete module rejected — module not in student path ModuleId={ModuleId} UserId={UserId}",
                command.ModuleId, command.UserId);
            throw new UnauthorizedAccessException("Module does not belong to the student's active path.");
        }

        if (module.IsCompleted)
        {
            _logger.LogInformation("Complete module — already completed ModuleId={ModuleId}", command.ModuleId);
            return;
        }

        // Check readiness before allowing explicit completion
        var moduleIds = new List<Guid> { module.Id };
        var progressData = await _progress.GetModuleProgressAsync(profile.Id, moduleIds, ct);
        var pd = progressData.GetValueOrDefault(module.Id);

        if (pd is null || !pd.IsReadyToComplete)
        {
            _logger.LogWarning(
                "Complete module rejected — not ready ModuleId={ModuleId} DistinctCompleted={Completed} AvgScore={Score}",
                command.ModuleId, pd?.DistinctCompleted ?? 0, pd?.AverageScore ?? 0);
            throw new InvalidOperationException(
                "Module is not ready to complete. Complete at least 3 different activities with an average score of 75 or above.");
        }

        module.MarkCompleted();
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Module completed successfully ModuleId={ModuleId} UserId={UserId}",
            command.ModuleId, command.UserId);
    }
}
