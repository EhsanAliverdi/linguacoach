namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Classifies the teaching role of a SessionExercise step within a lesson.
/// Used by the session generator to select the right exercise pattern key
/// and map steps to ActivityType when an activity must be generated.
///
/// This is a template-slot concept — distinct from ExercisePatternKey (which is
/// the specific named pattern) and ActivityType (which is the implementation type).
/// </summary>
public enum ExerciseKind
{
    VocabularyWarmup = 0,
    ContextInput = 1,
    ListeningInput = 2,
    ReadingInput = 3,
    WritingTask = 4,
    SpeakingTask = 5,
    Review = 6
}
