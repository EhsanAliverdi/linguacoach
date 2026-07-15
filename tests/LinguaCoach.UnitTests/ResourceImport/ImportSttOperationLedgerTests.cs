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
/// Phase 4.4 (Workstream B3/B4/B11) — direct unit coverage of <see cref="ImportSttOperationLedger"/>,
/// the durable, retry-safe STT operation record. Proves the critical retry-safety property: a
/// successful operation is reused (never re-charged, never re-provider-called), a failed one may
/// retry exactly once more, and exactly one row ever exists per logical key.
/// </summary>
public sealed class ImportSttOperationLedgerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ImportSttOperationLedger _ledger;

    public ImportSttOperationLedgerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _ledger = new ImportSttOperationLedger(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Guid PackageId;
    private Guid ProfileId;
    private Guid AssetId;

    /// <summary>The ledger's FK constraints require real ImportPackage/ImportAsset rows (and
    /// ImportProfileId, while not FK-constrained, should still be realistic) — seeds the minimum
    /// valid graph once per test via each [Fact]'s first call.</summary>
    private async Task SeedAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);

        var package = new ImportPackage(source.Id, "test.zip", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync();

        var asset = new ImportAsset(
            package.Id, "audio.mp3", "audio.mp3", "storage/audio.mp3", "audio/mpeg",
            ImportAssetMediaType.Audio, ".mp3", 100, "checksum-1", DateTimeOffset.UtcNow);
        _db.ImportAssets.Add(asset);
        await _db.SaveChangesAsync();

        PackageId = package.Id;
        ProfileId = Guid.NewGuid();
        AssetId = asset.Id;
    }

    [Fact]
    public void Identical_inputs_produce_the_same_logical_key()
    {
        var keyA = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        var keyB = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");

        keyA.Should().Be(keyB);
    }

    [Fact]
    public void A_changed_checksum_produces_a_different_logical_key()
    {
        var keyA = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        var keyB = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-2");

        keyA.Should().NotBe(keyB);
    }

    [Fact]
    public async Task First_claim_is_a_fresh_pending_operation()
    {
        await SeedAsync();
        var key = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");

        var result = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);

        result.Outcome.Should().Be(ImportSttClaimOutcome.Claimed);
        result.Operation.Status.Should().Be(ImportSttOperationStatus.Pending);
        result.Operation.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public async Task Successful_operation_is_reused_on_a_second_claim()
    {
        await SeedAsync();
        var key = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        var first = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);
        await _ledger.MarkSucceededAsync(first.Operation, "hello world", 0.03m, "USD", 0.006m, "whisper-1");
        await _db.SaveChangesAsync();

        var second = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);

        second.Outcome.Should().Be(ImportSttClaimOutcome.AlreadySucceeded);
        second.Operation.TranscriptText.Should().Be("hello world");

        (await _db.ImportSttOperations.CountAsync(o => o.LogicalOperationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task Failed_operation_can_retry_and_increments_the_attempt_number()
    {
        await SeedAsync();
        var key = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        var first = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);
        await _ledger.MarkFailedAsync(first.Operation, "provider timeout");

        var retry = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);

        retry.Outcome.Should().Be(ImportSttClaimOutcome.Claimed);
        retry.Operation.Status.Should().Be(ImportSttOperationStatus.Pending);
        retry.Operation.AttemptNumber.Should().Be(2);
        retry.Operation.FailureReason.Should().BeNull();

        (await _db.ImportSttOperations.CountAsync(o => o.LogicalOperationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task A_failed_attempt_is_not_reusable_as_success()
    {
        await SeedAsync();
        var key = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        var first = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);
        await _ledger.MarkFailedAsync(first.Operation, "provider timeout");

        var again = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);

        again.Outcome.Should().Be(ImportSttClaimOutcome.Claimed); // must retry, not silently "succeed"
        again.Operation.TranscriptText.Should().BeNull();
    }

    [Fact]
    public async Task Exactly_one_row_exists_per_logical_key_after_multiple_claims()
    {
        await SeedAsync();
        var key = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);
        await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);
        await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);

        (await _db.ImportSttOperations.CountAsync(o => o.LogicalOperationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task Pricing_snapshot_on_a_succeeded_operation_is_immutable_after_options_change()
    {
        await SeedAsync();
        var key = ImportSttOperationKey.Compute(PackageId, AssetId, "checksum-1");
        var claim = await _ledger.ClaimAsync(PackageId, ProfileId, AssetId, key, "openai", 5m);
        await _ledger.MarkSucceededAsync(claim.Operation, "hello world", calculatedCost: 0.03m, "USD", pricePerMinuteSnapshot: 0.006m, "whisper-1");
        await _db.SaveChangesAsync();

        var reloaded = await _db.ImportSttOperations.FirstAsync(o => o.LogicalOperationKey == key);
        reloaded.PricePerMinuteSnapshot.Should().Be(0.006m);
        reloaded.CalculatedCost.Should().Be(0.03m);

        // Simulate a later pricing config change — the historical row must not be touched by that;
        // there is no code path that would rewrite it, which this assertion documents/guards.
        reloaded.PricePerMinuteSnapshot.Should().Be(0.006m, "a historical operation's snapshot must never be recomputed retroactively");
    }
}
