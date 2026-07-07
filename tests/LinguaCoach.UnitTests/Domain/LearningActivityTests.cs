using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class LearningActivityTests
{
    private static LearningActivity Valid() => new(
        activityType: ActivityType.WritingScenario,
        source: ActivitySource.AiGenerated,
        title: "Follow up on pending approval",
        difficulty: "B1",
        aiGeneratedContentJson: """{"situation":"test"}""");

    [Fact]
    public void Constructor_WithValidArgs_CreatesActivity()
    {
        var act = Valid();
        act.ActivityType.Should().Be(ActivityType.WritingScenario);
        act.Source.Should().Be(ActivitySource.AiGenerated);
        act.Title.Should().Be("Follow up on pending approval");
        act.Difficulty.Should().Be("B1");
        act.IsActive.Should().BeTrue();
        act.SourceWritingScenarioId.Should().BeNull();
        act.LearningModuleId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithBlankTitle_Throws()
    {
        var fn = () => new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.AiGenerated, "  ", "B1", "{}");
        fn.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void Constructor_WithBlankDifficulty_Throws()
    {
        var fn = () => new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.AiGenerated, "Title", "", "{}");
        fn.Should().Throw<ArgumentException>().WithMessage("*Difficulty*");
    }

    [Fact]
    public void Constructor_WithNullContentJson_DefaultsToEmptyObject()
    {
        var act = new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.SystemFallback, "Title", "A2", null!);
        act.AiGeneratedContentJson.Should().Be("{}");
    }

    [Fact]
    public void Constructor_WithOptionalIds_SetsCorrectly()
    {
        var moduleId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        var act = new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.SystemFallback,
            "Title", "A2", "{}",
            learningModuleId: moduleId,
            sourceWritingScenarioId: scenarioId);

        act.LearningModuleId.Should().Be(moduleId);
        act.SourceWritingScenarioId.Should().Be(scenarioId);
    }

    [Fact]
    public void UpdateContent_ChangesJsonAndSetsSourceToAiGenerated()
    {
        var act = new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.SystemFallback, "Title", "B1", "{}");
        act.UpdateContent("""{"situation":"updated"}""");
        act.AiGeneratedContentJson.Should().Contain("updated");
        act.Source.Should().Be(ActivitySource.AiGenerated);
    }

    [Fact]
    public void UpdateContent_WithBlankJson_Throws()
    {
        var act = Valid();
        var fn = () => act.UpdateContent("  ");
        fn.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var act = Valid();
        act.Deactivate();
        act.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SetFormIoContent_SetsSchemaAndScoringRules()
    {
        var act = Valid();
        act.SetFormIoContent("""{"components":[]}""", """{"components":{}}""");

        act.FormIoSchemaJson.Should().Be("""{"components":[]}""");
        act.ScoringRulesJson.Should().Be("""{"components":{}}""");
    }

    [Fact]
    public void SetFormIoContent_NullScoringRules_Accepted()
    {
        var act = Valid();
        act.SetFormIoContent("""{"components":[]}""", null);

        act.ScoringRulesJson.Should().BeNull();
    }

    [Fact]
    public void SetFormIoContent_BlankSchema_Throws()
    {
        var act = Valid();
        var fn = () => act.SetFormIoContent("  ", null);
        fn.Should().Throw<ArgumentException>().WithMessage("*FormIoSchemaJson*");
    }
}
