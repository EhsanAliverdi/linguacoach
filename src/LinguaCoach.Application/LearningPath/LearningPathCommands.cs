namespace LinguaCoach.Application.LearningPath;

// ── Complete module ───────────────────────────────────────────────────────────

public sealed record CompleteModuleCommand(Guid UserId, Guid ModuleId);

public interface ICompleteModuleHandler
{
    Task HandleAsync(CompleteModuleCommand command, CancellationToken ct = default);
}

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

public sealed record GenerateNextModulesCommand(Guid UserId, Guid? PathId = null);

public interface IAdaptivePathGenerator
{
    Task<LearningPathDto> GenerateNextAsync(GenerateNextModulesCommand command, CancellationToken ct = default);
}

public interface IStudentMemoryQuery
{
    Task<StudentLearningMemoryDto> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<StudentLearningMemoryDto> GetForStudentProfileAsync(Guid studentProfileId, CancellationToken ct = default);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>The student's current weakness focus area, derived from recent feedback.</summary>
public sealed record LearningFocusAreaDto(
    string Category,           // e.g. "tone"
    string FriendlyLabel,      // e.g. "Polite workplace tone"
    int Frequency);            // how many times this category appeared in last 5 attempts

public sealed record LearningModuleDto(
    Guid ModuleId,
    string Title,
    string Description,
    int Order,
    int CompletedActivities,   // distinct LearningActivity IDs attempted (not retry count)
    int TotalActivities,       // CompletionThreshold (3)
    bool IsCurrent,
    bool IsCompleted,          // CompletedAt is set on the module
    bool IsReadyToComplete,    // distinctCompleted >= 3 AND averageScore >= 75
    double? AverageScore,      // average score across all attempts in this module
    double? LatestScore,       // most recent attempt score in this module
    string? FocusSkill = null,
    string? Reason = null,
    string? Difficulty = null);

public sealed record LearningPathDto(
    Guid PathId,
    string Title,
    bool IsActive,
    LearningModuleDto? CurrentModule,
    int ModulesCompleted,
    int TotalModules,
    IReadOnlyList<LearningModuleDto> Modules,
    LearningFocusAreaDto? CurrentFocus = null);

public sealed record StudentSkillProfileDto(
    string SkillKey,
    string SkillLabel,
    bool IsWeak);

public sealed record StudentLearningMemoryDto(
    string? JourneySummary,
    IReadOnlyList<string> StrongSkills,
    IReadOnlyList<string> WeakSkills,
    IReadOnlyList<string> RecurringMistakes,
    IReadOnlyList<string> NextRecommendedFocus,
    int CoveredScenarioCount,
    IReadOnlyList<StudentSkillProfileDto> SkillProfile);
