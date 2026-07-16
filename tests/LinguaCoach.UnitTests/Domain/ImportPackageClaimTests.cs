using FluentAssertions;
using LinguaCoach.Domain.Entities;
using Xunit;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Phase 4.8 (2026-07-17 security/concurrency/idempotency) — the in-memory half of the
/// claim/lease state machine. The real cross-worker race protection is the database-level
/// concurrency token on <see cref="ImportPackage.ConcurrencyStamp"/> (see
/// <c>ImportPackageProcessingServiceClaimTests</c> for the DB-backed proof); these tests pin the
/// entity-level rules a single caller must already respect.
/// </summary>
public sealed class ImportPackageClaimTests
{
    private static ImportPackage CreatePackage() =>
        new(Guid.NewGuid(), "package.zip", DateTimeOffset.UtcNow);

    [Fact]
    public void New_package_is_claimable_and_unclaimed()
    {
        var package = CreatePackage();
        package.ClaimedByWorkerId.Should().BeNull();
        package.IsClaimable(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void Claim_sets_worker_and_lease_expiry()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;

        package.Claim("worker-1", now, TimeSpan.FromMinutes(10));

        package.ClaimedByWorkerId.Should().Be("worker-1");
        package.ClaimedAtUtc.Should().Be(now);
        package.ClaimExpiresAtUtc.Should().Be(now.AddMinutes(10));
    }

    [Fact]
    public void Claim_regenerates_the_concurrency_stamp()
    {
        var package = CreatePackage();
        var stampBefore = package.ConcurrencyStamp;

        package.Claim("worker-1", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));

        package.ConcurrencyStamp.Should().NotBe(stampBefore);
    }

    [Fact]
    public void An_active_claim_cannot_be_stolen_by_a_different_worker()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-1", now, TimeSpan.FromMinutes(10));

        package.IsClaimable(now.AddMinutes(1)).Should().BeFalse();
        var act = () => package.Claim("worker-2", now.AddMinutes(1), TimeSpan.FromMinutes(10));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_expired_claim_becomes_claimable_by_a_different_worker()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-1", now, TimeSpan.FromMinutes(10));

        var afterExpiry = now.AddMinutes(11);
        package.IsClaimable(afterExpiry).Should().BeTrue();

        package.Claim("worker-2", afterExpiry, TimeSpan.FromMinutes(10));
        package.ClaimedByWorkerId.Should().Be("worker-2");
    }

    [Fact]
    public void RenewClaim_by_a_different_worker_throws()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-1", now, TimeSpan.FromMinutes(10));

        var act = () => package.RenewClaim("worker-2", now, TimeSpan.FromMinutes(10));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RenewClaim_by_the_same_worker_extends_the_lease()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-1", now, TimeSpan.FromMinutes(10));

        var later = now.AddMinutes(5);
        package.RenewClaim("worker-1", later, TimeSpan.FromMinutes(10));

        package.ClaimExpiresAtUtc.Should().Be(later.AddMinutes(10));
    }

    [Fact]
    public void ReleaseClaim_clears_worker_and_expiry_and_makes_the_package_claimable_again()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.Claim("worker-1", now, TimeSpan.FromMinutes(10));

        package.ReleaseClaim();

        package.ClaimedByWorkerId.Should().BeNull();
        package.ClaimedAtUtc.Should().BeNull();
        package.ClaimExpiresAtUtc.Should().BeNull();
        package.IsClaimable(now).Should().BeTrue();
    }
}
