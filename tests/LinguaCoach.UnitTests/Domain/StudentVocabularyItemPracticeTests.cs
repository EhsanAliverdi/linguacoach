using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class StudentVocabularyItemPracticeTests
{
    private static StudentVocabularyItem NewItem(VocabularyItemStatus initialStatus = VocabularyItemStatus.New)
    {
        var item = new StudentVocabularyItem(
            Guid.NewGuid(), "could you please", "Could you please send the file?",
            "A polite request phrase.", "Could you please confirm?",
            "polite_request", VocabularyItemSource.AiExtractedFromWritingAttempt);

        if (initialStatus != VocabularyItemStatus.New)
            item.UpdateStatus(initialStatus);

        return item;
    }

    // ── StrengthScore initial state ───────────────────────────────────────────

    [Fact]
    public void NewItem_HasStrengthScoreZero()
    {
        NewItem().StrengthScore.Should().Be(0);
    }

    // ── RecordPractice — correct ──────────────────────────────────────────────

    [Fact]
    public void RecordPractice_Correct_IncreasesStrengthScore()
    {
        var item = NewItem();
        item.RecordPractice(correct: true);
        item.StrengthScore.Should().Be(10);
    }

    [Fact]
    public void RecordPractice_Correct_IncrementsSeenCount()
    {
        var item = NewItem();
        item.RecordPractice(correct: true);
        item.SeenCount.Should().Be(2);
    }

    [Fact]
    public void RecordPractice_Correct_UpdatesLastSeenAtUtc()
    {
        var item = NewItem();
        var before = DateTime.UtcNow;
        item.RecordPractice(correct: true);
        item.LastSeenAtUtc.Should().NotBeNull();
        item.LastSeenAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    // ── RecordPractice — incorrect ────────────────────────────────────────────

    [Fact]
    public void RecordPractice_Incorrect_StrengthScoreClampsAtZero()
    {
        var item = NewItem();
        item.RecordPractice(correct: false);
        item.StrengthScore.Should().Be(0);  // 0 - 5 = -5 → clamped to 0
    }

    [Fact]
    public void RecordPractice_RepeatedlyIncorrect_StaysAtZero()
    {
        var item = NewItem();
        for (int i = 0; i < 10; i++)
            item.RecordPractice(correct: false);
        item.StrengthScore.Should().Be(0);
    }

    // ── StrengthScore clamp at 100 ────────────────────────────────────────────

    [Fact]
    public void RecordPractice_RepeatedlyCorrect_ClampsAt100()
    {
        var item = NewItem();
        for (int i = 0; i < 20; i++)
            item.RecordPractice(correct: true);
        item.StrengthScore.Should().Be(100);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public void RecordPractice_Correct_NewBecomePractising()
    {
        var item = NewItem(VocabularyItemStatus.New);
        item.RecordPractice(correct: true);
        item.Status.Should().Be(VocabularyItemStatus.Practising);
    }

    [Fact]
    public void RecordPractice_Incorrect_NewAlsoBecomePractising()
    {
        var item = NewItem(VocabularyItemStatus.New);
        item.RecordPractice(correct: false);
        item.Status.Should().Be(VocabularyItemStatus.Practising);
    }

    [Fact]
    public void RecordPractice_Practising_BecomesMasteredWhenStrengthHigh()
    {
        var item = NewItem(VocabularyItemStatus.New);
        // Get to Practising first
        item.RecordPractice(correct: true);
        item.Status.Should().Be(VocabularyItemStatus.Practising);

        // Raise score to 90+
        for (int i = 0; i < 8; i++)
            item.RecordPractice(correct: true);
        // Score = 10 + (8*10) = 90 → should be Mastered
        item.StrengthScore.Should().Be(90);
        item.Status.Should().Be(VocabularyItemStatus.Mastered);
    }

    [Fact]
    public void RecordPractice_MasteredItem_StaysMastered()
    {
        var item = NewItem(VocabularyItemStatus.Mastered);
        item.RecordPractice(correct: false);
        // Mastered items are not reverted by incorrect answers
        // (status only transitions New→Practising and Practising→Mastered)
        item.Status.Should().Be(VocabularyItemStatus.Mastered);
    }
}
