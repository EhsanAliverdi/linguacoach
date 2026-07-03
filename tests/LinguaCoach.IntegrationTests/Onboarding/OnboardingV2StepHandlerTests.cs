using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Onboarding;

/// <summary>
/// Phase 20I Phase 2 — closing V2 onboarding's answer-mapping gaps (career context, session
/// duration, work experience, learning goal description) and the work-relevance conditional
/// skip for career_context/work_experience. See the phase 20I plan for context.
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

        _handler = new OnboardingV2StepHandler(_db);
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

        var flow = await _db.OnboardingFlowDefinitions.FirstAsync(f => f.IsActive);
        var progress = new StudentOnboardingProgress(_userId, flow.Id, "welcome");
        _db.StudentOnboardingProgress.Add(progress);
        await _db.SaveChangesAsync();
        return profile.Id;
    }

    [Fact]
    public async Task SessionDuration_WritesPreferredSessionDurationMinutes()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "session_duration", """{"key":"20"}"""));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal(20, profile.PreferredSessionDurationMinutes);
    }

    [Fact]
    public async Task CareerContext_WritesCareerContextText()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "career_context", """{"value":"Nurse"}"""));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal("Nurse", profile.CareerContext);
    }

    [Fact]
    public async Task LearningGoalDescription_WritesLearningGoalDescription()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "learning_goal_description", """{"value":"I struggle in meetings."}"""));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal("I struggle in meetings.", profile.LearningGoalDescription);
    }

    [Fact]
    public async Task WorkExperience_WritesExperienceLevelAndRoleFamiliarity()
    {
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "work_experience",
            """{"experienceLevel":"MidLevel_2_5Years","roleFamiliarity":"ExperiencedInRole"}"""));

        var profile = await _db.StudentProfiles.FirstAsync(p => p.Id == profileId);
        Assert.Equal(ProfessionalExperienceLevel.MidLevel_2_5Years, profile.ProfessionalExperienceLevel);
        Assert.Equal(RoleFamiliarity.ExperiencedInRole, profile.RoleFamiliarity);
    }

    [Fact]
    public async Task AfterLearningGoals_WithoutWork_SkipsCareerContextAndWorkExperience()
    {
        await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "learning_goals", """{"keys":["day_to_day","travel"]}"""));
        var afterFocus = await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "focus_areas", """{"keys":["speaking"]}"""));
        var afterDifficulty = await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "difficulty_preference", """{"key":"Moderate"}"""));

        // Next step after difficulty_preference should be session_duration (not career_context),
        // and after session_duration should skip straight past career_context/work_experience
        // to learning_goal_description (also AdminConfigured but not work-gated) then assessment.
        Assert.Equal("session_duration", afterDifficulty.CurrentStepKey);

        var afterDuration = await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "session_duration", """{"key":"15"}"""));

        Assert.NotEqual("career_context", afterDuration.CurrentStepKey);
        Assert.Equal("learning_goal_description", afterDuration.CurrentStepKey);
    }

    [Fact]
    public async Task RecordStepCompleted_PersistsAcrossChangeTrackerReload()
    {
        // Regression: RecordStepCompleted() mutates CompletedStepKeys (a List<string>) in
        // place. Without an explicit EF ValueComparer, the default reference-equality change
        // tracker never saw a change on the same List instance, so completed_step_keys was
        // silently never written to the DB -- every V2 onboarding completion failed with
        // "Required steps not completed" listing every step, discovered live 2026-07-03.
        // Clearing the change tracker forces the next query to hit the DB for real, instead of
        // returning the already-tracked in-memory entity (which would pass even if the SQL
        // UPDATE never happened).
        var profileId = await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(_userId, "welcome", "{}"));
        _db.ChangeTracker.Clear();

        var reloaded = await _db.StudentOnboardingProgress.FirstAsync(p => p.UserId == _userId);
        Assert.Contains("welcome", reloaded.CompletedStepKeys);
    }

    [Fact]
    public async Task AfterLearningGoals_WithWork_IncludesCareerContextAndWorkExperience()
    {
        await SeedProfileAndProgressAsync();

        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "learning_goals", """{"keys":["work"]}"""));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "focus_areas", """{"keys":["speaking"]}"""));
        await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "difficulty_preference", """{"key":"Moderate"}"""));

        var afterDuration = await _handler.HandleAsync(new SubmitOnboardingStepCommand(
            _userId, "session_duration", """{"key":"15"}"""));

        Assert.Equal("career_context", afterDuration.CurrentStepKey);
    }
}
