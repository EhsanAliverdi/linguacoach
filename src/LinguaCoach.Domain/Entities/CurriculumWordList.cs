using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// An ordered list of words/phrases tied to a career profile and language pair.
/// LearningPlanner queries this to select new vocabulary for a student in priority order.
/// </summary>
public sealed class CurriculumWordList : BaseEntity
{
    public Guid CareerProfileId { get; private set; }
    public Guid LanguagePairId { get; private set; }

    public string Word { get; private set; }
    public string Definition { get; private set; }
    public string ExampleSentence { get; private set; }

    // Lower value = introduced earlier in the curriculum.
    public int Priority { get; private set; }

    // Comma-separated tags, e.g. "email,formal,document-control".
    public string Tags { get; private set; }

    private CurriculumWordList() { Word = string.Empty; Definition = string.Empty; ExampleSentence = string.Empty; Tags = string.Empty; }

    public CurriculumWordList(
        Guid careerProfileId,
        Guid languagePairId,
        string word,
        string definition,
        string exampleSentence,
        int priority,
        string tags = "")
    {
        if (careerProfileId == Guid.Empty) throw new ArgumentException("CareerProfileId must not be empty.", nameof(careerProfileId));
        if (languagePairId == Guid.Empty) throw new ArgumentException("LanguagePairId must not be empty.", nameof(languagePairId));
        if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("Word is required.", nameof(word));
        if (string.IsNullOrWhiteSpace(definition)) throw new ArgumentException("Definition is required.", nameof(definition));
        if (priority < 0) throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be >= 0.");

        CareerProfileId = careerProfileId;
        LanguagePairId = languagePairId;
        Word = word.Trim();
        Definition = definition.Trim();
        ExampleSentence = exampleSentence?.Trim() ?? string.Empty;
        Priority = priority;
        Tags = tags?.Trim() ?? string.Empty;
    }
}
