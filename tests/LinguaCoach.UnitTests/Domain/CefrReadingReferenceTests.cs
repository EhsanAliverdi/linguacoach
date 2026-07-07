using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CefrReadingReferenceTests
{
    private static readonly Guid ValidSourceId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidInput_CreatesReference()
    {
        var reference = new CefrReadingReference(ValidSourceId, "B1", textType: "email");

        Assert.Equal("B1", reference.CefrLevel);
        Assert.Equal("email", reference.TextType);
    }

    [Fact]
    public void Constructor_EmptySourceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrReadingReference(Guid.Empty, "B1"));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CefrReadingReference(ValidSourceId, "X1"));
    }

    [Fact]
    public void Constructor_OptionalFieldsNull_DoesNotThrow()
    {
        var reference = new CefrReadingReference(ValidSourceId, "B1");

        Assert.Null(reference.TextType);
        Assert.Null(reference.DifficultyNotes);
        Assert.Null(reference.ReferenceExcerpt);
    }
}
