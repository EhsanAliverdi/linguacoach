namespace LinguaCoach.Application.LearningPath;

// ── Generate learning path ────────────────────────────────────────────────────

public sealed record GenerateLearningPathCommand(Guid UserId);

public interface ILearningPathGenerator
{
    /// <summary>
    /// Generates (or retrieves the existing) active LearningPath for the student.
    /// Never throws — falls back to DefaultPathFactory on AI failure.
    /// </summary>
    Task<LearningPathDto> GenerateAsync(GenerateLearningPathCommand command, CancellationToken ct = default);
}

// ── Query learning path ───────────────────────────────────────────────────────

public sealed record GetLearningPathQuery(Guid UserId);

public interface IGetLearningPathHandler
{
    /// <summary>Returns the student's active LearningPath with per-module progress counts.</summary>
    Task<LearningPathDto?> HandleAsync(GetLearningPathQuery query, CancellationToken ct = default);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record LearningPathDto(
    Guid PathId,
    string Title,
    bool IsActive,
    LearningModuleDto? CurrentModule,
    int ModulesCompleted,
    int TotalModules,
    IReadOnlyList<LearningModuleDto> Modules);

public sealed record LearningModuleDto(
    Guid ModuleId,
    string Title,
    string Description,
    int Order,
    int CompletedActivities,
    int TotalActivities,
    bool IsCurrent);
