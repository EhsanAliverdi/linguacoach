using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Tracks a single word or phrase for a specific student, including mastery level,
/// review scheduling (SM-2), and separate recognition/recall/usage counters.
/// </summary>
public sealed class VocabularyEntry : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid LanguagePairId { get; private set; }

    public string Word { get; private set; }
    public string Definition { get; private set; }

    public VocabularyStatus Status { get; private set; }

    // Separate counters for recognition, recall, and usage in own output.
    public int RecognitionCount { get; private set; }
    public int RecallCount { get; private set; }
    public int UsageCount { get; private set; }

    public int ExposureCount { get; private set; }
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }

    public DateTime? LastSeen { get; private set; }
    public DateTime? LastPractised { get; private set; }
    public DateTime? NextReviewDate { get; private set; }

    // SM-2 ease factor. Default 2.5 per the SM-2 algorithm specification.
    public double EaseFactor { get; private set; }

    // Composite mastery score 0.0–1.0, updated after each interaction.
    public double MasteryScore { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private VocabularyEntry() { Word = string.Empty; Definition = string.Empty; }

    public VocabularyEntry(Guid studentProfileId, Guid languagePairId, string word, string definition)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (languagePairId == Guid.Empty) throw new ArgumentException("LanguagePairId must not be empty.", nameof(languagePairId));
        if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("Word is required.", nameof(word));
        if (string.IsNullOrWhiteSpace(definition)) throw new ArgumentException("Definition is required.", nameof(definition));

        StudentProfileId = studentProfileId;
        LanguagePairId = languagePairId;
        Word = word.Trim();
        Definition = definition.Trim();
        Status = VocabularyStatus.New;
        EaseFactor = 2.5;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordExposure()
    {
        ExposureCount++;
        LastSeen = DateTime.UtcNow;
        if (Status == VocabularyStatus.New)
            Status = VocabularyStatus.Seen;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordRecognition(bool correct)
    {
        RecognitionCount++;
        ExposureCount++;
        LastSeen = DateTime.UtcNow;
        if (correct)
        {
            CorrectCount++;
            if (Status == VocabularyStatus.Seen)
                Status = VocabularyStatus.Recognised;
        }
        else
        {
            IncorrectCount++;
            MarkWeak();
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordRecall(bool correct)
    {
        RecallCount++;
        LastPractised = DateTime.UtcNow;
        if (correct)
        {
            CorrectCount++;
            if (Status == VocabularyStatus.Recognised || Status == VocabularyStatus.Practised)
                Status = VocabularyStatus.Learning;
            else if (Status == VocabularyStatus.Weak)
                Status = VocabularyStatus.Learning;
            else if (Status == VocabularyStatus.New || Status == VocabularyStatus.Seen)
                Status = VocabularyStatus.Practised;
        }
        else
        {
            IncorrectCount++;
            MarkWeak();
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordUsage(bool correct)
    {
        UsageCount++;
        if (correct) CorrectCount++;
        else { IncorrectCount++; MarkWeak(); }
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMasteryScore(double score)
    {
        if (score < 0 || score > 1) throw new ArgumentOutOfRangeException(nameof(score), "Mastery score must be between 0.0 and 1.0.");
        MasteryScore = score;
        if (score >= 0.85 && Status == VocabularyStatus.Learning)
            Status = VocabularyStatus.Mastered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ScheduleNextReview(DateTime nextReviewDate, double easeFactor)
    {
        NextReviewDate = nextReviewDate;
        EaseFactor = easeFactor;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Retire()
    {
        Status = VocabularyStatus.Retired;
        UpdatedAt = DateTime.UtcNow;
    }

    private void MarkWeak()
    {
        if (Status != VocabularyStatus.Retired)
            Status = VocabularyStatus.Weak;
    }
}
