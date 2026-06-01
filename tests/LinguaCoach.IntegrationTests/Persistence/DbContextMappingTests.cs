using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Mapping smoke tests against an in-process SQLite database.
/// No Docker required. Validates schema creation, seed data, and round-trip
/// persistence for each entity type.
///
/// Tradeoff: SQLite doesn't enforce the same constraint semantics as PostgreSQL
/// (e.g. numeric precision, enum storage). Real PostgreSQL integration tests
/// belong in a separate suite once a test container is available.
/// </summary>
public sealed class DbContextMappingTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public DbContextMappingTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ── Seed data ─────────────────────────────────────────────────────────────

    [Fact]
    public void Seed_PersistsTwoLanguages()
    {
        var languages = _db.Languages.ToList();
        Assert.Equal(2, languages.Count);
    }

    [Fact]
    public void Seed_PersianLanguageHasRtlDirection()
    {
        var persian = _db.Languages.Single(l => l.Code == "fa");
        Assert.Equal(LanguageDirection.Rtl, persian.Direction);
    }

    [Fact]
    public void Seed_EnglishLanguageHasLtrDirection()
    {
        var english = _db.Languages.Single(l => l.Code == "en");
        Assert.Equal(LanguageDirection.Ltr, english.Direction);
    }

    [Fact]
    public void Seed_FaEnLanguagePairExists()
    {
        var pair = _db.LanguagePairs
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .Single();

        Assert.Equal("fa", pair.SourceLanguage.Code);
        Assert.Equal("en", pair.TargetLanguage.Code);
        Assert.True(pair.IsActive);
    }

    [Fact]
    public void Seed_WorkplaceEnglishTrackExists()
    {
        var track = _db.LearningTracks.Single();
        Assert.Equal("Workplace English", track.Name);
    }

    [Fact]
    public void Seed_DocumentControllerCareerProfileExists()
    {
        var profile = _db.CareerProfiles.Single();
        Assert.Equal("Document Controller", profile.Name);
    }

    // ── Round-trip persistence ─────────────────────────────────────────────────

    [Fact]
    public void StudentProfile_CanBeSavedAndLoaded()
    {
        var userId = Guid.NewGuid();
        var student = new StudentProfile(userId);

        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.StudentProfiles.Single(sp => sp.UserId == userId);
        Assert.Equal(userId, loaded.UserId);
        Assert.Equal(OnboardingStatus.NotStarted, loaded.OnboardingStatus);
        Assert.Equal(OnboardingStep.None, loaded.LastCompletedStep);
    }

    [Fact]
    public void StudentProfile_OnboardingStepsPersistedCorrectly()
    {
        var pair = _db.LanguagePairs.Include(lp => lp.SourceLanguage).Include(lp => lp.TargetLanguage).First();
        var track = _db.LearningTracks.First();
        var career = _db.CareerProfiles.First();

        var student = new StudentProfile(Guid.NewGuid());
        student.SetLanguagePair(pair);
        student.SetLearningTrack(track);
        student.SetCareerProfile(career);
        student.SetSkillFocus(SkillFocus.Writing);

        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.StudentProfiles.Single(sp => sp.UserId == student.UserId);
        Assert.Equal(OnboardingStatus.Complete, loaded.OnboardingStatus);
        Assert.Equal(OnboardingStep.Skill, loaded.LastCompletedStep);
        Assert.Equal(SkillFocus.Writing, loaded.SkillFocus);
        Assert.Equal(pair.Id, loaded.LanguagePairId);
    }

    [Fact]
    public void AiPrompt_CanBeSavedAndLoaded()
    {
        var prompt = new AiPrompt("cefr.assessment.v1", "You are a CEFR assessor...", version: 1);
        _db.AiPrompts.Add(prompt);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.AiPrompts.Single(p => p.Key == "cefr.assessment.v1");
        Assert.Equal(1, loaded.Version);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public void AiUsageLog_CanBeSavedAndLoaded()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();

        var log = new AiUsageLog(student.Id, "openai", "gpt-4o", 500, 200, 0.003m);
        _db.AiUsageLogs.Add(log);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.AiUsageLogs.Single(l => l.StudentProfileId == student.Id);
        Assert.Equal("openai", loaded.ProviderName);
        Assert.Equal("gpt-4o", loaded.ModelName);
        Assert.Equal(500, loaded.InputTokens);
        Assert.Equal(0.003m, loaded.CostUsd);
    }

    // ── Uniqueness constraints ─────────────────────────────────────────────────

    [Fact]
    public void Language_DuplicateCodeThrows()
    {
        // "fa" already exists from seed — adding another should violate the unique index
        var duplicate = new Language("fa", "Farsi Duplicate", LanguageDirection.Rtl);
        _db.Languages.Add(duplicate);
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }
}
