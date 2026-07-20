using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>Phase E1 admin CRUD/list handlers for CefrResourceSource and ResourceCandidate.</summary>
public sealed class AdminResourceHandlersTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceHandlersTests()
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
    public async Task Source_can_be_created_listed_updated_approved_and_revoked()
    {
        var addHandler = new AdminAddResourceSourceHandler(_db);
        var updateHandler = new AdminUpdateResourceSourceHandler(_db);
        var approvalHandler = new AdminResourceSourceApprovalHandler(_db);
        var listQuery = new AdminResourceSourceListQueryHandler(_db);

        var created = await addHandler.HandleAsync(new AddResourceSourceCommand(
            "Sample Source", "CC-BY-4.0", null, null, "en", false, false, null, null, null));
        created.IsImportApproved.Should().BeFalse();

        var updated = await updateHandler.HandleAsync(new UpdateResourceSourceCommand(
            created.SourceId, "Sample Source Renamed", "CC-BY-4.0", null, null, "en", true, true, "Attribution", "v1", null));
        updated.Name.Should().Be("Sample Source Renamed");
        updated.IsImportApproved.Should().BeFalse("metadata Update must never silently touch approval");

        var approved = await approvalHandler.HandleAsync(new SetResourceSourceApprovalCommand(created.SourceId, true, "cleared"));
        approved.IsImportApproved.Should().BeTrue();

        var revoked = await approvalHandler.HandleAsync(new SetResourceSourceApprovalCommand(created.SourceId, false, "license changed"));
        revoked.IsImportApproved.Should().BeFalse();

        var list = await listQuery.HandleAsync(new ListAdminResourceSourcesQuery());
        list.Items.Should().ContainSingle(s => s.SourceId == created.SourceId);
    }

    [Fact]
    public async Task List_query_filters_by_candidate_type_and_validation_status()
    {
        var source = new CefrResourceSource("Filter Test Source", "CC-BY-4.0");
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();

        var run = new ResourceImportRun(source.Id, ResourceImportMode.Json, "f.json", "hash1", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync();

        var raw1 = new ResourceRawRecord(run.Id, "h1", "en", "row", rawJson: "{\"word\":\"hello\"}");
        raw1.MarkParsed();
        var raw2 = new ResourceRawRecord(run.Id, "h2", "en", "row", rawJson: "{\"title\":\"A Passage\",\"text\":\"Some text\"}");
        raw2.MarkParsed();
        _db.ResourceRawRecords.AddRange(raw1, raw2);
        await _db.SaveChangesAsync();

        var vocabCandidate = new ResourceCandidate(
            raw1.Id, ResourceCandidateType.VocabularyEntry, "hello", "{}", "en", "hello",
            "fp1", ResourceCandidateValidationStatus.NeedsReview);
        var passageCandidate = new ResourceCandidate(
            raw2.Id, ResourceCandidateType.ReadingPassage, "A Passage", "{}", "en", "a passage some text",
            "fp2", ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.AddRange(vocabCandidate, passageCandidate);
        await _db.SaveChangesAsync();

        var listQuery = new AdminResourceCandidateListQueryHandler(_db);

        var vocabOnly = await listQuery.HandleAsync(new ListAdminResourceCandidatesQuery(CandidateType: "VocabularyEntry"));
        vocabOnly.Items.Should().ContainSingle(c => c.CandidateId == vocabCandidate.Id);

        var bySource = await listQuery.HandleAsync(new ListAdminResourceCandidatesQuery(SourceId: source.Id));
        bySource.Items.Should().HaveCount(2);

        var byValidation = await listQuery.HandleAsync(
            new ListAdminResourceCandidatesQuery(ValidationStatus: "NeedsReview"));
        byValidation.Items.Should().HaveCount(2);
    }

    // ── Phase K2 — CanAttemptPublish/PublishBlockReason DTO mapping, PublishableOnly filter,
    // review-state summary ──────────────────────────────────────────────────────────────────

    private static ResourceCandidate SeedCandidateAt(
        LinguaCoachDbContext db, ResourceRawRecord raw, string word, ResourceCandidateValidationStatus status)
    {
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, word, $$"""{"word":"{{word}}"}""", "en",
            word, $"fp-{Guid.NewGuid():N}", ResourceCandidateValidationStatus.NeedsReview);
        db.ResourceCandidates.Add(candidate);
        db.SaveChanges();
        candidate.ApplyValidation(status, status == ResourceCandidateValidationStatus.Failed
            ? """{"errors":["Safety concern(s) reported: flagged"],"warnings":[]}"""
            : """{"errors":[],"warnings":[]}""");
        db.SaveChanges();
        return candidate;
    }

    private async Task<(CefrResourceSource Source, ResourceRawRecord Raw)> SeedSourceAndRawAsync()
    {
        var source = new CefrResourceSource("Summary Test Source", "CC-BY-4.0");
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();

        var run = new ResourceImportRun(source.Id, ResourceImportMode.Json, "f.json", $"hash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync();

        var raw = new ResourceRawRecord(run.Id, $"rh-{Guid.NewGuid():N}", "en", "row", rawJson: "{}");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        await _db.SaveChangesAsync();

        return (source, raw);
    }

    [Fact]
    public async Task Dto_CanAttemptPublish_is_true_for_Passed_and_NeedsReview_false_for_Failed_and_Pending()
    {
        var (source, raw) = await SeedSourceAndRawAsync();
        var passed = SeedCandidateAt(_db, raw, "a", ResourceCandidateValidationStatus.Passed);
        var needsReview = SeedCandidateAt(_db, raw, "b", ResourceCandidateValidationStatus.NeedsReview);
        var failed = SeedCandidateAt(_db, raw, "c", ResourceCandidateValidationStatus.Failed);

        var getQuery = new AdminResourceCandidateGetQueryHandler(_db);

        (await getQuery.HandleAsync(new GetAdminResourceCandidateQuery(passed.Id)))!.CanAttemptPublish.Should().BeTrue();
        (await getQuery.HandleAsync(new GetAdminResourceCandidateQuery(needsReview.Id)))!.CanAttemptPublish.Should().BeTrue();

        var failedDto = await getQuery.HandleAsync(new GetAdminResourceCandidateQuery(failed.Id));
        failedDto!.CanAttemptPublish.Should().BeFalse();
        failedDto.PublishBlockReason.Should().Contain("Safety concern");
    }

    [Fact]
    public async Task PublishableOnly_filter_returns_only_not_yet_published_Passed_or_NeedsReview_candidates()
    {
        var (source, raw) = await SeedSourceAndRawAsync();
        var passed = SeedCandidateAt(_db, raw, "a", ResourceCandidateValidationStatus.Passed);
        var needsReview = SeedCandidateAt(_db, raw, "b", ResourceCandidateValidationStatus.NeedsReview);
        SeedCandidateAt(_db, raw, "c", ResourceCandidateValidationStatus.Failed);
        SeedCandidateAt(_db, raw, "d", ResourceCandidateValidationStatus.Pending);

        var listQuery = new AdminResourceCandidateListQueryHandler(_db);
        var result = await listQuery.HandleAsync(new ListAdminResourceCandidatesQuery(PublishableOnly: true));

        result.Items.Select(i => i.CandidateId).Should().BeEquivalentTo(new[] { passed.Id, needsReview.Id });
    }

    [Fact]
    public async Task Review_summary_reports_passed_needsReview_blocked_and_published_counts_separately()
    {
        var (source, raw) = await SeedSourceAndRawAsync();
        SeedCandidateAt(_db, raw, "a", ResourceCandidateValidationStatus.Passed);
        SeedCandidateAt(_db, raw, "b", ResourceCandidateValidationStatus.NeedsReview);
        SeedCandidateAt(_db, raw, "c", ResourceCandidateValidationStatus.NeedsReview);
        SeedCandidateAt(_db, raw, "d", ResourceCandidateValidationStatus.Failed);
        SeedCandidateAt(_db, raw, "e", ResourceCandidateValidationStatus.Pending);

        var summaryQuery = new AdminResourceCandidateReviewSummaryQueryHandler(_db);
        var summary = await summaryQuery.HandleAsync(new GetAdminResourceCandidateReviewSummaryQuery());

        summary.TotalCount.Should().Be(5);
        summary.PassedCount.Should().Be(1);
        summary.NeedsReviewCount.Should().Be(2);
        summary.BlockedCount.Should().Be(2); // Failed + Pending
        summary.PublishedCount.Should().Be(0);
        summary.PublishableCount.Should().Be(3); // Passed + NeedsReview
    }

    [Fact]
    public async Task Review_summary_reports_stuck_approved_but_unpublishable_candidates_separately()
    {
        // Sprint 12 — this is exactly the Sprint 8.1 scenario: approved for publish, but
        // validation never reached Passed, so PublishAsync's own gate rejects it forever.
        var (source, raw) = await SeedSourceAndRawAsync();
        var stuckFailed = SeedCandidateAt(_db, raw, "a", ResourceCandidateValidationStatus.Failed);
        stuckFailed.Approve();
        var stuckPending = SeedCandidateAt(_db, raw, "b", ResourceCandidateValidationStatus.Pending);
        stuckPending.Approve();
        // A normal not-yet-approved Failed candidate should NOT count here — only Approved ones.
        SeedCandidateAt(_db, raw, "c", ResourceCandidateValidationStatus.Failed);
        await _db.SaveChangesAsync();

        var summaryQuery = new AdminResourceCandidateReviewSummaryQueryHandler(_db);
        var summary = await summaryQuery.HandleAsync(new GetAdminResourceCandidateReviewSummaryQuery());

        summary.StuckApprovedUnpublishableCount.Should().Be(2);
    }
}
