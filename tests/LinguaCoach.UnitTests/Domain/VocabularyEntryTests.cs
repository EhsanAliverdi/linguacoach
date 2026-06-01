using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class VocabularyEntryTests
{
    private static Guid AStudentId() => Guid.NewGuid();
    private static Guid ALangPairId() => Guid.NewGuid();

    private static VocabularyEntry NewEntry() =>
        new(AStudentId(), ALangPairId(), "submittal", "A document submitted for approval.");

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var act = () => new VocabularyEntry(Guid.Empty, ALangPairId(), "word", "def");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyLanguagePairId_Throws()
    {
        var act = () => new VocabularyEntry(AStudentId(), Guid.Empty, "word", "def");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankWord_Throws()
    {
        var act = () => new VocabularyEntry(AStudentId(), ALangPairId(), "  ", "def");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NewEntry_HasNewStatusAndDefaultEaseFactor()
    {
        var entry = NewEntry();
        entry.Status.Should().Be(VocabularyStatus.New);
        entry.EaseFactor.Should().Be(2.5);
        entry.MasteryScore.Should().Be(0);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public void RecordExposure_TransitionsNewToSeen()
    {
        var entry = NewEntry();
        entry.RecordExposure();
        entry.Status.Should().Be(VocabularyStatus.Seen);
        entry.ExposureCount.Should().Be(1);
        entry.LastSeen.Should().NotBeNull();
    }

    [Fact]
    public void RecordRecognition_Correct_AfterSeen_TransitionsToRecognised()
    {
        var entry = NewEntry();
        entry.RecordExposure();
        entry.RecordRecognition(correct: true);
        entry.Status.Should().Be(VocabularyStatus.Recognised);
        entry.CorrectCount.Should().Be(1);
        entry.RecognitionCount.Should().Be(1);
    }

    [Fact]
    public void RecordRecognition_Incorrect_SetsWeak()
    {
        var entry = NewEntry();
        entry.RecordExposure();
        entry.RecordRecognition(correct: false);
        entry.Status.Should().Be(VocabularyStatus.Weak);
        entry.IncorrectCount.Should().Be(1);
    }

    [Fact]
    public void RecordRecall_Correct_AfterRecognised_TransitionsToLearning()
    {
        var entry = NewEntry();
        entry.RecordExposure();
        entry.RecordRecognition(correct: true);
        entry.RecordRecall(correct: true);
        entry.Status.Should().Be(VocabularyStatus.Learning);
        entry.RecallCount.Should().Be(1);
    }

    [Fact]
    public void SetMasteryScore_AboveThreshold_AfterLearning_TransitionsToMastered()
    {
        var entry = NewEntry();
        entry.RecordExposure();
        entry.RecordRecognition(correct: true);
        entry.RecordRecall(correct: true);
        entry.SetMasteryScore(0.90);
        entry.Status.Should().Be(VocabularyStatus.Mastered);
        entry.MasteryScore.Should().Be(0.90);
    }

    [Fact]
    public void SetMasteryScore_BelowThreshold_DoesNotMaster()
    {
        var entry = NewEntry();
        entry.RecordExposure();
        entry.RecordRecognition(correct: true);
        entry.RecordRecall(correct: true);
        entry.SetMasteryScore(0.70);
        entry.Status.Should().Be(VocabularyStatus.Learning);
    }

    [Fact]
    public void SetMasteryScore_OutOfRange_Throws()
    {
        var entry = NewEntry();
        var act = () => entry.SetMasteryScore(1.5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Retire_SetsStatusToRetired()
    {
        var entry = NewEntry();
        entry.Retire();
        entry.Status.Should().Be(VocabularyStatus.Retired);
    }

    [Fact]
    public void MarkWeak_OnRetired_DoesNotChangeStatus()
    {
        var entry = NewEntry();
        entry.Retire();
        entry.RecordRecognition(correct: false);
        entry.Status.Should().Be(VocabularyStatus.Retired);
    }

    [Fact]
    public void ScheduleNextReview_SetsDateAndEaseFactor()
    {
        var entry = NewEntry();
        var reviewDate = DateTime.UtcNow.AddDays(3);
        entry.ScheduleNextReview(reviewDate, 2.8);
        entry.NextReviewDate.Should().Be(reviewDate);
        entry.EaseFactor.Should().Be(2.8);
    }
}
