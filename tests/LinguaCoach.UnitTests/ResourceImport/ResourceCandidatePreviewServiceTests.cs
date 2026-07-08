using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E3 — read-only rendered preview for a staged ResourceCandidate. SQLite in-memory,
/// matching ResourceCandidateValidationServiceTests' Phase E2 convention.
/// </summary>
public sealed class ResourceCandidatePreviewServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceCandidatePreviewService _sut;
    private readonly ActivityContentFingerprintService _fingerprint = new();

    public ResourceCandidatePreviewServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceCandidatePreviewService(_db, new FormIoSchemaValidationService());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource()
    {
        var source = new CefrResourceSource(
            $"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", sourceUrl: "https://example.test/source",
            allowsStudentDisplay: true, allowsCommercialUse: true, attributionText: "Test Attribution",
            downloadUrl: "https://example.test/download");
        source.ApproveForImport("cleared for test");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    private (ResourceImportRun Run, ResourceRawRecord Raw) SeedRunAndRaw(CefrResourceSource source, string rawJson)
    {
        var run = new ResourceImportRun(source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, $"rawhash-{Guid.NewGuid():N}", "en", "row", rawJson: rawJson);
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();

        return (run, raw);
    }

    private string Fingerprint(string canonicalText, string normalizedJson) =>
        _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(normalizedJson, ActivityContentShape.Unknown, null, canonicalText));

    private ResourceCandidate SeedCandidate(
        ResourceRawRecord raw, ResourceCandidateType type, string canonicalText, string normalizedJson,
        ResourceCandidateValidationStatus status = ResourceCandidateValidationStatus.NeedsReview)
    {
        var candidate = new ResourceCandidate(
            raw.Id, type, canonicalText, normalizedJson, "en",
            canonicalText, Fingerprint(canonicalText, normalizedJson), status);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();
        return candidate;
    }

    [Fact]
    public async Task Missing_candidate_returns_null()
    {
        var result = await _sut.GetPreviewAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task Preview_never_mutates_the_candidate_or_writes_to_any_published_bank_table()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello","definition":"a greeting"}""");
        var updatedAtBefore = candidate.UpdatedAtUtc;

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        var reloaded = await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id);
        reloaded.UpdatedAtUtc.Should().Be(updatedAtBefore);

        (await _db.CefrVocabularyEntries.CountAsync()).Should().Be(0);
        (await _db.CefrGrammarProfileEntries.CountAsync()).Should().Be(0);
        (await _db.CefrReadingReferences.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Vocabulary_candidate_produces_expected_rendered_model()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello","pos":"interjection","definition":"a greeting","example":"Hello there!"}""");
        var candidate = SeedCandidate(
            raw, ResourceCandidateType.VocabularyEntry, "hello",
            """{"word":"hello","pos":"interjection","definition":"a greeting","example":"Hello there!"}""");

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.RenderedPreviewModel.Kind.Should().Be("VocabularyEntry");
        preview.RenderedPreviewModel.Word.Should().Be("hello");
        preview.RenderedPreviewModel.PartOfSpeech.Should().Be("interjection");
        preview.RenderedPreviewModel.Definition.Should().Be("a greeting");
        preview.RenderedPreviewModel.Example.Should().Be("Hello there!");
        preview.CanPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Grammar_candidate_produces_expected_rendered_model()
    {
        var source = SeedSource();
        var json = """{"grammarKey":"present_simple","title":"Present Simple","explanation":"Used for habits.","examples":"[\"I walk\",\"She walks\"]"}""";
        var (_, raw) = SeedRunAndRaw(source, json);
        var candidate = SeedCandidate(raw, ResourceCandidateType.GrammarProfileEntry, "Present Simple", json);

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.RenderedPreviewModel.Kind.Should().Be("GrammarProfileEntry");
        preview.RenderedPreviewModel.GrammarTitle.Should().Be("present_simple");
        preview.RenderedPreviewModel.Explanation.Should().Be("Used for habits.");
        preview.RenderedPreviewModel.GrammarExamples.Should().BeEquivalentTo(new[] { "I walk", "She walks" });
        preview.CanPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Reading_candidate_produces_expected_rendered_model_with_word_count_and_reading_time()
    {
        var source = SeedSource();
        var passage = string.Join(" ", Enumerable.Repeat("word", 400)); // 400 words -> ~2 min at 200wpm
        var json = JsonSerializer.Serialize(new { title = "A Passage", passage });
        var (_, raw) = SeedRunAndRaw(source, json);
        var candidate = SeedCandidate(raw, ResourceCandidateType.ReadingPassage, "A Passage", json);

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.RenderedPreviewModel.Kind.Should().Be("ReadingPassage");
        preview.RenderedPreviewModel.Title.Should().Be("A Passage");
        preview.RenderedPreviewModel.WordCount.Should().Be(400);
        preview.RenderedPreviewModel.EstimatedReadingMinutes.Should().Be(2);
        preview.CanPreview.Should().BeTrue();
    }

    [Fact]
    public async Task ActivityTemplateCandidate_preview_exposes_schema_but_never_leaks_scoring_data_into_student_visible_slot()
    {
        var source = SeedSource();
        var schema = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "textfield", key = "answer", label = "Your answer" },
            },
        });
        var normalized = JsonSerializer.Serialize(new
        {
            title = "Fill in the blank",
            formio = schema,
            rubric = "award 1 point for correct spelling",
            correctAnswer = "cat",
        });
        var (_, raw) = SeedRunAndRaw(source, normalized);
        var candidate = SeedCandidate(raw, ResourceCandidateType.ActivityTemplateCandidate, "Fill in the blank", normalized);

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.RenderedPreviewModel.Kind.Should().Be("ActivityTemplateCandidate");
        preview.RenderedPreviewModel.StudentVisibleFormIoSchemaJson.Should().NotBeNullOrEmpty();
        preview.CanPreview.Should().BeTrue();

        // The student-visible slot must never contain the scoring/rubric/answer-key data.
        preview.RenderedPreviewModel.StudentVisibleFormIoSchemaJson.Should().NotContain("rubric");
        preview.RenderedPreviewModel.StudentVisibleFormIoSchemaJson.Should().NotContain("correctAnswer");

        // Scoring/rubric data is only ever exposed via the admin-only field.
        preview.AdminOnlyActivityMetadataJson.Should().NotBeNullOrEmpty();
        preview.AdminOnlyActivityMetadataJson.Should().Contain("rubric");
        preview.AdminOnlyActivityMetadataJson.Should().Contain("correctAnswer");
    }

    [Fact]
    public async Task Unknown_typed_candidate_returns_generic_preview_with_warning()
    {
        var source = SeedSource();
        var json = """{"someField":"someValue"}""";
        var (_, raw) = SeedRunAndRaw(source, json);
        var candidate = SeedCandidate(raw, ResourceCandidateType.Unknown, "mystery content", json);

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.RenderedPreviewModel.Kind.Should().Be("Unknown");
        preview.CanPreview.Should().BeFalse();
        preview.PreviewWarnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Preview_includes_source_license_and_provenance_fields()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.Source.SourceName.Should().Be(source.Name);
        preview.Source.LicenseType.Should().Be("CC-BY-4.0");
        preview.Source.AttributionText.Should().Be("Test Attribution");
        preview.Source.AllowsStudentDisplay.Should().BeTrue();
        preview.Source.AllowsCommercialUse.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_includes_validation_status_errors_warnings_and_ai_summary_when_present()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""");
        candidate.ApplyAnalysis(
            """{"cefrLevel":"A1"}""", "A1", 0.95, "vocabulary", "vocabulary.receptive", 1,
            "[]", "[]", null, null, null, null, null, 0.9, "hello");
        candidate.ApplyValidation(
            ResourceCandidateValidationStatus.Passed,
            JsonSerializer.Serialize(new { errors = Array.Empty<string>(), warnings = new[] { "Duplicate: another candidate with the same content fingerprint exists elsewhere from the same source." } }));
        _db.SaveChanges();

        var preview = await _sut.GetPreviewAsync(candidate.Id);

        preview.Should().NotBeNull();
        preview!.ValidationStatus.Should().Be("Passed");
        preview.ValidationErrors.Should().BeEmpty();
        preview.ValidationWarnings.Should().ContainSingle(w => w.StartsWith("Duplicate:"));
        preview.DuplicateIndicators.Should().ContainSingle();
        preview.AiAnalysisSummary.Should().NotBeNull();
        preview.AiAnalysisSummary!.CefrLevel.Should().Be("A1");
        preview.AiAnalysisSummary.QualityScore.Should().Be(0.9);
    }
}
