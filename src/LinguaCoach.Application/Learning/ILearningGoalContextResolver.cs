using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.Learning;

/// <summary>
/// Resolves a student's learning goal context from their profile using a consistent priority chain.
/// Priority: ExplicitOverride > LearningGoals+FocusAreas > CustomLearningGoal/CustomFocusArea
///           > LearningGoalDescription > LearningGoal > CareerContext > generic fallback.
/// </summary>
public interface ILearningGoalContextResolver
{
    /// <summary>
    /// Resolves the learning goal context for the given student profile.
    /// Never throws; returns a non-null result with at minimum the generic fallback.
    /// </summary>
    ResolvedLearningGoalContext Resolve(StudentProfile profile, LearningGoalResolutionContext? context = null);
}

/// <summary>
/// Optional per-call context that can influence resolution (e.g. an explicit override for a specific exercise).
/// </summary>
public sealed class LearningGoalResolutionContext
{
    /// <summary>Caller name for tracing (e.g. "ActivityGetHandler", "PracticeGymGenerationJob").</summary>
    public string? Source { get; init; }

    public string? RequestedSkill { get; init; }
    public string? RequestedExerciseType { get; init; }
    public string? SessionTopic { get; init; }

    /// <summary>When set, overrides all profile-derived goal resolution.</summary>
    public string? ExplicitGoalOverride { get; init; }
}
