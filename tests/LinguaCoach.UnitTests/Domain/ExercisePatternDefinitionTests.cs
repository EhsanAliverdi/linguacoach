using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class ExercisePatternDefinitionTests
{
    private static ExercisePatternDefinition Make(
        string key = "test_pattern",
        string name = "Test Pattern",
        string primarySkill = "Vocabulary",
        ActivityType activityType = ActivityType.VocabularyPractice,
        InteractionMode interactionMode = InteractionMode.MatchingPairs,
        MarkingMode markingMode = MarkingMode.KeyedSelection,
        int estimatedMinutes = 3)
        => new(
            key: key, name: name, primarySkill: primarySkill,
            secondarySkillsJson: "[]", compatibleKindsJson: "[0]",
            activityType: activityType, interactionMode: interactionMode,
            markingMode: markingMode, estimatedMinutes: estimatedMinutes,
            aiGeneratePromptKey: "activity_generate_test",
            aiEvaluatePromptKey: "activity_evaluate_test",
            teachingPurpose: "Test purpose");

    [Fact]
    public void Constructor_SetsAllPropertiesCorrectly()
    {
        var pattern = Make();
        Assert.Equal("test_pattern", pattern.Key);
        Assert.Equal("Test Pattern", pattern.Name);
        Assert.Equal("Vocabulary", pattern.PrimarySkill);
        Assert.Equal(ActivityType.VocabularyPractice, pattern.ActivityType);
        Assert.Equal(InteractionMode.MatchingPairs, pattern.InteractionMode);
        Assert.Equal(MarkingMode.KeyedSelection, pattern.MarkingMode);
        Assert.Equal(3, pattern.EstimatedMinutes);
        Assert.True(pattern.IsActive);
        Assert.True(pattern.WorkplaceContext);
        Assert.False(pattern.RequiresAudio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsOnEmptyKey(string key)
        => Assert.Throws<ArgumentException>(() => Make(key: key));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsOnEmptyName(string name)
        => Assert.Throws<ArgumentException>(() => Make(name: name));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsOnEmptyPrimarySkill(string skill)
        => Assert.Throws<ArgumentException>(() => Make(primarySkill: skill));

    [Fact]
    public void Constructor_ThrowsOnZeroEstimatedMinutes()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Make(estimatedMinutes: 0));

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var pattern = Make();
        pattern.Deactivate();
        Assert.False(pattern.IsActive);
    }

    [Fact]
    public void Constructor_TrimsKey()
    {
        var p = new ExercisePatternDefinition(
            key: "  phrase_match  ", name: "N", primarySkill: "V",
            secondarySkillsJson: "[]", compatibleKindsJson: "[0]",
            activityType: ActivityType.VocabularyPractice,
            interactionMode: InteractionMode.MatchingPairs,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 2,
            aiGeneratePromptKey: "g", aiEvaluatePromptKey: "e",
            teachingPurpose: "t");
        Assert.Equal("phrase_match", p.Key);
    }

    [Fact]
    public void Constructor_NullSecondarySkills_DefaultsToEmptyArray()
    {
        var p = new ExercisePatternDefinition(
            key: "k", name: "N", primarySkill: "V",
            secondarySkillsJson: null!,
            compatibleKindsJson: null!,
            activityType: ActivityType.VocabularyPractice,
            interactionMode: InteractionMode.ReadOnly,
            markingMode: MarkingMode.NoMarking,
            estimatedMinutes: 2,
            aiGeneratePromptKey: "g", aiEvaluatePromptKey: "e",
            teachingPurpose: "t");
        Assert.Equal("[]", p.SecondarySkillsJson);
        Assert.Equal("[]", p.CompatibleKindsJson);
    }
}
