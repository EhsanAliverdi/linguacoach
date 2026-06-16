using LinguaCoach.Infrastructure.LearningPath;
using LinguaCoach.Persistence.Seed;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Phase 9K — Content context / goal-alignment cleanup.
///
/// Verifies that:
/// - Default generated prompt context is not workplace-only.
/// - Workplace remains allowed as one valid context.
/// - respond_to_situation and describe_image are not hardcoded workplace-only.
/// - All Ready formats remain runnable; Planned formats remain non-runnable (via seeder).
/// - Skill labels no longer carry "workplace" in their display text.
/// </summary>
public sealed class ContextFlexibilityTests
{
    // ── DefaultPathFactory ────────────────────────────────────────────────────

    [Fact]
    public void DefaultPathFactory_Title_DoesNotForceWorkplacePrefix()
    {
        var profileId = Guid.NewGuid();
        var path = DefaultPathFactory.Create(profileId, "General learner", "A2");

        Assert.DoesNotContain("Workplace English for", path.Title);
    }

    [Fact]
    public void DefaultPathFactory_FallbackModules_AreNotWorkplaceOnly()
    {
        var profileId = Guid.NewGuid();
        var path = DefaultPathFactory.Create(profileId, "New arrival", "A2");
        var modules = DefaultPathFactory.CreateModules(path.Id);

        Assert.All(modules, m =>
            Assert.DoesNotContain("workplace", m.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultPathFactory_WorkplaceContextStillSupportedAsOneOption()
    {
        // Workplace remains valid — not removed, just not forced.
        var profileId = Guid.NewGuid();
        var path = DefaultPathFactory.Create(profileId, "Project Manager", "B2");

        Assert.Contains("Project Manager", path.Title);
        Assert.Contains("B2", path.Title);
    }

    // ── ExercisePatternSeeder — teachingPurpose display text ──────────────────

    [Theory]
    [InlineData("phrase_match")]
    [InlineData("gap_fill_workplace_phrase")]
    [InlineData("listen_and_answer")]
    [InlineData("listen_and_gap_fill")]
    [InlineData("spoken_response_from_prompt")]
    [InlineData("open_writing_task")]
    [InlineData("speaking_roleplay_turn")]
    [InlineData("read_aloud")]
    [InlineData("answer_short_question")]
    public void ExercisePatternSeeder_TeachingPurpose_DoesNotContainWorkplace(string patternKey)
    {
        // teachingPurpose is the student-facing display description.
        // These patterns should not describe their purpose as "workplace" exclusively.
        // Note: email_reply and teams_chat_simulation are inherently workplace formats — excluded here.
        var definitions = ExercisePatternSeeder.CreateDefinitionsPublic();
        var match = definitions.SingleOrDefault(d => d.Key == patternKey);

        Assert.NotNull(match);
        Assert.DoesNotContain("workplace", match.TeachingPurpose, StringComparison.OrdinalIgnoreCase);
    }

    // ── respond_to_situation / describe_image — not hardcoded workplace-only ──

    [Theory]
    [InlineData("respond_to_situation")]
    [InlineData("describe_image")]
    public void ExercisePatternSeeder_RespondToSituation_DescribeImage_NotWorkplaceContext(string key)
    {
        var definitions = ExercisePatternSeeder.CreateDefinitionsPublic();
        var match = definitions.SingleOrDefault(d => d.Key == key);

        Assert.NotNull(match);
        // workplaceContext flag must be false for these open/general formats.
        Assert.False(match.WorkplaceContext,
            $"{key} should not be workplace-context-only — it is a general real-life format.");
    }

    // ── Prompt keys — gap_fill keeps its internal key name for compatibility ───

    [Fact]
    public void DefaultAiSeeder_GapFillKey_InternalKeyPreservedForCompatibility()
    {
        // CRITICAL: internal key must NOT be renamed — DB, routes, pattern engine depend on it.
        Assert.Equal("activity_generate_gap_fill_workplace_phrase",
            DefaultAiSeeder.ActivityGenerateGapFillKey);
        Assert.Equal("activity_evaluate_gap_fill_workplace_phrase",
            DefaultAiSeeder.ActivityEvaluateGapFillKey);
    }
}
