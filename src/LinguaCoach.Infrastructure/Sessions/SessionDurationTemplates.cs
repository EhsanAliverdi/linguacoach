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
            Instructions: "Match these phrases to their meanings.",
            EstimatedMinutes: 3,
            CandidatePatternKeys: ["phrase_match", "gap_fill_workplace_phrase"]),

        new(Order: 1, Kind: ExerciseKind.WritingTask,
            PatternKey: "email_reply",
            PrimarySkill: "Writing",
            Instructions: "Write a short professional reply to the situation.",
            EstimatedMinutes: 5,
            CandidatePatternKeys: ["email_reply", "writing_response", "teams_chat_simulation", "spoken_response_from_prompt"]),

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
            Instructions: "Match these phrases to their meanings.",
            EstimatedMinutes: 2,
            CandidatePatternKeys: ["phrase_match", "gap_fill_workplace_phrase"]),

        new(Order: 1, Kind: ExerciseKind.ContextInput,
            PatternKey: "listen_and_answer",
            PrimarySkill: "Listening",
            Instructions: "Listen to the audio and answer the questions.",
            EstimatedMinutes: 4,
            CandidatePatternKeys: ["listen_and_answer", "listen_and_gap_fill"]),

        new(Order: 2, Kind: ExerciseKind.WritingTask,
            PatternKey: "email_reply",
            PrimarySkill: "Writing",
            Instructions: "Write a professional reply to the situation.",
            EstimatedMinutes: 7,
            CandidatePatternKeys: ["email_reply", "writing_response", "teams_chat_simulation", "spoken_response_from_prompt"]),

        new(Order: 3, Kind: ExerciseKind.Review,
            PatternKey: "lesson_reflection",
            PrimarySkill: "Writing",
            Instructions: "Review the lesson vocabulary and note one phrase to practise.",
            EstimatedMinutes: 2)
    ];

    // ── 20-minute template ────────────────────────────────────────────────────
    // 4 steps: warmup + listening/reading input + main writing/speaking task + correction
    private static readonly IReadOnlyList<ExerciseStepTemplate> Template20Min =
    [
        new(Order: 0, Kind: ExerciseKind.VocabularyWarmup,
            PatternKey: "phrase_match",
            PrimarySkill: "Vocabulary",
            Instructions: "Match these phrases to their meanings.",
            EstimatedMinutes: 3,
            CandidatePatternKeys: ["phrase_match", "gap_fill_workplace_phrase"]),

        new(Order: 1, Kind: ExerciseKind.ListeningInput,
            PatternKey: "listen_and_gap_fill",
            PrimarySkill: "Listening",
            Instructions: "Listen and fill in the missing words.",
            EstimatedMinutes: 5,
            CandidatePatternKeys: ["listen_and_gap_fill", "listen_and_answer"]),

        new(Order: 2, Kind: ExerciseKind.WritingTask,
            PatternKey: "email_reply",
            PrimarySkill: "Writing",
            Instructions: "Write a complete professional reply to the situation.",
            EstimatedMinutes: 9,
            CandidatePatternKeys: ["email_reply", "writing_response", "teams_chat_simulation", "spoken_response_from_prompt"]),

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
            Instructions: "Match these phrases to their meanings.",
            EstimatedMinutes: 4,
            CandidatePatternKeys: ["phrase_match", "gap_fill_workplace_phrase"]),

        new(Order: 1, Kind: ExerciseKind.ListeningInput,
            PatternKey: "listen_and_answer",
            PrimarySkill: "Listening",
            Instructions: "Listen to the audio and answer comprehension questions.",
            EstimatedMinutes: 6,
            CandidatePatternKeys: ["listen_and_answer", "listen_and_gap_fill"]),

        new(Order: 2, Kind: ExerciseKind.WritingTask,
            PatternKey: "email_reply",
            PrimarySkill: "Writing",
            Instructions: "Write a full professional reply to the situation.",
            EstimatedMinutes: 10,
            CandidatePatternKeys: ["email_reply", "writing_response", "teams_chat_simulation"]),

        new(Order: 3, Kind: ExerciseKind.SpeakingTask,
            PatternKey: "spoken_response_from_prompt",
            PrimarySkill: "Speaking",
            Instructions: "Record a short spoken response to the same situation.",
            EstimatedMinutes: 7,
            CandidatePatternKeys: [
                "spoken_response_from_prompt",
                "answer_short_question",
                "read_aloud",
                "repeat_sentence",
                "respond_to_situation"]),

        new(Order: 4, Kind: ExerciseKind.Review,
            PatternKey: "lesson_reflection",
            PrimarySkill: "Writing",
            Instructions: "Review both your written and spoken responses. What will you do differently next time?",
            EstimatedMinutes: 3)
    ];
}

/// <summary>
/// One step slot in a duration template. The PatternKey is the default; CandidatePatternKeys
/// lists all alternatives the dynamic selector may choose from for this slot.
/// </summary>
public sealed record ExerciseStepTemplate(
    int Order,
    ExerciseKind Kind,
    string PatternKey,
    string PrimarySkill,
    string Instructions,
    int EstimatedMinutes,
    IReadOnlyList<string>? CandidatePatternKeys = null)
{
    /// <summary>
    /// Returns the candidate pool for this slot, always including the default PatternKey.
    /// </summary>
    public IReadOnlyList<string> GetCandidates()
    {
        if (CandidatePatternKeys is { Count: > 0 })
        {
            // Ensure the default is in the pool.
            return CandidatePatternKeys.Contains(PatternKey, StringComparer.OrdinalIgnoreCase)
                ? CandidatePatternKeys
                : [PatternKey, .. CandidatePatternKeys];
        }
        return [PatternKey];
    }
}
