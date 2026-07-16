using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase K2 — batch approve/publish over an explicit set of candidate ids. SQLite in-memory,
/// matching ResourceCandidatePublishServiceTests' convention. Exercises the same warning-only
/// NeedsReview publish path as the single-item flow, plus batch-specific behavior (continue-on-
/// error, already-published skip, per-item error reporting).
/// </summary>
public sealed class ResourceCandidateBatchActionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceCandidateBatchActionService _sut;
    private readonly ActivityContentFingerprintService _fingerprint = new();

    public ResourceCandidateBatchActionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var approveHandler = new AdminResourceCandidateApproveHandler(_db, new ResourceCandidateContentSerializer());
        var rejectHandler = new AdminResourceCandidateRejectHandler(_db);
        var skipHandler = new AdminResourceCandidateSkipHandler(_db);
        var publishService = new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer());
        _sut = new ResourceCandidateBatchActionService(_db, approveHandler, rejectHandler, skipHandler, publishService);
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

    /// <summary>Seeds a vocabulary candidate at the given ValidationStatus, with CEFR/skill set so
    /// it would map to a bank row if it does end up publishable.</summary>
    private ResourceCandidate SeedCandidate(
        CefrResourceSource source, string word, ResourceCandidateValidationStatus validationStatus,
        bool approve = true)
    {
        var raw = SeedRaw(source, $$"""{"word":"{{word}}"}""");
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, word, $$"""{"word":"{{word}}"}""", "en",
            word, Fingerprint(word, $$"""{"word":"{{word}}"}"""), ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();

        candidate.ApplyAnalysis(
            """{}""", "A1", 0.95, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, word);
        candidate.ApplyValidation(validationStatus, """{"errors":[],"warnings":[]}""");
        if (approve) candidate.Approve();
        _db.SaveChanges();
        return candidate;
    }

    // Phase 4.2 — the batch ApproveAndPublish shortcut was removed (it bypassed the separate
    // Phase 3 approve/publish review lifecycle); BatchApproveAsync + BatchPublishAsync remain and
    // are covered below.

    [Fact]
    public async Task Batch_publish_treats_an_already_published_candidate_as_a_safe_no_op()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source, "hello", ResourceCandidateValidationStatus.Passed);
        var firstPublish = await new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer()).PublishAsync(candidate.Id, null);
        firstPublish.Success.Should().BeTrue();

        var result = await _sut.PublishAsync(new BatchPublishResourceCandidatesCommand(new[] { candidate.Id }), null);

        result.RequestedCount.Should().Be(1);
        result.AlreadyPublishedCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(1);
    }

    [Fact]
    public async Task Batch_approve_sets_ReviewStatus_Approved_for_every_requested_candidate()
    {
        var source = SeedSource();
        var a = SeedCandidate(source, "hello", ResourceCandidateValidationStatus.NeedsReview, approve: false);
        var b = SeedCandidate(source, "world", ResourceCandidateValidationStatus.Passed, approve: false);

        var result = await _sut.ApproveAsync(new BatchApproveResourceCandidatesCommand(new[] { a.Id, b.Id }));

        result.SucceededCount.Should().Be(2);
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == a.Id)).ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Approved);
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == b.Id)).ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Approved);
    }

    [Fact]
    public async Task Batch_reject_sets_ReviewStatus_Rejected_for_every_requested_candidate()
    {
        var source = SeedSource();
        var a = SeedCandidate(source, "hello", ResourceCandidateValidationStatus.NeedsReview, approve: false);
        var b = SeedCandidate(source, "world", ResourceCandidateValidationStatus.Passed, approve: false);

        var result = await _sut.RejectAsync(new BatchRejectResourceCandidatesCommand(new[] { a.Id, b.Id }, "not usable"));

        result.SucceededCount.Should().Be(2);
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == a.Id)).ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Rejected);
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == b.Id)).ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Rejected);
    }

    [Fact]
    public async Task Batch_skip_sets_ReviewStatus_Skipped_for_every_requested_candidate()
    {
        var source = SeedSource();
        var a = SeedCandidate(source, "hello", ResourceCandidateValidationStatus.NeedsReview, approve: false);
        var b = SeedCandidate(source, "world", ResourceCandidateValidationStatus.Passed, approve: false);

        var result = await _sut.SkipAsync(new BatchSkipResourceCandidatesCommand(new[] { a.Id, b.Id }));

        result.SucceededCount.Should().Be(2);
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == a.Id)).ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Skipped);
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == b.Id)).ReviewStatus.Should().Be(ResourceCandidateReviewStatus.Skipped);
    }

    [Fact]
    public async Task Skipped_candidate_cannot_be_published()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source, "hello", ResourceCandidateValidationStatus.Passed, approve: false);
        await _sut.SkipAsync(new BatchSkipResourceCandidatesCommand(new[] { candidate.Id }));

        var result = await new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer()).PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ReviewStatus"));
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id)).IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task Rejected_candidate_cannot_be_published()
    {
        var source = SeedSource();
        var candidate = SeedCandidate(source, "hello", ResourceCandidateValidationStatus.Passed, approve: false);
        await _sut.RejectAsync(new BatchRejectResourceCandidatesCommand(new[] { candidate.Id }, "no good"));

        var result = await new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer()).PublishAsync(candidate.Id, null);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ReviewStatus"));
        (await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id)).IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task Batch_action_caps_at_MaxBatchSize_and_reports_BatchLimitReached()
    {
        var source = SeedSource();
        var ids = new List<Guid>();
        for (var i = 0; i < ResourceCandidateBatchActionService.MaxBatchSize + 5; i++)
        {
            var c = SeedCandidate(source, $"word{i}", ResourceCandidateValidationStatus.Passed, approve: false);
            ids.Add(c.Id);
        }

        var result = await _sut.ApproveAsync(new BatchApproveResourceCandidatesCommand(ids));

        result.BatchLimitReached.Should().BeTrue();
        result.RequestedCount.Should().Be(ResourceCandidateBatchActionService.MaxBatchSize);
    }
}
