using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.4 (Workstream A) — direct unit coverage of <see cref="ImportPlanDraftService"/>: draft
/// editing, optimistic concurrency, and the revision lifecycle. Deliberately constructs
/// <see cref="ImportPackage"/>/<see cref="ImportProfile"/> rows directly (bypassing plan
/// generation) so each scenario isolates exactly one behaviour.
/// </summary>
public sealed class ImportPlanDraftServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ImportPlanDraftService _draftService;

    public ImportPlanDraftServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var pricingResolver = new AiPricingResolver(_db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        // Phase 4.4E — no ImportAsset rows exist for these tests' packages, so the estimate
        // always falls back to the labeled per-file assumption; the probe is never actually
        // invoked, but a real resolver must still be constructible.
        var audioDurationResolver = new ImportAssetAudioDurationResolver(
            new LinguaCoach.Infrastructure.Storage.FakeFileStorageService(), new NeverCalledAudioDurationProbe());
        var estimateService = new ImportPlanEstimateService(
            pricingResolver, _db, audioDurationResolver, Options.Create(new LinguaCoach.Infrastructure.ResourceImport.ImportCostEstimationOptions()));
        _draftService = new ImportPlanDraftService(_db, estimateService);
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

    private static ImportPackageManifest SingleRootFileManifest(string relativePath, string extension)
    {
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

    private async Task<ImportPackage> SeedPackageAsync(Guid sourceId, string relativePath = "data.csv", string extension = ".csv")
    {
        var package = new ImportPackage(sourceId, "test.csv", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync();
        package.SetManifest(JsonSerializer.Serialize(SingleRootFileManifest(relativePath, extension)), 1);
        await _db.SaveChangesAsync();
        return package;
    }

    private static readonly ImportExecutionGroupInstruction[] SimpleRootInstruction =
    {
        new("(root)", true, ResourceCandidateType.VocabularyEntry,
            new Dictionary<string, string> { ["word"] = "word" }, Array.Empty<string>()),
    };

    private async Task<ImportProfile> SeedDraftPlanAsync(ImportPackage package, bool submitForApproval = false)
    {
        var plan = new ImportProfile(
            package.Id, 1, JsonSerializer.Serialize(SimpleRootInstruction), Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow, planEstimateJson: "{}");
        if (submitForApproval) plan.SubmitForApproval();
        _db.ImportProfiles.Add(plan);
        await _db.SaveChangesAsync();
        return plan;
    }

    [Fact]
    public async Task Draft_plan_can_be_updated()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package);

        var updated = new[]
        {
            new ImportExecutionGroupInstruction("(root)", true, ResourceCandidateType.ReadingPassage,
                new Dictionary<string, string> { ["word"] = "title" }, Array.Empty<string>()),
        };

        var result = await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, plan.ConcurrencyStamp, updated));

        result.GroupInstructions.Should().ContainSingle(i => i.ResourceType == ResourceCandidateType.ReadingPassage);
    }

    [Fact]
    public async Task Approved_plan_cannot_be_edited()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package, submitForApproval: true);
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        await _db.SaveChangesAsync();

        var act = async () => await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, plan.ConcurrencyStamp, SimpleRootInstruction));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Plan_belonging_to_another_package_cannot_be_edited()
    {
        var sourceId = await SeedSourceAsync();
        var packageA = await SeedPackageAsync(sourceId, "a.csv");
        var packageB = await SeedPackageAsync(sourceId, "b.csv");
        var planForB = await SeedDraftPlanAsync(packageB);

        var act = async () => await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            packageA.Id, planForB.Id, planForB.ConcurrencyStamp, SimpleRootInstruction));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Stale_plan_update_returns_conflict()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package);
        var staleStamp = plan.ConcurrencyStamp;

        // First edit succeeds and bumps the stamp.
        await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, staleStamp, SimpleRootInstruction));

        // Second edit using the now-stale stamp must be rejected.
        var act = async () => await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, staleStamp, SimpleRootInstruction));

        await act.Should().ThrowAsync<ImportPlanConcurrencyConflictException>();
    }

    [Fact]
    public async Task Unsupported_resource_type_fails_validation()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId, "audio.mp3", ".mp3");
        var plan = await SeedDraftPlanAsync(package);

        var badInstructions = new[]
        {
            new ImportExecutionGroupInstruction("(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string>(), Array.Empty<string>()),
        };

        var act = async () => await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, plan.ConcurrencyStamp, badInstructions));

        await act.Should().ThrowAsync<ImportPlanValidationFailedException>();
    }

    [Fact]
    public async Task Unknown_mapping_target_fails_validation()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package);

        var badInstructions = new[]
        {
            new ImportExecutionGroupInstruction("(root)", true, null,
                new Dictionary<string, string> { ["word"] = "not-a-real-field" }, Array.Empty<string>()),
        };

        var act = async () => await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, plan.ConcurrencyStamp, badInstructions));

        var thrown = await act.Should().ThrowAsync<ImportPlanValidationFailedException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.GroupKey == "(root)");
    }

    [Fact]
    public async Task Manifest_group_not_represented_fails_validation()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId, "folder-a/data.csv");
        var plan = await SeedDraftPlanAsync(package);

        var incompleteInstructions = new[]
        {
            new ImportExecutionGroupInstruction("(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string>(), Array.Empty<string>()),
        };

        var act = async () => await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, plan.ConcurrencyStamp, incompleteInstructions));

        await act.Should().ThrowAsync<ImportPlanValidationFailedException>();
    }

    [Fact]
    public async Task Excluding_a_group_recalculates_the_estimate_to_zero_candidates()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package);

        var excluded = new[]
        {
            new ImportExecutionGroupInstruction("(root)", false, null, new Dictionary<string, string>(), Array.Empty<string>()),
        };

        var result = await _draftService.UpdateDraftAsync(new UpdateImportPlanDraftCommand(
            package.Id, plan.Id, plan.ConcurrencyStamp, excluded));

        result.Estimate.Volume.ExpectedCandidateCount.Should().Be(0);
        result.Estimate.Cost.ExpectedCost.Should().Be(0);
    }

    [Fact]
    public async Task Revision_creates_a_new_draft_without_altering_the_approved_plan()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package, submitForApproval: true);
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        package.ApproveProfile(plan.Id);
        await _db.SaveChangesAsync();

        var originalProfileJson = plan.ProfileJson;

        var revision = await _draftService.ReviseAsync(new ReviseApprovedImportPlanCommand(
            package.Id, plan.Id, "test revision"));

        revision.PlanId.Should().NotBe(plan.Id);
        revision.Version.Should().Be(2);
        revision.Status.Should().Be(ImportProfileStatus.AwaitingApproval);
        revision.IsEditable.Should().BeTrue();

        var reloadedOriginal = await _db.ImportProfiles.FirstAsync(p => p.Id == plan.Id);
        reloadedOriginal.Status.Should().Be(ImportProfileStatus.Approved);
        reloadedOriginal.ProfileJson.Should().Be(originalProfileJson);

        var reloadedPackage = await _db.ImportPackages.FirstAsync(p => p.Id == package.Id);
        reloadedPackage.ApprovedImportProfileId.Should().Be(plan.Id); // unchanged until the revision is itself approved
    }

    [Fact]
    public async Task Revision_is_rejected_once_package_processing_has_started()
    {
        var sourceId = await SeedSourceAsync();
        var package = await SeedPackageAsync(sourceId);
        var plan = await SeedDraftPlanAsync(package, submitForApproval: true);
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        package.ApproveProfile(plan.Id);
        package.MoveToStatus(ImportPackageStatus.CreatingCandidates);
        await _db.SaveChangesAsync();

        var act = async () => await _draftService.ReviseAsync(new ReviseApprovedImportPlanCommand(
            package.Id, plan.Id, "too late"));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    private sealed class NeverCalledAudioDurationProbe : IAudioDurationProbe
    {
        public Task<AudioDurationProbeResult> ProbeDurationAsync(Stream audioStream, string fileExtension, CancellationToken ct = default) =>
            throw new InvalidOperationException("No ImportAsset rows exist in these tests — the probe should never be invoked.");
    }
}
