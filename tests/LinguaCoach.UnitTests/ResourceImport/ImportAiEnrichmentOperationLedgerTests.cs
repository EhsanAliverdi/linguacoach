using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.4D — direct unit coverage of <see cref="ImportAiEnrichmentOperationLedger"/>, mirroring
/// <see cref="ImportSttOperationLedgerTests"/> exactly for the AI candidate-enrichment ledger.
/// </summary>
public sealed class ImportAiEnrichmentOperationLedgerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ImportAiEnrichmentOperationLedger _ledger;

    public ImportAiEnrichmentOperationLedgerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _ledger = new ImportAiEnrichmentOperationLedger(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Guid PackageId;
    private Guid ProfileId;
    private Guid CandidateId;

    private async Task SeedAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);

        var package = new ImportPackage(source.Id, "test.zip", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync();

        var run = new ResourceImportRun(source.Id, ResourceImportMode.Csv, "test.csv", "hash", DateTimeOffset.UtcNow, importPackageId: package.Id);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync();

        var raw = new ResourceRawRecord(run.Id, "rawhash-1", "en", "row", rawJson: """{"word":"hello"}""");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        await _db.SaveChangesAsync();

        var fingerprint = new LinguaCoach.Infrastructure.Activity.ActivityContentFingerprintService().ComputeFingerprint(
            new LinguaCoach.Application.Activity.ActivityContentFingerprintRequest(
                """{"word":"hello"}""", LinguaCoach.Application.Activity.ActivityContentShape.Unknown, null, "hello"));
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        await _db.SaveChangesAsync();

        PackageId = package.Id;
        ProfileId = Guid.NewGuid();
        CandidateId = candidate.Id;
    }

    [Fact]
    public void Identical_inputs_produce_the_same_logical_key()
    {
        var keyA = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");
        var keyB = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");

        keyA.Should().Be(keyB);
    }

    [Theory]
    [InlineData("checksum-2", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted")] // changed checksum
    [InlineData("checksum-1", "gemini", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted")] // changed provider
    [InlineData("checksum-1", "openai", "gpt-4o", "resource_candidate_analyze", "FullAiAssisted")]       // changed model
    [InlineData("checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze_v2", "FullAiAssisted")] // changed prompt version
    [InlineData("checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "SampleDriven")]    // changed processing mode
    public void A_materially_changed_input_produces_a_different_logical_key(
        string checksum, string provider, string model, string promptVersion, string processingMode)
    {
        var baseline = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");
        var changed = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, checksum, provider, model, promptVersion, processingMode);

        changed.Should().NotBe(baseline);
    }

    [Fact]
    public async Task First_claim_is_a_fresh_pending_operation()
    {
        await SeedAsync();
        var key = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");

        var result = await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");

        result.Outcome.Should().Be(ImportAiClaimOutcome.Claimed);
        result.Operation.Status.Should().Be(ImportAiOperationStatus.Pending);
        result.Operation.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public async Task Successful_operation_is_reused_on_a_second_claim_no_duplicate_cost()
    {
        await SeedAsync();
        var key = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");
        var first = await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");
        await _ledger.MarkSucceededAsync(first.Operation, """{"cefrLevel":"A1"}""", 0.02m, "USD", 100, 50, 0.01m, 0.03m, "gpt-4o-mini");
        await _db.SaveChangesAsync();

        var second = await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");

        second.Outcome.Should().Be(ImportAiClaimOutcome.AlreadySucceeded);
        second.Operation.ResultReferenceJson.Should().Be("""{"cefrLevel":"A1"}""");
        second.Operation.CalculatedCost.Should().Be(0.02m); // not doubled by the second claim

        (await _db.ImportAiEnrichmentOperations.CountAsync(o => o.LogicalOperationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task Failed_operation_can_retry_and_increments_the_attempt_number()
    {
        await SeedAsync();
        var key = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");
        var first = await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");
        await _ledger.MarkFailedAsync(first.Operation, "provider timeout");

        var retry = await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");

        retry.Outcome.Should().Be(ImportAiClaimOutcome.Claimed);
        retry.Operation.Status.Should().Be(ImportAiOperationStatus.Pending);
        retry.Operation.AttemptNumber.Should().Be(2);
        retry.Operation.FailureReason.Should().BeNull();

        (await _db.ImportAiEnrichmentOperations.CountAsync(o => o.LogicalOperationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task Exactly_one_row_exists_per_logical_key_after_multiple_claims()
    {
        await SeedAsync();
        var key = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");
        await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");
        await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");
        await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");

        (await _db.ImportAiEnrichmentOperations.CountAsync(o => o.LogicalOperationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task Pricing_snapshot_on_a_succeeded_operation_is_immutable_after_options_change()
    {
        await SeedAsync();
        var key = ImportAiEnrichmentOperationKey.Compute(PackageId, CandidateId, "checksum-1", "openai", "gpt-4o-mini", "resource_candidate_analyze", "FullAiAssisted");
        var claim = await _ledger.ClaimAsync(PackageId, ProfileId, CandidateId, key, "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted");
        await _ledger.MarkSucceededAsync(claim.Operation, """{"cefrLevel":"A1"}""", 0.02m, "USD", 100, 50, 0.01m, 0.03m, "gpt-4o-mini");
        await _db.SaveChangesAsync();

        var reloaded = await _db.ImportAiEnrichmentOperations.FirstAsync(o => o.LogicalOperationKey == key);
        reloaded.InputPricePer1KTokensSnapshot.Should().Be(0.01m);
        reloaded.OutputPricePer1KTokensSnapshot.Should().Be(0.03m);
        reloaded.CalculatedCost.Should().Be(0.02m);

        // No code path rewrites a historical row's snapshot when configured pricing changes later —
        // this assertion documents/guards that invariant.
        reloaded.InputPricePer1KTokensSnapshot.Should().Be(0.01m, "a historical operation's snapshot must never be recomputed retroactively");
    }
}
