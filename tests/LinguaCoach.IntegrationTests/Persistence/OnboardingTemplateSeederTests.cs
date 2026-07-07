using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Persistence;

public sealed class OnboardingTemplateSeederTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingTemplateSeederTests()
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

    [Fact]
    public async Task SeedAsync_WhenEmpty_SeedsAPublishedTemplateWithAuthoringSchema()
    {
        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var template = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .FirstAsync(t => t.FlowKind == StudentFlowKind.Onboarding);
        var version = template.Versions.Single();

        Assert.False(string.IsNullOrWhiteSpace(version.AuthoringSchemaJson));
        Assert.Null(version.ScoringRulesJson);
        using var doc = JsonDocument.Parse(version.AuthoringSchemaJson!);
        var pageKeys = doc.RootElement.GetProperty("components")
            .EnumerateArray()
            .Select(p => p.GetProperty("key").GetString())
            .ToList();

        // CEFR level is determined by the placement assessment, not onboarding — the seeded
        // default carries no quick-check/quiz pages.
        Assert.DoesNotContain("page_quick_check_1", pageKeys);
        Assert.DoesNotContain("page_quick_check_2", pageKeys);
        Assert.Contains("page_about_you", pageKeys);
        Assert.Contains("page_goals", pageKeys);
        Assert.Contains("page_practice_preferences", pageKeys);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_DoesNotDuplicateOnRerun()
    {
        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);
        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var count = await _db.StudentFlowTemplates.CountAsync(t => t.FlowKind == StudentFlowKind.Onboarding);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SeedAsync_BackfillsMissingAuthoringSchemaOnExistingVersion_WithoutTouchingScoringData()
    {
        // Simulates a template seeded/saved before the Quiz tab existed: FormIoSchemaJson/
        // ScoringRulesJson present, AuthoringSchemaJson still null.
        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var version = await _db.StudentFlowTemplateVersions.FirstAsync();
        var originalSchema = version.FormIoSchemaJson;
        var legacyScoringRules = """{"components":{"preferred_name":{"kind":"text_normalized","correctAnswer":"x"}}}""";
        _db.Entry(version).Property("ScoringRulesJson").CurrentValue = legacyScoringRules;
        _db.Entry(version).Property("AuthoringSchemaJson").CurrentValue = null;
        await _db.SaveChangesAsync();

        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var reloaded = await _db.StudentFlowTemplateVersions.FirstAsync(v => v.Id == version.Id);
        Assert.False(string.IsNullOrWhiteSpace(reloaded.AuthoringSchemaJson));
        Assert.Equal(originalSchema, reloaded.FormIoSchemaJson);
        Assert.Equal(legacyScoringRules, reloaded.ScoringRulesJson);
    }

    [Fact]
    public async Task SeedAsync_NeverOverwritesAlreadyPresentAuthoringSchema()
    {
        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var version = await _db.StudentFlowTemplateVersions.FirstAsync();
        version.SetAuthoringSchema("""{"components":[{"type":"radio","key":"assessment_q1","admin":"customized"}]}""");
        await _db.SaveChangesAsync();

        await OnboardingTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var reloaded = await _db.StudentFlowTemplateVersions.FirstAsync(v => v.Id == version.Id);
        Assert.Contains("customized", reloaded.AuthoringSchemaJson);
    }
}
