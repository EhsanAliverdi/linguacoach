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
        var career = _db.CareerProfiles.First();

        var student = new StudentProfile(Guid.NewGuid());
        student.SetLanguagePair(pair);
        student.SetSessionPreference(30);
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

        var log = new AiUsageLog(student.Id, "cefr_assessment", "openai", "gpt-4o",
            isFallback: false, wasSuccessful: true, failureReason: null,
            500, 200, 0.003m, durationMs: 100, correlationId: null);
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

    [Fact]
    public void PlacementSkillResult_DuplicateSkillForSameAssessmentThrows()
    {
        // Regression: a completion race in PlacementAssessmentService.FinalizeCompletionAsync
        // could insert two skill-result rows for the same (assessment, skill) pair. This
        // unique index turns that into a constraint violation the service now catches.
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var assessment = PlacementAssessment.CreateAdaptive(student.Id, "student");
        _db.PlacementAssessments.Add(assessment);
        _db.SaveChanges();

        _db.PlacementSkillResults.Add(
            PlacementSkillResult.Create(assessment.Id, "listening", "A2", 0.35, 4));
        _db.SaveChanges();

        _db.PlacementSkillResults.Add(
            PlacementSkillResult.Create(assessment.Id, "listening", "A2", 0.35, 4));
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    // ── CEFR Resource Bank (Phase 3 — schema only) ─────────────────────────────

    [Fact]
    public void CefrDescriptor_CanBeSavedAndLoaded_WithSourceReference()
    {
        var source = new CefrResourceSource("CEFR-J Test Source", "unknown-pending-review");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();

        var descriptor = new CefrDescriptor(source.Id, "B1", "speaking", "Can describe experiences and events.");
        _db.CefrDescriptors.Add(descriptor);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var loaded = _db.CefrDescriptors.Single(d => d.Id == descriptor.Id);
        Assert.Equal(source.Id, loaded.SourceId);
        Assert.Equal("B1", loaded.CefrLevel);
        Assert.Equal("speaking", loaded.Skill);
    }

    [Fact]
    public void CefrResourceSource_DuplicateNameThrows()
    {
        _db.CefrResourceSources.Add(new CefrResourceSource("CEFR-J", "cc-by"));
        _db.SaveChanges();

        _db.CefrResourceSources.Add(new CefrResourceSource("CEFR-J", "cc-by-nc"));
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void CefrVocabularyEntry_DeletingReferencedSourceThrows()
    {
        var source = new CefrResourceSource("CEFR-J Vocab Source", "cc-by");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();

        _db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(source.Id, "greeting", "A1"));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var reloadedSource = _db.CefrResourceSources.Single(s => s.Id == source.Id);
        _db.CefrResourceSources.Remove(reloadedSource);
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }
}
