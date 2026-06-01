using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records which vocabulary words appeared in a given lesson for a student.
/// Used by LearningPlanner for anti-repetition: words in the last 3 lessons
/// or seen in the last 24 hours are excluded from new-word selection.
/// </summary>
public sealed class LessonVocabularyLog : BaseEntity
{
    public Guid StudentProfileId { get; private set; }

    // Foreign key to the vocabulary entry that appeared in the lesson.
    public Guid VocabularyEntryId { get; private set; }

    // Which lesson this word appeared in (1-based counter per student, incremented by LearningPlanner).
    public int LessonNumber { get; private set; }

    // When the lesson occurred — used for the 24-hour recency check.
    public DateTime OccurredAt { get; private set; }

    private LessonVocabularyLog() { }

    public LessonVocabularyLog(Guid studentProfileId, Guid vocabularyEntryId, int lessonNumber)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (vocabularyEntryId == Guid.Empty) throw new ArgumentException("VocabularyEntryId must not be empty.", nameof(vocabularyEntryId));
        if (lessonNumber < 1) throw new ArgumentOutOfRangeException(nameof(lessonNumber), "LessonNumber must be >= 1.");

        StudentProfileId = studentProfileId;
        VocabularyEntryId = vocabularyEntryId;
        LessonNumber = lessonNumber;
        OccurredAt = DateTime.UtcNow;
    }
}
