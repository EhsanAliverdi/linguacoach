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
}
