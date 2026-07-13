using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E2 — deterministic rule validation + exact-fingerprint dedup gate. SQLite in-memory,
/// matching ResourceImportServiceTests' Phase E1 convention.
/// </summary>
public sealed class ResourceCandidateValidationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceCandidateValidationService _sut;
    private readonly ActivityContentFingerprintService _fingerprint = new();

    public ResourceCandidateValidationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceCandidateValidationService(_db, new FormIoSchemaValidationService());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource(
        bool approved = true, string license = "CC-BY-4.0", string? attribution = "Test Attribution", string? name = null)
    {
        var source = new CefrResourceSource(
            name ?? $"Test Source {Guid.NewGuid():N}", license,
            allowsStudentDisplay: true, allowsCommercialUse: true, attributionText: attribution);
        if (approved) source.ApproveForImport("cleared for test");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    private (ResourceImportRun Run, ResourceRawRecord Raw) SeedRunAndRaw(CefrResourceSource source, string rawHash = "rawhash")
    {
        var run = new ResourceImportRun(source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, rawHash, "en", "row", rawJson: """{"word":"hello"}""");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();

        return (run, raw);
    }

    private string Fingerprint(string canonicalText, string normalizedJson) =>
        _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(normalizedJson, ActivityContentShape.Unknown, null, canonicalText));

    private ResourceCandidate SeedCandidate(
        ResourceRawRecord raw,
        string canonicalText = "hello",
        string normalizedJson = """{"word":"hello"}""",
        string languageCode = "en",
        ResourceCandidateType type = ResourceCandidateType.VocabularyEntry,
        string? fingerprintOverride = null)
    {
        var candidate = new ResourceCandidate(
            raw.Id, type, canonicalText, normalizedJson, languageCode,
            canonicalText, fingerprintOverride ?? Fingerprint(canonicalText, normalizedJson),
            ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();
        return candidate;
    }

    [Fact]
    public async Task Valid_cefr_and_skill_subskill_combination_passes()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw);
        candidate.ApplyAnalysis(
            """{"cefrLevel":"A1"}""", "A1", 0.95, "vocabulary", "vocabulary.receptive", 1,
            "[]", "[]", null, null, null, null, null, 0.9, "hello");
        _db.SaveChanges();

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Passed.ToString());
        result.Errors.Should().BeEmpty();

        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        reloaded.ReviewStatus.Should().Be(AdminReviewStatus.PendingReview);
    }

    [Fact]
    public async Task Invalid_subskill_for_skill_fails()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw);
        candidate.ApplyAnalysis(
            """{}""", "A1", 0.9, "vocabulary", "reading.gist", 1,
            "[]", "[]", null, null, null, null, null, 0.9, "hello");
        _db.SaveChanges();

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Failed.ToString());
        result.Errors.Should().Contain(e => e.Contains("does not belong to skill"));
    }

    [Fact]
    public async Task Low_cefr_confidence_needs_review_not_automatic_pass()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw);
        candidate.ApplyAnalysis(
            """{}""", "A1", 0.3, null, null, null,
            "[]", "[]", null, null, null, null, null, null, "hello");
        _db.SaveChanges();

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.NeedsReview.ToString());
        result.NeedsHumanReview.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("confidence"));

        // Phase K2 root-cause regression: NeedsReview (warning-only) must enter the same admin
        // review queue Passed does — previously only Passed promoted NotRequired->PendingReview,
        // so a NeedsReview candidate never became a real Approve & Publish candidate.
        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        reloaded.ReviewStatus.Should().Be(AdminReviewStatus.PendingReview);
    }

    [Fact]
    public async Task Failed_candidate_review_status_stays_NotRequired_not_promoted_to_PendingReview()
    {
        // Contrast case for the above: a true hard failure must NOT enter the review queue the
        // same way NeedsReview does — Failed candidates need a fix + re-validation, not an admin
        // approve-override.
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw, canonicalText: "سلام", normalizedJson: """{"word":"سلام"}""");

        await _sut.ValidateAsync(candidate.Id);

        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        reloaded.ValidationStatus.Should().Be(ResourceCandidateValidationStatus.Failed);
        reloaded.ReviewStatus.Should().Be(AdminReviewStatus.NotRequired);
    }

    [Fact]
    public async Task Persian_script_content_is_rejected()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw, canonicalText: "سلام", normalizedJson: """{"word":"سلام"}""");

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Failed.ToString());
        result.Errors.Should().Contain(e => e.Contains("English-only"));
    }

    [Fact]
    public async Task Source_no_longer_approved_fails_validation_on_revalidation()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw);

        // Original import-time approval is later revoked — a candidate staged while the source
        // was approved must fail when re-validated afterward.
        source.RevokeApproval("license terms changed");
        _db.SaveChanges();

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Failed.ToString());
        result.Errors.Should().Contain(e => e.Contains("no longer approved"));
    }

    [Fact]
    public async Task ActivityTemplateCandidate_with_leaking_correctAnswer_key_fails_formio_validation()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);

        var schemaWithLeak = JsonSerializer.Serialize(new
        {
            display = "form",
            components = new object[]
            {
                new { type = "textfield", key = "answer", correctAnswer = "leaked" }
            }
        });
        var normalizedJson = JsonSerializer.Serialize(new { formio = schemaWithLeak });

        var candidate = SeedCandidate(
            raw, canonicalText: "Some template", normalizedJson: normalizedJson,
            type: ResourceCandidateType.ActivityTemplateCandidate);

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Failed.ToString());
        result.Errors.Should().Contain(e => e.Contains("Form.io schema failed"));
    }

    [Fact]
    public async Task Duplicate_within_same_import_run_is_flagged_needs_review()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, "rawhash-1");
        var (_, raw2) = SeedRunAndRaw(source, "rawhash-2"); // still same run pattern via same source, different raw

        // Force both raw records into the SAME run to exercise the within-run dedup path.
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE resource_raw_records SET resource_import_run_id = {0} WHERE id = {1}",
            raw.ResourceImportRunId, raw2.Id);

        var first = SeedCandidate(raw, canonicalText: "hello", normalizedJson: """{"word":"hello"}""");
        var second = SeedCandidate(raw2, canonicalText: "hello", normalizedJson: """{"word":"hello"}""");

        var result = await _sut.ValidateAsync(second.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.NeedsReview.ToString());
        result.Warnings.Should().Contain(w => w.Contains("Duplicate") && w.Contains("same import run"));
    }

    [Fact]
    public async Task Duplicate_across_different_runs_and_sources_is_flagged_needs_review()
    {
        var sourceA = SeedSource();
        var sourceB = SeedSource();
        var (_, rawA) = SeedRunAndRaw(sourceA, "rawhash-a");
        var (_, rawB) = SeedRunAndRaw(sourceB, "rawhash-b");

        var first = SeedCandidate(rawA, canonicalText: "hello", normalizedJson: """{"word":"hello"}""");
        var second = SeedCandidate(rawB, canonicalText: "hello", normalizedJson: """{"word":"hello"}""");

        var result = await _sut.ValidateAsync(second.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.NeedsReview.ToString());
        result.Warnings.Should().Contain(w => w.Contains("Duplicate"));
    }

    [Fact]
    public async Task Duplicate_of_already_published_resource_bank_item_is_flagged_needs_review()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var fingerprint = Fingerprint("hello", """{"word":"hello"}""");

        var published = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "A1", """{"word":"hello"}""",
            contentFingerprint: fingerprint);
        _db.ResourceBankItems.Add(published);
        _db.SaveChanges();

        var candidate = SeedCandidate(raw, canonicalText: "hello", normalizedJson: """{"word":"hello"}""");

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.NeedsReview.ToString());
        result.Warnings.Should().Contain(w => w.Contains("Duplicate") && w.Contains("already-published"));
    }

    [Fact]
    public async Task No_published_duplicate_warning_when_fingerprint_does_not_match_any_bank_item()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);

        var published = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "A1", """{"word":"goodbye"}""",
            contentFingerprint: Fingerprint("goodbye", """{"word":"goodbye"}"""));
        _db.ResourceBankItems.Add(published);
        _db.SaveChanges();

        var candidate = SeedCandidate(raw, canonicalText: "hello", normalizedJson: """{"word":"hello"}""");

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Passed.ToString());
        result.Warnings.Should().NotContain(w => w.Contains("already-published"));
    }

    [Fact]
    public async Task Safety_tag_present_fails_validation()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw);
        candidate.ApplyAnalysis(
            """{}""", null, null, null, null, null,
            "[]", "[]", null, null, null, null, "[\"unsafe_content\"]", null, "hello");
        _db.SaveChanges();

        var result = await _sut.ValidateAsync(candidate.Id);

        result.Status.Should().Be(ResourceCandidateValidationStatus.Failed.ToString());
        result.Errors.Should().Contain(e => e.Contains("Safety concern"));
    }

    [Fact]
    public async Task No_rows_are_ever_written_to_any_published_cefr_bank_table()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source);
        var candidate = SeedCandidate(raw);

        await _sut.ValidateAsync(candidate.Id);

        (await _db.CefrDescriptors.CountAsync()).Should().Be(0);
        (await _db.ResourceBankItems.CountAsync()).Should().Be(0);
    }
}
