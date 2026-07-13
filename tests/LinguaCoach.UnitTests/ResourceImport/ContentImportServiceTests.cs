using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase H2 — Import Content UX v1. Covers the admin-friendly wrapper's own responsibilities
/// (source find-or-create + auto-approve, pasted-text/CSV/JSON → ImportAsync conversion) — the
/// underlying gate/parse behavior itself is covered by <see cref="ResourceImportServiceTests"/>.
/// </summary>
public sealed class ContentImportServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ContentImportService _sut;

    public ContentImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var importService = new ResourceImportService(_db, new ActivityContentFingerprintService());
        _sut = new ContentImportService(_db, importService);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task Creates_and_auto_approves_a_new_source_when_none_exists_with_that_name()
    {
        var result = await _sut.ImportContentAsync(new ContentImportRequest(
            "Brand New Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.PastedText, "hello\nworld"));

        var source = await _db.CefrResourceSources.SingleAsync();
        source.Name.Should().Be("Brand New Source");
        source.IsImportApproved.Should().BeTrue();
        result.SourceId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Reuses_existing_approved_source_by_exact_name_instead_of_creating_a_duplicate()
    {
        var existing = new CefrResourceSource("Existing Source", "CC-BY-4.0");
        existing.ApproveForImport();
        _db.CefrResourceSources.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.ImportContentAsync(new ContentImportRequest(
            "Existing Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.PastedText, "hello"));

        (await _db.CefrResourceSources.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Creates_import_run_raw_records_and_candidates_through_the_existing_pipeline()
    {
        var result = await _sut.ImportContentAsync(new ContentImportRequest(
            "Pipeline Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.PastedText, "hello\nworld"));

        (await _db.ResourceImportRuns.CountAsync()).Should().Be(1);
        (await _db.ResourceRawRecords.CountAsync()).Should().Be(2);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(2);
        result.RawRecordCount.Should().Be(2);
        result.CandidateCount.Should().Be(2);
    }

    [Fact]
    public async Task Candidates_are_pending_review_and_not_published()
    {
        await _sut.ImportContentAsync(new ContentImportRequest(
            "Review Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.PastedText, "hello"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.IsPublished.Should().BeFalse();
        // ReviewStatus only advances to PendingReview once IResourceCandidateValidationService
        // has validated the row (Phase E2+) — import alone stages it as NotRequired.
        candidate.ReviewStatus.Should().Be(AdminReviewStatus.NotRequired);

        (await _db.ResourceBankItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Admin_selected_resource_type_is_applied_to_every_candidate()
    {
        await _sut.ImportContentAsync(new ContentImportRequest(
            "Grammar Source", ResourceCandidateType.GrammarProfileEntry, ContentImportInputMode.PastedText,
            "Present perfect explained simply"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.GrammarProfileEntry);
    }

    [Fact]
    public async Task Csv_text_input_mode_parses_like_a_csv_file_upload()
    {
        var csv = "word,cefrLevel\nhello,A1\nworld,A2\n";

        var result = await _sut.ImportContentAsync(new ContentImportRequest(
            "Csv Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.CsvText, csv));

        result.CandidateCount.Should().Be(2);
        var levels = await _db.ResourceCandidates.Select(c => c.CefrLevel).ToListAsync();
        levels.Should().Contain("A1").And.Contain("A2");
    }

    [Fact]
    public async Task Json_text_input_mode_parses_a_json_array()
    {
        var json = "[{\"word\":\"hello\"},{\"word\":\"world\"}]";

        var result = await _sut.ImportContentAsync(new ContentImportRequest(
            "Json Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.JsonText, json));

        result.CandidateCount.Should().Be(2);
    }

    [Fact]
    public async Task Empty_content_is_rejected()
    {
        var act = async () => await _sut.ImportContentAsync(new ContentImportRequest(
            "Empty Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.PastedText, "   "));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
        (await _db.ResourceImportRuns.CountAsync()).Should().Be(0);
    }

    // ── Phase J5b — "Mixed" (null ResourceType) ─────────────────────────────────

    [Fact]
    public async Task Null_resource_type_lets_each_row_be_classified_independently()
    {
        var json = """[{"word":"hello"},{"grammarKey":"present perfect","explanation":"habitual actions"},{"prompt":"Describe your day."}]""";

        var result = await _sut.ImportContentAsync(new ContentImportRequest(
            "Mixed Source", ResourceType: null, ContentImportInputMode.JsonText, json));

        result.CandidateCount.Should().Be(3);
        var types = await _db.ResourceCandidates.Select(c => c.CandidateType).ToListAsync();
        types.Should().BeEquivalentTo(new[]
        {
            ResourceCandidateType.VocabularyEntry, ResourceCandidateType.GrammarProfileEntry, ResourceCandidateType.WritingPrompt,
        });
    }

    [Fact]
    public async Task No_student_assignment_or_learning_records_are_created()
    {
        await _sut.ImportContentAsync(new ContentImportRequest(
            "No Assignment Source", ResourceCandidateType.VocabularyEntry, ContentImportInputMode.PastedText, "hello"));

        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
        (await _db.LearningActivities.CountAsync()).Should().Be(0);
    }
}
