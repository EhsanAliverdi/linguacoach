using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.SkillGraph;

namespace LinguaCoach.UnitTests.SkillGraph;

public sealed class CefrJGrammarImportServiceTests
{
    private readonly CefrJGrammarImportService _sut = new();

    private const string SampleCsv =
        "ID,Shorthand Code,Grammatical Item,Sentence Type,CEFR-J Level,FREQ*DISP,Core Inventory,EGP,GSELO,Notes\n" +
        "1,PP.I_am,I am,AFF. DEC.,A1.1,A1,A1,A1,A1,\n" +
        "1-1,PP.I_am_not,I am not,NEG. DEC.,A1.1,A1,A1,\"A1-A2, C1\",A1,\n" +
        "1-2,PP.am_I,Am I ...?,AFF. INT.,,,A1,A1-A2,A1,note\n" +
        "1-3,PP.am_I_not,Am I not ...?,NEG. INT.,,,A1,N/A,A1,note\n" +
        "13,DT.a.an,INDEFINITE ARTICLES,,A1.1,A1,A1-B2,A1,A1,\n";

    [Fact]
    public void ParseAndProposeMapping_GroupsHyphenatedRowsUnderOneContainer()
    {
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);

        Assert.Single(preview.Containers);
        var family = preview.Containers[0];
        Assert.Equal(4, family.Leaves.Count); // base row 1 + 1-1, 1-2, 1-3
        Assert.Contains(family.Leaves, l => l.SourceRowId == "1");
        Assert.Contains(family.Leaves, l => l.SourceRowId == "1-1");
        Assert.Contains(family.Leaves, l => l.SourceRowId == "1-2");
        Assert.Contains(family.Leaves, l => l.SourceRowId == "1-3");
    }

    [Fact]
    public void ParseAndProposeMapping_RowWithoutHyphenatedChildren_BecomesStandaloneLeaf()
    {
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);

        Assert.Single(preview.StandaloneLeaves);
        Assert.Equal("13", preview.StandaloneLeaves[0].SourceRowId);
        Assert.Equal("INDEFINITE ARTICLES", preview.StandaloneLeaves[0].Title);
    }

    [Fact]
    public void ParseAndProposeMapping_UsesQuotedCommaFieldCorrectly()
    {
        // Row 1-1's EGP field is a quoted "A1-A2, C1" — must not be split into extra columns.
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);
        var negLeaf = preview.Containers[0].Leaves.Single(l => l.SourceRowId == "1-1");

        Assert.Equal("I am not", negLeaf.Title);
        Assert.Equal("A1", negLeaf.CefrLevel);
    }

    [Fact]
    public void ParseAndProposeMapping_BlankCefrJLevel_FallsBackToCoreInventory()
    {
        // Row 1-2 has a blank CEFR-J Level column but Core Inventory = "A1".
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);
        var questionLeaf = preview.Containers[0].Leaves.Single(l => l.SourceRowId == "1-2");

        Assert.Equal("A1", questionLeaf.CefrLevel);
        Assert.DoesNotContain(preview.Warnings, w => w.Contains("1-2"));
        // Phase GSG-1 — still real evidence (a genuine fallback column), so it's not a review-screen
        // warning, but it must be persisted as Fallback confidence, not silently treated as Attested.
        Assert.Equal(CefrConfidence.Fallback, questionLeaf.Confidence);
        Assert.Equal("coreInventory", questionLeaf.Source);
    }

    [Fact]
    public void ParseAndProposeMapping_CefrJLevelPresent_IsAttestedConfidence()
    {
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);
        var baseLeaf = preview.Containers[0].Leaves.Single(l => l.SourceRowId == "1");

        Assert.Equal(CefrConfidence.Attested, baseLeaf.Confidence);
        Assert.Equal("cefrj", baseLeaf.Source);
    }

    [Fact]
    public void ParseAndProposeMapping_SubLevelSuffix_DrivesDifficultyBand()
    {
        var csv =
            "ID,Shorthand Code,Grammatical Item,Sentence Type,CEFR-J Level,FREQ*DISP,Core Inventory,EGP,GSELO,Notes\n" +
            "50,X.item,Some item,,B1.2,A1,B1,B1,B1,\n";

        var preview = _sut.ParseAndProposeMapping(csv, existingGrammarNodes: []);

        var leaf = Assert.Single(preview.StandaloneLeaves);
        Assert.Equal("B1", leaf.CefrLevel);
        Assert.Equal(2, leaf.DifficultyBand);
    }

    [Fact]
    public void ParseAndProposeMapping_NoUsableLevelColumn_DefaultsToA1AndWarns()
    {
        var csv =
            "ID,Shorthand Code,Grammatical Item,Sentence Type,CEFR-J Level,FREQ*DISP,Core Inventory,EGP,GSELO,Notes\n" +
            "99,X.mystery,Mystery item,,,,,,,\n";

        var preview = _sut.ParseAndProposeMapping(csv, existingGrammarNodes: []);

        var leaf = Assert.Single(preview.StandaloneLeaves);
        Assert.Equal("A1", leaf.CefrLevel);
        Assert.Contains(preview.Warnings, w => w.Contains("99") && w.Contains("A1"));
        Assert.Equal(CefrConfidence.Fallback, leaf.Confidence);
        Assert.Equal("defaulted", leaf.Source);
    }

    [Fact]
    public void ParseAndProposeMapping_StrongTitleMatch_ProposesExistingContainerInsteadOfNew()
    {
        var existing = new[]
        {
            new CefrJExistingGrammarNodeCandidate(Guid.NewGuid(), "grammar.verb_to_be_affirmative.a1", "I am (all forms)", "A1"),
        };

        var preview = _sut.ParseAndProposeMapping(SampleCsv, existing);

        var family = preview.Containers[0];
        Assert.Equal(existing[0].Id, family.MatchedExistingNodeId);
        Assert.Equal(existing[0].Key, family.MatchedExistingNodeKey);
        Assert.NotNull(family.MatchConfidence);
    }

    [Fact]
    public void ParseAndProposeMapping_NoGoodTitleMatch_ProposesNewContainer()
    {
        var existing = new[]
        {
            new CefrJExistingGrammarNodeCandidate(Guid.NewGuid(), "grammar.past_continuous.b1", "Past continuous for interrupted actions", "B1"),
        };

        var preview = _sut.ParseAndProposeMapping(SampleCsv, existing);

        var family = preview.Containers[0];
        Assert.Null(family.MatchedExistingNodeId);
        Assert.Null(family.MatchedExistingNodeKey);
        Assert.False(string.IsNullOrWhiteSpace(family.Key));
        Assert.False(string.IsNullOrWhiteSpace(family.Title));
    }

    [Fact]
    public void ParseAndProposeMapping_EmptyCsv_ReturnsEmptyPreview()
    {
        var preview = _sut.ParseAndProposeMapping(
            "ID,Shorthand Code,Grammatical Item,Sentence Type,CEFR-J Level,FREQ*DISP,Core Inventory,EGP,GSELO,Notes\n",
            existingGrammarNodes: []);

        Assert.Empty(preview.Containers);
        Assert.Empty(preview.StandaloneLeaves);
        Assert.Equal(0, preview.TotalLeafCount);
    }

    [Fact]
    public void ParseAndProposeMapping_TotalLeafCount_CountsFamilyLeavesAndStandaloneLeaves()
    {
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);

        Assert.Equal(5, preview.TotalLeafCount); // 4 family leaves + 1 standalone
    }

    [Fact]
    public void ParseAndProposeMapping_LeafKeys_AreUniquePerRow()
    {
        var preview = _sut.ParseAndProposeMapping(SampleCsv, existingGrammarNodes: []);

        var allKeys = preview.Containers.SelectMany(c => c.Leaves).Select(l => l.Key)
            .Concat(preview.StandaloneLeaves.Select(l => l.Key))
            .ToList();

        Assert.Equal(allKeys.Count, allKeys.Distinct().Count());
    }
}
