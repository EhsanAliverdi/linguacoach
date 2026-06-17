using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Curriculum;

/// <summary>
/// Unit tests for Phase 10Q CurriculumObjective domain additions:
/// AdminUpdate, ExamplePrompts, AdminUpdatedAt, and seeder-safe UpdateDetails.
/// </summary>
public sealed class AdminCurriculumObjectiveUnitTests
{
    private static CurriculumObjective Valid(string key = "b1.writing.emails") =>
        new(key, "Title", "Description", "B1", "writing");

    // ── AdminUpdate: valid full update ─────────────────────────────────────

    [Fact]
    public void AdminUpdate_ValidInput_UpdatesAllFields()
    {
        var obj = Valid();
        obj.AdminUpdate(
            title: "New Title",
            description: "New Description",
            cefrLevel: "B2",
            primarySkill: "reading",
            secondarySkillsJson: """["writing"]""",
            contextTagsJson: """["general_english"]""",
            focusTagsJson: """["reading_foundation"]""",
            prerequisiteKeysJson: "[]",
            recommendedOrder: 50,
            difficultyBand: 3,
            isReviewable: true,
            isExamInspired: true,
            teachingNotes: "Some notes",
            examplePrompts: "Example prompt");

        Assert.Equal("New Title", obj.Title);
        Assert.Equal("B2", obj.CefrLevel);
        Assert.Equal("reading", obj.PrimarySkill);
        Assert.Equal(50, obj.RecommendedOrder);
        Assert.Equal(3, obj.DifficultyBand);
        Assert.True(obj.IsReviewable);
        Assert.True(obj.IsExamInspired);
        Assert.Equal("Some notes", obj.TeachingNotes);
        Assert.Equal("Example prompt", obj.ExamplePrompts);
        Assert.NotNull(obj.AdminUpdatedAt);
    }

    [Fact]
    public void AdminUpdate_SetsAdminUpdatedAt()
    {
        var obj = Valid();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        obj.AdminUpdate("T", "D", "A1", "speaking", "[]", "[]", "[]", "[]", 0, 1, false, false, null, null);
        Assert.True(obj.AdminUpdatedAt >= before);
    }

    [Fact]
    public void AdminUpdate_InvalidCefr_Throws()
    {
        var obj = Valid();
        Assert.Throws<ArgumentException>(() =>
            obj.AdminUpdate("T", "D", "Z9", "speaking", "[]", "[]", "[]", "[]", 0, 1, false, false, null, null));
    }

    [Fact]
    public void AdminUpdate_InvalidSkill_Throws()
    {
        var obj = Valid();
        Assert.Throws<ArgumentException>(() =>
            obj.AdminUpdate("T", "D", "A1", "flying", "[]", "[]", "[]", "[]", 0, 1, false, false, null, null));
    }

    [Fact]
    public void AdminUpdate_DifficultyBandOutOfRange_Throws()
    {
        var obj = Valid();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            obj.AdminUpdate("T", "D", "A1", "speaking", "[]", "[]", "[]", "[]", 0, 6, false, false, null, null));
    }

    [Fact]
    public void AdminUpdate_SelfPrerequisite_Throws()
    {
        var obj = Valid("b1.writing.emails");
        Assert.Throws<ArgumentException>(() =>
            obj.AdminUpdate("T", "D", "B1", "writing", "[]", "[]", "[]",
                """["b1.writing.emails"]""", 0, 1, false, false, null, null));
    }

    // ── ExamplePrompts field ───────────────────────────────────────────────

    [Fact]
    public void Constructor_ExamplePrompts_StoredTrimmed()
    {
        var obj = new CurriculumObjective(
            "k", "T", "D", "A1", "speaking",
            examplePrompts: "  example  ");
        Assert.Equal("example", obj.ExamplePrompts);
    }

    [Fact]
    public void Constructor_ExamplePrompts_NullByDefault()
    {
        var obj = Valid();
        Assert.Null(obj.ExamplePrompts);
    }

    // ── AdminUpdatedAt default ─────────────────────────────────────────────

    [Fact]
    public void Constructor_AdminUpdatedAt_NullByDefault()
    {
        var obj = Valid();
        Assert.Null(obj.AdminUpdatedAt);
    }

    // ── Activate / Deactivate preserve other fields ───────────────────────

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking", isActive: false);
        obj.Activate();
        Assert.True(obj.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var obj = Valid();
        obj.Deactivate();
        Assert.False(obj.IsActive);
    }

    // ── UpdateDetails (seeder-safe) does not touch AdminUpdatedAt ─────────

    [Fact]
    public void UpdateDetails_DoesNotSetAdminUpdatedAt()
    {
        var obj = Valid();
        obj.UpdateDetails("New Title", "New Desc", 5, 2, null);
        Assert.Null(obj.AdminUpdatedAt);
    }

    // ── General_english allowed as context tag ────────────────────────────

    [Fact]
    public void Constructor_GeneralEnglishContextTag_IsValid()
    {
        Assert.True(CurriculumContextTagConstants.IsValid("general_english"));
    }

    // ── Workplace is not a default context ────────────────────────────────

    [Fact]
    public void Constructor_WorkplaceNotDefault_GeneralEnglishIsDefault()
    {
        Assert.Equal("general_english", CurriculumContextTagConstants.GeneralEnglish);
        Assert.NotEqual("general_english", CurriculumContextTagConstants.Workplace);
    }

    // ── DifficultyBand 1–5 ────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_ValidDifficultyBand_Accepted(int band)
    {
        var obj = new CurriculumObjective("k", "T", "D", "A1", "speaking", difficultyBand: band);
        Assert.Equal(band, obj.DifficultyBand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_InvalidDifficultyBand_Throws(int band)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CurriculumObjective("k", "T", "D", "A1", "speaking", difficultyBand: band));
    }

    // ── RecommendedOrder must be non-negative ─────────────────────────────

    [Fact]
    public void Constructor_NegativeRecommendedOrder_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CurriculumObjective("k", "T", "D", "A1", "speaking", recommendedOrder: -1));
    }
}
