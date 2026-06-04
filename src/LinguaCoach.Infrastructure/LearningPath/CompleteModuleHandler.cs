using LinguaCoach.Application.LearningPath;
using LinguaCoach.Infrastructure.Progress;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.LearningPath;

public sealed class CompleteModuleHandler : ICompleteModuleHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly StudentProgressService _progress;

    public CompleteModuleHandler(LinguaCoachDbContext db, StudentProgressService progress)
    {
        _db = db;
        _progress = progress;
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

        if (module is null)
            throw new KeyNotFoundException($"Module {command.ModuleId} not found.");

        var pathBelongsToStudent = await _db.LearningPaths
            .AnyAsync(p => p.Id == module.LearningPathId
                        && p.StudentProfileId == profile.Id
                        && p.IsActive, ct);

        if (!pathBelongsToStudent)
            throw new UnauthorizedAccessException("Module does not belong to the student's active path.");

        if (module.IsCompleted)
            return; // idempotent — already completed

        // Check readiness before allowing explicit completion
        var moduleIds = new List<Guid> { module.Id };
        var progressData = await _progress.GetModuleProgressAsync(profile.Id, moduleIds, ct);
        var pd = progressData.GetValueOrDefault(module.Id);

        if (pd is null || !pd.IsReadyToComplete)
            throw new InvalidOperationException(
                "Module is not ready to complete. Complete at least 3 different activities with an average score of 75 or above.");

        module.MarkCompleted();
        await _db.SaveChangesAsync(ct);
    }
}
