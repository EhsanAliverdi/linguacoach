using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Phase C1 (2026-07-08) seeded a first batch of approved/published ActivityTemplates
/// (PhraseMatch, GapFillWorkplacePhrase, ReadingMultipleChoiceSingle). Phase C2 (2026-07-08) added
/// a second batch (ReadingMultipleChoiceMulti, ReadingFillInBlanks, ReadingWritingFillInBlanks).
/// </summary>
public sealed class ActivityTemplateSeederTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public ActivityTemplateSeederTests()
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
    public async Task SeedAsync_WhenEmpty_Seeds6ApprovedPublishedTemplates()
    {
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var templates = await _db.ActivityTemplates.ToListAsync();
        Assert.Equal(6, templates.Count);
        Assert.All(templates, t => Assert.Equal(AdminReviewStatus.Approved, t.ReviewStatus));
        Assert.All(templates, t => Assert.True(t.IsPublished));
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var count = await _db.ActivityTemplates.CountAsync();
        Assert.Equal(6, count);
    }

    [Theory]
    [InlineData("phrase_match")]
    [InlineData("gap_fill_workplace_phrase")]
    [InlineData("reading_multiple_choice_single")]
    [InlineData("reading_multiple_choice_multi")]
    [InlineData("reading_fill_in_blanks")]
    [InlineData("reading_writing_fill_in_blanks")]
    public async Task SeedAsync_EachMigratedPattern_HasApprovedPublishedTemplate(string patternKey)
    {
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var template = await _db.ActivityTemplates
            .Where(t => t.PatternKey == patternKey && t.IsPublished && t.ReviewStatus == AdminReviewStatus.Approved)
            .FirstOrDefaultAsync();

        Assert.NotNull(template);
        Assert.False(string.IsNullOrWhiteSpace(template!.FormIoBaseSchemaJson));
        Assert.False(string.IsNullOrWhiteSpace(template.ScoringModelJson));
        Assert.False(string.IsNullOrWhiteSpace(template.GenerationInstructions));
        // Scoring/answer data must never appear in the student-safe schema field.
        Assert.DoesNotContain("correctAnswer", template.FormIoBaseSchemaJson, StringComparison.OrdinalIgnoreCase);

        // Every seeded base schema must pass the same student-safe Form.io validation used by
        // placement/onboarding — no scripts, no answer/scoring-leak keys.
        var validator = new FormIoSchemaValidationService();
        var result = validator.ValidateSchema(template.FormIoBaseSchemaJson);
        Assert.True(result.IsValid, result.Error);
    }
}
