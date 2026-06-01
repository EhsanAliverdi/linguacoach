namespace LinguaCoach.Application.LearningPlan;

/// <summary>
/// Queries the student's current state and selects the vocabulary and lesson context
/// for the next AI lesson call. SQL/system-driven — AI does not choose vocabulary.
/// Not implemented in MVP skeleton.
/// </summary>
public interface ILearningPlanner
{
    /// <summary>
    /// Builds the next lesson plan for the student: selects new, review, and
    /// reinforcement vocabulary; determines lesson type and scenario template.
    /// </summary>
    Task<LessonPlan> BuildLessonPlanAsync(Guid studentProfileId, CancellationToken ct = default);
}

/// <summary>Value object produced by LearningPlanner and consumed by AiContextBuilder.</summary>
public sealed record LessonPlan(
    Guid StudentProfileId,
    string LanguagePairCode,
    string CefrLevel,
    string CareerContext,
    LessonType LessonType,
    IReadOnlyList<VocabItem> TargetVocabulary,
    IReadOnlyList<VocabItem> ReviewVocabulary,
    IReadOnlyList<VocabItem> ReinforcementVocabulary,
    string ScenarioTemplate,
    string WeaknessSummary,
    string RecentLessonSummary);

public sealed record VocabItem(string Word, string Definition, string? ExampleSentence = null, string? WeaknessNote = null);

public enum LessonType
{
    Writing,
    Vocabulary,
    Speaking
}
