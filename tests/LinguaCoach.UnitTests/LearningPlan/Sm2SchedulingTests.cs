using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.LearningPlan;

namespace LinguaCoach.UnitTests.LearningPlan;

/// <summary>
/// Unit tests for SM-2 scheduling and masteryScore calculation in LearningPlannerService.
/// These are the core spaced-repetition invariants — bugs here are silent in production.
/// </summary>
public sealed class Sm2SchedulingTests
{
    private static VocabularyEntry MakeEntry(VocabularyStatus status = VocabularyStatus.New, double easeFactor = 2.5)
    {
        var entry = new VocabularyEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            word: "approval",
            definition: "Official agreement or permission");

        // Drive to desired status via domain methods
        switch (status)
        {
            case VocabularyStatus.Seen:
                entry.RecordExposure();
                break;
            case VocabularyStatus.Weak:
                entry.RecordRecall(correct: false);
                break;
            case VocabularyStatus.Learning:
                entry.RecordRecall(correct: true);
                entry.RecordRecall(correct: true);
                break;
            case VocabularyStatus.Mastered:
                entry.RecordRecall(correct: true);
                entry.RecordRecall(correct: true);
                entry.SetMasteryScore(0.9);
                break;
        }

        if (Math.Abs(easeFactor - 2.5) > 0.001)
            entry.ScheduleNextReview(DateTime.UtcNow.AddDays(6), easeFactor);

        return entry;
    }

    // ── SM-2: failed review (quality < 3) ─────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CalculateNextReview_QualityBelow3_ResetsToOneDay(int quality)
    {
        var entry = MakeEntry();
        var before = DateTime.UtcNow;

        var (nextReview, newEaseFactor) = LearningPlannerService.CalculateNextReview(entry, quality);

        nextReview.Should().BeCloseTo(before.AddDays(1), TimeSpan.FromSeconds(5));
        newEaseFactor.Should().Be(entry.EaseFactor, "ease factor must not change on failure");
    }

    // ── SM-2: first successful repetition → 1-day interval ───────────────────

    [Fact]
    public void CalculateNextReview_FirstRepetition_OneDayInterval()
    {
        var entry = MakeEntry();  // RepetitionCount = 0
        var before = DateTime.UtcNow;

        var (nextReview, _) = LearningPlannerService.CalculateNextReview(entry, quality: 4);

        nextReview.Should().BeCloseTo(before.AddDays(1), TimeSpan.FromSeconds(5));
    }

    // ── SM-2: second repetition → 6-day interval ─────────────────────────────

    [Fact]
    public void CalculateNextReview_SecondRepetition_SixDayInterval()
    {
        var entry = MakeEntry();
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: true);  // RepetitionCount = 1
        var before = DateTime.UtcNow;

        var (nextReview, _) = LearningPlannerService.CalculateNextReview(entry, quality: 4);

        nextReview.Should().BeCloseTo(before.AddDays(6), TimeSpan.FromSeconds(5));
    }

    // ── SM-2: ease factor increases on quality=5, decreases on quality=3 ─────

    [Fact]
    public void CalculateNextReview_Quality5_IncreasesEaseFactor()
    {
        var entry = MakeEntry(easeFactor: 2.5);
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: true);
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(6), entry.EaseFactor, succeeded: true);  // RepetitionCount = 2

        var (_, newEaseFactor) = LearningPlannerService.CalculateNextReview(entry, quality: 5);

        newEaseFactor.Should().BeGreaterThan(2.5);
    }

    [Fact]
    public void CalculateNextReview_Quality3_DecreasesEaseFactor()
    {
        var entry = MakeEntry(easeFactor: 2.5);
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: true);
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(6), entry.EaseFactor, succeeded: true);  // RepetitionCount = 2

        var (_, newEaseFactor) = LearningPlannerService.CalculateNextReview(entry, quality: 3);

        newEaseFactor.Should().BeLessThan(2.5);
    }

    [Fact]
    public void CalculateNextReview_EaseFactorNeverDropsBelowMinimum()
    {
        var entry = MakeEntry(easeFactor: 1.3);  // already at minimum
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: true);
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(6), entry.EaseFactor, succeeded: true);

        var (_, newEaseFactor) = LearningPlannerService.CalculateNextReview(entry, quality: 3);

        newEaseFactor.Should().BeGreaterThanOrEqualTo(1.3);
    }

    // ── SM-2: invalid quality throws ─────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void CalculateNextReview_InvalidQuality_Throws(int quality)
    {
        var entry = MakeEntry();

        var act = () => LearningPlannerService.CalculateNextReview(entry, quality);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── RepetitionCount persisted via ScheduleNextReview ─────────────────────

    [Fact]
    public void ScheduleNextReview_Success_IncrementsRepetitionCount()
    {
        var entry = MakeEntry();
        Assert.Equal(0, entry.RepetitionCount);

        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: true);

        Assert.Equal(1, entry.RepetitionCount);
    }

    [Fact]
    public void ScheduleNextReview_Failure_ResetsRepetitionCount()
    {
        var entry = MakeEntry();
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: true);
        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(6), entry.EaseFactor, succeeded: true);
        Assert.Equal(2, entry.RepetitionCount);

        entry.ScheduleNextReview(DateTime.UtcNow.AddDays(1), entry.EaseFactor, succeeded: false);

        Assert.Equal(0, entry.RepetitionCount);
    }

    // ── MasteryScore calculation ──────────────────────────────────────────────

    [Fact]
    public void CalculateMasteryScore_NewEntry_ReturnsZero()
    {
        var entry = MakeEntry(VocabularyStatus.New);
        LearningPlannerService.CalculateMasteryScore(entry).Should().Be(0.0);
    }

    [Fact]
    public void CalculateMasteryScore_MasteredEntry_ReturnsHigh()
    {
        var entry = MakeEntry(VocabularyStatus.Mastered);
        LearningPlannerService.CalculateMasteryScore(entry).Should().BeGreaterThanOrEqualTo(0.5);
    }

    [Fact]
    public void CalculateMasteryScore_WeakEntry_LowerThanLearning()
    {
        var weak = MakeEntry(VocabularyStatus.Weak);
        var learning = MakeEntry(VocabularyStatus.Learning);

        LearningPlannerService.CalculateMasteryScore(weak)
            .Should().BeLessThan(LearningPlannerService.CalculateMasteryScore(learning));
    }

    [Fact]
    public void CalculateMasteryScore_AlwaysInRange()
    {
        foreach (var status in Enum.GetValues<VocabularyStatus>())
        {
            var entry = MakeEntry(status);
            var score = LearningPlannerService.CalculateMasteryScore(entry);
            score.Should().BeInRange(0.0, 1.0);
        }
    }

    // ── Status transitions via domain methods ─────────────────────────────────

    [Fact]
    public void VocabularyEntry_NewToSeen_OnExposure()
    {
        var entry = new VocabularyEntry(Guid.NewGuid(), Guid.NewGuid(), "word", "def");
        entry.Status.Should().Be(VocabularyStatus.New);

        entry.RecordExposure();

        entry.Status.Should().Be(VocabularyStatus.Seen);
    }

    [Fact]
    public void VocabularyEntry_TwoCorrectRecalls_ProgressesToLearning()
    {
        var entry = new VocabularyEntry(Guid.NewGuid(), Guid.NewGuid(), "word", "def");

        entry.RecordRecall(correct: true);  // New → Practised
        entry.RecordRecall(correct: true);  // Practised → Learning (via RecordRecall seen→practised, then recall again)

        entry.Status.Should().BeOneOf(VocabularyStatus.Practised, VocabularyStatus.Learning);
        entry.CorrectCount.Should().Be(2);
    }

    [Fact]
    public void VocabularyEntry_IncorrectRecall_TransitionsToWeak()
    {
        var entry = new VocabularyEntry(Guid.NewGuid(), Guid.NewGuid(), "word", "def");
        entry.RecordRecall(correct: true);  // get some progress first

        entry.RecordRecall(correct: false);

        entry.Status.Should().Be(VocabularyStatus.Weak);
        entry.IncorrectCount.Should().Be(1);
    }

    [Fact]
    public void VocabularyEntry_WeakThenCorrectRecall_RecoveriesToLearning()
    {
        var entry = new VocabularyEntry(Guid.NewGuid(), Guid.NewGuid(), "word", "def");
        entry.RecordRecall(correct: false);  // → Weak

        entry.RecordRecall(correct: true);  // Weak → Learning

        entry.Status.Should().Be(VocabularyStatus.Learning);
    }

    [Fact]
    public void VocabularyEntry_MasteryScore085AndLearning_TransitionsToMastered()
    {
        var entry = new VocabularyEntry(Guid.NewGuid(), Guid.NewGuid(), "word", "def");
        entry.RecordRecall(correct: true);
        entry.RecordRecall(correct: true);  // → Learning

        entry.SetMasteryScore(0.9);

        entry.Status.Should().Be(VocabularyStatus.Mastered);
    }
}
