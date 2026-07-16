using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.8 (2026-07-17 security/concurrency/idempotency) — the database-level half of the
/// claim/lease protection: proves two independent <see cref="LinguaCoachDbContext"/> instances
/// (mirroring two separate worker processes/connections) racing to claim the same
/// <see cref="ImportPackage"/> row cannot both win, using the real EF concurrency-token mechanism
/// configured in <c>ImportPackageConfiguration</c> — not a mock or an in-memory-only guard.
/// </summary>
public sealed class ImportPackageClaimConcurrencyTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public ImportPackageClaimConcurrencyTests()
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

    private LinguaCoachDbContext OpenSecondContext()
    {
        var connection = _db.Database.GetDbConnection();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>().UseSqlite(connection).Options;
        return new LinguaCoachDbContext(options);
    }

    private async Task<Guid> SeedPackageAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);
        var package = new ImportPackage(source.Id, "package.zip", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync();
        return package.Id;
    }

    [Fact]
    public async Task Two_workers_cannot_claim_the_same_package_simultaneously()
    {
        var packageId = await SeedPackageAsync();
        await using var otherDb = OpenSecondContext();

        var now = DateTimeOffset.UtcNow;
        var packageA = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var packageB = await otherDb.ImportPackages.FirstAsync(p => p.Id == packageId);

        // Both workers observe the package as unclaimed before either commits — the exact race
        // ProcessPendingAsync's TryClaimAsync must survive.
        packageA.IsClaimable(now).Should().BeTrue();
        packageB.IsClaimable(now).Should().BeTrue();

        packageA.Claim("worker-a", now, TimeSpan.FromMinutes(10));
        packageB.Claim("worker-b", now, TimeSpan.FromMinutes(10));

        await _db.SaveChangesAsync(); // worker A wins

        var act = async () => await otherDb.SaveChangesAsync(); // worker B must lose
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        var final = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        final.ClaimedByWorkerId.Should().Be("worker-a");
    }

    [Fact]
    public async Task An_expired_claim_can_be_recovered_by_a_different_worker()
    {
        var packageId = await SeedPackageAsync();

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var claimedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        package.Claim("crashed-worker", claimedAt, TimeSpan.FromMinutes(10));
        await _db.SaveChangesAsync();

        // Simulate the crashed worker never releasing — recovery must come purely from lease
        // expiry, not an explicit release.
        await using var otherDb = OpenSecondContext();
        var reloaded = await otherDb.ImportPackages.FirstAsync(p => p.Id == packageId);
        var now = DateTimeOffset.UtcNow;

        reloaded.IsClaimable(now).Should().BeTrue("the lease claimed 30 minutes ago with a 10-minute duration has expired");
        reloaded.Claim("recovery-worker", now, TimeSpan.FromMinutes(10));
        await otherDb.SaveChangesAsync();

        // _db's change tracker still holds its own earlier-loaded instance (EF's identity map
        // returns that same tracked instance for a repeated query rather than re-hitting the DB)
        // — reload it explicitly to observe what otherDb actually committed.
        await _db.Entry(package).ReloadAsync();
        package.ClaimedByWorkerId.Should().Be("recovery-worker");
    }

    [Fact]
    public async Task An_active_claim_cannot_be_stolen_by_a_second_worker()
    {
        var packageId = await SeedPackageAsync();

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-a", now, TimeSpan.FromMinutes(10));
        await _db.SaveChangesAsync();

        await using var otherDb = OpenSecondContext();
        var reloaded = await otherDb.ImportPackages.FirstAsync(p => p.Id == packageId);

        reloaded.IsClaimable(now.AddMinutes(2)).Should().BeFalse("the lease is still live");
        var act = () => reloaded.Claim("worker-b", now.AddMinutes(2), TimeSpan.FromMinutes(10));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ReleaseClaim_makes_the_package_immediately_claimable_by_another_worker()
    {
        var packageId = await SeedPackageAsync();

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-a", now, TimeSpan.FromMinutes(10));
        await _db.SaveChangesAsync();

        package.ReleaseClaim();
        await _db.SaveChangesAsync();

        await using var otherDb = OpenSecondContext();
        var reloaded = await otherDb.ImportPackages.FirstAsync(p => p.Id == packageId);
        reloaded.IsClaimable(now).Should().BeTrue();

        reloaded.Claim("worker-b", now, TimeSpan.FromMinutes(10));
        await otherDb.SaveChangesAsync();

        await _db.Entry(package).ReloadAsync();
        package.ClaimedByWorkerId.Should().Be("worker-b");
    }
}
