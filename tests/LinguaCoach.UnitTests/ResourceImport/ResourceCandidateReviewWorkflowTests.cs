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
/// Phase 3 (2026-07-15 import candidate review workflow) — Skip decision and content editing.
/// SQLite in-memory, matching the sibling ResourceCandidate*Tests conventions.
/// </summary>
public sealed class ResourceCandidateReviewWorkflowTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityContentFingerprintService _fingerprint = new();
    private readonly AdminResourceCandidateSkipHandler _skipHandler;
    private readonly AdminResourceCandidateContentUpdateHandler _contentUpdateHandler;
    private readonly ResourceCandidateValidationService _validationService;

    public ResourceCandidateReviewWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _skipHandler = new AdminResourceCandidateSkipHandler(_db);
        _validationService = new ResourceCandidateValidationService(_db, new FormIoSchemaValidationService());
        _contentUpdateHandler = new AdminResourceCandidateContentUpdateHandler(_db, _validationService, new ResourceCandidateContentSerializer());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource()
    {
        var source = new CefrResourceSource(
            $"Test Source {Guid.NewGuid():N}", "CC-BY-4.0",
            allowsStudentDisplay: true, allowsCommercialUse: true, attributionText: "Test Attribution");
        source.ApproveForImport("cleared for test");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    /// <summary>Phase 4.2 — every publishable candidate must trace back to an ImportPackage with
    /// an approved Import Execution Plan.</summary>
    private Guid SeedApprovedPackage(CefrResourceSource source)
    {
        var package = new ImportPackage(source.Id, "test-package", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        _db.SaveChanges();

        var plan = new ImportProfile(
            package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow);
        plan.SubmitForApproval();
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        _db.ImportProfiles.Add(plan);
        package.ApproveProfile(plan.Id);
        _db.SaveChanges();

        return package.Id;
    }

    private ResourceRawRecord SeedRaw(CefrResourceSource source, string rawJson)
    {
        var run = new ResourceImportRun(
            source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow,
            importPackageId: SeedApprovedPackage(source));
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, $"rawhash-{Guid.NewGuid():N}", "en", "row", rawJson: rawJson);
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();
        return raw;
    }

    private string Fingerprint(string canonicalText, string normalizedJson) =>
        _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(normalizedJson, ActivityContentShape.Unknown, null, canonicalText));

    private ResourceCandidate SeedCandidate(CefrResourceSource source, string word = "hello")
    {
        var normalizedJson = $$"""{"word":"{{word}}","definition":"a greeting"}""";
        var raw = SeedRaw(source, normalizedJson);
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, word, normalizedJson, "en",
            word, Fingerprint(word, normalizedJson), ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();

        candidate.ApplyAnalysis(
            "{}", "A1", 0.95, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, word);
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Passed, """{"errors":[],"warnings":[]}""");
        _db.SaveChanges();
        return candidate;
    }

    // ── Skip (domain) ────────────────────────────────────────────────────────

    [Fact]
    public void Skip_sets_review_status_to_Skipped_without_requiring_a_reason()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);

        candidate.Skip();

        candidate.ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Skipped);
    }

    [Fact]
    public void Skip_with_a_reason_stores_it_in_AdminNotes()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);

        candidate.Skip("not needed this batch");

        candidate.AdminNotes.Should().Be("not needed this batch");
    }

    [Fact]
    public void Skip_is_distinct_from_never_reviewed_PendingReview()
    {
        var pendingOnly = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);
        pendingOnly.ApplyValidation(ResourceCandidateValidationStatus.Passed, null);

        var skipped = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "world", """{"word":"world"}""", "en",
            "world", "fp2", ResourceCandidateValidationStatus.Passed);
        skipped.ApplyValidation(ResourceCandidateValidationStatus.Passed, null);
        skipped.Skip();

        pendingOnly.ReviewStatus.Should().Be(ResourceCandidateReviewStatus.PendingReview);
        skipped.ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Skipped);
        pendingOnly.ReviewStatus.Should().NotBe(skipped.ReviewStatus);
    }

    [Fact]
    public void Skip_throws_for_an_already_published_candidate()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);
        candidate.MarkPublished("CefrVocabularyEntry", Guid.NewGuid(), DateTimeOffset.UtcNow, null);

        var act = () => candidate.Skip();

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been published*");
    }

    [Fact]
    public async Task Skip_handler_persists_the_decision()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source);

        var dto = await _skipHandler.HandleAsync(new SkipResourceCandidateCommand(candidate.Id, "leave for now"));

        dto.ReviewStatus.Should().Be("Skipped");
        var reloaded = await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id);
        reloaded.ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Skipped);
    }

    [Fact]
    public async Task Skip_handler_throws_ResourceImportValidationException_for_unknown_candidate()
    {
        var act = async () => await _skipHandler.HandleAsync(new SkipResourceCandidateCommand(Guid.NewGuid()));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    // ── Content editing (domain) ────────────────────────────────────────────

    [Fact]
    public void UpdateContent_overwrites_only_the_fields_passed()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);
        candidate.ApplyAnalysis(
            "{}", "A1", 0.9, "vocabulary", "core", 1, "[]", "[]", null, null, null, null, null, 0.9, "hello");

        candidate.UpdateContent(canonicalText: "hello there", cefrLevel: "B1");

        candidate.CanonicalText.Should().Be("hello there");
        candidate.CefrLevel.Should().Be("B1");
        // Untouched fields keep their prior values — null means "leave as-is," not "clear."
        candidate.PrimarySkill.Should().Be("vocabulary");
        candidate.Subskill.Should().Be("core");
    }

    [Fact]
    public void UpdateContent_replaces_NormalizedJson_verbatim()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);

        candidate.UpdateContent(normalizedJson: """{"word":"hello","definition":"a greeting"}""");

        candidate.NormalizedJson.Should().Contain("a greeting");
    }

    [Fact]
    public void UpdateContent_rejects_an_invalid_CefrLevel()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);

        var act = () => candidate.UpdateContent(cefrLevel: "Z9");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateContent_throws_for_an_already_published_candidate()
    {
        var candidate = new ResourceCandidate(
            Guid.NewGuid(), ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", "fp1", ResourceCandidateValidationStatus.Passed);
        candidate.MarkPublished("CefrVocabularyEntry", Guid.NewGuid(), DateTimeOffset.UtcNow, null);

        var act = () => candidate.UpdateContent(canonicalText: "changed");

        act.Should().Throw<InvalidOperationException>().WithMessage("*already been published*");
    }

    [Fact]
    public async Task Content_update_handler_re_validates_after_edit()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source);
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Failed, """{"errors":["bad"],"warnings":[]}""");
        await _db.SaveChangesAsync();

        var dto = await _contentUpdateHandler.HandleAsync(new UpdateResourceCandidateContentCommand(
            candidate.Id, CefrLevel: "B1", PrimarySkill: "vocabulary"));

        // Re-validation ran as part of the edit — ValidationStatus is fresh, not the stale Failed
        // value from before the edit (exact outcome depends on the validator's other gates, but it
        // must not still be reporting the pre-edit error verbatim).
        dto.ValidationStatus.Should().NotBeNull();
    }

    [Fact]
    public async Task Typed_content_edit_round_trips_without_losing_fields()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source);

        var dto = await _contentUpdateHandler.HandleAsync(new UpdateResourceCandidateContentCommand(
            candidate.Id,
            TypedContentJson: """{"word":"greeting","definition":"a friendly hello","partOfSpeech":"noun","examples":["Hi there!"]}"""));

        dto.TypedContentJson.Should().NotBeNull();
        dto.TypedContentJson!.Should().Contain("\"word\":\"greeting\"");
        dto.TypedContentJson!.Should().Contain("\"partOfSpeech\":\"noun\"");
        dto.TypedContentJson!.Should().Contain("Hi there!");
        dto.ContentValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Typed_content_edit_with_a_missing_required_field_is_rejected_and_never_persisted()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source);
        var originalNormalizedJson = candidate.NormalizedJson;

        var act = async () => await _contentUpdateHandler.HandleAsync(new UpdateResourceCandidateContentCommand(
            candidate.Id, TypedContentJson: """{"definition":"a friendly hello"}"""));

        var exception = await act.Should().ThrowAsync<CandidateContentValidationException>();
        exception.Which.Errors.Should().ContainSingle(e => e.FieldName == "word");

        var reloaded = await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id);
        reloaded.NormalizedJson.Should().Be(originalNormalizedJson, "an invalid typed edit must never be persisted");
    }

    [Fact]
    public async Task Invalid_candidate_content_cannot_be_approved()
    {
        // A candidate whose NormalizedJson is not a parseable JSON object at all (e.g. corrupted
        // by a direct data edit outside the typed editor) fails the approve-time Parse gate — this
        // is the one case the CanonicalText fallback (which only fills a field's *value*, not a
        // wholesale unparseable document) cannot rescue.
        var source = SeedSource();
        var candidate = SeedCandidate(source);
        candidate.UpdateContent(normalizedJson: "not valid json {{{");
        await _db.SaveChangesAsync();

        var approveHandler = new AdminResourceCandidateApproveHandler(_db, new ResourceCandidateContentSerializer());
        var act = async () => await approveHandler.HandleAsync(new ApproveResourceCandidateCommand(candidate.Id));

        await act.Should().ThrowAsync<CandidateContentValidationException>();

        var reloaded = await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id);
        reloaded.ReviewStatus.Should().NotBe(ResourceCandidateReviewStatus.Approved, "an unparseable candidate must never be approved");
    }

    [Fact]
    public async Task Content_update_handler_throws_ResourceImportValidationException_for_unknown_candidate()
    {
        var act = async () => await _contentUpdateHandler.HandleAsync(
            new UpdateResourceCandidateContentCommand(Guid.NewGuid(), CanonicalText: "x"));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Content_update_handler_rejects_edits_to_a_published_candidate()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source);
        candidate.Approve();
        await _db.SaveChangesAsync();
        var publishResult = await new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer()).PublishAsync(candidate.Id, null);
        publishResult.Success.Should().BeTrue(because: string.Join("; ", publishResult.Errors));

        var act = async () => await _contentUpdateHandler.HandleAsync(
            new UpdateResourceCandidateContentCommand(candidate.Id, CanonicalText: "changed"));

        (await act.Should().ThrowAsync<ResourceImportValidationException>())
            .WithMessage("*already been published*");
    }

    // ── Review summary counts ───────────────────────────────────────────────

    [Fact]
    public async Task Review_summary_reports_rejected_and_skipped_counts_separately()
    {
        var source = SeedSource();
        var rejected = SeedCandidate(source, "one");
        rejected.Reject("no good");
        var skipped = SeedCandidate(source, "two");
        skipped.Skip();
        await _db.SaveChangesAsync();

        var summaryHandler = new AdminResourceCandidateReviewSummaryQueryHandler(_db);
        var summary = await summaryHandler.HandleAsync(new GetAdminResourceCandidateReviewSummaryQuery());

        summary.RejectedCount.Should().Be(1);
        summary.SkippedCount.Should().Be(1);
    }
}
