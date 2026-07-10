using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class ResourceBankItemTests
{
    private static readonly Guid ValidSourceId = Guid.NewGuid();
    private const string SampleContentJson = "{\"title\":\"A Title\",\"passageText\":\"Sample text.\"}";

    [Fact]
    public void Constructor_ValidInput_CreatesItem()
    {
        var item = new ResourceBankItem(PublishedResourceType.ReadingPassage, ValidSourceId, "B1", SampleContentJson);

        Assert.Equal(PublishedResourceType.ReadingPassage, item.Type);
        Assert.Equal("B1", item.CefrLevel);
        Assert.Equal(SampleContentJson, item.ContentJson);
    }

    [Fact]
    public void Constructor_EmptySourceId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ResourceBankItem(PublishedResourceType.Vocabulary, Guid.Empty, "B1", SampleContentJson));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ResourceBankItem(PublishedResourceType.Vocabulary, ValidSourceId, "X1", SampleContentJson));
    }

    [Fact]
    public void Constructor_EmptyContentJson_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ResourceBankItem(PublishedResourceType.Vocabulary, ValidSourceId, "B1", ""));
    }

    [Fact]
    public void Constructor_DifficultyBandOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ResourceBankItem(PublishedResourceType.Vocabulary, ValidSourceId, "B1", SampleContentJson, difficultyBand: 6));
    }

    [Fact]
    public void Constructor_OptionalFieldsNull_DoesNotThrow()
    {
        var item = new ResourceBankItem(PublishedResourceType.Vocabulary, ValidSourceId, "B1", SampleContentJson);

        Assert.Null(item.Subskill);
        Assert.Null(item.DifficultyBand);
        Assert.Null(item.ContextTagsJson);
        Assert.Null(item.FocusTagsJson);
        Assert.Null(item.ContentFingerprint);
        Assert.Null(item.UpdatedAt);
    }

    [Fact]
    public void Reconstitute_PreservesIdAndCreatedAt()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var item = ResourceBankItem.Reconstitute(
            id, createdAt, PublishedResourceType.Grammar, ValidSourceId, "B1", SampleContentJson,
            subskill: null, difficultyBand: null, contextTagsJson: null, focusTagsJson: null,
            contentFingerprint: null, updatedAt: null);

        Assert.Equal(id, item.Id);
        Assert.Equal(createdAt, item.CreatedAt);
    }
}
