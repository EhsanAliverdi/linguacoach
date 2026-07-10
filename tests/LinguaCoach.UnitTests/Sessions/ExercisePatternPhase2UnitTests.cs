using LinguaCoach.Domain;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;

namespace LinguaCoach.UnitTests.Sessions;

/// <summary>
/// Unit tests for Phase 2: pattern-aware routing logic in ExercisePrepareHandler
/// and canonical key migration in SessionDurationTemplates.
/// No DB or DI needed.
/// </summary>
public sealed class ExercisePatternPhase2UnitTests
{
    // ── SessionDurationTemplates — canonical key migration ────────────────────

    [Fact]
    public void Template10Min_WritingStepUsesEmailReply()
    {
        var steps = SessionDurationTemplates.GetTemplate(10);
        var writingStep = steps.FirstOrDefault(s => s.Kind == ExerciseKind.WritingTask);
        Assert.NotNull(writingStep);
        Assert.Equal(ExercisePatternKey.EmailReply, writingStep.PatternKey);
    }

    [Fact]
    public void Template15Min_WritingStepUsesEmailReply()
    {
        var steps = SessionDurationTemplates.GetTemplate(15);
        var writingStep = steps.FirstOrDefault(s => s.Kind == ExerciseKind.WritingTask);
        Assert.NotNull(writingStep);
        Assert.Equal(ExercisePatternKey.EmailReply, writingStep.PatternKey);
    }

    [Fact]
    public void Template20Min_WritingStepUsesEmailReply()
    {
        var steps = SessionDurationTemplates.GetTemplate(20);
        var writingStep = steps.FirstOrDefault(s => s.Kind == ExerciseKind.WritingTask);
        Assert.NotNull(writingStep);
        Assert.Equal(ExercisePatternKey.EmailReply, writingStep.PatternKey);
    }

    [Fact]
    public void Template30Min_WritingStepUsesEmailReply()
    {
        var steps = SessionDurationTemplates.GetTemplate(30);
        var writingStep = steps.FirstOrDefault(s => s.Kind == ExerciseKind.WritingTask);
        Assert.NotNull(writingStep);
        Assert.Equal(ExercisePatternKey.EmailReply, writingStep.PatternKey);
    }

    [Fact]
    public void Template30Min_SpeakingStepUsesSpokenResponseFromPrompt()
    {
        var steps = SessionDurationTemplates.GetTemplate(30);
        var speakingStep = steps.FirstOrDefault(s => s.Kind == ExerciseKind.SpeakingTask);
        Assert.NotNull(speakingStep);
        Assert.Equal(ExercisePatternKey.SpokenResponseFromPrompt, speakingStep.PatternKey);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_NoLegacyWritingResponseKey(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        Assert.DoesNotContain(steps, s => s.PatternKey == "writing_response");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_NoLegacySpeakingRolePlayKey(int duration)
    {
        var steps = SessionDurationTemplates.GetTemplate(duration);
        Assert.DoesNotContain(steps, s => s.PatternKey == "speaking_role_play");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Template_AllPatternKeysMatchSeededPatternKeys(int duration)
    {
        var seededKeys = new HashSet<string>
        {
            ExercisePatternKey.PhraseMatch,
            ExercisePatternKey.GapFillWorkplacePhrase,
            ExercisePatternKey.ListenAndAnswer,
            ExercisePatternKey.ListenAndGapFill,
            ExercisePatternKey.EmailReply,
            ExercisePatternKey.TeamsChatSimulation,
            ExercisePatternKey.SpokenResponseFromPrompt,
            ExercisePatternKey.LessonReflection,
        };

        var steps = SessionDurationTemplates.GetTemplate(duration);
        foreach (var step in steps)
        {
            Assert.Contains(step.PatternKey, seededKeys);
        }
    }

    // Phase I2B — the ExercisePrepareHandler.MapKindToActivityType coverage that used to live here
    // was removed along with ExercisePrepareHandler itself (deleted with the legacy generation
    // pipeline). See docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
}
