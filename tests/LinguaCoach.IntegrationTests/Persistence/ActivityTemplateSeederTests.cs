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
/// Phase C3 (2026-07-08) added a seventh (ReorderParagraphs, a stock Form.io datagrid-reorder).
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
    public async Task SeedAsync_WhenEmpty_Seeds7ApprovedPublishedTemplates()
    {
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var templates = await _db.ActivityTemplates.ToListAsync();
        Assert.Equal(7, templates.Count);
        Assert.All(templates, t => Assert.Equal(AdminReviewStatus.Approved, t.ReviewStatus));
        Assert.All(templates, t => Assert.True(t.IsPublished));
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var count = await _db.ActivityTemplates.CountAsync();
        Assert.Equal(7, count);
    }

    [Theory]
    [InlineData("phrase_match")]
    [InlineData("gap_fill_workplace_phrase")]
    [InlineData("reading_multiple_choice_single")]
    [InlineData("reading_multiple_choice_multi")]
    [InlineData("reading_fill_in_blanks")]
    [InlineData("reading_writing_fill_in_blanks")]
    [InlineData("reorder_paragraphs")]
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

    [Fact]
    public async Task SeedAsync_ReorderParagraphs_NeverLeaksCorrectOrderIntoStudentSafeSchema()
    {
        await ActivityTemplateSeeder.SeedAsync(_db, NullLogger.Instance);

        var template = await _db.ActivityTemplates
            .Where(t => t.PatternKey == "reorder_paragraphs")
            .FirstOrDefaultAsync();

        Assert.NotNull(template);

        // The correct order must live exclusively in ScoringModelJson (backend-only).
        Assert.Contains("correctOrder", template!.ScoringModelJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ordered_sequence", template.ScoringModelJson, StringComparison.OrdinalIgnoreCase);

        // The student-safe schema must never contain the correct-order key or value, or any
        // other answer/scoring-leak marker.
        Assert.DoesNotContain("correctOrder", template.FormIoBaseSchemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", template.FormIoBaseSchemaJson, StringComparison.OrdinalIgnoreCase);

        // The schema's row display order must NOT match the correct order — it is shuffled.
        using var doc = System.Text.Json.JsonDocument.Parse(template.FormIoBaseSchemaJson);
        var datagrid = doc.RootElement.GetProperty("components")
            .EnumerateArray()
            .First(c => c.GetProperty("key").GetString() == "paragraphs");
        var displayedIds = datagrid.GetProperty("defaultValue")
            .EnumerateArray()
            .Select(row => row.GetProperty("itemId").GetString())
            .ToList();

        Assert.NotEqual(new[] { "p1", "p2", "p3", "p4", "p5" }, displayedIds);
    }
}
