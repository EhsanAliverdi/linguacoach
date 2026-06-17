using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.UnitTests.Application;

public sealed class CurriculumContextMapperTests
{
    // ── Null input fallback ──────────────────────────────────────────────────

    [Fact]
    public void NullContext_ReturnsGeneralEnglishFallback()
    {
        var tags = CurriculumContextMapper.MapFromResolvedContext(null);
        Assert.Contains(CurriculumContextTagConstants.GeneralEnglish, tags);
        Assert.Single(tags);
    }

    // ── Non-workplace context ────────────────────────────────────────────────

    [Fact]
    public void NonWorkplace_ReturnsGeneralEnglish_NotWorkplace()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.GeneralEnglish, tags);
        Assert.DoesNotContain(CurriculumContextTagConstants.Workplace, tags);
    }

    // ── Workplace context ────────────────────────────────────────────────────

    [Fact]
    public void WorkplaceContext_ReturnsWorkplaceTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = true,
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.Workplace, tags);
        Assert.DoesNotContain(CurriculumContextTagConstants.GeneralEnglish, tags);
    }

    // ── Travel goal key ──────────────────────────────────────────────────────

    [Fact]
    public void TravelGoalKey_ReturnsTravelTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            PrimaryGoalKey = "travel",
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.Travel, tags);
    }

    // ── Job interview goal key ───────────────────────────────────────────────

    [Fact]
    public void InterviewGoalKey_ReturnsJobInterviewsTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            PrimaryGoalKey = "job_interviews",
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.JobInterviews, tags);
    }

    // ── Social conversation goal key ─────────────────────────────────────────

    [Fact]
    public void SocialConversationGoalKey_ReturnsSocialTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            PrimaryGoalKey = "social_conversation",
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.SocialConversation, tags);
    }

    // ── Pronunciation focus area ─────────────────────────────────────────────

    [Fact]
    public void PronunciationFocusArea_ReturnsPronunciationTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            FocusAreaKeys = "pronunciation,fluency",
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.Pronunciation, tags);
    }

    // ── Listening focus area ─────────────────────────────────────────────────

    [Fact]
    public void ListeningFocusArea_ReturnsListeningConfidenceTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            FocusAreaKeys = "listening",
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.ListeningConfidence, tags);
    }

    // ── Writing focus area ───────────────────────────────────────────────────

    [Fact]
    public void WritingFocusArea_ReturnsWritingConfidenceTag()
    {
        var ctx = new ResolvedLearningGoalContext
        {
            WorkplaceSpecific = false,
            FocusAreaKeys = "writing",
            Source = "Structured"
        };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        Assert.Contains(CurriculumContextTagConstants.WritingConfidence, tags);
    }

    // ── Generic fallback — never defaults to workplace only ──────────────────

    [Fact]
    public void EmptyContext_NeverDefaultsToWorkplaceOnly()
    {
        var ctx = new ResolvedLearningGoalContext { Source = "Fallback" };
        var tags = CurriculumContextMapper.MapFromResolvedContext(ctx);
        // Must not return only workplace with no explicit workplace flag.
        if (tags.Contains(CurriculumContextTagConstants.Workplace))
            Assert.Contains(CurriculumContextTagConstants.GeneralEnglish, tags);
    }

    // ── Result is never empty ────────────────────────────────────────────────

    [Fact]
    public void Result_IsNeverEmpty()
    {
        var tags = CurriculumContextMapper.MapFromResolvedContext(new ResolvedLearningGoalContext());
        Assert.NotEmpty(tags);
    }
}
