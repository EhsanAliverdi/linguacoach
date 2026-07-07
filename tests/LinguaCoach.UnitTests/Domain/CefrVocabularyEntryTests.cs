using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CefrVocabularyEntryTests
{
    private static readonly Guid ValidSourceId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidInput_CreatesEntry()
    {
        var entry = new CefrVocabularyEntry(ValidSourceId, "greeting", "A1", partOfSpeech: "noun");

        Assert.Equal("greeting", entry.Word);
        Assert.Equal("A1", entry.CefrLevel);
        Assert.Equal("noun", entry.PartOfSpeech);
    }

    [Fact]
    public void Constructor_EmptySourceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrVocabularyEntry(Guid.Empty, "greeting", "A1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyWord_Throws(string word)
    {
        Assert.Throws<ArgumentException>(() => new CefrVocabularyEntry(ValidSourceId, word, "A1"));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrVocabularyEntry(ValidSourceId, "greeting", "X1"));
    }
}
