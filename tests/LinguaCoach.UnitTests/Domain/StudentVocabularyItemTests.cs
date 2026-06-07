using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class StudentVocabularyItemTests
{
    private static Guid AProfileId() => Guid.NewGuid();

    private static StudentVocabularyItem NewItem(string term = "could you please", string category = "polite_request") =>
        new(AProfileId(), term, "Could you please send the file?",
            "A polite way to make a request.", "Could you please confirm the time?",
            category, VocabularyItemSource.AiExtractedFromWritingAttempt);

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithEmptyProfileId_Throws()
    {
        var act = () => new StudentVocabularyItem(
            Guid.Empty, "term", null, "explanation", null, "polite_request",
            VocabularyItemSource.AiExtractedFromWritingAttempt);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyTerm_Throws()
    {
        var act = () => new StudentVocabularyItem(
            AProfileId(), "  ", null, "explanation", null, "polite_request",
            VocabularyItemSource.AiExtractedFromWritingAttempt);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NormalisesTerm_ToLowercase()
    {
        var item = new StudentVocabularyItem(
            AProfileId(), "Could You Please", null, "explanation", null, "polite_request",
            VocabularyItemSource.AiExtractedFromWritingAttempt);
        item.Term.Should().Be("could you please");
    }

    [Fact]
    public void Constructor_SetsStatusToNew()
    {
        var item = NewItem();
        item.Status.Should().Be(VocabularyItemStatus.New);
    }

    [Fact]
    public void Constructor_SetsSeenCountTo1()
    {
        var item = NewItem();
        item.SeenCount.Should().Be(1);
    }

    // ── RecordSeen ────────────────────────────────────────────────────────────

    [Fact]
    public void RecordSeen_IncrementsSeenCount()
    {
        var item = NewItem();
        item.RecordSeen();
        item.SeenCount.Should().Be(2);
    }

    [Fact]
    public void RecordSeen_UpdatesLastSeenAtUtc()
    {
        var item = NewItem();
        var before = DateTime.UtcNow;
        item.RecordSeen();
        item.LastSeenAtUtc.Should().NotBeNull();
        item.LastSeenAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    // ── UpdateStatus ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VocabularyItemStatus.Practising)]
    [InlineData(VocabularyItemStatus.Mastered)]
    [InlineData(VocabularyItemStatus.Ignored)]
    public void UpdateStatus_SetsNewStatus(VocabularyItemStatus newStatus)
    {
        var item = NewItem();
        item.UpdateStatus(newStatus);
        item.Status.Should().Be(newStatus);
    }

    [Fact]
    public void UpdateStatus_UpdatesUpdatedAt()
    {
        var item = NewItem();
        var before = DateTime.UtcNow;
        item.UpdateStatus(VocabularyItemStatus.Mastered);
        item.UpdatedAt.Should().BeOnOrAfter(before);
    }

    // ── NormaliseTerm ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Could You Please", "could you please")]
    [InlineData("  follow up  ", "follow up")]
    [InlineData("AT YOUR EARLIEST CONVENIENCE", "at your earliest convenience")]
    public void NormaliseTerm_LowercasesAndTrims(string input, string expected)
    {
        StudentVocabularyItem.NormaliseTerm(input).Should().Be(expected);
    }
}
