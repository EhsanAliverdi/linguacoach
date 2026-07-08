using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CefrReadingPassageTests
{
    private static readonly Guid ValidSourceId = Guid.NewGuid();
    private const string SamplePassage =
        "This is a short sample passage used only to verify word count and reading time computation.";

    [Fact]
    public void Constructor_ValidInput_CreatesPassage()
    {
        var passage = new CefrReadingPassage(ValidSourceId, "A Title", SamplePassage, "B1");

        Assert.Equal("A Title", passage.Title);
        Assert.Equal(SamplePassage, passage.PassageText);
        Assert.Equal("B1", passage.CefrLevel);
        Assert.Equal("Reading", passage.PrimarySkill);
    }

    [Fact]
    public void Constructor_EmptySourceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrReadingPassage(Guid.Empty, "Title", SamplePassage, "B1"));
    }

    [Fact]
    public void Constructor_MissingTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrReadingPassage(ValidSourceId, "", SamplePassage, "B1"));
    }

    [Fact]
    public void Constructor_MissingPassageText_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrReadingPassage(ValidSourceId, "Title", "", "B1"));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrReadingPassage(ValidSourceId, "Title", SamplePassage, "X1"));
    }

    [Fact]
    public void Constructor_DifficultyBandOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CefrReadingPassage(ValidSourceId, "Title", SamplePassage, "B1", difficultyBand: 6));
    }

    [Fact]
    public void Constructor_QualityScoreOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CefrReadingPassage(ValidSourceId, "Title", SamplePassage, "B1", qualityScore: 1.5));
    }

    [Fact]
    public void Constructor_ComputesWordCountAndEstimatedReadingMinutes()
    {
        var passage = new CefrReadingPassage(ValidSourceId, "Title", SamplePassage, "B1");

        Assert.Equal(SamplePassage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length, passage.WordCount);
        Assert.True(passage.EstimatedReadingMinutes >= 1);
    }

    [Fact]
    public void Constructor_OptionalFieldsNull_DoesNotThrow()
    {
        var passage = new CefrReadingPassage(ValidSourceId, "Title", SamplePassage, "B1");

        Assert.Null(passage.Summary);
        Assert.Null(passage.Subskill);
        Assert.Null(passage.AttributionText);
        Assert.Null(passage.ContentFingerprint);
        Assert.Null(passage.QualityScore);
        Assert.Null(passage.DifficultyBand);
    }
}
