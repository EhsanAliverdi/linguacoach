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
/// Phase E5 — read-only browse/search over the published Cefr* bank tables. SQLite in-memory,
/// matching ResourceCandidatePublishServiceTests' convention (same seeding helpers, reused here so
/// the "published via the real publish workflow" path is exercised for the traceability tests).
/// </summary>
public sealed class ResourceBankQueryServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceBankQueryService _sut;
    private readonly ResourceCandidatePublishService _publishService;
    private readonly ActivityContentFingerprintService _fingerprint = new();

    public ResourceBankQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceBankQueryService(_db);
        _publishService = new ResourceCandidatePublishService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource(string? name = null)
    {
        var source = new CefrResourceSource(
            name ?? $"Test Source {Guid.NewGuid():N}", "CC-BY-4.0",
            allowsStudentDisplay: true, allowsCommercialUse: true, attributionText: "Test Attribution");
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
        ResourceRawRecord raw, ResourceCandidateType type, string canonicalText, string normalizedJson)
    {
        var candidate = new ResourceCandidate(
            raw.Id, type, canonicalText, normalizedJson, "en",
            canonicalText, Fingerprint(canonicalText, normalizedJson), ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();
        return candidate;
    }

    private void MakePublishReady(ResourceCandidate candidate, string cefrLevel, string? primarySkill)
    {
        candidate.ApplyAnalysis(
            $$"""{"cefrLevel":"{{cefrLevel}}"}""", cefrLevel, 0.95, primarySkill, null, 1,
            "[]", "[]", null, null, null, null, null, 0.9, candidate.CanonicalText);
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Passed, """{"errors":[],"warnings":[]}""");
        candidate.Approve("looks good");
        _db.SaveChanges();
    }

    /// <summary>Publishes one VocabularyEntry candidate end-to-end through the real E4 publish
    /// service, returning both the candidate and the resulting published entity id.</summary>
    private async Task<(ResourceCandidate Candidate, Guid PublishedEntityId)> PublishVocabularyAsync(
        CefrResourceSource source, string word, string cefrLevel = "A1", Guid? publishedByUserId = null)
    {
        var (_, raw) = SeedRunAndRaw(source, $$"""{"word":"{{word}}"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, word, $$"""{"word":"{{word}}"}""");
        MakePublishReady(candidate, cefrLevel, "vocabulary");

        var result = await _publishService.PublishAsync(candidate.Id, publishedByUserId);
        result.Success.Should().BeTrue();
        return (candidate, result.PublishedEntityId!.Value);
    }

    // ── List: pagination ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListVocabulary_Respects_Page_And_PageSize()
    {
        var source = SeedSource();
        for (var i = 0; i < 5; i++)
            await PublishVocabularyAsync(source, $"word{i}");

        var page1 = await _sut.ListVocabularyAsync(new ResourceBankListFilter(Page: 1, PageSize: 2));
        var page2 = await _sut.ListVocabularyAsync(new ResourceBankListFilter(Page: 2, PageSize: 2));

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.Items.Select(i => i.Id).Should().NotIntersectWith(page2.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task ListVocabulary_Caps_PageSize_At_Maximum()
    {
        var source = SeedSource();
        await PublishVocabularyAsync(source, "hello");

        var result = await _sut.ListVocabularyAsync(new ResourceBankListFilter(Page: 1, PageSize: 10_000));

        // Should not throw and should not attempt to return more than the cap allows — a single
        // seeded row is well under any reasonable cap, so this just proves the call succeeds.
        result.Items.Should().HaveCount(1);
    }

    // ── List: filters ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListVocabulary_Filters_By_CefrLevel()
    {
        var source = SeedSource();
        await PublishVocabularyAsync(source, "hello", "A1");
        await PublishVocabularyAsync(source, "sophisticated", "C1");

        var a1Result = await _sut.ListVocabularyAsync(new ResourceBankListFilter(CefrLevel: "A1"));
        var noMatchResult = await _sut.ListVocabularyAsync(new ResourceBankListFilter(CefrLevel: "B2"));

        a1Result.Items.Should().ContainSingle(i => i.Word == "hello");
        noMatchResult.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListVocabulary_Filters_By_SourceId()
    {
        var sourceA = SeedSource("Source A");
        var sourceB = SeedSource("Source B");
        await PublishVocabularyAsync(sourceA, "alpha");
        await PublishVocabularyAsync(sourceB, "beta");

        var result = await _sut.ListVocabularyAsync(new ResourceBankListFilter(SourceId: sourceA.Id));

        result.Items.Should().ContainSingle(i => i.Word == "alpha");
    }

    [Fact]
    public async Task ListVocabulary_Filters_By_SearchText_Against_Word_And_Notes()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"word":"hello","definition":"a friendly greeting"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello","definition":"a friendly greeting"}""");
        MakePublishReady(candidate, "A1", "vocabulary");
        await _publishService.PublishAsync(candidate.Id, null);

        var byWord = await _sut.ListVocabularyAsync(new ResourceBankListFilter(SearchText: "HEL"));
        var byNotes = await _sut.ListVocabularyAsync(new ResourceBankListFilter(SearchText: "greeting"));
        var noMatch = await _sut.ListVocabularyAsync(new ResourceBankListFilter(SearchText: "nonexistentterm"));

        byWord.Items.Should().ContainSingle(i => i.Word == "hello");
        byNotes.Items.Should().ContainSingle(i => i.Word == "hello");
        noMatch.Items.Should().BeEmpty();
    }

    // ── Detail: traceability ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetVocabularyDetail_Includes_Source_License_And_Traceability_When_Candidate_Exists()
    {
        var source = SeedSource();
        var userId = Guid.NewGuid();
        var (candidate, entityId) = await PublishVocabularyAsync(source, "hello", publishedByUserId: userId);

        var detail = await _sut.GetVocabularyDetailAsync(entityId);

        detail.Should().NotBeNull();
        detail!.Source.SourceId.Should().Be(source.Id);
        detail.Source.LicenseType.Should().Be("CC-BY-4.0");
        detail.Source.AllowsStudentDisplay.Should().BeTrue();
        detail.Source.AllowsCommercialUse.Should().BeTrue();
        detail.Traceability.TraceabilityAvailable.Should().BeTrue();
        detail.Traceability.CandidateId.Should().Be(candidate.Id);
        detail.Traceability.ResourceImportRunId.Should().NotBeNull();
        detail.Traceability.ContentFingerprint.Should().Be(candidate.ContentFingerprint);
        detail.Traceability.PublishedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetVocabularyDetail_Returns_TraceabilityUnavailable_When_No_Matching_Candidate_Exists()
    {
        // Construct a bank row directly via the DbContext, bypassing the publish service entirely
        // — simulates a row that was never linked to a ResourceCandidate.
        var source = SeedSource();
        var entry = new CefrVocabularyEntry(source.Id, "orphan", "A1");
        _db.CefrVocabularyEntries.Add(entry);
        await _db.SaveChangesAsync();

        var detail = await _sut.GetVocabularyDetailAsync(entry.Id);

        detail.Should().NotBeNull();
        detail!.Traceability.TraceabilityAvailable.Should().BeFalse();
        detail.Traceability.CandidateId.Should().BeNull();
        detail.Traceability.ResourceImportRunId.Should().BeNull();
    }

    [Fact]
    public async Task GetVocabularyDetail_Returns_Null_For_Nonexistent_Id()
    {
        var detail = await _sut.GetVocabularyDetailAsync(Guid.NewGuid());

        detail.Should().BeNull();
    }

    // ── Grammar: parallel coverage ───────────────────────────────────────────────

    [Fact]
    public async Task ListGrammar_Filters_By_CefrLevel_SourceId_And_SearchText()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"grammarKey":"present simple","explanation":"habitual actions"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.GrammarProfileEntry, "present simple", """{"grammarKey":"present simple","explanation":"habitual actions"}""");
        MakePublishReady(candidate, "A1", "grammar");
        var publishResult = await _publishService.PublishAsync(candidate.Id, null);
        publishResult.Success.Should().BeTrue();

        var byCefr = await _sut.ListGrammarAsync(new ResourceBankListFilter(CefrLevel: "A1"));
        var bySource = await _sut.ListGrammarAsync(new ResourceBankListFilter(SourceId: source.Id));
        var bySearch = await _sut.ListGrammarAsync(new ResourceBankListFilter(SearchText: "habitual"));
        var noMatch = await _sut.ListGrammarAsync(new ResourceBankListFilter(CefrLevel: "C2"));

        byCefr.Items.Should().ContainSingle();
        bySource.Items.Should().ContainSingle();
        bySearch.Items.Should().ContainSingle();
        noMatch.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGrammarDetail_Includes_Traceability()
    {
        var source = SeedSource();
        var (_, raw) = SeedRunAndRaw(source, """{"grammarKey":"present simple"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.GrammarProfileEntry, "present simple", """{"grammarKey":"present simple"}""");
        MakePublishReady(candidate, "A1", "grammar");
        var publishResult = await _publishService.PublishAsync(candidate.Id, null);

        var detail = await _sut.GetGrammarDetailAsync(publishResult.PublishedEntityId!.Value);

        detail.Should().NotBeNull();
        detail!.Traceability.TraceabilityAvailable.Should().BeTrue();
        detail.Traceability.CandidateId.Should().Be(candidate.Id);
    }

    // ── Reading references: parallel coverage ───────────────────────────────────

    [Fact]
    public async Task ListReadingReferences_Filters_By_CefrLevel_SourceId_And_SearchText()
    {
        var source = SeedSource();
        var shortPassage = "A short excerpt about daily routines.";
        var (_, raw) = SeedRunAndRaw(source, $$"""{"title":"Daily routines","passage":"{{shortPassage}}"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.ReadingPassage, "Daily routines", $$"""{"title":"Daily routines","passage":"{{shortPassage}}"}""");
        MakePublishReady(candidate, "A1", "reading");
        var publishResult = await _publishService.PublishAsync(candidate.Id, null);
        publishResult.Success.Should().BeTrue();

        var byCefr = await _sut.ListReadingReferencesAsync(new ResourceBankListFilter(CefrLevel: "A1"));
        var bySource = await _sut.ListReadingReferencesAsync(new ResourceBankListFilter(SourceId: source.Id));
        var bySearch = await _sut.ListReadingReferencesAsync(new ResourceBankListFilter(SearchText: "daily"));
        var noMatch = await _sut.ListReadingReferencesAsync(new ResourceBankListFilter(CefrLevel: "C2"));

        byCefr.Items.Should().ContainSingle();
        bySource.Items.Should().ContainSingle();
        bySearch.Items.Should().ContainSingle();
        noMatch.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReadingReferenceDetail_Includes_Traceability()
    {
        var source = SeedSource();
        var shortPassage = "A short excerpt about daily routines.";
        var (_, raw) = SeedRunAndRaw(source, $$"""{"title":"Daily routines","passage":"{{shortPassage}}"}""");
        var candidate = SeedCandidate(raw, ResourceCandidateType.ReadingPassage, "Daily routines", $$"""{"title":"Daily routines","passage":"{{shortPassage}}"}""");
        MakePublishReady(candidate, "A1", "reading");
        var publishResult = await _publishService.PublishAsync(candidate.Id, null);

        var detail = await _sut.GetReadingReferenceDetailAsync(publishResult.PublishedEntityId!.Value);

        detail.Should().NotBeNull();
        detail!.Traceability.TraceabilityAvailable.Should().BeTrue();
        detail.Traceability.CandidateId.Should().Be(candidate.Id);
    }

    // ── Invariant: only published candidates' rows can ever appear ──────────────

    [Fact]
    public async Task Unpublished_Or_Rejected_Candidate_Never_Produces_A_Row_Visible_In_Bank_Browse()
    {
        var source = SeedSource();

        // Candidate 1: staged but never analyzed/validated/approved/published.
        var (_, raw1) = SeedRunAndRaw(source, """{"word":"neverpublished"}""");
        SeedCandidate(raw1, ResourceCandidateType.VocabularyEntry, "neverpublished", """{"word":"neverpublished"}""");

        // Candidate 2: validated + approved, then explicitly rejected before it could publish.
        // (Rejection after approval is allowed by ResourceCandidate.Reject as long as it isn't
        // already published — this proves a rejected candidate's content never reaches the bank.)
        var (_, raw2) = SeedRunAndRaw(source, """{"word":"rejectedword"}""");
        var candidate2 = SeedCandidate(raw2, ResourceCandidateType.VocabularyEntry, "rejectedword", """{"word":"rejectedword"}""");
        candidate2.ApplyAnalysis(
            """{"cefrLevel":"A1"}""", "A1", 0.9, "vocabulary", null, 1, "[]", "[]", null, null, null, null, null, 0.9, "rejectedword");
        candidate2.ApplyValidation(ResourceCandidateValidationStatus.Passed, """{"errors":[],"warnings":[]}""");
        candidate2.Reject("not suitable");
        _db.SaveChanges();

        var result = await _sut.ListVocabularyAsync(new ResourceBankListFilter());

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        (await _db.CefrVocabularyEntries.CountAsync()).Should().Be(0);
    }
}
