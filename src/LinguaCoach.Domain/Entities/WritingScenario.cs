using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A reusable writing practice scenario. Generic scenarios (no LanguagePairId or CareerProfileId)
/// apply to all students. Specific ones can be filtered by career or language pair later.
/// </summary>
public sealed class WritingScenario : BaseEntity
{
    public Guid? LanguagePairId { get; private set; }
    public Guid? CareerProfileId { get; private set; }

    public string Title { get; private set; }
    public string Situation { get; private set; }
    public string LearningGoal { get; private set; }

    // JSON arrays stored as text, e.g. ["phrase one","phrase two"]
    public string TargetPhrasesJson { get; private set; }
    public string TargetVocabularyJson { get; private set; }

    public string ExampleText { get; private set; }
    public string CommonMistakeToAvoid { get; private set; }

    // CEFR level, e.g. "A2", "B1"
    public string Difficulty { get; private set; }

    public bool IsActive { get; private set; }

    private WritingScenario()
    {
        Title = string.Empty;
        Situation = string.Empty;
        LearningGoal = string.Empty;
        TargetPhrasesJson = "[]";
        TargetVocabularyJson = "[]";
        ExampleText = string.Empty;
        CommonMistakeToAvoid = string.Empty;
        Difficulty = string.Empty;
    }

    public WritingScenario(
        string title,
        string situation,
        string learningGoal,
        string targetPhrasesJson,
        string targetVocabularyJson,
        string exampleText,
        string commonMistakeToAvoid,
        string difficulty,
        bool isActive = true,
        Guid? languagePairId = null,
        Guid? careerProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(situation)) throw new ArgumentException("Situation is required.", nameof(situation));
        if (string.IsNullOrWhiteSpace(learningGoal)) throw new ArgumentException("LearningGoal is required.", nameof(learningGoal));
        if (string.IsNullOrWhiteSpace(difficulty)) throw new ArgumentException("Difficulty is required.", nameof(difficulty));

        Title = title.Trim();
        Situation = situation.Trim();
        LearningGoal = learningGoal.Trim();
        TargetPhrasesJson = string.IsNullOrWhiteSpace(targetPhrasesJson) ? "[]" : targetPhrasesJson;
        TargetVocabularyJson = string.IsNullOrWhiteSpace(targetVocabularyJson) ? "[]" : targetVocabularyJson;
        ExampleText = exampleText?.Trim() ?? string.Empty;
        CommonMistakeToAvoid = commonMistakeToAvoid?.Trim() ?? string.Empty;
        Difficulty = difficulty.Trim();
        IsActive = isActive;
        LanguagePairId = languagePairId;
        CareerProfileId = careerProfileId;
    }
}
