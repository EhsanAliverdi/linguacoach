using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.Questions;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Onboarding;

/// <summary>
/// Unified Question-Schema Phase 6b — onboarding steps are now generic (SingleChoice/
/// MultipleChoice/FreeText) grouped into categories, submitted via the shared QuestionAnswer
/// wire format ({"answers":[{"questionId":"q1","values":[...]}]}), validated by the shared
/// IQuestionAnswerValidator. Answer semantics (which StudentProfile field a step writes) are
/// unchanged — still driven by AnswerMapping, now read from QuestionAnswer instead of ad-hoc
/// per-step-type JSON shapes.
/// </summary>
public sealed class OnboardingV2StepHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly OnboardingV2StepHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public OnboardingV2StepHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        OnboardingFlowSeeder.SeedAsync(_db).GetAwaiter().GetResult();

        _handler = new OnboardingV2StepHandler(_db, new QuestionAnswerValidator());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<Guid> SeedProfileAndProgressAsync()
    {
        var profile = new StudentProfile(_userId);
        _db.StudentProfiles.Add(profile);

        // support_language's choices are resolved dynamically from Languages (Phase 6b) — seed
        // one so tests submitting a real language code pass validation.
        if (!await _db.Languages.AnyAsync(l => l.Code == "es"))
            _db.Languages.Add(new Language("es", "Spanish", LanguageDirection.Ltr));

        var flow = await _db.OnboardingFlowDefinitions.FirstAsync(f => f.IsActive);
        var progress = new StudentOnboardingProgress(_userId, flow.Id, "welcome");
        _db.StudentOnboardingProgress.Add(progress);
        await _db.SaveChangesAsync();
        return profile.Id;
    }

    private static string Answer(string value) => MultiAnswer(value);

    private static string MultiAnswer(params string[] values)
    {
        var answer = new LinguaCoach.Domain.Questions.QuestionAnswer(
            [new LinguaCoach.Domain.Questions.QuestionAnswerItem("q1", values)]);
        return System.Text.Json.JsonSerializer.Serialize(answer);
    }

    private const string Skip = "{}";

    [Fact]
    public async Task SessionDuration_WritesPreferredSessionDurationMinutes()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "session_duration", Answer("20")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal(20, profile.PreferredSessionDurationMinutes);
    }

    [Fact]
    public async Task CareerContext_WritesCareerContextText()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "career_context", Answer("Nurse")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal("Nurse", profile.CareerContext);
    }

    [Fact]
    public async Task LearningGoalDescription_WritesLearningGoalDescription()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "learning_goal_description", Answer("I struggle in meetings.")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal("I struggle in meetings.", profile.LearningGoalDescription);
    }

    [Fact]
    public async Task ProfessionalExperienceLevelAndRoleFamiliarity_WriteIndependently()
    {
        // Phase 6b: work experience is two independent steps instead of one composite step.
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "professional_experience_level", Answer("MidLevel_2_5Years")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "role_familiarity", Answer("ExperiencedInRole")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal(ProfessionalExperienceLevel.MidLevel_2_5Years, profile.ProfessionalExperienceLevel);
        Assert.Equal(RoleFamiliarity.ExperiencedInRole, profile.RoleFamiliarity);
    }

    [Fact]
    public async Task CustomLearningGoalAndFocusArea_WriteIndependently()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "custom_learning_goal", Answer("Negotiating contracts")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "custom_focus_area", Answer("Small talk")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal("Negotiating contracts", profile.CustomLearningGoal);
        Assert.Equal("Small talk", profile.CustomFocusArea);
    }

    [Fact]
    public async Task AfterLearningGoals_WithoutWork_SkipsCareerContextAndWorkExperienceSteps()
    {
        await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "learning_goals", MultiAnswer("day_to_day", "travel")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "custom_learning_goal", Skip));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "focus_areas", MultiAnswer("speaking")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "custom_focus_area", Skip));
        var afterGoalDescription = await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "learning_goal_description", Skip));
        Assert.Equal("difficulty_preference", afterGoalDescription.CurrentStepKey);

        var afterDifficulty = await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "difficulty_preference", Answer("Balanced")));
        Assert.Equal("session_duration", afterDifficulty.CurrentStepKey);

        var afterDuration = await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "session_duration", Answer("15")));

        Assert.NotEqual("career_context", afterDuration.CurrentStepKey);
        Assert.NotEqual("professional_experience_level", afterDuration.CurrentStepKey);
        Assert.Equal("assessment_intro", afterDuration.CurrentStepKey);
    }

    [Fact]
    public async Task SequentialStepSubmissions_DoNotWipeEachOthersFields()
    {
        // Regression found live 2026-07-03: StudentProfile.UpdateLearningPreferences
        // unconditionally overwrites PreferredName/SupportLanguageCode/SupportLanguageName/
        // TranslationHelpPreference/CustomLearningGoal/CustomFocusArea/DifficultyPreference on
        // every call (unlike LearningGoals/FocusAreas/PreferredSessionDurationMinutes, which
        // skip the update when null). Every field set by an earlier step must survive every
        // later step's submission.
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "support_language", Answer("es")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "learning_goals", MultiAnswer("work")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "focus_areas", MultiAnswer("speaking")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "difficulty_preference", Answer("Balanced")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "session_duration", Answer("20")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal("es", profile.SupportLanguageCode);
        Assert.Equal(TranslationHelpPreference.WhenDifficult, profile.TranslationHelpPreference);
        Assert.Equal(DifficultyPreference.Balanced, profile.DifficultyPreference);
        Assert.Equal(20, profile.PreferredSessionDurationMinutes);
        Assert.Contains("work", profile.LearningGoals);
        Assert.Contains("speaking", profile.FocusAreas);
    }

    [Fact]
    public async Task SupportLanguage_NoneKey_ClearsLanguageAndSetsTranslationHelpNever()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "support_language", Answer("none")));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Null(profile.SupportLanguageCode);
        Assert.Equal(TranslationHelpPreference.Never, profile.TranslationHelpPreference);
    }

    [Fact]
    public async Task RecordStepCompleted_PersistsAcrossChangeTrackerReload()
    {
        // Regression: RecordStepCompleted() mutates CompletedStepKeys (a List<string>) in
        // place. Without an explicit EF ValueComparer, the default reference-equality change
        // tracker never saw a change on the same List instance, so completed_step_keys was
        // silently never written to the DB. Clearing the change tracker forces the next query
        // to hit the DB for real.
        await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "welcome", "{}"));
        _db.ChangeTracker.Clear();

        var reloaded = await _db.StudentOnboardingProgress.FirstAsync(p => p.UserId == _userId);
        Assert.Contains("welcome", reloaded.CompletedStepKeys);
    }

    [Fact]
    public async Task AfterLearningGoals_WithWork_IncludesCareerContextStep()
    {
        await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "learning_goals", MultiAnswer("work")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "custom_learning_goal", Skip));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "focus_areas", MultiAnswer("speaking")));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "custom_focus_area", Skip));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "learning_goal_description", Skip));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "difficulty_preference", Answer("Balanced")));

        var afterDuration = await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "session_duration", Answer("15")));

        Assert.Equal("career_context", afterDuration.CurrentStepKey);
    }
}
