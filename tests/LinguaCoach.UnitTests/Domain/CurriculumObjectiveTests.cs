using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CurriculumObjectiveTests
{
    // ── Valid construction ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidInput_CreatesObjective()
    {
        var obj = new CurriculumObjective(
            key: "a1.speaking.greetings",
            title: "Greetings",
            description: "Basic greetings",
            cefrLevel: "A1",
            primarySkill: "speaking");

        Assert.Equal("a1.speaking.greetings", obj.Key);
        Assert.Equal("A1", obj.CefrLevel);
        Assert.Equal("speaking", obj.PrimarySkill);
        Assert.True(obj.IsActive);
        Assert.False(obj.IsReviewable);
        Assert.False(obj.IsExamInspired);
        Assert.Equal(1, obj.DifficultyBand);
        Assert.Equal(0, obj.RecommendedOrder);
    }

    [Fact]
    public void Constructor_CefrLevelNormalisedToUpperCase()
    {
        var obj = new CurriculumObjective("k", "T", "D", "b1", "speaking");
        Assert.Equal("B1", obj.CefrLevel);
    }

    [Fact]
    public void Constructor_PrimarySkillNormalisedToLowerCase()
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "WRITING");
        Assert.Equal("writing", obj.PrimarySkill);
    }

    // ── Validation — empty key ───────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_Throws(string key)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new CurriculumObjective(key, "T", "D", "A1", "speaking"));
        Assert.Contains("key", ex.ParamName, StringComparison.OrdinalIgnoreCase);
    }

    // ── Validation — invalid CEFR ────────────────────────────────────────────

    [Theory]
    [InlineData("X1")]
    [InlineData("b3")]
    [InlineData("")]
    [InlineData("A1B")]
    public void Constructor_InvalidCefr_Throws(string cefr)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new CurriculumObjective("key", "T", "D", cefr, "speaking"));
        Assert.Contains("cefrLevel", ex.ParamName);
    }

    // ── Validation — invalid skill ───────────────────────────────────────────

    [Theory]
    [InlineData("unknown_skill")]
    [InlineData("")]
    [InlineData("BADSKILL")]
    public void Constructor_InvalidSkill_Throws(string skill)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new CurriculumObjective("key", "T", "D", "A1", skill));
        Assert.Contains("primarySkill", ex.ParamName);
    }

    // ── Validation — difficulty band ────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Constructor_InvalidDifficultyBand_Throws(int band)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CurriculumObjective("k", "T", "D", "A1", "speaking",
                difficultyBand: band));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_ValidDifficultyBand_Accepted(int band)
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking",
            difficultyBand: band);
        Assert.Equal(band, obj.DifficultyBand);
    }

    // ── Validation — self-prerequisite ──────────────────────────────────────

    [Fact]
    public void Constructor_SelfPrerequisite_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new CurriculumObjective(
                key: "a1.speaking.test",
                title: "T", description: "D", cefrLevel: "A1", primarySkill: "speaking",
                prerequisiteKeysJson: """["a1.speaking.test"]"""));
        Assert.Contains("prerequisite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ValidPrerequisite_Accepted()
    {
        var obj = new CurriculumObjective(
            key: "a1.speaking.test",
            title: "T", description: "D", cefrLevel: "A1", primarySkill: "speaking",
            prerequisiteKeysJson: """["a1.speaking.other"]""");
        Assert.Equal("""["a1.speaking.other"]""", obj.PrerequisiteKeysJson);
    }

    // ── Activate / Deactivate ────────────────────────────────────────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking");
        obj.Deactivate();
        Assert.False(obj.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking", isActive: false);
        obj.Activate();
        Assert.True(obj.IsActive);
    }

    // ── UpdateDetails ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateDetails_ValidInput_UpdatesFields()
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking");
        obj.UpdateDetails("New Title", "New Desc", 5, 3, "Some notes");

        Assert.Equal("New Title", obj.Title);
        Assert.Equal("New Desc", obj.Description);
        Assert.Equal(5, obj.RecommendedOrder);
        Assert.Equal(3, obj.DifficultyBand);
        Assert.Equal("Some notes", obj.TeachingNotes);
    }

    [Fact]
    public void UpdateDetails_EmptyTitle_Throws()
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking");
        Assert.Throws<ArgumentException>(() => obj.UpdateDetails("", "D", 0, 1, null));
    }

    // ── CefrLevelConstants ───────────────────────────────────────────────────

    [Theory]
    [InlineData("A1", true)]
    [InlineData("A2", true)]
    [InlineData("B1", true)]
    [InlineData("B2", true)]
    [InlineData("C1", true)]
    [InlineData("C2", true)]
    [InlineData("a1", true)]   // case-insensitive
    [InlineData("X1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CefrLevelConstants_IsValid(string? level, bool expected)
    {
        Assert.Equal(expected, CefrLevelConstants.IsValid(level));
    }

    // ── CurriculumSkillConstants ─────────────────────────────────────────────

    [Theory]
    [InlineData("writing", true)]
    [InlineData("WRITING", true)]
    [InlineData("grammar", true)]
    [InlineData("fluency", true)]
    [InlineData("badskill", false)]
    [InlineData(null, false)]
    public void CurriculumSkillConstants_IsValid(string? skill, bool expected)
    {
        Assert.Equal(expected, CurriculumSkillConstants.IsValid(skill));
    }

    // ── CurriculumContextTagConstants ────────────────────────────────────────

    [Theory]
    [InlineData("general_english", true)]
    [InlineData("workplace", true)]
    [InlineData("travel", true)]
    [InlineData("exam_inspired", true)]
    [InlineData("bad_tag", false)]
    [InlineData(null, false)]
    public void CurriculumContextTagConstants_IsValid(string? tag, bool expected)
    {
        Assert.Equal(expected, CurriculumContextTagConstants.IsValid(tag));
    }
}
