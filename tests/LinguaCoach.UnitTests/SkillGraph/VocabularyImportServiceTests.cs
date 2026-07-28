using LinguaCoach.Infrastructure.SkillGraph;

namespace LinguaCoach.UnitTests.SkillGraph;

public sealed class VocabularyImportServiceTests
{
    private readonly VocabularyImportService _sut = new();

    private const string CefrJSample =
        "headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold\n" +
        "actor,noun,A1,Work and Jobs,Film,Personal identification\n" +
        "abandon,verb,B1,,,\n" +
        "about,adverb,A1,,,\n" +
        "about,preposition,A1,,,\n" +
        "accountant,noun,B1,Work and jobs,,Personal identification\n";

    private const string OctanoveSample =
        "headword,pos,CEFR,notes\n" +
        "exterior,noun,C1,\n" +
        "cloak,noun,C1,\n";

    [Fact]
    public void ParseCsvFiles_RowsWithCategory_GroupIntoContainers()
    {
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        Assert.Single(preview.CategorizedContainers); // "Work and Jobs" only, after alias normalization
        var container = preview.CategorizedContainers[0];
        Assert.Equal("Work and Jobs", container.Title);
        Assert.Equal(2, container.Leaves.Count); // actor + accountant
    }

    [Fact]
    public void ParseCsvFiles_NormalizesCaseVariantCategoryNames()
    {
        // "Work and Jobs" (actor) and "Work and jobs" (accountant) must collapse to one container.
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        var container = Assert.Single(preview.CategorizedContainers);
        Assert.Contains(container.Leaves, l => l.Headword == "actor");
        Assert.Contains(container.Leaves, l => l.Headword == "accountant");
    }

    [Fact]
    public void ParseCsvFiles_RowsWithoutCategory_BecomeUncategorizedLeaves()
    {
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        // abandon, about (adverb), about (preposition) from CEFR-J + exterior, cloak from Octanove.
        Assert.Equal(5, preview.UncategorizedLeaves.Count);
        Assert.Contains(preview.UncategorizedLeaves, l => l.Headword == "exterior");
        Assert.Contains(preview.UncategorizedLeaves, l => l.Headword == "cloak");
    }

    [Fact]
    public void ParseCsvFiles_SameHeadwordDifferentPartOfSpeech_ProducesDistinctLeaves()
    {
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        var aboutLeaves = preview.UncategorizedLeaves.Where(l => l.Headword == "about").ToList();
        Assert.Equal(2, aboutLeaves.Count);
        Assert.Contains(aboutLeaves, l => l.PartOfSpeech == "adverb");
        Assert.Contains(aboutLeaves, l => l.PartOfSpeech == "preposition");
        Assert.NotEqual(aboutLeaves[0].Key, aboutLeaves[1].Key);
    }

    [Fact]
    public void ParseCsvFiles_ContainerCefrLevel_IsTheEasiestLeafLevel()
    {
        var csv =
            "headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold\n" +
            "word1,noun,B1,Education,,\n" +
            "word2,noun,A2,Education,,\n" +
            "word3,noun,B2,Education,,\n";

        var preview = _sut.ParseCsvFiles(csv, "headword,pos,CEFR,notes\n");

        var container = Assert.Single(preview.CategorizedContainers);
        Assert.Equal("A2", container.CefrLevel); // easiest of B1/A2/B2
    }

    [Fact]
    public void ParseCsvFiles_OctanoveRows_NeverHaveACategory()
    {
        var preview = _sut.ParseCsvFiles("headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold\n", OctanoveSample);

        Assert.Empty(preview.CategorizedContainers);
        Assert.Equal(2, preview.UncategorizedLeaves.Count);
    }

    [Fact]
    public void ParseCsvFiles_InvalidCefrLevel_RowSkippedWithWarning()
    {
        var csv = "headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold\nbadword,noun,Z9,,,\n";

        var preview = _sut.ParseCsvFiles(csv, "headword,pos,CEFR,notes\n");

        Assert.Equal(0, preview.TotalLeafCount);
        Assert.Contains(preview.Warnings, w => w.Contains("badword"));
    }

    [Fact]
    public void ParseCsvFiles_LeafDescription_IsADeterministicPlaceholderMentioningTheWord()
    {
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        var leaf = preview.UncategorizedLeaves.First(l => l.Headword == "abandon");
        Assert.Contains("abandon", leaf.Description);
        Assert.Contains("pending AI pass", leaf.Description);
    }

    [Fact]
    public void ParseCsvFiles_EmptyFiles_ReturnsEmptyPreview()
    {
        var preview = _sut.ParseCsvFiles(
            "headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold\n",
            "headword,pos,CEFR,notes\n");

        Assert.Empty(preview.CategorizedContainers);
        Assert.Empty(preview.UncategorizedLeaves);
        Assert.Equal(0, preview.TotalLeafCount);
    }

    [Fact]
    public void ParseCsvFiles_TotalLeafCount_CountsBothCategorizedAndUncategorized()
    {
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        Assert.Equal(7, preview.TotalLeafCount); // 2 categorized (actor, accountant) + 5 uncategorized
    }

    [Fact]
    public void ParseCsvFiles_LeafKeys_AreUniqueAcrossBothFiles()
    {
        var preview = _sut.ParseCsvFiles(CefrJSample, OctanoveSample);

        var allKeys = preview.CategorizedContainers.SelectMany(c => c.Leaves).Select(l => l.Key)
            .Concat(preview.UncategorizedLeaves.Select(l => l.Key))
            .ToList();

        Assert.Equal(allKeys.Count, allKeys.Distinct().Count());
    }

    [Fact]
    public void ParseCsvFiles_UsesQuotedCommaFieldCorrectly()
    {
        var csv = "headword,pos,CEFR,CoreInventory 1,CoreInventory 2,Threshold\n" +
                   "accident,noun,A2,\"News, lifestyles and current affairs\",,\n";

        var preview = _sut.ParseCsvFiles(csv, "headword,pos,CEFR,notes\n");

        var container = Assert.Single(preview.CategorizedContainers);
        Assert.Equal("News, lifestyles and current affairs", container.Title);
    }
}
