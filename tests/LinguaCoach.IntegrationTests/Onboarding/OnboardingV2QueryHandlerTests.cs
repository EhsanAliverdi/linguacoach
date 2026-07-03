using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Onboarding;

/// <summary>
/// Regression tests for the 2026-07-03 onboarding-gate fix: a v1-complete student profile
/// missing the newer required preference fields (learning_goals, focus_areas,
/// support_language — added after v1 onboarding existed) must not be lazily marked as
/// v2-complete. See docs/reviews/2026-07-03-pilot-student-onboarding-placement-practice-live-audit.md.
/// </summary>
public sealed class OnboardingV2QueryHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly OnboardingV2QueryHandler _handler;

    public OnboardingV2QueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        OnboardingFlowSeeder.SeedAsync(_db).GetAwaiter().GetResult();

        _handler = new OnboardingV2QueryHandler(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<StudentProfile> SeedV1CompleteProfileAsync(
        Guid userId,
        IReadOnlyList<string>? learningGoals = null,
        IReadOnlyList<string>? focusAreas = null,
        string? supportLanguageCode = null,
        TranslationHelpPreference? translationHelpPreference = null)
    {
        var profile = new StudentProfile(userId);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // Set OnboardingStatus directly rather than driving the legacy v1 step state machine
        // (Language → Preference → Career → Skill) — this test only cares about the resulting
        // profile state (v1-complete, with or without the newer preference fields), matching
        // real legacy-complete accounts whose v1 step history predates this schema.
        _db.Entry(profile).Property("OnboardingStatus").CurrentValue = OnboardingStatus.Complete;

        if (learningGoals is not null || focusAreas is not null || supportLanguageCode is not null || translationHelpPreference is not null)
        {
            profile.UpdateLearningPreferences(
                preferredName: null,
                supportLanguageCode: supportLanguageCode,
                supportLanguageName: supportLanguageCode is null ? null : "Test Language",
                translationHelpPreference: translationHelpPreference,
                learningGoals: learningGoals,
                customLearningGoal: null,
                focusAreas: focusAreas,
                customFocusArea: null,
                difficultyPreference: null,
                preferredSessionDurationMinutes: null);
        }

        await _db.SaveChangesAsync();
        return profile;
    }

    [Fact]
    public async Task HandleAsync_V1CompleteStudent_MissingAllPreferenceFields_IsNotMarkedComplete()
    {
        var userId = Guid.NewGuid();
        await SeedV1CompleteProfileAsync(userId);

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        Assert.False(result.IsComplete);
        Assert.Equal("support_language", result.CurrentStepKey);
    }

    [Fact]
    public async Task HandleAsync_V1CompleteStudent_MissingOnlyFocusAreas_RoutesToFocusAreasStep()
    {
        var userId = Guid.NewGuid();
        await SeedV1CompleteProfileAsync(
            userId,
            learningGoals: ["day_to_day"],
            focusAreas: null,
            supportLanguageCode: "es",
            translationHelpPreference: TranslationHelpPreference.WhenDifficult);

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        Assert.False(result.IsComplete);
        Assert.Equal("focus_areas", result.CurrentStepKey);
    }

    [Fact]
    public async Task HandleAsync_V1CompleteStudent_WithAllPreferenceFieldsSet_IsMarkedComplete()
    {
        var userId = Guid.NewGuid();
        await SeedV1CompleteProfileAsync(
            userId,
            learningGoals: ["day_to_day"],
            focusAreas: ["speaking"],
            supportLanguageCode: "es",
            translationHelpPreference: TranslationHelpPreference.WhenDifficult);

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task HandleAsync_V1CompleteStudent_TranslationHelpNeverWithNoLanguageCode_CountsAsAnswered()
    {
        // TranslationHelpPreference.Never is itself an explicit answer to the support-language
        // step even without a specific SupportLanguageCode — must not be treated as unanswered.
        var userId = Guid.NewGuid();
        await SeedV1CompleteProfileAsync(
            userId,
            learningGoals: ["day_to_day"],
            focusAreas: ["speaking"],
            supportLanguageCode: null,
            translationHelpPreference: TranslationHelpPreference.Never);

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        Assert.True(result.IsComplete);
    }

    // ── Unified Question-Schema Phase 5/6: Content exposure ─────────────────

    [Fact]
    public async Task HandleAsync_GenericStepTypes_HaveContentPopulated()
    {
        var userId = Guid.NewGuid();
        var profile = new StudentProfile(userId);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        var assessmentIntro = result.Steps.First(s => s.StepKey == "assessment_intro");
        Assert.NotNull(assessmentIntro.Content);

        var assessmentQ1 = result.Steps.First(s => s.StepKey == "assessment_q1");
        Assert.NotNull(assessmentQ1.Content);
    }

    [Fact]
    public async Task HandleAsync_AssessmentQuestionContent_NeverExposesCorrectAnswerKey()
    {
        var userId = Guid.NewGuid();
        var profile = new StudentProfile(userId);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        var assessmentQ1 = result.Steps.First(s => s.StepKey == "assessment_q1");
        var content = Assert.IsType<Domain.Questions.SingleChoiceQuestion>(assessmentQ1.Content);
        Assert.Null(content.CorrectAnswerKey);
    }

    [Fact]
    public async Task HandleAsync_InfoStepTypes_HaveNoContent()
    {
        // Phase 6b: Welcome/Summary are non-question "Info" steps — everything else (including
        // support_language, now a generic SingleChoice with dynamically-resolved choices) has Content.
        var userId = Guid.NewGuid();
        var profile = new StudentProfile(userId);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        var welcome = result.Steps.First(s => s.StepKey == "welcome");
        Assert.Null(welcome.Content);
    }

    [Fact]
    public async Task HandleAsync_SupportLanguageStep_HasDynamicallyResolvedChoices()
    {
        var userId = Guid.NewGuid();
        var profile = new StudentProfile(userId);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetOnboardingV2Query(userId));

        var supportLanguage = result.Steps.First(s => s.StepKey == "support_language");
        var content = Assert.IsType<Domain.Questions.SingleChoiceQuestion>(supportLanguage.Content);
        Assert.Contains(content.Choices, c => c.Key == "none");
    }
}
