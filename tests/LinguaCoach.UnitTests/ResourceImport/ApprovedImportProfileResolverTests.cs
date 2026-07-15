using System.Text.Json;
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
/// Phase 4.3 (2026-07-16) — direct unit coverage of <see cref="ApprovedImportProfileResolver"/>,
/// the single place ProfileJson is deserialized/validated for execution. Deliberately constructs
/// <see cref="ImportPackage"/>/<see cref="ImportProfile"/> rows directly (bypassing plan generation)
/// so each scenario isolates exactly one failure mode.
/// </summary>
public sealed class ApprovedImportProfileResolverTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ApprovedImportProfileResolver _resolver;

    public ApprovedImportProfileResolverTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _resolver = new ApprovedImportProfileResolver(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<Guid> SeedSourceAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();
        return source.Id;
    }

    private static ImportPackageManifest SingleRootFileManifest(string relativePath)
    {
        var extension = System.IO.Path.GetExtension(relativePath);
        return new(
            IsAccepted: true, RejectionReason: null, CompressedSizeBytes: 10, ExpandedSizeBytes: 10, EntryCount: 1,
            Entries: new[]
            {
                new ImportPackageManifestEntry(relativePath, relativePath, extension, 10, 10, "application/octet-stream", "abc123", false, null),
            },
            FolderGroups: new[] { new ImportPackageFolderGroup(string.Empty, 1, new[] { extension }) },
            DistinctExtensions: new[] { extension },
            DuplicateChecksumEntries: Array.Empty<ImportPackageManifestEntry>(),
            UnsupportedEntries: Array.Empty<ImportPackageManifestEntry>(),
            SuspiciousEntries: Array.Empty<ImportPackageManifestEntry>());
    }

    private async Task<ImportPackage> SeedPackageWithManifestAsync(Guid sourceId, string relativePath = "data.csv")
    {
        var package = new ImportPackage(sourceId, "test.csv", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync();

        package.SetManifest(JsonSerializer.Serialize(SingleRootFileManifest(relativePath)), 1);
        await _db.SaveChangesAsync();
        return package;
    }

    private async Task<ImportProfile> SeedApprovedPlanAsync(
        ImportPackage package, IReadOnlyList<ImportExecutionGroupInstruction> instructions, bool approve = true)
    {
        var plan = new ImportProfile(
            package.Id, 1, JsonSerializer.Serialize(instructions), Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow,
            planEstimateJson: "{}");
        plan.SubmitForApproval();
        if (approve)
        {
            plan.Approve(null, DateTimeOffset.UtcNow, 100m);
            package.ApproveProfile(plan.Id);
        }
        _db.ImportProfiles.Add(plan);
        await _db.SaveChangesAsync();
        return plan;
    }

    private static readonly ImportExecutionGroupInstruction[] SimpleRootInstruction =
    {
        new("(root)", true, ResourceCandidateType.VocabularyEntry,
            new Dictionary<string, string> { ["word"] = "word" }, Array.Empty<string>()),
    };

    [Fact]
    public async Task Resolves_by_exact_ApprovedImportProfileId_not_the_latest_version()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);
        var v1 = await SeedApprovedPlanAsync(package, SimpleRootInstruction);

        // A newer, unapproved v2 for the same package must never be picked up.
        var v2 = new ImportProfile(
            package.Id, 2, JsonSerializer.Serialize(SimpleRootInstruction), Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow, changeReason: "later draft");
        _db.ImportProfiles.Add(v2);
        await _db.SaveChangesAsync();

        var resolved = await _resolver.ResolveAsync(package.Id);

        resolved.ImportProfileId.Should().Be(v1.Id);
        resolved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Package_with_no_approved_profile_cannot_be_resolved()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);

        var act = async () => await _resolver.ResolveAsync(package.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Draft_unapproved_profile_cannot_be_resolved()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);
        var plan = await SeedApprovedPlanAsync(package, SimpleRootInstruction, approve: false);
        // Force the package to reference a plan that never actually got approved (simulates a data
        // inconsistency the resolver must still reject deterministically) — ApproveProfile only
        // sets the FK/status, it does not itself require the plan to already be Approved.
        package.ApproveProfile(plan.Id);
        await _db.SaveChangesAsync();

        var act = async () => await _resolver.ResolveAsync(package.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Profile_belonging_to_another_package_cannot_be_resolved()
    {
        var sourceId = await SeedSourceAsync();
        var packageA = await SeedPackageWithManifestAsync(sourceId, "a.csv");
        var packageB = await SeedPackageWithManifestAsync(sourceId, "b.csv");
        var planForB = await SeedApprovedPlanAsync(packageB, SimpleRootInstruction);

        // Simulate a corrupted/foreign reference: package A "approved" a plan that actually
        // belongs to package B.
        packageA.ApproveProfile(planForB.Id);
        await _db.SaveChangesAsync();

        var act = async () => await _resolver.ResolveAsync(packageA.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Malformed_ProfileJson_fails_deterministically()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);
        var plan = new ImportProfile(
            package.Id, 1, "{not valid json", Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow, planEstimateJson: "{}");
        plan.SubmitForApproval();
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        package.ApproveProfile(plan.Id);
        _db.ImportProfiles.Add(plan);
        await _db.SaveChangesAsync();

        var act = async () => await _resolver.ResolveAsync(package.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Missing_instruction_for_a_manifest_group_fails()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId, "folder-a/data.csv");
        // The manifest has a file under "folder-a", but the approved plan only covers "(root)".
        await SeedApprovedPlanAsync(package, SimpleRootInstruction);

        var act = async () => await _resolver.ResolveAsync(package.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Unrecognized_field_mapping_target_fails()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);
        var badInstructions = new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, null,
                new Dictionary<string, string> { ["word"] = "not-a-real-field" }, Array.Empty<string>()),
        };
        await SeedApprovedPlanAsync(package, badInstructions);

        var act = async () => await _resolver.ResolveAsync(package.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Audio_group_routed_to_a_non_listening_type_is_an_unsupported_route()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId, "audio.mp3");
        var instructions = new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string>(), Array.Empty<string>()),
        };
        await SeedApprovedPlanAsync(package, instructions);

        var act = async () => await _resolver.ResolveAsync(package.Id);

        await act.Should().ThrowAsync<ApprovedImportProfileResolutionException>();
    }

    [Fact]
    public async Task Included_and_ResourceType_and_FieldMappings_round_trip_correctly()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);
        var instructions = new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.ReadingPassage,
                new Dictionary<string, string> { ["colA"] = "title", ["colB"] = "text" }, Array.Empty<string>()),
        };
        await SeedApprovedPlanAsync(package, instructions);

        var resolved = await _resolver.ResolveAsync(package.Id);
        var instruction = resolved.ResolveForRelativePath("data.csv");

        instruction.Should().NotBeNull();
        instruction!.Included.Should().BeTrue();
        instruction.ResourceType.Should().Be(ResourceCandidateType.ReadingPassage);
        instruction.FieldMappings.Should().ContainKey("colA").WhoseValue.Should().Be("title");
        instruction.FieldMappings.Should().ContainKey("colB").WhoseValue.Should().Be("text");
    }

    [Fact]
    public async Task Excluded_group_instruction_is_preserved_through_resolution()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageWithManifestAsync(sourceId);
        var instructions = new[]
        {
            new ImportExecutionGroupInstruction("(root)", false, null, new Dictionary<string, string>(), Array.Empty<string>()),
        };
        await SeedApprovedPlanAsync(package, instructions);

        var resolved = await _resolver.ResolveAsync(package.Id);
        var instruction = resolved.ResolveForRelativePath("data.csv");

        instruction!.Included.Should().BeFalse();
    }
}
