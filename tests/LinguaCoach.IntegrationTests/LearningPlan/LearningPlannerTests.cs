using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Aliases to avoid ambiguity with LinguaCoach.IntegrationTests.Api namespace members
using StudentProfile = LinguaCoach.Domain.Entities.StudentProfile;
using SkillFocus = LinguaCoach.Domain.Enums.SkillFocus;
using UserRole = LinguaCoach.Domain.Enums.UserRole;

namespace LinguaCoach.IntegrationTests.LearningPlan;

/// <summary>
/// Integration tests for LearningPlannerService.
/// Verifies the correct vocabulary mix, anti-repetition enforcement,
/// and review scheduling using the real EF/SQLite stack.
/// </summary>
public sealed class LearningPlannerTests : IClassFixture<LearningPlannerTestFactory>
{
    private readonly LearningPlannerTestFactory _factory;

    public LearningPlannerTests(LearningPlannerTestFactory factory)
    {
        _factory = factory;
    }

    // ── New student: only new words from curriculum ───────────────────────────

    [Fact]
    public async Task BuildLessonPlan_NewStudent_SelectsOnlyNewWords()
    {
        var (profileId, _) = await _factory.CreateOnboardedStudentProfileAsync($"new_{Guid.NewGuid():N}@t.com");

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();

        var plan = await planner.BuildLessonPlanAsync(profileId);

        Assert.NotEmpty(plan.TargetVocabulary);
        Assert.Empty(plan.ReviewVocabulary);
        Assert.Empty(plan.ReinforcementVocabulary);
        Assert.True(plan.TargetVocabulary.Count <= 5);
    }

    // ── Weak words appear in review slot ─────────────────────────────────────

    [Fact]
    public async Task BuildLessonPlan_WithWeakWords_IncludesWeakInReview()
    {
        var email = $"weak_{Guid.NewGuid():N}@t.com";
        var (profileId, languagePairId) = await _factory.CreateOnboardedStudentProfileAsync(email);

        using var setup = _factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var weakEntry1 = new VocabularyEntry(profileId, languagePairId, "approval", "Official permission");
        weakEntry1.RecordRecall(correct: false);
        var weakEntry2 = new VocabularyEntry(profileId, languagePairId, "submittal", "Formal submission");
        weakEntry2.RecordRecall(correct: false);
        db.VocabularyEntries.AddRange(weakEntry1, weakEntry2);
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();
        var plan = await planner.BuildLessonPlanAsync(profileId);

        Assert.NotEmpty(plan.ReviewVocabulary);
        Assert.Contains(plan.ReviewVocabulary, v => v.Word == "approval");
    }

    // ── Mastered words appear in reinforcement slot ───────────────────────────

    [Fact]
    public async Task BuildLessonPlan_WithMasteredWords_IncludesMasteredInReinforcement()
    {
        var email = $"mastered_{Guid.NewGuid():N}@t.com";
        var (profileId, languagePairId) = await _factory.CreateOnboardedStudentProfileAsync(email);

        using var setup = _factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var masteredEntry = new VocabularyEntry(profileId, languagePairId, "compliance", "Meeting standards");
        masteredEntry.RecordRecall(correct: true);
        masteredEntry.RecordRecall(correct: true);
        masteredEntry.SetMasteryScore(0.9);
        db.VocabularyEntries.Add(masteredEntry);
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();
        var plan = await planner.BuildLessonPlanAsync(profileId);

        Assert.NotEmpty(plan.ReinforcementVocabulary);
        Assert.Contains(plan.ReinforcementVocabulary, v => v.Word == "compliance");
    }

    // ── Overdue review words prioritised ─────────────────────────────────────

    [Fact]
    public async Task BuildLessonPlan_WithDueReviewWords_IncludesDueWordsInReview()
    {
        var email = $"review_{Guid.NewGuid():N}@t.com";
        var (profileId, languagePairId) = await _factory.CreateOnboardedStudentProfileAsync(email);

        using var setup = _factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var dueEntry = new VocabularyEntry(profileId, languagePairId, "transmittal", "Cover document");
        dueEntry.RecordRecall(correct: true);
        dueEntry.RecordRecall(correct: true);
        dueEntry.ScheduleNextReview(DateTime.UtcNow.AddDays(-1), easeFactor: 2.5);
        db.VocabularyEntries.Add(dueEntry);
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();
        var plan = await planner.BuildLessonPlanAsync(profileId);

        Assert.Contains(plan.ReviewVocabulary, v => v.Word == "transmittal");
    }

    // ── Anti-repetition: recent lesson words excluded ─────────────────────────

    [Fact]
    public async Task BuildLessonPlan_AntiRepetition_ExcludesRecentLessonWords()
    {
        var email = $"antirepeat_{Guid.NewGuid():N}@t.com";
        var (profileId, languagePairId) = await _factory.CreateOnboardedStudentProfileAsync(email);

        using var setup = _factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var recentEntry = new VocabularyEntry(profileId, languagePairId, "pending", "Awaiting decision");
        recentEntry.RecordRecall(correct: false);
        db.VocabularyEntries.Add(recentEntry);
        await db.SaveChangesAsync();

        var log = new LessonVocabularyLog(profileId, recentEntry.Id, lessonNumber: 1);
        db.LessonVocabularyLogs.Add(log);
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();
        var plan = await planner.BuildLessonPlanAsync(profileId);

        var allWords = plan.TargetVocabulary
            .Concat(plan.ReviewVocabulary)
            .Concat(plan.ReinforcementVocabulary)
            .Select(v => v.Word)
            .ToList();

        Assert.DoesNotContain("pending", allWords);
    }

    // ── Already-known words not selected as new ───────────────────────────────

    [Fact]
    public async Task BuildLessonPlan_AlreadyKnownWords_NotSelectedAsNew()
    {
        var email = $"known_{Guid.NewGuid():N}@t.com";
        var (profileId, languagePairId) = await _factory.CreateOnboardedStudentProfileAsync(email);

        using var setup = _factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var knownWords = new[] { "approval", "submittal", "revision", "pending", "outstanding" };
        foreach (var word in knownWords)
        {
            var entry = new VocabularyEntry(profileId, languagePairId, word, "definition");
            entry.RecordRecall(correct: true);
            db.VocabularyEntries.Add(entry);
        }
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();
        var plan = await planner.BuildLessonPlanAsync(profileId);

        foreach (var known in knownWords)
            Assert.DoesNotContain(plan.TargetVocabulary, v => v.Word == known);
    }

    // ── LessonPlan structural invariants ──────────────────────────────────────

    [Fact]
    public async Task BuildLessonPlan_AlwaysReturnsValidPlan()
    {
        var (profileId, _) = await _factory.CreateOnboardedStudentProfileAsync($"valid_{Guid.NewGuid():N}@t.com");

        using var scope = _factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<ILearningPlanner>();
        var plan = await planner.BuildLessonPlanAsync(profileId);

        Assert.Equal(profileId, plan.StudentProfileId);
        Assert.Equal(LessonType.Writing, plan.LessonType);
        Assert.False(string.IsNullOrEmpty(plan.CefrLevel));
        Assert.False(string.IsNullOrEmpty(plan.CareerContext));
        Assert.False(string.IsNullOrEmpty(plan.ScenarioTemplate));
    }
}

/// <summary>
/// Test factory that seeds curriculum word list data needed for LearningPlanner tests.
/// </summary>
public sealed class LearningPlannerTestFactory : Api.ApiTestFactory
{
    private bool _seeded;

    public async Task<(Guid ProfileId, Guid LanguagePairId)> CreateOnboardedStudentProfileAsync(string email)
    {
        await EnsureSeededAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            var existingProfile = await db.StudentProfiles.FirstAsync(p => p.UserId == existing.Id);
            return (existingProfile.Id, existingProfile.LanguagePairId!.Value);
        }

        var user = new ApplicationUser
        {
            UserName = email, Email = email,
            Role = UserRole.Student,
            EmailConfirmed = true, MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        var pair = db.LanguagePairs
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .First();
        var track = db.LearningTracks.First();
        var career = db.CareerProfiles.First();

        var profile = new StudentProfile(user.Id);
        profile.SetLanguagePair(pair);
        profile.SetLearningTrack(track);
        profile.SetCareerProfile(career);
        profile.SetSkillFocus(SkillFocus.Writing);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (profile.Id, pair.Id);
    }

    private async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        _seeded = true;

        await EnsureCreatedAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Seed curriculum words if not present (EnsureCreated skips migrations/seeds)
        if (!db.CurriculumWordLists.Any())
        {
            var pair = db.LanguagePairs.First();
            var career = db.CareerProfiles.First();

            var words = new[]
            {
                ("approval", "Official agreement or permission", 1),
                ("submittal", "A formal document submitted for review", 2),
                ("revision", "A corrected or updated document version", 3),
                ("pending", "Awaiting action or decision", 4),
                ("outstanding", "Not yet resolved", 5),
                ("transmittal", "A cover document recording what is sent", 6),
                ("compliance", "Meeting required standards", 7),
                ("RFI", "Request for Information", 8),
                ("specification", "A detailed technical description", 9),
                ("drawing register", "A log tracking all project drawings", 10),
            };

            foreach (var (word, def, priority) in words)
                db.CurriculumWordLists.Add(new CurriculumWordList(career.Id, pair.Id, word, def, string.Empty, priority));

            await db.SaveChangesAsync();
        }
    }
}
