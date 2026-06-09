using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Defines the canonical exercise step sequences for each supported session duration.
/// The session generator selects the template whose duration bucket matches the
/// student's PreferredSessionDurationMinutes, then substitutes weak-skill preferences
/// and domain complexity constraints into each step.
///
/// Templates are deterministic backend rules — AI is never involved in step selection.
/// </summary>
public static class SessionDurationTemplates
{
    /// <summary>Default duration when no preference is recorded on the student profile.</summary>
    public const int DefaultDurationMinutes = 15;

    public static IReadOnlyList<ExerciseStepTemplate> GetTemplate(int durationMinutes)
        => durationMinutes switch
        {
            <= 10 => Template10Min,
            <= 15 => Template15Min,
            <= 20 => Template20Min,
            _ => Template30Min
        };

    /// <summary>
    /// Normalize any arbitrary duration to the nearest supported bucket.
    /// Durations over 30 are clamped to 30.
    /// </summary>
    public static int NormalizeDuration(int? preferredMinutes)
    {
        if (preferredMinutes is null or <= 0)
            return DefaultDurationMinutes;

        return preferredMinutes.Value switch
        {
            <= 10 => 10,
            <= 15 => 15,
            <= 20 => 20,
            _ => 30
        };
    }

    // ── 10-minute template ────────────────────────────────────────────────────
    // 3 steps: warmup + main task + review
    private static readonly IReadOnlyList<ExerciseStepTemplate> Template10Min =
    [
        new(Order: 0, Kind: ExerciseKind.VocabularyWarmup,
            PatternKey: "phrase_match",
            PrimarySkill: "Vocabulary",
            Instructions: "Match these workplace phrases to their meanings.",
            EstimatedMinutes: 3),

        new(Order: 1, Kind: ExerciseKind.WritingTask,
            PatternKey: "writing_response",
            PrimarySkill: "Writing",
            Instructions: "Write a short professional response to the workplace situation.",
            EstimatedMinutes: 5),

        new(Order: 2, Kind: ExerciseKind.Review,
            PatternKey: "lesson_reflection",
            PrimarySkill: "Writing",
            Instructions: "Review your response and note one thing to improve next time.",
            EstimatedMinutes: 2)
    ];

    // ── 15-minute template ────────────────────────────────────────────────────
    // 4 steps: warmup + context input + main task + review
    private static readonly IReadOnlyList<ExerciseStepTemplate> Template15Min =
    [
        new(Order: 0, Kind: ExerciseKind.VocabularyWarmup,
            PatternKey: "phrase_match",
            PrimarySkill: "Vocabulary",
            Instructions: "Match these workplace phrases to their meanings.",
            EstimatedMinutes: 2),

        new(Order: 1, Kind: ExerciseKind.ContextInput,
            PatternKey: "listen_and_answer",
            PrimarySkill: "Listening",
            Instructions: "Listen to the workplace message and answer the questions.",
            EstimatedMinutes: 4),

        new(Order: 2, Kind: ExerciseKind.WritingTask,
            PatternKey: "writing_response",
            PrimarySkill: "Writing",
            Instructions: "Write a professional reply to the workplace situation.",
            EstimatedMinutes: 7),

        new(Order: 3, Kind: ExerciseKind.Review,
            PatternKey: "lesson_reflection",
            PrimarySkill: "Writing",
            Instructions: "Review the lesson vocabulary and note one phrase to use at work.",
            EstimatedMinutes: 2)
    ];

    // ── 20-minute template ────────────────────────────────────────────────────
    // 4 steps: warmup + listening/reading input + main writing/speaking task + correction
    private static readonly IReadOnlyList<ExerciseStepTemplate> Template20Min =
    [
        new(Order: 0, Kind: ExerciseKind.VocabularyWarmup,
            PatternKey: "phrase_match",
            PrimarySkill: "Vocabulary",
            Instructions: "Match these workplace phrases to their meanings.",
            EstimatedMinutes: 3),

        new(Order: 1, Kind: ExerciseKind.ListeningInput,
            PatternKey: "listen_and_gap_fill",
            PrimarySkill: "Listening",
            Instructions: "Listen and fill in the missing words.",
            EstimatedMinutes: 5),

        new(Order: 2, Kind: ExerciseKind.WritingTask,
            PatternKey: "writing_response",
            PrimarySkill: "Writing",
            Instructions: "Write a complete professional response to the workplace situation.",
            EstimatedMinutes: 9),

        new(Order: 3, Kind: ExerciseKind.Review,
            PatternKey: "lesson_reflection",
            PrimarySkill: "Writing",
            Instructions: "Review your writing and identify one grammar or vocabulary improvement.",
            EstimatedMinutes: 3)
    ];

    // ── 30-minute template ────────────────────────────────────────────────────
    // 5 steps: warmup + listening + main writing + speaking/rewrite extension + review
    private static readonly IReadOnlyList<ExerciseStepTemplate> Template30Min =
    [
        new(Order: 0, Kind: ExerciseKind.VocabularyWarmup,
            PatternKey: "phrase_match",
            PrimarySkill: "Vocabulary",
            Instructions: "Match these workplace phrases to their meanings.",
            EstimatedMinutes: 4),

        new(Order: 1, Kind: ExerciseKind.ListeningInput,
            PatternKey: "listen_and_answer",
            PrimarySkill: "Listening",
            Instructions: "Listen to the workplace audio and answer comprehension questions.",
            EstimatedMinutes: 6),

        new(Order: 2, Kind: ExerciseKind.WritingTask,
            PatternKey: "writing_response",
            PrimarySkill: "Writing",
            Instructions: "Write a full professional response to the workplace situation.",
            EstimatedMinutes: 10),

        new(Order: 3, Kind: ExerciseKind.SpeakingTask,
            PatternKey: "speaking_role_play",
            PrimarySkill: "Speaking",
            Instructions: "Record a short spoken response to the same workplace situation.",
            EstimatedMinutes: 7),

        new(Order: 4, Kind: ExerciseKind.Review,
            PatternKey: "lesson_reflection",
            PrimarySkill: "Writing",
            Instructions: "Review both your written and spoken responses. What will you do differently next time?",
            EstimatedMinutes: 3)
    ];
}

/// <summary>
/// One step slot in a duration template. Mutable by the generator for weak-skill substitution.
/// </summary>
public sealed record ExerciseStepTemplate(
    int Order,
    ExerciseKind Kind,
    string PatternKey,
    string PrimarySkill,
    string Instructions,
    int EstimatedMinutes);
