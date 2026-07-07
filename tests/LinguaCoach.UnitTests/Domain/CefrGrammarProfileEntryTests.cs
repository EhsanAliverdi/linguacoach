using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CefrGrammarProfileEntryTests
{
    private static readonly Guid ValidSourceId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidInput_CreatesEntry()
    {
        var entry = new CefrGrammarProfileEntry(ValidSourceId, "A2", "past_simple_regular");

        Assert.Equal("A2", entry.CefrLevel);
        Assert.Equal("past_simple_regular", entry.GrammarPoint);
    }

    [Fact]
    public void Constructor_EmptySourceId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrGrammarProfileEntry(Guid.Empty, "A2", "past_simple_regular"));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrGrammarProfileEntry(ValidSourceId, "X1", "past_simple_regular"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyGrammarPoint_Throws(string grammarPoint)
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrGrammarProfileEntry(ValidSourceId, "A2", grammarPoint));
    }
}
