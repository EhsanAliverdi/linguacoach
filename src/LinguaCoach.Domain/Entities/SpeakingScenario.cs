using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Defines a reusable speaking practice scenario for a career profile and language pair.
/// Admin-managed. Specifies the goal, turn limit, target phrases, and rubric
/// used to drive and evaluate a SpeakingSession.
/// </summary>
public sealed class SpeakingScenario : BaseEntity
{
    public Guid CareerProfileId { get; private set; }
    public Guid LanguagePairId { get; private set; }

    public string Title { get; private set; }
    public string Goal { get; private set; }

    // Default max turns for sessions created from this scenario.
    public int MaxTurns { get; private set; }

    // Comma-separated target phrases the scenario aims to elicit.
    public string TargetPhrases { get; private set; }

    public string Rubric { get; private set; }

    // CEFR level this scenario targets, e.g. "A2", "B1", "B2".
    public string DifficultyLevel { get; private set; }

    private SpeakingScenario()
    {
        Title = string.Empty;
        Goal = string.Empty;
        TargetPhrases = string.Empty;
        Rubric = string.Empty;
        DifficultyLevel = string.Empty;
    }

    public SpeakingScenario(
        Guid careerProfileId,
        Guid languagePairId,
        string title,
        string goal,
        int maxTurns,
        string targetPhrases,
        string rubric,
        string difficultyLevel)
    {
        if (careerProfileId == Guid.Empty) throw new ArgumentException("CareerProfileId must not be empty.", nameof(careerProfileId));
        if (languagePairId == Guid.Empty) throw new ArgumentException("LanguagePairId must not be empty.", nameof(languagePairId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Goal is required.", nameof(goal));
        if (maxTurns < 1) throw new ArgumentOutOfRangeException(nameof(maxTurns), "MaxTurns must be at least 1.");
        if (string.IsNullOrWhiteSpace(rubric)) throw new ArgumentException("Rubric is required.", nameof(rubric));
        if (string.IsNullOrWhiteSpace(difficultyLevel)) throw new ArgumentException("DifficultyLevel is required.", nameof(difficultyLevel));

        CareerProfileId = careerProfileId;
        LanguagePairId = languagePairId;
        Title = title.Trim();
        Goal = goal.Trim();
        MaxTurns = maxTurns;
        TargetPhrases = targetPhrases?.Trim() ?? string.Empty;
        Rubric = rubric.Trim();
        DifficultyLevel = difficultyLevel.Trim();
    }
}
